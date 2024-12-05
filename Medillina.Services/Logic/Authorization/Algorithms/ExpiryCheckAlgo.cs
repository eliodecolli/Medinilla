using Medinilla.Core.Interfaces;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Authorization;

namespace Medinilla.Core.Logic.Authorization.Algorithms;

public sealed class ExpiryCheckAlgo : IAuthAlgorithm
{
    public AuthorizationAlgorithm Algorithm => AuthorizationAlgorithm.ExpirationCheck;

    public int Priority => 1;

    public Task<string> Authorize(IdToken? idToken,
        AuthorizationContext context)
    {
        var status = AuthorizeStatus.Accepted;

        if (context.IdToken is not null)
        {
            status = context.IdToken.ExpiryDate > DateTime.UtcNow ?
            AuthorizeStatus.Expired :
            AuthorizeStatus.Accepted;
        }

        return Task.FromResult(status);
    }
}
