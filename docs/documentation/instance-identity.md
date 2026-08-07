---
title: Instance Identity
description: Per-process identity and the response queue name derived from it.
sidebar_position: 5
---

# Instance Identity

Project: `Medinilla.Core.WebApi`

## Interface

`Interfaces/IInstanceIdentifier.cs`

```csharp
public interface IInstanceIdentifier
{
    string InstanceId { get; }
    string ResponseQueue { get; }
}
```

## Implementation

`Services/InstanceIdentifier.cs` — `internal sealed class InstanceIdentifier`

| Member | Value |
| --- | --- |
| `InstanceId` | First 8 chars of `Guid.NewGuid().ToString("N")` |
| `ResponseQueue` | `medinilla.ws.{InstanceId}.response` |

Registered as a singleton, so the ID is generated once per host start.

```csharp
builder.Services.AddSingleton<IInstanceIdentifier, InstanceIdentifier>();
```

## Consumers

| Consumer | Use |
| --- | --- |
| `SubscriptionReceiverHostedService` | Queue name passed to `ISubscriptionReceiver.Start` |
| `WebSocketDigestionService` | Value written to `QueuedMessageRequest.ResponseQueue` and registered in the routing table |

## Properties

- Not configurable. Config would collide across replicas.
- Not persisted. A restarted process gets a new ID and a new queue; stale routing entries expire by TTL.
- Never sent to the CSMS directly. The CSMS learns the queue from the envelope or the routing table.
