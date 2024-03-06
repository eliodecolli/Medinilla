using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.WAMP;
using Microsoft.Extensions.Logging;


namespace Medinilla.Services.Actions.Ocpp201;

public sealed class BootNotificationAction : IOcppAction
{
    private ILogger<BootNotificationAction> _logger;

    public BootNotificationAction(ILogger<BootNotificationAction> logger)
        => _logger = logger;

    public string ActionName { get => "BootNotification"; }

    public Task<RpcResult> Execute(OcppCallRequest call)
    {
        var notification = call.As<BootNotificationRequest>();
        _logger.LogInformation("Received boot notification {0}. Vendor: {1} Model: {2}",
            notification.Reason, notification.ChargingStation.VendorName, notification.ChargingStation.Model);

        // idk do something here..?
        return Task.FromResult(new RpcResult()
        {
            Result = call.CreateResult(new BootNotificationResponse(1, RegistrationStatusEnum.Accepted, null)),
            Error = null,
            ReturnToCS = true
        });
    }
}
