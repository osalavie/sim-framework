using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sim.DeviceAgent.Configuration;
using Sim.DeviceAgent.Contracts;
using Sim.DeviceAgent.Runtime;
using Xunit;

namespace Sim.DeviceAgent.Tests;

public sealed class CenterSinkTests
{
    [Fact]
    public async Task CenterSink_SendsSpooledData_AndDrainsOnlyAfterAck()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "sim-device-agent-center-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var received = new ConcurrentQueue<MessageEnvelope>();
        var receivedBoth = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var authorization = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.UseWebSockets();
        app.Map("/device", async context =>
        {
            authorization.TrySetResult(context.Request.Headers.Authorization.ToString());
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var buffer = new byte[32 * 1024];
            using var message = new MemoryStream();

            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await socket.ReceiveAsync(buffer, context.RequestAborted);
                }
                catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                {
                    continue;
                }

                var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                message.SetLength(0);

                using var document = JsonDocument.Parse(text);
                if (!document.RootElement.TryGetProperty("type", out var type) ||
                    type.GetString() != MessageTypes.Data)
                {
                    continue;
                }

                var envelope = JsonSerializer.Deserialize<MessageEnvelope>(text, AgentJson.Options);
                Assert.NotNull(envelope);
                received.Enqueue(envelope);
                if (received.Count >= 2)
                {
                    receivedBoth.TrySetResult(true);
                }

                var ack = JsonSerializer.Serialize(new
                {
                    v = 1,
                    type = MessageTypes.Ack,
                    agentId = envelope!.AgentId,
                    deviceId = envelope.DeviceId,
                    seq = envelope.Seq
                }, AgentJson.Options);

                await socket.SendAsync(
                    Encoding.UTF8.GetBytes(ack),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    CancellationToken.None);
            }
        });

        CenterWebSocketSink? sink = null;
        try
        {
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!
                .Addresses.Single();
            var url = address.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase) + "/device";

            sink = new CenterWebSocketSink(
                "center",
                new SinkConfig
                {
                    Type = "wsClient",
                    Url = url,
                    Token = "test-center-token",
                    ReconnectMinSeconds = 1,
                    ReconnectMaxSeconds = 2,
                    SpoolSegmentMegabytes = 1,
                    SpoolMaxMegabytes = 8
                },
                new AgentOptions
                {
                    Id = "agent-test",
                    DisplayName = "Test agent",
                    StateDirectory = Path.Combine(directory, "state")
                },
                NullLoggerFactory.Instance);

            await sink.StartAsync(CancellationToken.None);
            await sink.PublishAsync(CreateEnvelope(1), CancellationToken.None);
            await sink.PublishAsync(CreateEnvelope(2), CancellationToken.None);

            await receivedBoth.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await WaitUntilAsync(
                () => sink.GetStatus().PendingEntries == 0,
                TimeSpan.FromSeconds(10));

            Assert.Equal("Bearer test-center-token", await authorization.Task);
            Assert.Equal([1L, 2L], received.Select(envelope => envelope.Seq ?? 0).ToArray());
        }
        finally
        {
            if (sink is not null)
            {
                await sink.DisposeAsync();
            }

            await app.DisposeAsync();

            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // Test cleanup only.
            }
        }
    }

    private static MessageEnvelope CreateEnvelope(long seq)
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
            Text = $"RESULT,FORCE={seq}.00N"
        };
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
}
