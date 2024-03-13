using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

public sealed class GetVariableResult
{
    [JsonConstructor]
    public GetVariableResult(GetVariableStatus attributeStatus, AttributeType attributeType, string attributeValue,
        Component component, Variable variable, StatusInfo attributeStatusInfo)
    {
        AttributeStatus = attributeStatus;
        AttributeType = attributeType;
        AttributeValue = attributeValue;
        Component = component;
        Variable = variable;
        AttributeStatusInfo = attributeStatusInfo;
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GetVariableStatus AttributeStatus { get; private set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AttributeType AttributeType { get; private set; }

    public string AttributeValue { get; private set; }

    public Component Component { get; private set; }

    public Variable Variable { get; private set; }

    public StatusInfo AttributeStatusInfo { get; private set; }
}
