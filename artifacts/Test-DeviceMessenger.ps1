$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Data
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'messenger-preview\Yakult.Inventory.App.exe'))
$database = 'YakultMessengerTest_' + [Guid]::NewGuid().ToString('N')
$master = New-Object Data.SqlClient.SqlConnection 'Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Initial Catalog=master;Connect Timeout=30'
$master.Open()
function Execute-Sql($connection, $sql) { $command = $connection.CreateCommand(); $command.CommandText=$sql; try { $command.ExecuteNonQuery() | Out-Null } finally { $command.Dispose() } }
function Scalar-Sql($connection, $sql) { $command = $connection.CreateCommand(); $command.CommandText=$sql; try { return $command.ExecuteScalar() } finally { $command.Dispose() } }
function Set-Identity($name,$id) { [Yakult.Inventory.App.Session.AppSession]::CurrentUserName=$name; [Yakult.Inventory.App.Session.AppSession]::CurrentUserId=$id }
$connection = $null
try {
    Execute-Sql $master "CREATE DATABASE [$database]"
    $testCs = "Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Initial Catalog=$database;Connect Timeout=30"
    $connection = New-Object Data.SqlClient.SqlConnection $testCs
    $connection.Open()
    $migration = Get-Content (Join-Path $PSScriptRoot '..\DATABASES\Yakult-DB-Production\dbo\Scripts\Migration_DesktopDeviceMessage_CreateTable.sql') -Raw
    Execute-Sql $connection $migration
    Execute-Sql $connection $migration
    Execute-Sql $connection 'CREATE TABLE dbo.CallClientPresence(MachineName nvarchar(64),UserName nvarchar(128),LastSeenUtc datetime2)'
    $command = $connection.CreateCommand()
    $command.CommandText = "INSERT dbo.CallClientPresence VALUES(@Machine,'Alice',SYSUTCDATETIME()),(@Machine,'Bob',SYSUTCDATETIME())"
    $command.Parameters.AddWithValue('@Machine',[Environment]::MachineName) | Out-Null
    $command.ExecuteNonQuery() | Out-Null; $command.Dispose()
    [Yakult.Inventory.App.Core.DatabaseConfig]::Bootstrap($testCs)
    $repo = New-Object Yakult.Inventory.App.Repositories.DeviceMessengerRepository
    $alice = New-Object Yakult.Inventory.App.Repositories.MessengerDevice
    $alice.MachineName = [Environment]::MachineName; $alice.UserName='Alice'; $alice.Online=$true
    $bob = New-Object Yakult.Inventory.App.Repositories.MessengerDevice
    $bob.MachineName = [Environment]::MachineName; $bob.UserName='Bob'; $bob.Online=$true
    Set-Identity 'Alice' 101
    $repo.SendAsync($bob,'Hello Bob. This message is temporary.').GetAwaiter().GetResult()
    $sent = $repo.GetConversationAsync($bob).GetAwaiter().GetResult()
    if ($sent.Count -ne 1 -or !$sent[0].IsOutgoing) { throw 'Sender conversation failed.' }
    $messageId = $sent[0].MessageId
    Set-Identity 'Bob' 102
    $received = $repo.GetConversationAsync($alice).GetAwaiter().GetResult()
    if ($received.Count -ne 1 -or $received[0].IsOutgoing) { throw 'Recipient routing failed.' }
    $devices = $repo.GetDevicesAsync().GetAwaiter().GetResult()
    if ($devices.Count -ne 1 -or $devices[0].PendingCount -ne 1) { throw 'Presence or pending count failed.' }
    Set-Identity 'Charlie' 103
    $repo.DismissAsync($messageId).GetAwaiter().GetResult()
    if ((Scalar-Sql $connection 'SELECT COUNT(*) FROM dbo.DesktopDeviceMessage WHERE RecipientDismissedAtUtc IS NULL AND SenderDismissedAtUtc IS NULL') -ne 1) { throw 'Non-participant dismissed a message.' }
    Set-Identity 'Bob' 102
    $repo.DismissAsync($messageId).GetAwaiter().GetResult()
    if ($repo.GetConversationAsync($alice).GetAwaiter().GetResult().Count -ne 0) { throw 'Recipient dismissal failed.' }
    Set-Identity 'Alice' 101
    if ($repo.GetConversationAsync($bob).GetAwaiter().GetResult().Count -ne 1) { throw 'Recipient dismissal removed sender copy.' }
    $repo.DismissAsync($messageId).GetAwaiter().GetResult()
    if ((Scalar-Sql $connection 'SELECT COUNT(*) FROM dbo.DesktopDeviceMessage') -ne 0) { throw 'Both-sided dismissal did not delete message.' }
    Execute-Sql $connection "UPDATE dbo.CallClientPresence SET LastSeenUtc=DATEADD(minute,-20,SYSUTCDATETIME()) WHERE UserName='Bob'"
    $rejected = $false
    try { $repo.SendAsync($bob,'Must not reach offline Bob').GetAwaiter().GetResult() } catch { $rejected=$true }
    if (!$rejected -or (Scalar-Sql $connection 'SELECT COUNT(*) FROM dbo.DesktopDeviceMessage') -ne 0) { throw 'Offline send was not rejected.' }
    Set-Identity '' 0
    $rejected = $false
    try { $repo.SendAsync($bob,'Must not reach Bob').GetAwaiter().GetResult() } catch { $rejected=$true }
    if (!$rejected) { throw 'Unauthenticated send was accepted.' }
    Write-Output 'PASS: migration rerun, sender/recipient routing, presence and pending counts, non-participant isolation, independent dismissal, deletion after both dismiss, offline rejection, sign-in requirement.'

    Set-Identity 'Alice' 101
    $window = New-Object Yakult.Inventory.App.Wpf.Portal.WpfDeviceMessengerWindow
    $type = $window.GetType(); $flags = [Reflection.BindingFlags]'NonPublic,Instance,DeclaredOnly'
    $sampleDevices = New-Object 'System.Collections.Generic.List[Yakult.Inventory.App.Repositories.MessengerDevice]'
    $bob.UserName='IT Helpdesk'; $bob.MachineName='IT-DESKTOP-02'; $bob.PendingCount=2
    $sampleDevices.Add($bob)
    $offline = New-Object Yakult.Inventory.App.Repositories.MessengerDevice
    $offline.UserName='Finance Office'; $offline.MachineName='FINANCE-PC-04'; $offline.Online=$false
    $sampleDevices.Add($offline)
    $type.GetField('_devices',$flags).SetValue($window,$sampleDevices)
    $type.GetField('_selected',$flags).SetValue($window,$bob)
    $type.GetMethod('RenderDevices',$flags).Invoke($window,@()) | Out-Null
    $type.GetMethod('UpdateHeader',$flags).Invoke($window,@()) | Out-Null
    $rows = New-Object 'System.Collections.Generic.List[Yakult.Inventory.App.Repositories.TemporaryDeviceMessage]'
    foreach ($index in 1..3) {
        $row=New-Object Yakult.Inventory.App.Repositories.TemporaryDeviceMessage
        $row.MessageId=$index; $row.CreatedAtUtc=[DateTime]::UtcNow.AddMinutes(-4+$index); $row.IsOutgoing=($index -eq 2)
        $row.Body=@('Hello, good day! There is a system update available. Please save your work before restarting.','Thank you! I will restart after finishing this invoice.','Sounds good. Double-click this message when you are done to dismiss it from your view.')[$index-1]
        $rows.Add($row)
    }
    $renderArguments=New-Object object[] 2; $renderArguments[0]=$rows.psobject.BaseObject; $renderArguments[1]=$bob.psobject.BaseObject
    $type.GetMethod('RenderMessages',$flags).Invoke($window,$renderArguments) | Out-Null
    $root=$window.Content
    foreach ($width in @(1020,760)) {
        $root.Measure([Windows.Size]::new($width,720)); $root.Arrange([Windows.Rect]::new(0,0,$width,720)); $root.UpdateLayout()
        $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new($width,720,96,96,[Windows.Media.PixelFormats]::Pbgra32)
        $bitmap.Render($root); $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
        $stream=[IO.File]::Create((Join-Path $PSScriptRoot "device-messenger-$width.png")); try {$encoder.Save($stream)} finally {$stream.Dispose()}
    }
    $window.Close()
    Write-Output 'Rendered messenger at normal and minimum widths with sample conversations.'
} finally {
    if ($connection) { $connection.Dispose() }
    [Data.SqlClient.SqlConnection]::ClearAllPools()
    if ($database -match '^YakultMessengerTest_[a-f0-9]{32}$') { Execute-Sql $master "ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]" }
    $master.Dispose()
}
