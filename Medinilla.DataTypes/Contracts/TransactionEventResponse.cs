using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

/// <summary>
/// Response to a TransactionEvent request
/// </summary>
public class TransactionEventResponse
{
    /// <summary>
    /// When eventType of TransactionEventRequest is Updated, then this value contains the running cost.
    /// Total cost of the transaction including taxes. In the currency configured with Configuration Variable: Currency. 
    /// When omitted, the transaction was NOT free. To indicate a free transaction, the CSMS SHALL send 0.00
    /// SHALL only be sent when charging has ended.
    /// </summary>
    public decimal? TotalCost { get; set; }

    /// <summary>
    /// Priority of charging from a business point of view. Default priority is 0, The range is from -9 to 9. 
    /// Higher values indicate a higher priority.
    /// The chargingPriority in TransactionEventResponse overrules the one in IdTokenInfo
    /// </summary>
    public int? ChargingPriority { get; set; }

    /// <summary>
    /// Information about the authorization status, expiry and group id token
    /// </summary>
    public IdTokenInfo? IdTokenInfo { get; set; }

    /// <summary>
    /// Contains the message to be displayed on the Charging Station
    /// </summary>
    public MessageContent? UpdatedPersonalMessage { get; set; }
}