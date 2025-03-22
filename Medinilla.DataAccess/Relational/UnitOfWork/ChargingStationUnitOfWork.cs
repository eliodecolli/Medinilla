using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Interfaces;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public sealed class ChargingStationUnitOfWork(MedinillaOcppDbContext context,
    IConfiguration config,
    ILogger<ChargingStationUnitOfWork> _logger) : BaseUnitOfWork(context)
{
    private IRepository<ChargingStation> repository = new GenericRepository<ChargingStation>(context);
    private IRepository<Tariff> tariffsRepo = new GenericRepository<Tariff>(context);
    private IRepository<AuthorizationDetails> authDetailsRepo = new GenericRepository<AuthorizationDetails>(context);
    private IRepository<Account> accountRepo = new GenericRepository<Account>(context);
    private IRepository<AuthorizationUser> authUsersRepo = new GenericRepository<AuthorizationUser>(context);
    private IRepository<IdToken> idTokensRepo = new GenericRepository<IdToken>(context);

    private EvseUnitOfWork _evseUnitOfWork = new EvseUnitOfWork(context);
    private TransactionsUnitOfWork _transactionsUnitOfWork = new TransactionsUnitOfWork(context);

    public EvseUnitOfWork EvseConnectorSubUnit => _evseUnitOfWork;

    public TransactionsUnitOfWork TransactionSubUnit => _transactionsUnitOfWork;

    public async Task<ChargingStation> ProcessBootNotification(ChargingStation chargingStation)
    {
        var result = await repository.Filter(c => c.ClientIdentifier == chargingStation.ClientIdentifier);
        var entity = result.FirstOrDefault();
        if (entity == null)
        {
            var accountQuery = await accountRepo.Filter(c => c.Name == "Account 1").ConfigureAwait(false);
            var account = accountQuery.First();
            chargingStation.AccountId = account.Id;

            chargingStation.CreatedAt = DateTime.UtcNow;
            entity = await repository.Create(chargingStation);
        }
        else
        {
            entity.LatestBootNotificationReason = chargingStation.LatestBootNotificationReason;
            entity.ModifiedAt = DateTime.UtcNow;
        }

        var medinillaSettings = config.GetSection("Medinilla");

        if (entity.Tariffs is null || entity.Tariffs.Count == 0)
        {
            // get default unit price
            var defaultUnit = medinillaSettings.GetSection("DefaultUnit");

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
                AuthBlob = JsonDocument.Parse(medinillaSettings["DefaultAuthDetails"] ?? "{}"),
                ChargingStationId = entity.Id,
            });
        }

        if (entity.IdTokens.Count == 0 && bool.TryParse(medinillaSettings["UseDefaultUser"], out bool useDefaultUser) && useDefaultUser)
        {
            var defaultUser = medinillaSettings.GetSection("DefaultUser");
            var entityUser = await authUsersRepo.Create(new AuthorizationUser()
            {
                ChargingStationId = entity.Id,
                ActiveCredit = int.Parse(defaultUser["ActiveCredit"] ?? "10000"),
                DisplayName = defaultUser["DisplayName"] ?? "Dummy Display Name (Not Found in Config)",
                IsActive = true
            });
            await idTokensRepo.Create(new IdToken()
            {
                ChargingStationId = entity.Id,
                AuthorizationUserId = entityUser.Id,
                Token = defaultUser["Token"] ?? "DEADBEEF",
                CreatedDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(100000),
                IdType = "ISO14443"
            });
        }

        return entity;
    }

    public async Task<ChargingStation?> GetChargingStation(string id)
    {
        var result = await repository.Filter(c => c.ClientIdentifier == id);
        return result.FirstOrDefault();
    }

    public async Task<IEnumerable<ChargingStation>> GetChargingStations(string accountId)
    {
        if(Guid.TryParse(accountId, out var guid))
        {
            return await repository.Filter(c => c.AccountId == guid);
        }
        else
        {
            throw new OcppCrudException($"Invalid account id: {accountId}");
        }
    }
}
