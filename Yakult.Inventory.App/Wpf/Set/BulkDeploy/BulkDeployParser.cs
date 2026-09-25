using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Wpf.Set.BulkDeploy
{
    public static class BulkDeployParser
    {
        public static string AutoCategoryFromRole(string itemRole)
        {
            string role = itemRole != null ? itemRole.Trim() : "";
            if (string.Equals(role, "CPU", StringComparison.OrdinalIgnoreCase)) return "Desktop";
            if (string.Equals(role, "Monitor", StringComparison.OrdinalIgnoreCase)) return "Monitor";
            if (string.Equals(role, "Laptop", StringComparison.OrdinalIgnoreCase)) return "Laptop";
            if (string.Equals(role, "Printer", StringComparison.OrdinalIgnoreCase)) return "Printer";
            if (string.Equals(role, "UPS", StringComparison.OrdinalIgnoreCase)) return "UPS";
            if (string.Equals(role, "Other", StringComparison.OrdinalIgnoreCase)) return "Other";
            if (role.Length > 0) return "Accessories";
            return "";
        }

        public static List<BulkDeployRow> ParseClipboardText(string text, bool includeDate, bool includeComputerName, bool includeIp, bool includeDeptCol, bool includeFixedAsset, bool includeEmployee = false, bool? forceLegacy = null, bool includeCompany = false, bool includeBranch = false, bool includeCategory = true)
        {
            var outRows = new List<BulkDeployRow>();
            if (string.IsNullOrEmpty(text)) return outRows;
            var lines = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cells = line.Split('\t');
                for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim();
                bool legacy = forceLegacy.HasValue ? forceLegacy.Value : IsLegacyYpiRow(cells);
                if (legacy && cells.Length >= 12)
                    outRows.AddRange(SplitLegacyRow(cells, includeDate, includeComputerName, includeIp, includeDeptCol, includeFixedAsset));
                else
                    outRows.Add(MapTemplateRow(cells, includeDate, includeComputerName, includeIp, includeDeptCol, includeFixedAsset, includeEmployee, includeCompany, includeBranch, includeCategory));
            }
            return outRows;
        }

        private static bool IsLegacyYpiRow(string[] cells)
        {
            if (cells.Length == 12)
                return true;
            if (cells.Length < 12)
                return false;
            return IsYpiShaped(cells);
        }

        private static bool IsYpiShaped(string[] cells)
        {
            int no;
            System.Net.IPAddress addr;
            return cells.Length > 4
                && int.TryParse(cells[0], out no)
                && System.Net.IPAddress.TryParse(cells[4], out addr);
        }

        private static IEnumerable<BulkDeployRow> SplitLegacyRow(string[] c, bool incDate, bool incPc, bool incIp, bool incDept, bool incFa)
        {
            string dept = incDept ? c[1] : "";
            string pc = incPc ? c[3] : "";
            string ip = incIp ? c[4] : "";
            string date = incDate ? c[11] : "";
            string fa = incFa ? c[7] : "";
            yield return new BulkDeployRow
            {
                BundleKey = string.IsNullOrWhiteSpace(pc) ? Guid.NewGuid().ToString("N") : pc.Trim(),
                Department = dept, ComputerName = pc, IPAddress = ip,
                ItemRole = "CPU", ItemName = c[2], ModelNumber = c[6],
                SerialNumber = c[5], FixedAssetNumber = fa, Category = "Desktop",
                Quantity = 1, DateDeployedText = date, Condition = "Good"
            };
            if (!string.IsNullOrWhiteSpace(c[9]))
            {
                yield return new BulkDeployRow
                {
                    BundleKey = string.IsNullOrWhiteSpace(pc) ? Guid.NewGuid().ToString("N") : pc.Trim(),
                    Department = dept, ComputerName = pc, IPAddress = ip,
                    ItemRole = "Monitor", ItemName = c[8], ModelNumber = c[10],
                    SerialNumber = c[9], FixedAssetNumber = "", Category = "Monitor",
                    Quantity = 1, DateDeployedText = date, Condition = "Good"
                };
            }
        }

        private static BulkDeployRow MapTemplateRow(string[] c, bool incDate, bool incPc, bool incIp, bool incDept, bool incFa, bool incEmp = false, bool incCom = false, bool incBranch = false, bool incCat = true)
        {
            string Get(int i) { return i < c.Length ? c[i] : ""; }
            int qty = 1;
            int.TryParse(Get(10), out qty);
            if (qty < 1) qty = 1;
            if (qty > 3) qty = 3;
            return new BulkDeployRow
            {
                BundleKey = Get(0),
                Department = incDept ? Get(1) : "",
                ComputerName = incPc ? Get(2) : "",
                IPAddress = incIp ? Get(3) : "",
                ItemRole = Get(4), ItemName = Get(5), ModelNumber = Get(6),
                SerialNumber = Get(7), FixedAssetNumber = incFa ? Get(8) : "",
                Category = incCat ? Get(9) : AutoCategoryFromRole(Get(4)), Quantity = qty,
                DateDeployedText = incDate ? Get(11) : "",
                Condition = string.IsNullOrWhiteSpace(Get(12)) ? "Good" : Get(12),
                Vendor = Get(13), Remarks = Get(14),
                Employee = incEmp ? Get(15) : "",
                Company = incCom ? Get(16) : "",
                Branch = incBranch ? Get(17) : ""
            };
        }
    }
}
