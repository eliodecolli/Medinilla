using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.v1.Services;

public class RouterServices(ILogger<RouterServices> logger,
    IChargingStationBootingService chargingStationBootingService,
    ChargingStationUnitOfWork unitOfWork) : IRouterServices
{
    public async Task<bool> ValidateChargingStationAvailability(string clientIdentifier)
    {
        try
        {
            await unitOfWork.Start(cs => cs.ClientIdentifier == clientIdentifier).ConfigureAwait(false);
            return unitOfWork.AggregateRoot.Booted;
        }
        catch (AggregateRootNotFoundException)
        {
            logger.LogError("Received message from {ClientIdentifier} but they're not yet bootstrapped!", clientIdentifier);
            return false;
        }
    }
}