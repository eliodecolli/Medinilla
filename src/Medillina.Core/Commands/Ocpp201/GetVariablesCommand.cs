using System.Text.Json;
using System.Text.Json.Serialization;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Commands.Ocpp201;

internal sealed class GetVariablesCommand(ILogger<GetVariablesCommand> log) : IOcppChargerCommand
{
    public string Action => OcppActionNames.GetVariables;

    public Task HandleError(OcppCallError error)
    {
        // TODO: react to OCPP error
        return Task.CompletedTask;
    }

    public Task HandleResponse(OcppCallResult result)
    {
        var variables = result.As<GetVariablesResponse>();
        if (variables is null)
        {
            log.LogError("{mid} could not be deserialized.", result.MessageId);
            return Task.CompletedTask;
        }

        var accepted = 0;
        var unknown = 0;
        var rejected = 0;
        var unrecognized = 0;

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

#if DEBUG
        try
        {
            var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDir);
            var pretty = JsonDocument.Parse(result.Payload ?? "{}").RootElement;
            File.WriteAllText(
                Path.Combine(logsDir, $"GetVariables-{result.MessageId}.txt"),
                JsonSerializer.Serialize(pretty, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "(GetVariables) {mid}: failed to dump response payload to logs folder.", result.MessageId);
        }
#endif

        return Task.CompletedTask;
    }
}
