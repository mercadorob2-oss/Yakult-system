# Reference: ITCM Status and Priority

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | IT support + IT technical |
| System | IT Call Monitoring (ITCM) |
| Version | 1.0 |
| Effective date | 2026-03-10 |
| Last updated | 2026-03-14 |

## 1. Purpose

Provide a single, authoritative list of ticket statuses and priorities used by ITCM, including validation rules that may be enforced by database stored procedures.

## 2. Priorities (canonical)

The database create-ticket procedure canonicalizes priorities to:

- `Low`
- `Medium`
- `High`
- `Critical`

Source:

- `DATABASES/Yakult-DB-Production/dbo/Stored Procedures/sp_Call_CreateTicket.sql`

Operational note:

- If users attempt to set other values, the procedure may reject the change (depending on the operation path).

## 3. Status values

### 3.1 Canonical statuses enforced by stored procedure

The status change procedure validates the status list to:

- `Pending`
- `In Progress`
- `Escalated`
- `Resolved (Temporary)`
- `Solved`
- `Closed`
- `Reopened`

Source:

- `DATABASES/Yakult-DB-Production/dbo/Stored Procedures/sp_Call_SetTicketStatus.sql`

### 3.2 Official production policy

Current approved status policy:

- Final statuses:
  - `Solved`
  - `Closed`
- Valid working but non-final status:
  - `Resolved (Temporary)`

Source alignment:

- The stored procedure list in the current DB installer scripts supports `Closed`.
- The WinForms workflow helper matches the same active status list.

## 4. Status transitions (support-level rules)

The WinForms workflow includes "no backward movement" logic for known statuses:

- Status should generally progress forward (e.g., Pending -> In Progress -> Escalated/Resolved/Solved)
- Reopened is intended to be used only from a final state

Source:

- `Yakult.Inventory.App/Forms/CallMonitoring/TicketWorkflow.cs`

Support guidance:

- If you see users "stuck" because of transition rules, verify whether their target status is allowed by both UI and DB.

## 5. Where to verify actual values in your environment

Because environments may be customized over time, confirm what is present in your database:

```sql
SELECT Status, COUNT(*) AS Cnt
FROM dbo.CallTicket
GROUP BY Status
ORDER BY Cnt DESC;
```

```sql
SELECT Priority, COUNT(*) AS Cnt
FROM dbo.CallTicket
GROUP BY Priority
ORDER BY Cnt DESC;
```

## 6. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-10 | Initial release |
| 1.1 | 2026-03-11 | Added approved final-status policy and full supported status set |
| 1.2 | 2026-03-14 | Removed Waiting on Department and Waiting on Vendor from the supported status list |
