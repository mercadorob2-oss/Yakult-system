<#
.SYNOPSIS
    Trusts the Yakult code-signing certificate on THIS machine.

.DESCRIPTION
    Fallback for PCs not covered by Group Policy (or for testing before you
    set up the GPO). Must be run elevated (as Administrator) -- it writes to
    the machine-wide certificate stores, which is exactly why a normal user
    can't be blocked/unblocked by this on their own.

    For a domain-wide rollout instead of running this per-PC, push
    YakultCodeSigning.cer via Group Policy:
      Computer Configuration -> Policies -> Windows Settings ->
      Security Settings -> Public Key Policies ->
        - right-click "Trusted Root Certification Authorities" -> Import -> YakultCodeSigning.cer
        - right-click "Trusted Publishers" -> Import -> YakultCodeSigning.cer
    Both stores are needed: Root establishes the chain of trust for a
    self-signed cert (nothing else vouches for it), Trusted Publishers is
    what AppLocker/SmartScreen/publisher-based checks look at directly.

.EXAMPLE
    # Run elevated:
    .\Deploy-TrustedPublisher.ps1
#>
param(
    [string]$CerPath = (Join-Path $PSScriptRoot "YakultCodeSigning.cer")
)

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).
    IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    throw "Run this script elevated (Right-click -> Run as administrator). Machine-wide cert stores require it."
}

if (-not (Test-Path $CerPath)) {
    throw "Certificate file not found: $CerPath. Copy YakultCodeSigning.cer next to this script first."
}

certutil -addstore -f "Root" $CerPath
certutil -addstore -f "TrustedPublisher" $CerPath

Write-Host ""
Write-Host "Yakult code-signing certificate is now trusted on this machine." -ForegroundColor Green
Write-Host "Signed builds of Yakult.Inventory.App should launch normally from here on."
