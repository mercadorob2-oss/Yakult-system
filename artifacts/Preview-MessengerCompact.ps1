$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'messenger-settings-preview\Yakult.Inventory.App.exe')) | Out-Null
[Yakult.Inventory.App.Session.AppSession]::CurrentUserName='Russel Mercado'
[Yakult.Inventory.App.Session.AppSession]::CurrentUserId=101
$bob=New-Object Yakult.Inventory.App.Repositories.MessengerDevice
$bob.Online=$true
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
        $stream=[IO.File]::Create((Join-Path $PSScriptRoot "messenger-settings-closed-$width.png")); try {$encoder.Save($stream)} finally {$stream.Dispose()}
    }
    $draft=$type.GetField('_draft',$flags).GetValue($window)
    $send=$type.GetField('_send',$flags).GetValue($window)
    $hint=$type.GetField('_draftHint',$flags).GetValue($window)
    $draft.Text='Hello from the design check'
    if (!$send.IsEnabled -or $hint.Visibility -ne [Windows.Visibility]::Collapsed) { throw 'Composer did not enable sending or hide the placeholder.' }
    $draft.Text='   '
    if ($send.IsEnabled) { throw 'Whitespace-only message enabled Send.' }
    $draft.Clear()
    if ($hint.Visibility -ne [Windows.Visibility]::Visible) { throw 'Empty composer placeholder was not restored.' }
    $active=$type.GetField('_activeOnly',$flags).GetValue($window)
    $active.IsChecked=$true
    $list=$type.GetField('_deviceList',$flags).GetValue($window)
    if ($list.Items.Count -ne 1) { throw 'Active filter did not hide the offline device.' }
    $active.IsChecked=$false
    $search=$type.GetField('_search',$flags).GetValue($window)
    $search.Text='FINANCE'
    if ($list.Items.Count -ne 1 -or $list.Items[0].Tag.UserName -ne 'Finance Office') { throw 'Device search failed.' }
    $search.Clear()
    if ($list.Items.Count -ne 2) { throw 'Clearing search did not restore both devices.' }
    $panel=$root.Children[$root.Children.Count-1]
    $panel.Visibility=[Windows.Visibility]::Visible
    foreach ($width in @(1020,760)) {
        $root.Measure([Windows.Size]::new($width,720)); $root.Arrange([Windows.Rect]::new(0,0,$width,720)); $root.UpdateLayout()
        $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new($width,720,96,96,[Windows.Media.PixelFormats]::Pbgra32)
        $bitmap.Render($root); $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
        $stream=[IO.File]::Create((Join-Path $PSScriptRoot "messenger-settings-open-$width.png")); try {$encoder.Save($stream)} finally {$stream.Dispose()}
    }
    $compact=$type.GetField('_compactWindow',$flags).GetValue($window)
    $previousCompact=$compact.IsChecked
    try {
        $compact.IsChecked=$true
        if ($window.Width -ne 720 -or $window.Height -ne 560) { throw 'Compact dimensions were not applied.' }
        if (!$window.GetType().Assembly.GetType("Yakult.Inventory.App.Properties.Settings").GetProperty("Default").GetValue($null,$null).DeviceMessenger_CompactWindow) { throw 'Compact preference was not saved.' }
        foreach ($state in @('closed','open')) {
            $panel.Visibility=if ($state -eq 'open') {[Windows.Visibility]::Visible} else {[Windows.Visibility]::Collapsed}
            $root.Measure([Windows.Size]::new(704,521)); $root.Arrange([Windows.Rect]::new(0,0,704,521)); $root.UpdateLayout()
            $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(704,521,96,96,[Windows.Media.PixelFormats]::Pbgra32)
            $bitmap.Render($root); $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new(); $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
            $stream=[IO.File]::Create((Join-Path $PSScriptRoot "messenger-compact-$state.png")); try {$encoder.Save($stream)} finally {$stream.Dispose()}
        }
        $compact.IsChecked=$false
        if ($window.Width -ne 1020 -or $window.Height -ne 720) { throw 'Normal dimensions were not restored.' }
        Write-Output 'PASS: compact size applies immediately, preference saves, and normal size restores.'
    } finally { $compact.IsChecked=$previousCompact }
    $window.Close()
    Write-Output 'PASS: composer enablement, whitespace rejection, placeholder states, active-only filter, search and clear.' 
Write-Output 'Rendered redesigned messenger at 1020px and 760px without connecting to the database.'



