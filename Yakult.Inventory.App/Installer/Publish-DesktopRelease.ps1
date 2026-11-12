#Requires -Version 5.1
[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [string]$InstallerPath,
    [string]$Version,
    [string]$ReleaseNotes = '',
    [string]$UpdateFolder = '\\ypi-hqfs\my_dept$\IT\ITDSharedFolder\Russel\Yakult_Desktop_Installer\New Versions\patch_versions',
    [switch]$ManifestOnly,
    [string]$ManifestOutput,
    [string]$PrivateKeyPath = (Join-Path $env:LOCALAPPDATA 'Yakult\DesktopReleaseSigning\manifest-private.xml'),
    [string]$ReleaseDirectory
)
$ErrorActionPreference = 'Stop'

function ConvertTo-ReleaseVersion([string]$Value) {
    if ($Value -notmatch '^\d{1,5}\.\d{1,5}(\.\d{1,5}){0,2}$') { throw 'Use a numeric version such as 1.6.1.0.' }
    $parsed = [version]$Value
    $normalized = [version]::new($parsed.Major,$parsed.Minor,[Math]::Max(0,$parsed.Build),[Math]::Max(0,$parsed.Revision))
    if (@($normalized.Major,$normalized.Minor,$normalized.Build,$normalized.Revision | Where-Object { $_ -gt 65534 }).Count) { throw 'Version components must be at most 65534.' }
    return $normalized
}

function Get-InstallerHash([string]$Path) {
    # Use a read-only stream so PowerShell 5.1's Get-FileHash/WhatIf interaction cannot suppress hashing.
    $hasher = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = [IO.File]::OpenRead($Path)
        try { return [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-','').ToLowerInvariant() }
        finally { $stream.Dispose() }
    } finally { $hasher.Dispose() }
}

function Read-ReleaseMetadata([string]$Path) {
    $file = Get-Item -LiteralPath $Path
    if ($file.PSIsContainer -or $file.Length -le 0 -or $file.Length -gt 131072) { throw "Invalid release metadata file: $Path" }
    $envelope = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($envelope.schemaVersion -ne 1 -or -not $envelope.payload -or -not $envelope.signature) { throw "Release metadata is not signed: $Path" }
    $payload = [Convert]::FromBase64String($envelope.payload)
    $verifier = [Security.Cryptography.RSACryptoServiceProvider]::new()
    $verifier.PersistKeyInCsp = $false
    try {
        $verifier.FromXmlString([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'UpdatePublicKey.xml')))
        if (-not $verifier.VerifyData($payload,'SHA256',[Convert]::FromBase64String($envelope.signature))) { throw "Release metadata signature is invalid: $Path" }
    } finally { $verifier.Dispose() }
    $metadata = [Text.Encoding]::UTF8.GetString($payload) | ConvertFrom-Json
    if ($metadata.schemaVersion -ne 1 -or $metadata.product -ne 'Yakult.Inventory.App') { throw "Release metadata is for an unsupported product or schema: $Path" }
    return $metadata
}

$automaticSelection = -not $InstallerPath -and -not $Version
$builtRelease = $null
if (-not $InstallerPath) {
    if ($ManifestOnly) { throw 'Specify -InstallerPath and -Version when generating build metadata.' }
    if (-not $ReleaseDirectory) {
        $repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
        $ReleaseDirectory = Join-Path $repoRoot 'artifacts\desktop-releases'
    }
    $requestedVersion = if ($Version) { ConvertTo-ReleaseVersion $Version } else { $null }
    $candidates = @(
        if (Test-Path -LiteralPath $ReleaseDirectory -PathType Container) {
            foreach ($directory in Get-ChildItem -LiteralPath $ReleaseDirectory -Directory) {
                if ($directory.Name -notmatch '^\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}$' -or ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) { continue }
                try { $candidateVersion = ConvertTo-ReleaseVersion $directory.Name } catch { continue }
                if ($requestedVersion -and $candidateVersion -ne $requestedVersion) { continue }
                $candidateInstaller = Join-Path $directory.FullName ('YISP_Setup_' + $candidateVersion.ToString(4) + '.exe')
                $candidateManifest = Join-Path $directory.FullName 'latest.json'
                if ((Test-Path -LiteralPath $candidateInstaller -PathType Leaf) -and (Test-Path -LiteralPath $candidateManifest -PathType Leaf)) {
                    [pscustomobject]@{ Version=$candidateVersion; Installer=$candidateInstaller; Manifest=$candidateManifest }
                }
            }
        }
    )
    $selected = $candidates | Sort-Object Version -Descending | Select-Object -First 1
    if (-not $selected) { throw "No completed installer build was found in '$ReleaseDirectory' for the selected version. Run Build-DesktopInstaller.cmd first." }
    $InstallerPath = $selected.Installer
    $Version = $selected.Version.ToString(4)
    $builtRelease = Read-ReleaseMetadata $selected.Manifest
    if ($builtRelease.version -ne $Version -or $builtRelease.installer -ne [IO.Path]::GetFileName($InstallerPath)) { throw 'The detected build metadata does not match its installer name and version.' }
    if (-not $PSBoundParameters.ContainsKey('ReleaseNotes')) { $ReleaseNotes = [string]$builtRelease.releaseNotes }
    Write-Host "Detected built Yakult release: $Version"
    Write-Host "Installer: $InstallerPath"
} elseif (-not $Version) {
    if ([IO.Path]::GetFileName($InstallerPath) -notmatch '^YISP_Setup_(\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5})\.exe$') { throw 'Specify -Version for an installer without the YISP_Setup_<version>.exe filename.' }
    $Version = $Matches[1]
}
$numericVersion = ConvertTo-ReleaseVersion $Version
$installer = Get-Item -LiteralPath $InstallerPath
if ($installer.PSIsContainer -or $installer.Extension -ne '.exe' -or $installer.Length -le 0 -or $installer.Length -gt 1GB) { throw 'Select an installer EXE smaller than 1 GB.' }
if ($ReleaseNotes.Length -gt 16000) { throw 'Release notes must be at most 16000 characters.' }
$name = 'YISP_Setup_' + $numericVersion.ToString(4) + '.exe'
$release = [ordered]@{ schemaVersion=1; product='Yakult.Inventory.App'; version=$numericVersion.ToString(4); installer=$name; sha256=(Get-InstallerHash $installer.FullName); sizeBytes=$installer.Length; releaseNotes=$ReleaseNotes }
if ($builtRelease -and ($builtRelease.sha256 -ne $release.sha256 -or $builtRelease.sizeBytes -ne $release.sizeBytes)) { throw 'The detected installer does not match its signed build metadata. Rebuild the release before publishing.' }
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
    if (-not $PSCmdlet.ShouldProcess($ManifestOutput,'Write signed release metadata')) { return }
    [IO.File]::WriteAllText($ManifestOutput,$signedJson,[Text.UTF8Encoding]::new($false))
    Write-Host "Release metadata: $ManifestOutput"
    return
}
if (-not (Test-Path -LiteralPath $UpdateFolder -PathType Container)) { throw "Update folder is not accessible: $UpdateFolder" }
if (-not $PSCmdlet.ShouldProcess($UpdateFolder,"Publish Yakult $numericVersion")) { return }
$latest = Join-Path $UpdateFolder 'latest.json'
$destination = Join-Path $UpdateFolder $name
$temporaryInstaller = Join-Path $UpdateFolder ($name + '.' + [guid]::NewGuid().ToString('N') + '.partial')
$temporaryManifest = Join-Path $UpdateFolder ('latest.' + [guid]::NewGuid().ToString('N') + '.tmp')
$probeName = 'YISP_PublishProbe_' + [guid]::NewGuid().ToString('N')
$probeSource = Join-Path $UpdateFolder ($probeName + '.tmp')
$probeExecutable = Join-Path $UpdateFolder ($probeName + '.exe')
$publishLock = [IO.File]::Open((Join-Path $UpdateFolder '.publish.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
    if (Test-Path -LiteralPath $latest) {
        $previous = Read-ReleaseMetadata $latest
        $previousVersion = ConvertTo-ReleaseVersion $previous.version
        if ($previousVersion -gt $numericVersion) {
            if ($automaticSelection) { Write-Host "No newer built release. Published: $previousVersion. Latest local build: $numericVersion. Nothing to publish."; return }
            throw 'Publish a version newer than the current release. Existing releases cannot be replaced.'
        }
        if ($previousVersion -eq $numericVersion) {
            if ($previous.installer -ne $name -or $previous.sha256 -ne $release.sha256 -or $previous.sizeBytes -ne $release.sizeBytes) { throw 'A different installer is already published for this version. Build a higher version.' }
            if (-not (Test-Path -LiteralPath $destination -PathType Leaf) -or (Get-InstallerHash $destination) -ne $release.sha256) { throw 'The published installer is missing or changed. Restore the original installer before publishing.' }
            Write-Host "Yakult $numericVersion is already published. Nothing to publish."
            return
        }
    }
    if ((Test-Path -LiteralPath $destination) -and (Get-InstallerHash $destination) -ne $release.sha256) { throw "A different installer already exists for this version: $destination" }
    if (-not (Test-Path -LiteralPath $destination)) {
        # Some shares allow temporary files but block their rename to an executable.
        try {
            [IO.File]::WriteAllText($probeSource,'Yakult installer publication permission check')
            Move-Item -LiteralPath $probeSource -Destination $probeExecutable -ErrorAction Stop
            Remove-Item -LiteralPath $probeExecutable -ErrorAction Stop
        } catch {
            throw [IO.IOException]::new("Cannot publish an EXE installer to '$UpdateFolder'. Ask the file-server administrator to check restrictions on .exe files and your account's Modify permissions. No release was announced. Details: $($_.Exception.Message)", $_.Exception)
        }
        Copy-Item -LiteralPath $installer.FullName -Destination $temporaryInstaller
        if ((Get-InstallerHash $temporaryInstaller) -ne $release.sha256) { throw 'The copied installer checksum does not match.' }
        Move-Item -LiteralPath $temporaryInstaller -Destination $destination
    }
    [IO.File]::WriteAllText($temporaryManifest,$signedJson,[Text.UTF8Encoding]::new($false))
    # Publish metadata last; clients never see a release whose EXE is still copying.
    if (Test-Path -LiteralPath $latest) { [IO.File]::Replace($temporaryManifest,$latest,(Join-Path $UpdateFolder 'latest.previous.json')) }
    else { [IO.File]::Move($temporaryManifest,$latest) }
    Write-Host "Published Yakult $numericVersion to $UpdateFolder"
} finally {
    try {
        foreach ($temporary in @($temporaryInstaller,$temporaryManifest,$probeSource,$probeExecutable)) {
            try {
                if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force -ErrorAction Stop }
            } catch { Write-Warning "Could not remove temporary publication file '$temporary': $($_.Exception.Message)" }
        }
    } finally { $publishLock.Dispose() }
}
