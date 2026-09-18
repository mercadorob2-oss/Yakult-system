using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using Microsoft.Reporting.WinForms;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Pages
{
    public partial class ReportViewerForm : Form
    {
        private string _reportPath;
        private byte[] _rdlcBytes;
        private string _reportTitle;
        private List<ReportDataSource> _dataSources = new List<ReportDataSource>();
        private ReportParameter[] _parameters;

        /// <summary>
        /// Constructor for simple report (no data binding needed)
        /// </summary>
        public ReportViewerForm(string reportPath, string reportTitle)
        {
            InitializeComponent();
            _reportPath = reportPath;
            _reportTitle = reportTitle;
            this.Text = reportTitle;
        }

        /// <summary>
        /// Constructor for report with a single data source
        /// </summary>
        public ReportViewerForm(string reportPath, string reportTitle, DataTable reportData, string dataSetName = "DataSet1")
        {
            InitializeComponent();
            _reportPath = reportPath;
            _reportTitle = reportTitle;
            _dataSources.Add(new ReportDataSource(dataSetName, reportData));
            this.Text = reportTitle;
        }

        /// <summary>
        /// Constructor for report with multiple data sources (e.g. main data + signatory data)
        /// </summary>
        public ReportViewerForm(string reportPath, string reportTitle, IEnumerable<(string dataSetName, DataTable data)> dataSources)
        {
            InitializeComponent();
            _reportPath = reportPath;
            _reportTitle = reportTitle;
            foreach (var ds in dataSources)
                _dataSources.Add(new ReportDataSource(ds.dataSetName, ds.data));
            this.Text = reportTitle;
        }

        /// <summary>
        /// Constructor for reports whose RDLC XML has been patched at runtime (e.g. dynamic column widths).
        /// </summary>
        public ReportViewerForm(byte[] rdlcBytes, string reportTitle, IEnumerable<(string dataSetName, DataTable data)> dataSources)
        {
            InitializeComponent();
            _rdlcBytes = rdlcBytes;
            _reportTitle = reportTitle;
            foreach (var ds in dataSources)
                _dataSources.Add(new ReportDataSource(ds.dataSetName, ds.data));
            this.Text = reportTitle;
        }

        /// <summary>
        /// Constructor for report with parameters
        /// </summary>
        public ReportViewerForm(string reportPath, string reportTitle, ReportParameter[] parameters)
        {
            InitializeComponent();
            _reportPath = reportPath;
            _reportTitle = reportTitle;
            _parameters = parameters;
            this.Text = reportTitle;
        }

        public void InitializeReport()
        {
            try
            {
                reportViewer1.ProcessingMode = ProcessingMode.Local;
                if (_rdlcBytes != null)
                    reportViewer1.LocalReport.LoadReportDefinition(new System.IO.MemoryStream(_rdlcBytes));
                else
                    reportViewer1.LocalReport.ReportPath = _reportPath;

                if (_parameters != null && _parameters.Length > 0)
                    reportViewer1.LocalReport.SetParameters(_parameters);

                if (_dataSources.Count > 0)
                {
                    reportViewer1.LocalReport.DataSources.Clear();
                    foreach (var rds in _dataSources)
                        reportViewer1.LocalReport.DataSources.Add(rds);
                }

                reportViewer1.RefreshReport();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading report: {_reportPath ?? "(bytes)"}", ex);
                MessageBox.Show($"Error loading report: {ex.Message}\n\nPlease check the log file for details.",
                    "Report Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ReportViewerForm_Load(object sender, EventArgs e)
        {
            InitializeReport();
        }

        /// <summary>
        /// Renders the report to PDF bytes using a fresh LocalReport — completely
        /// independent of the viewer's async render cycle, so it never conflicts.
        /// If <paramref name="signatoryData"/> is supplied it overrides whatever
        /// SignatoryData was stored at preview time.
        /// </summary>
        public byte[] RenderToPdf(DataTable signatoryData = null)
        {
            using (var local = new LocalReport())
            {
                if (_rdlcBytes != null)
                    local.LoadReportDefinition(new System.IO.MemoryStream(_rdlcBytes));
                else
                    local.ReportPath = _reportPath;

                if (_parameters != null && _parameters.Length > 0)
                    local.SetParameters(_parameters);

                foreach (var rds in _dataSources)
                {
                    if (rds.Name == "SignatoryData" && signatoryData != null)
                        local.DataSources.Add(new ReportDataSource("SignatoryData", signatoryData));
                    else
                        local.DataSources.Add(rds);
                }

                return local.Render("PDF");
            }
        }

        /// <summary>
        /// Applies report parameters before the form loads. Call before ShowDialog().
        /// </summary>
        public void SetReportParameters(ReportParameter[] parameters)
        {
            _parameters = parameters;
        }

        public Microsoft.Reporting.WinForms.ReportViewer GetViewer()
        {
            return reportViewer1;
        }
    }
}
