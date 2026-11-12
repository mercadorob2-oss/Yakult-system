#Requires -Version 5.1
[CmdletBinding()]
param([string]$UpdateFolder = '\\ypi-hqfs\my_dept$\IT\ITDSharedFolder\Russel\Yakult_Desktop_Installer\New Versions\patch_versions', [string]$PublishToolsPath, [switch]$ValidateOnly, [string]$PreviewPath, [ValidateSet('Overview','Library','Release','Activity','Groups')][string]$PreviewPage = 'Overview', [switch]$PreviewScrollEnd)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
Add-Type -AssemblyName System.Windows.Forms
trap {
    if ($ValidateOnly -or $PreviewPath) { Write-Output ('Release Desk failed: ' + $_.Exception.ToString()); exit 1 }
    [void][Windows.MessageBox]::Show(('Release Desk could not start or complete an action.' + "`n`n" + $_.Exception.Message), 'Yakult Release Desk', 'OK', 'Error')
    exit 1
}
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Start Release Desk with Windows PowerShell -STA.' }
$script:installerFolder = $PSScriptRoot
$script:repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$script:releaseFolder = Join-Path $script:repoRoot 'artifacts\desktop-releases'
$script:stateFolder = Join-Path $env:LOCALAPPDATA 'Yakult\ReleaseDesk'
$script:statePath = Join-Path $script:stateFolder 'workspace.json'
$script:state = @{ Groups = $null; GroupsLayoutRevision = ''; Favorites = @(); Recent = @(); PublishPath = (Join-Path ([Environment]::GetFolderPath('Desktop')) 'publish folder'); Width = 1440; Height = 900; DraftVersion = ''; DraftNotes = '' }
if (Test-Path -LiteralPath $script:statePath) {
    try {
        $saved = Get-Content -LiteralPath $script:statePath -Raw | ConvertFrom-Json
        foreach ($key in @($script:state.Keys)) { if ($null -ne $saved.$key) { $script:state[$key] = $saved.$key } }
    } catch { $script:stateWarning = 'Saved preferences could not be read. Defaults were loaded.' }
}
# Import a configured layout once, including when an older open window later saves
# its stale workspace. Subsequent group edits remain authoritative after import.
$layoutPath = Join-Path $script:stateFolder 'group-layout.json'
if (Test-Path -LiteralPath $layoutPath) {
    try {
        $layout = Get-Content -LiteralPath $layoutPath -Raw | ConvertFrom-Json
        if ($layout.Revision -and $script:state.GroupsLayoutRevision -ne $layout.Revision) {
            if (-not $layout.PSObject.Properties['Groups']) { throw 'Group layout is missing its groups.' }
            foreach ($group in @($layout.Groups)) {
                if (-not $group.Id -or -not $group.Name -or -not $group.PSObject.Properties['Paths']) { throw 'Group layout contains an invalid group.' }
            }
            $script:state.Groups = @($layout.Groups)
            $script:state.GroupsLayoutRevision = [string]$layout.Revision
        }
    } catch { $script:stateWarning = 'Configured groups could not be read. Existing workspace groups were retained.' }
}
$script:publishToolsPath = if ($PublishToolsPath) { [IO.Path]::GetFullPath($PublishToolsPath) } else { [string]$script:state.PublishPath }
$script:mode = 'Library'
$script:job = $null
$script:catalog = @()
$script:selected = $null
. (Join-Path $PSScriptRoot 'ReleaseDesk.Core.ps1')
. (Join-Path $PSScriptRoot 'ReleaseDesk.Services.ps1')
$script:logsFolder = Join-Path $script:stateFolder 'Jobs'
$script:inspection = $null
$reader = [Xml.XmlReader]::Create((Join-Path $PSScriptRoot 'ReleaseDesk.xaml'))
try { $script:window = [Windows.Markup.XamlReader]::Load($reader) } finally { $reader.Dispose() }
$script:ui = @{}
foreach ($name in @('Overview','Library','Release','Activity','Folders','PageTitle','PageSubtitle','Status','Navigation','SidebarStatus','NavOverview','NavLibrary','NavFavorites','NavRecent','NavRelease','NavActivity','NavFolders','Refresh','Browse','MetricTools','MetricFavorites','MetricRelease','MetricReady','Packages','PackageEmpty','Search','StateFilter','SourceFilter','ResultCount','Tools','ToolsEmpty','DetailName','DetailSummary','DetailMeta','DetailPath','Preview','LaunchTool','Favorite','Locate','CopyPath','AddToGroup','Version','Notes','Build','BuildPublish','PreviewPublish','Publish','OpenReleases','JobStatus','JobProgress','ViewOutput','CopyOutput','Output','PublishPath','InstallerPath','ChangeFolder','OpenPublish','OpenInstaller','PreferencesPath','PackageChoice','VerifyPackage','ReleaseSummary','FeedSummary','OpenLogs','JobHistory','HistoryDetails','NavGroups','GroupBar','GroupFilter','GroupName','CreateGroup','RenameGroup','DeleteGroup','TargetGroup','AddToGroup','RemoveFromGroup')) {
    $script:ui[$name] = $script:window.FindName($name)
    if (-not $script:ui[$name]) { throw "Missing WPF control: $name" }
}
function Save-Workspace {
    if ($ValidateOnly -or $PreviewPath) { return }
    try {
        [void][IO.Directory]::CreateDirectory($script:stateFolder)
        $script:state.PublishPath = $script:publishToolsPath
        $script:state.DraftVersion = $script:ui.Version.Text
        $script:state.DraftNotes = $script:ui.Notes.Text
        if ($script:window.WindowState -eq 'Normal') { $script:state.Width = $script:window.Width; $script:state.Height = $script:window.Height }
        $tempPath = Join-Path $script:stateFolder ('workspace-' + [guid]::NewGuid().ToString('N') + '.tmp')
        [IO.File]::WriteAllText($tempPath, ($script:state | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($true))
        Move-Item -LiteralPath $tempPath -Destination $script:statePath -Force
    } catch { $script:ui.Status.Text = 'Preferences could not be saved: ' + $_.Exception.Message }
}
function Add-DeskLog([string]$Text) {
    $line = ('[{0}] {1}' -f (Get-Date -Format 'HH:mm:ss'), $Text) + "`r`n"
    $script:ui.Output.AppendText($line)
    if ($script:ui.Output.Text.Length -gt 250000) { $script:ui.Output.Text = $script:ui.Output.Text.Substring($script:ui.Output.Text.Length - 200000) }
    if ($script:job -and $script:job.LogPath) {
        try { [IO.File]::AppendAllText($script:job.LogPath, $line, [Text.UTF8Encoding]::new($false)) }
        catch { $script:ui.JobStatus.Text = 'Log could not be saved: ' + $_.Exception.Message }
    }
    $script:ui.Output.ScrollToEnd()
    $script:ui.Status.Text = $Text
}
function Get-ToolSummary($Tool) {
    switch -Regex ($Tool.Name) {
        'Copy.*Backup|Server.Backup' { return 'Copy and manage server backups.' }
        'Copy.*Production|Production.Copy|Local.Copy' { return 'Copy application files between local and production folders.' }
        'Build.*Installer|Build.*Release' { return 'Build a versioned Yakult desktop installer.' }
        'Publish|Deploy' { return 'Publish or deploy Yakult application components.' }
        'Https' { return 'Configure HTTPS for the portal.' }
        'Setup.*IIS' { return 'Configure an application in Internet Information Services.' }
        'Localhost' { return 'Start a local development server.' }
        'Signing' { return 'Initialize or inspect desktop release signing.' }
        'ProtectionAccess' { return 'Manage access to server data protection keys.' }
        default { return ($Tool.Kind + ' in ' + $Tool.Root + '.') }
    }
}
function Refresh-Library {
    $query = $script:ui.Search.Text.Trim()
    $stateIndex = $script:ui.StateFilter.SelectedIndex
    $rootIndex = $script:ui.SourceFilter.SelectedIndex
    $items = @($script:catalog | Where-Object {
        (-not $query -or (($_.Name + ' ' + $_.Summary + ' ' + $_.Path + ' ' + $_.Kind).IndexOf($query, [StringComparison]::OrdinalIgnoreCase) -ge 0)) -and
        ($stateIndex -eq 0 -or ($stateIndex -eq 1 -and -not $_.IsArchived) -or ($stateIndex -eq 2 -and $_.IsArchived)) -and
        ($rootIndex -eq 0 -or ($rootIndex -eq 1 -and $_.Root -eq 'Desktop publish folder') -or ($rootIndex -eq 2 -and $_.Root -eq 'Installer folder')) -and
        ($script:mode -ne 'Favorites' -or $script:state.Favorites -contains $_.Path) -and
        ($script:mode -ne 'Recent' -or @($script:state.Recent | ForEach-Object { $_.Path }) -contains $_.Path)
    })
    if ($script:mode -eq 'Recent') {
        $items = @($items | Sort-Object { $toolPath = $_.Path; [array]::IndexOf(@($script:state.Recent | ForEach-Object { $_.Path }), $toolPath) })
    }
    if ($script:mode -eq 'Groups') {
        $groupId = [string]$script:ui.GroupFilter.SelectedValue
        $grouped = @()
        foreach ($group in @($script:state.Groups)) {
            if ($groupId -and $group.Id -ne $groupId) { continue }
            foreach ($tool in $items) {
                if ($group.Paths -contains $tool.Path) {
                    $copy = $tool.PSObject.Copy()
                    $copy | Add-Member NoteProperty GroupName $group.Name -Force
                    $copy | Add-Member NoteProperty GroupId $group.Id -Force
                    $grouped += $copy
                }
            }
        }
        $items = $grouped
    }
    $previousGroup = if ($script:selected -and $script:mode -eq 'Groups') { $script:selected.GroupId } else { '' }
    $previous = if ($script:selected) { $script:selected.Path } else { '' }
    $view = [Windows.Data.ListCollectionView]::new([System.Collections.IList]$items)
    if ($script:mode -eq 'Groups') { [void]$view.GroupDescriptions.Add([Windows.Data.PropertyGroupDescription]::new('GroupName')) }
    $script:ui.Tools.ItemsSource = $view
    $script:ui.ToolsEmpty.Visibility = if ($items.Count) { 'Collapsed' } else { 'Visible' }
    $script:ui.ResultCount.Text = "$($items.Count) shown / $($script:catalog.Count) catalog items"
    $script:ui.MetricFavorites.Text = [string]@($script:catalog | Where-Object { $script:state.Favorites -contains $_.Path }).Count
    if ($previous) { $script:ui.Tools.SelectedItem = $items | Where-Object { $_.Path -eq $previous -and (-not $previousGroup -or $_.GroupId -eq $previousGroup) } | Select-Object -First 1 }
    $script:ui.ToolsEmpty.Text = if ($script:mode -eq 'Groups') { 'No grouped tools match this view. Add tools from the library using Add to group, or clear the search and filters.' } else { 'No tools match this view. Try a different search or filter.' }
}
function Refresh-Desk {
    $script:catalog = @(Get-ToolCatalog)
    foreach ($tool in $script:catalog) {
        $tool | Add-Member NoteProperty DisplayName ($tool.Name -replace '\.(ps1|cmd|bat|lnk|iss|sql|url)$','' -replace '[-_]',' ')
        $tool | Add-Member NoteProperty Summary (Get-ToolSummary $tool)
        $tool | Add-Member NoteProperty Badge ([IO.Path]::GetExtension($tool.Path).TrimStart('.').ToUpperInvariant())
    }
    if ($null -eq $script:state.Groups) {
        $script:state.Groups = @()
        foreach ($definition in @(
            @{Name='Desktop releases'; Pattern='DesktopRelease|DesktopInstaller|Signing|ReleaseDesk'},
            @{Name='Portal deployment'; Pattern='Publish|Deploy|Production|Local.Copy'},
            @{Name='Backups & recovery'; Pattern='Backup|Restore'},
            @{Name='Server setup'; Pattern='IIS|Https|ProtectionAccess|Localhost'}
        )) {
            $paths = @($script:catalog | Where-Object { -not $_.IsArchived -and $_.Name -match $definition.Pattern } | ForEach-Object Path)
            $script:state.Groups += [pscustomobject]@{Id=[guid]::NewGuid().ToString('N'); Name=$definition.Name; Paths=$paths}
        }
    }
    Refresh-GroupChoices
    $script:releases = @(Get-ReleaseRows)
    $script:ui.Packages.ItemsSource = $script:releases
    $script:ui.PackageEmpty.Visibility = if ($script:releases.Count) { 'Collapsed' } else { 'Visible' }
    $script:ui.MetricTools.Text = [string]$script:catalog.Count
    $script:ui.MetricRelease.Text = if ($script:releases.Count) { $script:releases[0].Version } else { 'None yet' }
    $script:ui.MetricReady.Text = [string]@($script:releases | Where-Object Status -eq 'Files present').Count
    $previousPackage = if ($script:ui.PackageChoice.SelectedItem) { $script:ui.PackageChoice.SelectedItem.Version } else { '' }
    $script:ui.PackageChoice.ItemsSource = @($script:releases | Where-Object Status -eq 'Files present')
    $script:ui.PackageChoice.SelectedItem = $script:ui.PackageChoice.Items | Where-Object Version -eq $previousPackage | Select-Object -First 1
    if (-not $script:ui.PackageChoice.SelectedItem -and $script:ui.PackageChoice.Items.Count) { $script:ui.PackageChoice.SelectedIndex = 0 }
    Refresh-JobHistory
    $script:ui.PublishPath.Text = $script:publishToolsPath
    $script:ui.InstallerPath.Text = $script:installerFolder
    $sourceCount = @(@($script:publishToolsPath, $script:installerFolder) | Where-Object { Test-Path -LiteralPath $_ -PathType Container }).Count
    $script:ui.SidebarStatus.Text = "$sourceCount sources / $($script:catalog.Count) tools"
    Refresh-Library
    $script:ui.Status.Text = if ($sourceCount -eq 2) { "Refreshed $(Get-Date -Format 'h:mm tt') / $($script:catalog.Count) scripts and shortcuts" } else { 'Publish folder unavailable. Choose its location in Folders & preferences.' }
}
function Show-DeskPage([string]$Name) {
    foreach ($page in 'Overview','Library','Release','Activity','Folders') { $script:ui[$page].Visibility = 'Collapsed' }
    $script:mode = $Name
    $pageName = if ($Name -in @('Favorites','Recent','Groups')) { 'Library' } else { $Name }
    $script:ui[$pageName].Visibility = 'Visible'
    $titles = @{ Overview='Your workspace, at a glance'; Library='Scripts & shortcuts'; Groups='Grouped tools'; Favorites='Your favorite tools'; Recent='Recently opened'; Release='Desktop release'; Activity='Activity & output'; Folders='Folders & preferences' }
    $subtitles = @{ Overview='Build releases and find the tools you use every day.'; Library='Search both folders. Select a card to see its details.'; Groups='Related tools, organized into your own sections. A tool can belong to several groups.'; Favorites='Keep your everyday tools close at hand.'; Recent='Return to tools you opened from this workspace.'; Release='Prepare, review, and publish your next desktop update.'; Activity='Track tool launches and read live release output.'; Folders='Manage your sources and personal workspace.' }
    $script:ui.PageTitle.Text = $titles[$Name]; $script:ui.PageSubtitle.Text = $subtitles[$Name]
    foreach ($key in $titles.Keys) {
        $nav = $script:ui['Nav' + $key]
        $nav.Background = if ($key -eq $Name) { '#B9203C' } else { '#182438' }
        $nav.Foreground = if ($key -eq $Name) { 'White' } else { '#C2CEE0' }
        $nav.BorderBrush = $nav.Background
        $nav.HorizontalContentAlignment = 'Left'
    }
    $script:ui.GroupBar.Visibility = if ($Name -eq 'Groups') { 'Visible' } else { 'Collapsed' }
    if ($pageName -eq 'Library') { Refresh-Library }
}
function Show-ToolDetails {
    $script:selected = $script:ui.Tools.SelectedItem
    $tool = $script:selected
    foreach ($key in 'LaunchTool','Favorite','Locate','CopyPath','AddToGroup') { $script:ui[$key].IsEnabled = $null -ne $tool }
    $script:ui.RemoveFromGroup.IsEnabled = $null -ne $tool -and $script:mode -eq 'Groups'
    if (-not $tool) { $script:ui.DetailName.Text = 'Choose a tool'; $script:ui.DetailSummary.Text = 'Select a card to inspect its purpose, source, and file preview.'; $script:ui.DetailMeta.Text = ''; $script:ui.DetailPath.Text = ''; $script:ui.Preview.Text = ''; return }
    $script:ui.DetailName.Text = $tool.DisplayName
    $script:ui.DetailSummary.Text = $tool.Summary
    $script:ui.DetailMeta.Text = "$($tool.Kind) / $($tool.State)`nModified $($tool.Modified.ToString('MMM d, yyyy h:mm tt'))"
    $script:ui.DetailPath.Text = $tool.Path
    $script:ui.Favorite.Content = if ($script:state.Favorites -contains $tool.Path) { '★  Favorited' } else { '☆  Favorite' }
    try {
        if ($tool.Badge -eq 'LNK') {
            $shell = New-Object -ComObject WScript.Shell
            $shortcut = $shell.CreateShortcut($tool.Path)
            $script:ui.Preview.Text = "Target: $($shortcut.TargetPath)`r`nArguments: $($shortcut.Arguments)`r`nWorking folder: $($shortcut.WorkingDirectory)"
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut)
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
        } else { $script:ui.Preview.Text = (Get-Content -LiteralPath $tool.Path -TotalCount 100) -join "`r`n" }
    } catch { $script:ui.Preview.Text = 'Preview unavailable: ' + $_.Exception.Message }
}
function Open-DeskFolder([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { Add-DeskLog "Folder unavailable: $Path"; return }
    Start-Process -FilePath explorer.exe -ArgumentList ('"' + $Path + '"')
}
function Open-DeskTool {
    $tool = $script:selected
    if (-not $tool) { return }
    if (-not (Test-Path -LiteralPath $tool.Path -PathType Leaf)) { Add-DeskLog 'File no longer exists. Refresh the library.'; return }
    $copyNote = if ($tool.IsArchived) { "`n`nThis is an archived copy." } else { '' }
    if ([Windows.MessageBox]::Show($script:window, "Open $($tool.Name)?`n`n$($tool.Path)$copyNote", 'Open selected tool', 'YesNo', 'Question') -ne 'Yes') { return }
    try {
        if ($tool.Badge -eq 'PS1') {
            Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList ('-NoLogo -NoProfile -NoExit -ExecutionPolicy Bypass -File "' + $tool.Path + '"') -WorkingDirectory (Split-Path $tool.Path -Parent)
        } else { Start-Process -FilePath $tool.Path -WorkingDirectory (Split-Path $tool.Path -Parent) }
        $script:state.Recent = @([pscustomobject]@{ Path=$tool.Path; Opened=(Get-Date).ToString('o') }) + @($script:state.Recent | Where-Object Path -ne $tool.Path | Select-Object -First 29)
        Save-Workspace
        Add-DeskLog "Opened $($tool.Name) in its associated application."
    } catch { Add-DeskLog ('Could not open tool: ' + $_.Exception.Message) }
}
function Quote-DeskLiteral([string]$Value) { return "'" + $Value.Replace("'", "''") + "'" }
function Start-DeskRelease([string]$Action) {
    if ($script:job) { return }
    $resultPath = $null
    $version = $script:ui.Version.Text.Trim()
    $package = $script:ui.PackageChoice.SelectedItem
    try {
        if ($Action -in 'Build','BuildPublish') {
            [void](ConvertTo-DeskVersion $version)
            if ($script:ui.Notes.Text.Length -gt 16000) { throw 'Release notes must be at most 16000 characters.' }
        } elseif (-not $package) { throw 'Choose a completed local package first.' }
    } catch { [void][Windows.MessageBox]::Show($script:window, $_.Exception.Message, 'Review release'); return }
    if ($Action -in 'BuildPublish','Publish') {
        $reviewVersion = if ($Action -eq 'Publish') { $package.Version } else { $version }
        $reviewNotes = if ($Action -eq 'Publish') { 'Saved signed package notes will be used. Draft fields apply only to builds.' } else { $script:ui.Notes.Text }
        $review = "Publish version $reviewVersion?`n`nDestination: $UpdateFolder`n`n$reviewNotes`n`nThe publisher rechecks installer bytes, feed version and publication permissions."
        if ([Windows.MessageBox]::Show($script:window, $review, 'Review publication', 'YesNo', 'Question') -ne 'Yes') { return }
    }
    $scriptFile = if ($Action -in 'Build','BuildPublish') { 'Build-DesktopRelease.ps1' } elseif ($Action -eq 'Verify') { 'Inspect-DeskRelease.ps1' } else { 'Publish-DesktopRelease.ps1' }
    $command = '& ' + (Quote-DeskLiteral (Join-Path $script:installerFolder $scriptFile))
    if ($Action -in 'Build','BuildPublish') { $command += ' -Version ' + (Quote-DeskLiteral $version) + ' -ReleaseNotes ' + (Quote-DeskLiteral $script:ui.Notes.Text) }
    if ($Action -eq 'BuildPublish') { $command += ' -Publish' }
    if ($Action -in 'Publish','PreviewPublish') { $command += ' -Version ' + (Quote-DeskLiteral $package.Version) }
    if ($Action -eq 'PreviewPublish') { $command += ' -WhatIf' }
    if ($Action -eq 'Verify') {
        $script:inspection = $null
        Show-ReleaseSummary
        $resultPath = Join-Path $script:stateFolder ('inspection-' + [guid]::NewGuid().ToString('N') + '.json')
        $command += ' -Directory ' + (Quote-DeskLiteral $package.Directory) + ' -ResultPath ' + (Quote-DeskLiteral $resultPath)
    }
    if ($Action -ne 'Build') { $command += ' -UpdateFolder ' + (Quote-DeskLiteral $UpdateFolder) }
    $command = '$ErrorActionPreference = ''Stop''; $global:LASTEXITCODE = 0; [Console]::OutputEncoding = [Text.Encoding]::UTF8; try { ' + $command + '; exit $global:LASTEXITCODE } catch { [Console]::Error.WriteLine($_.Exception.ToString()); exit 1 }'
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = Join-Path $PSHOME 'powershell.exe'
    $info.Arguments = '-NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand ' + [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    $info.WorkingDirectory = $script:installerFolder
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = [Text.Encoding]::UTF8; $info.StandardErrorEncoding = [Text.Encoding]::UTF8
    [void]$info.EnvironmentVariables.Remove('PSModulePath')
    Save-Workspace
    try {
        $process = New-Object Diagnostics.Process; $process.StartInfo = $info
        [void][IO.Directory]::CreateDirectory($script:logsFolder)
        $id = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N')
        $recordPath = Join-Path $script:logsFolder ($id + '.json')
        $logPath = Join-Path $script:logsFolder ($id + '.log')
        $record = [ordered]@{ Id=$id; Action=$Action; Version=$(if ($Action -in 'Build','BuildPublish') { $version } else { $package.Version }); ArtifactDirectory=$(if ($Action -in 'Build','BuildPublish') { Join-Path $script:releaseFolder $version } else { $package.Directory }); Started=(Get-Date).ToString('o'); Finished=$null; Status='Starting'; ExitCode=$null; Destination=$UpdateFolder; LogPath=$logPath; ProcessId=$null }
        Write-DeskJobRecord $recordPath $record
        [void]$process.Start()
        $script:job = @{ Process=$process; OutTask=$process.StandardOutput.ReadLineAsync(); ErrTask=$process.StandardError.ReadLineAsync(); Action=$Action; Started=(Get-Date); Record=$record; RecordPath=$recordPath; LogPath=$logPath; ResultPath=$resultPath }
        $record.Status='Running'; $record.ProcessId=$process.Id
        Write-DeskJobRecord $recordPath $record
        foreach ($key in 'Build','BuildPublish','PreviewPublish','Publish','VerifyPackage') { $script:ui[$key].IsEnabled = $false }
        $script:ui.JobProgress.IsIndeterminate = $true
        $script:ui.JobStatus.Text = "$Action is running. Live output is available in Activity."
        Add-DeskLog "$Action started."
        $script:timer.Start()
    } catch {
        Add-DeskLog ('Could not start release action: ' + $_.Exception.Message)
        if ($script:job) { $script:timer.Start() }
        else {
            if ($record -and $recordPath) {
                $record.Status = 'Failed to start'; $record.Finished = (Get-Date).ToString('o')
                try { Write-DeskJobRecord $recordPath $record; Refresh-JobHistory } catch { $script:ui.Status.Text = 'Could not save failed job record.' }
            }
            if ($process) { $process.Dispose() }
        }
    }
}
$script:timer = New-Object Windows.Threading.DispatcherTimer
$script:timer.Interval = [TimeSpan]::FromMilliseconds(150)
function Update-DeskJob {
    if (-not $script:job) { return }
    $elapsed = (Get-Date) - $script:job.Started
    if ($script:job.Action -eq 'Verify' -and $elapsed.TotalSeconds -gt 30 -and -not $script:job.Process.HasExited -and -not $script:job.TimedOut) {
        $script:job.TimedOut = $true
        $script:job.Process.Kill()
        Add-DeskLog 'Read-only inspection exceeded 30 seconds and was stopped. No release files were changed.'
    }
    $script:ui.JobStatus.Text = ('{0} running / elapsed {1:hh\:mm\:ss}' -f $script:job.Action, $elapsed)
    foreach ($pair in @(@('OutTask','StandardOutput'), @('ErrTask','StandardError'))) {
        $key = $pair[0]; $stream = $pair[1]; $readCount = 0
        while ($script:job[$key] -and $script:job[$key].IsCompleted -and $readCount -lt 100) {
            try { $line = $script:job[$key].GetAwaiter().GetResult() }
            catch { Add-DeskLog ('Output stream closed: ' + $_.Exception.Message); $script:job[$key] = $null; break }
            if ($null -eq $line) { $script:job[$key] = $null; break }
            Add-DeskLog $line
            $script:job[$key] = $script:job.Process.$stream.ReadLineAsync(); $readCount++
        }
    }
    if ($script:job.Process.HasExited -and -not $script:job.OutTask -and -not $script:job.ErrTask) {
        $code = $script:job.Process.ExitCode
        $message = if ($script:job.TimedOut) { 'Inspection timed out. Check feed availability and retry.' } else { "$($script:job.Action) finished with exit code $code." }
        Add-DeskLog $message
        $script:job.Record.Finished = (Get-Date).ToString('o')
        $script:job.Record.ExitCode = $code
        $script:job.Record.Status = if ($script:job.TimedOut) { 'Timed out' } elseif ($code -eq 0) { 'Succeeded' } else { 'Failed' }
        try { Write-DeskJobRecord $script:job.RecordPath $script:job.Record } catch { Add-DeskLog ('Could not save job result: ' + $_.Exception.Message) }
        if ($script:job.ResultPath -and (Test-Path -LiteralPath $script:job.ResultPath)) {
            try {
                $script:inspection = Get-Content -LiteralPath $script:job.ResultPath -Raw | ConvertFrom-Json
                Show-ReleaseSummary
            } catch { Add-DeskLog ('Could not read inspection: ' + $_.Exception.Message) }
            finally { Remove-Item -LiteralPath $script:job.ResultPath -Force -ErrorAction SilentlyContinue }
        }
        $script:job.Process.Dispose(); $script:job = $null; $script:timer.Stop()
        $script:ui.JobProgress.IsIndeterminate = $false
        $script:ui.JobStatus.Text = $message
        foreach ($key in 'Build','BuildPublish','PreviewPublish','Publish','VerifyPackage') { $script:ui[$key].IsEnabled = $true }
        Refresh-Desk; Add-DeskLog $message
    }
}
function Show-ReleaseSummary {
    $package = $script:ui.PackageChoice.SelectedItem
    if (-not $package) { $script:ui.ReleaseSummary.Text = 'No completed local package. Build an installer first.'; $script:ui.FeedSummary.Text = "Destination: $UpdateFolder"; return }
    $script:ui.ReleaseSummary.Text = "Version $($package.Version) / $($package.Size)`n$($package.Installer)`nFiles present; select Verify package to inspect signature and installer bytes."
    $script:ui.FeedSummary.Text = "Destination: $UpdateFolder`nFeed has not been checked for this package."
    if ($script:inspection -and $script:inspection.Version -eq $package.Version) {
        $state = if ($script:inspection.Verified) { 'Verified' } else { 'Verification failed' }
        $script:ui.ReleaseSummary.Text = "$state / $($package.Version)`n$($script:inspection.Details)`nChecked $($script:inspection.CheckedAt). Publication always verifies again.`nSaved notes: $($script:inspection.Notes)"
        $script:ui.FeedSummary.Text = "Published version: $($script:inspection.PublishedVersion)`n$($script:inspection.FeedStatus)`nDestination: $UpdateFolder"
    }
}
function Refresh-JobHistory {
    $records = @()
    if (Test-Path -LiteralPath $script:logsFolder) {
        foreach ($file in Get-ChildItem -LiteralPath $script:logsFolder -Filter '*.json' -File | Sort-Object Name -Descending | Select-Object -First 100) {
            try {
                $record = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
                $record | Add-Member NoteProperty Label ("$($record.Started) / $($record.Action) $($record.Version) / $($record.Status)")
                $records += $record
            } catch { continue }
        }
    }
    $script:ui.JobHistory.ItemsSource = $records
}
function Refresh-GroupChoices([string]$SelectId) {
    $script:updatingGroups = $true
    try {
        $selected = if ($SelectId) { $SelectId } else { [string]$script:ui.GroupFilter.SelectedValue }
        $target = [string]$script:ui.TargetGroup.SelectedValue
        $script:ui.GroupFilter.ItemsSource = @([pscustomobject]@{Id=''; Name='All groups'}) + @($script:state.Groups)
        $script:ui.GroupFilter.SelectedValue = $selected
        if ($script:ui.GroupFilter.SelectedIndex -lt 0) { $script:ui.GroupFilter.SelectedIndex = 0 }
        $script:ui.TargetGroup.ItemsSource = @($script:state.Groups)
        $script:ui.TargetGroup.SelectedValue = if ($SelectId) { $SelectId } else { $target }
        if ($script:ui.TargetGroup.SelectedIndex -lt 0 -and $script:ui.TargetGroup.Items.Count) { $script:ui.TargetGroup.SelectedIndex = 0 }
    } finally { $script:updatingGroups = $false }
    Update-GroupEditor
}
function Update-GroupEditor {
    $group = $script:ui.GroupFilter.SelectedItem
    $hasGroup = $group -and $group.Id
    $script:ui.RenameGroup.IsEnabled = [bool]$hasGroup
    $script:ui.DeleteGroup.IsEnabled = [bool]$hasGroup
    $script:ui.GroupName.Text = if ($hasGroup) { $group.Name } else { '' }
}
function Set-DeskGroup([string]$Action) {
    $name = $script:ui.GroupName.Text.Trim()
    if (-not $name -or $name.Length -gt 80) { $script:ui.Status.Text = 'Enter a group name (1 to 80 characters).'; return }
    $id = if ($Action -eq 'Rename') { [string]$script:ui.GroupFilter.SelectedValue } else { '' }
    if (@($script:state.Groups | Where-Object { $_.Name -eq $name -and $_.Id -ne $id }).Count) { $script:ui.Status.Text = 'That group name already exists.'; return }
    if ($Action -eq 'Rename') {
        if (-not $id) { return }
        ($script:state.Groups | Where-Object Id -eq $id).Name = $name
    } else {
        $id = [guid]::NewGuid().ToString('N')
        $script:state.Groups = @($script:state.Groups) + [pscustomobject]@{Id=$id; Name=$name; Paths=@()}
    }
    Save-Workspace; Refresh-GroupChoices $id; Refresh-Library
    $script:ui.Status.Text = "Group saved: $name. Select a tool in the library and use Add to group."
}
function Set-ToolGroup([bool]$Remove) {
    if (-not $script:selected) { return }
    $id = if ($Remove) { $script:selected.GroupId } else { $script:ui.TargetGroup.SelectedValue }
    $group = $script:state.Groups | Where-Object Id -eq $id | Select-Object -First 1
    if (-not $group) { $script:ui.Status.Text = 'Create or choose a group in Grouped tools first.'; return }
    $path = $script:selected.Path
    if ($Remove) { $group.Paths = @($group.Paths | Where-Object { $_ -ne $path }) }
    elseif ($group.Paths -notcontains $path) { $group.Paths = @($group.Paths) + $path }
    Save-Workspace; Refresh-Library
    $script:ui.Status.Text = if ($Remove) { "Removed from $($group.Name). File and favorites unchanged." } else { "Added to $($group.Name)." }
}
$script:ui.GroupFilter.Add_SelectionChanged({ if (-not $script:updatingGroups) { Update-GroupEditor; Refresh-Library } })
$script:ui.CreateGroup.Add_Click({ Set-DeskGroup 'Create' })
$script:ui.RenameGroup.Add_Click({ Set-DeskGroup 'Rename' })
$script:ui.AddToGroup.Add_Click({ Set-ToolGroup $false })
$script:ui.RemoveFromGroup.Add_Click({ Set-ToolGroup $true })
$script:ui.DeleteGroup.Add_Click({
    $group = $script:ui.GroupFilter.SelectedItem
    if (-not $group -or -not $group.Id) { return }
    if ([Windows.MessageBox]::Show($script:window, "Delete group '$($group.Name)'? Tools and favorites will remain.", 'Delete group', 'YesNo', 'Question') -ne 'Yes') { return }
    $script:state.Groups = @($script:state.Groups | Where-Object Id -ne $group.Id)
    Save-Workspace; Refresh-GroupChoices; Refresh-Library
})
$script:timer.Add_Tick({ Update-DeskJob })
foreach ($name in 'Overview','Library','Favorites','Groups','Recent','Release','Activity','Folders') {
    $script:ui['Nav' + $name].Tag = $name
    $script:ui['Nav' + $name].Add_Click({ param($sender, $eventArgs) Show-DeskPage ([string]$sender.Tag) })
}
$script:ui.PackageChoice.Add_SelectionChanged({ Show-ReleaseSummary })
$script:ui.VerifyPackage.Add_Click({ Start-DeskRelease 'Verify' })
$script:ui.OpenLogs.Add_Click({ Open-DeskFolder $script:logsFolder })
$script:ui.JobHistory.Add_SelectionChanged({
    $record = $script:ui.JobHistory.SelectedItem
    if ($record) {
        try { $script:ui.HistoryDetails.Text = [IO.File]::ReadAllText($record.LogPath) }
        catch { $script:ui.HistoryDetails.Text = 'Log unavailable: ' + $_.Exception.Message }
    }
})
$script:ui.Browse.Add_Click({ Show-DeskPage 'Library' })
$script:ui.Refresh.Add_Click({ Refresh-Desk })
$script:ui.Search.Add_TextChanged({ Refresh-Library })
$script:ui.StateFilter.Add_SelectionChanged({ Refresh-Library })
$script:ui.SourceFilter.Add_SelectionChanged({ Refresh-Library })
$script:ui.Tools.Add_SelectionChanged({ Show-ToolDetails })
$script:ui.LaunchTool.Add_Click({ Open-DeskTool })
$script:ui.Favorite.Add_Click({
    if (-not $script:selected) { return }
    $path = $script:selected.Path
    if ($script:state.Favorites -contains $path) { $script:state.Favorites = @($script:state.Favorites | Where-Object { $_ -ne $path }) }
    else { $script:state.Favorites = @($script:state.Favorites) + $path }
    Save-Workspace; Refresh-Library; Show-ToolDetails
})
$script:ui.Locate.Add_Click({ if ($script:selected) { Start-Process explorer.exe -ArgumentList ('/select,"' + $script:selected.Path + '"') } })
$script:ui.CopyPath.Add_Click({ if ($script:selected) { [Windows.Clipboard]::SetText($script:selected.Path); $script:ui.Status.Text = 'Full file path copied.' } })
$script:ui.CopyOutput.Add_Click({ if ($script:ui.Output.Text) { [Windows.Clipboard]::SetText($script:ui.Output.Text) } })
$script:ui.Packages.Add_MouseDoubleClick({ if ($script:ui.Packages.SelectedItem) { Open-DeskFolder $script:ui.Packages.SelectedItem.Directory } })
foreach ($action in 'Build','BuildPublish','PreviewPublish','Publish') { $script:ui[$action].Tag = $action; $script:ui[$action].Add_Click({ param($sender,$eventArgs) Start-DeskRelease ([string]$sender.Tag) }) }
$script:ui.OpenReleases.Add_Click({ Open-DeskFolder $script:releaseFolder })
$script:ui.ViewOutput.Add_Click({ Show-DeskPage 'Activity' })
$script:ui.OpenPublish.Add_Click({ Open-DeskFolder $script:publishToolsPath })
$script:ui.OpenInstaller.Add_Click({ Open-DeskFolder $script:installerFolder })
$script:ui.ChangeFolder.Add_Click({
    $dialog = New-Object Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'Choose your publish toolbox folder'
    $dialog.SelectedPath = $script:publishToolsPath
    try { if ($dialog.ShowDialog() -eq 'OK') { $script:publishToolsPath = $dialog.SelectedPath; Save-Workspace; Refresh-Desk } } finally { $dialog.Dispose() }
})
$script:window.Add_PreviewKeyDown({ param($sender,$eventArgs)
    if ($eventArgs.Key -eq 'F5') { Refresh-Desk; $eventArgs.Handled=$true }
    if ($eventArgs.Key -eq 'K' -and ([Windows.Input.Keyboard]::Modifiers -band [Windows.Input.ModifierKeys]::Control)) { Show-DeskPage 'Library'; [void]$script:ui.Search.Focus(); $eventArgs.Handled=$true }
})
$script:window.Add_Closing({ param($sender,$eventArgs)
    if ($script:job) { $eventArgs.Cancel = $true; Show-DeskPage 'Activity'; $script:ui.Status.Text = 'A release is running. Keep this window open until it finishes.'; return }
    Save-Workspace; $script:timer.Stop()
})
$script:ui.PreferencesPath.Text = $script:statePath
$script:window.Width = [Math]::Max(960, [Math]::Min([double]$script:state.Width, [Windows.SystemParameters]::WorkArea.Width))
$script:window.Height = [Math]::Max(600, [Math]::Min([double]$script:state.Height, [Windows.SystemParameters]::WorkArea.Height))
Refresh-Desk
$script:ui.Version.Text = if ($script:releases.Count) { $v = [version]$script:releases[0].Version; '{0}.{1}.{2}.0' -f $v.Major,$v.Minor,($v.Build+1) } else { '1.0.0.0' }
if ($script:state.DraftVersion) { $script:ui.Version.Text = $script:state.DraftVersion }
$script:ui.Notes.Text = $script:state.DraftNotes
Show-ReleaseSummary
Show-DeskPage 'Overview'
Add-DeskLog "Workspace ready / $($script:catalog.Count) catalog items. F5 refreshes; Ctrl+K searches."
if ($script:stateWarning) { Add-DeskLog $script:stateWarning }
if ($ValidateOnly) {
    foreach ($page in 'Overview','Library','Favorites','Groups','Recent','Release','Activity','Folders') { Show-DeskPage $page }
    Show-DeskPage 'Library'
    if ($script:catalog.Count) { $script:ui.Tools.SelectedIndex = 0; Show-ToolDetails }
    $script:ui.Search.Text = 'NO_MATCH_3EFCAC'; if ($script:ui.Tools.Items.Count -ne 0) { throw 'Search did not filter results.' }
    $script:ui.Search.Text = ''
    if ($script:catalog.Count) {
        $originalFavorites = $script:state.Favorites
        $script:state.Favorites = @($script:catalog[0].Path)
        Show-DeskPage 'Favorites'
        if ($script:ui.Tools.Items.Count -ne 1) { throw 'Favorites view did not filter to the pinned tool.' }
        $script:state.Favorites = $originalFavorites
        Show-DeskPage 'Library'
        $script:ui.SourceFilter.SelectedIndex = 1
        if (@($script:ui.Tools.Items | Where-Object Root -ne 'Desktop publish folder').Count) { throw 'Source filter included the wrong folder.' }
        $script:ui.SourceFilter.SelectedIndex = 0
        $script:ui.StateFilter.SelectedIndex = 2
        if (@($script:ui.Tools.Items | Where-Object { -not $_.IsArchived }).Count) { throw 'Archive filter included a current file.' }
    }
    if ($script:catalog.Count) {
        $originalGroups = $script:state.Groups
        $originalPins = @($script:state.Favorites)
        $script:state.Groups = @()
        $script:ui.Search.Text = ''
        $script:ui.StateFilter.SelectedIndex = 0
        $script:ui.SourceFilter.SelectedIndex = 0
        Refresh-GroupChoices
        Show-DeskPage 'Groups'
        $script:ui.GroupName.Text = 'Test workflow'
        Set-DeskGroup 'Create'
        $testId = $script:state.Groups[0].Id
        if ($script:state.Groups.Count -ne 1) { throw 'Group creation failed.' }
        $script:ui.GroupName.Text = 'Renamed workflow'
        Set-DeskGroup 'Rename'
        if ($script:state.Groups[0].Name -ne 'Renamed workflow') { throw 'Group rename failed.' }
        Show-DeskPage 'Library'
        $script:ui.Tools.SelectedIndex = 0
        $testPath = $script:selected.Path
        Set-ToolGroup $false
        Set-ToolGroup $false
        if ($script:state.Groups[0].Paths.Count -ne 1) { throw 'Group membership must be unique.' }
        Show-DeskPage 'Groups'
        if ($script:ui.Tools.Items.Count -ne 1 -or $script:ui.Tools.Items.Groups.Count -ne 1) { throw 'Grouped view did not display its tool and header.' }
        $script:ui.Search.Text = 'NO_MATCH_GROUP_8BA'
        if ($script:ui.Tools.Items.Count -ne 0) { throw 'Grouped search failed.' }
        $script:ui.Search.Text = ''
        $script:ui.Tools.SelectedIndex = 0
        Set-ToolGroup $true
        if ($script:ui.Tools.Items.Count -ne 0 -or $script:state.Groups[0].Paths.Count -ne 0) { throw 'Removing group membership failed.' }
        $restored = $script:state | ConvertTo-Json -Depth 5 | ConvertFrom-Json
        if ($restored.Groups[0].Id -ne $testId -or $restored.Groups[0].Name -ne 'Renamed workflow') { throw 'Group preferences round-trip failed.' }
        if (($originalPins -join '|') -ne ($script:state.Favorites -join '|')) { throw 'Grouping changed favorites.' }
        $script:state.Groups = $originalGroups
        Refresh-GroupChoices
        Write-Output 'Groups: create, rename, unique membership, grouped headers, search, removal and saved preferences passed.'
    }
    Write-Output "WPF loaded; navigation, catalog, selection, preview, and filtering passed. Catalog: $($script:catalog.Count) items."
    $script:window.Close(); return
}
if ($PreviewPath) {
    Show-DeskPage $PreviewPage
    if ($PreviewPage -eq 'Library' -and $script:catalog.Count) { $script:ui.Tools.SelectedIndex = 0 }
    $script:window.WindowStartupLocation='Manual'; $script:window.Left=-10000; $script:window.Top=-10000
    $script:window.Width=1440; $script:window.Height=900; $script:window.ShowActivated=$false
    $script:window.Show(); $script:window.UpdateLayout()
    [void]$script:window.Dispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::ApplicationIdle)
    if ($PreviewScrollEnd -and $script:ui[$PreviewPage] -is [Windows.Controls.ScrollViewer]) {
        $script:ui[$PreviewPage].ScrollToEnd()
        [void]$script:window.Dispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::ApplicationIdle)
        $script:window.UpdateLayout()
    }
    $visual = $script:window.Content; $pixelWidth = [int][Math]::Ceiling($visual.ActualWidth); $pixelHeight = [int][Math]::Ceiling($visual.ActualHeight)
    $bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap($pixelWidth,$pixelHeight,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder; $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $file = [IO.File]::Create([IO.Path]::GetFullPath($PreviewPath)); try { $encoder.Save($file) } finally { $file.Dispose() }
    $script:window.Close(); return
}
[void]$script:window.ShowDialog()
