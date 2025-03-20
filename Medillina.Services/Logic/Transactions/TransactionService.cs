using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core;
using Medinilla.DataTypes.Core.Enums;

namespace Medinilla.Core.Logic.Transactions;

public sealed class TransactionService
{
    private bool TryGetTotalEnergyRegister(IEnumerable<SampledValue> collection, out SampledValue? sample)
    {
        sample = collection.FirstOrDefault(s => s.Measurand == MeasurandEnum.EnergyActiveImportRegister && s.Phase is null);
        return sample != null;
    }

    private decimal? GetTransactionBeginConsumption(IEnumerable<SampledValue> samples)
    {
        var transactionBeginSamples = samples.Where(s => s.Context == ReadingContextEnum.TransactionBegin);
        if (transactionBeginSamples.Any())
        {
            if (TryGetTotalEnergyRegister(transactionBeginSamples, out var transactionBeginSample))
            {
                // TODO: Handle signed values as well
                return transactionBeginSample!.Value;
            }
        }

        return null;
    }

    private decimal? GetTransactionEndConsumption(IEnumerable<SampledValue> samples)
    {
        var transactionEndSamples = samples.Where(s => s.Context == ReadingContextEnum.TransactionEnd);
        if (transactionEndSamples.Any())
        {
            if (TryGetTotalEnergyRegister(transactionEndSamples, out var transactionEndSample))
            {
                return transactionEndSample!.Value;
            }
        }

        return null;
    }

    private TransactionConsumption? GetTransactionLatestMeterValueConsumption(IEnumerable<MeterValue> meters)
    {
        var latestMeterValue = meters.OrderByDescending(m => m.Timestamp).FirstOrDefault();
        if (latestMeterValue != null)
        {
            if (TryGetTotalEnergyRegister(latestMeterValue.SampledValue, out var consumption))
            {
                return new TransactionConsumption
                {
                    ConsumptionType = ConsumptionType.Cumulative,
                    Consumption = consumption!.Value
                };
            }
        }

        return null;
    }

    public IEnumerable<SampledValue> FlatSamples(IEnumerable<MeterValue> meters)
    {
        return meters.SelectMany(m => m.SampledValue);
    }
}
