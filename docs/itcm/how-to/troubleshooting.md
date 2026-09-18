# Work Instruction: ITCM - Troubleshooting / Incident Checklist

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | IT support |
| System | IT Call Monitoring (ITCM) |
| Version | 1.0 |
| Effective date | 2026-03-10 |
| Last updated | 2026-03-10 |

## 1. Purpose

This document provides a standard support checklist to diagnose and resolve common ITCM issues in a consistent and auditable manner.

## 2. Scope

Applies to ITCM usage inside the WinForms application only.

## 3. Triage checklist (first response)

1. Confirm the user can open the ITCM dashboard in WinForms.
2. Confirm DB connectivity from the workstation.
3. Open the ITCM Diagnostics screen and click **Refresh**.
4. Check local application logs:
   - `%AppData%\\YakultInventoryApp\\Logs\\log_YYYY-MM-DD.txt`

## 4. Common incidents and resolutions

### A) “Database connection is not configured”

Symptoms:

- Error message indicating DB is not configured, or features are disabled because the connection string is empty.

Cause:

- The per-user DB connection string is missing/empty.

Fix:

- Open the DB setup dialog and save a known-good connection string:
  - `Yakult.Inventory.App/Pages/Admin/DBConn/DatabaseSetupForm.cs`
- Re-open ITCM and confirm Diagnostics shows correct server/database.

### B) ITCM shows “schema missing” / features disabled

Symptoms:

- Diagnostics schema checks show missing tables/views/procs.
- Ticket lists fail to load.

Cause:

- ITCM objects are not deployed in the target database, or the SQL user lacks permissions.

Fix:

- Validate DB object presence:
  - `docs/itcm/how-to/verify-itcm-schema.md`
- Publish/apply the ITCM database project to the correct database if needed.

### C) Email notifications not sending

Symptoms:

- No email received for new tickets/status changes/reminders/escalations.
- `CallEmailLog` shows `Failed` records.

Checks:

- Notification rules: `CallNotificationRules` (enabled flags, reminder days)
- Email settings: `CallEmailSettings` (SMTP server, sender, credentials)
- Templates: `CallEmailTemplate` (active templates for the required email type)
- Recipients: department/branch/global recipient configuration (as implemented in your environment)

Fix:

- Follow `docs/itcm/how-to/configure-email-notifications.md`
- Run SMTP test from the Diagnostics screen.

### D) Auto-escalation not happening

Checks:

- Confirm `CallEscalationSettings` exists and has a recent row.
- Confirm `Employee.Position` contains values matching the configured Supervisor/Manager positions.
- Confirm the current policy timing:
  - Supervisor escalation after `2` days
  - Manager escalation after `3` days
  - Overdue threshold after `3` days
- Confirm background job is running and not blocked by lock:
  - Diagnostics shows background lock probe and job timestamps.

## 5. Evidence to collect (for escalation)

Collect the minimum set of evidence before escalating:

- Repro steps (exact clicks and inputs)
- Timestamp(s) of failure
- Ticket ID(s) affected
- Diagnostics snapshot (copy button)
- Relevant log file path:
  - `%AppData%\\YakultInventoryApp\\Logs\\log_YYYY-MM-DD.txt`
- SQL Server name + database name (do not include passwords)

## 6. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-10 | Initial release |
| 1.1 | 2026-03-11 | Added approved escalation timing checks |
