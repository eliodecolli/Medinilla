using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Commands.Ocpp201;

internal sealed class ResetCommand(ILogger<ResetCommand> log) : IOcppChargerCommand
{
    public string Action => OcppActionNames.Reset;

    public async Task HandleError(string clientIdentifier, OcppCallError error, ICommandExecutionService executionService)
    {
        log.LogError("(Reset) {mi}: Errored: {err}, Error Details: {errd}, Error Code: {errc}",
            error.MessageId, error.ErrorDescription, error.ErrorDetails ?? "None", error.ErrorCode);

        await executionService.SetExecutionResult(clientIdentifier, new ExecutionResult(error.MessageId, Action, true, error.ErrorDescription));
    }

    public async Task HandleResponse(string clientIdentifier, OcppCallResult result, ICommandExecutionService executionService)
    {
        var response = result.As<ResetResponse>();
        if (response is null)
        {
            log.LogError("{mid} could not be deserialized.", result.MessageId);

            await executionService.SetExecutionResult(clientIdentifier,
                new ExecutionResult(result.MessageId, Action, true, "Incoming charger response could not be serialized."));

            return;
        }

        var failed = response.Status != ResetStatusEnum.Accepted;
        var info = response.StatusInfo is null
            ? response.Status.ToString()
            : $"{response.Status} ({response.StatusInfo.ReasonCode}: {response.StatusInfo.AdditionalInfo})";

        log.LogInformation("(Reset) {mi}: status={status}", result.MessageId, info);

        await executionService.SetExecutionResult(clientIdentifier,
            new ExecutionResult(result.MessageId, Action, failed, info));
    }
}
