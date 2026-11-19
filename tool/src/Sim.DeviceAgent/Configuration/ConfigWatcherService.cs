using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sim.DeviceAgent.Configuration;

public sealed class ConfigWatcherService : BackgroundService
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(400);

    private readonly AgentConfigFile _file;
    private readonly AgentConfigReloader _reloader;
    private readonly ILogger<ConfigWatcherService> _logger;

    public ConfigWatcherService(
        AgentConfigFile file,
        AgentConfigReloader reloader,
        ILogger<ConfigWatcherService> logger)
    {
        _file = file;
        _reloader = reloader;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var directory = Path.GetDirectoryName(_file.Path);
        var fileName = Path.GetFileName(_file.Path);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            _logger.LogWarning(
                "Configuration watcher is disabled because directory '{Directory}' does not exist.",
                directory);
            return;
        }

        using var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite
                           | NotifyFilters.FileName
                           | NotifyFilters.Size
                           | NotifyFilters.CreationTime
        };

        using var signal = new SemaphoreSlim(0, 1);

        void RequestReload()
        {
            try
            {
                if (signal.CurrentCount == 0)
                {
                    signal.Release();
                }
            }
            catch (SemaphoreFullException)
            {
                // A reload is already queued.
            }
        }

        FileSystemEventHandler onFileChanged = (_, _) => RequestReload();
        RenamedEventHandler onFileRenamed = (_, _) => RequestReload();

        watcher.Changed += onFileChanged;
        watcher.Created += onFileChanged;
        watcher.Deleted += onFileChanged;
        watcher.Renamed += onFileRenamed;
        watcher.Error += (_, args) =>
            _logger.LogWarning(args.GetException(), "Configuration watcher reported an error.");
        watcher.EnableRaisingEvents = true;

        _logger.LogInformation("Watching configuration file {Path} for changes.", _file.Path);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await signal.WaitAsync(stoppingToken).ConfigureAwait(false);

                await Task.Delay(Debounce, stoppingToken).ConfigureAwait(false);
                while (signal.Wait(0))
                {
                    // Coalesce editor write events.
                }

                var result = await _reloader.ReloadAsync(stoppingToken).ConfigureAwait(false);
                if (result.Success)
                {
                    _logger.LogInformation("Configuration change applied: {Message}", result.Message);
                }
                else
                {
                    _logger.LogWarning("Configuration change rejected: {Message}", result.Message);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
        finally
        {
            watcher.EnableRaisingEvents = false;
        }
    }
}
