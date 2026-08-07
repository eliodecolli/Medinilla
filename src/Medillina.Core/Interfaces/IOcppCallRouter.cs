using Medinilla.Infrastructure.WAMP;

namespace Medinilla.Core.Interfaces;

public interface IOcppCallRouter
{
    Task<RpcResult?> RouteOcppCall(byte[] buffer, string? clientIdentifier);

    Task SubmitAsync(string clientIdentifier, OcppCallRequest request);

    Task DisconnectClient(string clientIdentifier);
}
