using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sim.DeviceAgent.Configuration;
using Sim.DeviceAgent.Runtime;
using Sim.DeviceAgent.WebSockets;

namespace Sim.DeviceAgent.Hosting;

public sealed record AgentStatusSnapshot(
    string AgentId,
    string DisplayName,
    string LocalWsBind,
    int LocalWsPort,
    string ConfigPath,
    IReadOnlyList<ChannelStatus> Channels,
    IReadOnlyList<SinkStatus> Sinks,
    ConfigReloadStatus Config);

public sealed class AgentRuntimeHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private int _started;
    private int _disposed;

    private AgentRuntimeHost(WebApplication app, AgentConfig config, AgentConfigFile configFile)
    {
        _app = app;
        Config = config;
        ConfigFile = configFile;
    }

    public AgentConfig Config { get; }

    public AgentConfigFile ConfigFile { get; }

    public AgentConfigReloader Reloader => _app.Services.GetRequiredService<AgentConfigReloader>();

    public string LocalWsUrl => $"ws://{Config.Agent.LocalWs.Bind}:{Config.Agent.LocalWs.Port}/ws";

    public static AgentRuntimeHost Create(
        AgentConfig config,
        string configPath,
        Action<ILoggingBuilder>? configureLogging = null)
    {
        var configFile = new AgentConfigFile(configPath);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AgentConfigLoader.ProgramDirectory
        });

        builder.WebHost.UseUrls($"http://{config.Agent.LocalWs.Bind}:{config.Agent.LocalWs.Port}");
        builder.Logging.ClearProviders();
        if (configureLogging is not null)
        {
            configureLogging(builder.Logging);
        }
        else
        {
            builder.Logging.AddSimpleConsole(options =>
            {
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
                options.SingleLine = true;
            });
        }

        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton(configFile);
        builder.Services.AddSingleton<AgentChannelRegistry>();
        builder.Services.AddSingleton<AgentSinkRegistry>();
        builder.Services.AddSingleton<LocalWebSocketHub>();
        builder.Services.AddSingleton<ISequenceStore>(services => new FileSequenceStore(
            config.Agent.StateDirectory,
            services.GetRequiredService<ILogger<FileSequenceStore>>()));
        builder.Services.AddSingleton<DeviceAgentRuntime>();
        builder.Services.AddSingleton<AgentConfigReloader>();
        builder.Services.AddHostedService(services => services.GetRequiredService<DeviceAgentRuntime>());
        builder.Services.AddHostedService<ConfigWatcherService>();

        var app = builder.Build();

        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        });

        app.MapGet("/health", () => Results.Ok(new { status = "ok", agentId = config.Agent.Id }));

        app.MapGet("/api/status", (
            AgentChannelRegistry registry,
            AgentSinkRegistry sinks,
            AgentConfigReloader reloader) =>
        {
            return Results.Ok(new
            {
                agentId = config.Agent.Id,
                displayName = config.Agent.DisplayName,
                localWs = new
                {
                    bind = config.Agent.LocalWs.Bind,
                    port = config.Agent.LocalWs.Port,
                    tokenRequired = !string.IsNullOrEmpty(config.Agent.LocalWs.Token),
                    allowedOrigins = config.Agent.LocalWs.AllowedOrigins
                },
                channels = registry.GetStatuses(),
                sinks = sinks.GetStatuses(),
                config = reloader.GetStatus()
            });
        });

        app.MapPost("/api/config/reload", async (AgentConfigReloader reloader, CancellationToken cancellationToken) =>
        {
            var result = await reloader.ReloadAsync(cancellationToken);
            return result.Success
                ? Results.Ok(result)
                : Results.Json(result, statusCode: StatusCodes.Status400BadRequest);
        });

        app.Map("/ws", async context =>
        {
            var hub = context.RequestServices.GetRequiredService<LocalWebSocketHub>();
            await hub.HandleAsync(context);
        });

        return new AgentRuntimeHost(app, config, configFile);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        var logger = _app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Sim.DeviceAgent");

        if (string.IsNullOrEmpty(Config.Agent.LocalWs.Token))
        {
            logger.LogInformation(
                "No local token configured; loopback clients connect without authentication.");
        }

        if (Config.Agent.LocalWs.AllowedOrigins.Count == 0)
        {
            logger.LogWarning(
                "agent.localWs.allowedOrigins is empty. Browser WebSocket connections will be rejected; non-browser clients may connect without an Origin header.");
        }

        await _app.StartAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Device agent {AgentId} listening on ws://{Bind}:{Port}/ws (config: {ConfigPath}).",
            Config.Agent.Id,
            Config.Agent.LocalWs.Bind,
            Config.Agent.LocalWs.Port,
            ConfigFile.Path);
    }

    public Task StopAsync(CancellationToken cancellationToken) => _app.StopAsync(cancellationToken);

    public AgentStatusSnapshot GetSnapshot()
    {
        var registry = _app.Services.GetRequiredService<AgentChannelRegistry>();
        var sinks = _app.Services.GetRequiredService<AgentSinkRegistry>();
        return new AgentStatusSnapshot(
            Config.Agent.Id,
            Config.Agent.DisplayName,
            Config.Agent.LocalWs.Bind,
            Config.Agent.LocalWs.Port,
            ConfigFile.Path,
            registry.GetStatuses(),
            sinks.GetStatuses(),
            Reloader.GetStatus());
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _app.DisposeAsync().ConfigureAwait(false);
    }
}
