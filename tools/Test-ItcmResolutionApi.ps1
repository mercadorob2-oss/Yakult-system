$ErrorActionPreference = 'Stop'
$repoPath = Split-Path $PSScriptRoot -Parent
$apiPath = Join-Path $repoPath 'Yakult.Inventory.Api2_remote'
$checkPath = Join-Path ([IO.Path]::GetTempPath()) ('yakult-resolution-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $checkPath | Out-Null
$handlerPath = Join-Path $checkPath 'CallTicketAction.cs'
$lookupPath = Join-Path $checkPath 'CallItemsLookup.cs'
$testPath = Join-Path $checkPath 'ResolutionTests.cs'
$outputPath = Join-Path $checkPath 'ResolutionTests.exe'
[IO.File]::WriteAllText($handlerPath, ((Get-Content (Join-Path $apiPath 'call-ticket-action.ashx') -Raw) -replace '^<%@[^\r\n]+\r?\n',''))
[IO.File]::WriteAllText($lookupPath, ((Get-Content (Join-Path $apiPath 'call-items-lookup.ashx') -Raw) -replace '^<%@[^\r\n]+\r?\n',''))
@'
using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
public static class ResolutionTests {
    static int count;
    static void Check(string name, string json, bool valid) {
        var lines=JsonConvert.DeserializeObject<List<Dictionary<string,object>>>(json);
        var method=typeof(CallTicketActionHandler).GetMethod("ValidateReplacementLines",BindingFlags.Static|BindingFlags.NonPublic);
        var error=(string)method.Invoke(null,new object[]{lines});
        if((error==null)!=valid) throw new Exception(name+": "+(error??"Unexpected success"));
        count++;
    }
    static object Invoke(string method, params object[] args) {
        return typeof(CallTicketActionHandler).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
    }
    static void PullOut(string name,string json,bool valid) {
        var lines=JsonConvert.DeserializeObject<List<Dictionary<string,object>>>(json);
        var error=(string)Invoke("ValidatePullOutLines",lines);
        if((error==null)!=valid) throw new Exception(name+": "+(error??"Unexpected success"));
        count++;
    }
    static void Equal(string name,object expected,object actual) {
        if(!object.Equals(expected,actual)) throw new Exception(name+": expected "+expected+", got "+actual);
        count++;
    }
    public static void Main() {
        const string old="{ 'oldItemId':1, 'quantity':1, 'oldItemConditionId':2, 'oldItemRepairAction':'Unrepaired' }";
        const string issued="{ 'newItemId':10, 'quantity':2 }";
        Check("Collect only", "["+old+"]",true);
        Check("Issue only without old condition", "["+issued+"]",true);
        Check("Unequal counts and quantities", "["+old+",{'oldItemId':2,'quantity':1,'oldItemConditionId':2,'oldItemRepairAction':'Repaired'},"+issued+"]",true);
        Check("Duplicate collected item", "["+old+","+old+"]",false);
        Check("Collected item also issued", "["+old+",{'newItemId':1,'quantity':1}]",false);
        Check("Empty batch", "[]",false);
        Check("Null entry", "[null]",false);
        Check("Empty entry", "[{}]",false);
        Check("Negative quantity", "[{'newItemId':10,'quantity':-1}]",false);
        Check("Missing old condition", "[{'oldItemId':1,'quantity':1}]",false);
        Check("Missing unlisted details", "[{'useUnlistedOldItem':true,'quantity':1,'oldItemConditionId':2,'oldItemRepairAction':'Repaired'}]",false);
        const string unlisted="{'useUnlistedOldItem':true,'quantity':1,'oldItemConditionId':2,'oldItemRepairAction':'Repaired','unlistedOldItemName':'PC','unlistedOldItemModelNumber':'M','unlistedOldItemUnitOfMeasure':'Unit','unlistedOldItemCategoryId':1,'unlistedOldItemSerialNumber':' abc '}";
        Check("Unlisted collected item", "["+unlisted+"]",true);
        Check("Duplicate serial case and spaces", "["+unlisted+","+unlisted.Replace(" abc ","ABC")+"]",false);
        PullOut("Pull out listed item without replacement stock","["+old+"]",true);
        PullOut("Pull out unlisted item","["+unlisted+"]",true);
        PullOut("Pull out cannot issue new stock","["+old+","+issued+"]",false);
        PullOut("Pull out cannot swap items","[{'oldItemId':1,'newItemId':10,'quantity':1,'oldItemConditionId':2,'oldItemRepairAction':'Repaired'}]",false);
        PullOut("Pull out requires old item","["+issued+"]",false);
        PullOut("Pull out requires condition","[{'oldItemId':1,'quantity':1}]",false);
        PullOut("Pull out rejects empty batch","[]",false);
        Equal("Pull out forced temporary","Resolved (Temporary)",Invoke("GetResolutionStatus","Pull Out",false,false));
        Equal("Old-only replacement remains permanent","Solved",Invoke("GetResolutionStatus","Replacement",true,false));
        Equal("Temporary issued replacement","Resolved (Temporary)",Invoke("GetResolutionStatus","Replacement",true,true));
        Equal("Temporary service","Resolved (Temporary)",Invoke("GetResolutionStatus","Service Only",true,false));
        Equal("Temporary pull out can complete",true,Invoke("CanCompletePullOut","Resolved (Temporary)","Pull Out"));
        Equal("Solved pull out cannot repeat",false,Invoke("CanCompletePullOut","Solved","Pull Out"));
        Equal("Reopened pull out cannot complete",false,Invoke("CanCompletePullOut","Reopened","Pull Out"));
        Equal("Replacement cannot bypass returns",false,Invoke("CanCompletePullOut","Resolved (Temporary)","Replacement"));
        Equal("Missing resolution cannot complete",false,Invoke("CanCompletePullOut","Resolved (Temporary)",""));
        Console.WriteLine("API resolution validation: "+count+" scenarios passed. Both handlers compiled.");
    }
}
'@ | Set-Content -LiteralPath $testPath -Encoding UTF8
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$refs = @('System.Web.dll','System.Configuration.dll','System.Data.dll','System.Core.dll','System.Net.Http.dll', (Join-Path $apiPath 'bin/Newtonsoft.Json.dll'), (Join-Path $apiPath 'bin/Yakult.Inventory.Api2.CallTicketSecurity.dll'), (Join-Path $apiPath 'bin/App_Code.dll'))
$compileArgs = @('/nologo','/target:exe',"/out:$outputPath") + @($refs | ForEach-Object { '/r:' + $_ }) + @($handlerPath,$lookupPath,$testPath)
& $compilerPath @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'API compilation failed.' }
Copy-Item -LiteralPath (Join-Path $apiPath 'bin/Newtonsoft.Json.dll') -Destination $checkPath
& $outputPath
if ($LASTEXITCODE -ne 0) { throw 'API validation scenarios failed.' }
