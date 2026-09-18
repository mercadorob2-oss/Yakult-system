# Technical Reference: ITCM - Technical Design (WinForms)

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | Developers + IT technical |
| System | IT Call Monitoring (ITCM) |
| Version | 1.0 |
| Effective date | 2026-03-10 |
| Last updated | 2026-03-10 |

## 1. Purpose

This document explains how ITCM is implemented in the WinForms application (major components, responsibilities, database interaction, logging, and key technical behaviors).

## 2. Scope

Applies to ITCM implementation within `Yakult.Inventory.App`, including its supporting database objects.

## 3. Architecture (subsystem view)

ITCM runs inside the WinForms application; most data access is organized through repositories, with supporting services for email/SMTP.

- UI entry: `Yakult.Inventory.App/Forms/CallMonitoringDashboard.cs`
- Ticket domain repository: `Yakult.Inventory.App/Repositories/CallMonitoringRepository*.cs`
- Notification service: `Yakult.Inventory.App/Services/CallEmailNotificationService.cs`
- Persistence: SQL Server (tables/SPs/views under `DATABASES/`)

## 4. Key modules and responsibilities

### 4.1 UI shell

- `CallMonitoringDashboard` is the main ITCM container.
  - Hosts dashboard, ticket management, email/logs, reports, diagnostics, profile/config screens.

### 4.2 Repository layer

- `CallMonitoringRepository` encapsulates most ITCM database access.
- Uses `Dapper` + `SqlConnection`.
- Defers connection validation until execution (so forms can open without immediate crash).

### 4.3 Schema gating (compatibility behavior)

The repository checks for the existence of tables/columns before selecting certain objects:

- Example: view columns in `dbo.vw_Call_TicketList` can vary across environments.
- Behavior: probe with `OBJECT_ID` / `COL_LENGTH`; if missing, use safe fallbacks (e.g., `NULL` placeholder).

Code locations:

- `Yakult.Inventory.App/Repositories/CallMonitoringRepository.cs`
- `Yakult.Inventory.App/Repositories/CallSchemaGate.cs`

## 5. Background job behavior (reminders and auto-escalation)

The ITCM dashboard starts a timer that triggers work on an interval (due ~2 minutes, then every 15 minutes).

To prevent duplicate sends when multiple admin clients are open:

- A SQL application lock is used: `Yakult.Inventory.App|CallMonitoring|BackgroundJobs`

Primary code locations:

- `Yakult.Inventory.App/Forms/CallMonitoringDashboard.cs` (timer scheduling)
- `Yakult.Inventory.App/Services/CallEmailNotificationService.cs` (job logic + job status snapshot)
- `Yakult.Inventory.App/Repositories/CallMonitoringRepository.EmailNotifications.cs` (lock probe + DB reads/writes)

## 6. Logging

### 6.1 WinForms application logs

- Directory: `%AppData%\\YakultInventoryApp\\Logs`
- File pattern: `log_YYYY-MM-DD.txt` (UTC timestamps)
- Logger: `Yakult.Inventory.App/Core/Logger.cs`

### 6.2 Database logs (email delivery)

- Table: `CallEmailLog` records per-recipient delivery status and errors.

## 7. Security notes

- DB credentials are stored per Windows user profile in user settings (`DbConnectionString`).
- Email SMTP passwords are stored encrypted in DB (`SmtpPasswordEnc`) for ITCM email settings tables.
- Follow least-privilege principles for SQL users and SMTP access.

## 8. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-10 | Initial release |

