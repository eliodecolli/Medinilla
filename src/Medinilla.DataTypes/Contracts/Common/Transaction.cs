namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Class containing transaction-specific information
/// </summary>
public class Transaction
{
    /// <summary>
    /// This contains the Id of the transaction
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Current charging state, required when state has changed
    /// </summary>
    public ChargingStateEnum? ChargingState { get; set; }

    /// <summary>
    /// Contains the total time that energy flowed from EVSE to EV during the transaction (in seconds).
    /// Note that timeSpentCharging is smaller or equal to the duration of the transaction
    /// </summary>
    public int? TimeSpentCharging { get; set; }

    /// <summary>
    /// This contains the reason why the transaction was stopped
    /// </summary>
    public ReasonEnum? StoppedReason { get; set; }

    /// <summary>
    /// The ID given to remote start request. This enables to CSMS to match 
    /// the started transaction to the given start request
    /// </summary>
    public int? RemoteStartId { get; set; }
}