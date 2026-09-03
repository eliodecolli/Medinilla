using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Interfaces;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public sealed class ChargingStationUnitOfWork(MedinillaOcppDbContext context) : RootUnitOfWork<ChargingStation>(context)
{
    public DbSet<EvseConnector> EvseConnectors => GetDbSet<EvseConnector>();
    
    public DbSet<Tariff> Tariffs => GetDbSet<Tariff>();
    
    public DbSet<AuthorizationDetails> AuthorizationDetails => GetDbSet<AuthorizationDetails>();
    
    public DbSet<AuthorizationUser> AuthorizationUser => GetDbSet<AuthorizationUser>();
    
    public DbSet<IdToken> IdTokens => GetDbSet<IdToken>();
    
    public DbSet<Account> Accounts => GetDbSet<Account>();
    
    public DbSet<TransactionEvent> TransactionEvents => GetDbSet<TransactionEvent>();
    
    public DbSet<TransactionSnapshot> TransactionSnapshots => GetDbSet<TransactionSnapshot>();
}
