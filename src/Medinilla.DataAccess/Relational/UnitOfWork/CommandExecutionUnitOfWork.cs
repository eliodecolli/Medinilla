using Medinilla.DataAccess.Interfaces;
using Medinilla.DataAccess.Relational.Models.Audit;
using System;
using System.Collections.Generic;
using System.Text;

namespace Medinilla.DataAccess.Relational.UnitOfWork;

public sealed class CommandExecutionUnitOfWork(MedinillaOcppDbContext context) : BaseUnitOfWork(context)
{
    private readonly IRepository<CommandExecution> _repository = new GenericRepository<CommandExecution>(context);

    public async Task<IEnumerable<CommandExecution>> FetchExecutions(string clientIdentifier)
    {
        return await _repository.Filter(ce => ce.ChargingStationClientIdentifier == clientIdentifier);
    }

    public async Task<CommandExecution> CreateExecution(CommandExecution ex)
    {
        return await _repository.Create(ex);
    }

    public async Task<CommandExecution?> FetchExecution(string messageId)
    {
        return (await _repository.Filter(ce => ce.MessageId == messageId).ConfigureAwait(false)).FirstOrDefault();
    }
}
