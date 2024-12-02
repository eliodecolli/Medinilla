namespace Medinilla.DataAccess.Relational.Models.Authorization;

public class AuthorizationUser
{
    public Guid Id { get; set; }

    public Guid ChargingStationId { get; set; }

    public string DisplayName { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<IdToken> Tokens { get; set; }
}
