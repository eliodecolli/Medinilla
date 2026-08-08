using Medinilla.Core.Interfaces.Services;
using Medinilla.Infrastructure.WAMP;

namespace Medinilla.Core.Commands;

public interface IOcppChargerCommand
{
    string Action { get; }

    Task HandleResponse(string clientIdentifier, OcppCallResult result, ICommandExecutionService executionService);

    Task HandleError(string clientIdentifier, OcppCallError error, ICommandExecutionService executionService);
}
