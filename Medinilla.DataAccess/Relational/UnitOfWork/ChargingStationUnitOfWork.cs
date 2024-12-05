using Medinilla.DataAccess.Interfaces;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public sealed class ChargingStationUnitOfWork(MedinillaOcppDbContext context, IConfiguration config) : BaseUnitOfWork(context)
{
    private IRepository<ChargingStation> repository = new GenericRepository<ChargingStation>(context);
    private IRepository<Tariff> tariffsRepo = new GenericRepository<Tariff>(context);
    private IRepository<AuthorizationDetails> authDetailsRepo = new GenericRepository<AuthorizationDetails>(context);

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

        if (entity.Tariffs is null || entity.Tariffs.Count == 0)
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

        if (entity.AuthorizationDetails is null)
        {
            await authDetailsRepo.Create(new AuthorizationDetails()
            {
                AuthBlob = JsonDocument.Parse(config.GetSection("Medinilla")["DefaultAuthDetails"] ?? "{}"),
                ChargingStationId = entity.Id,
            });
        }
        
    }

    public async Task<ChargingStation?> GetChargingStation(string id)
    {
        var result = await repository.Filter(c => c.ClientIdentifier == id);
        return result.FirstOrDefault();
    }
}
