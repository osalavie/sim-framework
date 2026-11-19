using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sim.Framework.Domain.Shared.Messaging;

namespace Sim.Framework.Application.Messaging;

public sealed class KafkaMessageTransport(
    IOptions<MessagingOptions> options,
    ILogger<KafkaMessageTransport> logger) : IMessageTransport, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly MessagingOptions _options = options.Value;
    private IProducer<string, byte[]>? _producer;
    private IConsumer<string, byte[]>? _consumer;
    private CancellationTokenSource? _lifetime;
    private Task? _consumerTask;
    private Func<MessageEnvelope, CancellationToken, Task>? _handler;

    public string Name => "Kafka";

    public Task StartAsync(
        Func<MessageEnvelope, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        var bootstrapServers = RequiredConnectionString();
        _handler = handler;
        _producer = new ProducerBuilder<string, byte[]>(CreateProducerConfig(bootstrapServers)).Build();
        _consumer = new ConsumerBuilder<string, byte[]>(CreateConsumerConfig(bootstrapServers)).Build();
        _consumer.Subscribe(_options.Topic);
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumerTask = Task.Run(() => ConsumeAsync(_lifetime.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task PublishAsync(
        MessageEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var producer = _producer ?? throw new InvalidOperationException("Kafka transport is not started.");
        var result = await producer.ProduceAsync(
            _options.Topic,
            new Message<string, byte[]>
            {
                Key = envelope.PartitionKey ?? envelope.MessageId.ToString("N"),
                Value = JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions),
                Headers = new Headers
                {
                    { "sim-message-id", System.Text.Encoding.UTF8.GetBytes(envelope.MessageId.ToString("N")) },
                    { "sim-message-type", System.Text.Encoding.UTF8.GetBytes(envelope.Type) }
                }
            },
            cancellationToken);
        if (result.Status != PersistenceStatus.Persisted)
        {
            throw new KafkaException(new Error(
                ErrorCode.Local_MsgTimedOut,
                $"Kafka publish was not confirmed as persisted: {result.Status}."));
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_lifetime != null)
        {
            await _lifetime.CancelAsync();
        }

        if (_consumerTask != null)
        {
            try
            {
                await _consumerTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        _consumer?.Close();
        _producer?.Flush(TimeSpan.FromSeconds(10));
        _consumerTask = null;
        _lifetime?.Dispose();
        _lifetime = null;
    }

    public void Dispose()
    {
        _consumer?.Dispose();
        _producer?.Dispose();
        _lifetime?.Dispose();
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        var consumer = _consumer ?? throw new InvalidOperationException("Kafka consumer is not started.");
        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<string, byte[]> consumed;
            try
            {
                consumed = consumer.Consume(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ConsumeException exception)
            {
                logger.LogWarning(exception, "Kafka consume failed. IsFatal={IsFatal}", exception.Error.IsFatal);
                if (exception.Error.IsFatal) throw;
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                continue;
            }

            await HandleWithRetryAsync(consumed, cancellationToken);
            consumer.StoreOffset(consumed);
            consumer.Commit(consumed);
        }
    }

    private async Task HandleWithRetryAsync(
        ConsumeResult<string, byte[]> consumed,
        CancellationToken cancellationToken)
    {
        var attempts = Math.Clamp(_options.ConsumerMaxAttempts, 1, 20);
        Exception? lastException = null;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<MessageEnvelope>(
                                   consumed.Message.Value,
                                   SerializerOptions)
                               ?? throw new InvalidOperationException("Kafka message envelope is invalid.");
                await (_handler ?? throw new InvalidOperationException("Message handler is not set."))(
                    envelope,
                    cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                lastException = exception;
                logger.LogWarning(
                    exception,
                    "Kafka message handling failed. Topic={Topic}, Partition={Partition}, Offset={Offset}, Attempt={Attempt}",
                    consumed.Topic,
                    consumed.Partition.Value,
                    consumed.Offset.Value,
                    attempt);
                if (attempt < attempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, attempt * 2)), cancellationToken);
                }
            }
        }

        await PublishDeadLetterAsync(consumed, lastException!, cancellationToken);
    }

    private async Task PublishDeadLetterAsync(
        ConsumeResult<string, byte[]> consumed,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var producer = _producer ?? throw new InvalidOperationException("Kafka producer is not started.");
        var topic = string.IsNullOrWhiteSpace(_options.DeadLetterTopic)
            ? $"{_options.Topic}.dead-letter"
            : _options.DeadLetterTopic;
        var headers = new Headers
        {
            { "sim-source-topic", System.Text.Encoding.UTF8.GetBytes(consumed.Topic) },
            { "sim-source-partition", System.Text.Encoding.UTF8.GetBytes(consumed.Partition.Value.ToString()) },
            { "sim-source-offset", System.Text.Encoding.UTF8.GetBytes(consumed.Offset.Value.ToString()) },
            { "sim-error-type", System.Text.Encoding.UTF8.GetBytes(exception.GetType().Name) }
        };
        var result = await producer.ProduceAsync(
            topic,
            new Message<string, byte[]>
            {
                Key = consumed.Message.Key,
                Value = consumed.Message.Value,
                Headers = headers
            },
            cancellationToken);
        if (result.Status != PersistenceStatus.Persisted)
        {
            throw new InvalidOperationException("Kafka dead-letter publish was not confirmed.");
        }
    }

    private ProducerConfig CreateProducerConfig(string bootstrapServers)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            ClientId = _options.ClientId,
            Acks = Acks.All,
            EnableIdempotence = true,
            SocketKeepaliveEnable = true
        };
        ApplySecurity(config);
        return config;
    }

    private ConsumerConfig CreateConsumerConfig(string bootstrapServers)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            ClientId = _options.ClientId,
            GroupId = _options.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            SocketKeepaliveEnable = true
        };
        ApplySecurity(config);
        return config;
    }

    private void ApplySecurity(ClientConfig config)
    {
        if (Enum.TryParse<SecurityProtocol>(_options.SecurityProtocol, true, out var securityProtocol))
        {
            config.SecurityProtocol = securityProtocol;
        }

        if (Enum.TryParse<SaslMechanism>(_options.SaslMechanism, true, out var saslMechanism))
        {
            config.SaslMechanism = saslMechanism;
        }

        config.SaslUsername = _options.AccessKey;
        config.SaslPassword = _options.SecretKey;
    }

    private string RequiredConnectionString() =>
        !string.IsNullOrWhiteSpace(_options.ConnectionString)
            ? _options.ConnectionString
            : throw new InvalidOperationException(
                "Framework:Messaging:ConnectionString must contain Kafka bootstrap servers.");
}
