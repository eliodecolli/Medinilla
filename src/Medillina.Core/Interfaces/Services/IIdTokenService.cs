using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;

namespace Medinilla.Core.Interfaces.Services;

public interface IIdTokenService
{
    IdToken? TryGetForTransaction(ChargingStation chargingStation, IEnumerable<TransactionEvent> currentTransactions, string? requestToken);

    bool RemoveTemporaryToken(ChargingStation cs, IdToken token);
}
