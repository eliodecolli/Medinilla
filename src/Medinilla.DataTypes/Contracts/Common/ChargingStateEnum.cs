using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Current charging state, is required when state has changed
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChargingStateEnum
{
    /// <summary>
    /// EV is charging, power is flowing to vehicle
    /// </summary>
    Charging,

    /// <summary>
    /// EV is connected but not charging
    /// </summary>
    EVConnected,

    /// <summary>
    /// Charging has been suspended by the EV
    /// </summary>
    SuspendedEV,

    /// <summary>
    /// Charging has been suspended by the EVSE
    /// </summary>
    SuspendedEVSE,

    /// <summary>
    /// No EV is connected to the EVSE
    /// </summary>
    Idle
}
