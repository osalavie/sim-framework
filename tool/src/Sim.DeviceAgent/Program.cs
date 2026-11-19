using Sim.DeviceAgent.Configuration;
using Sim.DeviceAgent.Runtime;
using Sim.DeviceAgent.Tray;
using Sim.DeviceAgent.WebSockets;

var configPath = AgentConfigLoader.ResolveConfigPath(args);
AgentConfig config;
try
{
    config = AgentConfigLoader.Load(configPath);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to load agent configuration: {ex.Message}");
    return 1;
}

var validationErrors = AgentConfigValidator.Validate(config);
if (validationErrors.Count > 0)
{
    Console.Error.WriteLine("Agent configuration is invalid:");
    foreach (var error in validationErrors)
    {
        Console.Error.WriteLine($"  - {error}");
    }

    return 1;
}

var configFile = new AgentConfigFile(configPath);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Directory.GetCurrentDirectory()
});

builder.WebHost.UseUrls($"http://{config.Agent.LocalWs.Bind}:{config.Agent.LocalWs.Port}");
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
    options.SingleLine = true;
});

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
builder.Services.AddHostedService<TrayService>();

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Sim.DeviceAgent");

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

if (string.IsNullOrEmpty(config.Agent.LocalWs.Token))
{
    logger.LogInformation(
        "No local token configured; loopback clients connect without authentication.");
}

if (config.Agent.LocalWs.AllowedOrigins.Count == 0)
{
    logger.LogWarning(
        "agent.localWs.allowedOrigins is empty. Browser WebSocket connections will be rejected; non-browser clients may connect without an Origin header.");
}

logger.LogInformation(
    "Device agent {AgentId} listening on ws://{Bind}:{Port}/ws (config: {ConfigPath}).",
    config.Agent.Id,
    config.Agent.LocalWs.Bind,
    config.Agent.LocalWs.Port,
    configFile.Path);

await app.RunAsync();
return 0;
