using Google.Protobuf;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.WebApi.Services;
using Medinilla.RealTime;
using Medinilla.WebApi.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Xunit.Abstractions;

namespace Medinilla.Core.WebApi.Tests;

public class WebSocketDigestionServiceShould : IAsyncLifetime
{
    private readonly ITestOutputHelper _testOutputHelper;

    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IConfigurationSection> _commsSectionMock;
    private readonly Mock<IConfigurationSection> _generalSectionMock;
    private readonly Mock<ILogger<WebSocketDigestionService>> _loggerMock;
    private readonly Mock<ISender> _senderMock;
    private readonly Mock<ISubscriptionReceiver> _subscriptionMock;
    private readonly Mock<IWebSocketRoutingTable> _routingMock;
    private readonly Mock<IInstanceIdentifier> _instanceMock;
    private readonly Mock<IHostApplicationLifetime> _hostLifetimeMock;

    // Stands in for RedisSubscriptionReceiver's dispatch loop: PushInbound() queues a
    // message, the pump hands it to whatever callback the service subscribed with.
    private readonly ConcurrentQueue<QueuedMessageResponse> _inboundMessages = new();
    private readonly SemaphoreSlim _inboundSemaphore = new(0);
    private readonly CancellationTokenSource _pumpCts = new();
    private Func<QueuedMessageResponse, CancellationToken, Task>? _subscriberCallback;
    private Task? _pump;

    private const string TEST_CLIENT_ID = "TEST-CHARGER-001";
    private const string TEST_REQUEST_QUEUE = "test-request-queue";
    private const string TEST_RESPONSE_QUEUE = "medinilla.ws.deadbeef.response";

    public WebSocketDigestionServiceShould(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        _configMock = new Mock<IConfiguration>();
        _commsSectionMock = new Mock<IConfigurationSection>();
        _generalSectionMock = new Mock<IConfigurationSection>();
        _loggerMock = new Mock<ILogger<WebSocketDigestionService>>();
        _senderMock = new Mock<ISender>();
        _subscriptionMock = new Mock<ISubscriptionReceiver>();
        _routingMock = new Mock<IWebSocketRoutingTable>();
        _instanceMock = new Mock<IInstanceIdentifier>();
        _hostLifetimeMock = new Mock<IHostApplicationLifetime>();

        _commsSectionMock.Setup(s => s["RequestQueue"]).Returns(TEST_REQUEST_QUEUE);
        _configMock.Setup(c => c.GetSection("Comms")).Returns(_commsSectionMock.Object);

        _generalSectionMock.Setup(s => s["MessageQueueTTL"]).Returns("5");
        _configMock.Setup(c => c.GetSection("General")).Returns(_generalSectionMock.Object);

        _instanceMock.Setup(i => i.InstanceId).Returns("deadbeef");
        _instanceMock.Setup(i => i.ResponseQueue).Returns(TEST_RESPONSE_QUEUE);

        _hostLifetimeMock.Setup(h => h.ApplicationStopping).Returns(CancellationToken.None);

        _senderMock
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _routingMock
            .Setup(r => r.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _routingMock
            .Setup(r => r.UnregisterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _routingMock
            .Setup(r => r.RefreshEntryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _subscriptionMock
            .Setup(s => s.Subscribe(
                It.IsAny<string>(),
                It.IsAny<Func<QueuedMessageResponse, CancellationToken, Task>>()))
            .Callback<string, Func<QueuedMessageResponse, CancellationToken, Task>>((_, callback) =>
            {
                _subscriberCallback = callback;
                _pump ??= Task.Run(PumpInbound);
            });

        _subscriptionMock
            .Setup(s => s.Unsubscribe(It.IsAny<string>()))
            .Callback<string>(_ => _subscriberCallback = null);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _pumpCts.CancelAsync();

        if (_pump is not null)
        {
            try { await _pump; } catch (OperationCanceledException) { }
        }

        _pumpCts.Dispose();
        _inboundSemaphore.Dispose();
    }

    // ----------------------------------------------------------------
    // Helpers — inbound channel control
    // ----------------------------------------------------------------

    /// <summary>Delivers a message to the subscribed callback.</summary>
    private void PushInbound(QueuedMessageResponse message)
    {
        _inboundMessages.Enqueue(message);
        _inboundSemaphore.Release();
    }

    private async Task PumpInbound()
    {
        while (!_pumpCts.IsCancellationRequested)
        {
            try
            {
                await _inboundSemaphore.WaitAsync(_pumpCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!_inboundMessages.TryDequeue(out var message)) continue;

            var callback = _subscriberCallback;
            if (callback is null) continue;

            try
            {
                await callback(message, _pumpCts.Token);
            }
            catch (Exception ex)
            {
                _testOutputHelper.WriteLine($"Subscriber threw: {ex.Message}");
            }
        }
    }

    // ----------------------------------------------------------------
    // Helpers — OCPP message builders
    // ----------------------------------------------------------------

    private static string CreateOcppCall(string messageId, string action, string payload = "{}")
        => $"[2,\"{messageId}\",\"{action}\",{payload}]";

    private static string CreateOcppCallResult(string messageId, string payload = "{}")
        => $"[3,\"{messageId}\",{payload}]";

    private static QueuedMessageResponse BuildQueued(string clientId, CommsMessageType type, string ocpp)
        => new()
        {
            ClientIdentifier = clientId,
            Payload = new Comms
            {
                MessageType = type,
                ClientIdentifier = clientId,
                Payload = ByteString.CopyFrom(Encoding.UTF8.GetBytes(ocpp)),
            },
        };

    /// <summary>Envelope delivered when CSMS answers a charger request.</summary>
    private static QueuedMessageResponse BuildCsmsResponse(string clientId, string ocppCallResult)
        => BuildQueued(clientId, CommsMessageType.OcppResponse, ocppCallResult);

    /// <summary>Envelope delivered when CSMS initiates a request to the charger.</summary>
    private static QueuedMessageResponse BuildCsmsRequest(string clientId, string ocppCall)
        => BuildQueued(clientId, CommsMessageType.OcppRequest, ocppCall);

    // ----------------------------------------------------------------
    // Helpers — WebSocket mocks
    // ----------------------------------------------------------------

    private Mock<WebSocket> CreateClosingWebSocketMock()
    {
        var wsMock = new Mock<WebSocket>();
        wsMock.Setup(ws => ws.State).Returns(WebSocketState.Open);
        wsMock.Setup(ws => ws.ReceiveAsync(
                It.IsAny<ArraySegment<byte>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
                WebSocketCloseStatus.NormalClosure, "done"));
        wsMock.Setup(ws => ws.CloseStatus).Returns(WebSocketCloseStatus.NormalClosure);
        wsMock.Setup(ws => ws.CloseAsync(
                It.IsAny<WebSocketCloseStatus>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return wsMock;
    }

    private List<byte[]> WireWebSocketSendCapture(Mock<WebSocket> wsMock)
    {
        var sentToWs = new List<byte[]>();
        wsMock.Setup(ws => ws.SendAsync(
                It.IsAny<ArraySegment<byte>>(),
                WebSocketMessageType.Text,
                true,
                It.IsAny<CancellationToken>()))
            .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>(
                (data, _, _, _) => sentToWs.Add(data.ToArray()))
            .Returns(Task.CompletedTask);
        wsMock.Setup(ws => ws.CloseAsync(
                It.IsAny<WebSocketCloseStatus>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return sentToWs;
    }

    private IMessageQueueFactory CreateQueueFactory() =>
        new MessageQueueFactory(_configMock.Object);

    private WebSocketDigestionService CreateService() =>
        new(_configMock.Object,
            _loggerMock.Object,
            _senderMock.Object,
            _subscriptionMock.Object,
            _routingMock.Object,
            _instanceMock.Object,
            _hostLifetimeMock.Object,
            CreateQueueFactory());

    // ================================================================
    // Constructor validation
    // ================================================================

    [Fact]
    public void ThrowOnNullConfiguration()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSocketDigestionService(null!, _loggerMock.Object, _senderMock.Object,
                _subscriptionMock.Object, _routingMock.Object, _instanceMock.Object,
                _hostLifetimeMock.Object, CreateQueueFactory()));
    }

    [Fact]
    public void ThrowOnNullLogger()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSocketDigestionService(_configMock.Object, null!, _senderMock.Object,
                _subscriptionMock.Object, _routingMock.Object, _instanceMock.Object,
                _hostLifetimeMock.Object, CreateQueueFactory()));
    }

    [Fact]
    public void ThrowOnNullSender()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSocketDigestionService(_configMock.Object, _loggerMock.Object, null!,
                _subscriptionMock.Object, _routingMock.Object, _instanceMock.Object,
                _hostLifetimeMock.Object, CreateQueueFactory()));
    }

    [Fact]
    public void ThrowOnNullSubscriptionReceiver()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSocketDigestionService(_configMock.Object, _loggerMock.Object, _senderMock.Object,
                null!, _routingMock.Object, _instanceMock.Object,
                _hostLifetimeMock.Object, CreateQueueFactory()));
    }

    [Fact]
    public void ThrowOnNullRoutingTable()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSocketDigestionService(_configMock.Object, _loggerMock.Object, _senderMock.Object,
                _subscriptionMock.Object, null!, _instanceMock.Object,
                _hostLifetimeMock.Object, CreateQueueFactory()));
    }

    [Fact]
    public void ThrowOnNullInstanceIdentifier()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSocketDigestionService(_configMock.Object, _loggerMock.Object, _senderMock.Object,
                _subscriptionMock.Object, _routingMock.Object, null!,
                _hostLifetimeMock.Object, CreateQueueFactory()));
    }

    [Fact]
    public void ThrowOnNullHostLifetime()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSocketDigestionService(_configMock.Object, _loggerMock.Object, _senderMock.Object,
                _subscriptionMock.Object, _routingMock.Object, _instanceMock.Object,
                null!, CreateQueueFactory()));
    }

    [Fact]
    public void ThrowOnNullQueueFactory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSocketDigestionService(_configMock.Object, _loggerMock.Object, _senderMock.Object,
                _subscriptionMock.Object, _routingMock.Object, _instanceMock.Object,
                _hostLifetimeMock.Object, (IMessageQueueFactory)null!));
    }

    // ================================================================
    // Consume argument & config validation
    // ================================================================

    [Fact]
    public async Task ThrowOnNullWebSocket()
    {
        await using var service = CreateService();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.Consume(null!, TEST_CLIENT_ID));
    }

    [Fact]
    public async Task ThrowOnNullClientIdentifier()
    {
        var wsMock = new Mock<WebSocket>();
        await using var service = CreateService();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.Consume(wsMock.Object, null!));
    }

    [Fact]
    public async Task ThrowWhenConsumedAfterDispose()
    {
        var wsMock = new Mock<WebSocket>();
        var service = CreateService();
        await service.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => service.Consume(wsMock.Object, TEST_CLIENT_ID));
    }

    [Fact]
    public async Task ThrowWhenRequestQueueNotConfigured()
    {
        _commsSectionMock.Setup(s => s["RequestQueue"]).Returns((string?)null);
        var wsMock = new Mock<WebSocket>();
        await using var service = CreateService();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.Consume(wsMock.Object, TEST_CLIENT_ID));
    }

    // ================================================================
    // Lifecycle
    // ================================================================

    [Fact]
    public async Task DisposeIdempotently()
    {
        var service = CreateService();
        await service.DisposeAsync();
        await service.DisposeAsync();
    }

    [Fact]
    public async Task CloseWebSocketOnDispose()
    {
        var wsMock = CreateClosingWebSocketMock();
        await using var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);
        await service.DisposeAsync();

        wsMock.Verify(ws => ws.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            "Service disposing",
            CancellationToken.None), Times.AtLeastOnce);
    }

    // Consume must claim the charger on the routing table and subscribe to this
    // instance's response queue; disposal must give both back.
    [Fact]
    public async Task RegisterAndSubscribeOnConsumeThenReleaseOnDispose()
    {
        var wsMock = CreateClosingWebSocketMock();
        var service = CreateService();

        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        _routingMock.Verify(
            r => r.RegisterAsync(TEST_CLIENT_ID, TEST_RESPONSE_QUEUE, It.IsAny<CancellationToken>()),
            Times.Once);
        _subscriptionMock.Verify(
            s => s.Subscribe(TEST_CLIENT_ID, It.IsAny<Func<QueuedMessageResponse, CancellationToken, Task>>()),
            Times.Once);

        await service.DisposeAsync();

        _subscriptionMock.Verify(s => s.Unsubscribe(TEST_CLIENT_ID), Times.AtLeastOnce);
        _routingMock.Verify(
            r => r.UnregisterAsync(TEST_CLIENT_ID, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // A charger that never connected must not touch the routing table.
    [Fact]
    public async Task NotUnregisterWhenNeverConsumed()
    {
        var service = CreateService();
        await service.DisposeAsync();

        _routingMock.Verify(
            r => r.UnregisterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _subscriptionMock.Verify(s => s.Unsubscribe(It.IsAny<string>()), Times.Never);
    }

    // A failing routing unregister must not prevent the socket from closing down.
    [Fact]
    public async Task SurviveRoutingUnregisterFailureOnDispose()
    {
        _routingMock
            .Setup(r => r.UnregisterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis is down"));

        var wsMock = CreateClosingWebSocketMock();
        var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        await service.DisposeAsync();

        wsMock.Verify(ws => ws.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            "Service disposing",
            CancellationToken.None), Times.AtLeastOnce);
    }

    [Fact]
    public async Task HandleWebSocketExceptionDuringReceive()
    {
        var socketClosed = false;
        var wsMock = new Mock<WebSocket>();
        // After the exception, State switches to Closed so the main loop exits.
        wsMock.Setup(ws => ws.State)
            .Returns(() => socketClosed ? WebSocketState.Closed : WebSocketState.Open);
        wsMock.Setup(ws => ws.ReceiveAsync(
                It.IsAny<ArraySegment<byte>>(),
                It.IsAny<CancellationToken>()))
            .Returns<ArraySegment<byte>, CancellationToken>((_, _) =>
            {
                socketClosed = true;
                return Task.FromException<WebSocketReceiveResult>(
                    new WebSocketException("Connection lost"));
            });
        wsMock.Setup(ws => ws.CloseAsync(
                It.IsAny<WebSocketCloseStatus>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        _senderMock.Verify(q => q.SendAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ================================================================
    // Message validation
    // ================================================================

    [Fact]
    public async Task DropMessageWithUnparseableId()
    {
        var invalidMessage = Encoding.UTF8.GetBytes("not-a-valid-ocpp-message");

        var callCount = 0;
        var wsMock = new Mock<WebSocket>();
        wsMock.Setup(ws => ws.State).Returns(WebSocketState.Open);
        wsMock.Setup(ws => ws.ReceiveAsync(
                It.IsAny<ArraySegment<byte>>(),
                It.IsAny<CancellationToken>()))
            .Returns((ArraySegment<byte> buffer, CancellationToken ct) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    invalidMessage.CopyTo(buffer.Array!, buffer.Offset);
                    return Task.FromResult(
                        new WebSocketReceiveResult(invalidMessage.Length, WebSocketMessageType.Text, true));
                }
                return Task.FromResult(
                    new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
                        WebSocketCloseStatus.NormalClosure, "done"));
            });
        wsMock.Setup(ws => ws.CloseStatus)
            .Returns(() => callCount >= 2 ? WebSocketCloseStatus.NormalClosure : null);
        wsMock.Setup(ws => ws.CloseAsync(
                It.IsAny<WebSocketCloseStatus>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        _senderMock.Verify(q => q.SendAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ================================================================
    // Envelope shape
    // ================================================================

    // Everything published to the CSMS must be a QueuedMessageRequest carrying this
    // instance's response queue — that's how the CSMS knows where to reply.
    [Fact]
    public async Task WrapOutboundMessagesInQueuedRequestEnvelope()
    {
        var chargerRequest = CreateOcppCall("req-1", "BootNotification");
        var requestBytes = Encoding.UTF8.GetBytes(chargerRequest);

        var published = new List<byte[]>();
        _senderMock
            .Setup(q => q.SendAsync(TEST_REQUEST_QUEUE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], CancellationToken>((_, payload, _) => published.Add(payload))
            .Returns(Task.CompletedTask);

        var callCount = 0;
        var wsMock = new Mock<WebSocket>();
        wsMock.Setup(ws => ws.State).Returns(WebSocketState.Open);
        wsMock.Setup(ws => ws.ReceiveAsync(
                It.IsAny<ArraySegment<byte>>(),
                It.IsAny<CancellationToken>()))
            .Returns((ArraySegment<byte> buffer, CancellationToken ct) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    requestBytes.CopyTo(buffer.Array!, buffer.Offset);
                    return Task.FromResult(
                        new WebSocketReceiveResult(requestBytes.Length, WebSocketMessageType.Text, true));
                }
                return Task.FromResult(
                    new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
                        WebSocketCloseStatus.NormalClosure, "done"));
            });
        wsMock.Setup(ws => ws.CloseStatus)
            .Returns(() => callCount >= 2 ? WebSocketCloseStatus.NormalClosure : null);
        WireWebSocketSendCapture(wsMock);

        await using var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        var envelope = QueuedMessageRequest.Parser.ParseFrom(Assert.Single(published));

        Assert.Equal(TEST_CLIENT_ID, envelope.ClientIdentifier);
        Assert.Equal(TEST_RESPONSE_QUEUE, envelope.ResponseQueue);
        Assert.Equal(CommsMessageType.OcppRequest, envelope.Payload.MessageType);
        Assert.Equal(chargerRequest, envelope.Payload.Payload.ToStringUtf8());
    }

    // ================================================================
    // OCPP synchronization protocol
    // ================================================================

    // Scenario: Charger sends a Call, CSMS responds with CallResult.
    // The response must be correlated by message ID and forwarded back to the charger.
    [Fact]
    public async Task ForwardCsmsResponseToChargerAfterRequest()
    {
        var chargerRequest = CreateOcppCall("req-1", "BootNotification");
        var csmsResponse = CreateOcppCallResult("req-1");
        var requestBytes = Encoding.UTF8.GetBytes(chargerRequest);

        // When the charger request is forwarded to Redis, inject the CSMS response.
        _senderMock
            .Setup(q => q.SendAsync(TEST_REQUEST_QUEUE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns<string, byte[], CancellationToken>((_, _, _) =>
            {
                PushInbound(BuildCsmsResponse(TEST_CLIENT_ID, csmsResponse));
                return Task.CompletedTask;
            });

        var callCount = 0;
        var wsMock = new Mock<WebSocket>();
        wsMock.Setup(ws => ws.State).Returns(WebSocketState.Open);
        wsMock.Setup(ws => ws.ReceiveAsync(
                It.IsAny<ArraySegment<byte>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (ArraySegment<byte> buffer, CancellationToken ct) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    requestBytes.CopyTo(buffer.Array!, buffer.Offset);
                    return new WebSocketReceiveResult(requestBytes.Length, WebSocketMessageType.Text, true);
                }
                // Wait long enough for the subscription pump to deliver the CSMS response.
                await Task.Delay(100, ct);
                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
                    WebSocketCloseStatus.NormalClosure, "done");
            });
        wsMock.Setup(ws => ws.CloseStatus)
            .Returns(() => callCount >= 2 ? WebSocketCloseStatus.NormalClosure : null);

        var sentToWs = WireWebSocketSendCapture(wsMock);

        await using var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        _senderMock.Verify(
            q => q.SendAsync(TEST_REQUEST_QUEUE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(sentToWs);
        Assert.Equal(csmsResponse, Encoding.UTF8.GetString(sentToWs[0]));
    }

    // Scenario: CSMS initiates a Call to the charger, charger responds with CallResult.
    // The charger's response must be correlated and forwarded back to Redis.
    [Fact]
    public async Task ForwardChargerResponseAfterCsmsInitiatedRequest()
    {
        var csmsRequest = CreateOcppCall("out-1", "Reset");
        var chargerResponse = CreateOcppCallResult("out-1");
        var responseBytes = Encoding.UTF8.GetBytes(chargerResponse);

        // Pre-queue the CSMS request so the pump delivers it as soon as we subscribe.
        PushInbound(BuildCsmsRequest(TEST_CLIENT_ID, csmsRequest));

        var callCount = 0;
        var wsMock = new Mock<WebSocket>();
        wsMock.Setup(ws => ws.State).Returns(WebSocketState.Open);
        wsMock.Setup(ws => ws.ReceiveAsync(
                It.IsAny<ArraySegment<byte>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (ArraySegment<byte> buffer, CancellationToken ct) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // Give the pump time to deliver csmsRequest and set _processingOutboundId.
                    await Task.Delay(60, ct);
                    responseBytes.CopyTo(buffer.Array!, buffer.Offset);
                    return new WebSocketReceiveResult(
                        responseBytes.Length, WebSocketMessageType.Text, true);
                }
                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
                    WebSocketCloseStatus.NormalClosure, "done");
            });
        wsMock.Setup(ws => ws.CloseStatus)
            .Returns(() => callCount >= 2 ? WebSocketCloseStatus.NormalClosure : null);

        var sentToWs = WireWebSocketSendCapture(wsMock);

        await using var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        Assert.Single(sentToWs);
        Assert.Equal(csmsRequest, Encoding.UTF8.GetString(sentToWs[0]));
        _senderMock.Verify(
            q => q.SendAsync(TEST_REQUEST_QUEUE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Scenario: Charger sends two Calls before receiving a response to the first.
    // Second request must be queued. Once CSMS responds to the first, the second is drained.
    [Fact]
    public async Task QueueSecondChargerRequestUntilFirstIsAnswered()
    {
        var req1 = CreateOcppCall("req-1", "BootNotification");
        var req2 = CreateOcppCall("req-2", "Heartbeat");
        var csmsResponse1 = CreateOcppCallResult("req-1");
        var bytes1 = Encoding.UTF8.GetBytes(req1);
        var bytes2 = Encoding.UTF8.GetBytes(req2);

        var rabbitSendCount = 0;
        _senderMock
            .Setup(q => q.SendAsync(TEST_REQUEST_QUEUE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], CancellationToken>((_, _, _) => rabbitSendCount++)
            .Returns(Task.CompletedTask);

        var callCount = 0;
        var wsMock = new Mock<WebSocket>();
        wsMock.Setup(ws => ws.State).Returns(WebSocketState.Open);
        wsMock.Setup(ws => ws.ReceiveAsync(
                It.IsAny<ArraySegment<byte>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (ArraySegment<byte> buffer, CancellationToken ct) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    bytes1.CopyTo(buffer.Array!, buffer.Offset);
                    return new WebSocketReceiveResult(bytes1.Length, WebSocketMessageType.Text, true);
                }
                if (callCount == 2)
                {
                    bytes2.CopyTo(buffer.Array!, buffer.Offset);
                    return new WebSocketReceiveResult(bytes2.Length, WebSocketMessageType.Text, true);
                }
                // Inject CSMS response to req-1, then wait for DrainInbound to fire before closing.
                PushInbound(BuildCsmsResponse(TEST_CLIENT_ID, csmsResponse1));
                await Task.Delay(150, ct);
                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
                    WebSocketCloseStatus.NormalClosure, "done");
            });
        wsMock.Setup(ws => ws.CloseStatus)
            .Returns(() => callCount >= 3 ? WebSocketCloseStatus.NormalClosure : null);

        var sentToWs = WireWebSocketSendCapture(wsMock);

        await using var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        // req-1 forwarded immediately, req-2 drained after CSMS responds = 2 sends.
        Assert.Equal(2, rabbitSendCount);
        // CSMS response to req-1 forwarded back to charger.
        Assert.Single(sentToWs);
        Assert.Equal(csmsResponse1, Encoding.UTF8.GetString(sentToWs[0]));
    }

    // Scenario: CSMS sends two Calls before the charger responds to the first.
    // Second request must be queued in the outbound queue. Once the charger responds
    // to the first, the second is drained and sent to the charger.
    [Fact]
    public async Task QueueSecondCsmsRequestUntilChargerRespondsToFirst()
    {
        var csmsReq1 = CreateOcppCall("out-1", "Reset");
        var csmsReq2 = CreateOcppCall("out-2", "GetVariables");
        var chargerResponse = CreateOcppCallResult("out-1");
        var responseBytes = Encoding.UTF8.GetBytes(chargerResponse);

        // Pre-queue both CSMS requests. The pump delivers them in order.
        PushInbound(BuildCsmsRequest(TEST_CLIENT_ID, csmsReq1));
        PushInbound(BuildCsmsRequest(TEST_CLIENT_ID, csmsReq2));

        var callCount = 0;
        var wsMock = new Mock<WebSocket>();
        wsMock.Setup(ws => ws.State).Returns(WebSocketState.Open);
        wsMock.Setup(ws => ws.ReceiveAsync(
                It.IsAny<ArraySegment<byte>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (ArraySegment<byte> buffer, CancellationToken ct) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // Wait for the pump to deliver both CSMS requests.
                    await Task.Delay(100, ct);
                    responseBytes.CopyTo(buffer.Array!, buffer.Offset);
                    return new WebSocketReceiveResult(
                        responseBytes.Length, WebSocketMessageType.Text, true);
                }
                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
                    WebSocketCloseStatus.NormalClosure, "done");
            });
        wsMock.Setup(ws => ws.CloseStatus)
            .Returns(() => callCount >= 2 ? WebSocketCloseStatus.NormalClosure : null);

        var sentToWs = WireWebSocketSendCapture(wsMock);

        await using var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        // Both CSMS requests reached charger: out-1 directly, out-2 after drain.
        Assert.Equal(2, sentToWs.Count);
        Assert.Equal(csmsReq1, Encoding.UTF8.GetString(sentToWs[0]));
        Assert.Equal(csmsReq2, Encoding.UTF8.GetString(sentToWs[1]));

        _senderMock.Verify(
            q => q.SendAsync(TEST_REQUEST_QUEUE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Scenario: Full duplex — charger and CSMS each have an in-flight request simultaneously.
    //
    // Timeline:
    //   1. Charger sends  [2,"req-1","BootNotification",{}]  → _processingInboundId = "req-1"
    //   2. CSMS   sends  [2,"out-1","Reset",{}]              → _processingOutboundId = "out-1"
    //   3. Charger responds [3,"out-1",{}]                    → clears outbound slot
    //   4. CSMS   responds [3,"req-1",{}]                     → clears inbound slot
    [Fact]
    public async Task HandleBidirectionalMessagesWithIndependentSlots()
    {
        var chargerReq = CreateOcppCall("req-1", "BootNotification");
        var csmsReq = CreateOcppCall("out-1", "Reset");
        var chargerResp = CreateOcppCallResult("out-1");
        var csmsResp = CreateOcppCallResult("req-1");

        var reqBytes = Encoding.UTF8.GetBytes(chargerReq);
        var respBytes = Encoding.UTF8.GetBytes(chargerResp);

        var rabbitSendCount = 0;
        _senderMock
            .Setup(q => q.SendAsync(TEST_REQUEST_QUEUE, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns<string, byte[], CancellationToken>((_, _, _) =>
            {
                rabbitSendCount++;
                if (rabbitSendCount == 1)
                    PushInbound(BuildCsmsRequest(TEST_CLIENT_ID, csmsReq));
                else if (rabbitSendCount == 2)
                    PushInbound(BuildCsmsResponse(TEST_CLIENT_ID, csmsResp));
                return Task.CompletedTask;
            });

        var callCount = 0;
        var wsMock = new Mock<WebSocket>();
        wsMock.Setup(ws => ws.State).Returns(WebSocketState.Open);
        wsMock.Setup(ws => ws.ReceiveAsync(
                It.IsAny<ArraySegment<byte>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (ArraySegment<byte> buffer, CancellationToken ct) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // Step 1: charger sends its request.
                    reqBytes.CopyTo(buffer.Array!, buffer.Offset);
                    return new WebSocketReceiveResult(reqBytes.Length, WebSocketMessageType.Text, true);
                }
                if (callCount == 2)
                {
                    // Wait for the pump to deliver csmsReq and set _processingOutboundId.
                    await Task.Delay(60, ct);
                    // Step 3: charger responds to CSMS request.
                    respBytes.CopyTo(buffer.Array!, buffer.Offset);
                    return new WebSocketReceiveResult(respBytes.Length, WebSocketMessageType.Text, true);
                }
                // Wait for the pump to deliver csmsResp to charger.
                await Task.Delay(60, ct);
                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
                    WebSocketCloseStatus.NormalClosure, "done");
            });
        wsMock.Setup(ws => ws.CloseStatus)
            .Returns(() => callCount >= 3 ? WebSocketCloseStatus.NormalClosure : null);

        var sentToWs = WireWebSocketSendCapture(wsMock);

        await using var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        // Two messages sent to charger: CSMS request (out-1), then CSMS response (req-1).
        Assert.Equal(2, sentToWs.Count);
        Assert.Equal(csmsReq, Encoding.UTF8.GetString(sentToWs[0]));
        Assert.Equal(csmsResp, Encoding.UTF8.GetString(sentToWs[1]));

        // Two messages sent to core: charger request (req-1), then charger response (out-1).
        Assert.Equal(2, rabbitSendCount);
    }
}
