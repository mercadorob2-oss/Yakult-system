# SOP / WI: Yakult Scanner - Technical Design

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

This document explains the technical structure of the Yakult Scanner Android project, including runtime initialization, navigation, feature boundaries, API integration, local persistence, and notable design constraints.

## 2. Architecture Style

The application uses a mixed architecture:

- Jetpack Compose for the active UI
- Hilt for dependency injection in selected features
- Retrofit/OkHttp for HTTP integration
- State managed partly in ViewModels and partly directly in Composable screens
- SharedPreferences and internal file storage for local persistence

This is not a fully clean-layered implementation. Some features are properly routed through ViewModels and repositories, while other screens call `ApiClient.service` directly.

## 3. Startup Sequence

### 3.1 Application bootstrap

`YakultScannerApp` initializes API settings during `Application.onCreate()`.

Result:

- persisted or flavor-based base URL becomes available early
- `ConnectionHealthStore` is updated with the selected base URL

### 3.2 Activity bootstrap

`MainActivity` performs the following:

1. restores the persisted user session via `UserSession.init(...)`
2. registers `HoneywellScanReceiver`
3. mounts the Compose app through `AppNavigator()`
4. requests camera permission if not already granted

### 3.3 Navigation bootstrap

`AppNavigator` creates a single `NavController` and sets the start destination to `login`.

Important support note:

- the session is restored for token availability and UI context
- the app does not currently auto-skip the login screen when a prior session exists

## 4. Navigation Model

The active routes are:

- `login`
- `register`
- `root_home`
- `serial_scan_home`
- `home`
- `scanner_device`
- `scanner_camera`
- `details/{scannedString}?token={token}`
- `details_readonly/{scannedString}`
- `direct_scan_details/{scannedString}`
- `history`
- `report_issues`
- `uploaded/{setCode}`
- `help`
- `app_info`
- `batch_serial_entry`
- `direct_serial_scan`
- `processed_updates`
- `pending_updates`

Bottom navigation is hidden on login, register, and scanner capture screens.

## 5. Feature Areas

### 5.1 Authentication and session

Files:

- `LoginScreen.kt`
- `RegisterScreen.kt`
- `UserSession.kt`

Behavior:

- login calls `api/auth/login`
- register calls `api/auth/register`
- successful login stores user ID, username, display name, email, token, and token expiry in SharedPreferences
- token is later attached to API requests automatically

### 5.2 Connection settings and diagnostics

Files:

- `settings/ApiSettings.kt`
- `ConnectionHealthStore.kt`
- `ui/components/ConnectionStatusChip.kt`
- `ui/components/ConnectionSettingsDialog.kt`
- `ui/components/Connectivity.kt`

Behavior:

- effective API base URL is either:
  - the locally stored override, or
  - the flavor default from `BuildConfig.API_BASE_URL`
- health checks use `/api/health`
- the connection chip surfaces last-success timing, current host, and basic availability state

### 5.3 Dispatch scanning workflow

Files:

- `ui/screens/HomeScreen.kt`
- `EnhancedScannerScreen.kt`
- `HoneywellScanReceiver.kt`
- `DispatchDetailsScreen.kt`
- `viewmodels/DispatchDetailsViewModel.kt`

Behavior:

- supports camera scanning
- supports keyboard-wedge hardware scanning
- supports Honeywell broadcast scanning
- supports gallery image QR extraction for camera workflow
- dispatch QR can be:
  - token-based (`yakult:set:v1:<guid>`), or
  - raw JSON payload

Token-based QR is the more controlled path because it resolves the set from the server.

### 5.4 Serial workflows

Files:

- `ui/screens/SerialScanHomeScreen.kt`
- `DirectSerialScanScreen.kt`
- `BatchSerialEntryScreen.kt`
- `viewmodels/BatchEntryViewModel.kt`

Behavior:

- direct serial flow sends serials to the Windows integration endpoint
- batch entry flow creates full inventory items through `api/Items/CreateBatch`
- serial input can come from camera scan, hardware input, or manual typing

### 5.5 Monitoring and history

Files:

- `ui/screens/PendingUpdatesScreen.kt`
- `ui/screens/ProcessedUpdatesScreen.kt`
- `ui/screens/ReportIssueHistoryScreen.kt`
- `ui/screens/HistoryScreen.kt`
- `UploadedItemsScreen.kt`

Behavior:

- mobile-generated updates can be reviewed by pending and processed state
- history is local to the device and not a server-side audit trail
- uploaded items view reads server-side updates for a specific set code

### 5.6 PDF and signatures

Files:

- `utils/PdfUtils.kt`
- `ui/components/SignaturePad.kt`

Behavior:

- PDF is generated on-device
- requester and handler signatures are captured as custom bitmap output
- PDFs are saved to Downloads on newer Android versions through `MediaStore`
- older Android behavior uses app-scoped external storage with `FileProvider`

## 6. Data Flow

### 6.1 Authentication flow

1. user enters credentials
2. mobile app calls `api/auth/login`
3. API returns token and user details
4. token is stored in `yakult_scanner_session`
5. token is attached as `Authorization: Bearer <token>` on later requests

### 6.2 Dispatch scan flow

1. user enters scanner screen
2. scanner returns QR payload
3. app determines whether payload is:
   - token QR, or
   - raw JSON QR
4. app opens dispatch details
5. user marks changes or issues
6. app uploads `SetUpdatesRequest`
7. desktop side later processes those updates

### 6.3 Direct serial handoff flow

1. user scans or types serials
2. serials are normalized locally
3. app posts each serial to `api/Items/ReceiveSerialFromMobile`
4. Windows-side flow is expected to consume the serial

### 6.4 Batch item creation flow

1. app loads categories, conditions, and vendors from the API
2. user fills out batch metadata and serial list
3. app sends `BatchItemsRequest`
4. API creates multiple items in one submission

## 7. Local Persistence Design

### 7.1 SharedPreferences

- `yakult_scanner_session`
  - user identity and Bearer token
- `yakult_api_settings`
  - manually overridden base URL
- `yakult_scanner_pins`
  - pinned set codes

### 7.2 Internal file storage

- `scan_history.json`
  - capped to 50 entries
  - most-recent-first order
  - duplicate set codes are de-duplicated on insert

### 7.3 In-memory state

- `ConnectionHealthStore`
  - last success time
  - last failure time
  - endpoint and code details
- `ScanRouting`
  - current scan mode
  - current route
  - Honeywell armed state

## 8. Error Handling Model

Most network calls are wrapped with `safeApiCall(...)`, which maps failures into:

- `Success`
- `HttpError`
- `NetworkError`
- `UnknownError`

Operational effects:

- connection diagnostics are updated centrally
- many UI screens render an `ApiFailurePanel`
- not all screens share one common retry pattern

## 9. Dependency Injection Boundaries

Hilt is used, but not universally:

- repository-backed flow:
  - `BatchEntryViewModel`
  - `DispatchDetailsViewModel`
- direct service usage inside screens:
  - dashboard counts
  - pending/processed update screens
  - history-related badge refreshes
  - scanner-side token resolution in some paths

Support implication:

- issue ownership is easier to trace in ViewModel-backed modules
- runtime behavior is more spread out in direct-call screens

## 10. Legacy / Non-Primary Code

The following files appear to represent an earlier XML-based UI approach:

- `DetailsActivity.kt`
- `ItemAdapter.kt`

These are present in the repository but do not participate in the active Compose route map.

`ProfileScreen.kt` exists as an empty file and is not part of the active navigation.

## 11. Design Constraints

Current codebase constraints with operational impact:

- cleartext HTTP is enabled
- API host defaults are hardcoded to an internal IP
- no offline transaction queue exists
- no encrypted token storage is implemented
- release signing configuration is not defined in Gradle
- item issue status entry is free-text rather than fully controlled

## 12. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
