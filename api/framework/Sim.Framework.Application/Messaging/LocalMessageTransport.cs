using System.Threading.Channels;
using Sim.Framework.Domain.Shared.Messaging;

namespace Sim.Framework.Application.Messaging;

public sealed class LocalMessageTransport : IMessageTransport
{
    private readonly Channel<MessageEnvelope> _channel = Channel.CreateBounded<MessageEnvelope>(
        new BoundedChannelOptions(4096)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    private CancellationTokenSource? _stopping;
    private Task? _readerTask;

    public string Name => "Local";

    public Task StartAsync(
        Func<MessageEnvelope, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        if (_readerTask != null)
        {
            return Task.CompletedTask;
        }

        _stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _readerTask = ReadAsync(handler, _stopping.Token);
        return Task.CompletedTask;
    }

    public async Task PublishAsync(
        MessageEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(envelope, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _channel.Writer.TryComplete();
        if (_stopping != null)
        {
            await _stopping.CancelAsync();
        }

        if (_readerTask != null)
        {
            try
            {
                await _readerTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
        }
    }

    private async Task ReadAsync(
        Func<MessageEnvelope, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        await foreach (var envelope in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            await handler(envelope, cancellationToken);
        }
    }
}
