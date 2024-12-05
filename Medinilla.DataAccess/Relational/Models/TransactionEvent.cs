using Medinilla.DataAccess.Relational.Models.Authorization;

namespace Medinilla.DataAccess.Relational.Models;

public class TransactionEvent
{
    public Guid Id { get; set; }

    public Guid ChargingStationId { get; set; }

    public Guid? IdTokenId { get; set; }

    public string TransactionId { get; set; }

    public int SeqNo { get; set; }

    public int? EVSEId { get; set; }

    public DateTime Timestamp { get; set; }

    public bool? Offline { get; set; }

    public decimal MeteredValue { get; set; }

    public string UnitName { get; set; }

    public string TriggerReason { get; set; }

    public string EventType { get; set; }

    public virtual ChargingStation ChargingStation { get; set; }

    public virtual IdToken? IdToken { get; set; }
}
