using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Sim.Framework.Domain.Shared.Messaging;

namespace Sim.Framework.Application.Messaging;

public sealed class RabbitMqMessageTransport(
    IOptions<MessagingOptions> options,
    ILogger<RabbitMqMessageTransport> logger)
    : IMessageTransport, IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private IConnection? _connection;
    private IChannel? _publishChannel;
    private IChannel? _consumerChannel;
    private Func<MessageEnvelope, CancellationToken, Task>? _handler;

    public string Name => "RabbitMQ";

    public async Task StartAsync(
        Func<MessageEnvelope, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        _handler = handler;
        var uri = options.Value.ConnectionString;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ||
            parsed.Scheme is not ("amqp" or "amqps"))
        {
            throw new InvalidOperationException(
                "Framework:Messaging:ConnectionString must be a valid amqp(s) URI. " +
                "Reserved characters in the user name or password, such as '@', ':' or '/', " +
                "must be percent-encoded, for example amqp://user:p%40ss@host:5672/vhost.");
        }

        var factory = new ConnectionFactory
        {
            Uri = parsed,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            ConsumerDispatchConcurrency = 1
        };
        _connection = await factory.CreateConnectionAsync("sim-framework", cancellationToken);
        _publishChannel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                outstandingPublisherConfirmationsRateLimiter: null,
                consumerDispatchConcurrency: null),
            cancellationToken);
        _consumerChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await _publishChannel.ExchangeDeclareAsync(
            options.Value.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        var deadLetterExchange = $"{options.Value.Exchange}.dead-letter";
        var deadLetterQueue = $"{options.Value.Queue}.dlq";
        await _consumerChannel.ExchangeDeclareAsync(
            deadLetterExchange,
            ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await _consumerChannel.ExchangeDeclareAsync(
            options.Value.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await _consumerChannel.QueueDeclareAsync(
            deadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await _consumerChannel.QueueBindAsync(
            deadLetterQueue,
            deadLetterExchange,
            string.Empty,
            cancellationToken: cancellationToken);
        await _consumerChannel.QueueDeclareAsync(
            options.Value.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = deadLetterExchange
            },
            cancellationToken: cancellationToken);
        await _consumerChannel.QueueBindAsync(
            options.Value.Queue,
            options.Value.Exchange,
            options.Value.Topic,
            cancellationToken: cancellationToken);
        await _consumerChannel.BasicQosAsync(0, 16, false, cancellationToken);
        var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
        consumer.ReceivedAsync += HandleReceivedAsync;
        await _consumerChannel.BasicConsumeAsync(
            options.Value.Queue,
            autoAck: false,
            consumer,
            cancellationToken);
    }

    public async Task PublishAsync(
        MessageEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var channel = _publishChannel ??
                      throw new InvalidOperationException("RabbitMQ transport is not started.");
        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = envelope.MessageId.ToString("N"),
            Type = envelope.Type,
            ContentType = "application/json",
            CorrelationId = envelope.CorrelationId
        };
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions);
        await channel.BasicPublishAsync(
            options.Value.Exchange,
            options.Value.Topic,
            mandatory: true,
            properties,
            body,
            cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_consumerChannel != null)
        {
            await _consumerChannel.CloseAsync(cancellationToken);
            await _consumerChannel.DisposeAsync();
            _consumerChannel = null;
        }

        if (_publishChannel != null)
        {
            await _publishChannel.CloseAsync(cancellationToken);
            await _publishChannel.DisposeAsync();
            _publishChannel = null;
        }

        if (_connection != null)
        {
            await _connection.CloseAsync(cancellationToken);
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private async Task HandleReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        var channel = _consumerChannel;
        var handler = _handler;
        if (channel == null || handler == null) return;
        try
        {
            var envelope = JsonSerializer.Deserialize<MessageEnvelope>(
                               args.Body.Span,
                               SerializerOptions)
                           ?? throw new InvalidOperationException("RabbitMQ message envelope is invalid.");
            await handler(envelope, args.CancellationToken);
            await channel.BasicAckAsync(args.DeliveryTag, false, args.CancellationToken);
        }
        catch (OperationCanceledException) when (args.CancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "RabbitMQ message handling failed. DeliveryTag={DeliveryTag}, Redelivered={Redelivered}",
                args.DeliveryTag,
                args.Redelivered);
            await channel.BasicNackAsync(
                args.DeliveryTag,
                false,
                requeue: !args.Redelivered,
                args.CancellationToken);
        }
    }
}
