# Explanation: Borrow Items - Ledger and Workflow Logic

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

This document explains the business logic implemented by the Borrow Items subsystem, with emphasis on what a borrow row means, how workflow branches operate, and which conditions allow or block transactions.

## 2. Core Business Rule

The single most important rule in this subsystem is:

- one inventory item can have at most one open borrow record at a time

This rule is enforced in both the application layer and the database layer.

## 3. Meaning of a Borrow Row

Each row in `dbo.BorrowLog` represents one completed borrow transaction lifecycle.

Borrow stage content:

- item snapshot
- borrower identity
- borrower department
- encoder identity
- borrow timestamp

Return stage content added later to the same row:

- returner identity
- returner department
- return encoder identity
- return timestamp

This means a single row evolves from `open` to `closed` rather than spawning a separate return record.

## 4. Why Names Are Copied Into The Ledger

The borrow table stores employee and department names directly, even though employee IDs are also stored.

This is an intentional audit convenience:

- historical rows remain readable even if employee names change later
- exported CSV files remain understandable without joining current master tables
- the dashboard can display history directly without additional lookup dependency for old rows

## 5. Borrow Resolution Logic

When a user enters a serial number in the borrow panel:

1. the system trims and validates the serial
2. the system checks whether the item exists in `dbo.Item`
3. if schema exists, the system checks whether an open borrow already exists for that item

Possible outcomes:

- item exists and is not open:
  - borrow may proceed
- item exists but is already open:
  - borrow is blocked
- item does not exist:
  - user is offered the add-new-item path

## 6. Borrower Selection Logic

Borrower selection is driven by active employee records with departments.

Important rules:

- employee must exist
- employee must have `DeptId > 0`
- company and department comboboxes are lookup filters, not the stored source of truth by themselves
- the actual persisted borrower identity comes from the selected employee row

If a newly added employee has no department assigned, the employee will not become a valid borrower for the ledger.

## 7. Return Resolution Logic

When a user enters a serial number in the return panel:

1. the system looks up the currently open borrow row by serial
2. if not found, return is blocked
3. if found, the dashboard displays the original borrower and borrowed timestamp
4. the system preselects company, department, and employee based on the original borrower
5. user confirms the actual returner

This defaulting behavior improves speed for normal same-person returns but must be used carefully where another employee physically returns the item.

## 8. Add-New-Item Workflow Branches

The add-and-borrow dialog has two distinct business branches.

### 8.1 Listed In Inventory

Meaning:

- the operator wants to borrow an item that should already exist in inventory

Behavior:

- operator resolves the serial
- no new inventory item is created
- the dialog returns the serial plus the selected borrower
- the dashboard performs a normal borrow log insert

### 8.2 Not Listed (External/New)

Meaning:

- the operator wants to create a new item and optionally borrow it immediately

Behavior:

- user fills add-item details
- dashboard creates the inventory item first
- if schema and borrower are available, borrow is logged immediately
- otherwise, the item is created only and the operator is told to borrow it later

## 9. Schema-Gated Behavior

Borrow logging depends on the presence of `dbo.BorrowLog`.

If the schema is missing:

- borrow and return logging are unavailable
- open and history views cannot function as intended
- add-new item workflow may still be used to create inventory items only

Operational implication:

- deployment support must treat schema installation as mandatory for a fully working borrow subsystem

## 10. KPI Logic

The dashboard exposes two high-level indicators:

- total open borrow count
- age of the oldest open borrow

Purpose:

- quick operational visibility into unresolved borrowed items
- simple aging signal for support follow-up

This is not a formal SLA metric; it is only a monitoring convenience inside the dashboard.

## 11. Export Logic

The subsystem supports CSV export for:

- current open rows
- historical completed rows

Use cases:

- audit evidence
- support handoff
- manual review outside the application
- ad hoc operational reporting

## 12. What The System Does Not Do

The subsystem currently does not implement:

- due date assignment
- expected return date
- overdue notifications
- approval chain
- reason codes for borrow or return
- attachment storage
- multiple return stages
- damaged/lost workflow branching

These omissions are important because documentation should not imply policy controls that the code does not actually enforce.

## 13. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
