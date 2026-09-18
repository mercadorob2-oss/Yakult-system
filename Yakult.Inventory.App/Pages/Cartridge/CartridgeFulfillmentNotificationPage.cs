using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Forms.CartridgeManagement;
using Yakult.Inventory.App.Forms.SystemSettings;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    /// <summary>
    /// Send Notifications page for IT staff.
    ///
    /// WORKFLOW:
    ///   1. Lists fulfilled portal Sets from the last 90 days.
    ///   2. IT selects a row to see details and edit the Received By (PICKUP only).
    ///   3. IT clicks "Send Notification" to dispatch the SMTP email.
    ///
    /// This replaces the old ConfirmReceiverDialog auto-send pattern. The email is
    /// now a deliberate action taken AFTER fulfillment, not immediately after committing.
    /// </summary>
    public sealed class CartridgeFulfillmentNotificationPage : UserControl
    {
        // ── Repository ───────────────────────────────────────────────────────────────
        private readonly CartridgeManagementRepository _repo = new CartridgeManagementRepository();

        // ── Data ─────────────────────────────────────────────────────────────────────
        private List<FulfilledSetNotificationDto> _sets = new List<FulfilledSetNotificationDto>();
        private List<(int EmpId, string Name, string EmployeeNumber, string Position, string BranchName, string DepartmentName)> _employees;
        private FulfilledSetNotificationDto _selected;

        // ── Grid ─────────────────────────────────────────────────────────────────────
        private DataGridView _grid;

        // ── Pagination ────────────────────────────────────────────────────────────────
        private int _currentPage = 1;
        private const int PageSize = 20;
        private System.Windows.Forms.Button _btnFirst, _btnPrev, _btnNext, _btnLast;
        private Label _lblPageInfo;

        // ── Detail panel controls ─────────────────────────────────────────────────────
        private Panel     _detailPanel;
        private Label     _lblSetCode;
        private Label     _lblRequester;
        private Label     _lblBranchDept;
        private Label     _lblFulfillMethod;
        private Label     _lblStatus;
        private Panel     _receiverPanel;
        private ComboBox  _cmbReceiver;
        private Button    _btnSaveReceiver;
        private TextBox   _txtEmailNotes;
        private Button    _btnSendNotification;
        private Button    _btnPreviewEmail;
        private Label     _lblFeedback;

        public CartridgeFulfillmentNotificationPage()
        {
            BuildUi();
            LoadData();
        }

        // ── UI construction ──────────────────────────────────────────────────────────
        private void BuildUi()
        {
            BackColor = Color.White;
            Dock      = DockStyle.Fill;

            // ── Top bar ──────────────────────────────────────────────────────────────
            var topBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 56,
                BackColor = Color.White,
                Padding   = new Padding(16, 0, 16, 0)
            };

            var title = new Label
            {
                Text      = "Send Notifications",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.Black,
                Dock      = DockStyle.Left,
                AutoSize  = false,
                Width     = 300,
                TextAlign = ContentAlignment.MiddleLeft
            };
            topBar.Controls.Add(title);

            var btnRefresh = new Button
            {
                Text      = "⟳  Refresh",
                Font      = new Font("Segoe UI", 9.5F),
                Dock      = DockStyle.Right,
                Width     = 110,
                Height    = 36,
                Margin    = new Padding(0, 10, 0, 10),
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.FromArgb(40, 40, 40),
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => LoadData();
            topBar.Controls.Add(btnRefresh);

            Controls.Add(topBar);

            // ── Split container: list (left) + detail (right) ─────────────────────
            var split = new SplitContainer
            {
                Dock      = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = Color.White
            };
            // Set the splitter to ~72% left / 28% right once the control has real dimensions.
            bool splitterSet = false;
            split.SizeChanged += (s, e) =>
            {
                if (splitterSet || split.Width < 200) return;
                try
                {
                    split.SplitterDistance = (int)(split.Width * 0.72);
                    splitterSet = true;
                }
                catch { /* width not yet valid */ }
            };
            Controls.Add(split);
            split.BringToFront();

            // ── Grid (left panel) ─────────────────────────────────────────────────
            _grid = new DataGridView
            {
                Dock                     = DockStyle.Fill,
                ReadOnly                 = true,
                SelectionMode            = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect              = false,
                AllowUserToAddRows       = false,
                AllowUserToDeleteRows    = false,
                AllowUserToResizeRows    = false,
                AllowUserToResizeColumns = true,
                RowHeadersVisible        = false,
                BackgroundColor          = Color.White,
                BorderStyle              = BorderStyle.None,
                CellBorderStyle          = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                AutoSizeColumnsMode      = DataGridViewAutoSizeColumnsMode.Fill,
                Font                     = new Font("Segoe UI", 9F),
                GridColor                = Color.FromArgb(225, 225, 225),
                EnableHeadersVisualStyles = false
            };

            _grid.ColumnHeadersDefaultCellStyle.Font           = new Font("Segoe UI", 9F, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.BackColor       = Color.FromArgb(78, 154, 252);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor       = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(78, 154, 252);
            _grid.ColumnHeadersDefaultCellStyle.Padding         = new Padding(6, 0, 0, 0);
            _grid.ColumnHeadersHeight                           = 36;
            _grid.ColumnHeadersHeightSizeMode                   = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.DefaultCellStyle.SelectionBackColor           = Color.FromArgb(78, 154, 252);
            _grid.DefaultCellStyle.SelectionForeColor           = Color.White;
            _grid.DefaultCellStyle.Padding                      = new Padding(6, 6, 6, 6);

            var wrapStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True, Alignment = DataGridViewContentAlignment.MiddleLeft };

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date",   HeaderText = "Date",           FillWeight = 95,  MinimumWidth = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Code",   HeaderText = "Set Code",       FillWeight = 75,  MinimumWidth = 80  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Req",    HeaderText = "Requester",      FillWeight = 115, MinimumWidth = 100, DefaultCellStyle = wrapStyle });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Branch", HeaderText = "Branch / Dept",  FillWeight = 140, MinimumWidth = 120, DefaultCellStyle = wrapStyle });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Method", HeaderText = "Distribution",   FillWeight = 70,  MinimumWidth = 75  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Recv",   HeaderText = "Received By",    FillWeight = 115, MinimumWidth = 100, DefaultCellStyle = wrapStyle });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status",         FillWeight = 90,  MinimumWidth = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });

            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

            foreach (DataGridViewColumn col in _grid.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            _grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;

                // Don't repaint foreground colours on a selected row — let the blue
                // selection highlight (white text) win so the clicked row stands out.
                bool selected = _grid.Rows[e.RowIndex].Selected;

                var col = _grid.Columns[e.ColumnIndex].Name;
                if (col == "Status" && e.Value is string status)
                {
                    e.CellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                    if (!selected)
                    {
                        switch (status)
                        {
                            case "Fulfilled":           e.CellStyle.ForeColor = Color.FromArgb(39, 174, 96);  break;
                            case "Partially Fulfilled": e.CellStyle.ForeColor = Color.FromArgb(211, 84, 0);   break;
                            case "Unfulfilled":         e.CellStyle.ForeColor = Color.FromArgb(192, 57, 43);  break;
                        }
                    }
                }
                else if (col == "Recv" && e.Value is string recv && recv == "— Not Set —")
                {
                    if (!selected) e.CellStyle.ForeColor = Color.FromArgb(192, 57, 43);
                    e.CellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
                }
            };

            _grid.SelectionChanged += Grid_SelectionChanged;
            _grid.CellDoubleClick     += Grid_CellDoubleClick;

            // ── Pagination bar (bottom of left panel) ─────────────────────────────
            var paginationBar = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 36,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding   = new Padding(8, 4, 8, 4)
            };

            _btnFirst    = new System.Windows.Forms.Button { Text = "<<", Width = 36, Height = 26, Left = 0,   Top = 5, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8F) };
            _btnPrev     = new System.Windows.Forms.Button { Text = "<",  Width = 36, Height = 26, Left = 40,  Top = 5, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8F) };
            _lblPageInfo = new Label { AutoSize = false, Width = 180, Left = 82, Top = 10, Font = new Font("Segoe UI", 8.5F), TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(80, 80, 80) };
            _btnNext     = new System.Windows.Forms.Button { Text = ">",  Width = 36, Height = 26, Left = 266, Top = 5, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8F) };
            _btnLast     = new System.Windows.Forms.Button { Text = ">>", Width = 36, Height = 26, Left = 306, Top = 5, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8F) };

            _btnFirst.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            _btnPrev.FlatAppearance.BorderColor  = Color.FromArgb(200, 200, 200);
            _btnNext.FlatAppearance.BorderColor  = Color.FromArgb(200, 200, 200);
            _btnLast.FlatAppearance.BorderColor  = Color.FromArgb(200, 200, 200);

            _btnFirst.Click += (s, e) => { _currentPage = 1;            UpdatePagination(); };
            _btnPrev.Click  += (s, e) => { if (_currentPage > 1) { _currentPage--; UpdatePagination(); } };
            _btnNext.Click  += (s, e) => { _currentPage++;              UpdatePagination(); };
            _btnLast.Click  += (s, e) => { _currentPage = int.MaxValue; UpdatePagination(); };

            paginationBar.Controls.AddRange(new Control[] { _btnFirst, _btnPrev, _lblPageInfo, _btnNext, _btnLast });

            split.Panel1.Controls.Add(_grid);
            split.Panel1.Controls.Add(paginationBar);

            // ── Detail panel (right) ──────────────────────────────────────────────
            _detailPanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.White,
                Padding   = new Padding(20)
            };

            BuildDetailPanel();
            split.Panel2.Controls.Add(_detailPanel);

            ClearDetail();
        }

        private void BuildDetailPanel()
        {
            _detailPanel.Controls.Clear();

            int top = 20;
            int lw  = 110; // label width

            var header = new Label
            {
                Text      = "Set Details",
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Location  = new Point(0, top),
                AutoSize  = true
            };
            _detailPanel.Controls.Add(header);
            top += 36;

            var divider = new Panel { Location = new Point(0, top), Size = new Size(480, 1), BackColor = Color.FromArgb(220, 220, 220) };
            _detailPanel.Controls.Add(divider);
            top += 10;

            _lblSetCode       = AddDetailRow("Set Code:",    top, lw); top += 28;
            _lblRequester     = AddDetailRow("Requester:",   top, lw); top += 28;
            _lblBranchDept    = AddDetailRow("Branch/Dept:", top, lw); top += 28;
            _lblFulfillMethod = AddDetailRow("Dist. Method:", top, lw); top += 28;
            _lblStatus        = AddDetailRow("Status:",      top, lw); top += 28;

            top += 10;
            var div2 = new Panel { Location = new Point(0, top), Size = new Size(480, 1), BackColor = Color.FromArgb(220, 220, 220) };
            _detailPanel.Controls.Add(div2);
            top += 14;

            // ── Receiver section (PICKUP only) ─────────────────────────────────────
            _receiverPanel = new Panel
            {
                Location = new Point(0, top),
                Size     = new Size(480, 80),
                Visible  = false
            };

            var lblRecv = new Label
            {
                Text     = "Received By:",
                Font     = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(0, 8),
                Size     = new Size(lw, 20)
            };
            _receiverPanel.Controls.Add(lblRecv);

            _cmbReceiver = new ComboBox
            {
                Location             = new Point(lw + 5, 4),
                Size                 = new Size(240, 25),
                DropDownStyle        = ComboBoxStyle.DropDown,
                AutoCompleteMode     = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource   = AutoCompleteSource.ListItems
            };
            _receiverPanel.Controls.Add(_cmbReceiver);

            _btnSaveReceiver = new Button
            {
                Text      = "Save",
                Location  = new Point(lw + 250, 4),
                Size      = new Size(60, 26),
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9F),
                Cursor    = Cursors.Hand
            };
            _btnSaveReceiver.FlatAppearance.BorderSize = 0;
            _btnSaveReceiver.Click += BtnSaveReceiver_Click;
            _receiverPanel.Controls.Add(_btnSaveReceiver);

            var hint = new Label
            {
                Text      = "Select who physically collects the cartridges.",
                ForeColor = Color.Gray,
                Location  = new Point(lw + 5, 34),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8F)
            };
            _receiverPanel.Controls.Add(hint);

            _detailPanel.Controls.Add(_receiverPanel);
            top += 90;

            // ── Notes (optional) ──────────────────────────────────────────────────
            var lblNotes = new Label
            {
                Text     = "Notes (optional):",
                Font     = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(0, top),
                AutoSize = true
            };
            _detailPanel.Controls.Add(lblNotes);
            top += 20;

            _txtEmailNotes = new TextBox
            {
                Location   = new Point(0, top),
                Size       = new Size(460, 64),
                Multiline  = true,
                ScrollBars = ScrollBars.Vertical,
                Font       = new Font("Segoe UI", 9F),
                BackColor  = Color.FromArgb(250, 250, 250)
            };
            _detailPanel.Controls.Add(_txtEmailNotes);
            top += 74;

            // ── Send Notification button ───────────────────────────────────────────
            _btnSendNotification = new Button
            {
                Text      = "✉  Send Notification",
                Location  = new Point(0, top),
                Size      = new Size(200, 38),
                BackColor = Color.FromArgb(58, 134, 232),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                Enabled   = false
            };
            _btnSendNotification.FlatAppearance.BorderSize = 0;
            _btnSendNotification.Click += BtnSendNotification_Click;
            _detailPanel.Controls.Add(_btnSendNotification);

            _btnPreviewEmail = new Button
            {
                Text      = "👁  Preview HTML",
                Location  = new Point(210, top),
                Size      = new Size(150, 38),
                BackColor = Color.FromArgb(108, 122, 147),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                Enabled   = false
            };
            _btnPreviewEmail.FlatAppearance.BorderSize = 0;
            _btnPreviewEmail.Click += BtnPreviewEmail_Click;
            _detailPanel.Controls.Add(_btnPreviewEmail);
            top += 50;

            // ── Feedback label ─────────────────────────────────────────────────────
            _lblFeedback = new Label
            {
                Location    = new Point(0, top),
                AutoSize    = false,
                Size        = new Size(460, 60),
                MaximumSize = new Size(460, 0),   // fixed width, height grows with text
                Font        = new Font("Segoe UI", 9F),
                ForeColor   = Color.FromArgb(39, 174, 96),
                Visible     = false
            };
            _detailPanel.Controls.Add(_lblFeedback);
        }

        private Label AddDetailRow(string labelText, int top, int lw)
        {
            var lbl = new Label
            {
                Text     = labelText,
                Font     = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(0, top + 3),
                Size     = new Size(lw, 20)
            };
            _detailPanel.Controls.Add(lbl);

            var val = new Label
            {
                Text     = "—",
                Location = new Point(lw + 5, top + 3),
                AutoSize = true,
                Font     = new Font("Segoe UI", 9F)
            };
            _detailPanel.Controls.Add(val);
            return val;
        }

        // ── Data loading ─────────────────────────────────────────────────────────────
        private void LoadData()
        {
            try
            {
                _sets      = _repo.GetFulfilledSetsForNotification();
                _employees = _repo.GetActiveEmployeesForReceiver();
                _currentPage = 1;
                UpdatePagination();
                ClearDetail();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load notification data:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdatePagination()
        {
            int total      = _sets?.Count ?? 0;
            int totalPages = Math.Max(1, (int)Math.Ceiling((double)total / PageSize));
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1)          _currentPage = 1;

            var page = _sets == null
                ? new List<FulfilledSetNotificationDto>()
                : _sets.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();

            RefreshGrid(page);

            if (_lblPageInfo != null)
                _lblPageInfo.Text = total == 0
                    ? "No records"
                    : $"Page {_currentPage} of {totalPages}  ({total} records)";

            if (_btnFirst != null) _btnFirst.Enabled = _currentPage > 1;
            if (_btnPrev  != null) _btnPrev.Enabled  = _currentPage > 1;
            if (_btnNext  != null) _btnNext.Enabled  = _currentPage < totalPages;
            if (_btnLast  != null) _btnLast.Enabled  = _currentPage < totalPages;
        }

        private void RefreshGrid(List<FulfilledSetNotificationDto> page)
        {
            _grid.Rows.Clear();
            var today     = DateTime.Today;
            var yesterday = today.AddDays(-1);

            foreach (var s in page)
            {
                string date;
                if (s.CreatedAt == DateTime.MinValue)
                {
                    date = "—";
                }
                else
                {
                    var d = s.CreatedAt.Date;
                    if (d == today)
                        date = $"Today  {s.CreatedAt:HH:mm}";
                    else if (d == yesterday)
                        date = $"Yesterday  {s.CreatedAt:HH:mm}";
                    else
                    {
                        int daysAgo = (today - d).Days;
                        date = daysAgo <= 6
                            ? $"{daysAgo}d ago  {s.CreatedAt:HH:mm}"
                            : s.CreatedAt.ToString("MM/dd/yyyy  HH:mm");
                    }
                }

                string branchDept = $"{s.BranchName} / {s.DepartmentName}".Trim('/').Trim();
                string receiver   = string.IsNullOrWhiteSpace(s.ReceivedByName) ? "— Not Set —" : s.ReceivedByName;

                var row = _grid.Rows[_grid.Rows.Add()];
                row.Cells["Date"].Value   = date;
                row.Cells["Code"].Value   = s.SetCode;
                row.Cells["Req"].Value    = s.RequesterName;
                row.Cells["Branch"].Value = branchDept;
                row.Cells["Method"].Value = s.DistributionMethod;
                row.Cells["Recv"].Value   = receiver;
                row.Cells["Status"].Value = FriendlyStatus(s.SetStatus);
                row.Tag = s;

                // Subtle row background by fulfillment status
                switch (s.SetStatus)
                {
                    case "Dispatched":
                        row.DefaultCellStyle.BackColor = Color.FromArgb(232, 248, 232); // soft green
                        break;
                    case "Partial":
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 248, 220); // soft amber
                        break;
                    case "Pending":
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 232); // soft red
                        break;
                    default:
                        row.DefaultCellStyle.BackColor = Color.White;
                        break;
                }

                // Setting a row-level BackColor above also suppresses the grid-level selection
                // colour (an empty row-level SelectionBackColor falls back to the row BackColor,
                // not the grid's), so the clicked row never looked highlighted. Restore it here.
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(78, 154, 252);
                row.DefaultCellStyle.SelectionForeColor = Color.White;
            }

            _grid.ClearSelection();
        }

        // ── Grid selection ────────────────────────────────────────────────────────────
        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (_grid.SelectedRows.Count == 0)
            {
                ClearDetail();
                return;
            }

            _selected = _grid.SelectedRows[0].Tag as FulfilledSetNotificationDto;
            if (_selected == null)
            {
                ClearDetail();
                return;
            }

            ShowDetail(_selected);
        }

        private void ClearDetail()
        {
            _selected = null;
            if (_lblSetCode       != null) _lblSetCode.Text       = "—";
            if (_lblRequester     != null) _lblRequester.Text     = "—";
            if (_lblBranchDept    != null) _lblBranchDept.Text    = "—";
            if (_lblFulfillMethod != null) _lblFulfillMethod.Text = "—";
            if (_lblStatus        != null) _lblStatus.Text        = "—";
            if (_receiverPanel       != null) _receiverPanel.Visible       = false;
            if (_txtEmailNotes       != null) _txtEmailNotes.Text           = string.Empty;
            if (_btnSendNotification != null) _btnSendNotification.Enabled  = false;
            if (_btnPreviewEmail     != null) _btnPreviewEmail.Enabled      = false;
            if (_lblFeedback         != null) _lblFeedback.Visible          = false;
        }

        private void ShowDetail(FulfilledSetNotificationDto s)
        {
            _lblSetCode.Text       = s.SetCode;
            _lblRequester.Text     = s.RequesterName;
            _lblBranchDept.Text    = $"{s.BranchName} / {s.DepartmentName}";
            _lblFulfillMethod.Text = s.DistributionMethod;
            _lblStatus.Text        = FriendlyStatus(s.SetStatus);

            bool isPickup = string.Equals(s.DistributionMethod, "PICKUP", StringComparison.OrdinalIgnoreCase);
            _receiverPanel.Visible = isPickup;

            if (isPickup)
                PopulateReceiverCombo(s.ReceivedById);

            _btnSendNotification.Enabled = true;
            _btnPreviewEmail.Enabled = true;
            _lblFeedback.Visible = false;
        }

        private void PopulateReceiverCombo(int? currentReceivedById)
        {
            _cmbReceiver.Items.Clear();
            _cmbReceiver.Items.Add(new EmployeeComboItem(0, "— Select receiver —"));

            foreach (var emp in _employees)
            {
                var empNum   = !string.IsNullOrWhiteSpace(emp.EmployeeNumber) ? $" ({emp.EmployeeNumber})" : "";
                var deptPart = !string.IsNullOrWhiteSpace(emp.DepartmentName) ? $" – {emp.DepartmentName}" : "";
                _cmbReceiver.Items.Add(new EmployeeComboItem(emp.EmpId, $"{emp.Name}{empNum}{deptPart}"));
            }

            _cmbReceiver.SelectedIndex = 0;

            if (currentReceivedById.HasValue && currentReceivedById.Value > 0)
            {
                for (int i = 0; i < _cmbReceiver.Items.Count; i++)
                {
                    if (_cmbReceiver.Items[i] is EmployeeComboItem item && item.EmpId == currentReceivedById.Value)
                    {
                        _cmbReceiver.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        // ── Button handlers ───────────────────────────────────────────────────────────
        private void BtnSaveReceiver_Click(object sender, EventArgs e)
        {
            if (_selected == null) return;

            if (!(_cmbReceiver.SelectedItem is EmployeeComboItem chosen) || chosen.EmpId <= 0)
            {
                MessageBox.Show("Please select a receiver before saving.",
                    "Receiver Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;

                if (_selected.SubmissionSessionId.HasValue)
                    _repo.UpdateReceivedByForSession(_selected.SubmissionSessionId.Value, chosen.EmpId, userId);
                else
                    _repo.UpdateReceivedByForRequest(_selected.ReqId, chosen.EmpId, userId);

                // Update local state and refresh grid row
                _selected.ReceivedById   = chosen.EmpId;
                _selected.ReceivedByName = chosen.Label;

                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (row.Tag is FulfilledSetNotificationDto dto && dto.SetId == _selected.SetId)
                    {
                        row.Cells["Recv"].Value             = chosen.Label;
                        row.Cells["Recv"].Style.ForeColor   = Color.Empty;
                        row.Cells["Recv"].Style.Font        = null;
                        break;
                    }
                }

                ShowFeedback("Receiver saved.", success: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save receiver:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnSendNotification_Click(object sender, EventArgs e)
        {
            if (_selected == null) return;

            bool isPickup = string.Equals(_selected.DistributionMethod, "PICKUP", StringComparison.OrdinalIgnoreCase);

            // For PICKUP: receiver must be set before sending
            if (isPickup)
            {
                if (!_selected.ReceivedById.HasValue || _selected.ReceivedById.Value <= 0)
                {
                    // Auto-save if one is selected in the combo but not yet saved
                    if (_cmbReceiver.SelectedItem is EmployeeComboItem chosen && chosen.EmpId > 0)
                    {
                        try
                        {
                            int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
                            if (_selected.SubmissionSessionId.HasValue)
                                _repo.UpdateReceivedByForSession(_selected.SubmissionSessionId.Value, chosen.EmpId, userId);
                            else
                                _repo.UpdateReceivedByForRequest(_selected.ReqId, chosen.EmpId, userId);

                            _selected.ReceivedById   = chosen.EmpId;
                            _selected.ReceivedByName = chosen.Label;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Could not save receiver before sending:\n\n{ex.Message}",
                                "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show("Please select and save the receiver before sending the notification.",
                            "Receiver Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }

            _btnSendNotification.Enabled = false;
            _btnSendNotification.Text    = "Preparing…";

            try
            {
                int setId = _selected.SetId;

                var preview = await Task.Run(() => CartridgeManagementForm.GenerateEmailContentForSet(setId));

                if (preview == null || preview.Error != null)
                {
                    ShowFeedback($"Not sent: {preview?.Error ?? "Could not generate email content."}", success: false);
                    return;
                }

                // Substitute {NotesRow} before sending
                preview.Body = ApplyNotesRow(preview.Body, _txtEmailNotes.Text);

                _btnSendNotification.Text = "Sending…";

                string errorMsg = await Task.Run(() => CartridgeManagementForm.SendFulfillmentEmailWithPreview(preview));

                if (string.IsNullOrEmpty(errorMsg))
                    ShowFeedback("Notification sent successfully.", success: true);
                else
                    ShowFeedback($"Not sent: {errorMsg}", success: false);
            }
            catch (Exception ex)
            {
                ShowFeedback($"Send failed: {ex.Message}", success: false);
            }
            finally
            {
                _btnSendNotification.Text    = "✉  Send Notification";
                _btnSendNotification.Enabled = true;
            }
        }

        private async void BtnPreviewEmail_Click(object sender, EventArgs e)
        {
            if (_selected == null) return;

            _btnPreviewEmail.Enabled = false;
            var original = _btnPreviewEmail.Text;
            _btnPreviewEmail.Text = "Loading…";

            try
            {
                var preview = await Task.Run(() => CartridgeManagementForm.GenerateEmailContentForSet(_selected.SetId));

                if (preview == null || preview.Error != null)
                {
                    MessageBox.Show(preview?.Error ?? "Could not generate email content.",
                        "Email Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Substitute {NotesRow} so the preview matches exactly what Send would dispatch.
                var body = ApplyNotesRow(preview.Body, _txtEmailNotes.Text);

                using (var dlg = new EmailTemplateHtmlPreviewDialog(
                    _selected.SetCode, preview.Subject, body, isHtml: true))
                {
                    dlg.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not build the email preview:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnPreviewEmail.Text = original;
                _btnPreviewEmail.Enabled = true;
            }
        }

        // ── Double-click: show request details ───────────────────────────────────

        private async void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var s = _grid.Rows[e.RowIndex].Tag as FulfilledSetNotificationDto;
            if (s == null) return;
            await ShowSetDetailDialogAsync(s);
        }

        private async Task ShowSetDetailDialogAsync(FulfilledSetNotificationDto s)
        {
            List<SetDetailRequestDto> requests;
            List<CartridgeExchangeModelSummaryDto> modelSummaries;
            try
            {
                var setRepo = new SetRepository();
                requests      = await setRepo.GetSetRequestsAsync(s.SetId);
                modelSummaries = await setRepo.GetCartridgeExchangeModelSummariesBySetAsync(s.SetId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load cartridge details:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (var dlg = new SetRequestDetailDialog(s, requests, modelSummaries))
                dlg.ShowDialog(this);
        }

        private static string FriendlyStatus(string dbStatus)
        {
            switch (dbStatus)
            {
                case "Dispatched": return "Fulfilled";
                case "Partial":    return "Partially Fulfilled";
                case "Pending":    return "Unfulfilled";
                default:           return dbStatus ?? "—";
            }
        }

        /// <summary>
        /// Replaces {NotesRow} in the email body.
        /// If notes is empty the token is removed; otherwise a styled HTML row is injected.
        /// </summary>
        private static string ApplyNotesRow(string body, string notes)
        {
            string notesRow = string.Empty;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                // Preserve line breaks and escape minimal HTML characters
                string safeNotes = notes
                    .Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\r\n", "<br>")
                    .Replace("\n", "<br>");

                notesRow =
                    "<tr>" +
                    "<td style=\"padding:8px 0; color:#666;\"><strong>Notes:</strong></td>" +
                    $"<td style=\"padding:8px 0;\">{safeNotes}</td>" +
                    "</tr>";
            }
            return body.Replace("{NotesRow}", notesRow);
        }

        private void ShowFeedback(string message, bool success)
        {
            _lblFeedback.Text      = message;
            _lblFeedback.ForeColor = success ? Color.FromArgb(39, 174, 96) : Color.FromArgb(192, 57, 43);
            _lblFeedback.Visible   = true;
        }

        // ── Set Request Detail Dialog ─────────────────────────────────────────────

        private sealed class SetRequestDetailDialog : Form
        {
            public SetRequestDetailDialog(FulfilledSetNotificationDto s, List<SetDetailRequestDto> requests, List<CartridgeExchangeModelSummaryDto> modelSummaries)
            {
                SuspendLayout();

                Text            = $"Cartridge Details – {s.SetCode}";
                FormBorderStyle = FormBorderStyle.Sizable;
                StartPosition   = FormStartPosition.CenterParent;
                MinimizeBox     = false;
                ClientSize      = new Size(1060, 480);
                MinimumSize     = new Size(800, 340);
                Font            = new Font("Segoe UI", 9.5F);

                string statusLabel;
                switch (s.SetStatus)
                {
                    case "Dispatched": statusLabel = "Fulfilled";           break;
                    case "Partial":    statusLabel = "Partially Fulfilled"; break;
                    default:           statusLabel = "Unfulfilled";         break;
                }

                // ── Cartridge model grid ──────────────────────────────────────
                var grid = new DataGridView
                {
                    Dock                  = DockStyle.Fill,
                    ReadOnly              = true,
                    SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect           = false,
                    AllowUserToAddRows    = false,
                    AllowUserToDeleteRows = false,
                    RowHeadersVisible     = false,
                    BackgroundColor       = Color.White,
                    BorderStyle           = BorderStyle.None,
                    AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                    Font                  = new Font("Segoe UI", 9F),
                    GridColor             = Color.FromArgb(230, 230, 230)
                };
                grid.EnableHeadersVisualStyles                = false;
                grid.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
                grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(78, 154, 252);
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                grid.ColumnHeadersHeightSizeMode             = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
                grid.ColumnHeadersHeight                     = 30;
                grid.DefaultCellStyle.SelectionBackColor     = Color.FromArgb(78, 154, 252);
                grid.DefaultCellStyle.SelectionForeColor     = Color.White;

                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SetCode",   HeaderText = "Set Code",     FillWeight = 80  });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Dist",      HeaderText = "Distribution", FillWeight = 80  });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date",      HeaderText = "Date",         FillWeight = 110 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status",    HeaderText = "Status",       FillWeight = 110 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Model",     HeaderText = "Model",        FillWeight = 180 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Requested", HeaderText = "Requested",    FillWeight = 70  });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Issued",    HeaderText = "Issued",       FillWeight = 70  });
                grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Pending",   HeaderText = "Pending",      FillWeight = 70  });

                // Build requestByModel (same as SMTP email logic)
                var requestByModel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var req in requests ?? new List<SetDetailRequestDto>())
                {
                    if (req == null) continue;
                    var key = !string.IsNullOrWhiteSpace(req.ModelNumber)
                        ? req.ModelNumber.Trim()
                        : (!string.IsNullOrWhiteSpace(req.ItemName) ? req.ItemName.Trim() : "N/A");
                    if (requestByModel.ContainsKey(key)) requestByModel[key] += req.Quantity;
                    else requestByModel[key] = req.Quantity;
                }

                // Build summaryByModel from CartridgeMovement data (may be empty)
                var summaryByModel = new Dictionary<string, CartridgeExchangeModelSummaryDto>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in modelSummaries ?? new List<CartridgeExchangeModelSummaryDto>())
                {
                    if (m == null) continue;
                    var key = string.IsNullOrWhiteSpace(m.CartridgeModel) ? "N/A" : m.CartridgeModel.Trim();
                    if (summaryByModel.TryGetValue(key, out var existing))
                    {
                        existing.ReturnedEmptyQty += m.ReturnedEmptyQty;
                        existing.IssuedFullQty    += m.IssuedFullQty;
                        existing.UnfulfilledQty   += m.UnfulfilledQty;
                    }
                    else
                    {
                        summaryByModel[key] = new CartridgeExchangeModelSummaryDto
                        {
                            CartridgeModel   = key,
                            ReturnedEmptyQty = m.ReturnedEmptyQty,
                            IssuedFullQty    = m.IssuedFullQty,
                            UnfulfilledQty   = m.UnfulfilledQty
                        };
                    }
                }

                int setIssuedBrandNew = s.IssuedBrandNewQty;
                int setIssuedRefilled = s.IssuedRefilledQty;

                foreach (var kvp in requestByModel.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var  model       = string.IsNullOrWhiteSpace(kvp.Key) ? "N/A" : kvp.Key;
                    int  requestedQty = kvp.Value;
                    summaryByModel.TryGetValue(model, out var mv);

                    int returnedQty = mv != null ? mv.ReturnedEmptyQty : requestedQty;
                    int brandNewQty, refilledQty;
                    if (requestByModel.Count == 1)
                    { brandNewQty = setIssuedBrandNew; refilledQty = setIssuedRefilled; }
                    else
                    { brandNewQty = mv != null ? mv.IssuedFullQty : 0; refilledQty = 0; }

                    int totalIssued = brandNewQty + refilledQty;
                    int pendingQty  = Math.Max(0, requestedQty - totalIssued);

                    var row = grid.Rows[grid.Rows.Add()];
                    row.Cells["SetCode"].Value   = s.SetCode;
                    row.Cells["Dist"].Value      = s.DistributionMethod ?? "—";
                    row.Cells["Date"].Value      = s.CreatedAt == DateTime.MinValue ? "—" : s.CreatedAt.ToString("MM/dd/yyyy HH:mm");
                    row.Cells["Status"].Value    = statusLabel;
                    row.Cells["Model"].Value     = model;
                    row.Cells["Requested"].Value = requestedQty;
                    row.Cells["Issued"].Value    = totalIssued;
                    row.Cells["Pending"].Value   = pendingQty;

                    if (pendingQty > 0 && totalIssued == 0)
                        row.DefaultCellStyle.ForeColor = Color.FromArgb(192, 57, 43);  // unfulfilled — red
                    else if (pendingQty > 0)
                        row.DefaultCellStyle.ForeColor = Color.FromArgb(150, 80, 0);   // partial — amber
                    else
                        row.DefaultCellStyle.ForeColor = Color.FromArgb(39, 174, 96);  // fulfilled — green
                }

                // ── Close button (must be added before Fill control) ──────────
                var btnClose = new Button
                {
                    Text         = "Close",
                    DialogResult = DialogResult.Cancel,
                    Dock         = DockStyle.Bottom,
                    Height       = 36,
                    FlatStyle    = FlatStyle.Flat,
                    Font         = new Font("Segoe UI", 9.5F),
                    BackColor    = Color.FromArgb(245, 245, 245)
                };
                btnClose.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
                Controls.Add(btnClose);
                CancelButton = btnClose;

                Controls.Add(grid);

                ResumeLayout(true);
            }

        }

        // ── Helper ───────────────────────────────────────────────────────────────────
        private sealed class EmployeeComboItem
        {
            public int    EmpId { get; }
            public string Label { get; }

            public EmployeeComboItem(int empId, string label)
            {
                EmpId = empId;
                Label = label;
            }

            public override string ToString() => Label;
        }
    }
}
