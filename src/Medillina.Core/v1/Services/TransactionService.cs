using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.UnitOfWork;
using ChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using IdToken = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;

namespace Medinilla.Core.v1.Services;

public sealed class TransactionService(TransactionsUnitOfWork unitOfWork) : ITransactionService
{
    public Task<IReadOnlyList<TransactionSnapshot>> ListPaged(int offset, int limit) =>
        unitOfWork.ListSnapshotsPaged(offset, limit);
    
    public void RegisterTransaction(ChargingStation cs, TransactionEvent transaction, IdToken idToken)
    {
        transaction.IdToken = idToken;

        if (!idToken.IsUnderTx)
        {
            idToken.IsUnderTx = true;
        }

        cs.TransactionEvents.Add(transaction);
    }
    
    public Task<string?> GetTransactionUnit(ChargingStation cs, string transactionId)
    {
        var unit = cs.TransactionEvents?
            .FirstOrDefault(x => x.TransactionId == transactionId &&
                                 !string.IsNullOrEmpty(x.UnitName))?.UnitName;
        return Task.FromResult(unit);
    }
}
