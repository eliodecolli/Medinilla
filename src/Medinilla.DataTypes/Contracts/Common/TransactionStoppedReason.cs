using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// This contains the reason why the transaction was stopped.
/// MAY only be omitted when Reason is "Local"
/// </summary>
public enum ReasonEnum
{
    /// <summary>
    /// The transaction was stopped because of de-authorization
    /// </summary>
    DeAuthorized,

    /// <summary>
    /// Emergency stop button was pressed
    /// </summary>
    EmergencyStop,

    /// <summary>
    /// A maximum energy limit was reached
    /// </summary>
    EnergyLimitReached,

    /// <summary>
    /// The EV was disconnected
    /// </summary>
    EVDisconnected,

    /// <summary>
    /// Ground fault circuit interrupter was triggered
    /// </summary>
    GroundFault,

    /// <summary>
    /// Transaction was stopped due to immediate reset command
    /// </summary>
    ImmediateReset,

    /// <summary>
    /// Transaction was stopped locally at the charging station
    /// </summary>
    Local,

    /// <summary>
    /// Transaction was stopped because local credit ran out
    /// </summary>
    LocalOutOfCredit,

    /// <summary>
    /// Master pass was used to stop transaction
    /// </summary>
    MasterPass,

    /// <summary>
    /// Any other reason
    /// </summary>
    Other,

    /// <summary>
    /// Over current protection was triggered
    /// </summary>
    OvercurrentFault,

    /// <summary>
    /// Power loss occurred
    /// </summary>
    PowerLoss,

    /// <summary>
    /// Power quality issues occurred
    /// </summary>
    PowerQuality,

    /// <summary>
    /// Charging station rebooted
    /// </summary>
    Reboot,

    /// <summary>
    /// Transaction was stopped remotely
    /// </summary>
    Remote,

    /// <summary>
    /// A maximum SOC (State of Charge) limit was reached
    /// </summary>
    SOCLimitReached,

    /// <summary>
    /// Transaction was stopped by the EV
    /// </summary>
    StoppedByEV,

    /// <summary>
    /// A maximum time limit was reached
    /// </summary>
    TimeLimitReached,

    /// <summary>
    /// Transaction stopped due to a timeout
    /// </summary>
    Timeout
}
