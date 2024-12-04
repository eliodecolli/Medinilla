using Medinilla.Core.Interfaces;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core.Authorization;
using System.Text.Json;

namespace Medinilla.Core.Logic.Authorization.Algorithms;

public class DateRangeCheckAlgo : IAuthAlgorithm
{
    public int Priority => 1;

    public AuthorizationAlgorithm Algorithm => AuthorizationAlgorithm.DateRangeCheck;

    public Task<string> Authorize(IdToken? idToken, DataAccess.Relational.Models.Authorization.IdToken dbIdToken, AuthorizationContext context)
    {
        var details = context.AuthorizationDetails.AuthBlob.Deserialize<AuthDetailsBlob>();

        if (details is not null &&
            details.DateRangeCheck is not null)
        {
            var now = DateTime.UtcNow;
            var status = now > details.DateRangeCheck.Start && now < details.DateRangeCheck.End ?
                AuthorizeStatus.Accepted :
                AuthorizeStatus.NotAtThisTime;

            return Task.FromResult(status);
        }

        return Task.FromResult(AuthorizeStatus.Accepted);
    }
}
