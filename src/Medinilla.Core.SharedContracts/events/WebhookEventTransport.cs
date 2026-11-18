namespace Medinilla.Core.SharedContracts.Events;

/// <summary>
/// Transport-level defaults shared by the Core publisher and the webhook delivery worker.
/// Deployments may override the stream name and payload field through configuration.
/// </summary>
public static class WebhookEventTransport
{
    public const string EventsStream = "medinilla:{webhooks}:events";
    public const string PayloadField = "payload";
}
