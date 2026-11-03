$ErrorActionPreference='Stop'
$cfg=Get-Content -LiteralPath '\\192.168.100.186\c$\inetpub\wwwroot\Yakult.SystemsPortal\appsettings.Local.json' -Raw | ConvertFrom-Json
$connectionString=$cfg.ConnectionStrings.DefaultConnection -replace '(?i)Trust Server Certificate\s*=', 'TrustServerCertificate='
$connection=[System.Data.SqlClient.SqlConnection]::new($connectionString)
try {
 $connection.Open()
 $command=$connection.CreateCommand()
 $command.CommandText='SELECT PolicyId,Title,Slug,Body,IsPublished,UpdatedBy,UpdatedAt FROM dbo.CompanyPolicy WHERE Slug=@slug'
 $null=$command.Parameters.Add('@slug',[System.Data.SqlDbType]::VarChar,200)
 $command.Parameters['@slug'].Value='it-policy---employee-guidelines'
 $reader=$command.ExecuteReader()
 $rows=@()
 while($reader.Read()){
  $rows+=[pscustomobject]@{PolicyId=$reader['PolicyId'];Title=$reader['Title'];Slug=$reader['Slug'];Body=$reader['Body'];IsPublished=$reader['IsPublished'];UpdatedBy=if($reader.IsDBNull(5)){$null}else{$reader.GetInt32(5)};UpdatedAt=$reader['UpdatedAt']}
 }
 $reader.Close()
 if($rows.Count -ne 1){throw 'Expected exactly one live IT policy'}
 $backup=Join-Path (Resolve-Path artifacts/it-policy-upgrade).Path ('guest-email-before-'+(Get-Date -Format yyyyMMdd-HHmmss)+'.json')
 $rows[0] | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $backup -Encoding utf8
 $backup | Set-Content artifacts/it-policy-upgrade/guest-email-backup-path.txt
 $document=$rows[0].Body.Substring('yakult-policy:v1:'.Length) | ConvertFrom-Json
 $guest=$document.sections | Where-Object id -eq 'guest-internet'
 [pscustomobject]@{PolicyId=$rows[0].PolicyId;Published=$rows[0].IsPublished;Step4=$guest.blocks[0].items[3];Screenshot4=$guest.blocks[4].paths[0];Backup=$backup} | ConvertTo-Json
} finally {$connection.Dispose()}
