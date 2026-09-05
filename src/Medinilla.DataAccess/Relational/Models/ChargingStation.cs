using Medinilla.DataAccess.Relational.Models.Audit;
using Medinilla.DataAccess.Relational.Models.Authorization;

namespace Medinilla.DataAccess.Relational.Models;

public class ChargingStation
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Guid AuthorizationDetailsId { get; set; }
    
    public bool Booted { get; set; }

    public string ClientIdentifier { get; set; }

    public string Model { get; set; }

    public string Vendor { get; set; }

    public string LatestBootNotificationReason { get; set; }

    public string? Location { get; set; }

    public string? Alias { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public virtual Account Account { get; set; }

    public virtual AuthorizationDetails? AuthorizationDetails { get; set; }

    public virtual ICollection<EvseConnector> EvseConnectors { get; set; } = new List<EvseConnector>();

    public virtual ICollection<TransactionEvent> TransactionEvents { get; set; } = new List<TransactionEvent>();

    public virtual ICollection<TransactionSnapshot> TransactionSnapshots { get; set; } = new List<TransactionSnapshot>();

    public virtual ICollection<Tariff> Tariffs { get; set; } = new List<Tariff>();

    public virtual ICollection<IdToken> IdTokens { get; set; } = new List<IdToken>();

    public virtual ICollection<CommandExecution> CommandExecutions { get; set; } = new List<CommandExecution>();
    
    public virtual ICollection<AuthorizationUser> AuthorizationUsers { get; set; } = new List<AuthorizationUser>();
}
