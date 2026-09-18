<#
.SYNOPSIS
    Backs up the Yakult Inventory System databases (FULL backup) via sqlcmd, and
    prunes backups older than the configured retention period.

.DESCRIPTION
    Runs a T-SQL BACKUP DATABASE statement per database, timestamped, and stores
    the .bak files in $BackupFolder. The BACKUP command executes on the SQL Server
    itself, so $BackupFolder must be a path the SQL Server SERVICE ACCOUNT can
    write to (a local path on the server, as configured here, satisfies this).

    Credentials are loaded from the encrypted file created by
    Setup-BackupCredential.ps1 (Windows DPAPI) - never stored in plaintext here.

    Intended to be run by the scheduled task created via Install-BackupTask.ps1,
    but can also be run manually for an ad-hoc backup.

.EXAMPLE
    .\Backup-YakultDatabases.ps1
#>

[CmdletBinding()]
param(
    [string]$SqlInstance     = ".\SQLEXPRESS",
    [string[]]$Databases     = @("Yakult_Inventory_System_DEV", "YIMS_PROD"),
    [string]$BackupFolder    = "C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS\MSSQL\Backup",
    [int]$RetentionDays      = 14,
    [string]$CredentialPath  = "",
    [string]$LogFolder       = ""
)

$ErrorActionPreference = "Stop"

# $PSScriptRoot is unreliable under some -File invocation contexts (observed empty
# on this host); fall back to the invoked script's own path to locate sibling files.
$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot }
             elseif ($MyInvocation.MyCommand.Path) { Split-Path -Parent $MyInvocation.MyCommand.Path }
             else { (Get-Location).Path }

if (-not $CredentialPath) { $CredentialPath = Join-Path $ScriptDir "sql-backup.cred.xml" }
if (-not $LogFolder)      { $LogFolder      = Join-Path $ScriptDir "Logs" }

if (-not (Test-Path $LogFolder)) {
    New-Item -ItemType Directory -Path $LogFolder -Force | Out-Null
}
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logFile   = Join-Path $LogFolder "backup_$timestamp.log"

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $line = "[{0}] [{1}] {2}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Level, $Message
    Write-Host $line
    Add-Content -Path $logFile -Value $line
}

Write-Log "=== Backup job started ==="

if (-not (Test-Path $CredentialPath)) {
    Write-Log "Credential file not found at '$CredentialPath'. Run Setup-BackupCredential.ps1 first." "ERROR"
    exit 1
}

try {
    $cred = Import-Clixml -Path $CredentialPath
}
catch {
    Write-Log "Failed to decrypt credential file. It may have been created by a different Windows user/machine. $_" "ERROR"
    exit 1
}

$sqlUser = $cred.UserName
$sqlPass = $cred.GetNetworkCredential().Password

$sqlcmdPath = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmdPath) {
    Write-Log "sqlcmd.exe not found on PATH. Install the SQL Server command-line utilities." "ERROR"
    exit 1
}

$failures = @()

foreach ($db in $Databases) {
    $dbTimestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupFile  = Join-Path $BackupFolder "$($db)_FULL_$dbTimestamp.bak"

    # T-SQL identifiers/paths are quoted; $db and $backupFile come from trusted
    # local parameters (not external/user input), so this is safe to inline.
    $sql = @"
BACKUP DATABASE [$db]
TO DISK = N'$backupFile'
WITH INIT, CHECKSUM,
NAME = N'$db-Full Backup $dbTimestamp';
"@

    Write-Log "Backing up '$db' -> $backupFile"

    $result = & sqlcmd -S $SqlInstance -U $sqlUser -P $sqlPass -C -Q $sql -b 2>&1
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        Write-Log "BACKUP FAILED for '$db' (exit code $exitCode): $result" "ERROR"
        $failures += $db
    }
    else {
        Write-Log "Backup succeeded for '$db'."
    }
}

# Retention cleanup - only touch files matching our naming pattern for the databases we manage.
Write-Log "Pruning backups older than $RetentionDays day(s) in $BackupFolder"
try {
    $cutoff = (Get-Date).AddDays(-$RetentionDays)
    foreach ($db in $Databases) {
        $old = Get-ChildItem -Path $BackupFolder -Filter "$($db)_FULL_*.bak" -File -ErrorAction SilentlyContinue |
               Where-Object { $_.LastWriteTime -lt $cutoff }
        foreach ($file in $old) {
            Remove-Item -Path $file.FullName -Force
            Write-Log "Deleted old backup: $($file.Name)"
        }
    }
}
catch {
    Write-Log "Retention cleanup encountered an error: $_" "WARN"
}

if ($failures.Count -gt 0) {
    Write-Log "=== Backup job finished WITH FAILURES: $($failures -join ', ') ===" "ERROR"
    exit 1
}

Write-Log "=== Backup job finished successfully ==="
exit 0
