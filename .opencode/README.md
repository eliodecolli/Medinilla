# .opencode

Skills in this folder help navigate OCPP documentation and assist with Medinilla development.

## Available Skills

| Skill | Purpose |
|---|---|
| commands-generator | Scaffold a new `OcppChargerCommand` (class + DTOs from OCPP JSON schema). Handler logic left for the developer. |
| actions-generator | Scaffold a new inbound `IOcppAction` (CS -> CSMS) — action class + DTOs from OCPP JSON schema. Handler logic left for the developer. |
| test-alignment | Fix stale tests only. Never edit source code under test. |

Shortcuts to OCPP protocol context (actions, payloads, request/response flow) so you don't have to re-derive it from raw specs every time you touch the codebase.
