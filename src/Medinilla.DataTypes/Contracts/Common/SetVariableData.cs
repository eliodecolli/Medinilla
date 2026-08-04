namespace Medinilla.DataTypes.Contracts.Common;

public class SetVariableData
{
    public AttributeEnum? AttributeType { get; set; }

    public string AttributeValue { get; set; }

    public Component Component { get; set; }

    public Variable Variable { get; set; }
}
