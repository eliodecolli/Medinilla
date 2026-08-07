using Medinilla.Core.SharedContracts.Comms;

namespace Medinilla.RealTime;

/// <summary>
/// Drains the single per-instance response queue and fans out to subscribers by
/// routing key (the charger's client identifier).
/// </summary>
public interface ISubscriptionReceiver : IAsyncDisposable
{
    void Start(string queueName);

    void Subscribe(string routingKey, Func<QueuedMessageResponse, CancellationToken, Task> onMessage);

    void Unsubscribe(string routingKey);
}
