#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Version,
    [string]$ReleaseNotes = '',
    [string]$OutputDirectory,
    [string]$InnoCompiler,
    [string]$SignTool,
    [string]$DatabaseSettingsPath,
    [switch]$Publish,
    [string]$UpdateFolder = '\\ypi-hqfs\my_dept$\IT\ITDSharedFolder\Russel\Yakult_Desktop_Installer\New Versions\patch_versions'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
# The historical _DEV catalog is this application's working database, not the dummy database.
if (-not $DatabaseSettingsPath) { $DatabaseSettingsPath = Join-Path $repoRoot 'Yakult.Inventory.App\appsettings.json' }
$DatabaseSettingsPath = (Get-Item -LiteralPath $DatabaseSettingsPath).FullName
$databaseSettings = Get-Content -LiteralPath $DatabaseSettingsPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace([string]$databaseSettings.ConnectionStrings.DefaultConnection)) { throw 'The supplied database settings must contain ConnectionStrings.DefaultConnection.' }
if ($Version -notmatch '^\d{1,5}\.\d{1,5}(\.\d{1,5}){0,2}$') { throw 'Use a version such as 1.6.0.0.' }
$inputVersion = [version]$Version
foreach ($part in @($inputVersion.Major,$inputVersion.Minor,$inputVersion.Build,$inputVersion.Revision)) { if ($part -gt 65534) { throw 'Version components must be at most 65534.' } }
if ($ReleaseNotes.Length -gt 16000) { throw 'Release notes must be at most 16000 characters.' }
$releaseVersion = [version]::new($inputVersion.Major,$inputVersion.Minor,[Math]::Max(0,$inputVersion.Build),[Math]::Max(0,$inputVersion.Revision)).ToString(4)
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot "artifacts\desktop-releases\$releaseVersion" }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$lockFolder = Join-Path $repoRoot 'artifacts\desktop-releases'
[void][IO.Directory]::CreateDirectory($lockFolder)
try { $buildLock = [IO.File]::Open((Join-Path $lockFolder '.build.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None) }
catch { throw 'Another desktop release build is running for this checkout. Wait for it to finish before retrying.' }
try {
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$buildOutput = Join-Path $OutputDirectory ('build-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildOutput -Force | Out-Null
$versionSource = Join-Path $OutputDirectory 'ReleaseVersion.cs'
[IO.File]::WriteAllText($versionSource,'[assembly: System.Reflection.AssemblyFileVersion("' + $releaseVersion + '")]')
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Install Visual Studio Build Tools with the .NET Framework 4.8 desktop build tools.' }
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild was not found.' }
if (-not $InnoCompiler) {
    foreach($candidate in @((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),(Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),(Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'))) {
        if (Test-Path -LiteralPath $candidate) { $InnoCompiler = $candidate; break }
    }
}
if (-not $InnoCompiler -or -not (Test-Path -LiteralPath $InnoCompiler)) { throw 'Install the Inno Setup compiler, or specify -InnoCompiler.' }
Write-Host "Building Yakult $releaseVersion..."
& $msbuild (Join-Path $repoRoot 'Yakult.Inventory.App\Yakult.Inventory.App.csproj') /t:Build /p:Configuration=Release /p:Platform=AnyCPU "/p:OutDir=$buildOutput/" "/p:DesktopVersionSource=$versionSource" /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed. Nothing was published.' }
$app = Join-Path $buildOutput 'Yakult.Inventory.App.exe'
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($app).FileVersion -ne $releaseVersion) { throw 'The application version was not stamped correctly.' }
if (-not (Test-Path -LiteralPath (Join-Path $buildOutput 'Yakult.DesktopUpdater.exe'))) { throw 'The updater component is missing from the build.' }
# Ship the selected base connection; omit extra environment files that could override it.
foreach ($settingsFile in Get-ChildItem -LiteralPath $buildOutput -File -Filter 'appsettings*.json') {
    Remove-Item -LiteralPath $settingsFile.FullName -Force
}
Copy-Item -LiteralPath $DatabaseSettingsPath -Destination (Join-Path $buildOutput 'appsettings.json') -Force
$baseName = 'YISP_Setup_' + $releaseVersion
$compilerArgs = @('/Qp',"/DReleaseVersion=$releaseVersion","/DBuildOutput=$buildOutput","/DReleaseOutput=$OutputDirectory","/DInstallerBaseName=$baseName")
if ($SignTool) { $compilerArgs += "/DReleaseSignTool=$SignTool" }
$compilerArgs += "/DReleaseDatabaseSettings=$(Join-Path $buildOutput 'appsettings.json')"
$compilerArgs += (Join-Path $PSScriptRoot 'DesktopRelease.iss')
& $InnoCompiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed. Nothing was published.' }
$installer = Join-Path $OutputDirectory ($baseName + '.exe')
& (Join-Path $PSScriptRoot 'Publish-DesktopRelease.ps1') -InstallerPath $installer -Version $releaseVersion -ReleaseNotes $ReleaseNotes -ManifestOnly -ManifestOutput (Join-Path $OutputDirectory 'latest.json')
Write-Host "Installer ready: $installer"
if ($Publish) { & (Join-Path $PSScriptRoot 'Publish-DesktopRelease.ps1') -InstallerPath $installer -Version $releaseVersion -ReleaseNotes $ReleaseNotes -UpdateFolder $UpdateFolder }
else { Write-Host 'Created locally. The shared update folder has not been changed.' }

} finally { $buildLock.Dispose() }
