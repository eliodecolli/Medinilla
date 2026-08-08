using Medinilla.DataTypes.Core;
using Medinilla.Infrastructure.WAMP;
using System;
using System.Collections.Generic;
using System.Text;

namespace Medinilla.Core.Interfaces.Services;

public interface ICommandExecutionService
{
    Task RegisterExecution(string clientIdentifier, string messageId, string actionName);

    Task SetExecutionResult(string clientIdentifier, ExecutionResult result);

    Task<IEnumerable<ExecutionResult>> FetchExecutionsForCharger(string clientIdentifier);
}
