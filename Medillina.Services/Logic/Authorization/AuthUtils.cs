using Medinilla.DataAccess.Relational.Models;

namespace Medinilla.Core.Logic.Authorization;

public static class AuthUtils
{
    public static AuthorizationContext GenerateAuthContext(ChargingStation cs, int? evseId, bool skipIfNullToken)
    {
        return new AuthorizationContext()
        {
            LocationName = cs.Location,
            EvseId = evseId,
            AuthorizationDetails = cs.AuthorizationDetails,
            SkipIfNullToken = skipIfNullToken
        };
    }
}
