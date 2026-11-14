# Mobile IT Call Monitoring: Pull Out

## Deployment order

1. Deploy the two files in `api` to the existing `Yakult.Inventory.Api2_remote` IIS application root, replacing the matching files. No Web.config change or database migration is required for this feature on the existing IT Call schema.
2. Install `YakultScanner-pullout-prod-debug.apk` for the Production API target (port 7015), or `YakultScanner-pullout-dev-debug.apk` for Development (port 7014). These are debug APK builds, using the project's existing signing configuration and saved connection preferences.

The existing `Copy-Api2-Local-To-Production.ps1` also picks up the changed source handlers. That script copies the full API source tree, including Web.config; this bundle contains only this feature's changed API handlers. Nothing was deployed automatically.

## Behavior

- Mark as Resolved now offers Pull Out alongside Service Only and Replacement.
- Pull Out requires an old item (listed or unlisted), condition, action, positive quantity, and notes. It cannot issue a replacement item and always leaves the ticket Resolved (Temporary).
- Unrepaired units remain out of available stock. Repaired/spare units follow the desktop stock-return rules.
- A temporary Pull Out offers Complete Follow-up. Confirmation marks the ticket Solved, records a note, and requests the status email notification, without another inventory movement. Reopen remains available in More.
- PDF exports show TEMPORARY PULL OUT while awaiting follow-up and PULL OUT after completion.

## Verification

- Android JVM tests and Development/Production debug APK builds.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Test-ItcmResolutionApi.ps1`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Test-ItcmPullOutWorkflow.ps1`

The SQL workflow test uses a disposable LocalDB database and test-only authentication/email stubs. It does not connect to production or send email. Device UI and deployed IIS/email delivery still need a deployment smoke check.
