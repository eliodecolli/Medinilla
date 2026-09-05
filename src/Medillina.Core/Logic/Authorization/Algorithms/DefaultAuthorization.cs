using Medinilla.Core.Interfaces;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.Core.Authorization;

namespace Medinilla.Core.Logic.Authorization.Algorithms;

public class DefaultAuthorization : IAuthAlgorithm
{
    public AuthorizationAlgorithm Algorithm => AuthorizationAlgorithm.Default;

    public int Priority => 0;

    public Task<string> Authorize(AuthorizationContext context)
    {
        // The action layer is responsible for resolving the IdToken (DB lookup
        // for a request-supplied token, or recovery from a previous event) and
        // placing it on the context before invoking the pipeline. We don't
        // query the DB here.
        var idToken = context.IdToken;

        if (idToken is null)
        {
            return Task.FromResult(
                context.SkipIfNullToken ? AuthorizeStatus.Accepted : AuthorizeStatus.Invalid);
        }

        if (idToken.Blocked)
        {
            return Task.FromResult(AuthorizeStatus.Blocked);
        }

        return Task.FromResult(AuthorizeStatus.Accepted);
    }
}