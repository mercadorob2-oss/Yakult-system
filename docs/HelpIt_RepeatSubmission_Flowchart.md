# Help IT — Repeat Submission Confirmation Flowchart

Flow of the "You've already submitted a ticket" confirmation added to the Systems Portal Help-IT ticket submission (`HelpItController.cs` / `Views/HelpIt/Index.cshtml`).

```mermaid
flowchart TD
    A([User opens /HelpIt - Index GET]) --> B{Session<br/>HelpIt:TicketCount > 0?}
    B -- No --> C[View renders WITHOUT<br/>repeat warning]
    B -- Yes --> D[View renders warning block:<br/>'You've already submitted a ticket'<br/>+ checkbox + link to TCK-XXXXXX]

    C --> E[User fills form]
    D --> E

    E --> F[Click 'Review ticket']
    F --> G[Review dialog opens<br/>checkbox reset to unchecked]
    G --> H[Click 'Confirm & submit ticket']

    H --> I{Has prior submission?<br/>count > 0}
    I -- No --> J[POST ConfirmedSubmission=true<br/>ConfirmedRepeatSubmission=false]
    I -- Yes --> K{Checkbox<br/>'I understand - this is a separate issue'<br/>checked?}
    K -- No --> L[Show error:<br/>'Confirm that this is a separate issue...'<br/>no request sent]
    L --> G
    K -- Yes --> M[POST ConfirmedSubmission=true<br/>ConfirmedRepeatSubmission=true]

    J --> N[Controller gate 1:<br/>ConfirmedSubmission true?]
    M --> N
    N -- No --> O[Reject: review dialog required]
    O --> G

    N -- Yes --> P{Controller gate 2:<br/>has prior submission AND<br/>ConfirmedRepeatSubmission false?}
    P -- Yes --> Q[Reject:<br/>'You've already submitted a ticket...<br/>confirm it is a separate issue']
    Q --> G

    P -- No --> R[Validate issue / org / contact email]
    R -- Invalid --> S[Show validation error]
    S --> E
    R -- Valid --> T[CreateTicketAsync<br/>sp_Call_CreateTicket]

    T --> U[Session flags written:<br/>TicketCount +1<br/>LastTicketCode = TCK-XXXXXX]
    U --> V([Success panel shows ticket code])
```

## Legend

| Shape | Meaning |
|---|---|
| `[ ]` | Server-side step (controller / repository / session) |
| `( )` | Entry / exit point |
| `{ }` | Decision gate |
| `Diamond` | Client-side (browser) decision |

## Status transitions

| State | Transition |
|---|---|
| No prior ticket | Session `HelpIt:TicketCount` absent or 0 → flow identical to original single-confirm behavior |
| 1st submission succeeds | Count incremented to 1, `HelpIt:LastTicketCode` set → warning armed |
| 2nd+ submission (same session) | Warning shown; checkbox required client-side; `ConfirmedRepeatSubmission=true` required server-side |
| Session expires (8h) / new browser | Count resets → warning clears |
