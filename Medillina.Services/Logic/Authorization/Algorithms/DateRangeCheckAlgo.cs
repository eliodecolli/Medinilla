using Medinilla.Core.Interfaces;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.Core.Authorization;
using System.Text.Json;

namespace Medinilla.Core.Logic.Authorization.Algorithms;

public class DateRangeCheckAlgo : IAuthAlgorithm
{
    public int Priority => 1;

    public AuthorizationAlgorithm Algorithm => AuthorizationAlgorithm.DateRangeCheck;

    public Task<string> Authorize(IdToken? idToken, AuthorizationContext context)
    {
        var status = AuthorizeStatus.Accepted;

        var details = context.AuthorizationDetails.AuthBlob.Deserialize<AuthDetailsBlob>();

        if (details is not null &&
            details.DateRangeCheck is not null)
        {
            var now = DateTime.UtcNow;
            status = now > details.DateRangeCheck.Start && now < details.DateRangeCheck.End ?
                AuthorizeStatus.Accepted :
                AuthorizeStatus.NotAtThisTime;
        }

        return Task.FromResult(status);
    }
}
