$Manual = [ordered]@{
    Title = 'IT Call Monitoring (ITCM) - User Guide'
    Subtitle = 'For Encoders (End Users)'
    Version = 'v1.0'
    Date = '2026-03-11'
    Notes = @(
        'This guide is short and task-based. It explains what each screen/action is for and the correct workflow.'
    )
    Sections = @(
        @{
            Title = '1) Quick Workflow'
            Paragraphs = @(
                'Create ticket -> Assign -> Work/Update -> Mark as Solved (or Resolved (Temporary)).'
            )
            Bullets = @(
                'Always log work using Notes (Solutions tab) so the ticket has a clear history.'
                'You cannot mark a ticket as Solved from the Status dropdown; use "Mark As..." instead.'
            )
        }
        @{
            Title = '2) Dashboard (What it is for)'
            Paragraphs = @(
                'Shows workload summary and "requires attention" items.'
            )
            Images = @('dashboard.png')
        }
        @{
            Title = '3) Create a New Ticket'
            Paragraphs = @(
                'Fill out the New Ticket form, then click Create Ticket and confirm the details.'
            )
            Bullets = @(
                'Company: required.'
                'Department: required when available (some databases show N/A when not applicable).'
                'Branch: required when branch options exist for the selected company.'
                'Caller Name: must be an Employee from the dropdown list (you can type-to-search, but it must match a listed employee).'
                'Technical Problem: required.'
                'Initial Troubleshooting / Notes: optional but recommended.'
                'Escalation: optional (use Set escalation only when needed).'
            )
            Callouts = @(
                @{
                    Kind = 'tip'
                    Text = 'If the caller is not in the Caller Name list, click the small "+" beside Caller Name to Quick Add an Employee, then select them.'
                }
            )
            Images = @('new-ticket.png', 'ticket-set-escalation.png')
        }
        @{
            Title = '4) Ticket List, Search, and Filters'
            Paragraphs = @(
                'Use Search + filters to find the correct ticket quickly (by Ticket Code/Id, caller, issue text, assignee, etc.).'
            )
            Bullets = @(
                'Use Unassigned to find tickets with no owner.'
                'Use status filters to focus your queue (Pending / In Progress / Escalated / Overdue).'
            )
            Images = @('ticket-list-page.png', 'pending-tickets.png')
        }
        @{
            Title = '5) Assign / Reassign Tickets'
            Paragraphs = @(
                'Tickets should have an owner so they can be tracked and followed up.'
            )
            Bullets = @(
                'Assign to me: claims the selected ticket(s) to your linked employee profile.'
                'Reassign: choose a name in Assigned To, then click Reassign.'
            )
            Images = @('pending-case-details.png')
        }
        @{
            Title = '6) Work on a Ticket (Notes / Status / Priority)'
            Paragraphs = @(
                'Use the Solutions tab to add updates (what you did, next steps, follow-up schedule).'
            )
            Bullets = @(
                'Update button can change Status and/or Priority and/or add a Note.'
                'Escalated requires a note explaining why it is escalated and what is needed.'
                'Reopen requires a note and must be done using the Reopen button (not the status dropdown).'
            )
        }
        @{
            Title = '7) Resolve a Ticket (Mark As...)'
            Paragraphs = @(
                'Use Mark As... to finalize the ticket correctly. This is required for proper reporting and (when needed) inventory movement.'
            )
            Bullets = @(
                'Service Only (No Parts): use when the issue is fixed without replacing/using inventory parts.'
                'Replacement (Parts Used): use when you replace a unit / use inventory parts.'
                'Temporary replacement: check Temporary replacement to mark the ticket as Resolved (Temporary).'
                'Return Temp Item: appears only for Resolved (Temporary) tickets; use it when the temporary item is returned to inventory.'
            )
            Images = @('mark-as-resolutions-service.png', 'mark-as-resolutions-replacements.png')
        }
        @{
            Title = '8) Ticket Details (Double-click)'
            Paragraphs = @(
                'Double-click a ticket to open Ticket Details. This shows full Notes and History (audit trail).'
            )
            Bullets = @(
                'Notes: troubleshooting steps and updates.'
                'History: status changes, assignment changes, escalation changes, and resolution actions.'
            )
            Images = @('ticket-details.png')
        }
        @{
            Title = '9) Profiles (What it is for)'
            Paragraphs = @(
                'Profiles show technician workload and performance statistics for a selected time range.'
            )
            Images = @('profile-section-page.png', 'employee-profile.png', 'auto-escalation-assignee.png')
        }
        @{
            Title = '10) Reports (What it is for)'
            Paragraphs = @(
                'Reports allow filtering and exporting for tracking and updates.'
            )
            Bullets = @(
                'Export PDF: creates a PDF report of the current filtered rows.'
                'Export CSV: creates a CSV for Excel analysis.'
            )
            Images = @('reports-page.png')
        }
        @{
            Title = '11) Email Notifications & Diagnostics (Brief)'
            Paragraphs = @(
                'Email Notifications manages SMTP settings, templates, recipients, and email logs.',
                'Diagnostics helps check schema/health and review logs when something is not working.'
            )
            Images = @('email-setup.png', 'email-template.png', 'email-logs.png', 'diagnostics-logs.png')
        }
        @{
            Title = '12) Troubleshooting'
            Bullets = @(
                'Ticket not appearing: click Filter/Refresh and confirm your search/status filters.'
                'Cannot select caller: caller must be a listed Employee; use "+" Quick Add Employee if needed.'
                'Cannot mark Solved from Status: use Mark As... (Service Only or Replacement).'
                'Cannot set Escalated: add a short note first in the Solutions tab.'
            )
        }
    )
}
