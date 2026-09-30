<#
.SYNOPSIS
    Run on your PC. Builds the gateway and packs it into one zip to copy to the server.

.DESCRIPTION
    Output: Yakult.Inventory.Gateway\bin\Yakult.Inventory.Gateway.zip
    The zip contains the site files plus Install-Gateway-OnServer.ps1.
    It never contains appsettings.Local.json (your dev connection stays on your PC).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Yakult.Inventory.Gateway\Scripts\Publish-Gateway.ps1
#>

$ErrorActionPreference = "Stop"
$projectDir = Split-Path $PSScriptRoot -Parent
$publishDir = Join-Path $projectDir "bin\Release\net8.0\publish"
$zipPath    = Join-Path $projectDir "bin\Yakult.Inventory.Gateway.zip"

Write-Host "Publishing gateway (Release)..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish (Join-Path $projectDir "Yakult.Inventory.Gateway.csproj") -c Release -o $publishDir --self-contained false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit code $LASTEXITCODE)." }

# Belt and braces: a dev connection string must never reach the server package.
Get-ChildItem $publishDir -Filter "appsettings.Local*.json" | Remove-Item -Force

Copy-Item (Join-Path $PSScriptRoot "Install-Gateway-OnServer.ps1") $publishDir

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

Write-Host ""
Write-Host "Done: $zipPath" -ForegroundColor Green
Write-Host "Copy this zip to the server, extract it, and run Install-Gateway-OnServer.ps1 as Administrator."
