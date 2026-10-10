param([string]$AppPath)

# Run with Windows PowerShell 5.1 in STA mode after building the desktop app.
# Uses a disposable LocalDB database; no configured application database is read.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $AppPath) { $AppPath = Join-Path $repoRoot 'Yakult.Inventory.App/bin/BacklogPreviousRenewals/Yakult.Inventory.App.exe' }
$AppPath = (Resolve-Path -LiteralPath $AppPath).Path
$assemblyFolder = Split-Path $AppPath -Parent
Add-Type -AssemblyName System.Data, PresentationFramework, PresentationCore, WindowsBase
[Reflection.Assembly]::LoadFrom((Join-Path $assemblyFolder 'Dapper.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $assemblyFolder 'Newtonsoft.Json.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom($AppPath) | Out-Null

$token = [Guid]::NewGuid().ToString('N')
$databaseName = 'YakultLicenseHistoryTest_' + $token
$testRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot '_build/license-history-tests'))
$testDirectory = Join-Path $testRoot $token
New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null
$master = New-Object System.Data.SqlClient.SqlConnection('Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True;Connect Timeout=60')
$databaseCreated = $false
$testConnection = $null
$script:checks = 0

function Invoke-Sql([string]$sql) {
    $command = $testConnection.CreateCommand()
    $command.CommandText = $sql
    try { $command.ExecuteNonQuery() | Out-Null } finally { $command.Dispose() }
}

function Read-Scalar([string]$sql) {
    $command = $testConnection.CreateCommand()
    $command.CommandText = $sql
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw "FAIL: $message" }
    $script:checks++
    Write-Output "PASS: $message"
}

function Assert-Rejected([scriptblock]$action, [string]$messageFragment, [string]$label) {
    $failure = $null
    try { & $action | Out-Null } catch { $failure = $_.Exception.GetBaseException() }
    Assert-True ($null -ne $failure -and $failure.Message.Contains($messageFragment)) $label
}

function New-Period([string]$start, [string]$end, [string]$secondItem = 'Removed License B') {
    $period = New-Object Yakult.Inventory.App.Models.LicenseHistoricalPeriodDto
    $period.StartDate = [DateTime]::ParseExact($start, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
    $period.EndDate = [DateTime]::ParseExact($end, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
    $period.ReferenceNumber = 'OLD-' + $start
    $period.Notes = 'Entered from older paperwork.'
    $line = New-Object Yakult.Inventory.App.Models.LicenseHistoricalPeriodItemDto
    $line.ItemId = 1
    $line.ItemName = 'License A at purchase'
    $line.ItemCode = 'A-OLD'
    $line.Quantity = 2
    $period.Items.Add($line)
    $removed = New-Object Yakult.Inventory.App.Models.LicenseHistoricalPeriodItemDto
    $removed.ItemName = $secondItem
    $removed.ItemCode = 'REMOVED'
    $removed.Quantity = 1
    $period.Items.Add($removed)
    return $period
}

try {
    $master.Open()
    $create = $master.CreateCommand()
    $dataPath = (Join-Path $testDirectory 'history.mdf').Replace("'", "''")
    $logPath = (Join-Path $testDirectory 'history_log.ldf').Replace("'", "''")
    $create.CommandText = "CREATE DATABASE [$databaseName] ON PRIMARY (NAME=N'history', FILENAME=N'$dataPath') LOG ON (NAME=N'history_log', FILENAME=N'$logPath');"
    try { $create.ExecuteNonQuery() | Out-Null; $databaseCreated = $true } finally { $create.Dispose() }
    $connectionString = "Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=$databaseName;Integrated Security=True;Connect Timeout=30"
    $testConnection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $testConnection.Open()
    [Yakult.Inventory.App.Core.DatabaseConfig]::Bootstrap($connectionString)
    $repository = New-Object Yakult.Inventory.App.Repositories.LicenseHistoricalPeriodRepository
    Assert-Rejected { $repository.GetContext(1) } 'not available yet' 'Missing migration gives an actionable error'

    Invoke-Sql @'
CREATE TABLE dbo.[User] (UserId INT PRIMARY KEY, Name NVARCHAR(255));
CREATE TABLE dbo.[Set] (SetId INT PRIMARY KEY, RenewalOfSetId INT NULL, StartDate DATETIME2 NULL, EndDate DATETIME2 NULL, TotalAmountDue DECIMAL(18,2));
CREATE TABLE dbo.Item (ItemId INT PRIMARY KEY, Name NVARCHAR(4000), Active BIT, StockOnHand INT);
CREATE TABLE dbo.SetItem (SetItemId INT PRIMARY KEY, SetId INT, ItemId INT, ItemCode NVARCHAR(800), Description NVARCHAR(4000), Quantity DECIMAL(18,2), LineStartDate DATETIME2 NULL, LineEndDate DATETIME2 NULL, RenewalStatus NVARCHAR(20), RenewalReferenceId INT NULL);
CREATE TABLE dbo.Renewals (RenewalId INT PRIMARY KEY, ItemId INT, RenewalCount INT);
CREATE TABLE dbo.AuditTrail (Id INT IDENTITY PRIMARY KEY, Action NVARCHAR(100), EntityId INT, EntityType NVARCHAR(100), UserId INT, UserName NVARCHAR(255), Timestamp DATETIME2, Notes NVARCHAR(MAX), NewValues NVARCHAR(MAX));
INSERT dbo.[User] VALUES (1, 'History Tester');
INSERT dbo.Item VALUES (1, 'Current License A', 1, 99), (2, 'Current License C', 1, 50), (3, 'Current License D', 1, 20);
INSERT dbo.[Set] VALUES (1, NULL, '2026-01-01', '2026-12-31', 1000), (2, 1, '2027-01-01', '2027-12-31', 1500), (3, NULL, '2026-01-01', '2026-12-31', 2000), (4, NULL, NULL, NULL, 0), (5, NULL, '2025-01-01', '2025-11-30', 500);
INSERT dbo.SetItem VALUES (101, 1, 1, 'A', 'License A', 2, '2025-12-01', '2026-12-31', 'Renewed', 102), (102, 2, 1, 'A', 'License A', 3, '2027-01-01', '2027-12-31', NULL, NULL), (103, 2, 3, 'D', 'License D', 1, '2027-01-01', '2027-12-31', NULL, NULL), (104, 3, 1, 'A', 'Unrelated A', 99, '2026-01-01', '2026-12-31', NULL, NULL), (105, 4, 2, 'C', 'No dates', 1, NULL, NULL, NULL, NULL);
INSERT dbo.Renewals VALUES (1, 1, 1), (2, 1, 2);
'@
    $migration = Get-Content -LiteralPath (Join-Path $repoRoot 'DATABASES/Yakult-DB-Production/dbo/Scripts/Migration_LicenseHistoricalPeriod_CreateTables.sql') -Raw
    Invoke-Sql $migration
    Invoke-Sql $migration
    Assert-True ((Read-Scalar 'SELECT COUNT(*) FROM dbo.LicenseHistoricalPeriod') -eq 0) 'Migration can run twice'
    $before = Read-Scalar "SELECT (SELECT * FROM dbo.[Set] ORDER BY SetId FOR JSON PATH) + (SELECT * FROM dbo.SetItem ORDER BY SetItemId FOR JSON PATH) + (SELECT * FROM dbo.Item ORDER BY ItemId FOR JSON PATH) + (SELECT * FROM dbo.Renewals ORDER BY RenewalId FOR JSON PATH)"

    $context = $repository.GetContext(2)
    Assert-True ($context.RootSetId -eq 1 -and $context.SetIds.Count -eq 2) 'Chain resolves from a later invoice'
    Assert-True ($context.FirstRecordedStartDate -eq [DateTime]'2025-12-01') 'Earliest line coverage is included in the date boundary'
    Assert-True ($context.CurrentItems[0].Quantity -eq 3) 'Copied quantities use invoice lines instead of stock'
    $first = New-Period '2024-01-01' '2024-12-31'
    $firstId = $repository.Create(2, $first, 1)
    $second = New-Period '2025-01-01' '2025-11-30' 'License C'
    $second.Amount = 125.50
    $repository.Create(1, $second, 1) | Out-Null
    $history = $repository.GetContext(2).Periods
    Assert-True ($history.Count -eq 2 -and $history[0].StartDate -eq [DateTime]'2025-01-01') 'Periods are visible from the chain and sorted by coverage'
    Assert-True ($history[1].Items[1].ItemName -eq 'Removed License B' -and $null -eq $history[1].Items[1].ItemId) 'Removed items persist as independent snapshots'
    Assert-True ($null -eq $history[1].Amount -and $history[0].Amount -eq 125.50) 'Unknown and known period amounts are distinct'
    Assert-True ($repository.GetContext(3).Periods.Count -eq 0) 'Shared catalog items do not leak history into unrelated invoices'
    Assert-Rejected { $repository.Create(1, $first, 1) } 'already been recorded' 'Duplicate period is rejected across the chain'
    Assert-Rejected { $repository.Create(1, (New-Period '2025-11-30' '2025-12-01'), 1) } 'must end before' 'Historical period cannot reach recorded coverage'
    Assert-Rejected { $repository.Create(4, $first, 1) } 'coverage start date' 'Missing recorded coverage date is rejected'
    $overlap = New-Period '2024-06-01' '2025-06-30'
    Assert-Rejected { $repository.Create(1, $overlap, 1) } 'overlaps' 'Overlapping periods require confirmation'
    $repository.Create(1, $overlap, 1, $true) | Out-Null
    Assert-True ($repository.GetContext(1).Periods.Count -eq 3) 'Confirmed historical overlaps can be saved'

    $bad = New-Period '2023-01-01' '2023-12-31'
    $bad.Items[0].ItemId = 99999
    $countsBefore = Read-Scalar 'SELECT CONCAT((SELECT COUNT(*) FROM dbo.LicenseHistoricalPeriod), '','', (SELECT COUNT(*) FROM dbo.LicenseHistoricalPeriodItem), '','', (SELECT COUNT(*) FROM dbo.AuditTrail))'
    Assert-Rejected { $repository.Create(1, $bad, 1) } 'FOREIGN KEY' 'Invalid catalog link fails the save'
    $countsAfter = Read-Scalar 'SELECT CONCAT((SELECT COUNT(*) FROM dbo.LicenseHistoricalPeriod), '','', (SELECT COUNT(*) FROM dbo.LicenseHistoricalPeriodItem), '','', (SELECT COUNT(*) FROM dbo.AuditTrail))'
    Assert-True ($countsBefore -eq $countsAfter) 'Failed save rolls back header, lines, and audit'
    $invalid = New-Period '2023-01-01' '2023-12-31'
    $invalid.Items[0].Quantity = 0
    Assert-Rejected { $repository.Create(1, $invalid, 1) } 'quantity must be positive' 'Zero quantity is rejected'
    $invalid.Items[0].Quantity = 1.001
    Assert-Rejected { $repository.Create(1, $invalid, 1) } 'two decimal places' 'Quantity precision cannot be silently truncated'
    $invalid.Items[0].Quantity = 1
    $invalid.Amount = -1
    Assert-Rejected { $repository.Create(1, $invalid, 1) } 'nonnegative amount' 'Negative amount is rejected'
    $invalid.Amount = 0
    $invalid.EndDate = [DateTime]'2022-01-01'
    Assert-Rejected { $repository.Create(1, $invalid, 1) } 'on or after' 'Reversed coverage dates are rejected'
    $after = Read-Scalar "SELECT (SELECT * FROM dbo.[Set] ORDER BY SetId FOR JSON PATH) + (SELECT * FROM dbo.SetItem ORDER BY SetItemId FOR JSON PATH) + (SELECT * FROM dbo.Item ORDER BY ItemId FOR JSON PATH) + (SELECT * FROM dbo.Renewals ORDER BY RenewalId FOR JSON PATH)"
    Assert-True ($before -eq $after) 'Current dates, totals, line links, stock, and renewal counts remain unchanged'

    $oldAnchor = New-Period '2023-01-01' '2023-12-31'
    $repository.Create(5, $oldAnchor, 1) | Out-Null
    Invoke-Sql 'UPDATE dbo.[Set] SET RenewalOfSetId = 1 WHERE SetId = 5'
    Assert-True ($repository.GetContext(2).Periods.Count -eq 4) 'History remains visible after separately anchored chains are linked'
    Invoke-Sql "UPDATE dbo.Item SET Name = 'Renamed current item' WHERE ItemId = 1"
    Assert-True ($repository.GetContext(1).Periods[0].Items[0].ItemName -eq 'License A at purchase') 'Catalog renames preserve historical item names'
    Assert-True ((Read-Scalar "SELECT COUNT(*) FROM dbo.AuditTrail WHERE EntityType = 'LicenseHistoricalPeriod'") -eq 4) 'Every committed period has an audit entry'

    if ([System.Windows.Application]::Current -eq $null) { $application = New-Object System.Windows.Application }
    [System.Windows.Application]::Current.ShutdownMode = [System.Windows.ShutdownMode]::OnExplicitShutdown
    $dialog = New-Object Yakult.Inventory.App.WPF.Renewal.History.AddLicenseHistoricalPeriodWindow -ArgumentList 2, ($repository.GetContext(2))
    Assert-True ($null -ne $dialog.FindName('ItemsGrid')) 'Backlog dialog resources and item grid load'
    $detail = New-Object Yakult.Inventory.App.WPF.Renewal.RenewalDetail.Views.RenewalDetailWindow -ArgumentList 2
    $workspace = New-Object Yakult.Inventory.App.WPF.Renewal.RenewalWorkspace.Views.RenewalWorkspaceWindow -ArgumentList 2
    Assert-True ($null -ne $detail.FindName('BacklogHistory') -and $null -ne $workspace.FindName('BacklogHistory')) 'Both renewal detail windows load the shared history control'
    $dialog.Close(); $detail.Close(); $workspace.Close()
    Write-Output "SUCCESS: $script:checks checks passed."
}
finally {
    if ($testConnection) { $testConnection.Dispose() }
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    if ($databaseCreated -and $master.State -eq [System.Data.ConnectionState]::Open) {
        $drop = $master.CreateCommand()
        $drop.CommandText = "ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$databaseName];"
        try { $drop.ExecuteNonQuery() | Out-Null } finally { $drop.Dispose() }
    }
    $master.Dispose()
    $resolvedTestDirectory = [IO.Path]::GetFullPath($testDirectory)
    if ($resolvedTestDirectory.StartsWith($testRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedTestDirectory -Recurse -Force
    }
}
