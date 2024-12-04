using Medinilla.DataAccess.Relational.Models.Authorization;

namespace Medinilla.Core.Logic.Authorization;

public class AuthorizationContext
{
    public int? EvseId { get; set; }

    public string? LocationName { get; set; }

    public decimal? UserActiveCredit { get; set; }

    public AuthorizationDetails AuthorizationDetails { get; set; }

    public bool SkipIfNullToken { get; set; }
}
