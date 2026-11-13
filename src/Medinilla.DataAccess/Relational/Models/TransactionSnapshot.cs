namespace Medinilla.DataAccess.Relational.Models;

public class TransactionSnapshot
{
    public Guid Id { get; set; }

    public Guid ChargingStationId { get; set; }

    // The transaction ID supplied by the charger, not a TransactionEvent row ID.
    public required string TransactionId { get; set; }

    // Energy consumed in Wh; null when it cannot yet be calculated.
    public decimal? Consumption { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public virtual ChargingStation ChargingStation { get; set; } = null!;
}
