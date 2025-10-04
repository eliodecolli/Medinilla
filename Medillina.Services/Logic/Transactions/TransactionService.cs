using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core;
using Medinilla.DataTypes.Core.Enums;

namespace Medinilla.Core.Logic.Transactions;

public sealed class TransactionService
{
    private decimal ScaleToKW(SampledValue value)
    {
        if (value.UnitOfMeasure.Unit.ToLower() == "wh")
        {
            return value.Value / 1000;
        }
        else
        {
            return value.Value * (decimal)Math.Pow(10, value.UnitOfMeasure.Multiplier);
        }
    }

    private EnergyImport? GetTotalEnergyImportForContextPhases(IEnumerable<SampledValue> samples, ReadingContextEnum context, bool checkPhases)
    {
        // first we check for total cumulative consumption
        decimal? total = null;

        if (!checkPhases)
        {
            var first = samples.FirstOrDefault(s => s.Context == context &&
               (s.Phase is not null) == checkPhases &&
                s.Measurand == MeasurandEnum.EnergyActiveImportRegister);

            if (first is not null)
            {
                total = ScaleToKW(first);
            }
        }
        else
        {
            // when checking phases we need the sum of total phases
            total = samples.Where(s => s.Context == context &&
               (s.Phase is not null) == checkPhases &&
                s.Measurand == MeasurandEnum.EnergyActiveImportRegister).Select(ScaleToKW).Sum();
        }

        if (total is not null)
        {
            return new EnergyImport
            {
                EnergyImportValue = total.Value,
                ConsumptionType = ConsumptionType.Cumulative,
            };
        }


        var sum = samples.Where(s => s.Context == context && 
            (s.Phase is not null) == checkPhases && 
            s.Measurand == MeasurandEnum.EnergyActiveImportInterval)
            .Sum(ScaleToKW);

        if (sum > 0)
        {
            return new EnergyImport
            {
                EnergyImportValue = sum,
                ConsumptionType = context == ReadingContextEnum.SamplePeriodic ? ConsumptionType.Periodic : ConsumptionType.Cumulative,
            };
        }

        return null;
    }

    private EnergyImport GetTotalEnergyImport(IEnumerable<SampledValue> samples)
    {
        // first we try to get the total phase for the begging and end
        var totalBegin = GetTotalEnergyImportForContextPhases(samples, ReadingContextEnum.TransactionBegin, false);
        var totalEnd = GetTotalEnergyImportForContextPhases(samples, ReadingContextEnum.TransactionEnd, false);
        if (totalEnd is not null)
        {
            return new EnergyImport
            {
                EnergyImportValue = totalEnd.EnergyImportValue - totalBegin?.EnergyImportValue ?? 0.0M,
                ConsumptionType = ConsumptionType.Cumulative
            };
        }
        else
        {
            var totalPhaseBegin = GetTotalEnergyImportForContextPhases(samples, ReadingContextEnum.TransactionBegin, true);
            var totalPhaseEnd = GetTotalEnergyImportForContextPhases(samples, ReadingContextEnum.TransactionEnd, true);
            if (totalPhaseEnd is not null)
            {
                return new EnergyImport
                {
                    EnergyImportValue = totalPhaseEnd.EnergyImportValue - totalPhaseBegin?.EnergyImportValue ?? 0.0M,
                    ConsumptionType = ConsumptionType.Cumulative
                };
            }
        }

        // try checking for periodic samples
        var totalConsumptionPeriodic = GetTotalEnergyImportForContextPhases(samples, ReadingContextEnum.SamplePeriodic, false);
        if (totalConsumptionPeriodic is not null)
        {
            return totalConsumptionPeriodic;
        }

        var totalConsumptionPeriodicPhases = GetTotalEnergyImportForContextPhases(samples, ReadingContextEnum.SamplePeriodic, true);
        if (totalConsumptionPeriodicPhases is not null)
        {
            return totalConsumptionPeriodicPhases;
        }

        throw new InvalidOperationException("Could not determine transaction consumption");
    }

    public TransactionConsumption GetTransactionConsumption(IEnumerable<MeterValue>? meters)
    {
        if (meters is null)
        {
            return new TransactionConsumption()
            {
                Timestamp = DateTime.UtcNow,
                ConsumptionType = ConsumptionType.Periodic,
                Consumption = 0.0M
            };
        }

        var latestTransactionConsumptions = meters.OrderBy(c => c.Timestamp)
                                                  .SelectMany(t => t.SampledValue);

        var total = GetTotalEnergyImport(latestTransactionConsumptions);
        return new TransactionConsumption()
        {
            Timestamp = DateTime.UtcNow,
            ConsumptionType = ConsumptionType.Cumulative,
            Consumption = total.EnergyImportValue
        };
    }
}
