#Requires -Version 5.1
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$root = Join-Path $repo ('artifacts\desktop-update-checks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$fake = Join-Path $root 'fixture.exe'
$bytes = [byte[]]::new(512); $bytes[0]=0x4d; $bytes[1]=0x5a; [BitConverter]::GetBytes(128).CopyTo($bytes,60); $bytes[128]=0x50; $bytes[129]=0x45
[IO.File]::WriteAllBytes($fake,$bytes)
$publisher = Join-Path $repo 'Yakult.Inventory.App\Installer\Publish-DesktopRelease.ps1'
& $publisher -InstallerPath $fake -Version '1.6.0.0' -ReleaseNotes 'First synthetic release' -UpdateFolder $root
$initialHash = (Get-FileHash -LiteralPath (Join-Path $root 'latest.json')).Hash
$lock = [IO.File]::Open((Join-Path $root '.publish.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$lockBlocked = $false
try { try { & $publisher -InstallerPath $fake -Version '1.6.1.0' -UpdateFolder $root } catch { $lockBlocked = $true } } finally { $lock.Dispose() }
if (-not $lockBlocked -or (Get-FileHash -LiteralPath (Join-Path $root 'latest.json')).Hash -ne $initialHash) { throw 'Concurrent publication was not blocked safely.' }
# A completed package left by an interrupted publish can be reused if unchanged.
Copy-Item -LiteralPath $fake -Destination (Join-Path $root 'YISP_Setup_1.6.1.0.exe')
& $publisher -InstallerPath $fake -Version '1.6.1.0' -ReleaseNotes 'Second synthetic release' -UpdateFolder $root
$before = (Get-FileHash -LiteralPath (Join-Path $root 'latest.json')).Hash
$blocked = $false
try { & $publisher -InstallerPath $fake -Version '1.6.0.0' -UpdateFolder $root } catch { $blocked = $true }
if (-not $blocked -or (Get-FileHash -LiteralPath (Join-Path $root 'latest.json')).Hash -ne $before) { throw 'Rejected release changed the published manifest.' }
# Simulate a share that accepts temporary files but denies executable renames.
$deniedRoot = Join-Path $root 'exe-restricted-share'
New-Item -ItemType Directory -Path $deniedRoot | Out-Null
& {
    param($publisherPath,$fixture,$updateFolder)
    function Move-Item {
        [CmdletBinding()]
        param([string]$LiteralPath,[string]$Destination)
        if ([IO.Path]::GetExtension($Destination) -eq '.exe') { throw [UnauthorizedAccessException]::new('Synthetic server rule denies .exe files.') }
        Microsoft.PowerShell.Management\Move-Item @PSBoundParameters
    }
    function Copy-Item {
        [CmdletBinding()]
        param([string]$LiteralPath,[string]$Destination)
        throw 'Payload upload must not start when executable publication is denied.'
    }
    $failure = ''
    try { & $publisherPath -InstallerPath $fixture -Version '1.6.2.0' -UpdateFolder $updateFolder }
    catch { $failure = $_.Exception.Message }
    if ($failure -notlike '*file-server administrator*' -or $failure -notlike '*Synthetic server rule denies .exe files.*') { throw "Executable restriction did not produce a useful error: $failure" }
    $leftovers = @(Get-ChildItem -LiteralPath $updateFolder -Force | Where-Object { $_.Name -ne '.publish.lock' })
    if ($leftovers.Count) { throw 'Denied publication left an installer, manifest or temporary file behind.' }
} $publisher $fake $deniedRoot
Write-Host 'Executable restrictions stop publication before upload and leave the feed unchanged.'
# Automatic publication uses complete, signed builds and numeric version ordering.
$builds = Join-Path $root 'completed-builds'
$autoFeed = Join-Path $root 'automatic-feed'
New-Item -ItemType Directory -Path $builds,$autoFeed | Out-Null
foreach ($builtVersion in @('2.0.9.0','2.0.10.0')) {
    $builtFolder = Join-Path $builds $builtVersion
    New-Item -ItemType Directory -Path $builtFolder | Out-Null
    $builtInstaller = Join-Path $builtFolder ('YISP_Setup_' + $builtVersion + '.exe')
    Copy-Item -LiteralPath $fake -Destination $builtInstaller
    & $publisher -InstallerPath $builtInstaller -Version $builtVersion -ReleaseNotes "Notes for $builtVersion" -ManifestOnly -ManifestOutput (Join-Path $builtFolder 'latest.json')
}
$incomplete = Join-Path $builds '3.0.0.0'
New-Item -ItemType Directory -Path $incomplete | Out-Null
Copy-Item -LiteralPath $fake -Destination (Join-Path $incomplete 'YISP_Setup_3.0.0.0.exe')
# The newest timestamp must not beat the highest numeric version.
[IO.File]::SetLastWriteTime((Join-Path $builds '2.0.9.0\YISP_Setup_2.0.9.0.exe'),(Get-Date).AddDays(1))
& $publisher -ReleaseDirectory $builds -UpdateFolder $autoFeed -WhatIf
if (@(Get-ChildItem -LiteralPath $autoFeed -Force).Count) { throw 'Publication preview changed the feed.' }
# Exercise the double-click launcher with an inherited PowerShell 7 module path.
$launcher = Join-Path $repo 'Yakult.Inventory.App\Installer\Publish-DesktopInstaller.cmd'
$launcherArguments = '/d /s /c ""' + $launcher + '" -ReleaseDirectory "' + $builds + '" -UpdateFolder "' + $autoFeed + '" -WhatIf < NUL"'
$startInfo = [Diagnostics.ProcessStartInfo]::new($env:ComSpec,$launcherArguments)
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.EnvironmentVariables['PSModulePath'] = Join-Path $env:ProgramFiles 'PowerShell\7\Modules'
$launcherProcess = [Diagnostics.Process]::new()
try {
    $launcherProcess.StartInfo = $startInfo
    [void]$launcherProcess.Start()
    $launcherOutput = $launcherProcess.StandardOutput.ReadToEnd()
    $launcherError = $launcherProcess.StandardError.ReadToEnd()
    $launcherProcess.WaitForExit()
    if ($launcherProcess.ExitCode -ne 0 -or $launcherOutput -notlike '*Detected built Yakult release: 2.0.10.0*') { throw "CMD publication preview failed: $launcherOutput $launcherError" }
} finally { $launcherProcess.Dispose() }
if (@(Get-ChildItem -LiteralPath $autoFeed -Force).Count) { throw 'CMD publication preview changed the feed.' }
& $publisher -ReleaseDirectory $builds -UpdateFolder $autoFeed
$autoManifest = Join-Path $autoFeed 'latest.json'
$autoEnvelope = Get-Content -LiteralPath $autoManifest -Raw | ConvertFrom-Json
$autoRelease = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($autoEnvelope.payload)) | ConvertFrom-Json
if ($autoRelease.version -ne '2.0.10.0' -or $autoRelease.releaseNotes -ne 'Notes for 2.0.10.0') { throw 'Automatic publication selected the wrong version or lost the built release notes.' }
if ((Get-FileHash -LiteralPath (Join-Path $autoFeed $autoRelease.installer)).Hash -ne (Get-FileHash -LiteralPath $fake).Hash) { throw 'Automatic publication copied the wrong installer.' }
$autoHash = (Get-FileHash -LiteralPath $autoManifest).Hash
$autoWriteTime = (Get-Item -LiteralPath $autoManifest).LastWriteTimeUtc
& $publisher -ReleaseDirectory $builds -UpdateFolder $autoFeed
& $publisher -InstallerPath (Join-Path $builds '2.0.10.0\YISP_Setup_2.0.10.0.exe') -UpdateFolder $autoFeed
if ((Get-FileHash -LiteralPath $autoManifest).Hash -ne $autoHash -or (Get-Item -LiteralPath $autoManifest).LastWriteTimeUtc -ne $autoWriteTime -or (Test-Path -LiteralPath (Join-Path $autoFeed 'latest.previous.json'))) { throw 'Repeated publication changed an existing release.' }
& $publisher -InstallerPath $fake -Version '2.0.11.0' -UpdateFolder $autoFeed
$newerHash = (Get-FileHash -LiteralPath $autoManifest).Hash
& $publisher -ReleaseDirectory $builds -UpdateFolder $autoFeed
if ((Get-FileHash -LiteralPath $autoManifest).Hash -ne $newerHash) { throw 'Automatic publication downgraded a newer feed.' }
$selectedInstaller = Join-Path $builds '2.0.10.0\YISP_Setup_2.0.10.0.exe'
[IO.File]::WriteAllBytes($selectedInstaller,[byte[]]::new(513))
$failure = ''
try { & $publisher -ReleaseDirectory $builds -UpdateFolder $autoFeed } catch { $failure = $_.Exception.Message }
if ($failure -notlike '*does not match its signed build metadata*' -or (Get-FileHash -LiteralPath $autoManifest).Hash -ne $newerHash) { throw 'A changed selected build was not rejected safely.' }
Copy-Item -LiteralPath $fake -Destination $selectedInstaller -Force
$selectedManifest = Join-Path $builds '2.0.10.0\latest.json'
$originalMetadata = [IO.File]::ReadAllText($selectedManifest)
$invalidEnvelope = $originalMetadata | ConvertFrom-Json
$invalidEnvelope.signature = [Convert]::ToBase64String([byte[]]::new(384))
$invalidEnvelope | ConvertTo-Json | Set-Content -LiteralPath $selectedManifest -Encoding UTF8
$failure = ''
try { & $publisher -ReleaseDirectory $builds -UpdateFolder $autoFeed } catch { $failure = $_.Exception.Message }
if ($failure -notlike '*signature is invalid*' -or (Get-FileHash -LiteralPath $autoManifest).Hash -ne $newerHash) { throw 'An invalid selected build signature was not rejected safely.' }
[IO.File]::WriteAllText($selectedManifest,$originalMetadata)
$failure = ''
try { & $publisher -ReleaseDirectory (Join-Path $root 'no-builds') -UpdateFolder $autoFeed } catch { $failure = $_.Exception.Message }
if ($failure -notlike '*No completed installer build*') { throw 'Missing builds did not produce a useful noninteractive error.' }
$specificFeed = Join-Path $root 'specific-version-feed'
New-Item -ItemType Directory -Path $specificFeed | Out-Null
& $publisher -ReleaseDirectory $builds -Version '2.0.9.0' -UpdateFolder $specificFeed
if (-not (Test-Path -LiteralPath (Join-Path $specificFeed 'YISP_Setup_2.0.9.0.exe'))) { throw 'Explicit version selection did not find its completed build.' }
Write-Host 'Automatic release discovery, preview, numeric ordering, saved notes, repeat publication and tamper protection passed.'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
& $msbuild (Join-Path $repo 'tests\DesktopUpdateChecks\DesktopUpdateChecks.csproj') /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'Could not build updater checks.' }
& (Join-Path $repo 'tests\DesktopUpdateChecks\bin\Release\DesktopUpdateChecks.exe') (Join-Path $root 'latest.json')
if ($LASTEXITCODE -ne 0) { throw 'Updater checks failed.' }
Write-Host 'Atomic feed publication and downgrade protection passed in a local fixture folder.'
& (Join-Path $PSScriptRoot 'Test-DesktopReleasePackage.ps1') -FixtureRoot $root
