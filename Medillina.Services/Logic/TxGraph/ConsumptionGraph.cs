using MessagePack;

namespace Medinilla.Core.Logic.TxGraph;

[MessagePackObject]
public class ConsumptionGraph
{
    [Key(0)]
    public Logic.TxGraph.TxGraph? Begin { get; set; }
    
    [Key(1)]
    public Logic.TxGraph.TxGraph? End { get; set; }
    
    [Key(2)]
    public Logic.TxGraph.TxGraph? Sample { get; set; }

    public static ConsumptionGraph Empty = new ConsumptionGraph();

    public static bool IsEmpty(ConsumptionGraph graph)
    {
        return (graph.Begin is null && graph.End is null && graph.Sample is null);
    }

    public static ConsumptionGraph LoadGraph(byte[] buffer)
    {
        return MessagePackSerializer.Deserialize<ConsumptionGraph>(buffer);
    }

    public static byte[] DumpGraph(ConsumptionGraph graph)
    {
        return MessagePackSerializer.Serialize(graph);
    }
    
    public static ConsumptionGraph? operator << (ConsumptionGraph? graph, ConsumptionGraph? other)
    {
        if (graph is null)
        {
            return other;
        }

        if (other is null)
        {
            return graph;
        }
        
        graph.Begin <<= other.Begin;
        graph.Sample <<= other.Sample;
        graph.End <<= other.End;
        
        return graph;
    }
}