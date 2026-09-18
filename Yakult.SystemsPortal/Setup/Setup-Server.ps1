<#
.SYNOPSIS
    Sets up the target server for Yakult RDP launcher deployment.

.DESCRIPTION
    Run this script ON the target server as Administrator to:
    1. Enable Remote Desktop
    2. Configure firewall rules
    3. Create required directories
    4. Set appropriate permissions
    5. Add users to Remote Desktop Users group
    6. Verify all prerequisites are met

    This script must be run with elevated privileges on the server itself.

.PARAMETER YakultAppPath
    Path where Yakult.Inventory.App.exe should be installed.
    Default: C:\Program Files\Yakult

.PARAMETER LauncherPath
    Path where YakultLauncher.exe is deployed.
    Default: C:\YakultLauncher.exe

.PARAMETER AllowedUsers
    User or group to add to Remote Desktop Users.
    Default: BUILTIN\Remote Desktop Users

.PARAMETER SkipRdpEnable
    If set, skip enabling Remote Desktop (useful if already enabled)

.PARAMETER SkipFirewall
    If set, skip firewall configuration

.EXAMPLE
    .\Setup-Server.ps1
    # Run with all default settings

.EXAMPLE
    .\Setup-Server.ps1 -AllowedUsers @("YAKULT\InventoryUsers", "DOMAIN\RDPUsers")
    # Add specific domain groups to Remote Desktop Users

.NOTES
    Requires: Run as Administrator on the target server
#>

[CmdletBinding()]
param(
    [string] $YakultAppPath = "C:\Program Files\Yakult",
    [string] $LauncherPath = "C:\YakultLauncher.exe",
    [string] $PortalAppPoolName = "Yakult.SystemPortal",
    [string] $PortalRuntimeStatePath = "C:\ProgramData\Yakult\SystemsPortal",
    [switch] $SkipRdpEnable,
    [switch] $SkipFirewall
)

$ErrorActionPreference = "Stop"

function Write-Step($text) {
    Write-Host "`n>> $text" -ForegroundColor Cyan
}

function Write-Ok($text) {
    Write-Host "   [OK] $text" -ForegroundColor Green
}

function Write-Warn($text) {
    Write-Host "   [!] $text" -ForegroundColor Yellow
}

function Write-Err($text) {
    Write-Host "   [FAIL] $text" -ForegroundColor Red
}

# Check for admin privileges
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Yakult Server Setup Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Err "This script must be run as Administrator"
    Write-Warn "Right-click PowerShell and select 'Run as administrator'"
    exit 1
}

Write-Ok "Running with administrative privileges"

# Step 1: Enable Remote Desktop
if (-not $SkipRdpEnable) {
    Write-Step "Enabling Remote Desktop"
    
    try {
        # Enable Remote Desktop via registry
        $rdpKey = "HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server"
        $currentValue = Get-ItemProperty -Path $rdpKey -Name "fDenyTSConnections" -ErrorAction SilentlyContinue
        
        if ($currentValue.fDenyTSConnections -eq 0) {
            Write-Ok "Remote Desktop is already enabled"
        } else {
            Set-ItemProperty -Path $rdpKey -Name "fDenyTSConnections" -Value 0 -ErrorAction Stop
            Write-Ok "Remote Desktop enabled"
        }
        
        # Enable Network Level Authentication (recommended)
        $nlaKey = "HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp"
        Set-ItemProperty -Path $nlaKey -Name "UserAuthentication" -Value 1 -ErrorAction SilentlyContinue
        Write-Ok "Network Level Authentication enabled"
        
    } catch {
        Write-Err "Failed to enable Remote Desktop: $_"
        Write-Warn "You may need to enable it manually via System Properties"
    }
} else {
    Write-Step "Skipping Remote Desktop enablement"
}

# Step 2: Configure Firewall
if (-not $SkipFirewall) {
    Write-Step "Configuring Windows Firewall"
    
    try {
        # Check if RDP rule exists
        $rdpRule = Get-NetFirewallRule -DisplayName "Remote Desktop" -ErrorAction SilentlyContinue | 
                   Where-Object { $_.Enabled -eq "True" }
        
        if ($rdpRule) {
            Write-Ok "Remote Desktop firewall rule already enabled"
        } else {
            # Enable the built-in RDP rule
            Enable-NetFirewallRule -DisplayGroup "Remote Desktop" -ErrorAction Stop
            Write-Ok "Remote Desktop firewall rule enabled"
        }
        
        # Verify port 3389 is listening
        $port = Get-NetTCPConnection -LocalPort 3389 -State Listen -ErrorAction SilentlyContinue
        if ($port) {
            Write-Ok "Port 3389 is listening"
        } else {
            Write-Warn "Port 3389 is not listening (Remote Desktop service may need to be restarted)"
        }
        
    } catch {
        Write-Err "Failed to configure firewall: $_"
        Write-Warn "You may need to configure firewall manually"
    }
} else {
    Write-Step "Skipping firewall configuration"
}

# Step 3: Create directories
Write-Step "Creating required directories"

try {
    # Create Yakult app directory
    if (-not (Test-Path -LiteralPath $YakultAppPath -PathType Container)) {
        New-Item -Path $YakultAppPath -ItemType Directory -Force -ErrorAction Stop | Out-Null
        Write-Ok "Created directory: $YakultAppPath"
    } else {
        Write-Ok "Directory already exists: $YakultAppPath"
    }
    
    # Set permissions on Yakult directory (allow Read & Execute for everyone)
    $acl = Get-Acl -Path $YakultAppPath
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        "Everyone", 
        "ReadAndExecute", 
        "ContainerInherit,ObjectInherit", 
        "None", 
        "Allow"
    )
    $acl.AddAccessRule($rule)
    Set-Acl -Path $YakultAppPath -AclObject $acl -ErrorAction SilentlyContinue
    Write-Ok "Set Read & Execute permissions on $YakultAppPath"
    
} catch {
    Write-Err "Failed to create/configure directories: $_"
}

# Step 3b: Prepare encrypted portal runtime storage for IIS
Write-Step "Preparing Yakult Portal runtime storage"

try {
    if (-not (Test-Path -LiteralPath $PortalRuntimeStatePath -PathType Container)) {
        New-Item -Path $PortalRuntimeStatePath -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }

    $portalIdentity = "IIS AppPool\$PortalAppPoolName"
    $runtimeAcl = Get-Acl -Path $PortalRuntimeStatePath
    $runtimeRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $portalIdentity,
        "Modify",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow"
    )
    $runtimeAcl.SetAccessRule($runtimeRule)
    Set-Acl -Path $PortalRuntimeStatePath -AclObject $runtimeAcl -ErrorAction Stop
    Write-Ok "Granted Modify permission to $portalIdentity on $PortalRuntimeStatePath"
} catch {
    Write-Err "Failed to prepare portal runtime storage: $_"
    Write-Warn "The portal may start with temporary storage, but encrypted profiles will not survive restarts until this permission is fixed."
}

# Step 4: Verify launcher location
Write-Step "Checking launcher deployment"

$launcherFolder = Split-Path $LauncherPath -Parent
if (-not (Test-Path -LiteralPath $launcherFolder -PathType Container)) {
    Write-Warn "Launcher parent directory does not exist: $launcherFolder"
    Write-Warn "Run Deploy-Launcher.ps1 from the development machine first"
} else {
    Write-Ok "Launcher directory exists: $launcherFolder"
    
    if (Test-Path -LiteralPath $LauncherPath -PathType Leaf) {
        $launcherFile = Get-Item -LiteralPath $LauncherPath
        Write-Ok "YakultLauncher.exe found ($([math]::Round($launcherFile.Length / 1MB, 2)) MB)"
    } else {
        Write-Warn "YakultLauncher.exe not found at: $LauncherPath"
        Write-Warn "Run Deploy-Launcher.ps1 from the development machine"
    }
}

# Step 5: Add users to Remote Desktop Users group
Write-Step "Configuring Remote Desktop user access"

foreach ($user in $AllowedUsers) {
    try {
        # Check if user/group exists
        $exists = Get-LocalUser -Name $user -ErrorAction SilentlyContinue
        if (-not $exists) {
            $exists = Get-LocalGroup -Name $user -ErrorAction SilentlyContinue
        }
        
        if (-not $exists) {
            Write-Warn "User/Group not found: $user (skipping)"
            continue
        }
        
        # Add to Remote Desktop Users group
        Add-LocalGroupMember -Group "Remote Desktop Users" -Member $user -ErrorAction Stop
        Write-Ok "Added $user to Remote Desktop Users group"
        
    } catch {
        if ($_.Exception.Message -like "*already a member*") {
            Write-Ok "$user is already a member of Remote Desktop Users"
        } else {
            Write-Warn "Failed to add ${user}: $_"
        }
    }
}

# Step 6: Verify prerequisites
Write-Step "Verifying prerequisites"

$allGood = $true

# Check Remote Desktop is enabled
$rdpEnabled = (Get-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server" -Name "fDenyTSConnections").fDenyTSConnections
if ($rdpEnabled -eq 0) {
    Write-Ok "Remote Desktop is enabled"
} else {
    Write-Err "Remote Desktop is NOT enabled"
    $allGood = $false
}

# Check firewall
$firewallRule = Get-NetFirewallRule -DisplayGroup "Remote Desktop" -ErrorAction SilentlyContinue | 
                Where-Object { $_.Enabled -eq "True" }
if ($firewallRule) {
    Write-Ok "Firewall rule is enabled"
} else {
    Write-Warn "Firewall rule may not be configured"
}

# Check port 3389
$portListening = Get-NetTCPConnection -LocalPort 3389 -State Listen -ErrorAction SilentlyContinue
if ($portListening) {
    Write-Ok "Port 3389 is listening"
} else {
    Write-Err "Port 3389 is NOT listening"
    $allGood = $false
}

# Check launcher exists
if (Test-Path -LiteralPath $LauncherPath -PathType Leaf) {
    Write-Ok "YakultLauncher.exe is deployed"
} else {
    Write-Warn "YakultLauncher.exe is NOT deployed"
}

# Summary
Write-Host "`n========================================" -ForegroundColor $(if ($allGood) { "Green" } else { "Yellow" })
Write-Host "Setup Summary" -ForegroundColor $(if ($allGood) { "Green" } else { "Yellow" })
Write-Host "========================================" -ForegroundColor $(if ($allGood) { "Green" } else { "Yellow" })

if ($allGood) {
    Write-Host "  Status: READY" -ForegroundColor Green
    Write-Host ""
    Write-Host "The server is ready for Yakult RDP connections." -ForegroundColor Green
    Write-Host "Users can now download the .rdp file from the portal." -ForegroundColor Green
} else {
    Write-Host "  Status: NEEDS ATTENTION" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Some prerequisites are not met. Please review the output above." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Install Yakult.Inventory.App.exe to $YakultAppPath"
Write-Host "  2. Run Test-RdpSetup.ps1 to validate the complete setup"
Write-Host "  3. Test the .rdp file from a client machine"
Write-Host ""

exit $(if ($allGood) { 0 } else { 1 })
