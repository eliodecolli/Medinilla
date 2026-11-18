using Medinilla.Webhooks.Service.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Medinilla.Webhooks.Service.Delivery;

public sealed class WebhookSecretResolver(IConfiguration configuration)
{
    public string Resolve(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new InvalidOperationException("Webhook subscription signing secret reference is required.");
        }

        var configured = configuration[$"{WebhookOptions.SectionName}:Secrets:{reference}"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var environmentName = "MEDINILLA_WEBHOOK_SECRET_" + Normalize(reference);
        var fromEnvironment = Environment.GetEnvironmentVariable(environmentName);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        throw new InvalidOperationException(
            $"Webhook signing secret '{reference}' was not found. Configure " +
            $"'{WebhookOptions.SectionName}:Secrets:{reference}' or environment variable '{environmentName}'.");
    }

    private static string Normalize(string reference)
    {
        var buffer = new char[reference.Length];
        for (var i = 0; i < reference.Length; i++)
        {
            var value = reference[i];
            buffer[i] = char.IsLetterOrDigit(value) ? char.ToUpperInvariant(value) : '_';
        }

        return new string(buffer);
    }
}

public sealed class WebhookSubscription
{
    public WebhookSubscription(
        string id,
        Uri url,
        string signingSecret,
        IReadOnlySet<string>? eventTypes)
    {
        Id = id;
        Url = url;
        SigningSecret = signingSecret;
        EventTypes = eventTypes;
    }

    public string Id { get; }

    public Uri Url { get; }

    public string SigningSecret { get; }

    public IReadOnlySet<string>? EventTypes { get; }

    public bool Accepts(string eventType) =>
        EventTypes is null || EventTypes.Contains(eventType);
}

/// <summary>
/// Immutable subscription snapshot loaded from configuration at startup. Matching uses
/// only the wrapper envelope, not the event-specific payload.
/// </summary>
public sealed class WebhookSubscriptionRegistry
{
    private readonly IReadOnlyList<WebhookSubscription> _subscriptions;

    public WebhookSubscriptionRegistry(
        WebhookOptions options,
        WebhookSecretResolver secretResolver,
        ILogger<WebhookSubscriptionRegistry> logger)
    {
        var subscriptions = new List<WebhookSubscription>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var configured in options.Subscriptions)
        {
            if (!configured.Enabled)
            {
                logger.LogInformation("Webhook subscription {SubscriptionId} is disabled.", configured.Id);
                continue;
            }

            if (string.IsNullOrWhiteSpace(configured.Id))
            {
                throw new InvalidOperationException("Every enabled webhook subscription requires a stable ID.");
            }

            if (!ids.Add(configured.Id))
            {
                throw new InvalidOperationException(
                    $"Duplicate webhook subscription ID '{configured.Id}'.");
            }

            if (!Uri.TryCreate(configured.Url, UriKind.Absolute, out var url) ||
                (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"Webhook subscription '{configured.Id}' has an invalid absolute HTTP(S) URL.");
            }

            var eventTypes = configured.EventTypes is { Count: > 0 }
                ? new HashSet<string>(
                    configured.EventTypes
                        .Where(type => !string.IsNullOrWhiteSpace(type))
                        .Select(type => type.Trim()),
                    StringComparer.Ordinal)
                : null;

            subscriptions.Add(new WebhookSubscription(
                configured.Id,
                url,
                secretResolver.Resolve(configured.SigningSecretRef ?? string.Empty),
                eventTypes));
        }

        _subscriptions = subscriptions;

        if (subscriptions.Count == 0)
        {
            logger.LogWarning(
                "No enabled webhook subscriptions are configured. Events will be acknowledged without delivery.");
        }
        else
        {
            logger.LogInformation(
                "Loaded {SubscriptionCount} enabled webhook subscription(s).",
                subscriptions.Count);
        }
    }

    public IReadOnlyList<WebhookSubscription> Match(string eventType) =>
        _subscriptions.Where(subscription => subscription.Accepts(eventType)).ToArray();
}
