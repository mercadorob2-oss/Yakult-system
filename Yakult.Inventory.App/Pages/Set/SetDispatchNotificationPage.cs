using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Forms.SystemSettings;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Set
{
    /// <summary>
    /// Send Notifications page for IT staff (Inventory Sets).
    ///
    /// WORKFLOW:
    ///   1. Lists dispatched inventory Sets from the last 90 days.
    ///   2. IT selects a row to see details and edit the Received By.
    ///   3. IT clicks "Send Notification" to dispatch the SMTP email.
    ///
    /// Automatic SMTP sending has been removed from DispatchSetAsync.
    /// Notifications are now sent deliberately from this page.
    /// </summary>
    public sealed class SetDispatchNotificationPage : UserControl
    {
        // ── Repository ───────────────────────────────────────────────────────────────
        private readonly SetRepository _repo = new SetRepository();

        // ── Data ─────────────────────────────────────────────────────────────────────
        private List<DispatchedSetNotificationDto> _sets = new List<DispatchedSetNotificationDto>();
        private List<(int EmpId, string Name, string EmployeeNumber, string Position, string BranchName, string DepartmentName)> _employees;
        private DispatchedSetNotificationDto _selected;

        // ── Grid ─────────────────────────────────────────────────────────────────────
        private DataGridView _grid;

        // ── Detail panel controls ─────────────────────────────────────────────────────
        private Panel     _detailPanel;
        private Label     _lblSetCode;
        private Label     _lblEmployee;
        private Label     _lblBranchDept;
        private Label     _lblSetType;
        private Label     _lblStatus;
        private ComboBox  _cmbReceiver;
        private Button    _btnSaveReceiver;
        private TextBox   _txtEmailNotes;
        private Button    _btnSendNotification;
        private Button    _btnPreviewEmail;
        private Label     _lblFeedback;

        public SetDispatchNotificationPage()
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
                BackColor = Color.FromArgb(52, 152, 219),
                Padding   = new Padding(16, 0, 16, 0)
            };

            var title = new Label
            {
                Text      = "Send Notifications (Sets)",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock      = DockStyle.Left,
                AutoSize  = false,
                Width     = 340,
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
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
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
                Dock        = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor   = Color.White
            };
            bool splitterSet = false;
            split.SizeChanged += (s, e) =>
            {
                if (splitterSet || split.Width < 200) return;
                try
                {
                    split.SplitterDistance = (int)(split.Width * 0.72);
                    splitterSet = true;
                }
                catch { }
            };
            Controls.Add(split);
            split.BringToFront();

            // ── Grid (left panel) ─────────────────────────────────────────────────
            _grid = new DataGridView
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
            _grid.ColumnHeadersDefaultCellStyle.Font            = new Font("Segoe UI", 9F, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.BackColor       = Color.FromArgb(52, 152, 219);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor       = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.Padding         = new Padding(6, 0, 0, 0);
            _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            _grid.ColumnHeadersHeight                           = 40;
            _grid.ColumnHeadersHeightSizeMode                   = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.EnableHeadersVisualStyles                     = false;
            _grid.DefaultCellStyle.SelectionBackColor     = Color.FromArgb(52, 152, 219);
            _grid.DefaultCellStyle.SelectionForeColor     = Color.White;
            _grid.DefaultCellStyle.Padding                = new Padding(6, 6, 6, 6);

            var wrapStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True, Alignment = DataGridViewContentAlignment.MiddleLeft };

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date",     HeaderText = "Date",           FillWeight = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Code",     HeaderText = "Set Code",       FillWeight = 80  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Employee", HeaderText = "Employee",       FillWeight = 120, DefaultCellStyle = wrapStyle });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Branch",   HeaderText = "Branch / Dept",  FillWeight = 140, DefaultCellStyle = wrapStyle });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type",     HeaderText = "Type",           FillWeight = 70  });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Recv",     HeaderText = "Received By",    FillWeight = 120, DefaultCellStyle = wrapStyle });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status",   HeaderText = "Status",         FillWeight = 80  });

            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

            _grid.SelectionChanged += Grid_SelectionChanged;
            _grid.CellDoubleClick  += Grid_CellDoubleClick;
            split.Panel1.Controls.Add(_grid);

            // ── Detail panel (right) ──────────────────────────────────────────────
            _detailPanel = new Panel
            {
                Dock        = DockStyle.Fill,
                BackColor   = Color.White,
                Padding     = new Padding(20),
                AutoScroll  = true
            };
            BuildDetailPanel();
            split.Panel2.Controls.Add(_detailPanel);

            ClearDetail();
        }

        private void BuildDetailPanel()
        {
            _detailPanel.Controls.Clear();
            _detailPanel.Padding = new Padding(20);

            const int lw = 130;

            // ── Root TableLayoutPanel fills the detail panel ───────────────────────
            var root = new TableLayoutPanel
            {
                Dock            = DockStyle.Fill,
                ColumnCount     = 2,
                AutoSize        = false,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Padding         = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, lw + 5));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _detailPanel.Controls.Add(root);

            int r = 0;

            // ── Header ────────────────────────────────────────────────────────────
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            var header = new Label
            {
                Text      = "Set Details",
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 152, 219),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            };
            root.Controls.Add(header, 0, r);
            root.SetColumnSpan(header, 2);
            r++;

            // ── Divider ───────────────────────────────────────────────────────────
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
            var divider = new Panel
            {
                BackColor = Color.FromArgb(220, 220, 220),
                Height    = 1,
                Dock      = DockStyle.Top,
                Margin    = new Padding(0, 6, 0, 0)
            };
            root.Controls.Add(divider, 0, r);
            root.SetColumnSpan(divider, 2);
            r++;

            // ── Info rows ─────────────────────────────────────────────────────────
            _lblSetCode    = AddRootRow(root, "Set Code:",    r++, lw);
            _lblEmployee   = AddRootRow(root, "Employee:",    r++, lw);
            _lblBranchDept = AddRootRow(root, "Branch/Dept:", r++, lw);
            _lblSetType    = AddRootRow(root, "Set Type:",    r++, lw);
            _lblStatus     = AddRootRow(root, "Status:",      r++, lw);

            // ── Divider 2 ─────────────────────────────────────────────────────────
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            var div2 = new Panel
            {
                BackColor = Color.FromArgb(220, 220, 220),
                Height    = 1,
                Dock      = DockStyle.Top,
                Margin    = new Padding(0, 10, 0, 0)
            };
            root.Controls.Add(div2, 0, r);
            root.SetColumnSpan(div2, 2);
            r++;

            // ── Received By row ───────────────────────────────────────────────────
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

            var lblRecv = new Label
            {
                Text      = "Received By:",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(lblRecv, 0, r);

            var recvHost = new Panel { Dock = DockStyle.Fill };

            _cmbReceiver = new ComboBox
            {
                Location           = new Point(0, 10),
                Size               = new Size(220, 25),
                DropDownStyle      = ComboBoxStyle.DropDown,
                AutoCompleteMode   = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };

            _btnSaveReceiver = new Button
            {
                Text      = "Save",
                Location  = new Point(225, 10),
                Size      = new Size(60, 26),
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9F),
                Cursor    = Cursors.Hand
            };
            _btnSaveReceiver.FlatAppearance.BorderSize = 0;
            _btnSaveReceiver.Click += BtnSaveReceiver_Click;

            recvHost.Controls.AddRange(new Control[] { _cmbReceiver, _btnSaveReceiver });
            root.Controls.Add(recvHost, 1, r);
            r++;

            // ── Notes label ───────────────────────────────────────────────────────
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            var lblNotes = new Label
            {
                Text      = "Notes (optional):",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            };
            root.Controls.Add(lblNotes, 0, r);
            root.SetColumnSpan(lblNotes, 2);
            r++;

            // ── Notes textbox ─────────────────────────────────────────────────────
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            _txtEmailNotes = new TextBox
            {
                Dock       = DockStyle.Fill,
                Multiline  = true,
                ScrollBars = ScrollBars.Vertical,
                Font       = new Font("Segoe UI", 9F),
                BackColor  = Color.FromArgb(250, 250, 250)
            };
            root.Controls.Add(_txtEmailNotes, 0, r);
            root.SetColumnSpan(_txtEmailNotes, 2);
            r++;

            // ── Send button ───────────────────────────────────────────────────────
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            _btnSendNotification = new Button
            {
                Text      = "✉  Send Notification",
                Dock      = DockStyle.None,
                Location  = new Point(0, 6),
                Size      = new Size(200, 38),
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                Enabled   = false
            };
            _btnSendNotification.FlatAppearance.BorderSize = 0;
            _btnSendNotification.Click += BtnSendNotification_Click;

            _btnPreviewEmail = new Button
            {
                Text      = "👁  Preview HTML",
                Dock      = DockStyle.None,
                Location  = new Point(210, 6),
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

            var sendRowHost = new Panel { Dock = DockStyle.Fill };
            sendRowHost.Controls.Add(_btnSendNotification);
            sendRowHost.Controls.Add(_btnPreviewEmail);
            root.Controls.Add(sendRowHost, 0, r);
            root.SetColumnSpan(sendRowHost, 2);
            r++;

            // ── Feedback label ────────────────────────────────────────────────────
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _lblFeedback = new Label
            {
                Dock      = DockStyle.Fill,
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(39, 174, 96),
                Visible   = false
            };
            root.Controls.Add(_lblFeedback, 0, r);
            root.SetColumnSpan(_lblFeedback, 2);
        }

        private Label AddRootRow(TableLayoutPanel table, string labelText, int row, int lw)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lbl = new Label
            {
                Text      = labelText,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Margin    = new Padding(0, 6, 0, 4),
                AutoSize  = false
            };

            var val = new Label
            {
                Text        = "—",
                Font        = new Font("Segoe UI", 9F),
                AutoSize    = true,
                MaximumSize = new Size(300, 0),
                Margin      = new Padding(0, 6, 0, 4)
            };

            table.Controls.Add(lbl, 0, row);
            table.Controls.Add(val, 1, row);
            return val;
        }

        // ── Data loading ─────────────────────────────────────────────────────────────
        private void LoadData()
        {
            try
            {
                _sets      = _repo.GetDispatchedSetsForNotification();
                _employees = _repo.GetActiveEmployeesForReceiver();
                RefreshGrid();
                ClearDetail();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load notification data:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshGrid()
        {
            _grid.Rows.Clear();
            var today     = DateTime.Today;
            var yesterday = today.AddDays(-1);

            foreach (var s in _sets)
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
                row.Cells["Date"].Value     = date;
                row.Cells["Code"].Value     = s.SetCode;
                row.Cells["Employee"].Value = s.EmployeeName;
                row.Cells["Branch"].Value   = branchDept;
                row.Cells["Type"].Value     = s.SetType;
                row.Cells["Recv"].Value     = receiver;
                row.Cells["Status"].Value   = FriendlyStatus(s.SetStatus);
                row.Tag = s;

                switch (s.SetStatus)
                {
                    case "Dispatched":
                        row.DefaultCellStyle.BackColor = Color.FromArgb(220, 245, 220);
                        break;
                    case "Partial":
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 243, 200);
                        break;
                    case "Pending":
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 228, 225);
                        break;
                }

                if (!s.ReceivedById.HasValue)
                    row.Cells["Recv"].Style.ForeColor = Color.FromArgb(192, 57, 43);
            }

            _grid.ClearSelection();
        }

        // ── Grid selection ────────────────────────────────────────────────────────────
        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (_grid.SelectedRows.Count == 0) { ClearDetail(); return; }

            _selected = _grid.SelectedRows[0].Tag as DispatchedSetNotificationDto;
            if (_selected == null) { ClearDetail(); return; }

            ShowDetail(_selected);
        }

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var dto = _grid.Rows[e.RowIndex].Tag as DispatchedSetNotificationDto;
            if (dto == null) return;

            using (var detail = new ViewSetDetailPage(dto.SetId))
                detail.ShowDialog(this);

            LoadData();
        }

        private void ClearDetail()
        {
            _selected = null;
            if (_lblSetCode    != null) _lblSetCode.Text    = "—";
            if (_lblEmployee   != null) _lblEmployee.Text   = "—";
            if (_lblBranchDept != null) _lblBranchDept.Text = "—";
            if (_lblSetType    != null) _lblSetType.Text    = "—";
            if (_lblStatus     != null) _lblStatus.Text     = "—";
            if (_txtEmailNotes       != null) _txtEmailNotes.Text           = string.Empty;
            if (_btnSendNotification != null) _btnSendNotification.Enabled  = false;
            if (_btnPreviewEmail     != null) _btnPreviewEmail.Enabled      = false;
            if (_lblFeedback         != null) _lblFeedback.Visible          = false;
        }

        private void ShowDetail(DispatchedSetNotificationDto s)
        {
            _lblSetCode.Text    = s.SetCode;
            _lblEmployee.Text   = s.EmployeeName;
            _lblBranchDept.Text = $"{s.BranchName} / {s.DepartmentName}";
            _lblSetType.Text    = s.SetType;
            _lblStatus.Text     = FriendlyStatus(s.SetStatus);

            PopulateReceiverCombo(s.ReceivedById);

            _btnSendNotification.Enabled = true;
            _btnPreviewEmail.Enabled = true;
            _lblFeedback.Visible = false;
        }

        private void PopulateReceiverCombo(int? currentReceivedById)
        {
            _cmbReceiver.Items.Clear();
            _cmbReceiver.Items.Add(new ReceiverComboItem(0, "— Not Set —"));

            foreach (var emp in _employees)
            {
                var empNum   = !string.IsNullOrWhiteSpace(emp.EmployeeNumber) ? $" ({emp.EmployeeNumber})" : "";
                var deptPart = !string.IsNullOrWhiteSpace(emp.DepartmentName) ? $" – {emp.DepartmentName}" : "";
                _cmbReceiver.Items.Add(new ReceiverComboItem(emp.EmpId, $"{emp.Name}{empNum}{deptPart}"));
            }

            _cmbReceiver.SelectedIndex = 0;

            if (currentReceivedById.HasValue && currentReceivedById.Value > 0)
            {
                for (int i = 0; i < _cmbReceiver.Items.Count; i++)
                {
                    if (_cmbReceiver.Items[i] is ReceiverComboItem item && item.EmpId == currentReceivedById.Value)
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

            if (!(_cmbReceiver.SelectedItem is ReceiverComboItem chosen) || chosen.EmpId <= 0)
            {
                MessageBox.Show("Please select a receiver before saving.",
                    "Receiver Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
                _repo.UpdateReceivedByForSet(_selected.SetId, chosen.EmpId, userId);

                _selected.ReceivedById   = chosen.EmpId;
                _selected.ReceivedByName = chosen.Label;

                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if (row.Tag is DispatchedSetNotificationDto dto && dto.SetId == _selected.SetId)
                    {
                        row.Cells["Recv"].Value            = chosen.Label;
                        row.Cells["Recv"].Style.ForeColor  = Color.Empty;
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

            // Auto-save receiver if one is selected but not yet persisted
            if (!_selected.ReceivedById.HasValue || _selected.ReceivedById.Value <= 0)
            {
                if (_cmbReceiver.SelectedItem is ReceiverComboItem chosen && chosen.EmpId > 0)
                {
                    try
                    {
                        int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
                        _repo.UpdateReceivedByForSet(_selected.SetId, chosen.EmpId, userId);
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
            }

            _btnSendNotification.Enabled = false;
            _btnSendNotification.Text    = "Sending…";

            try
            {
                int setId = _selected.SetId;
                string errorMsg = await _repo.SendDeploymentEmailAsync(setId);

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
                var preview = await _repo.GetDeploymentEmailPreviewAsync(_selected.SetId);

                if (preview == null || !string.IsNullOrEmpty(preview.Error))
                {
                    MessageBox.Show(preview?.Error ?? "Could not build the email preview.",
                        "Email Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var dlg = new EmailTemplateHtmlPreviewDialog(
                    _selected.SetCode, preview.Subject, preview.BodyHtml, preview.IsHtml))
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

        private static string FriendlyStatus(string dbStatus)
        {
            switch (dbStatus)
            {
                case "Dispatched": return "Dispatched";
                case "Partial":    return "Partially Dispatched";
                case "Pending":    return "Pending";
                default:           return dbStatus ?? "—";
            }
        }

        private void ShowFeedback(string message, bool success)
        {
            _lblFeedback.Text      = message;
            _lblFeedback.ForeColor = success ? Color.FromArgb(39, 174, 96) : Color.FromArgb(192, 57, 43);
            _lblFeedback.Visible   = true;
            _detailPanel.ScrollControlIntoView(_lblFeedback);
        }

        // ── Helper ───────────────────────────────────────────────────────────────────
        private sealed class ReceiverComboItem
        {
            public int    EmpId { get; }
            public string Label { get; }

            public ReceiverComboItem(int empId, string label)
            {
                EmpId = empId;
                Label = label;
            }

            public override string ToString() => Label;
        }
    }
}
