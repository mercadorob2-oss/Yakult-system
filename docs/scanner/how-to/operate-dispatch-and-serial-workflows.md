# Work Instruction: Yakult Scanner - Operate Dispatch and Serial Workflows

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

This document explains the main operational workflows used inside the scanner app so support can validate expected behavior and identify the correct failure point when a user reports a problem.

## 2. Scope

Covered workflows:

- dispatch scanning
- item issue/status upload
- deploy
- direct serial handoff
- batch serial entry

## 3. Dispatch Workflow

### 3.1 Entry point

Path:

- `root_home` -> `home`

From `home`, user chooses:

- **New Device Scan**
- **Use Phone Camera**

### 3.2 Expected scan sources

Valid capture methods:

- camera QR scan
- Honeywell or wedge hardware scan
- gallery QR import in camera mode

### 3.3 Expected output

After a successful dispatch scan:

- dispatch details screen opens
- set information loads
- item list loads
- scan is stored in local history

### 3.4 Item issue capture

Current active behavior:

1. swipe an item from right to left
2. `Update Item Status` dialog opens
3. enter new status and/or remarks
4. save the edit

Support note:

- the status field is free-text
- remarks are also free-text

### 3.5 Upload changes

When at least one item has a pending change:

1. tap **Upload Changes**
2. confirm action in the bottom sheet
3. wait for upload completion

Expected result:

- success toast appears
- updates are later visible through uploaded/pending lists

### 3.6 View uploaded items

If a set code exists, the **View Uploads** action opens all uploaded mobile updates for the current set.

### 3.7 Deploy

Deploy is only available when:

- screen is not read-only
- scan came from token-based QR flow

If the button is disabled, check:

- was the QR token-based
- was the screen opened from `details_readonly`
- is the token query present

## 4. Direct Serial Workflow

### 4.1 Entry point

Path:

- `root_home` -> `serial_scan_home` -> `direct_serial_scan`

### 4.2 Supported capture methods

- manual typing
- scanner camera route returning serial
- hardware scanner input

### 4.3 Operational steps

1. scan or type a serial
2. add it to the list
3. repeat as needed
4. tap **Send Serial(s) to Windows**

Expected result:

- app sends serials sequentially to backend
- success and failure counts are shown

### 4.4 Support interpretation

- success in app does not prove downstream Windows consumption completed correctly
- if user says “mobile send worked but desktop did nothing,” the next checkpoint is the API/desktop integration path

## 5. Batch Serial Entry Workflow

### 5.1 Entry point

Path:

- `root_home` -> `serial_scan_home` -> `batch_serial_entry`

### 5.2 Required data

Minimum validation requires:

- item name
- model number
- category
- condition
- at least one serial

### 5.3 Dynamic behavior

The form changes based on selected type:

- `Hardware`
- `Software/License`
- `Services`

Examples:

- software/license enables start and end date handling
- unit options change by type
- non-hardware behavior includes license-related field usage

### 5.4 Save behavior

1. add serials
2. complete required metadata
3. tap save

Expected result:

- API creates multiple items
- success message shows created count
- form resets after successful creation

## 6. Common Validation Checks

Use this checklist while supporting users:

- can the user log in
- does scanner screen open
- does QR resolve to details
- can an item update be entered
- can upload succeed
- do pending/processed screens reflect activity
- can direct serial handoff succeed
- can categories/conditions/vendors load for batch entry

## 7. Evidence to Capture

For workflow issues, capture:

- exact route or screen
- input type used:
  - camera
  - wedge
  - Honeywell
  - manual
- set code or serial involved
- screenshot before and after failure
- whether connection test passed

## 8. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
