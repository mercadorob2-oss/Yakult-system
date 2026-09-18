$ErrorActionPreference = "Stop"

$outputPath = Join-Path (Get-Location) "Subsystem_Test_Cases.xlsx"

$datasets = [ordered]@{
    "ITCM" = @(
        @("Dashboard", "Open ITCM dashboard", "ITCM dashboard loads and ticket summary appears"),
        @("New Ticket", "Create a new ticket with required details", "New ticket is created and appears in pending tickets"),
        @("Ticket List", "Search and filter tickets", "Matching tickets appear based on selected filters"),
        @("Assign To Me", "Assign selected ticket to current user", "Selected ticket is assigned to the current employee"),
        @("Reassign Ticket", "Reassign selected ticket to another employee", "Ticket assignment is updated successfully"),
        @("Add Note", "Add note in Solutions tab", "Ticket note is saved successfully"),
        @("Update Status", "Change ticket status using allowed workflow status", "Ticket status is updated successfully"),
        @("Update Priority", "Change ticket priority", "Ticket priority is updated successfully"),
        @("Reopen Ticket", "Reopen final ticket with required note", "Ticket is reopened and returns to pending tickets"),
        @("Mark As Service Only", "Mark selected ticket as service only resolution", "Ticket is marked solved and resolution is recorded"),
        @("Mark As Replacement", "Mark selected ticket as replacement resolution", "Ticket replacement is recorded and ticket is marked solved"),
        @("Mark As Temporary Replacement", "Mark selected ticket as temporary replacement", "Ticket is marked resolved temporary and resolution is recorded"),
        @("Ticket Details", "Open ticket details and history", "Full ticket details notes and history appear"),
        @("Profiles", "View technician profile statistics", "Profile data and workload statistics appear"),
        @("Reports Filter", "Filter ticket reports", "Filtered report data appears"),
        @("Reports Export CSV", "Export filtered report to CSV", "CSV export file is generated successfully"),
        @("Reports Export PDF", "Export filtered report to PDF", "PDF export file is generated successfully"),
        @("Email Notifications", "Open email settings templates and logs", "Email settings templates and logs appear"),
        @("Diagnostics", "Open diagnostics and review system health", "Schema health and email pipeline status appear"),
        @("SMTP Test", "Run SMTP test from diagnostics", "Test email result is displayed successfully")
    )
    "Borrow System" = @(
        @("Borrow Dashboard", "Open borrow items dashboard", "Borrow dashboard loads with borrow and return panels"),
        @("Resolve Item", "Enter serial number and resolve existing item", "Matching item details appear"),
        @("Borrow Item", "Select borrower and borrow item", "Borrow transaction is saved and open borrow appears"),
        @("Add New External Item and Borrow", "Create new item then borrow it", "New item is created and borrow transaction is recorded"),
        @("Listed Inventory Borrow", "Use listed inventory mode and borrow existing item", "Existing item is borrowed without creating a new item"),
        @("Open Borrows", "View current open borrowed items", "Open borrow list appears with borrower and elapsed time"),
        @("Resolve Return", "Find open borrow by serial number", "Open borrow details appear in return panel"),
        @("Return Item", "Return resolved borrowed item", "Selected open borrow is closed and return is recorded"),
        @("Return Selected", "Return item from open borrows grid", "Selected borrow record is returned successfully"),
        @("History", "View borrow history", "Completed borrow and return records appear"),
        @("CSV Export Open", "Export open borrow records", "CSV file is generated for the open borrows tab"),
        @("CSV Export History", "Export borrow history records", "CSV file is generated for the history tab")
    )
    "Yakult Scanner" = @(
        @("Login", "Log in with valid account", "User is redirected to scanner home screen"),
        @("Register", "Register a new mobile account", "New account is created successfully"),
        @("Root Home", "Open scanner dashboard", "Main dashboard and navigation options appear"),
        @("Dispatch Scan Device", "Scan dispatch QR using scanner device", "Dispatch details screen opens and set information loads"),
        @("Dispatch Scan Camera", "Scan dispatch QR using phone camera", "Dispatch details screen opens and set information loads"),
        @("Item Status Update", "Update item status in dispatch details", "Item change is saved in the current update list"),
        @("Item Remark Update", "Add item remark in dispatch details", "Item remark is saved in the current update list"),
        @("Upload Changes", "Upload dispatch item changes", "Changes are uploaded successfully"),
        @("View Uploads", "Open uploaded items for a set", "Uploaded mobile updates for the set appear"),
        @("Deploy Set", "Deploy scanned token-based set", "Selected set is deployed successfully"),
        @("Generate PDF", "Generate dispatch PDF with signatures", "PDF file is generated successfully"),
        @("Direct Serial Scan", "Scan or type serial and send to Windows", "Serials are sent and success or failure count appears"),
        @("Batch Serial Entry", "Create items using batch serial entry", "Items are created and created count appears"),
        @("Pending Updates", "View pending mobile updates", "Pending mobile updates appear in the list"),
        @("Processed Updates", "View processed mobile updates", "Processed mobile updates appear in the list"),
        @("History", "View scan history", "Recent scan history appears"),
        @("App Info / Diagnostics", "Open diagnostics and connection info", "Connection and app diagnostic details appear"),
        @("Help", "Open help and usage screen", "Help and usage information appears")
    )
}

$excel = $null
$workbook = $null

try {
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $excel.DisplayAlerts = $false
    $workbook = $excel.Workbooks.Add()

    while ($workbook.Worksheets.Count -gt 1) {
        $workbook.Worksheets.Item($workbook.Worksheets.Count).Delete()
    }

    $sheetIndex = 1
    foreach ($sheetName in $datasets.Keys) {
        if ($sheetIndex -eq 1) {
            $sheet = $workbook.Worksheets.Item(1)
            $sheet.Name = $sheetName
        }
        else {
            $sheet = $workbook.Worksheets.Add()
            $sheet.Name = $sheetName
        }

        $headers = @("Feature/Module", "Test Case Description", "Expected Result")
        for ($col = 0; $col -lt $headers.Count; $col++) {
            $sheet.Cells.Item(1, $col + 1) = $headers[$col]
        }

        $rowIndex = 2
        foreach ($row in $datasets[$sheetName]) {
            for ($col = 0; $col -lt $row.Count; $col++) {
                $sheet.Cells.Item($rowIndex, $col + 1) = $row[$col]
            }
            $rowIndex++
        }

        $headerRange = $sheet.Range("A1", "C1")
        $headerRange.Font.Bold = $true
        $headerRange.Interior.Color = 0xD9EAD3
        $headerRange.Borders.LineStyle = 1

        $usedRange = $sheet.UsedRange
        $usedRange.WrapText = $true
        $usedRange.VerticalAlignment = -4160
        $usedRange.Borders.LineStyle = 1

        $sheet.Columns.Item(1).ColumnWidth = 28
        $sheet.Columns.Item(2).ColumnWidth = 42
        $sheet.Columns.Item(3).ColumnWidth = 48
        $sheet.Rows.Item(1).RowHeight = 24

        $sheet.Range("A2:C200").Rows.AutoFit() | Out-Null
        $sheet.Activate() | Out-Null
        $excel.ActiveWindow.SplitRow = 1
        $excel.ActiveWindow.FreezePanes = $true

        $sheetIndex++
    }

    if (Test-Path $outputPath) {
        Remove-Item $outputPath -Force
    }

    $workbook.SaveAs($outputPath, 51)
    Write-Output "Created: $outputPath"
}
finally {
    if ($workbook -ne $null) {
        $workbook.Close($true) | Out-Null
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($workbook)
    }
    if ($excel -ne $null) {
        $excel.Quit()
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
    }

    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}
