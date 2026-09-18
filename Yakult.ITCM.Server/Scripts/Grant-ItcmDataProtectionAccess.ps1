<#
.SYNOPSIS
    Grants the Yakult.ITCM.Scheduler IIS app-pool identity access to persistent Data Protection keys.

.DESCRIPTION
    Run this script on the IIS server as Administrator before the authenticated
    ITCM Server is started. The key directory is outside the publish folder so
    deployments do not invalidate all ITCM administrator sessions.
#>

[CmdletBinding()]
param(
    [string]$AppPoolName = "Yakult.ITCM.Scheduler",
    [string]$KeyPath = "C:\ProgramData\Yakult\ITCM\DataProtection-Keys"
)

$ErrorActionPreference = "Stop"
$identity = "IIS AppPool\$AppPoolName"

Write-Host "Preparing ITCM Data Protection key directory..."
New-Item -ItemType Directory -Path $KeyPath -Force | Out-Null

$acl = Get-Acl $KeyPath
$existing = $acl.Access | Where-Object {
    $_.IdentityReference.Value -eq $identity -and
    $_.FileSystemRights -match "Modify|FullControl" -and
    $_.AccessControlType -eq "Allow"
}

if (-not $existing) {
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $identity,
        "Modify",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow"
    )
    $acl.AddAccessRule($rule)
    Set-Acl -Path $KeyPath -AclObject $acl
    Write-Host "Granted Modify access to $identity."
} else {
    Write-Host "$identity already has Modify access."
}

Write-Host "Data Protection key path ready: $KeyPath"
