using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;

namespace Medinilla.Core.Logic.Authorization;

public static class AuthUtils
{
    public static AuthorizationContext GenerateAuthContext(
        ChargingStation cs,
        int? evseId,
        IdToken? idToken = null,
        bool skipIfNullToken = false)
    {
        return new AuthorizationContext()
        {
            LocationName = cs.Location,
            EvseId = evseId,
            AuthorizationDetails = cs.AuthorizationDetails,
            IdToken = idToken,
            SkipIfNullToken = skipIfNullToken,
        };
    }
}