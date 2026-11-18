using System.Diagnostics;
using System.Diagnostics.Metrics;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Medinilla.Core.SharedContracts.Events;
using Medinilla.Infrastructure.Events;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Events;

/// <summary>
/// Builds the shared <see cref="MedinillaHttpEvent"/> wrapper and performs a bounded
/// publication attempt. Mapping, serialization, cancellation, and transport failures
/// are isolated from the application operation that emitted the event.
/// </summary>
public sealed class ApplicationEventEmitter(
    IChargerEventPublisher publisher,
    ILogger<ApplicationEventEmitter> logger) : IApplicationEventEmitter
{
    private static readonly TimeSpan PublicationTimeout = TimeSpan.FromSeconds(5);

    private static readonly Meter Meter = new("Medinilla.Core.Events");
    private static readonly Counter<long> PublishedCounter =
        Meter.CreateCounter<long>("medinilla.webhook.events.published");
    private static readonly Counter<long> FailedCounter =
        Meter.CreateCounter<long>("medinilla.webhook.events.failed");
    private static readonly Histogram<double> PublicationDuration =
        Meter.CreateHistogram<double>("medinilla.webhook.events.duration", unit: "ms");

    public async Task TryEmitAsync<TPayload>(
        string eventType,
        DateTimeOffset completedAtUtc,
        Func<TPayload> buildPayload,
        CancellationToken cancellationToken = default)
        where TPayload : IMessage<TPayload>
    {
        // One stable ID for this logical event/publication attempt.
        var eventId = Guid.NewGuid().ToString("D");
        var started = Stopwatch.GetTimestamp();

        MedinillaHttpEvent httpEvent;
        try
        {
            var payload = buildPayload();
            if (payload is null)
            {
                throw new InvalidOperationException("Event payload factory returned null.");
            }

            httpEvent = new MedinillaHttpEvent
            {
                EventId = eventId,
                EventType = eventType,
                EventTimestamp = Timestamp.FromDateTime(completedAtUtc.UtcDateTime),
                EventData = payload.ToByteString(),
            };
        }
        catch (Exception ex)
        {
            FailedCounter.Add(1, new KeyValuePair<string, object?>("event_type", eventType));
            logger.LogError(
                ex,
                "Failed to build webhook event {EventType} {EventId}.",
                eventType,
                eventId);
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PublicationTimeout);

        try
        {
            await publisher
                .PublishAsync(httpEvent, timeout.Token)
                .WaitAsync(timeout.Token)
                .ConfigureAwait(false);

            var elapsed = Stopwatch.GetElapsedTime(started);
            PublishedCounter.Add(1, new KeyValuePair<string, object?>("event_type", eventType));
            PublicationDuration.Record(
                elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("event_type", eventType));

            logger.LogInformation(
                "Published webhook event {EventType} {EventId} in {ElapsedMilliseconds}ms.",
                eventType,
                eventId,
                elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            FailedCounter.Add(1, new KeyValuePair<string, object?>("event_type", eventType));
            logger.LogWarning(
                "Webhook publication timed out for event {EventType} {EventId}.",
                eventType,
                eventId);
        }
        catch (Exception ex)
        {
            FailedCounter.Add(1, new KeyValuePair<string, object?>("event_type", eventType));
            logger.LogError(
                ex,
                "Failed to publish webhook event {EventType} {EventId}.",
                eventType,
                eventId);
        }
    }
}
