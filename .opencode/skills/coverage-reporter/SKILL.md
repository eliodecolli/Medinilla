---
name: coverage-reporter
description: Reconcile the OCPP 2.0.1 implementation in Medinilla (Actions + Commands) against `docs/ocpp-coverage.md` and update that doc in place — without changing its structure — to reflect what is actually wired up. Use when the user asks to refresh / update / regenerate / sync the OCPP coverage document, or after adding/removing OCPP actions or commands. Do NOT use for actually implementing new actions/commands (use `actions-generator` / `commands-generator` for that).
---

# Coverage Reporter

Keep `docs/ocpp-coverage.md` honest about what Medinilla actually implements,
without changing the document's structure.

## Mental model

Medinilla splits OCPP messages across two folders along the same direction
boundary OCPP itself uses for `CALL`s:

| Our term | Folder | Direction | Source-of-truth files |
| --- | --- | --- | --- |
| **Action** | `src/Medillina.Core/Actions/Ocpp201/<Name>Action.cs` | Charger → CSMS | Class implementing `IOcppAction` (`src/Medillina.Core/Actions/IOcppAction.cs:6`) |
| **Command** | `src/Medillina.Core/Commands/Ocpp201/<Name>Command.cs` | CSMS → Charger | Class implementing `IOcppChargerCommand` (`src/Medillina.Core/Commands/IOcppChargerCommand.cs:6`) |

**Both are OCPP actions** in the protocol sense. The internal split exists only
because our code treats the two directions differently (inbound has no gRPC
layer and no audit row, outbound is built gRPC-first and audited through
`ICommandExecutionService`). For documentation we collapse the two — every
OCPP 2.0.1 message lives in exactly one of the two per-direction tables in
`docs/ocpp-coverage.md`, regardless of which folder holds the C# class.

## Inputs

- **Coverage doc**: `docs/ocpp-coverage.md` (fixed path; do not move it).
- **Spec PDF**: `docs/OCPP-2.0.1_part2_specification_edition2.pdf` — use the
  `ocpp-module-researcher` skill to look up message names, functional blocks,
  and page numbers when in doubt.

## Workflow

### 1. Discover what is actually implemented

Do **not** trust the doc as the source of truth — read the code:

1. **Actions (charger → CSMS)** — list every file under
   `src/Medillina.Core/Actions/Ocpp201/`. The OCPP-J `ActionName` you care
   about comes from:
   - the `ActionName` property on the class (e.g. `AuthorizeAction`
     returns `"Authorize"`), and/or
   - the constant in `src/Medinilla.Infrastructure/WAMP/OcppActionNames.cs`
     (`public const string Authorize = "Authorize";` etc.).
2. **Commands (CSMS → charger)** — list every file under
   `src/Medillina.Core/Commands/Ocpp201/`. The OCPP-J `Action` comes from:
   - the `Action` property on the class, and/or
   - the same `OcppActionNames.cs` constants.
3. **Sanity-check** by reading `src/Medillina.Core/ServiceExtensions.cs`:
   - The `AddOcppActions` block lists every `services.AddScoped<IOcppAction, …Action>()`
     — that is the authoritative set of inbound actions.
   - The `AddOcppChargerCommands` block lists every
     `services.AddScoped<IOcppChargerCommand, …Command>()` — that is the
     authoritative set of outbound commands.

   If a class exists on disk but is not registered in
   `ServiceExtensions.cs`, treat it as **not implemented** — it will not be
   reachable through the router/factory. Mention this only if the user asks.

### 2. Read the current doc

Read `docs/ocpp-coverage.md` end-to-end and identify the structure:

- **Intro paragraphs** describing what Medinilla is and where the source is.
- **Base implementation reference** — use-case → message → status tables
  grouped by functional area (Charging Station Bootup, Configuration, Reset,
  Authorization, Transaction Mechanism, Availability, Monitoring Events,
  Meter values, Data Transfer).
- **Functional blocks summary** — one row per block A–P, summarising
  coverage as **None / Partial / Complete**.
- **Charger → CSMS (Actions)** table — every OCPP 2.0.1 message that flows
  charger → CSMS, with `#`, OCPP message name, functional block, spec page,
  and Status.
- **CSMS → Charger (Commands)** table — same shape for CSMS → charger
  messages.
- **Summary** table — three rows (Actions, Commands, Overall) with Total /
  Implemented / Missing counts.
- **Inline narrative** at the end of the **Base implementation reference**
  and **Functional blocks** sections (e.g. "5 of 16 blocks are partial.
  11 of 16 are not started. None are complete.") and the "Extra messages
  implemented beyond the base" line.

Treat all of the above as **fixed structure**. You may only edit **content
inside cells and inside narrative sentences** — never add or remove a row
from the per-message tables or a block from the Functional blocks summary,
never reorder them, and never invent a new section.

### 3. Verify against the spec

For every message whose **functional block** or **spec page number** you are
about to change, run the `ocpp-module-researcher` skill with the message
name as the keyword. Cite page numbers from the PDF (Part 2) — do not invent
them. If a page number in the current doc disagrees with the PDF, the PDF
wins.

If `ocpp-module-researcher` flags that a message name doesn't match Part 2
exactly (e.g. casing, punctuation), correct it in the doc only when the
discrepancy is clear from the spec; otherwise ask the user.

### 4. Update the doc in place

Walk the doc top-to-bottom and apply minimal edits:

1. **Base implementation reference tables** — flip each affected row's
   `Status` cell between `Implemented` and `**Missing**` (bold). Add or
   remove a use-case row only if the user explicitly asks — the table is
   the **minimum-required-by-the-Application-Guide** list, not a
   free-form list of everything we cover, so a new message doesn't
   automatically mean a new row here.
2. **Narrative under "Base implementation reference"** — keep the
   "**X of Y use case groups covered.** The missing ones are …" sentence
   accurate. Update "Extra messages implemented beyond the base" so it
   lists every implemented message that is **not** in the base reference
   (e.g. `Heartbeat (G02)`, `SecurityEventNotification (A04)`).
3. **Functional blocks summary** — update the **Medinilla coverage** cell
   for every block whose coverage changed (e.g. "Partial" → "Complete",
   or adding/removing the parenthetical list of implemented use cases).
   Do **not** renumber blocks or add/remove rows. Update the totals
   sentence ("5 of 16 blocks are partial. 11 of 16 are not started.
   None are complete.") to match.
4. **Charger → CSMS (Actions) table** — change `Status` to `Implemented`
   for every implemented inbound message, leave it as `Missing`
   otherwise. Do not add rows.
5. **CSMS → Charger (Commands) table** — same rule for outbound messages.
6. **Summary table** — recompute the three rows exactly. Format:
   `Implemented / Missing` per direction and an `Overall` row that sums
   them.

### 5. Verification

Before finishing, re-read `docs/ocpp-coverage.md` once more and check:

- **Counts add up**: `Total = Implemented + Missing` per row, and
  `Overall.Total = Actions.Total + Commands.Total`, etc.
- **Every implemented message is in exactly one of the two per-direction
  tables**, and every implemented message has `Status = Implemented`.
- **Every `Implemented` row in the per-direction tables has a matching
  registration in `ServiceExtensions.cs`** and a constant in
  `OcppActionNames.cs`.
- **No row was added, removed, or reordered** in the per-message tables or
  the Functional blocks table.
- **The narrative sentences under "Base implementation reference" and
  "Functional blocks" still parse** (e.g. "5 of 16 blocks are partial" must
  equal the count of `**Partial**` rows in the table above it).

If any check fails, fix the doc and re-run the checks. Only declare the
update done when every check passes.

## Out of scope

- Implementing new actions or commands — use `actions-generator` /
  `commands-generator`.
- Reformatting the doc, renaming sections, or rearranging tables.
- Updating the OCPP spec PDF or the JSON schemas.
- Adding new rows to the per-message tables — the message list is fixed
  by OCPP 2.0.1 Part 2 (the tables already enumerate every message
  defined by the spec).
