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

Adds the message ID to `BaseOcppRoutingTable`, then calls `executionService.RegisterExecution(clientId, messageId, action)` to open the audit row, then calls `dispatcher.SubmitRequest`. If either call throws, the routing-table entry is removed, the audit row is closed via `executionService.SetExecutionResult(clientId, ExecutionResult{MessageId, Error=true, ErrorMessage="Error contacting charging station"})`, and the exception is rethrown.

The corresponding `IOcppChargerCommand.HandleResponse` / `HandleError` is responsible for closing the audit row via `executionService.SetExecutionResult(...)` when the charger replies. See [Command audit](./message-flows.md#command-audit).

## Exception

`Exceptions/ChargerNotConnectedException.cs`

```csharp
public sealed class ChargerNotConnectedException(string clientIdentifier) : Exception
{
    public string ClientIdentifier { get; }
}
```

## Command execution audit

Outbound calls are audited through `ICommandExecutionService` (`Medillina.Core/Interfaces/Services/ICommandExecutionService.cs`):

```csharp
public interface ICommandExecutionService
{
    Task RegisterExecution(string clientIdentifier, string messageId, string actionName);
    Task SetExecutionResult(string clientIdentifier, ExecutionResult result);
    Task<IEnumerable<ExecutionResult>> FetchExecutionsForCharger(string clientIdentifier);
}
```

| Method | Caller | Effect |
| --- | --- | --- |
| `RegisterExecution` | `OcppCallRouter.SubmitAsync` | Inserts a `CommandExecution` row (`Completed = false`, `Error = false`, `StartTime = UtcNow`) |
| `SetExecutionResult` | `OcppCallRouter.SubmitAsync` (on dispatch failure), `IOcppChargerCommand.HandleResponse` / `HandleError` | Updates the row by `MessageId` (`Completed = true`, `Error`, `ErrorMessage`, `EndTime = UtcNow`) |
| `FetchExecutionsForCharger` | callers | Reads back the rows for a charger |

The full state machine and per-handler rules live in [Message Flows → Command audit](./message-flows.md#command-audit).

Implementation pieces:

| File | Role |
| --- | --- |
| `Medinilla.DataTypes/Core/ExecutionResult.cs` | `record ExecutionResult(string MessageId, bool Error, string? ErrorMessage)` |
| `Medinilla.DataAccess/Relational/Models/Audit/CommandExecution.cs` | EF entity; mapped to table `core_command_executions` with an index on `MessageId` |
| `Medinilla.DataAccess/Relational/UnitOfWork/CommandExecutionUnitOfWork.cs` | `CreateExecution` / `FetchExecution` / `FetchExecutions` |
| `Medillina.Core/v1/Services/CommandExecutionService.cs` | `ICommandExecutionService` implementation |

## gRPC mapping

`Communication/MedinillaGrpc.cs` catches it in `SetVariables` and `GetVariables` and returns an `Error` whose `Message` is a serialised OCPP `CALLERROR` with code `GenericError` and a `NotConnected:` prefix. Any other exception falls through to the generic handler.
