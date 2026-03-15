using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class BootNotificationAction(IChargingStationBootingService service,
    ILogger<BootNotificationAction> _logger) : IOcppAction
{
    public string ActionName => OcppActionNames.BootNotification;

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var notification = call.As<BootNotificationRequest>();
        _logger.LogInformation("Received boot notification {0} from {3}. Vendor: {1} Model: {2}",
            notification.Reason,
            notification.ChargingStation is not null ? notification.ChargingStation.VendorName : "UNDEFINED",
            notification.ChargingStation is not null ? notification.ChargingStation.Model : "UNDEFINED",
            clientIdentifier);

        try
        {
            await service.ProcessBootup(clientIdentifier, notification);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{clientIdentifier}]: Error while processing BootNotification request: {ex}");
            return new RpcResult()
            {
                Result = call.CreateResult(new BootNotificationResponse(1440, RegistrationStatusEnum.Rejected, new StatusInfo()
                {
                    ReasonCode = BootReasonCode.BootError.Code,
                    AdditionalInfo = BootReasonCode.BootError.Detail,
                })),
                Error = null,
                ReturnToCS = true,
            };
        }

        return new RpcResult()
        {
            Result = call.CreateResult(new BootNotificationResponse(1440, RegistrationStatusEnum.Accepted, new StatusInfo()
            {
                ReasonCode = BootReasonCode.BootOk.Code,
                AdditionalInfo = BootReasonCode.BootOk.Detail,
            })),
            Error = null,
            ReturnToCS = true
        };
    }
}
