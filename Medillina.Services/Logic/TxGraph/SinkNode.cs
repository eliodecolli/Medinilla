using Medinilla.Core.Interfaces.Transactions;
using Medinilla.DataTypes.Core.Enums;
using MessagePack;

namespace Medinilla.Core.v1.TxGraph;

[MessagePackObject]
public class SinkNode : INode
{
    public SinkNode()
    {
        Type = NodeType.Unknown;
    }
    
    public SinkNode(float value, NodeType type)
        => (Value, Type) = (value, type);
    
    [Key(0)]
    public float? Value { get; set; }
    
    [Key(1)]
    public int Count { get; set; }

    [Key(2)]
    public NodeType Type { get; set; }

    public void AddChild(INode child)
    {
        Value ??= 0;
        
        var computed = child.Compute();
        Value += computed;
        Count++;
    }

    public int GetChildCount() => Count;
    
    public INode Copy()
    {
        return new SinkNode(Value ?? 0, Type);
    }

    public float Compute()
    {
        return Value ?? 0;
    }

    public NodeType GetNodeType() => Type;

    public static SinkNode operator <<(SinkNode lNode, SinkNode? rNode)
    {
        if (rNode is null)
        {
            return lNode;
        }
        
        lNode.Count += rNode.Count;
        lNode.Value += rNode.Value;
        return lNode;
    }
}
