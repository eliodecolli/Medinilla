using Medinilla.Infrastructure.WAMP;
using Medinilla.Core.Commands;

namespace Medinilla.Core.Interfaces;

public interface IOcppCallRouter
{
    Task<RpcResult?> RouteOcppCall(byte[] buffer, string? clientIdentifier);

    Task SubmitAsync(string clientIdentifier, OcppCallRequest request, CancellationToken ct);

    void SetCallSubmitter(Func<string, string, CancellationToken, Task> submitter);

    Task DisconnectClient(string clientIdentifier);
}
