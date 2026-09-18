$files = @(
    'Pages\UserAccountManagementPage.cs',
    'Pages\InvoiceImportDialog.cs'
)

foreach ($file in $files) {
    if (-not (Test-Path $file)) { continue }
    $bytes = [System.IO.File]::ReadAllBytes($file)

    Write-Host "=== $file ===" -ForegroundColor Cyan

    # Look for patterns with C3 B0 (4-byte emoji start)
    for ($i = 0; $i -lt [Math]::Min($bytes.Length - 20, 100000); $i++) {
        if ($bytes[$i] -eq 0xC3 -and $bytes[$i+1] -eq 0xB0) {
            $byteStr = [System.BitConverter]::ToString($bytes[$i..($i+15)])
            Write-Host "C3B0 Position $i : $byteStr"
        }
    }

    # Look for patterns with C3 A2 (3-byte symbol start)
    for ($i = 0; $i -lt [Math]::Min($bytes.Length - 20, 100000); $i++) {
        if ($bytes[$i] -eq 0xC3 -and $bytes[$i+1] -eq 0xA2) {
            $byteStr = [System.BitConverter]::ToString($bytes[$i..($i+10)])
            Write-Host "C3A2 Position $i : $byteStr"
        }
    }
    Write-Host ""
}
