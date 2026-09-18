# Reference: Yakult Scanner - Security Notes and Known Gaps

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / Development |
| Audience | IT technical + developers + support leads |
| System | Yakult Scanner Mobile Application |
| Version | 1.0 |
| Effective date | 2026-03-11 |
| Last updated | 2026-03-11 |

## 1. Purpose

This document records current technical risks, security-relevant behaviors, and known design gaps observed in the Yakult Scanner codebase.

## 2. Security-Relevant Behaviors

### 2.1 Cleartext HTTP enabled

The manifest currently allows:

- `android:usesCleartextTraffic="true"`

Implication:

- HTTP traffic may traverse internal networks without TLS protection

### 2.2 Hardcoded internal default endpoints

Default flavor URLs point to a fixed internal IP:

- `192.168.12.9`

Implication:

- configuration drift is possible
- internal infrastructure changes require endpoint maintenance or user override

### 2.3 Token stored in SharedPreferences

Session token is stored in local preferences.

Implication:

- token storage is not encrypted by default in this codebase

### 2.4 Exported Honeywell receiver

The manifest exports `HoneywellScanReceiver`.

Implication:

- external scan intents can reach the app
- receiver gating depends on current route and scan mode logic

### 2.5 No certificate pinning

The API client does not implement certificate pinning or HTTPS enforcement.

## 3. Operational Gaps

### 3.1 No offline queue

If the network or API is unavailable:

- uploads fail
- serial handoff fails
- batch item creation fails

The app does not queue failed transactions for later replay.

### 3.2 Start route always returns to login

Even with a restored session, the app starts at `login`.

Implication:

- user convenience is reduced
- support must distinguish between “session restored” and “auto-login”

### 3.3 Free-text status entry

Item issue status entry is not fully controlled by a strict dropdown.

Implication:

- inconsistent status vocabulary may occur
- downstream analytics and support reports may be harder to normalize

### 3.4 Help text drift

In-app instructional text still describes an older interaction pattern in some places.

Implication:

- support may receive user confusion around how issue entry is initiated

### 3.5 Release signing not defined in source

No signing config is declared in Gradle.

Implication:

- release packaging requires external signing steps or local IDE configuration

## 4. Code Hygiene Findings

### 4.1 Mixed architecture

Some screens use ViewModels and repositories; others call the API directly.

Impact:

- behavior is more difficult to standardize
- support investigation may require tracing multiple styles of implementation

### 4.2 Legacy files remain in source tree

Legacy XML-era files remain in the project:

- `DetailsActivity.kt`
- `ItemAdapter.kt`

Impact:

- new readers may mistake them for active runtime paths

### 4.3 Empty placeholder file

- `ui/screens/ProfileScreen.kt`

Impact:

- repository contains incomplete or future-placeholder artifact

## 5. Recommended Documentation Controls

Until the code changes, the following should be documented operationally:

- approved environment host/port values
- approved status vocabulary for item updates
- allowed device models and scanner modes
- escalation rule when mobile succeeds but desktop does not process updates
- release-signing owner and process

## 6. Recommended Technical Improvements

The following are reasonable future hardening actions:

1. move to HTTPS-only API transport
2. encrypt token storage
3. standardize endpoint access behind repositories/use-cases
4. convert free-text status to controlled values
5. define release signing configuration and release checklist
6. remove or archive legacy inactive files

## 7. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
