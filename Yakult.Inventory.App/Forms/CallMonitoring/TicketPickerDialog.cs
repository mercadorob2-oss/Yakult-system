using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public class TicketPickerDialog : Form
    {
        private TextBox txtSearch;
        private DateTimePicker dtFrom;
        private DateTimePicker dtTo;
        private ComboBox cboRequester;
        private CheckBox chkShowSolvedClosed;
        private DataGridView dgvTickets;
        private Button btnSelect;
        private Button btnCancel;
        private Label lblStatus;

        private readonly List<CallTicketListItem> _allTickets;
        private List<CallTicketListItem> _filteredTickets;

        public CallTicketListItem SelectedTicket { get; private set; }
        public int SelectedTicketId => SelectedTicket?.TicketId ?? 0;

        public TicketPickerDialog(List<CallTicketListItem> tickets, string initialQuery = null)
        {
            _allTickets = tickets ?? new List<CallTicketListItem>();
            _filteredTickets = new List<CallTicketListItem>(_allTickets);
            InitializeComponent();

            InitializeDefaultDateRange();
            InitializeRequesterList();
             
            if (!string.IsNullOrWhiteSpace(initialQuery))
            {
                txtSearch.Text = initialQuery;
            }
            
            FilterList();
        }

        private static string GetRequester(CallTicketListItem t)
        {
            if (t == null) return string.Empty;

            var dept = (t.Department ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(dept)) return dept;

            var caller = (t.CallerName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(caller)) return caller;

            return string.Empty;
        }

        private void InitializeDefaultDateRange()
        {
            if (dtFrom == null || dtTo == null)
                return;

            if (_allTickets == null || _allTickets.Count == 0)
            {
                dtTo.Value = DateTime.Today;
                dtFrom.Value = DateTime.Today.AddDays(-30);
                return;
            }

            var ticketsForRange = _allTickets;
            if (chkShowSolvedClosed != null && !chkShowSolvedClosed.Checked)
            {
                ticketsForRange = _allTickets
                    .Where(t => t != null && !IsSolvedOrClosed(t.Status))
                    .ToList();
            }

            if (ticketsForRange == null || ticketsForRange.Count == 0)
            {
                dtTo.Value = DateTime.Today;
                dtFrom.Value = DateTime.Today.AddDays(-30);
                return;
            }

            var min = ticketsForRange.Min(t => t.CreatedAt).Date;
            var max = ticketsForRange.Max(t => t.CreatedAt).Date;

            if (min > max)
            {
                var tmp = min;
                min = max;
                max = tmp;
            }

            // Keep within DateTimePicker's bounds.
            if (min < dtFrom.MinDate) min = dtFrom.MinDate.Date;
            if (max > dtTo.MaxDate) max = dtTo.MaxDate.Date;

            dtFrom.Value = min;
            dtTo.Value = max;
        }

        private static bool IsSolvedOrClosed(string status)
        {
            var s = (status ?? string.Empty).Trim();
            return s.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                || s.Equals("Closed", StringComparison.OrdinalIgnoreCase)
                || s.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Text = "Select Ticket";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;
            this.ClientSize = new Size(950, 600);
            this.BackColor = Color.White;
            this.Font = ModernUiHelper.FontNormal;

            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24)
            };

            // 1. Header & Search
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 140 };
             
            var lblTitle = ModernUiHelper.CreateHeaderLabel("Search Ticket");
            lblTitle.Dock = DockStyle.Top;
            topPanel.Controls.Add(lblTitle);

            var filterPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            filterPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); // Search row
            filterPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 8)); // Spacer
            filterPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Filters row

            var filtersRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 1,
                Height = 30,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            filtersRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45)); // From label
            filtersRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // From picker
            filtersRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30)); // To label
            filtersRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // To picker
            filtersRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85)); // Requester label
            filtersRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Requester dropdown
            filtersRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); // Toggle

            dtFrom = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short, Font = ModernUiHelper.FontNormal };
            dtTo = new DateTimePicker { Width = 130, Format = DateTimePickerFormat.Short, Font = ModernUiHelper.FontNormal };
            dtFrom.ValueChanged += (_, __) => FilterList();
            dtTo.ValueChanged += (_, __) => FilterList();

            cboRequester = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = ModernUiHelper.FontNormal,
                Height = 28,
                Dock = DockStyle.Fill
            };
            cboRequester.SelectedIndexChanged += (_, __) => FilterList();

            chkShowSolvedClosed = new CheckBox
            {
                Text = "Show solved/closed",
                AutoSize = true,
                ForeColor = Color.Gray,
                Anchor = AnchorStyles.Left
            };
            chkShowSolvedClosed.CheckedChanged += (_, __) => FilterList();

            filtersRow.Controls.Add(new Label { Text = "From:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Gray }, 0, 0);
            filtersRow.Controls.Add(dtFrom, 1, 0);
            filtersRow.Controls.Add(new Label { Text = "To:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Gray }, 2, 0);
            filtersRow.Controls.Add(dtTo, 3, 0);
            filtersRow.Controls.Add(new Label { Text = "Requester:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Gray }, 4, 0);
            filtersRow.Controls.Add(cboRequester, 5, 0);
            filtersRow.Controls.Add(chkShowSolvedClosed, 6, 0);

            var searchPanel = new Panel { Dock = DockStyle.Bottom, Height = 35 };
            txtSearch = ModernUiHelper.CreateTextBox();
            txtSearch.Dock = DockStyle.Fill;
            txtSearch.TextChanged += (_, __) => FilterList();
            
            var lblSearchIcon = new Label { Text = "🔍", AutoSize = true, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(0, 5, 5, 0), Font = new Font("Segoe UI Emoji", 12) };
             
            searchPanel.Controls.Add(txtSearch);
            searchPanel.Controls.Add(lblSearchIcon);

            var filterSpacer = new Panel { Dock = DockStyle.Fill, Height = 8 };
            filterPanel.Controls.Add(searchPanel, 0, 0);
            filterPanel.Controls.Add(filterSpacer, 0, 1);
            filterPanel.Controls.Add(filtersRow, 0, 2);
            topPanel.Controls.Add(filterPanel);

            mainPanel.Controls.Add(topPanel);

            // 2. Grid
            var gridContainer = ModernUiHelper.CreateStyledPanel();
            gridContainer.Dock = DockStyle.Fill;
            gridContainer.Padding = new Padding(1); // Border
            gridContainer.Margin = new Padding(0, 15, 0, 0); // Spacing from top

            dgvTickets = new DataGridView();
            ModernUiHelper.ConfigureModernGrid(dgvTickets);
            dgvTickets.Dock = DockStyle.Fill;
            dgvTickets.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvTickets.MultiSelect = false;
            dgvTickets.CellDoubleClick += (_, __) => ConfirmSelection();
            
            // Columns
            dgvTickets.Columns.Add(new DataGridViewTextBoxColumn { Name = "TicketCode", HeaderText = "TICKET #", Width = 110 });
            dgvTickets.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "DATE", Width = 110 });
            dgvTickets.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", Width = 100 });
            dgvTickets.Columns.Add(new DataGridViewTextBoxColumn { Name = "Requester", HeaderText = "REQUESTER", Width = 150 });
            dgvTickets.Columns.Add(new DataGridViewTextBoxColumn { Name = "Subject", HeaderText = "SUBJECT", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            gridContainer.Controls.Add(dgvTickets);
            
            // Spacer to separate grid from top
            var spacer = new Panel { Dock = DockStyle.Top, Height = 20 };
            mainPanel.Controls.Add(gridContainer);
            mainPanel.Controls.Add(spacer);
            gridContainer.BringToFront();


            // 3. Footer
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
                Text = $"{_allTickets.Count} tickets",
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 9F),
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft
            };

            btnSelect = ModernUiHelper.CreatePrimaryButton("Select Ticket");
            btnSelect.Click += (_, __) => ConfirmSelection();

            btnCancel = ModernUiHelper.CreateSecondaryButton("Cancel");
            btnCancel.Click += (_, __) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            var btnFlow = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, WrapContents = false };
            btnFlow.Controls.Add(btnSelect);
            btnFlow.Controls.Add(btnCancel);

            footer.Controls.Add(lblStatus);
            footer.Controls.Add(btnFlow);

            this.Controls.Add(mainPanel);
            this.Controls.Add(footer);

            this.AcceptButton = btnSelect;
            this.CancelButton = btnCancel;

            this.ResumeLayout(false);
        }

        private void InitializeRequesterList()
        {
            if (cboRequester == null)
                return;

            var prev = cboRequester.SelectedItem as string;

            var requesters = _allTickets
                .Select(GetRequester)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            cboRequester.BeginUpdate();
            cboRequester.Items.Clear();
            cboRequester.Items.Add("All Requesters");
            foreach (var r in requesters) cboRequester.Items.Add(r);
            cboRequester.EndUpdate();

            var idx = !string.IsNullOrWhiteSpace(prev) ? cboRequester.FindStringExact(prev) : -1;
            cboRequester.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private void FilterList()
        {
            var txt = (txtSearch.Text ?? "").Trim().ToLowerInvariant();
            var hasTxt = !string.IsNullOrWhiteSpace(txt);

            var fromDate = dtFrom?.Value.Date ?? DateTime.MinValue.Date;
            var toDate = dtTo?.Value.Date ?? DateTime.MaxValue.Date;
            if (fromDate > toDate)
            {
                var tmp = fromDate;
                fromDate = toDate;
                toDate = tmp;
            }

            var requester = cboRequester?.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(requester) && requester.StartsWith("All", StringComparison.OrdinalIgnoreCase))
                requester = null;

            var includeSolvedClosed = chkShowSolvedClosed != null && chkShowSolvedClosed.Checked;

            _filteredTickets = _allTickets
                .Where(t =>
                {
                    if (t == null) return false;

                    if (!includeSolvedClosed && IsSolvedOrClosed(t.Status))
                        return false;

                    var createdDate = t.CreatedAt.Date;
                    if (createdDate < fromDate || createdDate > toDate) return false;

                    if (!string.IsNullOrWhiteSpace(requester))
                    {
                        var tRequester = GetRequester(t);
                        if (!string.Equals(tRequester, requester.Trim(), StringComparison.OrdinalIgnoreCase))
                            return false;
                    }

                    if (hasTxt)
                    {
                        var hay = $"{t.TicketCode} {t.Issue} {t.CallerName} {t.Department}".ToLowerInvariant();
                        if (!hay.Contains(txt)) return false;
                    }

                    return true;
                })
                .ToList();

            PopulateGrid();
        }

        private void PopulateGrid()
        {
            dgvTickets.Rows.Clear();
            foreach (var t in _filteredTickets)
            {
                var requester = GetRequester(t);
                var rowId = dgvTickets.Rows.Add(
                    t.TicketCode ?? t.TicketId.ToString(),
                    t.CreatedAt.ToString("yyyy-MM-dd"),
                    t.Status,
                    requester,
                    t.Issue
                );
                dgvTickets.Rows[rowId].Tag = t;
                
                // Status Color
                if (t.Status == "Closed" || t.Status == "Solved")
                {
                    dgvTickets.Rows[rowId].Cells[2].Style.ForeColor = ModernUiHelper.ColorSuccess;
                }
                else if (t.Status == "New" || t.Status == "Open")
                {
                     dgvTickets.Rows[rowId].Cells[2].Style.ForeColor = ModernUiHelper.ColorPrimary;
                }
            }
            lblStatus.Text = $"{_filteredTickets.Count} match(es)";
        }

        private void ConfirmSelection()
        {
            if (dgvTickets.SelectedRows.Count == 0)
                return;

            SelectedTicket = dgvTickets.SelectedRows[0].Tag as CallTicketListItem;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
