using Medinilla.Core.Logic.TxGraph;
using Medinilla.Core.v1.TxGraph;
using Medinilla.DataTypes.Core.Enums;
using MessagePack;

namespace Medinilla.Core.Interfaces.Transactions;


[Union(0, typeof(SinkNode))]
[Union(1, typeof(MeasurandNode))]
[Union(2, typeof(PhaseNode))]
public interface INode
{
    void AddChild(INode child);
    
    float Compute();

    NodeType GetNodeType();
    
    int GetChildCount();

    INode Copy();
}