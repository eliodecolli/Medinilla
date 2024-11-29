using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// This contains the current status of the Connector.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConnectorStatusEnum
{
    Available,
    Occupied,
    Reserved,
    Unavailable,
    Faulted
}