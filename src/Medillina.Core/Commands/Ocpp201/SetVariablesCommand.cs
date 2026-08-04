using Medinilla.Infrastructure.WAMP;

namespace Medinilla.Core.Commands.Ocpp201;

internal sealed class SetVariablesCommand : IOcppChargerCommand
{
    public string Action => OcppActionNames.SetVariables;

    public Task HandleError(OcppCallError error)
    {
        throw new NotImplementedException();
    }

    public Task HandleResponse(OcppCallResult result)
    {
        throw new NotImplementedException();
    }
}
