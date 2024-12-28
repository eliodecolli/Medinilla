using Medinilla.DataAccess.Relational.Models.Authorization;

namespace Medinilla.DataAccess.Relational.Models;

public class TransactionSnapshot
{
    public Guid Id { get; set; }

    public Guid ChargingStationId { get; set; }

    public Guid? IdTokenId { get; set; }

    public string TransactionId { get; set; }

    public string StartReason { get; set; }

    public string EndReason { get; set; }

    public decimal TotalMeteredValue { get; set; }

    public string Unit { get; set; }

    public decimal TotalCost { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime EndedAt { get; set; }

    public string TokenId { get; set; }

    public Guid? EvseConnectorId { get; set; }

    public virtual ChargingStation ChargingStation { get; set; }

    public virtual EvseConnector? EvseConnector { get; set; }

    public virtual IdToken? IdToken { get; set; }
}
