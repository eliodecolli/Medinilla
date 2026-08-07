---
title: Subscription Receiver
description: Drains the per-instance response queue and fans out by client identifier.
sidebar_position: 7
---

# Subscription Receiver

Project: `Medinilla.RealTime`

One instance per WebApi process. It owns the only reader of that process's response queue and dispatches to the `WebSocketDigestionService` that registered for each client identifier.

## Interface

`ISubscriptionReceiver.cs`

```csharp
public interface ISubscriptionReceiver : IAsyncDisposable
{
    void Start(string queueName);
    void Subscribe(string routingKey, Func<QueuedMessageResponse, CancellationToken, Task> onMessage);
    void Unsubscribe(string routingKey);
}
```

The routing key is the charger's client identifier.

## Implementation

`Redis/RedisSubscriptionReceiver.cs` — `internal sealed class RedisSubscriptionReceiver`

```csharp
RedisSubscriptionReceiver(IReceiver receiver, ILogger<RedisSubscriptionReceiver> logger)
```

Subscribers are held in a `ConcurrentDictionary<string, Func<QueuedMessageResponse, CancellationToken, Task>>`.

## Dispatch loop

Per iteration:

1. `IReceiver.ReceiveAsync(queueName, ct)` — one `BLPOP` at a time.
2. Parse `QueuedMessageResponse`. On `InvalidProtocolBufferException`, log and continue.
3. Look up `response.ClientIdentifier`. On miss, log and continue.
4. Invoke the callback without awaiting it.

| Condition | Result |
| --- | --- |
| Malformed payload | Dropped, loop continues |
| No subscriber for the key | Dropped, loop continues |
| Callback throws synchronously | Logged, loop continues |
| Callback returns a faulted task | Logged by fault-only continuation, loop continues |
| Token cancelled | Loop returns |

Callbacks are not awaited. Per-charger ordering is enforced by `WebSocketDigestionService`, not by this loop.

## Lifecycle

- `Start` is idempotent. A second call is ignored, including with a different queue name.
- `Subscribe` is last-writer-wins for a given routing key.
- `DisposeAsync` cancels the loop, awaits it, and is safe to call twice.

## Hosted service

`Medinilla.Core.WebApi/Services/SubscriptionReceiverHostedService.cs`

```csharp
internal sealed class SubscriptionReceiverHostedService(
    ISubscriptionReceiver receiver,
    IInstanceIdentifier instance) : IHostedService
```

| Member | Action |
| --- | --- |
| `StartAsync` | `receiver.Start(instance.ResponseQueue)` |
| `StopAsync` | `receiver.DisposeAsync()` |

## Registration

```csharp
builder.Services.AddSubscriptionReceiver();
builder.Services.AddHostedService<SubscriptionReceiverHostedService>();
```

WebApi only. Requires `AddRealTimeServices()` first.

## Not used by the CSMS

`CoreInterfaceCommunication` reads `IReceiver` directly. The request queue has one consumer and no per-client fan-out, and it carries `QueuedMessageRequest`, which this interface does not accept.
