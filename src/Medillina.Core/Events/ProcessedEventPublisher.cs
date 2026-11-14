using System.Diagnostics;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Medinilla.Core.SharedContracts.Events;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.Events;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Events;

/// <summary>
/// Maps successfully processed OCPP calls to their event contracts and publishes the
/// completed <see cref="MedinillaHttpEvent"/> wrapper. Every mapping and transport
/// failure is isolated here so it cannot change the OCPP result or suppress the charger
/// response.
/// </summary>
public sealed class ProcessedEventPublisher(
    IChargerEventPublisher publisher,
    BootupEventMapper bootupMapper,
    StatusNotificationEventMapper statusNotificationMapper,
    TransactionLifecycleEventMapper transactionLifecycleMapper,
    ILogger<ProcessedEventPublisher> logger) : IProcessedEventPublisher
{
    /// <summary>Total time allowed for optional enrichment plus transport publication.</summary>
    private static readonly TimeSpan PublicationTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Optional fields must never dominate the response path. This portion of the total
    /// publication budget is reserved for connector-type/consumption enrichment.
    /// </summary>
    private static readonly TimeSpan OptionalEnrichmentTimeout = TimeSpan.FromSeconds(1);

    public async Task PublishForCallAsync(
        OcppCallRequest call,
        string clientIdentifier,
        RpcResult result,
        DateTime completedAtUtc)
    {
        // A null/error result means the action did not complete successfully.
        if (result.Error is not null || result.Result is null)
        {
            return;
        }

        using var publicationTimeout = new CancellationTokenSource(PublicationTimeout);
        var started = Stopwatch.GetTimestamp();

        try
        {
            switch (call.Action)
            {
                case OcppActionNames.BootNotification:
                    await PublishBootupAsync(
                            call,
                            clientIdentifier,
                            result,
                            completedAtUtc,
                            publicationTimeout.Token,
                            started)
                        .ConfigureAwait(false);
                    break;

                case OcppActionNames.StatusNotification:
                    await PublishStatusNotificationAsync(
                            call,
                            clientIdentifier,
                            completedAtUtc,
                            publicationTimeout.Token,
                            started)
                        .ConfigureAwait(false);
                    break;

                case OcppActionNames.TransactionEvent:
                    await PublishTransactionEventAsync(
                            call,
                            clientIdentifier,
                            result,
                            completedAtUtc,
                            publicationTimeout.Token,
                            started)
                        .ConfigureAwait(false);
                    break;
            }
        }
        catch (OperationCanceledException) when (publicationTimeout.IsCancellationRequested)
        {
            logger.LogWarning(
                "Webhook publication timed out for action {Action} from charger {ChargerId}. Charger processing remains successful.",
                call.Action,
                clientIdentifier);
        }
        catch (Exception ex)
        {
            // Never let event construction or publication turn completed charger
            // processing into an OCPP failure.
            logger.LogError(
                ex,
                "Failed to build webhook event for action {Action} from charger {ChargerId}.",
                call.Action,
                clientIdentifier);
        }
    }

    private async Task PublishBootupAsync(
        OcppCallRequest call,
        string clientIdentifier,
        RpcResult result,
        DateTime completedAtUtc,
        CancellationToken publicationToken,
        long started)
    {
        // BootNotificationAction catches processing failures and returns a rejected
        // registration inside an ordinary OCPP result, so inspect the outcome itself.
        var response = result.Result!.As<BootNotificationResponse>();
        if (response?.Status != RegistrationStatusEnum.Accepted)
        {
            return;
        }

        var request = call.As<BootNotificationRequest>();
        var payload = bootupMapper.Map(clientIdentifier, request);
        await PublishAsync(WebhookEventTypes.ChargerBootup, clientIdentifier, completedAtUtc, payload, publicationToken, started)
            .ConfigureAwait(false);
    }

    private async Task PublishStatusNotificationAsync(
        OcppCallRequest call,
        string clientIdentifier,
        DateTime completedAtUtc,
        CancellationToken publicationToken,
        long started)
    {
        var request = call.As<StatusNotificationRequest>();

        using var enrichmentTimeout = CreateEnrichmentTimeout(publicationToken);
        var payload = await statusNotificationMapper
            .MapAsync(clientIdentifier, request, enrichmentTimeout.Token)
            .ConfigureAwait(false);

        await PublishAsync(WebhookEventTypes.ConnectorStatus, clientIdentifier, completedAtUtc, payload, publicationToken, started)
            .ConfigureAwait(false);
    }

    private async Task PublishTransactionEventAsync(
        OcppCallRequest call,
        string clientIdentifier,
        RpcResult result,
        DateTime completedAtUtc,
        CancellationToken publicationToken,
        long started)
    {
        // Transaction ingestion is idempotent by sequence number. Only publish when this
        // call actually appended a Started/Ended transaction event.
        if (result.ProcessingContext is not TransactionEventProcessingContext { WasNewlyProcessed: true })
        {
            return;
        }

        var request = call.As<TransactionEventRequest>();

        var eventType = request.EventType switch
        {
            TransactionEventEnum.Started => WebhookEventTypes.TransactionStarted,
            TransactionEventEnum.Ended => WebhookEventTypes.TransactionEnded,
            _ => null,
        };

        if (eventType is null)
        {
            return;
        }

        using var enrichmentTimeout = CreateEnrichmentTimeout(publicationToken);
        var payload = await transactionLifecycleMapper
            .MapAsync(clientIdentifier, request, enrichmentTimeout.Token)
            .ConfigureAwait(false);

        await PublishAsync(eventType, clientIdentifier, completedAtUtc, payload, publicationToken, started)
            .ConfigureAwait(false);
    }

    private async Task PublishAsync(
        string eventType,
        string chargerId,
        DateTime completedAtUtc,
        IMessage payload,
        CancellationToken publicationToken,
        long started)
    {
        var httpEvent = new MedinillaHttpEvent
        {
            // Protobuf has no GUID scalar; use the canonical D-format UUID string.
            EventId = Guid.NewGuid().ToString("D"),
            EventType = eventType,
            EventTimestamp = Timestamp.FromDateTime(ToUtc(completedAtUtc)),
            EventData = payload.ToByteString(),
        };

        try
        {
            await publisher
                .PublishAsync(httpEvent, publicationToken)
                .WaitAsync(publicationToken)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Published webhook event {EventType} {EventId} for charger {ChargerId} in {ElapsedMilliseconds}ms.",
                eventType,
                httpEvent.EventId,
                chargerId,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (publicationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Webhook publication timed out for event {EventType} {EventId} for charger {ChargerId}.",
                eventType,
                httpEvent.EventId,
                chargerId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish webhook event {EventType} {EventId} for charger {ChargerId}.",
                eventType,
                httpEvent.EventId,
                chargerId);
        }
    }

    private static CancellationTokenSource CreateEnrichmentTimeout(CancellationToken publicationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(publicationToken);
        timeout.CancelAfter(OptionalEnrichmentTimeout);
        return timeout;
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
