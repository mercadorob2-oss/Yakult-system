<#
.SYNOPSIS
    Run ON THE SERVER (over Remote Desktop), as Administrator, from the extracted zip folder.
    Installs or updates the Yakult Inventory Gateway in IIS.

.DESCRIPTION
    First run:  creates the app pool, IIS site, key folder, firewall rule, and a
                template appsettings.Local.json for you to fill in.
    Later runs: updates the site files only. The server's appsettings.Local.json
                is never overwritten or deleted.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Install-Gateway-OnServer.ps1
    powershell -ExecutionPolicy Bypass -File .\Install-Gateway-OnServer.ps1 -Port 7020
#>

[CmdletBinding()]
param(
    [string] $SiteName = "Yakult.Inventory.Gateway",
    [int]    $Port     = 7018,
    [string] $SitePath = "C:\inetpub\wwwroot\Yakult.Inventory.Gateway",
    [string] $KeyPath  = "C:\ProgramData\Yakult\Gateway\DataProtection-Keys"
)

$ErrorActionPreference = "Stop"
function Step($t) { Write-Host "`n>> $t" -ForegroundColor Cyan }
function Ok($t)   { Write-Host "   [OK] $t" -ForegroundColor Green }
function Warn($t) { Write-Host "   [!]  $t" -ForegroundColor Yellow }

# ---- Checks ----
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this as Administrator (right-click PowerShell > Run as administrator)."
}
if (-not (Test-Path (Join-Path $PSScriptRoot "Yakult.Inventory.Gateway.dll"))) {
    throw "Run this from the extracted zip folder (Yakult.Inventory.Gateway.dll must be next to this script)."
}
if (-not (Test-Path "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll")) {
    Warn "ASP.NET Core Hosting Bundle not found. ITCM uses it too, so it is usually already installed. If the site returns 500.19/500.31, install the .NET 8 Hosting Bundle."
}
Import-Module WebAdministration

# ---- App pool ----
Step "App pool '$SiteName'"
if (-not (Test-Path "IIS:\AppPools\$SiteName")) {
    New-WebAppPool -Name $SiteName | Out-Null
    Ok "Created"
}
Set-ItemProperty "IIS:\AppPools\$SiteName" -Name managedRuntimeVersion -Value ""   # No Managed Code
if ((Get-WebAppPoolState -Name $SiteName).Value -eq "Started") {
    Stop-WebAppPool -Name $SiteName
    Start-Sleep -Seconds 3
    Ok "Stopped for update"
}

# ---- Files ----
Step "Copying files to $SitePath"
New-Item -ItemType Directory -Path $SitePath -Force | Out-Null
# /XF: excluded files are neither copied nor purged, so the server's config survives /MIR.
& robocopy $PSScriptRoot $SitePath /MIR /R:3 /W:5 /NJH /NJS /NP /NFL /NDL `
    /XF appsettings.Local.json Install-Gateway-OnServer.ps1 | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed (exit code $LASTEXITCODE)." }
Ok "Files copied"

$localConfig = Join-Path $SitePath "appsettings.Local.json"
$needsConfig = $false
if (-not (Test-Path $localConfig)) {
    $template = @'
{
  "ConnectionStrings": {
    "Yakult_Inventory_System": "Server=192.168.100.186,50301;Database=DATABASE_NAME;User ID=SQL_LOGIN;Password=SQL_PASSWORD;TrustServerCertificate=True;Encrypt=False;",
    "LegacyClient": "Server=192.168.100.186,50301;Database=DATABASE_NAME;User ID=SQL_LOGIN;Password=SQL_PASSWORD;TrustServerCertificate=True;Encrypt=False;"
  }
}
'@
    [IO.File]::WriteAllText($localConfig, $template, (New-Object Text.UTF8Encoding($false)))
    $needsConfig = $true
    Warn "Created a template appsettings.Local.json. You must fill it in (see the end of this script's output)."
} elseif ((Get-Content $localConfig -Raw) -match "SQL_PASSWORD|DATABASE_NAME") {
    $needsConfig = $true
    Warn "appsettings.Local.json still has placeholders."
} else {
    Ok "Kept the existing appsettings.Local.json"
}

# Only Administrators and the app pool may read the file holding the password.
& icacls $localConfig /inheritance:r /grant:r "Administrators:F" "SYSTEM:F" "IIS AppPool\$($SiteName):R" | Out-Null
Ok "Locked down appsettings.Local.json (Administrators, SYSTEM, app pool only)"

# ---- Token keys ----
Step "Token key folder $KeyPath"
New-Item -ItemType Directory -Path $KeyPath -Force | Out-Null
& icacls $KeyPath /grant "IIS AppPool\$($SiteName):(OI)(CI)M" | Out-Null
Ok "App pool can write token keys"

# ---- Site ----
Step "IIS site on port $Port"
if (-not (Get-Website -Name $SiteName)) {
    $taken = Get-WebBinding | Where-Object { $_.bindingInformation -match ":$($Port):" }
    if ($taken) { throw "Port $Port is already used by another IIS site. Re-run with -Port <free port>." }
    New-Website -Name $SiteName -Port $Port -PhysicalPath $SitePath -ApplicationPool $SiteName | Out-Null
    Ok "Created site"
} else {
    Ok "Site already exists"
}

# ---- Firewall ----
Step "Firewall"
$ruleName = "Yakult Inventory Gateway (TCP $Port)"
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow | Out-Null
    Ok "Opened TCP $Port"
} else {
    Ok "Rule already exists"
}

# ---- Start + health ----
Step "Starting"
Start-WebAppPool -Name $SiteName
Start-Website -Name $SiteName -ErrorAction SilentlyContinue
$health = $null
for ($i = 1; $i -le 10 -and -not $health; $i++) {
    Start-Sleep -Seconds 2
    try { $health = Invoke-RestMethod "http://localhost:$Port/api/health" -TimeoutSec 5 } catch { }
}

Write-Host ""
if (-not $health) {
    Warn "The site did not answer http://localhost:$Port/api/health. Check Event Viewer > Windows Logs > Application."
} elseif ($health.databaseReachable) {
    Ok "Gateway is running and reached the database."
    Write-Host "   Desktop App.config:  <add key=`"GatewayUrl`" value=`"http://$($env:COMPUTERNAME):$Port`" />  (or use the server IP)"
} else {
    Warn "Gateway is running but can NOT reach the database."
}

if ($needsConfig) {
    Write-Host ""
    Write-Host "NEXT: open $localConfig in Notepad (as Administrator)," -ForegroundColor Yellow
    Write-Host "      replace DATABASE_NAME / SQL_LOGIN / SQL_PASSWORD with the real values," -ForegroundColor Yellow
    Write-Host "      save, then run:  Restart-WebAppPool -Name '$SiteName'" -ForegroundColor Yellow
    Write-Host "      and check:       http://localhost:$Port/api/health  shows databaseReachable: true" -ForegroundColor Yellow
}
