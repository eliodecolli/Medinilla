using Medinilla.Core.Interfaces;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.Core.Authorization;

namespace Medinilla.Core.Logic.Authorization.Algorithms;

public class DefaultAuthorization : IAuthAlgorithm
{
    public AuthorizationAlgorithm Algorithm => AuthorizationAlgorithm.Default;

    public int Priority => 0;

    public Task<string> Authorize(IdToken? idToken, AuthorizationContext context)
    {
        var status = AuthorizeStatus.Accepted;
        if (idToken is null)
        {
            status = context.SkipIfNullToken ? AuthorizeStatus.Accepted : AuthorizeStatus.Unknown;
        }
        else
        {
            var token = context.Tokens.FirstOrDefault(t => t.Token == idToken.Token);
            if (token is null)
            {
                status = AuthorizeStatus.Unknown;
            }
            else
            {
                if (token.Blocked)
                {
                    status = AuthorizeStatus.Blocked;
                }
                else
                {
                    // pass it over to the next authorizers
                    context.IdToken = token;
                }
            }
        }

        return Task.FromResult(status);
    }
}
