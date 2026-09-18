# Tutorial: Yakult Scanner Support Onboarding

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | New IT support / technical support personnel |
| System | Yakult Scanner Mobile Application |
| Version | 1.0 |
| Effective date | 2026-03-11 |
| Last updated | 2026-03-11 |

## 1. Purpose

This tutorial gives a new support person a guided first pass through the Yakult Scanner subsystem so they can understand what the app does, what “healthy” behavior looks like, and what evidence to gather when something fails.

## 2. Prerequisites

Before starting, make sure you have:

- an Android test device
- a build or APK of the scanner application
- reachable network access to the target API host
- at least one valid mobile user account
- at least one dispatch QR or token QR for test use
- permission to use the backend environment being tested

## 3. Learning Goal

At the end of this tutorial, the support person should be able to:

- sign in and verify API connectivity
- switch API host/port if needed
- complete a dispatch scan
- complete a serial scan
- identify where pending and processed mobile updates appear
- collect the correct support evidence for escalation

## 4. Step-by-Step Walkthrough

### Step 1: Confirm app installation

1. Install the APK or launch the debug build on a device.
2. Open the app.
3. Confirm the login screen appears.

Expected result:

- app opens without crash
- login screen is visible
- settings icon is visible in the upper area

### Step 2: Verify API connection target

1. Tap the settings icon on the login screen.
2. Review the current server host and port.
3. If needed, adjust the values for the target environment.
4. Tap **Test Connection**.
5. Confirm the app reports successful API health response.
6. Save settings.

Expected result:

- test connection succeeds
- support person understands whether the app is pointing to `DEV` or `PROD`

### Step 3: Log in

1. Enter a valid username and password.
2. Tap **Sign In**.

Expected result:

- app navigates to `root_home`
- user greeting and dashboard cards are visible
- no authentication error is shown

### Step 4: Review dashboard modules

From `root_home`, identify:

- processed count
- pending count
- Yakult Scanner module
- Serial Dashboard module
- connection status chip

Expected result:

- support person understands where the main operational entry points are

### Step 5: Perform a dispatch scan

1. Open the Yakult Scanner module.
2. Choose either:
   - **New Device Scan**, or
   - **Use Phone Camera**
3. Scan a known dispatch QR.

Expected result:

- dispatch details screen opens
- set information is visible
- items are visible
- scan is added to local history

### Step 6: Perform an issue/update upload

1. On a dispatch details screen, swipe one item.
2. Enter a new status and remark.
3. Save the change.
4. Tap **Upload Changes**.
5. Confirm upload succeeds.

Expected result:

- upload success message appears
- the same set appears under pending issues or uploaded items

### Step 7: Perform a direct serial scan

1. Return to `root_home`.
2. Open **Serial Dashboard**.
3. Open **Direct Serial Scan**.
4. Scan or type one serial.
5. Add it to the list.
6. Tap **Send Serial(s) to Windows**.

Expected result:

- app attempts sequential handoff to the server
- success or failure count is shown clearly

### Step 8: Review operational lists

Open the following:

- `pending_updates`
- `processed_updates`
- `history`
- `report_issues`

Expected result:

- support person understands which data is server-side and which is device-local

### Step 9: Open diagnostics

1. Open **App info & diagnostics**.
2. Review:
   - version
   - package name
   - device model
   - Android version
   - Android ID
   - API base URL

Expected result:

- support person knows exactly what to capture in an incident report

## 5. Acceptance Checklist

The tutorial is considered complete when the support person can demonstrate:

- successful login
- successful connection test
- one successful dispatch scan
- one successful mobile update upload
- one successful direct serial handoff attempt
- correct identification of pending vs processed screens
- correct use of diagnostics page

## 6. Evidence to Keep

For onboarding completion, capture:

- screenshot of login settings with target host/port
- screenshot of root dashboard
- screenshot of a dispatch details page
- screenshot of pending updates
- screenshot of diagnostics page

## 7. Next Documents

After completing this tutorial, continue with:

- `docs/scanner/how-to/admin-runbook.md`
- `docs/scanner/how-to/configure-api-connection.md`
- `docs/scanner/how-to/troubleshooting.md`

## 8. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
