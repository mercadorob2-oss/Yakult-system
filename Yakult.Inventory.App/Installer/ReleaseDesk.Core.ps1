# Shared, read-only inventory functions for the standalone WPF release desk.
function Format-Bytes([long]$Bytes) {
    if ($Bytes -ge 1GB) { return ('{0:N2} GB' -f ($Bytes / 1GB)) }
    if ($Bytes -ge 1MB) { return ('{0:N1} MB' -f ($Bytes / 1MB)) }
    if ($Bytes -ge 1KB) { return ('{0:N0} KB' -f ($Bytes / 1KB)) }
    return ('{0} B' -f $Bytes)
}
function Get-ReleaseRows {
    $rows = @()
    if (-not (Test-Path -LiteralPath $script:releaseFolder -PathType Container)) { return $rows }
    foreach ($directory in Get-ChildItem -LiteralPath $script:releaseFolder -Directory) {
        if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
        if ($directory.Name -notmatch '^\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}$') { continue }
        try { $version = [version]$directory.Name } catch { continue }
        $installer = Join-Path $directory.FullName ('YISP_Setup_' + $version.ToString(4) + '.exe')
        $manifest = Join-Path $directory.FullName 'latest.json'
        $hasInstaller = Test-Path -LiteralPath $installer -PathType Leaf
        $hasManifest = Test-Path -LiteralPath $manifest -PathType Leaf
        $status = if ($hasInstaller -and $hasManifest) { 'Files present' } elseif ($hasInstaller) { 'Installer only' } else { 'Incomplete build' }
        $bytes = if ($hasInstaller) { (Get-Item -LiteralPath $installer).Length } else { 0 }
        $rows += [pscustomobject]@{
            Version = $version.ToString(4)
            Status = $status
            Installer = $installer
            Directory = $directory.FullName
            Size = Format-Bytes $bytes
            Modified = $directory.LastWriteTime
            NumericVersion = $version
        }
    }
    return @($rows | Sort-Object NumericVersion -Descending)
}

function Get-ToolCatalog {
    $catalog = @()
    $roots = @(
        [pscustomobject]@{ Name = 'Desktop publish folder'; Path = $script:publishToolsPath },
        [pscustomobject]@{ Name = 'Installer folder'; Path = $script:installerFolder }
    )
    $extensions = @('.ps1', '.psm1', '.psd1', '.cmd', '.bat', '.lnk', '.url', '.iss', '.sql', '.vbs', '.js', '.wsf', '.sh')
    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root.Path -PathType Container)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $root.Path -Recurse -File -Force -ErrorAction SilentlyContinue) {
            if ($extensions -notcontains $file.Extension.ToLowerInvariant()) { continue }
            $relative = $file.FullName.Substring($root.Path.TrimEnd('\').Length).TrimStart('\')
            $relativeFolder = Split-Path $relative -Parent
            $location = if ($relativeFolder) { Join-Path $root.Name $relativeFolder } else { $root.Name }
            $isArchived = $relative -match '(?i)(^|\\)(backup folder|old version|archive[^\\]*|[^\\]*backup[^\\]*)(\\|$)'
            $kind = switch ($file.Extension.ToLowerInvariant()) {
                '.ps1' { 'PowerShell script'; break }
                '.psm1' { 'PowerShell module'; break }
                '.psd1' { 'PowerShell data or manifest'; break }
                '.cmd' { 'Command script'; break }
                '.bat' { 'Batch script'; break }
                '.lnk' { 'Shortcut'; break }
                '.url' { 'Internet shortcut'; break }
                '.iss' { 'Inno Setup script'; break }
                '.sql' { 'SQL script'; break }
                '.vbs' { 'VBScript'; break }
                '.js' { 'JavaScript script'; break }
                '.wsf' { 'Windows Script File'; break }
                '.sh' { 'Shell script'; break }
                default { $file.Extension }
            }
            $catalog += [pscustomobject]@{
                Name = $file.Name
                Kind = $kind
                Root = $root.Name
                Location = $location
                State = if ($isArchived) { 'Archived copy' } else { 'Current tool' }
                IsArchived = $isArchived
                Path = $file.FullName
                Modified = $file.LastWriteTime
            }
        }
    }
    return @($catalog | Sort-Object IsArchived, Root, Name)
}

