using Medinilla.DataAccess.Interfaces;
using Medinilla.DataAccess.Relational.Models;
using Microsoft.EntityFrameworkCore;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public sealed class TransactionsUnitOfWork(MedinillaOcppDbContext context)
{
    private IRepository<TransactionEvent> _transactionRepository = new GenericRepository<TransactionEvent>(context);
    private IRepository<TransactionSnapshot> _snapshotRepository = new GenericRepository<TransactionSnapshot>(context);

    // TODO: This motherfucker needs some refactoring: https://github.com/eliodecolli/Medinilla/issues/49
    public async Task<IReadOnlyList<TransactionSnapshot>> ListSnapshotsPaged(int offset, int limit)
    {
        const int defaultLimit = 50;
        const int maxLimit = 200;

        var safeOffset = Math.Max(offset, 0);
        var safeLimit = limit <= 0 ? defaultLimit : Math.Min(limit, maxLimit);

        return await context.Set<TransactionSnapshot>()
            .Include(s => s.ChargingStation)
            .Include(s => s.EvseConnector)
            .Include(s => s.IdToken)
            .OrderBy(s => s.Id)
            .Skip(safeOffset)
            .Take(safeLimit)
            .ToListAsync();
    }
}
