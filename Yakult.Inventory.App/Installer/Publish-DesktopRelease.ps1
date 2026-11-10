#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$InstallerPath,
    [Parameter(Mandatory=$true)][string]$Version,
    [string]$ReleaseNotes = '',
    [string]$UpdateFolder = '\\ypi-hqfs\my_dept$\IT\ITDSharedFolder\Russel\Yakult_Desktop_Installer\New Versions\patch_versions',
    [switch]$ManifestOnly,
    [string]$ManifestOutput,
    [string]$PrivateKeyPath = (Join-Path $env:LOCALAPPDATA 'Yakult\DesktopReleaseSigning\manifest-private.xml')
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d{1,5}\.\d{1,5}(\.\d{1,5}){0,2}$') { throw 'Use a numeric version such as 1.6.1.0.' }
$parsedVersion = [version]$Version
$numericVersion = [version]::new($parsedVersion.Major,$parsedVersion.Minor,[Math]::Max(0,$parsedVersion.Build),[Math]::Max(0,$parsedVersion.Revision))
if (@($numericVersion.Major,$numericVersion.Minor,$numericVersion.Build,$numericVersion.Revision | Where-Object { $_ -gt 65534 }).Count) { throw 'Version components must be at most 65534.' }
$installer = Get-Item -LiteralPath $InstallerPath
if ($installer.Extension -ne '.exe' -or $installer.Length -le 0 -or $installer.Length -gt 1GB) { throw 'Select an installer EXE smaller than 1 GB.' }
if ($ReleaseNotes.Length -gt 16000) { throw 'Release notes must be at most 16000 characters.' }
$name = 'YISP_Setup_' + $numericVersion.ToString(4) + '.exe'
$release = [ordered]@{ schemaVersion=1; product='Yakult.Inventory.App'; version=$numericVersion.ToString(4); installer=$name; sha256=(Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); sizeBytes=$installer.Length; releaseNotes=$ReleaseNotes }
$json = $release | ConvertTo-Json
$rsa = [Security.Cryptography.RSACryptoServiceProvider]::new()
$rsa.PersistKeyInCsp = $false
try {
    if (-not (Test-Path -LiteralPath $PrivateKeyPath)) { throw 'The release signing key is missing. Obtain the existing key from the release administrator.' }
    $rsa.FromXmlString([IO.File]::ReadAllText($PrivateKeyPath))
    if ($rsa.ToXmlString($false) -ne [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'UpdatePublicKey.xml')).Trim()) { throw 'The release key does not match the key trusted by the desktop application.' }
    $payload = [Text.Encoding]::UTF8.GetBytes($json)
    $signedJson = [ordered]@{schemaVersion=1;payload=[Convert]::ToBase64String($payload);signature=[Convert]::ToBase64String($rsa.SignData($payload,'SHA256'))} | ConvertTo-Json
} finally { $rsa.Dispose() }
if ($ManifestOnly) {
    if (-not $ManifestOutput) { throw 'Specify -ManifestOutput when generating metadata only.' }
    [IO.File]::WriteAllText($ManifestOutput,$signedJson,[Text.UTF8Encoding]::new($false))
    Write-Host "Release metadata: $ManifestOutput"
    return
}
if (-not (Test-Path -LiteralPath $UpdateFolder -PathType Container)) { throw "Update folder is not accessible: $UpdateFolder" }
$latest = Join-Path $UpdateFolder 'latest.json'
$destination = Join-Path $UpdateFolder $name
$temporaryInstaller = Join-Path $UpdateFolder ($name + '.' + [guid]::NewGuid().ToString('N') + '.partial')
$temporaryManifest = Join-Path $UpdateFolder ('latest.' + [guid]::NewGuid().ToString('N') + '.tmp')
$publishLock = [IO.File]::Open((Join-Path $UpdateFolder '.publish.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
    if (Test-Path -LiteralPath $latest) {
        $previousEnvelope = Get-Content -LiteralPath $latest -Raw | ConvertFrom-Json
        $previous = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($previousEnvelope.payload)) | ConvertFrom-Json
        if ($previous.product -ne $release.product -or [version]$previous.version -ge $numericVersion) { throw 'Publish a version newer than the current release. Existing releases cannot be replaced.' }
    }
    if ((Test-Path -LiteralPath $destination) -and (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $release.sha256) { throw "A different installer already exists for this version: $destination" }
    if (-not (Test-Path -LiteralPath $destination)) {
        Copy-Item -LiteralPath $installer.FullName -Destination $temporaryInstaller
        if ((Get-FileHash -LiteralPath $temporaryInstaller -Algorithm SHA256).Hash -ne $release.sha256) { throw 'The copied installer checksum does not match.' }
        Move-Item -LiteralPath $temporaryInstaller -Destination $destination
    }
    [IO.File]::WriteAllText($temporaryManifest,$signedJson,[Text.UTF8Encoding]::new($false))
    # Publish metadata last; clients never see a release whose EXE is still copying.
    if (Test-Path -LiteralPath $latest) { [IO.File]::Replace($temporaryManifest,$latest,(Join-Path $UpdateFolder 'latest.previous.json')) }
    else { [IO.File]::Move($temporaryManifest,$latest) }
    Write-Host "Published Yakult $numericVersion to $UpdateFolder"
} finally {
    try {
        foreach ($temporary in @($temporaryInstaller,$temporaryManifest)) {
            if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
        }
    } finally { $publishLock.Dispose() }
}
