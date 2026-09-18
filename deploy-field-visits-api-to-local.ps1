# Deploy call-field-visits.ashx to Api2_local (Development Server)
# Run this script as Administrator

$source = "C:\Users\russel.mercado\source\repos\Yakult-System-for-Merging\Yakult.Inventory.Api2_remote\call-field-visits.ashx"
$destination = "C:\inetpub\wwwroot\Yakult.Inventory.Api2_local\call-field-visits.ashx"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deploying call-field-visits.ashx to Development Server" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as admin
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Right-click this script and select 'Run as Administrator'" -ForegroundColor Yellow
    Write-Host ""
    pause
    exit 1
}

Write-Host "Source:      $source" -ForegroundColor Gray
Write-Host "Destination: $destination" -ForegroundColor Gray
Write-Host ""

if (-not (Test-Path $source)) {
    Write-Host "ERROR: Source file not found!" -ForegroundColor Red
    Write-Host "Expected: $source" -ForegroundColor Yellow
    Write-Host ""
    pause
    exit 1
}

try {
    Copy-Item -Path $source -Destination $destination -Force
    
    if (Test-Path $destination) {
        Write-Host "[SUCCESS] File deployed successfully to Api2_local!" -ForegroundColor Green
        Write-Host ""
        Write-Host "The mobile app can now access the field work API at:" -ForegroundColor Cyan
        Write-Host "http://192.168.100.186:7015/call-field-visits.ashx" -ForegroundColor White
    } else {
        Write-Host "[ERROR] Deployment failed - file not found at destination" -ForegroundColor Red
    }
}
catch {
    Write-Host "[ERROR] Deployment failed!" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Yellow
}

Write-Host ""
pause
