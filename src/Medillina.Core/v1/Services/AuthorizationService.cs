using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Logic.Authorization;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Relational;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.v1.Services;

public sealed class AuthorizationService(
    MedinillaOcppDbContext context,
    ILogger<AuthorizationService> logger,
    AuthorizationAlgorithmFactory authorizationAlgorithmFactory) : IAuthorizationService
{
    public async Task<AuthorizationProcessingResult> ProcessAuthorization(
        string clientIdentifier,
        AuthorizeRequest request)
    {
        logger.LogInformation($"Processing Authorize request from {clientIdentifier}");

        if (request.ISO15118CertificateHashData is not null || request.Certificate is not null)
        {
            return AuthorizationProcessingResult.UnsupportedCertificate();
        }

        try
        {
            var chargingStation = await context.GetChargingStation(clientIdentifier);
            var status = AuthorizeStatus.Accepted;

            try
            {
                var tokenEntity = request.IdToken is null
                    ? null
                    : chargingStation.IdTokens.FirstOrDefault(t => t.Token == request.IdToken.Token);

                if (request.IdToken is not null && tokenEntity is null)
                {
                    status = AuthorizeStatus.Unknown;
                    logger.LogInformation($"{clientIdentifier}: Unknown token {request.IdToken.Token}.");
                }
                else
                {
                    var authContext = AuthUtils.GenerateAuthContext(chargingStation, null, tokenEntity);
                    status = await authorizationAlgorithmFactory.RunAuthorization(authContext);

                    if (status != AuthorizeStatus.Accepted)
                    {
                        logger.LogInformation(
                            $"{clientIdentifier}: Failed Authorization for token {request.IdToken?.Token}: {status}.");
                    }
                }
            }
            catch (OcppAuthorizationException ex)
            {
                logger.LogError($"{clientIdentifier}: Error during authorization: {ex.Message}");
                return AuthorizationProcessingResult.AuthorizationError(ex.Message);
            }

            return AuthorizationProcessingResult.Completed(GenerateResponse(status));
        }
        catch (AggregateRootNotFoundException)
        {
            return AuthorizationProcessingResult.ChargingStationNotFound();
        }
    }

    private static AuthorizeResponse GenerateResponse(string status)
    {
        return new AuthorizeResponse
        {
            IdTokenInfo = new IdTokenInfo
            {
                Status = status,
                CacheExpiryDateTime = DateTime.Now,
            },
            CertificateStatus = AuthorizeCertificateStatus.NoCertificateAvailable,
        };
    }
}
