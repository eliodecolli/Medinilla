using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Contracts;
using Medinilla.Infrastructure.WAMP;
using Medinilla.RealTime;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class StatusNotificationAction(ChargingStationUnitOfWork unitOfWork,
    ILogger<StatusNotificationAction> logger,
    IRealTimeMessenger realTime) : IOcppAction
{
    public string ActionName => "StatusNotification";

    private EvseConnector GetEvseConnector(ChargingStation cs, StatusNotificationRequest request)
    {
        return new EvseConnector()
        {
            ChargingStationId = cs.Id,

            // we assume that if the connector is not specified, then the EVSE probably has only one connector
            ConnectorId = request.ConnectorId ?? 1,

            EvseId = request.EvseId,
            ConnectorStatus = Enum.GetName(request.ConnectorStatus)!,
            ModifiedAt = request.Timestamp ?? DateTime.UtcNow,
        };
    }

    private async Task ProcessStatusNotification(EvseConnector evseConnector)
    {
        var result = await unitOfWork.EvseConnectorSubUnit.EvseConnectorRepository.Filter(c => c.ChargingStationId == evseConnector.ChargingStationId &&
            c.EvseId == evseConnector.EvseId && c.ConnectorId == evseConnector.ConnectorId);
        var connector = result.FirstOrDefault();

        if (connector == null)
        {
            // oopsies, create a new one
            await unitOfWork.EvseConnectorSubUnit.EvseConnectorRepository.Create(evseConnector);
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

        var chargingStation = await unitOfWork.GetChargingStation(clientIdentifier);
        if (chargingStation == null)
        {
            logger.LogError($"Something weird has happened: Charging Station with ID {clientIdentifier} does not exist on our end.");
            return new RpcResult()
            {
                Error = call.CreateErrorResult<StatusNotificationResponse>(OcppCallError.ErrorCodes.GenericError, $"Client Identifier {clientIdentifier} not found."),
                ReturnToCS = true
            };
        }

        var evseConnector = GetEvseConnector(chargingStation, request);

        await ProcessStatusNotification(evseConnector);
        await unitOfWork.Save();

        // throw this event whenever it's convenient
        realTime.SendMessage("", []);

        return new RpcResult()
        {
            Result = call.CreateResult(new StatusNotificationResponse()),
            ReturnToCS = true
        };
    }
}
