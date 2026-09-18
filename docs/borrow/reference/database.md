# Reference: Borrow Items - Database

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / Development |
| Audience | IT technical + developers + DB support |
| System | Borrow Items Subsystem |
| Version | 1.0 |
| Effective date | 2026-03-11 |
| Last updated | 2026-03-11 |

## 1. Purpose

This reference records the database objects, dependencies, and key data rules used by the Borrow Items subsystem.

## 2. Primary Script

Schema install script:

- `Yakult.Inventory.App/Database/BorrowItems.Prod.Install.sql`

The script is designed to be rerunnable for object creation safety.

## 3. Target Database

Current script target:

- `YIMS_PROD`

Operational note:

- this hardcoded target requires careful handling when deploying to non-production environments

## 4. Required Existing Tables

Preflight checks require the following tables:

- `dbo.Item`
- `dbo.Employee`
- `dbo.Department`
- `dbo.User`

If any of these are missing, the script throws and stops.

## 5. Main Table

### 5.1 `dbo.BorrowLog`

Purpose:

- stores the full lifecycle of a borrow transaction

Key columns:

- `BorrowId`
- `ItemId`
- `SerialNumber`
- `ItemName`
- `ItemDescription`
- `ModelNumber`
- `BorrowedByEmpId`
- `BorrowedByEmpName`
- `BorrowedByDeptId`
- `BorrowedByDeptName`
- `BorrowEncodedByUserId`
- `BorrowEncodedByUserName`
- `BorrowedAtUtc`
- `ReturnedByEmpId`
- `ReturnedByEmpName`
- `ReturnedByDeptId`
- `ReturnedByDeptName`
- `ReturnEncodedByUserId`
- `ReturnEncodedByUserName`
- `ReturnedAtUtc`
- `RowVer`

## 6. Key Constraints

Primary key:

- `PK_BorrowLog` on `BorrowId`

Foreign keys:

- `FK_BorrowLog_Item`
- `FK_BorrowLog_BorrowedEmp`
- `FK_BorrowLog_ReturnedEmp`
- `FK_BorrowLog_BorrowEncodedBy`
- `FK_BorrowLog_ReturnEncodedBy`

## 7. Key Indexes

### 7.1 `UX_BorrowLog_OpenItem`

Purpose:

- enforces one open borrow per item

Filter:

- `ReturnedAtUtc IS NULL`

### 7.2 `IX_BorrowLog_OpenSerial`

Purpose:

- fast lookup of the current open borrow by serial

Filter:

- `ReturnedAtUtc IS NULL`

### 7.3 `IX_BorrowLog_Returned_BorrowedAt`

Purpose:

- support returned-history browsing

## 8. Application Read Patterns

The repository issues these main read patterns:

- resolve item by exact serial from `dbo.Item`
- resolve open borrow by exact serial from `dbo.BorrowLog`
- load active employees with departments
- load active companies
- page open rows
- page returned-history rows

## 9. Application Write Patterns

### 9.1 Borrow

Write type:

- `INSERT` into `dbo.BorrowLog`

### 9.2 Return

Write type:

- `UPDATE` existing row in `dbo.BorrowLog`

Condition:

- only updates rows where `ReturnedAtUtc IS NULL`

## 10. Data Rules In Practice

- a borrower must be a valid employee with department
- a returner must be a valid employee with department
- encoder user must be valid
- one item cannot have two concurrent open rows
- history rows are rows with non-null `ReturnedAtUtc`

## 11. Retention

No explicit purge or archive rule is implemented in the borrow schema itself.

Operational implication:

- retention policy is procedural unless separate DBA controls exist outside this repository

## 12. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
