# Verify mobile Mark As resolutions

Deploy `Yakult.Inventory.Api2_remote/call-ticket-action.ashx` and
`call-items-lookup.ashx` before installing the updated mobile build. No database
migration is required. The action handler continues accepting legacy single-item
requests; the updated app sends a `lines` array for item resolutions.

Run automated checks from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-ItcmResolutionApi.ps1
Push-Location Latest_sys/YakultScanner
.\gradlew.bat :app:testDevDebugUnitTest :app:assembleProdDebug --offline
Pop-Location
```

Use test tickets and test inventory for these integration checks:

1. Mark Service Only as permanent, then use a separate ticket for temporary
   service. Confirm Solved and Resolved (Temporary), with no inventory movement.
2. Collect an old item without issuing stock, including when no stock exists.
   Confirm its condition, stock action, notes and item audit history.
3. Issue a new item without collecting an old item. Confirm only issued stock is
   deducted and no old-item condition is required.
4. Collect two old items and issue one new item. Use different quantities and
   notes, edit an entry, remove an entry, and verify the final review matches.
5. Select the same replacement stock item in multiple entries. Confirm the
   combined quantity is checked. Change available stock after loading mobile
   lookups and confirm the API rejects an over-allocation without saving any
   entry, note, history, inventory movement or status change.
6. Try duplicate collected IDs, an old ID reused as new, duplicate unlisted
   serials, empty quantities and missing notes. Confirm review is blocked.
7. Save a temporary batch with paired, collected-only and issued-only entries.
   In desktop, return the temporary items and confirm all issued items return
   exactly once and collected-only items are not returned again.
8. Search by serial, name, model and fixed asset number. Confirm matches and
   available stock agree with desktop. Rotate the device while editing and
   confirm the draft, selected items and completed entries survive.
9. Confirm a status-change email log entry is created for a mobile resolution.

Repair forwarding is hidden in this Mark As flow to match the desktop gate.
Existing API repair-forwarding support remains available to older clients.

Automated tests compile both handlers and exercise batch validation and mobile
request serialization. They do not connect to the database or verify IIS,
inventory transactions, SMTP delivery or device rendering.
