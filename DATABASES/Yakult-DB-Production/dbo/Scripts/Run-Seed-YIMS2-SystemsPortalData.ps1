<#
.SYNOPSIS
    Copies the data Yakult.SystemsPortal needs from Yakult_Inventory_System_DEV
    into YIMS2 (same SQL Server instance).

.DESCRIPTION
    Thin wrapper around Seed_YIMS2_SystemsPortalData_FromDEV.sql. Does a
    pre-flight (connectivity + current YIMS2 row counts for the target tables),
    then runs the seed script. The seed script itself is transactional and
    aborts if any target table already has rows.

.EXAMPLE
    .\Run-Seed-YIMS2-SystemsPortalData.ps1 -Password 'Yakult-ITD'

.EXAMPLE
    .\Run-Seed-YIMS2-SystemsPortalData.ps1 -Password 'Yakult-ITD' -PreflightOnly
#>
[CmdletBinding()]
param(
    [string] $ServerInstance = '192.168.100.186,50301',
    [string] $Database       = 'YIMS2',
    [string] $SourceDatabase = 'Yakult_Inventory_System_DEV',
    [string] $User           = 'remote_user',
    [Parameter(Mandatory)] [string] $Password,
    [string] $SqlFile        = (Join-Path $PSScriptRoot 'Seed_YIMS2_SystemsPortalData_FromDEV.sql'),
    [switch] $PreflightOnly
)

$ErrorActionPreference = 'Stop'

# --- locate sqlcmd.exe -------------------------------------------------------
$sqlcmd = (Get-Command sqlcmd.exe -ErrorAction SilentlyContinue).Source
if (-not $sqlcmd) {
    $candidate = Get-ChildItem 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\*\Tools\Binn\sqlcmd.exe' -ErrorAction SilentlyContinue |
                 Select-Object -Last 1
    if ($candidate) { $sqlcmd = $candidate.FullName }
}
if (-not $sqlcmd) { throw 'sqlcmd.exe not found. Install the SQL Server command-line tools or add sqlcmd to PATH.' }

if (-not (Test-Path $SqlFile)) { throw "Seed script not found: $SqlFile" }

$env:SQLCMDPASSWORD = $Password
# -I : QUOTED_IDENTIFIER ON (the seed script needs it for its XML .value() calls)
$common = @('-S', $ServerInstance, '-U', $User, '-C', '-b', '-I')

Write-Host "sqlcmd     : $sqlcmd"
Write-Host "server     : $ServerInstance"
Write-Host "target DB  : $Database"
Write-Host "source DB  : $SourceDatabase"
Write-Host "seed script: $SqlFile`n"

# --- pre-flight ------------------------------------------------------------
Write-Host '--- pre-flight: current YIMS2 row counts for the target tables ---'
$preflightSql = @"
SET NOCOUNT ON;
SELECT t.name AS TableName, SUM(p.rows) AS YIMS2_Rows
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
WHERE t.name IN (
 'AccountLevel','Title','Role','User','EmailAddress','Company','Department','Branch',
 'Employee','UserRole','DepartmentEmail','DepartmentAccount','EmployeeEmail','AuditTrail',
 'EmployeeResourceCategory','PortalContentCategory','PortalContent','PortalContentLink',
 'PortalCard','PortalSetting')
GROUP BY t.name ORDER BY t.name;
"@
& $sqlcmd @common '-d' $Database '-Q' $preflightSql
if ($LASTEXITCODE -ne 0) { throw "Pre-flight query failed (exit $LASTEXITCODE)." }

if ($PreflightOnly) { Write-Host "`n-PreflightOnly set - stopping before the seed run."; return }

# --- confirm -------------------------------------------------------------
Write-Host ''
$answer = Read-Host "Proceed to seed $Database from $SourceDatabase? Type YES to continue"
if ($answer -ne 'YES') { Write-Host 'Cancelled.'; return }

# --- run the seed ------------------------------------------------------------
Write-Host "`n--- running seed script ---"
& $sqlcmd @common '-d' $Database '-i' $SqlFile
$exit = $LASTEXITCODE
Remove-Item Env:\SQLCMDPASSWORD -ErrorAction SilentlyContinue

if ($exit -ne 0) { throw "Seed script failed (exit $exit). The script is transactional - YIMS2 was rolled back." }
Write-Host "`nDone. Seed completed successfully."
