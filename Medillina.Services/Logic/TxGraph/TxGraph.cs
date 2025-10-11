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
        var lhs = lgraph?.Copy();
        var rhs = rgraph?.Copy();
        
        if (rhs is null)
        {
            return lhs;
        }

        if (lhs is null)
        {
            return rhs;
        }
        
        if (lhs.Register is null)
        {
            lhs.Register =  rhs.Register;
        }
        else
        {
            lhs.Register <<= rhs.Register;
        }

        if (lhs.Interval is null)
        {
            lhs.Interval = rhs.Interval;
        }
        else
        {
            lhs.Interval <<=  rhs.Interval;
        }

        return lhs;
    }
}