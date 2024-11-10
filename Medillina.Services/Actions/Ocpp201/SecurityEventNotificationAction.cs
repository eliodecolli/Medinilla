using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.WAMP;
using Microsoft.Extensions.Logging;


namespace Medinilla.Services.Actions.Ocpp201;

public sealed class SecurityEventNotificationAction(ILogger<BootNotificationAction> logger) : IOcppAction
{
    public string ActionName { get => "SecurityEventNotification"; }

    public Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var notification = call.As<SecurityEventNotificationRequest>();
        logger.LogInformation("{0} Received Security Notification Request: \"{1}\" with type \"{2}\"",
            clientIdentifier, notification.TechInfo, notification.Type);

        // idk do something here..?
        return Task.FromResult(new RpcResult()
        {
            Result = call.CreateResult(new SecurityEventNotificationResponse()),
            Error = null,
            ReturnToCS = true
        });
    }
}
