# Read-only release inspection and durable job records. No private key is needed.
function ConvertTo-DeskVersion([string]$Value) {
    if ($Value -notmatch '^\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}$') { throw 'Enter a four-part version such as 1.6.7.0.' }
    $parsed = [version]$Value
    foreach ($part in @($parsed.Major, $parsed.Minor, $parsed.Build, $parsed.Revision)) {
        if ($part -gt 65534) { throw 'Version components must be between 0 and 65534.' }
    }
    return $parsed
}
function Read-DeskMetadata([string]$Path, [string]$PublicKeyPath) {
    $file = Get-Item -LiteralPath $Path -ErrorAction Stop
    if ($file.PSIsContainer -or $file.Length -le 0 -or $file.Length -gt 131072) { throw 'Invalid metadata file size.' }
    $envelope = [IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json
    if ($envelope.schemaVersion -ne 1 -or -not $envelope.payload -or -not $envelope.signature) { throw 'Unsigned or unsupported metadata.' }
    $payload = [Convert]::FromBase64String($envelope.payload)
    $rsa = [Security.Cryptography.RSACryptoServiceProvider]::new()
    $rsa.PersistKeyInCsp = $false
    try {
        $rsa.FromXmlString([IO.File]::ReadAllText($PublicKeyPath))
        if (-not $rsa.VerifyData($payload, 'SHA256', [Convert]::FromBase64String($envelope.signature))) { throw 'Metadata signature is invalid.' }
    } finally { $rsa.Dispose() }
    $metadata = [Text.Encoding]::UTF8.GetString($payload) | ConvertFrom-Json
    if ($metadata.schemaVersion -ne 1 -or $metadata.product -ne 'Yakult.Inventory.App') { throw 'Unsupported metadata product or schema.' }
    $version = ConvertTo-DeskVersion ([string]$metadata.version)
    if ($metadata.installer -cne ('YISP_Setup_' + $version.ToString(4) + '.exe') -or $metadata.sha256 -notmatch '^[a-fA-F0-9]{64}$' -or $metadata.sizeBytes -le 0 -or $metadata.sizeBytes -gt 1GB) { throw 'Invalid installer metadata.' }
    return $metadata
}
function Test-DeskPackage([string]$Directory, [string]$PublicKeyPath) {
    $expected = (ConvertTo-DeskVersion (Split-Path $Directory -Leaf)).ToString(4)
    $metadata = Read-DeskMetadata (Join-Path $Directory 'latest.json') $PublicKeyPath
    if ($metadata.version -ne $expected) { throw 'Metadata version does not match its package folder.' }
    $installer = Join-Path $Directory $metadata.installer
    $stream = [IO.File]::Open($installer, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $hash = [Security.Cryptography.SHA256]::Create()
    try {
        if ($stream.Length -ne $metadata.sizeBytes) { throw 'Installer size does not match signed metadata.' }
        $actual = [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
        if ($actual -ne $metadata.sha256) { throw 'Installer checksum does not match signed metadata.' }
    } finally { $stream.Dispose(); $hash.Dispose() }
    return $metadata
}
function Write-DeskJobRecord([string]$Path, $Record) {
    $temporary = $Path + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::WriteAllText($temporary, ($Record | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
        if ([IO.File]::Exists($Path)) { [IO.File]::Replace($temporary, $Path, [System.Management.Automation.Language.NullString]::Value) }
        else { [IO.File]::Move($temporary, $Path) }
    } finally { if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) } }
}
