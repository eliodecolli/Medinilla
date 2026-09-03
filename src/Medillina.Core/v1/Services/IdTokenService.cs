using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts.Common;
using Microsoft.EntityFrameworkCore;
using DbChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using IdTokenDb = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;

namespace Medinilla.Core.v1.Services;

public class IdTokenService(ChargingStationUnitOfWork unitOfWork) : IIdTokenService
{
    public async Task<IdTokenDb?> TryGetForTransaction(string clientIdentifier, string transactionId)
    {
        await unitOfWork.Start(c => c.ClientIdentifier ==  clientIdentifier);
        var tx = await unitOfWork.TransactionEvents
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

        if (tx is null) return null; // probably dont do this but just so that it's backwards compatible for now
        return tx.IdToken;
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
