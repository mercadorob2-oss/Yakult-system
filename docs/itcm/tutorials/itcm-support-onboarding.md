# Tutorial: ITCM Support Onboarding (WinForms Only)

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | New IT support / IT technical staff |
| System | IT Call Monitoring (ITCM) |
| Version | 1.0 |
| Effective date | 2026-03-10 |
| Last updated | 2026-03-10 |

## 1. What you will learn

By the end of this tutorial, you will be able to:

- Open ITCM and confirm it is healthy (Diagnostics)
- Configure DB connectivity on a workstation (per user profile)
- Understand the ticket lifecycle and what “escalation” means in the system
- Validate email notifications and interpret email logs
- Collect the correct evidence when something breaks

Estimated time: 45–90 minutes (depending on access to DB and SMTP).

## 2. Prerequisites

- Access to a test/non-production environment (recommended)
- A SQL account to connect to the ITCM database
- A test recipient email address (for SMTP test)
- The WinForms app installed or runnable from Visual Studio

## 3. Step-by-step walkthrough

### Step 1: Open ITCM and locate Diagnostics

1. Launch the WinForms application.
2. Log in with your support account.
3. Open IT Call Monitoring.
4. Navigate to **Diagnostics**.

Expected outcome:

- Diagnostics loads without errors.
- You can see server/database information after refresh.

### Step 2: Confirm database connectivity

If the DB setup dialog appears:

- Follow `docs/itcm/how-to/configure-db-connection.md`

If it does not appear:

1. In Diagnostics, click **Refresh**.
2. Confirm server/database are correct for your environment.

### Step 3: Confirm schema readiness

1. In Diagnostics schema checks, confirm core ticket tables exist.
2. If items are missing, follow:
   - `docs/itcm/how-to/verify-itcm-schema.md`

### Step 4: Learn statuses and priorities

Read:

- `docs/itcm/reference/status-and-priority.md`

Exercise:

- Create a test ticket and observe default status/priority.
- Change status to `In Progress` and then `Solved` (if permitted in your test environment).

### Step 5: Validate email pipeline (optional but recommended)

1. Open Diagnostics.
2. Run **SMTP Test** with:
   - A test TicketId
   - Template type `NewTicket` or `Reminder`
   - Your test recipient address
3. Confirm receipt.
4. Review `CallEmailLog` list and interpret outcomes (Sent/Skipped/Failed).

If you cannot send email due to environment policy, document that email notifications are “disabled by design” in that environment and ensure `CallNotificationRules` aligns with the policy.

### Step 6: Understand background reminders and auto-escalations

Read:

- `docs/itcm/how-to/run-reminders-and-auto-escalations.md`
- `docs/itcm/explanation/escalation-and-notification-logic.md`

Exercise:

- Observe job status timestamps in Diagnostics over at least one 15-minute interval.

### Step 7: Practice evidence collection

When you encounter a known problem (or simulate one), practice collecting:

- Diagnostics snapshot (Copy)
- Local log file path
- Ticket IDs and timestamps

Reference:

- `docs/itcm/reference/logs-and-evidence.md`

## 4. Completion checklist

- [ ] Can open ITCM Diagnostics and refresh successfully
- [ ] Understand statuses/priorities and their constraints
- [ ] Can configure DB connectivity for a new user profile
- [ ] Can interpret `CallEmailLog` and identify skipped vs failed
- [ ] Can explain what triggers auto escalation and how to verify it
- [ ] Can collect the correct evidence for escalation to development

## 5. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-10 | Initial release |

