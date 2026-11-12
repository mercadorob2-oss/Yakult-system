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

    Add-Type -TypeDefinition @"
using System;
public static class MessengerPopupProbe {
 public static int Popups, Closed;
 public static bool Reading;
 public static Action LastActivate;
 public static bool IsReading() { return Reading; }
 public static Action Capture(string title,string body,Action activate) { Popups++; LastActivate=activate; return () => Closed++; }
}
"@
    [Threading.SynchronizationContext]::SetSynchronizationContext([Windows.Threading.DispatcherSynchronizationContext]::new([Windows.Threading.Dispatcher]::CurrentDispatcher))
    function Await-Ui($task) {
        if (!$task.IsCompleted) {
            $frame=[Windows.Threading.DispatcherFrame]::new()
            $waitTimer=[Windows.Threading.DispatcherTimer]::new()
            $waitTimer.Interval=[TimeSpan]::FromMilliseconds(10)
            $waitTimer.Add_Tick({ if ($task.IsCompleted) { $frame.Continue=$false } })
            $waitTimer.Start()
            try { [Windows.Threading.Dispatcher]::PushFrame($frame) } finally { $waitTimer.Stop() }
        }
        $task.GetAwaiter().GetResult() | Out-Null
    }
    function Queue-Sample($body) {
        $command=$connection.CreateCommand()
        $command.CommandText="INSERT dbo.DesktopDeviceMessage(SenderMachine,SenderUser,SenderUserId,RecipientMachine,RecipientUser,Body) VALUES('IT-TEST-PC','Alice',101,@Machine,'Bob',@Body)"
        $command.Parameters.AddWithValue('@Machine',[Environment]::MachineName) | Out-Null
        $command.Parameters.AddWithValue('@Body',$body) | Out-Null
        $command.ExecuteNonQuery() | Out-Null; $command.Dispose()
    }
    Set-Identity 'Bob' 102
    $service=[Yakult.Inventory.App.Services.DeviceMessengerNotifications]::Instance
    $flags=[Reflection.BindingFlags]'NonPublic,Instance'
    $serviceType=$service.GetType()
    $serviceType.GetField('_userId',$flags).SetValue($service,102)
    $probeType=[MessengerPopupProbe]
    $serviceType.GetField('IsMessengerReading',$flags).SetValue($service,[Delegate]::CreateDelegate([Func[bool]],$probeType.GetMethod('IsReading')))
    $serviceType.GetField('ShowPopup',$flags).SetValue($service,[Delegate]::CreateDelegate([Func[string,string,Action,Action]],$probeType.GetMethod('Capture')))
    Queue-Sample 'Hello, good day! An update is ready.'
    Await-Ui ($service.RefreshAsync())
    if ($service.PendingCount -ne 1 -or [MessengerPopupProbe]::Popups -ne 1) { throw 'First incoming message did not update badge and popup.' }
    Await-Ui ($service.RefreshAsync())
    if ([MessengerPopupProbe]::Popups -ne 1) { throw 'Repeated polling replayed the same popup.' }
    Queue-Sample 'A second message'
    Await-Ui ($service.RefreshAsync())
    if ($service.PendingCount -ne 2 -or [MessengerPopupProbe]::Popups -ne 2) { throw 'New message did not trigger a new popup and count.' }
    [MessengerPopupProbe]::Reading=$true
    Queue-Sample 'Message received while reading chat'
    Await-Ui ($service.RefreshAsync())
    if ($service.PendingCount -ne 3 -or [MessengerPopupProbe]::Popups -ne 2) { throw 'Active-chat suppression failed.' }
    [MessengerPopupProbe]::Reading=$false
    Await-Ui ($service.RefreshAsync())
    if ([MessengerPopupProbe]::Popups -ne 2) { throw 'Closing chat replayed messages already observed.' }
    [Yakult.Inventory.App.Session.AppSession]::NotificationsEnabled=$false
    Queue-Sample 'Message with popups disabled'
    Await-Ui ($service.RefreshAsync())
    if ($service.PendingCount -ne 4 -or [MessengerPopupProbe]::Popups -ne 2) { throw 'Notification preference incorrectly affected count or popup.' }
    [Yakult.Inventory.App.Session.AppSession]::NotificationsEnabled=$true
    Await-Ui ($service.RefreshAsync())
    if ([MessengerPopupProbe]::Popups -ne 2) { throw 'Re-enabling notifications replayed old messages.' }
    $pending=$repo.GetPendingIncomingAsync()
    Await-Ui $pending
    $id=$pending.Result[0].MessageId
    Await-Ui ($repo.DismissAsync($id))
    Await-Ui ($service.RefreshAsync())
    if ($service.PendingCount -ne 3 -or [MessengerPopupProbe]::Popups -ne 2) { throw 'Dismissal did not decrease count without a duplicate popup.' }
    Queue-Sample 'Fresh message after a dismissal'
    Await-Ui ($service.RefreshAsync())
    if ($service.PendingCount -ne 4 -or [MessengerPopupProbe]::Popups -ne 3) { throw 'New message after dismissal was missed.' }
    Set-Identity 'Charlie' 103
    $privateQuery=$repo.GetPendingIncomingAsync(); Await-Ui $privateQuery
    if ($privateQuery.Result.Count -ne 0) { throw 'Pending messages leaked to another user.' }
    Set-Identity 'Bob' 102
    $service.Stop()
    if ($service.PendingCount -ne 0 -or [MessengerPopupProbe]::Closed -lt 1) { throw 'Logout cleanup failed.' }
    [MessengerPopupProbe]::LastActivate.Invoke()
    $messengerType=$assembly.GetType('Yakult.Inventory.App.Wpf.Portal.WpfDeviceMessengerWindow')
    if ($messengerType.GetField('_open',[Reflection.BindingFlags]'NonPublic,Static').GetValue($null) -ne $null) { throw 'Expired popup reopened a conversation after logout.' }
    Write-Output 'PASS: new-message popup, duplicate suppression, active-chat suppression, notification preferences, recipient privacy, dismissal counts, logout reset, stale popup callback guard.'

    [Yakult.Inventory.App.Session.AppSession]::IsDeveloper=$true
    [System.Windows.Forms.Application].GetField('executablePath',[Reflection.BindingFlags]'NonPublic,Static').SetValue($null,(Join-Path $PSScriptRoot 'messenger-preview\Yakult.Inventory.App.exe'))
    $dashboardPreview=New-Object Yakult.Inventory.App.Wpf.Portal.WpfMainDashboardShell
    $dashboardPreviewType=$dashboardPreview.GetType()
    $update=$dashboardPreviewType.GetMethod('UpdateMessengerBadge',$flags)
    $badge=$dashboardPreviewType.GetField('_messengerBadge',$flags).GetValue($dashboardPreview)
    $badgeText=$dashboardPreviewType.GetField('_messengerBadgeText',$flags).GetValue($dashboardPreview)
    foreach ($count in @(0,7,120,0)) {
        $update.Invoke($dashboardPreview,@($count)) | Out-Null
        if ($count -eq 0 -and $badge.Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Zero count badge was not hidden.' }
        if ($count -gt 0 -and $badge.Visibility -ne [Windows.Visibility]::Visible) { throw 'Positive count badge was not visible.' }
        if ($count -eq 120 -and $badgeText.Text -ne '99+') { throw 'Large badge count did not cap at 99+.' }
    }
    $update.Invoke($dashboardPreview,@(7)) | Out-Null
    $badgeHost=$dashboardPreviewType.GetField('_messengerHeadsetButton',$flags).GetValue($dashboardPreview).Parent
    $badgeHost.Measure([Windows.Size]::new(80,80)); $badgeHost.Arrange([Windows.Rect]::new(0,0,80,80)); $badgeHost.UpdateLayout()
    $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(80,80,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($badgeHost)
    $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream=[IO.File]::Create((Join-Path $PSScriptRoot 'messenger-headset-badge.png')); try {$encoder.Save($stream)} finally {$stream.Dispose()}
    $dashboardPreview.Close()
    [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'messenger-preview\ToastNotifications.dll')) | Out-Null
    $options=New-Object ToastNotifications.Core.MessageOptions
    $toast=[Yakult.Inventory.App.Wpf.Toast.YakultToastNotification]::new('Message from IT Helpdesk','IT-DESKTOP-02 • Hello, good day! An update is ready. Click to open conversation.',[Yakult.Inventory.App.Wpf.Toast.YakultToastKind]::Info,$options,[Action]{})
    $part=$toast.DisplayPart
    $part.Visibility=[Windows.Visibility]::Visible; $part.Opacity=1; $part.RenderTransform=[Windows.Media.Transform]::Identity
    Write-Output ('Popup visibility: ' + $part.Visibility + ', opacity: ' + $part.Opacity)
    $part.Measure([Windows.Size]::new(360,800)); $height=[int][Math]::Ceiling($part.DesiredSize.Height)
    $part.Arrange([Windows.Rect]::new(0,0,360,$height)); $part.UpdateLayout()
    $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(360,$height,96,96,[Windows.Media.PixelFormats]::Pbgra32); $bitmap.Render($part)
    $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream=[IO.File]::Create((Join-Path $PSScriptRoot 'messenger-popup-preview.png')); try {$encoder.Save($stream)} finally {$stream.Dispose()}
    Write-Output 'PASS: badge visibility, 99+ cap, and zero reset. Rendered badge and popup using sample content.'
} finally {
    if ($connection) { $connection.Dispose() }
    [Data.SqlClient.SqlConnection]::ClearAllPools()
    if ($database -match '^YakultMessengerTest_[a-f0-9]{32}$') { Execute-Sql $master "ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]" }
    $master.Dispose()
}





