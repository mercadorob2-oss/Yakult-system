param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '..\_build_verify\ai-expanded\app\Yakult.Inventory.App.exe'),
    [string]$ScriptDomPath,
    [string]$Question,
    [string]$PreviewPath,
    [switch]$HostLifecycle,
    [switch]$OllamaSmoke,
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
    if ($HostLifecycle) { $runnerArguments += '-HostLifecycle' }
    if ($OllamaSmoke) { $runnerArguments += '-OllamaSmoke' }
    if ($LiveConnectionEnvironmentVariable) { $runnerArguments += @('-LiveConnectionEnvironmentVariable',$LiveConnectionEnvironmentVariable) }
    if ($SqlOnly) { $runnerArguments += '-SqlOnly' }
    & "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" @runnerArguments
    exit $LASTEXITCODE
}
try {
    [Reflection.Assembly]::LoadFrom($AssemblyPath) | Out-Null
    if ($LiveConnectionEnvironmentVariable) {
        $connectionText=[Environment]::GetEnvironmentVariable($LiveConnectionEnvironmentVariable)
        if(-not $connectionText) {throw 'The live read-only connection environment variable is empty.'}
        $connectionBuilder=New-Object Data.SqlClient.SqlConnectionStringBuilder -ArgumentList $connectionText
        $connectionBuilder['ApplicationIntent']='ReadOnly';$connectionBuilder['Connect Timeout']=5
        [Yakult.Inventory.App.Core.DatabaseConfig]::Bootstrap($connectionBuilder.ConnectionString)
        $cts=New-Object Threading.CancellationTokenSource -ArgumentList 120000
        try {[Yakult.Inventory.App.Tests.ExpandedAssistantTests]::LiveDataChecksAsync($cts.Token).GetAwaiter().GetResult()} finally {$cts.Dispose();$connectionText=$null;$connectionBuilder.Clear()}
        exit 0
    }
    if ($Question) { [Yakult.Inventory.App.Tests.ExpandedAssistantTests]::InspectQuestions(@($Question)); exit 0 }
    if ($PreviewPath) { [Yakult.Inventory.App.Tests.ExpandedAssistantTests]::RenderPreview([IO.Path]::GetFullPath($PreviewPath)); exit 0 }
    if ($HostLifecycle) { [Yakult.Inventory.App.Tests.ExpandedAssistantTests]::HostLifecycle(); exit 0 }
    if ($OllamaSmoke) {
        Add-Type -AssemblyName System.Configuration
        $configMap=New-Object System.Configuration.ExeConfigurationFileMap
        $configMap.ExeConfigFilename=$AssemblyPath+'.config'
        $config=[Configuration.ConfigurationManager]::OpenMappedExeConfiguration($configMap,[Configuration.ConfigurationUserLevel]::None)
        foreach ($setting in @(@('OllamaBaseUrl','_ollamaBaseUrl'),@('OllamaModel','_ollamaModel'))) {
            $entry=$config.AppSettings.Settings[$setting[0]]
            if ($entry) {[Yakult.Inventory.App.Core.AppConfig].GetField($setting[1],[Reflection.BindingFlags]'Static,NonPublic').SetValue($null,$entry.Value)}
        }
        $cts=New-Object Threading.CancellationTokenSource -ArgumentList 90000
        try { [Yakult.Inventory.App.Tests.ExpandedAssistantTests]::OllamaSmokeAsync($cts.Token).GetAwaiter().GetResult() } finally {$cts.Dispose()}
        exit 0
    }
    if (-not $SqlOnly) {
        [Yakult.Inventory.App.Tests.AiAssistantRegressionTest]::RunAll()
        [Yakult.Inventory.App.Tests.ExpandedAssistantTests]::RunAll()
    }
    if ($ScriptDomPath) {
        [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ScriptDomPath).Path) | Out-Null
        $parser = New-Object Microsoft.SqlServer.TransactSql.ScriptDom.TSql160Parser -ArgumentList $true
        $cases = [Yakult.Inventory.App.Tests.ExpandedAssistantTests]::SqlCases()
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
