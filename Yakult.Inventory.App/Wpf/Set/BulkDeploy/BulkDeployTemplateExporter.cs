using System;
using ClosedXML.Excel;

namespace Yakult.Inventory.App.Wpf.Set.BulkDeploy
{
    public static class BulkDeployTemplateExporter
    {
        private static readonly string[] Headers =
        {
            "BundleKey*", "Department*", "ComputerName", "IPAddress", "ItemRole*",
            "ItemName*", "ModelNumber", "SerialNumber*", "FixedAssetNumber", "Category*",
            "Quantity*", "DateDeployed", "Condition", "Vendor", "Remarks", "Employee"
        };

        private static readonly int[] Widths =
            { 14, 22, 16, 15, 12, 30, 14, 18, 18, 14, 10, 13, 11, 18, 24, 20 };

        private static readonly string[][] Examples =
        {
            new[] { "AUDIT-001", "Audit", "Audit2", "192.168.13.2", "CPU", "LENOVO Thinkcentre m70s gen 5", "m70s gen 5", "SPCGM0Q36RB", "P-OE-CM-2503-1", "Desktop", "1", "2025-03-07", "Good", "", "", "" },
            new[] { "AUDIT-001", "Audit", "Audit2", "192.168.13.2", "Monitor", "thinkvision s22e", "S22e", "VR007C4L", "", "Monitor", "1", "2025-03-07", "Good", "", "", "" },
            new[] { "SALES-050", "Direct Sales", "DSales30", "192.168.11.30", "Laptop", "ThinkPad T14", "T14 G4", "GM0ABC123", "P-OE-LP-2501-5", "Laptop", "1", "2025-04-11", "Good", "", "single laptop, no bundle pair", "" },
        };

        private static readonly string[] Categories =
            { "Desktop", "Monitor", "Laptop", "Printer", "Keyboard", "Mouse", "UPS", "Charger", "Dock", "Accessories", "Other" };

        private static readonly string[] Roles =
            { "CPU", "Monitor", "Laptop", "Printer", "Keyboard", "Mouse", "UPS", "Charger", "Dock", "Other" };

        private static readonly string[] Conditions = { "Good", "Damaged" };

        public static void ExportTemplate(string filePath)
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Import");

                for (int i = 0; i < Headers.Length; i++)
                    ws.Cell(1, i + 1).Value = Headers[i];
                var hdr = ws.Range(1, 1, 1, Headers.Length);
                hdr.Style.Font.Bold = true;
                hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#70AD47");
                hdr.Style.Font.FontColor = XLColor.White;
                hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                for (int r = 0; r < Examples.Length; r++)
                    for (int c = 0; c < Examples[r].Length; c++)
                        ws.Cell(r + 2, c + 1).Value = Examples[r][c];
                ws.Range(2, 1, 1 + Examples.Length, Headers.Length).Style.Fill.BackgroundColor =
                    XLColor.FromHtml("#FFF2CC");

                for (int c = 0; c < Widths.Length; c++)
                    ws.Column(c + 1).Width = Widths[c];
                ws.SheetView.FreezeRows(1);

                var lists = wb.Worksheets.Add("Lists");
                lists.Cell(1, 1).Value = "Departments (canonical)";
                lists.Cell(1, 2).Value = "Categories";
                lists.Cell(1, 3).Value = "ItemRoles";
                lists.Cell(1, 4).Value = "Conditions";
                var listHdr = lists.Range(1, 1, 1, 4);
                listHdr.Style.Font.Bold = true;
                listHdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
                listHdr.Style.Font.FontColor = XLColor.White;

                var depts = DeptAliasMap.CanonicalDepartments;
                for (int i = 0; i < depts.Count; i++)
                    lists.Cell(i + 2, 1).Value = depts[i];
                for (int i = 0; i < Categories.Length; i++)
                    lists.Cell(i + 2, 2).Value = Categories[i];
                for (int i = 0; i < Roles.Length; i++)
                    lists.Cell(i + 2, 3).Value = Roles[i];
                for (int i = 0; i < Conditions.Length; i++)
                    lists.Cell(i + 2, 4).Value = Conditions[i];
                lists.Column(1).Width = 24;
                lists.Column(2).Width = 16;
                lists.Column(3).Width = 14;
                lists.Column(4).Width = 14;

                var readme = wb.Worksheets.Add("README");
                readme.Column(1).Width = 110;
                string[] lines =
                {
                    "DepartmentSet Import Template - HOW TO USE",
                    "",
                    "1. Fill the Import sheet, one row per UNIT (a PC plus monitor pair is 2 rows sharing one BundleKey).",
                    "2. Department must match the Lists sheet exactly. Variants like Acctg or GA are rejected on import.",
                    "3. SerialNumber is required and must be unique. Duplicates are blocked with an error.",
                    "4. Same ComputerName on rows with the same BundleKey is a bundle pair (OK). Same ComputerName on different BundleKeys is an error.",
                    "5. DateDeployed format is yyyy-mm-dd. Leave blank for a NULL dispatch date (Set stays Pending).",
                    "6. Quantity is 1 to 3. Use 1 for serialized PCs, laptops, and monitors.",
                    "7. FixedAssetNumber has no dedicated database column: the importer stores it in Item Description plus Request Remarks.",
                    "9. Employee is optional (Name or Employee #). Tick Employee link in the app to use it. Matched rows are assigned to that person; blank rows stay department level.",
                    "8. The 3 yellow example rows show a PC plus monitor bundle (AUDIT-001 is 1 Set with 2 items) and a single laptop. Overwrite or delete them.",
                    "",
                    "WHAT THE IMPORTER CREATES PER BUNDLE (1 transaction per BundleKey): Item rows (find or create by serial), dept-level Request rows, 1 dispatched Set with N SetItems, Inventory rows, and audit trail entries.",
                };
                for (int i = 0; i < lines.Length; i++)
                {
                    readme.Cell(i + 1, 1).Value = lines[i];
                    if (i == 0)
                        readme.Cell(i + 1, 1).Style.Font.Bold = true;
                }

                wb.SaveAs(filePath);
            }
        }
    }
}
