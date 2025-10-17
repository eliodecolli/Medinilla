using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Indicates how the measured value is to be interpreted
/// For instance between L1 and neutral (L1-N)
/// When phase is absent, the measured value is interpreted as an overall value
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PhaseEnum
{
    /// <summary>
    /// Phase L1
    /// </summary>
    L1,

    /// <summary>
    /// Phase L2
    /// </summary>
    L2,

    /// <summary>
    /// Phase L3
    /// </summary>
    L3,

    /// <summary>
    /// Neutral line
    /// </summary>
    N,

    /// <summary>
    /// Phase L1 with respect to neutral
    /// </summary>
    L1N,

    /// <summary>
    /// Phase L2 with respect to neutral
    /// </summary>
    L2N,

    /// <summary>
    /// Phase L3 with respect to neutral
    /// </summary>
    L3N,

    /// <summary>
    /// Phase L1 with respect to phase L2
    /// </summary>
    L1L2,

    /// <summary>
    /// Phase L2 with respect to phase L3
    /// </summary>
    L2L3,

    /// <summary>
    /// Phase L3 with respect to phase L1
    /// </summary>
    L3L1
}