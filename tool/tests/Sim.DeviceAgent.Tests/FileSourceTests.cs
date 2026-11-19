using Microsoft.Extensions.Logging.Abstractions;
using Sim.DeviceAgent.Configuration;
using Sim.DeviceAgent.Sources;
using Xunit;

namespace Sim.DeviceAgent.Tests;

public sealed class FileSourceTests
{
    [Fact]
    public async Task FileSource_WaitsForStableFile_ThenRaisesEventAndArchives()
    {
        var root = Path.Combine(Path.GetTempPath(), "sim-device-agent-tests", Guid.NewGuid().ToString("N"));
        var input = Path.Combine(root, "in");
        var archive = Path.Combine(root, "archive");
        var error = Path.Combine(root, "error");
        Directory.CreateDirectory(input);

        try
        {
            var config = new SourceConfig
            {
                Id = "files",
                Type = "file",
                Directory = input,
                Pattern = "*.csv",
                StableSeconds = 0.2,
                PollIntervalSeconds = 0.1,
                ArchiveDirectory = archive,
                ErrorDirectory = error
            };

            var source = new FileSource(config, NullLogger<FileSource>.Instance);
            var detected = new TaskCompletionSource<FileMetadata>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var runTask = source.RunAsync(async (sourceEvent, _) =>
            {
                if (sourceEvent is SourceFileReady ready)
                {
                    detected.TrySetResult(ready.Metadata);
                }

                await Task.CompletedTask;
                return SourceEventResult.Ok;
            }, cts.Token);

            try
            {
                var filePath = Path.Combine(input, "pull.csv");
                await File.WriteAllTextAsync(filePath, "Sample,ForceN\nA,12.5\nB,8.0\n");

                var metadata = await detected.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal("pull.csv", metadata.Name);
                Assert.False(string.IsNullOrWhiteSpace(metadata.Sha256));

                await WaitUntilAsync(
                    () => Directory.EnumerateFiles(archive, "*.csv", SearchOption.AllDirectories).Any(),
                    TimeSpan.FromSeconds(5));

                Assert.False(File.Exists(filePath));
            }
            finally
            {
                cts.Cancel();
                await runTask.WaitAsync(TimeSpan.FromSeconds(5));
                await source.DisposeAsync();
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met before the timeout.");
            }

            await Task.Delay(50);
        }
    }
}
