using Medinilla.Core.Interfaces.Transactions;
using Medinilla.Core.Logic.TxGraph;
using Medinilla.Core.v1.TxGraph;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core;
using Medinilla.DataTypes.Core.Enums;

namespace Medinilla.Core.v1.Transactions;

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

    private INode GetNodeForSampleValue(SampledValue sample)
    {
        if (sample.Phase is not null)
        {
            return new SinkNode(ScaleToKW(sample), NodeType.Phase);
        }
        else
        {
            return new SinkNode(ScaleToKW(sample), NodeType.Outlet);
        }
    }

    private void AddGraphNode(SampledValue sample, Logic.TxGraph.TxGraph subGraph)
    {
        var node = GetNodeForSampleValue(sample);

        switch (sample.Measurand)
        {
            case MeasurandEnum.EnergyActiveImportRegister:
                subGraph.AddRegister(node);
                break;
            case MeasurandEnum.EnergyActiveImportInterval:
                subGraph.AddInterval(node);
                break;
        }
    }

    private ConsumptionGraph GenerateConsumptionGraph(IEnumerable<SampledValue> samples,
        ConsumptionGraph? currentGraph = null)
    {
        var graph = currentGraph ?? new ConsumptionGraph();
        
        using var iter = samples.GetEnumerator();
        while (iter.MoveNext())
        {
            var current = iter.Current;

            switch (current.Context)
            {
                case ReadingContextEnum.TransactionBegin:
                    graph.Begin ??= new Logic.TxGraph.TxGraph();
                    AddGraphNode(current, graph.Begin);
                    break;
                case ReadingContextEnum.TransactionEnd:
                    graph.End ??= new Logic.TxGraph.TxGraph();
                    AddGraphNode(current, graph.End);
                    break;
                case ReadingContextEnum.SamplePeriodic:
                    graph.Sample ??= new Logic.TxGraph.TxGraph();
                    AddGraphNode(current, graph.Sample);
                    break;
            }
        }

        return graph;
    }

    public ConsumptionGraph GetConsumptionGraph(IEnumerable<MeterValue>? meters, ConsumptionGraph? currentGraph = null)
    {
        if (meters is null)
        {
            return ConsumptionGraph.Empty;
        }
        
        var samples = meters.OrderBy(c => c.Timestamp)
            .SelectMany(t => t.SampledValue);
        
        return GenerateConsumptionGraph(samples,  currentGraph);
    }

    public TransactionConsumption GetTransactionConsumption(ConsumptionGraph graph)
    {
        if (graph.End is not null)
        {
            return new TransactionConsumption()
            {
                Consumption = graph.End.Compute() - graph.Begin?.Compute() ?? 0,
                ConsumptionType = ConsumptionType.Cumulative,
                Timestamp = DateTime.UtcNow,
            };
        }

        if (graph.Sample is not null)
        {
            return new TransactionConsumption()
            {
                Consumption = graph.Sample.Compute(),
                ConsumptionType = ConsumptionType.Cumulative,
                Timestamp = DateTime.UtcNow,
            };
        }

        return new TransactionConsumption()
        {
            Timestamp = DateTime.UtcNow,
            ConsumptionType = ConsumptionType.Periodic,
            Consumption = 0.0M
        };
    }

    /// <summary>
    /// Generates a final transaction consumption.
    /// </summary>
    /// <param name="meters">The incoming meter values from the request.</param>
    /// <param name="currentGraph">The current transaction graph (if any).</param>
    /// <remarks>
    /// If currentGraph is set, then once this method finishes, the reference to "currentGraph" will contain the final
    /// graph. This is faster than using the sink operator to merge two graphs. 
    /// </remarks>
    /// <returns></returns>
    public TransactionConsumption GetTransactionConsumption(IEnumerable<MeterValue>? meters,
        ConsumptionGraph? currentGraph = null)
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
        var graph = GetConsumptionGraph(meters,  currentGraph);

        return GetTransactionConsumption(graph);
    }
}
