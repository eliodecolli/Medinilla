using Medinilla.Core.Interfaces;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.Core.Authorization;
using System.Text.Json;

namespace Medinilla.Core.Logic.Authorization.Algorithms;

public class LocationCheckAlgo : IAuthAlgorithm
{
    public int Priority => 1;

    public AuthorizationAlgorithm Algorithm => AuthorizationAlgorithm.LocationCheck;

    public Task<string> Authorize(IdToken? idToken, AuthorizationContext context)
    {
        var status = AuthorizeStatus.Accepted;

        var authDetails = context.AuthorizationDetails.AuthBlob.Deserialize<AuthDetailsBlob>();
        if (authDetails is not null &&
            authDetails.LocationCheck is not null &&
            context.LocationName is not null)
        {
            status = authDetails.LocationCheck.BlockedLocations.Contains(context.LocationName) ?
                AuthorizeStatus.Accepted :
                AuthorizeStatus.NotAtThisLocation;
        }

        return Task.FromResult(status);
    }
}
