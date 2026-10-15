param(
    [Parameter(Mandatory=$true)][string]$ConnectionStringEnvironmentVariable,
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '..\_build_verify\ai-expanded\app\Yakult.Inventory.App.exe')
)
# Optional metadata-only check. Never starts Program.Main or executes the underlying record queries.
# Supply a SELECT-only SQL login via an environment variable; do not put credentials on the command line.
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    & "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -AssemblyPath $AssemblyPath -ConnectionStringEnvironmentVariable $ConnectionStringEnvironmentVariable
    exit $LASTEXITCODE
}
$connectionText=[Environment]::GetEnvironmentVariable($ConnectionStringEnvironmentVariable)
if ([string]::IsNullOrWhiteSpace($connectionText)) {throw 'The specified connection-string environment variable is empty.'}
$builder=New-Object Data.SqlClient.SqlConnectionStringBuilder -ArgumentList $connectionText
$builder['ApplicationIntent']='ReadOnly'
$builder['Connect Timeout']=5
$builder['Persist Security Info']=$false
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath).Path) | Out-Null
$cases=[Yakult.Inventory.App.Tests.ExpandedAssistantTests]::SqlCases()
$declarations=[Yakult.Inventory.App.Tests.ExpandedAssistantTests]::SqlParameterDeclarations()
$connection=New-Object Data.SqlClient.SqlConnection -ArgumentList $builder.ConnectionString
$failed=0
try {
    $connection.Open()
    foreach ($case in $cases.GetEnumerator()) {
        $command=$connection.CreateCommand()
        $command.CommandTimeout=15
        $command.CommandText='SELECT error_number, error_message FROM sys.dm_exec_describe_first_result_set(@sql, @parameters, 0) WHERE error_number IS NOT NULL;'
        [void]$command.Parameters.Add('@sql',[Data.SqlDbType]::NVarChar,-1)
        [void]$command.Parameters.Add('@parameters',[Data.SqlDbType]::NVarChar,-1)
        $command.Parameters['@sql'].Value=$case.Value
        $command.Parameters['@parameters'].Value=$declarations[$case.Key]
        try {
            $reader=$command.ExecuteReader()
            try {while($reader.Read()) { $failed++; "$($case.Key): SQL metadata error $($reader.GetInt32(0)): $($reader.GetString(1))" }} finally {$reader.Dispose()}
        }
        catch { $failed++; "$($case.Key): metadata validation failed. Check schema and SELECT/metadata permissions." }
        finally {$command.Dispose()}
    }
}
catch {throw 'Could not connect for metadata validation. No connection string or credentials are printed.'}
finally {$connection.Dispose();$connectionText=$null;$builder.Clear()}
if ($failed) {throw "$failed deployed schema checks failed. No business queries or database modifications were executed."}
"$($cases.Count) generated SELECTs compiled against deployed metadata. No business records were read or changed. Counts and user-permission parity still require an authenticated desktop session."
