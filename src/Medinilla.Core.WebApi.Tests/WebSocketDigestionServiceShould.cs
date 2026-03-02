using Google.Protobuf;
using Medinilla.Core.SharedContracts.Comms;
using Medinilla.Core.SharedContracts.Comms.Ocpp;
using Medinilla.RealTime;
using Medinilla.WebApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Net.WebSockets;
using System.Text;
using Xunit.Abstractions;

namespace Medinilla.Core.WebApi.Tests;

public class WebSocketDigestionServiceShould
{
    private readonly ITestOutputHelper _testOutputHelper;

    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IConfigurationSection> _commsSectionMock;
    private readonly Mock<ILogger<WebSocketDigestionService>> _loggerMock;
    private readonly Mock<ICommunicationProvider> _commsProviderMock;
    private readonly Mock<IRealTimeMessenger> _messengerMock;

    private Func<object, Task>? _capturedRabbitHandler;

    private const string TEST_CLIENT_ID = "TEST-CHARGER-001";
    private const string TEST_REQUEST_QUEUE = "test-request-queue";
    private const string TEST_RESPONSE_QUEUE = "test-response-queue";

    public WebSocketDigestionServiceShould(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        _configMock = new Mock<IConfiguration>();
        _commsSectionMock = new Mock<IConfigurationSection>();
        _loggerMock = new Mock<ILogger<WebSocketDigestionService>>();
        _commsProviderMock = new Mock<ICommunicationProvider>();
        _messengerMock = new Mock<IRealTimeMessenger>();

        _commsSectionMock.Setup(s => s["RequestQueue"]).Returns(TEST_REQUEST_QUEUE);
        _commsSectionMock.Setup(s => s["ResponseQueue"]).Returns(TEST_RESPONSE_QUEUE);
        _configMock.Setup(c => c.GetSection("Comms")).Returns(_commsSectionMock.Object);

        _commsProviderMock.Setup(p => p.GetMessenger("RabbitMQ")).Returns(_messengerMock.Object);

        _messengerMock.Setup(m => m.RegisterChannel(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _messengerMock.Setup(m => m.RegisterHandler(
                It.IsAny<string>(), It.IsAny<Func<object, Task>>()))
            .Callback<string, Func<object, Task>>((_, handler) => _capturedRabbitHandler = handler)
            .Returns(Task.CompletedTask);

        _messengerMock.Setup(m => m.SendMessage(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);
    }

    private WebSocketDigestionService CreateService()
    {
        return new WebSocketDigestionService(
            _configMock.Object,
            _loggerMock.Object,
            _commsProviderMock.Object);
    }

    // --- OCPP message helpers ---

    private static string CreateOcppCall(string messageId, string action, string payload = "{}")
        => $"[2,\"{messageId}\",\"{action}\",{payload}]";

    private static string CreateOcppCallResult(string messageId, string payload = "{}")
        => $"[3,\"{messageId}\",{payload}]";

    /// <summary>
    /// Builds the Dictionary{ea → BasicDeliverEventArgs} that RunCommsChannel expects,
    /// wrapping an OCPP message through the Comms → WampResult protobuf envelope.
    /// </summary>
    private static Dictionary<string, object> BuildRabbitDelivery(string clientId, string ocppMessage)
    {
        var messageBytes = Encoding.UTF8.GetBytes(ocppMessage);

        var wampResult = new WampResult
        {
            Result = ByteString.CopyFrom(messageBytes),
            ClientIdentifier = clientId
        };

        var comms = new Comms
        {
            MessageType = CommsMessageType.OcppResponse,
            Payload = ByteString.CopyFrom(wampResult.ToByteArray())
        };

        var ea = new BasicDeliverEventArgs(
            "", 0UL, false, "", "test",
            new Mock<IReadOnlyBasicProperties>().Object,
            comms.ToByteArray(),
            CancellationToken.None);

        return new Dictionary<string, object> { ["ea"] = ea };
    }

    // --- WebSocket mock helpers ---

    /// <summary>
    /// Creates a WebSocket mock that immediately returns a close frame.
    /// Useful for tests that only care about setup/teardown, not message flow.
    /// </summary>
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

    /// <summary>
    /// Adds standard close/send tracking to a WebSocket mock.
    /// Returns the list that captures bytes sent to the charger via SendAsync.
    /// </summary>
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

    // ================================================================
    // Constructor validation
    // ================================================================

    [Fact]
    public void ThrowOnNullConfiguration()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSocketDigestionService(null!, _loggerMock.Object, _commsProviderMock.Object));
    }

    [Fact]
    public void ThrowOnNullLogger()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSocketDigestionService(_configMock.Object, null!, _commsProviderMock.Object));
    }

    [Fact]
    public void ThrowOnNullCommsProvider()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WebSocketDigestionService(_configMock.Object, _loggerMock.Object, null!));
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
    public async Task ThrowWhenMessengerIsNull()
    {
        _commsProviderMock
            .Setup(p => p.GetMessenger("RabbitMQ"))
            .Returns((IRealTimeMessenger?)null);

        var wsMock = new Mock<WebSocket>();
        await using var service = CreateService();
        await Assert.ThrowsAsync<InvalidOperationException>(
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
    public async Task RegisterChannelsOnConsume()
    {
        var wsMock = CreateClosingWebSocketMock();
        await using var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        _messengerMock.Verify(m => m.RegisterChannel(TEST_REQUEST_QUEUE), Times.Once);
        _messengerMock.Verify(m => m.RegisterChannel(TEST_RESPONSE_QUEUE), Times.Once);
        _messengerMock.Verify(m => m.RegisterHandler(
            TEST_RESPONSE_QUEUE, It.IsAny<Func<object, Task>>()), Times.Once);
    }

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
        var wsMock = new Mock<WebSocket>();
        wsMock.Setup(ws => ws.State).Returns(WebSocketState.Open);
        wsMock.Setup(ws => ws.ReceiveAsync(
                It.IsAny<ArraySegment<byte>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WebSocketException("Connection lost"));
        wsMock.Setup(ws => ws.CloseAsync(
                It.IsAny<WebSocketCloseStatus>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        // Service should handle the exception and exit gracefully
        _messengerMock.Verify(m => m.SendMessage(
            It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
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

        _messengerMock.Verify(m => m.SendMessage(
            It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
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

        // When charger's request reaches rabbit, simulate CSMS responding immediately
        _messengerMock.Setup(m => m.SendMessage(TEST_REQUEST_QUEUE, It.IsAny<byte[]>()))
            .Returns<string, byte[]>(async (_, _) =>
            {
                await _capturedRabbitHandler!(
                    BuildRabbitDelivery(TEST_CLIENT_ID, csmsResponse));
            });

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

        var sentToWs = WireWebSocketSendCapture(wsMock);

        await using var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        // Charger's request was forwarded to core
        _messengerMock.Verify(
            m => m.SendMessage(TEST_REQUEST_QUEUE, It.IsAny<byte[]>()), Times.Once);

        // CSMS response was forwarded back to charger
        Assert.Single(sentToWs);
        Assert.Equal(csmsResponse, Encoding.UTF8.GetString(sentToWs[0]));
    }

    // Scenario: CSMS initiates a Call to the charger, charger responds with CallResult.
    // The charger's response must be correlated and forwarded back to rabbit.
    [Fact]
    public async Task ForwardChargerResponseAfterCsmsInitiatedRequest()
    {
        var csmsRequest = CreateOcppCall("out-1", "Reset");
        var chargerResponse = CreateOcppCallResult("out-1");
        var responseBytes = Encoding.UTF8.GetBytes(chargerResponse);

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
                    // CSMS sends request to charger via rabbit (sets _processingOutboundId)
                    await _capturedRabbitHandler!(
                        BuildRabbitDelivery(TEST_CLIENT_ID, csmsRequest));

                    // Then charger delivers its response via WebSocket
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

        // CSMS request was forwarded to charger
        Assert.Single(sentToWs);
        Assert.Equal(csmsRequest, Encoding.UTF8.GetString(sentToWs[0]));

        // Charger response was forwarded to core (matched _processingOutboundId)
        _messengerMock.Verify(
            m => m.SendMessage(TEST_REQUEST_QUEUE, It.IsAny<byte[]>()), Times.Once);
    }

    // Scenario: Charger sends two Calls before receiving a response to the first.
    // Second request must be queued. Once CSMS responds to the first, the second
    // is drained from the queue and forwarded.
    [Fact]
    public async Task QueueSecondChargerRequestUntilFirstIsAnswered()
    {
        var req1 = CreateOcppCall("req-1", "BootNotification");
        var req2 = CreateOcppCall("req-2", "Heartbeat");
        var csmsResponse1 = CreateOcppCallResult("req-1");
        var bytes1 = Encoding.UTF8.GetBytes(req1);
        var bytes2 = Encoding.UTF8.GetBytes(req2);

        var rabbitSendCount = 0;
        _messengerMock.Setup(m => m.SendMessage(TEST_REQUEST_QUEUE, It.IsAny<byte[]>()))
            .Callback<string, byte[]>((_, _) => rabbitSendCount++)
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
                    return new WebSocketReceiveResult(
                        bytes1.Length, WebSocketMessageType.Text, true);
                }
                if (callCount == 2)
                {
                    // Protocol violation: charger sends req-2 while req-1 is still pending
                    bytes2.CopyTo(buffer.Array!, buffer.Offset);
                    return new WebSocketReceiveResult(
                        bytes2.Length, WebSocketMessageType.Text, true);
                }
                // Before closing, CSMS responds to req-1 → clears slot → drains req-2
                await _capturedRabbitHandler!(
                    BuildRabbitDelivery(TEST_CLIENT_ID, csmsResponse1));

                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
                    WebSocketCloseStatus.NormalClosure, "done");
            });
        wsMock.Setup(ws => ws.CloseStatus)
            .Returns(() => callCount >= 3 ? WebSocketCloseStatus.NormalClosure : null);

        var sentToWs = WireWebSocketSendCapture(wsMock);

        await using var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        // req-1 forwarded immediately, req-2 drained after CSMS responds = 2 sends
        Assert.Equal(2, rabbitSendCount);

        // CSMS response to req-1 forwarded back to charger
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
                    // CSMS sends two requests; second gets queued
                    await _capturedRabbitHandler!(
                        BuildRabbitDelivery(TEST_CLIENT_ID, csmsReq1));
                    await _capturedRabbitHandler!(
                        BuildRabbitDelivery(TEST_CLIENT_ID, csmsReq2));

                    // Charger responds to out-1 → clears slot → drains out-2
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

        // Both CSMS requests reached charger: out-1 directly, out-2 after drain
        Assert.Equal(2, sentToWs.Count);
        Assert.Equal(csmsReq1, Encoding.UTF8.GetString(sentToWs[0]));
        Assert.Equal(csmsReq2, Encoding.UTF8.GetString(sentToWs[1]));

        // Charger response was forwarded to core
        _messengerMock.Verify(
            m => m.SendMessage(TEST_REQUEST_QUEUE, It.IsAny<byte[]>()), Times.Once);
    }

    // Scenario: Full duplex — charger and CSMS each have an in-flight request simultaneously.
    // The two slots (_processingInboundId and _processingOutboundId) must be independent.
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

        // On first rabbit send (charger req-1 forwarded): CSMS sends out-1
        // On second rabbit send (charger response forwarded): CSMS responds to req-1
        _messengerMock.Setup(m => m.SendMessage(TEST_REQUEST_QUEUE, It.IsAny<byte[]>()))
            .Returns<string, byte[]>(async (_, _) =>
            {
                rabbitSendCount++;
                if (rabbitSendCount == 1)
                {
                    await _capturedRabbitHandler!(
                        BuildRabbitDelivery(TEST_CLIENT_ID, csmsReq));
                }
                else if (rabbitSendCount == 2)
                {
                    await _capturedRabbitHandler!(
                        BuildRabbitDelivery(TEST_CLIENT_ID, csmsResp));
                }
            });

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
                    // Step 1: charger sends its request
                    reqBytes.CopyTo(buffer.Array!, buffer.Offset);
                    return Task.FromResult(new WebSocketReceiveResult(
                        reqBytes.Length, WebSocketMessageType.Text, true));
                }
                if (callCount == 2)
                {
                    // Step 3: charger responds to CSMS request
                    respBytes.CopyTo(buffer.Array!, buffer.Offset);
                    return Task.FromResult(new WebSocketReceiveResult(
                        respBytes.Length, WebSocketMessageType.Text, true));
                }
                return Task.FromResult(new WebSocketReceiveResult(
                    0, WebSocketMessageType.Close, true,
                    WebSocketCloseStatus.NormalClosure, "done"));
            });
        wsMock.Setup(ws => ws.CloseStatus)
            .Returns(() => callCount >= 3 ? WebSocketCloseStatus.NormalClosure : null);

        var sentToWs = WireWebSocketSendCapture(wsMock);

        await using var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        // Two messages sent to charger: CSMS request (out-1), then CSMS response (req-1)
        Assert.Equal(2, sentToWs.Count);
        Assert.Equal(csmsReq, Encoding.UTF8.GetString(sentToWs[0]));
        Assert.Equal(csmsResp, Encoding.UTF8.GetString(sentToWs[1]));

        // Two messages sent to core: charger request (req-1), then charger response (out-1)
        Assert.Equal(2, rabbitSendCount);
    }

    // Scenario: CSMS response arrives for a different client — must be ignored.
    [Fact]
    public async Task IgnoreResponseForDifferentClient()
    {
        var chargerRequest = CreateOcppCall("req-1", "BootNotification");
        var wrongClientResponse = CreateOcppCallResult("req-1");
        var requestBytes = Encoding.UTF8.GetBytes(chargerRequest);

        _messengerMock.Setup(m => m.SendMessage(TEST_REQUEST_QUEUE, It.IsAny<byte[]>()))
            .Returns<string, byte[]>(async (_, _) =>
            {
                // Response arrives but for a DIFFERENT client
                await _capturedRabbitHandler!(
                    BuildRabbitDelivery("WRONG-CHARGER-999", wrongClientResponse));
            });

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
                    return Task.FromResult(new WebSocketReceiveResult(
                        requestBytes.Length, WebSocketMessageType.Text, true));
                }
                return Task.FromResult(new WebSocketReceiveResult(
                    0, WebSocketMessageType.Close, true,
                    WebSocketCloseStatus.NormalClosure, "done"));
            });
        wsMock.Setup(ws => ws.CloseStatus)
            .Returns(() => callCount >= 2 ? WebSocketCloseStatus.NormalClosure : null);

        var sentToWs = WireWebSocketSendCapture(wsMock);

        await using var service = CreateService();
        await service.Consume(wsMock.Object, TEST_CLIENT_ID);

        // Nothing should have been sent back to charger — wrong client was ignored
        Assert.Empty(sentToWs);
    }
}
