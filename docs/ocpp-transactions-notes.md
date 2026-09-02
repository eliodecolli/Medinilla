# OCPP 2.0.1 — Transactions & Energy Consumption

**NOTE**: This document is AI-generated to keep track of possible transaction-related features. Don't pay much attention to it.

---

Reference notes compiled from research into the OCPP 2.0.1 Part 2 specification
(Edition 2, 2022-12-15). Intended to give a future agent enough domain context
to help refactor transactions / energy-consumption logic in the Medinilla CSMS
without re-doing the spec research.

Source spec: `docs/OCPP-2.0.1_part2_specification_edition2.pdf`
Page citations use the form `p. NNN` and refer to that PDF.

---

## Scope & perspective

- **Perspective:** CSMS-side (Medinilla is a CSMS — confirmed by
  `docs/ocpp-coverage.md:3`). These notes cover what the CSMS receives,
  processes, and stores.
- **Focus:** computing final energy consumption from `TransactionEventRequest`
  data, and the related integrity / anti-fraud considerations.
- **Out of scope:** cost calculation math (the spec does not define it; CSMS
  owns it). Pricing, tariff structure, and customer billing are deliberately
  not covered here.

---

## Glossary (non-electrical terms)

The user who produced these notes does not have an electrical-engineering
background. An agent may need to explain any of these terms when refactoring.

| Term | Meaning |
|---|---|
| **Phase (L1, L2, L3)** | One of three wires carrying AC power in most European/Asian installations. Like three separate hoses feeding the same device. Some EVs charge on one phase, some on three. |
| **Register** | A cumulative counter on a meter (analogous to a car's odometer). Only goes up. Energy meters have one register per phase, and sometimes one overall. |
| **Meter** | The physical hardware that measures electricity. Has registers. The Charging Station reads the registers and reports them via OCPP. |
| **Outlet** | The connector / cable the EV plugs into. Energy delivered "at the outlet" = energy the EV actually received. |
| **Inlet** | The grid-side connection point of the Charging Station. Useful for measuring station-level consumption (parasitic load, etc.). |
| **Tariff** | The price plan (e.g. €0.25/kWh). Stored in the CSMS, not the OCPP messages themselves. |
| **Fiscal meter** | A legally certified meter used for billing. OCPP supports signed readings from these. |

---

## OCPP transaction lifecycle at a glance

The spec defines the transaction lifecycle in functional block **E (Transactions)**
through use cases E01–E15 (`p. 117–169`). The full lifecycle is reported via the
single `TransactionEventRequest` message with `eventType` ∈ `{Started, Updated,
Ended}`.

| Use case | Page | Purpose |
|---|---|---|
| E01 Start Transaction options | 120 | Defines `TxStartPoint` triggers |
| E02 Start Transaction — Cable Plugin First | 127 | Cable-first flow |
| E03 Start Transaction — IdToken First | 131 | Auth-first flow |
| E04 Transaction started while offline | 135 | Queues messages with `offline=true` |
| E05 Start Transaction — Id not Accepted | 139 | CSMS rejects auth |
| E06 Stop Transaction options | 142 | Defines `TxStopPoint` triggers |
| E07 Transaction locally stopped by IdToken | 146 | EV driver presents IdToken to stop |
| E08 Transaction stopped while offline | 151 | Stop-while-offline, queued |
| E09 EV-side cable disconnect → Stop | 154 | `StopTxOnEVSideDisconnect=true` |
| E10 EV-side cable disconnect → Suspend | 157 | `StopTxOnEVSideDisconnect=false` |
| E11 Connection Loss During Transaction | 160 | Queue + retry on reconnect |
| E12 Inform CSMS of Offline Occurred Transaction | 162 | Transactions that started AND ended offline |
| E13 Transaction-related message not accepted | 164 | Retry policy via `MessageAttemptsTransactionEvent` × `MessageAttemptIntervalTransactionEvent` |
| E14 Get Transaction Status | 166 | CSMS can ask CS for queued msgs |
| E15 End of charging process (ISO 15118) | 167 | PnC-specific |

Other relevant functional blocks:

- **I (TariffAndCost)** — `p. 212–221`. Display-only; no cost math in the spec.
- **J (MeterValues)** — `p. 223–232`. The metering model details.

---

## TransactionEventRequest PDU (`p. 381–382`)

```
TransactionEventRequest
├─ eventType: TransactionEventEnumType  (Started | Updated | Ended)
├─ timestamp: dateTime
├─ triggerReason: TriggerReasonEnumType
├─ seqNo: integer                          (incremental, 0 at start of tx)
├─ offline: boolean?                       (default false)
├─ numberOfPhasesUsed: integer?
├─ cableMaxCurrent: integer?
├─ reservationId: integer?                 (first request only — H01.FR.15)
├─ transactionInfo: TransactionType        (required)
│   ├─ transactionId: identifierString[0..36]
│   ├─ chargingState: ChargingStateEnumType?
│   ├─ timeSpentCharging: integer?         (seconds energy flowed)
│   ├─ stoppedReason: ReasonEnumType?      (omitted iff Local)
│   └─ remoteStartId: integer?
├─ idToken: IdTokenType?                   (first auth + first stop-auth only)
├─ evse: EVSEType?                         (first request only — E01.FR.16)
└─ meterValue: MeterValueType[]            (see below)
   └─ sampledValue: SampledValueType[]
      ├─ value: decimal                    ← raw register reading
      ├─ measurand: MeasurandEnumType?     (default Energy.Active.Import.Register)
      ├─ context: ReadingContextEnumType?  (Transaction.Begin | Transaction.End | Sample.Periodic | ...)
      ├─ location: LocationEnumType?       (default Outlet)
      ├─ phase: PhaseEnumType?
      ├─ signedMeterValue: SignedMeterValueType?
      └─ unitOfMeasure: UnitOfMeasureType? (default { unit: "Wh", multiplier: 0 })
```

### Optional-field rules (`p. 118`)

- `evse` — first request only (E01.FR.16).
- `idToken` — first authorization event and stop-authorization event (E03.FR.01,
  E07.FR.02, C12.FR.02, F02.FR.05).
- `reservationId` — first request after the reservation matches (E03.FR.03,
  H01.FR.15, F02.FR.06).
- `meterValue` — populated per `SampledDataCtrlr.Tx{Started,Updated,Ended}Measurands`.
- `transactionInfo.chargingState` — every state change.
- `transactionInfo.stoppedReason` — required in Ended unless value is `Local`.

### Sequence numbers (`p. 117–118`)

- Start at 0 when transaction starts.
- Increment by 1 after each request.
- Per-EVSE counter.
- CSMS uses these to verify completeness: must have every integer from
  `seqNo_start` to `seqNo_end` inclusive.

### Offline / queueing (`p. 137, 152, 161, 164`)

- When offline, queue messages. On reconnect, send all queued messages with
  `offline=true`.
- Retry policy per E13: `MessageAttemptsTransactionEvent` ×
  `MessageAttemptIntervalTransactionEvent`.

---

## TransactionEventResponse PDU (`p. 382`)

```
TransactionEventResponse
├─ totalCost: decimal?             ← running cost (Updated) | final cost (Ended)
├─ chargingPriority: integer?      (-9..9, overrides IdTokenInfoType)
├─ idTokenInfo: IdTokenInfoType?   (required if request had idToken)
└─ updatedPersonalMessage: MessageContentType?  (tariff updates per I06)
```

**Important:** `totalCost` is **populated by the CSMS**, not derived by the
Charging Station. Free transaction = `0.00` (absent ≠ free). Currency comes from
the `Currency` config variable (`p. 462`).

---

## Energy calculation — the core formula

### Formula (`MeasurandEnumType` Note 3, `p. 430`)

> "The actual quantity of energy corresponding to a reported '.Register' value
> is computed as the register value in question minus the register value
> recorded/reported at the start of the transaction or other relevant starting
> reference point in time."

```
energy_Wh  = (register at context=Transaction.End)
           - (register at context=Transaction.Begin)

energy_Wh *= 10 ^ unitOfMeasure.multiplier   # default multiplier = 0
energy_kWh = energy_Wh / 1000
```

### Where the two readings come from

| Event | Context | Requirement | Page |
|---|---|---|---|
| `eventType=Started` | `Transaction.Begin` | E02.FR.09, E03.FR.07, E04.FR.05, E05.FR.05 | 129, 133, 137, 140 |
| `eventType=Ended` | `Transaction.End` | E06.FR.11, E07.FR.08 | 146, 150 |

Configured by:
- `SampledDataCtrlr.TxStartedMeasurands` (default recommended:
  `Energy.Active.Import.Register`) — `p. 454`.
- `SampledDataCtrlr.TxEndedMeasurands` (same default) — `p. 454`.

### Registers are NOT reset (`p. 230–231, J02.FR.16–17`)

- All `.Register` values MUST be monotonically increasing.
- SHOULD be reported exactly as read from the meter hardware.
- SHOULD NOT be re-based to zero at start of transaction.
- Exception: meter replacement breaks monotonicity — must be detected and
  handled.

---

## Phases & per-phase data — agreed design

### "Source of truth" — what we mean by it

Two different meanings need to be kept apart:

| | Meaning | Data |
|---|---|---|
| **Source of truth** | what we persist and run integrity / anti-fraud checks against | per-phase |
| **Billable quantity** | what we charge the customer | total = Σ phases |

Per-phase is the source of truth **for integrity**. The total is the source of
truth **for billing**. These are different roles.

### Architectural conclusions (agreed in the producing conversation)

1. **Per-phase data is the primary source of truth (for storage and integrity).**
   Persist it. You cannot reconstruct per-phase from a total once it's gone.
2. **Total = Σ phases.** The total is derived from the per-phase data, not the
   other way around.
3. **Billing uses the total** (customer pays for delivered energy, not
   per-phase breakdown).
4. **Integrity checks use per-phase** (anomaly detection, fraud signals,
   wiring validation).

### Why the spec reports phases separately (`p. 230–231, p. 458`)

- The meter may not expose a single "total" register.
- Cross-checking per-phase vs. total is a fraud / wiring diagnostic.
- Other measurands (`Voltage`, `Current.Import`, `Power.Active.Import`) are
  inherently per-phase.
- Charging is not always balanced across phases.

### Opt-out (`p. 458`)

`SampledDataCtrlr.RegisterValuesWithoutPhases = true` tells the CS to report
only the combined `Energy.Active.Import.Register`, not per-phase splits.

### Implementation options

**A. Keep per-phase** (recommended for fraud detection / regulated contexts):

```
# Source of truth (stored):
store_all_per_phase_register_readings()

# Billable quantity (derived at billing time):
total_Wh = Σ over phase ∈ {L1, L2, L3} of
              (register_at_Transaction.End[phase]
               - register_at_Transaction.Begin[phase])

# Integrity check (also derived):
if overall_register_present:
    assert abs(sum_of_phase_deltas - overall_register_delta) < tolerance
```

Cross-check against any overall register if present. Disagreement = alert.

**B. Opt out per CS** (for simpler billing-only deployments):

Set `SampledDataCtrlr.RegisterValuesWithoutPhases = true` on each CS. Delta
the single overall register. No per-phase forensics possible — billing still
works, integrity checks against per-phase bypass are gone.

The CSMS should ideally support **both** — CSes in the field may be in either
configuration regardless of fleet-wide policy.

---

## Sample stream during Updates — what it is, what it's for

### Structure (`ReadingContextEnumType`, `p. 426`)

| Context | Meaning |
|---|---|
| `Transaction.Begin` | Reading at start of transaction |
| `Transaction.End` | Reading at end of transaction |
| `Sample.Periodic` | Periodic snapshot during transaction |
| `Sample.Clock` | Clock-aligned snapshot (e.g. every 15 min) |
| `Interruption.Begin` / `Interruption.End` | Around a suspend |
| `Trigger` | In response to `TriggerMessageRequest` |
| `Other` | Anything else |

### Sample stream use cases

Per J02.FR.11 (`p. 230`), the CS sends periodic Updated events every
`SampledDataTxUpdatedInterval` seconds with `context = Sample.Periodic`. These
are **the same register** as the Begin/End readings, just snapshot at
intermediate times.

**For final consumption: NOT needed.** `End − Begin` is sufficient. The sample
stream contains zero additional information about the total.

**For real-time UX:**

- Powers the EV driver's running cost display (I02, `p. 215–216`).
- Powers mobile app / web portal progress.
- Powers CSMS dashboards.

**For integrity / anti-fraud:**

- Monotonicity check during the transaction — catch backwards meter movement
  in real time.
- Dropped-message detection — gaps in expected interval reveal lost messages.
- In-progress fraud detection — bypass / tamper can be spotted as it happens.
- Sanity-check the End value against the last sample.

### Configuration

| CV | Purpose | Page |
|---|---|---|
| `SampledDataCtrlr.TxUpdatedMeasurands` | what to sample | 455 |
| `SampledDataCtrlr.TxUpdatedInterval` | sample interval (0 = none) | 455 |
| `AlignedDataCtrlr.TxEndedMeasurands` | clock-aligned snapshots at End | 467 |
| `AlignedDataCtrlr.TxEndedInterval` | spacing for those | 467 |
| `SampledDataCtrlr.SignReadings` | enable signed meter values | 454 |
| `AlignedDataCtrlr.SignReadings` | enable signed clock-aligned values | 467 |

---

## Integrity & anti-fraud — layered checks

These are the checks established as valuable in the producing conversation.

1. **Monotonicity** — `.Register` values never go backwards within a
   transaction (J02.FR.16, `p. 230`).
2. **Inter-transaction continuity** — End register of tx N must equal Begin
   register of tx N+1 on the same connector. Exception: meter replacement.
3. **Per-phase sum vs. overall register** — if both are reported, they must
   agree. Disagreement = alert (per-phase bypass / mis-wiring).
4. **Phase that legitimately reads zero ≠ phase that was bypassed** —
   distinguish them. Single-phase EV on 3-phase outlet is legitimate;
   identically-zero phase under load is not.
5. **Sample-stream monotonicity** — detect drift mid-transaction.
6. **Dropped-message detection** — gap between expected `seqNo` values
   indicates lost queued message. E13 retries will eventually fill the gap;
   E14 can request status proactively.
7. **`stoppedReason` gates billing logic** — `PowerLoss`, `GroundFault`,
   `EVDisconnected`, `EmergencyStop`, `Reboot` etc. each imply different
   billability. ReasonEnumType at `p. 426`.

### Anti-fraud scenarios that per-phase data exposes

- **Bypass on one phase** — current flows around one sensor. Total undercounts,
  per-phase shows the missing phase.
- **Wiring swap / mis-wiring** — phase labels don't match actual current
  flows.
- **Tampered sensor** — one phase consistently under-reports.
- **Single-phase EV on 3-phase outlet** — legitimate, but only distinguishable
  from bypass if you have per-phase data.

---

## What's NOT in OCPP (explicit scope exclusion)

### Cost calculation (`p. 213, p. 117`)

> "Because tariff structures can become very complex it will be difficult to
> convert these to human-readable text in the Charging Station. The CSO is the
> owner of the tariffs and should be able to provide the Charging Station with
> a human-readable tariff text. … That is why we have kept the complexity of
> tariffs out of OCPP." (`p. 213`)

> "The primary purpose of TransactionEventRequest messages is to give the CSMS
> the information that it will later use to bill the transaction." (`p. 117`)

The CSMS owns cost math. The CS just renders whatever `totalCost` the CSMS
sends in `TransactionEventResponse`, plus optional `CostUpdatedRequest`
pushes.

### Tariff display configuration variables (`p. 461–462`)

| CV | Purpose |
|---|---|
| `TariffCostCtrlr.TariffEnabled` | toggle tariff display |
| `TariffCostCtrlr.TariffAvailable` | reported capability |
| `TariffCostCtrlr.TariffFallbackMessage` | shown when no driver-specific tariff (I04) |
| `TariffCostCtrlr.CostEnabled` | toggle cost display |
| `TariffCostCtrlr.CostAvailable` | reported capability |
| `TariffCostCtrlr.TotalCostFallbackMessage` | shown when offline at stop (I05) |
| `TariffCostCtrlr.Currency` | ISO 4217 code for `totalCost` |

### Sales tariff data types

`SalesTariffType`, `SalesTariffEntryType`, `ConsumptionCostType`, `CostType`,
`CostKindEnumType` (`p. 391, 398, 401, 423`) exist but only for **ISO 15118
Plug & Charge schedule negotiation** (K15/K17, `p. 275–281`). The CS passes
them to the EV; the billing math still lives server-side.

---

## Transaction config variables (TxCtrlr, `p. 451–453`)

| CV | Required | Purpose |
|---|---|---|
| `TxCtrlr.EVConnectionTimeOut` | yes | Timeout for EV to plug in after auth |
| `TxCtrlr.StopTxOnEVSideDisconnect` | yes | Stop tx when cable unplugged at EV |
| `TxCtrlr.TxBeforeAcceptedEnabled` | no | Allow tx before `BootNotification` accepted |
| `TxCtrlr.TxStartPoint` | yes | When to start tx (MemberList) |
| `TxCtrlr.TxStopPoint` | yes | When to end tx (MemberList) |
| `TxCtrlr.MaxEnergyOnInvalidId` | no | Wh cap on invalid-id tx |
| `TxCtrlr.StopTxOnInvalidId` | yes | Deauthorize tx on non-Accepted auth |

### Allowed values for `TxStartPoint` / `TxStopPoint` (`p. 451–453`)

| Value | Start | Stop (= NOT …) |
|---|---|---|
| `ParkingBayOccupancy` | object in bay | object leaves bay |
| `EVConnected` | cable connected | cable disconnected |
| `Authorized` | driver/EV authorized | driver/EV not authorized |
| `PowerPathClosed` | preconditions met | preconditions not met |
| `EnergyTransfer` | energy flowing | energy not flowing |
| `DataSigned` | first signed meter value | n/a as TxStopPoint |

OCPP 1.6-compatible config: `TxStartPoint = PowerPathClosed`,
`TxStopPoint = EVConnected, Authorized` (`p. 117`).

---

## TransactionId generation (`p. 117`)

- Generated by the Charging Station (new in 2.0.1).
- MUST be unique for the lifetime of the CS — never reused.
- Survives reboot, firmware update, repair.
- RECOMMENDED: UUID. OCPP does not mandate an algorithm.

---

## Open questions / decisions pending

The producing user has not made these explicit. An agent should ask before
acting on them.

1. **Where in Medinilla's code does the energy calculation live?** The notes
   don't cover the existing implementation.
2. **Is Medinilla persisting per-phase data?** Agreed in principle (per-phase is
   the source of truth for storage and integrity). If not currently
   persisted, the refactor needs schema/storage work.
3. **Is fraud detection a current requirement, or future?** Drives whether
   per-phase storage is worth it.
4. **What's the policy on `RegisterValuesWithoutPhases`?** Set it fleet-wide?
   Per-CS? Leave alone?
5. **How are meter replacements currently handled?** The spec exception
   (J02.FR.16 note, `p. 230`) needs explicit handling.
6. **What does the existing stoppedReason filtering do?** Needs review against
   the full ReasonEnumType list (`p. 426`).
7. **Offline / queued transaction handling** — does the existing
   `seqNo`-gap detection exist? E13 retry semantics?

---

## Quick reference — page index

| Topic | Pages |
|---|---|
| Definition of Transaction | 12–13, 18 |
| Transaction event mechanism | 117–118 |
| Use cases E01–E15 | 120–169 |
| TariffAndCost I01–I06 | 214–221 |
| MeterValues J01–J03 | 223–232 |
| CostUpdated PDU | 357 |
| TransactionEvent PDU | 381–382 |
| TransactionType | 403 |
| UnitOfMeasureType | 403 |
| SampledValueType | 398–399 |
| MeasurandEnumType + Note 3 | 420, 430 |
| ReadingContextEnumType | 426 |
| ReasonEnumType | 426 |
| TxCtrlr CVs | 451–453 |
| SampledDataCtrlr CVs | 453–458 |
| AlignedDataCtrlr CVs | 465–467 |
| TariffCostCtrlr CVs | 461–462 |
