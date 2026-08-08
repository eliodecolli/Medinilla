# OCPP 2.0.1 Coverage

Medinilla is a CSMS. **Actions** are messages received from the charger
(`src/Medillina.Core/Actions/Ocpp201/`); **Commands** are messages sent to the
charger (`src/Medillina.Core/Commands/Ocpp201/`).

The OCPP 2.0.1 specification (Part 2, Edition 2, 2022-12-15) is organised into
**functional blocks** (A through P). The Application Guide defines a minimum
set of use cases that must be implemented for OCPP 2.0.1 compliance — see
[Base implementation reference](#base-implementation-reference) below. The
complete per-message catalogue is in the bottom tables.

Source: `docs/OCPP-2.0.1_part2_specification_edition2.pdf`.

## Base implementation reference

Use cases the OCPP 2.0.1 Application Guide requires for compliance, mapped to
the messages they need.

### Charging Station Bootup

| Use case | Message(s) | Status |
|---|---|---|
| B01 - Cold Boot Charging Station | BootNotification | Implemented |
| B02 - Cold Boot - Pending | BootNotification | Implemented |
| B03 - Cold Boot - Rejected | BootNotification | Implemented |
| B04 - Offline Behavior Idle Charging Station | StatusNotification | Implemented |

### Charging Station Configuration

| Use case | Message(s) | Status |
|---|---|---|
| B05 - Set Variables | SetVariables | Implemented |
| B06 - Get Variables | GetVariables | Implemented |
| B07 - Get Base Report | GetBaseReport, NotifyReport | **Missing** |

### Charging Station Reset

| Use case | Message(s) | Status |
|---|---|---|
| B11 - Reset - Without Ongoing Transaction | Reset | **Missing** |
| B12 - Reset - With Ongoing Transaction | Reset | **Missing** |

### Authorization Options (one of C01, C02, C04)

| Use case | Message(s) | Status |
|---|---|---|
| C01 - EV Driver Authorization using RFID | Authorize | Implemented |

### Transaction Mechanism

| Use case | Message(s) | Status |
|---|---|---|
| E01 - Start Transaction options (one of S1–S6) | TransactionEvent, Authorize | Implemented |
| E02 - Start Transaction - Cable Plugin First | TransactionEvent | Implemented |
| E03 - Start Transaction - IdToken First | TransactionEvent, Authorize | Implemented |
| E05 - Start Transaction - Id not Accepted | TransactionEvent | Implemented |
| E06 - Stop Transaction options (one of S1–S6) | TransactionEvent | Implemented |
| E07 - Transaction locally stopped by IdToken | TransactionEvent | Implemented |
| E08 - Transaction stopped while Charging Station is offline | TransactionEvent | Implemented |
| One of E09–E13 | TransactionEvent | Implemented |

### Availability

| Use case | Message(s) | Status |
|---|---|---|
| G01 - Status Notification | StatusNotification | Implemented |
| G03 - Change Availability EVSE/Connector | ChangeAvailability | **Missing** |
| G04 - Change Availability Charging Station | ChangeAvailability | **Missing** |

### Monitoring Events

| Use case | Message(s) | Status |
|---|---|---|
| G05 - Lock Failure | StatusNotification | Implemented |
| N07 - Alert Event | NotifyEvent | **Missing** |

### Transaction related Meter values

| Use case | Message(s) | Status |
|---|---|---|
| J02 - Sending transaction related Meter Values | TransactionEvent (with `meterValue`) | Implemented |

### Data Transfer

| Use case | Message(s) | Status |
|---|---|---|
| P01 - Data Transfer to the Charging Station | DataTransfer | **Missing** |
| P02 - Data Transfer to the CSMS | DataTransfer | **Missing** |

**Base compliance result: 14 of 17 use case groups covered.** The missing
ones are B07 (Get Base Report), B11/B12 (Reset), G03/G04 (ChangeAvailability),
N07 (Alert Event), and P01/P02 (Data Transfer).

Extra messages implemented beyond the base: Heartbeat (G02), SecurityEventNotification (A04).

## Functional blocks

| Block | Use cases | Medinilla coverage |
|---|---|---|
| A. Security | A01–A05 | **Partial** — `SecurityEventNotification` (A04) only |
| B. Provisioning | B01–B12 | **Partial** — BootNotification (B01–B03), GetVariables (B06), SetVariables (B05) |
| C. Authorization | C01–C16 | **Partial** — Authorize (C01) only |
| D. LocalAuthorizationList Management | D01–D02 | **None** |
| E. Transactions | E01–E15 | **Partial** — TransactionEvent (E01–E13) |
| F. RemoteControl | F01–F06 | **None** |
| G. Availability | G01–G05 | **Partial** — Heartbeat (G02), StatusNotification (G01, G05) |
| H. Reservation | H01–H04 | **None** |
| I. TariffAndCost | I01–I06 | **None** |
| J. MeterValues | J01–J03 | **Partial** — J02 covered via TransactionEvent; standalone `MeterValues` not supported |
| K. SmartCharging | K01–K17 | **None** |
| L. FirmwareManagement | L01–L04 | **None** |
| M. ISO 15118 Certificate Management | M01–M06 | **None** |
| N. Diagnostics | N01–N10 | **None** |
| O. DisplayMessage | O01–O06 | **None** |
| P. DataTransfer | P01–P02 | **None** |

**5 of 16 blocks are partial. 11 of 16 are not started.** None are complete.

## Charger → CSMS (Actions)

| # | OCPP 2.0.1 message | Functional block | Spec section | Status |
|---|---|---|---|---|
| 1 | Authorize | C. Authorization | p. 353 | Implemented |
| 2 | BootNotification | B. Provisioning | p. 353 | Implemented |
| 3 | FirmwareStatusNotification | L. FirmwareManagement | p. 359 | Missing |
| 4 | Heartbeat | G. Availability | p. 366 | Implemented |
| 5 | LogStatusNotification | N. Diagnostics | p. 367 | Missing |
| 6 | MeterValues | J. MeterValues | p. 367 | Missing |
| 7 | NotifyChargingLimit | K. SmartCharging | p. 367 | Missing |
| 8 | NotifyCustomerInformation | N. Diagnostics | p. 368 | Missing |
| 9 | NotifyDisplayMessages | O. DisplayMessage | p. 368 | Missing |
| 10 | NotifyEVChargingNeeds | K. SmartCharging | p. 369 | Missing |
| 11 | NotifyEVChargingSchedule | K. SmartCharging | p. 369 | Missing |
| 12 | NotifyEvent | N. Diagnostics | p. 370 | Missing |
| 13 | NotifyMonitoringReport | N. Diagnostics | p. 370 | Missing |
| 14 | NotifyReport | B. Provisioning | p. 371 | Missing |
| 15 | PublishFirmwareStatusNotification | L. FirmwareManagement | p. 372 | Missing |
| 16 | ReportChargingProfiles | K. SmartCharging | p. 372 | Missing |
| 17 | ReservationStatusUpdate | H. Reservation | p. 374 | Missing |
| 18 | SecurityEventNotification | A. Security | p. 375 | Implemented |
| 19 | StatusNotification | G. Availability | p. 380 | Implemented |
| 20 | TransactionEvent | E. Transactions | p. 381 | Implemented |

## CSMS → Charger (Commands)

| # | OCPP 2.0.1 message | Functional block | Spec section | Status |
|---|---|---|---|---|
| 1 | CancelReservation | H. Reservation | p. 354 | Missing |
| 2 | CertificateSigned | M. ISO 15118 CertMgmt | p. 354 | Missing |
| 3 | ChangeAvailability | G. Availability | p. 355 | Missing |
| 4 | ClearCache | C. Authorization | p. 355 | Missing |
| 5 | ClearChargingProfile | K. SmartCharging | p. 356 | Missing |
| 6 | ClearDisplayMessage | O. DisplayMessage | p. 356 | Missing |
| 7 | ClearedChargingLimit | K. SmartCharging | p. 356 | Missing |
| 8 | ClearVariableMonitoring | N. Diagnostics | p. 357 | Missing |
| 9 | CostUpdated | I. TariffAndCost | p. 357 | Missing |
| 10 | CustomerInformation | N. Diagnostics | p. 358 | Missing |
| 11 | DataTransfer | P. DataTransfer | p. 358 | Missing |
| 12 | DeleteCertificate | M. ISO 15118 CertMgmt | p. 359 | Missing |
| 13 | Get15118EVCertificate | M. ISO 15118 CertMgmt | p. 360 | Missing |
| 14 | GetBaseReport | B. Provisioning | p. 360 | Missing |
| 15 | GetCertificateStatus | M. ISO 15118 CertMgmt | p. 361 | Missing |
| 16 | GetChargingProfiles | K. SmartCharging | p. 361 | Missing |
| 17 | GetCompositeSchedule | K. SmartCharging | p. 362 | Missing |
| 18 | GetDisplayMessages | O. DisplayMessage | p. 362 | Missing |
| 19 | GetInstalledCertificateIds | M. ISO 15118 CertMgmt | p. 363 | Missing |
| 20 | GetLocalListVersion | D. LocalAuthList Mgmt | p. 363 | Missing |
| 21 | GetLog | N. Diagnostics | p. 363 | Missing |
| 22 | GetMonitoringReport | N. Diagnostics | p. 364 | Missing |
| 23 | GetReport | B. Provisioning | p. 365 | Missing |
| 24 | GetTransactionStatus | E. Transactions | p. 365 | Missing |
| 25 | GetVariables | B. Provisioning | p. 366 | Implemented |
| 26 | InstallCertificate | M. ISO 15118 CertMgmt | p. 366 | Missing |
| 27 | PublishFirmware | L. FirmwareManagement | p. 371 | Missing |
| 28 | RequestStartTransaction | F. RemoteControl | p. 373 | Missing |
| 29 | RequestStopTransaction | F. RemoteControl | p. 373 | Missing |
| 30 | ReserveNow | H. Reservation | p. 374 | Missing |
| 31 | Reset | B. Provisioning | p. 375 | Missing |
| 32 | SendLocalList | D. LocalAuthList Mgmt | p. 375 | Missing |
| 33 | SetChargingProfile | K. SmartCharging | p. 376 | Missing |
| 34 | SetDisplayMessage | O. DisplayMessage | p. 377 | Missing |
| 35 | SetMonitoringBase | N. Diagnostics | p. 377 | Missing |
| 36 | SetMonitoringLevel | N. Diagnostics | p. 378 | Missing |
| 37 | SetNetworkProfile | B. Provisioning | p. 379 | Missing |
| 38 | SetVariableMonitoring | N. Diagnostics | p. 379 | Missing |
| 39 | SetVariables | B. Provisioning | p. 379 | Implemented |
| 40 | SignCertificate | M. ISO 15118 CertMgmt | p. 380 | Missing |
| 41 | TriggerMessage | F. RemoteControl | p. 382 | Missing |
| 42 | UnlockConnector | F. RemoteControl | p. 383 | Missing |
| 43 | UnpublishFirmware | L. FirmwareManagement | p. 383 | Missing |
| 44 | UpdateFirmware | L. FirmwareManagement | p. 384 | Missing |

## Summary

| Bucket | Total | Implemented | Missing |
|---|---|---|---|
| Charger → CSMS (Actions) | 20 | 6 | 14 |
| CSMS → Charger (Commands) | 44 | 2 | 42 |
| **Overall** | **64** | **8** | **56** |
