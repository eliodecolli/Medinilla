using Google.Protobuf;
using Medinilla.Core.SharedContracts.Events;
using Medinilla.Webhooks.Service.Configuration;
using Medinilla.Webhooks.Service.Delivery;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Medinilla.Webhooks.Service;

/// <summary>
/// Reads accepted processed events from the Redis events consumer group and performs
/// the initial HTTP fan-out. Successful deliveries (and events with no matching
/// subscriptions) are acknowledged.
///
/// Durable retry scheduling, pending-entry recovery, and dead-lettering are
/// intentionally not implemented in this phase. A failed delivery is therefore left
/// pending in the consumer group rather than acknowledged and silently lost.
/// </summary>
public sealed class WebhookDeliveryWorker(
    IConnectionMultiplexer redis,
    WebhookOptions options,
    WebhookDispatcher dispatcher,
    ILogger<WebhookDeliveryWorker> logger) : BackgroundService
{
    private readonly IDatabase _database = redis.GetDatabase();
    private readonly int _batchSize = Math.Max(1, options.ReadBatchSize);
    private readonly int _idleReadDelayMilliseconds = Math.Max(1, options.IdleReadDelayMilliseconds);
    private readonly int _errorBackoffMilliseconds = Math.Max(1, options.ErrorBackoffSeconds * 1000);
    private readonly string _consumerName = string.IsNullOrWhiteSpace(options.ConsumerName)
        ? $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.NewGuid():N}"
        : options.ConsumerName;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConsumerGroupAsync(stoppingToken).ConfigureAwait(false);

        logger.LogInformation(
            "Webhook delivery worker started. Stream={Stream} Group={ConsumerGroup} Consumer={Consumer}.",
            options.EventsStream,
            options.ConsumerGroup,
            _consumerName);

        while (!stoppingToken.IsCancellationRequested)
        {
            StreamEntry[] entries;

            try
            {
                entries = await _database.StreamReadGroupAsync(
                    options.EventsStream,
                    options.ConsumerGroup,
                    _consumerName,
                    ">",
                    count: _batchSize).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Redis unavailability must not produce a hot retry loop.
                logger.LogError(ex, "Could not read webhook events from Redis. Backing off.");
                await DelayAsync(_errorBackoffMilliseconds, stoppingToken).ConfigureAwait(false);
                continue;
            }

            if (entries.Length == 0)
            {
                await DelayAsync(_idleReadDelayMilliseconds, stoppingToken).ConfigureAwait(false);
                continue;
            }

            foreach (var entry in entries)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await HandleEntryAsync(entry, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Leave the entry pending for recovery.
                    break;
                }
                catch (Exception ex)
                {
                    // Leave the entry pending; the durable retry/recovery phase will
                    // decide how to replay it.
                    logger.LogError(
                        ex,
                        "Unexpected failure while processing webhook stream entry {EntryId}. Leaving it pending.",
                        entry.Id);
                }
            }
        }

        logger.LogInformation("Webhook delivery worker stopped.");
    }

    private async Task HandleEntryAsync(StreamEntry entry, CancellationToken cancellationToken)
    {
        var payload = entry[options.PayloadField];

        if (payload.IsNull)
        {
            logger.LogError(
                "Webhook stream entry {EntryId} has no '{PayloadField}' field. Leaving it pending.",
                entry.Id,
                options.PayloadField);
            return;
        }

        var body = (byte[])payload!;
        MedinillaHttpEvent httpEvent;

        try
        {
            httpEvent = MedinillaHttpEvent.Parser.ParseFrom(body);
        }
        catch (InvalidProtocolBufferException ex)
        {
            logger.LogError(
                ex,
                "Webhook stream entry {EntryId} is not a valid MedinillaHttpEvent. Leaving it pending.",
                entry.Id);
            return;
        }

        if (string.IsNullOrWhiteSpace(httpEvent.EventId) ||
            string.IsNullOrWhiteSpace(httpEvent.EventType))
        {
            logger.LogError(
                "Webhook stream entry {EntryId} is missing EventId or EventType. Leaving it pending.",
                entry.Id);
            return;
        }

        var result = await dispatcher
            .DispatchAsync(httpEvent, body, cancellationToken)
            .ConfigureAwait(false);

        if (result.HasFailures)
        {
            // Deliberately do not acknowledge: acknowledging here without the agreed
            // durable handoff would lose the failed deliveries permanently.
            logger.LogError(
                "Webhook event {EventType} {EventId} had {Failed} failed delivery target(s) out of {Matched}. " +
                "Stream entry {EntryId} remains pending because durable retry is not implemented yet.",
                httpEvent.EventType,
                httpEvent.EventId,
                result.Failed,
                result.Matched,
                entry.Id);
            return;
        }

        var acknowledged = await _database
            .StreamAcknowledgeAsync(options.EventsStream, options.ConsumerGroup, entry.Id)
            .ConfigureAwait(false);

        if (acknowledged == 0)
        {
            logger.LogWarning(
                "Webhook event {EventId} stream entry {EntryId} was not acknowledged (already claimed or trimmed?).",
                httpEvent.EventId,
                entry.Id);
        }
        else
        {
            logger.LogDebug(
                "Acknowledged webhook event {EventType} {EventId} stream entry {EntryId}.",
                httpEvent.EventType,
                httpEvent.EventId,
                entry.Id);
        }
    }

    private async Task EnsureConsumerGroupAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Start at the beginning so events already in the stream are not skipped
                // if the worker starts after Core has published.
                await _database.StreamCreateConsumerGroupAsync(
                    options.EventsStream,
                    options.ConsumerGroup,
                    "0-0",
                    true).ConfigureAwait(false);
                return;
            }
            catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP", StringComparison.Ordinal))
            {
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Could not create/ensure webhook consumer group {ConsumerGroup} on {Stream}. Backing off.",
                    options.ConsumerGroup,
                    options.EventsStream);
                await DelayAsync(_errorBackoffMilliseconds, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(milliseconds, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }
}
