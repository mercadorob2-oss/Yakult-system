<#
.SYNOPSIS
    One-time setup: creates the internal code-signing certificate used to sign
    Yakult.Inventory.App builds, so the office fleet stops treating every
    rebuild as an unrecognized/untrusted binary.

.DESCRIPTION
    Run this ONCE (or whenever the cert is due to expire) on the machine you
    trust to hold the signing key -- normally the build machine, not a shared
    PC. It produces two files:

      YakultCodeSigning.pfx  - contains the PRIVATE key. Keep this secret.
                                Only the build machine needs it, to sign
                                releases. Never commit it to git, never copy
                                it to end-user PCs.

      YakultCodeSigning.cer  - the PUBLIC certificate only. This is what gets
                                distributed to every office PC (via GPO or
                                Deploy-TrustedPublisher.ps1) so Windows
                                recognizes "Yakult Philippines Inc" as a
                                trusted publisher and stops silently blocking
                                the exe.

.EXAMPLE
    .\Generate-CodeSigningCert.ps1 -PfxPassword (Read-Host -AsSecureString "PFX password")
#>
param(
    [string]$Subject = "CN=Yakult Philippines Inc, O=Yakult Philippines Inc, C=PH",
    [string]$OutDir = $PSScriptRoot,
    [Parameter(Mandatory = $true)][securestring]$PfxPassword,
    [int]$ValidYears = 5
)

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -CertStoreLocation Cert:\CurrentUser\My `
    -KeyExportPolicy Exportable `
    -KeyUsage DigitalSignature `
    -KeySpec Signature `
    -NotAfter (Get-Date).AddYears($ValidYears) `
    -HashAlgorithm SHA256

$pfxPath = Join-Path $OutDir "YakultCodeSigning.pfx"
$cerPath = Join-Path $OutDir "YakultCodeSigning.cer"

Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $PfxPassword | Out-Null
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null

# The signing identity only needs to live in CurrentUser\My for signtool to use it
# later via -PfxPath; remove it from the store now so it isn't sitting around
# unlocked (the .pfx itself is the durable copy, password-protected).
Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Certificate created." -ForegroundColor Green
Write-Host "  Private (signing) key -- KEEP SECRET, build machine only:"
Write-Host "    $pfxPath"
Write-Host "  Public certificate -- distribute to every office PC:"
Write-Host "    $cerPath"
Write-Host "  Thumbprint: $($cert.Thumbprint)"
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Back up YakultCodeSigning.pfx + its password somewhere secure (password manager)."
Write-Host "  2. Run Sign-Release.ps1 after every build to sign the exe/installer."
Write-Host "  3. Deploy YakultCodeSigning.cer to office PCs -- see Deploy-TrustedPublisher.ps1"
Write-Host "     or push it via Group Policy (Trusted Root + Trusted Publishers)."
