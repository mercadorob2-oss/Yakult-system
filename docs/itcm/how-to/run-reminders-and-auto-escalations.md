# How-to: Run and Verify ITCM Reminders and Auto-Escalations

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | IT support + IT technical |
| System | IT Call Monitoring (ITCM) |
| Version | 1.0 |
| Effective date | 2026-03-10 |
| Last updated | 2026-03-10 |

## 1. Goal

Ensure reminder emails and auto-escalation logic are running and not duplicated across multiple client machines.

Current approved timing defaults:

- Reminder after `1` day of inactivity
- Supervisor escalation after `2` days
- Manager escalation after `3` days
- Overdue threshold after `3` days

## 2. What runs automatically

When the ITCM dashboard is open, the application schedules background work:

- First run: approximately 2 minutes after starting ITCM dashboard
- Repeat: every 15 minutes

Work performed:

- Reminder processing (if enabled by `CallNotificationRules.NotifyOnReminder`)
- Auto-escalation processing (if enabled/configured in the environment)

## 3. How duplication is prevented

The system uses a SQL application lock so only one client runs background work at a time:

- Lock name: `Yakult.Inventory.App|CallMonitoring|BackgroundJobs`
- Behavior: if the lock cannot be acquired immediately, the job run is skipped for that cycle.

This is intended to prevent duplicate background sends when multiple admin/developer clients are open.

## 4. Verification procedure (recommended: Diagnostics screen)

1. Open ITCM → Diagnostics.
2. Click **Refresh**.
3. Review “Job status” fields:
   - Last run start/end time (UTC)
   - Next run time (UTC)
   - Lock probe / lock availability
   - Counts: candidates vs sent/applied
4. If you suspect a stuck background lock:
   - Use the lock probe output.
   - Confirm no other client is holding the lock (close other admin ITCM clients).

## 5. What to check when reminders do not send

1. Confirm rules enabled:
   - `CallNotificationRules.NotifyOnReminder = 1`
   - `ReminderDays` is set to a reasonable value
2. Confirm templates exist and are active:
   - `CallEmailTemplate.TemplateType = 'Reminder'`
3. Confirm SMTP sender settings resolve:
   - `CallEmailSettings` is configured
4. Confirm recipients resolve:
   - Recipient lists are configured for the ticket’s department/branch or global rules email fields
5. Check delivery outcomes:
   - `CallEmailLog` for `Reminder` EmailType rows with status `Sent`, `Skipped`, or `Failed`

## 6. What to check when auto-escalation does not apply

Auto-escalation logic is applied in the database procedure `sp_Call_ProcessEscalations` based on:

- Ticket age in days since `CreatedAt`
- Settings in `CallEscalationSettings` (days thresholds and position names)
- Optional per-ticket overrides in `CallTicketEscalationOverride`
- Employee master data:
  - A supervisor employee is selected by matching `Employee.Position` (default `IT Supervisor`)
  - A manager employee is selected by matching `Employee.Position` (default `IT Manager`)

Checklist:

1. Confirm `CallEscalationSettings` has an active row.
2. Confirm at least one active employee exists for the configured positions.
3. Confirm the ticket is not in a final state:
   - Auto escalation excludes `Solved` and `Resolved (Temporary)` statuses.
4. Confirm job runs are occurring in Diagnostics.
5. Review `CallTicketHistory` for “AutoEscalation” entries.

## 7. Evidence to collect

- Diagnostics snapshot (Copy)
- Ticket IDs expected to be escalated/reminded
- Recent rows in `CallEmailLog` (reminders/escalation emails)
- `CallTicketHistory` rows for those TicketIds
- Local app logs if exceptions occurred:
  - `%AppData%\\YakultInventoryApp\\Logs\\log_YYYY-MM-DD.txt`

## 8. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-10 | Initial release |
| 1.1 | 2026-03-11 | Added approved timing defaults for reminder and escalation behavior |
