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
            .Where(IsConsumptionReading)
            .OrderBy(s => s.Timestamp)
            .ToList();

        return Task.FromResult(ComputeConsumption(readings));
    }

    public async Task<decimal?> TryComputeConsumption(
        IAsyncEnumerable<TransactionSample> samples,
        CancellationToken cancellationToken = default)
    {
        var readings = new List<TransactionSample>();

        await foreach (var sample in samples.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (IsConsumptionReading(sample))
            {
                readings.Add(sample);
            }
        }

        return ComputeConsumption(readings.OrderBy(s => s.Timestamp).ToList());
    }

    private static bool IsConsumptionReading(TransactionSample sample) =>
        sample.Location == MeasurementLocation.Outlet &&
        sample.Measurand == Measurand.EnergyActiveImportRegister &&
        sample.Phase is null;

    private static decimal? ComputeConsumption(IReadOnlyList<TransactionSample> readings)
    {
        var begin = readings.LastOrDefault(s => s.Context == ReadingContext.TransactionBegin);
        var current = readings.LastOrDefault(s => s.Context == ReadingContext.TransactionEnd)
                      ?? readings.LastOrDefault(s => s.Context == ReadingContext.SamplePeriodic);

        if (begin is null || current is null)
        {
            return null;
        }

        return current.Value - begin.Value;
    }
}
