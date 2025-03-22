using Medinilla.DataAccess.Interfaces;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataTypes.Core;
using Medinilla.DataTypes.Core.Enums;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public sealed class TransactionsUnitOfWork(MedinillaOcppDbContext context)
{
    private IRepository<TransactionEvent> _transactionRepository = new GenericRepository<TransactionEvent>(context);
    private IRepository<TransactionSnapshot> _snapshotRepository = new GenericRepository<TransactionSnapshot>(context);

    private decimal GetTotalConsumption(decimal currentConsumption, TransactionConsumption? consumption)
    {
        if (consumption is null)
        {
            return currentConsumption;
        }

        var result = currentConsumption;
        if (consumption.ConsumptionType == ConsumptionType.Cumulative)
        {
            result = consumption.Consumption;
        }
        else if (consumption.ConsumptionType == ConsumptionType.Periodic)
        {
            result += consumption.Consumption;
        }

        return result;
    }

    public async Task RegisterTransaction(TransactionEvent transaction, IdToken? idToken)
    {
        if (idToken is not null)
        {
            transaction.IdTokenId = idToken.Id;

            if (!idToken.IsUnderTx)
            {
                idToken.IsUnderTx = true;
            }
        }

        await _transactionRepository.Create(transaction);
    }

    public async Task<string?> GetTransactionUnit(string transactionId)
    {
        var relatedTransactions = await _transactionRepository.Filter(x => x.TransactionId == transactionId);
        return relatedTransactions.FirstOrDefault()?.UnitName;
    }

    public async Task<TransactionEvent?> TryGetLatestTransaction(string transactionId)
    {
        var relatedTransactions = await _transactionRepository.Filter(x => x.TransactionId == transactionId);
        return await Task.FromResult(relatedTransactions.OrderByDescending(x => x.SeqNo).FirstOrDefault());
    }

    public async Task<int[]> TryGetMissingTransactionsForIncomingEvent(IEnumerable<TransactionEvent> relatedTransactions, int incomingSeqNo)
    {
        var retval = new HashSet<int>();

        var seqNos = await Task.FromResult(relatedTransactions.Select(x => x.SeqNo).ToList());
        seqNos.Add(incomingSeqNo);

        var lastSeqNo = seqNos.OrderBy(x => x).LastOrDefault();

        for (var i = 1; i <= lastSeqNo; i++)
        {
            if (!seqNos.Contains(i))
            {
                // yeah we're already past it
                retval.Add(i);
            }
        }

        return [.. retval];
    }

    public async Task<TransactionSnapshot> GetOrCreateSnapshot(TransactionEvent @event)
    {
        var snapshots = await _snapshotRepository.Filter(s => s.TransactionEvents.Where(e => e.TransactionId == @event.TransactionId).Any()).ConfigureAwait(false);
        var snapshot = snapshots.FirstOrDefault();

        if (snapshot is not null)
        {
            return snapshot;
        }
        else
        {
            var newSnapshot = new TransactionSnapshot
            {
                ChargingStationId = @event.ChargingStationId,
                TransactionId = @event.TransactionId,
                StartedAt = @event.Timestamp,
                LastEvent = @event.Timestamp,
                StartReason = @event.TriggerReason,
            };

            return await _snapshotRepository.Create(newSnapshot);
        }
    }

    public async Task<TransactionSnapshot> UpdateSnapshot(TransactionEvent @event, TransactionConsumption? consumption, TransactionSnapshot snapshot)
    {
        snapshot.TotalMeteredValue = GetTotalConsumption(snapshot.TotalMeteredValue, consumption);
        snapshot.LastEvent = @event.Timestamp;
        @event.TransactionSnapshotId = snapshot.Id;

        await _transactionRepository.Update(@event);

        return await _snapshotRepository.Update(snapshot);
    }

    public async Task<TransactionSnapshot> FinalizeSnapshot(TransactionEvent @event, TransactionSnapshot snapshot, TransactionConsumption? consumption)
    {
        snapshot.EndedAt = @event.Timestamp;
        snapshot.EndReason = @event.TriggerReason;
        snapshot.TotalCost = GetTotalConsumption(snapshot.TotalMeteredValue, consumption);

        @event.TransactionSnapshotId = snapshot.Id;
        await _transactionRepository.Update(@event);

        return await _snapshotRepository.Update(snapshot);
    }
}
