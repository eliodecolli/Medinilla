namespace Medinilla.DataAccess.Relational.Models.ChargerConfig;

public class ChargerComponent
{
    public long Id { get; set; }

    public Guid ChargingStationId { get; set; }

    public Guid? EvseConnectorId { get; set; }

    public required string ClientIdentifier { get; set; }

    public required string ComponentName { get; set; }

    public string? ComponentInstance { get; set; }

    public virtual EvseConnector? Connector { get; set; }
    
    public virtual required ChargingStation ChargingStation { get; set; }

    public virtual ICollection<ComponentVariable> ComponentVariables { get; set; }
}