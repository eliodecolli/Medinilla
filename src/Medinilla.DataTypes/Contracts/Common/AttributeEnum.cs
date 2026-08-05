using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttributeEnum
{
    Actual,
    Target,
    MinSet,
    MaxSet
}
