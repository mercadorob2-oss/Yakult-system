# How-To: Troubleshoot Borrow Items

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

This document provides first-level troubleshooting guidance for the Borrow Items subsystem.

## 2. Common Issues and Actions

### 2.1 `Borrow Items` card is not visible

Checks:

1. confirm the user is logged in successfully
2. verify assigned roles
3. verify the role is one of the permitted Borrow Items roles

Likely cause:

- permission gating through `PermissionResolver`

Escalate when:

- a correct role still cannot see the module

### 2.2 Dashboard shows schema missing warning

Typical message:

- `Borrow Items schema is missing`
- `Install BorrowItems.Prod.Install.sql`

Meaning:

- application reached the DB
- `dbo.BorrowLog` does not exist in that connected database

Action:

1. verify target environment
2. confirm whether the install script was deployed
3. do not assume full borrow logging is available until schema exists

### 2.3 Item cannot be resolved by serial

Checks:

1. confirm the serial is correct
2. confirm the item exists in inventory
3. test with another known valid serial

Likely causes:

- serial is not yet in `dbo.Item`
- user entered wrong serial
- wrong environment / wrong database

### 2.4 System says item is already borrowed

Meaning:

- an open row exists for the item

Action:

1. search open rows by serial
2. review whether the item was never returned
3. confirm whether the row should be returned rather than borrowed again

Escalate when:

- no open row is visible but borrow still fails

### 2.5 No open borrow found during return

Checks:

1. confirm the serial number
2. confirm the record is still open
3. verify the row was not already returned by another user

Likely causes:

- wrong serial
- row already closed
- open row exists in a different environment

### 2.6 Employee dropdown is empty or incomplete

Checks:

1. confirm active employees exist
2. confirm employees have valid departments
3. switch company and department filters

Likely causes:

- employee missing `DeptId`
- filtering excludes the intended employee
- data load issue

### 2.7 Newly added employee does not appear

Likely cause:

- employee was created without a department

Action:

1. update employee master data
2. reload lookups
3. retry selection

### 2.8 CSV export fails or has unexpected contents

Checks:

1. confirm whether `Open Borrows` or `History` tab is selected
2. confirm rows are present
3. verify destination path is writable

Interpretation:

- exported columns differ by tab by design

## 3. Evidence To Gather

Before escalation, collect:

- screenshot of the exact screen state
- serial number used
- username
- environment / connected DB
- whether schema warning is shown
- export file if available
- exact message text

## 4. Escalation Guidance

Escalate to Development when:

- role access logic is incorrect
- duplicate-open logic behaves incorrectly
- return logic fails against a clearly valid open row
- dashboard loads but rows/counts are inconsistent
- export behavior is broken

Escalate to database owner when:

- schema objects are missing
- core referenced tables are missing or inconsistent
- data-integrity issue is confirmed in the ledger

## 5. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
