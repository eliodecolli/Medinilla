using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;
using Medinilla.Infrastructure.WAMP;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class SecurityEventNotificationAction(ISecurityEventNotificationService service) : IOcppAction
{
    public string ActionName => OcppActionNames.SecurityEventNotification;

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var notification = call.As<SecurityEventNotificationRequest>();
        await service.ProcessNotification(clientIdentifier, notification);

        return new RpcResult()
        {
            Result = call.CreateResult(new SecurityEventNotificationResponse()),
            Error = null,
            ReturnToCS = true
        };
    }
}
