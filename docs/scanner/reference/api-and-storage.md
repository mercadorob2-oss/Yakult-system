# Reference: Yakult Scanner - API and Storage

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / Development |
| Audience | IT technical + developers |
| System | Yakult Scanner Mobile Application |
| Version | 1.0 |
| Effective date | 2026-03-11 |
| Last updated | 2026-03-11 |

## 1. Purpose

This reference lists the mobile app’s known API endpoints, request/response areas, and device-local storage mechanisms.

## 2. API Endpoints Consumed by the Mobile App

### 2.1 Authentication

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `api/auth/login` | `POST` | Sign in and obtain token |
| `api/auth/register` | `POST` | Register a mobile account |

### 2.2 Dispatch and set updates

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `api/SetUpdates/Upload` | `POST` | Upload item updates from mobile |
| `api/SetUpdates` | `GET` | Read updates for a set code |
| `api/SetUpdates/PendingCount` | `GET` | Get dashboard counts |
| `api/SetUpdates/Processed` | `GET` | List processed updates |
| `api/SetUpdates/Pending` | `GET` | List pending updates |

### 2.3 Item master and serial

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `api/Items/CreateBatch` | `POST` | Create multiple items from serial list |
| `api/Items/ReceiveSerialFromMobile` | `POST` | Hand off serial to desktop-related flow |
| `api/Items/Categories` | `GET` | Load item categories |
| `api/Items/Conditions` | `GET` | Load condition list |
| `api/Items/Vendors` | `GET` | Load vendor list |

### 2.4 Token and dispatch handling

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `api/sets/by-token/{token}` | `GET` | Resolve token QR to dispatch set |
| `api/dispatch/{setId}` | `PUT` | Deploy a set |
| `api/dispatch/resolve-token/{token}` | `GET` | Resolve token to set ID |

### 2.5 Connectivity check

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `api/health` | `GET` | Basic API reachability test |

## 3. Primary DTOs Used by Mobile

### 3.1 Authentication DTOs

- `LoginRequest`
- `LoginResponse`
- `RegisterRequest`
- `RegisterResponse`

### 3.2 Dispatch DTOs

- `DispatchSet`
- `DispatchItem`
- `SetItemUpdateEntry`
- `SetUpdatesRequest`
- `SetUpdatesResponse`
- `SetItemUpdateDto`

### 3.3 Batch item DTOs

- `BatchItemRequest`
- `BatchItemsRequest`
- `BatchItemsResponse`
- `ItemCategoryDto`
- `ConditionDto`
- `VendorDto`

### 3.4 Serial handoff DTOs

- `SerialFromMobileRequest`
- `SerialFromMobileResponse`

## 4. Authorization Behavior

When `UserSession.authToken` exists, the mobile app adds:

- `Authorization: Bearer <token>`

The header is applied automatically by the OkHttp interceptor in `ApiClient`.

## 5. Timeouts

Configured OkHttp client timeouts:

- connect timeout: `10` seconds
- read timeout: `30` seconds
- write timeout: `30` seconds
- call timeout: `35` seconds

## 6. Logging Behavior

HTTP logging:

- enabled at `BASIC` level in debug builds
- disabled in non-debug builds

Authorization header is redacted in logging.

## 7. SharedPreferences Keys

### 7.1 Session store

Preferences file:

- `yakult_scanner_session`

Keys:

- `user_id`
- `username`
- `display_name`
- `email`
- `token`
- `expires_utc`

### 7.2 API settings store

Preferences file:

- `yakult_api_settings`

Key:

- `api_base_url`

### 7.3 Pinned sets store

Preferences file:

- `yakult_scanner_pins`

Key:

- `pinned_set_codes`

## 8. File-Based Storage

### 8.1 Scan history

Internal file:

- `scan_history.json`

Characteristics:

- local to the device
- stores recent dispatch scans
- capped to 50 entries

### 8.2 Generated PDF

Saved through:

- `MediaStore.Downloads` on Android 10+
- app-scoped external Downloads path on older Android

Shared using:

- `${applicationId}.fileprovider`

## 9. Connection Health Snapshot Fields

In-memory fields maintained by `ConnectionHealthStore`:

- `baseUrl`
- `lastSuccessAtMs`
- `lastFailureAtMs`
- `lastHttpCode`
- `lastEndpoint`
- `lastErrorMessage`

## 10. Scan History Entry Fields

`ScanHistoryEntry` contains:

- `timestamp`
- `rawJson`
- `setCode`
- `employee`
- `status`

## 11. Operational Meaning of Local Data

Use local data for convenience only:

- session: local auth state
- pinned sets: user convenience
- scan history: recent navigation convenience
- connection health: local diagnostics

Do not treat these as authoritative enterprise audit records.

## 12. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
