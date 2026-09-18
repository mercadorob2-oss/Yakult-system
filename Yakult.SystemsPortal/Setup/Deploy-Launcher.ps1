<#
.SYNOPSIS
    Builds and deploys YakultLauncher.exe to the target RDP server.

.DESCRIPTION
    This script automates the build and deployment process for YakultLauncher.exe.
    It builds the launcher from source, copies it to the target server via admin share,
    and verifies the deployment.

    Run this script from the development machine (not the server).

.PARAMETER Server
    The target server hostname or IP address. Default: IHSServer (192.168.100.186)

.PARAMETER DestinationPath
    The path on the server where YakultLauncher.exe will be deployed.
    Default: C$\YakultLauncher.exe (admin share)

.PARAMETER LauncherFolder
    Path to the YakultLauncher source folder. Default: current directory's YakultLauncher subfolder

.PARAMETER Configuration
    Build configuration. Default: Release

.PARAMETER SkipBuild
    If set, skip the build step and only copy existing binaries from the publish folder.

.PARAMETER Force
    If set, overwrite the file on the server even if it already exists with the same version.

.EXAMPLE
    .\Deploy-Launcher.ps1
    # Deploy to default server (IHSServer) with default settings

.EXAMPLE
    .\Deploy-Launcher.ps1 -Server "192.168.100.186" -Force
    # Deploy to specific server, overwriting existing file

.EXAMPLE
    .\Deploy-Launcher.ps1 -SkipBuild
    # Only copy already-built binaries, don't rebuild

.NOTES
    Requires:
    - .NET 8 SDK (for building)
    - Administrative access to the target server (for admin share copy)
    - Network connectivity to the target server
#>

[CmdletBinding()]
param(
    [string] $Server = "IHSServer",
    [string] $DestinationPath = "C$\YakultLauncher.exe",
    [string] $LauncherFolder = "$PSScriptRoot\YakultLauncher",
    [string] $Configuration = "Release",
    [switch] $SkipBuild,
    [switch] $Force
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

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "YakultLauncher Deployment Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Server           : $Server"
Write-Host "  Destination      : \\$Server\$DestinationPath"
Write-Host "  Launcher Folder  : $LauncherFolder"
Write-Host "  Configuration    : $Configuration"
Write-Host ""

# Validate source folder exists
Write-Step "Checking source folder"
if (-not (Test-Path -LiteralPath $LauncherFolder -PathType Container)) {
    Write-Err "Launcher source folder not found: $LauncherFolder"
    exit 1
}
Write-Ok "Source folder exists"

# Build the launcher (unless skipped)
if (-not $SkipBuild) {
    Write-Step "Building YakultLauncher ($Configuration)"
    
    $publishOutput = Join-Path $LauncherFolder "publish"
    
    # Clean previous publish output
    if (Test-Path -LiteralPath $publishOutput -PathType Container) {
        Write-Host "   Cleaning previous build..."
        Remove-Item -LiteralPath $publishOutput -Recurse -Force -ErrorAction Stop
    }
    
    # Build command
    $buildParams = @(
        "publish"
        "-c", $Configuration
        "-o", $publishOutput
        "--self-contained"
        "true"
        "-p:PublishSingleFile=true"
        "-p:IncludeNativeLibrariesForSelfExtract=true"
    )
    
    Write-Host "   Running: dotnet $($buildParams -join ' ')"
    
    $buildResult = & dotnet $buildParams 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Build failed with exit code $LASTEXITCODE"
        $buildResult | ForEach-Object { Write-Host "   $_" -ForegroundColor Red }
        exit 1
    }
    
    Write-Ok "Build completed successfully"
    
    # Verify the output file exists
    $exePath = Join-Path $publishOutput "YakultLauncher.exe"
    if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
        Write-Err "Build succeeded but output file not found: $exePath"
        exit 1
    }
    
    $fileInfo = Get-Item -LiteralPath $exePath
    Write-Ok "Output file: $($fileInfo.Name) ($([math]::Round($fileInfo.Length / 1MB, 2)) MB)"
} else {
    Write-Step "Skipping build (using existing binaries)"
    $exePath = Join-Path $LauncherFolder "publish\YakultLauncher.exe"
    
    if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
        Write-Err "No built executable found at: $exePath"
        Write-Warn "Run without -SkipBuild to build first"
        exit 1
    }
    
    $fileInfo = Get-Item -LiteralPath $exePath
    Write-Ok "Found existing executable: $($fileInfo.Name) ($([math]::Round($fileInfo.Length / 1MB, 2)) MB)"
}

# Test connectivity to server
Write-Step "Testing connectivity to $Server"

try {
    # Try to ping the server
    $pingResult = Test-Connection -ComputerName $Server -Count 2 -ErrorAction SilentlyContinue
    
    if ($pingResult) {
        Write-Ok "Server is reachable (ping successful)"
    } else {
        Write-Warn "Ping failed (server might block ICMP), continuing with SMB test..."
    }
} catch {
    Write-Warn "Ping test failed: $_"
}

# Test admin share access
Write-Step "Testing admin share access"

$remotePath = "\\$Server\$DestinationPath"
$remoteFolder = Split-Path $remotePath -Parent

try {
    # Test if we can access the parent folder
    $folderExists = Test-Path -LiteralPath $remoteFolder -ErrorAction Stop
    
    if ($folderExists) {
        Write-Ok "Admin share access confirmed"
    } else {
        Write-Err "Cannot access admin share: $remoteFolder"
        Write-Warn "Ensure you have administrative access to $Server"
        exit 1
    }
} catch {
    Write-Err "Failed to access admin share: $_"
    Write-Warn "Ensure you have administrative access to $Server and the admin share (C$) is enabled"
    exit 1
}

# Check if file already exists
Write-Step "Checking for existing deployment"

$remoteFileExists = Test-Path -LiteralPath $remotePath -ErrorAction SilentlyContinue

if ($remoteFileExists) {
    $existingFile = Get-Item -LiteralPath $remotePath
    Write-Host "   Existing file found on server:"
    Write-Host "     Size: $([math]::Round($existingFile.Length / 1MB, 2)) MB"
    Write-Host "     Modified: $($existingFile.LastWriteTime)"
    
    # Compare sizes
    if ($existingFile.Length -eq $fileInfo.Length -and -not $Force) {
        Write-Warn "File on server is identical in size. Use -Force to overwrite anyway."
        
        $confirm = Read-Host "   Continue anyway? (y/N)"
        if ($confirm -ne 'y' -and $confirm -ne 'Y') {
            Write-Host "`nDeployment cancelled." -ForegroundColor Yellow
            exit 0
        }
    }
} else {
    Write-Ok "No existing deployment found (fresh install)"
}

# Deploy the file
Write-Step "Deploying YakultLauncher.exe to $Server"

try {
    # Copy the file
    Copy-Item -LiteralPath $exePath -Destination $remotePath -Force -ErrorAction Stop
    
    Write-Ok "File copied successfully"
    
    # Verify the copy
    $deployedFile = Get-Item -LiteralPath $remotePath
    
    if ($deployedFile.Length -eq $fileInfo.Length) {
        Write-Ok "Verification: file size matches ($([math]::Round($deployedFile.Length / 1MB, 2)) MB)"
    } else {
        Write-Warn "Warning: deployed file size ($([math]::Round($deployedFile.Length / 1MB, 2)) MB) differs from source ($([math]::Round($fileInfo.Length / 1MB, 2)) MB)"
    }
    
    Write-Ok "Deployment completed at: $($deployedFile.LastWriteTime)"
} catch {
    Write-Err "Deployment failed: $_"
    exit 1
}

# Summary
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Deployment Summary" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Server     : $Server"
Write-Host "  Path       : \\$Server\$DestinationPath"
Write-Host "  File Size  : $([math]::Round($fileInfo.Length / 1MB, 2)) MB"
Write-Host "  Status     : DEPLOYED"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Run Setup-Server.ps1 on $Server (if first time)"
Write-Host "  2. Run Test-RdpSetup.ps1 to validate the deployment"
Write-Host "  3. Download the .rdp file from the portal and test"
Write-Host ""

exit 0
