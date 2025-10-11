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

    public decimal Compute()
    {
        return Register?.Compute() ?? 0 + Interval?.Compute() ?? 0;
    }

    public static TxGraph? operator <<(TxGraph? lhs, TxGraph? rhs)
    {
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