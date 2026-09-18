param(
    [string]$ContentFile = 'borrow-support-manual.content.ps1',
    [string]$OutputPrefix = 'Borrow_Items_IT_Support_Manual',
    [string]$ImageSource = 'C:\Users\ITD\Desktop\Yakult-inventory-monitoring-system-User-manual-main\borrow_images'
)

$ErrorActionPreference = 'Stop'

Set-StrictMode -Version Latest

function Get-RepoRoot {
    # docs/borrow-manual -> repo root
    return (Resolve-Path (Join-Path $PSScriptRoot '..\\..')).Path
}

function Ensure-Dir([string]$Path) {
    if (-not (Test-Path $Path)) { New-Item -ItemType Directory -Force -Path $Path | Out-Null }
}

function Copy-AssetsIfPresent([string]$assetsDir) {
    $source = $ImageSource
    if (-not (Test-Path $source)) { return }
    Ensure-Dir $assetsDir
    Copy-Item -Path (Join-Path $source '*') -Destination $assetsDir -Force -ErrorAction SilentlyContinue
}

function HtmlEncode([string]$s) {
    if ($null -eq $s) { return '' }
    return [System.Net.WebUtility]::HtmlEncode($s)
}

function As-List($value) {
    if ($null -eq $value) { return @() }
    if ($value -is [string]) { return @($value) }
    if ($value -is [System.Collections.IEnumerable]) { return @($value) }
    return @($value)
}

function Coalesce($value, $fallback) {
    if ($null -eq $value) { return $fallback }
    return $value
}

function Get-Field($obj, [string]$name) {
    if ($null -eq $obj -or [string]::IsNullOrWhiteSpace($name)) { return $null }
    if ($obj -is [hashtable]) {
        if ($obj.ContainsKey($name)) { return $obj[$name] }
        return $null
    }
    try {
        $p = $obj.PSObject.Properties[$name]
        if ($null -ne $p) { return $p.Value }
    } catch { }
    return $null
}

function Build-HtmlDoc {
    param(
        [hashtable]$Manual,
        [string]$OutDocPath,
        [string]$AssetsRelative = 'assets'
    )

    $title = HtmlEncode $Manual.Title
    $subtitle = HtmlEncode $Manual.Subtitle
    $version = HtmlEncode $Manual.Version
    $date = HtmlEncode $Manual.Date

    $css = @"
body{font-family:Segoe UI,Arial,sans-serif;line-height:1.45;color:#111;margin:28px}
h1{font-size:22pt;margin:0 0 2px 0}
h2{font-size:13.5pt;margin:22px 0 8px 0}
p{margin:6px 0}
ul{margin:6px 0 10px 20px}
li{margin:2px 0}
.meta{color:#555;margin:4px 0 16px 0}
.callout{border-left:4px solid #3498db;background:#f3f8ff;padding:10px 12px;margin:10px 0}
.callout.tip{border-left-color:#2ecc71;background:#f1fff6}
.callout.warn{border-left-color:#f39c12;background:#fff8e8}
img{max-width:100%;height:auto;border:1px solid #eee;border-radius:6px;margin:8px 0}
.hr{height:1px;background:#eee;margin:16px 0}
"@

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('<!DOCTYPE html><html><head><meta charset="utf-8">')
    [void]$sb.AppendLine("<title>$title</title>")
    [void]$sb.AppendLine("<style>$css</style></head><body>")

    [void]$sb.AppendLine("<h1>$title</h1>")
    [void]$sb.AppendLine("<div class='meta'>$subtitle<br>$version - $date</div>")
    [void]$sb.AppendLine("<div class='hr'></div>")

    foreach ($n in (As-List $Manual.Notes)) {
        if ([string]::IsNullOrWhiteSpace($n)) { continue }
        [void]$sb.AppendLine("<p><em>Note:</em> $(HtmlEncode $n)</p>")
    }

    foreach ($section in (As-List $Manual.Sections)) {
        $st = HtmlEncode (Coalesce (Get-Field $section 'Title') '')
        if (-not [string]::IsNullOrWhiteSpace($st)) {
            [void]$sb.AppendLine("<h2>$st</h2>")
        }

        foreach ($p in (As-List (Get-Field $section 'Paragraphs'))) {
            if ([string]::IsNullOrWhiteSpace($p)) { continue }
            [void]$sb.AppendLine("<p>$(HtmlEncode $p)</p>")
        }

        $bullets = As-List (Get-Field $section 'Bullets')
        $opened = $false
        foreach ($b in $bullets) {
            if ([string]::IsNullOrWhiteSpace($b)) { continue }
            if (-not $opened) { [void]$sb.AppendLine('<ul>'); $opened = $true }
            [void]$sb.AppendLine("<li>$(HtmlEncode $b)</li>")
        }
        if ($opened) { [void]$sb.AppendLine('</ul>') }

        foreach ($c in (As-List (Get-Field $section 'Callouts'))) {
            $kind = (Coalesce (Get-Field $c 'Kind') 'info').ToString().Trim().ToLowerInvariant()
            if ($kind -notin @('tip','warn','info')) { $kind = 'info' }
            $text = HtmlEncode (Coalesce (Get-Field $c 'Text') '')
            if (-not [string]::IsNullOrWhiteSpace($text)) {
                [void]$sb.AppendLine("<div class='callout $kind'>$text</div>")
            }
        }

        foreach ($img in (As-List (Get-Field $section 'Images'))) {
            if ([string]::IsNullOrWhiteSpace($img)) { continue }
            $src = "$AssetsRelative/$img"
            [void]$sb.AppendLine("<img src='$(HtmlEncode $src)' alt='$(HtmlEncode $img)'>")
        }
    }

    [void]$sb.AppendLine('</body></html>')
    $html = $sb.ToString()
    Set-Content -Path $OutDocPath -Value $html -Encoding UTF8
}

function Wrap-TextLines {
    param(
        $gfx,
        [string]$Text,
        $Font,
        [double]$MaxWidth
    )
    $t = (Coalesce $Text '').Trim()
    if ($t.Length -eq 0) { return @() }

    $words = $t -split '\\s+'
    $lines = New-Object System.Collections.Generic.List[string]
    $current = ''
    foreach ($w in $words) {
        $candidate = if ($current.Length -eq 0) { $w } else { "$current $w" }
        $size = $gfx.MeasureString($candidate, $Font)
        if ($size.Width -le $MaxWidth) {
            $current = $candidate
            continue
        }
        if ($current.Length -gt 0) {
            $lines.Add($current) | Out-Null
            $current = $w
        } else {
            # single long token fallback
            $lines.Add($candidate) | Out-Null
            $current = ''
        }
    }
    if ($current.Length -gt 0) { $lines.Add($current) | Out-Null }
    return $lines.ToArray()
}

function Build-Pdf {
    param(
        [hashtable]$Manual,
        [string]$OutPdfPath,
        [string]$AssetsDir,
        [string]$RepoRoot
    )

    $pdfSharp = Join-Path $RepoRoot 'packages\\PDFsharp.1.50.5147\\lib\\net20\\PdfSharp.dll'
    if (-not (Test-Path $pdfSharp)) { throw "PdfSharp not found at: $pdfSharp" }
    [Reflection.Assembly]::LoadFrom($pdfSharp) | Out-Null

    $doc = New-Object PdfSharp.Pdf.PdfDocument
    $doc.Info.Title = $Manual.Title

    $fontTitle = New-Object PdfSharp.Drawing.XFont('Segoe UI', 20, ([PdfSharp.Drawing.XFontStyle]::Bold))
    $fontSub = New-Object PdfSharp.Drawing.XFont('Segoe UI', 11, ([PdfSharp.Drawing.XFontStyle]::Regular))
    $fontH = New-Object PdfSharp.Drawing.XFont('Segoe UI', 13, ([PdfSharp.Drawing.XFontStyle]::Bold))
    $fontP = New-Object PdfSharp.Drawing.XFont('Segoe UI', 10, ([PdfSharp.Drawing.XFontStyle]::Regular))
    $fontB = New-Object PdfSharp.Drawing.XFont('Segoe UI', 10, ([PdfSharp.Drawing.XFontStyle]::Bold))

    $margin = 48.0
    $page = $doc.AddPage()
    $null = ($page.Size = [PdfSharp.PageSize]::A4)
    $null = ($page.Orientation = [PdfSharp.PageOrientation]::Portrait)
    $gfx = [PdfSharp.Drawing.XGraphics]::FromPdfPage($page)
    $pageIndex = 1

    $pageWidth = $page.Width.Point
    $pageHeight = $page.Height.Point
    $contentWidth = $pageWidth - ($margin * 2)
    $y = $margin

    function Add-Footer {
        param($g, $p, [int]$idx)
        $f = New-Object PdfSharp.Drawing.XFont('Segoe UI', 9)
        $s = "Page $idx"
        $w = $g.MeasureString($s, $f).Width
        $g.DrawString($s, $f, [PdfSharp.Drawing.XBrushes]::Gray, $p.Width.Point - $margin - $w, $p.Height.Point - 28)
    }

    function Ensure-Space {
        param([double]$needed)
        if (($y + $needed) -le ($pageHeight - 50)) { return }
        Add-Footer $gfx $page $pageIndex
        $pageIndex++
        $page = $doc.AddPage()
        $null = ($page.Size = [PdfSharp.PageSize]::A4)
        $null = ($page.Orientation = [PdfSharp.PageOrientation]::Portrait)
        $gfx = [PdfSharp.Drawing.XGraphics]::FromPdfPage($page)
        $pageWidth = $page.Width.Point
        $pageHeight = $page.Height.Point
        $contentWidth = $pageWidth - ($margin * 2)
        $y = $margin
    }

    function Draw-Heading {
        param([string]$text)
        Ensure-Space 26
        $gfx.DrawString($text, $fontH, [PdfSharp.Drawing.XBrushes]::Black, $margin, $y)
        $y += 22
    }

    function Draw-Para {
        param([string]$text, [bool]$bold = $false)
        $font = if ($bold) { $fontB } else { $fontP }
        $lines = Wrap-TextLines -gfx $gfx -Text $text -Font $font -MaxWidth $contentWidth
        if ($lines.Length -eq 0) { return }
        $lineH = 14
        Ensure-Space (($lines.Length * $lineH) + 6)
        foreach ($ln in $lines) {
            $gfx.DrawString($ln, $font, [PdfSharp.Drawing.XBrushes]::Black, $margin, $y)
            $y += $lineH
        }
        $y += 4
    }

    function Draw-Bullets {
        param([string[]]$items)
        if ($null -eq $items -or $items.Length -eq 0) { return }
        foreach ($it in $items) {
            $t = (Coalesce $it '').Trim()
            if ($t.Length -eq 0) { continue }
            $lines = Wrap-TextLines -gfx $gfx -Text $t -Font $fontP -MaxWidth ($contentWidth - 18)
            if ($lines.Length -eq 0) { continue }
            $lineH = 14
            Ensure-Space (($lines.Length * $lineH) + 6)
            $gfx.DrawString(([string][char]0x2022), $fontP, [PdfSharp.Drawing.XBrushes]::Black, $margin, $y)
            $first = $true
            foreach ($ln in $lines) {
                $x = $margin + 14
                $gfx.DrawString($ln, $fontP, [PdfSharp.Drawing.XBrushes]::Black, $x, $y)
                $y += $lineH
                $first = $false
            }
            $y += 2
        }
        $y += 2
    }

    function Draw-Callout {
        param([string]$kind, [string]$text)
        $t = (Coalesce $text '').Trim()
        if ($t.Length -eq 0) { return }
        $border = switch ((Coalesce $kind '').ToLowerInvariant()) {
            'tip' { [PdfSharp.Drawing.XColor]::FromArgb(46, 204, 113) }
            'warn' { [PdfSharp.Drawing.XColor]::FromArgb(243, 156, 18) }
            default { [PdfSharp.Drawing.XColor]::FromArgb(52, 152, 219) }
        }
        $bg = [PdfSharp.Drawing.XColor]::FromArgb(245, 248, 255)
        $lines = Wrap-TextLines -gfx $gfx -Text $t -Font $fontP -MaxWidth ($contentWidth - 20)
        $lineH = 14
        $h = ($lines.Length * $lineH) + 14
        Ensure-Space ($h + 8)
        $rect = New-Object PdfSharp.Drawing.XRect($margin, $y, $contentWidth, $h)
        $gfx.DrawRectangle((New-Object PdfSharp.Drawing.XSolidBrush($bg)), $rect)
        $pen = New-Object PdfSharp.Drawing.XPen($border, 2)
        $gfx.DrawLine($pen, $margin, $y, $margin, $y + $h)
        $ty = $y + 12
        foreach ($ln in $lines) {
            $gfx.DrawString($ln, $fontP, [PdfSharp.Drawing.XBrushes]::Black, $margin + 12, $ty)
            $ty += $lineH
        }
        $y += $h + 8
    }

    function Draw-ImageFile {
        param([string]$fileName)
        $path = Join-Path $AssetsDir $fileName
        if (-not (Test-Path $path)) { return }
        $img = [PdfSharp.Drawing.XImage]::FromFile($path)
        $maxW = $contentWidth
        $w = [Math]::Min($maxW, $img.PixelWidth)
        if ($w -lt 1) { return }
        $h = ($img.PixelHeight / $img.PixelWidth) * $w
        # Fit to remaining page height; if too tall, scale down.
        $availableH = ($pageHeight - 50) - $y
        if ($h -gt $availableH -and $availableH -gt 120) {
            $scale = ($availableH - 10) / $h
            $w = $w * $scale
            $h = $h * $scale
        }
        if ($h -gt (($pageHeight - 50) - $margin)) {
            # Still too big, force new page.
            Ensure-Space ($pageHeight) # triggers new page
        } else {
            Ensure-Space ($h + 10)
        }
        $gfx.DrawImage($img, $margin, $y, $w, $h)
        $y += $h + 10
    }

    # Title page header
    $gfx.DrawString($Manual.Title, $fontTitle, [PdfSharp.Drawing.XBrushes]::Black, $margin, $y)
    $y += 34
    $gfx.DrawString((Coalesce $Manual.Subtitle ''), $fontSub, [PdfSharp.Drawing.XBrushes]::Black, $margin, $y)
    $y += 18
    $gfx.DrawString(("{0} - {1}" -f (Coalesce $Manual.Version ''), (Coalesce $Manual.Date '')), $fontSub, [PdfSharp.Drawing.XBrushes]::Gray, $margin, $y)
    $y += 20

    foreach ($n in (As-List $Manual.Notes)) {
        Draw-Callout 'info' ("Note: " + $n)
    }

    foreach ($section in (As-List $Manual.Sections)) {
        $st = (Coalesce (Get-Field $section 'Title') '').Trim()
        if ($st.Length -gt 0) { Draw-Heading $st }
        foreach ($p in (As-List (Get-Field $section 'Paragraphs'))) { Draw-Para $p }
        Draw-Bullets (As-List (Get-Field $section 'Bullets'))
        foreach ($c in (As-List (Get-Field $section 'Callouts'))) {
            Draw-Callout (Coalesce (Get-Field $c 'Kind') 'info') (Coalesce (Get-Field $c 'Text') '')
        }
        foreach ($img in (As-List (Get-Field $section 'Images'))) { Draw-ImageFile $img }
    }

    Add-Footer $gfx $page $pageIndex
    Ensure-Dir (Split-Path -Parent $OutPdfPath)
    $doc.Save($OutPdfPath)
    $doc.Close()
}

function Main {
    $repoRoot = Get-RepoRoot
    $contentPath = if ([System.IO.Path]::IsPathRooted($ContentFile)) {
        $ContentFile
    } else {
        Join-Path $PSScriptRoot $ContentFile
    }
    if (-not (Test-Path $contentPath)) { throw "Missing content file: $contentPath" }

    . $contentPath
    if ($null -eq $Manual) { throw 'Manual content not loaded.' }

    $baseDir = $PSScriptRoot
    $assetsDir = Join-Path $baseDir 'assets'
    $outDir = Join-Path $baseDir 'out'
    Ensure-Dir $outDir

    Copy-AssetsIfPresent -assetsDir $assetsDir

    $date = (Coalesce $Manual.Date 'YYYY-MM-DD')
    $safePrefix = (Coalesce $OutputPrefix 'ITCM_Manual').Trim()
    if ([string]::IsNullOrWhiteSpace($safePrefix)) { $safePrefix = 'Yakult_Scanner_Manual' }
    $docOut = Join-Path $outDir ("{0}_{1}.doc" -f $safePrefix, $date)
    $pdfOut = Join-Path $outDir ("{0}_{1}.pdf" -f $safePrefix, $date)

    Build-HtmlDoc -Manual $Manual -OutDocPath $docOut -AssetsRelative '..\\assets'
    Build-Pdf -Manual $Manual -OutPdfPath $pdfOut -AssetsDir $assetsDir -RepoRoot $repoRoot

    Write-Host "Generated Word (HTML .doc): $docOut"
    Write-Host "Generated PDF:             $pdfOut"
}

Main
