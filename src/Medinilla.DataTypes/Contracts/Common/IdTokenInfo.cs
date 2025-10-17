namespace Medinilla.DataTypes.Contracts.Common;

public sealed class IdTokenInfo
{
    /// <summary>
    /// Current status of the ID Token.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Date and Time after which the token must be considered invalid.
    /// </summary>
    public DateTime? CacheExpiryDateTime { get; set; }

    /// <summary>
    /// Between [-9; 9]. The chargingPriority in TransactionEventResponse overrules this one.
    /// </summary>
    public int ChargingPriority { get; set; }

    /// <summary>
    /// Preferred user interface language of identifier user.Contains a language code as defined in RFC5646.
    /// </summary>
    public string? Language1 { get; set; }

    /// <summary>
    /// Only used when the IdToken is only valid for one or more specific EVSEs, not for the entire Charging Station.
    /// </summary>
    public int[]? EvseId { get; set; }

    /// <summary>
    /// Second preferred user interface language of identifier user.Don’t use when language1 is omitted, has to be different from language1.
    /// Contains a language code as defined in RFC5646.
    /// </summary>
    public string? Language2 { get; set; }

    /// <summary>
    ///  This contains the group identifier.
    /// </summary>
    public IdToken? GroupIdToken { get; set; }

    public MessageContent? PersonalMessage { get; set; }
}
