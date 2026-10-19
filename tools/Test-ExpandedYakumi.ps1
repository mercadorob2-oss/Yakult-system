param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '..\_build_verify\ai-expanded\app\Yakult.Inventory.App.exe'),
    [string]$ScriptDomPath,
    [string]$Question,
    [string]$PreviewPath,
    [switch]$SettingsPreview,
    [string]$PresentationPreviews,
    [string]$SpeedPreviews,
    [switch]$HostLifecycle,
    [switch]$OllamaSmoke,
    [switch]$SpeedSmoke,
    [switch]$OrganizationLive,
    [string]$LiveConnectionEnvironmentVariable,
    [switch]$SqlOnly
)
$ErrorActionPreference = 'Stop'
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    $runnerArguments = @('-NoProfile','-STA','-ExecutionPolicy','Bypass','-File',$PSCommandPath,'-AssemblyPath',$AssemblyPath)
    if ($ScriptDomPath) { $runnerArguments += @('-ScriptDomPath',$ScriptDomPath) }
    if ($Question) { $runnerArguments += @('-Question',$Question) }
    if ($PreviewPath) { $runnerArguments += @('-PreviewPath',$PreviewPath) }
    if ($SettingsPreview) { $runnerArguments += '-SettingsPreview' }
    if ($PresentationPreviews) { $runnerArguments += @('-PresentationPreviews',$PresentationPreviews) }
    if ($SpeedPreviews) { $runnerArguments += @('-SpeedPreviews',$SpeedPreviews) }
    if ($HostLifecycle) { $runnerArguments += '-HostLifecycle' }
    if ($OllamaSmoke) { $runnerArguments += '-OllamaSmoke' }
    if ($SpeedSmoke) { $runnerArguments += '-SpeedSmoke' }
    if ($OrganizationLive) { $runnerArguments += '-OrganizationLive' }
    if ($LiveConnectionEnvironmentVariable) { $runnerArguments += @('-LiveConnectionEnvironmentVariable',$LiveConnectionEnvironmentVariable) }
    if ($SqlOnly) { $runnerArguments += '-SqlOnly' }
    & "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" @runnerArguments
    exit $LASTEXITCODE
}
try {
    [Reflection.Assembly]::LoadFrom($AssemblyPath) | Out-Null
    if ($PresentationPreviews) { [Yakult.Inventory.App.Tests.AssistantPresentationTests]::RenderPreviews([IO.Path]::GetFullPath($PresentationPreviews)); exit 0 }
    if ($SpeedPreviews) { [Yakult.Inventory.App.Tests.AssistantPerformanceTests]::RenderPreviews([IO.Path]::GetFullPath($SpeedPreviews)); exit 0 }
    if ($LiveConnectionEnvironmentVariable) {
        $connectionText=[Environment]::GetEnvironmentVariable($LiveConnectionEnvironmentVariable)
        if(-not $connectionText) {throw 'The live read-only connection environment variable is empty.'}
        $connectionBuilder=New-Object Data.SqlClient.SqlConnectionStringBuilder -ArgumentList $connectionText
        $connectionBuilder['ApplicationIntent']='ReadOnly';$connectionBuilder['Connect Timeout']=5
        [Yakult.Inventory.App.Core.DatabaseConfig]::Bootstrap($connectionBuilder.ConnectionString)
        $cts=New-Object Threading.CancellationTokenSource -ArgumentList 120000
        try {
            if($OrganizationLive) {[Yakult.Inventory.App.Tests.AssistantOrganizationTests]::LiveChecksAsync($cts.Token).GetAwaiter().GetResult()}
            else {[Yakult.Inventory.App.Tests.ExpandedAssistantTests]::LiveDataChecksAsync($cts.Token).GetAwaiter().GetResult()}
        } finally {$cts.Dispose();$connectionText=$null;$connectionBuilder.Clear()}
        exit 0
    }
    if ($Question) { [Yakult.Inventory.App.Tests.ExpandedAssistantTests]::InspectQuestions(@($Question)); exit 0 }
    if ($PreviewPath) {
        if($SettingsPreview) {[Yakult.Inventory.App.Tests.ExpandedAssistantTests]::RenderConnectionPreview([IO.Path]::GetFullPath($PreviewPath))}
        else {[Yakult.Inventory.App.Tests.ExpandedAssistantTests]::RenderPreview([IO.Path]::GetFullPath($PreviewPath))}
        exit 0
    }
    if ($HostLifecycle) { [Yakult.Inventory.App.Tests.ExpandedAssistantTests]::HostLifecycle(); exit 0 }
    if ($OllamaSmoke -or $SpeedSmoke) {
        Add-Type -AssemblyName System.Configuration
        $configMap=New-Object System.Configuration.ExeConfigurationFileMap
        $configMap.ExeConfigFilename=$AssemblyPath+'.config'
        $config=[Configuration.ConfigurationManager]::OpenMappedExeConfiguration($configMap,[Configuration.ConfigurationUserLevel]::None)
        foreach ($setting in @(@('OllamaBaseUrl','_ollamaBaseUrl'),@('OllamaModel','_ollamaModel'))) {
            $entry=$config.AppSettings.Settings[$setting[0]]
            if ($entry) {[Yakult.Inventory.App.Core.AppConfig].GetField($setting[1],[Reflection.BindingFlags]'Static,NonPublic').SetValue($null,$entry.Value)}
        }
        $cts=New-Object Threading.CancellationTokenSource -ArgumentList 180000
        try {
            if($SpeedSmoke) {[Yakult.Inventory.App.Tests.AssistantPerformanceTests]::LiveSmokeAsync($cts.Token).GetAwaiter().GetResult()}
            else {[Yakult.Inventory.App.Tests.ExpandedAssistantTests]::OllamaSmokeAsync($cts.Token).GetAwaiter().GetResult()}
        } finally {$cts.Dispose()}
        exit 0
    }
    if (-not $SqlOnly) {
        [Yakult.Inventory.App.Tests.AiAssistantRegressionTest]::RunAll()
        [Yakult.Inventory.App.Tests.ExpandedAssistantTests]::RunAll()
        [Yakult.Inventory.App.Tests.AssistantPresentationTests]::RunAll()
        [Yakult.Inventory.App.Tests.AssistantUnderstandingTests]::RunAll()
        [Yakult.Inventory.App.Tests.AssistantClarificationTests]::RunAll()
        [Yakult.Inventory.App.Tests.AssistantProductMatchingTests]::RunAll()
        [Yakult.Inventory.App.Tests.AssistantFollowUpTests]::RunAll()
        [Yakult.Inventory.App.Tests.AssistantOrganizationTests]::RunAll()
        [Yakult.Inventory.App.Tests.AssistantPerformanceTests]::RunAll()
        [Yakult.Inventory.App.Tests.AssistantWordMappingTests]::RunAll()
    }
    if ($ScriptDomPath) {
        [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ScriptDomPath).Path) | Out-Null
        $parser = New-Object Microsoft.SqlServer.TransactSql.ScriptDom.TSql160Parser -ArgumentList $true
        $cases = [Yakult.Inventory.App.Tests.ExpandedAssistantTests]::SqlCases()
        foreach ($case in [Yakult.Inventory.App.Tests.AssistantProductMatchingTests]::SqlCases().GetEnumerator()) { $cases.Add($case.Key,$case.Value) }
        foreach ($case in [Yakult.Inventory.App.Tests.AssistantFollowUpTests]::SqlCases().GetEnumerator()) { $cases.Add($case.Key,$case.Value) }
        foreach ($case in [Yakult.Inventory.App.Tests.AssistantOrganizationTests]::SqlCases().GetEnumerator()) { $cases.Add($case.Key,$case.Value) }
        foreach ($case in [Yakult.Inventory.App.Tests.AssistantWordMappingTests]::SqlCases().GetEnumerator()) { $cases.Add($case.Key,$case.Value) }
        foreach ($case in $cases.GetEnumerator()) {
            $errors = $null
            $reader = New-Object IO.StringReader -ArgumentList $case.Value
            try { $script = $parser.Parse($reader,[ref]$errors) }
            finally { $reader.Dispose() }
            if ($errors.Count) { throw "SQL syntax error in $($case.Key): $($errors | Out-String)" }
            foreach ($batch in $script.Batches) {
                foreach ($statement in $batch.Statements) {
                    if ($statement.GetType().Name -ne 'SelectStatement') { throw "Non-SELECT statement in $($case.Key)." }
                }
            }
            if (@($script.ScriptTokenStream | Where-Object { $_.TokenType.ToString() -eq 'Into' }).Count) {
                throw "SELECT INTO is forbidden: $($case.Key)."
            }
        }
        "$($cases.Count) expanded query variants passed offline SQL syntax and SELECT-only validation."
    }
}
catch {
    Write-Error $_
    exit 1
}
