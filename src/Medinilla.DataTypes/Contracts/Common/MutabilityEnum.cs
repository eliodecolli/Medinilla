using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MutabilityEnum
{
    ReadOnly,
    WriteOnly,
    ReadWrite
}
