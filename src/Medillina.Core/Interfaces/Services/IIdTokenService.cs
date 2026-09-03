using Medinilla.DataTypes.Contracts.Common;
using DbChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using IdTokenDb = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;

namespace Medinilla.Core.Interfaces.Services;

public interface IIdTokenService
{
    Task<IdTokenDb?> TryGetForTransaction(string clientIdentifier, string transactionId);

    bool RemoveTemporaryToken(DbChargingStation cs, IdToken token);
    
    
}
