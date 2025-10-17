namespace Medinilla.DataTypes.Contracts.Common;

public static class AuthorizeStatus
{
    /// <summary>
    /// Identifier is allowed for charging.
    /// </summary>
    public const string Accepted = "Accepted";

    /// <summary>
    /// Identifier has been blocked. Not allowed for charging.
    /// </summary>
    public const string Blocked = "Blocked";

    /// <summary>
    /// Identifier is already involved in another transaction and multiple transactions are not allowed.
    /// (Only relevant for the response to a transactionEventRequest(eventType= Started).)
    /// </summary>
    public const string ConcurrentTx = "ConcurrentTx";

    /// <summary>
    /// Identifier has expired. Not allowed for charging.
    /// </summary>
    public const string Expired = "Expired";

    /// <summary>
    /// Identifier is invalid. Not allowed for charging.
    /// </summary>
    public const string Invalid = "Invalid";

    /// <summary>
    /// Identifier is valid, but EV Driver doesn’t have enough credit to start charging. Not allowed for charging.
    /// </summary>
    public const string NoCredit = "NoCredit";

    /// <summary>
    /// Identifier is valid, but not allowed to charge at this type of EVSE.
    /// </summary>
    public const string NotAllowedTypeEVSE = "NotAllowedTypeEVSE";

    /// <summary>
    /// Identifier is valid, but not allowed to charge at this location.
    /// </summary>
    public const string NotAtThisLocation = "NotAtThisLocation";

    /// <summary>
    /// Identifier is valid, but not allowed to charge at this location at this time.
    /// </summary>
    public const string NotAtThisTime = "NotAtThisTime";

    /// <summary>
    /// Identifier is unknown. Not allowed for charging.
    /// </summary>
    public const string Unknown = "Unknown";
}
