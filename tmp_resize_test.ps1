Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$asm = [Reflection.Assembly]::LoadFrom('Yakult.Inventory.App\bin\Debug-Codex2\Yakult.Inventory.App.exe')
$t = $asm.GetType('Yakult.Inventory.App.Forms.CallMonitoring.NotificationDrawerControl', $true)
$nested = $t.GetNestedType('TicketRowControl', [Reflection.BindingFlags]'NonPublic')
$row = [Activator]::CreateInstance($nested)

$container = New-Object System.Windows.Forms.Panel
$container.Size = New-Object System.Drawing.Size(0, 200)
$container.AutoScroll = $true
$container.Controls.Add($row)
$row.Dock = [System.Windows.Forms.DockStyle]::Top
$container.CreateControl(); $row.CreateControl();
$container.PerformLayout(); $row.PerformLayout();

Write-Host "initial container client=$($container.ClientRectangle) row=$($row.Bounds)"

# Now resize container
$container.Size = New-Object System.Drawing.Size(800, 200)
$container.PerformLayout(); $row.PerformLayout();
Write-Host "after resize container client=$($container.ClientRectangle) row=$($row.Bounds)"

# Find right panel bounds
$pnlMain = $row.Controls | Where-Object { $_ -is [System.Windows.Forms.Panel] -and $_.Dock -eq [System.Windows.Forms.DockStyle]::Fill } | Select-Object -First 1
$pnlRight = $pnlMain.Controls | Where-Object { $_ -is [System.Windows.Forms.Panel] -and $_.Dock -eq [System.Windows.Forms.DockStyle]::Right } | Select-Object -First 1
Write-Host "pnlMain=$($pnlMain.Bounds) pnlRight=$($pnlRight.Bounds)"

