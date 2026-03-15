using Medinilla.Core.Logic.Authorization;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class AuthorizeAction(ChargingStationUnitOfWork unitOfWork,
    ILogger<AuthorizeAction> logger,
    AuthorizationAlgorithmFactory authorizationAlgorithmFactory) : IOcppAction
{
    public string ActionName => "Authorize";

    private AuthorizeResponse GenerateResponse(string status)
    {
        return new AuthorizeResponse()
        {
            IdTokenInfo = new IdTokenInfo()
            {
                Status = status,
                CacheExpiryDateTime = DateTime.Now,
            },
            CertificateStatus = AuthorizeCertificateStatus.NoCertificateAvailable
        };
    }

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var request = call.As<AuthorizeRequest>();
        var status = AuthorizeStatus.Accepted;

        logger.LogInformation($"Processing Authorize request from {clientIdentifier}");

        if (request.ISO15118CertificateHashData is not null || request.Certificate is not null)
        {
            return new RpcResult()
            {
                Error = call.CreateErrorResult<AuthorizeResponse>(OcppCallError.ErrorCodes.NotSupported,
                    "Medinilla does not support ISO15118 authorization yet.")
            };
        }

        var chargingStaiton = await unitOfWork.GetChargingStation(clientIdentifier);
        if (chargingStaiton is null)
        {
            return new RpcResult()
            {
                Error = call.CreateErrorResult<AuthorizeResponse>(OcppCallError.ErrorCodes.InternalError,
                        $"Specified client identifier {clientIdentifier} was not found.")
            };
        }

        try
        {
            status = await authorizationAlgorithmFactory.RunAuthorization(request.IdToken,
                AuthUtils.GenerateAuthContext(chargingStaiton, null, false));
            if (status != AuthorizeStatus.Accepted)
            {
                logger.LogInformation($"{clientIdentifier}: Failed Authorization for token {request.IdToken.Token}: {status}.");
            }
        }
        catch (OcppAuthorizationException ex)
        {
            logger.LogError($"{clientIdentifier}: Error during authorization: {ex.Message}");
            return new RpcResult()
            {
                Error = call.CreateErrorResult<AuthorizeResponse>(OcppCallError.ErrorCodes.InternalError, ex.Message),
            };
        }

        return new RpcResult()
        {
            Result = call.CreateResult(GenerateResponse(status)),
        };
    }
}
