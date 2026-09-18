# SOP / WI: IT Call Monitoring (ITCM) - Overview

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | IT technical + IT support |
| System | IT Call Monitoring (ITCM) |
| Location | WinForms app: `Yakult.Inventory.App` |
| Version | 1.0 |
| Effective date | 2026-03-10 |
| Last updated | 2026-03-10 |

## 1. Purpose

This document defines what IT Call Monitoring (ITCM) is, what it depends on, and what parts of the repository belong to ITCM. It serves as the baseline reference for IT support and technical teams.

## 2. Scope

Included:

- WinForms UI and workflows
  - Entry shell: `Yakult.Inventory.App/Forms/CallMonitoringDashboard.cs`
  - ITCM forms: `Yakult.Inventory.App/Forms/CallMonitoring/*`
  - ITCM pages: `Yakult.Inventory.App/Pages/CallMonitoring/*`
- Data access and services
  - Repository: `Yakult.Inventory.App/Repositories/CallMonitoringRepository*.cs`
  - Notification/email services: `Yakult.Inventory.App/Services/CallEmailNotificationService.cs`
- Database objects
  - Tables: `DATABASES/Yakult-DB-Production/dbo/Tables/Call*.sql`
  - Stored procedures: `DATABASES/Yakult-DB-Production/dbo/Stored Procedures/sp_Call_*.sql`
  - Views: `DATABASES/Yakult-DB-Production/dbo/Views/vw_Call_*.sql`

Excluded:

- Web/API/portal/scanner components (documented separately)

## 3. Definitions

- Ticket: An ITCM work item representing an IT call/request/issue.
- Assignee/Responsible person: Person accountable for handling the ticket.
- Escalation: A rule or action that increases attention/priority or changes assignment.
- Notification: An email generated based on ticket events/rules.

## 4. Responsibilities

- IT Support / IT Technical
  - Operate ITCM daily (ticket handling, updates, resolutions)
  - Maintain email configuration (SMTP profiles, templates, recipients) if used
  - Perform first-level troubleshooting and evidence collection
- All ITCM roles
  - Operate with equal access in the current policy
  - Have full administrative access, including ticket handling, templates, notification rules, email settings, and diagnostics
- DB Support / DBA (if separate from IT Support)
  - Ensure ITCM schema exists in the correct database
  - Maintain backup/restore procedures and access controls

## 5. Environments

Official environments currently documented for ITCM:

- `DEV`
- `PROD`

No separate `UAT` environment is currently documented.

## 6. Dependencies

ITCM requires:

- Windows machine running the WinForms app
- SQL Server reachable from the client (network + firewall)
- DB credentials configured in the app (per Windows user profile)
- SMTP access only if email notifications are enabled

## 7. Process Summary (ticket lifecycle)

Typical ticket flow:

1. Create ticket
2. Assign to an assignee / responsible person
3. Add notes and status updates
4. Escalate (manual or automatic, depending on configuration)
5. Notify recipients (if configured)
6. Resolve/close ticket
7. Review reports and dashboards

## 8. Policy summary

Current approved ITCM policy for documentation:

- Final statuses: `Solved`, `Closed`
- Non-final working status: `Resolved (Temporary)`
- Priority set: `Low`, `Medium`, `High`, `Critical`
- Reminder after `1` day of inactivity
- Supervisor escalation after `2` days
- Manager escalation after `3` days
- Overdue threshold after `3` days

## 9. Records and logs

### 7.1 Database records

Primary ITCM tables include:

- `CallTicket` (main ticket)
- `CallTicketHistory`, `CallTicketNote` (audit trail and notes)
- `CallEscalationSettings`, `CallTicketEscalationOverride` (escalation configuration)
- `CallNotificationRules` (notification rules)
- `CallEmailSettings`, `CallEmailTemplate`, `CallEmailLog` (email settings, templates, delivery log)

### 7.2 Application logs (WinForms)

The WinForms app writes local log files:

- Directory: `%AppData%\\YakultInventoryApp\\Logs`
- File pattern: `log_YYYY-MM-DD.txt` (UTC timestamps)

Evidence checklist is documented in `docs/itcm/reference/logs-and-evidence.md`.

## 10. Related documents (Diataxis mapping)

- How-to: `docs/itcm/how-to/admin-runbook.md`
- How-to: `docs/itcm/how-to/troubleshooting.md`
- Reference: `docs/itcm/reference/database.md`
- Reference: `docs/itcm/reference/status-and-priority.md`
- Explanation: `docs/itcm/explanation/technical-design.md`
- Explanation: `docs/itcm/explanation/escalation-and-notification-logic.md`

## 11. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-10 | Initial release |
| 1.1 | 2026-03-11 | Added approved environment, access, status, and escalation policy |
