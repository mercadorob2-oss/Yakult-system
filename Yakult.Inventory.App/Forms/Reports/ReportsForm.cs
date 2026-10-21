using InventoryTheme = Yakult.Inventory.App.Helpers.UiTheme;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.IO;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Security;

namespace Yakult.Inventory.App.Forms.Reports
{
    public partial class ReportsForm : Form
    {
        public enum ReportsExitAction
        {
            None = 0,
            BackToPortal = 1,
            Logout = 2
        }

        public ReportsExitAction ExitAction { get; private set; } = ReportsExitAction.None;

        // ── Top bar / shared controls ────────────────────────────────────────────
        private Panel topBar;
        private Panel contentPanel;

        // ── Side-menu controls (mirrors CartridgeManagementPortalForm) ───────────
        private const int MENU_WIDTH = 320;
        private const int SideMenuItemHeight = 42;

        private Panel _menuOverlay;
        private Panel _sideMenuPanel;
        private Button _hamburgerButton;
        private Button _activeNavButton;
        private bool _isMenuOpen;

        public ReportsForm()
        {
            InitializeComponent();          // owned by ReportsForm.Designer.cs
            BuildUi();
        } 

        // ReportsForm_Load is wired up in the designer file (this.Load += ReportsForm_Load).
        private void ReportsForm_Load(object sender, EventArgs e)
        {
            ShowViewInvoicePage();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  UI CONSTRUCTION
        // ════════════════════════════════════════════════════════════════════════

        private void BuildUi()
        {
            Controls.Clear();

            // ── Top bar — shares the workspace chrome with MainForm and the sibling
            //    portals (Cartridge Management / Request & Set Management): deep-navy
            //    surface, white logo badge, and a hairline bottom edge.
            topBar = new Panel
            {
                Height = 70,
                Dock = DockStyle.Top,
                BackColor = InventoryTheme.Colors.NavigationBack
            };
            topBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(InventoryTheme.Colors.NavigationHover, 1))
                    e.Graphics.DrawLine(pen, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
            };
            Controls.Add(topBar);

            // Hamburger button
            _hamburgerButton = new Button
            {
                Text = "☰",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                Size = new Size(70, 60),
                Location = new Point(5, 5),
                BackColor = InventoryTheme.Colors.NavigationBack,
                ForeColor = InventoryTheme.Colors.RailText,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _hamburgerButton.FlatAppearance.BorderSize = 0;
            _hamburgerButton.FlatAppearance.MouseOverBackColor = InventoryTheme.Colors.NavigationHover;
            _hamburgerButton.Click += HamburgerButton_Click;
            topBar.Controls.Add(_hamburgerButton);

            // Brand logo — white rounded badge so the red wordmark stays legible on navy
            var logoBadge = new Panel
            {
                Size = new Size(220, 46),
                Location = new Point(85, 12),
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8)
            };
            ApplyRoundedRegion(logoBadge, 8);

            var logoBox = new PictureBox
            {
                Dock = DockStyle.Fill,
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
            logoBadge.Controls.Add(logoBox);
            topBar.Controls.Add(logoBadge);

            // Title — overline + subtitle, matching the workspace header
            var titleHost = new Panel
            {
                Location = new Point(315, 0),
                Size = new Size(560, 70),
                BackColor = Color.Transparent
            };
            titleHost.Controls.Add(new Label
            {
                Text = "REPORT PORTAL",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(0, 17),
                AutoSize = true
            });
            titleHost.Controls.Add(new Label
            {
                Text = "Report monitoring system",
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColorTranslator.FromHtml("#B8C3D4"),
                Location = new Point(1, 36),
                AutoSize = true
            });
            topBar.Controls.Add(titleHost);

            // Signed-in identity on the right, mirroring the workspace header's account area
            var userName = string.IsNullOrWhiteSpace(AppSession.CurrentUserName)
                ? AppSession.CurrentEmployeeName
                : AppSession.CurrentUserName;
            if (!string.IsNullOrWhiteSpace(userName))
            {
                topBar.Controls.Add(new Label
                {
                    Text = "Signed in as " + userName.Trim(),
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = ColorTranslator.FromHtml("#B8C3D4"),
                    TextAlign = ContentAlignment.MiddleRight,
                    AutoSize = false,
                    Size = new Size(280, 70),
                    Location = new Point(ClientSize.Width - 300, 0),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                });
            }

            // ── Content panel ────────────────────────────────────────────────────
            contentPanel = new Panel
            {
                Location = new Point(0, topBar.Height),
                Size = new Size(ClientSize.Width, ClientSize.Height - topBar.Height),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = InventoryTheme.Colors.HeaderBack
            };
            Controls.Add(contentPanel);
            topBar.BringToFront();

            Resize += (s, e) =>
            {
                contentPanel.Size = new Size(ClientSize.Width, ClientSize.Height - topBar.Height);
            };

            // ── Overlay (closes menu on outside click) ───────────────────────────
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

            // ── Side-menu panel — same rail as MainForm's hamburger menu ─────────
            _sideMenuPanel = new Panel
            {
                Width = MENU_WIDTH,
                Height = ClientSize.Height - topBar.Height,
                Location = new Point(0, topBar.Height),
                BackColor = InventoryTheme.Colors.RailBack,
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
                using (var pen = new Pen(InventoryTheme.Colors.RailLine, 1))
                    e.Graphics.DrawLine(pen,
                        _sideMenuPanel.Width - 1, 0,
                        _sideMenuPanel.Width - 1, _sideMenuPanel.Height);
            };

            PopulateSideMenu();

            Controls.Add(_sideMenuPanel);
            _sideMenuPanel.BringToFront();
        }

        /// <summary>Clips a fixed-size control to a rounded rectangle (logo badge).</summary>
        private static void ApplyRoundedRegion(Control control, int radius)
        {
            if (control.Width <= 0 || control.Height <= 0)
                return;

            using (var path = new GraphicsPath())
            {
                int d = radius * 2;
                path.AddArc(0, 0, d, d, 180, 90);
                path.AddArc(control.Width - d, 0, d, d, 270, 90);
                path.AddArc(control.Width - d, control.Height - d, d, d, 0, 90);
                path.AddArc(0, control.Height - d, d, d, 90, 90);
                path.CloseFigure();
                control.Region = new Region(path);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  SIDE MENU
        //  Controls are added BOTTOM-TO-TOP: with DockStyle.Top, the control added
        //  last renders at the top (same convention as MainForm / sibling portals).
        // ════════════════════════════════════════════════════════════════════════

        private void PopulateSideMenu()
        {
            _sideMenuPanel.Controls.Clear();
            _activeNavButton = null;

            // ── LOGOUT (very bottom) — quiet rail row with the workspace's soft-red text ──
            _sideMenuPanel.Controls.Add(AddMenuButton("🚪 Logout", (s, e) =>
            {
                ExitAction = ReportsExitAction.Logout;
                AppSession.Clear();
                Close();
            }, isPrimary: true, foreColor: InventoryTheme.Colors.RailDanger));

            _sideMenuPanel.Controls.Add(Spacer(10));

            // ── BACK TO PORTAL — quiet rail row ──────────────────────────────────
            _sideMenuPanel.Controls.Add(AddMenuButton("←  Back to Portal", (s, e) =>
            {
                ExitAction = ReportsExitAction.BackToPortal;
                Close();
            }));

            _sideMenuPanel.Controls.Add(Spacer(8));

            // ── REPORT LINKS (added bottom-to-top) ───────────────────────────────
            AddNavItem("Cartridge Disposed/Sold", ShowCartridgeDisposedSoldPage);
            AddRailSeparator();

            AddNavItem("E-Documents", ShowEDocumentsPage);
            AddRailSeparator();

            AddNavItem("View Renewals (Grouped)", ShowViewRenewalsGroupedPage);
            AddRailSeparator();

            AddNavItem("View Renewals", ShowViewRenewalsPage);
            AddRailSeparator();

            AddNavItem("View Sets", ShowViewSetsPage);
            AddRailSeparator();

            var invoiceButton = AddNavItem("View Invoice", ShowViewInvoicePage);

            _sideMenuPanel.Controls.Add(Spacer(10));

            // ── SEPARATOR ────────────────────────────────────────────────────────
            _sideMenuPanel.Controls.Add(new Panel
            {
                Height = 1,
                Dock = DockStyle.Top,
                BackColor = InventoryTheme.Colors.RailLine
            });

            // ── HEADER LABEL — muted micro-caps, like the workspace rail ─────────
            _sideMenuPanel.Controls.Add(new Label
            {
                Text = "REPORT MONITORING",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = InventoryTheme.Colors.RailLabel,
                Height = 34,
                AutoSize = false,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            });

            // Top padding spacer — added LAST so it appears at the very top
            _sideMenuPanel.Controls.Add(Spacer(20));

            // The form opens on View Invoice (see ReportsForm_Load)
            SetActiveNav(invoiceButton);
        }

        private Button AddNavItem(string text, Func<bool> showPage)
        {
            Button button = null;
            button = AddMenuButton(text, (s, e) =>
            {
                if (showPage())
                    SetActiveNav(button);
                ToggleMenu();
            });
            _sideMenuPanel.Controls.Add(button);
            return button;
        }

        /// <summary>Highlights the report the user is currently viewing.</summary>
        private void SetActiveNav(Button button)
        {
            if (_activeNavButton != null && !_activeNavButton.IsDisposed)
            {
                _activeNavButton.BackColor = InventoryTheme.Colors.RailBack;
                _activeNavButton.ForeColor = InventoryTheme.Colors.RailText;
            }

            _activeNavButton = button;

            if (_activeNavButton != null && !_activeNavButton.IsDisposed)
            {
                _activeNavButton.BackColor = InventoryTheme.Colors.RailHover;
                _activeNavButton.ForeColor = Color.White;
            }
        }

        private void AddRailSeparator()
        {
            _sideMenuPanel.Controls.Add(new Panel
            {
                Height = 1,
                Dock = DockStyle.Top,
                BackColor = InventoryTheme.Colors.RailLine
            });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  MENU BUTTON HELPERS  (exact pattern from CartridgeManagementPortalForm)
        // ════════════════════════════════════════════════════════════════════════

        private Button AddMenuButton(string text, EventHandler onClick,
            bool isPrimary = false, Color? backColor = null, Color? foreColor = null)
        {
            var button = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 10F, isPrimary ? FontStyle.Bold : FontStyle.Regular),
                Height = SideMenuItemHeight,
                Dock = DockStyle.Top,
                BackColor = backColor ?? InventoryTheme.Colors.RailBack,
                ForeColor = foreColor ?? InventoryTheme.Colors.RailText,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = InventoryTheme.Colors.RailHover;
            button.FlatAppearance.MouseDownBackColor = InventoryTheme.Colors.RailHover;

            if (onClick != null)
                button.Click += onClick;

            return button;
        }

        /// <summary>Creates a spacer panel with DockStyle.Top on the rail surface.</summary>
        private static Panel Spacer(int height) =>
            new Panel { Height = height, Dock = DockStyle.Top, BackColor = InventoryTheme.Colors.RailBack };

        // ════════════════════════════════════════════════════════════════════════
        //  MENU TOGGLE LOGIC  (exact copy from CartridgeManagementPortalForm)
        // ════════════════════════════════════════════════════════════════════════

        private void HamburgerButton_Click(object sender, EventArgs e) => ToggleMenu();

        private void ToggleMenu()
        {
            _isMenuOpen = !_isMenuOpen;

            if (_isMenuOpen)
            {
                _menuOverlay.Visible = true;
                _menuOverlay.BringToFront();
                contentPanel.BringToFront();

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

        // ════════════════════════════════════════════════════════════════════════
        //  PAGE NAVIGATION
        // ════════════════════════════════════════════════════════════════════════

        private bool ShowPage(UserControl page)
        {
            contentPanel.Controls.Clear();
            page.Dock = DockStyle.Fill;
            contentPanel.Controls.Add(page);
            return true;
        }

        private bool ShowViewInvoicePage()
        {
            if (!PermissionResolver.HasPageAccess("ViewInvoicePage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return ShowPage(new Yakult.Inventory.App.Pages.Invoice.ViewInvoiceReportPage());
        }

        private bool ShowViewSetsPage()
        {
            if (!PermissionResolver.HasPageAccess("ViewSetsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return ShowPage(new Yakult.Inventory.App.Pages.Set.ViewSetPage());
        }

        private bool ShowViewRenewalsPage()
        {
            if (!PermissionResolver.HasPageAccess("ViewRenewalPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return ShowPage(new Yakult.Inventory.App.Pages.Renewal.ViewRenewalPage());
        }

        private bool ShowViewRenewalsGroupedPage()
        {
            if (!PermissionResolver.HasPageAccess("ViewRenewalGroupPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return ShowPage(new Yakult.Inventory.App.Pages.Renewal.ViewRenewalGroupPage());
        }

        private bool ShowExportPage()
        {
            if (!PermissionResolver.HasPageAccess("ExportReportPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return ShowPage(new Yakult.Inventory.App.Pages.Export.ReportPickerPage());
        }

        private bool ShowCartridgeDisposedSoldPage()
        {
            if (!PermissionResolver.HasPageAccess("OutboundBatchesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return ShowPage(new Yakult.Inventory.App.Pages.Cartridge.OutboundBatchesPage());
        }

        private bool ShowEDocumentsPage()
        {
            if (!PermissionResolver.HasPageAccess("EDocumentsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return ShowPage(new Yakult.Inventory.App.Pages.Reports.EDocumentsPage());
        }
    }
}
