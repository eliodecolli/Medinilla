using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Medinilla.Core.v1.Services;

public sealed class ChargerService(MedinillaOcppDbContext context) : IChargerService
{
    
    public async Task<ChargingStation> GetByClientIdentifier(string clientIdentifier)
    {
        return await context.GetChargingStation(clientIdentifier);
    }

    public async Task<IReadOnlyList<ChargingStation>> ListPaged(int offset, int limit)
    {
        const int defaultLimit = 50;
        const int maxLimit = 200;

        var safeOffset = Math.Max(offset, 0);
        var safeLimit = limit <= 0 ? defaultLimit : Math.Min(limit, maxLimit);

        return await context.Set<ChargingStation>()
            .Include(c => c.EvseConnectors)
            .OrderBy(c => c.ClientIdentifier)
            .Skip(safeOffset)
            .Take(safeLimit)
            .ToListAsync();
    }
}
