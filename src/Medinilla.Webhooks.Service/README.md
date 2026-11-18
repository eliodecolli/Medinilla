# Medinilla Webhooks Service

Standalone worker that consumes processed charger events from Redis Streams and POSTs
the serialized event wrapper to configured HTTP endpoints.

## Current scope

Implemented:

- Core publication from the owning application service for accepted `charger.bootup`,
  every processed `connector.status`, newly ingested `transaction.started` /
  `transaction.ended`, and `charger.registration.completed` after a final report chunk is
  ingested. Replayed transaction sequence numbers do not produce duplicate lifecycle
  events.
- Redis Streams transport from Core to this worker.
- Configuration-based subscriptions, `EventType` filtering, bounded HTTP fan-out,
  request timeout, no redirects, and HMAC-SHA256 request signing.

Producer ownership:

| Event | Core producer |
|---|---|
| `charger.bootup` | `ChargingStationBooting` boot service |
| `connector.status` | `StatusNotificationService` |
| `transaction.started` / `transaction.ended` | `TransactionService` |
| `charger.registration.completed` | `ChargerConfigService` report ingestion |

OCPP actions are protocol adapters only. They await one service processing call and
translate its outcome into an OCPP response; they do not publish events.

Not implemented yet:

- Durable retry scheduling / `DeliveryFallback`.
- Pending-entry recovery with `XAUTOCLAIM`.
- Dead-lettering and retry/dead retention.

A failed initial delivery is **not acknowledged**, so the Redis consumer-group pending
entry acts as a recoverable backlog. Do not add retry/recovery code without the durable
handoff described in `docs/webhook-implementation-plan.md`.

## Wire format

Each Redis stream entry field `payload` contains the raw serialized
`Medinilla.Core.SharedContracts.Events.MedinillaHttpEvent` protobuf. The same bytes are
sent as the HTTP request body with `Content-Type: application/protobuf`.

Consumers first parse the wrapper, then select the payload parser by `EventType`:

| `EventType` | Payload protobuf |
|---|---|
| `charger.bootup` | `BootupEvent` |
| `connector.status` | `StatusNotificationEvent` |
| `transaction.started` | `TransactionLifecycleEvent` |
| `transaction.ended` | `TransactionLifecycleEvent` |
| `charger.registration.completed` | `ChargerRegistrationCompletedEvent` |

`EventId` is a canonical UUID `D` string and stays stable for the logical event. The
wrapper's `EventTimestamp` is the UTC processing-completion instant. Charge/transaction
specific timestamps are inside the event payload where applicable. `EventData` is the
raw serialized event-specific protobuf, not base64 or JSON.

Delivery is at-least-once for accepted events, subject to the deferred retry/recovery
work. There is no global or per-charger ordering guarantee. `EventId` is not a
deduplication key for independently reprocessed charger notifications; use it to make
transport retries idempotent where the same logical publication is repeated.

### Registration metadata and status reconciliation

`charger.registration.completed` is emitted when a `NotifyReport` notification is
successfully ingested with its final marker (`tbc=false`, including an omitted value) and
all required sequence numbers have been received and persisted:

- An accepted `GetBaseReport` command response is not registration completion.
- An omitted `ReportData` payload with a final marker is delegated to ingestion as empty
  data; it is not skipped by the action.
- Missing report chunks or failed persistence do not emit the event.
- No registration-attempt state is persisted for webhook delivery. A successfully
  processed final report may emit even without a designated attempt or command acceptance.
- `connectors` currently comes from all stored `Connector` components known for the charger,
  not from a single report-scoped inventory. Historical connectors or variables can
  therefore appear in later events. **Do not treat this payload as an authoritative
  report-specific complete inventory until that contract is resolved.**
- `connector_type` is absent when no stored value is known.
- `EventId` identifies an individual published event and is stable across transport
  retries. It does not deduplicate independently reprocessed reports.
- Registration and status events are not ordered. Consumers must tolerate status events
  before registration metadata and reconcile inventory when registration arrives.

`StatusNotificationEvent.connector_type` (field 6) is deprecated for new events and is
left unset. It remains in the protobuf contract so older events can still carry it.
Consumers should use registration inventory for connector types and treat status events
as identity/status updates. Do not block status handling while waiting for registration
metadata.

There is no automatic report issuance on boot; the caller owns trigger and report
selection.

## Adding a subscription

Use the bundled script to append a subscription to this project's
`appsettings.json` and generate its signing secret:

```bash
python add_subscription.py https://example.internal/hooks/medinilla
python add_subscription.py http://localhost:9000/hook \
  --event-type connector.status \
  --event-type transaction.started
```

The script prints the generated secret. For production, do not commit it in
`appsettings.json`; remove the generated `Webhooks:Secrets` entry and provide the
secret through the printed `MEDINILLA_WEBHOOK_SECRET_<REF>` environment variable.

## HTTP headers

| Header | Meaning |
|---|---|
| `X-Medinilla-Event-Id` | Stable event UUID for consumer deduplication |
| `X-Medinilla-Event-Type` | Wrapper `EventType` discriminator |
| `X-Medinilla-Timestamp` | Unix epoch seconds used in the signature |
| `X-Medinilla-Signature` | `v1=<lowercase hex HMAC-SHA256>` |

Signature input is the exact concatenation:

```text
UTF8(timestamp + ".") || exact HTTP request body bytes
```

Use the subscription's shared signing secret as the HMAC key. Reject requests whose
signature does not match, and apply a timestamp tolerance in the consumer.

## Configuration

```json
{
  "Webhooks": {
    "RedisUri": "localhost:6379",
    "EventsStream": "medinilla:{webhooks}:events",
    "PayloadField": "payload",
    "ConsumerGroup": "medinilla.webhooks",
    "ReadBatchSize": 10,
    "IdleReadDelayMilliseconds": 500,
    "ErrorBackoffSeconds": 5,
    "MaxConcurrency": 8,
    "HttpTimeoutSeconds": 10,
    "Subscriptions": [
      {
        "Id": "example-connector-consumer",
        "Url": "https://example.internal/hooks/medinilla",
        "Enabled": true,
        "SigningSecretRef": "ExampleConnectorConsumer",
        "EventTypes": [
          "connector.status",
          "transaction.started",
          "transaction.ended",
          "charger.registration.completed"
        ]
      }
    ]
  }
}
```

- `SigningSecretRef` is resolved from `Webhooks:Secrets:<ref>` (for example via
  `Webhooks__Secrets__ExampleConnectorConsumer`) or the environment variable
  `MEDINILLA_WEBHOOK_SECRET_<REF>`. The resolved value is never written to logs or
  delivery metadata.
- Omit `EventTypes` or leave it empty to match every supported event type.
- An omitted `ConsumerName` generates a unique consumer name per process. Set it only
  when a stable identity is required.
- Subscription URLs are validated at startup. Redirects are never followed; private
  network addresses are allowed because endpoints are deployment-controlled.
