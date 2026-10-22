---
name: actions-generator
description: Scaffold a new OCPP 2.0.1 inbound charger action end-to-end (action class + OCPP DTOs). Inbound flow only — CS -> CSMS. No gRPC layer, no audit row.
---

## What this skill produces

Everything needed to add a new **inbound** OCPP action (charger -> CSMS):

1. The OCPP payload `Request` / `Response` DTOs in `Medinilla.DataTypes/Contracts/` (plus any common types under `Contracts/Common/`).
2. The action class in `Medillina.Core/Actions/Ocpp201/<NewAction>Action.cs` that implements `IOcppAction.Execute(...)` and returns a `RpcResult`.
3. Registration in `AddOcppActions` and the action constant in `OcppActionNames`.

The skill does **not** implement the rich business logic of `Execute` — that's left as a stub for the developer. The minimum required is to call `call.As<TRequest>()` to deserialize the payload and to return a populated `RpcResult` (either `Result = call.CreateResult(new TResponse(...))` or `Error = call.CreateErrorResult<TResponse>(code, msg)`), with `ReturnToCS = true` so the router sends the response back to the charger.

Do not explore other already-implemented actions unless you are told the scaffold is wrong. Follow the directions in this skill end-to-end.

## Inbound vs outbound — how this differs from `commands-generator`

`commands-generator` is for **outbound** calls (CSMS -> charger) — it adds a gRPC contract, a gRPC service method, a `MedinillaMapping` partial class, and an `IOcppChargerCommand` that audits through `ICommandExecutionService`. All of that is **outbound-only**.

**This skill is for inbound calls** (charger -> CSMS). There is no gRPC layer, no mapping, and no audit. The flow is:

`Charger -> OcppCallRouter.HandleCall -> actionsFactory.GetAction(action).Execute(call, clientIdentifier) -> RpcResult (Result or Error, ReturnToCS) -> back to charger`

| Outbound (commands) | Inbound (actions, this skill) |
| --- | --- |
| `IOcppChargerCommand` (`src/Medillina.Core/Commands/`) | `IOcppAction` (`src/Medillina.Core/Actions/`) |
| gRPC service method in `MedinillaGrpc.cs` | **None** — the charger drives the call |
| `MedinillaMapping.Map<NewAction>` gRPC -> OCPP | **None** — `call.As<TRequest>()` does it |
| `HandleResponse` / `HandleError` close an audit row | **None** — no `ICommandExecutionService` |
| `RpcResult` returned to the router? No — `HandleResponse` writes to the audit row directly. | **Yes** — `Execute` returns `Task<RpcResult>` containing the response to send back to the charger |

## What goes where

Use `<NewAction>` as the placeholder for the new OCPP action's PascalCase name (e.g. `Heartbeat`, `StatusNotification`, `BootNotification`). The same name is reused in every file below.

| Step | File |
| --- | --- |
| 1. OCPP payload DTOs | `src/Medinilla.DataTypes/Contracts/<NewAction>Request.cs`, `.../<NewAction>Response.cs` |
| 1. (optional) common type | `src/Medinilla.DataTypes/Contracts/Common/<Name>.cs` |
| 2. Action class | `src/Medillina.Core/Actions/Ocpp201/<NewAction>Action.cs` |
| 3a. Action constant | `src/Medinilla.Infrastructure/WAMP/OcppActionNames.cs` |
| 3b. DI registration | `src/Medillina.Core/ServiceExtensions.cs` (`AddOcppActions`) |

## Interface

`IOcppAction` (`src/Medillina.Core/Actions/IOcppAction.cs:6`):

```csharp
public interface IOcppAction
{
    string ActionName { get; }
    Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier);
}
```

- `ActionName` must match the OCPP-J action string (e.g. `"Heartbeat"`). The router uses it to look up the right action via `IOcppActionsFactory.GetAction(string)`.
- `Execute` returns `RpcResult` (`src/Medinilla.Infrastructure/WAMP/RpcResult.cs:3`): set `Result` for a successful OCPP reply, `Error` for an OCPP-level error (e.g. `NotSupported`, `InternalError`), and `ReturnToCS = true` so the reply is sent back to the charger (leave `false` only if you explicitly don't want to reply — almost always `true`).

Use the helpers on `OcppCallRequest` (`src/Medinilla.Infrastructure/WAMP/OcppCallRequest.cs:20`):

- `call.As<TRequest>()` — deserializes `call.Payload` into the typed request. Throws on failure.
- `call.CreateResult(response)` — builds an `OcppCallResult` carrying the response payload.
- `call.CreateErrorResult<TResponse>(errorCode, errorDescription, details?)` — builds an `OcppCallError`.

## Architecture (inbound flow)

`Charger (OCPP-J CALL) -> WAMP/Router -> OcppMessageParser.ParseCall -> OcppCallRequest -> OcppCallRouter.HandleCall -> actionsFactory.GetAction(action) -> IOcppAction.Execute(call, clientIdentifier) -> RpcResult (Result | Error, ReturnToCS) -> WebSocket reply`

- `OcppCallRouter.HandleCall` (`src/Medillina.Core/v1/OcppCallRouter.cs:75`) looks the action up via `IOcppActionsFactory.GetAction(ocppCall.Action)`, validates routing (boot notifications are exempt, others require the station to be registered), and dispatches `Execute(call, clientIdentifier)`.
- `OcppActionsFactory` (`src/Medillina.Core/Actions/OcppActionsFactory.cs:5`) keys its registry by `ActionName` — each `ActionName` has exactly one action.
- `OcppCallRequest.As<T>()` uses camelCase-insensitive deserialization with `DottedEnumJsonConverter` (`OcppCallRequest.cs:22`), so the OCPP JSON wire format ("dotted" enums like `ChargingStateCharging` / `MeasurandEnergyActiveImportRegister`) round-trips straight into the typed DTOs.

---

## Step 1 — OCPP payload DTOs

Wire payload for the OCPP message itself (what `OcppCallRequest.Payload` is serialized from/to). One file per DTO.

`src/Medinilla.DataTypes/Contracts/<NewAction>Request.cs`:

```csharp
using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public sealed class <NewAction>Request
{
    // OCPP-schema-shaped fields go here
}
```

`src/Medinilla.DataTypes/Contracts/<NewAction>Response.cs`:

```csharp
using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public sealed class <NewAction>Response
{
    // OCPP-schema-shaped fields go here
}
```

**DTO generation rule — read the JSON schema, do not invent.** Populate the `Request` and `Response` property types from the action's OCPP JSON schema in `docs/OCPP-2.0.1_part3_JSON_schemas/OCPP-2.0.1_part3_JSON_schemas/<NewAction>Request.json` and `.../<NewAction>Response.json`. Read both files **before** writing any DTO — the schema is the source of truth for field names, types, optionality, enums, and references to common types. Reuse existing common types under `Contracts/Common/` wherever the schema's `$ref` points to them — don't redefine them. If the schema references a common type that doesn't exist yet, add it under `src/Medinilla.DataTypes/Contracts/Common/`, again shaped by the schema. Match the existing convention in this folder: most existing action DTOs are `sealed`; the command-generator skill targets non-`sealed` for commands, so keep actions `sealed` unless the existing pattern in `Contracts/` says otherwise.

---

## Step 2 — Action skeleton

`src/Medillina.Core/Actions/Ocpp201/<NewAction>Action.cs`. The class implements `IOcppAction`, deserializes the inbound payload with `call.As<TRequest>()`, and returns a populated `RpcResult`. The `Execute` body is left as a stub — the developer fills in the actual business logic (DB writes, lookups, authorization checks, etc.) and the response shape.

Match the existing per-action style: `HeartbeatAction` is `internal class`, `BootNotificationAction` is `public sealed class` with primary-constructor DI, `AuthorizeAction` is `public sealed class` with primary-constructor DI. Pick whichever style is closest to the new action's role:

- A trivial ack-only action (like `Heartbeat`) can use `internal class` with a `_logger` field.
- An action that needs DB / auth / services should use `public sealed class` with primary-constructor DI like `BootNotificationAction` / `AuthorizeAction` / `StatusNotificationAction`.

Minimum viable skeleton for an action that needs `ILogger`:

```csharp
using Medinilla.DataTypes.Contracts;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class <NewAction>Action(ILogger<<NewAction>Action> logger) : IOcppAction
{
    public string ActionName => OcppActionNames.<NewAction>;

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var request = call.As<<NewAction>Request>();
        logger.LogInformation("{ci}: Received <NewAction> request.", clientIdentifier);

        // TODO: implement business logic (DB lookup, persistence, validation, etc.)

        return new RpcResult
        {
            Result = call.CreateResult(new <NewAction>Response
            {
                // populate OCPP response fields here
            }),
            Error = null,
            ReturnToCS = true,
        };
    }
}
```

Minimum viable skeleton for a trivial ack-only action:

```csharp
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Actions.Ocpp201;

internal class <NewAction>Action : IOcppAction
{
    private readonly ILogger<<NewAction>Action> _logger;

    public <NewAction>Action(ILogger<<NewAction>Action> logger)
        => _logger = logger;

    public string ActionName => OcppActionNames.<NewAction>;

    public Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        return Task.FromResult(new RpcResult
        {
            Result = call.CreateResult(new <NewAction>Response()),
            Error = null,
            ReturnToCS = true,
        });
    }
}
```

`Execute` rules:

- **Always return a populated `RpcResult`** with `ReturnToCS = true` for the success path; the OCPP layer expects a reply for every `CALL`.
- **Set `Error`** instead of `Result` when the action can't process the call (charger sent something invalid, station not found, downstream service failed, etc.). Use `call.CreateErrorResult<TResponse>(OcppCallError.ErrorCodes.<Code>, message)` and leave `Result = null`. The standard codes used by existing actions are `GenericError`, `NotImplemented`, `NotSupported`, `InternalError`, `OccurrenceConstraintViolation` — see `src/Medinilla.Infrastructure/WAMP/OcppCallError.cs`.
- **Log meaningful context** (client id, parsed fields) before mutating state. Existing actions log at `Information` for happy-path receipt and `Error` / `Warning` for failures.
- **Throw only for unexpected exceptions.** The router's `HandleCall` catches `Exception` and returns `OcppCallError.InternalError(ocppCall.MessageId)`. So if you want a specific error code / description back to the charger, return it explicitly rather than throwing.
- Do **not** inject `ICommandExecutionService` — actions don't audit. That's a commands-only concern.

---

## Step 3 — Registration

### 3a. Action constant — `src/Medinilla.Infrastructure/WAMP/OcppActionNames.cs`

```csharp
public const string <NewAction> = "<NewAction>";
```

Keep the value identical to the OCPP-J action string and to the action's `ActionName`. The router keys the lookup off this constant.

### 3b. DI — `src/Medillina.Core/ServiceExtensions.cs`

Add to `AddOcppActions`:

```csharp
private static void AddOcppActions(IServiceCollection services)
{
    services.AddScoped<IOcppAction, <NewAction>Action>();
    // ...existing registrations...
}
```

`OcppActionsFactory` enumerates `IEnumerable<IOcppAction>` (`src/Medillina.Core/Actions/OcppActionsFactory.cs:11`), so adding the registration here is sufficient for the router to find the action by name — no factory changes needed.