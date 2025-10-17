using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Reason the Charging Station sends this message to the CSMS
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TriggerReasonEnum
{
    /// <summary>
    /// Transaction was authorized by authorization mechanism
    /// </summary>
    Authorized,

    /// <summary>
    /// Cable was plugged in to the EV
    /// </summary>
    CablePluggedIn,

    /// <summary>
    /// Charging rate has changed
    /// </summary>
    ChargingRateChanged,

    /// <summary>
    /// Charging state has changed
    /// </summary>
    ChargingStateChanged,

    /// <summary>
    /// Transaction was deauthorized by authorization mechanism
    /// </summary>
    Deauthorized,

    /// <summary>
    /// Maximum energy limit has been reached
    /// </summary>
    EnergyLimitReached,

    /// <summary>
    /// Communication with EV was lost
    /// </summary>
    EVCommunicationLost,

    /// <summary>
    /// EV not connected within timeout
    /// </summary>
    EVConnectTimeout,

    /// <summary>
    /// Clock-aligned meter values
    /// </summary>
    MeterValueClock,

    /// <summary>
    /// Periodic meter values
    /// </summary>
    MeterValuePeriodic,

    /// <summary>
    /// Maximum time limit has been reached
    /// </summary>
    TimeLimitReached,

    /// <summary>
    /// Triggered by trigger message
    /// </summary>
    Trigger,

    /// <summary>
    /// Charging station unlocked by command
    /// </summary>
    UnlockCommand,

    /// <summary>
    /// Stop was authorized by authorization mechanism
    /// </summary>
    StopAuthorized,

    /// <summary>
    /// EV departed from charging station
    /// </summary>
    EVDeparted,

    /// <summary>
    /// EV detected at charging station
    /// </summary>
    EVDetected,

    /// <summary>
    /// Remote stop request received
    /// </summary>
    RemoteStop,

    /// <summary>
    /// Remote start request received
    /// </summary>
    RemoteStart,

    /// <summary>
    /// Abnormal condition detected
    /// </summary>
    AbnormalCondition,

    /// <summary>
    /// Signed data received from EV
    /// </summary>
    SignedDataReceived,

    /// <summary>
    /// Reset command received
    /// </summary>
    ResetCommand
}