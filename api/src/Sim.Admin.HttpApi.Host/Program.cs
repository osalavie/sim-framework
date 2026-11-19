using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Sim.Admin.HttpApi.Host.Data;
using Sim.Admin.HttpApi.Host.Logging;
using Serilog;
using Sim.SqlSugar.Core.Migrations;
using Sim.Framework.Domain.Security;

namespace Sim.Admin.HttpApi.Host;

public class Program
{
    private static ILogger HostLog => Log.ForContext<Program>();

    public static async Task<int> Main(string[] args)
    {
        // Failures before Serilog is fully configured must still reach Docker
        // stdout. Keep a minimal console logger until CreateApplicationAsync
        // replaces it with the environment-specific sink configuration.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            var app = await CreateApplicationAsync(args);

            if (args.Contains("--migrate"))
            {
                HostLog.Information("开始执行受控数据库结构迁移...");
                await MigrateDatabaseAsync(app);
                HostLog.Information("数据库结构迁移完成");
                return 0;
            }

            // 检查是否需要执行数据种子
            if (args.Contains("--seed") || args.Contains("/seed"))
            {
                HostLog.Information("执行数据种子检查（通过命令行参数）...");
                await SeedDataAsync(app);
                HostLog.Information("数据种子检查完成，应用将继续启动");
            }

            await app.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            if (ex is HostAbortedException)
            {
                throw;
            }

            HostLog.Fatal(ex, "Host terminated unexpectedly!");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static async Task<WebApplication> CreateApplicationAsync(
        string[] args,
        Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 后台服务的瞬时故障（如数据库连接耗尽）不应导致整个 API 进程退出。
        builder.Services.Configure<HostOptions>(options =>
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

        EnsureEnvironmentSettingsFile(builder);

        var secretsDirectory = ReadSecretsDirectory();

        if (!string.IsNullOrWhiteSpace(secretsDirectory))
        {
            if (!Path.IsPathFullyQualified(secretsDirectory))
            {
                throw new InvalidOperationException(
                    "SIM_SECRETS_DIRECTORY 必须是绝对路径。");
            }

            builder.Configuration.AddKeyPerFile(
                directoryPath: secretsDirectory,
                optional: false,
                reloadOnChange: false);
        }

        // App_Data is normally a writable deployment volume. This runtime switch reloads without restarting the host.
        var maintenanceConfigurationPath = Path.Combine(
            builder.Environment.ContentRootPath,
            "App_Data",
            "maintenance.json");
        builder.Configuration.AddJsonFile(
            maintenanceConfigurationPath,
            optional: true,
            reloadOnChange: true);

        configureBuilder?.Invoke(builder);

        var bootstrapConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information();
        LoggingConfiguration.ConfigureSinks(
            builder.Configuration,
            builder.Environment,
            bootstrapConfiguration);
        Log.Logger = bootstrapConfiguration.CreateBootstrapLogger();

        // 仅记录不包含账号、命令行参数和本地路径的启动信息。
        HostLog.Information("======== 运行环境信息 ========");
        HostLog.Information("Framework: {Framework}", RuntimeInformation.FrameworkDescription);
        HostLog.Information("OS: {OS}", RuntimeInformation.OSDescription);
        HostLog.Information("Architecture: {Architecture}", RuntimeInformation.OSArchitecture);
        var assembly = Assembly.GetEntryAssembly()!;
        HostLog.Information("App: {App}", assembly.GetName().Name);
        HostLog.Information("Environment: {Environment}", builder.Environment.EnvironmentName);
        HostLog.Information("Version: {Version}", assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        HostLog.Information("=============================");

        builder.Host
            .AddAppSettingsSecretsJson()
            .UseSerilog((context, _, loggerConfiguration) =>
            {
                // 最低级别与类别覆盖由环境配置提供（Logging:MinimumLevel），可按环境调整。
                LoggingConfiguration.ConfigureMinimumLevel(
                    context.Configuration,
                    loggerConfiguration);

                // Quartz 3.x logs SQL through its own LibLog bridge directly to Serilog,
                // bypassing Microsoft.Extensions.Logging filters. Gate the Quartz SQL
                // categories here with the unified DataBase:EnableSqlLog switch.
                LoggingConfiguration.ConfigureQuartzSqlLogging(
                    context.Configuration,
                    loggerConfiguration);

                LoggingConfiguration.ConfigureSinks(
                    context.Configuration,
                    context.HostingEnvironment,
                    loggerConfiguration);
            });

        await builder.Services.AddApplicationAsync<AdminHttpApiHostModule>();

        var app = builder.Build();

        // Resolve the effective signing configuration before the host accepts requests.
        // The template deliberately contains no usable key; failing here avoids a login-time 500.
        if (app.Services.GetRequiredService<JwtSigningKeyStore>().GetActiveKey() is null)
        {
            throw new InvalidOperationException(
                "No JWT signing key is configured. Configure JwtSettings:IssuerSigningKey " +
                "(at least 32 UTF-8 bytes) or an active JwtSettings:SigningKeys key.");
        }

        // 替代 Volo.Abp.AspNetCore 的 InitializeApplicationAsync：
        // 手动填充模块初始化所需的对象访问器，再执行 ABP 模块生命周期初始化
        app.Services.GetRequiredService<ObjectAccessor<IApplicationBuilder>>().Value = app;
        app.Services.GetRequiredService<ObjectAccessor<WebApplication>>().Value = app;
        app.Services.GetRequiredService<ObjectAccessor<IHost>>().Value = app;
        app.Services.GetRequiredService<ObjectAccessor<IEndpointRouteBuilder>>().Value = app;

        var abpApplication = app.Services.GetRequiredService<IAbpApplicationWithExternalServiceProvider>();
        await abpApplication.InitializeAsync(app.Services);

        HostLog.Information("Application initialized successfully.");
        return app;
    }

    /// <summary>
    /// 环境配置文件不入库，仓库只保留 appsettings.Example.json 模板。
    /// 启动时若当前环境对应的 appsettings.{Environment}.json 缺失，则基于模板生成本地文件，
    /// 并按 ASP.NET Core 默认顺序（先于环境变量与命令行）并入配置，避免模板中的空值覆盖外部注入的配置。
    /// </summary>
    private static void EnsureEnvironmentSettingsFile(WebApplicationBuilder builder)
    {
        var contentRoot = builder.Environment.ContentRootPath;
        var fileName = $"appsettings.{builder.Environment.EnvironmentName}.json";
        var settingsFile = Path.Combine(contentRoot, fileName);

        if (File.Exists(settingsFile))
        {
            return;
        }

        var exampleFile = Path.Combine(contentRoot, "appsettings.Example.json");
        if (!File.Exists(exampleFile))
        {
            throw new InvalidOperationException(
                $"缺少环境配置模板文件：{exampleFile}");
        }

        try
        {
            File.Copy(exampleFile, settingsFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"环境配置文件 {fileName} 不存在且无法根据模板生成，请确认 {contentRoot} 可写或手动创建该文件。",
                ex);
        }

        var insertIndex = builder.Configuration.Sources.Count;
        for (var i = builder.Configuration.Sources.Count - 1; i >= 0; i--)
        {
            if (builder.Configuration.Sources[i] is FileConfigurationSource fileSource
                && fileSource.Path is { Length: > 0 } path
                && path.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase)
                && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                insertIndex = i + 1;
                break;
            }
        }

        builder.Configuration.Sources.Insert(
            insertIndex,
            new JsonConfigurationSource
            {
                FileProvider = new PhysicalFileProvider(contentRoot),
                Path = fileName,
                Optional = false,
                ReloadOnChange = true,
            });

        HostLog.Information(
            "环境配置文件 {SettingsFile} 不存在，已根据 appsettings.Example.json 生成，请按本机环境补齐连接串等配置",
            fileName);
    }

    private static async Task SeedDataAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        try
        {
            await scope.ServiceProvider
                .GetRequiredService<DataSeedRunner>()
                .RunIfInitializedAsync();
        }
        catch (Exception ex)
        {
            HostLog.Error(ex, "数据种子执行失败: {Message}", ex.Message);
            throw;
        }
    }

    private static async Task MigrateDatabaseAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseSchemaMigrator>();
        await migrator.MigrateAsync();
    }

    private static string? ReadSecretsDirectory()
    {
        var value = Environment.GetEnvironmentVariable("SIM_SECRETS_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        // User/Machine targets read the Windows registry and return null on other platforms.
        if (OperatingSystem.IsWindows())
        {
            value = Environment.GetEnvironmentVariable(
                "SIM_SECRETS_DIRECTORY",
                EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            value = Environment.GetEnvironmentVariable(
                "SIM_SECRETS_DIRECTORY",
                EnvironmentVariableTarget.Machine);
        }

        return value;
    }
}
