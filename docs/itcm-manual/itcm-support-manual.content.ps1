$Manual = [ordered]@{
    Title = 'IT Call Monitoring (ITCM) - IT Support Manual'
    Subtitle = 'Operations, Support, and Administration Guide'
    Version = 'v1.1'
    Date = '2026-03-11'
    Notes = @(
        'This manual is intended for IT support and IT technical staff who operate, configure, monitor, and troubleshoot IT Call Monitoring inside the WinForms application.'
        'This document reflects the currently approved internal policy: all ITCM roles have equal full-admin access, final statuses are Solved and Closed, and Resolved (Temporary) is a valid working status but not a final state.'
    )
    Sections = @(
        @{
            Title = '1) Purpose and Scope'
            Paragraphs = @(
                'IT Call Monitoring (ITCM) is the internal ticketing and operational support module embedded in the Yakult WinForms application. It is used to record support calls, assign ownership, update progress, escalate inactive tickets, send operational emails, and review reports and diagnostics.',
                'This manual covers day-to-day support operations, configuration checks, email setup, escalation behavior, diagnostics, retention, and evidence collection. It is not limited to end-user actions; it is meant for support staff with administrative responsibility.'
            )
            Bullets = @(
                'System location: Yakult.Inventory.App (WinForms desktop application).'
                'Official environments documented: DEV and PROD.'
                'Deployment owner for ITCM changes: developer.'
            )
        }
        @{
            Title = '2) Access Model and Governance'
            Paragraphs = @(
                'Current operating policy gives all ITCM roles equal access. In practice, this means any authorized ITCM operator can create tickets, update status, assign tickets, manage notification settings, edit templates, review logs, and use diagnostics.',
                'Because this is a full-admin model, operational discipline is important. Support staff should avoid changing rules, templates, or sender settings without coordination when the production environment is active.'
            )
            Bullets = @(
                'All ITCM roles have full administrative capability.'
                'Supervisor is the business owner for email sender identity, recipient governance, and template approval.'
                'Developer is responsible for schema and code deployment.'
            )
        }
        @{
            Title = '3) Standard Ticket Lifecycle'
            Paragraphs = @(
                'The standard ticket lifecycle is: Create -> Assign -> Work/Update -> Escalate if needed -> Resolve or Close -> Report/Review.',
                'Only Solved and Closed are treated as final statuses for policy and reporting purposes. Resolved (Temporary) is valid, but it remains a working state because a follow-up or return action may still be required.'
            )
            Bullets = @(
                'Final statuses: Solved, Closed.'
                'Working but non-final status: Resolved (Temporary).'
                'Approved priorities: Low, Medium, High, Critical.'
            )
            Images = @('dashboard.png', 'ticket-list-page.png')
        }
        @{
            Title = '4) Create and Assign Tickets'
            Paragraphs = @(
                'Support staff should create tickets completely and consistently so the ticket can move through the process without rework. Company, caller, and issue description should be accurate before assignment.',
                'Ownership matters. A ticket should be assigned to a responsible technician as early as possible so the queue remains actionable.'
            )
            Bullets = @(
                'Caller Name should match an employee entry from the list.'
                'Technical Problem / Issue description must be clear enough for another technician to continue the work if reassigned.'
                'Assignment should happen as soon as practical after ticket creation.'
            )
            Images = @('new-ticket.png', 'pending-case-details.png')
        }
        @{
            Title = '5) Working a Ticket Properly'
            Paragraphs = @(
                'The Solutions and update areas should be used as the primary operational record. Each important action should be reflected either as a note, a status change, or both.',
                'If a ticket is blocked by another group or outside dependency, the note should state exactly who is involved and what must happen next. This is necessary for clean handoff, escalation, and audit review.'
            )
            Bullets = @(
                'Use notes to record troubleshooting steps, next actions, and follow-up commitments.'
                'Escalated tickets should include a reason for escalation and what support is needed.'
            )
            Images = @('ticket-details.png', 'pending-tickets.png')
        }
        @{
            Title = '6) Resolution and Closure Policy'
            Paragraphs = @(
                'Tickets should be finalized using the proper resolution path so reporting, audit history, and any linked support actions remain accurate.',
                'Use Solved when the issue is fully resolved. Use Closed when the case is administratively complete and no further action is expected. Use Resolved (Temporary) only when the issue is partially stabilized and a later return, replacement, or follow-up is still expected.'
            )
            Bullets = @(
                'Solved: final status.'
                'Closed: final status.'
                'Resolved (Temporary): valid operational state but not final.'
            )
            Images = @('mark-as-resolutions-service.png', 'mark-as-resolutions-replacements.png')
        }
        @{
            Title = '7) Database Connection per User Profile'
            Paragraphs = @(
                'The WinForms application stores the SQL connection string per Windows user profile. Because of this, each Windows account using the workstation may need its own DB setup even on the same PC.',
                'When the DB connection is missing, the application opens a setup dialog and requires a successful Test Connection before Save and Continue is allowed.'
            )
            Bullets = @(
                'Connection setting: Properties.Settings.Default.DbConnectionString.'
                'Authentication model in the current implementation: SQL Login.'
                'If connectivity is valid but ITCM still fails, the next check should be schema presence, not repeated password entry.'
            )
        }
        @{
            Title = '8) Diagnostics and Health Checks'
            Paragraphs = @(
                'Diagnostics is the primary first-line support screen for ITCM. It shows database identity, schema checks, email pipeline signals, recent failures, and background job status.',
                'Before escalating any issue to development, support staff should capture the Diagnostics snapshot and verify whether the problem is configuration, schema, background lock, or actual application behavior.'
            )
            Bullets = @(
                'Use Refresh to reload live health data.'
                'Use Copy to capture a support-safe text snapshot.'
                'Use Open Logs to access local application log files.'
                'Use Run SMTP Test to validate email sender resolution and SMTP connectivity.'
            )
            Images = @('diagnostics-logs.png')
        }
        @{
            Title = '9) Email Notification Administration'
            Paragraphs = @(
                'ITCM email notifications are database-driven. Support staff can manage the sender settings, templates, notification rules, recipients, and log review from the email administration screens.',
                'A notification is only sent when four things align: the event is enabled, a valid template exists, recipients resolve successfully, and an SMTP sender resolves successfully.'
            )
            Bullets = @(
                'Template types used by the system: NewTicket, StatusUpdate, Escalation, Reminder.'
                'Required configuration tables include CallNotificationRules, CallEmailSettings, CallEmailTemplate, and CallEmailLog.'
                'Supervisor is the operational approver for sender identity and template content.'
            )
            Images = @('email-setup.png', 'email-template.png', 'email-logs.png', 'email-branch-dept-smtpconfig.png', 'email-dept-branch.png')
        }
        @{
            Title = '10) Reminder and Auto-Escalation Rules'
            Paragraphs = @(
                'The ITCM dashboard starts a background process that periodically evaluates reminder eligibility and auto-escalation conditions. A SQL application lock is used so multiple open clients do not send duplicate reminders or apply duplicate escalation actions.',
                'Current approved defaults are documented as follows: reminder after 1 day of inactivity, supervisor escalation after 2 days, manager escalation after 3 days, and overdue threshold after 3 days.'
            )
            Bullets = @(
                'Background lock name: Yakult.Inventory.App|CallMonitoring|BackgroundJobs.'
                'Reminder policy: 1 day of inactivity.'
                'Supervisor escalation: 2 days.'
                'Manager escalation: 3 days.'
                'Overdue threshold: 3 days.'
            )
            Images = @('auto-escalation-assignee.png')
        }
        @{
            Title = '11) Reports, Profiles, and Operational Review'
            Paragraphs = @(
                'The reporting and profile areas should be used to review workload distribution, technician handling history, solved volumes, and outstanding attention items. These screens are not just for management reporting; they are also useful for support triage and follow-up planning.',
                'When queues become unclear, support leads should review dashboard attention items, recently solved items, and profile statistics together rather than using only one screen.'
            )
            Images = @('reports-page.png', 'profile-section-page.png', 'employee-profile.png', 'recently-solved-ticket.png')
        }
        @{
            Title = '12) Retention and Record Handling'
            Paragraphs = @(
                'The current approved retention policy keeps ticket records indefinitely so the operational and audit history remains available. Email logs and local workstation logs have separate retention periods because they are higher-volume support artifacts.',
                'Retention should be reviewed periodically if database growth or policy changes require a more formal archival process.'
            )
            Bullets = @(
                'Tickets: indefinite.'
                'Ticket history and notes: indefinite.'
                'Email logs: 1 year.'
                'Local application logs on PCs: 90 days.'
            )
        }
        @{
            Title = '13) Incident Escalation Path'
            Paragraphs = @(
                'Operational incidents should follow a simple management path unless the issue is clearly technical and already diagnosed. Support should attempt first-line checks, then raise to Supervisor, then Manager when operational blocking or priority escalation is required.',
                'Developer involvement should happen when the problem is confirmed as application logic, deployment mismatch, or schema/code defect. DBA involvement should be added when the issue is database-specific.'
            )
            Bullets = @(
                'Primary incident path: IT Support -> Supervisor -> Manager.'
                'Add developer when the issue is a code, schema, or deployment problem.'
                'Add DBA when the issue is database-specific.'
            )
        }
        @{
            Title = '14) Evidence Collection Standard'
            Paragraphs = @(
                'Any issue escalated beyond first-line support should include enough evidence for another person to continue investigation without repeating the same questions.',
                'The minimum evidence set is: ticket ID, exact user action, time of issue, Diagnostics copy output, and relevant log references. Passwords and raw connection strings must never be included.'
            )
            Bullets = @(
                'Capture the Diagnostics snapshot using Copy.'
                'Record the affected TicketId or TicketCode.'
                'Include local log file path from %AppData%\\YakultInventoryApp\\Logs.'
                'If email is involved, include relevant CallEmailLog entries.'
            )
        }
        @{
            Title = '15) Quick Support Checklist'
            Bullets = @(
                'Can the user open the WinForms app and reach ITCM?'
                'Is the DB connection configured for the current Windows profile?'
                'Does Diagnostics show the correct server and database?'
                'Do schema checks pass?'
                'If email is enabled, do templates, sender settings, and recipients exist?'
                'Is the background job healthy and not blocked by a stuck lock?'
                'Has the required evidence been captured before escalation?'
            )
        }
    )
}
