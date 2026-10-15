param(
    [string]$ConfigDirectory = (Join-Path $PSScriptRoot '..\Yakult.Inventory.App'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\_build_verify\ai-expanded'),
    [string]$ConnectionStringEnvironmentVariable
)
# Read-only investigation: no application startup, stored-procedure execution or data modification.
$ErrorActionPreference='Stop'
$connectionText=$null
if($ConnectionStringEnvironmentVariable) {$connectionText=[Environment]::GetEnvironmentVariable($ConnectionStringEnvironmentVariable)}
if(-not $connectionText) {
    $profileRoot=Join-Path $env:LOCALAPPDATA 'Yakult'
    $saved=@(Get-ChildItem -LiteralPath $profileRoot -Filter user.config -File -Recurse -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)
    foreach($profile in $saved) {
        try {
            [xml]$xml=Get-Content -LiteralPath $profile.FullName -Raw
            $entry=$xml.SelectSingleNode('//setting[@name="DbConnectionString"]/value')
            if($entry -and -not [string]::IsNullOrWhiteSpace($entry.InnerText)) {$connectionText=$entry.InnerText;break}
        } catch { }
    }
}
if(-not $connectionText) {
    $environment=[Environment]::GetEnvironmentVariable('DOTNET_ENVIRONMENT')
    if(-not $environment) {$environment=[Environment]::GetEnvironmentVariable('ASPNETCORE_ENVIRONMENT')}
    foreach($file in @($(if($environment){'appsettings.'+$environment+'.json'}),'appsettings.Development.json','appsettings.json')) {
        if(-not $file) {continue}
        $path=Join-Path $ConfigDirectory $file
        if(Test-Path -LiteralPath $path) {
            $config=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            if($config.ConnectionStrings.DefaultConnection) {$connectionText=$config.ConnectionStrings.DefaultConnection;break}
        }
    }
}
if(-not $connectionText) {throw 'No configured connection was found. Credentials are never printed.'}
$connectionText=$connectionText.Replace('192.168.100.186,50300','192.168.100.186,50301')
$builder=[Data.SqlClient.SqlConnectionStringBuilder]::new($connectionText)
$builder['ApplicationIntent']='ReadOnly'
$builder['Connect Timeout']=5
$builder['Persist Security Info']=$false
$connection=[Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
function Read-Select([string]$sql) {
    if($sql -notmatch '^\s*SELECT\b' -or $sql -match '(?i)\b(INTO|INSERT|UPDATE|DELETE|ALTER|CREATE|DROP|MERGE|EXEC(?:UTE)?)\b') {throw 'Only reviewed SELECT statements are permitted.'}
    $command=$connection.CreateCommand();$command.CommandText=$sql;$command.CommandTimeout=20
    try {
        $reader=$command.ExecuteReader()
        try {
            $rows=[Collections.Generic.List[object]]::new()
            while($reader.Read()) {
                $row=[ordered]@{}
                for($i=0;$i -lt $reader.FieldCount;$i++) {$row[$reader.GetName($i)]=$(if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)})}
                $rows.Add([pscustomobject]$row)
            }
            return ,$rows.ToArray()
        } finally {$reader.Dispose()}
    } finally {$command.Dispose()}
}
try {
    $connection.Open()
    $objects=Read-Select @'
SELECT s.name SchemaName,o.name ObjectName,o.type_desc ObjectType,
 (SELECT SUM(p.rows) FROM sys.partitions p WHERE p.object_id=o.object_id AND p.index_id IN (0,1)) ApproximateRows
FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id
WHERE o.type IN ('U','V') AND o.is_ms_shipped=0 ORDER BY s.name,o.name;
'@
    $columns=Read-Select @'
SELECT s.name SchemaName,o.name ObjectName,c.column_id Ordinal,c.name ColumnName,t.name DataType,c.max_length MaxLength,c.is_nullable Nullable,c.is_computed Computed
FROM sys.columns c JOIN sys.objects o ON o.object_id=c.object_id JOIN sys.schemas s ON s.schema_id=o.schema_id
JOIN sys.types t ON t.user_type_id=c.user_type_id WHERE o.type IN ('U','V') AND o.is_ms_shipped=0 ORDER BY s.name,o.name,c.column_id;
'@
    $relations=Read-Select @'
SELECT OBJECT_SCHEMA_NAME(f.parent_object_id) SchemaName,OBJECT_NAME(f.parent_object_id) ParentTable,
 COL_NAME(f.parent_object_id,f.parent_column_id) ParentColumn,OBJECT_NAME(f.referenced_object_id) ReferencedTable,
 COL_NAME(f.referenced_object_id,f.referenced_column_id) ReferencedColumn FROM sys.foreign_key_columns f;
'@
    $categories=Read-Select @'
SELECT i.Category,COUNT_BIG(*) ItemRecords,SUM(CAST(ISNULL(i.StockOnHand,0) AS bigint)) StockUnits,
 SUM(CASE WHEN i.Active=1 THEN 1 ELSE 0 END) ActiveRecords FROM dbo.Item i GROUP BY i.Category ORDER BY i.Category;
'@
    $phones=Read-Select @'
SELECT TOP (30) i.ItemId,i.Name,i.ModelNumber,i.Category,i.StockOnHand,i.Active,
 CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ArchiveStatus a WHERE a.EntityType='Item' AND a.EntityId=i.ItemId AND a.IsArchived=1) THEN 1 ELSE 0 END AS bit) Archived
FROM dbo.Item i WHERE i.Category LIKE '%phone%' OR i.Name LIKE '%redmi%' OR i.ModelNumber LIKE '%redmi%'
ORDER BY i.ItemId;
'@
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($OutputDirectory))
    $report=[ordered]@{Database=$connection.Database;RetrievedUtc=[DateTime]::UtcNow.ToString('o');Objects=$objects;Columns=$columns;Relations=$relations;Categories=$categories;PhoneExamples=$phones}
    [IO.File]::WriteAllText((Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'database-readonly-inspection.json'),($report | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
    'Read-only inspection database: '+$connection.Database
    'Objects: '+$objects.Count+'; columns: '+$columns.Count+'; foreign-key columns: '+$relations.Count
    $categories | Format-Table -AutoSize | Out-String -Width 180
    $phones | Format-Table -AutoSize | Out-String -Width 190
}
catch {throw ('Read-only database inspection failed: '+$_.Exception.GetType().Name+'. No credentials are printed.')}
finally {$connection.Dispose();$connectionText=$null;$builder.Clear()}
