# Medinilla Webhooks Service

Standalone worker that consumes processed charger events from Redis Streams and POSTs
the serialized event wrapper to configured HTTP endpoints.

## Current scope

Implemented:

- Core publication of accepted `charger.bootup`, every processed `connector.status`, and
  newly ingested `transaction.started` / `transaction.ended`. Replayed transaction
  sequence numbers do not produce another lifecycle event.
- Redis Streams transport from Core to this worker.
- Configuration-based subscriptions, `EventType` filtering, bounded HTTP fan-out,
  request timeout, no redirects, and HMAC-SHA256 request signing.

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

`EventId` is a canonical UUID `D` string and stays stable for the logical event. The
wrapper's `EventTimestamp` is the UTC processing-completion instant. Charge/transaction
specific timestamps are inside the event payload where applicable. `EventData` is the
raw serialized event-specific protobuf, not base64 or JSON.

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
        "EventTypes": ["connector.status", "transaction.started", "transaction.ended"]
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
