using Medinilla.DataAccess.Relational.Enums;
using Medinilla.DataAccess.Relational.Models.Authorization;

namespace Medinilla.DataAccess.Relational.Models;

public class TransactionEvent
{
    public Guid Id { get; set; }

    public Guid ChargingStationId { get; set; }

    public required string TransactionId { get; set; }

    public TransactionEventType EventType { get; set; }

    public DateTime Timestamp { get; set; }

    public TransactionTriggerReason TriggerReason { get; set; }

    public int SeqNo { get; set; }

    public bool? Offline { get; set; }

    public int? NumberOfPhasesUsed { get; set; }

    public int? CableMaxCurrent { get; set; }

    public int? ReservationId { get; set; }

    public ChargingState? ChargingState { get; set; }

    public int? TimeSpentCharging { get; set; }

    public TransactionStoppedReason? StoppedReason { get; set; }

    public int? RemoteStartId { get; set; }

    public int? EvseId { get; set; }

    public int? ConnectorId { get; set; }

    public Guid? IdTokenId { get; set; }

    public virtual IdToken? IdToken { get; set; }

    public virtual required ChargingStation ChargingStation { get; set; }
}
