using Medinilla.Core.Interfaces.Transactions;
using Medinilla.Core.v1.TxGraph;
using Medinilla.DataTypes.Core.Enums;
using MessagePack;

namespace Medinilla.Core.Logic.TxGraph;


[MessagePackObject]
public class MeasurandNode : INode
{
    [Key(0)]
    public SinkNode? Outlet { get; set; }  // this is essentially a value node, since Register->Outlet->Value is an array consisting of 1 element
    
    [Key(1)]
    public PhaseNode? Phases { get; set; }
    
    public NodeType GetNodeType() => NodeType.Measurand;
    
    public int GetChildCount() => 2;  // cause it's always two
    
    public INode Copy()
    {
        return new MeasurandNode()
        {
            Outlet = Outlet?.Copy() as SinkNode,
            Phases = Phases?.Copy() as PhaseNode,
        };
    }

    private void _AddOutlet(INode outlet)
    {
        if (Outlet is null)
        {
            Outlet = new SinkNode(0, NodeType.Outlet);
        }
        
        Outlet.AddChild(outlet);
    }

    public void _AddPhase(INode phase)
    {
        if (Phases is null)
        {
            Phases = new PhaseNode();
        }
        
        Phases.AddChild(phase);
    }

    public void AddChild(INode child)
    {
        switch (child.GetNodeType())
        {
            case NodeType.Outlet:
                _AddOutlet(child);
                break;
            case NodeType.Phase:
                _AddPhase(child);
                break;
            default:
                throw new InvalidOperationException($"Trying to add child of type {child.GetNodeType()} to MeasurandNode. Only allowed types are Outlet and Phase.");
        }
    }

    public decimal Compute()
    {
        if (Outlet?.GetChildCount() > 0)
        {
            return Outlet.Compute();  // prefer this, since Outlet == Phases.Sum()
        }
        
        return Phases?.Compute() ?? 0;
    }
    
    public static MeasurandNode operator <<(MeasurandNode mLhs, MeasurandNode? mRhs)
    {
        if (mRhs is null)
        {
            return mLhs!;
        }
        
        if (mLhs.Outlet is null)
        {
            mLhs.Outlet = mRhs.Outlet;
        }
        else
        {
            mLhs.Outlet <<= mRhs.Outlet;
        }

        if (mLhs.Phases is null)
        {
            mLhs.Phases = mRhs.Phases;
        }
        else
        {
            mLhs.Phases <<= mRhs.Phases;
        }
        
        return mLhs;
    }
}