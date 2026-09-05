using Medinilla.DataAccess.Relational.Models.Authorization;

namespace Medinilla.Core.Logic.Authorization;

public class AuthorizationContext
{
    public int? EvseId { get; set; }

    public string? LocationName { get; set; }

    public decimal? UserActiveCredit { get; set; }

    public IdToken? IdToken { get; set; }

    public AuthorizationDetails AuthorizationDetails { get; set; }

    /// <summary>
    /// Configures how the auth pipeline should treat a null <see cref="IdToken"/>.
    /// </summary>
    public bool SkipIfNullToken { get; set; }
}