using Medinilla.DataAccess.Relational.Models;

namespace Medinilla.Core.Interfaces.Services;

public interface IChargerQueryService
{
    Task<ChargingStation?> GetByClientIdentifier(string clientIdentifier);

    Task<IReadOnlyList<ChargingStation>> ListPaged(int offset, int limit);
}
