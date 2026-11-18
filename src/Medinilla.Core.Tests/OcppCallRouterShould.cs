using Medinilla.Core.Actions;
using Medinilla.Core.Commands;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.v1;
using Medinilla.DataTypes.Core;
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
    private readonly Mock<IChargingStationBootingService> _bootingServiceMock = new();
    private readonly Mock<IOcppRequestDispatcher> _dispatcherMock = new();
    private readonly Mock<ICommandExecutionService> _executionServiceMock = new();

    private const string CLIENT_ID = "TEST-CHARGER-001";
    private const string KNOWN_ACTION = "Heartbeat";
    private const string UNKNOWN_ACTION = "MysteryAction";

    public OcppCallRouterShould(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        _routerServicesMock
            .Setup(s => s.ValidateChargingStationAvailability(CLIENT_ID))
            .ReturnsAsync(true);

        _bootingServiceMock
            .Setup(s => s.DisconnectClient(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _dispatcherMock
            .Setup(d => d.SubmitRequest(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);
    }

    private (OcppCallRouter Router, FakeRoutingTable Table) CreateSut()
    {
        var table = new FakeRoutingTable();
        var router = new OcppCallRouter(
            _loggerMock.Object,
            _routerServicesMock.Object,
            _bootingServiceMock.Object,
            _actionsFactoryMock.Object,
            _commandsFactoryMock.Object,
            _dispatcherMock.Object,
            table,
            _executionServiceMock.Object);
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
        byte[]? capturedFrame = null;
        _dispatcherMock
            .Setup(d => d.SubmitRequest(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Callback<string, byte[]>((clientId, frame) =>
            {
                capturedClientId = clientId;
                capturedFrame = frame;
            })
            .Returns(Task.CompletedTask);

        await sut.SubmitAsync(CLIENT_ID, request);

        Assert.Equal(CLIENT_ID, capturedClientId);
        Assert.NotNull(capturedFrame);

        var registeredAction = await table.TryGetValue("Reset-abc123");
        Assert.Equal("Reset", registeredAction);

        var frameString = Encoding.UTF8.GetString(capturedFrame!);
        Assert.StartsWith("[2,\"Reset-abc123\"", frameString);
        Assert.Contains("\"Reset\"", frameString);
    }

    [Fact]
    public async Task SubmitAsync_PreservesPayloadInWireFrame()
    {
        var (sut, _) = CreateSut();
        const string payload = "{\"type\":\"Full\",\"evseId\":1}";
        var request = new OcppCallRequest("Reset-xyz", "Reset", payload);

        byte[]? capturedFrame = null;
        _dispatcherMock
            .Setup(d => d.SubmitRequest(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Callback<string, byte[]>((_, frame) => capturedFrame = frame)
            .Returns(Task.CompletedTask);

        await sut.SubmitAsync(CLIENT_ID, request);

        Assert.NotNull(capturedFrame);
        var frameString = Encoding.UTF8.GetString(capturedFrame!);
        Assert.Contains(payload, frameString);
    }

    [Fact]
    public async Task SubmitAsync_RegistersAllSentRequests_ForLaterResponseDispatch()
    {
        var (sut, table) = CreateSut();

        await sut.SubmitAsync(CLIENT_ID, new OcppCallRequest("msg-A", "Reset", "{}"));
        await sut.SubmitAsync(CLIENT_ID, new OcppCallRequest("msg-B", "TriggerMessage", "{}"));

        Assert.Equal("Reset", await table.TryGetValue("msg-A"));
        Assert.Equal("TriggerMessage", await table.TryGetValue("msg-B"));
    }

    [Fact]
    public async Task SubmitAsync_ThenCallResult_DispatchesToRegisteredCommand()
    {
        var fakeCommand = new FakeCommand("Reset");
        _commandsFactoryMock.Setup(f => f.GetCommand("Reset")).Returns(fakeCommand);

        var (sut, _) = CreateSut();

        await sut.SubmitAsync(CLIENT_ID, new OcppCallRequest("Reset-1", "Reset", "{}"));

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

        await sut.SubmitAsync(CLIENT_ID, new OcppCallRequest("Reset-2", "Reset", "{}"));

        var frame = Encoding.UTF8.GetBytes(BuildCallError("Reset-2", "GenericError", "nope", "{}"));
        await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Equal(1, fakeCommand.HandleErrorCallCount);
        Assert.NotNull(fakeCommand.LastError);
        Assert.Equal("GenericError", fakeCommand.LastError!.ErrorCode);
    }

    [Fact]
    public async Task SubmitAsync_WhenDispatcherThrows_PropagatesException()
    {
        var (sut, _) = CreateSut();
        _dispatcherMock
            .Setup(d => d.SubmitRequest(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Throws(new InvalidOperationException("wire down"));

        var request = new OcppCallRequest("Reset-3", "Reset", "{}");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SubmitAsync(CLIENT_ID, request));
    }

    [Fact]
    public async Task SubmitAsync_RegistersAuditRow_BeforeDispatching()
    {
        var (sut, _) = CreateSut();

        await sut.SubmitAsync(CLIENT_ID, new OcppCallRequest("Reset-audit-1", "Reset", "{}"));

        _executionServiceMock.Verify(
            s => s.RegisterExecution(CLIENT_ID, "Reset-audit-1", "Reset"),
            Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_WhenDispatcherThrows_ClosesAuditRowWithError()
    {
        var (sut, _) = CreateSut();
        _dispatcherMock
            .Setup(d => d.SubmitRequest(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Throws(new InvalidOperationException("wire down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SubmitAsync(CLIENT_ID, new OcppCallRequest("Reset-audit-2", "Reset", "{}")));

        _executionServiceMock.Verify(
            s => s.SetExecutionResult(
                CLIENT_ID,
                It.Is<ExecutionResult>(r =>
                    r.MessageId == "Reset-audit-2" &&
                    r.Error == true &&
                    r.ErrorMessage == "Error contacting charging station")),
            Times.Once);
    }

    [Fact]
    public async Task RouteOcppCall_ForCallResult_PassesClientIdentifierAndExecutionServiceToCommand()
    {
        var fakeCommand = new FakeCommand("Reset");
        _commandsFactoryMock.Setup(f => f.GetCommand("Reset")).Returns(fakeCommand);

        var (sut, table) = CreateSut();
        await table.Add("Reset-audit-3", "Reset");

        var frame = Encoding.UTF8.GetBytes(BuildCallResult("Reset-audit-3", "{\"status\":\"Accepted\"}"));
        await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Equal(1, fakeCommand.HandleResponseCallCount);
        Assert.Equal(CLIENT_ID, fakeCommand.LastClientIdentifier);
        Assert.Same(_executionServiceMock.Object, fakeCommand.LastExecutionService);
    }

    [Fact]
    public async Task RouteOcppCall_ForCallError_PassesClientIdentifierAndExecutionServiceToCommand()
    {
        var fakeCommand = new FakeCommand("Reset");
        _commandsFactoryMock.Setup(f => f.GetCommand("Reset")).Returns(fakeCommand);

        var (sut, table) = CreateSut();
        await table.Add("Reset-audit-4", "Reset");

        var frame = Encoding.UTF8.GetBytes(BuildCallError("Reset-audit-4", "GenericError", "x", "{}"));
        await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Equal(1, fakeCommand.HandleErrorCallCount);
        Assert.Equal(CLIENT_ID, fakeCommand.LastClientIdentifier);
        Assert.Same(_executionServiceMock.Object, fakeCommand.LastExecutionService);
    }

    // ----------------------------------------------------------------
    // DisconnectClient
    // ----------------------------------------------------------------

    [Fact]
    public async Task DisconnectClient_ForwardsToBootingService()
    {
        var (sut, _) = CreateSut();
        await sut.DisconnectClient(CLIENT_ID);
        _bootingServiceMock.Verify(s => s.DisconnectClient(CLIENT_ID), Times.Once);
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
        public string? LastClientIdentifier { get; private set; }
        public ICommandExecutionService? LastExecutionService { get; private set; }
        public int HandleResponseCallCount { get; private set; }
        public int HandleErrorCallCount { get; private set; }

        public Task HandleResponse(string clientIdentifier, OcppCallResult result, ICommandExecutionService executionService)
        {
            HandleResponseCallCount++;
            LastClientIdentifier = clientIdentifier;
            LastExecutionService = executionService;
            LastResponsePayload = result.Payload;
            return Task.CompletedTask;
        }

        public Task HandleError(string clientIdentifier, OcppCallError error, ICommandExecutionService executionService)
        {
            HandleErrorCallCount++;
            LastClientIdentifier = clientIdentifier;
            LastExecutionService = executionService;
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