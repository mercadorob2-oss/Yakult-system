param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '..\_build_verify\ai-expanded\app\Yakult.Inventory.App.exe'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\docs\Yakumi-Coverage.md')
)
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    & "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -AssemblyPath $AssemblyPath -OutputPath $OutputPath
    exit $LASTEXITCODE
}
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath).Path) | Out-Null
function Field($object,$name) {$object.GetType().GetField($name,[Reflection.BindingFlags]'Instance,NonPublic').GetValue($object)}
function Cell($value) {if($null -eq $value -or @($value).Count -eq 0) {return '-'}; (@($value) -join ', ').Replace('|','/').Replace("`r",' ').Replace("`n",' ')}
$caps=[Yakult.Inventory.App.Services.AssistantCatalog]::All
$guides=[Yakult.Inventory.App.Services.AssistantKnowledge]::Articles
$lines=New-Object 'Collections.Generic.List[string]'
$lines.Add('# Yakumi read-only coverage')
$lines.Add('')
$lines.Add('Generated from the desktop capability, screen and guide registries. This describes implemented scope, not live database or model certification. See Desktop-AI-Review.md for validation and limits.')
$lines.Add('')
$lines.Add("$($caps.Count) data capabilities. Lists show eight rows per page; counts and aggregate sums cover every matching accessible record. Details require a numeric saved ID. Summaries use only the groupings below. History is represented by its own source. Every factual response labels its source, filters, total and retrieval time.")
$lines.Add('')
$lines.Add('## Data sources and permissions')
$lines.Add('')
$lines.Add('Portal alternatives are joined with OR; listed page restrictions apply to the normal route. Explicit Admin, Developer and Super Admin guards apply in addition. Requester access is restricted to CreatedBy=current user unless Inventory/View Requests or Admin/Department History grants broader access. Category restrictions apply before aggregation wherever Category scope is listed. Credentials, signatures, QR tokens and attachment bytes are excluded.')
$lines.Add('')
$lines.Add('| Module / capability | Data source | Filters | Groupings | SQL sums | Date field | Access / scope |')
$lines.Add('| --- | --- | --- | --- | --- | --- | --- |')
foreach ($cap in $caps) {
    $access=@((Cell $cap.PortalKeys),('pages: '+(Cell $cap.PageKeys)))
    foreach($guard in @('AdminOnly','DeveloperOnly','SuperAdminOnly','CategoryScope')) {if(Field $cap $guard) {$access+=$guard}}
    if(Field $cap 'RequesterScope') {$access+='requester own scope'}
    if(Field $cap 'AlternatePortal') {$access+='or '+(Field $cap 'AlternatePortal')+'/'+(Field $cap 'AlternatePage')}
    if(Field $cap 'ArchiveColumn') {$access+='archive excluded by default'}
    if(Field $cap 'DefaultStatus') {$access+='default status: '+(Field $cap 'DefaultStatus')}
    $sql=Field $cap 'BaseSql'
    $sources=@([regex]::Matches($sql,'(?i)\b(?:FROM|JOIN)\s+(dbo\.\[?\w+\]?)') | ForEach-Object {$_.Groups[1].Value} | Select-Object -Unique)
    $lines.Add('| '+$cap.Module+' / '+$cap.Id+' | '+(Cell $sources)+' | '+(Cell $cap.SupportedFilters)+' | '+(Cell (Field $cap 'GroupColumns'))+' | '+(Cell (Field $cap 'AggregateColumns'))+' | '+(Cell (Field $cap 'DefaultDateField'))+' | '+(Cell $access)+' |')
}
$lines.Add('')
$lines.Add('## Registered screen context')
$lines.Add('')
$lines.Add('The shared Ask Yakumi window is available from all nine desktop module families and Ctrl+Shift+Y. Screen detection uses known control types; selected-record context reads only allowlisted positive numeric persisted keys. Editor inputs and arbitrary control text are never read. A screen mapped to a related lookup does not imply the lookup reproduces every UI tab/filter. Acronym wrappers share one control type, so ask for the branch/department source explicitly. Screens without a live adapter receive curated guidance or an explicit unsupported answer.')
$lines.Add('')
$lines.Add('| Module | Page | Data capability | Context |')
$lines.Add('| --- | --- | --- | --- |')
foreach($screen in [Yakult.Inventory.App.Services.AssistantScreens]::All) {
    $kind=if($screen.IsEditor){'workflow only; unsaved fields excluded'}elseif($screen.Capability){'registered saved-record lookup'}else{'guidance only; no data adapter'}
    $lines.Add('| '+$screen.Module+' | '+$screen.PageKey+' | '+(Cell $screen.Capability)+' | '+$kind+' |')
}
$navigation=Get-Content (Join-Path $PSScriptRoot '..\Yakult.Inventory.App\Pages\User\MainForm.cs') -Raw
$registered=@([Yakult.Inventory.App.Services.AssistantScreens]::All | ForEach-Object {$_.PageKey})
$other=@([regex]::Matches($navigation,'HasPageAccess\("([^"]+)"\)') | ForEach-Object {$_.Groups[1].Value} | Sort-Object -Unique | Where-Object {$_ -notin $registered})
$lines.Add('')
$lines.Add('Other page-permission keys in MainForm without a dedicated context adapter: '+(Cell $other)+'. The assistant entry still works, but these keys do not provide selected-record context.')
$lines.Add('')
$lines.Add('## Guide references')
$lines.Add('')
$lines.Add('Authored articles contain reviewed workflow prose with source paths. Navigation-only articles are generated from registered capabilities and do not claim a detailed workflow. At runtime the app loads only its embedded JSON; it does not crawl the repository, configuration files or database credentials.')
$lines.Add('')
$lines.Add('| Guide | Module | Capability | Type | Reviewed references |')
$lines.Add('| --- | --- | --- | --- | --- |')
foreach($guide in $guides) {
    $kind=if($guide.IsNavigationOnly){'navigation only'}else{'authored guide'}
    $lines.Add('| '+$guide.Id+' | '+$guide.Module+' | '+(Cell $guide.Capability)+' | '+$kind+' | '+(Cell $guide.SourceFiles)+' |')
}
[IO.File]::WriteAllLines([IO.Path]::GetFullPath($OutputPath),$lines,(New-Object Text.UTF8Encoding -ArgumentList $false))
"Coverage written: $OutputPath"
