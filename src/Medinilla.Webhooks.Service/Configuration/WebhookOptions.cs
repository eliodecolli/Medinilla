using Medinilla.Core.SharedContracts.Events;

namespace Medinilla.Webhooks.Service.Configuration;

public sealed class WebhookOptions
{
    public const string SectionName = "Webhooks";

    public string RedisUri { get; set; } = "localhost:6379";

    public string EventsStream { get; set; } = WebhookEventTransport.EventsStream;

    public string PayloadField { get; set; } = WebhookEventTransport.PayloadField;

    public string ConsumerGroup { get; set; } = "medinilla.webhooks";

    /// <summary>
    /// Optional stable consumer name. When omitted, a unique name is generated for this
    /// process so replicas do not fight over the same group member.
    /// </summary>
    public string? ConsumerName { get; set; }

    public int ReadBatchSize { get; set; } = 10;

    public int IdleReadDelayMilliseconds { get; set; } = 500;

    public int ErrorBackoffSeconds { get; set; } = 5;

    public int MaxConcurrency { get; set; } = 8;

    public int HttpTimeoutSeconds { get; set; } = 10;

    public List<WebhookSubscriptionOptions> Subscriptions { get; set; } = [];
}

public sealed class WebhookSubscriptionOptions
{
    public string Id { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Reference to a secret resolved through <c>Webhooks:Secrets:&lt;ref&gt;</c> or the
    /// <c>MEDINILLA_WEBHOOK_SECRET_&lt;REF&gt;</c> environment variable. The secret value
    /// itself is never stored in a delivery record.
    /// </summary>
    public string? SigningSecretRef { get; set; }

    /// <summary>
    /// Exact <c>EventType</c> values this subscription accepts. Omit or leave empty to
    /// receive all supported event types.
    /// </summary>
    public List<string>? EventTypes { get; set; }
}
