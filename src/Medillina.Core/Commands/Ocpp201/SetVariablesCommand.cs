using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Medinilla.Core.Commands.Ocpp201;

internal sealed class SetVariablesCommand(ILogger<SetVariablesCommand> log) : IOcppChargerCommand
{
    public string Action => OcppActionNames.SetVariables;

    public async Task HandleError(string clientIdentifier, OcppCallError error, ICommandExecutionService executionService)
    {
        log.LogError("(SetVariables) {mi}: Errored: {err}, Error Details: {errd}, Error Code: {errc}",
            error.MessageId, error.ErrorDescription, error.ErrorDetails ?? "None", error.ErrorCode);

        await executionService.SetExecutionResult(clientIdentifier, new ExecutionResult(error.MessageId, true, error.ErrorDescription));
    }

    public async Task HandleResponse(string clientIdentifier, OcppCallResult result, ICommandExecutionService executionService)
    {
        var response = result.As<SetVariablesResponse>();
        if (response is null)
        {
            log.LogError("{mid} could not be deserialized.", result.MessageId);

            await executionService.SetExecutionResult(clientIdentifier,
                new ExecutionResult(result.MessageId, true, "Incoming charger response could not be serialized."));
            
            return;
        }

        var accepted = 0;
        var rebootRequired = 0;
        var notAccepted = 0;

        var sb = new StringBuilder();

        foreach (var varResp in response.SetVariableResult)
        {
            var evseId = varResp.Component.Evse?.Id;
            var connectorId = varResp.Component.Evse?.ConnectorId;

            switch (varResp.AttributeStatus)
            {
                case SetVariableStatusEnum.Accepted:
                    accepted++;
                    log.LogDebug(
                        "(SetVariables) {mi}: accepted {vname} on {cname}.{cinst} (evse={evseId}, connector={connectorId}, attr={attr})",
                        result.MessageId,
                        varResp.Variable.Name,
                        varResp.Component.Name,
                        varResp.Component.Instance,
                        evseId,
                        connectorId,
                        varResp.AttributeType);
                    break;

                case SetVariableStatusEnum.RebootRequired:
                    rebootRequired++;
                    log.LogInformation(
                        "(SetVariables) {mi}: reboot required for {vname} on {cname}.{cinst} (evse={evseId}, connector={connectorId}, attr={attr})",
                        result.MessageId,
                        varResp.Variable.Name,
                        varResp.Component.Name,
                        varResp.Component.Instance,
                        evseId,
                        connectorId,
                        varResp.AttributeType);
                    break;

                default:
                    notAccepted++;
                    log.LogWarning(
                        "(SetVariables) {mi}: {status} for {vname} on {cname}.{cinst} (evse={evseId}, connector={connectorId}, attr={attr})",
                        result.MessageId,
                        varResp.AttributeStatus,
                        varResp.Variable.Name,
                        varResp.Component.Name,
                        varResp.Component.Instance,
                        evseId,
                        connectorId,
                        varResp.AttributeType);

                    sb.AppendLine($"[{varResp.Component.Name}.{varResp.Component.Instance}.{varResp.Variable.Name}: {varResp.AttributeStatus}]");
                    break;
            }
        }

        await executionService.SetExecutionResult(clientIdentifier,
            new ExecutionResult(result.MessageId, notAccepted > 0, sb.Length > 0 ? sb.ToString() : null));

        log.LogInformation(
            "(SetVariables) {mi}: {ok}/{total} accepted, {rr} reboot-required, {failed} not accepted",
            result.MessageId,
            accepted,
            response.SetVariableResult.Count,
            rebootRequired,
            notAccepted);
    }
}
