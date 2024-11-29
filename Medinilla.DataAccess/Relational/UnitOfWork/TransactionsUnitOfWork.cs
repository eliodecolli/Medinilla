using Medinilla.DataAccess.Interfaces;
using Medinilla.DataAccess.Relational.Models;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public sealed class TransactionsUnitOfWork(MedinillaOcppDbContext context)
{
    private IRepository<TransactionEvent> _transactionRepository = new GenericRepository<TransactionEvent>(context);
    private IRepository<TransactionSnapshot> _snapshotRepository = new GenericRepository<TransactionSnapshot>(context);

    public async Task RegisterTransaction(TransactionEvent transaction)
    {
        await _transactionRepository.Create(transaction);
    }

    public async Task<TransactionEvent?> TryGetLatestTransaction(string transactionId)
    {
        var relatedTransactions = await _transactionRepository.Filter(x => x.TransactionId == transactionId);
        return await Task.FromResult(relatedTransactions.OrderByDescending(x => x.SeqNo).FirstOrDefault());
    }

    public async Task<int[]> TryGetMissingTransactions(string transactionId)
    {
        var retval = new List<int>();

        var relatedTransactions = await _transactionRepository.Filter(x => x.TransactionId == transactionId);
        var seqNos = await Task.FromResult(relatedTransactions.Select(x => x.SeqNo).OrderBy(x => x).ToList());

        // this might be a little bit too much but it's the best I can do right now :(
        for (var i = 0; i < seqNos.Count; i++)
        {
            if (i + 1 < seqNos.Count && seqNos[i + 1] - seqNos[i] > 1)
            {
                var range = seqNos[i + 1] - seqNos[i];
                for (var j = 1; j <= range; j++)
                {
                    retval.Add(seqNos[i] + j);
                }
            }
        }

        return [.. retval];
    }

    public async Task<TransactionSnapshot> RegisterFinalSnapshot(TransactionSnapshot snapshot)
    {
        return await _snapshotRepository.Create(snapshot);
    }
}
