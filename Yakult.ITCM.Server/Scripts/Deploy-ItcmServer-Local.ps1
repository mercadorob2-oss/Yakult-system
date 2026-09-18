<#
.SYNOPSIS
    Builds and deploys the Yakult ITCM Server to local IIS for development testing.

.DESCRIPTION
    This script:
    1. Restores and builds the project in Release configuration
    2. Publishes to the local IIS directory at C:\inetpub\wwwroot\Yakult.ITCM.Scheduler
    3. Stops/starts the IIS app pool (Yakult.ITCM.Scheduler)
    4. Copies appsettings.local.json if present
    5. Runs a health check against the deployed site

.PARAMETER LocalDeploy
    Switch. Run directly on the IIS server (executes app pool commands).

.PARAMETER SkipBuild
    Switch. Skip dotnet publish and use existing publish output.

.PARAMETER Configuration
    Build configuration. Default: Release

.EXAMPLE
    .\Deploy-ItcmServer-Local.ps1 -LocalDeploy
#>

param(
    [switch]$LocalDeploy,
    [switch]$SkipBuild,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Directories
$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir  = Resolve-Path "$scriptDir\.."
$projectFile = Join-Path $projectDir "Yakult.ITCM.Server.csproj"
$publishDir  = Join-Path $projectDir "bin\$Configuration\net8.0\publish"
$targetDir   = "C:\inetpub\wwwroot\Yakult.ITCM.Scheduler"
$localConfig = Join-Path $projectDir "appsettings.local.json"
$appPoolName = "Yakult.ITCM.Scheduler"
$portalUrl   = "http://localhost:50330"

Write-Host "=== Yakult ITCM Server Deployment (Local) ===" -ForegroundColor Cyan
Write-Host "Project:       $projectFile"
Write-Host "Publish to:    $publishDir"
Write-Host "Target (IIS):  $targetDir"
Write-Host "App Pool:      $appPoolName"
Write-Host ""

if (-not $SkipBuild) {
    # Step 1: Restore
    Write-Host "[1/4] Restoring packages..." -ForegroundColor Yellow
    dotnet restore $projectFile
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

    # Step 2: Publish
    Write-Host "[2/4] Publishing..." -ForegroundColor Yellow
    if (Test-Path $publishDir) { Remove-Item "$publishDir\*" -Recurse -Force }
    dotnet publish $projectFile `
        --configuration $Configuration `
        --output $publishDir `
        --no-restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }
} else {
    Write-Host "[1/4] Skipped: restore" -ForegroundColor Yellow
    Write-Host "[2/4] Skipped: publish (using existing)" -ForegroundColor Yellow
}

# Step 3: Include appsettings.local.json if present (server-specific config)
Write-Host "[3/4] Including local config..." -ForegroundColor Yellow
if (Test-Path $localConfig) {
    Copy-Item $localConfig "$publishDir\appsettings.local.json" -Force
    Write-Host "  Copied: appsettings.local.json"
} else {
    Write-Host "  Skipped: appsettings.local.json not found (create one at project root)"
}

# Step 4: Deploy to IIS
Write-Host "[4/4] Deploying to IIS..." -ForegroundColor Yellow

# Stop app pool (only on local IIS)
if ($LocalDeploy) {
    try {
        Import-Module WebAdministration -ErrorAction Stop
        if (Get-WebAppPoolState $appPoolName -ErrorAction SilentlyContinue) {
            Stop-WebAppPool -Name $appPoolName
            Write-Host "  Stopped app pool: $appPoolName"
            Start-Sleep -Seconds 2
        }
    } catch {
        Write-Host "  Warning: Could not manage app pool (not on IIS server or missing WebAdministration). Trying appcmd..." -ForegroundColor Yellow
        try {
            & "$env:windir\system32\inetsrv\appcmd.exe" stop apppool /apppool.name:"$appPoolName" 2>$null
            Start-Sleep -Seconds 2
        } catch { }
    }
}

# Ensure target exists
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Write-Host "  Created: $targetDir"
}

# Robocopy (mirrors Portal's pattern)
Write-Host "  Copying files to $targetDir ..."
$robocopyExit = (robocopy $publishDir $targetDir /E /MIR /R:3 /W:5 /NJH /NJS /NP 2>&1)
Write-Host "  Robocopy exit code: $($LASTEXITCODE)"  # 0-7 = success

# Clear web.config if generated (hosting bundle handles it)
$webConfigPath = Join-Path $targetDir "web.config"
if (Test-Path $webConfigPath) {
    Write-Host "  web.config exists at: $webConfigPath" -ForegroundColor Green
}

# Start app pool (local only)
if ($LocalDeploy) {
    try {
        Import-Module WebAdministration -ErrorAction Stop
        Start-WebAppPool -Name $appPoolName
        Write-Host "  Started app pool: $appPoolName"
    } catch {
        try {
            & "$env:windir\system32\inetsrv\appcmd.exe" start apppool /apppool.name:"$appPoolName" 2>$null
        } catch { }
    }
}

Write-Host ""
Write-Host "=== Deployment complete ===" -ForegroundColor Cyan
Write-Host "IIS physical path: $targetDir"
Write-Host ""

# Health check
Write-Host "Running health check..." -ForegroundColor Yellow
$healthOk = $false
for ($i = 1; $i -le 5; $i++) {
    Start-Sleep -Seconds 3
    try {
        $response = Invoke-WebRequest -Uri "$portalUrl/api/itcm/health" -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
        if ($response.StatusCode -ge 200 -and $response.StatusCode -le 399) {
            Write-Host "  Health check passed (status $($response.StatusCode))" -ForegroundColor Green
            $healthOk = $true
            break
        }
    } catch {
        Write-Host "  Health check attempt $i/5 failed: $_" -ForegroundColor Yellow
    }
}
if (-not $healthOk) {
    Write-Warn "  Health check did not pass. Verify the site is running at $portalUrl"
}

Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Browse to http://localhost:50330 to verify the dashboard"
Write-Host "  2. Open /Config to configure DB connection and test connectivity"
Write-Host "  3. Set ASPNETCORE_ENVIRONMENT=Production in IIS app pool env vars"
Write-Host ""