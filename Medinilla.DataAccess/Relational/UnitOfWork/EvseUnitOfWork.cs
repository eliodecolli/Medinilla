using Medinilla.DataAccess.Interfaces;
using Medinilla.DataAccess.Relational.Models;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public sealed class EvseUnitOfWork(MedinillaOcppDbContext context)
{
    private readonly IRepository<EvseConnector> _evseConnectorRepository = new GenericRepository<EvseConnector>(context);
    
    public IRepository<EvseConnector> EvseConnectorRepository => _evseConnectorRepository;
}
