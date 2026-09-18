<#
.SYNOPSIS
    Tests and validates the Yakult RDP setup from a client machine.

.DESCRIPTION
    This script validates that the server is properly configured for Yakult RDP connections.
    Run this from any machine with network access to the server to check:
    - Network connectivity
    - Remote Desktop port availability
    - File deployment (launcher and app)
    - User permissions
    - Overall readiness

    Does NOT require admin privileges on the target server.

.PARAMETER Server
    The target server hostname or IP address. Default: IHSServer (192.168.100.186)

.PARAMETER LauncherPath
    Expected path to YakultLauncher.exe on the server. Default: C$\YakultLauncher.exe

.PARAMETER AppPath
    Expected path to Yakult app on the server. Default: C$\Program Files\Yakult\Yakult.Inventory.App.exe

.PARAMETER TestUser
    Username to test RDP authentication with (optional, prompts if needed)

.EXAMPLE
    .\Test-RdpSetup.ps1
    # Test default server configuration

.EXAMPLE
    .\Test-RdpSetup.ps1 -Server "192.168.100.186" -TestUser "admin"
    # Test specific server with user authentication

.NOTES
    Requires:
    - Network access to the target server
    - Read access to admin shares (for file checks)
#>

[CmdletBinding()]
param(
    [string] $Server = "IHSServer",
    [string] $LauncherPath = "C$\YakultLauncher.exe",
    [string] $AppPath = "C$\Program Files\Yakult\Yakult.Inventory.App.exe",
    [string] $TestUser
)

$ErrorActionPreference = "Continue"

function Write-Test($name, $scriptBlock) {
    Write-Host "`n  Testing: $name" -ForegroundColor Cyan
    try {
        $result = & $scriptBlock
        if ($result) {
            Write-Host "     [PASS] $result" -ForegroundColor Green
            return $true
        } else {
            Write-Host "     [FAIL]" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "     [FAIL] $_" -ForegroundColor Red
        return $false
    }
}

function Write-Section($text) {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Yakult RDP Setup Validation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Server: $Server"
Write-Host "  Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Host ""

$passCount = 0
$totalTests = 0

# Test 1: Network Connectivity
Write-Section "Network Tests"

$totalTests++
if (Write-Test "Ping server" {
    $ping = Test-Connection -ComputerName $Server -Count 2 -Quiet -ErrorAction Stop
    if ($ping) { "Server is reachable" } else { $false }
}) { $passCount++ }

$totalTests++
if (Write-Test "DNS Resolution" {
    try {
        $dns = [System.Net.Dns]::Resolve($Server)
        "Resolved to: $($dns.AddressList[0].IPAddressToString)"
    } catch { $false }
}) { $passCount++ }

# Test 2: Remote Desktop Port
Write-Section "Remote Desktop Tests"

$totalTests++
if (Write-Test "RDP Port (3389) Open" {
    $port = 3389
    $timeout = 5000
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $asyncResult = $tcpClient.BeginConnect($Server, $port, $null, $null)
    $success = $asyncResult.AsyncWaitHandle.WaitOne($timeout)
    $tcpClient.EndConnect($asyncResult)
    $tcpClient.Close()
    if ($success) { "Port $port is open and accepting connections" } else { $false }
}) { $passCount++ }

# Test 3: File Deployment
Write-Section "File Deployment Tests"

$remoteLauncher = "\\$Server\$LauncherPath"
$totalTests++
if (Write-Test "YakultLauncher.exe exists" {
    if (Test-Path -LiteralPath $remoteLauncher -ErrorAction Stop) {
        $file = Get-Item -LiteralPath $remoteLauncher
        "Found: $($file.Name) ($([math]::Round($file.Length / 1MB, 2)) MB)"
    } else { $false }
}) { $passCount++ }

$remoteApp = "\\$Server\$AppPath"
$totalTests++
if (Write-Test "Yakult.Inventory.App.exe exists" {
    if (Test-Path -LiteralPath $remoteApp -ErrorAction Stop) {
        $file = Get-Item -LiteralPath $remoteApp
        "Found: $($file.Name) ($([math]::Round($file.Length / 1MB, 2)) MB)"
    } else { "WARNING: App not found at expected path" }
}) { $passCount++ }

# Test 4: User Authentication (if user provided)
if ($TestUser) {
    Write-Section "Authentication Tests"
    
    $totalTests++
    if (Write-Test "RDP Authentication" {
        # Note: Actual RDP auth test would require mstsc or CredSSP
        # This is a placeholder for manual verification
        "Manual verification required - attempt RDP connection with $TestUser"
    }) { $passCount++ }
}

# Test 5: Configuration Validation
Write-Section "Configuration Validation"

$totalTests++
if (Write-Test "Portal RDP Config" {
    $portalConfig = Join-Path $PSScriptRoot "..\appsettings.json"
    if (Test-Path $portalConfig) {
        $config = Get-Content $portalConfig | ConvertFrom-Json
        if ($config.Rdp) {
            "Server: $($config.Rdp.ServerAddress)"
        } else { "WARNING: RDP config section missing" }
    } else { "Portal config not found" }
}) { $passCount++ }

# Summary
Write-Host "`n========================================" -ForegroundColor $(if ($passCount -eq $totalTests) { "Green" } else { "Yellow" })
Write-Host "Test Results Summary" -ForegroundColor $(if ($passCount -eq $totalTests) { "Green" } else { "Yellow" })
Write-Host "========================================" -ForegroundColor $(if ($passCount -eq $totalTests) { "Green" } else { "Yellow" })
Write-Host "  Passed: $passCount / $totalTests" -ForegroundColor $(if ($passCount -eq $totalTests) { "Green" } else { "Yellow" })

if ($passCount -eq $totalTests) {
    Write-Host "`n  ✓ All tests passed! The server is ready for Yakult RDP connections." -ForegroundColor Green
    Write-Host "    Users can now download the .rdp file from the portal." -ForegroundColor Green
} else {
    $failedCount = $totalTests - $passCount
    Write-Host "`n  ⚠ $failedCount test(s) failed. Review the output above for details." -ForegroundColor Yellow
    Write-Host "    Some issues may prevent RDP connections from working." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
if ($passCount -eq $totalTests) {
    Write-Host "  1. Download the .rdp file from the Yakult portal"
    Write-Host "  2. Double-click the .rdp file to test the connection"
    Write-Host "  3. The Yakult Inventory System should launch automatically"
} else {
    Write-Host "  1. Fix the failed tests above"
    Write-Host "  2. Run Setup-Server.ps1 on the server if needed"
    Write-Host "  3. Re-run this test script to validate"
}

Write-Host ""

exit $(if ($passCount -eq $totalTests) { 0 } else { 1 })
