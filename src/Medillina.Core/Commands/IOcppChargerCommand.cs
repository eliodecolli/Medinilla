using Medinilla.DataTypes.Core.CommandRequests;
using Medinilla.Infrastructure.WAMP;

namespace Medinilla.Core.Commands;

public interface IOcppChargerCommand
{
    string Action { get; }

    Task HandleResponse(OcppCallResult result);

    Task HandleError(OcppCallError error);
}
