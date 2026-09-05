using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Logic.Exceptions;
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

    public void FinalizeTransaction(ChargingStation cs, TransactionSnapshot snapshot)
    {
        // first check whether there's a current already persisted snapshot for this transaction
        if (cs.TransactionSnapshots.Any(ts => ts.TransactionId == snapshot.TransactionId))
        {
            // yeah, another request won this race
            throw new TransactionException($"Transaction {snapshot.TransactionId} has already been finalized.");
        }

        cs.TransactionSnapshots.Add(snapshot);
    }
}
