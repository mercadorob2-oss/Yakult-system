param([switch]$NoSleep)

$ErrorActionPreference = "Stop"
$src  = "C:\Users\russel.mercado\source\repos\Yakult-System-for-Merging\Yakult.Inventory.Api2_remote"
$dst  = "C:\inetpub\wwwroot\Yakult.Inventory.Api2_remote"

Write-Host "=== Yakult API2 - Deploy to IIS ===" -ForegroundColor Cyan
Write-Host ""

# Ensure target directory exists and grant modify
if (-not (Test-Path $dst)) { New-Item -ItemType Directory -Path $dst -Force | Out-Null; Write-Host "Created: $dst" }
else { Write-Host "Target: $dst" }

$acl = Get-Acl $dst
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule("Everyone", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.AddAccessRule($rule)
Set-Acl -Path $dst -AclObject $acl
Write-Host "Permissions: Everyone/Modify granted"

# Copy files
Write-Host "Copying files..." -ForegroundColor Yellow
robocopy $src $dst /E /MIR /R:3 /W:5 /NJH /NJS /NP /XD ".vs" /XF "*.log" "*.ps1"
Write-Host "Robocopy exit code: $LASTEXITCODE"

Write-Host ""
Write-Host "=== Files deployed ===" -ForegroundColor Green
Write-Host "Source: $src"
Write-Host "Target: $dst"
Write-Host ""
Write-Host "Next step: Run the IIS setup as Administrator:" -ForegroundColor Yellow
Write-Host "  Right-click and run as Admin:" -ForegroundColor White
Write-Host "  $PSScriptRoot\Setup-Api2-IIS.bat" -ForegroundColor White
Write-Host ""

if (-not $NoSleep) { Start-Sleep -Seconds 5 }
