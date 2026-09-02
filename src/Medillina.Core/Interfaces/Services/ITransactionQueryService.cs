using Medinilla.DataAccess.Relational.Models;

namespace Medinilla.Core.Interfaces.Services;

public interface ITransactionQueryService
{
    Task<IReadOnlyList<TransactionSnapshot>> ListPaged(int offset, int limit);
}
