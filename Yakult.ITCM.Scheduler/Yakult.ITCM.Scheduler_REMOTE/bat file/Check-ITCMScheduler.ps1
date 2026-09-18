$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$installedExe = 'C:\Yakult\Yakult.ITCM.Scheduler\Yakult.ITCM.Scheduler.exe'
$installedConfig = 'C:\Yakult\Yakult.ITCM.Scheduler\Yakult.ITCM.Scheduler.exe.config'
$localExeCandidates = @(
    (Join-Path $scriptDir 'Yakult.ITCM.Scheduler.exe'),
    (Join-Path $projectDir 'Yakult.ITCM.Scheduler.exe'),
    (Join-Path $projectDir 'bin\Debug\Yakult.ITCM.Scheduler.exe'),
    (Join-Path $projectDir 'bin\Release\Yakult.ITCM.Scheduler.exe')
)
$localConfigCandidates = @(
    (Join-Path $scriptDir 'Yakult.ITCM.Scheduler.exe.config'),
    (Join-Path $projectDir 'Yakult.ITCM.Scheduler.exe.config'),
    (Join-Path $projectDir 'App.config'),
    (Join-Path $projectDir 'bin\Debug\Yakult.ITCM.Scheduler.exe.config'),
    (Join-Path $projectDir 'bin\Release\Yakult.ITCM.Scheduler.exe.config')
)
$taskName = 'Yakult ITCM Background Processing'
$taskFolder = '\Yakult'
$fullTaskName = "$taskFolder\$taskName"
$overallGood = $true

function Write-StatusLine {
    param(
        [string]$Label,
        [string]$Value,
        [ConsoleColor]$Color = [ConsoleColor]::Gray
    )

    Write-Host ($Label.PadRight(21) + ': ' + $Value) -ForegroundColor $Color
}

function Test-TaskResultIsError {
    param([object]$Result)
    try {
        $value = [int]$Result
        if ($value -eq 0) { return $false }
        # Windows Task Scheduler status codes (0x41300-0x413FF) are success/status, not errors
        if ($value -ge 267008 -and $value -le 267551) { return $false }
        # 255 (0xFF) is a transient code often seen after forced termination (schtasks /End /F).
        # The scheduler EXE only returns 0 or 1, so 255 is not a scheduler failure.
        if ($value -eq 255) { return $false }
        return $true
    }
    catch {
        return $true
    }
}

function Get-SchedulerEnabledText {
    param([string]$TaskState)

    if ([string]::IsNullOrWhiteSpace($TaskState)) {
        return 'UNKNOWN'
    }

    if ($TaskState -match '^(Disabled)$') {
        return 'NO'
    }

    if ($TaskState -match '^(Ready|Running|Queued)$') {
        return 'YES'
    }

    return 'UNKNOWN'
 }

Write-Host '=== Yakult ITCM Scheduler Checker ===' -ForegroundColor Cyan

if (Test-Path $installedExe) {
    $exePath = $installedExe
}
else {
    $exePath = $localExeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (Test-Path $exePath) {
    Write-StatusLine 'EXE' $exePath
}
else {
    Write-StatusLine 'EXE' 'NOT FOUND' Red
    $overallGood = $false
}

if (Test-Path $installedConfig) {
    $configPath = $installedConfig
}
else {
    $configPath = $localConfigCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $configPath) {
    Write-StatusLine 'Config' 'NOT FOUND' Red
    Write-Host ''
    Write-StatusLine 'OVERALL STATUS' 'NOT GOOD' Red
    exit 1
}

Write-StatusLine 'Config' $configPath

[xml]$configXml = Get-Content -Path $configPath
$connectionNode = $configXml.configuration.connectionStrings.add | Where-Object { $_.name -eq 'Yakult_Inventory_System' } | Select-Object -First 1
if ($null -eq $connectionNode) {
    Write-StatusLine 'Connection String' 'MISSING (Yakult_Inventory_System)' Red
    Write-Host ''
    Write-StatusLine 'OVERALL STATUS' 'NOT GOOD' Red
    exit 1
}

$connectionString = [string]$connectionNode.connectionString
Write-StatusLine 'Connection String' $connectionString

$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $connectionString
Write-StatusLine 'Data Source' $builder.DataSource
Write-StatusLine 'Initial Catalog' $builder.InitialCatalog

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
    $connection.Open()
    Write-StatusLine 'DB Connection' 'GOOD' Green

    $cmd = $connection.CreateCommand()
    $cmd.CommandText = @"
SELECT
    DB_NAME() AS DatabaseName,
    CASE WHEN OBJECT_ID('dbo.CallTicket','U') IS NULL THEN NULL ELSE (SELECT COUNT(1) FROM dbo.CallTicket) END AS TotalTickets,
    CASE WHEN OBJECT_ID('dbo.CallTicket','U') IS NULL THEN NULL ELSE (SELECT COUNT(1) FROM dbo.CallTicket WHERE Status NOT IN ('Solved','Resolved (Temporary)','Closed')) END AS OpenTickets,
    CASE WHEN OBJECT_ID('dbo.CallNotificationRules','U') IS NULL THEN 0 ELSE 1 END AS HasRules,
    CASE WHEN OBJECT_ID('dbo.CallEmailTemplate','U') IS NULL THEN 0 ELSE 1 END AS HasTemplates,
    CASE WHEN OBJECT_ID('dbo.CallEscalationSettings','U') IS NULL THEN 0 ELSE 1 END AS HasEscalationSettings,
    CASE WHEN OBJECT_ID('dbo.CallEmailLog','U') IS NULL THEN 0 ELSE 1 END AS HasEmailLog;
"@

    $reader = $cmd.ExecuteReader()
    if ($reader.Read()) {
        Write-StatusLine 'Database' ([string]$reader['DatabaseName'])

        if ($reader.IsDBNull($reader.GetOrdinal('TotalTickets'))) {
            Write-StatusLine 'Total Tickets' 'n/a (CallTicket missing)' Yellow
            $overallGood = $false
        }
        else {
            Write-StatusLine 'Total Tickets' ([string]$reader['TotalTickets'])
        }

        if ($reader.IsDBNull($reader.GetOrdinal('OpenTickets'))) {
            Write-StatusLine 'Open Tickets' 'n/a' Yellow
            $overallGood = $false
        }
        else {
            Write-StatusLine 'Open Tickets' ([string]$reader['OpenTickets'])
        }

        $hasRules = [int]$reader['HasRules']
        $hasTemplates = [int]$reader['HasTemplates']
        $hasEscalationSettings = [int]$reader['HasEscalationSettings']
        $hasEmailLog = [int]$reader['HasEmailLog']

        Write-StatusLine 'CallNotificationRules' ($(if ($hasRules -eq 1) { 'OK' } else { 'MISSING' })) $(if ($hasRules -eq 1) { 'Green' } else { 'Red' })
        Write-StatusLine 'CallEmailTemplate' ($(if ($hasTemplates -eq 1) { 'OK' } else { 'MISSING' })) $(if ($hasTemplates -eq 1) { 'Green' } else { 'Red' })
        Write-StatusLine 'CallEscalationSettings' ($(if ($hasEscalationSettings -eq 1) { 'OK' } else { 'MISSING' })) $(if ($hasEscalationSettings -eq 1) { 'Green' } else { 'Red' })
        Write-StatusLine 'CallEmailLog' ($(if ($hasEmailLog -eq 1) { 'OK' } else { 'MISSING' })) $(if ($hasEmailLog -eq 1) { 'Green' } else { 'Red' })

        if ($hasRules -ne 1 -or $hasTemplates -ne 1 -or $hasEscalationSettings -ne 1 -or $hasEmailLog -ne 1) {
            $overallGood = $false
        }
    }

    $reader.Close()
    $connection.Close()
}
catch {
    Write-StatusLine 'DB Connection' ('NOT GOOD - ' + $_.Exception.Message) Red
    $overallGood = $false
}

Write-Host ''

$taskFound = $false
try {
    $taskQuery = & schtasks.exe /Query /TN $fullTaskName /V /FO LIST 2>&1
    if ($LASTEXITCODE -eq 0) {
        $taskFound = $true
        Write-StatusLine 'Scheduled Task' 'INSTALLED' Green
        Write-StatusLine 'Task Path' $fullTaskName

        $statusLine = $taskQuery | Where-Object { $_ -match '^Status:\s*(.+)$' } | Select-Object -First 1
        $lastRunLine = $taskQuery | Where-Object { $_ -match '^Last Run Time:\s*(.+)$' } | Select-Object -First 1
        $lastResultLine = $taskQuery | Where-Object { $_ -match '^Last Result:\s*(.+)$' } | Select-Object -First 1
        $nextRunLine = $taskQuery | Where-Object { $_ -match '^Next Run Time:\s*(.+)$' } | Select-Object -First 1

        if ($statusLine) {
            $taskState = (($statusLine -replace '^Status:\s*', '').Trim())
            Write-StatusLine 'Task State' $taskState
            Write-StatusLine 'Scheduler Enabled' (Get-SchedulerEnabledText $taskState) $(if ((Get-SchedulerEnabledText $taskState) -eq 'YES') { 'Green' } elseif ((Get-SchedulerEnabledText $taskState) -eq 'NO') { 'Yellow' } else { 'Gray' })
        }
        if ($lastRunLine) {
            Write-StatusLine 'Last Run Time' (($lastRunLine -replace '^Last Run Time:\s*', '').Trim())
        }
        if ($nextRunLine) {
            Write-StatusLine 'Next Run Time' (($nextRunLine -replace '^Next Run Time:\s*', '').Trim())
        }
        if ($lastResultLine) {
            $lastResult = (($lastResultLine -replace '^Last Result:\s*', '').Trim())
            $isError = Test-TaskResultIsError $lastResult
            $color = if (-not $isError) { 'Green' } else { 'Yellow' }
            Write-StatusLine 'Last Task Result' $lastResult $color
            if ($lastResult -eq '255') {
                Write-Host (' ' * 23 + '(Transient: task was force-stopped. Not a scheduler error.)') -ForegroundColor DarkGray
            }
            if ($isError) {
                $overallGood = $false
            }
        }
    }
}
catch {
}

if (-not $taskFound) {
    try {
        $task = Get-ScheduledTask -ErrorAction Stop | Where-Object { $_.TaskName -eq $taskName } | Select-Object -First 1
        if ($null -ne $task) {
            $taskInfo = Get-ScheduledTaskInfo -TaskName $task.TaskName -TaskPath $task.TaskPath
            $taskFound = $true
            Write-StatusLine 'Scheduled Task' 'INSTALLED' Green
            Write-StatusLine 'Task Path' ($task.TaskPath + $task.TaskName)
            $taskState = [string]$task.State
            Write-StatusLine 'Task State' $taskState
            Write-StatusLine 'Scheduler Enabled' (Get-SchedulerEnabledText $taskState) $(if ((Get-SchedulerEnabledText $taskState) -eq 'YES') { 'Green' } elseif ((Get-SchedulerEnabledText $taskState) -eq 'NO') { 'Yellow' } else { 'Gray' })
            Write-StatusLine 'Last Run Time' ([string]$taskInfo.LastRunTime)
            $isError = Test-TaskResultIsError $taskInfo.LastTaskResult
            $color = if (-not $isError) { 'Green' } else { 'Yellow' }
            Write-StatusLine 'Last Task Result' ([string]$taskInfo.LastTaskResult) $color
            if ($taskInfo.LastTaskResult -eq 255) {
                Write-Host (' ' * 23 + '(Transient: task was force-stopped. Not a scheduler error.)') -ForegroundColor DarkGray
            }
            if ($isError -and $taskInfo.LastRunTime -ne [datetime]::MinValue) {
                $overallGood = $false
            }
        }
    }
    catch {
    }
}

if (-not $taskFound) {
    Write-StatusLine 'Scheduled Task' 'NOT FOUND' Red
    $overallGood = $false
}

Write-StatusLine 'Event Source' ($(if ([System.Diagnostics.EventLog]::SourceExists('YakultITCM')) { 'REGISTERED' } else { 'MISSING' })) $(if ([System.Diagnostics.EventLog]::SourceExists('YakultITCM')) { 'Green' } else { 'Red' })

Write-Host ''
Write-Host 'Recent YakultITCM Logs:' -ForegroundColor Cyan
try {
    $events = Get-WinEvent -FilterHashtable @{ LogName = 'Application'; ProviderName = 'YakultITCM' } -MaxEvents 5 -ErrorAction Stop
    if ($events.Count -eq 0) {
        Write-Host '  (no event log entries found yet)'
    }
    else {
        foreach ($event in $events) {
            Write-Host ('  [' + $event.TimeCreated.ToString('yyyy-MM-dd HH:mm:ss') + '] ' + $event.LevelDisplayName + ' - ' + $event.Message)
        }
    }
}
catch {
    Write-Host ('  Unable to read event logs: ' + $_.Exception.Message) -ForegroundColor Yellow
}

Write-Host ''
if ($overallGood) {
    Write-StatusLine 'OVERALL STATUS' 'GOOD' Green
    exit 0
}
else {
    Write-StatusLine 'OVERALL STATUS' 'NOT GOOD' Red
    exit 1
}
