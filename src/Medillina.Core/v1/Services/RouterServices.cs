using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Relational;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.v1.Services;

public class RouterServices(ILogger<RouterServices> logger,
    MedinillaOcppDbContext context) : IRouterServices
{
    public async Task<bool> ValidateChargingStationAvailability(string clientIdentifier)
    {
        try
        {
            var chargingStation = await context.GetChargingStation(clientIdentifier).ConfigureAwait(false);
            return chargingStation.Booted;
        }
        catch (AggregateRootNotFoundException)
        {
            logger.LogError("Received message from {ClientIdentifier} but they're not yet bootstrapped!", clientIdentifier);
            return false;
        }
    }
}