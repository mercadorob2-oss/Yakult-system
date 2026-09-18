# Work Instruction: Yakult Scanner - Configure API Connection

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

This document explains how to view, test, change, and save the backend API endpoint used by the Yakult Scanner mobile app.

## 2. Scope

This procedure applies to:

- first-time device setup
- environment switching between `DEV` and `PROD`
- connection troubleshooting after server, network, or port changes

## 3. Preconditions

- device has the app installed
- device has network access to the target server
- support knows the correct host/IP and port for the environment

## 4. Default Behavior

The app starts with a flavor-based default base URL:

- `dev`: `http://192.168.12.9:80/`
- `prod`: `http://192.168.12.9:7326/`

If a user saves a different base URL in the app, that saved value overrides the flavor default on future launches.

## 5. Procedure

### 5.1 Open settings from login

1. Launch the app.
2. On the login screen, tap the settings icon.

### 5.2 Review current values

The dialog shows:

- Server IP / Host
- Port

These values are derived from the current effective base URL.

### 5.3 Enter target connection

1. Enter the server IP or hostname.
2. Enter the port number.

Notes:

- do not include trailing slash manually unless desired
- the app normalizes the final URL automatically
- if no scheme is entered, the app assumes `http://`

### 5.4 Test connection

1. Tap **Test Connection**.
2. Wait for the result.

The app checks:

- `GET /api/health`

Interpretation:

- **Connected! (API health OK)** = host/port and basic API reachability are valid
- **Failed (HTTP xxx)** = server was reached, but health endpoint did not return success
- **Failed: ...** = network or request exception

### 5.5 Save settings

1. Tap **Save** after a successful test.
2. Confirm the success message shows the saved API URL.

Result:

- new base URL is saved locally
- cached Retrofit service is cleared
- future API calls use the new URL

## 6. Alternate Access Path

After login, support can also access connection settings through the connection status chip and its dialog.

Use this path when:

- the login screen is already passed
- support needs to inspect connection health while signed in

## 7. Acceptance Criteria

This procedure is complete when:

- health test succeeds
- saved URL matches the intended environment
- a real workflow such as login or dashboard refresh also works

## 8. Common Failure Patterns

### 8.1 Health check fails immediately

Possible causes:

- wrong host
- wrong port
- device not on the correct network
- server offline

### 8.2 Health succeeds but login fails

Possible causes:

- credential issue
- auth endpoint issue
- token/auth contract issue

### 8.3 Login succeeds but dashboard calls fail

Possible causes:

- endpoint-specific backend issue
- token accepted for auth but rejected elsewhere
- version mismatch between mobile app and API

## 9. Records / Evidence

Capture for support evidence:

- screenshot of host and port
- result of Test Connection
- screenshot of actual failing screen after save

## 10. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
