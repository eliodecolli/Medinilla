using Medinilla.Core.Interfaces;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Authorization;

namespace Medinilla.Core.Logic.Authorization.Algorithms;

public sealed class ExpiryCheckAlgo : IAuthAlgorithm
{
    public AuthorizationAlgorithm Algorithm => AuthorizationAlgorithm.ExpirationCheck;

    public async Task<string?> Authorize(DataTypes.Contracts.Common.IdToken idToken,
        DataAccess.Relational.Models.Authorization.IdToken dbIdToken,
        AuthorizationDetails authDetails,
        object? state)
    {
        var status = dbIdToken.ExpiryDate > DateTime.UtcNow ?
            AuthorizeStatus.Expired :
            AuthorizeStatus.Accepted;

        return status;
    }
}
