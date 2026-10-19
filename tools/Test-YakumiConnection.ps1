param(
    [string]$AssemblyPath=(Join-Path $PSScriptRoot '..\_build_verify\ai-expanded\app\Yakult.Inventory.App.exe'),
    [string]$ServerUrl,
    [string]$Model='qwen2.5:3b',
    [switch]$Persistence
)
# Explicit connection/persistence checks only. Never starts Program.Main or connects to a database.
$ErrorActionPreference='Stop'
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
if($PSVersionTable.PSEdition -ne 'Desktop') {
    $taskArguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$PSCommandPath,'-AssemblyPath',$AssemblyPath,'-Model',$Model)
    if($ServerUrl) {$taskArguments+=@('-ServerUrl',$ServerUrl)}
    if($Persistence) {$taskArguments+='-Persistence'}
    & "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" @taskArguments
    exit $LASTEXITCODE
}
if(-not $ServerUrl -and -not $Persistence) {throw 'Specify -ServerUrl or -Persistence.'}
if($ServerUrl) {
    [Reflection.Assembly]::LoadFrom($AssemblyPath) | Out-Null
    $taskCts=New-Object Threading.CancellationTokenSource -ArgumentList 20000
    try {
        $taskCheck=[Yakult.Inventory.App.Services.OllamaConnectionSettings]::ValidateAsync($ServerUrl,$Model,$taskCts.Token).GetAwaiter().GetResult()
        if(-not $taskCheck.ModelAvailable) {throw 'Server reachable, but the selected model is not installed.'}
        "Live connection passed: Ollama $($taskCheck.Version), model $($taskCheck.Model), $($taskCheck.Models.Count) installed models. No settings were saved."
    } finally {$taskCts.Dispose()}
}
if($Persistence) {
    Add-Type -AssemblyName System.Configuration
    $taskProbeName='YakumiSettingsProbe_'+[guid]::NewGuid().ToString('N')
    $taskProbe=Join-Path (Split-Path -Parent $AssemblyPath) ($taskProbeName+'.exe')
    $taskUserConfig=$null
    $taskOriginalConfig=$AssemblyPath+'.config'
    $taskOriginalHash=(Get-FileHash -LiteralPath $taskOriginalConfig).Hash
    $taskCompiler=New-Object Microsoft.CSharp.CSharpCodeProvider
    try {
        $taskParameters=New-Object System.CodeDom.Compiler.CompilerParameters
        $taskParameters.GenerateExecutable=$true
        $taskParameters.OutputAssembly=$taskProbe
        $taskParameters.CompilerOptions='/platform:x64'
        [void]$taskParameters.ReferencedAssemblies.Add('System.dll')
        [void]$taskParameters.ReferencedAssemblies.Add('System.Configuration.dll')
        [void]$taskParameters.ReferencedAssemblies.Add($AssemblyPath)
        $taskSource=@'
using System;
using System.Configuration;
using Yakult.Inventory.App.Core;
public static class YakumiSettingsProbe {
    public static int Main(string[] args) {
        try {
            if(args[0]=="write") AppConfig.SetOllamaConnection("http://settings-probe.invalid:11434", "qwen2.5:3b");
            if(args[0]=="reject") {
                bool rejected=false;
                try {AppConfig.SetOllamaConnection("ftp://invalid", "another-model");}
                catch(ArgumentException) {rejected=true;}
                if(!rejected) throw new Exception("Invalid settings were accepted.");
            }
            if(args[0]!="path" && (AppConfig.OllamaBaseUrl!="http://settings-probe.invalid:11434" || AppConfig.OllamaModel!="qwen2.5:3b"))
                throw new Exception("Settings did not persist as a pair across process restarts.");
            Console.WriteLine(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath);
            return 0;
        } catch(Exception ex) {Console.Error.WriteLine(ex.Message);return 1;}
    }
}
'@
        $taskCompilation=$taskCompiler.CompileAssemblyFromSource($taskParameters,$taskSource)
        if($taskCompilation.Errors.HasErrors) {throw ($taskCompilation.Errors | Out-String)}
        $taskCandidate=(& $taskProbe path | Select-Object -Last 1)
        if($LASTEXITCODE -ne 0) {throw 'Could not resolve the isolated probe settings path.'}
        $taskCandidate=[IO.Path]::GetFullPath($taskCandidate)
        $taskLocalRoot=[IO.Path]::GetFullPath($env:LOCALAPPDATA).TrimEnd('\')+'\'
        # .NET truncates the friendly executable name to 25 characters in this path.
        $taskProbeRoot=Join-Path $taskLocalRoot 'YakumiSettingsProbe'
        if(-not $taskCandidate.StartsWith($taskProbeRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or
           -not $taskCandidate.Contains('\'+$taskProbeName.Substring(0,25)+'_Url_') -or (Test-Path -LiteralPath $taskCandidate)) {
            throw "The probe settings path is not a fresh, isolated test profile: $taskCandidate. Nothing was saved."
        }
        $taskUserConfig=$taskCandidate
        foreach($taskMode in @('write','read','reject','read')) {
            $taskResult=(& $taskProbe $taskMode | Select-Object -Last 1)
            if($LASTEXITCODE -ne 0 -or $taskResult -ne $taskUserConfig) {throw "Isolated persistence check failed: $taskMode"}
        }
        if((Get-FileHash -LiteralPath $taskOriginalConfig).Hash -ne $taskOriginalHash -or (Test-Path -LiteralPath ($taskProbe+'.config'))) {
            throw 'Connection persistence unexpectedly changed an executable configuration.'
        }
        'Persistence passed: URL/model survive process restarts, invalid input keeps the saved pair, and executable config is unchanged. Only a fresh isolated test profile was written.'
    } finally {
        $taskCompiler.Dispose()
        if($taskUserConfig -and (Test-Path -LiteralPath $taskUserConfig)) {Remove-Item -LiteralPath $taskUserConfig -Force}
        if(Test-Path -LiteralPath $taskProbe) {Remove-Item -LiteralPath $taskProbe -Force}
    }
}
