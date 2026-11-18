using Medinilla.Core.Actions.Ocpp201;
using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Medinilla.Core.Tests;

public class StatusNotificationActionShould
{
    private readonly Mock<IStatusNotificationService> _service = new();

    [Fact]
    public async Task DelegateTheNotificationAndReturnAnAcknowledgement()
    {
        _service.Setup(s => s.ProcessNotification("station-1", It.IsAny<StatusNotificationRequest>()))
            .Returns(Task.CompletedTask);

        var result = await CreateAction().Execute(CreateCall(), "station-1");

        Assert.True(result.ReturnToCS);
        Assert.Null(result.Error);
        Assert.NotNull(result.Result);
        var response = result.Result.As<StatusNotificationResponse>();
        Assert.NotNull(response);
        _service.Verify(
            s => s.ProcessNotification("station-1", It.IsAny<StatusNotificationRequest>()),
            Times.Once);
    }

    [Fact]
    public async Task ReturnTheExistingErrorWhenTheChargingStationIsMissing()
    {
        _service.Setup(s => s.ProcessNotification("station-1", It.IsAny<StatusNotificationRequest>()))
            .ThrowsAsync(new AggregateRootNotFoundException("missing"));

        var result = await CreateAction().Execute(CreateCall(), "station-1");

        Assert.True(result.ReturnToCS);
        Assert.Null(result.Result);
        Assert.NotNull(result.Error);
        Assert.Equal(OcppCallError.ErrorCodes.GenericError, result.Error.ErrorCode);
        Assert.Contains("station-1", result.Error.ErrorDescription);
    }

    private StatusNotificationAction CreateAction() =>
        new(_service.Object, NullLogger<StatusNotificationAction>.Instance);

    private static OcppCallRequest CreateCall()
    {
        var request = new StatusNotificationRequest
        {
            EvseId = 2,
            ConnectorId = 1,
            ConnectorStatus = ConnectorStatusEnum.Available,
            Timestamp = new DateTime(2026, 11, 14, 10, 30, 0, DateTimeKind.Utc),
        };

        return new OcppCallRequest(
            "message-1",
            OcppActionNames.StatusNotification,
            OcppPayloadSerializer.SerializePayload(request));
    }
}
