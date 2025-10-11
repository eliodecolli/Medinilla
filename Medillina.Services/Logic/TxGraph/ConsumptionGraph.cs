using MessagePack;

namespace Medinilla.Core.Logic.TxGraph;

[MessagePackObject]
public class ConsumptionGraph
{
    [Key(0)]
    public TxGraph? Begin { get; set; }
    
    [Key(1)]
    public TxGraph? End { get; set; }
    
    [Key(2)]
    public TxGraph? Sample { get; set; }

    private ConsumptionGraph Copy()
    {
        return new ConsumptionGraph()
        {
            Begin = Begin?.Copy(),
            Sample = Sample?.Copy(),
            End = End?.Copy()
        };
    }

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
        var lgraph = graph?.Copy();
        
        if (lgraph is null)
        {
            return other;
        }

        if (other is null)
        {
            return lgraph;
        }
        
        lgraph.Begin <<= other.Begin;
        lgraph.Sample <<= other.Sample;
        lgraph.End <<= other.End;
        
        return lgraph;
    }
}