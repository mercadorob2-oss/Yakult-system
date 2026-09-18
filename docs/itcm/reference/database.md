# Technical Reference: ITCM - Database Guide

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | DBAs + IT technical |
| System | IT Call Monitoring (ITCM) |
| Version | 1.0 |
| Effective date | 2026-03-10 |
| Last updated | 2026-03-10 |

## 1. Purpose

This document identifies where the ITCM database objects live in the repository and provides baseline validation checks used during deployment and troubleshooting.

## 2. Scope

Applies to ITCM database objects (tables, views, stored procedures) referenced by the WinForms subsystem.

## 3. Source of truth (repo paths)

- Production DB project: `DATABASES/Yakult-DB-Production/`
- Prototype/alt project: `DATABASES-PROTOTYPE/YIMS_PROD/`

Within the production project:

- Tables: `DATABASES/Yakult-DB-Production/dbo/Tables/Call*.sql`
- Stored procedures: `DATABASES/Yakult-DB-Production/dbo/Stored Procedures/sp_Call_*.sql`
- Views: `DATABASES/Yakult-DB-Production/dbo/Views/vw_Call_*.sql`

## 4. Core object inventory (high level)

### 4.1 Tickets

- `CallTicket`
- `CallTicketHistory`
- `CallTicketNote`

### 4.2 Escalations

- `CallEscalationSettings`
- `CallTicketEscalationOverride`
- `sp_Call_ProcessEscalations`

### 4.3 Email and notifications

- `CallNotificationRules`
- `CallEmailSettings`
- `CallEmailTemplate`
- `CallEmailLog`
- `sp_Call_LogEmail`

## 5. Operational validation checks (starter queries)

Confirm core objects exist (no destructive changes):

- `SELECT TOP 1 * FROM dbo.CallTicket;`
- `SELECT TOP 1 * FROM dbo.CallTicketHistory;`
- `SELECT TOP 1 * FROM dbo.CallTicketNote;`
- `SELECT TOP 1 * FROM dbo.vw_Call_TicketList;` (if used in your environment)
- `SELECT TOP 1 * FROM dbo.CallEmailLog ORDER BY DateSent DESC;` (if email enabled)

## 6. Deployment notes

Deployment method depends on your process (SSDT publish, migration scripts, DBA-managed deploys).

When ITCM shows “schema missing” or diagnostics schema checks fail, the minimum requirements are:

- ITCM tables exist in the target database.
- Required views exist (if the UI/repository expects them).
- Stored procedures exist and are executable by the SQL user configured in the WinForms app.

## 7. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-10 | Initial release |

