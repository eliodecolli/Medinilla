using Medinilla.Infrastructure.WAMP;

namespace Medinilla.Core.Interfaces;

public interface IOcppCallRouter
{
    Task<RpcResult> RouteOcppCall(byte[] buffer, string? clientIdentifier);
    
    Task DisconnectClient(string clientIdentifier);
}
