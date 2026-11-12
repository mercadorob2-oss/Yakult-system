$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'messenger-preview\Yakult.Inventory.App.exe')) | Out-Null
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
