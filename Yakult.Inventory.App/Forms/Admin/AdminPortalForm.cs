using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Yakult.Inventory.App.Pages.Admin;
using Yakult.Inventory.App.Pages.Admin.AccountManagement;
using Yakult.Inventory.App.Pages.Admin.ReferenceData;
using Yakult.Inventory.App.Pages.Admin.EmailManagement;
using Yakult.Inventory.App.Pages.Admin.Security;
using Yakult.Inventory.App.Forms.Admin.AccountManagement;
using Yakult.Inventory.App.Forms.SystemSettings;
using Yakult.Inventory.App.Pages.User;
using Yakult.Inventory.App.Data;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Forms.Admin
{
    public enum AdminPortalExitAction
    {
        None = 0,
        BackToPortal = 1,
        Logout = 2
    }

    public partial class AdminPortalForm : Form
    {
        private const int MENU_WIDTH = 320;

        private Panel _contentPanel;
        private Panel _menuOverlay;
        private Panel _sideMenuPanel;
        private Button _hamburgerButton;
        private bool _isMenuOpen;

        // Collapsible section state
        private Panel _accountMgmtPanel;
        private Button _accountMgmtButton;
        private bool _isAccountMgmtExpanded;

        private Panel _emailSmtpPanel;
        private Button _emailSmtpButton;
        private bool _isEmailSmtpExpanded;

        private Panel _referenceDataPanel;
        private Button _referenceDataButton;
        private bool _isReferenceDataExpanded;

        // Developer-only section
        private Panel _developerToolsPanel;
        private Button _developerToolsButton;
        private bool _isDeveloperToolsExpanded;

        private Button _backToPortalButton;
        private Button _logoutButton;

        public AdminPortalExitAction ExitAction { get; private set; } = AdminPortalExitAction.None;

        public AdminPortalForm()
        {
            InitializeComponent();
            BuildUi();
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1400, 800);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.Sizable;
            Text = "Admin Portal";
            BackColor = Color.White;
            ResumeLayout(false);
        }

        private void BuildUi()
        {
            Controls.Clear();

            // ── TOP BAR ──────────────────────────────────────────────────────────────
            var topBar = new Panel
            {
                Height = 70,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(78, 154, 252)
            };
            Controls.Add(topBar);

            _hamburgerButton = new Button
            {
                Text = "☰",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                Size = new Size(70, 60),
                Location = new Point(5, 5),
                BackColor = Color.FromArgb(58, 134, 232),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _hamburgerButton.FlatAppearance.BorderSize = 0;
            _hamburgerButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(78, 154, 252);
            _hamburgerButton.Click += HamburgerButton_Click;
            topBar.Controls.Add(_hamburgerButton);

            var logoBox = new PictureBox
            {
                Size = new Size(220, 46),
                Location = new Point(85, 12),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            try
            {
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "yakult_Name.png");
                if (File.Exists(logoPath))
                    logoBox.Image = Image.FromFile(logoPath);
            }
            catch { }
            topBar.Controls.Add(logoBox);

            var titleLabel = new Label
            {
                Text = "Admin Portal",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(315, 14),
                AutoSize = true
            };
            topBar.Controls.Add(titleLabel);


            // ── CONTENT PANEL ────────────────────────────────────────────────────────
            _contentPanel = new Panel
            {
                Location = new Point(0, topBar.Height),
                Size = new Size(ClientSize.Width, ClientSize.Height - topBar.Height),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White
            };
            Controls.Add(_contentPanel);

            // ── MENU OVERLAY (click-outside to close) ────────────────────────────────
            _menuOverlay = new Panel
            {
                Location = new Point(MENU_WIDTH, topBar.Height),
                Size = new Size(ClientSize.Width - MENU_WIDTH, ClientSize.Height - topBar.Height),
                BackColor = Color.Transparent,
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _menuOverlay.Click += MenuOverlay_Click;
            Controls.Add(_menuOverlay);
            Controls.SetChildIndex(_menuOverlay, Controls.Count - 1);

            // ── SIDE MENU PANEL ──────────────────────────────────────────────────────
            _sideMenuPanel = new Panel
            {
                Width = MENU_WIDTH,
                Height = ClientSize.Height - topBar.Height,
                Location = new Point(0, topBar.Height),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
                AutoScroll = true,
                Visible = false
            };

            _sideMenuPanel.HorizontalScroll.Enabled = false;
            _sideMenuPanel.HorizontalScroll.Visible = false;
            _sideMenuPanel.HorizontalScroll.Maximum = 0;
            _sideMenuPanel.VerticalScroll.Enabled = true;
            _sideMenuPanel.VerticalScroll.Visible = false;

            _sideMenuPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(200, 200, 200), 1))
                {
                    e.Graphics.DrawLine(pen, _sideMenuPanel.Width - 1, 0, _sideMenuPanel.Width - 1, _sideMenuPanel.Height);
                }
            };

            PopulateSideMenu();

            Controls.Add(_sideMenuPanel);
            _sideMenuPanel.BringToFront();

            // Load default page after UI is built and shown
            Shown += (s, e) => ShowEmployeeManagementPage();
        }

        private void PopulateSideMenu()
        {
            _sideMenuPanel.Controls.Clear();

            // NOTE: Controls are added in BOTTOM-TO-TOP visual order.
            // With DockStyle.Top, the last control added ends up at the top.
            // Sub-buttons inside sub-panels are also added in reverse (last item first = visually first).

            // ── LOGOUT (very bottom) ─────────────────────────────────────────────────
            _logoutButton = AddMenuButton("🚪 Logout", (s, e) =>
            {
                ExitAction = AdminPortalExitAction.Logout;
                AppSession.Clear();
                Close();
            }, isPrimary: true, backColor: Color.FromArgb(220, 53, 69), foreColor: Color.White);
            _sideMenuPanel.Controls.Add(_logoutButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 10, Dock = DockStyle.Top, BackColor = Color.White });

            // ── BACK TO PORTAL ───────────────────────────────────────────────────────
            _backToPortalButton = AddMenuButton("Back to Portal", (s, e) =>
            {
                ExitAction = AdminPortalExitAction.BackToPortal;
                Close();
            }, isPrimary: true, backColor: Color.FromArgb(52, 152, 219), foreColor: Color.White);
            _sideMenuPanel.Controls.Add(_backToPortalButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── EMAIL & SMTP SUB-PANEL ───────────────────────────────────────────────
            _emailSmtpPanel = new Panel
            {
                Height = 3 * 50,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            // Reverse order — last added appears at top visually
            AddSubMenuButton(_emailSmtpPanel, "Email Logs", (s, e) => { ShowEmailLogsPage(); ToggleMenu(); });
            AddSubMenuButton(_emailSmtpPanel, "Email Configuration", (s, e) => { ShowEmailConfigurationPage(); ToggleMenu(); });
            AddSubMenuButton(_emailSmtpPanel, "SMTP Settings", (s, e) => { ShowSmtpSettingsPage(); ToggleMenu(); });
            _sideMenuPanel.Controls.Add(_emailSmtpPanel);

            // ── EMAIL & SMTP HEADER BUTTON ───────────────────────────────────────────
            _emailSmtpButton = AddCollapsibleSection("✉ Email & SMTP");
            _emailSmtpButton.Click += (s, e) => ToggleEmailSmtpSection();
            _sideMenuPanel.Controls.Add(_emailSmtpButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── REFERENCE DATA SUB-PANEL ─────────────────────────────────────────────
            _referenceDataPanel = new Panel
            {
                // 3 buttons while Master Data Update is hidden (was 4 * 50).
                Height = 3 * 50,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            // Reverse order — last added appears at top visually
            AddSubMenuButton(_referenceDataPanel, "Dept Acronyms", (s, e) => { ShowDepartmentAcronymPage(); ToggleMenu(); });
            AddSubMenuButton(_referenceDataPanel, "Branch Acronyms", (s, e) => { ShowBranchAcronymPage(); ToggleMenu(); });
            AddSubMenuButton(_referenceDataPanel, "Holidays", (s, e) => { ShowHolidayManagementPage(); ToggleMenu(); });
            // Master Data Update is hidden for now. Uncomment this line and set Height back to 4 * 50 to restore it.
            //AddSubMenuButton(_referenceDataPanel, "Master Data Update", (s, e) => { ShowMasterDataUpdatePage(); ToggleMenu(); });
            _sideMenuPanel.Controls.Add(_referenceDataPanel);

            // ── REFERENCE DATA HEADER BUTTON ─────────────────────────────────────────
            _referenceDataButton = AddCollapsibleSection("📋 Reference Data");
            _referenceDataButton.Click += (s, e) => ToggleReferenceDataSection();
            _sideMenuPanel.Controls.Add(_referenceDataButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── ACCOUNT MANAGEMENT SUB-PANEL ────────────────────────────────────────
            _accountMgmtPanel = new Panel
            {
                // Account Management (user accounts) is Super Admin only, so one less button otherwise.
                Height = (CanOpenUserAccountManagement ? 6 : 5) * 50,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            // Reverse order — last added appears at top visually
            AddSubMenuButton(_accountMgmtPanel, "User Positions", (s, e) => { ShowUserPositionsPage(); ToggleMenu(); });
            AddSubMenuButton(_accountMgmtPanel, "User Activity", (s, e) => { ShowUserActivityPage(); ToggleMenu(); });
            AddSubMenuButton(_accountMgmtPanel, "Approver Management", (s, e) => { ShowAccountPermissionsPage(); ToggleMenu(); });
            AddSubMenuButton(_accountMgmtPanel, "Department Accounts", (s, e) => { ShowDepartmentAccountsPage(); ToggleMenu(); });
            if (CanOpenUserAccountManagement)
                AddSubMenuButton(_accountMgmtPanel, "Account Management", (s, e) => { ShowUserAccountManagementPage(); ToggleMenu(); });
            AddSubMenuButton(_accountMgmtPanel, "Employee Management", (s, e) => { ShowEmployeeManagementPage(); ToggleMenu(); });
            _sideMenuPanel.Controls.Add(_accountMgmtPanel);

            // ── ACCOUNT MANAGEMENT HEADER BUTTON ────────────────────────────────────
            _accountMgmtButton = AddCollapsibleSection("👤 Account Management");
            _accountMgmtButton.Click += (s, e) => ToggleAccountMgmtSection();
            _sideMenuPanel.Controls.Add(_accountMgmtButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── MY ACCOUNT ───────────────────────────────────────────────────────────
            var myAccountButton = AddMenuButton("👤 My Account", (s, e) =>
            {
                ShowMyAccountPage();
                ToggleMenu();
            });
            _sideMenuPanel.Controls.Add(myAccountButton);

            // ── CONSUMABLE STOCK MONITOR ─────────────────────────────────────────────
            var stockMonitorButton = AddMenuButton("📊 Consumable Stock Monitor", (s, e) =>
            {
                ShowConsumableStockMonitorPage();
                ToggleMenu();
            });
            _sideMenuPanel.Controls.Add(stockMonitorButton);

            // ── DEVELOPER TOOLS (Developer only) ────────────────────────────────────
            if (AppSession.IsDeveloper)
            {
                _developerToolsPanel = new Panel
                {
                    Height = 3 * 50,
                    Dock = DockStyle.Top,
                    BackColor = Color.FromArgb(250, 250, 250),
                    Visible = false
                };
                // Reverse order — last added appears at top visually
                AddSubMenuButton(_developerToolsPanel, "Portal Access", (s, e) => { ShowPortalAccessPage(); ToggleMenu(); });
                AddSubMenuButton(_developerToolsPanel, "User Portal Access", (s, e) => { ShowUserPortalAccessPage(); ToggleMenu(); });
                AddSubMenuButton(_developerToolsPanel, "Migrate QR Codes to DB", (s, e) => { ShowQrImageBackfillPage(); ToggleMenu(); });
                AddSubMenuButton(_developerToolsPanel, "Migrate Receipts to DB", (s, e) => { ShowReceiptBackfillPage(); ToggleMenu(); });
                _sideMenuPanel.Controls.Add(_developerToolsPanel);

                _developerToolsButton = AddCollapsibleSection("🔐 Developer Tools");
                _developerToolsButton.BackColor = Color.FromArgb(52, 73, 94);
                _developerToolsButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 83, 104);
                _developerToolsButton.Click += (s, e) => ToggleDeveloperToolsSection();
                _sideMenuPanel.Controls.Add(_developerToolsButton);

                _sideMenuPanel.Controls.Add(new Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });
            }

            _sideMenuPanel.Controls.Add(new Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── SEPARATOR ────────────────────────────────────────────────────────────
            _sideMenuPanel.Controls.Add(new Panel
            {
                Height = 2,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(230, 230, 230)
            });

            _sideMenuPanel.Controls.Add(new Panel { Height = 10, Dock = DockStyle.Top, BackColor = Color.White });

            // ── HEADER LABEL (top of menu) ───────────────────────────────────────────
            _sideMenuPanel.Controls.Add(new Label
            {
                Text = "ADMIN PORTAL",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Height = 40,
                AutoSize = false,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0)
            });

            // Top padding spacer — added LAST so it appears at the very top
            _sideMenuPanel.Controls.Add(new Panel { Height = 20, Dock = DockStyle.Top, BackColor = Color.White });
        }

        private Button AddMenuButton(string text, EventHandler onClick, bool isPrimary = false, Color? backColor = null, Color? foreColor = null)
        {
            var button = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 10F, isPrimary ? FontStyle.Bold : FontStyle.Regular),
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = backColor ?? Color.White,
                ForeColor = foreColor ?? Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };

            button.FlatAppearance.BorderSize = 0;
            if (!isPrimary)
            {
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 240, 240);
            }

            button.Click += onClick;
            return button;
        }

        private Button AddCollapsibleSection(string title)
        {
            var button = new Button
            {
                Text = title + "  ▶",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(78, 154, 252),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(98, 174, 255);

            return button;
        }

        private void AddSubMenuButton(Panel parentPanel, string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 9.5F),
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                ForeColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(30, 0, 0, 0)
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 240, 240);

            button.Click += onClick;
            parentPanel.Controls.Add(button);
        }

        private void ToggleAccountMgmtSection()
        {
            _isAccountMgmtExpanded = !_isAccountMgmtExpanded;
            if (_accountMgmtPanel != null)
                _accountMgmtPanel.Visible = _isAccountMgmtExpanded;
            if (_accountMgmtButton != null)
                _accountMgmtButton.Text = "👤 Account Management  " + (_isAccountMgmtExpanded ? "▼" : "▶");
        }

        private void ToggleEmailSmtpSection()
        {
            _isEmailSmtpExpanded = !_isEmailSmtpExpanded;
            if (_emailSmtpPanel != null)
                _emailSmtpPanel.Visible = _isEmailSmtpExpanded;
            if (_emailSmtpButton != null)
                _emailSmtpButton.Text = "✉ Email & SMTP  " + (_isEmailSmtpExpanded ? "▼" : "▶");
        }

        private void ToggleReferenceDataSection()
        {
            _isReferenceDataExpanded = !_isReferenceDataExpanded;
            if (_referenceDataPanel != null)
                _referenceDataPanel.Visible = _isReferenceDataExpanded;
            if (_referenceDataButton != null)
                _referenceDataButton.Text = "📋 Reference Data  " + (_isReferenceDataExpanded ? "▼" : "▶");
        }

        private void ToggleDeveloperToolsSection()
        {
            _isDeveloperToolsExpanded = !_isDeveloperToolsExpanded;
            if (_developerToolsPanel != null)
                _developerToolsPanel.Visible = _isDeveloperToolsExpanded;
            if (_developerToolsButton != null)
                _developerToolsButton.Text = "🔐 Developer Tools  " + (_isDeveloperToolsExpanded ? "▼" : "▶");
        }

        private void HamburgerButton_Click(object sender, EventArgs e)
        {
            ToggleMenu();
        }

        private void ToggleMenu()
        {
            _isMenuOpen = !_isMenuOpen;

            if (_isMenuOpen)
            {
                _menuOverlay.Visible = true;
                _menuOverlay.BringToFront();
                _contentPanel.BringToFront();

                _sideMenuPanel.Visible = true;
                _sideMenuPanel.BringToFront();
            }
            else
            {
                _sideMenuPanel.Visible = false;
                _menuOverlay.Visible = false;
            }
        }

        private void MenuOverlay_Click(object sender, EventArgs e)
        {
            if (_isMenuOpen)
                ToggleMenu();
        }

        private void ShowPage(UserControl page)
        {
            foreach (Control c in _contentPanel.Controls) c.Dispose();
            _contentPanel.Controls.Clear();

            page.Dock = DockStyle.Fill;
            _contentPanel.Controls.Add(page);
        }

        // ── PAGE SHOW METHODS ────────────────────────────────────────────────────────

        private void ShowEmployeeManagementPage()
        {
            ShowPage(new EmployeeManagementWpfHost());
        }

        /// <summary>
        /// Super Admin only, and not restricted on User Access > Pages
        /// (PermissionItem 'UserAccountManagementPage').
        /// </summary>
        private static bool CanOpenUserAccountManagement =>
            AppSession.IsSuperAdmin &&
            Yakult.Inventory.App.Security.PermissionResolver.HasPageAccess("UserAccountManagementPage");

        private void ShowUserAccountManagementPage()
        {
            if (!CanOpenUserAccountManagement)
            {
                MessageBox.Show("Access denied. Super Admin privileges required.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new UserAccountManagementWpfHost());
        }

        private void ShowDepartmentAccountsPage()
        {
            ShowPage(new DepartmentAccountsWpfHost());
        }

        private void ShowAccountPermissionsPage()
        {
            ShowPage(new ApproverManagementWpfHost());
        }

        private void ShowUserActivityPage()
        {
            ShowPage(new UserActivityWpfHost());
        }

        private void ShowUserPositionsPage()
        {
            ShowPage(new UserPositionsWpfHost());
        }

        private void ShowSmtpSettingsPage()
        {
            ShowPage(new Yakult.Inventory.App.Forms.Admin.EmailManagement.SmtpSendSettingsWpfHost());
        }

        private void ShowEmailConfigurationPage()
        {
            var win = new Yakult.Inventory.App.WPF.Admin.EmailManagement.Views.EmailConfigurationWindow();
            new System.Windows.Interop.WindowInteropHelper(win).Owner = this.Handle;
            win.ShowDialog();
        }

        private void ShowEmailLogsPage()
        {
            ShowPage(new Yakult.Inventory.App.Forms.Admin.EmailManagement.EmailLogsWpfHost());
        }

        private void ShowMyAccountPage()
        {
            ShowPage(new MyAccountPage());
        }

        private void ShowConsumableStockMonitorPage()
        {
            ShowPage(new ConsumableStockMonitorWpfHost());
        }

        private void ShowBranchAcronymPage()
        {
            ShowPage(new Yakult.Inventory.App.Forms.Admin.ReferenceData.AcronymWpfHost(
                Yakult.Inventory.App.WPF.Admin.ReferenceData.ViewModels.AcronymKind.Branch));
        }

        private void ShowDepartmentAcronymPage()
        {
            ShowPage(new Yakult.Inventory.App.Forms.Admin.ReferenceData.AcronymWpfHost(
                Yakult.Inventory.App.WPF.Admin.ReferenceData.ViewModels.AcronymKind.Department));
        }

        private void ShowHolidayManagementPage()
        {
            ShowPage(new Yakult.Inventory.App.Forms.Admin.ReferenceData.HolidayManagementWpfHost());
        }

        private void ShowMasterDataUpdatePage()
        {
            ShowPage(new MasterDataUpdatePage());
        }

        private void ShowPortalAccessPage()
        {
            if (!AppSession.IsDeveloper)
            {
                MessageBox.Show("Access denied. Developer privileges required.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new PortalAccessPage());
        }

        private void ShowUserPortalAccessPage()
        {
            if (!AppSession.IsAdmin)
            {
                MessageBox.Show("Access denied. Admin privileges required.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new Yakult.Inventory.App.Forms.Admin.UserAccess.UserAccessWpfHost());
        }

        private void ShowQrImageBackfillPage()
        {
            if (!AppSession.IsDeveloper)
            {
                MessageBox.Show("Access denied. Developer privileges required.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new QrImageBackfillPage());
        }

        private void ShowReceiptBackfillPage()
        {
            if (!AppSession.IsDeveloper)
            {
                MessageBox.Show("Access denied. Developer privileges required.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new ReceiptBackfillPage());
        }

    }
}
