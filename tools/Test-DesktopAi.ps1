param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '..\_build_verify\ai-assistant\app\Yakult.Inventory.App.exe'),
    [string]$ScriptDomPath
)

$ErrorActionPreference = 'Stop'
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    $runnerArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath, '-AssemblyPath', $AssemblyPath)
    if ($ScriptDomPath) { $runnerArguments += @('-ScriptDomPath', $ScriptDomPath) }
    & "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" @runnerArguments
    exit $LASTEXITCODE
}

try {
    # Loading the assembly does not run Program.Main, open the app, or connect to SQL/Ollama.
    [Reflection.Assembly]::LoadFrom($AssemblyPath) | Out-Null
    [Yakult.Inventory.App.Tests.AiAssistantRegressionTest]::RunAll()
    if ($ScriptDomPath) {
        [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ScriptDomPath).Path) | Out-Null
        $parser = New-Object Microsoft.SqlServer.TransactSql.ScriptDom.TSql160Parser -ArgumentList $true
        $provider = [Yakult.Inventory.App.Services.AiContextProviders]
        $buildQuery = $provider.GetMethod('BuildQuery', [Reflection.BindingFlags]'Static,NonPublic')
        [Yakult.Inventory.App.Session.AppSession]::CurrentEmployeeId = 202
        [Yakult.Inventory.App.Session.AppSession]::IsDeveloper = $true
        $questions = @('keyboard stock','toner stock','cartridge stock','refilled cartridge stock',
            'who is John Smith','borrowing policy','my tickets','how many solved tickets','TCK-69',
            'my requests','my pending requests','REQ-123','my borrowed items','my returned borrows',
            'borrow history','renewals expiring','expired renewals','When does Adobe expire?','SET-1')
        foreach ($question in $questions) {
            $route = [Yakult.Inventory.App.Services.AiIntentRouter]::Route($question)
            $query = $buildQuery.Invoke($null, @($route))
            $sql = $query.GetType().GetField('Sql').GetValue($query)
            $parseErrors = $null
            $reader = New-Object IO.StringReader -ArgumentList $sql
            try { $null = $parser.Parse($reader, [ref]$parseErrors) }
            finally { $reader.Dispose() }
            if ($parseErrors.Count) { throw "SQL syntax errors for '$question': $($parseErrors | Out-String)" }
        }
        "$($questions.Count) generated SQL statements passed offline syntax validation."
    }
}
catch {
    Write-Error $_
    exit 1
}
