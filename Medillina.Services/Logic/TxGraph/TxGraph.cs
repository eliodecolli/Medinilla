using Medinilla.Core.Interfaces.Transactions;
using Medinilla.Core.v1.TxGraph;
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

    public decimal Compute()
    {
        return Register?.Compute() ?? 0 + Interval?.Compute() ?? 0;
    }

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
            lgraph.Interval <<=  rgraph?.Interval;
        }

        return lgraph;
    }
}