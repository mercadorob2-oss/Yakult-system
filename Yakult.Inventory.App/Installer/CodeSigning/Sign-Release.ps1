<#
.SYNOPSIS
    Signs Yakult.Inventory.App build output with the internal code-signing cert.

.DESCRIPTION
    Run this twice per release:
      1. After building Release, against bin\Release\Yakult.Inventory.App.exe
         -- BEFORE compiling the Inno Setup installer, so the installer bundles
         an already-signed exe.
      2. After compiling the installer, against the resulting
         YakultInventory_Setup.exe -- so the installer itself is trusted too
         (this is what actually gets run on end-user PCs).

.EXAMPLE
    # Step 1: sign the app exe before building the installer
    .\Sign-Release.ps1 -Path "..\bin\Release\Yakult.Inventory.App.exe" -PfxPassword (Read-Host -AsSecureString)

    # ... compile YakultInventory_Setup.iss in Inno Setup ...

    # Step 2: sign the compiled installer
    .\Sign-Release.ps1 -Path "..\YakultInventory_Setup.exe" -PfxPassword (Read-Host -AsSecureString)
#>
param(
    [Parameter(Mandatory = $true)][string[]]$Path,
    [string]$PfxPath = (Join-Path $PSScriptRoot "YakultCodeSigning.pfx"),
    [Parameter(Mandatory = $true)][securestring]$PfxPassword,
    [string]$TimestampServer = "http://timestamp.digicert.com"
)

if (-not (Test-Path $PfxPath)) {
    throw "Signing certificate not found at $PfxPath. Run Generate-CodeSigningCert.ps1 first."
}

$signtool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName

if (-not $signtool) {
    throw "signtool.exe not found under the Windows 10/11 SDK. Install the Windows SDK (or the 'Windows SDK Signing Tools' component via Visual Studio Installer)."
}

$plainPwd = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($PfxPassword))

foreach ($file in $Path) {
    $resolved = Resolve-Path $file -ErrorAction SilentlyContinue
    if (-not $resolved) {
        Write-Warning "Skipping missing file: $file"
        continue
    }

    & $signtool sign /f $PfxPath /p $plainPwd /fd SHA256 /tr $TimestampServer /td SHA256 $resolved.Path
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for $($resolved.Path) (exit $LASTEXITCODE)"
    }

    & $signtool verify /pa $resolved.Path
    Write-Host "Signed and verified: $($resolved.Path)" -ForegroundColor Green
}
