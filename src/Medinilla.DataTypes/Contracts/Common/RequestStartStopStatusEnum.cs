using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RequestStartStopStatusEnum
{
    Accepted,
    Rejected
}