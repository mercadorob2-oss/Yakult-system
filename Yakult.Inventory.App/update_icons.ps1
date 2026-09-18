# PowerShell script to add icon to all Form Designer.cs files

$designerFiles = @(
    "Pages\LoginPage.Designer.cs",
    "Pages\RegisterPage.Designer.cs",
    "Pages\ReportViewerForm.Designer.cs",
    "Pages\EditInventoryDialog.Designer.cs",
    "Pages\AddInventoryDialog.Designer.cs",
    "Dialogs\AddInventoryDialog.Designer.cs",
    "Dialogs\EditInventoryDialog.Designer.cs",
    "Pages\SoftwareServiceSetDialog.Designer.cs",
    "Pages\AddSetPage.Designer.cs",
    "Pages\EditRequestDialog.Designer.cs",
    "Pages\EmployeeDialog.Designer.cs",
    "Pages\EditItemDialog.Designer.cs",
    "Pages\BatchAddRequestDialog.Designer.cs",
    "Pages\CompanyDialog.Designer.cs",
    "Pages\DepartmentDialog.Designer.cs",
    "Pages\EditBranchDialog.Designer.cs",
    "Pages\BatchAddItemDialog.Designer.cs",
    "Pages\AddDepartmentDialog.Designer.cs",
    "Pages\AddBranchDialog.Designer.cs",
    "Pages\ViewSetDetailPage.Designer.cs",
    "Pages\ViewInvoiceDetailPage.Designer.cs",
    "Pages\QuickAddEmployeeDialog.Designer.cs",
    "Pages\QuickAddVendorDialog.Designer.cs",
    "Pages\QuickAddCategoryDialog.Designer.cs",
    "Pages\InvoiceImportDialog.Designer.cs",
    "Pages\ItemArchiveDetailsDialog.Designer.cs",
    "Pages\AddItemDialog.Designer.cs"
)

$iconLine = "            this.Icon = Helpers.ApplicationIcon.GetIcon();"

foreach ($file in $designerFiles) {
    $filePath = Join-Path $PSScriptRoot $file

    if (Test-Path $filePath) {
        Write-Host "Processing: $file"

        $content = Get-Content $filePath -Raw

        # Skip if icon is already set
        if ($content -match "this\.Icon\s*=") {
            Write-Host "  Already has icon, skipping..."
            continue
        }

        # Find the line with ClientSize and add Icon after it
        if ($content -match "(\s+this\.ClientSize\s*=\s*new System\.Drawing\.Size\([^;]+;)") {
            $newContent = $content -replace "(\s+this\.ClientSize\s*=\s*new System\.Drawing\.Size\([^;]+;)", "`$1`r`n$iconLine"
            Set-Content -Path $filePath -Value $newContent -NoNewline
            Write-Host "  Icon added successfully"
        }
        # If no ClientSize, try after AutoScaleMode
        elseif ($content -match "(\s+this\.AutoScaleMode\s*=\s*[^;]+;)") {
            $newContent = $content -replace "(\s+this\.AutoScaleMode\s*=\s*[^;]+;)", "`$1`r`n$iconLine"
            Set-Content -Path $filePath -Value $newContent -NoNewline
            Write-Host "  Icon added successfully (after AutoScaleMode)"
        }
        else {
            Write-Host "  Could not find suitable location, skipping..."
        }
    }
    else {
        Write-Host "File not found: $filePath"
    }
}

Write-Host "`nDone! All forms have been updated."
