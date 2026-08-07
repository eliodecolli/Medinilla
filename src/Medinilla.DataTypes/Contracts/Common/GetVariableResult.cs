namespace Medinilla.DataTypes.Contracts.Common;

public class GetVariableResult
{
    public AttributeEnum? AttributeType { get; set; }

    public GetVariableStatusEnum AttributeStatus { get; set; }

    public StatusInfo? AttributeStatusInfo { get; set; }

    public string? AttributeValue { get; set; }

    public Component Component { get; set; }

    public Variable Variable { get; set; }
}
