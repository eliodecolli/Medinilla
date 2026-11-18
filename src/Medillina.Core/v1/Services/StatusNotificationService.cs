using Medinilla.Core.Events;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.SharedContracts.Events;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataTypes.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.v1.Services;

public sealed class StatusNotificationService(
    MedinillaOcppDbContext context,
    IApplicationEventEmitter eventEmitter,
    StatusNotificationEventMapper statusNotificationEventMapper,
    ILogger<StatusNotificationService> logger) : IStatusNotificationService
{
    public async Task ProcessNotification(string clientIdentifier, StatusNotificationRequest request)
    {
        logger.LogInformation(
            "{ci}: Status Notification Event Received - Connector Id: {cii} - Status: {cs}",
            clientIdentifier,
            request.ConnectorId,
            request.ConnectorStatus.ToString());

        var chargingStation = await context.GetChargingStation(clientIdentifier);
        var evseConnector = GetEvseConnector(chargingStation, request);

        ProcessStatusNotification(chargingStation, evseConnector);
        await context.SaveChangesAsync();

        var snapshot = new StatusNotificationSnapshot(
            clientIdentifier,
            chargingStation.AccountId,
            evseConnector.EvseId,
            evseConnector.ConnectorId,
            evseConnector.ConnectorStatus,
            ToUtc(evseConnector.ModifiedAt));

        var completedAtUtc = DateTimeOffset.UtcNow;
        await eventEmitter.TryEmitAsync(
            WebhookEventTypes.ConnectorStatus,
            completedAtUtc,
            () => statusNotificationEventMapper.Map(snapshot));
    }

    private static EvseConnector GetEvseConnector(
        ChargingStation chargingStation,
        StatusNotificationRequest request)
    {
        return new EvseConnector
        {
            ChargingStationId = chargingStation.Id,
            ConnectorId = request.ConnectorId ?? 1,
            EvseId = request.EvseId,
            ConnectorStatus = Enum.GetName(request.ConnectorStatus)!,
            ModifiedAt = request.Timestamp ?? DateTime.UtcNow,
        };
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    private static void ProcessStatusNotification(
        ChargingStation chargingStation,
        EvseConnector evseConnector)
    {
        var connector = chargingStation.EvseConnectors
            .AsQueryable()
            .FirstOrDefault(c =>
                c.ChargingStationId == evseConnector.ChargingStationId &&
                c.EvseId == evseConnector.EvseId &&
                c.ConnectorId == evseConnector.ConnectorId);

        if (connector is null)
        {
            chargingStation.EvseConnectors.Add(evseConnector);
        }
        else
        {
            connector.ConnectorStatus = evseConnector.ConnectorStatus;
            connector.ModifiedAt = evseConnector.ModifiedAt;
        }
    }
}
