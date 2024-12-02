using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Authorization;

using IdTokenDbContext = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;
using AuthDetailsDbContext = Medinilla.DataAccess.Relational.Models.Authorization.AuthorizationDetails;

namespace Medinilla.Core.Interfaces;

public interface IAuthAlgorithm
{
    AuthorizationAlgorithm Algorithm { get; }

    Task<string?> Authorize(IdToken idToken,
        IdTokenDbContext dbIdToken,
        AuthDetailsDbContext authDetails,
        object? state = null);
}
