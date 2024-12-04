using Medinilla.Core.Interfaces;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Authorization;

namespace Medinilla.Core.Logic.Authorization.Algorithms;

public class DefaultAuthorization : IAuthAlgorithm
{
    public AuthorizationAlgorithm Algorithm => AuthorizationAlgorithm.Default;

    public int Priority => 0;

    public Task<string> Authorize(IdToken? idToken, DataAccess.Relational.Models.Authorization.IdToken dbIdToken, AuthorizationContext context)
    {
        var status = AuthorizeStatus.Accepted;
        if (idToken is not null && !context.SkipIfNullToken)
        {
            status = AuthorizeStatus.Unknown;
        }

        return Task.FromResult(status);
    }
}
