using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

/// <summary>
/// Status Notification Response from CSMS to Charging Station
/// </summary>
public class StatusNotificationResponse
{
    /// <summary>
    /// Optional. Custom data for this class
    /// </summary>
    public CustomData CustomData { get; set; }
}