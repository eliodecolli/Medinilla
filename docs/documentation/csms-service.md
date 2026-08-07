---
title: CSMS Service
description: Request queue consumer and the CSMS-initiated dispatch path.
sidebar_position: 9
---

# CSMS Service

Project: `Medinilla.Core.Service`

## Worker

`InboundWorker.cs`

```csharp
internal class InboundWorker(IInterfaceCommunication communication) : BackgroundService
```

`ExecuteAsync` calls `communication.Run(stoppingToken)`.

`Interfaces/IInterfaceCommunication.cs`

```csharp
internal interface IInterfaceCommunication
{
    Task Run(CancellationToken ct);
}
```

## Request queue consumer

`Communication/InterfaceCommunication.cs` — `internal sealed class CoreInterfaceCommunication`

```csharp
CoreInterfaceCommunication(
    IServiceProvider serviceProvider,
    IReceiver receiver,
    ISender sender,
    ILogger<CoreInterfaceCommunication> logger,
    CommunicationSettings settings)
```

| Method | Signature |
| --- | --- |
| `Run` | `Task Run(CancellationToken ct)` |
| `RunEvent` | `Task RunEvent(string requestChannel, CancellationToken ct)` |
| `ProcessOcppAsync` | `Task ProcessOcppAsync(string clientIdentifier, byte[] payload, string responseQueue)` |
| `DisconnectAsync` | `Task DisconnectAsync(string clientIdentifier)` |

### Loop

Reads `settings.RequestQueue`, parses `QueuedMessageRequest`, then switches on `Payload.MessageType`.

| `CommsMessageType` | Dispatch |
| --- | --- |
| `OcppRequest`, `OcppResponse` | `ProcessOcppAsync(request.ClientIdentifier, ocppBytes, request.ResponseQueue)` |
| `ClientDisconnect` | `DisconnectAsync(request.ClientIdentifier)` |

Both dispatches run on `Task.Run` and are not awaited by the loop. Exceptions are caught and logged per iteration, so malformed payloads do not stop the loop.

### Response path

`ProcessOcppAsync` resolves `IOcppCallRouter` from a new scope and calls `RouteOcppCall(payload, clientIdentifier)`.

| `RpcResult` | Sent |
| --- | --- |
| `Error` set | `Error.ToByteArray()` |
| `Result` set | `Result.ToByteArray()` |
| Both null | Nothing |

The reply is wrapped in a `Comms` of type `OcppResponse`, then in a `QueuedMessageResponse`, and sent to the `responseQueue` taken from the inbound envelope. The routing table is not consulted.

## CSMS-initiated dispatch

`Communication/OcppRequestDispatcher.cs`

```csharp
internal sealed class OcppRequestDispatcher(
    ISender sender,
    IWebSocketRoutingTable routing) : IOcppRequestDispatcher
```

`Medillina.Core/Interfaces/IOcppRequestDispatcher.cs`

```csharp
public interface IOcppRequestDispatcher
{
    Task SubmitRequest(string clientIdentifier, byte[] payload);
}
```

`SubmitRequest` looks up `routing.GetResponseQueueAsync(clientIdentifier)`, wraps the payload in a `Comms` of type `OcppRequest` then a `QueuedMessageResponse`, and sends it to the returned queue.

A `null` lookup throws `ChargerNotConnectedException`. There is no retry and no fallback queue.

## Caller

`Medillina.Core/v1/OcppCallRouter.cs`

```csharp
Task SubmitAsync(string clientIdentifier, OcppCallRequest request)
```

Adds the message ID to `BaseOcppRoutingTable`, then calls `dispatcher.SubmitRequest`. If dispatch throws, the entry is removed and the exception rethrown.

## Exception

`Exceptions/ChargerNotConnectedException.cs`

```csharp
public sealed class ChargerNotConnectedException(string clientIdentifier) : Exception
{
    public string ClientIdentifier { get; }
}
```

## gRPC mapping

`Communication/MedinillaGrpc.cs` catches it in `SetVariables` and `GetVariables` and returns an `Error` whose `Message` is a serialised OCPP `CALLERROR` with code `GenericError` and a `NotConnected:` prefix. Any other exception falls through to the generic handler.
