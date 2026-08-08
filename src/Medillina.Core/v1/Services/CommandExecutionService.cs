using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Relational.Models.Audit;
using Medinilla.DataAccess.Relational.UnitOfWork;
using Medinilla.DataTypes.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Medinilla.Core.v1.Services;

public sealed class CommandExecutionService(
    ILogger<CommandExecutionService> log,
    CommandExecutionUnitOfWork unitOfWork) : ICommandExecutionService
{
    public async Task<IEnumerable<ExecutionResult>> FetchExecutionsForCharger(string clientIdentifier)
    {
        return (await unitOfWork.FetchExecutions(clientIdentifier).ConfigureAwait(false))
            .Select(e => new ExecutionResult(e.MessageId, e.ActionName, e.Error, e.ErrorMessage));
    }

    public async Task RegisterExecution(string clientIdentifier, string messageId, string actionName)
    {
        var execution = await unitOfWork.CreateExecution(new CommandExecution()
        {
            ActionName = actionName,
            MessageId = messageId,
            Completed = false,
            Error = false,
            StartTime = DateTime.UtcNow,
            ChargingStationClientIdentifier = clientIdentifier,
        });
        await unitOfWork.Save();

        log.LogInformation("[{ci}]: Registered execution id={ei} msgId={mi} action={an}",
            clientIdentifier,
            execution.Id,
            messageId,
            actionName);
    }

    public async Task SetExecutionResult(string clientIdentifier, ExecutionResult result)
    {
        var entity = await unitOfWork.FetchExecution(result.MessageId);
        if (entity is null)
        {
            log.LogError("[{ci}]: Could not find execution for msgId={mi}",
                clientIdentifier,
                result.MessageId);

            return;
        }

        entity.Error = result.Error;
        entity.ErrorMessage = result.ErrorMessage;
        entity.Completed = true;
        entity.EndTime = DateTime.UtcNow;

        await unitOfWork.Save();
    }
}
