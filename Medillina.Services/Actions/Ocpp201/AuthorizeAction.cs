using Medinilla.Core.Logic.Authorization;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Authorization;
using Medinilla.DataTypes.WAMP;
using Microsoft.Extensions.Logging;

using IdTokenDbContext = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;
using ChargingStationDbContext = Medinilla.DataAccess.Relational.Models.ChargingStation;

namespace Medinilla.Services.Actions.Ocpp201;

public sealed class AuthorizeAction(ChargingStationUnitOfWork unitOfWork,
    ILogger<AuthorizeAction> logger,
    AuthorizationAlgorithmFactory authorizationAlgorithmFactory) : IOcppAction
{
    public string ActionName => "Authorize";

    public AuthorizationAlgorithm[] AlgorithmsCheck = [
        AuthorizationAlgorithm.Default,
        AuthorizationAlgorithm.LocationCheck,
        AuthorizationAlgorithm.DateRangeCheck,
        AuthorizationAlgorithm.ExpirationCheck,
        AuthorizationAlgorithm.CreditCheck,
    ];

    private AuthorizeResponse GenerateResponse(string status)
    {
        return new AuthorizeResponse()
        {
            CertificateStatus = status,
            IdTokenInfo = new IdTokenInfo()
            {
                Status = status,
            },

        };
    }

    private async Task<(string?, AuthorizationAlgorithm?)> CheckAuthorizationWithAlgo(IdToken token,
        IdTokenDbContext dbIdToken,
        ChargingStationDbContext cs)
    {
        var context = AuthUtils.GenerateAuthContext(cs, null, false);
        var result = AuthorizeStatus.Accepted;

        foreach (var algorithmName in AlgorithmsCheck)
        {
            var algorithm = authorizationAlgorithmFactory.Get(algorithmName);
            if (algorithm is not null)
            {
                var tempResult = await algorithm.Authorize(token, dbIdToken, context);

                if (tempResult != AuthorizeStatus.Accepted)
                {
                    return (tempResult, algorithmName);
                }
            }
        }

        return (result, null);
    }

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var request = call.As<AuthorizeRequest>();

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

        var idToken = chargingStaiton.IdTokens.Where(c => c.Token == request.IdToken.Token).FirstOrDefault();
        if (idToken is null)
        {
            logger.LogInformation($"{clientIdentifier}: Id Token with value {request.IdToken.Token} was not found");
            return new RpcResult()
            {
                Result = call.CreateResult(GenerateResponse(AuthorizeStatus.Unknown))
            };
        }

        try
        {
            var (resultStatus, algo) = await CheckAuthorizationWithAlgo(request.IdToken, idToken, chargingStaiton);
            if (resultStatus != AuthorizeStatus.Accepted)
            {
                logger.LogInformation($"{clientIdentifier}: {Enum.GetName(algo.Value)}: Failed Authorization for token {idToken.Token}.");
                return new RpcResult()
                {
                    Result = call.CreateResult(GenerateResponse(resultStatus))
                };
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

        var status = idToken.Blocked ? AuthorizeStatus.Blocked : AuthorizeStatus.Accepted;
        return new RpcResult()
        {
            Result = call.CreateResult(GenerateResponse(status)),
        };
    }
}
