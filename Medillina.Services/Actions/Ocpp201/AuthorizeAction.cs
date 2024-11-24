using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.WAMP;

namespace Medinilla.Services.Actions.Ocpp201;

public sealed class AuthorizeAction : IOcppAction
{
    public string ActionName => "Authorize";

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var request = call.As<AuthorizeRequest>();

        // TODO: Implement authorization
        var response = new AuthorizeResponse()
        {
            CertificateStatus = AuthorizeStatus.Accepted,
            IdTokenInfo = new IdTokenInfo()
            {
                Status = AuthorizeStatus.Accepted,
            }
        };

        return await Task.FromResult(new RpcResult()
        {
            Result = call.CreateResult(response),
            ReturnToCS = true
        });
    }
}
