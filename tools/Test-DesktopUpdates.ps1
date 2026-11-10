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
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
& $msbuild (Join-Path $repo 'tests\DesktopUpdateChecks\DesktopUpdateChecks.csproj') /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'Could not build updater checks.' }
& (Join-Path $repo 'tests\DesktopUpdateChecks\bin\Release\DesktopUpdateChecks.exe') (Join-Path $root 'latest.json')
if ($LASTEXITCODE -ne 0) { throw 'Updater checks failed.' }
Write-Host 'Atomic feed publication and downgrade protection passed in a local fixture folder.'
& (Join-Path $PSScriptRoot 'Test-DesktopReleasePackage.ps1') -FixtureRoot $root
