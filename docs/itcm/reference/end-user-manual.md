# Reference: ITCM - End-User Manual (Existing Deliverable)

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | ITCM end users (encoders / operators) |
| System | IT Call Monitoring (ITCM) |
| Version | 1.0 |
| Effective date | 2026-03-10 |
| Last updated | 2026-03-10 |

## 1. Purpose

This document points to the existing end-user manual deliverables already generated from this repository.

## 2. Scope

End-user operation only (not administration, deployment, or technical troubleshooting).

## 3. Deliverables

- Word (HTML-in-`.doc`): `docs/itcm-manual/out/ITCM_User_Manual_Encoders_*.doc`
- PDF: `docs/itcm-manual/out/ITCM_User_Manual_Encoders_*.pdf`

## 4. Regeneration procedure

From the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File docs/itcm-manual/generate.ps1
```

For IT-support-focused documentation, see:

- `docs/itcm/how-to/admin-runbook.md`
- `docs/itcm/how-to/troubleshooting.md`

## 5. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-10 | Initial release |

