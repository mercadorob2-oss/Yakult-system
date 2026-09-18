# Tutorial: Borrow Items Support Onboarding

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | New IT support + new IT technical staff |
| System | Borrow Items Subsystem |
| Version | 1.0 |
| Effective date | 2026-03-11 |
| Last updated | 2026-03-11 |

## 1. Purpose

This tutorial provides a guided introduction for support personnel who are new to the Borrow Items subsystem.

## 2. Learning Outcome

After completing this tutorial, the learner should be able to:

- identify where Borrow Items lives in the application
- explain what the subsystem does and does not do
- perform one test borrow
- perform one test return
- identify the open and history views
- recognize the common failure states

## 3. Step 1 - Enter The Module

1. Sign in to the WinForms application.
2. Open the portal selector.
3. Locate `Borrow Items`.
4. Open the module.

What to observe:

- left side contains transaction panels
- right side contains monitoring and history

## 4. Step 2 - Understand The Borrow Panel

Review the `Borrow Item` section:

- serial number input
- `Resolve`
- `Add New`
- company / department / employee selection
- `Borrow`

Meaning:

- `Resolve` checks whether the serial already exists in inventory
- `Add New` opens the create-and-borrow dialog

## 5. Step 3 - Perform A Test Borrow

1. Enter a known available serial.
2. Click `Resolve`.
3. Select a valid employee.
4. Click `Borrow`.
5. Review and confirm the dialog.

Expected result:

- the record appears in `Open Borrows`
- open count updates

## 6. Step 4 - Understand The Return Panel

Review the `Return Item` section:

- serial number input
- `Find Open`
- borrower info label
- company / department / employee selection
- `Return`

Meaning:

- `Find Open` does not search inventory generally; it searches for an active open borrow row

## 7. Step 5 - Perform A Test Return

1. Enter the borrowed serial.
2. Click `Find Open`.
3. Confirm the borrower information appears.
4. Select the actual returner.
5. Click `Return`.
6. Confirm the dialog.

Expected result:

- row leaves `Open Borrows`
- row appears in `History`

## 8. Step 6 - Review Monitoring Area

Observe:

- `Open` KPI
- `Oldest Open` KPI
- open grid
- history grid
- export button

Interpretation:

- open grid = active custody
- history grid = completed transactions

## 9. Step 7 - Recognize Common Problems

Common support states:

- schema missing warning
- item not found by serial
- item already borrowed
- no open borrow found on return
- employee missing from dropdown

## 10. Next Reading

After this tutorial, continue with:

- `docs/borrow/how-to/admin-runbook.md`
- `docs/borrow/how-to/verify-schema-and-access.md`
- `docs/borrow/how-to/troubleshooting.md`

## 11. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
