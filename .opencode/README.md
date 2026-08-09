# .opencode

Skills in this folder help navigate OCPP documentation and assist with Medinilla development.

## Available Skills

| Skill | Purpose |
|---|---|
| commands-generator | Scaffold a new `OcppChargerCommand` (class + DTOs from OCPP JSON schema). Handler logic left for the developer. |
| actions-generator | Scaffold a new inbound `IOcppAction` (CS -> CSMS) — action class + DTOs from OCPP JSON schema. Handler logic left for the developer. |
| test-alignment | Fix stale tests only. Never edit source code under test. |
| ocpp-module-researcher | Research OCPP 2.0.1 protocol behaviour (message flows, PDUs, data types, configuration variables) from the local Part 2 spec PDF. Read-only — does not touch code. |
| coverage-reporter | Reconcile the OCPP 2.0.1 implementation against `docs/ocpp-coverage.md` and update that doc in place to reflect what is actually wired up. Does not change the doc's structure. |

Shortcuts to OCPP protocol context (actions, payloads, request/response flow) so you don't have to re-derive it from raw specs every time you touch the codebase.
