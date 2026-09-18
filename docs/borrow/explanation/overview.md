# SOP / WI: Borrow Items Subsystem - Overview

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support / Application Support |
| Audience | IT technical + IT support + developers |
| System | Borrow Items Subsystem |
| Location | `Yakult.Inventory.App` |
| Version | 1.0 |
| Effective date | 2026-03-11 |
| Last updated | 2026-03-11 |

## 1. Purpose

This document defines the scope, responsibilities, dependencies, and operating context of the Borrow Items subsystem inside the Yakult Inventory WinForms application. It serves as the baseline reference for support teams before performing deployment validation, operational support, troubleshooting, or formal documentation work.

## 2. Scope

Included:

- Borrow and return logging for serialized items
- Dashboard behavior in `BorrowItemsDashboard`
- Borrow ledger storage in `dbo.BorrowLog`
- Borrower and returner selection logic
- Existing inventory item resolution by serial number
- Add-new-item-and-borrow workflow
- Open borrow monitoring and borrow history browsing
- CSV export of open and historical borrow records
- Access control from the portal selector

Excluded:

- General inventory management outside borrow-related item lookup or item creation
- Request portal workflows
- IT Call Monitoring workflows
- Mobile scanner workflows
- Any SQL objects not directly used by the borrow subsystem

## 3. System Definition

Borrow Items is a support-facing logbook module used to record temporary custody of serialized hardware items.

The subsystem performs four main functions:

1. Resolve an item by serial number from the inventory master
2. Log a borrow transaction for a selected employee
3. Resolve an open borrow by serial number and log its return
4. Display open records and completed history for audit and operational follow-up

This subsystem is intentionally lightweight. It is not a full approval workflow, loan request workflow, due-date enforcement engine, or multi-stage asset custody system.

## 4. Repository Ownership Map

Primary runtime areas:

- Dashboard UI: `Yakult.Inventory.App/Forms/BorrowItemsDashboard.cs`
- Borrow dialogs: `Yakult.Inventory.App/Forms/BorrowItems/`
- Data access: `Yakult.Inventory.App/Repositories/BorrowItemsRepository.cs`
- Data models: `Yakult.Inventory.App/Models/BorrowItems/`
- Install script: `Yakult.Inventory.App/Database/BorrowItems.Prod.Install.sql`

Portal integration points:

- Portal selector card: `Yakult.Inventory.App/Forms/Portal/MainDashboardForm.cs`
- Launch point after selection: `Yakult.Inventory.App/Pages/User/MainForm.cs`

## 5. Responsibilities

### 5.1 IT Support

- Use the dashboard to verify borrow and return functionality
- Validate schema readiness after deployment
- Confirm employee lookup behavior and open-record visibility
- Collect screenshots and exported CSV evidence when incidents occur
- Escalate defects or schema issues with reproducible steps

### 5.2 IT Technical / Application Support

- Confirm the module is available to the correct user roles
- Confirm the database schema exists in the connected environment
- Validate dependencies on `Item`, `Employee`, `Department`, and `User`
- Support controlled release verification after schema installation or app updates

### 5.3 Developers

- Maintain dashboard behavior and business rules in the repository layer
- Maintain compatibility between the WinForms UI and the borrow schema
- Update or extend the ledger logic when business requirements change

## 6. Core Dependencies

The borrow subsystem depends on the following existing database tables:

- `dbo.Item`
- `dbo.Employee`
- `dbo.Department`
- `dbo.User`

The subsystem adds and uses:

- `dbo.BorrowLog`

Operational dependencies:

- active SQL connectivity from the WinForms application
- valid logged-in user context for encoder attribution
- active employee records with assigned departments

## 7. Access Model

Borrow Items is exposed through the portal selector and is permission-gated.

Roles with access based on current code:

- `Developer`
- `Admin`
- `IT Manager`
- `Supervisor`
- `Tech Support`

The portal card is shown or hidden based on `PermissionResolver`, and direct portal selection is blocked when the current role set does not include Borrow Items access.

## 8. High-Level Workflow Summary

Primary borrow workflow:

1. User scans or types a serial number
2. System resolves the item from inventory
3. System checks whether an open borrow already exists
4. User selects company, department, and borrower
5. User confirms the transaction
6. System inserts a new open row into `dbo.BorrowLog`

Primary return workflow:

1. User scans or types a serial number
2. System resolves the current open borrow row
3. User selects company, department, and returner
4. User confirms the return
5. System updates the existing open row with return information

## 9. Operating Characteristics

Current behavior observed in code:

- only one open borrow can exist for a given item at a time
- open records are monitored in a real-time-style dashboard with elapsed time refreshing every second
- return defaults to the original borrower when the row is resolved
- CSV export is available for both open and history views
- if the borrow schema is missing, item creation can still occur but borrow logging is unavailable

## 10. Business Interpretation

The subsystem behaves as an auditable borrow ledger rather than a borrowing request system.

Important practical meaning:

- a borrow row represents actual logged custody, not a pending request
- a return updates the original transaction row instead of creating a separate return transaction row
- names and department values are copied into the log row so that historical records remain readable even when master data later changes

## 11. Related Documents

- `docs/borrow/how-to/admin-runbook.md`
- `docs/borrow/how-to/verify-schema-and-access.md`
- `docs/borrow/how-to/operate-borrow-and-return.md`
- `docs/borrow/how-to/troubleshooting.md`
- `docs/borrow/reference/database.md`
- `docs/borrow/reference/screens-roles-and-controls.md`
- `docs/borrow/reference/policies-and-known-gaps.md`
- `docs/borrow/explanation/technical-design.md`
- `docs/borrow/explanation/borrow-ledger-and-workflow-logic.md`

## 12. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial borrow subsystem documentation baseline |
