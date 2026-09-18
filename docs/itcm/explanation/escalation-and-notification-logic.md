# Explanation: ITCM Escalation and Notification Logic

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | IT technical + developers |
| System | IT Call Monitoring (ITCM) |
| Version | 1.0 |
| Effective date | 2026-03-10 |
| Last updated | 2026-03-10 |

## 1. Purpose

Explain how ITCM decides:

- When a ticket should be auto-escalated and to whom
- When emails should be sent vs skipped
- How recipients and SMTP sender are resolved

This document is intended for technical readers and for diagnosing “why did this escalate?” or “why was email skipped?” questions.

## 2. Auto-escalation (database-driven)

Auto-escalation is implemented primarily in:

- Stored procedure: `dbo.sp_Call_ProcessEscalations`
- Settings table: `dbo.CallEscalationSettings`
- Optional override table: `dbo.CallTicketEscalationOverride`

### 2.1 Inputs and defaults

Escalation settings:

- `DaysToSupervisor` (documented default: `2`)
- `DaysToManager` (documented default: `3`)
- `SupervisorPosition` (default: `IT Supervisor`)
- `ManagerPosition` (default: `IT Manager`)

The procedure identifies the target employees by selecting the minimum active `EmpId` where `Employee.Position` matches those configured position names (case-insensitive).

### 2.2 Eligibility

Tickets are considered eligible if:

- Status is not in final resolved states (current support policy treats `Solved` and `Closed` as final; `Resolved (Temporary)` is documented as working but non-final).
- Ticket age in days reaches the configured threshold.
- Overrides, if present, take precedence for that ticket.

### 2.3 Actions and auditability

When auto escalation is applied, the procedure:

- Updates ticket assignment to supervisor or manager
- Sets status to `Escalated` when escalating to manager (if not already)
- Writes to `CallTicketHistory` with `FieldName = 'AutoEscalation'` and a note describing what happened

Operationally, IT support can verify escalation by reviewing:

- Diagnostics “Last auto escalation” field (if available)
- `CallTicketHistory` for the ticket

## 3. Reminder notifications and eligibility

Reminder behavior is driven by notification rules:

- Table: `CallNotificationRules`
- Field: `NotifyOnReminder`
- Field: `ReminderDays` (documented default: `1`)

The background job:

- Periodically fetches tickets needing reminders
- Attempts to send reminders
- Marks reminders as sent on success

Eligibility is based on idle/last-contact timing as computed by the repository/service.

## 4. Notification rules: send vs skip

For each email event type (NewTicket, StatusUpdate, Escalation, Reminder), the system checks:

1. Are notifications enabled in `CallNotificationRules` for that event?
2. Does an active template exist in `CallEmailTemplate` for that template type?
3. Can recipients be resolved for the ticket’s department/branch?
4. Can an SMTP sender be resolved for the ticket’s department/branch?

If any required condition fails, the email is not sent and a “Skipped” outcome is logged with an actionable reason when possible.

## 5. Recipient and sender resolution (high level)

The system resolves:

- Recipients from a combination of:
  - Branch/department configured recipients (including escalation-specific recipients when applicable)
  - Global rule emails (`GroupEmail` and `EscalationEmail`)
- Sender from configured SMTP settings scoped by branch/department rules (as implemented by repository methods)

Diagnostics can show a “recipient resolution trace” for a ticket to explain exactly why a set of recipients was chosen.

## 6. Concurrency and duplication control

Background sends are protected by a SQL application lock:

- `Yakult.Inventory.App|CallMonitoring|BackgroundJobs`

If the lock is not acquired:

- The run is skipped, preventing duplicates when multiple clients are open.

## 7. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-10 | Initial release |
| 1.1 | 2026-03-11 | Aligned explanation with approved reminder, escalation, and final-status policy |
