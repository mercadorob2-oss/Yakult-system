# Fix mojibake icons in C# files by replacing UTF-8 encoded mojibake with C# escape sequences
param(
    [string]$FilePath
)

$bytes = [System.IO.File]::ReadAllBytes($FilePath)
$content = [System.Text.Encoding]::UTF8.GetString($bytes)
$original = $content

# Mojibake patterns (UTF-8 double-encoded from Windows-1252 interpretation of UTF-8)

# 4-byte emoji patterns

# Folder (F0 9F 93 81) -> C3 B0 C5 B8 E2 80 9C C2 81
$mojibake_folder = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9C, 0xC2, 0x81))
$content = $content.Replace($mojibake_folder, '\U0001F4C1')

# Clipboard (F0 9F 93 8B) -> C3 B0 C5 B8 E2 80 9C E2 80 B9
$mojibake_clipboard = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9C, 0xE2, 0x80, 0xB9))
$content = $content.Replace($mojibake_clipboard, '\U0001F4CB')

# Memo (F0 9F 93 9D) -> C3 B0 C5 B8 E2 80 9C C2 9D
$mojibake_memo = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9C, 0xC2, 0x9D))
$content = $content.Replace($mojibake_memo, '\U0001F4DD')

# Outbox (F0 9F 93 A4) -> C3 B0 C5 B8 E2 80 9C C2 A4
$mojibake_outbox = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9C, 0xC2, 0xA4))
$content = $content.Replace($mojibake_outbox, '\U0001F4E4')

# Sync/Refresh (F0 9F 94 84) -> C3 B0 C5 B8 E2 80 9D E2 80 9E (CORRECTED!)
$mojibake_sync = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9D, 0xE2, 0x80, 0x9E))
$content = $content.Replace($mojibake_sync, '\U0001F504')

# Chart (F0 9F 93 8A) -> C3 B0 C5 B8 E2 80 9C C5 A0
$mojibake_chart = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9C, 0xC5, 0xA0))
$content = $content.Replace($mojibake_chart, '\U0001F4CA')

# Package (F0 9F 93 A6) -> C3 B0 C5 B8 E2 80 9C C2 A6
$mojibake_package = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9C, 0xC2, 0xA6))
$content = $content.Replace($mojibake_package, '\U0001F4E6')

# Thumbs up (F0 9F 91 8D) -> C3 B0 C5 B8 E2 80 98 C2 8D
$mojibake_thumbs = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x98, 0xC2, 0x8D))
$content = $content.Replace($mojibake_thumbs, '\U0001F44D')

# Group/People (F0 9F 91 A5) -> C3 B0 C5 B8 E2 80 98 C2 A5
$mojibake_people = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x98, 0xC2, 0xA5))
$content = $content.Replace($mojibake_people, '\U0001F465')

# Office building (F0 9F 8F A2) -> C3 B0 C5 B8 C2 8F C2 A2
$mojibake_office = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xC2, 0x8F, 0xC2, 0xA2))
$content = $content.Replace($mojibake_office, '\U0001F3E2')

# Store (F0 9F 8F AA) -> C3 B0 C5 B8 C2 8F C2 AA
$mojibake_store = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xC2, 0x8F, 0xC2, 0xAA))
$content = $content.Replace($mojibake_store, '\U0001F3EA')

# Factory (F0 9F 8F AD) -> C3 B0 C5 B8 C2 8F C2 AD
$mojibake_factory = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xC2, 0x8F, 0xC2, 0xAD))
$content = $content.Replace($mojibake_factory, '\U0001F3ED')

# File cabinet with variation selector (F0 9F 97 84 EF B8 8F) -> C3 B0 C5 B8 E2 80 94 E2 80 9E C3 AF C2 B8 C2 8F (CORRECTED!)
$mojibake_cabinet = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x94, 0xE2, 0x80, 0x9E, 0xC3, 0xAF, 0xC2, 0xB8, 0xC2, 0x8F))
$content = $content.Replace($mojibake_cabinet, '\U0001F5C4\uFE0F')

# 3-byte patterns

# Checkmark (E2 9C 85) -> C3 A2 C5 93 E2 80 A6 (CORRECTED!)
$mojibake_check = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC5, 0x93, 0xE2, 0x80, 0xA6))
$content = $content.Replace($mojibake_check, '\u2705')

# Hourglass (E2 8F B3) -> C3 A2 C2 8F C2 B3
$mojibake_hourglass = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC2, 0x8F, 0xC2, 0xB3))
$content = $content.Replace($mojibake_hourglass, '\u23F3')

# Alarm (E2 8F B0) -> C3 A2 C2 8F C2 B0
$mojibake_alarm = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC2, 0x8F, 0xC2, 0xB0))
$content = $content.Replace($mojibake_alarm, '\u23F0')

# Pause (E2 8F B8) -> C3 A2 C2 8F C2 B8
$mojibake_pause = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC2, 0x8F, 0xC2, 0xB8))
$content = $content.Replace($mojibake_pause, '\u23F8')

# Warning with variation selector (E2 9A A0 EF B8 8F) -> C3 A2 C5 A1 C2 A0 C3 AF C2 B8 C2 8F
$mojibake_warning_vs = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC5, 0xA1, 0xC2, 0xA0, 0xC3, 0xAF, 0xC2, 0xB8, 0xC2, 0x8F))
$content = $content.Replace($mojibake_warning_vs, '\u26A0\uFE0F')

# Warning without variation selector (E2 9A A0) -> C3 A2 C5 A1 C2 A0
$mojibake_warning = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC5, 0xA1, 0xC2, 0xA0))
$content = $content.Replace($mojibake_warning, '\u26A0')

# Bullet point (E2 80 A2) -> C3 A2 E2 82 AC C2 A2
$mojibake_bullet = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xE2, 0x82, 0xAC, 0xC2, 0xA2))
$content = $content.Replace($mojibake_bullet, '\u2022')

# Less than or equal (E2 89 A4) -> C3 A2 E2 80 B0 C2 A4
$mojibake_lte = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xE2, 0x80, 0xB0, 0xC2, 0xA4))
$content = $content.Replace($mojibake_lte, '\u2264')

# Summary
Write-Host "Original length: $($original.Length)"
Write-Host "New length: $($content.Length)"

if ($content -ne $original) {
    # Write with UTF-8 encoding with BOM
    $utf8WithBom = New-Object System.Text.UTF8Encoding $true
    [System.IO.File]::WriteAllText($FilePath, $content, $utf8WithBom)
    Write-Host "Fixed: $FilePath"
} else {
    Write-Host "No changes made to: $FilePath"
}
