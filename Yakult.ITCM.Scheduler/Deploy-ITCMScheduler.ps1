<#
.SYNOPSIS
    Deploys the Yakult ITCM background job to Windows Task Scheduler.

.DESCRIPTION
    Registers a scheduled task that runs Yakult.ITCM.Scheduler.exe every N minutes.
    Must be run as Administrator.

.PARAMETER ExePath
    Full path to the compiled Yakult.ITCM.Scheduler.exe.
    Default: C:\Yakult\Yakult.ITCM.Scheduler\Yakult.ITCM.Scheduler.exe

.PARAMETER TaskName
    Name of the scheduled task as it appears in Task Scheduler.

.PARAMETER IntervalMinutes
    How often the job runs. Default: 15 minutes.

.PARAMETER ServiceAccount
    Windows account to run the task as. Default: NT AUTHORITY\SYSTEM.
    For domain environments, use DOMAIN\YakultServiceAccount (Windows Auth SQL login).

.EXAMPLE
    # Basic deployment using defaults
    .\Deploy-ITCMScheduler.ps1 -ExePath "C:\Yakult\Yakult.ITCM.Scheduler\Yakult.ITCM.Scheduler.exe"

.EXAMPLE
    # Domain service account, run every 10 minutes
    .\Deploy-ITCMScheduler.ps1 `
        -ExePath "C:\Yakult\Yakult.ITCM.Scheduler\Yakult.ITCM.Scheduler.exe" `
        -ServiceAccount "YAKULT\svc_itcm" `
        -IntervalMinutes 10
#>

#requires -RunAsAdministrator

param(
    [string]$ExePath        = "C:\Yakult\Yakult.ITCM.Scheduler\Yakult.ITCM.Scheduler.exe",
    [string]$TaskName       = "Yakult ITCM Background Processing",
    [string]$TaskFolder     = "\Yakult",
    [string]$ServiceAccount = "NT AUTHORITY\SYSTEM",
    [int]   $IntervalMinutes = 15
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── AUTO-DEPLOY: copy from source / package to target folder ──────────────────
# If the script lives next to the compiled EXE (bin\Debug, bin\Release, or a
# hand-rolled deployment folder), copy everything to the canonical target.
$scriptDir   = Split-Path $MyInvocation.MyCommand.Path -Parent
$sourceExe   = Join-Path $scriptDir "Yakult.ITCM.Scheduler.exe"
$sourceConfig= Join-Path $scriptDir "Yakult.ITCM.Scheduler.exe.config"
$targetDir   = "C:\Yakult\Yakult.ITCM.Scheduler"

if ((Test-Path $sourceExe) -and (Test-Path $sourceConfig) -and ($sourceExe -ne $ExePath)) {
    Write-Host "Auto-deploying from '$scriptDir' -> '$targetDir' ..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Copy-Item -Path "$scriptDir\*" -Destination $targetDir -Force -Recurse
    Write-Host "Files copied. Target EXE : $ExePath" -ForegroundColor Green
}

# ── Validate prerequisites ─────────────────────────────────────────────────────
Write-Host "`n=== Yakult ITCM Scheduler Deployment ===" -ForegroundColor Cyan

if (-not (Test-Path $ExePath)) {
    throw "Executable not found at '$ExePath'.`nBuild the Yakult.ITCM.Scheduler project and ensure the EXE + .config exist."
}

$workDir = Split-Path $ExePath -Parent
$configPath = Join-Path $workDir "Yakult.ITCM.Scheduler.exe.config"
if (-not (Test-Path $configPath)) {
    Write-Warning "App.config not found at '$configPath'. Make sure it was copied with the EXE."
}

Write-Host "EXE       : $ExePath"
Write-Host "Task name : $TaskName"
Write-Host "Account   : $ServiceAccount"
Write-Host "Interval  : every $IntervalMinutes minutes"

# ── Register Windows Event Log source (one-time) ──────────────────────────────
try {
    if (-not [System.Diagnostics.EventLog]::SourceExists("YakultITCM")) {
        New-EventLog -LogName Application -Source "YakultITCM"
        Write-Host "`nEvent source 'YakultITCM' registered in Application log." -ForegroundColor Green
    } else {
        Write-Host "`nEvent source 'YakultITCM' already registered." -ForegroundColor DarkGray
    }
} catch {
    Write-Warning "Could not register event source: $_"
}

# ── Build task components ──────────────────────────────────────────────────────
$action = New-ScheduledTaskAction `
    -Execute $ExePath `
    -WorkingDirectory $workDir

# Trigger: start 1 minute from now, repeat every N minutes indefinitely
$startTime = (Get-Date).AddMinutes(1)
$trigger   = New-ScheduledTaskTrigger `
    -Once `
    -At $startTime `
    -RepetitionInterval  (New-TimeSpan -Minutes $IntervalMinutes) `
    -RepetitionDuration  (New-TimeSpan -Days 3650)

$settings  = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `           # Run as soon as possible if a scheduled start is missed
    -DontStopOnIdleEnd `
    -AllowStartOnDemand `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 5) `   # Kill if hung for > 5 min
    -MultipleInstances IgnoreNew    # Never run two copies simultaneously

$principal = New-ScheduledTaskPrincipal `
    -UserId    $ServiceAccount `
    -LogonType ServiceAccount `
    -RunLevel  Highest

# ── Remove old task if it exists ──────────────────────────────────────────────
$existing = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($existing) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    Write-Host "Previous task '$TaskName' removed." -ForegroundColor DarkGray
}

# ── Ensure task folder exists ─────────────────────────────────────────────────
$scheduler = New-Object -ComObject Schedule.Service
$scheduler.Connect()
$rootFolder = $scheduler.GetFolder("\")
try { $rootFolder.GetFolder($TaskFolder) | Out-Null }
catch {
    $rootFolder.CreateFolder($TaskFolder) | Out-Null
    Write-Host "Created Task Scheduler folder '$TaskFolder'." -ForegroundColor DarkGray
}

# ── Register the task ─────────────────────────────────────────────────────────
$task = Register-ScheduledTask `
    -TaskName  $TaskName `
    -TaskPath  $TaskFolder `
    -Action    $action `
    -Trigger   $trigger `
    -Settings  $settings `
    -Principal $principal `
    -Force

Write-Host "`nTask registered successfully." -ForegroundColor Green
Write-Host "Task path : $TaskFolder\$TaskName"
Write-Host "First run : $($startTime.ToString('yyyy-MM-dd HH:mm:ss'))"

# ── Show quick-reference commands ─────────────────────────────────────────────
Write-Host "`n--- Quick Reference ---" -ForegroundColor Yellow
Write-Host "Run manually :"
Write-Host "  schtasks /run /tn `"$TaskFolder\$TaskName`""
Write-Host ""
Write-Host "Check last result :"
Write-Host "  Get-ScheduledTaskInfo -TaskName '$TaskName' | Select-Object LastRunTime, LastTaskResult"
Write-Host ""
Write-Host "View Event Log entries :"
Write-Host "  Get-WinEvent -FilterHashtable @{LogName='Application'; ProviderName='YakultITCM'} -MaxEvents 20 | Format-List TimeCreated, LevelDisplayName, Message"
Write-Host ""
Write-Host "Disable (after verifying Task Scheduler is stable) :"
Write-Host "  Disable-ScheduledTask -TaskName '$TaskName'"
Write-Host ""
Write-Host "Remove entirely :"
Write-Host "  Unregister-ScheduledTask -TaskName '$TaskName' -Confirm:`$false"
