using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Reporting.WinForms;
using System.Collections.Specialized;
using Yakult.Inventory.App.Core;


namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// Report helper utilities for RDLC/RDL rendering inside WinForms.
    /// Drop this file into Helpers and rebuild.
    /// </summary>
    public static class ReportHelper
    {
        /// <summary>
        /// Return the report path under the output's Reports folder.
        /// </summary>
        public static string GetReportPath(string rdlcFileName)
        {
            if (string.IsNullOrWhiteSpace(rdlcFileName))
                throw new ArgumentNullException(nameof(rdlcFileName));

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, "Reports", rdlcFileName);
        }

        /// <summary>
        /// Convenience method used by your UI: tries to show the requested report.
        /// It will use parameterless flow (ShowReportWithParameters with null).
        /// If the report expects a dataset named (e.g. "SetData"), ensure you call
        /// ShowReportWithData and supply a DataTable or call ShowReportWithParameters with parameters
        /// that let the helper load data.
        /// </summary>
        public static void ShowReport(string rdlcFileName, string windowTitle)
        {
            // Try the parameters-based flow — this will attempt to load a SetId if the RDLC expects one.
            ShowReportWithParameters(rdlcFileName, windowTitle, null);
        }

        /// <summary>
        /// Quick existence check for a report file in Reports folder.
        /// </summary>
        public static bool ReportExists(string rdlcFileName)
        {
            var p = GetReportPath(rdlcFileName);
            return File.Exists(p);
        }

        /// <summary>
        /// Show a report using a provided DataTable. dataSetName must match the dataset name inside the RDLC.
        /// </summary>
        public static void ShowReportWithData(string rdlcFileName, string windowTitle, DataTable dataTable, string dataSetName)
        {
            if (string.IsNullOrWhiteSpace(rdlcFileName))
                throw new ArgumentNullException(nameof(rdlcFileName));
            if (dataTable == null)
                throw new ArgumentNullException(nameof(dataTable));
            if (string.IsNullOrWhiteSpace(dataSetName))
                throw new ArgumentNullException(nameof(dataSetName));

            string rdlcPath = GetReportPath(rdlcFileName);
            if (!File.Exists(rdlcPath))
            {
                MessageBox.Show($"Report file not found: {rdlcPath}", "Report Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (var frm = BuildViewerForm(rdlcPath, windowTitle))
            {
                var viewer = (ReportViewer)frm.Controls[0];

                try
                {
                    viewer.Reset();
                    viewer.ProcessingMode = ProcessingMode.Local;

                    var ext = Path.GetExtension(rdlcPath).ToLowerInvariant();
                    if (ext == ".rdl")
                    {
                        using (var fs = File.OpenRead(rdlcPath))
                        {
                            viewer.LocalReport.LoadReportDefinition(fs);
                        }
                    }
                    else
                    {
                        viewer.LocalReport.ReportPath = rdlcPath;
                    }

                    viewer.LocalReport.DataSources.Clear();
                    var rds = new ReportDataSource(dataSetName, dataTable);
                    viewer.LocalReport.DataSources.Add(rds);

                    viewer.RefreshReport();
                    frm.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to show report:\n" + ex.Message, "Report Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Show a report by loading data internally based on parameters.
        /// Ensures the provided ReportParameter[] are applied to the LocalReport
        /// BEFORE RefreshReport() so the RDLC won't complain about missing params.
        /// </summary>
        public static void ShowReportWithParameters(string rdlcFileName, string windowTitle, ReportParameter[] parameters)
        {
            if (string.IsNullOrWhiteSpace(rdlcFileName))
                throw new ArgumentNullException(nameof(rdlcFileName));

            string rdlcPath = GetReportPath(rdlcFileName);
            if (!File.Exists(rdlcPath))
            {
                MessageBox.Show($"Report file not found: {rdlcPath}", "Report Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                // If parameters include SetId, try to parse it
                int setId = 0;
                if (parameters != null && parameters.Length > 0)
                {
                    var p = parameters.FirstOrDefault(x => string.Equals(x.Name, "SetId", StringComparison.OrdinalIgnoreCase));
                    if (p != null && p.Values != null && p.Values.Count > 0)
                        int.TryParse(p.Values[0], out setId);
                }

                // Default dataset name used by your RDLC (adjust if your RDLC uses a different name)
                var datasetName = "SetData";

                // Load the DataTable for the report. Ensure SQL returns columns exactly matching your RDLC fields.
                var dt = LoadSetReportData(setId);

                // Build viewer form and viewer control
                using (var frm = BuildViewerForm(rdlcPath, windowTitle))
                {
                    var viewer = (ReportViewer)frm.Controls[0];

                    viewer.Reset();
                    viewer.ProcessingMode = ProcessingMode.Local;

                    var ext = Path.GetExtension(rdlcPath).ToLowerInvariant();
                    if (ext == ".rdl")
                    {
                        using (var fs = File.OpenRead(rdlcPath))
                        {
                            viewer.LocalReport.LoadReportDefinition(fs);
                        }
                    }
                    else
                    {
                        viewer.LocalReport.ReportPath = rdlcPath;
                    }

                    // Clear any existing data sources and add the table we loaded
                    viewer.LocalReport.DataSources.Clear();
                    var rds = new ReportDataSource(datasetName, dt);
                    viewer.LocalReport.DataSources.Add(rds);

                    // IMPORTANT: set the report parameters *before* calling RefreshReport
                    if (parameters != null && parameters.Length > 0)
                    {
                        try
                        {
                            viewer.LocalReport.SetParameters(parameters);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Failed to set report parameters: " + ex.Message, "Report Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }

                    // Do a quick sanity check — does the report expect dataset names we didn't supply?
                    var expectedNames = viewer.LocalReport.GetDataSourceNames();
                    if (expectedNames != null && expectedNames.Count > 0)
                    {
                        bool missing = false;
                        foreach (var name in expectedNames)
                        {
                            if (!viewer.LocalReport.DataSources.Cast<ReportDataSource>().Any(ds => string.Equals(ds.Name, name, StringComparison.OrdinalIgnoreCase)))
                            {
                                missing = true;
                                break;
                            }
                        }
                        if (missing)
                        {
                            MessageBox.Show("Report expects dataset(s): " + string.Join(",", expectedNames) +
                                "\nBut the helper did not supply all required datasets. Make sure dataset name(s) match exactly.", "Report Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }

                    // Now render
                    viewer.RefreshReport();
                    frm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to show parameterized report:\n" + ex.Message, "Report Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        /// <summary>
        /// Load a DataTable with the columns your Set report expects. Adjust the SELECT list to match the RDLC field names.
        /// </summary>
        private static DataTable LoadSetReportData(int setId)
        {
            // This SQL returns one header row per SetId with aggregates and aliases matching your RDLC fields.
            // It does NOT assume dbo.[Set] has a 'Status' column; instead it derives Status from related Request rows (MAX).
            const string sql = @"
    SELECT
        s.SetId,
        s.SetCode,
        s.SetType,
        s.CreatedAt,
        s.DispatchDate,
        ISNULL(u.Name, '') AS CreatedBy,
        ISNULL(s.Remarks, '') AS Remarks,

        -- subtotal = sum(quantity * unitprice) for the set
        ISNULL(SUM(r.Quantity * ISNULL(r.UnitPrice,0)), 0) AS Subtotal,

        -- VAT example (12%) - adjust if you compute VAT differently
        ISNULL(ROUND(SUM(r.Quantity * ISNULL(r.UnitPrice,0)) * 0.12, 2), 0) AS VatAmount,

        -- WHT and Discount are read from the Set header if present; else 0
        ISNULL(s.WhtAmount, 0) AS WhtAmount,
        ISNULL(s.DiscountAmount, 0) AS DiscountAmount,

        -- total due = subtotal - discount + VAT - WHT (adjust if your formula differs)
        ISNULL(ROUND(SUM(r.Quantity * ISNULL(r.UnitPrice,0)) - ISNULL(s.DiscountAmount,0) + SUM(r.Quantity * ISNULL(r.UnitPrice,0))*0.12 - ISNULL(s.WhtAmount,0), 2), 0) AS TotalAmountDue,

        -- Derive a 'Status' for the set by taking the MAX of request statuses (fallback if set header has no Status column)
        ISNULL(
            (SELECT TOP 1 r2.Status FROM dbo.Request r2 WHERE r2.SetId = s.SetId ORDER BY r2.DateCreated DESC),
            ''
        ) AS Status

    FROM dbo.[Set] s
    LEFT JOIN dbo.Request r ON r.SetId = s.SetId
    LEFT JOIN dbo.Employee u ON s.CreatedBy = u.EmpId
    WHERE s.SetId = @SetId
    GROUP BY s.SetId, s.SetCode, s.SetType, s.CreatedAt, s.DispatchDate, u.Name, s.Remarks, s.WhtAmount, s.DiscountAmount
    ";

            var p = new SqlParameter("@SetId", setId);
            return GetDataTableFromSql(sql, p);
        }



        /// <summary>
        /// Generic SQL -> DataTable helper.
        /// </summary>
        public static DataTable GetDataTableFromSql(string sql, params SqlParameter[] parameters)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentNullException(nameof(sql));

            var dt = new DataTable();

            var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException("Connection string not configured.");

            using (var con = new SqlConnection(cs))
            using (var cmd = new SqlCommand(sql, con))
            using (var da = new SqlDataAdapter(cmd))
            {
                if (parameters != null && parameters.Length > 0)
                    cmd.Parameters.AddRange(parameters);

                da.Fill(dt);
            }

            return dt;
        }

        /// <summary>
        /// Builds a simple Form with a single docked ReportViewer.
        /// </summary>
        private static Form BuildViewerForm(string reportPath, string windowTitle)
        {
            var frm = new Form();
            frm.Text = string.IsNullOrWhiteSpace(windowTitle) ? Path.GetFileNameWithoutExtension(reportPath) : windowTitle;
            frm.StartPosition = FormStartPosition.CenterParent;
            frm.WindowState = FormWindowState.Maximized;

            var viewer = new ReportViewer();
            viewer.Dock = DockStyle.Fill;
            viewer.ProcessingMode = ProcessingMode.Local;

            frm.Controls.Add(viewer);

            return frm;
        }

        #region ReportParameterBuilder helper (public so other code can use it)

        /// <summary>
        /// Small builder convenience for ReportParameter[].
        /// </summary>
        public class ReportParameterBuilder
        {
            private readonly System.Collections.Generic.List<ReportParameter> _list = new System.Collections.Generic.List<ReportParameter>();

            public ReportParameterBuilder AddParameter(string name, object value)
            {
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentNullException(nameof(name));

                var v = value == null ? string.Empty : value.ToString();
                _list.Add(new ReportParameter(name, v));
                return this;
            }

            public ReportParameter[] Build()
            {
                return _list.ToArray();
            }
        }

        #endregion
    }
}

