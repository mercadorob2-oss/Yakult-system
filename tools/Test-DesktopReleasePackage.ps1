#Requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$FixtureRoot)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')).TrimEnd('\') + '\'
$FixtureRoot = [IO.Path]::GetFullPath($FixtureRoot).TrimEnd('\')
if (-not $FixtureRoot.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $FixtureRoot -Leaf) -notlike 'desktop-update-checks-*') { throw 'Package checks require an isolated desktop-update-checks artifact folder.' }
$root = Join-Path $FixtureRoot 'package-checks'
New-Item -ItemType Directory -Path $root -Force | Out-Null
$compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Inno Setup 6 is required for the isolated package checks.' }
$payload = Join-Path $root 'payload'
$packages = Join-Path $root 'packages'
New-Item -ItemType Directory -Path $payload,$packages -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'Yakult.DesktopUpdater\bin\Release\Yakult.DesktopUpdater.exe') -Destination $payload
# The application fixture is never executed. Only the real helper's headless XML merge runs.
Copy-Item -LiteralPath (Join-Path $FixtureRoot 'fixture.exe') -Destination (Join-Path $payload 'Yakult.Inventory.App.exe')
$packagedXml = '<configuration><appSettings><add key="ApiBaseUrl" value="https://default.example"/><add key="DesktopUpdateShare" value="default-share"/><add key="NewSetting" value="new-default"/></appSettings><runtime><assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1"><dependentAssembly><assemblyIdentity name="Example" publicKeyToken="0000000000000000" culture="neutral"/><bindingRedirect oldVersion="0.0.0.0-9.0.0.0" newVersion="2.0.0.0"/></dependentAssembly></assemblyBinding></runtime></configuration>'
[IO.File]::WriteAllText((Join-Path $payload 'Yakult.Inventory.App.exe.config'),$packagedXml)
foreach ($name in @('appsettings.Development.json','appsettings.Local.json')) {
    [IO.File]::WriteAllText((Join-Path $payload $name),'checkout settings must never ship')
}
$defaultJson = '{"ConnectionStrings":{"DefaultConnection":"Server=working.example;Database=Yakult_Inventory_System_DEV;Integrated Security=True"}}'
[IO.File]::WriteAllText((Join-Path $payload 'appsettings.json'),$defaultJson)
$productionJson = '{"ConnectionStrings":{"DefaultConnection":"Server=production.example;Database=Production;Integrated Security=True"}}'
$deploymentSettings = Join-Path $root 'explicit-production.json'
[IO.File]::WriteAllText($deploymentSettings,$productionJson)

# Keep production file selection and upgrade hooks. Isolate identity and disable all integration/launch actions.
$source = [IO.File]::ReadAllText((Join-Path $repo 'Yakult.Inventory.App\Installer\DesktopRelease.iss'))
$source = [regex]::Replace($source,'(?m)^AppId=.*$',('AppId={{' + [guid]::NewGuid().ToString().ToUpperInvariant() + '}'))
$source = $source.Replace('UsePreviousAppDir=yes',"UsePreviousAppDir=no`r`nUninstallable=no`r`nCreateUninstallRegKey=no")
$source = [regex]::Replace($source,'(?ms)^\[Icons\].*?(?=^\[Code\])','')
$script = Join-Path $root 'PackageSmoke.iss'
[IO.File]::WriteAllText($script,$source)
foreach ($kind in @('default','provisioned')) {
    $compilerArguments = @('/Q',"/DBuildOutput=$payload","/DReleaseOutput=$packages","/DInstallerBaseName=smoke-$kind")
    if ($kind -eq 'provisioned') { $compilerArguments += "/DReleaseDatabaseSettings=$deploymentSettings" }
    & $compiler @compilerArguments $script
    if ($LASTEXITCODE -ne 0) { throw "Could not compile the $kind package fixture." }
}

function Invoke-PackageFixture([string]$Kind,[string]$Directory) {
    $resolvedTarget = [IO.Path]::GetFullPath($Directory)
    if (-not $resolvedTarget.StartsWith($root.TrimEnd('\') + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe package fixture installation target.' }
    $arguments = '/SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RESTARTEXITCODE=3010 /DIR="' + $resolvedTarget + '" /LOG="' + (Join-Path $root ($Kind + '-' + [guid]::NewGuid().ToString('N') + '.log')) + '"'
    $setup = Join-Path $packages "smoke-$Kind.exe"
    $verifiedBytes = [IO.File]::Open($setup,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    try {
        $process = Start-Process -FilePath $setup -ArgumentList $arguments -PassThru -WindowStyle Hidden
        if (-not $process.WaitForExit(30000)) { throw 'The isolated installer fixture did not finish in 30 seconds.' }
        if ($process.ExitCode -ne 0) { throw "The isolated installer fixture failed (code $($process.ExitCode))." }
    } finally { $verifiedBytes.Dispose() }
}

$fresh = Join-Path $root 'fresh working database'
Invoke-PackageFixture 'default' $fresh
if (@(Get-ChildItem -LiteralPath $fresh -Filter 'appsettings*.json').Count -ne 1 -or [IO.File]::ReadAllText((Join-Path $fresh 'appsettings.json')) -ne $defaultJson) { throw 'The default installer must include the selected working database and exclude extra environment files.' }

$existing = Join-Path $root 'existing production installation'
Invoke-PackageFixture 'provisioned' $existing
if ([IO.File]::ReadAllText((Join-Path $existing 'appsettings.json')) -ne $productionJson) { throw 'Explicit database provisioning failed.' }
if (Test-Path -LiteralPath (Join-Path $existing 'appsettings.Development.json')) { throw 'Development settings leaked into the provisioned package.' }
$customXml = $packagedXml.Replace('https://default.example','https://custom.example').Replace('default-share','custom-share').Replace('<add key="NewSetting" value="new-default"/>','').Replace('newVersion="2.0.0.0"','newVersion="1.0.0.0"')
$installedConfig = Join-Path $existing 'Yakult.Inventory.App.exe.config'
[IO.File]::WriteAllText($installedConfig,$customXml)
$existingJson = $productionJson.Replace('Database=Production','Database=ExistingProduction')
[IO.File]::WriteAllText((Join-Path $existing 'appsettings.json'),$existingJson)
Invoke-PackageFixture 'default' $existing
[xml]$merged = [IO.File]::ReadAllText($installedConfig)
$settings = @{}
foreach ($entry in $merged.configuration.appSettings.add) { $settings[[string]$entry.key] = [string]$entry.value }
if ($settings.ApiBaseUrl -ne 'https://custom.example' -or $settings.DesktopUpdateShare -ne 'custom-share' -or $settings.NewSetting -ne 'new-default') { throw 'The real installer did not preserve EXE overrides and add new defaults.' }
if ($merged.SelectSingleNode("//*[local-name()='bindingRedirect']").GetAttribute('newVersion') -ne '2.0.0.0') { throw 'The real installer lost the new binding redirect.' }
Invoke-PackageFixture 'provisioned' $existing
if ([IO.File]::ReadAllText((Join-Path $existing 'appsettings.json')) -ne $existingJson) { throw 'An upgrade replaced the existing production database settings.' }
if (@(Get-ChildItem -LiteralPath $existing -Filter 'appsettings*.json').Count -ne 1) { throw 'An upgrade added an environment override.' }
Write-Host 'PASS: isolated Inno packages provision the working database, exclude extra environment files, and preserve existing JSON/EXE settings and new binding redirects. No application or database was opened.'
