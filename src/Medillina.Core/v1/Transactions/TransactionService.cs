using Medinilla.Core.Interfaces.Transactions;
using Medinilla.Core.Logic.TxGraph;
using Medinilla.Core.v1.TxGraph;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core;
using Medinilla.DataTypes.Core.Enums;

namespace Medinilla.Core.v1.Transactions;

public sealed class TransactionService
{
    private float ScaleToKW(SampledValue value)
    {
        if (value.UnitOfMeasure.Unit.ToLower() == "wh")
        {
            return Convert.ToSingle(value.Value) / 1000.0f;
        }
        else
        {
            return Convert.ToSingle(value.Value) * (float)Math.Pow(10, value.UnitOfMeasure.Multiplier);
        }
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

        foreach (var current in samples)
        {
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
        
        var graph  = currentGraph ?? new ConsumptionGraph();

        foreach (var meterValue in meters)
        {
            GenerateConsumptionGraph(meterValue.SampledValue, graph);
        }

        return graph;
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
            Consumption = 0.0f
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
                Consumption = 0.0f
            };
        }
        var graph = GetConsumptionGraph(meters,  currentGraph);

        return GetTransactionConsumption(graph);
    }
}
