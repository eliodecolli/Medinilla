using Medinilla.Core.Interfaces;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Authorization;

namespace Medinilla.Core.Logic.Authorization.Algorithms;

public sealed class ExpiryCheckAlgo : IAuthAlgorithm
{
    public AuthorizationAlgorithm Algorithm => AuthorizationAlgorithm.ExpirationCheck;

    public int Priority => 1;

    public async Task<string> Authorize(IdToken idToken,
        DataAccess.Relational.Models.Authorization.IdToken dbIdToken,
        AuthorizationContext context)
    {
        var status = dbIdToken.ExpiryDate > DateTime.UtcNow ?
            AuthorizeStatus.Expired :
            AuthorizeStatus.Accepted;

        return status;
    }
}
