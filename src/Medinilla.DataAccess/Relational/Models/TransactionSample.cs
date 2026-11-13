using Medinilla.DataAccess.Relational.Enums;

namespace Medinilla.DataAccess.Relational.Models;

public class TransactionSample
{
    public Guid Id { get; set; }

    public required string TransactionId { get; set; }

    public DateTime Timestamp { get; set; }

    public Measurand Measurand { get; set; }

    public MeasurementLocation Location { get; set; }

    public ReadingContext Context { get; set; }

    public MeasurementPhase? Phase { get; set; }

    // this is the final computed value
    public decimal Value { get; set; }

    public Guid TransactionEventId { get; set; }

    public virtual TransactionEvent TransactionEvent { get; set; }

    public Guid ChargingStationId { get; set; }
    
    public virtual ChargingStation ChargingStation { get; set; }
}
