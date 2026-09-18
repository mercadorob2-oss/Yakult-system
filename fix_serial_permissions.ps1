# fix_serial_permissions.ps1
# Run this as Administrator on the IIS server (192.168.27.124)
# Fixes write permissions for mobile serial queue (App_Data\mobile-serials)
# AND ensures mobile-serials folder exists for BOTH PROD and DEV APIs.

$ErrorActionPreference = "Stop"

$prodAppData = "C:\inetpub\wwwroot\Yakult.Inventory.Api2\App_Data"
$devAppData  = "C:\inetpub\wwwroot\Yakult.Inventory.Api2_DEV\App_Data"

$prodSerials = Join-Path $prodAppData "mobile-serials"
$devSerials  = Join-Path $devAppData  "mobile-serials"

Write-Host "`n=== Yakult API – Fix App_Data Write Permissions ===" -ForegroundColor Cyan

# ── PROD ──────────────────────────────────────────────────────────────────────
Write-Host "`n[PROD] $prodAppData" -ForegroundColor Yellow

if (-not (Test-Path $prodAppData)) {
    Write-Host "  [CREATE] App_Data folder (did not exist)"
    New-Item -ItemType Directory -Path $prodAppData | Out-Null
}

if (-not (Test-Path $prodSerials)) {
    Write-Host "  [CREATE] mobile-serials folder"
    New-Item -ItemType Directory -Path $prodSerials | Out-Null
} else {
    Write-Host "  [OK]     mobile-serials folder already exists"
}

Write-Host "  [PERM]   Granting IIS_IUSRS modify access..."
icacls $prodAppData /grant "IIS_IUSRS:(OI)(CI)M" /T | Out-Null
Write-Host "  [OK]     Permissions applied" -ForegroundColor Green

# ── DEV ───────────────────────────────────────────────────────────────────────
Write-Host "`n[DEV] $devAppData" -ForegroundColor Yellow

if (-not (Test-Path $devAppData)) {
    Write-Host "  [CREATE] App_Data folder (did not exist)"
    New-Item -ItemType Directory -Path $devAppData | Out-Null
}

if (-not (Test-Path $devSerials)) {
    Write-Host "  [CREATE] mobile-serials folder"
    New-Item -ItemType Directory -Path $devSerials | Out-Null
} else {
    Write-Host "  [OK]     mobile-serials folder already exists"
}

Write-Host "  [PERM]   Granting IIS_IUSRS modify access..."
icacls $devAppData /grant "IIS_IUSRS:(OI)(CI)M" /T | Out-Null
Write-Host "  [OK]     Permissions applied" -ForegroundColor Green

# ── Verify ────────────────────────────────────────────────────────────────────
Write-Host "`n=== Verification ===" -ForegroundColor Cyan

foreach ($path in @($prodSerials, $devSerials)) {
    $testFile = Join-Path $path "write-test.tmp"
    try {
        [IO.File]::WriteAllText($testFile, "test")
        Remove-Item $testFile -Force
        Write-Host "  [PASS] Write test: $path" -ForegroundColor Green
    } catch {
        Write-Host "  [FAIL] Write test: $path  -> $_" -ForegroundColor Red
    }
}

Write-Host "`n=== Done! ===" -ForegroundColor Green
Write-Host "Now test from the mobile app by scanning a serial and tapping 'Send to Windows'."
Write-Host ""
Write-Host "You can also verify with curl from the IIS server:"
Write-Host "  curl -X POST http://192.168.27.124:7000/api/Items/ReceiveSerialFromMobile -H 'Content-Type: application/json' -d '{""serialNumber"":""TEST-SERIAL-001""}'"
