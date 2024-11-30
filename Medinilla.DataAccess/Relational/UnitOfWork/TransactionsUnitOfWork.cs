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

    public async Task<TransactionSnapshot> RegisterFinalSnapshot(TransactionSnapshot snapshot)
    {
        return await _snapshotRepository.Create(snapshot);
    }
}
