using Medinilla.DataAccess.Interfaces;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core;
using Medinilla.DataTypes.Core.Enums;
using IdToken = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public sealed class TransactionsUnitOfWork(MedinillaOcppDbContext context)
{
    private IRepository<TransactionEvent> _transactionRepository = new GenericRepository<TransactionEvent>(context);
    private IRepository<TransactionSnapshot> _snapshotRepository = new GenericRepository<TransactionSnapshot>(context);

    public async Task<TransactionEvent> RegisterTransaction(TransactionEvent transaction, IdToken? idToken)
    {
        if (idToken is null)
        {
            return await _transactionRepository.Create(transaction);
        }
        
        transaction.IdTokenId = idToken.Id;

        if (!idToken.IsUnderTx)
        {
            idToken.IsUnderTx = true;
        }

        return await _transactionRepository.Create(transaction);
    }

    public async Task<string?> GetTransactionUnit(string transactionId)
    {
        var relatedTransactions = await _transactionRepository.Filter(x => x.TransactionId == transactionId);
        return relatedTransactions.FirstOrDefault()?.UnitName;
    }

    public async Task<IdToken?> TryGetIdTokenForTransaction(string transactionId)
    {
        var query = await _transactionRepository.Filter(tx => tx.TransactionId == transactionId && 
                                                              tx.IdTokenId is not null);
        return query.FirstOrDefault()?.IdToken;
    }

    public async Task FinalizeSnapshot(TransactionEvent? firstEvent, TransactionEvent lastEvent, TransactionConsumption? consumption)
    {
        var snapshot = new TransactionSnapshot();
        snapshot.ChargingStationId = lastEvent.ChargingStationId;
        snapshot.TransactionId = lastEvent.TransactionId;
        snapshot.StartReason = firstEvent?.TriggerReason ?? Enum.GetName(TriggerReasonEnum.AbnormalCondition)!;
        snapshot.EndedAt = lastEvent.Timestamp;
        snapshot.EndReason = lastEvent.TriggerReason;

        snapshot.TotalCost = Convert.ToDecimal(consumption?.Consumption ?? 0);

        await _snapshotRepository.Update(snapshot);
    }
}
