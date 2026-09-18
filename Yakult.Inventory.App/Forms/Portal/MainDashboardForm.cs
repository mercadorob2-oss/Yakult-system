using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using Yakult.Inventory.App.Forms.CallMonitoring;
using Yakult.Inventory.App.Pages.RequestPortal;
using Yakult.Inventory.App.Pages.User;
using Yakult.Inventory.App.Security;

namespace Yakult.Inventory.App.Forms.Portal
{
    public partial class MainDashboardForm : Form
    {
        // UI Components
        private Panel pnlHeader;
        private PictureBox picBrand;
        private Label lblBrand;
        private Label lblWelcome;
        private Button btnLogout;
        private Button btnAccount;

        private Panel pnlCenter;
        private FlowLayoutPanel flpModules;

        private Panel cardInventory;
        private Panel cardCallMonitoring;
        private Panel cardReports;
        private Panel cardRequesterPortal;
        private Panel cardCartridgeManagement;
        private Panel cardBorrowItems;
        private Panel cardAdminPortal;

        private RequesterPortalForm _requesterPortalInstance;


        public PortalSelection SelectedModule { get; private set; } = PortalSelection.None;

        public MainDashboardForm()
        {
            InitializeComponent();
            this.Load += MainDashboardForm_Load;
            this.KeyDown += MainDashboardForm_KeyDown;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // 1. FORM SETTINGS
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1100, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Yakult Internal Systems";
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.FromArgb(245, 247, 250); // Light Gray-Blue background
            this.KeyPreview = true;

            // 2. HEADER (Gradient)
            this.pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                Padding = new Padding(40, 0, 40, 0)
            };
            this.pnlHeader.Paint += PnlHeader_Paint; // Custom Gradient

            this.btnLogout = new Button
            {
                Text = "Logout",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(192, 57, 43),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 36),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            this.btnLogout.FlatAppearance.BorderSize = 0;
            this.btnLogout.Click += BtnLogout_Click;

            this.btnAccount = new Button
            {
                Text = "Account",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(39, 174, 96),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 36),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            this.btnAccount.FlatAppearance.BorderSize = 0;
            this.btnAccount.Click += BtnAccount_Click;

            this.lblBrand = new Label
            {
                Text = "INTERNAL PORTAL",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 2)
            };

            this.lblWelcome = new Label
            {
                Text = "Welcome back, User",
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = Color.FromArgb(230, 230, 230),
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };

            this.picBrand = new PictureBox
            {
                Size = new Size(240, 56),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 20, 16, 20)
            };
            TryLoadBrandImage();

            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var textStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 22, 0, 0)
            };
            textStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            textStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            textStack.Controls.Add(this.lblBrand, 0, 0);
            textStack.Controls.Add(this.lblWelcome, 0, 1);

            this.btnAccount.Margin = new Padding(0, 32, 8, 0);
            this.btnLogout.Margin = new Padding(0, 32, 0, 0);

            headerLayout.Controls.Add(this.picBrand, 0, 0);
            headerLayout.Controls.Add(textStack, 1, 0);
            headerLayout.Controls.Add(this.btnAccount, 2, 0);
            headerLayout.Controls.Add(this.btnLogout, 3, 0);

            this.pnlHeader.Controls.Add(headerLayout);


            // 3. CENTER CONTENT (Modules) - 2x2 Grid Layout (Windows 11 style)
            this.pnlCenter = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 40, 0, 0),
                AutoScroll = true
            };
            
            // Use TableLayoutPanel for 2-column grid layout
            var gridLayout = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            gridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            gridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            gridLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            gridLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            gridLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            
            // Keep flpModules for centering logic but use gridLayout inside
            this.flpModules = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Anchor = AnchorStyles.Top,
                BackColor = Color.Transparent
            };
            
            // --- CARDS (2x2 Grid Order) ---
            // Top Row: Inventory System, IT Call Monitoring
            // Bottom Row: Request/s Portal, Cartridge Management
            
            this.cardInventory = CreateModuleCard(
                "Inventory System", 
                "Manage stock levels, requests, and analyze movement data.", 
                "📦", 
                Color.FromArgb(52, 152, 219), 
                BtnInventory_Click);

            this.cardCallMonitoring = CreateModuleCard(
                "IT Call Monitoring",
                "Track helpdesk tickets, escalations, and issue resolution.",
                "🛠",
                Color.FromArgb(0, 150, 136),
                BtnCallMonitoring_Click);

            this.cardRequesterPortal = CreateModuleCard(
                "Request/s Portal",
                "Quick access to submit and track cartridge requests.",
                "📋",
                Color.FromArgb(155, 89, 182),
                BtnRequesterPortal_Click);

            this.cardCartridgeManagement = CreateModuleCard(
                "Cartridge Management",
                "Manage cartridge exchange, refills, and fulfillment.", 
                "🖨",
                Color.FromArgb(230, 126, 34),
                BtnCartridgeManagement_Click);

            this.cardBorrowItems = CreateModuleCard(
                "Borrow Items",
                "Log borrowed hardware by serial number and track returns.",
                "\U0001F501",
                Color.FromArgb(231, 76, 60),
                BtnBorrowItems_Click);

            // Reports card (opens Reports portal)
            this.cardReports = CreateModuleCard(
                "Reports",
                "Generate and view reports.",
                "\U0001F4CA",
                Color.FromArgb(149, 165, 166),
                BtnReports_Click);

            // Admin Portal card (Developer / SuperAdmin only)
            this.cardAdminPortal = CreateModuleCard(
                "Admin Portal",
                "Manage user accounts, roles, SMTP settings, and email configuration.",
                "⚙",
                Color.FromArgb(219, 112, 147),
                BtnAdminPortal_Click);

            // Add cards to grid: Row 0 = top, Row 1 = bottom
            gridLayout.Controls.Add(this.cardInventory, 0, 0);
            gridLayout.Controls.Add(this.cardCallMonitoring, 1, 0);
            gridLayout.Controls.Add(this.cardCartridgeManagement, 0, 1);
            gridLayout.Controls.Add(this.cardBorrowItems, 1, 1);
            gridLayout.Controls.Add(this.cardReports, 0, 2);
            
            this.flpModules.Controls.Add(gridLayout);
            this.pnlCenter.Controls.Add(this.flpModules);


            // Assembly header/center
            this.Controls.Add(this.pnlCenter); // Fill first inside
            this.Controls.Add(this.pnlHeader); // Top

            this.pnlCenter.Resize += (s, e) => CenterModules();

            this.ResumeLayout(false);
        }

        // --- DRAWING & EVENTS ---

        private void TryLoadBrandImage()
        {
            if (this.picBrand == null) return;

            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var candidates = new[]
                {
                    Path.Combine(baseDir, "Images", "yakult_Name.png"),
                    Path.Combine(baseDir, "images", "yakult_Name.png"),
                    Path.Combine(baseDir, "yakult_Name.png")
                };

                foreach (var path in candidates)
                {
                    if (!File.Exists(path)) continue;
                    this.picBrand.Image = LoadImageUnlocked(path);
                    this.picBrand.Visible = true;
                    return;
                }
            }
            catch
            {
                // Non-fatal: hide logo if anything goes wrong.
            }

            this.picBrand.Visible = false;
        }

        private static Image LoadImageUnlocked(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var img = Image.FromStream(fs))
            {
                return (Image)img.Clone();
            }
        }

        private void PnlHeader_Paint(object sender, PaintEventArgs e)
        {
            // Gradient Background using Brand Colors
            using (var brush = new LinearGradientBrush(
                this.pnlHeader.ClientRectangle,
                Color.FromArgb(44, 62, 80),    // Dark Blue
                Color.FromArgb(52, 152, 219),  // Yakult Blue-ish
                LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(brush, this.pnlHeader.ClientRectangle);
            }
        }

        private void CenterModules()
        {
            if (this.flpModules != null && this.pnlCenter != null)
            {
                this.flpModules.Left = (this.pnlCenter.Width - this.flpModules.Width) / 2;
                // Keep top margin
            }
        }

        private Panel CreateModuleCard(string title, string desc, string icon, Color accent, EventHandler onClick)
        {
            var card = new Panel
            {
                Size = new Size(320, 200),
                BackColor = Color.White,
                Margin = new Padding(20)
            };

            // Accent bar
            var bar = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = accent };

            // Title label (child control — no overlap with icon)
            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(64, 64, 64),
                Location = new Point(20, 30),
                AutoSize = false,
                Size = new Size(270, 28),
                BackColor = Color.Transparent
            };

            // "Click to Open →" label (child control — left-aligned, no overlap with icon)
            var lblAction = new Label
            {
                Text = onClick == null ? "Coming soon" : "Click to Open →",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = onClick == null ? Color.Gray : accent,
                Location = new Point(20, 150),
                AutoSize = true,
                BackColor = Color.Transparent
            };

            // Draw desc text and watermark icon directly on the card surface via Paint,
            // so no child-control white rectangle obscures the icon.
            var descFont = new Font("Segoe UI", 10F, FontStyle.Regular);
            var iconFont = new Font("Segoe UI Emoji", 46F, FontStyle.Regular);
            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // Description text
                using (var brush = new SolidBrush(Color.Gray))
                {
                    var fmt = new StringFormat { Trimming = StringTrimming.Word, FormatFlags = StringFormatFlags.NoClip };
                    g.DrawString(desc, descFont, brush, new RectangleF(22, 70, 260, 74), fmt);
                }

                // Watermark icon — shifted left slightly so it stays within card bounds
                using (var brush = new SolidBrush(Color.FromArgb(220, 220, 220)))
                {
                    g.DrawString(icon, iconFont, brush, new PointF(210, 100));
                }
            };

            // Wire events on card and its child controls
            card.Controls.Add(bar);
            card.Controls.Add(lblTitle);
            card.Controls.Add(lblAction);

            if (onClick != null)
            {
                card.Cursor = Cursors.Hand;
                card.Click += onClick;
                foreach (Control c in card.Controls)
                {
                    c.Click += onClick;
                    c.MouseEnter += (s, e) => { card.BackColor = Color.FromArgb(248, 250, 252); card.BorderStyle = BorderStyle.FixedSingle; card.Invalidate(); };
                    c.MouseLeave += (s, e) => { card.BackColor = Color.White; card.BorderStyle = BorderStyle.None; card.Invalidate(); };
                }
                card.MouseEnter += (s, e) => { card.BackColor = Color.FromArgb(248, 250, 252); card.BorderStyle = BorderStyle.FixedSingle; card.Invalidate(); };
                card.MouseLeave += (s, e) => { card.BackColor = Color.White; card.BorderStyle = BorderStyle.None; card.Invalidate(); };
            }
            else
            {
                card.Cursor = Cursors.Default;
            }

            return card;
        }

        private void MainDashboardForm_Load(object sender, EventArgs e)
        {
            try
            {
                var name = Yakult.Inventory.App.Session.AppSession.CurrentUserName;
                this.lblWelcome.Text = string.IsNullOrWhiteSpace(name) ? "Welcome Guest" : $"Welcome back, {name}";
            }
            catch { }
            
            // Apply role-based portal visibility
            ApplyPortalVisibility();
            
            // Trigger initial layout
            CenterModules();
        }

        private void MainDashboardForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) BtnLogout_Click(this, EventArgs.Empty);
            if (e.Alt && e.KeyCode == Keys.I) BtnInventory_Click(this, EventArgs.Empty);
            if (e.Alt && e.KeyCode == Keys.C) BtnCallMonitoring_Click(this, EventArgs.Empty);
            if (e.Alt && e.KeyCode == Keys.B)
            {
                if (CanAccessBorrowItems())
                    BtnBorrowItems_Click(this, EventArgs.Empty);
                else
                    MessageBox.Show("You do not have access to Borrow Items.", "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (e.Alt && e.KeyCode == Keys.M) BtnCartridgeManagement_Click(this, EventArgs.Empty);
            if (e.Alt && e.KeyCode == Keys.R) BtnReports_Click(this, EventArgs.Empty);


            // Ctrl+Shift+D: Developer-only database configuration reset
            if (e.Control && e.Shift && e.KeyCode == Keys.D)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (IsDeveloperEnvironment())
                {
                    HandleDeveloperDatabaseReset();
                }
            }
        }

        #region Developer-Only Database Reset

        /// <summary>
        /// A machine is a developer environment when appsettings.Development.json is present
        /// next to the executable. Production deployments exclude that file.
        /// </summary>
        private static bool IsDeveloperEnvironment()
        {
            var devConfig = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "appsettings.Development.json");
            return System.IO.File.Exists(devConfig);
        }

        private void HandleDeveloperDatabaseReset()
        {
            using (var form = new Yakult.Inventory.App.Pages.Admin.DBConn.DatabaseSetupForm())
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    Application.Restart();
                    Environment.Exit(0);
                }
            }
        }

        #endregion

        // --- ACTIONS ---

        private void BtnInventory_Click(object sender, EventArgs e)
        {
            SelectedModule = PortalSelection.InventorySystem;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnCallMonitoring_Click(object sender, EventArgs e)
        {
            SelectedModule = PortalSelection.ITCallMonitoring;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnRequesterPortal_Click(object sender, EventArgs e)
        {
            // If already open, just bring it to front
            if (_requesterPortalInstance != null && !_requesterPortalInstance.IsDisposed)
            {
                _requesterPortalInstance.BringToFront();
                if (_requesterPortalInstance.WindowState == FormWindowState.Minimized)
                    _requesterPortalInstance.WindowState = FormWindowState.Maximized;
                return;
            }

            _requesterPortalInstance = new RequesterPortalForm();
            _requesterPortalInstance.FormClosed += (s, args) =>
            {
                _requesterPortalInstance.Dispose();
                _requesterPortalInstance = null;
            };
            _requesterPortalInstance.Show();
        }

        private void BtnCartridgeManagement_Click(object sender, EventArgs e)
        {
            SelectedModule = PortalSelection.CartridgeManagement;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnBorrowItems_Click(object sender, EventArgs e)
        {
            if (!CanAccessBorrowItems())
            {
                MessageBox.Show("You do not have access to Borrow Items.", "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SelectedModule = PortalSelection.BorrowItems;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private static bool CanAccessBorrowItems()
        {
            try
            {
                var roles = Yakult.Inventory.App.Session.AppSession.CurrentUserRoles;
                return PermissionResolver.HasPortalAccess(roles, PermissionResolver.Portal.BorrowItems);
            }
            catch
            {
                return false;
            }
        }

        private void BtnReports_Click(object sender, EventArgs e)
        {
            SelectedModule = PortalSelection.Reports;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnAdminPortal_Click(object sender, EventArgs e)
        {
            SelectedModule = PortalSelection.AdminPortal;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnLogout_Click(object sender, EventArgs e)
        {
            // Close any open non-modal windows tied to this session
            if (_requesterPortalInstance != null && !_requesterPortalInstance.IsDisposed)
            {
                _requesterPortalInstance.Close();
                _requesterPortalInstance = null;
            }

            SelectedModule = PortalSelection.Logout;
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void BtnAccount_Click(object sender, EventArgs e)
        {
            using (var dlg = new Form
            {
                Text = "Account Settings",
                Size = new Size(880, 660),
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                FormBorderStyle = FormBorderStyle.FixedDialog
            })
            {
                var page = new MyAccountPage { Dock = DockStyle.Fill };
                dlg.Controls.Add(page);
                dlg.ShowDialog(this);
            }
        }

        private void ApplyPortalVisibility()
        {
            var userRoles = Yakult.Inventory.App.Session.AppSession.CurrentUserRoles;
            
            // Check portal access using PermissionResolver
            bool canAccessInventory = PermissionResolver.HasPortalAccess(
                userRoles, PermissionResolver.Portal.InventorySystem);
            bool canAccessCallIT = PermissionResolver.HasPortalAccess(
                userRoles, PermissionResolver.Portal.CallITMonitoring);
            bool canAccessRequester = PermissionResolver.HasPortalAccess(
                userRoles, PermissionResolver.Portal.RequesterPortal);
            bool canAccessCartridgeMgmt = PermissionResolver.HasPortalAccess(
                userRoles, PermissionResolver.Portal.CartridgeManagement);
            bool canAccessBorrowItems = PermissionResolver.HasPortalAccess(
                userRoles, PermissionResolver.Portal.BorrowItems);
            bool canAccessReports = PermissionResolver.HasPortalAccess(
                userRoles, PermissionResolver.Portal.Reports);

            // Show/hide portal cards based on permissions
            if (this.cardInventory != null)
            {
                this.cardInventory.Visible = canAccessInventory;
            }
            
            if (this.cardCallMonitoring != null)
            {
                this.cardCallMonitoring.Visible = canAccessCallIT;
            }
            
            if (this.cardRequesterPortal != null)
            {
                this.cardRequesterPortal.Visible = canAccessRequester;
                this.cardRequesterPortal.Enabled = canAccessRequester;
            }
            
            if (this.cardCartridgeManagement != null)
            {
                this.cardCartridgeManagement.Visible = canAccessCartridgeMgmt;
            }

            if (this.cardBorrowItems != null)
            {
                this.cardBorrowItems.Visible = canAccessBorrowItems;
            }

            if (this.cardReports != null)
            {
                this.cardReports.Visible = canAccessReports;
            }

            bool canAccessAdminPortal = PermissionResolver.HasPortalAccess(userRoles, PermissionResolver.Portal.AdminPortal);

            if (this.cardAdminPortal != null)
            {
                this.cardAdminPortal.Visible = canAccessAdminPortal;
            }

            // Rebuild the grid layout based on visible cards
            // Find the grid layout inside flpModules
            TableLayoutPanel gridLayout = null;
            foreach (Control c in this.flpModules.Controls)
            {
                if (c is TableLayoutPanel tlp)
                {
                    gridLayout = tlp;
                    break;
                }
            }

            if (gridLayout != null)
            {
                gridLayout.Controls.Clear();
                
                // Collect visible cards
                var visibleCards = new List<Panel>();
                if (canAccessInventory && this.cardInventory != null) visibleCards.Add(this.cardInventory);
                if (canAccessCallIT && this.cardCallMonitoring != null) visibleCards.Add(this.cardCallMonitoring);
                if (canAccessRequester && this.cardRequesterPortal != null) visibleCards.Add(this.cardRequesterPortal);
                if (canAccessCartridgeMgmt && this.cardCartridgeManagement != null) visibleCards.Add(this.cardCartridgeManagement);
                if (canAccessBorrowItems && this.cardBorrowItems != null) visibleCards.Add(this.cardBorrowItems);
                if (canAccessReports && this.cardReports != null) visibleCards.Add(this.cardReports);
                if (canAccessAdminPortal && this.cardAdminPortal != null) visibleCards.Add(this.cardAdminPortal);

                // Ensure grid rows exist for any number of cards.
                var neededRows = Math.Max(1, (visibleCards.Count + 1) / 2);
                gridLayout.RowStyles.Clear();
                gridLayout.RowCount = neededRows;
                for (int r = 0; r < neededRows; r++)
                {
                    gridLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                }

                // Add visible cards to grid in 2-column layout
                for (int i = 0; i < visibleCards.Count; i++)
                {
                    int col = i % 2;
                    int row = i / 2;
                    gridLayout.Controls.Add(visibleCards[i], col, row);
                }
            }
        }
    }

    public enum PortalSelection
    {
        None = 0,
        InventorySystem = 1,
        ITCallMonitoring = 2,
        RequesterPortal = 3,
        Logout = 4,
        CartridgeManagement = 5,
        BorrowItems = 6,
        Reports = 7,
        AdminPortal = 8,
        RepairTechnicianPortal = 9
    }
}
