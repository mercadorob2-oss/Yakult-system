# How-to: Configure ITCM Email Notifications (Rules, Templates, SMTP, Logs)

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

Enable and validate ITCM email notifications so relevant recipients receive emails for:

- New tickets
- Status changes
- Escalations
- Reminders (optional)

## 2. Overview of the email pipeline

Email notifications are configured and tracked through database tables:

- Rules: `dbo.CallNotificationRules`
- Sender settings: `dbo.CallEmailSettings` (SMTP server + sender identity)
- Templates: `dbo.CallEmailTemplate` (subject/body, active flag)
- Delivery log: `dbo.CallEmailLog` (per-recipient status and errors)

WinForms orchestration:

- `Yakult.Inventory.App/Services/CallEmailNotificationService.cs`

Support UI:

- Email setup and log screens under ITCM
- Diagnostics includes deep links and a “Run SMTP Test” action

## 3. Template types (required)

The system uses these canonical template types (stored in `CallEmailTemplate.TemplateType`):

- `NewTicket`
- `StatusUpdate`
- `Escalation`
- `Reminder`

If templates are missing or inactive, emails are skipped and logged with an actionable message.

## 4. Procedure

### 4.1 Configure notification rules

Table: `dbo.CallNotificationRules`

Key fields:

- `NotifyOnNewTicket` (true/false)
- `NotifyOnStatusChange` (true/false)
- `NotifyOnEscalation` (true/false)
- `NotifyOnReminder` (true/false)
- `ReminderDays` (integer; number of idle days before a reminder is eligible)
- Optional emails:
  - `GroupEmail`
  - `EscalationEmail`

Procedure (UI recommended):

1. Open ITCM → Email Notification → Setup → Notification Rules.
2. Enable the required notify flags.
3. Set `ReminderDays` according to support policy if reminders are enabled.
4. Save changes.

### 4.2 Configure SMTP sender settings

Table: `dbo.CallEmailSettings`

Key fields:

- `SmtpServer`
- `SmtpPort` (default 587)
- `UseSsl`
- `SmtpUsername`
- `SmtpPasswordEnc` (encrypted)
- `FromName`
- `FromEmail`

Procedure:

1. Open ITCM → Email Notification → Setup → Email Settings.
2. Enter SMTP server/port/SSL settings provided by your mail administrator.
3. Enter username and password (if required).
4. Set `FromName` and `FromEmail` to the approved sender identity.
5. Save changes.

Security note:

- Passwords are stored encrypted in the database in `SmtpPasswordEnc`. Do not store raw passwords in documentation.

### 4.3 Configure templates

Table: `dbo.CallEmailTemplate`

For each template type (NewTicket, StatusUpdate, Escalation, Reminder):

1. Open ITCM → Email Notification → Setup → Templates.
2. Verify the template exists and is marked active.
3. Update subject/body text according to your corporate messaging standards.
4. Save changes.

Operational guidance:

- Keep subjects short and consistent so recipients can filter.
- Include TicketCode or TicketId in the subject for quick searching.

### 4.4 Validate with Diagnostics SMTP test

1. Open ITCM → Diagnostics.
2. Click **Run SMTP Test**.
3. Provide:
   - A known test TicketId
   - Email type (mapped to template type)
   - A test recipient email address
4. Confirm the email is received.

If the test fails:

- Review error details in the dialog.
- Check `CallEmailLog` for `Failed` rows and error messages.

### 4.5 Validate actual notification events

Perform controlled tests (non-production preferred):

- Create a test ticket (expect NewTicket email)
- Change status (expect StatusUpdate email)
- Trigger escalation (manual escalation, or allow auto escalation to run) (expect Escalation email)
- Allow reminder eligibility (or simulate) (expect Reminder email)

## 5. Troubleshooting checklist

### 5.1 Emails are not being sent at all

Check:

- SMTP settings exist and are valid (`CallEmailSettings`)
- Sender resolution is configured for the ticket scope (branch/department rules as implemented)
- Recipient resolution returns at least one valid email address
- Templates exist and are active
- Background job is running (for reminders/auto escalation)

### 5.2 Emails are being skipped

Check the skip reason in logs:

- `CallEmailLog.Status = 'Skipped'` and `ErrorMessage` contains actionable guidance (when available).

Typical causes:

- Notify flags disabled in `CallNotificationRules`
- Template missing/inactive
- No recipients resolved (empty recipient lists)
- No SMTP sender resolved for that ticket scope

### 5.3 Emails are failing

Check:

- `CallEmailLog.Status = 'Failed'`
- SMTP credentials and policy (TLS/SSL requirements, authentication)
- Mail server throttling or blocked sender

## 6. Evidence to collect

When escalating, include:

- TicketId tested and EmailType used
- Recipient email address used in the test
- Copy of Diagnostics snapshot
- Recent `CallEmailLog` entries (without exposing credentials)
- Local logs if exceptions occurred:
  - `%AppData%\\YakultInventoryApp\\Logs\\log_YYYY-MM-DD.txt`

## 7. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-10 | Initial release |

