using Medinilla.DataTypes.Contracts;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;
using Medinilla.DataTypes.Contracts.Common;
using System.Text.Json;


namespace Medinilla.Core.Commands.Ocpp201;

internal sealed class SetVariablesCommand(ILogger<SetVariablesCommand> log) : IOcppChargerCommand
{
    public string Action => OcppActionNames.SetVariables;

    public Task HandleError(OcppCallError error)
    {
        log.LogError("(SetVariables) {mi}: Errored: {err}, Error Details: {errd}, Error Code: {errc}",
            error.MessageId, error.ErrorDescription, error.ErrorDetails ?? "None", error.ErrorCode);

        return Task.CompletedTask;
    }

    public async Task HandleResponse(OcppCallResult result)
    {
        var response = result.As<SetVariablesResponse>();
        if (response is null)
        {
            log.LogError("{mid} could not be deserialized.", result.MessageId);
            return;
        }

        foreach (var varResp in response.SetVariableResult)
        {
            if (varResp.AttributeStatus != SetVariableStatusEnum.Accepted)
            {
                log.LogError("(SetVariables) {mi}: Set variable result for variable {vname} failed: {s}. Component details: {cd}",
                    result.MessageId, varResp.Variable.Name, varResp.AttributeStatus.ToString(), GetComponentDetails(varResp.Component));
            }
        }
    }

    private string GetComponentDetails(Component component)
    {
        // take the easy way
        return JsonSerializer.Serialize(component);
    }
}
