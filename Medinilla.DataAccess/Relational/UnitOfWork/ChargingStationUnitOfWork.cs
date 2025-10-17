using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Interfaces;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public sealed class ChargingStationUnitOfWork(MedinillaOcppDbContext context) : BaseUnitOfWork(context)
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

    public IRepository<ChargingStation> ChargingStationRepository => repository;

    public IRepository<Tariff> TariffsRepository => tariffsRepo;

    public IRepository<AuthorizationDetails> AuthDetailsRepository => authDetailsRepo;

    public IRepository<Account> AccountRepository => accountRepo;

    public IRepository<AuthorizationUser> AuthorizationUserRepository => authUsersRepo;

    public IRepository<IdToken> IdTokenRepository => idTokensRepo;

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

    public async Task<IdToken?> TryGetIdToken(string transactionId, string token)
    {
        // first check by request
        var idTokenQuery = await idTokensRepo.Filter(t => t.Token == token).ConfigureAwait(false);
        var idToken = idTokenQuery.FirstOrDefault();

        if (idToken is not null)
        {
            return idToken;
        }

        // try by tx
        return await _transactionsUnitOfWork.TryGetIdTokenForTransaction(transactionId);
    }

    public async Task ReleaseToken(IdToken idToken)
    {
        idToken.IsUnderTx = false;
        await idTokensRepo.Update(idToken);
    }
}
