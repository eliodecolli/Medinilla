---
name: commands-generator
description: Scaffold a new OCPP 2.0.1 charger command end-to-end (command, OCPP DTOs, gRPC contract, gRPC service method, gRPC->OCPP mapping).
---

## What this skill produces
Everything needed to add a new outbound OCPP action (CSMS -> charger):

1. The gRPC contract message(s) and `rpc` declaration in `Medinilla.Core.gRPC`.
2. The gRPC service method implementation in `Medinilla.Core.Service/Communication/MedinillaGrpc.cs`.
3. The `gRPC -> OCPP payload` mapping as a partial `MedinillaMapping` class in `Medinilla.Core.Service/Communication/Mapping/<CommandName>Mapping.cs`.
4. The OCPP payload `Request`/`Response` DTOs in `Medinilla.DataTypes/Contracts/` (plus any common types under `Contracts/Common/`).
5. The command class in `Medillina.Core/Commands/Ocpp201/` (response handlers only — no mapping, no call-building).
6. Registration in `AddOcppChargerCommands` and the action constant in `OcppActionNames`.

The skill does **not** implement the rich `HandleResponse` / `HandleError` logic — those are left as empty stubs for the developer to fill in. However, every command **must** call `executionService.SetExecutionResult(...)` so the audit row is closed. The command does **no mapping**: a single `OcppCallRequest` is built in the gRPC service method and handed to the router.

## CSMS -> charger audit (mandatory)

Every outbound OCPP call (CSMS -> charger) is audited end-to-end via `ICommandExecutionService`:

- When the gRPC method submits the call to the router and the router successfully dispatches it, the router calls `executionService.RegisterExecution(clientIdentifier, messageId, action)` (see `src/Medillina.Core/v1/OcppCallRouter.cs:156`). This writes a `CommandExecution` row (`Completed = false`, `Error = false`) so callers can later correlate the audit by message id.
- When the charger replies, the matching `IOcppChargerCommand.HandleResponse` / `HandleError` is invoked. That handler **must** call `executionService.SetExecutionResult(clientIdentifier, new ExecutionResult(messageId, actionName, error, errorMessage))` to close the row (`Completed = true`, `Error = error`, `EndTime = UtcNow`).

`ExecutionResult` (`src/Medinilla.DataTypes/Core/ExecutionResult.cs`) is `record ExecutionResult(string MessageId, string ActionName, bool Error, string? ErrorMessage)`. The `ActionName` should be the command's own `Action` property (e.g. `OcppActionNames.<NewAction>`), not a string literal.

If the handler doesn't call `SetExecutionResult` the audit row stays open forever — so treat it as required, not optional.

Do not explore other already-implemented commands unless you are told the scaffold is wrong. Follow the directions in this skill end-to-end.

## What goes where
Use `<NewAction>` as the placeholder for the new OCPP action's PascalCase name (e.g. `GetVariables`, `Reset`, `UnlockConnector`). The same name is reused in every file below.

| Step | File |
| --- | --- |
| 1. gRPC `rpc` | `src/Medinilla.Core.gRPC/Service/OcppService.proto` |
| 1. gRPC request/response messages | `src/Medinilla.Core.gRPC/Service/Types.proto` |
| 1. (optional) shared sub-message | `src/Medinilla.Core.gRPC/Contracts/<Name>.proto` + register in `src/Medinilla.Core.gRPC/Medinilla.Core.gRPC.csproj` |
| 2. Service method | `src/Medinilla.Core.Service/Communication/MedinillaGrpc.cs` |
| 3. Mapping | `src/Medinilla.Core.Service/Communication/Mapping/<NewAction>Mapping.cs` |
| 4. OCPP payload DTOs | `src/Medinilla.DataTypes/Contracts/<NewAction>Request.cs`, `.../<NewAction>Response.cs` |
| 4. (optional) common type | `src/Medinilla.DataTypes/Contracts/Common/<Name>.cs` |
| 5. Command class | `src/Medillina.Core/Commands/Ocpp201/<NewAction>Command.cs` |
| 6a. Action constant | `src/Medinilla.Infrastructure/WAMP/OcppActionNames.cs` |
| 6b. DI registration | `src/Medillina.Core/ServiceExtensions.cs` (`AddOcppChargerCommands`) |

## Interface
`IOcppChargerCommand` (`src/Medillina.Core/Commands/IOcppChargerCommand.cs:6`):

```csharp
public interface IOcppChargerCommand
{
    string Action { get; }
    Task HandleResponse(string clientIdentifier, OcppCallResult result, ICommandExecutionService executionService);
    Task HandleError(string clientIdentifier, OcppCallError error, ICommandExecutionService executionService);
}
```

There is **no `BuildCall`** and **no payload mapping** on the command. Commands are thin: they only react to the response and **must** call `executionService.SetExecutionResult(...)` to close the audit row (see [CSMS -> charger audit](#csms---charger-audit-mandatory)).

## Architecture (outbound flow)
`gRPC caller -> MedinillaGrpc.<NewAction>(request) -> MedinillaMapping.Map<NewAction>(request) -> OcppCallRequest -> IOcppCallRouter.SubmitAsync -> RoutingTable (msgId -> action) -> Charger -> ... -> IOcppCallRouter.RouteOcppCall -> RoutingTable lookup -> IOcppChargerCommand.HandleResponse/HandleError`

- The gRPC service method generates a fresh `MessageId`, calls the mapping, serializes the payload (camelCase, enums-as-strings, omit nulls), builds an `OcppCallRequest(messageId, OcppActionNames.<NewAction>, payloadJson)`, and calls `IOcppCallRouter.SubmitAsync(clientIdentifier, ocppRequest)`.
- `OcppCallRouter.HandleCommandResponse` (`src/Medillina.Core/v1/OcppCallRouter.cs:124`) looks up the inbound `MessageId` in the routing table, finds the `Action`, asks `IOcppChargerCommandFactory.GetCommand(action)` for the matching `IOcppChargerCommand`, removes the entry, and dispatches `HandleResponse(result)` or `HandleError(error)` on it.
- `OcppChargerCommandsFactory` (`src/Medillina.Core/Commands/OcppChargerCommandsFactory.cs:5`) keys its registry by the command's `Action` string — each `Action` has exactly one command.

---

## Step 1 — gRPC contract

### 1a. `OcppService.proto` (`src/Medinilla.Core.gRPC/Service/OcppService.proto`)
Add a new `rpc` to the existing `OcppService` service block:

```proto
syntax = "proto3";

option csharp_namespace = "Medinilla.Core.gRPC.Service";

import "Service/Types.proto";

package medinilla.core.grpc;

service OcppService {
    rpc <NewAction> (<NewAction>Request) returns (<NewAction>Response) {}
}
```

### 1b. `Types.proto` (`src/Medinilla.Core.gRPC/Service/Types.proto`)
Add the request and response messages. The response always wraps `Error`:

```proto
syntax = "proto3";

option csharp_namespace = "Medinilla.Core.gRPC.Service";

package medinilla.core.grpc;

message Error {
    bool has_error = 1;
    optional string message = 2;
}

message <NewAction>Request {
    string client_identifier = 1;
    // action-specific gRPC fields go here
}

message <NewAction>Response {
    Error error = 1;
}
```

If the request needs a shared sub-message (a single item reused across the message), put it in `src/Medinilla.Core.gRPC/Contracts/<Name>.proto`, then add an `import "<Name>.proto";` to `Types.proto` and reference it.

### 1c. `Medinilla.Core.gRPC.csproj` (only if you added a new proto under `Contracts/`)
The csproj does **not** glob — add an explicit `<Protobuf Include="Contracts\<Name>.proto" />` line under `<ItemGroup>`.

---

## Step 2 — gRPC service method
Add the override to `src/Medinilla.Core.Service/Communication/MedinillaGrpc.cs` (alongside the other `OcppServiceBase` overrides). The method:

- Logs the request with `an` / `mi` / `ci` (action / messageId / clientIdentifier).
- Generates a fresh `messageId`.
- Calls `MedinillaMapping.Map<NewAction>(request)`.
- Serializes via `OcppPayloadSerializer.SerializePayload(payload)` (it already sets camelCase + enum-as-string + omit-null).
- Builds an `OcppCallRequest(messageId, OcppActionNames.<NewAction>, payloadJson)` and hands it to the private `SubmitAsync` helper (see below).
- Catches `ChargerNotConnectedException` separately — that's the only way to surface "charger is offline" as a real OCPP `CALLERROR` to the caller; the generic `catch (Exception)` is the fallback.

```csharp
public override async Task<<NewAction>Response> <NewAction>(<NewAction>Request request, ServerCallContext context)
{
    try
    {
        var messageId = Guid.NewGuid().ToString();
        log.LogInformation("Request: {an} msgId={mi} ci={ci}",
            OcppActionNames.<NewAction>,
            messageId,
            request.ClientIdentifier);

        var payload = MedinillaMapping.Map<NewAction>(request);

        var ocppRequest = new OcppCallRequest(messageId, OcppActionNames.<NewAction>, OcppPayloadSerializer.SerializePayload(payload));
        await SubmitAsync(request.ClientIdentifier, ocppRequest);
        return new <NewAction>Response()
        {
            Error = new Error() { HasError = false },
        };
    }
    catch (ChargerNotConnectedException e)
    {
        return new <NewAction>Response()
        {
            Error = NotConnectedError(e, OcppActionNames.<NewAction>)
        };
    }
    catch (Exception e)
    {
        log.LogError("{ci}: {an}: Error: {msg}", request.ClientIdentifier, OcppActionNames.<NewAction>, e.Message);
        return new <NewAction>Response()
        {
            Error = new Error() { HasError = true, Message = e.Message },
        };
    }
}
```

**`SubmitAsync` helper** — already defined at the bottom of `MedinillaGrpc` from a previous refactor. It owns the scope/router lifetime so the per-method body stays focused on building the OCPP call. The helper **must be `async`/`await`** (not return the Task directly) — otherwise `using var scope` would dispose before the router's own awaits resolve.

```csharp
private async Task SubmitAsync(string clientIdentifier, OcppCallRequest request)
{
    using var scope = serviceProvider.CreateScope();
    var router = scope.ServiceProvider.GetRequiredService<IOcppCallRouter>();
    await router.SubmitAsync(clientIdentifier, request);
}
```

The required usings already in the file: `Grpc.Core`, `Medinilla.Core.gRPC.Contracts` (only if the request uses shared sub-messages), `Medinilla.Core.gRPC.Service`, `Medinilla.Core.Interfaces`, `Medinilla.Core.Service.Communication.Mapping`, `Medinilla.Infrastructure`, `Medinilla.Infrastructure.WAMP`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging`, `System.Text` (for `Encoding.UTF8`). Don't add duplicates — `System.Text.Json` and `System.Text.Json.Serialization` are **not** needed because the helper uses `OcppPayloadSerializer`.

---

## Step 3 — Mapping (gRPC -> OCPP payload)
Create `src/Medinilla.Core.Service/Communication/Mapping/<NewAction>Mapping.cs`. One static method per command, named `Map<NewAction>`:

```csharp
using Medinilla.DataTypes.Contracts;

namespace Medinilla.Core.Service.Communication.Mapping;

public partial class MedinillaMapping
{
    public static <NewAction>Request Map<NewAction>(Medinilla.Core.gRPC.Service.<NewAction>Request request)
    {
        return new <NewAction>Request
        {
            // map gRPC fields -> OCPP payload DTO fields here
        };
    }
}
```

Rules:

- Reuse existing common types under `Medinilla.DataTypes.Contracts.Common/` — don't redefine them.
- Be tolerant of null collections (`?.Where(x => x is not null).Select(...).ToList() ?? []`).
- For enum strings, use `Enum.TryParse<TEnum>(s, out var v) ? v : null` (or a sensible default).

---

## Step 4 — OCPP payload DTOs
Wire payload for the OCPP message itself (what `OcppCallRequest.Payload` is serialized from/to). One file per DTO:

`src/Medinilla.DataTypes/Contracts/<NewAction>Request.cs`:

```csharp
using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public class <NewAction>Request
{
    // OCPP-schema-shaped fields go here
}
```

`src/Medinilla.DataTypes/Contracts/<NewAction>Response.cs`:

```csharp
using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public class <NewAction>Response
{
    // OCPP-schema-shaped fields go here
}
```

**DTO generation rule — read the JSON schema, do not invent.** Populate the `Request` and `Response` property types from the action's OCPP JSON schema in `docs/OCPP-2.0.1_part3_JSON_schemas/OCPP-2.0.1_part3_JSON_schemas/<NewAction>Request.json` and `.../<NewAction>Response.json`. Read both files **before** writing any DTO — the schema is the source of truth for field names, types, optionality, enums, and references to common types. Reuse existing common types under `Contracts/Common/` wherever the schema's `$ref` points to them — don't redefine them. If the schema references a common type that doesn't exist yet, add it under `src/Medinilla.DataTypes/Contracts/Common/`, again shaped by the schema.

---

## Step 5 — Command skeleton
`src/Medillina.Core/Commands/Ocpp201/<NewAction>Command.cs`. The signatures take `clientIdentifier` and `ICommandExecutionService` so the handler can close the audit row. Both handlers **must** call `executionService.SetExecutionResult(...)` before returning — otherwise the audit stays open.

```csharp
using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Core;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Commands.Ocpp201;

internal sealed class <NewAction>Command(ILogger<<NewAction>Command> log) : IOcppChargerCommand
{
    public string Action => OcppActionNames.<NewAction>;

    public async Task HandleError(string clientIdentifier, OcppCallError error, ICommandExecutionService executionService)
    {
        log.LogError("(<NewAction>) {mi}: Errored: {err}, Error Details: {errd}, Error Code: {errc}",
            error.MessageId, error.ErrorDescription, error.ErrorDetails ?? "None", error.ErrorCode);

        await executionService.SetExecutionResult(clientIdentifier, new ExecutionResult(error.MessageId, Action, true, error.ErrorDescription));
    }

    public async Task HandleResponse(string clientIdentifier, OcppCallResult result, ICommandExecutionService executionService)
    {
        var response = result.As<<NewAction>Response>();
        if (response is null)
        {
            log.LogError("{mid} could not be deserialized.", result.MessageId);

            await executionService.SetExecutionResult(clientIdentifier,
                new ExecutionResult(result.MessageId, Action, true, "Incoming charger response could not be serialized."));

            return;
        }

        // TODO: react to response (per-status logging, counters, etc.)

        await executionService.SetExecutionResult(clientIdentifier,
            new ExecutionResult(result.MessageId, Action, /* error: */ false, /* errorMessage: */ null));
    }
}
```

Audit rules for the success path:

- If deserialization succeeds and every item is `Accepted`: `Error = false`, `ErrorMessage = null`.
- If any item is not `Accepted` (charger refused, unknown component / variable, or any status outside the OCPP 2.0.1 spec for the action): `Error = true`, and pass a human-readable summary of the failing items as `ErrorMessage`. Mirror the pattern used by `SetVariablesCommand`/`GetVariablesCommand` — collect the failing items into a `StringBuilder` and pass `sb.Length > 0 ? sb.ToString() : null`. Even "unknown" statuses (`UnknownComponent`, `UnknownVariable`, or anything outside the spec) count as failures: from a CSMS perspective, asking the charger for something it doesn't know about is a real configuration / support problem worth surfacing.
- If deserialization fails: `Error = true`, `ErrorMessage = "Incoming charger response could not be serialized."` (matches the existing commands).

For `HandleError`: the OCPP layer already failed, so always `Error = true`, `ErrorMessage = error.ErrorDescription`.

---

## Step 6 — Registration

### 6a. Action constant — `src/Medinilla.Infrastructure/WAMP/OcppActionNames.cs`

```csharp
public const string <NewAction> = "<NewAction>";
```

### 6b. DI — `src/Medillina.Core/ServiceExtensions.cs`

```csharp
private static void AddOcppChargerCommands(IServiceCollection services)
{
    services.AddScoped<IOcppChargerCommand, <NewAction>Command>();
    // ...existing registrations...
}
```