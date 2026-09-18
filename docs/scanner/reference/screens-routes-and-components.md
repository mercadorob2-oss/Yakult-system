# Reference: Yakult Scanner - Screens, Routes, and Components

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

This document lists the primary active screens, route names, and core components used by the scanner application.

## 2. Route Map

| Route | Purpose |
| --- | --- |
| `login` | Sign in |
| `register` | Create account |
| `root_home` | Main dashboard |
| `serial_scan_home` | Serial workflow dashboard |
| `home` | Dispatch scan home |
| `scanner_device` | Hardware scanner capture screen |
| `scanner_camera` | Camera scanner capture screen |
| `details/{scannedString}?token={token}` | Dispatch details with optional token |
| `details_readonly/{scannedString}` | Read-only history reopen |
| `direct_scan_details/{scannedString}` | Return scanned serial to caller |
| `history` | Local scan history |
| `report_issues` | Pending issue grouping by set |
| `uploaded/{setCode}` | Uploaded updates for one set |
| `help` | In-app how-to information |
| `app_info` | Diagnostics and local maintenance |
| `batch_serial_entry` | Batch item creation |
| `direct_serial_scan` | Quick serial handoff |
| `processed_updates` | Processed mobile updates |
| `pending_updates` | Pending mobile updates |

## 3. Primary Active Screens

### 3.1 Authentication

- `LoginScreen.kt`
- `RegisterScreen.kt`

### 3.2 Dashboard / navigation

- `ui/screens/RootHomeScreen.kt`
- `ui/components/CustomNavigationBar.kt`

### 3.3 Dispatch workflow

- `ui/screens/HomeScreen.kt`
- `EnhancedScannerScreen.kt`
- `DispatchDetailsScreen.kt`
- `UploadedItemsScreen.kt`

### 3.4 Serial workflow

- `ui/screens/SerialScanHomeScreen.kt`
- `DirectSerialScanScreen.kt`
- `BatchSerialEntryScreen.kt`

### 3.5 Monitoring / support

- `ui/screens/PendingUpdatesScreen.kt`
- `ui/screens/ProcessedUpdatesScreen.kt`
- `ui/screens/ReportIssueHistoryScreen.kt`
- `ui/screens/HistoryScreen.kt`
- `HelpAndInfoScreens.kt`

## 4. Important Supporting Components

| Component | Purpose |
| --- | --- |
| `ConnectionStatusChip` | Displays connection state and settings access |
| `ConnectionSettingsDialog` | Edits API host and port |
| `ConfirmSensitiveActionSheet` | Confirms deploy/upload actions |
| `SignaturePad` / `SignatureDialog` | Captures requester and handler signatures |
| `ReportIssueDialog` | Captures item status and remark |
| `ScannerOverlay` | Camera scanner visual overlay |
| `ScannerControls` | Camera scanner control bar |

## 5. ViewModels

| ViewModel | Purpose |
| --- | --- |
| `DispatchDetailsViewModel` | Parses dispatch payload, tracks edits, uploads set updates |
| `BatchEntryViewModel` | Loads item masters, validates form, submits batch items |

## 6. State / Utility Objects

| Object | Purpose |
| --- | --- |
| `UserSession` | Local session holder and token access |
| `ApiSettings` | Effective API base URL |
| `ApiClient` | Retrofit service builder and auth interceptor |
| `ConnectionHealthStore` | In-memory connection diagnostics |
| `ScanRouting` | Determines dispatch vs serial scan mode |
| `PinnedSetsStore` | Local pinned set storage |
| `ScanHistory` | Local recent-scan history persistence |

## 7. Legacy or Non-Primary Files

These are present but not part of the primary Compose route map:

- `DetailsActivity.kt`
- `ItemAdapter.kt`

Additional note:

- `ui/screens/ProfileScreen.kt` exists as an empty file and is not currently part of the active app flow.

## 8. Bottom Navigation Mapping

Current custom navigation items:

| Nav item title | Route |
| --- | --- |
| `Search` | `history` |
| `Explore` | `home` |
| `Home` | `root_home` |
| `Quick Scan` | `direct_serial_scan` |
| `Profile` | `app_info` |

## 9. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
