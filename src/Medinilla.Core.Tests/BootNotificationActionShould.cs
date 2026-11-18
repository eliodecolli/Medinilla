using Medinilla.Core.Actions.Ocpp201;
using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core;
using Medinilla.DataTypes.Core.Enums;
using Medinilla.Infrastructure;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OcppChargingStation = Medinilla.DataTypes.Contracts.Common.ChargingStation;

namespace Medinilla.Core.Tests;

public class BootNotificationActionShould
{
    private readonly Mock<IChargingStationBootingService> _service = new();

    private BootNotificationAction CreateAction() => new(
        _service.Object,
        NullLogger<BootNotificationAction>.Instance);

    [Fact]
    public async Task ReturnAcceptedWhenTheServiceProcessesTheBoot()
    {
        _service.Setup(s => s.ProcessBootup("station-1", It.IsAny<BootNotificationRequest>()))
            .ReturnsAsync(BootupResult.Ok);

        var result = await CreateAction().Execute(CreateCall(), "station-1");

        Assert.Null(result.Error);
        Assert.NotNull(result.Result);
        var response = result.Result.As<BootNotificationResponse>();
        Assert.Equal(RegistrationStatusEnum.Accepted, response.Status);
        Assert.Equal(BootReasonCode.BootOk.Code, response.StatusInfo.ReasonCode);
    }

    [Fact]
    public async Task ReturnRejectedWhenTheServiceFailsToProcessTheBoot()
    {
        _service.Setup(s => s.ProcessBootup("station-1", It.IsAny<BootNotificationRequest>()))
            .ThrowsAsync(new InvalidOperationException("boot failed"));

        var result = await CreateAction().Execute(CreateCall(), "station-1");

        Assert.Null(result.Error);
        Assert.NotNull(result.Result);
        var response = result.Result.As<BootNotificationResponse>();
        Assert.Equal(RegistrationStatusEnum.Rejected, response.Status);
        Assert.Equal(BootReasonCode.BootError.Code, response.StatusInfo.ReasonCode);
    }

    private static OcppCallRequest CreateCall()
    {
        var request = new BootNotificationRequest(
            BootReasonEnum.PowerUp,
            new OcppChargingStation
            {
                VendorName = "Vendor",
                Model = "Model",
                SerialNumber = "serial-1",
            });

        return new OcppCallRequest(
            "message-1",
            OcppActionNames.BootNotification,
            OcppPayloadSerializer.SerializePayload(request));
    }
}
