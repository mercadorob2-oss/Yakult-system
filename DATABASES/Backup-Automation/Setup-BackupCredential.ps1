<#
.SYNOPSIS
    One-time setup: stores the SQL Server login used for automated backups as an
    encrypted credential file, so no password is ever written to disk in plaintext
    or embedded in a script.

.DESCRIPTION
    Run this ONCE (interactively) on the machine/user account that will run the
    scheduled backup task. It prompts for the SQL login and password, then saves
    them via Export-Clixml, which encrypts the SecureString using Windows DPAPI.

    IMPORTANT: DPAPI encryption is tied to the Windows user account (and machine)
    that created the file. The scheduled task in Install-BackupTask.ps1 MUST run
    as this same Windows user, or it will not be able to decrypt the file.

.EXAMPLE
    .\Setup-BackupCredential.ps1
#>

[CmdletBinding()]
param(
    [string]$CredentialPath = ""
)

# $PSScriptRoot is unreliable under some invocation contexts; fall back to the
# invoked script's own path to locate sibling files.
$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot }
             elseif ($MyInvocation.MyCommand.Path) { Split-Path -Parent $MyInvocation.MyCommand.Path }
             else { (Get-Location).Path }

if (-not $CredentialPath) { $CredentialPath = Join-Path $ScriptDir "sql-backup.cred.xml" }

Write-Host "Setting up encrypted SQL Server credential for automated backups." -ForegroundColor Cyan
Write-Host "This will be saved to: $CredentialPath" -ForegroundColor Cyan
Write-Host "It can only be decrypted by the current Windows user ($env:USERDOMAIN\$env:USERNAME) on this machine." -ForegroundColor Yellow
Write-Host ""

$cred = Get-Credential -Message "Enter the SQL Server login used for backups (e.g. remote_user)"

if (-not $cred) {
    Write-Error "No credential entered. Aborting."
    exit 1
}

$cred | Export-Clixml -Path $CredentialPath

Write-Host ""
Write-Host "Saved. File is encrypted and unreadable outside this Windows account/machine." -ForegroundColor Green
Write-Host "Next: run Install-BackupTask.ps1 (as Administrator) to register the scheduled task." -ForegroundColor Cyan
