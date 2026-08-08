using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Medinilla.Core.Commands.Ocpp201;

internal sealed class GetVariablesCommand(ILogger<GetVariablesCommand> log) : IOcppChargerCommand
{
    public string Action => OcppActionNames.GetVariables;

    public async Task HandleError(string clientIdentifier, OcppCallError error, ICommandExecutionService executionService)
    {
        log.LogError("(GetVariables) {mi}: Errored: {err}, Error Details: {errd}, Error Code: {errc}",
            error.MessageId, error.ErrorDescription, error.ErrorDetails ?? "None", error.ErrorCode);

        await executionService.SetExecutionResult(clientIdentifier, new ExecutionResult(error.MessageId, Action, true, error.ErrorDescription));
    }

    public async Task HandleResponse(string clientIdentifier, OcppCallResult result, ICommandExecutionService executionService)
    {
        var variables = result.As<GetVariablesResponse>();
        if (variables is null)
        {
            log.LogError("{mid} could not be deserialized.", result.MessageId);

            await executionService.SetExecutionResult(clientIdentifier,
                new ExecutionResult(result.MessageId, Action, true, "Incoming charger response could not be serialized."));

            return;
        }

        var accepted = 0;
        var unknown = 0;
        var rejected = 0;
        var unrecognized = 0;

        var sb = new StringBuilder();

        foreach (var v in variables.GetVariableResult ?? Enumerable.Empty<GetVariableResult>())
        {
            var evseId = v.Component.Evse?.Id;
            var connectorId = v.Component.Evse?.ConnectorId;

            switch (v.AttributeStatus)
            {
                case GetVariableStatusEnum.Accepted:
                    accepted++;
                    log.LogDebug(
                        "(GetVariables) {mi}: {vname} on {cname}.{cinst} (evse={evseId}, connector={connectorId}, attr={attr}) = {val}",
                        result.MessageId,
                        v.Variable.Name,
                        v.Component.Name,
                        v.Component.Instance,
                        evseId,
                        connectorId,
                        v.AttributeType,
                        v.AttributeValue);
                    break;

                case GetVariableStatusEnum.UnknownComponent:
                case GetVariableStatusEnum.UnknownVariable:
                    unknown++;
                    log.LogWarning(
                        "(GetVariables) {mi}: {status} for {vname} on {cname}.{cinst} (evse={evseId}, connector={connectorId}, attr={attr})",
                        result.MessageId,
                        v.AttributeStatus,
                        v.Variable.Name,
                        v.Component.Name,
                        v.Component.Instance,
                        evseId,
                        connectorId,
                        v.AttributeType);

                    sb.AppendLine($"[{v.Component.Name}.{v.Component.Instance}.{v.Variable.Name}: {v.AttributeStatus}]");
                    break;

                case GetVariableStatusEnum.Unknown:
                    unrecognized++;
                    log.LogWarning(
                        "(GetVariables) {mi}: unrecognized attributeStatus for {vname} on {cname}.{cinst} (evse={evseId}, connector={connectorId}, attr={attr})",
                        result.MessageId,
                        v.Variable.Name,
                        v.Component.Name,
                        v.Component.Instance,
                        evseId,
                        connectorId,
                        v.AttributeType);

                    sb.AppendLine($"[{v.Component.Name}.{v.Component.Instance}.{v.Variable.Name}: {v.AttributeStatus}]");
                    break;

                default:
                    rejected++;
                    log.LogWarning(
                        "(GetVariables) {mi}: {status} for {vname} on {cname}.{cinst} (evse={evseId}, connector={connectorId}, attr={attr})",
                        result.MessageId,
                        v.AttributeStatus,
                        v.Variable.Name,
                        v.Component.Name,
                        v.Component.Instance,
                        evseId,
                        connectorId,
                        v.AttributeType);

                    sb.AppendLine($"[{v.Component.Name}.{v.Component.Instance}.{v.Variable.Name}: {v.AttributeStatus}]");
                    break;
            }
        }

        var total = variables.GetVariableResult?.Count ?? 0;
        log.LogInformation(
            "(GetVariables) {mi}: {ok}/{total} returned, {nf} not-found, {unrec} unrecognized, {rej} rejected",
            result.MessageId,
            accepted,
            total,
            unknown,
            unrecognized,
            rejected);

        if (unrecognized > 0)
        {
            log.LogWarning(
                "(GetVariables) {mi}: {n} items had an attributeStatus outside the OCPP 2.0.1 spec. Raw payload: {payload}",
                result.MessageId,
                unrecognized,
                result.Payload);
        }

        await executionService.SetExecutionResult(clientIdentifier,
            new ExecutionResult(result.MessageId, Action, rejected + unknown + unrecognized > 0, sb.Length > 0 ? sb.ToString() : null));
    }
}
