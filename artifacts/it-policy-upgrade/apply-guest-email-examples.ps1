$ErrorActionPreference='Stop'
$backupPath=(Get-Content artifacts/it-policy-upgrade/guest-email-backup-path.txt -Raw).Trim()
$before=Get-Content -LiteralPath $backupPath -Raw | ConvertFrom-Json
$after=[IO.File]::ReadAllText((Resolve-Path artifacts/it-policy-upgrade/guest-email-body-after.txt).Path)
$cfg=Get-Content -LiteralPath '\\192.168.100.186\c$\inetpub\wwwroot\Yakult.SystemsPortal\appsettings.Local.json' -Raw | ConvertFrom-Json
$connectionString=$cfg.ConnectionStrings.DefaultConnection -replace '(?i)Trust Server Certificate\s*=', 'TrustServerCertificate='
$connection=[System.Data.SqlClient.SqlConnection]::new($connectionString)
try {
 $connection.Open()
 $transaction=$connection.BeginTransaction()
 try {
  $cmd=$connection.CreateCommand();$cmd.Transaction=$transaction
  $cmd.CommandText='UPDATE dbo.CompanyPolicy SET Body=@after, UpdatedAt=SYSUTCDATETIME() WHERE PolicyId=@id AND Slug=@slug AND IsPublished=1 AND Body COLLATE Latin1_General_100_BIN2=@before COLLATE Latin1_General_100_BIN2;'
  $null=$cmd.Parameters.Add('@after',[System.Data.SqlDbType]::NVarChar,-1);$cmd.Parameters['@after'].Value=$after
  $null=$cmd.Parameters.Add('@before',[System.Data.SqlDbType]::NVarChar,-1);$cmd.Parameters['@before'].Value=$before.Body
  $null=$cmd.Parameters.Add('@id',[System.Data.SqlDbType]::Int);$cmd.Parameters['@id'].Value=$before.PolicyId
  $null=$cmd.Parameters.Add('@slug',[System.Data.SqlDbType]::VarChar,200);$cmd.Parameters['@slug'].Value=$before.Slug
  if($cmd.ExecuteNonQuery() -ne 1){throw 'Policy changed since backup; update was not applied.'}
  $verify=$connection.CreateCommand();$verify.Transaction=$transaction
  $verify.CommandText='SELECT Body FROM dbo.CompanyPolicy WHERE PolicyId=@id'
  $null=$verify.Parameters.Add('@id',[System.Data.SqlDbType]::Int);$verify.Parameters['@id'].Value=$before.PolicyId
  if($verify.ExecuteScalar() -cne $after){throw 'Stored policy does not match reviewed content'}
  $transaction.Commit()
  Write-Output 'Published policy updated and read-back verified. One policy row changed; publication status and URL preserved.'
 }catch{$transaction.Rollback();throw}
}finally{$connection.Dispose()}
