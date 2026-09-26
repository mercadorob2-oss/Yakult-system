using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using ExcelDataReader;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs
{
    public partial class EmployeeImportColumnMappingDialog : Window
    {
        public bool Confirmed { get; private set; }

        public sealed class ColumnMappingReport
        {
            public int DataRowCount;
            public List<string> MatchedColumns = new List<string>();
            public List<string> MissingColumns = new List<string>();
            public List<string> UnrecognizedFileColumns = new List<string>();
        }

        private static readonly string[][] ExpectedGroups =
        {
            new[] { "Emp #", "Employee Number" },
            new[] { "Title" },
            new[] { "Name*", "Name" },
            new[] { "Position" },
            new[] { "Company*", "Company" },
            new[] { "Department" },
            new[] { "Branch*", "Branch" },
            new[] { "Active (TRUE/FALSE)", "Active" },
        };

        public EmployeeImportColumnMappingDialog(string fileName, ColumnMappingReport report)
        {
            InitializeComponent();

            SubtitleText.Text = string.Format("{0} \u2014 {1} data row(s) detected.",
                fileName, report.DataRowCount);

            MatchedText.Text = report.MatchedColumns.Count > 0
                ? string.Join(", ", report.MatchedColumns)
                : "(none)";

            MissingText.Text = report.MissingColumns.Count > 0
                ? string.Join(", ", report.MissingColumns)
                : "(none - every expected column was found)";

            if (report.UnrecognizedFileColumns.Count > 0)
            {
                UnrecognizedCard.Visibility = Visibility.Visible;
                UnrecognizedText.Text = string.Join(", ", report.UnrecognizedFileColumns.Take(20));
            }

            if (report.DataRowCount == 0)
            {
                BtnImport.IsEnabled = false;
                BtnImport.ToolTip = "No data rows were detected in this file's first sheet.";
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
        }

        public static ColumnMappingReport Inspect(string path)
        {
            var report = new ColumnMappingReport();
            string ext = (Path.GetExtension(path) ?? string.Empty).ToLowerInvariant();

            List<string> headers;
            List<string[]> dataLines;
            if (ext == ".csv")
            {
                var all = ReadCsvLines(path);
                if (all.Count == 0) return report;
                headers = all[0].Select(h => (h ?? string.Empty).Trim()).ToList();
                dataLines = all.Skip(1).ToList();
            }
            else
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var ds = reader.AsDataSet(new ExcelDataSetConfiguration
                    {
                        ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                    });
                    if (ds.Tables.Count == 0) return report;
                    var table = ds.Tables[0];
                    headers = table.Columns.Cast<System.Data.DataColumn>()
                        .Select(c => (c.ColumnName ?? string.Empty).Trim()).ToList();
                    dataLines = new List<string[]>();
                    foreach (System.Data.DataRow row in table.Rows)
                    {
                        var cells = new string[table.Columns.Count];
                        for (int i = 0; i < cells.Length; i++)
                        {
                            var v = row[i];
                            cells[i] = (v == null || v == DBNull.Value) ? string.Empty : (v.ToString() ?? string.Empty).Trim();
                        }
                        dataLines.Add(cells);
                    }
                }
            }

            var recognized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in ExpectedGroups)
            {
                int idx = FindHeaderIndex(headers, group);
                foreach (var alias in group)
                    recognized.Add(Norm(alias));
                if (idx >= 0)
                    report.MatchedColumns.Add(group[0]);
                else
                    report.MissingColumns.Add(group[0]);
            }

            foreach (var h in headers)
            {
                string norm = Norm(h);
                if (!string.IsNullOrEmpty(norm) && !recognized.Contains(norm))
                    report.UnrecognizedFileColumns.Add(h);
            }

            int nameIdx = FindHeaderIndex(headers, new[] { "Name*", "Name" });
            int count = 0;
            foreach (var line in dataLines)
            {
                bool any = line.Any(c => !string.IsNullOrWhiteSpace(c));
                if (!any) continue;
                if (nameIdx >= 0)
                {
                    string name = nameIdx < line.Length ? line[nameIdx] : string.Empty;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                }
                count++;
            }
            report.DataRowCount = count;
            return report;
        }

        private static int FindHeaderIndex(List<string> headers, string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                for (int i = 0; i < headers.Count; i++)
                {
                    if (string.Equals(Norm(headers[i]), Norm(candidate), StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }
            return -1;
        }

        private static string Norm(string s)
        {
            return (s ?? string.Empty).Trim();
        }

        private static List<string[]> ReadCsvLines(string path)
        {
            var rows = new List<string[]>();
            using (var reader = new StreamReader(path, Encoding.GetEncoding(1252), detectEncodingFromByteOrderMarks: true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    rows.Add(SplitCsvLine(line));
                }
            }
            return rows;
        }

        private static string[] SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                    else sb.Append(c);
                }
            }
            fields.Add(sb.ToString());
            return fields.ToArray();
        }
    }
}
