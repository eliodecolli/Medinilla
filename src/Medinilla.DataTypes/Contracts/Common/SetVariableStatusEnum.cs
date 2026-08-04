using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SetVariableStatusEnum
{
    Accepted,
    Rejected,
    UnknownComponent,
    UnknownVariable,
    NotSupportedAttributeType,
    RebootRequired
}
