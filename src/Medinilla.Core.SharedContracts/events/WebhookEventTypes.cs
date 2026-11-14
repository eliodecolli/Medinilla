namespace Medinilla.Core.SharedContracts.Events;

/// <summary>
/// Stable discriminators carried in <see cref="MedinillaHttpEvent.EventType"/>.
/// Consumers use these values to choose the parser for <see cref="MedinillaHttpEvent.EventData"/>.
/// </summary>
public static class WebhookEventTypes
{
    /// <summary>Payload: <see cref="BootupEvent"/>.</summary>
    public const string ChargerBootup = "charger.bootup";

    /// <summary>Payload: <see cref="StatusNotificationEvent"/>.</summary>
    public const string ConnectorStatus = "connector.status";

    /// <summary>Payload: <see cref="TransactionLifecycleEvent"/>.</summary>
    public const string TransactionStarted = "transaction.started";

    /// <summary>Payload: <see cref="TransactionLifecycleEvent"/>.</summary>
    public const string TransactionEnded = "transaction.ended";
}
