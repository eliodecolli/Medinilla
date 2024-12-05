namespace Medinilla.DataAccess.Relational.Models.Authorization;

public class IdToken
{
    public Guid Id { get; set; }

    public Guid ChargingStationId { get; set; }

    public Guid AuthorizationUserId { get; set; }

    public string Token { get; set; }

    public string IdType { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public bool Blocked { get; set; }

    public bool IsUnderTx { get; set; }

    public virtual ChargingStation ChargingStation { get; set; }

    public virtual AuthorizationUser User { get; set; }

    public virtual ICollection<TransactionSnapshot> TransactionSnapshots { get; set; }

    public virtual ICollection<TransactionEvent> TransactionEvents { get; set; }
}
