---
title: Contracts
description: Proto envelopes carried on the Redis queues.
sidebar_position: 3
---

# Contracts

Project: `Medinilla.Core.SharedContracts`
Proto package: `medinilla.core.SharedContracts.comms`
C# namespace: `Medinilla.Core.SharedContracts.Comms`

## `comms/CommunicationMessage.proto`

```proto
enum CommsMessageType {
    OcppRequest      = 0;
    OcppResponse     = 1;
    ClientDisconnect = 2;
}

message Comms {
    CommsMessageType MessageType      = 1;
    string           ClientIdentifier = 2;
    bytes            Payload          = 3;
}
```

`Comms.Payload` is the raw OCPP-J array as UTF-8 bytes. It has no proto schema.

## `comms/QueuedMessage.proto`

```proto
message QueuedMessageRequest {
    string ClientIdentifier = 1;
    string ResponseQueue    = 2;
    Comms  Payload          = 3;
}

message QueuedMessageResponse {
    string ClientIdentifier = 1;
    Comms  Payload          = 2;
}
```

## Envelope usage

| Envelope | Queue | Written by | Read by |
| --- | --- | --- | --- |
| `QueuedMessageRequest` | `medinilla.core.request` | `WebSocketDigestionService` | `CoreInterfaceCommunication` |
| `QueuedMessageResponse` | `medinilla.ws.{instanceId}.response` | `CoreInterfaceCommunication`, `OcppRequestDispatcher` | `RedisSubscriptionReceiver` |

## Field notes

- `ClientIdentifier` appears on both the envelope and the inner `Comms`. The envelope copy is the routing key; consumers read that one.
- `ResponseQueue` exists only on `QueuedMessageRequest`. It tells the CSMS where to send the reply.
- There is no correlation-id field. Correlation uses the OCPP message ID inside `Comms.Payload`.

## Wire compatibility

`QueuedMessageRequest` and `QueuedMessageResponse` are not interchangeable. Field 2 is a `string` in one and a `Comms` in the other; both are wire type 2, so a cross-parse fails silently rather than erroring. Always parse with the type matching the queue.
