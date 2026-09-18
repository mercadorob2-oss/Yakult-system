# Work Instruction: Borrow Items - Admin / Support Runbook

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | IT technical + IT support |
| System | Borrow Items Subsystem |
| Version | 1.0 |
| Effective date | 2026-03-11 |
| Last updated | 2026-03-11 |

## 1. Purpose

This runbook provides the day-to-day operational procedures used by IT support and technical personnel to keep the Borrow Items subsystem available and usable.

## 2. Scope

This runbook covers:

- access validation
- schema readiness checks
- borrower lookup validation
- borrow and return transaction checks
- dashboard monitoring checks
- export and evidence collection
- escalation criteria

## 3. Preconditions / Requirements

### 3.1 Application requirements

- WinForms application can launch normally
- logged-in user has a supported portal role
- database connection is valid for the intended environment

### 3.2 Data requirements

- `dbo.Item` exists and contains test or production serials as appropriate
- `dbo.Employee` records exist
- employee rows used for borrow/return have valid departments
- `dbo.BorrowLog` exists for full subsystem operation

### 3.3 Access requirements

One of the following portal-enabled roles:

- `Developer`
- `Admin`
- `IT Manager`
- `Supervisor`
- `Tech Support`

## 4. Daily / Shift Checklist

Perform the following at the start of a support shift or after a deployment.

### 4.1 Confirm module visibility

1. Open the portal selector.
2. Confirm the `Borrow Items` card is visible for the support account.
3. Open the module.

Acceptance:

- card is visible
- module opens without immediate error

### 4.2 Confirm schema readiness

1. Open Borrow Items.
2. Check for any schema-missing warning.
3. Confirm status text does not say `Install BorrowItems.Prod.Install.sql`.

Acceptance:

- no schema warning is shown
- open/history area loads normally

### 4.3 Confirm employee lookups

1. In Borrow Item, open company, department, and employee selectors.
2. In Return Item, open company, department, and employee selectors.
3. Confirm employees display as `Name - Department`.

Acceptance:

- dropdowns populate
- department-filtered employees appear

### 4.4 Confirm borrow readiness

1. Enter a known available serial.
2. Click `Resolve`.
3. Confirm item details appear.

Acceptance:

- item display and description resolve
- `Borrow` button becomes available once employee is selected

### 4.5 Confirm return readiness

1. Enter a known open-borrow serial.
2. Click `Find Open`.
3. Confirm borrower info appears.

Acceptance:

- open row resolves
- `Return` button becomes available once returner is selected

### 4.6 Confirm monitoring area

1. Review KPI cards.
2. Review `Open Borrows` tab.
3. Review `History` tab.

Acceptance:

- counts are present
- rows load
- elapsed values update on open rows

## 5. Routine Support Procedures

### 5.1 Validate borrow transaction

1. Enter a test serial that exists and is not currently open.
2. Click `Resolve`.
3. Select borrower.
4. Click `Borrow`.
5. Review confirmation dialog.
6. Confirm the transaction.

Expected result:

- `Borrow logged.` message is shown
- row appears in `Open Borrows`
- return panel is prefilled with the same serial

### 5.2 Validate return transaction

1. Enter a serial with an open borrow.
2. Click `Find Open`.
3. Confirm borrower details display.
4. Select the actual returner.
5. Click `Return`.
6. Review confirmation dialog.
7. Confirm the transaction.

Expected result:

- `Return logged.` message is shown
- row disappears from `Open Borrows`
- row appears in `History`

### 5.3 Validate add-new-item path

1. Enter a serial that does not yet exist.
2. Click `Resolve`.
3. Accept the prompt to add a new item.
4. Complete the add-item fields.
5. Select or add a borrower if borrowing is required.
6. Save the dialog.

Expected result:

- item is created in inventory
- if schema and borrower are available, borrow is logged immediately
- otherwise, operator is instructed to borrow later by serial

### 5.4 Validate quick-add employee path

1. In borrow or return mode, choose `Not listed (Add employee)`.
2. Open quick-add employee.
3. Save a new employee.
4. Confirm the employee can be selected afterward.

Acceptance:

- new employee becomes selectable only when department data is valid

### 5.5 Export operational evidence

1. Open the desired tab:
   - `Open Borrows`, or
   - `History`
2. Click `CSV Export`.
3. Save the file.

Acceptance:

- CSV saves successfully
- exported columns match the selected tab

## 6. Support Evidence To Collect

Collect the following before escalation:

- screenshot of the borrow or return panel state
- screenshot of open/history tab
- exact serial number used
- current logged-in user
- exact error text
- whether schema warning is shown
- CSV export if row state is in dispute

## 7. Escalation Path

Recommended escalation path for Borrow Items:

1. IT Support
2. Supervisor
3. Development

Escalate to database owner only when the issue is clearly schema- or data-integrity-related.

## 8. Escalation Criteria

Escalate when:

- portal card is missing for a role that should have access
- schema warning appears in the intended target environment
- existing serial cannot be resolved though it exists in inventory
- borrow is blocked incorrectly as already open
- return says no open row exists when one is visible
- employee dropdowns fail to populate
- history/open counts are inconsistent with actual rows
- CSV export fails

## 9. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
