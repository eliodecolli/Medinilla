using Medinilla.DataAccess.Interfaces;
using Medinilla.DataAccess.Relational.Models;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public sealed class ChargingStationUnitOfWork(MedinillaOcppDbContext context) : BaseUnitOfWork(context)
{
    private IRepository<ChargingStation> repository = new GenericRepository<ChargingStation>(context);

    public async Task ProcessBootNotification(ChargingStation chargingStation)
    {
        var entity = await repository.Get(chargingStation.Id);
        if (entity == null)
        {
            chargingStation.CreatedAt = DateTime.UtcNow;
            await repository.Create(chargingStation);
        }
        else
        {
            entity.LatestBootNotificationReason = chargingStation.LatestBootNotificationReason;
            entity.ModifiedAt = DateTime.UtcNow;
        }
    }
}
