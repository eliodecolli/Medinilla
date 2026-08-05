namespace Medinilla.DataTypes.Contracts.Common;

public class SetVariableResult
{
    public AttributeEnum? AttributeType { get; set; }

    public SetVariableStatusEnum AttributeStatus { get; set; }

    public StatusInfo? AttributeStatusInfo { get; set; }

    public Component Component { get; set; }

    public Variable Variable { get; set; }
}
