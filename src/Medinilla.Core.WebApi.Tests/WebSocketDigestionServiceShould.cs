using Google.Protobuf;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.WebApi.Services;
using Medinilla.RealTime;
using Medinilla.RealTime.Redis;
using Medinilla.WebApi.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Xunit.Abstractions;

namespace Medinilla.Core.WebApi.Tests;

public class WebSocketDigestionServiceShould
{
    private readonly ITestOutputHelper _testOutputHelper;

    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IConfigurationSection> _commsSectionMock;
    private readonly Mock<IConfigurationSection> _generalSectionMock;
    private readonly Mock<ILogger<WebSocketDigestionService>> _loggerMock;
    private readonly Mock<ILogger<InternalCommunicationService>> _commsLoggerMock;
    private readonly Mock<IReceiver> _receiverMock;
    private readonly Mock<ISender> _senderMock;

    // Inbound channel synchronization: RunCommsChannel blocks here until PushInbound() releases it.
    private readonly ConcurrentQueue<byte[]?> _inboundMessages = new();
    private readonly SemaphoreSlim _inboundSemaphore = new(0);

    private const string TEST_CLIENT_ID = "TEST-CHARGER-001";
    private const string TEST_REQUEST_QUEUE = "test-request-queue";
    private const string TEST_RESPONSE_QUEUE = "test-response-queue";

    // "test-response-queue-TEST-CHARGER-001"
    private static readonly string TEST_INBOUND_QUEUE =
        RedisUtils.BuildChannelName(TEST_RESPONSE_QUEUE, TEST_CLIENT_ID);

    public WebSocketDigestionServiceShould(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        _configMock = new Mock<IConfiguration>();
        _commsSectionMock = new Mock<IConfigurationSection>();
        _generalSectionMock = new Mock<IConfigurationSection>();
        _loggerMock = new Mock<ILogger<WebSocketDigestionService>>();
        _commsLoggerMock = new Mock<ILogger<InternalCommunicationService>>();
        _receiverMock = new Mock<IReceiver>();
        _senderMock = new Mock<ISender>();

        _commsSectionMock.Setup(s => s["RequestQueue"]).Returns(TEST_REQUEST_QUEUE);
        _commsSectionMock.Setup(s => s["ResponseQueue"]).Returns(TEST_RESPONSE_QUEUE);
        _configMock.Setup(c => c.GetSection("Comms")).Returns(_commsSectionMock.Object);

        _generalSectionMock.Setup(s => s["MessageQueueTTL"]).Returns("5");
        _configMock.Setup(c => c.GetSection("General")).Returns(_generalSectionMock.Object);

        // ReceiveAsync blocks on the semaphore until PushInbound() releases it.
        _receiverMock
            .Setup(q => q.ReceiveAsync(TEST_INBOUND_QUEUE, It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (_, ct) =>
            {
                await _inboundSemaphore.WaitAsync(ct);
                _inboundMessages.TryDequeue(out var msg);
                return msg;
            });

        _senderMock
            .Setup(q => q.SendAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ----------------------------------------------------------------
    // Helpers — inbound channel control
    // ----------------------------------------------------------------

    /// <summary>Delivers a message to the next ReceiveAsync call.</summary>
    private void PushInbound(byte[] message)
    {
        _inboundMessages.Enqueue(message);
        _inboundSemaphore.Release();
    }

    // ----------------------------------------------------------------
    // Helpers — OCPP message builders
    // ----------------------------------------------------------------

    private static string CreateOcppCall(string messageId, string action, string payload = "{}")
        => $"[2,\"{messageId}\",\"{action}\",{payload}]";

    private static string CreateOcppCallResult(string messageId, string payload = "{}")
        => $"[3,\"{messageId}\",{payload}]";

    /// <summary>Bytes returned by ReceiveAsync when CSMS answers a charger request.</summary>
    private static byte[] BuildCsmsResponseBytes(string clientId, string ocppCallResult)
    {
        return new Comms
        {
            MessageType = CommsMessageType.OcppResponse,
            ClientIdentifier = clientId,
            Payload = ByteString.CopyFrom(Encoding.UTF8.GetBytes(ocppCallResult)),
        }.ToByteArray();
    }

    /// <summary>Bytes returned by ReceiveAsync when CSMS initiates a request to the charger.</summary>
    private static byte[] BuildCsmsRequestBytes(string clientId, string ocppCall)
    {
        return new Comms
        {
            MessageType = CommsMessageType.OcppRequest,
            ClientIdentifier = clientId,
            Payload = ByteString.CopyFrom(Encoding.UTF8.GetBytes(ocppCall)),
        }.ToByteArray();
    }

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

    private InternalCommunicationService CreateCommsService() =>
        new(_senderMock.Object, _receiverMock.Object, _commsLoggerMock.Object);

    private IMessageQueueFactory CreateQueueFactory() =>
        new MessageQueueFactory(_configMock.Object);

    private WebSocketDigestionService CreateService() =>
        new(_configMock.Object, _loggerMock.Object, CreateCommsService(), CreateQueueFactory());

    // ================================================================
    // Constructor validation
    // ================================================================

    [Fact]
    public void ThrowOnNullConfiguration()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSocketDigestionService(null!, _loggerMock.Object, CreateCommsService(), CreateQueueFactory()));
    }

    [Fact]
    public void ThrowOnNullLogger()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSocketDigestionService(_configMock.Object, null!, CreateCommsService(), CreateQueueFactory()));
    }

    [Fact]
    public void ThrowOnNullComms()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSocketDigestionService(_configMock.Object, _loggerMock.Object, null!, CreateQueueFactory()));
    }

    [Fact]
    public void ThrowOnNullQueueFactory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSocketDigestionService(_configMock.Object, _loggerMock.Object, CreateCommsService(), (IMessageQueueFactory)null!));
    }

    [Fact]
    public void ThrowOnNullSender()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new InternalCommunicationService(null!, _receiverMock.Object, _commsLoggerMock.Object));
    }

    [Fact]
    public void ThrowOnNullReceiver()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new InternalCommunicationService(_senderMock.Object, null!, _commsLoggerMock.Object));
    }

    [Fact]
    public void ThrowOnNullCommsLogger()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new InternalCommunicationService(_senderMock.Object, _receiverMock.Object, null!));
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
    public async Task ThrowWhenResponseQueueNotConfigured()
    {
        _commsSectionMock.Setup(s => s["ResponseQueue"]).Returns((string?)null);
        var wsMock = new Mock<WebSocket>();
        await using var service = CreateService();
        await Assert.ThrowsAsync<InvalidOperationException>(
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
                PushInbound(BuildCsmsResponseBytes(TEST_CLIENT_ID, csmsResponse));
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
                // Wait long enough for RunCommsChannel to deliver the CSMS response.
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

        // Pre-queue the CSMS request so RunCommsChannel delivers it immediately.
        PushInbound(BuildCsmsRequestBytes(TEST_CLIENT_ID, csmsRequest));

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
                    // Give RunCommsChannel time to deliver csmsRequest and set _processingOutboundId.
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
                PushInbound(BuildCsmsResponseBytes(TEST_CLIENT_ID, csmsResponse1));
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

        // Pre-queue both CSMS requests. RunCommsChannel processes them in order.
        PushInbound(BuildCsmsRequestBytes(TEST_CLIENT_ID, csmsReq1));
        PushInbound(BuildCsmsRequestBytes(TEST_CLIENT_ID, csmsReq2));

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
                    // Wait for RunCommsChannel to deliver both CSMS requests.
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
                    PushInbound(BuildCsmsRequestBytes(TEST_CLIENT_ID, csmsReq));
                else if (rabbitSendCount == 2)
                    PushInbound(BuildCsmsResponseBytes(TEST_CLIENT_ID, csmsResp));
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
                    // Wait for RunCommsChannel to deliver csmsReq and set _processingOutboundId.
                    await Task.Delay(60, ct);
                    // Step 3: charger responds to CSMS request.
                    respBytes.CopyTo(buffer.Array!, buffer.Offset);
                    return new WebSocketReceiveResult(respBytes.Length, WebSocketMessageType.Text, true);
                }
                // Wait for RunCommsChannel to deliver csmsResp to charger.
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
