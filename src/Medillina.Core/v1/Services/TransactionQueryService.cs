using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.UnitOfWork;

namespace Medinilla.Core.v1.Services;

public sealed class TransactionQueryService(TransactionsUnitOfWork unitOfWork) : ITransactionQueryService
{
    public Task<IReadOnlyList<TransactionSnapshot>> ListPaged(int offset, int limit) =>
        unitOfWork.ListSnapshotsPaged(offset, limit);
}
