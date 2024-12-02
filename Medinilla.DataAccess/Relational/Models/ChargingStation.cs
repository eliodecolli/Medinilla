using Medinilla.DataAccess.Relational.Models.Authorization;

namespace Medinilla.DataAccess.Relational.Models;

public class ChargingStation
{
    public Guid Id { get; set; }

    public Guid AuthDetailsId { get; set; }

    public string ClientIdentifier { get; set; }

    public string Model {  get; set; }

    public string Vendor { get; set; }

    public string LatestBootNotificationReason {  get; set; }

    public DateTime CreatedAt {  get; set; }

    public DateTime? ModifiedAt { get; set; }

    public virtual AuthorizationDetails AuthorizationDetails { get; set; }

    public virtual ICollection<EvseConnector> EvseConnectors { get; set; }

    public virtual ICollection<TransactionEvent> TransactionEvents { get; set; }

    public virtual ICollection<TransactionSnapshot> TransactionSnapshots { get; set; }

    public virtual ICollection<Tariff> Tariffs { get; set; }

    public virtual ICollection<IdToken> IdTokens { get; set; }
}
