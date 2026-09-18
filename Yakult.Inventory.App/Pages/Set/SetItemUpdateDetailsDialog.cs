using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Repositories;
using WinPanel = System.Windows.Forms.Panel;

namespace Yakult.Inventory.App.Pages.Set
{
    public sealed class SetItemUpdateDetailsDialog : Form
    {
        private readonly SetItemUpdateDto _update;
        private readonly SetItemUpdateRepository _repository;
        private System.Windows.Forms.Button _btnClose;
        private WinPanel resizeGrip;

        // Colors
        private readonly Color ClrTextMain = Color.FromArgb(45, 55, 72);
        private readonly Color ClrTextLabel = Color.FromArgb(100, 116, 139);
        private readonly Color ClrBorder = Color.FromArgb(235, 238, 240);
        private readonly Color ClrPrimary = Color.FromArgb(52, 152, 219);
        private readonly Color ClrSuccess = Color.FromArgb(39, 174, 96);
        private readonly Color ClrDanger = Color.FromArgb(220, 53, 69);
        private readonly Color ClrSecondaryBg = Color.FromArgb(245, 247, 250);
        private readonly Color ClrTimelineBg = Color.FromArgb(250, 252, 254);

        private List<SetItemUpdateDto> _fullHistory;
        private Task _loadHistoryTask;

        public SetItemUpdateDetailsDialog(SetItemUpdateDto update)
        {
            _update = update ?? throw new ArgumentNullException(nameof(update));
            _repository = new SetItemUpdateRepository();
            KeyPreview = true;
            DoubleBuffered = true;
            _loadHistoryTask = LoadHistoryAsync();

            BuildUi();
        }

        private async Task LoadHistoryAsync()
        {
             if (string.IsNullOrWhiteSpace(_update.SerialNumber))
             {
                 _fullHistory = new List<SetItemUpdateDto>();
                 return;
             }
             try
             {
                 var history = await _repository.GetHistoryBySerialAsync(_update.SerialNumber, top: 200);
                 _fullHistory = (history ?? new List<SetItemUpdateDto>())
                     .OrderByDescending(x => x.CreatedAt)
                     .ToList();
             }
             catch
             {
                 _fullHistory = new List<SetItemUpdateDto>();
             }
        }

        private async Task EnsureHistoryLoadedAsync()
        {
            if (_loadHistoryTask == null)
                _loadHistoryTask = LoadHistoryAsync();
            try { await _loadHistoryTask; } catch { }
        }

        private void BuildUi()
        {
            Text = $"Update Details - {_update.SerialNumber}";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(600, 750);
            MinimumSize = new Size(500, 600);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.White;

            // Border container
            var root = new WinPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(2) };
            root.Paint += (s, e) => {
                using (var p = new Pen(ClrBorder, 2))
                    e.Graphics.DrawRectangle(p, 0, 0, root.Width - 1, root.Height - 1);
            };
            Controls.Add(root);

            // Header
            var header = new WinPanel { Dock = DockStyle.Top, Height = 60, BackColor = Color.White, Padding = new Padding(24, 0, 24, 0) };
            header.Paint += (s, e) => {
                using (var pen = new Pen(ClrBorder))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            header.MouseDown += DragWindow;

            var lblTitle = new Label
            {
                Text = "Update Details",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = ClrTextMain,
                AutoSize = true,
                Location = new Point(24, 16)
            };

            _btnClose = CreateFlatButton("Close", ClrPrimary, Color.White);
            _btnClose.Size = new Size(80, 32);
            _btnClose.Location = new Point(root.Width - 104, 14);
            _btnClose.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _btnClose.Click += (s, e) => Close();

            header.Controls.Add(lblTitle);
            header.Controls.Add(_btnClose);
            root.Controls.Add(header);

            // Body
            var body = new WinPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24) };
            root.Controls.Add(body);

            // 1. Transaction Header
            var sectionTrans = CreateSectionPanel(80);
            var statusColor = _update.Processed ? ClrSuccess : ClrDanger;
            var badgeStatus = CreateBadge(_update.NewStatus ?? "Unknown", statusColor);
            badgeStatus.Location = new Point(0, 0);

            var lblDate = new Label
            {
                Text = _update.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = ClrTextMain,
                AutoSize = true,
                Location = new Point(badgeStatus.Right + 12, -2)
            };

            var lblUpdatedByLabel = new Label { Text = "Updated By", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = ClrTextLabel, AutoSize = true, Location = new Point(0, 40) };
            var lblUpdatedByValue = new Label { Text = NullToDash(_update.UpdatedByName), Font = new Font("Segoe UI", 11F, FontStyle.Regular), ForeColor = ClrTextMain, AutoSize = true, Location = new Point(0, 56) };

             var lblProcessedLabel = new Label { Text = "Processed", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = ClrTextLabel, AutoSize = true, Location = new Point(150, 40) };
            var lblProcessedValue = new Label { Text = _update.Processed ? "Yes" : "No", Font = new Font("Segoe UI", 11F, FontStyle.Regular), ForeColor = ClrTextMain, AutoSize = true, Location = new Point(150, 56) };

            sectionTrans.Controls.AddRange(new Control[] { badgeStatus, lblDate, lblUpdatedByLabel, lblUpdatedByValue, lblProcessedLabel, lblProcessedValue });
            body.Controls.Add(sectionTrans);
            sectionTrans.BringToFront();

            // 2. Item Information
            var sectionItem = CreateGroup("Item Information");
            AddRow(sectionItem, "Serial Number", _update.SerialNumber, "Set Code", _update.SetCode);
            AddRow(sectionItem, "Item Type", _update.ItemType, "Model", _update.ModelNumber);
            body.Controls.Add(sectionItem);
            sectionItem.BringToFront();

            // 3. Location & Status
            var sectionLoc = CreateGroup("Location & Status");
            AddRow(sectionLoc, "Branch", _update.BranchName, "Department", _update.DepartmentName);
            AddRow(sectionLoc, "Previous Status", _update.PreviousStatus, "New Status", _update.NewStatus);
            body.Controls.Add(sectionLoc);
            sectionLoc.BringToFront();

            // 4. Update History Timeline
            var sectionTimeline = CreateCollapsibleTimeline();
            body.Controls.Add(sectionTimeline);
            sectionTimeline.BringToFront();

            // 5. Remarks
            var sectionNotes = new WinPanel { Dock = DockStyle.Top, Height = 180, Padding = new Padding(0, 20, 0, 10) };
            var pnlNotesHeader = new WinPanel { Dock = DockStyle.Top, Height = 30 };
            var lblNotes = new Label { Text = "Remark", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = ClrPrimary, AutoSize = true, Location = new Point(0,0) };
            var btnCopy = CreateFlatButton("Copy", ClrSecondaryBg, ClrTextMain);
            btnCopy.Size = new Size(60, 24);
            btnCopy.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            btnCopy.Location = new Point(0, 0);
            btnCopy.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCopy.Click += (s, e) => Clipboard.SetText(_update.Remark ?? string.Empty);
            
            pnlNotesHeader.Controls.Add(btnCopy);
            pnlNotesHeader.Controls.Add(lblNotes);
            sectionNotes.Controls.Add(pnlNotesHeader);

            var txtNotes = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                Text = _update.Remark ?? string.Empty,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None
            };
            sectionNotes.Controls.Add(txtNotes);
            body.Controls.Add(sectionNotes);
            sectionNotes.BringToFront();

            // Resize Grip
            resizeGrip = new WinPanel { Size = new Size(16, 16), BackColor = Color.Transparent, Cursor = Cursors.SizeNWSE, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            resizeGrip.Location = new Point(root.Width - 16, root.Height - 16);
            resizeGrip.MouseDown += ResizeGrip_MouseDown;
            resizeGrip.MouseMove += ResizeGrip_MouseMove;
            resizeGrip.Paint += (s, e) => {
                using(var p = new Pen(Color.LightGray, 2)) {
                    e.Graphics.DrawLine(p, 4, 12, 12, 4);
                    e.Graphics.DrawLine(p, 8, 12, 12, 8);
                }
            };
            root.Controls.Add(resizeGrip);
            root.Resize += (s, e) => {
                resizeGrip.Location = new Point(root.Width - 16, root.Height - 16);
                _btnClose.Left = root.Width - 104;
                ApplyRoundedCorners();
            };

            ApplyRoundedCorners();
        }

        // --- Helpers ---
        private System.Windows.Forms.Button CreateFlatButton(string text, Color bg, Color fg)
        {
            var btn = new System.Windows.Forms.Button
            {
                Text = text, BackColor = bg, ForeColor = fg, FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand, UseVisualStyleBackColor = false, Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private WinPanel CreateSectionPanel(int height) => new WinPanel { Dock = DockStyle.Top, Height = height, Padding = new Padding(0, 0, 0, 10), BackColor = Color.Transparent };
        
        private WinPanel CreateGroup(string title)
        {
            var p = new WinPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 20, 0, 10) };
            var lbl = new Label { Text = title, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = ClrPrimary, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 10) };
            p.Controls.Add(lbl);
            return p;
        }

        private void AddRow(WinPanel parent, string l1, string v1, string l2, string v2)
        {
            var row = new WinPanel { Dock = DockStyle.Top, Height = 50 };
            var lbl1 = new Label { Text = l1, Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = ClrTextLabel, AutoSize = true, Location = new Point(0, 0) };
            var val1 = new Label { Text = NullToDash(v1), Font = new Font("Segoe UI", 10F, FontStyle.Regular), ForeColor = ClrTextMain, AutoSize = true, Location = new Point(0, 20), MaximumSize = new Size(240, 0) };
            var lbl2 = new Label { Text = l2, Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = ClrTextLabel, AutoSize = true, Location = new Point(280, 0) };
            var val2 = new Label { Text = NullToDash(v2), Font = new Font("Segoe UI", 10F, FontStyle.Regular), ForeColor = ClrTextMain, AutoSize = true, Location = new Point(280, 20), MaximumSize = new Size(240, 0) };
            row.Controls.AddRange(new Control[] { lbl1, val1, lbl2, val2 });
            parent.Controls.Add(row); row.BringToFront();
        }

        private Label CreateBadge(string text, Color bg)
        {
            var lbl = new Label { Text = text, BackColor = bg, ForeColor = Color.White, Font = new Font("Segoe UI", 8F, FontStyle.Bold), AutoSize = true, Padding = new Padding(6, 4, 6, 4) };
            lbl.Resize += (s, e) => MakePill(lbl);
            MakePill(lbl);
            return lbl;
        }

        private void MakePill(Control c)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            using (var path = new GraphicsPath()) {
                path.AddArc(0, 0, c.Height, c.Height, 90, 180);
                path.AddArc(c.Width - c.Height, 0, c.Height, c.Height, 270, 180);
                path.CloseAllFigures();
                c.Region = new Region(path);
            }
        }

        private string NullToDash(string s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();
        private void ApplyRoundedCorners()
        {
            int r = 16;
            using (var p = new GraphicsPath()) {
                p.AddArc(0, 0, r, r, 180, 90); p.AddArc(Width - r, 0, r, r, 270, 90);
                p.AddArc(Width - r, Height - r, r, r, 0, 90); p.AddArc(0, Height - r, r, r, 90, 90);
                p.CloseFigure(); Region = new Region(p);
            }
        }

        // Drag & Resize
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        private void DragWindow(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); } }
        private bool _resizing; private Point _resizeOriginMouse; private Size _resizeOriginSize;
        private void ResizeGrip_MouseDown(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _resizing = true; _resizeOriginMouse = Cursor.Position; _resizeOriginSize = Size; } }
        private void ResizeGrip_MouseMove(object sender, MouseEventArgs e) {
            if (_resizing && e.Button == MouseButtons.Left) {
                var delta = new Size(Cursor.Position.X - _resizeOriginMouse.X, Cursor.Position.Y - _resizeOriginMouse.Y);
                Size = new Size(Math.Max(MinimumSize.Width, _resizeOriginSize.Width + delta.Width), Math.Max(MinimumSize.Height, _resizeOriginSize.Height + delta.Height));
            }
        }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _resizing = false; }
        protected override void OnResize(EventArgs e) { base.OnResize(e); ApplyRoundedCorners(); }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) { if (keyData == Keys.Escape) { Close(); return true; } return base.ProcessCmdKey(ref msg, keyData); }

        // Timeline
        private WinPanel CreateCollapsibleTimeline()
        {
            var container = new WinPanel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(0, 20, 0, 10) };
            var header = new WinPanel { Dock = DockStyle.Top, Height = 40, Cursor = Cursors.Hand };
            var lblTitle = new Label { Text = "Update History", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = ClrPrimary, AutoSize = true, Location = new Point(0, 12) };
            var btnToggle = new Label { Text = "▶ Show", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = ClrTextLabel, AutoSize = true, Location = new Point(120, 14) };
            header.Controls.AddRange(new Control[] { lblTitle, btnToggle });
            container.Controls.Add(header);

            var timelineContent = new WinPanel { Dock = DockStyle.Top, Height = 0, BackColor = ClrTimelineBg, Padding = new Padding(20, 10, 20, 20), AutoScroll = true, Visible = false };
            var loadingPanel = new WinPanel { Dock = DockStyle.Fill };
            loadingPanel.Controls.Add(new Label { Text = "Loading...", Font = new Font("Segoe UI", 9F, FontStyle.Italic), ForeColor = ClrTextLabel, AutoSize = true, Location = new Point(20, 20) });
            timelineContent.Controls.Add(loadingPanel);
            container.Controls.Add(timelineContent);

            bool isExpanded = false;
            Action toggle = () => {
                isExpanded = !isExpanded;
                if (isExpanded) {
                    timelineContent.Visible = true; timelineContent.Height = 260; btnToggle.Text = "▼ Hide";
                    _ = RenderTimelineAsync(timelineContent, loadingPanel);
                    container.Height = header.Height + timelineContent.Height + container.Padding.Vertical;
                } else {
                    timelineContent.Height = 0; timelineContent.Visible = false; btnToggle.Text = "▶ Show";
                    container.Height = header.Height + container.Padding.Vertical;
                }
            };
            header.Click += (s, e) => toggle(); lblTitle.Click += (s, e) => toggle(); btnToggle.Click += (s, e) => toggle();
            return container;
        }

        private async Task RenderTimelineAsync(WinPanel content, WinPanel loading)
        {
            if (content.Controls.Count == 1 && content.Controls[0] == loading) {
                await EnsureHistoryLoadedAsync();
                content.Controls.Clear();
                if (_fullHistory == null || _fullHistory.Count == 0) {
                     content.Controls.Add(new Label { Text = "No history available", Font = new Font("Segoe UI", 9F, FontStyle.Italic), AutoSize = true, Location = new Point(20, 20) });
                     return;
                }
                foreach (var item in _fullHistory) {
                    var p = new WinPanel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(0, 5, 0, 5) };
                    var dot = new Label { Text = "●", ForeColor = item.Processed ? ClrSuccess : ClrDanger, Font = new Font("Segoe UI", 14F), AutoSize = true, Location = new Point(20, 10) };
                    var time = new Label { Text = item.CreatedAt.ToString("MMM dd HH:mm"), Font = new Font("Segoe UI", 8F), ForeColor = ClrTextLabel, AutoSize = true, Location = new Point(50, 8) };
                    var stat = new Label { Text = $"{item.PreviousStatus} → {item.NewStatus}", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = ClrTextMain, AutoSize = true, Location = new Point(50, 26) };
                    p.Controls.AddRange(new Control[] { dot, time, stat });
                    content.Controls.Add(p); p.BringToFront();
                }
            }
        }
    }
}
