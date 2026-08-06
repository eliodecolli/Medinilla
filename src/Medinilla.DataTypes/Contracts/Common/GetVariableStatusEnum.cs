using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

[JsonConverter(typeof(TolerantEnumConverter<GetVariableStatusEnum>))]
public enum GetVariableStatusEnum
{
    Unknown = 0,
    Accepted,
    Rejected,
    NotSupportedAttribute,
    UnknownComponent,
    UnknownVariable,
}
