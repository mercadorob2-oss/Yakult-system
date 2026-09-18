# Work Instruction: Yakult Scanner - Troubleshooting

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | IT technical + IT support |
| System | Yakult Scanner Mobile Application |
| Version | 1.0 |
| Effective date | 2026-03-11 |
| Last updated | 2026-03-11 |

## 1. Purpose

This guide provides a structured troubleshooting path for the most common mobile scanner failures.

## 2. Troubleshooting Principles

Always classify the problem first:

1. install/startup problem
2. connection problem
3. authentication problem
4. scanning/capture problem
5. dispatch loading problem
6. upload/sync problem
7. serial handoff problem
8. batch item creation problem

Do not start with random reinstalls unless the issue has already been narrowed to device corruption or app package inconsistency.

## 3. First Checks

Before deeper troubleshooting:

1. identify current host and port
2. run **Test Connection**
3. capture app version from diagnostics page
4. identify the exact route/screen
5. identify input type:
   - camera
   - hardware wedge
   - Honeywell broadcast
   - manual entry

## 4. Symptom-Based Troubleshooting

### 4.1 App will not open

Check:

- install package validity
- Android version compatibility
- whether issue affects one device or all devices

Next steps:

- reinstall app
- test on second device
- if reproducible across builds, escalate to development

### 4.2 Test Connection fails

Likely causes:

- wrong host/IP
- wrong port
- device off the expected network
- API server offline

Actions:

1. verify host and port
2. compare with environment standard
3. test from a second device on same network
4. escalate as backend/network incident if repeated

### 4.3 Login fails but health test succeeds

Likely causes:

- invalid credentials
- auth endpoint issue
- token generation or auth contract issue

Actions:

1. retry with known-good account
2. confirm whether all users are affected
3. capture HTTP behavior if available from backend

### 4.4 Camera scanner opens but does not detect QR

Check:

- camera permission granted
- QR clarity and size
- room lighting
- whether test QR is valid

Actions:

1. retry with a known-good QR
2. use gallery import if available
3. test hardware scan path if device supports it

### 4.5 Hardware scanner does nothing

Check:

- is the app on a scanner-armed route
- is the device acting as wedge input or Honeywell broadcast input
- does the hardware work in another app or test field

Actions:

1. test `scanner_device`
2. test direct serial route
3. verify device broadcast profile for Honeywell

Interpretation:

- if hardware works elsewhere but not here, route arming or receiver behavior is suspect

### 4.6 QR scans but dispatch details do not load

Possible causes:

- invalid token QR format
- backend token resolution failure
- malformed raw JSON QR
- API connectivity issue after scan

Actions:

1. determine whether QR is token-based or raw JSON
2. test another known-good QR
3. confirm `/api/health`
4. check whether failure is HTTP or parsing related

### 4.7 Upload Changes fails

Possible causes:

- no pending edits
- expired/invalid session token
- endpoint failure
- network interruption

Actions:

1. confirm at least one real item change exists
2. log out and log back in
3. retry upload
4. inspect whether the set later appears under uploaded items

### 4.8 Pending screen shows item but desktop process does not complete

Interpretation:

- mobile upload likely succeeded
- failure point is now downstream of the mobile app

Next checkpoint:

- desktop/API processing path

### 4.9 Direct serial handoff fails

Possible causes:

- serial normalization issue
- endpoint failure
- downstream Windows consumer issue

Actions:

1. test one known-good serial
2. verify connection health
3. compare result across devices
4. escalate to desktop/API owner if mobile request succeeds but desktop behavior is absent

### 4.10 Batch item creation fails

Possible causes:

- missing required fields
- categories/conditions/vendors not loading
- backend validation failure

Actions:

1. confirm required fields
2. confirm dropdown data loaded from API
3. retry with smallest valid dataset
4. capture returned HTTP or error message

## 5. Device Cleanup Actions

Use only when justified:

- clear local cache from diagnostics page
- log out and back in
- re-enter API connection
- reinstall app

Remember:

- cache clearing removes scan history and pinned sets only
- server-side updates are not removed by cache clearing

## 6. Escalation Package

When escalating, include:

- device model
- Android version
- app version
- package name
- API base URL
- exact failing screen
- exact operation attempted
- screenshots
- whether failure is reproducible on another device
- whether health check succeeds
- affected set code or serial

## 7. Known Current Gaps Relevant to Troubleshooting

- help text does not fully match current swipe-based issue entry behavior
- no offline queue means transient network failures stop transactional workflows
- cleartext HTTP means network infrastructure issues can affect behavior more directly
- local history is not the official audit source

## 8. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
