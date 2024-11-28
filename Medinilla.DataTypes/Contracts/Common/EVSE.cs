namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Electric Vehicle Supply Equipment
/// </summary>
public class EVSE
{
    /// <summary>
    /// EVSE Identifier. This contains a number (> 0) designating an EVSE of the Charging Station
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// An id to designate a specific connector (on an EVSE) by connector index number
    /// </summary>
    public int? ConnectorId { get; set; }
}