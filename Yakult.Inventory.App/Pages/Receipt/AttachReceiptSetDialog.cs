using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using ReaLTaiizor.Util;

namespace Yakult.Inventory.App.Pages.Receipt
{
    public partial class AttachReceiptSetDialog : Form
    {
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private readonly ReceiptSetRepository _repository;
        private List<ReceiptSetDto> _allSets;
        private List<ReceiptSetDto> _filteredSets;
        private readonly int? _targetSetId;

        public ReceiptSetDto SelectedSet { get; private set; }
        public int? SelectedReceiptSetId => SelectedSet?.ReceiptSetId;

        private System.Windows.Forms.ListBox lbSets;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.Label lblEmpty, btnCloseX;
        private ReaLTaiizor.Controls.HopeButton btnConfirm, btnCancel;
        private System.Windows.Forms.Panel pnlSearchBorder;
        private const string SearchPlaceholder = "Search by Supplier, SI, DR, or PO...";

        private readonly Color PrimaryBlue = Color.FromArgb(41, 128, 185);
        private readonly Color TextMain = Color.FromArgb(44, 62, 80);
        private readonly Color TextMuted = Color.FromArgb(127, 140, 141);

        public AttachReceiptSetDialog(int? setId = null)
        {
            _targetSetId = setId;
            _repository = new ReceiptSetRepository();
            InitializeComponent();
            SetupForm();
            SetupControls();
            LoadData();
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(580, 720);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;
            this.Padding = new Padding(1); // Border space
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 24, 24));
            
            this.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 2))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    // Draw a subtle border inside the rounded region
                    e.Graphics.DrawPath(pen, GetRoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), 24));
                }
            };
        }

        private GraphicsPath GetRoundedRectPath(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, radius, radius, 180, 90);
            path.AddArc(r.Right - radius, r.Y, radius, radius, 270, 90);
            path.AddArc(r.Right - radius, r.Bottom - radius, radius, radius, 0, 90);
            path.AddArc(r.X, r.Bottom - radius, radius, radius, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void SetupControls()
        {
            // Root panel for overall padding
            var pnlRoot = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(25)
            };
            this.Controls.Add(pnlRoot);

            // Container for top controls
            var pnlTop = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 135, // Increased height for better spacing
                BackColor = Color.Transparent,
                Padding = new Padding(0, 0, 0, 15)
            };
            pnlRoot.Controls.Add(pnlTop);

            // Title (Placed inside pnlTop)
            var lblTitle = new Label
            {
                Text = "Attach Receipt Set",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = TextMain,
                AutoSize = false,
                Height = 45,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlTop.Controls.Add(lblTitle);

            // Spacer between title and search
            var titleSpacer = new System.Windows.Forms.Panel { Dock = DockStyle.Top, Height = 10 };
            pnlTop.Controls.Add(titleSpacer);

            // Close Button (X)
            btnCloseX = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TextMuted,
                Size = new Size(30, 30),
                Location = new Point(pnlRoot.Width - 30, 0),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnCloseX.MouseEnter += (s, e) => btnCloseX.ForeColor = Color.IndianRed;
            btnCloseX.MouseLeave += (s, e) => btnCloseX.ForeColor = TextMuted;
            btnCloseX.Click += (s, e) => this.Close();
            pnlRoot.Controls.Add(btnCloseX);
            btnCloseX.BringToFront();

            pnlSearchBorder = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.White,
                Padding = new Padding(12, 11, 40, 11) 
            };
            pnlSearchBorder.Paint += (s, e) =>
            {
                bool isFocused = txtSearch.Focused || (txtSearch.Text != SearchPlaceholder && !string.IsNullOrEmpty(txtSearch.Text));
                using (var pen = new Pen(isFocused ? PrimaryBlue : Color.FromArgb(230, 230, 230), isFocused ? 2 : 1))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    var rect = new Rectangle(0, 0, pnlSearchBorder.Width - 1, pnlSearchBorder.Height - 1);
                    using (var path = GetRoundedRectPath(rect, 8))
                    {
                        e.Graphics.DrawPath(pen, path);
                    }
                }
                
                // Draw Search Icon
                using (var pen = new Pen(Color.FromArgb(180, 180, 180), 2))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    int iconSize = 14;
                    int x = pnlSearchBorder.Width - 30;
                    int y = (pnlSearchBorder.Height - iconSize) / 2;
                    e.Graphics.DrawEllipse(pen, x, y, iconSize - 4, iconSize - 4);
                    e.Graphics.DrawLine(pen, x + iconSize - 6, y + iconSize - 6, x + iconSize, y + iconSize);
                }
            };
            txtSearch = new System.Windows.Forms.TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 11F),
                Text = SearchPlaceholder,
                ForeColor = Color.Gray
            };
            txtSearch.Enter += (s, e) =>
            {
                if (txtSearch.Text == SearchPlaceholder)
                {
                    txtSearch.Text = "";
                    txtSearch.ForeColor = Color.FromArgb(60, 60, 60);
                }
                pnlSearchBorder.Invalidate();
            };
            txtSearch.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    txtSearch.Text = SearchPlaceholder;
                    txtSearch.ForeColor = Color.Gray;
                }
                pnlSearchBorder.Invalidate();
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;

            pnlSearchBorder.Controls.Add(txtSearch);
            pnlTop.Controls.Add(pnlSearchBorder);

            // Drag support
            lblTitle.MouseDown += Title_MouseDown;
            pnlTop.MouseDown += Title_MouseDown;
            pnlRoot.MouseDown += Title_MouseDown;

            // Button Panel (Bottom)
            var pnlButtons = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80, 
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };

            pnlButtons.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(240, 240, 240), 1))
                {
                    e.Graphics.DrawLine(pen, 0, 5, pnlButtons.Width, 5);
                }
            };

            btnConfirm = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "Confirm",
                Size = new Size(150, 40),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                ButtonType = HopeButtonType.Primary,
                PrimaryColor = PrimaryBlue,
                Enabled = false,
                Cursor = Cursors.Hand
            };
            // Manually position Confirm button to the right
            btnConfirm.Location = new Point(pnlButtons.Width - btnConfirm.Width, 20);
            btnConfirm.Click += BtnConfirm_Click;

            btnCancel = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "Cancel",
                Size = new Size(110, 40),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                ButtonType = HopeButtonType.Primary,
                PrimaryColor = Color.FromArgb(108, 117, 125), // Gray color for Cancel
                TextColor = Color.White,
                Cursor = Cursors.Hand
            };
            // Position Delete button to the left of Confirm button
            btnCancel.Location = new Point(btnConfirm.Left - btnCancel.Width - 15, 20);
            btnCancel.Click += (s, e) => this.Close();

            pnlButtons.Controls.Add(btnConfirm);
            pnlButtons.Controls.Add(btnCancel);
            pnlRoot.Controls.Add(pnlButtons);

            // Empty state label
            lblEmpty = new Label
            {
                Text = "No available receipt sets found.",
                Font = new Font("Segoe UI", 11F),
                ForeColor = TextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Visible = false
            };
            pnlRoot.Controls.Add(lblEmpty);

            // ListBox (Center/Fill)
            lbSets = new System.Windows.Forms.ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 92, // Increased for more breathing room
                IntegralHeight = false,
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };
            lbSets.DrawItem += LbSets_DrawItem;
            lbSets.SelectedIndexChanged += LbSets_SelectedIndexChanged;
            lbSets.MouseDown += LbSets_MouseDown;

            pnlRoot.Controls.Add(lbSets);

            // Correct Docking Order (Top and Bottom must be added LAST to Z-Order -> FIRST to Dock)
            // Controls.Add(x) puts x at index 0 (Top of Z).
            // So we Add in this order:
            // 1. Top Panel (will be bottom of Z -> Docks First)
            // 2. Bottom Panel (will be above Top -> Docks Second)
            // 3. Fill List (will be above Bottom -> Docks Last/Fill)
            // 4. Close Button (Floating -> Top of Z)
            
            // Explicit Z-Ordering for Docking
            // We want Docking Priority: Top -> Bottom -> Fill.
            // Docking Priority corresponds to Z-Order: Bottom -> Top (Highest Index -> Lowest Index).
            // So pnlTop must be at the BOTTOM of Z-Order (Highest Index).
            // lbSets must be at the TOP of Z-Order (Lowest Index).
            
            pnlRoot.Controls.Clear();
            // Add in reverse docking priority (Fill -> Bottom -> Top) if Add puts at 0 (Top).
            // Or just Add all then explicit SendToBack.
            
            pnlRoot.Controls.Add(btnCloseX);    // Top Z (Floating)
            pnlRoot.Controls.Add(lbSets);       // Middle Z (Fill)
            pnlRoot.Controls.Add(lblEmpty);     // Middle Z (Fill)
            pnlRoot.Controls.Add(pnlButtons);   // Bottom Z (Dock Bottom)
            pnlRoot.Controls.Add(pnlTop);       // Bottom Z (Dock Top)

            // Explicitly correct Z-Order just to be safe:
            // pnlTop should be Lowest Z (Highest Index) -> Docks First
            pnlTop.SendToBack(); 
            // pnlButtons should be next -> Docks Second
            pnlButtons.SendToBack();
            // lbSets/lblEmpty should be Front -> Docks Last
            lbSets.BringToFront();
            lblEmpty.BringToFront();
            // btnCloseX is Anchor Top-Right, needs to be Top Z
            btnCloseX.BringToFront();

            pnlTop.Controls.Add(lblTitle);
            pnlTop.Controls.Add(titleSpacer);
            pnlTop.Controls.Add(pnlSearchBorder);
            
            // Explicitly order pnlTop children for Docking (Search -> Spacer -> Title)
            // We want Search at TOP visually.
            // So Search must Dock First -> Bottom of Z-Order.
            pnlSearchBorder.SendToBack();
            titleSpacer.SendToBack();
            lblTitle.SendToBack();
            
            btnCloseX.BringToFront();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.Opacity = 0;
            lbSets.MouseDown += LbSets_MouseDown;
            var timer = new Timer { Interval = 10 };
            timer.Tick += (s, ev) =>
            {
                this.Opacity += 0.15;
                if (this.Opacity >= 1)
                {
                    this.Opacity = 1;
                    timer.Stop();
                    timer.Dispose();
                }
            };
            timer.Start();
        }

        private void LbSets_MouseDown(object sender, MouseEventArgs e)
        {
            int index = lbSets.IndexFromPoint(e.Location);
            if (index != ListBox.NoMatches)
            {
                var rect = lbSets.GetItemRectangle(index);
                // "View" button rect must match DrawItem exactly
                // Rect: Right - 100, centered vertically, 75x36
                var viewBtnRect = new Rectangle(rect.Right - 100, rect.Top + (rect.Height - 36) / 2, 75, 36);

                if (viewBtnRect.Contains(e.Location))
                {
                    var set = (ReceiptSetDto)lbSets.Items[index];
                    OpenViewer(set.ReceiptSetId);
                }
            }
        }

        private void OpenViewer(int receiptSetId)
        {
            var dto = _repository.GetByReceiptSetId(receiptSetId);
            if (dto != null)
                Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForReceipt(this, dto);
            else
                Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForNewReceipt(this);

            LoadData(); // Refresh in case changes were made
        }

        private void LbSets_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var set = (ReceiptSetDto)lbSets.Items[e.Index];
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var graphics = e.Graphics;
            var rect = e.Bounds;

            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Background
            Color bgColor = isSelected ? Color.FromArgb(240, 248, 255) : Color.White;

            using (var brush = new SolidBrush(bgColor))
            {
                graphics.FillRectangle(brush, rect);
            }

            // Selection indicator (left vertical pill)
            if (isSelected)
            {
                using (var pillBrush = new SolidBrush(PrimaryBlue))
                {
                    graphics.FillRectangle(pillBrush, rect.X, rect.Y + 12, 4, rect.Height - 24);
                }
            }

            // Border
            using (var pen = new Pen(Color.FromArgb(242, 244, 247), 1))
            {
                graphics.DrawLine(pen, rect.X + 20, rect.Bottom - 1, rect.Right - 20, rect.Bottom - 1);
            }

            var textRect = new Rectangle(rect.X + 25, rect.Y + 15, rect.Width - 130, rect.Height);

            // Supplier Name
            string supplierName = string.IsNullOrWhiteSpace(set.Supplier) ? "Unknown Supplier" : set.Supplier;
            using (var font = new Font("Segoe UI", 11.5F, FontStyle.Bold))
            using (var brush = new SolidBrush(isSelected ? PrimaryBlue : TextMain))
            {
                graphics.DrawString(supplierName, font, brush, textRect.X, textRect.Y);
            }

            // Details Line (SI / DR / PO)
            string details = $"SI: {set.SiNumber ?? "--"}  |  DR: {set.DrNumber ?? "--"}  |  PO: {set.PoNumber ?? "--"}";
            using (var font = new Font("Segoe UI", 9F))
            using (var brush = new SolidBrush(TextMuted))
            {
                graphics.DrawString(details, font, brush, textRect.X, textRect.Y + 30);
            }

            // Date
            using (var font = new Font("Segoe UI", 8F))
            using (var brush = new SolidBrush(Color.FromArgb(180, 190, 200)))
            {
                graphics.DrawString($"Added {set.CreatedAt:MMM dd, yyyy}", font, brush, textRect.X, textRect.Y + 54);
            }

            // Draw "View" button
            var viewBtnRect = new Rectangle(rect.Right - 100, rect.Top + (rect.Height - 36) / 2, 75, 36);
            
            using (var brush = new SolidBrush(Color.FromArgb(245, 247, 250)))
            {
                FillRoundedRect(graphics, brush, viewBtnRect, 10);
            }
            using (var font = new Font("Segoe UI", 9F, FontStyle.Bold))
            using (var brush = new SolidBrush(TextMain))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphics.DrawString("View", font, brush, viewBtnRect, sf);
            }
        }

        private void FillRoundedRect(Graphics g, Brush b, Rectangle r, int radius)
        {
            using (var path = new GraphicsPath())
            {
                path.AddArc(r.X, r.Y, radius, radius, 180, 90);
                path.AddArc(r.Right - radius, r.Y, radius, radius, 270, 90);
                path.AddArc(r.Right - radius, r.Bottom - radius, radius, radius, 0, 90);
                path.AddArc(r.X, r.Bottom - radius, radius, radius, 90, 90);
                path.CloseFigure();
                g.FillPath(b, path);
            }
        }

        private void LoadData()
        {
            try
            {
                if (_targetSetId.HasValue)
                {
                    _allSets = _repository.GetReceiptSetsAvailableForSetMetadata(_targetSetId.Value);
                }
                else
                {
                    _allSets = _repository.GetAllMetadata();
                }
                FilterList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading sets: {ex.Message}");
            }
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            FilterList();
        }

        private void FilterList()
        {
            if (_allSets == null) return;

            string query = txtSearch.Text.Trim().ToLower();
            if (query == SearchPlaceholder.ToLower() || string.IsNullOrEmpty(query))
            {
                _filteredSets = _allSets;
            }
            else
            {
                _filteredSets = _allSets.Where(s =>
                    (s.Supplier?.ToLower().Contains(query) ?? false) ||
                    (s.SiNumber?.ToLower().Contains(query) ?? false) ||
                    (s.DrNumber?.ToLower().Contains(query) ?? false) ||
                    (s.PoNumber?.ToLower().Contains(query) ?? false)
                ).ToList();
            }

            lbSets.DataSource = null;
            lbSets.DataSource = _filteredSets;
            lbSets.DisplayMember = "Supplier";

            lblEmpty.Visible = _filteredSets.Count == 0;
            lbSets.Visible = _filteredSets.Count > 0;
            
            if (_filteredSets.Count == 0)
            {
                btnConfirm.Enabled = false;
            }
        }

        private void LbSets_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnConfirm.Enabled = lbSets.SelectedIndex != -1;
        }

        private void BtnConfirm_Click(object sender, EventArgs e)
        {
            if (lbSets.SelectedItem is ReceiptSetDto set)
            {
                SelectedSet = set;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void Title_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        // InitializeComponent is typically in Designer.cs but keeping it integrated for simplicity in rewrite
        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "AttachReceiptSetDialog";
            this.Text = "Attach Receipt Set";
            this.ResumeLayout(false);
        }
    }
}
