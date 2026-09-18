<#
.SYNOPSIS
    Builds and deploys the Yakult ITCM Server to a remote IIS server.

.DESCRIPTION
    This script:
    1. Restores and builds the project in Release configuration
    2. Publishes to a local folder
    3. Copies the published output to the remote IIS server via admin share
    4. Copies appsettings.local.json if present
    5. Runs a health check

.PARAMETER Server
    Remote IIS server IP or hostname. Default: 192.168.100.186

.PARAMETER TargetFolder
    IIS physical path on the remote server. Default: C:\inetpub\wwwroot\Yakult.ITCM.Scheduler

.PARAMETER AppPoolName
    IIS application pool name. Default: Yakult.ITCM.Scheduler

.PARAMETER LocalDeploy
    Switch. Run directly on the IIS server (executes app pool commands).

.PARAMETER SkipBuild
    Switch. Skip dotnet publish and use existing publish output.

.PARAMETER Configuration
    Build configuration. Default: Release

.EXAMPLE
    .\Deploy-ItcmServer-Remote.ps1

.EXAMPLE
    .\Deploy-ItcmServer-Remote.ps1 -Server 192.168.100.186 -SkipBuild
#>

param(
    [string]$Server       = "192.168.100.186",
    [string]$TargetFolder = "C:\inetpub\wwwroot\Yakult.ITCM.Scheduler",
    [string]$AppPoolName  = "Yakult.ITCM.Scheduler",
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
$localConfig = Join-Path $projectDir "appsettings.local.json"
$remotePath  = "\\$Server\C$\$($TargetFolder.Substring(3))"  # C:\... -> \\server\C$\...
$portalUrl   = "http://$Server`:50330"

Write-Host "=== Yakult ITCM Server Deployment (Remote) ===" -ForegroundColor Cyan
Write-Host "Project:       $projectFile"
Write-Host "Publish to:    $publishDir"
Write-Host "Target:        $TargetFolder"
Write-Host "Remote share:  $remotePath"
Write-Host "Server:        $Server"
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

# Step 3: Include appsettings.local.json if present
Write-Host "[3/4] Including local config..." -ForegroundColor Yellow
if (Test-Path $localConfig) {
    Copy-Item $localConfig "$publishDir\appsettings.local.json" -Force
    Write-Host "  Copied: appsettings.local.json"
} else {
    Write-Host "  Skipped: appsettings.local.json not found (create one at project root)"
}

# Step 4: Deploy to remote IIS
Write-Host "[4/4] Deploying to $Server ..." -ForegroundColor Yellow

# Stop app pool (local only — remote uses admin share, no WinRM)
if ($LocalDeploy) {
    try {
        Import-Module WebAdministration -ErrorAction Stop
        if (Get-WebAppPoolState $AppPoolName -ErrorAction SilentlyContinue) {
            Stop-WebAppPool -Name $AppPoolName
            Write-Host "  Stopped app pool: $AppPoolName"
            Start-Sleep -Seconds 2
        }
    } catch {
        Write-Host "  Warning: Could not manage app pool." -ForegroundColor Yellow
    }
}

# Ensure target exists on remote
if (-not (Test-Path $remotePath)) {
    New-Item -ItemType Directory -Path $remotePath -Force | Out-Null
    Write-Host "  Created remote directory: $remotePath"
}

# Robocopy to remote admin share
Write-Host "  Copying files to $remotePath ..."
$robocopyExit = (robocopy $publishDir $remotePath /E /MIR /R:3 /W:5 /NJH /NJS /NP 2>&1)
$robocopyOk = $LASTEXITCODE -le 7
if ($robocopyOk) {
    Write-Host "  Copy completed (exit code: $LASTEXITCODE)" -ForegroundColor Green
} else {
    throw "Robocopy failed with exit code $LASTEXITCODE"
}

# Start app pool (local only)
if ($LocalDeploy) {
    try {
        Import-Module WebAdministration -ErrorAction Stop
        Start-WebAppPool -Name $AppPoolName
        Write-Host "  Started app pool: $AppPoolName"
    } catch {
        Write-Host "  Warning: Could not start app pool." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "=== Deployment complete ===" -ForegroundColor Cyan
Write-Host "Target: $Server`:$TargetFolder"
Write-Host ""

# Health check
Write-Host "Running health check against $portalUrl ..." -ForegroundColor Yellow
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
    Write-Host "  Warning: Health check did not pass. Verify the site is running at $portalUrl" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Browse to http://$Server`:50330 to verify the dashboard"
Write-Host "  2. Open /Config to configure DB connection settings remotely"
Write-Host "  3. Set ASPNETCORE_ENVIRONMENT=Production in IIS app pool env vars"
Write-Host "  4. Ensure the app pool identity has read/execute access to $TargetFolder"
Write-Host ""