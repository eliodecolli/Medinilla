namespace Medinilla.DataAccess.Relational.Models;

public class Tariff
{
    public Guid Id { get; set; }

    public Guid ChargingStationId { get; set; }

    public string UnitName { get; set; }

    public decimal UnitPrice { get; set; }

    public virtual ChargingStation ChargingStation { get; set; }
}
