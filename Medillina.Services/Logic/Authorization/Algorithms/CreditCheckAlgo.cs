using Medinilla.Core.Interfaces;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.Core.Authorization;
using System.Text.Json;

namespace Medinilla.Core.Logic.Authorization.Algorithms;

public class CreditCheckAlgo : IAuthAlgorithm
{
    public int Priority => 1;

    public AuthorizationAlgorithm Algorithm => AuthorizationAlgorithm.CreditCheck;

    public Task<string> Authorize(IdToken? idToken, AuthorizationContext context)
    {
        var status = AuthorizeStatus.Accepted;
        var details = context.AuthorizationDetails.AuthBlob.Deserialize<AuthDetailsBlob>();

        if(context.IdToken is not null &&
            details is not null &&
            details.CreditCheck is not null &&
            details.CreditCheck.Flag)
        {
            status = (context.IdToken.User.ActiveCredit ?? -1.0M) >= (context.UserActiveCredit ?? 0.0M) ?
                AuthorizeStatus.Accepted :
                AuthorizeStatus.NoCredit;

            return Task.FromResult(status);
        }

        return Task.FromResult(status);
    }
}
