using Medinilla.Core.Interfaces;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Authorization;
using System.Text.Json;

namespace Medinilla.Core.Logic.Authorization.Algorithms;

public class CreditCheckAlgo : IAuthAlgorithm
{
    public int Priority => 1;

    public AuthorizationAlgorithm Algorithm => AuthorizationAlgorithm.CreditCheck;

    public Task<string> Authorize(IdToken? idToken, DataAccess.Relational.Models.Authorization.IdToken dbIdToken, AuthorizationContext context)
    {
        var details = context.AuthorizationDetails.AuthBlob.Deserialize<AuthDetailsBlob>();

        if(details is not null &&
            details.CreditCheck is not null &&
            details.CreditCheck.Flag)
        {
            var status = (dbIdToken.User.ActiveCredit ?? -1.0M) >= (context.UserActiveCredit ?? 0.0M) ?
                AuthorizeStatus.Accepted :
                AuthorizeStatus.NoCredit;

            return Task.FromResult(status);
        }

        return Task.FromResult(AuthorizeStatus.Accepted);
    }
}
