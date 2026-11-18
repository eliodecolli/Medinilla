using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;
using Medinilla.Infrastructure.WAMP;

namespace Medinilla.Core.Actions.Ocpp201;

internal class HeartbeatAction(IHeartbeatService service) : IOcppAction
{
    public string ActionName => OcppActionNames.Heartbeat;

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var response = await service.ProcessHeartbeat(clientIdentifier);

        return new RpcResult()
        {
            Result = call.CreateResult(response),
            Error = null,
            ReturnToCS = true
        };
    }
}
