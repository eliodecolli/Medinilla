using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataTypes.Contracts.Common;
using DbChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using IdTokenDb = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;

namespace Medinilla.Core.Interfaces.Services;

public interface IIdTokenService
{
    Task<IdTokenDb?> TryGetForTransaction(string transactionId, string token);

    bool RemoveTemporaryToken(DbChargingStation cs, IdToken token);
}
