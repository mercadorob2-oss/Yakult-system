# SOP / WI: Yakult Scanner - Scanning and Synchronization Logic

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / Development |
| Audience | IT technical + IT support |
| System | Yakult Scanner Mobile Application |
| Version | 1.0 |
| Effective date | 2026-03-11 |
| Last updated | 2026-03-11 |

## 1. Purpose

This document explains how the scanner determines scan mode, how QR payloads are interpreted, how serial values are normalized, and how mobile-originated changes are synchronized back to the inventory ecosystem.

## 2. Scan Mode Routing

The application keeps a lightweight global routing state through `ScanRouting`.

Defined modes:

- `None`
- `Dispatch`
- `Serial`

Routing purpose:

- prevent Honeywell scans from hijacking the wrong screen
- decide whether a scanned value should open dispatch details or return a serial result
- control when hardware scanning is armed

Routes treated as serial mode:

- `direct_serial_scan`
- `batch_serial_entry`

Routes treated as dispatch mode:

- `home`
- `details/{scannedString}?token={token}`
- `details_readonly/{scannedString}`

Special scanner routes:

- `scanner_device`
- `scanner_camera`

These inherit the intended mode from the previous route.

## 3. Supported Scan Inputs

### 3.1 Camera scan

Used through `EnhancedScannerScreen` with CameraX and ML Kit.

### 3.2 Keyboard-wedge scan

In device-scanner mode, the screen hosts an almost invisible text field to catch serial or QR data as keyboard input.

### 3.3 Honeywell broadcast scan

`HoneywellScanReceiver` listens for:

- `com.honeywell.decode.intent.action.DECODE_DATA`
- `com.example.yakultscanner.SCAN`

Receiver behavior is intentionally restricted by current route and computed scan mode.

### 3.4 Gallery QR extraction

The camera-based scanner flow can also open an image from the gallery and attempt QR extraction from that image.

## 4. QR Payload Interpretation

### 4.1 Token QR format

Preferred dispatch QR format:

- `yakult:set:v1:<guid>`

Behavior:

1. app validates GUID format
2. app calls `api/sets/by-token/{token}`
3. server returns a structured dispatch set
4. set is written to local scan history
5. app opens `details/...` with the resolved payload

This path also enables the deploy action later in the dispatch details screen.

### 4.2 Raw JSON QR

If the QR does not match the token format, the app attempts to treat it as raw JSON for `DispatchSet`.

Behavior:

- app parses the payload locally
- if parsing succeeds, the result is added to local history
- app navigates to dispatch details

Support implication:

- token QR is server-authoritative
- raw JSON QR depends on payload structure already containing usable dispatch information

## 5. Dispatch Update Logic

### 5.1 Local edit state

Each dispatch item is represented through `ItemEditState`.

Tracked fields:

- original item
- status input
- remark input
- edit flag

### 5.2 Issue entry behavior

The active UI behavior is swipe-based:

- user swipes an item
- `ReportIssueDialog` opens
- user enters free-text status and remarks

This is important because the in-app help text still implies an older “tap edit” interaction.

### 5.3 Upload eligibility

An item is uploaded only when:

- it has a changed status, or
- it has a non-empty remark

Items already marked as processed by the backend are excluded from re-upload.

### 5.4 Upload payload

Uploads are sent as `SetUpdatesRequest`:

- `setCode`
- `updatedByUser`
- `updates[]`

Each update contains:

- item serial number
- model number
- previous status
- new status
- remark
- item type

### 5.5 Pending vs processed semantics

The mobile app surfaces two backend states:

- pending updates: uploaded from mobile but not yet processed by the desktop/server flow
- processed updates: already handled by the desktop/server flow

The app does not process those updates locally; it only displays the current server state.

## 6. Deploy Logic

Deploy is a restricted action.

Conditions:

- screen must not be read-only
- a token must be available from token-based scan flow

Deploy sequence:

1. app resolves token to `setId` through `api/dispatch/resolve-token/{token}`
2. app calls `api/dispatch/{setId}` with `PUT`
3. result is shown to the user

Support implication:

- raw JSON-only scans cannot perform deploy
- a user reporting “deploy button disabled” may simply be working from a non-token QR route

## 7. Serial Normalization Logic

Serial normalization rules are shared through `normalizeSerial(...)`:

- trim leading/trailing whitespace
- remove internal whitespace
- uppercase using `Locale.ROOT`

Operational purpose:

- prevent duplicate captures caused by spacing differences
- keep manual entry, camera scan, and hardware scan behavior aligned

## 8. Direct Serial Handoff Logic

Direct serial flow is intended for quick serial transfer to the Windows-side process.

Behavior:

1. user adds one or more normalized serials
2. app sends them sequentially to `api/Items/ReceiveSerialFromMobile`
3. success/failure counts are shown
4. list is cleared only on full success

The logic is intentionally sequential rather than parallel.

Operational implication:

- this is safer for backend and desktop-side consumption
- large batches may feel slower but are easier to reason about during support incidents

## 9. Batch Item Creation Logic

The batch creation flow is more structured than direct serial handoff.

Before submission, the app loads:

- item categories
- conditions
- vendors

Submission combines:

- item metadata
- unit of measure
- purchase details
- optional software/service fields
- list of serials

Output is a `BatchItemsRequest` posted to `api/Items/CreateBatch`.

## 10. Connection State Logic

`ConnectionHealthStore` tracks API interaction health.

Recorded values:

- selected base URL
- last success timestamp
- last failure timestamp
- last HTTP code
- last endpoint
- last error message

This data feeds:

- connection chip
- support diagnostics
- faster issue classification

## 11. Local History Logic

History behavior:

- saved in `scan_history.json`
- max 50 records
- new entries are inserted at the top
- duplicate set codes are collapsed on re-scan

History is local-device convenience data only.

It should not be treated as the official audit record for enterprise operations.

## 12. Support Interpretation Rules

When troubleshooting, use the following logic:

- scan reaches details screen but deploy is unavailable:
  - likely raw JSON path or read-only path
- scan works in camera mode but not Honeywell mode:
  - inspect receiver route arming and device broadcast behavior
- pending updates visible but desktop not changed:
  - mobile upload succeeded; desktop/server processing is the next checkpoint
- direct serial scan succeeds on phone but not in Windows:
  - inspect `api/Items/ReceiveSerialFromMobile` consumer path on the desktop side

## 13. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
