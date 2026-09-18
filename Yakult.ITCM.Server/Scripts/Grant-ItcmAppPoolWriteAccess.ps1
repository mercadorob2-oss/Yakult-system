<#
.SYNOPSIS
    Grants the Yakult.ITCM.Scheduler app pool identity write access to its own site folder,
    so the Config page can write appsettings.local.json at runtime.

.DESCRIPTION
    IIS app pools run under "IIS AppPool\<PoolName>" virtual identities by default.
    ASP.NET Core apps that write files at runtime (like this one's Config Save
    handler) need Modify rights on their own folder — which is NOT granted by
    default when a site is deployed via robocopy/xcopy.

    This script adds Modify + Read&Execute rights for the app pool identity
    directly (no recycle needed for permissions to apply — NTFS ACLs take effect
    immediately for new file handles).

.NOTES
    Run this ON the IIS server (192.168.100.186), elevated (Run as Administrator).
    Safe to re-run — it only grants (does not remove) permissions, and skips
    re-adding a rule that already exists.
#>

param(
    [string]$SiteName = "Yakult.ITCM.Scheduler",
    [string]$SitePath = "C:\inetpub\wwwroot\Yakult.ITCM.Scheduler"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================"
Write-Host "Grant IIS App Pool Write Access"
Write-Host "========================================"
Write-Host "  App Pool : $SiteName"
Write-Host "  Site path: $SitePath"
Write-Host ""

if (-not (Test-Path $SitePath)) {
    throw "Site path not found: $SitePath"
}

$identity = "IIS AppPool\$SiteName"
Write-Host ">> Checking current ACL on $SitePath ..."

$acl = Get-Acl $SitePath
$existing = $acl.Access | Where-Object {
    $_.IdentityReference.Value -eq $identity -and
    $_.FileSystemRights -match "Modify|FullControl"
}

if ($existing) {
    Write-Host "  [OK] '$identity' already has Modify/FullControl on this folder." -ForegroundColor Green
} else {
    Write-Host ">> Granting Modify rights to '$identity' ..."
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $identity,
        "Modify",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow"
    )
    $acl.AddAccessRule($rule)
    Set-Acl -Path $SitePath -AclObject $acl
    Write-Host "  [OK] Modify rights granted to '$identity' on $SitePath (and subfolders)." -ForegroundColor Green
}

Write-Host ""
Write-Host ">> Verifying write access with a test file ..."
$testFile = Join-Path $SitePath "_write_test_$(Get-Random).tmp"
try {
    "test" | Out-File -FilePath $testFile -Force
    Remove-Item $testFile -Force
    Write-Host "  [OK] Write test succeeded." -ForegroundColor Green
} catch {
    Write-Host "  [FAIL] Write test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  You may need to run this script as Administrator, or check for" -ForegroundColor Yellow
    Write-Host "  additional deny rules / disk-level permissions." -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "========================================"
Write-Host "Done. No app pool recycle required — try Save again on the Config page."
Write-Host "========================================"
