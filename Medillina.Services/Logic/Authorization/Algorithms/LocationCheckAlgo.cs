using Medinilla.Core.Interfaces;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Authorization;
using System.Text.Json;

namespace Medinilla.Core.Logic.Authorization.Algorithms;

public class LocationCheckAlgo : IAuthAlgorithm
{
    public int Priority => 1;

    public AuthorizationAlgorithm Algorithm => AuthorizationAlgorithm.LocationCheck;

    public Task<string?> Authorize(IdToken? idToken, DataAccess.Relational.Models.Authorization.IdToken dbIdToken, AuthorizationContext context)
    {
        var authDetails = context.AuthorizationDetails.AuthBlob.Deserialize<AuthDetailsBlob>();
        if (authDetails is null ||
            authDetails.LocationCheck is null ||
            context.LocationName is null)
        {
            return Task.FromResult<string?>(null);
        }
        var status = authDetails.LocationCheck.BlockedLocations.Contains(context.LocationName) ?
            AuthorizeStatus.Accepted :
            AuthorizeStatus.NotAtThisLocation;

        return Task.FromResult<string?>(status);
    }
}
