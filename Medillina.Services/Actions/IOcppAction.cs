using Medinilla.DataTypes.WAMP;

namespace Medinilla.Services.Actions;

public interface IOcppAction
{
    string ActionName { get; }

    Task<RpcResult> Execute(OcppCallRequest call);
}
