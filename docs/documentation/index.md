---
title: Message Queueing
description: How OCPP traffic moves between the WebApi hosts and the CSMS over Redis.
sidebar_position: 1
---

# Message Queueing

OCPP chargers hold a WebSocket against a `Medinilla.Core.WebApi` instance. Protocol handling lives in `Medinilla.Core.Service` (the CSMS). Redis lists carry traffic between them.

## Documents

| Page | Contents |
| --- | --- |
| [Architecture](./architecture.md) | Processes, queues, naming scheme |
| [Contracts](./contracts.md) | Proto envelopes and enums |
| [Transport](./transport.md) | `ISender` / `IReceiver`, Redis connections |
| [Instance Identity](./instance-identity.md) | `IInstanceIdentifier`, response queue naming |
| [Routing Table](./routing-table.md) | `IWebSocketRoutingTable`, keys and TTL |
| [Subscription Receiver](./subscription-receiver.md) | `ISubscriptionReceiver`, fan-out by client |
| [WebSocket Digestion](./websocket-digestion.md) | `WebSocketDigestionService`, ordering slots |
| [CSMS Service](./csms-service.md) | `CoreInterfaceCommunication`, `OcppRequestDispatcher` |
| [Message Flows](./message-flows.md) | Step-by-step sequences |
| [Configuration](./configuration.md) | Config keys and DI registration |

## Projects

| Project | Role |
| --- | --- |
| `Medinilla.Core.WebApi` | Hosts charger WebSockets |
| `Medinilla.Core.Service` | CSMS; routes OCPP calls |
| `Medinilla.RealTime` | Queue transport, routing table, subscription receiver |
| `Medinilla.Core.SharedContracts` | Proto envelope definitions |
