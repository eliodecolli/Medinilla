---
title: Message Flows
description: Step-by-step sequences for each direction.
sidebar_position: 10
---

# Message Flows

## Connection setup

| # | Component | Action |
| --- | --- | --- |
| 1 | `WSController` | Accepts `GET /ws/{clientIdentifier}`, subprotocol `ocpp2.0.1` |
| 2 | `WSController` | `IWSDigestionServiceCollection.Set(clientIdentifier, service)` |
| 3 | `WebSocketDigestionService` | `routing.RegisterAsync(clientId, instance.ResponseQueue, ApplicationStopping)` |
| 4 | `WebSocketDigestionService` | `subscription.Subscribe(clientId, callback)` |
| 5 | `WebSocketDigestionService` | Starts heartbeat and vacuum tasks, enters the receive loop |

## Charger-initiated call

```
charger ──[2,"req-1","BootNotification",{}]──► WebSocketDigestionService
                                                 │ _processingInboundId = "req-1"
                                                 ▼
                    RPUSH medinilla.core.request  (QueuedMessageRequest)
                      ClientIdentifier = charger
                      ResponseQueue    = medinilla.ws.a1b2c3d4.response
                      Payload.MessageType = OcppRequest
                                                 │
                                                 ▼
                                    CoreInterfaceCommunication
                                      IOcppCallRouter.RouteOcppCall
                                                 │
                    RPUSH medinilla.ws.a1b2c3d4.response  (QueuedMessageResponse)
                      Payload.MessageType = OcppResponse
                                                 │
                                                 ▼
                                    RedisSubscriptionReceiver
                                      fan-out by ClientIdentifier
                                                 │
                                                 ▼
                                    WebSocketDigestionService.OnComms
                                      matches _processingInboundId, clears slot
                                                 │
charger ◄─[3,"req-1",{}]─────────────────────────┘
```

The reply address comes from the envelope. The routing table is not read.

## CSMS-initiated call

```
gRPC SetVariables / GetVariables
        │
        ▼
OcppCallRouter.SubmitAsync
   BaseOcppRoutingTable.Add(messageId, action)
   ICommandExecutionService.RegisterExecution(clientId, messageId, action)
        │
        ▼
OcppRequestDispatcher.SubmitRequest
   routing.GetResponseQueueAsync(clientId) ──► medinilla.ws.a1b2c3d4.response
        │
        ▼
RPUSH medinilla.ws.a1b2c3d4.response  (QueuedMessageResponse)
   Payload.MessageType = OcppRequest
        │
        ▼
RedisSubscriptionReceiver ──► WebSocketDigestionService.OnComms
   _processingOutboundId = "out-1"
        │
charger ◄─[2,"out-1","GetVariables",{}]
        │
charger ──[3,"out-1",{}]──► ProcessMessageInbound
   matches _processingOutboundId, clears slot
        │
        ▼
RPUSH medinilla.core.request  (QueuedMessageRequest, OcppResponse)
        │
        ▼
CoreInterfaceCommunication ──► RouteOcppCall
   lookup action by messageId in BaseOcppRoutingTable
        │
        ▼
IOcppChargerCommand.HandleResponse(clientId, result, executionService)
   ICommandExecutionService.SetExecutionResult(clientId, ExecutionResult{...})
```

Each OCPP command owns the audit row it opened. See [Command audit](#command-audit) below.

## Offline charger

| # | Component | Action |
| --- | --- | --- |
| 1 | `OcppRequestDispatcher` | `GetResponseQueueAsync` returns `null` |
| 2 | `OcppRequestDispatcher` | Throws `ChargerNotConnectedException`; nothing is sent |
| 3 | `OcppCallRouter` | Removes the message ID from `BaseOcppRoutingTable`, closes the audit row with `SetExecutionResult(clientId, ExecutionResult{MessageId, Error=true, ErrorMessage="Error contacting charging station"})`, rethrows |
| 4 | `MedinillaGrpc` | Returns `Error` carrying an OCPP `CALLERROR` |

A dispatch failure always closes the audit row synchronously with `Error = true`. `ErrorMessage = "Error contacting charging station"` covers the `ChargerNotConnectedException` path; other dispatcher exceptions fall through the same `catch` and get the same audit result.

## Command audit

Every CSMS → charger call is audited via `ICommandExecutionService` so callers can correlate results by `MessageId`.

| Phase | Component | Call | Row state |
| --- | --- | --- | --- |
| Dispatch | `OcppCallRouter.SubmitAsync` | `RegisterExecution(clientId, messageId, action)` | `Completed = false`, `Error = false` |
| Dispatch failure | `OcppCallRouter.SubmitAsync` (`catch` arm) | `SetExecutionResult(clientId, ExecutionResult{MessageId, Error=true, ErrorMessage="Error contacting charging station"})` | `Completed = true`, `Error = true` |
| Reply (success) | `IOcppChargerCommand.HandleResponse` | `SetExecutionResult(clientId, ExecutionResult{MessageId, Error=false, ErrorMessage=null})` | `Completed = true`, `Error = false` |
| Reply (partial) | `IOcppChargerCommand.HandleResponse` | `SetExecutionResult(clientId, ExecutionResult{MessageId, Error=true, ErrorMessage="<failing items>"})` | `Completed = true`, `Error = true` |
| Reply (deserialize fail) | `IOcppChargerCommand.HandleResponse` | `SetExecutionResult(clientId, ExecutionResult{MessageId, Error=true, ErrorMessage="Incoming charger response could not be serialized."})` | `Completed = true`, `Error = true` |
| OCPP error | `IOcppChargerCommand.HandleError` | `SetExecutionResult(clientId, ExecutionResult{MessageId, Error=true, ErrorMessage=error.ErrorDescription})` | `Completed = true`, `Error = true` |

Audit rules the handler is responsible for:

- `Error = false` only when every per-item status is `Accepted`. Anything else (`Rejected`, `NotSupportedAttribute`, `Unknown`, `UnknownComponent`, `UnknownVariable`, or any status outside the OCPP 2.0.1 spec for the action) sets `Error = true` and contributes a one-line entry to `ErrorMessage`. Unknown statuses are real problems from a CSMS perspective — asking the charger for something it does not know about is a configuration / support issue worth surfacing.
- `HandleError` always sets `Error = true`; `ErrorMessage` is the OCPP error description.
- If the handler does not call `SetExecutionResult`, the row stays open with `Completed = false`. Treat handlers as required, not optional.

## Concurrent requests in one direction

| # | Event | Result |
| --- | --- | --- |
| 1 | Charger sends `req-1` | Forwarded; `_processingInboundId = "req-1"` |
| 2 | Charger sends `req-2` | Buffered in `_inbound` |
| 3 | CSMS replies to `req-1` | Slot cleared, reply sent to charger |
| 4 | `DrainInbound` | `req-2` forwarded; `_processingInboundId = "req-2"` |

The outbound direction behaves the same via `_outbound` and `DrainOutbound`. The two directions are independent, so a charger request and a CSMS request can be in flight simultaneously.

## Disconnect

| # | Component | Action |
| --- | --- | --- |
| 1 | `WebSocketDigestionService` | Receive loop exits on close status or cancellation |
| 2 | `WebSocketDigestionService` | Cancels heartbeat, `subscription.Unsubscribe(clientId)` |
| 3 | `WSController` | `IWSDigestionServiceCollection.Remove`, then `DisposeAsync` |
| 4 | `DisposeAsync` | `routing.UnregisterAsync(clientId)` deletes the routing key |

If the process dies without disposing, the routing key expires 60 seconds after its last refresh.
