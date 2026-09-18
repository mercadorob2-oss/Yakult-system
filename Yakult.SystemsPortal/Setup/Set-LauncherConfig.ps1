<#
.SYNOPSIS
    Configures environment variables for YakultLauncher on the server.

.DESCRIPTION
    Run this script ON the target server to configure optional environment variables
    that control the launcher behavior:

    - YAKULT_APP_PATH: Override the Yakult Inventory App path
      (default: C:\Program Files\Yakult\Yakult.Inventory.App.exe)
    - YAKULT_STARTUP_DELAY_SECONDS: Override the startup delay in seconds
      (default: 5)

    These settings are persisted at the system level and survive reboots.

.PARAMETER YakultAppPath
    Full path to Yakult.Inventory.App.exe on the server.

.PARAMETER StartupDelaySeconds
    Number of seconds to wait after starting the desktop before launching the app.

.PARAMETER ClearAll
    If set, removes all launcher environment variables and restores defaults.

.PARAMETER Scope
    Variable scope: Machine (system-wide) or User (current user only).
    Default: Machine

.EXAMPLE
    .\Set-LauncherConfig.ps1 -YakultAppPath "D:\Apps\Yakult.Inventory.App.exe"
    # Change the app path

.EXAMPLE
    .\Set-LauncherConfig.ps1 -StartupDelaySeconds 10
    # Increase startup delay to 10 seconds

.EXAMPLE
    .\Set-LauncherConfig.ps1 -ClearAll
    # Reset all settings to defaults

.NOTES
    Requires: Run as Administrator (for Machine scope)
#>

[CmdletBinding()]
param(
    [string] $YakultAppPath,
    [int] $StartupDelaySeconds,
    [switch] $ClearAll,
    [ValidateSet("Machine", "User")]
    [string] $Scope = "Machine"
)

$ErrorActionPreference = "Stop"

function Write-Step($text) {
    Write-Host "`n>> $text" -ForegroundColor Cyan
}

function Write-Ok($text) {
    Write-Host "   [OK] $text" -ForegroundColor Green
}

function Write-Warn($text) {
    Write-Host "   [!] $text" -ForegroundColor Yellow
}

$defaultAppPath = "C:\Program Files\Yakult\Yakult.Inventory.App.exe"
$defaultDelay = 5

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "YakultLauncher Configuration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Scope: $Scope"
Write-Host ""

# Check admin privileges for Machine scope
if ($Scope -eq "Machine") {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Warning "Machine scope requires Administrator privileges"
        Write-Host "  Re-run with 'Run as administrator' or use -Scope User" -ForegroundColor Yellow
        exit 1
    }
}

# Clear all settings
if ($ClearAll) {
    Write-Step "Clearing all launcher environment variables"
    
    [Environment]::SetEnvironmentVariable("YAKULT_APP_PATH", $null, $Scope)
    [Environment]::SetEnvironmentVariable("YAKULT_STARTUP_DELAY_SECONDS", $null, $Scope)
    
    Write-Ok "Cleared YAKULT_APP_PATH"
    Write-Ok "Cleared YAKULT_STARTUP_DELAY_SECONDS"
    
    Write-Host "`n  Settings reset to defaults:" -ForegroundColor Green
    Write-Host "    App path  : $defaultAppPath"
    Write-Host "    Delay     : ${defaultDelay}s"
    exit 0
}

# Set Yakult app path
if ($PSBoundParameters.ContainsKey("YakultAppPath")) {
    Write-Step "Setting YAKULT_APP_PATH"
    
    if (-not (Test-Path -LiteralPath $YakultAppPath -PathType Leaf)) {
        Write-Warn "File not found at: $YakultAppPath"
        $confirm = Read-Host "   Continue anyway? (y/N)"
        if ($confirm -ne 'y' -and $confirm -ne 'Y') {
            Write-Host "`nCancelled." -ForegroundColor Yellow
            exit 0
        }
    }
    
    [Environment]::SetEnvironmentVariable("YAKULT_APP_PATH", $YakultAppPath, $Scope)
    Write-Ok "Set YAKULT_APP_PATH = $YakultAppPath"
}

# Set startup delay
if ($PSBoundParameters.ContainsKey("StartupDelaySeconds")) {
    Write-Step "Setting YAKULT_STARTUP_DELAY_SECONDS"
    
    if ($StartupDelaySeconds -lt 0) {
        Write-Warning "Delay cannot be negative. Setting to 0."
        $StartupDelaySeconds = 0
    }
    
    if ($StartupDelaySeconds -gt 30) {
        Write-Warning "Delay of ${StartupDelaySeconds}s is unusually long. Consider reducing."
    }
    
    [Environment]::SetEnvironmentVariable("YAKULT_STARTUP_DELAY_SECONDS", $StartupDelaySeconds.ToString(), $Scope)
    Write-Ok "Set YAKULT_STARTUP_DELAY_SECONDS = $StartupDelaySeconds"
}

# Show current settings
Write-Step "Current Configuration"

$currentAppPath = [Environment]::GetEnvironmentVariable("YAKULT_APP_PATH", $Scope)
$currentDelay = [Environment]::GetEnvironmentVariable("YAKULT_STARTUP_DELAY_SECONDS", $Scope)

if ([string]::IsNullOrWhiteSpace($currentAppPath)) {
    $currentAppPath = $defaultAppPath
    Write-Host "  App path  : $currentAppPath (default)" -ForegroundColor Gray
} else {
    Write-Host "  App path  : $currentAppPath" -ForegroundColor Green
}

if ([string]::IsNullOrWhiteSpace($currentDelay)) {
    $currentDelay = $defaultDelay
    Write-Host "  Delay     : ${currentDelay}s (default)" -ForegroundColor Gray
} else {
    Write-Host "  Delay     : ${currentDelay}s" -ForegroundColor Green
}

Write-Host "`n  These settings take effect on the next RDP session." -ForegroundColor Cyan
Write-Host ""

exit 0
