<#
.SYNOPSIS
    Publishes the Yakult Inventory App as a RemoteApp on the local RD Connection Broker
    and authorizes user groups to launch it.

.DESCRIPTION
    Run this script on the RD Connection Broker server (typically 192.168.100.186)
    as Administrator, from an elevated PowerShell window.

    The script will:
      1. Verify that the Yakult app is installed at the expected path.
      2. Locate the RD Session Collection (or prompt if more than one exists).
      3. Publish Yakult.Inventory.App.exe as a RemoteApp named "Yakult Inventory System".
      4. Authorize the specified user groups to launch the RemoteApp.
      5. Print the final state for verification.

.PARAMETER YakultAppPath
    Full path to Yakult.Inventory.App.exe on the broker. Default: the path configured
    in the portal's appsettings.json.

.PARAMETER DisplayName
    Friendly name shown to the user in the mstsc RemoteApp picker.

.PARAMETER ConnectionBroker
    Name of the RD Connection Broker server. Default: the local computer.

.PARAMETER CollectionName
    Name of the RD Session Collection. Default: auto-detect.

.PARAMETER AllowedUserGroups
    User groups to authorize on the collection. Domain Users is added by default so
    any authenticated RDP user can launch the app. Replace with a tighter list
    (e.g. "YAKULT\Inventory Users") for least-privilege.

.PARAMETER CreateCollection
    If set and no Session Collection exists, the script creates one named
    -CollectionName on the broker with the broker itself as the only Session Host.
    Default is to print instructions and exit.

.EXAMPLE
    .\Publish-YakultRemoteApp.ps1

.EXAMPLE
    .\Publish-YakultRemoteApp.ps1 -CollectionName "Inventory" -AllowedUserGroups @("YAKULT\Domain Users")

.EXAMPLE
    .\Publish-YakultRemoteApp.ps1 -CreateCollection -CollectionName "Inventory"

.NOTES
    Requires: RunAsAdministrator, RemoteDesktop module.
    Install the module: Install-WindowsFeature RSAT-RDS-PowerShell
#>
[CmdletBinding()]
param(
    [string] $YakultAppPath   = "C:\Program Files\Yakult\Yakult.Inventory.App.exe",
    [string] $DisplayName     = "Yakult Inventory System",
    [string] $Alias           = "YakultInventory",
    [string] $ConnectionBroker = $env:COMPUTERNAME,
    [string] $CollectionName  = "Inventory",
    [switch] $CreateCollection,
    [string[]] $AllowedUserGroups = @("BUILTIN\Remote Desktop Users")
)

$ErrorActionPreference = "Stop"

function Write-Section($text) {
    Write-Host ""
    Write-Host "==== $text ====" -ForegroundColor Cyan
}

Write-Section "Yakult RemoteApp Publisher"
Write-Host "  Yakult app path : $YakultAppPath"
Write-Host "  Display name    : $DisplayName"
Write-Host "  CollectionName  : $CollectionName"
Write-Host "  Allowed groups  : $($AllowedUserGroups -join ', ')"

if ($ConnectionBroker -eq $env:COMPUTERNAME -and -not $ConnectionBroker.Contains('.')) {
    try {
        $fqdn = [System.Net.Dns]::GetHostEntry($env:COMPUTERNAME).HostName
        if ($fqdn -and $fqdn.Contains('.')) {
            $ConnectionBroker = $fqdn
            Write-Host "  Auto-detected FQDN: $ConnectionBroker" -ForegroundColor Green
        } else {
            Write-Host "  ConnectionBroker: $ConnectionBroker (no FQDN found; server may be workgroup-only)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  ConnectionBroker: $ConnectionBroker (FQDN lookup failed)" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ConnectionBroker: $ConnectionBroker"
}

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run from an elevated PowerShell. Right-click PowerShell and choose 'Run as administrator', then re-run."
    exit 1
}

if (-not (Test-Path -LiteralPath $YakultAppPath -PathType Leaf)) {
    Write-Error "Yakult app not found at: $YakultAppPath. Install the Yakult Inventory App first."
    exit 1
}

try {
    Import-Module RemoteDesktop -ErrorAction Stop
} catch {
    Write-Error "Could not load the RemoteDesktop PowerShell module. Install the RSAT-RDS-PowerShell feature: Install-WindowsFeature RSAT-RDS-PowerShell"
    exit 1
}

Write-Section "Step 1: Locate RD Session Collection"
$collections = @(Get-RDSessionCollection -ConnectionBroker $ConnectionBroker -ErrorAction SilentlyContinue)

if ($collections.Count -eq 0) {
    if ($CreateCollection) {
        if ([string]::IsNullOrWhiteSpace($CollectionName)) {
            Write-Error "-CreateCollection was set but -CollectionName is empty. Specify -CollectionName, e.g. -CollectionName 'Inventory'."
            exit 1
        }
        Write-Host "No Session Collections found. Creating '$CollectionName' on '$ConnectionBroker'..." -ForegroundColor Yellow
        try {
            New-RDSessionCollection `
                -ConnectionBroker $ConnectionBroker `
                -CollectionName $CollectionName `
                -SessionHost @($ConnectionBroker) `
                -ErrorAction Stop | Out-Null
            Write-Host "Collection created." -ForegroundColor Green
            Start-Sleep -Seconds 2
            $collections = @(Get-RDSessionCollection -ConnectionBroker $ConnectionBroker -ErrorAction SilentlyContinue)
        } catch {
            Write-Error "Failed to create collection: $_"
            exit 1
        }
    } else {
        Write-Host "No Session Collections found on $ConnectionBroker." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "You need to create one before this script can publish a RemoteApp." -ForegroundColor Yellow
        Write-Host "Either:"
        Write-Host "  1. Re-run with -CreateCollection -CollectionName 'Inventory' (the script will do it), or"
        Write-Host "  2. Open Server Manager -> Remote Desktop Services -> Collections"
        Write-Host "     -> Tasks -> Create Session Collection, name it '$CollectionName',"
        Write-Host "     and pick this server as the only Session Host."
        exit 1
    }
}

if ([string]::IsNullOrWhiteSpace($CollectionName)) {
    if ($collections.Count -eq 1) {
        $CollectionName = $collections[0].CollectionName
        Write-Host "Auto-detected collection: $CollectionName" -ForegroundColor Green
    } else {
        Write-Host "Multiple collections found. Pick one:" -ForegroundColor Yellow
        for ($i = 0; $i -lt $collections.Count; $i++) {
            Write-Host ("  [{0}] {1}" -f $i, $collections[$i].CollectionName)
        }
        $choice = Read-Host "Enter collection number"
        if ($choice -notmatch '^\d+$' -or [int]$choice -ge $collections.Count) {
            Write-Error "Invalid selection."
            exit 1
        }
        $CollectionName = $collections[[int]$choice].CollectionName
    }
} else {
    if (-not ($collections.CollectionName -contains $CollectionName)) {
        Write-Error "Collection '$CollectionName' not found. Available: $($collections.CollectionName -join ', ')"
        exit 1
    }
}
Write-Host "Using collection: $CollectionName" -ForegroundColor Green

Write-Section "Step 2: Publish RemoteApp"
$existing = Get-RDRemoteApp -ConnectionBroker $ConnectionBroker -CollectionName $CollectionName -ErrorAction SilentlyContinue |
            Where-Object { $_.FilePath -eq $YakultAppPath -or $_.Alias -eq $Alias }

if ($existing) {
    Write-Host "RemoteApp already published:" -ForegroundColor Green
    Write-Host "  DisplayName: $($existing.DisplayName)"
    Write-Host "  Alias      : $($existing.Alias)"
    Write-Host "  FilePath   : $($existing.FilePath)"
} else {
    Write-Host "Publishing '$DisplayName' to collection '$CollectionName'..."
    $publishParams = @{
        ConnectionBroker = $ConnectionBroker
        CollectionName   = $CollectionName
        DisplayName      = $DisplayName
        FilePath         = $YakultAppPath
        Alias            = $Alias
        IconPath         = $YakultAppPath
        IconIndex        = 0
    }
    try {
        New-RDRemoteApp @publishParams -ErrorAction Stop | Out-Null
        Write-Host "RemoteApp published successfully." -ForegroundColor Green
    } catch {
        Write-Error "Failed to publish RemoteApp: $_"
        exit 1
    }
}

Write-Section "Step 3: Authorize User Groups"
$currentGroups = @(Get-RDSessionCollectionUserGroup -ConnectionBroker $ConnectionBroker -CollectionName $CollectionName -ErrorAction SilentlyContinue |
                  Select-Object -ExpandProperty UserGroup)

foreach ($group in $AllowedUserGroups) {
    if ($currentGroups -contains $group) {
        Write-Host "  [OK] $group is already authorized." -ForegroundColor Green
    } else {
        try {
            Add-RDSessionCollectionUserGroup -ConnectionBroker $ConnectionBroker -CollectionName $CollectionName -UserGroup $group -ErrorAction Stop
            Write-Host "  [OK] Added $group to authorized user groups." -ForegroundColor Green
        } catch {
            Write-Warning "  [FAIL] Could not add $group : $_"
        }
    }
}

Write-Section "Step 4: Verification"
$published = Get-RDRemoteApp -ConnectionBroker $ConnectionBroker -CollectionName $CollectionName |
             Where-Object { $_.Alias -eq $Alias -or $_.FilePath -eq $YakultAppPath }
if ($published) {
    Write-Host "  [OK] Published program:" -ForegroundColor Green
    Write-Host "        DisplayName: $($published.DisplayName)"
    Write-Host "        Alias      : $($published.Alias)"
    Write-Host "        FilePath   : $($published.FilePath)"
    Write-Host "        IconPath   : $($published.IconPath)"
} else {
    Write-Error "  [FAIL] RemoteApp not found after publishing. Open Server Manager and check RemoteApp Programs manually."
}

$groups = @(Get-RDSessionCollectionUserGroup -ConnectionBroker $ConnectionBroker -CollectionName $CollectionName |
           Select-Object -ExpandProperty UserGroup)
Write-Host "  [OK] Authorized user groups on collection '$CollectionName':"
$groups | ForEach-Object { Write-Host "        - $_" }

Write-Section "Done"
Write-Host "Try downloading the .rdp file from the portal again and double-clicking it."
Write-Host ""
Write-Host "If the 'not in the list of authorized programs' error still appears:"
Write-Host "  1. Confirm the connecting user is a member of one of the groups listed above."
Write-Host "  2. Have the user sign out of RDP and back in (mstsc caches the program list)."
Write-Host "  3. Check Event Viewer -> Applications and Services Logs -> Microsoft -> Windows"
Write-Host "     -> RemoteDesktopServices-RdpCoreTS for the actual error."
Write-Host "  4. Verify 'Allow log on through Remote Desktop Services' user right includes"
Write-Host "     the group (secpol.msc -> Local Policies -> User Rights Assignment)."
