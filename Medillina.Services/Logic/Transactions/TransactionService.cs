using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core;
using Medinilla.DataTypes.Core.Enums;

namespace Medinilla.Core.Logic.Transactions;

public sealed class TransactionService
{
    private readonly List<MeasurandEnum> _energyImportMeasurands = new()
    {
        MeasurandEnum.EnergyActiveImportRegister,
        MeasurandEnum.EnergyActiveImportInterval
    };

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

    private EnergyImport GetTotalEnergyImport(IEnumerable<SampledValue> samples)
    {
        // we assume that in order to generate a collection of sampled values, we've ordered the meter values by timestamp descending

        var total = 0.0M;
        var latestConsumptionType = ConsumptionType.Cumulative;

        foreach (var sample in samples)
        {
            var value = ScaleToKW(sample);
            if (sample.Measurand == MeasurandEnum.EnergyActiveImportInterval)
            {
                total += value;
                latestConsumptionType = ConsumptionType.Periodic;
            }
            else if (sample.Measurand == MeasurandEnum.EnergyActiveImportRegister)
            {
                total = value;
                latestConsumptionType = ConsumptionType.Cumulative;
            }
        }

        return new EnergyImport
        {
            EnergyImportValue = total,
            ConsumptionType = latestConsumptionType
        };
    }

    private TransactionConsumption? GetMeterValueConsumption(MeterValue meter)
    {
        var samples = meter.SampledValue.Where(s => s.Measurand.HasValue ? _energyImportMeasurands.Contains(s.Measurand.Value) : false);
        if (samples.Any())
        {
            var samplesConsumption = GetTotalEnergyImport(samples);
            return new TransactionConsumption
            {
                Consumption = samplesConsumption.EnergyImportValue,
                ConsumptionType = samplesConsumption.ConsumptionType,
                Timestamp = meter.Timestamp
            };
        }

        return null;
    }

    public TransactionConsumption GetTransactionConsumption(IEnumerable<MeterValue> meters, DateTime lastMeterTimestamp)
    {
        var latestTransactionConsumptions = meters.Where(m => m.Timestamp > lastMeterTimestamp)
                                                  .Select(GetMeterValueConsumption)
                                                  .Where(c => c is not null)
                                                  .OrderBy(c => c!.Timestamp);

        var consumption = 0.0M;
        var lastConsumptionType = ConsumptionType.Cumulative;

        foreach (var transactionConsumption in latestTransactionConsumptions)
        {
            if (transactionConsumption?.ConsumptionType == ConsumptionType.Cumulative)
            {
                consumption = transactionConsumption.Consumption;
            }
            else if (transactionConsumption?.ConsumptionType == ConsumptionType.Periodic)
            {
                consumption += transactionConsumption.Consumption;
            }
            lastConsumptionType = transactionConsumption?.ConsumptionType ?? lastConsumptionType;
        }

        return new TransactionConsumption
        {
            ConsumptionType = lastConsumptionType,
            Consumption = consumption,
            Timestamp = latestTransactionConsumptions.LastOrDefault()?.Timestamp ?? DateTime.MinValue  // we've already filtered out the nulls here, so this is a bit redundand but the compiler likes it so ¯\_(ツ)_/¯
        };
    }
}
