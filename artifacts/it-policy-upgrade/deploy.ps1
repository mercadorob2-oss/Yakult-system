$ErrorActionPreference = 'Stop'
$target = '\\192.168.100.186\c$\inetpub\wwwroot\Yakult.SystemsPortal'
$publish = (Resolve-Path artifacts/it-policy-upgrade/publish).Path
$backup = (Get-Content artifacts/it-policy-upgrade/backup-path.txt -Raw).Trim()
$manifest = Get-Content (Join-Path $backup 'manifest.json') -Raw | ConvertFrom-Json
$offline = Join-Path $target 'app_offline.htm'
if (Test-Path -LiteralPath $offline) { throw 'The portal already has an offline file; deployment stopped.' }
try {
    Set-Content -LiteralPath $offline -Value '<!doctype html><html lang="en"><meta charset="utf-8"><title>Portal update</title><p>The employee portal is being updated. Please refresh shortly.</p></html>' -Encoding utf8
    # Allow IIS to release the application assembly, checking without a fixed long delay.
    $deadline = (Get-Date).AddSeconds(30)
    do {
        try {
            $handle = [IO.File]::Open((Join-Path $target 'Yakult.SystemsPortal.dll'), 'Open', 'ReadWrite', 'None')
            $handle.Dispose()
            break
        } catch {
            if ((Get-Date) -ge $deadline) { throw }
            Start-Sleep -Milliseconds 500
        }
    } while ($true)
    foreach ($entry in $manifest) {
        $source = Join-Path $publish $entry.Path
        $destination = Join-Path $target $entry.Path
        Copy-Item -LiteralPath $source -Destination $destination -Force
        if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw "Hash mismatch for $($entry.Path)" }
    }
    Write-Output 'Deployed six verified application files.'
} catch {
    $failure = $_
    foreach ($entry in $manifest) {
        $destination = Join-Path $target $entry.Path
        if ($entry.Existed) { Copy-Item -LiteralPath (Join-Path $backup $entry.Path) -Destination $destination -Force }
        elseif (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Force }
    }
    throw $failure
} finally {
    if (Test-Path -LiteralPath $offline) { Remove-Item -LiteralPath $offline -Force }
}
