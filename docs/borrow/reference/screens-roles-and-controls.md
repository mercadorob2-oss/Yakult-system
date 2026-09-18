# Reference: Borrow Items - Screens, Roles, and Controls

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | IT technical + IT support + developers |
| System | Borrow Items Subsystem |
| Version | 1.0 |
| Effective date | 2026-03-11 |
| Last updated | 2026-03-11 |

## 1. Purpose

This document lists the main screens, access roles, and important operator controls used by the Borrow Items subsystem.

## 2. Access Roles

Current roles with Borrow Items access:

- `Developer`
- `Admin`
- `IT Manager`
- `Supervisor`
- `Tech Support`

Current roles without Borrow Items access by default include:

- `Viewer`
- `Requester`
- `InventoryManager`

## 3. Main Entry Point

Portal selector card:

- label: `Borrow Items`
- description: `Log borrowed hardware by serial number and track returns.`

Module launch path:

- Portal selector
- `PortalSelection.BorrowItems`
- `BorrowItemsDashboard`

## 4. Main Dashboard Areas

### 4.1 Borrow Item panel

Key controls:

- serial input
- `Resolve`
- `Add New`
- company selector
- department selector
- employee selector
- `Listed employee`
- `Not listed (Add employee)`
- `Borrow`

### 4.2 Return Item panel

Key controls:

- serial input
- `Find Open`
- borrower info label
- company selector
- department selector
- returner selector
- `Listed employee`
- `Not listed (Add employee)`
- `Return`

### 4.3 Monitoring / grid area

Key controls:

- KPI card: `Open`
- KPI card: `Oldest Open`
- tab: `Open Borrows`
- tab: `History`
- `Return Selected`
- `CSV Export`
- `Prev`
- `Next`
- `Refresh`
- `Back to Portal`

## 5. Dialogs

### 5.1 `ConfirmBorrowDialog`

Purpose:

- final review before a borrow row is inserted

Shows:

- item
- serial
- borrowed by
- company
- department
- encoded by
- date/time

### 5.2 `ConfirmReturnDialog`

Purpose:

- final review before a return closes the open row

Shows:

- item
- serial
- borrowed by
- borrowed at
- returned by
- department
- encoded by
- date/time

### 5.3 `AddExternalAndBorrowDialog`

Purpose:

- create a new item or resolve a listed item, then borrow

Modes:

- `Listed in Inventory`
- `Not Listed (External/New)`

### 5.4 Quick add employee

Purpose:

- create an employee when the intended borrower/returner does not exist in lookup

## 6. Grid Columns

### 6.1 Open Borrows

Columns:

- `Serial Number`
- `Item Description`
- `Borrowed By`
- `Department`
- `Elapsed`
- `Encoded By`
- `Borrowed`

### 6.2 History

Columns:

- `Serial Number`
- `Item Description`
- `Borrowed By`
- `Department`
- `Borrowed`
- `Returned`
- `Returned By`
- `Encoded By`

## 7. Operator Notes

- `Borrow` is enabled only when an item is resolved and a valid listed employee is selected
- `Return` is enabled only when an open row is resolved and a valid listed employee is selected
- `Return Selected` acts on the selected row in the open grid
- `CSV Export` exports the currently selected tab only

## 8. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
