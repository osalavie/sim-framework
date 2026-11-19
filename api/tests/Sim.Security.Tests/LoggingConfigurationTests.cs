using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Sim.Admin.HttpApi.Host.Logging;

namespace Sim.Security.Tests;

public sealed class LoggingConfigurationTests
{
    [Theory]
    [InlineData("Quartz.Core.QuartzScheduler", "ScheduleTask")]
    [InlineData("Sim.System.Application.User.UserService", "System")]
    [InlineData("Sim.System.Application.FileManagement.FileStorage", "System")]
    [InlineData("Sim.Admin.Domain.Data.SystemDataSeeder", "Admin")]
    [InlineData("Microsoft.AspNetCore.Hosting", "System")]
    public void Source_context_should_be_grouped_into_coarse_modules(string source, string expected)
    {
        Assert.Equal(expected, LoggingConfiguration.ClassifyModule(source));
    }

    [Fact]
    public void File_sink_should_use_unified_format_and_sink_switches()
    {
        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "sim-logging-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var filePath = Path.Combine(workingDirectory, "unified-.log");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Logging:Console:Enabled"] = "false",
                    ["Logging:File:Enabled"] = "true",
                    ["Logging:File:Path"] = filePath
                })
                .Build();
            var environment = new TestHostEnvironment(workingDirectory);
            var loggerConfiguration = new LoggerConfiguration().MinimumLevel.Debug();

            var configured = LoggingConfiguration.ConfigureSinks(
                configuration,
                environment,
                loggerConfiguration);
            using (var logger = loggerConfiguration.CreateLogger())
            {
                logger
                    .ForContext("SourceContext", "Database/SqlSugar")
                    .Information("执行 SQL {Count}\n下一行", 3);
            }

            Assert.True(configured);
            var logFile = Assert.Single(Directory.GetFiles(workingDirectory, "unified-*.log"));
            var lines = File.ReadAllLines(logFile);
            var log = Assert.Single(lines);
            Assert.Matches(
                @"^\[Framework\]\[INF\]\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}\] 执行 SQL 3",
                log);
            Assert.Contains("\\n下一行", log, StringComparison.Ordinal);

            var disabledConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Logging:Console:Enabled"] = "false",
                    ["Logging:File:Enabled"] = "false"
                })
                .Build();
            Assert.False(LoggingConfiguration.ConfigureSinks(
                disabledConfiguration,
                environment,
                new LoggerConfiguration()));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void String_properties_should_render_without_serilog_quotes()
    {
        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "sim-logging-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var filePath = Path.Combine(workingDirectory, "quotes-.log");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Logging:Console:Enabled"] = "false",
                    ["Logging:File:Enabled"] = "true",
                    ["Logging:File:Path"] = filePath
                })
                .Build();
            var environment = new TestHostEnvironment(workingDirectory);
            var loggerConfiguration = new LoggerConfiguration().MinimumLevel.Information();

            LoggingConfiguration.ConfigureSinks(configuration, environment, loggerConfiguration);
            using (var logger = loggerConfiguration.CreateLogger())
            {
                logger
                    .ForContext("SourceContext", "Sim.System.Application.User.UserService")
                    .Information("User {Name} logged in with role {Role}", "admin", "Administrator");
            }

            var logFile = Assert.Single(Directory.GetFiles(workingDirectory, "quotes-*.log"));
            var log = Assert.Single(File.ReadAllLines(logFile));
            Assert.Matches(
                @"^\[System\]\[INF\]\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}\] User admin logged in with role Administrator$",
                log);
            Assert.DoesNotContain("\"admin\"", log, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void Application_code_should_not_register_independent_console_loggers()
    {
        var repositoryRoot = FindRepositoryRoot();
        var violations = Directory
            .EnumerateFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return source.Contains(".AddConsole(", StringComparison.Ordinal) ||
                       source.Contains("Console.Write", StringComparison.Ordinal) ||
                       source.Contains("Debug.Write", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"检测到绕过统一 Serilog 管道的控制台输出:{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Sim.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法定位包含 Sim.slnx 的仓库根目录。");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Quartz_sql_debug_events_should_follow_enable_sql_log_switch(bool enableSqlLog)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataBase:EnableSqlLog"] = enableSqlLog.ToString()
            })
            .Build();
        var sink = new CollectingSink();
        var loggerConfiguration = LoggingConfiguration.ConfigureQuartzSqlLogging(
            configuration,
            new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(sink));

        using var logger = loggerConfiguration.CreateLogger();
        logger
            .ForContext("SourceContext", "Quartz.SQL")
            .Debug("Prepared SQL: {Sql}", "SELECT 1");
        logger
            .ForContext("SourceContext", "Quartz.Impl.AdoJobStore.StdAdoDelegate")
            .Debug("Prepared SQL: {Sql}", "SELECT 2");

        Assert.Equal(enableSqlLog ? 2 : 0, sink.Events.Count);
    }

    [Fact]
    public void Disabling_sql_log_should_not_suppress_warnings_or_other_categories()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataBase:EnableSqlLog"] = "false"
            })
            .Build();
        var sink = new CollectingSink();
        var loggerConfiguration = LoggingConfiguration.ConfigureQuartzSqlLogging(
            configuration,
            new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(sink));

        using var logger = loggerConfiguration.CreateLogger();
        logger
            .ForContext("SourceContext", "Quartz.SQL")
            .Warning("Quartz warning must remain visible");
        logger
            .ForContext("SourceContext", "Sim.System.Application.User.UserService")
            .Debug("Business debug must remain visible");

        Assert.Equal(2, sink.Events.Count);
    }

    [Fact]
    public void Enabling_sql_log_should_override_release_broad_quartz_warning_level()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataBase:EnableSqlLog"] = "true"
            })
            .Build();
        var sink = new CollectingSink();
        var loggerConfiguration = LoggingConfiguration.ConfigureQuartzSqlLogging(
            configuration,
            new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Quartz", LogEventLevel.Warning)
                .WriteTo.Sink(sink));

        using var logger = loggerConfiguration.CreateLogger();
        logger
            .ForContext("SourceContext", "Quartz.SQL")
            .Debug("Prepared SQL: {Sql}", "SELECT 1");
        logger
            .ForContext("SourceContext", "Quartz.Core.QuartzScheduler")
            .Information("Scheduler started");

        // The specific Quartz.SQL override re-enables SQL Debug events even though the
        // broad Quartz override is at Warning (longest-prefix match wins in Serilog).
        Assert.Single(sink.Events);
    }

    [Fact]
    public void Minimum_level_should_come_from_configuration_with_category_overrides()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:MinimumLevel:Default"] = "Information",
                ["Logging:MinimumLevel:Override:Noisy.Category"] = "Debug",
                ["Logging:MinimumLevel:Override:Chatty.Category"] = "Warning"
            })
            .Build();
        var sink = new CollectingSink();

        using var logger = LoggingConfiguration
            .ConfigureMinimumLevel(configuration, new LoggerConfiguration().WriteTo.Sink(sink))
            .CreateLogger();

        logger.ForContext("SourceContext", "Noisy.Category").Debug("kept by override");
        logger.ForContext("SourceContext", "Chatty.Category").Information("dropped by override");
        logger.ForContext("SourceContext", "Chatty.Category").Warning("kept by override");
        logger.ForContext("SourceContext", "Quiet.Category").Debug("dropped by default");
        logger.ForContext("SourceContext", "Quiet.Category").Information("kept by default");

        Assert.Equal(3, sink.Events.Count);
    }

    [Fact]
    public void Invalid_minimum_level_should_fall_back_to_information()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:MinimumLevel"] = "NotALevel"
            })
            .Build();
        var sink = new CollectingSink();

        using var logger = LoggingConfiguration
            .ConfigureMinimumLevel(configuration, new LoggerConfiguration().WriteTo.Sink(sink))
            .CreateLogger();

        logger.Debug("dropped by fallback");
        logger.Information("kept by fallback");

        Assert.Single(sink.Events);
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = nameof(LoggingConfigurationTests);
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
