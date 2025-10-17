using Medinilla.Infrastructure.WAMP;

namespace Medinilla.Core.Actions;

public interface IOcppAction
{
    string ActionName { get; }

    Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier);
}
