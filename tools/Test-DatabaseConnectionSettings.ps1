#Requires -Version 5.1
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$root = Join-Path $repo ('artifacts\database-connection-checks-' + [guid]::NewGuid().ToString('N'))
$output = Join-Path $root 'application'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
$log = Join-Path $root 'build.log'
& $msbuild (Join-Path $repo 'Yakult.Inventory.App\Yakult.Inventory.App.csproj') /t:Build /p:Configuration=Release /p:Platform=AnyCPU "/p:OutDir=$output/" /nologo /noconsolelogger /fileLogger "/fileLoggerParameters:LogFile=$log;Verbosity=normal"
if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $log -Tail 35; throw 'Database connection UI build failed.' }
& $msbuild (Join-Path $repo 'tests\DatabaseConnectionChecks\DatabaseConnectionChecks.csproj') /p:Configuration=Release "/p:DesktopAppPath=$(Join-Path $output 'Yakult.Inventory.App.exe')" "/p:OutDir=$output/" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'Could not build database connection checks.' }
& (Join-Path $output 'DatabaseConnectionChecks.exe') (Join-Path $root 'previews')
if ($LASTEXITCODE -ne 0) { throw 'Database connection checks failed.' }
Write-Host "Build log and synthetic UI previews: $root"
