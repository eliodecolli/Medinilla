using Grpc.Core;
using Medinilla.Core.gRPC.Query;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Service.Communication.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Service.Communication;

internal sealed class MedinillaQueryGrpc(
    ILogger<MedinillaQueryGrpc> log,
    IServiceProvider serviceProvider) : QueryService.QueryServiceBase
{
    public override async Task<GetChargerResponse> GetCharger(GetChargerRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ClientIdentifier))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "client_identifier is required"));
        }

        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IChargerService>();

        var cs = await service.GetByClientIdentifier(request.ClientIdentifier);
        if (cs is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Charger not found: {request.ClientIdentifier}"));
        }

        log.LogInformation("Query GetCharger ci={ci}", request.ClientIdentifier);

        return new GetChargerResponse
        {
            Charger = MedinillaMapping.MapCharger(cs),
        };
    }

    public override async Task<ListChargersResponse> ListChargers(ListChargersRequest request, ServerCallContext context)
    {
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IChargerService>();

        var chargers = await service.ListPaged(request.Offset, request.Limit);

        log.LogInformation("Query ListChargers offset={o} limit={l} returned={n}",
            request.Offset, request.Limit, chargers.Count);

        var response = new ListChargersResponse();
        response.Chargers.AddRange(chargers.Select(MedinillaMapping.MapCharger));
        return response;
    }

    public override async Task<ListTransactionSnapshotsResponse> ListTransactionSnapshots(ListTransactionSnapshotsRequest request, ServerCallContext context)
    {
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITransactionService>();

        var snapshots = await service.ListPaged(request.Offset, request.Limit);

        log.LogInformation("Query ListTransactionSnapshots offset={o} limit={l} returned={n}",
            request.Offset, request.Limit, snapshots.Count);

        var response = new ListTransactionSnapshotsResponse();
        response.Snapshots.AddRange(snapshots.Select(MedinillaMapping.MapTransactionSnapshot));
        return response;
    }
}
