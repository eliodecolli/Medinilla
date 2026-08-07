using Google.Protobuf;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Service.Communication;
using Medinilla.Core.Service.Types;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Infrastructure.WAMP;
using Medinilla.RealTime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;
using System.Text;

namespace Medinilla.Core.Service.Tests;

public class CoreInterfaceCommunicationShould : IDisposable
{
    private const string CLIENT_ID = "TEST-CHARGER-001";
    private const string REQUEST_QUEUE = "medinilla.core.request";
    private const string RESPONSE_QUEUE = "medinilla.ws.deadbeef.response";

    private readonly Mock<IReceiver> _receiverMock = new();
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<ILogger<CoreInterfaceCommunication>> _loggerMock = new();
    private readonly Mock<IOcppCallRouter> _routerMock = new();

    private readonly ServiceProvider _serviceProvider;
    private readonly CommunicationSettings _settings;
    private readonly string _settingsFile;

    // Feeds ReceiveAsync one payload at a time, blocking in between like BLPOP.
    private readonly ConcurrentQueue<byte[]> _queue = new();
    private readonly SemaphoreSlim _available = new(0);

    public CoreInterfaceCommunicationShould()
    {
        _settingsFile = Path.Combine(Path.GetTempPath(), $"medinilla-settings-{Guid.NewGuid():N}.json");
        File.WriteAllText(_settingsFile, $$"""
        {
          "RequestQueue": "{{REQUEST_QUEUE}}"
        }
        """);
        _settings = CommunicationSettings.FromSettingsFile(_settingsFile);

        var services = new ServiceCollection();
        services.AddScoped(_ => _routerMock.Object);
        _serviceProvider = services.BuildServiceProvider();

        _receiverMock
            .Setup(r => r.ReceiveAsync(REQUEST_QUEUE, It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (_, ct) =>
            {
                await _available.WaitAsync(ct);
                _queue.TryDequeue(out var payload);
                return payload;
            });

        _senderMock
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _available.Dispose();
        if (File.Exists(_settingsFile)) File.Delete(_settingsFile);
        GC.SuppressFinalize(this);
    }

    private CoreInterfaceCommunication CreateService()
        => new(_serviceProvider, _receiverMock.Object, _senderMock.Object, _loggerMock.Object, _settings);

    private void Push(byte[] raw)
    {
        _queue.Enqueue(raw);
        _available.Release();
    }

    private void Push(CommsMessageType type, string ocpp, string responseQueue = RESPONSE_QUEUE)
        => Push(new QueuedMessageRequest
        {
            ClientIdentifier = CLIENT_ID,
            ResponseQueue = responseQueue,
            Payload = new Comms
            {
                MessageType = type,
                ClientIdentifier = CLIENT_ID,
                Payload = ByteString.CopyFromUtf8(ocpp),
            },
        }.ToByteArray());

    private static async Task<bool> Settles(Task task, int timeoutMs = 2000)
        => await Task.WhenAny(task, Task.Delay(timeoutMs)) == task;

    /// <summary>Runs the loop until <paramref name="signal"/> fires, then tears it down.</summary>
    private async Task<bool> RunUntil(Task signal, Action pump)
    {
        using var cts = new CancellationTokenSource();
        var service = CreateService();
        var loop = service.Run(cts.Token);

        pump();

        var settled = await Settles(signal);

        await cts.CancelAsync();
        _available.Release();
        await Settles(loop);

        return settled;
    }

    [Fact]
    public async Task RouteThePayloadFromTheEnvelope()
    {
        const string ocpp = "[2,\"req-1\",\"BootNotification\",{}]";

        var routed = new TaskCompletionSource<(byte[] Payload, string? Client)>();
        _routerMock
            .Setup(r => r.RouteOcppCall(It.IsAny<byte[]>(), It.IsAny<string?>()))
            .Callback<byte[], string?>((payload, client) => routed.TrySetResult((payload, client)))
            .ReturnsAsync((RpcResult?)null);

        Assert.True(await RunUntil(routed.Task, () => Push(CommsMessageType.OcppRequest, ocpp)));

        var (routedPayload, routedClient) = await routed.Task;
        Assert.Equal(CLIENT_ID, routedClient);
        Assert.Equal(ocpp, Encoding.UTF8.GetString(routedPayload));
    }

    [Fact]
    public async Task PublishTheResponseToTheQueueNamedInTheEnvelope()
    {
        const string customQueue = "medinilla.ws.cafebabe.response";

        _routerMock
            .Setup(r => r.RouteOcppCall(It.IsAny<byte[]>(), It.IsAny<string?>()))
            .ReturnsAsync(new RpcResult { Result = new OcppCallResult("req-1", "{}") });

        var sent = new TaskCompletionSource<(string Queue, byte[] Payload)>();
        _senderMock
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], CancellationToken>((queue, payload, _) => sent.TrySetResult((queue, payload)))
            .Returns(Task.CompletedTask);

        Assert.True(await RunUntil(sent.Task, () => Push(
            CommsMessageType.OcppRequest, "[2,\"req-1\",\"BootNotification\",{}]", customQueue)));

        var (queue, raw) = await sent.Task;

        Assert.Equal(customQueue, queue);

        var envelope = QueuedMessageResponse.Parser.ParseFrom(raw);
        Assert.Equal(CLIENT_ID, envelope.ClientIdentifier);
        Assert.Equal(CommsMessageType.OcppResponse, envelope.Payload.MessageType);
        Assert.Equal("[3,\"req-1\",{}]", envelope.Payload.Payload.ToStringUtf8());
    }

    [Fact]
    public async Task PublishTheErrorWhenRoutingFails()
    {
        _routerMock
            .Setup(r => r.RouteOcppCall(It.IsAny<byte[]>(), It.IsAny<string?>()))
            .ReturnsAsync(new RpcResult { Error = OcppCallError.InternalError("req-1") });

        var sent = new TaskCompletionSource<byte[]>();
        _senderMock
            .Setup(s => s.SendAsync(RESPONSE_QUEUE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], CancellationToken>((_, payload, _) => sent.TrySetResult(payload))
            .Returns(Task.CompletedTask);

        Assert.True(await RunUntil(sent.Task, () => Push(
            CommsMessageType.OcppRequest, "[2,\"req-1\",\"BootNotification\",{}]")));

        var envelope = QueuedMessageResponse.Parser.ParseFrom(await sent.Task);
        Assert.StartsWith("[4,\"req-1\",\"InternalError\"", envelope.Payload.Payload.ToStringUtf8());
    }

    [Fact]
    public async Task NotPublishAnythingWhenTheRouterHasNoAnswer()
    {
        var routed = new TaskCompletionSource();
        _routerMock
            .Setup(r => r.RouteOcppCall(It.IsAny<byte[]>(), It.IsAny<string?>()))
            .Callback(() => routed.TrySetResult())
            .ReturnsAsync((RpcResult?)null);

        Assert.True(await RunUntil(routed.Task, () => Push(
            CommsMessageType.OcppResponse, "[3,\"req-1\",{}]")));

        // The router owns CALL_RESULT handling; there is nothing to send back.
        await Task.Delay(100);
        _senderMock.Verify(
            s => s.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchClientDisconnectToTheRouter()
    {
        var disconnected = new TaskCompletionSource<string>();
        _routerMock
            .Setup(r => r.DisconnectClient(It.IsAny<string>()))
            .Callback<string>(client => disconnected.TrySetResult(client))
            .Returns(Task.CompletedTask);

        Assert.True(await RunUntil(disconnected.Task, () => Push(CommsMessageType.ClientDisconnect, "")));

        Assert.Equal(CLIENT_ID, await disconnected.Task);
    }

    [Fact]
    public async Task DropMalformedEnvelopesAndKeepRunning()
    {
        var routed = new TaskCompletionSource();
        _routerMock
            .Setup(r => r.RouteOcppCall(It.IsAny<byte[]>(), It.IsAny<string?>()))
            .Callback(() => routed.TrySetResult())
            .ReturnsAsync((RpcResult?)null);

        var settled = await RunUntil(routed.Task, () =>
        {
            Push([0xFF, 0xFF, 0xFF, 0xFF]);
            Push(CommsMessageType.OcppRequest, "[2,\"req-1\",\"BootNotification\",{}]");
        });

        Assert.True(settled, "loop stopped after a malformed envelope");
    }
}
