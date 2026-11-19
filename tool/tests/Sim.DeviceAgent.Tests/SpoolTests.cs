using Microsoft.Extensions.Logging.Abstractions;
using Sim.DeviceAgent.Contracts;
using Sim.DeviceAgent.Runtime;
using Sim.DeviceAgent.Spool;
using Xunit;

namespace Sim.DeviceAgent.Tests;

public sealed class SpoolTests
{
    [Fact]
    public void SequenceStore_ReservesBlocks_SoRestartDoesNotReuseSequences()
    {
        var directory = CreateTempDirectory();

        try
        {
            var first = new FileSequenceStore(directory, NullLogger<FileSequenceStore>.Instance);
            Assert.Equal(1, first.Next("PT-001"));
            Assert.Equal(2, first.Next("PT-001"));

            var restarted = new FileSequenceStore(directory, NullLogger<FileSequenceStore>.Instance);
            Assert.Equal(100_001, restarted.Next("PT-001"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task DeviceSpool_ReadsInOrder_AndReplaysOnlyUnacknowledgedEntries()
    {
        var directory = CreateTempDirectory();
        var spool = new DeviceSpool(
            "PT-001",
            directory,
            maxSegmentBytes: 1024 * 1024,
            maxTotalBytes: 10 * 1024 * 1024,
            NullLogger.Instance);

        try
        {
            await spool.AppendAsync(CreateEnvelope(1, "first"), CancellationToken.None);
            await spool.AppendAsync(CreateEnvelope(2, "second"), CancellationToken.None);

            Assert.Equal(1, (await spool.TryReadNextAsync(CancellationToken.None))!.Seq);
            Assert.Equal(2, (await spool.TryReadNextAsync(CancellationToken.None))!.Seq);
            Assert.Null(await spool.TryReadNextAsync(CancellationToken.None));
            Assert.Equal(2, spool.PendingCount);

            spool.MarkDelivered(1);
            await spool.RewindAsync(CancellationToken.None);

            var replay = await spool.TryReadNextAsync(CancellationToken.None);
            Assert.NotNull(replay);
            Assert.Equal(2, replay!.Seq);

            spool.MarkDelivered(2);
            Assert.Equal(0, spool.PendingCount);

            await spool.RewindAsync(CancellationToken.None);
            Assert.Null(await spool.TryReadNextAsync(CancellationToken.None));
        }
        finally
        {
            await spool.DisposeAsync();
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ReopenedSpool_ReportsBacklogAndResumesAfterDeliveredSequence()
    {
        var directory = CreateTempDirectory();
        var first = new DeviceSpool(
            "PT-001",
            directory,
            maxSegmentBytes: 1024 * 1024,
            maxTotalBytes: 10 * 1024 * 1024,
            NullLogger.Instance);

        await first.AppendAsync(CreateEnvelope(1, "first"), CancellationToken.None);
        await first.AppendAsync(CreateEnvelope(2, "second"), CancellationToken.None);
        first.MarkDelivered(1);
        await first.DisposeAsync();

        var reopened = new DeviceSpool(
            "PT-001",
            directory,
            maxSegmentBytes: 1024 * 1024,
            maxTotalBytes: 10 * 1024 * 1024,
            NullLogger.Instance);

        try
        {
            Assert.Equal(1, reopened.PendingCount);

            var next = await reopened.TryReadNextAsync(CancellationToken.None);
            Assert.NotNull(next);
            Assert.Equal(2, next!.Seq);
        }
        finally
        {
            await reopened.DisposeAsync();
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task DeviceSpool_ContinuesReadingActiveSegment_AfterAppendFollowingEof()
    {
        var directory = CreateTempDirectory();
        var spool = new DeviceSpool(
            "PT-001",
            directory,
            maxSegmentBytes: 1024 * 1024,
            maxTotalBytes: 10 * 1024 * 1024,
            NullLogger.Instance);

        try
        {
            await spool.AppendAsync(CreateEnvelope(1, "first"), CancellationToken.None);
            Assert.Equal(1, (await spool.TryReadNextAsync(CancellationToken.None))!.Seq);
            Assert.Null(await spool.TryReadNextAsync(CancellationToken.None));

            await spool.AppendAsync(CreateEnvelope(2, "second"), CancellationToken.None);
            spool.MarkDelivered(1);

            var next = await spool.TryReadNextAsync(CancellationToken.None);
            Assert.NotNull(next);
            Assert.Equal(2, next!.Seq);
        }
        finally
        {
            await spool.DisposeAsync();
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task DeviceSpool_DeletesOnlyAcknowledgedInactiveSegments()
    {
        var directory = CreateTempDirectory();
        var spool = new DeviceSpool(
            "PT-001",
            directory,
            maxSegmentBytes: 1,
            maxTotalBytes: 10 * 1024 * 1024,
            NullLogger.Instance);

        try
        {
            await spool.AppendAsync(CreateEnvelope(1, "first"), CancellationToken.None);
            await spool.AppendAsync(CreateEnvelope(2, "second"), CancellationToken.None);

            Assert.Equal(2, Directory.GetFiles(directory, "segment-*.jsonl").Length);

            Assert.Equal(1, (await spool.TryReadNextAsync(CancellationToken.None))!.Seq);
            Assert.Equal(2, (await spool.TryReadNextAsync(CancellationToken.None))!.Seq);
            Assert.Null(await spool.TryReadNextAsync(CancellationToken.None));

            spool.MarkDelivered(2);

            var remaining = Directory.GetFiles(directory, "segment-*.jsonl");
            Assert.Single(remaining);
            Assert.EndsWith("00000002.jsonl", remaining[0], StringComparison.Ordinal);
        }
        finally
        {
            await spool.DisposeAsync();
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task DeviceSpool_RejectsWritesWhenQuotaIsExceeded()
    {
        var directory = CreateTempDirectory();
        var spool = new DeviceSpool(
            "PT-001",
            directory,
            maxSegmentBytes: 1024,
            maxTotalBytes: 1024,
            NullLogger.Instance);

        try
        {
            await spool.AppendAsync(CreateEnvelope(1, "small"), CancellationToken.None);

            await Assert.ThrowsAsync<IOException>(async () =>
                await spool.AppendAsync(CreateEnvelope(2, new string('x', 4096)), CancellationToken.None));
        }
        finally
        {
            await spool.DisposeAsync();
            DeleteDirectory(directory);
        }
    }

    private static MessageEnvelope CreateEnvelope(long seq, string text)
    {
        return new MessageEnvelope
        {
            Type = MessageTypes.Data,
            AgentId = "agent-test",
            DeviceId = "PT-001",
            DeviceType = "pullTest",
            ChannelId = "pt-01",
            Seq = seq,
            Ts = DateTimeOffset.UtcNow,
            Raw = "eA==",
            Text = text
        };
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "sim-device-agent-spool-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Test cleanup only.
        }
    }
}
