# QR Branding Design — Logo + PC Model Caption (2026-09-24)

## Goal
Brand the Set QR image stored in `dbo.Set.QRImageData`: Yakult bottle logo in the QR center (per `Yakult_QR_Dummy_Design.png` reference), plus the PC model number as text below the QR when the set contains a PC.

## Decisions (user-confirmed, revised 2026-09-24)
- PC rule: superseded. Caption source is the saved set computer name (`dbo.Set.ComputerName`, edited via the Set Details header `TxtComputerName` textbox).
- Caption: computer name only, centered below QR (e.g. `ALIEN-499`); empty computer name = no caption, logo-only QR.
- Logo: black Yakult bottle silhouette with white `Yakult` label, monochrome, centered with white border/halo (matches dummy reference). New square asset, not the wide `yakult_Name.png` wordmark.
- Scope: stored blob — new `BtnGenerateQR_Click` saves compose the branded PNG; viewer, 2x2 print, and dispatch PDF reuse it unchanged. Old sets keep old QR until re-generated. No `QRData` JSON change, no migration.

## Architecture
- Single change point: `Helpers/SetQRGenerator.cs::GenerateQRCodeImage` (Approach A: QRCoder native icon overlay + footer composition).
- New asset: `Yakult.Inventory.App/images/yakult_bottle_qr_icon.png` (~200x200, black bottle / white label on white), added as Content + `<Compile Include>`-adjacent resource entry per AGENTS.md csproj rule.
- No scanner change: payload stays `yakult:set:v1:{GUID:D}` at ECC-Q; caption sits outside quiet zone.

## Components
1. `SetQRGenerator.GenerateQRCodeImage` — overload resolution: build token string (unchanged) → `CreateQrCode(ECCLevel.Q)` → `GetGraphic(5, black, white, icon, 15, 6, true)` with icon loaded from asset (fallback: plain `GetGraphic(5)` if asset missing) → if model caption found, compose taller bitmap (QR on top, white footer ~18% height, centered Arial Bold, shrink-to-fit single line) → return.
2. Caption resolution is inline: trimmed `setDto.ComputerName`; empty means no footer. No Category/ItemName detection (removed `FindFirstPcModelNumber`).
3. `ComposeCaptionFooter(qrBitmap, caption)` helper — GDI+ composition, disposes intermediates.
4. Asset loading helper — resolves `images/yakult_bottle_qr_icon.png` relative to `AppDomain.CurrentDomain.BaseDirectory` with design-time fallback; never throws (returns null → plain QR).

## Data flow
`BtnGenerateQR_Click` (unchanged order) → `GenerateQRDataString` (unchanged JSON) + `GenerateQRCodeImage` (now branded) → `GetQRCodeImageBytes` (unchanged PNG encode) → `UpdateQRDataAsync` (unchanged). Viewer/PDF read the same blob.

## Error handling
- Missing/invalid icon asset → plain QR (no failure).
- Caption render failure → return uncaptioned QR (never block generation).
- Empty-token fallback path (full JSON payload) → skip icon/caption (capacity risk noted in prior analysis) to avoid `CreateQrCode` throw.
- All GDI+ objects (`QRCodeGenerator`, `QRCodeData`, `QRCode`, `Bitmap`, `Graphics`, `Font`) wrapped in `using`.

## Testing
- Generate for: (a) Hardware set with model → logo + caption; (b) non-PC set → logo only; (c) missing icon file → plain QR; (d) empty set guard unchanged.
- Scan each branded PNG with phone camera + `dispatch-set.ashx` token path (`yakult:set:v1:` strip + Guid parse).
- Print via `ImageViewerDialog` 2x2 and confirm scannability.
- Confirm `QRData` JSON bytes unchanged in shape.
