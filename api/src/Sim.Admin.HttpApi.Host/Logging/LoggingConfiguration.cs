using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Display;

namespace Sim.Admin.HttpApi.Host.Logging;

public static class LoggingConfiguration
{
    public const string OutputTemplate =
        "[{Module}][{Level:u3}][{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] {Message:lj}{NewLine}{Exception}";

    public static string ClassifyModule(string? sourceContext)
    {
        if (string.IsNullOrWhiteSpace(sourceContext)) return "System";
        if (ContainsAny(sourceContext, "ScheduleTask", "Quartz", ".Jobs", ".Worker"))
            return "ScheduleTask";
        if (ContainsAny(sourceContext, "Sim.System", "RBAC")) return "System";
        if (ContainsAny(sourceContext, "Sim.Framework", "Sim.SqlSugar", "Database/", "FileManagement"))
            return "Framework";
        if (ContainsAny(sourceContext, "Sim.Admin")) return "Admin";
        return "System";
    }

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Applies the unified <c>DataBase:EnableSqlLog</c> switch to Quartz ADO.NET SQL logging.
    /// Quartz 3.x routes its own logs through a LibLog-style bridge that auto-detects Serilog
    /// and writes directly to the global <c>Log.Logger</c>, bypassing Microsoft.Extensions.Logging
    /// filters, so the gate must live at the Serilog level. The SQL statements are logged at Debug
    /// under the <c>Quartz.SQL</c> category; the ADO job store chatter (lock acquisition, check-ins,
    /// misfire scans) lives under <c>Quartz.Impl.AdoJobStore</c>.
    /// </summary>
    public static LoggerConfiguration ConfigureQuartzSqlLogging(
        IConfiguration configuration,
        LoggerConfiguration loggerConfiguration)
    {
        var quartzSqlLevel = configuration.GetValue<bool>("DataBase:EnableSqlLog")
            ? LogEventLevel.Debug
            : LogEventLevel.Warning;

        return loggerConfiguration
            .MinimumLevel.Override("Quartz.SQL", quartzSqlLevel)
            .MinimumLevel.Override("Quartz.Impl.AdoJobStore", quartzSqlLevel);
    }

    /// <summary>
    /// 从环境配置读取最低日志级别与类别覆盖，便于按环境调整而无需改代码。
    /// <c>Logging:MinimumLevel:Default</c>（或简写为 <c>Logging:MinimumLevel</c>）设置全局最低级别，
    /// <c>Logging:MinimumLevel:Override:&lt;类别&gt;</c> 覆盖单个 <c>SourceContext</c> 前缀；
    /// 取值不合法时回退到上一层级别（全局级别回退到 Information，类别覆盖回退到全局级别）。
    /// </summary>
    public static LoggerConfiguration ConfigureMinimumLevel(
        IConfiguration configuration,
        LoggerConfiguration loggerConfiguration)
    {
        var defaultLevel = ParseLevel(
            configuration["Logging:MinimumLevel:Default"] ?? configuration["Logging:MinimumLevel"],
            LogEventLevel.Information);

        loggerConfiguration.MinimumLevel.Is(defaultLevel);

        foreach (var overrideEntry in configuration.GetSection("Logging:MinimumLevel:Override").GetChildren())
        {
            if (string.IsNullOrWhiteSpace(overrideEntry.Key))
            {
                continue;
            }

            loggerConfiguration.MinimumLevel.Override(
                overrideEntry.Key,
                ParseLevel(overrideEntry.Value, defaultLevel));
        }

        return loggerConfiguration;
    }

    private static LogEventLevel ParseLevel(string? value, LogEventLevel fallback) =>
        Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var level) ? level : fallback;

    public static bool ConfigureSinks(
        IConfiguration configuration,
        IHostEnvironment environment,
        LoggerConfiguration loggerConfiguration)
    {
        var basePath = Path.GetDirectoryName(AppContext.BaseDirectory)
                       ?? environment.ContentRootPath;

        // --- Enrichers ---
        loggerConfiguration
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentUserName()
            .Enrich.WithProperty("SourceContext", "System");

        // --- Formatter selection ---
        var useJson = configuration.GetValue("Logging:Format", "text")
            ?.Equals("json", StringComparison.OrdinalIgnoreCase) == true;

        var sinkConfigured = false;

        // Console sink
        if (configuration.GetValue("Logging:Console:Enabled", true))
        {
            ITextFormatter consoleFormatter = useJson
                ? new CompactJsonFormatter()
                : new SingleLineLogFormatter();
            loggerConfiguration.WriteTo.Async(sink =>
                sink.Console(consoleFormatter));
            sinkConfigured = true;
        }

        // File sink with optional retention
        var fileEnabled = configuration.GetValue<bool?>("Logging:File:Enabled")
            ?? environment.IsDevelopment();
        if (fileEnabled)
        {
            var configuredPath = configuration["Logging:File:Path"];
            var filePath = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(basePath, "Logs/log.txt")
                : Path.IsPathFullyQualified(configuredPath)
                    ? configuredPath
                    : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));

            ITextFormatter fileFormatter = useJson
                ? new CompactJsonFormatter()
                : new SingleLineLogFormatter();

            var retainedFileCountLimit = configuration.GetValue<int?>("Logging:File:RetainedFileCountLimit");
            var fileSizeLimitBytes = ParseFileSizeLimitBytes(configuration["Logging:File:FileSizeLimitBytes"]);

            loggerConfiguration.WriteTo.Async(sink =>
            {
                var fileSinkConfig = sink.File(
                    fileFormatter,
                    filePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: retainedFileCountLimit,
                    fileSizeLimitBytes: fileSizeLimitBytes);

                // Note: retainedFileCountLimit and fileSizeLimitBytes are set above as params.
                // When both are null, Serilog defaults apply (unlimited).
            });
            sinkConfigured = true;
        }

        return sinkConfigured;
    }

    private static long? ParseFileSizeLimitBytes(string? configValue)
    {
        if (string.IsNullOrWhiteSpace(configValue))
            return null;

        // Support human-readable suffixes: 10MB, 100KB, 1GB
        if (long.TryParse(configValue, out var direct))
            return direct;

        var upper = configValue.Trim().ToUpperInvariant();
        var multiplier = 1L;
        if (upper.EndsWith("KB"))
            multiplier = 1024;
        else if (upper.EndsWith("MB"))
            multiplier = 1024 * 1024;
        else if (upper.EndsWith("GB"))
            multiplier = 1024 * 1024 * 1024;

        if (multiplier > 1)
        {
            var numPart = upper[..^2].Trim();
            if (long.TryParse(numPart, out var num))
                return num * multiplier;
        }

        return null;
    }

    private sealed class SingleLineLogFormatter : ITextFormatter
    {
        public void Format(LogEvent logEvent, TextWriter output)
        {
            var sourceContext = logEvent.Properties.TryGetValue("SourceContext", out var sourceValue) &&
                                sourceValue is ScalarValue scalar
                ? scalar.Value?.ToString()
                : null;
            var exception = logEvent.Exception == null ? string.Empty : $" {logEvent.Exception}";

            // Render the message with the "lj" literal format so string-typed properties
            // (e.g. ASP.NET Core request logs' Protocol/Method/Host/Path) are not wrapped
            // in double quotes by Serilog's default string quoting.
            using var messageWriter = new StringWriter(CultureInfo.InvariantCulture);
            new MessageTemplateTextFormatter("{Message:lj}", CultureInfo.InvariantCulture)
                .Format(logEvent, messageWriter);

            var rendered = $"[{ClassifyModule(sourceContext)}]" +
                           $"[{ToLevel(logEvent.Level)}]" +
                           $"[{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] " +
                           messageWriter.ToString() + exception;
            rendered = rendered
                .TrimEnd('\r', '\n')
                .Replace("\r\n", "\\n", StringComparison.Ordinal)
                .Replace("\r", "\\n", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
            output.WriteLine(rendered);
        }

        private static string ToLevel(LogEventLevel level) => level switch
        {
            LogEventLevel.Verbose => "VRB",
            LogEventLevel.Debug => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning => "WRN",
            LogEventLevel.Error => "ERR",
            LogEventLevel.Fatal => "FTL",
            _ => "UNK"
        };
    }
}
