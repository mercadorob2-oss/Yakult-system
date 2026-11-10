#Requires -Version 5.1
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'Visual Studio MSBuild was not found.' }
$app = Join-Path $repo 'Yakult.Inventory.App\Yakult.Inventory.App.csproj'
$helper = Join-Path $repo 'Yakult.DesktopUpdater\Yakult.DesktopUpdater.csproj'
$root = Join-Path $repo ('artifacts\desktop-updater-build-checks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
foreach ($configuration in @('Debug','Release')) {
    # Exercise the missing-helper case instead of relying on a prior successful build.
    & $msbuild $helper /t:Clean "/p:Configuration=$configuration" /v:quiet /nologo
    if ($LASTEXITCODE -ne 0) { throw "Could not clean the $configuration helper output." }
    $output = Join-Path $root "$configuration output with spaces"
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    & $msbuild $app /t:CopyDesktopUpdater "/p:Configuration=$configuration" /p:Platform=x64 /p:BuildingInsideVisualStudio=true /p:BuildProjectReferences=false "/p:OutDir=$output/" /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "$configuration updater build/copy failed." }
    $source = Join-Path $repo "Yakult.DesktopUpdater\bin\$configuration\Yakult.DesktopUpdater.exe"
    $copied = Join-Path $output 'Yakult.DesktopUpdater.exe'
    if (-not (Test-Path -LiteralPath $copied) -or (Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $copied).Hash) {
        throw "$configuration output did not receive the helper built for that configuration."
    }
    Write-Host "PASS: fresh $configuration helper builds and copies under Visual Studio flags and a custom output path."
}
