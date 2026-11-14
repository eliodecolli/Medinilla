using Medinilla.Infrastructure.WAMP;

namespace Medinilla.Core.Events;

/// <summary>
/// Core-owned publication boundary. Implementations decide whether a successfully
/// processed OCPP call maps to a webhook event and publish it with failure isolation.
/// </summary>
public interface IProcessedEventPublisher
{
    Task PublishForCallAsync(
        OcppCallRequest call,
        string clientIdentifier,
        RpcResult result,
        DateTime completedAtUtc);
}
