using Google.Protobuf.WellKnownTypes;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.SharedContracts.Events;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.ChargerConfig;
using Medinilla.DataTypes.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Events;

/// <summary>
/// Builds <see cref="BootupEvent"/> from a successfully processed, accepted BootNotification.
/// </summary>
public sealed class BootupEventMapper
{
    public BootupEvent Map(string chargerId, BootNotificationRequest request)
    {
        var @event = new BootupEvent
        {
            ChargerId = chargerId,
            BootReason = request.Reason.ToString(),
            VendorName = request.ChargingStation.VendorName ?? string.Empty,
            Model = request.ChargingStation.Model ?? string.Empty,
        };

        // Preserve absence for optional fields instead of fabricating an empty value.
        if (!string.IsNullOrWhiteSpace(request.ChargingStation.SerialNumber))
        {
            @event.SerialNumber = request.ChargingStation.SerialNumber;
        }

        if (!string.IsNullOrWhiteSpace(request.ChargingStation.FirmwareVersion))
        {
            @event.FirmwareVersion = request.ChargingStation.FirmwareVersion;
        }

        return @event;
    }
}

/// <summary>
/// Builds <see cref="StatusNotificationEvent"/> after StatusNotification state changes
/// have been committed. Connector type is optional enrichment from known charger
/// metadata and never blocks publication.
/// </summary>
public sealed class StatusNotificationEventMapper(
    MedinillaOcppDbContext context,
    ILogger<StatusNotificationEventMapper> logger)
{
    public async Task<StatusNotificationEvent> MapAsync(
        string chargerId,
        StatusNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var connectorId = request.ConnectorId ?? 1;
        var timestamp = ToUtc(request.Timestamp ?? DateTime.UtcNow);

        var @event = new StatusNotificationEvent
        {
            ChargerId = chargerId,
            EvseId = request.EvseId,
            ConnectorId = connectorId,
            ConnectorStatus = request.ConnectorStatus.ToString(),
            StatusTimestamp = Timestamp.FromDateTime(timestamp),
        };

        var connectorType = await TryGetConnectorTypeAsync(
                chargerId,
                request.EvseId,
                connectorId,
                cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(connectorType))
        {
            @event.ConnectorType = connectorType;
        }

        return @event;
    }

    private async Task<string?> TryGetConnectorTypeAsync(
        string chargerId,
        int evseId,
        int connectorId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await context.Set<ChargerComponent>()
                .AsNoTracking()
                .Where(component =>
                    component.ClientIdentifier == chargerId &&
                    component.ComponentName == "Connector" &&
                    component.Connector != null &&
                    component.Connector.EvseId == evseId &&
                    component.Connector.ConnectorId == connectorId)
                .SelectMany(component => component.ComponentVariables)
                .Where(variable => variable.Name == "ConnectorType")
                .Select(variable => variable.Value)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "Connector type enrichment timed out for charger {ChargerId} EVSE {EvseId} connector {ConnectorId}.",
                chargerId,
                evseId,
                connectorId);
            return null;
        }
        catch (Exception ex)
        {
            // Metadata enrichment is best-effort. An unavailable ConnectorType must
            // not suppress the status event.
            logger.LogWarning(
                ex,
                "Could not enrich connector type for charger {ChargerId} EVSE {EvseId} connector {ConnectorId}.",
                chargerId,
                evseId,
                connectorId);
            return null;
        }
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}

/// <summary>
/// Builds the shared <see cref="TransactionLifecycleEvent"/> for started and ended
/// transactions. Consumption is read from already processed samples.
/// </summary>
public sealed class TransactionLifecycleEventMapper(
    MedinillaOcppDbContext context,
    IConsumptionService consumptionService,
    ILogger<TransactionLifecycleEventMapper> logger)
{
    public async Task<TransactionLifecycleEvent> MapAsync(
        string chargerId,
        TransactionEventRequest request,
        CancellationToken cancellationToken)
    {
        var transactionId = request.TransactionInfo.TransactionId;

        var @event = new TransactionLifecycleEvent
        {
            ChargerId = chargerId,
            TransactionId = transactionId,
        };

        var consumption = await TryGetConsumptionAsync(chargerId, transactionId, cancellationToken)
            .ConfigureAwait(false);

        if (consumption.HasValue)
        {
            @event.ConsumptionWh = (double)consumption.Value;
        }

        return @event;
    }

    private async Task<decimal?> TryGetConsumptionAsync(
        string chargerId,
        string transactionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var samples = context.Set<TransactionSample>()
                .AsNoTracking()
                .Where(sample =>
                    sample.ChargingStation.ClientIdentifier == chargerId &&
                    sample.TransactionId == transactionId)
                .AsAsyncEnumerable();

            return await consumptionService
                .TryComputeConsumption(samples, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "Consumption enrichment timed out for charger {ChargerId} transaction {TransactionId}.",
                chargerId,
                transactionId);
            return null;
        }
        catch (Exception ex)
        {
            // Consumption is optional. Never block a lifecycle event on it.
            logger.LogWarning(
                ex,
                "Could not resolve consumption for charger {ChargerId} transaction {TransactionId}.",
                chargerId,
                transactionId);
            return null;
        }
    }
}
