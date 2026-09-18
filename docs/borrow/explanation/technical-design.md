# Explanation: Borrow Items - Technical Design

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / Development |
| Audience | IT technical + developers + support leads |
| System | Borrow Items Subsystem |
| Version | 1.0 |
| Effective date | 2026-03-11 |
| Last updated | 2026-03-11 |

## 1. Purpose

This document explains how the Borrow Items subsystem is structured in code, how the UI communicates with the repository layer, and how data is persisted in the database.

## 2. Design Summary

The subsystem follows a relatively simple architecture:

- WinForms dashboard and dialogs provide the operator-facing UI
- a single repository encapsulates SQL access and transaction logic
- lightweight models represent lookup and ledger rows
- a standalone install script creates the borrow schema

This is not a service-oriented or stored-procedure-based subsystem. The application calls SQL directly from `BorrowItemsRepository`.

## 3. Main Components

### 3.1 Dashboard

Primary form:

- `Yakult.Inventory.App/Forms/BorrowItemsDashboard.cs`

Responsibilities:

- build and host the borrow and return panels
- initialize lookups and schema status
- manage open/history pagination
- update KPI cards and elapsed-time display
- trigger borrow and return confirmation dialogs

### 3.2 Dialogs

Dialogs under `Yakult.Inventory.App/Forms/BorrowItems/`:

- `ConfirmBorrowDialog.cs`
- `ConfirmReturnDialog.cs`
- `AddExternalAndBorrowDialog.cs`
- `ReturnBorrowDialog.cs`

Responsibilities:

- collect and confirm user input
- separate complex UI branches from the main dashboard
- support add-item plus immediate-borrow behavior

### 3.3 Repository

Primary repository:

- `Yakult.Inventory.App/Repositories/BorrowItemsRepository.cs`

Responsibilities:

- detect whether the borrow schema exists
- resolve items by serial number
- load active employee and company lookups
- load open and historical borrow rows
- insert borrow rows
- update existing rows for returns

### 3.4 Models

Main models:

- `BorrowLogRow`
- `BorrowLogPage`
- `BorrowItemLookup`
- `BorrowEmployeeLookup`

Responsibilities:

- carry lookup and ledger data to and from the repository
- expose display-oriented helper properties such as `DisplayText`, `ItemDisplay`, `BorrowedAtLocal`, and `ElapsedText`

## 4. UI Layout Model

The dashboard is visually divided into three major areas:

1. Header
   - module name
   - search box
   - refresh controls
2. Left operations area
   - borrow panel
   - return panel
3. Right monitoring area
   - KPI cards
   - open/history tabs
   - action footer

This layout matches the borrow screenshots and the constructed form layout in the dashboard source.

## 5. Data Flow

### 5.1 Borrow

1. operator enters a serial number
2. dashboard calls `FindItemBySerialAsync`
3. dashboard optionally calls `GetOpenBorrowBySerialAsync`
4. if item is valid and not already open, operator selects borrower
5. dashboard opens `ConfirmBorrowDialog`
6. dashboard calls `BorrowAsync`
7. repository inserts a row into `dbo.BorrowLog`
8. dashboard reloads open/history views and updates KPI

### 5.2 Return

1. operator enters a serial number
2. dashboard calls `GetOpenBorrowBySerialAsync`
3. dashboard preloads borrower context into the return panel
4. operator selects returner
5. dashboard opens `ConfirmReturnDialog`
6. dashboard calls `ReturnAsync`
7. repository updates the open row in `dbo.BorrowLog`
8. dashboard reloads open/history views and updates KPI

### 5.3 Add New Item Then Borrow

1. operator attempts borrow on an unknown serial
2. dashboard offers the add-new path
3. `AddExternalAndBorrowDialog` opens
4. user either:
   - resolves an existing listed item and borrows it, or
   - creates a new inventory item
5. dashboard creates the item through `ItemRepository`
6. dashboard immediately calls `BorrowAsync` if schema and borrower are available

## 6. Transaction Model

Borrow and return operations both execute within SQL transactions.

Borrow transaction responsibilities:

- confirm schema exists
- resolve item by serial
- verify no open borrow exists for the item
- resolve borrower and department
- resolve encoder name
- insert the new row

Return transaction responsibilities:

- confirm schema exists
- resolve the specific borrow row by `BorrowId`
- verify the row is still open
- resolve returner and department
- resolve encoder name
- update the row and stamp `ReturnedAtUtc`

## 7. Concurrency Control

The subsystem uses two layers of protection against duplicate open rows:

1. application-level check before insert
2. filtered unique index on `ItemId` where `ReturnedAtUtc IS NULL`

This is the correct design pattern for a logbook UI that can be used concurrently by more than one operator.

For return operations, the update statement also requires `ReturnedAtUtc IS NULL`, which prevents a second operator from returning the same row after another operator already closed it.

## 8. Display Logic

`BorrowLogRow` contains display-friendly derived values:

- `IsOpen`
- `ItemDisplay`
- `BorrowedAtLocal`
- `ReturnedAtLocal`
- `ElapsedText`

This reduces formatting work in the UI and keeps table rendering logic simple.

## 9. Search, Pagination, and Monitoring

The dashboard supports:

- open-record filtering by serial text
- history filtering by serial text
- paged loading for open and history views
- KPI count for total open rows
- KPI elapsed timer for oldest open borrow

Elapsed values on the open grid refresh every second through a `Timer`, while the underlying records are reloaded on explicit refresh or after transaction activity.

## 10. Architectural Strengths

- narrow scope and clear functional boundary
- strong database enforcement for open-row uniqueness
- readable audit-oriented data model
- direct mapping between UI behavior and repository logic
- operationally simple support surface

## 11. Architectural Weaknesses

- direct SQL in repository means business rules are not isolated behind an API or stored procedure layer
- no due-date or expected-return model exists
- no approval workflow or request stage exists
- return defaults may encourage operators to accept the original borrower as the returner without verification
- schema target in the install script is production-oriented and should be handled carefully during deployment

## 12. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
