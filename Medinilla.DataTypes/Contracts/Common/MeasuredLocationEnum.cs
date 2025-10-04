using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Indicates where the measured value has been sampled
/// Default = "Outlet"
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LocationEnum
{
    /// <summary>
    /// Value measured at charge point body
    /// </summary>
    Body,

    /// <summary>
    /// Value measured at cable
    /// </summary>
    Cable,

    /// <summary>
    /// Value measured at EV
    /// </summary>
    EV,

    /// <summary>
    /// Value measured at charge point inlet
    /// </summary>
    Inlet,

    /// <summary>
    /// Value measured at charge point outlet
    /// </summary>
    Outlet
}
