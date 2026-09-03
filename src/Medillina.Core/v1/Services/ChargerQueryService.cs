using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace Medinilla.Core.v1.Services;

public sealed class ChargerQueryService(ChargingStationUnitOfWork unitOfWork) : IChargerQueryService
{
    
    public async Task<ChargingStation> GetByClientIdentifier(string clientIdentifier)
    {
        await unitOfWork.Start(c => c.ClientIdentifier == clientIdentifier);
        return unitOfWork.AggregateRoot;
    }

    public async Task<IReadOnlyList<ChargingStation>> ListPaged(int offset, int limit)
    {
        const int defaultLimit = 50;
        const int maxLimit = 200;

        var safeOffset = Math.Max(offset, 0);
        var safeLimit = limit <= 0 ? defaultLimit : Math.Min(limit, maxLimit);

        return await unitOfWork.FetchAll()
            .Include(c => c.EvseConnectors)
            .OrderBy(c => c.ClientIdentifier)
            .Skip(safeOffset)
            .Take(safeLimit)
            .ToListAsync();
    }
}
