using Medinilla.DataAccess.Relational.Models;

namespace Medinilla.Core.Interfaces.Services;

public interface IConsumptionService
{
    Task<decimal?> TryComputeConsumption(IEnumerable<TransactionSample> samples);
}