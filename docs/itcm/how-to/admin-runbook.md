# Work Instruction: ITCM - Admin / Support Runbook (WinForms)

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | IT technical + IT support |
| System | IT Call Monitoring (ITCM) |
| Version | 1.0 |
| Effective date | 2026-03-10 |
| Last updated | 2026-03-10 |

## 1. Purpose

This runbook provides the day-to-day setup and operational procedures used by IT support to keep ITCM functioning inside the WinForms application.

## 2. Scope

This runbook covers:

- Workstation prerequisites and access requirements
- Per-user DB connection configuration
- Database schema presence checks (ITCM objects)
- ITCM email pipeline (settings, templates, rules, logs)
- Routine operational checks and escalation criteria

## 3. Preconditions / Requirements

### 3.1 Workstation requirements

- Windows PC capable of running `Yakult.Inventory.App` (.NET Framework 4.8)
- Stable network access to the SQL Server

### 3.2 Accounts and permissions

- A SQL account (or Windows auth, if used in your environment) with required permissions for ITCM objects
- SMTP access only if ITCM email notifications are enabled

Support note:

- ITCM uses a per-user connection string stored under the current Windows profile. If multiple Windows accounts use the same PC, each will need connectivity configured.

## 4. Operating policy

Current documented ITCM operating policy:

- All ITCM roles have equal access
- All ITCM roles have full admin capability, including email/template/settings changes
- Final statuses: `Solved`, `Closed`
- Non-final working status: `Resolved (Temporary)`
- Priority set: `Low`, `Medium`, `High`, `Critical`
- Environments: `DEV`, `PROD`
- Deployment owner for ITCM changes: developer
- Incident escalation path:
  - IT Support
  - Supervisor
  - Manager

## 5. Procedures

### 4.1 Install / update the desktop application

The repository includes an installer:

- Installer artifacts: `Yakult.Inventory.App/Installer/`
- Inno Setup script: `Yakult.Inventory.App/Installer/YakultInventory_Setup.iss`

Minimum acceptance checks after install/update:

- App launches without crash.
- User can log in.
- ITCM dashboard opens.

### 4.2 Configure database connectivity (per user)

If the application detects missing DB credentials at startup, it opens a setup dialog:

- Form: `Yakult.Inventory.App/Pages/Admin/DBConn/DatabaseSetupForm.cs`
- Storage: user setting `Properties.Settings.Default.DbConnectionString`

Steps:

1. Launch the application.
2. In the DB Setup dialog, enter:
   - Server (hostname/IP, and port if needed)
   - Database name (e.g., `YIMS` per your environment)
   - Username and password
3. Click **Test Connection** (must succeed).
4. Click **Save & Continue**.

Acceptance criteria:

- A successful test confirms the workstation can connect using the provided account.
- If ITCM still shows schema errors, verify schema deployment (see `docs/itcm/how-to/verify-itcm-schema.md`).

### 4.3 Verify ITCM schema is installed

Use either:

- The ITCM Diagnostics screen inside the app (recommended), or
- Manual SQL checks (see `docs/itcm/how-to/verify-itcm-schema.md`)

### 4.4 Configure and validate ITCM email pipeline (if used)

Email pipeline is DB-driven:

- Rules: `CallNotificationRules`
- Settings: `CallEmailSettings`
- Templates: `CallEmailTemplate`
- Delivery logs: `CallEmailLog`

Procedure:

- See `docs/itcm/how-to/configure-email-notifications.md`

### 4.5 Background reminder/escalation job behavior

The WinForms app runs a background process from the ITCM dashboard:

- Initial due time: ~2 minutes after starting the ITCM dashboard
- Period: every 15 minutes
- Work executed:
  - Reminder notifications (if enabled)
  - Auto-escalations (if enabled/configured)

Only one client should execute this work at a time. A SQL application lock is used to avoid duplicates:

- Lock name: `Yakult.Inventory.App|CallMonitoring|BackgroundJobs`

Support procedure:

- See `docs/itcm/how-to/run-reminders-and-auto-escalations.md`

### 4.6 Routine Day-2 checks (support checklist)

Daily/shift checklist:

1. Open ITCM dashboard; confirm it loads.
2. Open Diagnostics; confirm:
   - Connected server and database are correct
   - Required schema checks pass
   - Background lock probe is healthy (not stuck)
3. Review “requires attention” tickets (overdue/unassigned/high priority).
4. If email is enabled:
   - Check recent `CallEmailLog` for failures
   - Send a test email from the Diagnostics screen if needed
5. Review local logs for errors:
   - `%AppData%\\YakultInventoryApp\\Logs\\log_YYYY-MM-DD.txt`

## 6. Records / Evidence

- Application logs: `%AppData%\\YakultInventoryApp\\Logs\\log_YYYY-MM-DD.txt`
- Email delivery history: `CallEmailLog`
- Ticket audit trail: `CallTicketHistory`, `CallTicketNote`

## 7. Retention policy

- Tickets: indefinite
- Ticket history and notes: indefinite
- Email logs: `1 year`
- Local app logs on PCs: `90 days`

## 8. Backup / recovery (DB-focused)

ITCM data is stored in SQL Server; recovery is therefore primarily database restore.

Recommended (confirm with DBAs):

- Regular SQL Server backups (full + log, per policy)
- Periodic restore tests to a non-production environment

## 9. Escalation criteria

Escalate to development when:

- ITCM schema exists but the app fails due to missing columns/views (version mismatch)
- Issues are reproducible with clear steps and logs
- Stored procedures return unexpected results after DB validation

When escalating, include:

- Screenshot(s)
- Repro steps
- Timestamp(s)
- Ticket IDs involved
- Log file path and relevant excerpt (no passwords)
- SQL Server and database name (no passwords)

## 10. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-10 | Initial release |
| 1.1 | 2026-03-11 | Added approved operating policy, retention, and escalation path |
