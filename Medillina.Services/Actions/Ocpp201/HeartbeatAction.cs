using Medinilla.DataTypes.Contracts;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;

namespace Medinilla.Services.Actions.Ocpp201;

internal class HeartbeatAction : IOcppAction
{
    private readonly ILogger<HeartbeatAction> _logger;

    public HeartbeatAction(ILogger<HeartbeatAction> logger)
        => _logger = logger;

    public string ActionName => "Heartbeat";

    public Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        return Task.FromResult(new RpcResult()
        {
            Result = call.CreateResult(new HeartbeatResponse()),
            Error = null,
            ReturnToCS = true
        });
    }
}
