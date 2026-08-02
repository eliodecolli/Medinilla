using Medinilla.Core.Actions;
using Medinilla.Core.Commands;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.v1;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

    private OcppCallRouter CreateSut() =>
        new(_loggerMock.Object, _actionsFactoryMock.Object, _commandsFactoryMock.Object, _routerServicesMock.Object);

    // ----------------------------------------------------------------
    // CALL → Action dispatch
    // ----------------------------------------------------------------

    [Fact]
    public async Task RouteOcppCall_ForBootNotification_BypassesAvailabilityValidation()
    {
        var fakeAction = new FakeAction { ActionName = "BootNotification" };
        _actionsFactoryMock.Setup(f => f.GetAction("BootNotification")).Returns(fakeAction);

        var sut = CreateSut();
        var frame = Encoding.UTF8.GetBytes(BuildCall("msg-1", "BootNotification", "{}"));

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.NotNull(result);
        Assert.Same(fakeAction.ExecutedCall, fakeAction.ExecutedCall);   // executed
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

        var sut = CreateSut();
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

        var sut = CreateSut();
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

        var sut = CreateSut();
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

        var sut = CreateSut();
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
        var sut = CreateSut();
        var frame = Encoding.UTF8.GetBytes("[2,\"broken");   // unparseable

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Null(result);
    }

    // ----------------------------------------------------------------
    // CALL_RESULT → Command dispatch
    // ----------------------------------------------------------------

    [Fact]
    public async Task RouteOcppCall_ForCallResultWithKnownAction_DispatchesToCommand()
    {
        var fakeCommand = new FakeCommand { Action = "Reset" };
        _commandsFactoryMock.Setup(f => f.GetCommand("Reset")).Returns(fakeCommand);

        var sut = CreateSut();
        var frame = Encoding.UTF8.GetBytes(BuildCallResult("Reset-abc123", "{\"status\":\"Accepted\"}"));

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Null(result);
        Assert.Equal(1, fakeCommand.HandleResponseCallCount);
        Assert.Equal("{\"status\":\"Accepted\"}", fakeCommand.LastResponsePayload);
        Assert.Null(fakeCommand.LastError);
    }

    [Fact]
    public async Task RouteOcppCall_ForCallResultWithUnknownAction_DoesNotInvokeAnyCommand()
    {
        _commandsFactoryMock.Setup(f => f.GetCommand(It.IsAny<string>())).Returns((IOcppChargerCommand?)null);

        var sut = CreateSut();
        var frame = Encoding.UTF8.GetBytes(BuildCallResult("Whatever-abc123", "{}"));

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Null(result);
    }

    // ----------------------------------------------------------------
    // CALL_ERROR → Command dispatch
    // ----------------------------------------------------------------

    [Fact]
    public async Task RouteOcppCall_ForCallErrorWithKnownAction_DispatchesToCommandWithError()
    {
        var fakeCommand = new FakeCommand { Action = "Reset" };
        _commandsFactoryMock.Setup(f => f.GetCommand("Reset")).Returns(fakeCommand);

        var sut = CreateSut();
        var frame = Encoding.UTF8.GetBytes(BuildCallError("Reset-abc123", "GenericError", "charger rejected", "{}"));

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Null(result);
        Assert.Equal(1, fakeCommand.HandleResponseCallCount);
        Assert.Null(fakeCommand.LastResponsePayload);
        Assert.NotNull(fakeCommand.LastError);
        Assert.Equal("GenericError", fakeCommand.LastError!.ErrorCode);
    }

    [Fact]
    public async Task RouteOcppCall_ForCallErrorWithUnknownAction_DoesNotInvokeAnyCommand()
    {
        _commandsFactoryMock.Setup(f => f.GetCommand(It.IsAny<string>())).Returns((IOcppChargerCommand?)null);

        var sut = CreateSut();
        var frame = Encoding.UTF8.GetBytes(BuildCallError("Whatever-abc123", "GenericError", "x", "{}"));

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.Null(result);
    }

    // ----------------------------------------------------------------
    // Unknown message type
    // ----------------------------------------------------------------

    [Fact]
    public async Task RouteOcppCall_ForUnknownMessageType_ReturnsMessageTypeNotSupported()
    {
        var sut = CreateSut();
        var frame = Encoding.UTF8.GetBytes("[99,\"msg-1\"]");   // message type 99 doesn't exist

        var result = await sut.RouteOcppCall(frame, CLIENT_ID);

        Assert.NotNull(result);
        Assert.NotNull(result.Error);
        Assert.Equal(OcppCallError.ErrorCodes.MessageTypeNotSupported, result.Error.ErrorCode);
    }

    // ----------------------------------------------------------------
    // SubmitAsync
    // ----------------------------------------------------------------

    [Fact]
    public async Task SubmitAsync_BuildsFrameWithActionPrefixedMessageId_AndForwardsToSubmitter()
    {
        var fakeCommand = new FakeCommand { Action = "Reset" };

        var sut = CreateSut();

        string? capturedClientId = null;
        string? capturedFrame = null;
        sut.SetCallSubmitter((clientId, frame, ct) =>
        {
            capturedClientId = clientId;
            capturedFrame = frame;
            return Task.CompletedTask;
        });

        await sut.SubmitAsync(CLIENT_ID, fakeCommand, CancellationToken.None);

        Assert.Equal(CLIENT_ID, capturedClientId);
        Assert.NotNull(capturedFrame);

        // BuildCall must have been called with the action-prefixed messageId
        Assert.NotNull(fakeCommand.LastMessageIdPassedToBuildCall);
        Assert.StartsWith("Reset-", fakeCommand.LastMessageIdPassedToBuildCall);
        Assert.True(fakeCommand.LastMessageIdPassedToBuildCall.Length > "Reset-".Length);

        // The wire frame starts with [2,"Reset-... and contains the action
        Assert.StartsWith("[2,\"Reset-", capturedFrame);
        Assert.Contains("\"Reset\"", capturedFrame);
    }

    [Fact]
    public async Task SubmitAsync_WithoutSubmitterWired_Throws()
    {
        var sut = CreateSut();
        var fakeCommand = new FakeCommand { Action = "Reset" };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SubmitAsync(CLIENT_ID, fakeCommand, CancellationToken.None));
    }

    [Fact]
    public async Task SubmitAsync_PassesCancellationTokenToSubmitter()
    {
        var fakeCommand = new FakeCommand { Action = "Reset" };
        var sut = CreateSut();

        using var cts = new CancellationTokenSource();
        CancellationToken capturedCt = default;
        sut.SetCallSubmitter((_, _, ct) => { capturedCt = ct; return Task.CompletedTask; });

        await sut.SubmitAsync(CLIENT_ID, fakeCommand, cts.Token);

        Assert.Equal(cts.Token, capturedCt);
    }

    [Fact]
    public async Task SubmitAsync_GeneratesUniqueMessageIdsPerCall()
    {
        var fakeCommand = new FakeCommand { Action = "Reset" };
        var sut = CreateSut();
        sut.SetCallSubmitter((_, _, _) => Task.CompletedTask);

        await sut.SubmitAsync(CLIENT_ID, fakeCommand, CancellationToken.None);
        await sut.SubmitAsync(CLIENT_ID, fakeCommand, CancellationToken.None);

        Assert.Equal(2, fakeCommand.BuildCallCallCount);
        Assert.NotEqual(
            fakeCommand.AllMessageIdsPassedToBuildCall[0],
            fakeCommand.AllMessageIdsPassedToBuildCall[1]);
    }

    // ----------------------------------------------------------------
    // DisconnectClient
    // ----------------------------------------------------------------

    [Fact]
    public async Task DisconnectClient_ForwardsToRouterServices()
    {
        var sut = CreateSut();
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
        public string Action { get; set; } = "";
        public string Payload { get; set; } = "{}";
        public string? LastMessageIdPassedToBuildCall { get; private set; }
        public List<string> AllMessageIdsPassedToBuildCall { get; } = new();
        public int BuildCallCallCount { get; private set; }
        public string? LastResponsePayload { get; private set; }
        public OcppCallError? LastError { get; private set; }
        public int HandleResponseCallCount { get; private set; }

        public OcppCallRequest BuildCall(string messageId)
        {
            BuildCallCallCount++;
            LastMessageIdPassedToBuildCall = messageId;
            AllMessageIdsPassedToBuildCall.Add(messageId);
            return new OcppCallRequest(messageId, Action, Payload);
        }

        public void HandleResponse(string? responsePayload, OcppCallError? error)
        {
            HandleResponseCallCount++;
            LastResponsePayload = responsePayload;
            LastError = error;
        }
    }
}
