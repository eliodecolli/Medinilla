using System.Globalization;
using System.Net.Http.Headers;
using Medinilla.Core.SharedContracts.Events;
using Medinilla.Webhooks.Service.Configuration;
using Microsoft.Extensions.Logging;

namespace Medinilla.Webhooks.Service.Delivery;

public readonly record struct WebhookDispatchResult(int Matched, int Succeeded, int Failed)
{
    public bool HasFailures => Failed > 0;
}

/// <summary>
/// Fans a single processed event out to the subscriptions that match its EventType.
/// The exact serialized <see cref="MedinillaHttpEvent"/> bytes read from Redis are used
/// as the HTTP body; the event-specific payload is never decoded or reshaped here.
/// </summary>
public sealed class WebhookDispatcher(
    IHttpClientFactory httpClientFactory,
    WebhookSubscriptionRegistry subscriptions,
    WebhookOptions options,
    TimeProvider timeProvider,
    ILogger<WebhookDispatcher> logger)
{
    public const string HttpClientName = "medinilla-webhooks";

    public async Task<WebhookDispatchResult> DispatchAsync(
        MedinillaHttpEvent httpEvent,
        byte[] body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpEvent);
        ArgumentNullException.ThrowIfNull(body);

        var targets = subscriptions.Match(httpEvent.EventType);
        if (targets.Count == 0)
        {
            logger.LogDebug(
                "No webhook subscriptions matched event {EventType} {EventId}.",
                httpEvent.EventType,
                httpEvent.EventId);
            return new WebhookDispatchResult(0, 0, 0);
        }

        var succeeded = 0;
        var failed = 0;
        var maxConcurrency = Math.Max(1, options.MaxConcurrency);

        await Parallel.ForEachAsync(
            targets,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxConcurrency,
                CancellationToken = cancellationToken,
            },
            async (subscription, token) =>
            {
                var delivered = await TryDeliverAsync(httpEvent, body, subscription, token)
                    .ConfigureAwait(false);

                if (delivered)
                {
                    Interlocked.Increment(ref succeeded);
                }
                else
                {
                    Interlocked.Increment(ref failed);
                }
            }).ConfigureAwait(false);

        return new WebhookDispatchResult(targets.Count, succeeded, failed);
    }

    private async Task<bool> TryDeliverAsync(
        MedinillaHttpEvent httpEvent,
        byte[] body,
        WebhookSubscription subscription,
        CancellationToken cancellationToken)
    {
        var timestamp = timeProvider
            .GetUtcNow()
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
        {
            Content = new ByteArrayContent(body),
        };

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/protobuf");
        request.Headers.Add(WebhookHeaders.EventId, httpEvent.EventId);
        request.Headers.Add(WebhookHeaders.EventType, httpEvent.EventType);
        request.Headers.Add(WebhookHeaders.Timestamp, timestamp);
        request.Headers.Add(
            WebhookHeaders.Signature,
            WebhookSigner.ComputeSignature(subscription.SigningSecret, timestamp, body));

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Delivered webhook event {EventType} {EventId} to subscription {SubscriptionId} (attempt 1, status {StatusCode}).",
                    httpEvent.EventType,
                    httpEvent.EventId,
                    subscription.Id,
                    (int)response.StatusCode);
                return true;
            }

            logger.LogWarning(
                "Webhook delivery failed for event {EventType} {EventId} to subscription {SubscriptionId} (attempt 1, status {StatusCode}).",
                httpEvent.EventType,
                httpEvent.EventId,
                subscription.Id,
                (int)response.StatusCode);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown must leave the input entry unacknowledged.
            throw;
        }
        catch (Exception ex)
        {
            // A transport error or timeout is a failed attempt, not a reason to stop
            // delivering the same event to other subscriptions.
            logger.LogWarning(
                ex,
                "Webhook delivery failed for event {EventType} {EventId} to subscription {SubscriptionId} (attempt 1).",
                httpEvent.EventType,
                httpEvent.EventId,
                subscription.Id);
            return false;
        }
    }
}
