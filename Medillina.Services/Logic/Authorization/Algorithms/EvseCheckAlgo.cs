using Medinilla.Core.Interfaces;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.Core.Authorization;
using System.Text.Json;

namespace Medinilla.Core.Logic.Authorization.Algorithms;

public sealed class EvseCheckAlgo : IAuthAlgorithm
{
    public AuthorizationAlgorithm Algorithm => AuthorizationAlgorithm.EvseCheck;

    public int Priority => 1;

    public Task<string> Authorize(IdToken? idToken,
        AuthorizationContext context)
    {
        var status = AuthorizeStatus.Accepted;

        var authDetails = context.AuthorizationDetails;
        if (authDetails.AuthBlob is null)
        {
            throw new OcppAuthorizationException("Authorization Details Blob was not set.");
        }

        var data = authDetails.AuthBlob.Deserialize<AuthDetailsBlob>();

        if (data is not null &&
            data.EvseCheck is not null &&
            context.EvseId is not null)
        {
            var evseId = context.EvseId;
            status = data.EvseCheck.Evses.Any(e => e.EvseId == evseId) ?
                AuthorizeStatus.Accepted :
                AuthorizeStatus.NotAllowedTypeEVSE;
        }

        return Task.FromResult(status);
    }
}
