namespace Medinilla.DataAccess.Relational.Models;

public class EvseConnector
{
    public Guid Id { get; set; }

    public Guid ChargingStationId { get; set; }

    public int EvseId { get; set; }

    public int ConnectorId { get; set; }

    public string ConnectorStatus { get; set; }

    public DateTime ModifiedAt { get; set; }

    public virtual ChargingStation ChargingStation { get; set; }
}
