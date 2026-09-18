# SOP / WI: Yakult Scanner Mobile Application - Overview

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support / Mobile Support |
| Audience | IT technical + IT support + developers |
| System | Yakult Scanner Mobile Application |
| Location | `Latest_sys/YakultScanner` |
| Version | 1.0 |
| Effective date | 2026-03-11 |
| Last updated | 2026-03-11 |

## 1. Purpose

This document defines the scope, responsibilities, dependencies, and operating context of the Yakult Scanner mobile subsystem. It serves as the baseline reference for support teams before working on setup, incident handling, release preparation, or technical troubleshooting.

## 2. Scope

Included:

- Android application source code under `Latest_sys/YakultScanner`
- Authentication and session handling
- Dispatch QR scanning workflows
- Serial capture workflows
- Pending/processed mobile update visibility
- Batch item creation from mobile
- Mobile-to-Windows serial handoff
- Local storage used by the mobile app
- PDF generation and signature capture features
- Honeywell hardware scan integration

Excluded:

- WinForms desktop application behavior outside the mobile integration points
- IIS API implementation details outside the endpoints consumed by the mobile app
- SQL Server schema details outside what the mobile app reads/writes through the API
- MDM, store publishing, or enterprise app distribution tooling not present in the repository

## 3. System Definition

Yakult Scanner is an Android mobile companion application used to:

1. Authenticate a mobile user against the Yakult inventory API
2. Scan a dispatch set QR code using camera or hardware scanner
3. Review dispatch set and item details on the device
4. Report item status changes and remarks back to the backend
5. View pending and processed updates created from mobile
6. Send serial numbers to the Windows workflow
7. Create batch inventory items from scanned serials
8. Generate a PDF dispatch form with captured signatures

The application is not designed as an offline-first system. Core transactions depend on API availability.

## 4. Repository Ownership Map

Primary project path:

- `Latest_sys/YakultScanner`

Primary runtime areas:

- App bootstrap: `app/src/main/java/com/example/yakultscanner/YakultScannerApp.kt`
- Main activity: `app/src/main/java/com/example/yakultscanner/MainActivity.kt`
- Navigation: `app/src/main/java/com/example/yakultscanner/navigation/AppNavigator.kt`
- API client: `app/src/main/java/com/example/yakultscanner/api/ApiClient.kt`
- Scanner UI: `app/src/main/java/com/example/yakultscanner/EnhancedScannerScreen.kt`
- Dispatch details: `app/src/main/java/com/example/yakultscanner/DispatchDetailsScreen.kt`
- Serial workflows: `app/src/main/java/com/example/yakultscanner/DirectSerialScanScreen.kt`, `app/src/main/java/com/example/yakultscanner/BatchSerialEntryScreen.kt`

Legacy code present in the repository but not part of the active Compose navigation:

- `app/src/main/java/com/example/yakultscanner/DetailsActivity.kt`
- `app/src/main/java/com/example/yakultscanner/ItemAdapter.kt`

## 5. Responsibilities

### 5.1 IT Support / Mobile Support

- Install or update the application on support or operational devices
- Verify API connectivity and correct environment target
- Verify camera scanning and hardware scanning behavior
- Perform first-level troubleshooting for login, scan, upload, and synchronization issues
- Collect operational evidence and screenshots for escalation

### 5.2 IT Technical / Application Support

- Validate integration with the backend API
- Confirm correct build flavor and base URL settings
- Reproduce field issues using device diagnostics and API health checks
- Support controlled build generation and deployment to test or production devices

### 5.3 Developers

- Maintain the Android codebase and feature behavior
- Update API contracts and corresponding mobile models
- Correct defects in scan routing, upload logic, and local storage handling
- Maintain release packaging and build configuration

### 5.4 Backend Owners

- Keep required endpoints reachable and compatible
- Maintain token resolution, authentication, dispatch, set update, and item master endpoints
- Resolve server-side failures affecting mobile functions

## 6. Environments and Build Flavors

The scanner project defines two product flavors:

- `dev`
  - Application ID suffix: `.dev`
  - Default API base URL: `http://192.168.12.9:80/`
- `prod`
  - Default API base URL: `http://192.168.12.9:7326/`

The stored API base URL can be overridden at runtime through the app settings dialog. The runtime override is persisted locally on the device and takes precedence over the build default.

Documented environments currently inferred from the repository:

- `DEV`
- `PROD`

No separate `UAT` flavor or environment is defined in the Android project.

## 7. Platform and Technical Dependencies

The current Android project is configured as follows:

- Android application namespace: `com.example.yakultscanner`
- `compileSdk`: `34`
- `minSdk`: `24`
- `targetSdk`: `34`
- Kotlin + Compose UI
- Hilt dependency injection
- Retrofit + OkHttp networking
- CameraX camera pipeline
- Google ML Kit barcode scanning
- FileProvider-based PDF sharing

Device-side requirements:

- Android device running API level 24 or higher
- Reachable network path to the configured API server
- Camera permission for camera-based scanning
- Honeywell-compatible broadcast scanning only when supported hardware is used

## 8. Operational Summary

Primary user journey:

1. User signs in through the mobile login screen
2. User lands on `root_home`
3. User chooses either:
   - Dispatch scanning workflow, or
   - Serial workflow dashboard
4. User completes one of the following:
   - Scan dispatch QR and upload issues/status changes
   - Send serial numbers to the Windows workflow
   - Create batch items through the API
5. Support can inspect pending/processed updates and diagnostics screens

## 9. Current Operating Characteristics

Current behavior observed in code:

- User session is persisted locally
- App startup still routes to `login` even when a previous session exists
- Local history stores up to `50` recent scan entries
- Pinned sets are stored locally per device
- No offline queue exists for uploads or serial handoff
- API requests use a saved Bearer token when available
- PDF output is saved locally on the device

## 10. Records and Local Data

The mobile application stores operational data locally in the following ways:

- Shared preferences
  - Session: `yakult_scanner_session`
  - API settings: `yakult_api_settings`
  - Pinned sets: `yakult_scanner_pins`
- Internal file storage
  - Scan history file: `scan_history.json`
- Downloads / app-scoped files
  - Generated dispatch PDFs

## 11. Related Documents

- How-to: `docs/scanner/how-to/admin-runbook.md`
- How-to: `docs/scanner/how-to/configure-api-connection.md`
- How-to: `docs/scanner/how-to/troubleshooting.md`
- Reference: `docs/scanner/reference/api-and-storage.md`
- Reference: `docs/scanner/reference/screens-routes-and-components.md`
- Reference: `docs/scanner/reference/security-and-known-gaps.md`
- Explanation: `docs/scanner/explanation/technical-design.md`
- Explanation: `docs/scanner/explanation/scanning-and-sync-logic.md`

## 12. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial scanner documentation baseline |
