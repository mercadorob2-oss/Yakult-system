$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'device-notice-preview\Yakult.Inventory.App.exe'))
$dtoType = $assembly.GetType('Yakult.Inventory.App.Repositories.AdminAlertDto', $true)
$alert = [Activator]::CreateInstance($dtoType)
$alert.Title = 'System update available'
$alert.Message = "Hello, good day!`r`n`r`nThere is an update available for the Yakult system. Please save your work and restart the application when convenient.`r`n`r`nThank you!"
$alert.CreatedByName = 'IT Department (preview)'
$alert.CreatedAt = [DateTime]::UtcNow
$type = $assembly.GetType('Yakult.Inventory.App.Dialogs.MaintenanceAlertDialog', $true)
$argsForNotice = New-Object object[] 1
$argsForNotice[0] = $alert.psobject.BaseObject
$form = [Activator]::CreateInstance($type, [Reflection.BindingFlags]'Instance,NonPublic', $null, $argsForNotice, $null)
try {
    if ($form.Text -ne $alert.Title) { throw 'Custom notice title was not applied.' }
    $form.StartPosition = [Windows.Forms.FormStartPosition]::Manual
    $form.Location = [Drawing.Point]::new(-32000,-32000)
    $form.Show()
    [Windows.Forms.Application]::DoEvents()
    $form.PerformLayout()
    $bitmap = New-Object Drawing.Bitmap $form.Width,$form.Height
    try {
        $form.DrawToBitmap($bitmap, [Drawing.Rectangle]::new(0,0,$form.Width,$form.Height))
        $bitmap.Save((Join-Path $PSScriptRoot 'device-notification-preview.png'), [Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }
    Write-Output 'Custom recipient title verified and notice rendered using sample content. No broadcast queued.'
} finally { $form.Dispose() }

