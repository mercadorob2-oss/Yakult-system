#Requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Directory, [Parameter(Mandatory=$true)][string]$UpdateFolder,
      [Parameter(Mandatory=$true)][string]$ResultPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReleaseDesk.Services.ps1')
$result = [ordered]@{ Version=(Split-Path $Directory -Leaf); Verified=$false; Details=''; Notes=''; PublishedVersion='Unknown'; FeedStatus='Not checked'; CheckedAt=(Get-Date).ToString('o') }
try {
    Write-Output 'Verifying signed metadata and installer bytes...'
    $metadata = Test-DeskPackage $Directory (Join-Path $PSScriptRoot 'UpdatePublicKey.xml')
    $result.Verified = $true; $result.Notes = [string]$metadata.releaseNotes
    $result.Details = 'Signature, product, version, installer size and SHA256 verified.'
    Write-Output $result.Details
} catch { $result.Details = $_.Exception.Message; Write-Output ('Verification failed: ' + $result.Details) }
try {
    Write-Output 'Reading published feed (read-only; no permission probe or upload)...'
    $published = Read-DeskMetadata (Join-Path $UpdateFolder 'latest.json') (Join-Path $PSScriptRoot 'UpdatePublicKey.xml')
    $result.PublishedVersion = [string]$published.version
    $result.FeedStatus = 'Signed feed available'
    if ($result.Verified) {
        if ([version]$published.version -gt [version]$metadata.version) { $result.FeedStatus = 'Feed is newer; publication would be rejected' }
        elseif ($published.version -eq $metadata.version) {
            $result.FeedStatus = if ($published.sha256 -eq $metadata.sha256 -and $published.sizeBytes -eq $metadata.sizeBytes) { 'Same release announced (remote installer not checked)' } else { 'Version conflict: different bytes already announced' }
        } else { $result.FeedStatus = 'Local package is newer than the published feed' }
    }
} catch { $result.FeedStatus = 'Feed unavailable or invalid: ' + $_.Exception.Message }
Write-Output $result.FeedStatus
Write-DeskJobRecord $ResultPath $result
if (-not $result.Verified) { exit 1 }
