Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$asm = [Reflection.Assembly]::LoadFrom('Yakult.Inventory.App\bin\Debug-Codex2\Yakult.Inventory.App.exe')
$t = $asm.GetType('Yakult.Inventory.App.Forms.CallMonitoring.NotificationDrawerControl', $true)
$nested = $t.GetNestedType('TicketRowControl', [Reflection.BindingFlags]'NonPublic')
$row = [Activator]::CreateInstance($nested)

$container = New-Object System.Windows.Forms.Panel
$container.Size = New-Object System.Drawing.Size(800, 200)
$container.AutoScroll = $true
$container.Controls.Add($row)
$row.Dock = [System.Windows.Forms.DockStyle]::Top

$container.CreateControl()
$row.CreateControl()
$container.PerformLayout()
$row.PerformLayout()

Write-Host ("container: $($container.Bounds) client=$($container.ClientRectangle)")
Write-Host ("row bounds: $($row.Bounds)")

foreach ($c in $row.Controls) {
  Write-Host (" child $($c.GetType().Name) dock=$($c.Dock) bounds=$($c.Bounds)")
  foreach ($cc in $c.Controls) {
    Write-Host ("  grandchild $($cc.GetType().Name) dock=$($cc.Dock) bounds=$($cc.Bounds) text='$($cc.Text)'")
    foreach ($ccc in $cc.Controls) {
      Write-Host ("   ggchild $($ccc.GetType().Name) dock=$($ccc.Dock) bounds=$($ccc.Bounds) text='$($ccc.Text)'")
    }
  }
}
