using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.Models;
using Microsoft.EntityFrameworkCore;
using DbChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using IdTokenDb = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;

namespace Medinilla.Core.v1.Services;

public class IdTokenService(MedinillaOcppDbContext context) : IIdTokenService
{
    public IdTokenDb? TryGetForTransaction(DbChargingStation chargingStation,
        IEnumerable<TransactionEvent> currentTransactions,
        string? requestToken)
    {
        if (requestToken is not null)
        {
            return chargingStation.IdTokens.FirstOrDefault(t => t.Token == requestToken);
        }
        return currentTransactions.FirstOrDefault()?.IdToken;
    }

    public bool RemoveTemporaryToken(DbChargingStation chargingStation, IdTokenDb contextIdToken)
    {
        return chargingStation.IdTokens.Remove(contextIdToken);
    }
}
