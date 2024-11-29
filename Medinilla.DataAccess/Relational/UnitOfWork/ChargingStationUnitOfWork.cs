using Medinilla.DataAccess.Interfaces;
using Medinilla.DataAccess.Relational.Models;
using Microsoft.Extensions.Configuration;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public sealed class ChargingStationUnitOfWork(MedinillaOcppDbContext context, IConfiguration config) : BaseUnitOfWork(context)
{
    private IRepository<ChargingStation> repository = new GenericRepository<ChargingStation>(context);
    private IRepository<Tariff> tariffsRepo = new GenericRepository<Tariff>(context);

    public EvseUnitOfWork EvseConnectorSubUnit = new EvseUnitOfWork(context); 

    public TransactionsUnitOfWork TransactionsSubUnit = new TransactionsUnitOfWork(context);

    public async Task ProcessBootNotification(ChargingStation chargingStation)
    {
        var result = await repository.Filter(c => c.ClientIdentifier == chargingStation.ClientIdentifier);
        var entity = result.FirstOrDefault();
        if (entity == null)
        {
            chargingStation.CreatedAt = DateTime.UtcNow;
            entity = await repository.Create(chargingStation);
        }
        else
        {
            entity.LatestBootNotificationReason = chargingStation.LatestBootNotificationReason;
            entity.ModifiedAt = DateTime.UtcNow;
        }

        if (chargingStation.Tariffs is null || chargingStation.Tariffs.Count == 0)
        {
            // get default unit price
            var defaultUnit = config.GetSection("Medinilla").GetSection("DefaultUnit");

            await tariffsRepo.Create(new Tariff()
            {
                Id = Guid.NewGuid(),
                ChargingStationId = entity.Id,
                UnitName = defaultUnit["Name"],
                UnitPrice = decimal.Parse(defaultUnit["Price"])
            });
        }
        
    }

    public async Task<ChargingStation?> GetChargingStation(string id)
    {
        var result = await repository.Filter(c => c.ClientIdentifier == id);
        return result.FirstOrDefault();
    }
}
