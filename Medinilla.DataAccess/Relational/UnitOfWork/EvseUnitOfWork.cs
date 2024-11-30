using Medinilla.DataAccess.Interfaces;
using Medinilla.DataAccess.Relational.Models;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public sealed class EvseUnitOfWork(MedinillaOcppDbContext context)
{
    private readonly IRepository<EvseConnector> _evseConnectorRepository = new GenericRepository<EvseConnector>(context);

    public async Task ProcessStatusNotification(EvseConnector evseConnector)
    {
        var result = await _evseConnectorRepository.Filter(c => c.ChargingStationId == evseConnector.ChargingStationId &&
            c.EvseId == evseConnector.EvseId && c.ConnectorId == evseConnector.ConnectorId);
        var connector = result.FirstOrDefault();

        if (connector == null)
        {
            // oopsies, create a new one
            await _evseConnectorRepository.Create(evseConnector);
        }
        else
        {
            connector.ConnectorStatus = evseConnector.ConnectorStatus;
            connector.ModifiedAt = evseConnector.ModifiedAt;
        }
    }
}
