---
title: Routing Table
description: Maps a charger to the response queue of the instance hosting its WebSocket.
sidebar_position: 6
---

# Routing Table

Project: `Medinilla.RealTime`

## Interface

`IWebSocketRoutingTable.cs`

```csharp
public interface IWebSocketRoutingTable
{
    Task RegisterAsync(string clientId, string queue, CancellationToken hostCt = default);
    Task UnregisterAsync(string clientId, CancellationToken ct = default);
    Task<string?> GetResponseQueueAsync(string clientId, CancellationToken ct = default);
    Task RefreshEntryAsync(string clientId, CancellationToken ct = default);
}
```

## Implementation

`Redis/RedisWebSocketRoutingTable.cs` — `internal sealed class RedisWebSocketRoutingTable`

```csharp
RedisWebSocketRoutingTable(IDatabase db, ILogger<RedisWebSocketRoutingTable> logger)
```

| Constant | Value |
| --- | --- |
| Key prefix | `medinilla.ws.routing` |
| Key format | `medinilla.ws.routing.{clientId}` |
| `Ttl` | 60 seconds |
| `RefreshDue` | 30 seconds |

## Redis commands

| Method | Command |
| --- | --- |
| `RegisterAsync` | `SET key queue EX 60` |
| `RefreshEntryAsync` | `SET key queue EX 60` |
| `UnregisterAsync` | `DEL key` |
| `GetResponseQueueAsync` | `GET key` |

## Behaviour

- `RegisterAsync` writes the key, then starts a per-client refresh loop linked to `hostCt`. Callers pass `IHostApplicationLifetime.ApplicationStopping`, so host shutdown ends every loop.
- Re-registering the same `clientId` cancels the previous loop and replaces the entry.
- `UnregisterAsync` cancels the loop and deletes the key. It deletes the key even for a client that was never registered.
- `RefreshEntryAsync` is a no-op for unknown clients. It writes only for entries this instance owns.
- `GetResponseQueueAsync` returns `null` when the key is absent or expired. The two cases are indistinguishable at this layer.

## Registration

```csharp
builder.Services.AddWebSocketRoutingTable();
```

Registered in both `Medinilla.Core.WebApi/Program.cs` and `Medinilla.Core.Service/Program.cs`. Requires `AddRealTimeServices()` first.
