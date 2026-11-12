$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
$previewDir = Join-Path $PSScriptRoot 'notification-ui-preview'
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $previewDir 'Yakult.Inventory.App.exe'))
$type = $assembly.GetType('Yakult.Inventory.App.Wpf.CallMonitoring.WpfNotificationDrawer', $true)
$drawer = [Activator]::CreateInstance($type, $true)
$flags = [Reflection.BindingFlags]'NonPublic,Instance,DeclaredOnly'
$tickets = New-Object 'System.Collections.Generic.List[Yakult.Inventory.App.Models.CallMonitoring.CallTicketListItem]'
foreach ($index in 1..3) {
    $ticket = New-Object Yakult.Inventory.App.Models.CallMonitoring.CallTicketListItem
    $ticket.TicketId = $index
    $ticket.TicketCode = 'TCK-00012' + $index
    $ticket.Issue = @('Printer disconnects during invoice printing', 'Desktop will not start after power interruption', 'Outlook cannot send attachments to external recipients')[$index-1]
    $ticket.Status = 'Pending'
    $ticket.Priority = @('High','Critical','Medium')[$index-1]
    $ticket.CreatedAt = [DateTime]::UtcNow.AddDays(-5)
    $ticket.UpdatedAt = [DateTime]::UtcNow.AddDays(-4)
    $ticket.Department = 'Finance and Accounting'
    $ticket.Branch = 'Head Office'
    $tickets.Add($ticket)
}
$arguments = New-Object object[] 2; $arguments[0] = $tickets.psobject.BaseObject; $arguments[1] = 3
$feed = $type.GetMethod('BuildFeed', [Reflection.BindingFlags]'NonPublic,Static').Invoke($null, $arguments)
$type.GetField('_items', $flags).SetValue($drawer, $feed)
$type.GetMethod('Render', $flags).Invoke($drawer, @()) | Out-Null
foreach ($width in @(560,380)) {
    $drawer.Width = $width
    $drawer.Height = 850
    $drawer.Measure([Windows.Size]::new($width,850))
    $drawer.Arrange([Windows.Rect]::new(0,0,$width,850))
    $drawer.UpdateLayout()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($width,850,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($drawer)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create((Join-Path $PSScriptRoot "notification-panel-$width.png"))
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
Write-Output 'Rendered notification panel with sample tickets at 560px and 380px.'


$search = $type.GetField('_txtSearch', $flags).GetValue($drawer)
$unread = $type.GetField('_unreadOnly', $flags).GetValue($drawer)
$search.Text = 'Outlook'
if ($type.GetField('_currentView', $flags).GetValue($drawer).Count -ne 1) { throw 'Search did not narrow the feed.' }
$readIds = $type.GetField('_readIds', $flags).GetValue($drawer)
$readIds.Add(3) | Out-Null
$unread.IsChecked = $true
if ($type.GetField('_currentView', $flags).GetValue($drawer).Count -ne 0) { throw 'Unread filter included a read notification.' }
$unread.IsChecked = $false
if ($type.GetField('_currentView', $flags).GetValue($drawer).Count -ne 1) { throw 'Unread filter did not restore matching read notification.' }
$search.Clear()
if ($type.GetField('_currentView', $flags).GetValue($drawer).Count -ne 3) { throw 'Clearing search did not restore tickets.' }
Write-Output 'Passed search, unread filtering, and clear-search checks using sample tickets.'
