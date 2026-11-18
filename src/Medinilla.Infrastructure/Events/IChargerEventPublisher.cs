using Medinilla.Core.SharedContracts.Events;

namespace Medinilla.Infrastructure.Events;

/// <summary>
/// Transport-neutral publication boundary for processed charger events.
/// The call completes when the configured transport accepts the event, not when a
/// webhook endpoint has been delivered to.
/// </summary>
public interface IChargerEventPublisher
{
    Task PublishAsync(
        MedinillaHttpEvent httpEvent,
        CancellationToken cancellationToken = default);
}
