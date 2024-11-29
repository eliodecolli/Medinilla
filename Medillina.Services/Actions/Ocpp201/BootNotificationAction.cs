using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.WAMP;
using Microsoft.Extensions.Logging;

using CS = Medinilla.DataAccess.Relational.Models.ChargingStation;

namespace Medinilla.Services.Actions.Ocpp201;

public sealed class BootNotificationAction(ChargingStationUnitOfWork unitOfWork, ILogger<BootNotificationAction> _logger) : IOcppAction
{
    public string ActionName { get => "BootNotification"; }

    private CS GetChargingStation(string clientIdentifier, BootNotificationRequest request)
    {
        return new CS()
        {
            ClientIdentifier = clientIdentifier,
            Model = request.ChargingStation.Model,
            Vendor = request.ChargingStation.VendorName,
            LatestBootNotificationReason = Enum.GetName(request.Reason)!
        };
    }

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var notification = call.As<BootNotificationRequest>();
        _logger.LogInformation("Received boot notification {0} from {3}. Vendor: {1} Model: {2}",
            notification.Reason,
            notification.ChargingStation is not null ? notification.ChargingStation.VendorName : "UNDEFINED",
            notification.ChargingStation is not null ? notification.ChargingStation.Model : "UNDEFINED",
            clientIdentifier);

        var chargingStation = GetChargingStation(clientIdentifier, notification);
        await unitOfWork.ProcessBootNotification(chargingStation);
        await unitOfWork.Save();

        // idk do something here..?
        return new RpcResult()
        {
            Result = call.CreateResult(new BootNotificationResponse(1440, RegistrationStatusEnum.Accepted, new StatusInfo() {
                ReasonCode = "200",
                AdditionalInfo = ""
            })),
            Error = null,
            ReturnToCS = true
        };
    }
}
