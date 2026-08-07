using Google.Protobuf;
using Medinilla.Core.SharedContracts.Comms;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Medinilla.RealTime.Redis;

internal sealed class RedisSubscriptionReceiver(
    IReceiver receiver,
    ILogger<RedisSubscriptionReceiver> logger) : ISubscriptionReceiver
{
    private readonly ConcurrentDictionary<string, Func<QueuedMessageResponse, CancellationToken, Task>> _subscribers = new();

    private string? _queueName;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public void Start(string queueName)
    {
        if (_cts is not null) return;

        _queueName = queueName;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => DispatchLoop(_cts.Token), CancellationToken.None);

        logger.LogInformation("Subscription receiver listening on {Queue}", queueName);
    }

    // Last subscriber wins: a reconnecting charger replaces its own stale callback.
    public void Subscribe(string routingKey, Func<QueuedMessageResponse, CancellationToken, Task> onMessage)
        => _subscribers.AddOrUpdate(routingKey, onMessage, (_, _) => onMessage);

    public void Unsubscribe(string routingKey)
        => _subscribers.TryRemove(routingKey, out _);

    // One BLPOP at a time. Subscribers are invoked without awaiting — ordering per
    // routing key is the WebSocketDigestionService's job, not this loop's.
    private async Task DispatchLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            byte[]? raw;
            try
            {
                raw = await receiver.ReceiveAsync(_queueName!, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (raw is null) continue;

            QueuedMessageResponse response;
            try
            {
                response = QueuedMessageResponse.Parser.ParseFrom(raw);
            }
            catch (InvalidProtocolBufferException)
            {
                logger.LogWarning("Malformed QueuedMessageResponse dropped");
                continue;
            }

            if (!_subscribers.TryGetValue(response.ClientIdentifier, out var callback))
            {
                logger.LogWarning("No subscriber for {ClientId} — dropping", response.ClientIdentifier);
                continue;
            }

            // Fault-only continuation: a throwing subscriber logs without stalling the loop.
            // The try/catch covers the synchronous part, which throws before there is
            // any task to attach a continuation to.
            try
            {
                _ = callback(response, ct).ContinueWith(
                    t => logger.LogError(t.Exception!, "Subscriber for {ClientId} threw", response.ClientIdentifier),
                    TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Subscriber for {ClientId} threw", response.ClientIdentifier);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();

        if (_loop is not null)
        {
            try
            {
                await _loop;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts?.Dispose();
        _cts = null;
    }
}
