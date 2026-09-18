# Reference: ITCM Logs and Evidence Collection

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

Standardize what logs and evidence IT support should collect for troubleshooting and escalation, without exposing credentials.

## 2. Local application logs (WinForms)

### Location

- Folder: `%AppData%\\YakultInventoryApp\\Logs`
- File name: `log_YYYY-MM-DD.txt`
- Timestamps: UTC

### What to look for

- Exceptions around ITCM dashboard load
- Email sending failures
- Background job failures (reminder/escalation)

Retention policy:

- Local app logs on PCs: `90 days`

Source:

- Logger: `Yakult.Inventory.App/Core/Logger.cs`

## 3. ITCM Diagnostics snapshot

The ITCM Diagnostics screen provides:

- Server and database name
- Schema checks
- Recent email failures/skips
- Background job status snapshot and lock probe

Use the **Copy** button in Diagnostics to capture a safe text snapshot for support tickets.

## 4. Database evidence

### 4.1 Ticket audit trail

- `CallTicketHistory` shows structured changes (status, assignment, auto escalation notes)
- `CallTicketNote` stores user-entered notes

### 4.2 Email delivery evidence

- `CallEmailLog` records per-recipient results:
  - `Status` (e.g., Sent / Failed / Skipped)
  - `ErrorMessage` (for Failed/Skipped)
  - `EmailType` (e.g., NewTicket, Escalation, Reminder)
  - `DateSent` (UTC)

Retention policy:

- `CallEmailLog`: `1 year`

### 4.3 Ticket data retention

- `CallTicket`: indefinite
- `CallTicketHistory`: indefinite
- `CallTicketNote`: indefinite

## 5. Evidence collection checklist (for escalation)

Collect:

- TicketId(s) affected
- Exact steps to reproduce (click path)
- Timestamps (local time and/or UTC)
- Diagnostics snapshot (Copy output)
- Local log file path:
  - `%AppData%\\YakultInventoryApp\\Logs\\log_YYYY-MM-DD.txt`
- Relevant DB evidence (provided by DBA, if needed):
  - `CallTicketHistory` rows for those TicketIds
  - `CallEmailLog` rows for the relevant time window

Do not include:

- Database passwords
- SMTP passwords
- Full connection strings with credentials

## 6. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-10 | Initial release |
| 1.1 | 2026-03-11 | Added approved retention policy for tickets, email logs, and local logs |
