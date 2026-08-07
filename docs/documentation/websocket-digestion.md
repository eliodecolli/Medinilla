---
title: WebSocket Digestion
description: Per-connection service bridging a charger socket and the queues.
sidebar_position: 8
---

# WebSocket Digestion

Project: `Medinilla.Core.WebApi`

One instance per WebSocket connection, scoped to the request.

## Entry point

`Controllers/WSController.cs`

| Route | Subprotocol |
| --- | --- |
| `GET /ws/{clientIdentifier}` | `ocpp2.0.1` |

The controller registers the service in `IWSDigestionServiceCollection`, calls `Consume`, then removes it and disposes in a `finally`.

## Interface

`Interfaces/IBasicWebSocketDigestionService.cs`

```csharp
public interface IBasicWebSocketDigestionService : IAsyncDisposable
{
    Task Consume(WebSocket webSocket, string clientIdentifier);
}
```

## Implementation

`Services/WebSocketDigestionService.cs`

```csharp
public WebSocketDigestionService(
    IConfiguration config,
    ILogger<WebSocketDigestionService> logger,
    ISender sender,
    ISubscriptionReceiver subscription,
    IWebSocketRoutingTable routing,
    IInstanceIdentifier instance,
    IHostApplicationLifetime hostLifetime,
    IMessageQueueFactory queueFactory)
```

Every parameter is null-checked and throws `ArgumentNullException`.

## Consume sequence

1. Store the socket and client identifier.
2. Read `Comms:RequestQueue` into the outbound queue name. Missing key throws `InvalidOperationException`.
3. `routing.RegisterAsync(clientIdentifier, instance.ResponseQueue, hostLifetime.ApplicationStopping)`.
4. `subscription.Subscribe(clientIdentifier, callback)`.
5. Start the heartbeat loop.
6. Start the inbound and outbound vacuum tasks.
7. Run the receive loop until the socket closes or the token cancels.
8. Cancel the heartbeat and unsubscribe.

## Heartbeat

A background loop calls `routing.RefreshEntryAsync(clientIdentifier, ct)` every 30 seconds (`HeartbeatInterval`), keeping the routing entry alive while the socket is connected.

## Ordering slots

Two fields serialise OCPP request/response pairs in each direction.

| Field | Meaning |
| --- | --- |
| `_processingInboundId` | Message ID of the charger request awaiting a CSMS reply |
| `_processingOutboundId` | Message ID of the CSMS request awaiting a charger reply |

A second request arriving while a slot is occupied is buffered in an `IMessageQueue` and drained when the slot clears.

| Queue | Holds |
| --- | --- |
| `_inbound` | Charger requests waiting for the inbound slot |
| `_outbound` | CSMS requests waiting for the outbound slot |

`IMessageQueue.ScheduleVacuum` ticks every 5 seconds and calls `OnDrainQueue` when the queue is non-empty and its most recent enqueue is older than `General:MessageQueueTTL` seconds.

## Key methods

| Method | Role |
| --- | --- |
| `ProcessMessageInbound(byte[])` | Handles a frame received from the charger |
| `OnComms(Comms)` | Handles a message delivered by the subscription receiver |
| `PublishCommsMessage(Comms)` | Wraps in `QueuedMessageRequest` and sends to the request queue |
| `DrainInbound()` / `DrainOutbound()` | Release one buffered message |
| `SendRaw(byte[])` | Write to the socket under `_sendLock` |
| `GetMessageHeader(byte[])` | Extract `OcppHeader` (message ID and `CommsMessageType`) |
| `HeartbeatLoop(CancellationToken)` | Refresh the routing entry |

## Published envelope

`PublishCommsMessage` sends to `Comms:RequestQueue`:

| Field | Value |
| --- | --- |
| `QueuedMessageRequest.ClientIdentifier` | Connection's client identifier |
| `QueuedMessageRequest.ResponseQueue` | `IInstanceIdentifier.ResponseQueue` |
| `QueuedMessageRequest.Payload` | The `Comms` message |

## Disposal

`DisposeAsync` is idempotent and, in order:

1. Cancels the main and heartbeat tokens.
2. `subscription.Unsubscribe(clientIdentifier)`.
3. `routing.UnregisterAsync(clientIdentifier)` — failures are logged, not thrown.
4. Closes and disposes the socket.
5. Disposes the message queues, send lock and token sources.

Steps 2 and 3 are skipped when `Consume` was never called.

## Registration

```csharp
builder.Services.AddScoped<IBasicWebSocketDigestionService, WebSocketDigestionService>();
```
