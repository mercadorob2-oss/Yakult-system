# Reference: Borrow Items - Policies and Known Gaps

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / Development |
| Audience | IT technical + support leads + developers |
| System | Borrow Items Subsystem |
| Version | 1.0 |
| Effective date | 2026-03-11 |
| Last updated | 2026-03-11 |

## 1. Purpose

This document records the current operating assumptions, practical policies, and known gaps observed in the Borrow Items subsystem.

## 2. Current Operating Policies Inferred From Code

### 2.1 Borrow record model

- one row represents one borrow lifecycle
- a return updates the same row rather than creating a separate return row

### 2.2 Open-row exclusivity

- only one open row is allowed per item

### 2.3 Employee validity rule

- borrower and returner must be valid employees with departments

### 2.4 Role-based visibility

- subsystem is not globally visible; access is controlled by portal role mapping

## 3. Known Functional Gaps

### 3.1 No due date or expected return date

Impact:

- aging is visible only as elapsed time
- no formal overdue concept exists in code

### 3.2 No approval workflow

Impact:

- borrow logging is direct operational entry
- no request, approval, or release stage exists

### 3.3 No reason codes

Impact:

- documentation cannot claim the system captures borrow purpose or return reason as structured data

### 3.4 No attachment or evidence upload

Impact:

- physical-condition evidence must be handled outside the subsystem

### 3.5 No damaged/lost branching

Impact:

- non-standard outcomes must be handled procedurally, not through dedicated UI states

## 4. Operational Risks

### 4.1 Returner default may be misleading

The return panel preselects the original borrower when an open row is resolved.

Risk:

- operators may leave the default unchanged even when another employee actually returned the item

### 4.2 Schema-gated partial behavior

When the borrow schema is missing, the add-item flow may still create inventory items.

Risk:

- support may create inventory records without a matching borrow log if they misunderstand the warning

### 4.3 Production-oriented install script target

The install script explicitly uses `YIMS_PROD`.

Risk:

- careless deployment may target the wrong environment if operators do not adjust process controls

## 5. Recommended Documentation Controls

Until the code changes, operational documentation should clearly define:

- who is authorized to use Borrow Items in production
- when quick-add employee is allowed
- how to handle borrow transactions for employees without departments
- how to handle damaged, lost, or disputed items outside the system
- who is allowed to run the schema install script

## 6. Recommended Future Improvements

Reasonable future changes if the subsystem is formalized further:

1. add expected return date
2. add reason code for borrow and return
3. add optional remarks or handover notes
4. add dedicated handling for lost or damaged items
5. add a formal deployment script process for non-production and production targets
6. consider a read-only audit view for managers

## 7. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
