using Medinilla.Core.Logic.Authorization;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class AuthorizeAction(MedinillaOcppDbContext context,
    ILogger<AuthorizeAction> logger,
    AuthorizationAlgorithmFactory authorizationAlgorithmFactory) : IOcppAction
{
    public string ActionName => OcppActionNames.Authorize;

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

        try
        {
            var chargingStation = await context.GetChargingStation(clientIdentifier);
            try
            {
                // Resolve the token against our DB before handing it off. The auth
                // pipeline is now DB-free — the action places the resolved IdToken
                // (or null) on the context and lets the algorithms do their work.
                var tokenEntity = request.IdToken is null
                    ? null
                    : chargingStation.IdTokens.FirstOrDefault(t => t.Token == request.IdToken.Token);

                if (request.IdToken is not null && tokenEntity is null)
                {
                    // Charger sent a token but we don't know it. OCPP distinguishes
                    // Unknown from Invalid, so surface it here rather than letting
                    // the pipeline misreport it.
                    status = AuthorizeStatus.Unknown;
                    logger.LogInformation($"{clientIdentifier}: Unknown token {request.IdToken.Token}.");
                }
                else
                {
                    var authContext = AuthUtils.GenerateAuthContext(chargingStation, null, tokenEntity);
                    status = await authorizationAlgorithmFactory.RunAuthorization(authContext);

                    if (status != AuthorizeStatus.Accepted)
                    {
                        logger.LogInformation($"{clientIdentifier}: Failed Authorization for token {request.IdToken?.Token}: {status}.");
                    }
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
        catch (AggregateRootNotFoundException)
        {
            return new RpcResult()
            {
                Error = call.CreateErrorResult<AuthorizeResponse>(OcppCallError.ErrorCodes.InternalError,
                    $"Specified client identifier {clientIdentifier} was not found.")
            };
        }
    }
}