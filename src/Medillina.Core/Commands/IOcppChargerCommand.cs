using Medinilla.Infrastructure.WAMP;

namespace Medinilla.Core.Commands;

public interface IOcppChargerCommand
{
    string Action { get; }

    OcppCallRequest BuildCall(string messageId);

    void HandleResponse(string? responsePayload, OcppCallError? error);
}
