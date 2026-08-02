using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts.Common;
using DbChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using IdTokenDb = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;

namespace Medinilla.Core.v1.Services;

public sealed class IdTokenService(ChargingStationUnitOfWork unitOfWork)
{
    public Task<IdTokenDb?> TryGetForTransaction(string transactionId, string token)
    {
        return unitOfWork.TryGetIdToken(transactionId, token);
    }

    public bool RemoveTemporaryToken(DbChargingStation cs, IdToken token)
    {
        var contextIdToken = cs.IdTokens.FirstOrDefault(t => t.Token == token.Token);
        if (contextIdToken is not null)
        {
            return cs.IdTokens.Remove(contextIdToken);
        }

        return true;
    }
}
