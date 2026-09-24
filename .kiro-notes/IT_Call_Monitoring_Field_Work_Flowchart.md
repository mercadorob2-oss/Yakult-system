# IT Call Monitoring + Field Work — Flowchart Pack (Repair Portal excluded)

Scoped to only the two systems requested: **IT Call Monitoring** (ticket lifecycle) and its
**Field Work** component (`CallFieldVisit` — on-site technician visits scheduled against an IT
Call ticket, with photo evidence and customer signature). Repair Portal is a separate module and
is intentionally NOT included here.

Structured the same way as the reference image: an overview panel (shared entities + relationship
between ticket and field visit), then two detailed lifecycle panels, each Start → End with
decision diamonds and an "Event Details"/"Note" callout. Paste any Mermaid block into ChatGPT web
or mermaid.live to render/redraw.

---

## Panel 1 — Overall Relationship (IT Call Ticket ↔ Field Visit)

```mermaid
flowchart TB
    EMP[("Employee / Caller Master<br/>(who reports, who is assigned as technician)")]
    TICKET[("IT Call Ticket<br/>(the support case)")]

    subgraph P1["IT Call Monitoring Path (Report → Resolution)"]
        direction TB
        A1["Ticket Created<br/>(caller, issue, priority)"]
    end

    subgraph P2["Field Work Path (Schedule → Sign-off)"]
        direction TB
        B1["Field Visit Scheduled<br/>(0 or 1 per ticket, technician + date)"]
    end

    EMP -.-> A1
    EMP -.-> B1
    TICKET -.-> B1

    A1 -->|"Optional: technician schedules<br/>an on-site visit for this ticket"| B1
    B1 -->|"Visit notes / signature<br/>inform ticket resolution"| A1

    classDef master fill:#e6dcf5,stroke:#7a52b3,stroke-width:2px;
    classDef path fill:#eef4fb,stroke:#3f6fb0,stroke-width:1px;
    class EMP,TICKET master;
```

**Key relationship notes:**
- A Field Visit is **optional and 1:1 with a ticket** — a ticket does not require a field visit,
  and only one active visit can exist per ticket at a time (`POST` returns 409 if one already
  exists, including a Cancelled one — that case must be rescheduled via `PUT`, not re-created).
- The Field Visit is a **sub-workflow nested inside** the ticket's own lifecycle, not a separate
  bridged ticket type (unlike Repair Portal) — it doesn't change the ticket's own Status field by
  itself; it's supporting evidence/scheduling data attached to the ticket.
- Completing a visit is **hard-gated by a customer signature** — the API rejects `Completed`
  with no signature bytes decoded, whether that's from a fresh submission or a previously empty
  one.

---

## Panel 2 — IT Call Monitoring Detailed Lifecycle

```mermaid
flowchart TB
    S(["Start"]) --> C1["Ticket Created<br/>(caller, item, issue, priority)"]
    C1 --> C2["Status: Pending"]
    C2 --> D1{"Action Taken?"}

    D1 -->|"Technician picks up"| C3["Status: In Progress"]
    D1 -->|"Needs higher-level help<br/>(note required)"| C4["Status: Escalated"]
    D1 -->|"Needs on-site work"| FV["Schedule Field Visit<br/>(see Panel 3)"]

    FV -.->|"Visit completed,<br/>ticket work continues"| C3

    C3 --> D2{"Resolution Type?"}
    C4 --> D2
    D2 -->|"Fixed now"| C5["Status: Solved"]
    D2 -->|"Temporary workaround<br/>(e.g. loaner item issued)"| C6["Status: Resolved (Temporary)"]
    D2 -->|"Still needs on-site work"| FV

    C6 --> D3{"Temp fix resolved /<br/>permanent fix applied?"}
    D3 -->|"Yes"| C5
    D3 -->|"Not yet"| C6

    C5 --> C7["Status: Closed"]

    C7 --> D4{"Issue recurs?"}
    D4 -->|"No"| E(["End"])
    D4 -->|"Yes — Reopen"| C8["Status: Reopened<br/>(note required)"]
    C8 --> D1

    NOTE["Note:<br/>• Status can never move backward except via explicit Reopen from a final state.<br/>• Escalated and Reopened both require a mandatory note before the transition is allowed.<br/>• 'Solved'/'Resolved (Temporary)' must go through Mark As, not the raw status dropdown.<br/>• Scheduling a Field Visit does NOT itself change the ticket Status — it runs alongside it."]

    classDef startend fill:#2e8b57,stroke:#1c5c39,color:#fff;
    classDef decision fill:#fdf1d6,stroke:#c99a2e;
    classDef status fill:#eaf3fb,stroke:#3f6fb0;
    classDef bridge fill:#dcecff,stroke:#3f6fb0,stroke-dasharray: 4 3;
    class S,E startend;
    class D1,D2,D3,D4 decision;
    class C1,C2,C3,C4,C5,C6,C7,C8 status;
    class FV bridge;
```

---

## Panel 3 — Field Work (Field Visit) Detailed Lifecycle

**Verified directly against the live DB constraint and canonical stored procedure**
(`Migration_ITCM_FieldVisit_CanonicalStateMachine.sql`):
`CK_CallFieldVisit_Status CHECK (Status IN ('Scheduled','Completed','Cancelled'))`.
**There is no time-in/time-out, no "On Site," no "In Progress" state for Field Work.**
Those intermediate states (`InTransit`, `CheckedIn`, `CheckedOut`) plus their
`CheckedInAt`/`CheckedOutAt` timestamp columns **did exist in an earlier design and were
deliberately removed** on 2026-09-01 per `Migration_ITCM_FieldWork_SimplifyStateMachine.sql`
("Remove InTransit, CheckedIn, CheckedOut intermediate states... Remove time tracking fields").
Photo/signature capture happens while still `Scheduled` — completing the visit is a single
direct transition, not a separate step after some on-site/checked-in state.

```mermaid
flowchart TB
    S(["Start"]) --> F1["Schedule Field Visit<br/>(ticketId, technicianEmpId, scheduledAt, notes)"]
    F1 --> D1{"A visit already<br/>exists for this ticket?"}
    D1 -->|"Yes, and it is Cancelled"| RESCHED["Reschedule<br/>(PUT status=Scheduled,<br/>updates technician/date)"]
    D1 -->|"Yes, and it is Scheduled/Completed"| ERR["409 Conflict<br/>one active visit per ticket"]
    D1 -->|"No"| F2["Status: Scheduled"]
    RESCHED --> F2

    F2 --> D2{"Upload photo evidence?<br/>(optional, any time while Scheduled)"}
    D2 -->|"Yes"| F3["Photo stored<br/>(0–20 photos, PNG/JPEG, 20MB each,<br/>MIME sniffed from file bytes, not trusted)"]
    F3 --> D2
    D2 -->|"No / done uploading"| D3{"Technician Action?"}

    D3 -->|"Visit called off"| F4["Status: Cancelled<br/>(terminal unless rescheduled)"]
    F4 -.->|"Reschedule"| RESCHED

    D3 -->|"Mark as done"| D4{"Customer signature<br/>provided?"}
    D4 -->|"No"| BLOCK["400 Blocked:<br/>'Customer signature is required<br/>to complete a visit'"]
    BLOCK --> D3
    D4 -->|"Yes, valid base64 image"| F5["Signature + Status='Completed'<br/>saved atomically (DB transaction)<br/>CompletedAt = now(). Terminal — cannot be changed."]

    F5 --> F6["CallTicketHistory rows logged<br/>('FieldVisitStatus'=Completed,<br/>'FieldVisitSignature'=Saved)"]
    F6 --> E(["End<br/>(visit closed; ticket workflow continues in Panel 2)"])

    EVDET["Event Details:<br/>• Only 3 possible statuses, enforced by a DB CHECK constraint: Scheduled, Completed, Cancelled. No 'On Site'/'In Progress'/time-in/time-out — those were removed 2026-09-01.<br/>• Completed is permanently terminal — the stored proc throws if you try to change a Completed visit.<br/>• Cancelled is the only status that can transition again, and only back to Scheduled (reschedule).<br/>• Max 20 photos per visit; each capped at 20MB; blocked once visit is Completed.<br/>• Status-change Notes APPEND to existing visit notes rather than overwrite them.<br/>• Auth: any IT-authorized technician can act on a visit; a non-IT user is only allowed if they created or are assigned the parent ticket."]

    classDef startend fill:#2e8b57,stroke:#1c5c39,color:#fff;
    classDef decision fill:#fdf1d6,stroke:#c99a2e;
    classDef status fill:#eaf3fb,stroke:#3f6fb0;
    classDef warn fill:#f7d9d9,stroke:#b03a3a;
    classDef terminal fill:#d9f2e3,stroke:#2e8b57;
    class S,E startend;
    class D1,D2,D3,D4 decision;
    class F1,F2,F3,F6,RESCHED status;
    class ERR,BLOCK warn;
    class F4 warn;
    class F5 terminal;
```

---

## Source references used to build this (IT Call + Field Work only)

- `Forms\CallMonitoring\TicketWorkflow.cs` — canonical IT Call status catalog, rank order, and
  allowed-transition rules (no backward movement; Reopened/Escalated gating).
- `Forms\CallMonitoring\TicketManagementControl.StatusWorkflow.cs` — UI-level enforcement
  (note-required prompts, Mark As redirection for Solved/Resolved (Temporary), Reopen flow).
- `Yakult.Inventory.Api2_remote\call-field-visits.ashx` — Field Visit state machine
  (`Scheduled → Completed / Cancelled`, `Cancelled → Scheduled` reschedule), signature-gated
  completion, calls `dbo.sp_Call_FieldVisit_Schedule` / `dbo.sp_Call_FieldVisit_SetStatus`.
- `Yakult.Inventory.Api2_remote\call-field-visit-photo-upload.ashx` — evidence photo upload for a
  visit: 20MB/file, 20 files/visit cap, magic-number MIME sniffing (PNG/JPEG only), blocked once
  visit is Completed, 400px/85% JPEG thumbnail generation.
- `Yakult.Inventory.Api2_remote\call-ticket-action.ashx` — mobile mirror of the desktop ticket
  actions (`status`, `priority`, `note`, `assign`, `resolution`) confirming the same ticket
  lifecycle is shared by desktop and mobile/field technicians.
- `AGENTS.md` note on `vw_Call_TicketList` / mobile dry run — confirms mobile field flow (create →
  field visit → photo upload → status changes) writes into these same `CallTicket` /
  `CallFieldVisit` tables via this API, not a separate system.
