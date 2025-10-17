namespace Medinilla.DataAccess.Relational.Models;

public class Account
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public virtual ICollection<ChargingStation> ChargingStations { get; set; }
}
