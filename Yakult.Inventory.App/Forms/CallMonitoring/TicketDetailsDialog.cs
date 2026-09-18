using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class TicketDetailsDialog : Form
    {
        public enum TicketDetailAction
        {
            None,
            OpenEmailLog,
            Reassign,
            MarkAs,
            Reopen
        }

        private readonly ICallMonitoringRepository _repo;
        private readonly int _ticketId;
        private CallTicketListItem _loadedTicket;
        private TextBox _txtIssueRender;

        // UI Controls
        private Label lblHeaderTitle;
        private Label lblHeaderSubtitle;
        private Label lblStatusBadge;
        private Button btnClose;
        private Button btnDelete;
        private Button btnCopyId;
        private Button btnCopySummary;
        private Button btnEmailLog;
        private Button btnReassign;
        private Button btnMarkAs;
        private Button btnReopen;

        private Label lblCallerName;
        private Label lblDeptName;
        private Label lblAssigneeName;
        private Label lblPriority;
        private Label lblLastContactValue;
        private Label lblCreatedValue;

        private Label lblIssueDescription;
        
        private DataGridView dgvNotes;
        private DataGridView dgvHistory;
        private Label lblLoadStatus;

        public TicketDetailAction RequestedAction { get; private set; } = TicketDetailAction.None;

        public TicketDetailsDialog(ICallMonitoringRepository repo, int ticketId)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _ticketId = ticketId;
            if (_ticketId <= 0) throw new ArgumentOutOfRangeException(nameof(ticketId));

            InitializeComponent();
            this.Shown += async (_, __) => await LoadAsync();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "Ticket Details";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimizeBox = false;
            this.MaximizeBox = true;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(245, 247, 250); // Light gray background
            this.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            this.ClientSize = new Size(980, 720);
            this.MinimumSize = new Size(860, 560);

            // 1. HEADER PANEL
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.White,
                Padding = new Padding(24, 16, 24, 16)
            };
            
            this.lblHeaderTitle = new Label
            {
                Text = "Ticket Details", // Will update on load
                AutoSize = true,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location = new Point(20, 16)
            };

            this.lblHeaderSubtitle = new Label
            {
                Text = $"TicketId: {_ticketId}", 
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(149, 165, 166),
                Location = new Point(22, 48)
            };

            this.lblStatusBadge = new Label
            {
                Text = "Loading...",
                AutoSize = false,
                Size = new Size(120, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Gray, // Default
                Location = new Point(300, 20) // Positioned relative to title (approx)
                // Note: We'll adjust location in Load or Layout
            };
            // Round corners for badge would require custom paint, simplified for standard Label here
            
            this.btnClose = new Button
            {
                Text = "Close",
                Size = new Size(100, 36),
                BackColor = Color.FromArgb(52, 73, 94),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
            };
            this.btnClose.FlatAppearance.BorderSize = 0;
            this.btnClose.Click += (_, __) => this.Close();

            this.btnDelete = new Button
            {
                Text = "Delete",
                Size = new Size(100, 36),
                BackColor = Color.FromArgb(231, 76, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            this.btnDelete.FlatAppearance.BorderSize = 0;
            this.btnDelete.Click += async (_, __) => await DeleteTicketAsync();
            this.btnDelete.Enabled = AppSession.IsLoggedIn && (AppSession.IsAdmin || AppSession.IsDeveloper);

            this.btnCopyId = CreateHeaderButton("Copy ID", Color.FromArgb(71, 85, 105));
            this.btnCopyId.Click += (_, __) => CopyTicketId();

            this.btnCopySummary = CreateHeaderButton("Copy Summary", Color.FromArgb(71, 85, 105));
            this.btnCopySummary.Click += (_, __) => CopyTicketSummary();

            this.btnEmailLog = CreateHeaderButton("Email Log", Color.FromArgb(14, 116, 144));
            this.btnEmailLog.Click += (_, __) => OpenEmailLogShortcut();

            var headerButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            headerButtons.Controls.Add(this.btnClose);
            headerButtons.Controls.Add(this.btnDelete);
            headerButtons.Controls.Add(this.btnEmailLog);
            headerButtons.Controls.Add(this.btnCopySummary);
            headerButtons.Controls.Add(this.btnCopyId);

            pnlHeader.Controls.Add(headerButtons);
            pnlHeader.Controls.Add(this.lblStatusBadge);
            pnlHeader.Controls.Add(this.lblHeaderSubtitle);
            pnlHeader.Controls.Add(this.lblHeaderTitle);

            // 2. MAIN BODY SCROLLABLE
            var pnlBody = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(24)
            };

            // 2a. KEY INFO CARDS (Grid-like layout but cleaner)
            var tlpInfo = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 1,
                Height = 100,
                Margin = new Padding(0, 0, 0, 20)
            };
            tlpInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            tlpInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            tlpInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));

            // Column 1: Caller Info
            var pnlCol1 = CreateInfoPanel();
            pnlCol1.Controls.Add(CreateMetaLabel("Caller"));
            this.lblCallerName = CreateValueLabel("-");
            pnlCol1.Controls.Add(this.lblCallerName);
            pnlCol1.Controls.Add(CreateMetaLabel("Department"));
            this.lblDeptName = CreateValueLabel("-", isSub: true);
            pnlCol1.Controls.Add(this.lblDeptName);
            
            // Column 2: Assignment Info
            var pnlCol2 = CreateInfoPanel();
            pnlCol2.Controls.Add(CreateMetaLabel("Assigned To"));
            this.lblAssigneeName = CreateValueLabel("-");
            pnlCol2.Controls.Add(this.lblAssigneeName);
             pnlCol2.Controls.Add(CreateMetaLabel("Priority"));
            this.lblPriority = CreateValueLabel("-", isSub: true); // Update color dynamically
            pnlCol2.Controls.Add(this.lblPriority);

            // Column 3: Timing Info
            var pnlCol3 = CreateInfoPanel();
            pnlCol3.Controls.Add(CreateMetaLabel("Last Update"));
            this.lblLastContactValue = CreateValueLabel("-");
            pnlCol3.Controls.Add(this.lblLastContactValue);
             pnlCol3.Controls.Add(CreateMetaLabel("Created"));
            this.lblCreatedValue = CreateValueLabel("-", isSub: true);
            pnlCol3.Controls.Add(this.lblCreatedValue);

            tlpInfo.Controls.Add(pnlCol1, 0, 0);
            tlpInfo.Controls.Add(pnlCol2, 1, 0);
            tlpInfo.Controls.Add(pnlCol3, 2, 0);

            // 2b. ISSUE DESCRIPTION BOX
            var grpIssue = new GroupBox
            {
                Text = "Issue Description",
                Dock = DockStyle.Top,
                Height = 180,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Padding = new Padding(15, 25, 15, 15),
                BackColor = Color.White
            };
            
            // Container for text to allow scrolling if huge
            var pnlIssueText = new Panel 
            { 
                Dock = DockStyle.Fill, 
                BackColor = Color.FromArgb(250, 252, 254), // Very slight blue/gray tint
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10)
            };
            this.lblIssueDescription = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Loading...",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(44, 62, 80),
                TextAlign = ContentAlignment.TopLeft,
                AutoEllipsis = false, // Let it flow? No, simple label inside scrollable panel? Label auto-size is tricky.
                // Better: Use a readonly textbox with NO border for robust multiline rendering
            };
            // Actually, ReadOnly TextBox with no border is best for large wrap text users can select/copy
            var txtIssueRender = new TextBox
            {
                 Dock = DockStyle.Fill,
                 ReadOnly = true,
                 Multiline = true,
                 BorderStyle = BorderStyle.None,
                 BackColor = Color.FromArgb(250, 252, 254),
                 ScrollBars = ScrollBars.Vertical,
                 Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                 ForeColor = Color.FromArgb(44, 62, 80)
            };
            _txtIssueRender = txtIssueRender;
            this.lblIssueDescription = new Label(); // Dummy to satisfy field, we use txtIssueRender locally to bind
            grpIssue.Controls.Add(pnlIssueText);
            pnlIssueText.Controls.Add(txtIssueRender);
            this.Tag = txtIssueRender; // Hack to access it in Load

            // 2c. TABS (Notes & History)
            var pnlTabs = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 20, 0, 0) };
            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ItemSize = new Size(100, 30)
            };

            var tpNotes = new TabPage("Notes") { Padding = new Padding(8), UseVisualStyleBackColor = true };
            dgvNotes = MakeGrid();
            SetupNotesColumns();
            tpNotes.Controls.Add(dgvNotes);

            var tpHistory = new TabPage("History") { Padding = new Padding(8), UseVisualStyleBackColor = true };
            dgvHistory = MakeGrid();
            SetupHistoryColumns();
            tpHistory.Controls.Add(dgvHistory);

            tabs.TabPages.Add(tpNotes);
            tabs.TabPages.Add(tpHistory);
            pnlTabs.Controls.Add(tabs);

            // Assemble Body
            pnlBody.Controls.Add(pnlTabs);
            pnlBody.Controls.Add(grpIssue);
            pnlBody.Controls.Add(tlpInfo);

            // Footer Status
            lblLoadStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.Gray,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(10, 0, 0, 0)
            };

            this.Controls.Add(pnlBody);
            this.Controls.Add(lblLoadStatus);
            this.Controls.Add(pnlHeader);

            var actionBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                BackColor = Color.White,
                Padding = new Padding(16, 10, 16, 10)
            };
            actionBar.Paint += (_, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(221, 226, 232)))
                    e.Graphics.DrawLine(pen, 0, 0, actionBar.Width, 0);
            };

            var actionFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };

            btnMarkAs = CreateActionButton("Mark As", Color.FromArgb(52, 73, 94));
            btnMarkAs.Click += (_, __) => RequestAction(TicketDetailAction.MarkAs);
            btnReassign = CreateActionButton("Reassign", Color.FromArgb(155, 89, 182));
            btnReassign.Click += (_, __) => RequestAction(TicketDetailAction.Reassign);
            btnReopen = CreateActionButton("Reopen", Color.FromArgb(22, 163, 74));
            btnReopen.Click += (_, __) => RequestAction(TicketDetailAction.Reopen);

            actionFlow.Controls.Add(btnMarkAs);
            actionFlow.Controls.Add(btnReassign);
            actionFlow.Controls.Add(btnReopen);
            actionBar.Controls.Add(actionFlow);
            this.Controls.Add(actionBar);

            this.AcceptButton = btnClose;
            this.CancelButton = btnClose;

            this.ResumeLayout(false);
        }

        // --- HELPER COMPONENT FACTORIES ---

        private static Button CreateHeaderButton(string text, Color backColor)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(112, 36),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(4, 0, 0, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private static Button CreateActionButton(string text, Color backColor)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(116, 36),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(8, 0, 0, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private Panel CreateInfoPanel()
        {
            var p = new FlowLayoutPanel 
            { 
                 Dock = DockStyle.Fill, 
                 FlowDirection = FlowDirection.TopDown,
                 WrapContents = false,
                 Padding = new Padding(10),
                 BackColor = Color.White
            };
            // Simulate card border? 
            // For now just white background separate from main gray bg
            return p;
        }

        private Label CreateMetaLabel(string text)
        {
            return new Label
            {
                Text = text.ToUpper(),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(149, 165, 166), // Muted header
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
        }

        private Label CreateValueLabel(string text, bool isSub = false)
        {
            return new Label
            {
                Text = text,
                Font = isSub ? new Font("Segoe UI", 9F, FontStyle.Regular) : new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = isSub ? Color.FromArgb(90, 90, 90) : Color.FromArgb(44, 62, 80),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, isSub ? 0 : 8)
            };
        }

        private DataGridView MakeGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = true,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(80, 90, 100);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grid.EnableHeadersVisualStyles = false;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(239, 246, 255);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(44, 62, 80);
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            grid.CellFormatting += Grid_CellFormatting;

            return grid;
        }

        private void SetupNotesColumns()
        {
            dgvNotes.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreatedAt", HeaderText = "CREATED", FillWeight = 15 });
            dgvNotes.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "NoteType", HeaderText = "TYPE", FillWeight = 10 });
            dgvNotes.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreatedByName", HeaderText = "BY", FillWeight = 15 });
            dgvNotes.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "NoteText", HeaderText = "NOTE", FillWeight = 60 });
        }

        private void SetupHistoryColumns()
        {
            dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ChangedAt", HeaderText = "WHEN", FillWeight = 15 });
            dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FieldName", HeaderText = "FIELD", FillWeight = 15 });
            dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "OldValue", HeaderText = "OLD", FillWeight = 15 });
            dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "NewValue", HeaderText = "NEW", FillWeight = 15 });
            dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ChangedByName", HeaderText = "BY", FillWeight = 15 });
            dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Note", HeaderText = "NOTE", FillWeight = 25 });
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var grid = (DataGridView)sender;
            var prop = grid.Columns[e.ColumnIndex].DataPropertyName;

            if ((prop == "CreatedAt" || prop == "ChangedAt") && e.Value is DateTime dt)
            {
                var utc = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                e.Value = utc.ToLocalTime().ToString("g"); // Short date + short time
                e.FormattingApplied = true;
            }
        }

        private async Task LoadAsync()
        {
            lblLoadStatus.Text = "Loading details...";
            try
            {
                var ticket = await _repo.GetTicketByIdAsync(_ticketId);
                if (ticket == null)
                {
                    lblHeaderTitle.Text = "Ticket Not Found";
                    lblLoadStatus.Text = "Ticket ID invalid or removed.";
                    return;
                }

                _loadedTicket = ticket;

                // Header
                lblHeaderTitle.Text = ticket.TicketCode ?? $"Ticket #{ticket.TicketId}";
                lblHeaderSubtitle.Text = $"ID: {ticket.TicketId} | Branch: {ticket.Branch ?? "Head Office"}";
                
                // Status Badge Logic
                lblStatusBadge.Text = (ticket.Status ?? "Unknown").ToUpper();
                lblStatusBadge.BackColor = GetStatusColor(ticket.Status);
                // Adjust badge position to be to the right of the title text
                using (var g = lblHeaderTitle.CreateGraphics())
                {
                    var size = g.MeasureString(lblHeaderTitle.Text, lblHeaderTitle.Font);
                    lblStatusBadge.Location = new Point(lblHeaderTitle.Location.X + (int)size.Width + 15, lblHeaderTitle.Location.Y - 2);
                }

                // Info Columns
                lblCallerName.Text = ticket.CallerName ?? "-";
                lblDeptName.Text = ticket.Department ?? "-"; // Sub-label

                lblAssigneeName.Text = ticket.ResponsiblePerson ?? "Unassigned";
                lblPriority.Text = (ticket.Priority ?? "-").ToUpper();
                lblPriority.ForeColor = GetPriorityColor(ticket.Priority);

                lblLastContactValue.Text = ticket.LastContactAt.HasValue ? GetTimeAgo(ticket.LastContactAt.Value) : "Never";
                lblCreatedValue.Text = $"Created: {ToLocalString(ticket.CreatedAt)}";

                // Issue
                if (this.Tag is TextBox txtIssueBox)
                {
                    txtIssueBox.Text = ticket.Issue ?? "";
                }

                UpdateActionButtonStates(ticket);

                // Data Grids
                var notes = await _repo.GetTicketNotesAsync(ticket.TicketId, 200);
                dgvNotes.DataSource = (notes ?? new List<CallTicketNoteItem>()).ToList();

                var history = await _repo.GetTicketHistoryAsync(ticket.TicketId, 200);
                dgvHistory.DataSource = (history ?? new List<CallTicketHistoryItem>()).ToList();

                lblLoadStatus.Text = $"Ready. {notes?.Count ?? 0} notes loaded.";
            }
            catch (Exception ex)
            {
                lblLoadStatus.Text = "Error loading ticket details.";
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --- UTILS ---

        private void UpdateActionButtonStates(CallTicketListItem ticket)
        {
            var hasTicket = ticket != null && ticket.TicketId > 0;
            var status = (ticket?.Status ?? string.Empty).Trim();
            var isFinal = status.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Closed", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase);

            if (btnCopyId != null) btnCopyId.Enabled = hasTicket;
            if (btnCopySummary != null) btnCopySummary.Enabled = hasTicket;
            if (btnEmailLog != null) btnEmailLog.Enabled = hasTicket;
            if (btnReassign != null) btnReassign.Enabled = hasTicket && !isFinal;
            if (btnMarkAs != null) btnMarkAs.Enabled = hasTicket && !isFinal;
            if (btnReopen != null)
            {
                btnReopen.Visible = hasTicket && isFinal;
                btnReopen.Enabled = hasTicket && isFinal;
            }
        }

        private void RequestAction(TicketDetailAction action)
        {
            if (_loadedTicket == null || _loadedTicket.TicketId <= 0)
                return;

            RequestedAction = action;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OpenEmailLogShortcut()
        {
            var ticket = _loadedTicket;
            if (ticket == null || ticket.TicketId <= 0)
                return;

            using (var dlg = new EmailLogShortcutDialog(_repo, ticket.TicketId, ticket.TicketCode))
            {
                dlg.ShowDialog(this);
            }
        }

        private void CopyTicketId()
        {
            var ticket = _loadedTicket;
            if (ticket == null)
                return;

            var code = string.IsNullOrWhiteSpace(ticket.TicketCode) ? ticket.TicketId.ToString() : ticket.TicketCode.Trim();
            Clipboard.SetText(code);
            lblLoadStatus.Text = "Copied ticket ID/code to clipboard.";
        }

        private void CopyTicketSummary()
        {
            var ticket = _loadedTicket;
            if (ticket == null)
                return;

            Clipboard.SetText(BuildTicketSummaryText(ticket));
            lblLoadStatus.Text = "Copied ticket summary to clipboard.";
        }

        private string BuildTicketSummaryText(CallTicketListItem ticket)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Ticket: " + (ticket.TicketCode ?? ticket.TicketId.ToString()));
            sb.AppendLine("ID: " + ticket.TicketId);
            sb.AppendLine("Status: " + (ticket.Status ?? "-"));
            sb.AppendLine("Priority: " + (ticket.Priority ?? "-"));
            sb.AppendLine("Caller: " + (ticket.CallerName ?? "-"));
            sb.AppendLine("Department: " + (ticket.Department ?? "-"));
            sb.AppendLine("Branch: " + (ticket.Branch ?? "-"));
            sb.AppendLine("Assigned To: " + (ticket.ResponsiblePerson ?? "(Unassigned)"));
            sb.AppendLine("Created: " + ToLocalString(ticket.CreatedAt));
            if (ticket.LastContactAt.HasValue)
                sb.AppendLine("Last Contact: " + ToLocalString(ticket.LastContactAt.Value));
            sb.AppendLine();
            sb.AppendLine("Issue:");
            sb.AppendLine(ticket.Issue ?? string.Empty);
            if (_txtIssueRender != null && !string.IsNullOrWhiteSpace(_txtIssueRender.Text) && string.IsNullOrWhiteSpace(ticket.Issue))
                sb.AppendLine(_txtIssueRender.Text);
            return sb.ToString();
        }

        private Color GetStatusColor(string status)
        {
            if (string.IsNullOrEmpty(status)) return Color.Gray;
            switch (status.ToLower())
            {
                case "pending": return Color.FromArgb(52, 152, 219); // Blue
                case "in progress": return Color.FromArgb(155, 89, 182); // Purple
                case "solved": return Color.FromArgb(46, 204, 113); // Green
                case "closed": return Color.FromArgb(39, 174, 96); // Dark Green
                case "overdue": return Color.FromArgb(231, 76, 60); // Red
                case "escalated": return Color.FromArgb(243, 156, 18); // Orange
                case "waiting on department": return Color.FromArgb(22, 160, 133); // Teal
                case "waiting on vendor": return Color.FromArgb(127, 140, 141); // Gray
                default: return Color.Gray;
            }
        }

        private Color GetPriorityColor(string priority)
        {
             if (string.IsNullOrEmpty(priority)) return Color.Gray;
             switch(priority.ToLower())
             {
                 case "critical": return Color.FromArgb(192, 57, 43);
                 case "high": return Color.FromArgb(231, 76, 60);
                 case "medium": return Color.FromArgb(243, 156, 18);
                 case "low": return Color.FromArgb(46, 204, 113);
                 default: return Color.FromArgb(149, 165, 166);
             }
        }

        private static string ToLocalString(DateTime dt)
        {
            return AppTime.ToLocalString(dt, "g");
        }

        private static string GetTimeAgo(DateTime dt)
        {
            var local = AppTime.AssumeUtc(dt).ToLocalTime();
            var diff = DateTime.Now - local;

            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return local.ToString("MMM dd");
        }

        private async Task DeleteTicketAsync()
        {
            if (!AppSession.IsLoggedIn || (!AppSession.IsAdmin && !AppSession.IsDeveloper))
            {
                MessageBox.Show("You don't have permission to delete tickets.", "Delete Ticket", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var msg =
                "This will permanently delete this ticket and its related records (notes, history, email logs, escalation override).\n\n" +
                "This cannot be undone.\n\n" +
                "Delete this ticket?";

            if (MessageBox.Show(msg, "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            var userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
            if (userId == null)
            {
                MessageBox.Show("No current user ID found.", "Delete Ticket", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (this.btnDelete != null) this.btnDelete.Enabled = false;
                if (this.btnClose != null) this.btnClose.Enabled = false;
                this.Cursor = Cursors.WaitCursor;

                var deleted = await _repo.DeleteTicketAsync(_ticketId, userId);
                if (!deleted)
                {
                    MessageBox.Show("Ticket not found or already deleted.", "Delete Ticket", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Delete Ticket Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
                if (this.btnClose != null) this.btnClose.Enabled = true;
                if (this.btnDelete != null) this.btnDelete.Enabled = AppSession.IsLoggedIn && (AppSession.IsAdmin || AppSession.IsDeveloper);
            }
        }
    }

    internal sealed class EmailLogShortcutDialog : Form
    {
        private readonly ICallMonitoringRepository _repo;
        private readonly int _ticketId;
        private readonly string _ticketCode;
        private Wpf.CallMonitoring.WpfEmailNotificationWorkspace _emailWorkspace;

        public EmailLogShortcutDialog(ICallMonitoringRepository repo, int ticketId, string ticketCode)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _ticketId = ticketId;
            _ticketCode = ticketCode;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Ticket Email Log";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = true;
            MaximizeBox = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(245, 247, 250);
            Font = new Font("Segoe UI", 9.5F);
            ClientSize = new Size(1060, 680);
            MinimumSize = new Size(900, 560);

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 54,
                BackColor = Color.White,
                Padding = new Padding(16, 10, 16, 10)
            };
            header.Paint += (_, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(221, 226, 232)))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            var title = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Email Log - " + (string.IsNullOrWhiteSpace(_ticketCode) ? "Ticket #" + _ticketId : _ticketCode.Trim()),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 41, 55),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var close = new Button
            {
                Dock = DockStyle.Right,
                Width = 96,
                Text = "Close",
                BackColor = Color.FromArgb(52, 73, 94),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            close.FlatAppearance.BorderSize = 0;
            close.Click += (_, __) => Close();

            header.Controls.Add(title);
            header.Controls.Add(close);

            var host = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                BackColor = Color.White
            };

            _emailWorkspace = new Wpf.CallMonitoring.WpfEmailNotificationWorkspace(_repo);
            var emailHost = new System.Windows.Forms.Integration.ElementHost
            {
                Dock = DockStyle.Fill,
                Child = _emailWorkspace
            };
            host.Controls.Add(emailHost);

            Controls.Add(host);
            Controls.Add(header);

            Load += (_, __) =>
            {
                _emailWorkspace.NavigateTo(
                    Wpf.CallMonitoring.WpfEmailNotificationDeepLinkTarget.EmailLog,
                    emailLogSearch: _ticketId.ToString());
            };
        }
    }
}
