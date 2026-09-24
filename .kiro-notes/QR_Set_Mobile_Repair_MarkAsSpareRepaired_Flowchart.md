# QR (Set/Repair → Mobile → Repair Page) + Mark as Spare/Repaired — Simple Version

## Panel 1 — How each QR code is scanned and where it leads

```mermaid
flowchart TB
    SETQR[("Set QR<br/>yakult:set:v1:...")]
    REPAIRQR[("Repair Ticket QR<br/>yakult:repair:v1:...")]

    SETQR --> SCAN["Scan with mobile camera"]
    REPAIRQR --> SCAN

    SCAN --> D1{"Is it a Set QR?"}
    D1 -->|Yes| SETPAGE["Opens Dispatch/Set details page"]
    D1 -->|No, it's a Repair QR| BUG["🛑 Not recognized by the scanner<br/>('Unsupported QR code')"]

    BUG --> MANUAL["Workaround: technician types<br/>the Repair Number by hand"]
    MANUAL --> REPAIRPAGE["Opens Repair Ticket page"]

    classDef bug fill:#f7d9d9,stroke:#b03a3a,stroke-width:2px;
    class BUG bug;
```

**Key point:** The mobile scanner only understands Set QR codes right now. Scanning a Repair
Ticket's QR code fails with "Unsupported QR code" — technicians have to type the Repair Number
manually instead. This is a real gap in the current app, not expected behavior.

---

## Panel 2 — Once on the Repair Ticket page, what can the technician do?

```mermaid
flowchart TB
    S(["Repair Ticket page opened"]) --> D1{"Action?"}
    D1 -->|Upload photos/videos| A1["Attach evidence to a Part"]
    D1 -->|Loan a spare| A2["Assign a temporary spare item"]
    D1 -->|Update a part| A3["Change part repair status"]
    A1 --> E(["Done"])
    A2 --> E
    A3 --> E
```

---

## Panel 3 — Mark as Spare / Repaired / Unrepaired

```mermaid
flowchart TB
    S(["Start"]) --> D1{"Pick an action"}
    D1 -->|Repaired| A["Item fixed, condition restored"]
    D1 -->|Unrepaired| B["Item still broken"]
    D1 -->|Repaired - Spare inventory| C["Item fixed but kept as spare stock,<br/>not returned to the requester"]

    A --> CONFIRM["Enter a reason, confirm"]
    B --> CONFIRM
    C --> CONFIRM

    CONFIRM --> SAVE["Saved to repair history<br/>(same 3 choices whether done on<br/>Desktop or Mobile)"]
    SAVE --> E(["End"])
```

**Where this happens:**
- Desktop: Repair Items page → Mark Repaired / Mark Unrepaired / Mark Spare buttons.
- Mobile: resolving an IT Call ticket with a replacement item — same 3 choices for the old item.
