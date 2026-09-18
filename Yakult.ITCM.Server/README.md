# Yakult ITCM Server

Standalone server-side scheduler/API for IT Call Monitoring.

This project is intentionally separate from `Yakult.SystemsPortal`.

## Current endpoints

- `GET /api/itcm/health`
- `GET /api/itcm/scheduler/status`
- `POST /api/itcm/scheduler/run-now`
- `POST /api/itcm/scheduler/pause`
- `POST /api/itcm/scheduler/resume`

## Next implementation step

Move the real reminder, auto-escalation, email, and heartbeat logic into
`Services/ItcmBackgroundJob.cs`.
