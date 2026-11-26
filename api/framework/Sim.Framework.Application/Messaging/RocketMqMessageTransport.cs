using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Org.Apache.Rocketmq;
using Sim.Framework.Domain.Shared.Messaging;
using RocketMessage = Org.Apache.Rocketmq.Message;

namespace Sim.Framework.Application.Messaging;

public sealed class RocketMqMessageTransport(
    IOptions<MessagingOptions> options,
    ILoggerFactory loggerFactory,
    ILogger<RocketMqMessageTransport> logger) : IMessageTransport
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly MessagingOptions _options = options.Value;
    private Producer? _producer;
    private SimpleConsumer? _consumer;
    private CancellationTokenSource? _lifetime;
    private Task? _consumerTask;
    private Func<MessageEnvelope, CancellationToken, Task>? _handler;

    public string Name => "RocketMQ";

    public async Task StartAsync(
        Func<MessageEnvelope, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        MqLogManager.UseLoggerFactory(loggerFactory);
        _handler = handler;
        var clientConfig = CreateClientConfig();
        _producer = await new Producer.Builder()
            .SetTopics(_options.Topic)
            .SetClientConfig(clientConfig)
            .Build()
            .WaitAsync(cancellationToken);
        _consumer = await new SimpleConsumer.Builder()
            .SetClientConfig(clientConfig)
            .SetConsumerGroup(_options.ConsumerGroup)
            .SetAwaitDuration(TimeSpan.FromSeconds(15))
            .SetSubscriptionExpression(new Dictionary<string, FilterExpression>
            {
                [_options.Topic] = new("*")
            })
            .Build()
            .WaitAsync(cancellationToken);
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumerTask = Task.Run(() => ConsumeAsync(_lifetime.Token), CancellationToken.None);
    }

    public async Task PublishAsync(
        MessageEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var producer = _producer ?? throw new InvalidOperationException("RocketMQ transport is not started.");
        var message = new RocketMessage.Builder()
            .SetTopic(_options.Topic)
            .SetBody(JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions))
            .SetKeys(envelope.MessageId.ToString("N"))
            .SetTag("sim")
            .Build();
        await producer.Send(message).WaitAsync(cancellationToken);
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

        if (_consumer != null)
        {
            await _consumer.DisposeAsync();
            _consumer = null;
        }

        if (_producer != null)
        {
            await _producer.DisposeAsync();
            _producer = null;
        }

        _lifetime?.Dispose();
        _lifetime = null;
        _consumerTask = null;
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        var consumer = _consumer ?? throw new InvalidOperationException("RocketMQ consumer is not started.");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = await consumer.Receive(16, TimeSpan.FromSeconds(15))
                    .WaitAsync(cancellationToken);
                foreach (var message in messages)
                {
                    var envelope = JsonSerializer.Deserialize<MessageEnvelope>(
                                       message.Body,
                                       SerializerOptions)
                                   ?? throw new InvalidOperationException(
                                       "RocketMQ message envelope is invalid.");
                    await (_handler ?? throw new InvalidOperationException("Message handler is not set."))(
                        envelope,
                        cancellationToken);
                    await consumer.Ack(message).WaitAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "RocketMQ consume or handling failed; message will become visible again.");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private ClientConfig CreateClientConfig()
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException(
                "Framework:Messaging:ConnectionString must contain the RocketMQ 5.x gRPC endpoint.");
        }

        var builder = new ClientConfig.Builder().SetEndpoints(_options.ConnectionString);
        if (!string.IsNullOrWhiteSpace(_options.AccessKey) &&
            !string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            builder.SetCredentialsProvider(
                new StaticSessionCredentialsProvider(_options.AccessKey, _options.SecretKey));
        }

        return builder.Build();
    }
}
