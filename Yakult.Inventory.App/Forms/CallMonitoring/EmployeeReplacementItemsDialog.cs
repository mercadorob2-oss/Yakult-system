using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class EmployeeReplacementItemsDialog : Form
    {
        private readonly CallEmployeeProfileLookup _employee;
        private readonly ICallMonitoringRepository _repo;
        private readonly DateTime _fromUtc;
        private readonly DateTime _toUtc;

        private TextBox txtSearch;
        private DataGridView dgv;
        private Label lblStatus;
        private Button btnClose;
        private readonly Timer _filterDebounce = new Timer { Interval = 250 };

        private List<CallEmployeeReplacementItemRow> _allRows = new List<CallEmployeeReplacementItemRow>();
        private List<CallEmployeeReplacementItemRow> _filtered = new List<CallEmployeeReplacementItemRow>();

        public EmployeeReplacementItemsDialog(CallEmployeeProfileLookup employee, ICallMonitoringRepository repo, DateTime fromUtc, DateTime toUtc)
        {
            _employee = employee ?? throw new ArgumentNullException(nameof(employee));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _fromUtc = AppTime.AssumeUtc(fromUtc);
            _toUtc = AppTime.AssumeUtc(toUtc);

            _filterDebounce.Tick += (_, __) =>
            {
                try
                {
                    _filterDebounce.Stop();
                    Filter();
                }
                catch
                {
                }
            };

            InitializeComponent();
            this.Shown += async (_, __) => await LoadAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _filterDebounce.Stop(); } catch { }
                try { _filterDebounce.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "Replacement Items";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.White;
            this.Font = ModernUiHelper.FontNormal;
            this.ClientSize = new Size(1180, 680);

            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24)
            };

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 92 };
            var lblTitle = ModernUiHelper.CreateHeaderLabel($"Replacement Items • {_employee.EmployeeName}");
            lblTitle.Dock = DockStyle.Top;
            topPanel.Controls.Add(lblTitle);

            var lblPeriod = new Label
            {
                Text = $"Period: {AppTime.ToLocalString(_fromUtc, "yyyy-MM-dd")} → {AppTime.ToLocalString(_toUtc, "yyyy-MM-dd")}",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 9F),
                Padding = new Padding(0, 4, 0, 8)
            };
            topPanel.Controls.Add(lblPeriod);

            var searchPanel = new Panel { Dock = DockStyle.Bottom, Height = 35 };
            txtSearch = ModernUiHelper.CreateTextBox();
            txtSearch.Dock = DockStyle.Fill;
            txtSearch.TextChanged += (_, __) => RequestFilterDebounced();

            var lblSearchIcon = new Label
            {
                Text = "🔍",
                AutoSize = true,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(0, 5, 5, 0),
                Font = new Font("Segoe UI Emoji", 12)
            };

            searchPanel.Controls.Add(txtSearch);
            searchPanel.Controls.Add(lblSearchIcon);
            topPanel.Controls.Add(searchPanel);

            mainPanel.Controls.Add(topPanel);

            var gridContainer = ModernUiHelper.CreateStyledPanel();
            gridContainer.Dock = DockStyle.Fill;
            gridContainer.Padding = new Padding(1);
            gridContainer.Margin = new Padding(0, 15, 0, 0);

            dgv = new DataGridView();
            ModernUiHelper.ConfigureModernGrid(dgv);
            dgv.Dock = DockStyle.Fill;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.CellDoubleClick += (_, __) => OpenSelectedTicket();

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "MarkedAt", HeaderText = "DATE", Width = 120 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "TicketCode", HeaderText = "TICKET #", Width = 110 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", Width = 140 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "OldItem", HeaderText = "OLD ITEM", Width = 270 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "NewItem", HeaderText = "NEW ITEM", Width = 270 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Qty", HeaderText = "QTY", Width = 60 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Remarks", HeaderText = "REMARKS", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            gridContainer.Controls.Add(dgv);
            mainPanel.Controls.Add(gridContainer);
            gridContainer.BringToFront();

            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = ModernUiHelper.ColorBackground,
                Padding = new Padding(24, 12, 24, 12)
            };
            footer.Paint += (s, e) => { using (var pen = new Pen(Color.FromArgb(220, 220, 220))) e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0); };

            lblStatus = new Label
            {
                AutoSize = true,
                Text = "Loading...",
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 9F),
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft
            };

            btnClose = ModernUiHelper.CreateSecondaryButton("Close");
            btnClose.Click += (_, __) => { this.DialogResult = DialogResult.OK; this.Close(); };

            var btnFlow = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, WrapContents = false };
            btnFlow.Controls.Add(btnClose);

            footer.Controls.Add(lblStatus);
            footer.Controls.Add(btnFlow);

            this.Controls.Add(mainPanel);
            this.Controls.Add(footer);

            this.CancelButton = btnClose;

            this.ResumeLayout(false);
        }

        private async Task LoadAsync()
        {
            try
            {
                _allRows = await _repo.GetEmployeeReplacementItemsAsync(_employee.EmpId, _fromUtc, _toUtc, maxRows: 5000);
                _filtered = new List<CallEmployeeReplacementItemRow>(_allRows);
                Populate();
            }
            catch
            {
                lblStatus.Text = "Failed to load replacement items.";
            }
        }

        private void Filter()
        {
            var q = (txtSearch.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(q))
            {
                _filtered = new List<CallEmployeeReplacementItemRow>(_allRows);
                Populate();
                return;
            }

            _filtered = _allRows.Where(r =>
                    ContainsIgnoreCase(r.TicketCode, q) ||
                    ContainsIgnoreCase(r.TicketStatus, q) ||
                    ContainsIgnoreCase(r.ReplacementOldItem, q) ||
                    ContainsIgnoreCase(r.ReplacementNewItem, q) ||
                    ContainsIgnoreCase(r.Remarks, q)
                )
                .ToList();

            Populate();
        }

        private void RequestFilterDebounced()
        {
            if (this.IsDisposed)
                return;

            try
            {
                _filterDebounce.Stop();
                _filterDebounce.Start();
            }
            catch
            {
                Filter();
            }
        }

        private static bool ContainsIgnoreCase(string haystack, string needle)
        {
            if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(needle))
                return false;
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Populate()
        {
            dgv.SuspendLayout();
            try
            {
                dgv.Rows.Clear();
                foreach (var r in _filtered)
                {
                    var rowId = dgv.Rows.Add(
                        AppTime.ToLocalString(r.ResolutionMarkedAtUtc, "yyyy-MM-dd"),
                        r.TicketCode ?? r.TicketId.ToString(),
                        r.TicketStatus,
                        r.ReplacementOldItem ?? "-",
                        r.ReplacementNewItem ?? "-",
                        r.ReplacementQty?.ToString() ?? "-",
                        r.Remarks ?? string.Empty
                    );
                    dgv.Rows[rowId].Tag = r;
                }
            }
            finally
            {
                dgv.ResumeLayout();
            }

            lblStatus.Text = $"{_filtered.Count} item(s)";
        }

        private void OpenSelectedTicket()
        {
            if (dgv.SelectedRows.Count == 0)
                return;

            if (!(dgv.SelectedRows[0].Tag is CallEmployeeReplacementItemRow row))
                return;

            if (row.TicketId <= 0)
                return;

            using (var dlg = new TicketDetailsDialog(_repo, row.TicketId))
            {
                dlg.ShowDialog(this);
            }
        }
    }
}

