using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;
using Medinilla.Infrastructure.WAMP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class StatusNotificationAction(MedinillaOcppDbContext context,
    ILogger<StatusNotificationAction> logger) : IOcppAction
{
    public string ActionName => OcppActionNames.StatusNotification;

    private EvseConnector GetEvseConnector(ChargingStation chargingStation, StatusNotificationRequest request)
    {
        return new EvseConnector()
        {
            ChargingStationId = chargingStation.Id,

            // we assume that if the connector is not specified, then the EVSE probably has only one connector
            ConnectorId = request.ConnectorId ?? 1,

            EvseId = request.EvseId,
            ConnectorStatus = Enum.GetName(request.ConnectorStatus)!,
            ModifiedAt = request.Timestamp ?? DateTime.UtcNow,
        };
    }

    private void ProcessStatusNotification(ChargingStation chargingStation, EvseConnector evseConnector)
    {
        var connector = chargingStation.EvseConnectors.
            AsQueryable()
            .FirstOrDefault(c =>
                c.ChargingStationId == evseConnector.ChargingStationId &&
                c.EvseId == evseConnector.EvseId &&
                c.ConnectorId == evseConnector.ConnectorId);

        if (connector == null)
        {
            // oopsies, create a new one
            chargingStation.EvseConnectors.Add(evseConnector);
        }
        else
        {
            connector.ConnectorStatus = evseConnector.ConnectorStatus;
            connector.ModifiedAt = evseConnector.ModifiedAt;
        }
    }

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var request = call.As<StatusNotificationRequest>();
        logger.LogInformation("{ci}: Status Notification Event Received - Connector Id: {cii} - Status: {cs}", clientIdentifier, request.ConnectorId, request.ConnectorStatus.ToString());

        try
        {
            var chargingStation = await context.GetChargingStation(clientIdentifier);
            var evseConnector = GetEvseConnector(chargingStation, request);

            ProcessStatusNotification(chargingStation, evseConnector);
            await context.SaveChangesAsync();

            return new RpcResult()
            {
                Result = call.CreateResult(new StatusNotificationResponse()),
                ReturnToCS = true
            };
        }
        catch (AggregateRootNotFoundException)
        {
            logger.LogError($"Something weird has happened: Charging Station with ID {clientIdentifier} does not exist on our end.");
            return new RpcResult()
            {
                Error = call.CreateErrorResult<StatusNotificationResponse>(OcppCallError.ErrorCodes.GenericError,
                    $"Client Identifier {clientIdentifier} not found."),
                ReturnToCS = true
            };
        }
    }
}
