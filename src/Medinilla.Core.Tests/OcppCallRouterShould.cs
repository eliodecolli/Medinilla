using Medinilla.Core.Actions;
using Medinilla.Core.Commands;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.v1;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using Xunit.Abstractions;

namespace Medinilla.Core.Tests;

public class OcppCallRouterShould
{
    private readonly ITestOutputHelper _testOutputHelper;

    private readonly Mock<ILogger<OcppCallRouter>> _loggerMock = new();
    private readonly Mock<IOcppActionsFactory> _actionsFactoryMock = new();
    private readonly Mock<IOcppChargerCommandFactory> _commandsFactoryMock = new();
    private readonly Mock<IRouterServices> _routerServicesMock = new();

    private const string CLIENT_ID = "TEST-CHARGER-001";
    private const string KNOWN_ACTION = "Heartbeat";
    private const string UNKNOWN_ACTION = "MysteryAction";

    public OcppCallRouterShould(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        _routerServicesMock
            .Setup(s => s.ValidateChargingStationAvailability(CLIENT_ID))
            .ReturnsAsync(true);

        _routerServicesMock
            .Setup(s => s.DisconnectClient(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
    }

    private (OcppCallRouter Router, FakeRoutingTable Table) CreateSut()
    {
        var table = new FakeRoutingTable();
        var router = new OcppCallRouter(
            _loggerMock.Object,
            _routerServicesMock.Object,
            _actionsFactoryMock.Object,
            _commandsFactoryMock.Object);
        router.InitializeRoutingTable(table);
        return (router, table);
    }

    // ----------------------------------------------------------------
    // CALL → Action dispatch
    // ----------------------------------------------------------------

    [Fact]
    public async Task RouteOcppCall_ForBootNotification_BypassesAvailabilityValidation()
    {
        var fakeAction = new FakeAction { ActionName = "BootNotification" };
        _actionsFactoryMock.Setup(f => f.GetAction("BootNotification")).Returns(fakeAction);

        var (sut, _) = CreateSut();
        var frame = Encoding.UTF8.GetBytes(BuildCall("msg-1", "BootNotification", "{}"));

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.NotNull(result);
        Assert.Same(fakeAction.ExecutedCall, fakeAction.ExecutedCall);
        _routerServicesMock.Verify(s => s.ValidateChargingStationAvailability(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RouteOcppCall_ForKnownAction_InvokesActionAndReturnsItsResult()
    {
        var fakeAction = new FakeAction
        {
            ActionName = KNOWN_ACTION,
            ResultToReturn = new RpcResult
            {
                Result = new OcppCallResult("msg-1", "{\"ok\":true}"),
                Error = null,
                ReturnToCS = true,
            }
        };
        _actionsFactoryMock.Setup(f => f.GetAction(KNOWN_ACTION)).Returns(fakeAction);

        var (sut, _) = CreateSut();
        var frame = Encoding.UTF8.GetBytes(BuildCall("msg-1", KNOWN_ACTION, "{}"));

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.NotNull(result);
        Assert.NotNull(result.Result);
        Assert.Equal("{\"ok\":true}", result.Result.Payload);
        Assert.True(result.ReturnToCS);
        Assert.Equal(1, fakeAction.ExecuteCallCount);
    }

    [Fact]
    public async Task RouteOcppCall_ForUnknownAction_ReturnsNotImplementedError()
    {
        _actionsFactoryMock.Setup(f => f.GetAction(UNKNOWN_ACTION)).Returns((IOcppAction?)null);

        var (sut, _) = CreateSut();
        var frame = Encoding.UTF8.GetBytes(BuildCall("msg-1", UNKNOWN_ACTION, "{}"));

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.NotNull(result);
        Assert.NotNull(result.Error);
        Assert.Null(result.Result);
        Assert.True(result.ReturnToCS);
        Assert.Equal(OcppCallError.ErrorCodes.NotImplemented, result.Error.ErrorCode);
    }

    [Fact]
    public async Task RouteOcppCall_WhenAvailabilityValidationFails_ReturnsSecurityError()
    {
        _actionsFactoryMock.Setup(f => f.GetAction(KNOWN_ACTION)).Returns(new FakeAction { ActionName = KNOWN_ACTION });
        _routerServicesMock
            .Setup(s => s.ValidateChargingStationAvailability(CLIENT_ID))
            .ReturnsAsync(false);

        var (sut, _) = CreateSut();
        var frame = Encoding.UTF8.GetBytes(BuildCall("msg-1", KNOWN_ACTION, "{}"));

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.NotNull(result);
        Assert.NotNull(result.Error);
        Assert.Equal(OcppCallError.ErrorCodes.SecurityError, result.Error.ErrorCode);
    }

    [Fact]
    public async Task RouteOcppCall_WhenActionThrows_ReturnsInternalError()
    {
        var fakeAction = new FakeAction { ActionName = KNOWN_ACTION, ThrowOnExecute = new InvalidOperationException("boom") };
        _actionsFactoryMock.Setup(f => f.GetAction(KNOWN_ACTION)).Returns(fakeAction);

        var (sut, _) = CreateSut();
        var frame = Encoding.UTF8.GetBytes(BuildCall("msg-1", KNOWN_ACTION, "{}"));

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.NotNull(result);
        Assert.NotNull(result.Error);
        Assert.Equal(OcppCallError.ErrorCodes.InternalError, result.Error.ErrorCode);
        Assert.True(result.ReturnToCS);
    }

    [Fact]
    public async Task RouteOcppCall_ForMalformedFrame_ReturnsNull()
    {
        var (sut, _) = CreateSut();
        var frame = Encoding.UTF8.GetBytes("[2,\"broken");

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Null(result);
    }

    // ----------------------------------------------------------------
    // CALL_RESULT → Command dispatch
    // ----------------------------------------------------------------

    [Fact]
    public async Task RouteOcppCall_ForCallResultWithKnownAction_DispatchesToCommand()
    {
        var fakeCommand = new FakeCommand("Reset");
        _commandsFactoryMock.Setup(f => f.GetCommand("Reset")).Returns(fakeCommand);

        var (sut, table) = CreateSut();
        await table.Add("Reset-abc123", "Reset");

        var frame = Encoding.UTF8.GetBytes(BuildCallResult("Reset-abc123", "{\"status\":\"Accepted\"}"));

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Null(result);
        Assert.Equal(1, fakeCommand.HandleResponseCallCount);
        Assert.Equal("{\"status\":\"Accepted\"}", fakeCommand.LastResponsePayload);
        Assert.Null(fakeCommand.LastError);
        Assert.False(await table.Contains("Reset-abc123"));
    }

    [Fact]
    public async Task RouteOcppCall_ForCallResultWithUnknownAction_DoesNotInvokeAnyCommand()
    {
        _commandsFactoryMock.Setup(f => f.GetCommand(It.IsAny<string>())).Returns((IOcppChargerCommand?)null);

        var (sut, _) = CreateSut();
        var frame = Encoding.UTF8.GetBytes(BuildCallResult("Whatever-abc123", "{}"));

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Null(result);
        _commandsFactoryMock.Verify(f => f.GetCommand(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RouteOcppCall_ForCallResultWithoutTableEntry_DoesNotInvokeAnyCommand()
    {
        var (sut, _) = CreateSut();
        var frame = Encoding.UTF8.GetBytes(BuildCallResult("Unknown-msg-id", "{}"));

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Null(result);
        _commandsFactoryMock.Verify(f => f.GetCommand(It.IsAny<string>()), Times.Never);
    }

    // ----------------------------------------------------------------
    // CALL_ERROR → Command dispatch
    // ----------------------------------------------------------------

    [Fact]
    public async Task RouteOcppCall_ForCallErrorWithKnownAction_DispatchesToCommandWithError()
    {
        var fakeCommand = new FakeCommand("Reset");
        _commandsFactoryMock.Setup(f => f.GetCommand("Reset")).Returns(fakeCommand);

        var (sut, table) = CreateSut();
        await table.Add("Reset-abc123", "Reset");

        var frame = Encoding.UTF8.GetBytes(BuildCallError("Reset-abc123", "GenericError", "charger rejected", "{}"));

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Null(result);
        Assert.Equal(1, fakeCommand.HandleErrorCallCount);
        Assert.Null(fakeCommand.LastResponsePayload);
        Assert.NotNull(fakeCommand.LastError);
        Assert.Equal("GenericError", fakeCommand.LastError!.ErrorCode);
        Assert.False(await table.Contains("Reset-abc123"));
    }

    [Fact]
    public async Task RouteOcppCall_ForCallErrorWithUnknownAction_DoesNotInvokeAnyCommand()
    {
        _commandsFactoryMock.Setup(f => f.GetCommand(It.IsAny<string>())).Returns((IOcppChargerCommand?)null);

        var (sut, _) = CreateSut();
        var frame = Encoding.UTF8.GetBytes(BuildCallError("Whatever-abc123", "GenericError", "x", "{}"));

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Null(result);
        _commandsFactoryMock.Verify(f => f.GetCommand(It.IsAny<string>()), Times.Never);
    }

    // ----------------------------------------------------------------
    // Unknown message type
    // ----------------------------------------------------------------

    [Fact]
    public async Task RouteOcppCall_ForUnknownMessageType_ReturnsMessageTypeNotSupported()
    {
        var (sut, _) = CreateSut();
        var frame = Encoding.UTF8.GetBytes("[99,\"msg-1\"]");

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.NotNull(result);
        Assert.NotNull(result.Error);
        Assert.Equal(OcppCallError.ErrorCodes.MessageTypeNotSupported, result.Error.ErrorCode);
    }

    // ----------------------------------------------------------------
    // SubmitAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task SubmitAsync_RegistersMessageIdInOutboundTable_AndForwardsToSubmitter()
    {
        var (sut, table) = CreateSut();
        var request = new OcppCallRequest("Reset-abc123", "Reset", "{}");

        string? capturedClientId = null;
        string? capturedFrame = null;
        sut.SetCallSubmitter((clientId, frame, ct) =>
        {
            capturedClientId = clientId;
            capturedFrame = frame;
            return Task.CompletedTask;
        });

        await sut.SubmitAsync(CLIENT_ID, request, CancellationToken.None);

        Assert.Equal(CLIENT_ID, capturedClientId);
        Assert.NotNull(capturedFrame);

        var registeredAction = await table.TryGetValue("Reset-abc123");
        Assert.Equal("Reset", registeredAction);

        Assert.StartsWith("[2,\"Reset-abc123\"", capturedFrame);
        Assert.Contains("\"Reset\"", capturedFrame);
    }

    [Fact]
    public async Task SubmitAsync_WithoutSubmitterWired_Throws()
    {
        var (sut, _) = CreateSut();
        var request = new OcppCallRequest("Reset-abc123", "Reset", "{}");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SubmitAsync(CLIENT_ID, request, CancellationToken.None));
    }

    [Fact]
    public async Task SubmitAsync_PassesCancellationTokenToSubmitter()
    {
        var (sut, _) = CreateSut();
        var request = new OcppCallRequest("Reset-abc123", "Reset", "{}");

        using var cts = new CancellationTokenSource();
        CancellationToken capturedCt = default;
        sut.SetCallSubmitter((_, _, ct) => { capturedCt = ct; return Task.CompletedTask; });

        await sut.SubmitAsync(CLIENT_ID, request, cts.Token);

        Assert.Equal(cts.Token, capturedCt);
    }

    [Fact]
    public async Task SubmitAsync_WithoutInitializedRoutingTable_Throws()
    {
        var router = new OcppCallRouter(
            _loggerMock.Object,
            _routerServicesMock.Object,
            _actionsFactoryMock.Object,
            _commandsFactoryMock.Object);
        router.SetCallSubmitter((_, _, _) => Task.CompletedTask);

        var request = new OcppCallRequest("Reset-abc123", "Reset", "{}");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.SubmitAsync(CLIENT_ID, request, CancellationToken.None));
    }

    [Fact]
    public async Task SubmitAsync_PreservesPayloadInWireFrame()
    {
        var (sut, _) = CreateSut();
        const string payload = "{\"type\":\"Full\",\"evseId\":1}";
        var request = new OcppCallRequest("Reset-xyz", "Reset", payload);

        string? capturedFrame = null;
        sut.SetCallSubmitter((_, frame, _) => { capturedFrame = frame; return Task.CompletedTask; });

        await sut.SubmitAsync(CLIENT_ID, request, CancellationToken.None);

        Assert.NotNull(capturedFrame);
        Assert.Contains(payload, capturedFrame);
    }

    [Fact]
    public async Task SubmitAsync_RegistersAllSentRequests_ForLaterResponseDispatch()
    {
        var (sut, table) = CreateSut();
        sut.SetCallSubmitter((_, _, _) => Task.CompletedTask);

        await sut.SubmitAsync(CLIENT_ID, new OcppCallRequest("msg-A", "Reset", "{}"), CancellationToken.None);
        await sut.SubmitAsync(CLIENT_ID, new OcppCallRequest("msg-B", "TriggerMessage", "{}"), CancellationToken.None);

        Assert.Equal("Reset", await table.TryGetValue("msg-A"));
        Assert.Equal("TriggerMessage", await table.TryGetValue("msg-B"));
    }

    [Fact]
    public async Task SubmitAsync_ThenCallResult_DispatchesToRegisteredCommand()
    {
        var fakeCommand = new FakeCommand("Reset");
        _commandsFactoryMock.Setup(f => f.GetCommand("Reset")).Returns(fakeCommand);

        var (sut, _) = CreateSut();
        sut.SetCallSubmitter((_, _, _) => Task.CompletedTask);

        await sut.SubmitAsync(CLIENT_ID, new OcppCallRequest("Reset-1", "Reset", "{}"), CancellationToken.None);

        var frame = Encoding.UTF8.GetBytes(BuildCallResult("Reset-1", "{\"status\":\"Accepted\"}"));
        await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Equal(1, fakeCommand.HandleResponseCallCount);
        Assert.Equal("{\"status\":\"Accepted\"}", fakeCommand.LastResponsePayload);
    }

    [Fact]
    public async Task SubmitAsync_ThenCallError_DispatchesToRegisteredCommand()
    {
        var fakeCommand = new FakeCommand("Reset");
        _commandsFactoryMock.Setup(f => f.GetCommand("Reset")).Returns(fakeCommand);

        var (sut, _) = CreateSut();
        sut.SetCallSubmitter((_, _, _) => Task.CompletedTask);

        await sut.SubmitAsync(CLIENT_ID, new OcppCallRequest("Reset-2", "Reset", "{}"), CancellationToken.None);

        var frame = Encoding.UTF8.GetBytes(BuildCallError("Reset-2", "GenericError", "nope", "{}"));
        await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Equal(1, fakeCommand.HandleErrorCallCount);
        Assert.NotNull(fakeCommand.LastError);
        Assert.Equal("GenericError", fakeCommand.LastError!.ErrorCode);
    }

    [Fact]
    public async Task SubmitAsync_WithCallSubmitterThatThrows_PropagatesException()
    {
        var (sut, _) = CreateSut();
        sut.SetCallSubmitter((_, _, _) => throw new InvalidOperationException("wire down"));

        var request = new OcppCallRequest("Reset-3", "Reset", "{}");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SubmitAsync(CLIENT_ID, request, CancellationToken.None));
    }

    // ----------------------------------------------------------------
    // DisconnectClient
    // ----------------------------------------------------------------

    [Fact]
    public async Task DisconnectClient_ForwardsToRouterServices()
    {
        var (sut, _) = CreateSut();
        await sut.DisconnectClient(CLIENT_ID);
        _routerServicesMock.Verify(s => s.DisconnectClient(CLIENT_ID), Times.Once);
    }

    // ----------------------------------------------------------------
    // Helpers — frame builders
    // ----------------------------------------------------------------

    private static string BuildCall(string messageId, string action, string payload) =>
        $"[2,\"{messageId}\",\"{action}\",{payload}]";

    private static string BuildCallResult(string messageId, string payload) =>
        $"[3,\"{messageId}\",{payload}]";

    private static string BuildCallError(string messageId, string code, string description, string details) =>
        $"[4,\"{messageId}\",\"{code}\",\"{description}\",{details}]";

    // ----------------------------------------------------------------
    // Test doubles
    // ----------------------------------------------------------------

    private sealed class FakeAction : IOcppAction
    {
        public string ActionName { get; set; } = "";
        public RpcResult? ResultToReturn { get; set; }
        public Exception? ThrowOnExecute { get; set; }
        public OcppCallRequest? ExecutedCall { get; private set; }
        public string? ExecutedClientId { get; private set; }
        public int ExecuteCallCount { get; private set; }

        public Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
        {
            ExecuteCallCount++;
            ExecutedCall = call;
            ExecutedClientId = clientIdentifier;
            if (ThrowOnExecute is not null) throw ThrowOnExecute;
            return Task.FromResult(ResultToReturn ?? new RpcResult());
        }
    }

    private sealed class FakeCommand : IOcppChargerCommand
    {
        public FakeCommand(string action) => Action = action;

        public string Action { get; }
        public string? LastResponsePayload { get; private set; }
        public OcppCallError? LastError { get; private set; }
        public int HandleResponseCallCount { get; private set; }
        public int HandleErrorCallCount { get; private set; }

        public Task HandleResponse(OcppCallResult result)
        {
            HandleResponseCallCount++;
            LastResponsePayload = result.Payload;
            return Task.CompletedTask;
        }

        public Task HandleError(OcppCallError error)
        {
            HandleErrorCallCount++;
            LastError = error;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRoutingTable : BaseOcppRoutingTable
    {
        private readonly Dictionary<string, string> _entries = new();

        public override Task Add(string messageId, string value)
        {
            _entries[messageId] = value;
            return Task.CompletedTask;
        }

        public override Task Remove(string messageId)
        {
            _entries.Remove(messageId);
            return Task.CompletedTask;
        }

        public override Task<string?> TryGetValue(string messageId)
        {
            _entries.TryGetValue(messageId, out var value);
            return Task.FromResult(value);
        }

        public Task<bool> Contains(string messageId) => Task.FromResult(_entries.ContainsKey(messageId));
    }
}