using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;

namespace Medinilla.Core.Interfaces.Services;

public interface ITransactionService
{
    Task<IReadOnlyList<TransactionSnapshot>> ListPaged(int offset, int limit);

    void RegisterTransaction(ChargingStation cs, TransactionEvent transaction, IdToken idToken);

    Task<string?> GetTransactionUnit(ChargingStation cs, string transactionId);
}
