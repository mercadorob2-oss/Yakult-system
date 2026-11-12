#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$installerFolder = Join-Path $repo 'Yakult.Inventory.App\Installer'
. (Join-Path $installerFolder 'ReleaseDesk.Services.ps1')
$fixture = Join-Path $repo ('artifacts\release-desk-tests\' + [guid]::NewGuid().ToString('N'))
$package = Join-Path $fixture '1.2.3.4'
[void][IO.Directory]::CreateDirectory($package)
$keyPath = Join-Path $fixture 'public.xml'
$rsa = [Security.Cryptography.RSACryptoServiceProvider]::new(2048)
$rsa.PersistKeyInCsp = $false
function Assert-Rejected([scriptblock]$Action, [string]$Label) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw "Expected rejection: $Label" }
    Write-Output "PASS: $Label"
}
try {
    [IO.File]::WriteAllText($keyPath, $rsa.ToXmlString($false))
    $installer = Join-Path $package 'YISP_Setup_1.2.3.4.exe'
    [IO.File]::WriteAllText($installer, 'Synthetic installer bytes. Never execute.')
    $hash = [Security.Cryptography.SHA256]::Create()
    try { $digest = [BitConverter]::ToString($hash.ComputeHash([IO.File]::ReadAllBytes($installer))).Replace('-','').ToLowerInvariant() }
    finally { $hash.Dispose() }
    $metadata = @{schemaVersion=1; product='Yakult.Inventory.App'; version='1.2.3.4'; installer='YISP_Setup_1.2.3.4.exe'; sha256=$digest; sizeBytes=(Get-Item $installer).Length; releaseNotes="Saved notes with 'quotes'"}
    $payload = [Text.Encoding]::UTF8.GetBytes(($metadata | ConvertTo-Json))
    $envelope = @{schemaVersion=1; payload=[Convert]::ToBase64String($payload); signature=[Convert]::ToBase64String($rsa.SignData($payload,'SHA256'))}
    $manifest = Join-Path $package 'latest.json'
    [IO.File]::WriteAllText($manifest, ($envelope | ConvertTo-Json))
    $verified = Test-DeskPackage $package $keyPath
    if ($verified.releaseNotes -ne $metadata.releaseNotes) { throw 'Saved notes were changed.' }
    Write-Output 'PASS: valid signed package and saved notes'
    [IO.File]::WriteAllText($installer, 'Tampered installer bytes. Never execute.')
    Assert-Rejected { Test-DeskPackage $package $keyPath } 'changed installer bytes'
    $envelope.signature = [Convert]::ToBase64String((New-Object byte[] 256))
    [IO.File]::WriteAllText($manifest, ($envelope | ConvertTo-Json))
    Assert-Rejected { Read-DeskMetadata $manifest $keyPath } 'invalid signature'
    Assert-Rejected { ConvertTo-DeskVersion '1.2.65535.0' } 'version component overflow'
    Assert-Rejected { ConvertTo-DeskVersion '1.2.3' } 'incomplete version'
    $recordPath = Join-Path $fixture 'job.json'
    Write-DeskJobRecord $recordPath @{Status='Running'; Version='1.2.3.4'}
    Write-DeskJobRecord $recordPath @{Status='Failed'; Version='1.2.3.4'; ExitCode=1}
    $record = Get-Content $recordPath -Raw | ConvertFrom-Json
    if ($record.Status -ne 'Failed' -or $record.ExitCode -ne 1) { throw 'Job record replacement failed.' }
    Write-Output 'PASS: durable job record creation and atomic replacement'
    $lockPath = Join-Path $fixture '.build.lock'
    $lease = [IO.File]::Open($lockPath,'OpenOrCreate','ReadWrite','None')
    try { Assert-Rejected { $other = [IO.File]::Open($lockPath,'OpenOrCreate','ReadWrite','None'); $other.Dispose() } 'concurrent build lock' }
    finally { $lease.Dispose() }
    foreach ($name in @('YakultReleaseDesk.ps1','ReleaseDesk.Services.ps1','Inspect-DeskRelease.ps1','ReleaseDesk.Core.ps1','Build-DesktopRelease.ps1')) {
        $tokens = $null; $errors = $null
        [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $installerFolder $name), [ref]$tokens, [ref]$errors)
        if ($errors.Count) { throw ($errors | Out-String) }
    }
    [xml](Get-Content (Join-Path $installerFolder 'ReleaseDesk.xaml') -Raw) | Out-Null
    Write-Output 'PASS: PowerShell parsing and XAML XML validation'
    # Load the actual WPF controller without displaying a window or writing preferences.
    . (Join-Path $installerFolder 'YakultReleaseDesk.ps1') -ValidateOnly -UpdateFolder $fixture
    $script:stateFolder = $fixture
    $script:logsFolder = Join-Path $fixture 'Jobs'
    $script:ui.PackageChoice.ItemsSource = @([pscustomobject]@{Version='1.2.3.4'; Directory=$package; Installer=$installer; Size='fixture'})
    $script:ui.PackageChoice.SelectedIndex = 0
    Start-DeskRelease 'Verify'
    if (-not $script:job) { throw 'Inspection process was not started.' }
    $deadline = (Get-Date).AddSeconds(15)
    while ($script:job -and (Get-Date) -lt $deadline) { Update-DeskJob; Start-Sleep -Milliseconds 30 }
    if ($script:job) { $script:job.Process.Kill(); throw 'Inspection job did not finish.' }
    $savedJob = Get-ChildItem $script:logsFolder -Filter '*.json' | Select-Object -First 1 | Get-Content -Raw | ConvertFrom-Json
    if ($savedJob.Status -ne 'Failed' -or $savedJob.ExitCode -eq 0) { throw 'Invalid package did not produce a failed job.' }
    if (-not (Test-Path -LiteralPath $savedJob.LogPath)) { throw 'Job output was not persisted.' }
    if (-not $script:ui.VerifyPackage.IsEnabled -or $script:ui.JobProgress.IsIndeterminate) { throw 'Job completion did not restore the controls.' }
    if ($script:ui.JobHistory.Items.Count -ne 1) { throw 'Saved job was not loaded into history.' }
    Write-Output 'PASS: actual WPF child-process failure, stream draining, saved log/history and control recovery'
    # When this checkout has a completed release, inspect it against its own local
    # manifest as the feed. This reads real bytes but never accesses the shared feed.
    $available = @(Get-ReleaseRows | Where-Object Status -eq 'Files present')
    if ($available.Count) {
        $script:ui.PackageChoice.ItemsSource = @($available[0])
        $script:ui.PackageChoice.SelectedIndex = 0
        $UpdateFolder = $available[0].Directory
        Start-DeskRelease 'Verify'
        $deadline = (Get-Date).AddSeconds(15)
        while ($script:job -and (Get-Date) -lt $deadline) { Update-DeskJob; Start-Sleep -Milliseconds 30 }
        if ($script:job) { $script:job.Process.Kill(); throw 'Valid inspection did not finish.' }
        if (-not $script:inspection.Verified -or $script:inspection.PublishedVersion -ne $available[0].Version) { throw 'Local package/feed comparison failed.' }
        Write-Output 'PASS: actual signed local package inspection and read-only feed comparison'
    }
} finally { $rsa.Dispose() }
Write-Output "Release Desk checks passed. Synthetic fixtures: $fixture"
