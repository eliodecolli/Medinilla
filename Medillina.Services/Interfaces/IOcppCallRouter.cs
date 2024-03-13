using Medinilla.DataTypes.WAMP;

namespace Medinilla.Services.Interfaces;

public interface IOcppCallRouter
{
    Task<RpcResult> RouteOcppCall(byte[] buffer, string? clientIdentifier);
}
