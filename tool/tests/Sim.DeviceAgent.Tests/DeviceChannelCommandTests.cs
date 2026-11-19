using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Sim.DeviceAgent.Codecs;
using Sim.DeviceAgent.Configuration;
using Sim.DeviceAgent.Contracts;
using Sim.DeviceAgent.Runtime;
using Sim.DeviceAgent.Sources;
using Xunit;

namespace Sim.DeviceAgent.Tests;

public sealed class DeviceChannelCommandTests
{
    [Fact]
    public async Task SendCommand_MatchesResponseFrame_AndPublishesData()
    {
        var (channel, sink) = CreateChannel();
        await channel.StartAsync(CancellationToken.None);

        try
        {
            var outcome = await channel.SendCommandAsync(
                "READ\r\n",
                TimeSpan.FromSeconds(2),
                "^RESULT",
                CancellationToken.None);

            Assert.True(outcome.Success);
            Assert.False(outcome.TimedOut);
            Assert.NotNull(outcome.Match);
            Assert.Equal("RESULT,FORCE=12.34N", outcome.Match!.Frame.Text);

            await WaitUntilAsync(
                () => sink.Envelopes.Any(
                    envelope => envelope.Type == MessageTypes.Data
                                && envelope.DeviceId == "PT-001"
                                && envelope.Text == "RESULT,FORCE=12.34N"
                                && envelope.Seq > 0),
                TimeSpan.FromSeconds(2));
        }
        finally
        {
            await channel.StopAsync();
        }
    }

    [Fact]
    public async Task SendCommand_TimesOut_WhenNoMatchingFrameArrives()
    {
        var (channel, _) = CreateChannel(respond: false);
        await channel.StartAsync(CancellationToken.None);

        try
        {
            var outcome = await channel.SendCommandAsync(
                "READ\r\n",
                TimeSpan.FromMilliseconds(200),
                "^RESULT",
                CancellationToken.None);

            Assert.False(outcome.Success);
            Assert.True(outcome.TimedOut);
            Assert.Equal("command_timeout", outcome.ErrorCode);
        }
        finally
        {
            await channel.StopAsync();
        }
    }

    private static (DeviceChannel Channel, CapturingSink Sink) CreateChannel(bool respond = true)
    {
        var config = new ChannelConfig
        {
            Id = "pt-01",
            DeviceId = "PT-001",
            DeviceType = "pullTest",
            DeviceName = "Pull tester",
            SourceRef = "fake",
            QueueCapacity = 64,
            Codec = new CodecConfig
            {
                Type = "delimiter",
                Delimiter = "\r\n",
                Encoding = "ascii"
            },
            Sinks = ["local"]
        };

        var sink = new CapturingSink("local", "wsServer");
        var channel = new DeviceChannel(
            "agent-test",
            config,
            new FakeSource(respond),
            CodecFactory.Create(config.Codec, "serial"),
            [sink],
            new InMemorySequenceStore(),
            NullLogger<DeviceChannel>.Instance);

        return (channel, sink);
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

            await Task.Delay(25);
        }
    }

    private sealed class FakeSource : IDeviceSource
    {
        private readonly bool _respond;
        private readonly TaskCompletionSource<SourceEventHandler> _handlerReady =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public FakeSource(bool respond)
        {
            _respond = respond;
        }

        public string Id => "fake";

        public bool CanWrite => true;

        public async Task RunAsync(SourceEventHandler handler, CancellationToken cancellationToken)
        {
            _handlerReady.TrySetResult(handler);
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown path.
            }
        }

        public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            if (!_respond)
            {
                return;
            }

            var handler = await _handlerReady.Task.WaitAsync(cancellationToken);
            await handler(new SourceBytes(Encoding.ASCII.GetBytes("RESULT,FORCE=12.34N\r\n")), cancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CapturingSink : IEnvelopeSink
    {
        private readonly ConcurrentQueue<MessageEnvelope> _envelopes = new();

        public CapturingSink(string id, string type)
        {
            Id = id;
            Type = type;
        }

        public string Id { get; }

        public string Type { get; }

        public IReadOnlyCollection<MessageEnvelope> Envelopes => _envelopes.ToArray();

        public SinkStatus GetStatus() => new(Id, Type, "connected", 0, null);

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask PublishAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        {
            _envelopes.Enqueue(envelope);
            return ValueTask.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class InMemorySequenceStore : ISequenceStore
    {
        private long _current;

        public long Next(string deviceId) => Interlocked.Increment(ref _current);
    }
}
