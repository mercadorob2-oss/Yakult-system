# How-To: Verify Borrow Items Schema and Access

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

This procedure verifies that the Borrow Items subsystem is both visible to the user and fully operational against the connected database.

## 2. Preconditions

- user can sign in to the WinForms application
- connected database environment is known
- support account has one of the approved portal roles

## 3. Procedure

### 3.1 Verify portal access

1. Launch the main portal selector.
2. Confirm the `Borrow Items` card is visible.
3. If not visible, verify the user role assignment.

Expected result:

- the module card is shown for permitted users

### 3.2 Verify dashboard opens

1. Click `Borrow Items`.
2. Confirm the dashboard opens.

Expected result:

- dashboard opens without access denied

### 3.3 Verify schema state

1. Observe startup behavior.
2. Check whether a warning mentions:
   - `Borrow Items schema is missing`
   - `Install BorrowItems.Prod.Install.sql`
3. Check the status text at the bottom of the dashboard.

Expected result:

- no schema warning is shown in a healthy environment

### 3.4 Verify full read-path functionality

1. Open company, department, and employee selectors.
2. Review `Open Borrows` and `History`.

Expected result:

- selectors are populated
- tabs load rows or show empty state without failure

## 4. Interpretation

### 4.1 Card missing

Likely causes:

- user role does not include Borrow Items permission
- login session or permission resolution issue

### 4.2 Dashboard opens but schema warning appears

Meaning:

- application can reach the database
- borrow schema is not installed in that database

Operational effect:

- add-item-only behavior may still be possible
- borrow and return logging are not fully available

### 4.3 Dropdowns empty

Likely causes:

- employee data not available
- active employees missing departments
- database connection or query failure

## 5. Evidence To Capture

- screenshot of portal selector
- screenshot of schema warning or bottom status text
- current username and role set
- target environment name

## 6. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
