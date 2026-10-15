param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '..\_build_verify\ai-expanded\app\Yakult.Inventory.App.exe'),
    [string]$ScriptDomPath = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\Extensions\Microsoft\SQLCommon\Microsoft.SqlServer.TransactSql.ScriptDom.dll'
)
# Offline source-schema lint only. This script never opens a database connection.
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    & "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -AssemblyPath $AssemblyPath -ScriptDomPath $ScriptDomPath
    exit $LASTEXITCODE
}
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath).Path) | Out-Null
[Reflection.Assembly]::LoadFrom((Resolve-Path $ScriptDomPath).Path) | Out-Null
Add-Type -ReferencedAssemblies $ScriptDomPath -TypeDefinition @'
using System;
using System.Collections.Generic;
using Microsoft.SqlServer.TransactSql.ScriptDom;
public class YakumiSchemaVisitor : TSqlFragmentVisitor {
    public readonly Dictionary<string,HashSet<string>> Tables = new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    void Add(string table, IEnumerable<string> columns) {
        HashSet<string> set; if (!Tables.TryGetValue(table,out set)) Tables[table]=set=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string column in columns) if(column!=null) set.Add(column);
    }
    IEnumerable<string> Columns(TableDefinition definition) {foreach(var c in definition.ColumnDefinitions) yield return c.ColumnIdentifier.Value;}
    public override void ExplicitVisit(CreateTableStatement node) {Add(node.SchemaObjectName.BaseIdentifier.Value,Columns(node.Definition));base.ExplicitVisit(node);}
    public override void ExplicitVisit(AlterTableAddTableElementStatement node) {Add(node.SchemaObjectName.BaseIdentifier.Value,Columns(node.Definition));base.ExplicitVisit(node);}
    IEnumerable<string> Projection(QueryExpression query) {
        var binary=query as BinaryQueryExpression; if(binary!=null) return Projection(binary.FirstQueryExpression);
        var parens=query as QueryParenthesisExpression; if(parens!=null) return Projection(parens.QueryExpression);
        var columns=new List<string>();var select=query as QuerySpecification;
        if(select!=null) foreach(var element in select.SelectElements) {
            var scalar=element as SelectScalarExpression;if(scalar==null) continue;
            if(scalar.ColumnName!=null) {columns.Add(scalar.ColumnName.Value);continue;}
            var reference=scalar.Expression as ColumnReferenceExpression;
            if(reference!=null && reference.MultiPartIdentifier!=null) {var ids=reference.MultiPartIdentifier.Identifiers;columns.Add(ids[ids.Count-1].Value);}
        }
        return columns;
    }
    public string[] Projected(QueryExpression query) {return new List<string>(Projection(query)).ToArray();}
    public override void ExplicitVisit(CreateViewStatement node) {Add(node.SchemaObjectName.BaseIdentifier.Value,Projection(node.SelectStatement.QueryExpression));base.ExplicitVisit(node);}
    public override void ExplicitVisit(AlterViewStatement node) {Add(node.SchemaObjectName.BaseIdentifier.Value,Projection(node.SelectStatement.QueryExpression));base.ExplicitVisit(node);}
    public override void ExplicitVisit(CreateOrAlterViewStatement node) {Add(node.SchemaObjectName.BaseIdentifier.Value,Projection(node.SelectStatement.QueryExpression));base.ExplicitVisit(node);}
}
public class YakumiBindingVisitor : TSqlFragmentVisitor {
    public readonly Dictionary<string,HashSet<string>> Aliases = new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    public readonly List<string[]> Columns = new List<string[]>();
    public override void ExplicitVisit(FunctionCall node) {
        string name=node.FunctionName.Value.ToUpperInvariant();
        if(name=="DATEDIFF" || name=="DATEDIFF_BIG" || name=="DATEADD" || name=="DATEPART" || name=="DATENAME") {
            for(int i=1;i<node.Parameters.Count;i++) node.Parameters[i].Accept(this);return;
        }
        base.ExplicitVisit(node);
    }
    public override void ExplicitVisit(NamedTableReference node) {
        string table=node.SchemaObject.BaseIdentifier.Value,key=node.Alias==null?table:node.Alias.Value;
        HashSet<string> set;if(!Aliases.TryGetValue(key,out set)) Aliases[key]=set=new HashSet<string>(StringComparer.OrdinalIgnoreCase);set.Add(table);
        base.ExplicitVisit(node);
    }
    public override void ExplicitVisit(ColumnReferenceExpression node) {
        if(node.MultiPartIdentifier!=null) {var names=new List<string>();foreach(var id in node.MultiPartIdentifier.Identifiers) names.Add(id.Value);Columns.Add(names.ToArray());}
        base.ExplicitVisit(node);
    }
}
'@
$parser = New-Object Microsoft.SqlServer.TransactSql.ScriptDom.TSql160Parser -ArgumentList $true
$schema = New-Object YakumiSchemaVisitor
$schemaFiles = @(Get-ChildItem (Join-Path $PSScriptRoot '..\DATABASES\Yakult-DB-Production\dbo') -Filter *.sql -Recurse)
foreach ($file in $schemaFiles) {
    $errors=$null; $reader=New-Object IO.StringReader -ArgumentList ([IO.File]::ReadAllText($file.FullName))
    try { $tree=$parser.Parse($reader,[ref]$errors); $tree.Accept($schema) } finally { $reader.Dispose() }
}
# HolidayRepository maintains this table locally; its CREATE text is outside the SQL project.
$holiday = Get-Content (Join-Path $PSScriptRoot '..\Yakult.Inventory.App\Repositories\HolidayRepository.cs') -Raw
$create = [regex]::Match($holiday,'(?s)CREATE TABLE dbo.CompanyHoliday\s*\(.*?\);')
if ($create.Success) { $errors=$null; $reader=New-Object IO.StringReader -ArgumentList $create.Value;try {$parser.Parse($reader,[ref]$errors).Accept($schema)} finally {$reader.Dispose()} }
$failures = New-Object 'Collections.Generic.List[string]'
$field = [Yakult.Inventory.App.Services.AssistantCapability].GetField('BaseSql',[Reflection.BindingFlags]'NonPublic,Instance')
foreach ($capability in [Yakult.Inventory.App.Services.AssistantCatalog]::All) {
    $errors=$null; $reader=New-Object IO.StringReader -ArgumentList ($field.GetValue($capability))
    try { $tree=$parser.Parse($reader,[ref]$errors) } finally { $reader.Dispose() }
    if ($errors.Count) { throw "Invalid source SQL: $($capability.Id)" }
    $binding=New-Object YakumiBindingVisitor; $tree.Accept($binding)
    $projection=$schema.Projected($tree.Batches[0].Statements[0].QueryExpression)
    $columnField=$capability.GetType().GetField('Columns',[Reflection.BindingFlags]'NonPublic,Instance')
    foreach($expected in $columnField.GetValue($capability)) {if($expected -notin $projection) {$failures.Add("$($capability.Id): declared result field $expected is not projected")}}
    foreach ($table in @($binding.Aliases.Values | ForEach-Object { $_ } | Select-Object -Unique)) {
        if (-not $schema.Tables.ContainsKey($table)) { $failures.Add("$($capability.Id): unknown source $table") }
    }
    foreach ($column in $binding.Columns) {
        if ($column.Count -eq 2 -and $binding.Aliases.ContainsKey($column[0])) {
            $found=$false
            foreach ($table in $binding.Aliases[$column[0]]) { if($schema.Tables.ContainsKey($table) -and $schema.Tables[$table].Contains($column[1])) {$found=$true} }
            if (-not $found) {$failures.Add("$($capability.Id): missing $($column -join '.') (source: $($binding.Aliases[$column[0]] -join ', '))")}
        }
        elseif ($column.Count -eq 1) {
            $found=$false
            foreach($table in @($binding.Aliases.Values | ForEach-Object {$_})) {if($schema.Tables.ContainsKey($table) -and $schema.Tables[$table].Contains($column[0])) {$found=$true}}
            if(-not $found) {$failures.Add("$($capability.Id): unbound column $($column[0])")}
        }
    }
}
$failures=@($failures | Select-Object -Unique)
if ($failures.Count) { $failures; throw "$($failures.Count) local schema binding issues. No SQL was executed." }
"$([Yakult.Inventory.App.Services.AssistantCatalog]::All.Count) capability sources passed local schema binding lint. No SQL was executed. This does not verify a deployed database."
