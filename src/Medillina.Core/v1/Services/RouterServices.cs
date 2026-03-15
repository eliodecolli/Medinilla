using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.v1.Services;

public class RouterServices(ILogger<RouterServices> logger, IChargingStationBootingService chargingStationBootingService, ChargingStationUnitOfWork unitOfWork) : IRouterServices
{
    public async Task<bool> ValidateChargingStationAvailability(string clientIdentifier)
    {
        var chargingStation =
            (await unitOfWork.ChargingStationRepository.Filter(cs => cs.ClientIdentifier == clientIdentifier).ConfigureAwait(false)).FirstOrDefault();

        if (chargingStation is not null)
        {
            return chargingStation.Booted;
        }
        
        logger.LogError("Received message from {ClientIdentifier} but they're not yet bootstrapped!", clientIdentifier);
        return false;
    }

    public async Task DisconnectClient(string clientIdentifier)
    {
        await chargingStationBootingService.DisconnectClient(clientIdentifier);
    }
}