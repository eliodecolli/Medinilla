using Medinilla.DataAccess.Interfaces;
using Medinilla.DataAccess.Relational.Models;
using Microsoft.EntityFrameworkCore;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public sealed class TransactionsUnitOfWork(MedinillaOcppDbContext context)
{
    private IRepository<Transaction> _transactionRepository = new GenericRepository<Transaction>(context);

    public async Task RegisterTransaction(Transaction transaction)
    {
        await _transactionRepository.Create(transaction);
    }

    public async Task<Transaction?> TryGetLatestTransaction(string transactionId)
    {
        var relatedTransactions = await _transactionRepository.Filter(x => x.TransactionId == transactionId);
        return await relatedTransactions.OrderByDescending(x => x.SeqNo).FirstOrDefaultAsync();
    }

    public async Task<int[]> TryGetMissingTransactions(string transactionId)
    {
        var retval = new List<int>();

        var relatedTransactions = await _transactionRepository.Filter(x => x.TransactionId == transactionId);
        var seqNos = await relatedTransactions.Select(x => x.SeqNo).OrderBy(x => x).ToListAsync();

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

    public async Task Save()
    {
        await context.SaveChangesAsync();
    }
}
