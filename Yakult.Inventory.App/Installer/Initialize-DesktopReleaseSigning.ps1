#Requires -Version 5.1
[CmdletBinding()]
param([string]$PrivateKeyPath = (Join-Path $env:LOCALAPPDATA 'Yakult\DesktopReleaseSigning\manifest-private.xml'))
$ErrorActionPreference = 'Stop'
$publicPath = Join-Path $PSScriptRoot 'UpdatePublicKey.xml'
$rsa = [Security.Cryptography.RSACryptoServiceProvider]::new(3072)
$rsa.PersistKeyInCsp = $false
try {
    if (Test-Path -LiteralPath $PrivateKeyPath) { $rsa.FromXmlString([IO.File]::ReadAllText($PrivateKeyPath)) }
    elseif (Test-Path -LiteralPath $publicPath) { throw 'This checkout already trusts a release key. Obtain its private key from your release administrator; do not generate a replacement.' }
    else {
        New-Item -ItemType Directory -Path (Split-Path $PrivateKeyPath -Parent) -Force | Out-Null
        [IO.File]::WriteAllText($PrivateKeyPath,$rsa.ToXmlString($true))
        $acl = [Security.AccessControl.FileSecurity]::new()
        $acl.SetAccessRuleProtection($true,$false)
        foreach ($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))) {
            $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','Allow'))
        }
        Set-Acl -LiteralPath $PrivateKeyPath -AclObject $acl
    }
    $publicXml = $rsa.ToXmlString($false)
    if ((Test-Path -LiteralPath $publicPath) -and [IO.File]::ReadAllText($publicPath).Trim() -ne $publicXml) { throw 'The private key does not match the public key trusted by this application.' }
    [IO.File]::WriteAllText($publicPath,$publicXml)
    Write-Host 'Release signing is ready. The private key stays in your Windows profile, outside the repository and installers.'
} finally { $rsa.Dispose() }
