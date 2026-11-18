using Medinilla.DataAccess.Relational.Models;

namespace Medinilla.Core.Interfaces.Services;

public interface IConsumptionService
{
    Task<decimal?> TryComputeConsumption(IEnumerable<TransactionSample> samples);

    /// <summary>
    /// Async variant used when samples are still a database query. Cancellation
    /// propagates into query enumeration.
    /// </summary>
    Task<decimal?> TryComputeConsumption(
        IAsyncEnumerable<TransactionSample> samples,
        CancellationToken cancellationToken = default);
}