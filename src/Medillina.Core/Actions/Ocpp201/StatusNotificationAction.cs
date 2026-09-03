using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;
using Medinilla.Infrastructure.WAMP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class StatusNotificationAction(ChargingStationUnitOfWork unitOfWork,
    ILogger<StatusNotificationAction> logger) : IOcppAction
{
    public string ActionName => OcppActionNames.StatusNotification;

    private EvseConnector GetEvseConnector(StatusNotificationRequest request)
    {
        return new EvseConnector()
        {
            ChargingStationId = unitOfWork.AggregateRoot.Id,

            // we assume that if the connector is not specified, then the EVSE probably has only one connector
            ConnectorId = request.ConnectorId ?? 1,

            EvseId = request.EvseId,
            ConnectorStatus = Enum.GetName(request.ConnectorStatus)!,
            ModifiedAt = request.Timestamp ?? DateTime.UtcNow,
        };
    }

    private async Task ProcessStatusNotification(EvseConnector evseConnector)
    {
        var connector = await unitOfWork.EvseConnectors.FirstOrDefaultAsync(c => c.ChargingStationId == evseConnector.ChargingStationId &&
            c.EvseId == evseConnector.EvseId && c.ConnectorId == evseConnector.ConnectorId).ConfigureAwait(false);

        if (connector == null)
        {
            // oopsies, create a new one
            await unitOfWork.EvseConnectors.AddAsync(evseConnector);
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
            await unitOfWork.Start(c => c.ClientIdentifier == clientIdentifier);
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

        var evseConnector = GetEvseConnector(request);

        await ProcessStatusNotification(evseConnector);
        await unitOfWork.Save();

        return new RpcResult()
        {
            Result = call.CreateResult(new StatusNotificationResponse()),
            ReturnToCS = true
        };
    }
}
