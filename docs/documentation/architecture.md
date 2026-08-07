---
title: Architecture
description: Processes, queues and the naming scheme used between WebApi and CSMS.
sidebar_position: 2
---

# Architecture

## Processes

| Process | Instances | Queue role |
| --- | --- | --- |
| `Medinilla.Core.WebApi` | Many | Publishes to one shared request queue; consumes its own response queue |
| `Medinilla.Core.Service` | One or more | Consumes the shared request queue; publishes to per-instance response queues |

## Queues

All queues are Redis lists. `RPUSH` writes, `BLPOP` reads.

| Queue | Direction | Carries | Named by |
| --- | --- | --- | --- |
| `medinilla.core.request` | WebApi → CSMS | `QueuedMessageRequest` | `Comms:RequestQueue` config |
| `medinilla.ws.{instanceId}.response` | CSMS → WebApi | `QueuedMessageResponse` | `IInstanceIdentifier.ResponseQueue` |

One request queue exists for the whole deployment. One response queue exists per running WebApi process.

## Routing table

Redis string keys, not lists.

| Key | Value | TTL |
| --- | --- | --- |
| `medinilla.ws.routing.{clientId}` | Response queue name of the owning WebApi instance | 60s |

The CSMS reads this table to address CSMS-initiated calls. Charger-initiated traffic does not use it — the reply address travels on the envelope.

## Diagram

```
                    ┌──────────────────────────────┐
   charger ──WS──►  │ WebApi (instance a1b2c3d4)   │
                    │  WebSocketDigestionService   │
                    └───┬──────────────────────▲───┘
                        │                      │
        medinilla.core.request      medinilla.ws.a1b2c3d4.response
         (QueuedMessageRequest)      (QueuedMessageResponse)
                        │                      │
                    ┌───▼──────────────────────┴───┐
                    │ Core.Service (CSMS)          │
                    │  CoreInterfaceCommunication  │
                    │  OcppRequestDispatcher       │
                    └──────────────┬───────────────┘
                                   │
                       medinilla.ws.routing.{clientId}
                          (clientId → response queue)
```

## Reply addressing

| Origin | How the reply address is resolved |
| --- | --- |
| Charger-initiated | `QueuedMessageRequest.ResponseQueue` on the inbound envelope |
| CSMS-initiated | `IWebSocketRoutingTable.GetResponseQueueAsync(clientId)` |

A routing-table miss on the CSMS-initiated path throws `ChargerNotConnectedException`.
