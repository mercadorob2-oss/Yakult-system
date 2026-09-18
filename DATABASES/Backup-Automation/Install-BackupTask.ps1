<#
.SYNOPSIS
    Registers the Windows Scheduled Task that runs Backup-YakultDatabases.ps1
    Monday-Saturday at 5:00 PM.

.DESCRIPTION
    Must be run as Administrator, on the machine where the backups should run
    (i.e. the machine with access to .\SQLEXPRESS and the encrypted credential
    file created by Setup-BackupCredential.ps1).

    The task runs as the SAME Windows user who ran Setup-BackupCredential.ps1,
    because the credential file is DPAPI-encrypted to that user/machine pair.
    You will be prompted for that Windows account's password so the task can
    log on and load the user profile (required for DPAPI to decrypt).

    Schedule requested: Monday-Saturday, 5:00 PM.
    NOTE ON TIME ZONE: Task Scheduler triggers fire at the specified time in
    the SERVER's own local clock/time zone, not a named time zone. If this
    server's Windows time zone is already set to "Taipei Standard Time" /
    Asia/Manila (UTC+8), 5:00 PM below is correct as-is. If the server is on a
    different time zone (e.g. UTC), either change the server's time zone to
    Asia/Manila, or pass -TriggerTime with the equivalent local time.

.EXAMPLE
    .\Install-BackupTask.ps1
.EXAMPLE
    .\Install-BackupTask.ps1 -TriggerTime "17:00"
#>

[CmdletBinding()]
param(
    [string]$TaskName    = "Yakult DB Backup",
    [string]$TriggerTime = "17:00",
    [string]$ScriptPath  = ""
)

#Requires -RunAsAdministrator

# $PSScriptRoot is unreliable under some invocation contexts; fall back to the
# invoked script's own path to locate sibling files.
$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot }
             elseif ($MyInvocation.MyCommand.Path) { Split-Path -Parent $MyInvocation.MyCommand.Path }
             else { (Get-Location).Path }

if (-not $ScriptPath) { $ScriptPath = Join-Path $ScriptDir "Backup-YakultDatabases.ps1" }

if (-not (Test-Path $ScriptPath)) {
    Write-Error "Backup script not found at '$ScriptPath'."
    exit 1
}

$credFile = Join-Path $ScriptDir "sql-backup.cred.xml"
if (-not (Test-Path $credFile)) {
    Write-Warning "No encrypted SQL credential found at '$credFile'."
    Write-Warning "Run Setup-BackupCredential.ps1 as the account below BEFORE using this task."
}

Write-Host "The task must run as the same Windows account that created the encrypted" -ForegroundColor Yellow
Write-Host "SQL credential (Setup-BackupCredential.ps1), so it can decrypt it via DPAPI." -ForegroundColor Yellow
$runAsCred = Get-Credential -Message "Enter the Windows account (DOMAIN\User or .\User) to run this task as"

$action = New-ScheduledTaskAction `
    -Execute "powershell.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`""

$trigger = New-ScheduledTaskTrigger `
    -Weekly `
    -DaysOfWeek Monday,Tuesday,Wednesday,Thursday,Friday,Saturday `
    -At $TriggerTime

$settings = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -DontStopOnIdleEnd `
    -ExecutionTimeLimit (New-TimeSpan -Hours 2) `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 5)

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -User $runAsCred.UserName `
    -Password $runAsCred.GetNetworkCredential().Password `
    -RunLevel Highest `
    -Force | Out-Null

Write-Host ""
Write-Host "Scheduled task '$TaskName' registered: Mon-Sat at $TriggerTime (server local time)." -ForegroundColor Green
Write-Host "Verify in Task Scheduler, or run it once manually with:" -ForegroundColor Cyan
Write-Host "  Start-ScheduledTask -TaskName `"$TaskName`"" -ForegroundColor Cyan
Write-Host "Logs are written to: $ScriptDir\Logs" -ForegroundColor Cyan
