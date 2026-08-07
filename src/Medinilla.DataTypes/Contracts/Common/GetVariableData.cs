namespace Medinilla.DataTypes.Contracts.Common;

public class GetVariableData
{
    public Component Component { get; set; }

    public Variable Variable { get; set; }

    public AttributeEnum? AttributeType { get; set; }
}
