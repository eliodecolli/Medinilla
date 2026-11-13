using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Relational.Enums;
using Medinilla.DataAccess.Relational.Models;

namespace Medinilla.Core.v1.Services;

public class ConsumptionService : IConsumptionService
{
    public Task<decimal?> TryComputeConsumption(IEnumerable<TransactionSample> samples)
    {
        // One transaction's overall outlet register readings, already normalized to Wh.
        var readings = samples
            .Where(s => s.Location == MeasurementLocation.Outlet &&
                        s.Measurand == Measurand.EnergyActiveImportRegister &&
                        s.Phase is null)
            .OrderBy(s => s.Timestamp)
            .ToList();

        var begin = readings.LastOrDefault(s => s.Context == ReadingContext.TransactionBegin);
        var current = readings.LastOrDefault(s => s.Context == ReadingContext.TransactionEnd)
                      ?? readings.LastOrDefault(s => s.Context == ReadingContext.SamplePeriodic);

        if (begin is null || current is null)
        {
            return Task.FromResult<decimal?>(null);
        }

        return Task.FromResult<decimal?>(current.Value - begin.Value);
    }
}
