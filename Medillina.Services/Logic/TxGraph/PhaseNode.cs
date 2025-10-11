using Medinilla.Core.Interfaces.Transactions;
using Medinilla.Core.v1.TxGraph;
using Medinilla.DataTypes.Core.Enums;
using MessagePack;

namespace Medinilla.Core.Logic.TxGraph;

[MessagePackObject]
public class PhaseNode : INode
{
    [Key(0)]
    public List<SinkNode>? Children { get; set; }
    
    public NodeType GetNodeType() => NodeType.Phase;
    
    public int GetChildCount() => Children?.Count ?? 0;

    public void AddChild(INode child)
    {
        if (child is not SinkNode sink)
        {
            throw new ArgumentException($"{nameof(child)} must be of type {nameof(SinkNode)}.");
        }
        Children ??= [];
        
        Children.Add(sink);
    }

    public decimal Compute()
    {
        return Children?.Select(c => c.Compute()).Sum() ?? 0;
    }

    public INode Copy()
    {
        var childrenCopy =  new List<SinkNode>();
        foreach (var child in Children ?? [])
        {
            if (child.Copy() is SinkNode nodeCopy)
            {
                childrenCopy.Add(nodeCopy);
            }
        }

        return new PhaseNode()
        {
            Children = childrenCopy
        };
    }
    
    public static PhaseNode operator <<(PhaseNode lhs, PhaseNode? rhs)
    {
        if (rhs is null)
        {
            return lhs;
        }
        
        lhs.Children?.AddRange((rhs.Copy() as PhaseNode)?.Children ?? []);
        return lhs;
    }
}