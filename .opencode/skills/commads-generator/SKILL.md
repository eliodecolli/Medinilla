---
name: commands-generator
description: Scaffold a new OcppChargerCommand instance (skeleton + DTOs only).
---

## Summary
Scaffolds the skeleton of a new `OcppChargerCommand` and its associated DTOs. The skill produces:

1. The command class (interface implementation, no handler logic).
2. The `Request`/`Response` DTOs in `Medinilla.DataTypes/Contracts/`, populated from the action's OCPP JSON schema.

The skill does **not** implement `HandleResponse` / `HandleError` logic — those are left as empty stubs for the developer to fill in.

OCPP Charger Commands are part of the "Core" component. They handle data flowing **from the CSMS to the charger** (the outbound direction). A command:

1. Holds the `Action` name it owns.
2. Receives the `OcppCallResult` (or `OcppCallError`) when the charger replies, asynchronously.

CSMS <-> Charger communication is asynchronous: the response comes back at a later time. Outbound correlation is handled by the **routing table** (see Architecture), so commands do **not** build calls or manage `MessageId`s themselves.

When creating a new Command:

1. Implement `IOcppChargerCommand` (`src/Medillina.Core/Commands/IOcppChargerCommand.cs:6`).
2. Place the implementation under `src/Medillina.Core/Commands/Ocpp201/` (or `Ocpp16/` for OCPP 1.6 actions).
3. Add the action constant to `OcppActionNames` (`src/Medinilla.Infrastructure/WAMP/OcppActionNames.cs`) if it isn't there yet.
4. Register the command in `AddOcppChargerCommands` (`src/Medillina.Core/ServiceExtensions.cs:29`).

## Interface
`IOcppChargerCommand` has only three members (`src/Medillina.Core/Commands/IOcppChargerCommand.cs:6`):

```csharp
public interface IOcppChargerCommand
{
    string Action { get; }
    Task HandleResponse(OcppCallResult result);
    Task HandleError(OcppCallError error);
}
```

There is **no `BuildCall`** and **no payload mapping** on the command — see Architecture. Commands are thin: they only react to the response.

## Architecture
Outbound flow:

`Caller -> IOcppCallRouter.SubmitAsync(OcppCallRequest) -> RoutingTable (msgId -> action) -> Charger -> ... -> IOcppCallRouter.RouteOcppCall -> RoutingTable lookup -> IOcppChargerCommand.HandleResponse/HandleError`

### 1. Caller builds the request
Whoever wants to talk to the charger (e.g. an API endpoint, a background job) constructs an `OcppCallRequest` (`src/Medinilla.Infrastructure/WAMP/OcppCallRequest.cs`) with a fresh `MessageId`, the OCPP `Action` string, and the JSON payload, then calls `IOcppCallRouter.SubmitAsync(clientIdentifier, request, ct)` (`src/Medillina.Core/Interfaces/IOcppCallRouter.cs:10`).

The router:

- Registers `request.MessageId -> request.Action` in the outbound routing table (`BaseOcppRoutingTable`).
- Serializes the request and hands it to the wired `submitCall` delegate (the transport).

### 2. Response arrives
`OcppCallRouter.HandleCommandResponse` (`src/Medillina.Core/v1/OcppCallRouter.cs:124`) looks up the inbound `MessageId` in the routing table, finds the `Action`, asks `IOcppChargerCommandFactory.GetCommand(action)` for the matching `IOcppChargerCommand`, removes the entry, and dispatches `HandleResponse(result)` or `HandleError(error)` on it.

`OcppChargerCommandsFactory` (`src/Medillina.Core/Commands/OcppChargerCommandsFactory.cs:5`) keys its registry by the command's `Action` string, so each `Action` may only have **one** command registered.

## DTOs
Two DTOs are involved — they are **separate** concerns and live in different namespaces:

### User request DTO — `Medinilla.DataTypes.Core.CommandRequests`
Carries what the caller (typically an HTTP API) hands to the CSMS. Extends `BaseCommandRequest`. Example skeleton: `UserSetVariablesRequest` (`src/Medinilla.DataTypes/Core/CommandRequests/UserSetVariablesRequest.cs:3`).

```csharp
public sealed class UserSetVariablesRequest : BaseCommandRequest
{
    // user-supplied fields go here
}
```

If the user request type for the new command doesn't exist yet, add it next to `BaseCommandRequest.cs`.

### OCPP payload DTO — `Medinilla.DataTypes.Contracts`
The wire payload for the OCPP message itself (what `OcppCallRequest.Payload` is serialized from/to). Example: `SetVariablesRequest` / `SetVariablesResponse` (`src/Medinilla.DataTypes/Contracts/`), and the shared common types under `src/Medinilla.DataTypes/Contracts/Common/` (e.g. `Variable`, `Component`, `SetVariableData`, `SetVariableResult`).

The mapping from user DTO -> OCPP payload DTO happens **in the caller**, before `SubmitAsync`, not inside the command.

**DTO generation rule:** When scaffolding a new action, populate the `Request` and `Response` property types from the action's OCPP JSON schema (OCPP 1.6 / 2.0.1 spec). Reuse existing common types under `Contracts/Common/` wherever the schema references them — don't redefine them.

## Skeleton

```csharp
using Medinilla.Infrastructure.WAMP;

namespace Medinilla.Core.Commands.Ocpp201;

internal sealed class NewCommand : IOcppChargerCommand
{
    public string Action => OcppActionNames.NewAction;

    public Task HandleResponse(OcppCallResult result)
    {
        // TODO: deserialize result.Payload and act on it
        return Task.CompletedTask;
    }

    public Task HandleError(OcppCallError error)
    {
        // TODO: react to OCPP error
        return Task.CompletedTask;
    }
}
```

Then in `src/Medillina.Core/ServiceExtensions.cs`:

```csharp
private static void AddOcppChargerCommands(IServiceCollection services)
{
    services.AddScoped<IOcppChargerCommand, NewCommand>();
    // ...
}
```

And in `src/Medinilla.Infrastructure/WAMP/OcppActionNames.cs`:

```csharp
public const string NewAction = "NewAction";
```

## Reference
Existing (WIP) example: `src/Medillina.Core/Commands/Ocpp201/SetVariablesCommand.cs`.