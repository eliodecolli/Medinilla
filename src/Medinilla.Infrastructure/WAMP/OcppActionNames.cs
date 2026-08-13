namespace Medinilla.Infrastructure.WAMP;

public static class OcppActionNames
{
    public const string Authorize = "Authorize";
    
    public const string BootNotification = "BootNotification";
    
    public const string Heartbeat = "Heartbeat";

    public const string SecurityEventNotification = "SecurityEventNotification";

    public const string StatusNotification = "StatusNotification";

    public const string TransactionEvent = "TransactionEvent";

    public const string SetVariables = "SetVariables";

    public const string GetVariables = "GetVariables";

    public const string GetBaseReport = "GetBaseReport";

    public const string Reset = "Reset";

    public const string NotifyReport = "NotifyReport";
}
