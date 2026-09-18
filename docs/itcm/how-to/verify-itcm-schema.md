# How-to: Verify ITCM Schema (DB Objects) Exists and Is Compatible

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | IT support + DB support |
| System | IT Call Monitoring (ITCM) |
| Version | 1.0 |
| Effective date | 2026-03-10 |
| Last updated | 2026-03-10 |

## 1. Goal

Confirm the target database contains the ITCM tables/views/stored procedures required by the WinForms ITCM subsystem.

## 2. Preferred method: use the built-in Diagnostics screen

The WinForms ITCM UI includes a Diagnostics view implemented in:

- `Yakult.Inventory.App/Forms/CallMonitoring/CallMonitoringDiagnosticsForm.cs`

Procedure:

1. Open ITCM dashboard.
2. Go to **Diagnostics**.
3. Click **Refresh**.
4. Review:
   - Server name + database name
   - Schema check list (tables/views/procs)
   - Email pipeline status and last failures (if email enabled)
   - Background lock probe (to detect stuck background processing)
5. Use **Copy** to copy a support-friendly snapshot when escalating.

This is the fastest way for IT support to confirm “is the database ready for ITCM?” without running SQL manually.

## 3. Manual method: minimal DB object checklist

### 3.1 Core ticket tables

- `dbo.CallTicket`
- `dbo.CallTicketHistory`
- `dbo.CallTicketNote`

### 3.2 Escalation configuration tables

- `dbo.CallEscalationSettings`
- `dbo.CallTicketEscalationOverride` (if overrides are enabled in the environment)

### 3.3 Email pipeline tables (only if email is used)

- `dbo.CallNotificationRules`
- `dbo.CallEmailSettings`
- `dbo.CallEmailTemplate`
- `dbo.CallEmailLog`

### 3.4 Views (commonly used by the app)

- `dbo.vw_Call_TicketList`
- Additional ITCM views under `dbo.vw_Call_*` may be used for dashboards/reports.

### 3.5 Stored procedures (ITCM)

Common stored procedures include:

- `dbo.sp_Call_CreateTicket`
- `dbo.sp_Call_AssignTicket`
- `dbo.sp_Call_AddTicketNote`
- `dbo.sp_Call_SetTicketStatus`
- `dbo.sp_Call_SetTicketPriority`
- `dbo.sp_Call_GetEscalationSettings`
- `dbo.sp_Call_ProcessEscalations`
- `dbo.sp_Call_LogEmail`
- `dbo.sp_Call_GetTicketEscalationOverride`
- `dbo.sp_Call_SetTicketEscalationOverride`

## 4. Starter SQL checks (read-only)

Run only with DBA approval in production.

```sql
-- Core tables
SELECT TOP 1 * FROM dbo.CallTicket;
SELECT TOP 1 * FROM dbo.CallTicketHistory;
SELECT TOP 1 * FROM dbo.CallTicketNote;

-- View used by ticket lists (if present)
SELECT TOP 1 * FROM dbo.vw_Call_TicketList;

-- Email tables (if enabled)
SELECT TOP 1 * FROM dbo.CallNotificationRules ORDER BY UpdatedAt DESC;
SELECT TOP 1 * FROM dbo.CallEmailSettings ORDER BY UpdatedAt DESC;
SELECT TOP 1 * FROM dbo.CallEmailTemplate ORDER BY UpdatedAt DESC;
SELECT TOP 1 * FROM dbo.CallEmailLog ORDER BY DateSent DESC;
```

## 5. If schema is missing: what to deploy

### 5.1 Source of schema in this repo

- Primary SQL project: `DATABASES/Yakult-DB-Production/`

### 5.2 Installer script used by the WinForms project

The WinForms project also includes an idempotent install script intended for SSMS execution:

- `Yakult.Inventory.App/Database/CallMonitoring.Prod.Install.sql`

This script is designed to be safe to run multiple times (creates missing objects, uses `CREATE OR ALTER` where applicable).

## 6. Compatibility warning: status values

In the DB layer, `sp_Call_SetTicketStatus` validates a canonical status list. If your organization uses additional status values, you may need to update the database procedure and/or UI configuration accordingly.

Canonical list enforced by the procedure includes:

- `Pending`
- `In Progress`
- `Escalated`
- `Resolved (Temporary)`
- `Solved`
- `Reopened`

See `docs/itcm/reference/status-and-priority.md` for details.

## 7. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-10 | Initial release |

