<#
.SYNOPSIS
    One-click publish and deploy for the Yakult Systems Portal.

.DESCRIPTION
    Builds the portal via dotnet publish, stops IIS, copies output to the site's
    physical path, and restarts IIS.

.PARAMETER Server
    IP or hostname of the IIS server. Default: 192.168.100.186

.PARAMETER SitePath
    Physical path of the IIS site on the server. Default: C:\inetpub\wwwroot\Yakult.SystemsPortal

.PARAMETER AppPoolName
    IIS Application Pool name to recycle. Default: YakultPortal

.PARAMETER LocalDeploy
    Use this switch when running the script directly on the IIS server.

.PARAMETER SkipBuild
    Skip dotnet publish and use existing publish/ folder.

.PARAMETER LocalConfigPath
    Path to the server/local-only appsettings.Local.json file to include in the
    published output. Default: <portal root>\appsettings.Local.json

.PARAMETER SkipLocalConfig
    Deploy without appsettings.Local.json. Only use this when the IIS site
    provides ConnectionStrings__DefaultConnection via environment variables.

.PARAMETER PortalUrl
    URL to check after deployment. Default: http://<Server>:7016

.PARAMETER RuntimeStatePath
    Writable machine-level path for encrypted Data Protection keys and portal state.
    Default: C:\ProgramData\Yakult\SystemsPortal

.EXAMPLE
    .\Publish-Portal.ps1
    Build locally, deploy to 192.168.100.186, recycle app pool

.EXAMPLE
    .\Publish-Portal.ps1 -LocalDeploy
    Run directly on the server in one shot
#>

[CmdletBinding()]
param(
    [string] $Server = "192.168.100.186",
    [string] $SitePath = "C:\inetpub\wwwroot\Yakult.SystemsPortal",
    [string] $AppPoolName = "Yakult.SystemPortal",
    [string] $LocalConfigPath,
    [string] $PortalUrl,
    [ValidateSet("Yakult_Inventory_System_DEV", "YIMS_PROD")]
    [string] $DatabaseName,
    [string] $RuntimeStatePath = "C:\ProgramData\Yakult\SystemsPortal",
    [switch] $LocalDeploy,
    [switch] $SkipBuild,
    [switch] $SkipLocalConfig
)

$ErrorActionPreference = "Stop"
$portalRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($LocalConfigPath)) {
    $LocalConfigPath = Join-Path $portalRoot "appsettings.Local.json"
}
if ([string]::IsNullOrWhiteSpace($PortalUrl)) {
    # IIS binding used by the deployed Yakult Portal.
    $PortalUrl = "http://$($Server):7016"
}

function Write-Step($text) { Write-Host "`n>> $text" -ForegroundColor Cyan }
function Write-Ok($text) { Write-Host "   [OK] $text" -ForegroundColor Green }
function Write-Err($text) { Write-Host "   [FAIL] $text" -ForegroundColor Red }
function Pause-And-Exit([int] $code) {
    Write-Host ""
    Write-Host "Closing in 4 seconds..." -ForegroundColor DarkGray
    Start-Sleep -Seconds 4
    exit $code
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Yakult Portal - Publish and Deploy" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Portal root : $portalRoot"
if (-not $LocalDeploy) {
    Write-Host "  Server      : $Server"
    Write-Host "  Remote path : \\$Server\$($SitePath.Replace(':','$'))"
}
Write-Host "  App Pool    : $AppPoolName"
Write-Host "  Runtime path: $RuntimeStatePath"
Write-Host "  Local config: $LocalConfigPath"
Write-Host "  Health URL  : $PortalUrl"
Write-Host ""

# ---- Build ----
if (-not $SkipBuild) {
    Write-Step "Publishing portal (Release)"
    $publishDir = Join-Path $portalRoot "publish"

    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }

    Push-Location $portalRoot
    try {
        dotnet publish -c Release -o publish --self-contained false
        if ($LASTEXITCODE -ne 0) {
            Write-Err "dotnet publish failed with exit code $LASTEXITCODE"
            Pause-And-Exit 1
        }
    } finally {
        Pop-Location
    }

    Write-Ok "Publish complete -> $publishDir"
} else {
    $publishDir = Join-Path $portalRoot "publish"
    if (-not (Test-Path $publishDir)) {
        Write-Err "publish/ folder not found. Run without -SkipBuild first."
        Pause-And-Exit 1
    }
    Write-Ok "Using existing publish folder"
}

# ---- Include Local Configuration ----
if (-not $SkipLocalConfig) {
    Write-Step "Including local app configuration"

    if (-not (Test-Path $LocalConfigPath)) {
        Write-Err "Local config not found: $LocalConfigPath"
        Write-Host ""
        Write-Host "Create appsettings.Local.json beside the project file, or pass -LocalConfigPath." -ForegroundColor Yellow
        Write-Host "Use appsettings.Local.example.json as the template." -ForegroundColor Yellow
        Write-Host "If IIS provides ConnectionStrings__DefaultConnection instead, rerun with -SkipLocalConfig." -ForegroundColor Yellow
        Pause-And-Exit 1
    }

    Copy-Item -Path $LocalConfigPath -Destination (Join-Path $publishDir "appsettings.Local.json") -Force

    # Ask for the target database only. Server, user, password, and other
    # connection settings remain in the protected/local configuration file.
    if ([string]::IsNullOrWhiteSpace($DatabaseName)) {
        do {
            $DatabaseName = (Read-Host "Enter target database (Yakult_Inventory_System_DEV or YIMS_PROD)").Trim()
            if ($DatabaseName -notin @("Yakult_Inventory_System_DEV", "YIMS_PROD")) {
                Write-Warning "Invalid database. Enter exactly Yakult_Inventory_System_DEV or YIMS_PROD."
                $DatabaseName = $null
            }
        } while ([string]::IsNullOrWhiteSpace($DatabaseName))
    }

    $publishedLocalConfigPath = Join-Path $publishDir "appsettings.Local.json"
    try {
        $localConfig = Get-Content -LiteralPath $publishedLocalConfigPath -Raw | ConvertFrom-Json
        $currentConnectionString = [string]$localConfig.ConnectionStrings.DefaultConnection
        if ([string]::IsNullOrWhiteSpace($currentConnectionString)) {
            throw "ConnectionStrings:DefaultConnection is empty in $LocalConfigPath"
        }

        # Replace only Database/Initial Catalog; preserve server, credentials,
        # encryption, timeout, and all other deployment-specific settings.
        $databasePattern = '(?i)(^|;)\s*(Database|Initial Catalog)\s*=\s*[^;]*'
        if (-not [regex]::IsMatch($currentConnectionString, $databasePattern)) {
            throw "DefaultConnection does not contain a Database or Initial Catalog property."
        }
        $updatedConnectionString = [regex]::Replace(
            $currentConnectionString,
            $databasePattern,
            { param($match) "$($match.Groups[1].Value)Database=$DatabaseName" })
        $localConfig.ConnectionStrings.DefaultConnection = $updatedConnectionString

        $localConfig | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $publishedLocalConfigPath -Encoding UTF8
        Write-Ok "Configured portal database: $DatabaseName"
    } catch {
        Write-Err "Could not configure the selected database: $_"
        Pause-And-Exit 1
    }

    Write-Ok "Copied and configured appsettings.Local.json in publish output"

} else {
    Write-Warning "Skipping appsettings.Local.json. IIS must provide ConnectionStrings__DefaultConnection."
}

if ($LocalDeploy) {
    Write-Step "Stopping IIS App Pool: $AppPoolName"
    & appcmd stop apppool /apppool.name:$AppPoolName 2>$null
    if ($LASTEXITCODE -ne 0) {
        Import-Module WebAdministration -ErrorAction Stop
        Stop-WebAppPool -Name $AppPoolName -ErrorAction Stop
    }
    Write-Ok "App pool stopped locally"
    Start-Sleep -Seconds 2
} else {
    Write-Step "Skipping remote IIS management (WinRM not available)"
    Write-Warning "You must manually recycle the app pool on $Server after deploy"
}

# ---- Copy Files ----
Write-Step "Deploying files"

$remoteShare = "\\$Server\$($SitePath.Replace(':','$'))"

try {
    if (-not (Test-Path $remoteShare)) {
        New-Item -ItemType Directory -Path $remoteShare -Force | Out-Null
    }
    # Use robocopy for speed. /MIR protects portal-managed files from stale
    # leftovers, but server-preserved directories must be excluded from the
    # mirror. The installer folder may contain files maintained on the IIS
    # server, so copying it would overwrite files and mirroring it could delete
    # files that are not present in the repository publish output. The media
    # tree is preserved as a whole; employee-resources is also listed explicitly
    # so its uploaded PDFs and other attachments remain protected if the broader
    # media exclusion is ever changed.
    $serverMediaDirectory = Join-Path $publishDir "wwwroot\media"
    $employeeResourcesDirectory = Join-Path $publishDir "wwwroot\media\employee-resources"
    $excludedServerDirectories = @(
        $serverMediaDirectory,
        $employeeResourcesDirectory,
        (Join-Path $publishDir "installer")
    )
    $robocopyArgs = @($publishDir, $remoteShare, "/E", "/NJH", "/NJS", "/NP", "/R:3", "/W:5", "/MIR", "/XD") + $excludedServerDirectories
    & robocopy $robocopyArgs 2>&1 | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "Robocopy failed with exit code $LASTEXITCODE"
    }

    # /XD protects uploaded content that is already on the IIS server. It cannot
    # recreate a directory deleted by an older /MIR deployment, so establish the
    # writable Employee Resources root after every successful deployment.
    $remoteMediaDirectory = Join-Path $remoteShare "wwwroot\media"
    $remoteEmployeeResourcesDirectory = Join-Path $remoteMediaDirectory "employee-resources"
    New-Item -ItemType Directory -Path $remoteEmployeeResourcesDirectory -Force | Out-Null

    Write-Ok "Files copied to $remoteShare"
    Write-Ok "Preserved server directory: $remoteMediaDirectory"
    Write-Ok "Ensured and preserved Employee Resources attachments: $remoteEmployeeResourcesDirectory"
    Write-Ok "Preserved server directory: $(Join-Path $remoteShare 'installer')"
} catch {
    Write-Err "Copy failed: $_"
    Pause-And-Exit 1
}

if ($LocalDeploy) {
    Write-Step "Starting IIS App Pool: $AppPoolName"
    & appcmd start apppool /apppool.name:$AppPoolName 2>$null
    if ($LASTEXITCODE -ne 0) {
        Import-Module WebAdministration -ErrorAction SilentlyContinue
        Start-WebAppPool -Name $AppPoolName
    }
    Write-Ok "App pool started locally"
}

# ---- Verify Deployment ----
Write-Step "Checking deployed portal"

$portalReady = $false
$lastError = $null
for ($attempt = 1; $attempt -le 5; $attempt++) {
    try {
        $response = Invoke-WebRequest -Uri $PortalUrl -UseBasicParsing -TimeoutSec 10 -MaximumRedirection 0 -ErrorAction Stop
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
            $portalReady = $true
            Write-Ok "Portal responded with HTTP $($response.StatusCode)"
            break
        }
        $lastError = "HTTP $($response.StatusCode)"
    } catch {
        $lastError = $_.Exception.Message
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -ge 300 -and [int]$_.Exception.Response.StatusCode -lt 400) {
            $portalReady = $true
            Write-Ok "Portal responded with HTTP $([int]$_.Exception.Response.StatusCode)"
            break
        }
    }

    if ($attempt -lt 5) {
        Write-Host "   Waiting for portal... attempt $attempt/5" -ForegroundColor Yellow
        Start-Sleep -Seconds 3
    }
}

if (-not $portalReady) {
    Write-Warning "Portal check did not succeed: $lastError"
    Write-Warning "Deployment files were copied, but verify IIS/app pool and browse $PortalUrl manually."
}

Write-Host "`n========================================" -ForegroundColor Green
if ($portalReady) {
    Write-Host "Deploy complete and portal is responding!" -ForegroundColor Green
} else {
    Write-Host "Deploy complete, but portal check needs attention." -ForegroundColor Yellow
}
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Portal URL  : $PortalUrl" -ForegroundColor Cyan
Pause-And-Exit 0
