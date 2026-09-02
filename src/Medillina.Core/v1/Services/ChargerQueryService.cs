using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.UnitOfWork;

namespace Medinilla.Core.v1.Services;

public sealed class ChargerQueryService(ChargingStationUnitOfWork unitOfWork) : IChargerQueryService
{
    public Task<ChargingStation?> GetByClientIdentifier(string clientIdentifier) =>
        unitOfWork.GetByClientIdentifierWithConnectors(clientIdentifier);

    public Task<IReadOnlyList<ChargingStation>> ListPaged(int offset, int limit) =>
        unitOfWork.ListPaged(offset, limit);
}
