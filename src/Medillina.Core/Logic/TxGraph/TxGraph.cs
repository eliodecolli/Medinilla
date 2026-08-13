using Medinilla.Core.Interfaces.Transactions;
using MessagePack;

namespace Medinilla.Core.Logic.TxGraph;

[MessagePackObject]
public class TxGraph
{
    [Key(0)]
    public MeasurandNode? Register { get; set; }
    
    [Key(1)]
    public MeasurandNode? Interval { get; set; }

    public void AddRegister(INode register)
    {
        Register ??= new MeasurandNode();

        Register.AddChild(register);
    }

    public void AddInterval(INode interval)
    {
        Interval ??= new MeasurandNode();

        Interval.AddChild(interval);
    }

    public float Compute()
    {
        return Register?.Compute() ?? Interval?.Compute() ?? 0;
    }

    private float? ComputePhase(int index)
    {
        return Register?.Phases?.Children?[index].Compute() ?? Interval?.Phases?.Children?[index].Compute() ?? null;
    }

    public float? ComputeL1() => ComputePhase(0);
    
    public float? ComputeL2() => ComputePhase(1);
    
    public float? ComputeL3() => ComputePhase(2);

    public TxGraph Copy()
    {
        return new TxGraph()
        {
            Interval = Interval?.Copy() as MeasurandNode,
            Register = Register?.Copy() as MeasurandNode,
        };
    }

    public static TxGraph? operator <<(TxGraph? lgraph, TxGraph? rgraph)
    {
        if (rgraph is null)
        {
            return lgraph;
        }

        if (lgraph is null)
        {
            return rgraph.Copy();
        }
        
        if (lgraph.Register is null)
        {
            lgraph.Register =  rgraph?.Register?.Copy() as MeasurandNode;
        }
        else
        {
            lgraph.Register <<= rgraph.Register;
        }

        if (lgraph.Interval is null)
        {
            lgraph.Interval = rgraph?.Interval?.Copy() as MeasurandNode;
        }
        else
        {
            lgraph.Interval <<= rgraph?.Interval;
        }

        return lgraph;
    }
}