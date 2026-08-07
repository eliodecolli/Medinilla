---
title: Transport
description: Queue primitives and the Redis connections behind them.
sidebar_position: 4
---

# Transport

Project: `Medinilla.RealTime`

## Interfaces

`ISender.cs`

```csharp
public interface ISender : IDisposable
{
    Task SendAsync(string queue, byte[] message, CancellationToken ct = default);
}
```

`IReceiver.cs`

```csharp
public interface IReceiver : IDisposable
{
    Task<byte[]?> ReceiveAsync(string queue, CancellationToken ct = default);
}
```

## Redis implementations

`Redis/RedisQueue.cs`

| Type | Command | Notes |
| --- | --- | --- |
| `RedisSender` | `RPUSH` | Appends to the tail |
| `RedisReceiver` | `BLPOP` | 3-second block per call, retried until cancelled |

`ReceiveAsync` returns `null` only when the cancellation token fires. `RedisTimeoutException` is swallowed and the block is reissued.

## Connection multiplexers

Registered as keyed singletons in `Redis/ServiceCollectionExtensions.cs`. Keys are exposed on `RedisUtils`.

| Key constant | Value | Used by |
| --- | --- | --- |
| `RedisUtils.ProducerConnectionMultiplexer` | `medinilla.producer` | `RedisSender`, `RedisWebSocketRoutingTable` |
| `RedisUtils.ConsumerConnectionMultiplexer` | `medinilla.consumer` | `RedisReceiver` (scoped `IReceiver`) |
| `RedisUtils.SubscriptionConnectionMultiplexer` | `medinilla.subscription` | `RedisSubscriptionReceiver` |

Each multiplexer gets its own `ClientName`. The subscription multiplexer is separate because its dispatch loop holds a permanent `BLPOP`.

Registrations are lazy factories. A process that never resolves the subscription receiver never opens that connection.

## Service lifetimes

| Service | Lifetime |
| --- | --- |
| `ISender` | Scoped |
| `IReceiver` | Scoped |
| `ISubscriptionReceiver` | Singleton |
| `IWebSocketRoutingTable` | Singleton |
| `ConnectionMultiplexer` (all keys) | Singleton |

## Redis endpoint

`Medinilla.RealTime/appsettings.json`, embedded as a resource.

```json
{
  "Redis": {
    "Uri": "localhost:6379"
  }
}
```
