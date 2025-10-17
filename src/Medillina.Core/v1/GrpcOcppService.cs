using Grpc.Core;
using Medinilla.Core.gRPC.Contracts;
using Medinilla.Core.gRPC.Service;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Relational.UnitOfWork;

namespace Medinilla.Core.v1;

public class GrpcOcppService (ChargingStationUnitOfWork chargingStationUnitOfWork)
    : OcppService.OcppServiceBase
{
    public override async Task<GetChargingStationsResponse> GetChargingStations(GetChargingStationsQuery request, ServerCallContext context)
    {
        try
        {
            var chargingStations = await chargingStationUnitOfWork.GetChargingStations(request.AccountId);
            return new GetChargingStationsResponse
            {
                ChargingStations =
                {
                    chargingStations.Select(c => new ChargingStation
                    {
                        Id = c.Id.ToString(),
                        AccountId = c.AccountId.ToString(),
                        Alias = c.Alias,
                        LastStatus = c.LatestBootNotificationReason,
                        Location = c.Location,
                    })
                }
            };
        }
        catch (OcppCrudException ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<GetChargingStationResponse> GetChargingStation(GetChargingStationQuery request, ServerCallContext context)
    {
        try
        {
            var chargingStation = await chargingStationUnitOfWork.GetChargingStation(request.ChargingStationId);
            if (chargingStation is null)
            {
                return new GetChargingStationResponse()
                {
                    ChargingStation = null,
                    Error = new Error()
                    {
                        Message = $"Charging Station with Id {request.ChargingStationId} not found.",
                        HasError = true
                    }
                };
            }

            return new GetChargingStationResponse
            {
                ChargingStation = new ChargingStation
                {
                    Id = chargingStation.Id.ToString(),
                    AccountId = chargingStation.AccountId.ToString(),
                    Alias = chargingStation.Alias,
                    LastStatus = chargingStation.LatestBootNotificationReason,
                    Location = chargingStation.Location,
                }
            };
        }
        catch (OcppCrudException ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }
}
