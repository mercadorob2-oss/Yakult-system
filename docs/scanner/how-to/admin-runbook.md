# Work Instruction: Yakult Scanner - Admin / Support Runbook

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

This runbook provides the day-to-day operational procedures used by IT support and technical personnel to keep the Yakult Scanner mobile subsystem functioning correctly in the field.

## 2. Scope

This runbook covers:

- app installation and basic readiness
- API target verification
- login and dashboard checks
- scanner workflow checks
- serial workflow checks
- device diagnostics and evidence collection
- support escalation criteria

## 3. Preconditions / Requirements

### 3.1 Device requirements

- Android device with API level 24 or higher
- functioning camera for camera-based scanning
- stable Wi-Fi or network path to the target API host

### 3.2 Access requirements

- valid mobile user account
- reachable API environment
- test dispatch QR and test serial data when performing operational checks

### 3.3 Optional hardware requirements

- Honeywell scanner-capable device when hardware broadcast scanning is required

## 4. Operating Policy

Current technical facts that support should assume unless formally changed:

- build flavors: `dev` and `prod`
- default `dev` API URL: `http://192.168.12.9:80/`
- default `prod` API URL: `http://192.168.12.9:7326/`
- runtime base URL override is allowed and persisted locally
- app stores session token locally
- local history is limited to 50 records
- no offline transaction queue is implemented
- deploy action requires token-based dispatch scanning

## 5. Daily / Shift Checklist

Perform the following at the start of a support shift or after a new deployment.

### 5.1 Confirm app launches

1. Open the mobile app.
2. Confirm the login screen loads.
3. Confirm the app does not immediately crash.

### 5.2 Confirm API target

1. Open connection settings from login or connection chip.
2. Review host and port.
3. Run **Test Connection**.

Acceptance:

- `/api/health` responds successfully

### 5.3 Confirm user authentication

1. Sign in with a valid support account.
2. Confirm `root_home` opens.

Acceptance:

- login works
- greeting, dashboard counters, and modules appear

### 5.4 Confirm scanner readiness

1. Open Yakult Scanner module.
2. Open device scan or camera scan.
3. Confirm the scanning screen opens properly.

Acceptance:

- camera mode opens and camera preview appears
- device scanner mode opens and shows hardware-ready state

### 5.5 Confirm serial workflow readiness

1. Open Serial Dashboard.
2. Open Direct Serial Scan.
3. Open Add Items by Serial.

Acceptance:

- both screens load without crash
- form controls are responsive

### 5.6 Confirm pending and processed screens

1. Open Pending Updates.
2. Open Processed Updates.

Acceptance:

- data loads or a controlled API failure panel is shown

## 6. Routine Support Procedures

### 6.1 Reset API endpoint

Use:

- `docs/scanner/how-to/configure-api-connection.md`

### 6.2 Validate dispatch flow

1. Scan a test dispatch QR.
2. Confirm dispatch details load.
3. If token-based QR is used, confirm deploy button is available.
4. If needed, upload one test item update.

### 6.3 Validate direct serial handoff

1. Open Direct Serial Scan.
2. Capture one serial.
3. Send it to Windows.
4. Confirm success message or capture failure evidence.

### 6.4 Validate batch item entry

1. Open Add Items by Serial.
2. Confirm categories, conditions, and vendors load.
3. Add minimal valid test data.
4. Save batch items only in approved non-production environments unless specifically authorized.

### 6.5 Collect diagnostics before escalation

Required capture set:

- screenshot of error
- screenshot of diagnostics page
- API host/port in use
- route or screen name involved
- whether issue occurs in camera, wedge, or Honeywell mode
- test QR or set code involved
- exact time of failure

## 7. Device Maintenance Tasks

### 7.1 Clear local cache

From **App info & diagnostics**:

- use **Clear cache**

Effect:

- removes pinned sets
- removes local scan history
- does not remove server-side data

### 7.2 Re-login

If token/session is suspected to be stale:

1. log out
2. sign in again
3. re-test failing workflow

### 7.3 Re-test network independently

When API calls fail:

1. open connection settings
2. run **Test Connection**
3. compare with actual app workflow result

Interpretation:

- health succeeds but feature fails:
  - likely endpoint-specific issue
- health fails:
  - likely host/port/network/server issue

## 8. Evidence / Records

Operational evidence sources:

- app diagnostics page
- pending updates list
- processed updates list
- uploaded items list for a set
- device-local scan history
- generated PDF in Downloads
- server/API logs outside the mobile app

## 9. Escalation Path

Recommended support escalation path for this subsystem:

1. IT Support
2. Supervisor
3. Development / API owner

If the issue is clearly backend-only, escalate with API evidence instead of continuing repeated device tests.

## 10. Escalation Criteria

Escalate when:

- login fails across multiple devices with correct credentials
- `/api/health` passes but business endpoints fail consistently
- camera mode and hardware mode both fail on valid QR
- token QR resolves incorrectly
- pending uploads never become processed and issue is not device-local
- batch master data endpoints fail (`categories`, `conditions`, `vendors`)
- direct serial handoff reaches API but downstream Windows behavior fails

## 11. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
