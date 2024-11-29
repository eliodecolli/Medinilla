using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

/// <summary>
/// Status Notification Request from Charging Station to CSMS
/// </summary>
public class StatusNotificationRequest
{

    /// <summary>
    /// The time for which the status is reported. If absent time of receipt of the message will be assumed.
    /// </summary>
    public DateTime? Timestamp { get; set; }

    /// <summary>
    /// The current status of the Connector.
    /// </summary>
    public ConnectorStatusEnum ConnectorStatus { get; set; }

    /// <summary>
    /// The id of the EVSE to which the connector belongs for which the status is reported.
    /// </summary>
    public int EvseId { get; set; }

    /// <summary>
    /// The id of the connector within the EVSE for which the status is reported.
    /// </summary>
    public int? ConnectorId { get; set; }
}