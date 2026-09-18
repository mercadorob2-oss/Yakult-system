# Fix mojibake in all C# files
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Get all .cs files in the project
$files = Get-ChildItem -Path $scriptDir -Filter "*.cs" -Recurse | Where-Object { $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" }

function Fix-Mojibake($FilePath) {
    if (-not (Test-Path $FilePath)) {
        return
    }

    $bytes = [System.IO.File]::ReadAllBytes($FilePath)
    $content = [System.Text.Encoding]::UTF8.GetString($bytes)
    $original = $content

    # =====================================================
    # 4-byte emoji patterns (F0 9F xx xx)
    # =====================================================

    # Folder (F0 9F 93 81)
    $mojibake_folder = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9C, 0xC2, 0x81))
    $content = $content.Replace($mojibake_folder, '\U0001F4C1')

    # Clipboard (F0 9F 93 8B)
    $mojibake_clipboard = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9C, 0xE2, 0x80, 0xB9))
    $content = $content.Replace($mojibake_clipboard, '\U0001F4CB')

    # Memo (F0 9F 93 9D)
    $mojibake_memo = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9C, 0xC2, 0x9D))
    $content = $content.Replace($mojibake_memo, '\U0001F4DD')

    # Outbox (F0 9F 93 A4)
    $mojibake_outbox = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9C, 0xC2, 0xA4))
    $content = $content.Replace($mojibake_outbox, '\U0001F4E4')

    # Sync/Refresh (F0 9F 94 84)
    $mojibake_sync = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9D, 0xE2, 0x80, 0x9E))
    $content = $content.Replace($mojibake_sync, '\U0001F504')

    # Chart (F0 9F 93 8A)
    $mojibake_chart = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9C, 0xC5, 0xA0))
    $content = $content.Replace($mojibake_chart, '\U0001F4CA')

    # Package (F0 9F 93 A6)
    $mojibake_package = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9C, 0xC2, 0xA6))
    $content = $content.Replace($mojibake_package, '\U0001F4E6')

    # Thumbs up (F0 9F 91 8D)
    $mojibake_thumbs = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x98, 0xC2, 0x8D))
    $content = $content.Replace($mojibake_thumbs, '\U0001F44D')

    # Group/People (F0 9F 91 A5)
    $mojibake_people = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x98, 0xC2, 0xA5))
    $content = $content.Replace($mojibake_people, '\U0001F465')

    # Office building (F0 9F 8F A2)
    $mojibake_office = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xC2, 0x8F, 0xC2, 0xA2))
    $content = $content.Replace($mojibake_office, '\U0001F3E2')

    # Store (F0 9F 8F AA)
    $mojibake_store = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xC2, 0x8F, 0xC2, 0xAA))
    $content = $content.Replace($mojibake_store, '\U0001F3EA')

    # Factory (F0 9F 8F AD)
    $mojibake_factory = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xC2, 0x8F, 0xC2, 0xAD))
    $content = $content.Replace($mojibake_factory, '\U0001F3ED')

    # File cabinet with variation selector (F0 9F 97 84 EF B8 8F)
    $mojibake_cabinet = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x94, 0xE2, 0x80, 0x9E, 0xC3, 0xAF, 0xC2, 0xB8, 0xC2, 0x8F))
    $content = $content.Replace($mojibake_cabinet, '\U0001F5C4\uFE0F')

    # Lock (F0 9F 94 92)
    $mojibake_lock = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9D, 0xE2, 0x80, 0x99))
    $content = $content.Replace($mojibake_lock, '\U0001F512')

    # Unlock (F0 9F 94 93)
    $mojibake_unlock = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9D, 0xE2, 0x80, 0x9C))
    $content = $content.Replace($mojibake_unlock, '\U0001F513')

    # Search/Magnifying glass (F0 9F 94 8D)
    $mojibake_search = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9D, 0xC2, 0x8D))
    $content = $content.Replace($mojibake_search, '\U0001F50D')

    # Search tilted (F0 9F 94 8E)
    $mojibake_search2 = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9D, 0xC5, 0xBD))
    $content = $content.Replace($mojibake_search2, '\U0001F50E')

    # Trash can (F0 9F 97 91)
    $mojibake_trash = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x94, 0xE2, 0x80, 0x98))
    $content = $content.Replace($mojibake_trash, '\U0001F5D1')

    # Lightbulb (F0 9F 92 A1)
    $mojibake_bulb = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x99, 0xC2, 0xA1))
    $content = $content.Replace($mojibake_bulb, '\U0001F4A1')

    # Key (F0 9F 94 91)
    $mojibake_key = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xB0, 0xC5, 0xB8, 0xE2, 0x80, 0x9D, 0xE2, 0x80, 0x98))
    $content = $content.Replace($mojibake_key, '\U0001F511')

    # =====================================================
    # 3-byte patterns (E2 xx xx)
    # =====================================================

    # Checkmark box (E2 9C 85) - ✅
    $mojibake_check_box = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC5, 0x93, 0xE2, 0x80, 0xA6))
    $content = $content.Replace($mojibake_check_box, '\u2705')

    # Checkmark (E2 9C 94) - ✔
    $mojibake_checkmark = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC5, 0x93, 0xE2, 0x80, 0x9C))
    $content = $content.Replace($mojibake_checkmark, '\u2714')

    # X mark cross (E2 9C 97) - ✗
    $mojibake_xmark = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC5, 0x93, 0xE2, 0x80, 0x94))
    $content = $content.Replace($mojibake_xmark, '\u2717')

    # X mark (E2 9C 95) - ✕
    $mojibake_xcancel = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC5, 0x93, 0xE2, 0x80, 0xA2))
    $content = $content.Replace($mojibake_xcancel, '\u2715')

    # Plus sign (E2 9E 95) - ➕
    $mojibake_plus = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC5, 0xBE, 0xE2, 0x80, 0xA2))
    $content = $content.Replace($mojibake_plus, '\u2795')

    # Pencil (E2 9C 8F) - ✏
    $mojibake_pencil = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC5, 0x93, 0xC2, 0x8F))
    $content = $content.Replace($mojibake_pencil, '\u270F')

    # Lower left pencil (E2 9C 8E) - ✎
    $mojibake_pencil2 = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC5, 0x93, 0xC5, 0xBD))
    $content = $content.Replace($mojibake_pencil2, '\u270E')

    # Refresh arrow (E2 9F B3) - ⟳
    $mojibake_refresh = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC5, 0xB8, 0xC2, 0xB3))
    $content = $content.Replace($mojibake_refresh, '\u27F3')

    # Hourglass (E2 8F B3) - ⏳
    $mojibake_hourglass = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC2, 0x8F, 0xC2, 0xB3))
    $content = $content.Replace($mojibake_hourglass, '\u23F3')

    # Alarm (E2 8F B0) - ⏰
    $mojibake_alarm = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC2, 0x8F, 0xC2, 0xB0))
    $content = $content.Replace($mojibake_alarm, '\u23F0')

    # Pause (E2 8F B8) - ⏸
    $mojibake_pause = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC2, 0x8F, 0xC2, 0xB8))
    $content = $content.Replace($mojibake_pause, '\u23F8')

    # First track (E2 8F AE) - ⏮
    $mojibake_first = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC2, 0x8F, 0xC2, 0xAE))
    $content = $content.Replace($mojibake_first, '\u23EE')

    # Last track (E2 8F AD) - ⏭
    $mojibake_last = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC2, 0x8F, 0xC2, 0xAD))
    $content = $content.Replace($mojibake_last, '\u23ED')

    # Left triangle (E2 97 80) - ◀
    $mojibake_prev = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xE2, 0x80, 0x94, 0xE2, 0x82, 0xAC))
    $content = $content.Replace($mojibake_prev, '\u25C0')

    # Right triangle (E2 96 B6) - ▶
    $mojibake_next = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xE2, 0x80, 0x93, 0xC2, 0xB6))
    $content = $content.Replace($mojibake_next, '\u25B6')

    # Up right arrow (E2 A4 B4) - ⤴
    $mojibake_arrow = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC2, 0xA4, 0xC2, 0xB4))
    $content = $content.Replace($mojibake_arrow, '\u2934')

    # Cross mark (E2 9D 8C) - ❌
    $mojibake_crossmark = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC2, 0x9D, 0xC5, 0x92))
    $content = $content.Replace($mojibake_crossmark, '\u274C')

    # Gear with variation selector (E2 9A 99 EF B8 8F) - ⚙️
    $mojibake_gear = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC5, 0xA1, 0xE2, 0x84, 0xA2, 0xC3, 0xAF, 0xC2, 0xB8, 0xC2, 0x8F))
    $content = $content.Replace($mojibake_gear, '\u2699\uFE0F')

    # Warning with variation selector (E2 9A A0 EF B8 8F) - ⚠️
    $mojibake_warning_vs = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC5, 0xA1, 0xC2, 0xA0, 0xC3, 0xAF, 0xC2, 0xB8, 0xC2, 0x8F))
    $content = $content.Replace($mojibake_warning_vs, '\u26A0\uFE0F')

    # Warning without variation selector (E2 9A A0) - ⚠
    $mojibake_warning = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xC5, 0xA1, 0xC2, 0xA0))
    $content = $content.Replace($mojibake_warning, '\u26A0')

    # Em dash (E2 80 94) - — (variant 1)
    $mojibake_emdash = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xE2, 0x82, 0xAC, 0xE2, 0x80, 0x9D))
    $content = $content.Replace($mojibake_emdash, '\u2014')

    # Em dash (E2 80 94) - — (variant 2: â€")
    $mojibake_emdash2 = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xE2, 0x82, 0xAC, 0xE2, 0x80, 0x9C))
    $content = $content.Replace($mojibake_emdash2, '\u2014')

    # Bullet point (E2 80 A2) - •
    $mojibake_bullet = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xE2, 0x82, 0xAC, 0xC2, 0xA2))
    $content = $content.Replace($mojibake_bullet, '\u2022')

    # Less than or equal (E2 89 A4) - ≤
    $mojibake_lte = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xC3, 0xA2, 0xE2, 0x80, 0xB0, 0xC2, 0xA4))
    $content = $content.Replace($mojibake_lte, '\u2264')

    if ($content -ne $original) {
        $utf8WithBom = New-Object System.Text.UTF8Encoding $true
        [System.IO.File]::WriteAllText($FilePath, $content, $utf8WithBom)
        Write-Host "Fixed: $FilePath"
    }
}

Write-Host "Processing $($files.Count) files..."
foreach ($file in $files) {
    Fix-Mojibake $file.FullName
}
Write-Host "Done!"
