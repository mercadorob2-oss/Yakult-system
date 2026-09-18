<#
.SYNOPSIS
    One-click publish and deploy for Yakult ITCM Server.

.DESCRIPTION
    Builds the ITCM Server via dotnet publish, copies output to the IIS site's
    physical path on the remote server via admin share, then recycles the app pool.

.PARAMETER Server
    IP or hostname of the IIS server. Default: 192.168.100.186

.PARAMETER SitePath
    Physical path of the IIS site on the server. Default: C:\inetpub\wwwroot\Yakult.ITCM.Scheduler

.PARAMETER AppPoolName
    IIS Application Pool name to recycle. Default: Yakult.ITCM.Scheduler

.PARAMETER LocalDeploy
    Use this switch when running the script directly on the IIS server.

.PARAMETER SkipBuild
    Skip dotnet publish and use existing publish/ folder.

.PARAMETER PortalUrl
    Base URL of the deployed site. Default: http://<Server>:7017
    (port 7017 is the live IIS site; 50330 was the old Kestrel dev port and
    nothing listens there anymore).

.PARAMETER NoPause
    Do not wait for ESC at the end. Useful for automated runs.

.EXAMPLE
    .\Publish-ItcmServer.ps1
    Build locally, deploy to 192.168.100.186, recycle app pool

.EXAMPLE
    .\Publish-ItcmServer.ps1 -LocalDeploy
    Run directly on the server in one shot
#>

[CmdletBinding()]
param(
    [string] $Server = "192.168.100.186",
    [string] $SitePath = "C:\inetpub\wwwroot\Yakult.ITCM.Scheduler",
    [string] $AppPoolName = "Yakult.ITCM.Scheduler",
    [string] $PortalUrl,
    [switch] $LocalDeploy,
    [switch] $SkipBuild,
    [switch] $NoPause
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($PortalUrl)) {
    $PortalUrl = "http://$($Server):7017"
}

function Write-Step($text) { Write-Host "`n>> $text" -ForegroundColor Cyan }
function Write-Ok($text) { Write-Host "   [OK] $text" -ForegroundColor Green }
function Write-Err($text) { Write-Host "   [FAIL] $text" -ForegroundColor Red }
function Pause-And-Exit([int] $code) {
    if ($NoPause) { exit $code }
    Write-Host ""
    Write-Host "Press ESC to exit..." -ForegroundColor DarkGray
    do {
        $key = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    } while ($key.VirtualKeyCode -ne 27)
    exit $code
}

function Ensure-DataProtectionAccess {
    param(
        [string] $TargetServer,
        [string] $PoolName,
        [switch] $RunLocally
    )

    $keyPath = "C:\ProgramData\Yakult\ITCM\DataProtection-Keys"
    $identity = "IIS AppPool\$PoolName"
    $remoteKeyPath = "\\$TargetServer\C$\ProgramData\Yakult\ITCM\DataProtection-Keys"

    $configureKeyPath = {
        param($Path, $AppPool)

        $ErrorActionPreference = "Stop"
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
        $identityName = "IIS AppPool\$AppPool"
        $acl = Get-Acl -Path $Path
        $existing = $acl.Access | Where-Object {
            $_.IdentityReference.Value -eq $identityName -and
            $_.FileSystemRights -match "Modify|FullControl" -and
            $_.AccessControlType -eq "Allow"
        }

        if (-not $existing) {
            $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
                $identityName,
                "Modify",
                "ContainerInherit,ObjectInherit",
                "None",
                "Allow"
            )
            $acl.AddAccessRule($rule)
            Set-Acl -Path $Path -AclObject $acl
        }

        return "Data Protection key directory ready: $Path"
    }

    Write-Step "Preparing persistent Data Protection keys"
    try {
        if ($RunLocally) {
            & $configureKeyPath -Path $keyPath -AppPool $PoolName | ForEach-Object { Write-Host "   $_" }
        } else {
            try {
                # Prefer remote PowerShell because it resolves the IIS virtual
                # account on the IIS server itself.
                Invoke-Command -ComputerName $TargetServer -ScriptBlock $configureKeyPath `
                    -ArgumentList $keyPath, $PoolName -ErrorAction Stop |
                    ForEach-Object { Write-Host "   $_" }
            } catch {
                # Fall back to the existing admin-share deployment mechanism.
                # This avoids requiring WinRM when C$ access is available.
                Write-Warning "PowerShell remoting was unavailable; applying the IIS ACL through the C$ admin share."
                New-Item -ItemType Directory -Path $remoteKeyPath -Force | Out-Null
                $grant = "{0}:(OI)(CI)M" -f $identity
                $specificGrantSucceeded = $false
                try {
                    # Native icacls can throw under PowerShell's stop-on-error
                    # behavior when the remote account name cannot be resolved.
                    # Convert that exception into a normal fallback condition.
                    & icacls $remoteKeyPath /grant $grant /T /C 2>&1 | Out-Null
                    $specificGrantSucceeded = ($LASTEXITCODE -eq 0)
                } catch {
                    $specificGrantSucceeded = $false
                }

                if (-not $specificGrantSucceeded) {
                    # Resolving an IIS virtual account through a UNC path can
                    # fail when the publishing workstation has a broken domain
                    # trust. The built-in IIS_IUSRS SID is local to Windows and
                    # avoids that name-resolution dependency. It is broader
                    # than the app-pool-specific grant, so use it only as a
                    # compatibility fallback and report it clearly.
                    Write-Warning "The app-pool identity could not be resolved remotely; falling back to the built-in IIS_IUSRS group."
                    $iisUsersGrant = "*S-1-5-32-568:(OI)(CI)M"
                    $iisUsersGrantSucceeded = $false
                    try {
                        & icacls $remoteKeyPath /grant $iisUsersGrant /T /C 2>&1 | Out-Null
                        $iisUsersGrantSucceeded = ($LASTEXITCODE -eq 0)
                    } catch {
                        $iisUsersGrantSucceeded = $false
                    }

                    if (-not $iisUsersGrantSucceeded) {
                        throw "Unable to grant Modify access to the IIS app pool or IIS_IUSRS on $remoteKeyPath."
                    }
                    Write-Warning "Granted key-directory Modify access to local IIS_IUSRS (SID S-1-5-32-568) on the IIS server."
                }
                Write-Ok "Data Protection key directory and IIS ACL prepared through the admin share"
            }
        }
        Write-Ok "Persistent Data Protection access is ready"
    } catch {
        throw "Data Protection access preparation failed: $($_.Exception.Message)"
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Yakult ITCM Server - Publish and Deploy" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Project dir : $scriptDir"
if (-not $LocalDeploy) {
    Write-Host "  Server      : $Server"
    Write-Host "  Remote path : \\$Server\$($SitePath.Replace(':','$'))"
}
Write-Host "  App Pool    : $AppPoolName"
Write-Host "  Health URL  : $PortalUrl"
Write-Host ""

# ---- Build ----
if (-not $SkipBuild) {
    Write-Step "Publishing ITCM Server (Release)"
    $publishDir = Join-Path $scriptDir "bin\Release\net8.0\publish"

    if (Test-Path $publishDir) {
        Remove-Item "$publishDir\*" -Recurse -Force -ErrorAction SilentlyContinue
    }

    Push-Location $scriptDir
    try {
        dotnet publish -c Release -o $publishDir --self-contained false
        if ($LASTEXITCODE -ne 0) {
            Write-Err "dotnet publish failed with exit code $LASTEXITCODE"
            Pause-And-Exit 1
        }
    } finally {
        Pop-Location
    }

    # Server-specific appsettings.local.json is intentionally not copied from
    # the developer workstation. Keep it on the IIS server and preserve it
    # during robocopy so database credentials cannot be overwritten by a local
    # publish configuration.
    Write-Ok "Server-local configuration will be preserved"

    Write-Ok "Publish complete -> $publishDir"
} else {
    $publishDir = Join-Path $scriptDir "bin\Release\net8.0\publish"
    if (-not (Test-Path $publishDir)) {
        Write-Err "publish/ folder not found. Run without -SkipBuild first."
        Pause-And-Exit 1
    }
    Write-Ok "Using existing publish folder"
}

# ---- Stop App Pool before copy (unlocks files) ----
if ($LocalDeploy) {
    Write-Step "Stopping local IIS App Pool: $AppPoolName"
    try {
        Import-Module WebAdministration -ErrorAction Stop
        Stop-WebAppPool -Name $AppPoolName -ErrorAction Stop
        Write-Ok "Local app pool stopped"
    } catch {
        Write-Warning "Could not stop app pool locally: $_"
    }
    Start-Sleep -Seconds 2
} else {
    Write-Step "Stopping remote IIS App Pool (best-effort): $AppPoolName"
    try {
        Invoke-Command -ComputerName $Server -ErrorAction Stop -ScriptBlock {
            param($PoolName)
            Import-Module WebAdministration -ErrorAction Stop
            Stop-WebAppPool -Name $PoolName -ErrorAction Stop
            "App pool '$PoolName' stopped"
        } -ArgumentList $AppPoolName | ForEach-Object { Write-Host "   $_" }
        Write-Ok "Remote app pool stopped"
        Start-Sleep -Seconds 2
    } catch {
        Write-Warning "Remote stop unavailable ($($_.Exception.Message)). Copy may hit locked files; recycle '$AppPoolName' manually if so."
    }
}

# ---- Persistent Data Protection keys ----
try {
    Ensure-DataProtectionAccess -TargetServer $Server -PoolName $AppPoolName -RunLocally:$LocalDeploy
} catch {
    Write-Err $_
    Pause-And-Exit 1
}

# ---- Copy Files ----
Write-Step "Deploying files"

$remoteShare = "\\$Server\$($SitePath.Replace(':','$'))"

try {
    if (-not (Test-Path $remoteShare)) {
        New-Item -ItemType Directory -Path $remoteShare -Force | Out-Null
    }
    $robocopyArgs = @($publishDir, $remoteShare, "/E", "/NJH", "/NJS", "/NP", "/R:3", "/W:5", "/MIR", "/XF", "appsettings.local.json")
    & robocopy $robocopyArgs 2>&1 | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "Robocopy failed with exit code $LASTEXITCODE"
    }
    Write-Ok "Files copied to $remoteShare"
    $remoteLocalConfig = Join-Path $remoteShare "appsettings.local.json"
    if (Test-Path $remoteLocalConfig) {
        Write-Ok "Preserved server-local appsettings.local.json"
    } else {
        Write-Warning "Server-local appsettings.local.json was not found. Configure the server before starting ITCM."
    }
} catch {
    Write-Err "Copy failed: $_"
    Pause-And-Exit 1
}

# ---- Database migrations (manual step reminder) ----
# The deploy never touches the database. If either table below is missing on
# the target DB, scheduler history and/or connected-client lists stay empty.
Write-Step "Database migration reminder (manual, needs DDL rights on the DB)"
$repoRoot = Split-Path $scriptDir -Parent
$migrations = @(
    (Join-Path $scriptDir "Migrations\Migration_ITCM_SchedulerHeartbeat.sql"),
    (Join-Path $repoRoot "DATABASES\Yakult-DB-Production\dbo\Scripts\Migration_ITCM_CallClientPresence.sql")
)
foreach ($m in $migrations) {
    if (Test-Path $m) {
        Write-Host "   Run once on the target DB: $m" -ForegroundColor Yellow
    } else {
        Write-Host "   Expected script not found (skipped): $m" -ForegroundColor DarkGray
    }
}

# ---- Start App Pool after copy (loads the new build) ----
if ($LocalDeploy) {
    Write-Step "Starting local IIS App Pool: $AppPoolName"
    try {
        Import-Module WebAdministration -ErrorAction Stop
        Start-WebAppPool -Name $AppPoolName -ErrorAction Stop
        Write-Ok "Local app pool started"
    } catch {
        Write-Warning "Could not start app pool locally: $_"
        Write-Warning "Start '$AppPoolName' manually in IIS Manager."
    }
    Start-Sleep -Seconds 2
} else {
    Write-Step "Starting remote IIS App Pool (best-effort): $AppPoolName"
    try {
        Invoke-Command -ComputerName $Server -ErrorAction Stop -ScriptBlock {
            param($PoolName)
            Import-Module WebAdministration -ErrorAction Stop
            Start-WebAppPool -Name $PoolName -ErrorAction Stop
            "App pool '$PoolName' started"
        } -ArgumentList $AppPoolName | ForEach-Object { Write-Host "   $_" }
        Write-Ok "Remote app pool started"
    } catch {
        Write-Warning "Remote start unavailable ($($_.Exception.Message))."
        Write-Warning "Start '$AppPoolName' manually on $Server, or the old build keeps serving."
    }
    Start-Sleep -Seconds 2
}

# ---- Verify Deployment ----
Write-Step "Checking deployed ITCM Server"

$portalReady = $false
$lastError = $null
$loginUrl = "$PortalUrl/Account/Login"
for ($attempt = 1; $attempt -le 5; $attempt++) {
    try {
        # The ITCM API is administrator-protected. Verify the deployed web
        # application through its anonymous login page instead of bypassing auth.
        $response = Invoke-WebRequest -Uri $loginUrl -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            $portalReady = $true
            Write-Ok "ITCM Server login page responded with HTTP $($response.StatusCode)"
            break
        }
        $lastError = "HTTP $($response.StatusCode)"
    } catch {
        $lastError = $_.Exception.Message
    }

    if ($attempt -lt 5) {
        Write-Host "   Waiting for server... attempt $attempt/5" -ForegroundColor Yellow
        Start-Sleep -Seconds 3
    }
}

if (-not $portalReady) {
    Write-Warning "Health check did not succeed: $lastError"
    Write-Warning "Files were copied, but verify IIS/app pool and browse $PortalUrl manually."
}

# ---- Verify the NEW build is serving (anonymous ping) ----
# /api/itcm/ping exists only in recent builds, so a 200 here proves the app
# pool recycled onto the new files. A 404 means the old build is still live.
Write-Step "Checking deployed build version"
try {
    $ping = Invoke-RestMethod -Uri "$PortalUrl/api/itcm/ping" -TimeoutSec 10 -ErrorAction Stop
    if ($ping.ok -eq $true) {
        Write-Ok "Ping OK -- build serving (scheduler enabled: $($ping.scheduler.enabled), version: $($ping.version))"
    } else {
        Write-Warning "Ping responded but payload was unexpected. Browse $PortalUrl manually."
    }
} catch {
    if ($_.Exception.Message -match "404") {
        Write-Warning "Ping returned 404: the OLD build is still serving."
        Write-Warning "Recycle the app pool '$AppPoolName' and re-run this check."
    } else {
        Write-Warning "Ping check failed: $($_.Exception.Message)"
    }
}

Write-Host "`n========================================" -ForegroundColor Green
if ($portalReady) {
    Write-Host "Deploy complete and ITCM Server is responding!" -ForegroundColor Green
} else {
    Write-Host "Deploy complete, but health check needs attention." -ForegroundColor Yellow
}
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Dashboard : $PortalUrl/" -ForegroundColor Cyan
Write-Host "  Config    : $PortalUrl/Config" -ForegroundColor Cyan
Pause-And-Exit 0
