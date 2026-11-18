---
name: webhook-event-generator
description: Add or change a Medinilla webhook event end-to-end. Use when adding a new `EventType`, protobuf payload, completion snapshot, pure mapper, service-owned emission point, or when changing the data carried by existing webhook events. Covers producer ownership, account scoping, N+1 avoidance, failure isolation, tests, and consumer documentation.
---

# Webhook Event Generator

Use this skill when adding a new outbound webhook event or changing an existing event's
payload or producer.

The webhook system publishes **completed application milestones** from Core to a Redis
Stream, which `Medinilla.Webhooks.Service` fans out to configured HTTP endpoints. An event
is not a copy of an OCPP message and is not emitted from the transport layer.

## 1. Current architecture

```text
OCPP action (protocol adapter)
    -> owning application service processing entry point
        -> validate / resolve state / persist / commit
        -> capture completion time + materialized snapshot
        -> pure mapper
        -> IApplicationEventEmitter
            -> MedinillaHttpEvent protobuf wrapper
            -> IChargerEventPublisher
            -> Redis stream
            -> Medinilla.Webhooks.Service -> HTTP subscribers
```

Ownership rules:

- The **application service** owns event selection and emission. It knows whether the
  operation actually completed.
- Actions only deserialize OCPP requests, await one service call, and translate the
  outcome into `CALL_RESULT` / `CALL_ERROR`.
- `OcppCallRouter` is dispatch/routing only. Do not add an event switch, mapper, or
  publication hook there.
- `IApplicationEventEmitter` owns wrapping, event ID generation, bounded publication,
  logging/metrics, and mapping/publication failure isolation. It must remain unaware of
  OCPP action names and database state.
- Exactly one service owns each event. Never emit the same completion from both an action
  and its service.

Current producers:

| Event type | Payload | Owner |
|---|---|---|
| `charger.bootup` | `BootupEvent` | `ChargingStationBooting` |
| `connector.status` | `StatusNotificationEvent` | `StatusNotificationService` |
| `transaction.started` / `transaction.ended` | `TransactionLifecycleEvent` | `TransactionService` |
| `charger.registration.completed` | `ChargerRegistrationCompletedEvent` | `ChargerConfigService` |

Do not add a new event for something the existing events already represent. Extend the
existing payload only when the new data belongs to the same milestone.

## 2. Decide the event before writing code

Answer these before editing files:

1. **What completed application operation does this represent?**
   An event is a milestone, not an OCPP message receipt and not an intermediate state.
2. **Which service owns that operation?**
   If no service owns it, extract or create the service processing entry point first.
3. **What is the successful commit boundary?**
   Emit after the state change has committed, never before.
4. **What is the stable `EventType` string?**
   Use lowercase, dot-separated names such as `charger.bootup` or
   `transaction.ended`. It is a consumer-facing contract.
5. **What is the payload?**
   Include only fields consumers need. Include required identity fields
   (`charger_id`, `account_id`) and operation-specific values.
6. **What must not emit?**
   Failures, rejected operations, duplicates/replays, intermediate updates, and operations
   that did not persist.

## 3. File map

Use `<Name>` as the PascalCase payload name (`BootupEvent`, `FaultEvent`, etc.) and
`<event_type>` as the stable discriminator.

| Step | File |
|---|---|
| 1. Protobuf payload | `src/Medinilla.Core.SharedContracts/events/<Name>.proto` |
| 2. Protobuf registration | `src/Medinilla.Core.SharedContracts/Medinilla.Core.SharedContracts.csproj` |
| 3. Event type constant | `src/Medinilla.Core.SharedContracts/events/WebhookEventTypes.cs` |
| 4. Materialized snapshot | `src/Medillina.Core/Events/CompletionSnapshots.cs` |
| 5. Pure mapper | `src/Medillina.Core/Events/EventMappers.cs` |
| 6. Mapper registration | `src/Medillina.Core/ServiceExtensions.cs` |
| 7. Emission | The owning service under `src/Medillina.Core/v1/Services/` |
| 8. Tests | `src/Medinilla.Core.Tests/` |
| 9. Consumer docs | `docs/webhook-implementation-plan.md`, `src/Medinilla.Webhooks.Service/README.md` |

Do not put event contracts in `Medinilla.DataTypes`. The event protobufs and the
`MedinillaHttpEvent` wrapper belong to `Medinilla.Core.SharedContracts`.

## 4. Protobuf contract

Create `events/<Name>.proto`:

```proto
syntax = "proto3";

option csharp_namespace = "Medinilla.Core.SharedContracts.Events";

message <Name> {
  string charger_id = 1;
  string account_id = 2;
  // Event-specific fields.
}
```

If the payload carries a timestamp, use protobuf's well-known timestamp:

```proto
import "google/protobuf/timestamp.proto";
```

Then map it with `Timestamp.FromDateTime(utcValue)`.

Contract rules:

- `charger_id` is the Medinilla client identifier, not a manufacturer serial number.
- `account_id` is the owning account UUID in canonical `D` format.
- Use `optional` when absence is meaningful.
- Use `int32` for OCPP EVSE/connector identifiers.
- Use `string` for UUIDs in wire contracts.
- Do not expose secrets, credentials, tracked entities, or unfiltered database dumps.
- Do not mirror an OCPP DTO wholesale. Select the processed values consumers need.
- Use the next unused field number. Do not renumber existing fields. If a field was
  removed from a published contract, reserve its number and name instead of reusing them.
- Add the proto explicitly to the SharedContracts project; the event protos are not
  globbed.

```xml
<Protobuf Include="events\<Name>.proto" GrpcServices="None" />
```

## 5. Event type constant

Add the discriminator in `WebhookEventTypes.cs`:

```csharp
/// <summary>Payload: <see cref="<Name>"/>.</summary>
public const string <ConstantName> = "<event_type>";
```

Keep the XML doc comment. Consumers select the payload parser from this value.

## 6. Materialized snapshot

Add a plain record to `CompletionSnapshots.cs`. The snapshot must be fully materialized
before mapping and must not contain EF entities, navigation properties, or deferred
queries.

```csharp
public sealed record <Name>Snapshot(
    string ChargerId,
    Guid AccountId,
    // Event-specific materialized values.
);
```

Use primitives, enums, and simple immutable collections. If a value is optional and not
already available from processing, omit it rather than adding an enrichment query.

## 7. Pure mapper

Add a mapper class to `EventMappers.cs`, following the existing style:

```csharp
public sealed class <Name>EventMapper
{
    public <Name> Map(<Name>Snapshot snapshot)
    {
        return new <Name>
        {
            ChargerId = snapshot.ChargerId,
            AccountId = snapshot.AccountId.ToString("D"),
            // Copy event-specific values.
        };
    }
}
```

Mapper rules:

- Synchronous method, no `Task`.
- No `DbContext`, repository, unit of work, HTTP client, Redis, logger, or service
  dependency.
- No clock reads. All timestamps come from the snapshot.
- No database queries, lazy loading, or `IQueryable` enumeration.
- No tracked entities. The caller passes primitives/snapshots only.
- Preserve optional-field absence with protobuf presence; do not fabricate values.
- Map repeated values with a deterministic order when order is consumer-visible.

Register the mapper in `AddMedinillaServices`:

```csharp
serviceCollection.AddScoped<<Name>EventMapper>();
```

`IApplicationEventEmitter` is already registered.

## 8. Service-owned emission

Emit from the owning service at the successful completion point.

```csharp
await context.SaveChangesAsync();

var snapshot = new <Name>Snapshot(
    clientIdentifier,
    chargingStation.AccountId,
    // Values already used by the operation.
);

var completedAtUtc = DateTimeOffset.UtcNow;
await eventEmitter.TryEmitAsync(
    WebhookEventTypes.<ConstantName>,
    completedAtUtc,
    () => eventMapper.Map(snapshot));
```

Rules:

- Emit **after** the operation's commit.
- Capture `completedAtUtc` immediately after the commit, not after optional queries.
- Keep mapper invocation inside the synchronous payload factory.
- Await publication. Do not use `Task.Run`, fire-and-forget, or request-scoped work after
  the scope ends.
- Do not add try/catch that changes the operation outcome. The emitter isolates mapping
  and transport failures, so a webhook failure must not reject a committed operation.
- Do not return `RpcResult` from the service or pass `OcppCallRequest` into it.
- Do not emit for failures, rejected operations, duplicates, replayed sequence numbers,
  or intermediate updates unless the event contract explicitly represents them.
- Emit one event per completed milestone. Do not create a generic "something happened"
  event with an action-name switch.

### Account scoping without N+1

Every event that concerns a charger must carry `account_id`.

Get it from the `ChargingStation` already loaded by the operation:

```csharp
var chargingStation = await context.GetChargingStation(clientIdentifier);
var accountId = chargingStation.AccountId;
```

Do **not**:

- Query `Account` separately per event.
- Add an account lookup just to fill the payload.
- Put an account lookup in the mapper.
- Iterate chargers/connectors and query the account for each row.

If the operation creates a new charger, use the account ID assigned during that same
operation. If the account ID is genuinely unavailable without another read, stop and
reassess the operation boundary rather than hiding an N+1 in event code.

### Optional fields and snapshot queries

Prefer values already produced by processing. Never add a per-event enrichment query just
to populate an optional field. If a snapshot query is genuinely required:

- It belongs explicitly to the owning service.
- Keep it bounded and cancellation-aware.
- Do not start it before the completion timestamp if it is not part of the operation.
- Do not abandon a running query while disposing or reusing its `DbContext`.
- Do not move the query into the mapper or emitter.

Do not replace a complete multi-row snapshot with only the last processed chunk merely to
avoid a query.

## 9. Tests

Read the `tests` skill and assess each candidate before writing test code.

Recommended minimum coverage:

| Scenario | Why | Decision |
|---|---|---|
| Mapper transforms the snapshot, including `account_id` and optional presence | Protects the consumer contract | Include |
| Owning service emits after a successful commit | Proves the operation boundary and ownership | Include |
| Failure/duplicate/intermediate path does not emit | Prevents false milestones | Include when the branch exists |
| Event mapping/transport failure leaves processing successful | Proves failure isolation | Reuse existing emitter coverage; add one service case if this producer is the first |
| Every DTO field setter or entity property | No application behavior | Skip |
| EF metadata / table/column mapping | Prohibited model metadata test | Skip |

Use `FakeApplicationEventEmitter` for service emission tests:

```csharp
var emitted = Assert.Single(emitter.Events);
Assert.Equal(WebhookEventTypes.<ConstantName>, emitted.EventType);
var payload = Assert.IsType<<Name>>(emitted.Payload);
Assert.Equal(accountId.ToString("D"), payload.AccountId);
```

For failure isolation, use a real `ApplicationEventEmitter` with a throwing
`IChargerEventPublisher` and assert the service operation still succeeds.

Do not add live Redis, real HTTP, or shared-database tests for a producer change.

## 10. Documentation

Update documentation that describes the current contract and producer inventory:

- `docs/webhook-implementation-plan.md` — add the event to the event table and document
  its payload fields and emission rule.
- `src/Medinilla.Webhooks.Service/README.md` — add the `EventType` to the payload mapping
  table and describe producer ownership, ordering, and optional-field semantics.
- Any current review/ownership document that enumerates events or producers, if one is
  explicitly current rather than historical.
- Do not rewrite historical review findings as if they were current validation. Add a
  clear current-status note instead.

Document:

- The stable `EventType` string.
- When exactly the event is emitted.
- Which service owns it.
- Required identity fields (`charger_id`, `account_id`).
- Optional-field absence semantics.
- Any ordering or replay limitations.
- Any unresolved payload-scope question. Do not silently present accumulated or partial
  data as authoritative.

## 11. Verification

From the repository root:

```bash
dotnet test src/Medinilla.Core.Tests/Medinilla.Core.Tests.csproj --no-restore --verbosity minimal
dotnet test src/Medinilla.Core.Service.Tests/Medinilla.Core.Service.Tests.csproj --no-restore --verbosity minimal
dotnet test src/Medinilla.Webhooks.Service.Tests/Medinilla.Webhooks.Service.Tests.csproj --no-restore --verbosity minimal
git diff --check
```

No live database, migrations, Redis, deployment, or real HTTP validation is required for
an event producer change. Do not run application startup or seed scripts as a shortcut.

## 12. Checklist

```
[ ] Event represents a completed application milestone
[ ] Exactly one owning service selected
[ ] Payload proto added under SharedContracts/events
[ ] Proto added explicitly to SharedContracts.csproj with GrpcServices="None"
[ ] EventType constant added to WebhookEventTypes
[ ] Snapshot contains only materialized values and account_id
[ ] Mapper is synchronous, pure, and has no I/O
[ ] Mapper registered in AddMedinillaServices
[ ] Service emits after commit with DateTimeOffset.UtcNow
[ ] account_id comes from the already-loaded ChargingStation (no N+1)
[ ] No emission on failure, duplicate, replay, or intermediate state
[ ] action/router emit nothing
[ ] Tests cover mapper contract, successful emission, and no-emission branch
[ ] Plan, README, and current event inventory updated
[ ] Focused tests pass and git diff --check is clean
```

## 13. Common mistakes

- Publishing from `OcppCallRouter` or an action.
- Adding a second emitter for the same completion.
- Capturing the timestamp before commit or after an optional query.
- Adding a per-event `Account` lookup, or querying account inside a mapper.
- Passing a tracked `ChargingStation` into a mapper.
- Adding a protobuf file without registering it in the csproj.
- Forgetting the `WebhookEventTypes` constant or README mapping.
- Emitting on an OCPP `Updated` event or a duplicate sequence number.
- Making the mapper async or letting it read the clock.
- Letting a publication failure turn committed processing into an OCPP error.
- Documenting accumulated or incomplete data as an authoritative complete snapshot.
