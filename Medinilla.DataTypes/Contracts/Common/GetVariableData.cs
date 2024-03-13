using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

public sealed class GetVariableData
{
    [JsonConstructor]
    public GetVariableData(AttributeType? attributeType, Component component, Variable variable)
    {
        if(attributeType is null)
        {
            AttributeType = AttributeType.Actual;
        }
        else
        {
            AttributeType = attributeType.Value;
        }

        Component = component;
        Variable = variable;
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AttributeType AttributeType { get; private set; }

    public Component Component { get; private set; }

    public Variable Variable { get; private set; }
}
