using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using ExcelDataReader;

namespace Yakult.Inventory.App.Wpf.Set.BulkDeploy
{
    public static class BulkDeployExcelImporter
    {
        private static readonly string[] LegacyOrder =
        {
            "No.", "Department", "Item Name CPU", "Computer Name", "IP Address",
            "Serial Number CPU", "Model Number CPU", "Fixed Asset Number CPU",
            "Monitor item name", "Monitor S/N", "Monitor Model number", "Date Deploy"
        };

        private static readonly string[] TemplateOrder =
        {
            "BundleKey", "Department", "ComputerName", "IPAddress", "ItemRole",
            "ItemName", "ModelNumber", "SerialNumber", "FixedAssetNumber", "Category",
            "Quantity", "DateDeployed", "Condition", "Vendor", "Remarks", "Employee",
            "Company", "Branch"
        };

        public static List<BulkDeployRow> LoadRows(
            string filePath,
            bool includeDate, bool includeComputerName, bool includeIp,
            bool includeDeptCol, bool includeFixedAsset, bool includeEmployee = false,
            bool includeCompany = false, bool includeBranch = false, bool includeCategory = true)
        {
            var table = LoadExcelToDataTable(filePath);
            var rows = new List<BulkDeployRow>();
            if (table == null || table.Rows.Count == 0)
                return rows;

            bool legacy = IsLegacyTable(table);
            string[] order = legacy ? LegacyOrder : TemplateOrder;
            int[] indexes = order.Select(h => GetColumnIndex(table.Columns, h)).ToArray();

            var sb = new StringBuilder();
            foreach (DataRow dr in table.Rows)
            {
                var cells = new string[order.Length];
                bool allEmpty = true;
                for (int i = 0; i < order.Length; i++)
                {
                    string value = indexes[i] < 0 ? "" : GetCellString(dr[indexes[i]]);
                    cells[i] = value;
                    if (!string.IsNullOrWhiteSpace(value))
                        allEmpty = false;
                }
                if (allEmpty)
                    continue;
                sb.AppendLine(string.Join("\t", cells));
            }

            if (sb.Length == 0)
                return rows;
            return BulkDeployParser.ParseClipboardText(
                sb.ToString(), includeDate, includeComputerName, includeIp, includeDeptCol, includeFixedAsset, includeEmployee, legacy, includeCompany, includeBranch, includeCategory);
        }

        /// <summary>
        /// Column-mapping preview report for a candidate import file (feature:
        /// confirm-before-import dialog). Reads only the header row/shape, does
        /// NOT parse any data rows yet - lets the caller show the user exactly
        /// which template columns were found in their file, which are missing
        /// (and will import blank), and how many data rows were detected,
        /// before committing to LoadRows().
        /// </summary>
        public sealed class ColumnMappingReport
        {
            public bool IsLegacyFormat;
            public int DataRowCount;
            public List<string> MatchedColumns = new List<string>();
            public List<string> MissingColumns = new List<string>();
            public List<string> UnrecognizedFileColumns = new List<string>();
        }

        public static ColumnMappingReport InspectColumns(string filePath)
        {
            var report = new ColumnMappingReport();
            var table = LoadExcelToDataTable(filePath);
            if (table == null || table.Columns.Count == 0)
                return report;

            report.IsLegacyFormat = IsLegacyTable(table);
            string[] order = report.IsLegacyFormat ? LegacyOrder : TemplateOrder;

            foreach (var header in order)
            {
                if (GetColumnIndex(table.Columns, header) >= 0)
                    report.MatchedColumns.Add(header);
                else
                    report.MissingColumns.Add(header);
            }

            var recognized = new HashSet<string>(order.Select(NormHeader), StringComparer.OrdinalIgnoreCase);
            foreach (DataColumn col in table.Columns)
            {
                string norm = NormHeader(col.ColumnName);
                if (!string.IsNullOrEmpty(norm) && !recognized.Contains(norm))
                    report.UnrecognizedFileColumns.Add(col.ColumnName);
            }

            int[] indexes = order.Select(h => GetColumnIndex(table.Columns, h)).ToArray();
            int dataRows = 0;
            foreach (DataRow dr in table.Rows)
            {
                bool allEmpty = true;
                for (int i = 0; i < order.Length; i++)
                {
                    if (indexes[i] < 0) continue;
                    if (!string.IsNullOrWhiteSpace(GetCellString(dr[indexes[i]])))
                    {
                        allEmpty = false;
                        break;
                    }
                }
                if (!allEmpty) dataRows++;
            }
            report.DataRowCount = dataRows;

            return report;
        }

        private static bool IsLegacyTable(DataTable table)
        {
            return GetColumnIndex(table.Columns, "Computer Name") >= 0
                && GetColumnIndex(table.Columns, "Serial Number CPU") >= 0;
        }

        private static DataTable LoadExcelToDataTable(string filePath)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var conf = new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                };
                var dataSet = reader.AsDataSet(conf);
                if (dataSet.Tables.Count == 0)
                    return new DataTable();
                return dataSet.Tables[0];
            }
        }

        private static string NormHeader(string s)
        {
            return (s ?? "").Trim().TrimEnd('*').Trim();
        }

        private static int GetColumnIndex(DataColumnCollection columns, string name)
        {
            string want = NormHeader(name);
            for (int i = 0; i < columns.Count; i++)
            {
                if (string.Equals(NormHeader(columns[i].ColumnName), want, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static string GetCellString(object value)
        {
            if (value == null || value == DBNull.Value)
                return "";
            if (value is DateTime dt)
                return dt.ToString("yyyy-MM-dd");
            return (value.ToString() ?? "").Trim();
        }
    }
}
