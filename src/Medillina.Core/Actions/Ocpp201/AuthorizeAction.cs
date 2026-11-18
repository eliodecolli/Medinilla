using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;
using Medinilla.Infrastructure.WAMP;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class AuthorizeAction(IAuthorizationService service) : IOcppAction
{
    public string ActionName => OcppActionNames.Authorize;

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var request = call.As<AuthorizeRequest>();
        var outcome = await service.ProcessAuthorization(clientIdentifier, request);

        return outcome.Status switch
        {
            AuthorizationProcessingStatus.Completed => new RpcResult()
            {
                Result = call.CreateResult(outcome.Response!),
            },
            AuthorizationProcessingStatus.UnsupportedCertificate => new RpcResult()
            {
                Error = call.CreateErrorResult<AuthorizeResponse>(OcppCallError.ErrorCodes.NotSupported,
                    "Medinilla does not support ISO15118 authorization yet.")
            },
            AuthorizationProcessingStatus.ChargingStationNotFound => new RpcResult()
            {
                Error = call.CreateErrorResult<AuthorizeResponse>(OcppCallError.ErrorCodes.InternalError,
                    $"Specified client identifier {clientIdentifier} was not found.")
            },
            AuthorizationProcessingStatus.AuthorizationError => new RpcResult()
            {
                Error = call.CreateErrorResult<AuthorizeResponse>(
                    OcppCallError.ErrorCodes.InternalError,
                    outcome.ErrorMessage ?? string.Empty)
            },
            _ => throw new InvalidOperationException($"Unhandled authorization outcome {outcome.Status}.")
        };
    }
}
