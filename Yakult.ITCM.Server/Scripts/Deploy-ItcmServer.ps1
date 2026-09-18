<#
.SYNOPSIS
    Builds and deploys the Yakult ITCM Server to IIS.

.DESCRIPTION
    This script is a launcher that delegates to the appropriate deployment script:
    - Deploy-ItcmServer-Local.ps1  : for local IIS (direct app pool control)
    - Deploy-ItcmServer-Remote.ps1 : for remote IIS (admin share copy)

.PARAMETER Mode
    Deployment mode: "local" or "remote". Default: local

.PARAMETER Server
    Remote IIS server (only used for remote mode). Default: 192.168.100.186

.PARAMETER SkipBuild
    Switch. Skip dotnet publish and use existing publish output.

.PARAMETER Configuration
    Build configuration. Default: Release

.EXAMPLE
    .\Deploy-ItcmServer.ps1 -Mode local -LocalDeploy

.EXAMPLE
    .\Deploy-ItcmServer.ps1 -Mode remote
#>

param(
    [ValidateSet("local", "remote")]
    [string]$Mode          = "local",
    [string]$Server        = "192.168.100.186",
    [switch]$SkipBuild,
    [string]$Configuration = "Release"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

if ($Mode -eq "local") {
    $localScript = Join-Path $scriptDir "Deploy-ItcmServer-Local.ps1"
    if (-not (Test-Path $localScript)) { throw "Missing: $localScript" }

    $argsList = @()
    if ($SkipBuild)    { $argsList += "-SkipBuild" }
    if ($Configuration -ne "Release") { $argsList += "-Configuration"; $argsList += $Configuration }

    Write-Host "Delegating to Deploy-ItcmServer-Local.ps1 ..." -ForegroundColor Cyan
    & $localScript @argsList
    exit $LASTEXITCODE
}
else {
    $remoteScript = Join-Path $scriptDir "Deploy-ItcmServer-Remote.ps1"
    if (-not (Test-Path $remoteScript)) { throw "Missing: $remoteScript" }

    $argsList = @()
    $argsList += "-Server"; $argsList += $Server
    if ($SkipBuild)    { $argsList += "-SkipBuild" }
    if ($Configuration -ne "Release") { $argsList += "-Configuration"; $argsList += $Configuration }

    Write-Host "Delegating to Deploy-ItcmServer-Remote.ps1 ..." -ForegroundColor Cyan
    & $remoteScript @argsList
    exit $LASTEXITCODE
}
