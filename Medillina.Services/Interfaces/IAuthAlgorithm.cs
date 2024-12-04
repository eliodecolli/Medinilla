using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Authorization;

using IdTokenDbContext = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;
using Medinilla.Core.Logic.Authorization;

namespace Medinilla.Core.Interfaces;

public interface IAuthAlgorithm
{
    int Priority { get; }

    AuthorizationAlgorithm Algorithm { get; }

    Task<string> Authorize(IdToken? idToken,
        IdTokenDbContext dbIdToken,
        AuthorizationContext context);
}
