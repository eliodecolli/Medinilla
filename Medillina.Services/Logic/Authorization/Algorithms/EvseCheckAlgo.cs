using Medinilla.Core.Interfaces;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Authorization;
using System.Text.Json;

namespace Medinilla.Core.Logic.Authorization.Algorithms;

public sealed class EvseCheckAlgo : IAuthAlgorithm
{
    public AuthorizationAlgorithm Algorithm => AuthorizationAlgorithm.EvseCheck;

    public async Task<string?> Authorize(DataTypes.Contracts.Common.IdToken idToken,
        DataAccess.Relational.Models.Authorization.IdToken dbIdToken,
        AuthorizationDetails authDetails,
        object? state)
    {
        if (authDetails.AuthBlob is null)
        {
            throw new OcppAuthorizationException("Authorization Details Blob was not set.");
        }

        var data = authDetails.AuthBlob.Deserialize<AuthDetailsBlob>();

        if (data.EvseCheck is not null)
        {
            var evseId = (int)state;
            var status = data.EvseCheck.Evses.Any(e => e.EvseId == evseId) ?
                AuthorizeStatus.Accepted :
                AuthorizeStatus.NotAllowedTypeEVSE;

            return status;
        }
        else
        {
            return null;
        }
    }
}
