using System.Text.Json.Serialization;
using Medinilla.DataTypes.Interops;

namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Type of detail value: start, end or sample
/// Default = "Sample.Periodic"
/// </summary>
public enum ReadingContextEnum
{
    /// <summary>
    /// Begin of interruption
    /// </summary>
    InterruptionBegin,

    /// <summary>
    /// End of interruption
    /// </summary>
    InterruptionEnd,

    /// <summary>
    /// Other type of reading
    /// </summary>
    Other,

    /// <summary>
    /// Clock-aligned reading
    /// </summary>
    SampleClock,

    /// <summary>
    /// Periodic reading, based on configured interval
    /// </summary>
    SamplePeriodic,

    /// <summary>
    /// Reading at start of transaction
    /// </summary>
    TransactionBegin,

    /// <summary>
    /// Reading at end of transaction
    /// </summary>
    TransactionEnd,

    /// <summary>
    /// Reading triggered by trigger message
    /// </summary>
    Trigger
}