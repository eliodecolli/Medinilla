using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

/// <summary>
/// Main Transaction Event Request message for OCPP transactions
/// </summary>
public class TransactionEventRequest
{
    /// <summary>
    /// Type of this event. The first TransactionEvent of a transaction SHALL contain: "Started"
    /// The last TransactionEvent of a transaction SHALL contain: "Ended". All others SHALL contain: "Updated".
    /// </summary>
    public TransactionEventEnum EventType { get; set; }

    /// <summary>
    /// The date and time at which this transaction event occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Reason the Charging Station sends this message to the CSMS
    /// </summary>
    public TriggerReasonEnum TriggerReason { get; set; }

    /// <summary>
    /// Incremental sequence number, helps with determining if all messages of a transaction have been received
    /// </summary>
    public int SeqNo { get; set; }

    /// <summary>
    /// Indication that this transaction event happened when the Charging Station was offline. 
    /// Default = false, meaning: the event occurred when the Charging Station was online
    /// </summary>
    public bool? Offline { get; set; }

    /// <summary>
    /// If the Charging Station is able to report the number of phases used, then it SHALL provide it. 
    /// When omitted the CSMS may be able to determine the number of phases used via device management
    /// </summary>
    public int? NumberOfPhasesUsed { get; set; }

    /// <summary>
    /// The maximum current of the connected cable in Ampere (A)
    /// </summary>
    public int? CableMaxCurrent { get; set; }

    /// <summary>
    /// This contains the Id of the reservation that terminates as a result of this transaction
    /// </summary>
    public int? ReservationId { get; set; }

    /// <summary>
    /// Contains transaction-specific information
    /// </summary>
    public Transaction TransactionInfo { get; set; }

    /// <summary>
    /// EVSE details where the transaction occurs
    /// </summary>
    public EVSE? Evse { get; set; }

    /// <summary>
    /// Token used to start this transaction
    /// </summary>
    public IdToken? IdToken { get; set; }

    /// <summary>
    /// List of meter values with timestamps
    /// </summary>
    public List<MeterValue>? MeterValue { get; set; }
}