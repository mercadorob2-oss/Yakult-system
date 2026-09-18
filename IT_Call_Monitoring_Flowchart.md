# Yakult IT Call Monitoring and Ticketing Workflow

This document describes the end-to-end ticket lifecycle across the Systems Portal, legacy API, desktop IT Call Monitoring workspace, shared SQL Server database, notifications, and ITCM scheduler.

```mermaid
flowchart TD
    %% Ticket entry
    subgraph ENTRY["1. Ticket Entry"]
        Requester["Employee or anonymous requester"]
        PortalForm["Systems Portal\nHelp IT form"]
        Review["Review ticket details"]
        Confirm{"Confirm submission?"}
        DesktopCreate["Desktop IT Call Monitoring\nNew Ticket"]
        ApiCreate["Legacy API\ncall-tickets.ashx"]
        ApiAuth{"CallIT source?"}
        RequireIT["Validate JWT, active account\nand IT authorization"]
    end

    Requester --> PortalForm
    PortalForm --> Reviewc
    Review --> Confirm
    Confirm -->|Edit| PortalForm
    Confirm -->|Confirm| PortalCreate["HelpItController.Create()"]

    DesktopCreate --> DesktopStoredProc["dbo.sp_Call_CreateTicket"]
    ApiCreate --> ApiAuth
    ApiAuth -->|Yes| RequireIT
    ApiAuth -->|No / Portal| ApiStoredProc["dbo.sp_Call_CreateTicket"]
    RequireIT --> ApiValidated["Validated CallIT ticket"]
    ApiValidated --> ApiStoredProc
    PortalCreate --> PortalValidation["Validate issue, caller, organization\nand contact email"]
    PortalValidation -->|Invalid| PortalError["Show submission error"]
    PortalValidation -->|Valid| PortalStoredProc["dbo.sp_Call_CreateTicket"]

    %% Shared database
    subgraph DB["2. Shared SQL Server / YIMS Database"]
        Ticket["dbo.CallTicket"]
        TicketView["vw_Call_TicketList"]
        History["dbo.CallTicketHistory"]
        Notes["dbo.CallTicketNote"]
        Inventory["dbo.Item + dbo.Inventory"]
        EmailLog["dbo.CallEmailLog"]
        Rules["Notification and escalation settings"]
    end

    PortalStoredProc --> PortalDefaults["Portal ticket:\nPending / Medium / Unassigned\nTicketSource = Portal"]
    DesktopStoredProc --> DesktopDefaults["Desktop ticket:\nTicketSource = CallIT"]
    ApiStoredProc --> ApiDefaults["API ticket:\nPortal or CallIT source"]
    PortalDefaults --> Ticket
    DesktopStoredProc --> Ticket
    ApiDefaults --> Ticket
    Ticket --> TicketView
    Ticket --> History
    Ticket --> Notes
    Ticket --> Inventory
    Ticket --> Rules

    %% Desktop workspace
    subgraph DESKTOP["3. Desktop IT Call Monitoring Workspace"]
        Queue["Incoming Portal / Pending queue"]
        Details["Ticket details, history and notes"]
        Assign["Assign to self or reassign"]
        Priority["Change priority"]
        Update["Update status and add notes"]
        MarkAs["Mark As... resolution"]
        Reopen["Reopen final ticket"]
        Reports["Dashboard, reports and technician metrics"]
    end

    TicketView --> Queue
    Queue --> Details
    Details --> Assign
    Details --> Priority
    Details --> Update
    Details --> MarkAs
    Details --> Reopen
    TicketView --> Reports

    Assign --> AssignSP["dbo.sp_Call_AssignTicket"]
    Priority --> PrioritySP["dbo.sp_Call_SetTicketPriority"]
    Update --> StatusSP["dbo.sp_Call_SetTicketStatus"]
    Update --> NoteSP["dbo.sp_Call_AddTicketNote"]
    AssignSP --> Ticket
    PrioritySP --> Ticket
    StatusSP --> Ticket
    NoteSP --> Notes
    AssignSP --> History
    PrioritySP --> History
    StatusSP --> History

    %% Status lifecycle
    subgraph STATUS["4. Ticket Status Lifecycle"]
        Pending["Pending"]
        InProgress["In Progress"]
        Escalated["Escalated"]
        TempResolved["Resolved (Temporary)"]
        Solved["Solved"]
        Closed["Closed"]
        Reopened["Reopened"]
    end

    Pending -->|Assigned and work starts| InProgress
    InProgress -->|Manual or automatic escalation| Escalated
    InProgress -->|Service-only resolution| Solved
    InProgress -->|Temporary replacement| TempResolved
    Escalated -->|Resolution completed| Solved
    Escalated -->|Temporary replacement| TempResolved
    TempResolved -->|Temporary item finalized| Solved
    Solved --> Closed
    Solved -->|Reopen with explanation| Reopened
    TempResolved -->|Reopen with explanation| Reopened
    Closed -->|Reopen with explanation| Reopened
    Reopened --> InProgress

    Pending --> StatusSP
    InProgress --> StatusSP
    Escalated --> StatusSP
    TempResolved --> StatusSP
    Solved --> StatusSP
    Closed --> StatusSP
    Reopened --> StatusSP

    %% Resolution
    subgraph RESOLUTION["5. Resolution Processing"]
        ResolutionType{"Resolution type?"}
        Service["Service Only"]
        Replacement["Replacement"]
        ValidateReplacement["Validate old item, new item,\nquantity and condition"]
        Stock{"Enough replacement stock?"}
        StockFailure["Reject resolution"]
        InventoryOut["Inventory OUT:\nnew replacement item"]
        OldItem["Update old item condition\nand repair state"]
        InventoryIn["Inventory IN old item\nwhen repaired or spare"]
        ResolutionHistory["Write resolution history\nand solution note"]
        TemporaryDecision{"Temporary replacement?"}
    end

    MarkAs --> ResolutionType
    ResolutionType -->|Service Only| Service
    ResolutionType -->|Replacement| Replacement
    Service --> ResolutionHistory
    Replacement --> ValidateReplacement
    ValidateReplacement --> Stock
    Stock -->|No| StockFailure
    Stock -->|Yes| InventoryOut
    InventoryOut --> OldItem
    OldItem --> InventoryIn
    InventoryIn --> ResolutionHistory
    ResolutionHistory --> TemporaryDecision
    TemporaryDecision -->|Yes| TempResolved
    TemporaryDecision -->|No| Solved
    ResolutionHistory --> History

    Reopen --> ReopenCheck{"Final status?"}
    ReopenCheck -->|Yes| ReopenNote["Require reopen explanation"]
    ReopenCheck -->|No| ReopenBlocked["Reopen unavailable"]
    ReopenNote --> Reopened

    %% Notifications
    subgraph EMAIL["6. Notifications"]
        NewEvent["New ticket"]
        AssignmentEvent["Assignment"]
        StatusEvent["Status / priority / note change"]
        EscalationEvent["Escalation"]
        ReminderEvent["Reminder"]
        NotificationRules["Read notification rules"]
        Template["Read active email template"]
        Recipients["Resolve recipients:\nassigned technician, contact email,\nbranch, department or escalation group"]
        SMTP["Resolve SMTP profile"]
        Send["Send email"]
        Log["Write sent, skipped or failed result"]
    end

    PortalStoredProc --> NewEvent
    DesktopStoredProc --> NewEvent
    ApiStoredProc --> NewEvent
    AssignSP --> AssignmentEvent
    StatusSP --> StatusEvent
    Escalated --> EscalationEvent

    NewEvent --> NotificationRules
    AssignmentEvent --> NotificationRules
    StatusEvent --> NotificationRules
    EscalationEvent --> NotificationRules
    ReminderEvent --> NotificationRules
    NotificationRules --> Template
    Template --> Recipients
    Recipients --> SMTP
    SMTP --> Send
    Send --> Log
    Log --> EmailLog
    Rules --> NotificationRules

    %% ITCM scheduler
    subgraph SCHEDULER["7. ITCM Scheduler and Automation"]
        Hosted["ItcmSchedulerHostedService"]
        Job["ItcmBackgroundJob"]
        Lock["Acquire SQL application lock"]
        LockResult{"Lock acquired?"}
        ReminderQuery["Find active tickets past reminder threshold"]
        ReminderSend["Send reminder email"]
        MarkReminder["Update LastReminderSentAt"]
        EscalationQuery["Find inactive tickets past escalation threshold"]
        PortalCheck{"Unassigned Portal ticket?"}
        SkipPortal["Keep in Pending triage queue"]
        ApplyEscalation["Set Escalated status"]
        AutoAssign["Optional automatic IT assignment"]
        Heartbeat["Write scheduler heartbeat"]
    end

    Hosted -->|Interval elapsed and enabled| Job
    Job --> Lock
    Lock --> LockResult
    LockResult -->|No| Heartbeat
    LockResult -->|Yes| ReminderQuery
    ReminderQuery --> ReminderSend
    ReminderSend --> MarkReminder
    MarkReminder --> ReminderEvent
    MarkReminder --> EscalationQuery
    EscalationQuery --> PortalCheck
    PortalCheck -->|Yes| SkipPortal
    PortalCheck -->|No / assigned / non-Portal| ApplyEscalation
    ApplyEscalation --> AutoAssign
    AutoAssign --> EscalationEvent
    ApplyEscalation --> Ticket
    AutoAssign --> Ticket
    Job --> Heartbeat
    Heartbeat --> Ticket

    %% Requester tracking
    subgraph TRACKER["8. Requester Tracking"]
        Tracker["Systems Portal ticket tracker"]
        Search["Search by ticket code, issue, caller\nor organization"]
        Detail["Portal ticket details"]
        Timeline["Timeline from CallTicketHistory\nand CallTicketNote"]
        StatusDisplay["Display current status, priority,\nassignment and solved date"]
    end

    TicketView --> Tracker
    Tracker --> Search
    Search --> Detail
    Ticket --> Detail
    History --> Timeline
    Notes --> Timeline
    Detail --> Timeline
    Timeline --> StatusDisplay
    StatusDisplay --> Requester

    %% Monitoring
    Diagnostics["Desktop diagnostics"] --> ITCMApi["ITCM health, scheduler status,\nheartbeat and monitoring APIs"]
    ITCMApi --> Hosted
    ITCMApi --> Heartbeat
```

## Primary status path

`Pending → In Progress → Escalated → Resolved (Temporary) → Solved → Closed`

A final ticket may be reopened:

`Solved / Resolved (Temporary) / Closed → Reopened → In Progress`

## Main implementation locations

- Portal entry and tracking: `Yakult.SystemsPortal/Controllers/HelpItController.cs`, `Repositories/HelpItRepository.cs`
- Legacy API entry and actions: `Yakult.Inventory.Api2_remote/call-tickets.ashx`, `call-ticket-action.ashx`
- Desktop ticket workspace: `Yakult.Inventory.App/Forms/CallMonitoring/TicketManagementControl.cs`, `Wpf/CallMonitoring/WpfTicketManagementWorkspace.cs`
- Desktop status rules: `Yakult.Inventory.App/Forms/CallMonitoring/TicketWorkflow.cs`
- Desktop database operations: `Yakult.Inventory.App/Repositories/CallMonitoringRepository.cs`
- Desktop notifications and background jobs: `Yakult.Inventory.App/Services/CallEmailNotificationService.cs`
- ITCM scheduler: `Yakult.ITCM.Server/Services/ItcmSchedulerHostedService.cs`, `ItcmBackgroundJob.cs`
- ITCM database and locking: `Yakult.ITCM.Server/Data/ItcmRepository.cs`
- ITCM monitoring endpoints: `Yakult.ITCM.Server/Program.cs`
