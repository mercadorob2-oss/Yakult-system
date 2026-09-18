# fix_api_503.ps1 - Run this as Administrator
# Fixes CS0433 duplicate 'ASP.global_asax' type causing 503 on POST /api/auth/login

$ErrorActionPreference = "Stop"

$src = "C:\Users\russel.mercado\source\repos\Yakult-System-for-Merging\Yakult.Inventory.Api2\PrecompiledWeb\Yakult.Inventory.Api"
$dst = "C:\inetpub\wwwroot\Yakult.Inventory.Api2"
$appPool = "api_testing"
$appcmd = "$env:windir\system32\inetsrv\appcmd.exe"
$tempCache = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files\root\5ae14314"

Write-Host "`n=== Step 1: Stop App Pool '$appPool' ===" -ForegroundColor Cyan
& $appcmd stop apppool $appPool
Start-Sleep -Seconds 2

Write-Host "`n=== Step 2: Copy precompiled deployment files ===" -ForegroundColor Cyan
Copy-Item "$src\PrecompiledApp.config" "$dst\PrecompiledApp.config" -Force
Write-Host "  [OK] PrecompiledApp.config"
Copy-Item "$src\bin\App_global.asax.dll" "$dst\bin\App_global.asax.dll" -Force
Write-Host "  [OK] App_global.asax.dll"
Copy-Item "$src\bin\App_global.asax.compiled" "$dst\bin\App_global.asax.compiled" -Force
Write-Host "  [OK] App_global.asax.compiled"

Write-Host "`n=== Step 3: Clear Temporary ASP.NET Files cache ===" -ForegroundColor Cyan
if (Test-Path $tempCache) {
    Remove-Item $tempCache -Recurse -Force
    Write-Host "  [OK] Cache cleared"
} else {
    Write-Host "  [SKIP] Cache path not found"
}

Write-Host "`n=== Step 4: Start App Pool '$appPool' ===" -ForegroundColor Cyan
& $appcmd start apppool $appPool

Write-Host "`n=== Done! Test the endpoint: POST http://192.168.39.74:7000/api/auth/login ===" -ForegroundColor Green
