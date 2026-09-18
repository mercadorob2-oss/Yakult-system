using System;
using System.Drawing;
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

        private Panel _menuOverlay;
        private Panel _sideMenuPanel;
        private Button _hamburgerButton;
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

            // ── Top bar ──────────────────────────────────────────────────────────
            topBar = new Panel
            {
                Height = 70,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(78, 154, 252)
            };
            Controls.Add(topBar);

            // Hamburger button
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

            // Brand logo
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

            // Title
            var titleLabel = new Label
            {
                Text = "Report Monitoring System",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(315, 14),
                AutoSize = true
            };
            topBar.Controls.Add(titleLabel);

            // ── Content panel ────────────────────────────────────────────────────
            contentPanel = new Panel
            {
                Location = new Point(0, topBar.Height),
                Size = new Size(ClientSize.Width, ClientSize.Height - topBar.Height),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(245, 247, 250)
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

            // ── Side-menu panel ──────────────────────────────────────────────────
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
                    e.Graphics.DrawLine(pen,
                        _sideMenuPanel.Width - 1, 0,
                        _sideMenuPanel.Width - 1, _sideMenuPanel.Height);
            };

            PopulateSideMenu();

            Controls.Add(_sideMenuPanel);
            _sideMenuPanel.BringToFront();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  SIDE MENU
        //  NOTE: Controls are added in BOTTOM-TO-TOP visual order.
        //        With DockStyle.Top, the last control added ends up at the top.
        // ════════════════════════════════════════════════════════════════════════

        private void PopulateSideMenu()
        {
            _sideMenuPanel.Controls.Clear();

            // ── LOGOUT (very bottom) ──────────────────────────────────────────────
            _sideMenuPanel.Controls.Add(AddMenuButton("Logout", (s, e) =>
            {
                ExitAction = ReportsExitAction.Logout;
                AppSession.Clear();
                Close();
            }, isPrimary: true, backColor: Color.FromArgb(220, 53, 69), foreColor: Color.White));

            _sideMenuPanel.Controls.Add(Spacer(10));

            // ── BACK TO PORTAL ─────────────────────────────────────────────────────────
            _sideMenuPanel.Controls.Add(AddMenuButton("Back to Portal", (s, e) =>
            {
                ExitAction = ReportsExitAction.BackToPortal;
                Close();
            }, isPrimary: true, backColor: Color.FromArgb(52, 152, 219), foreColor: Color.White));

            _sideMenuPanel.Controls.Add(Spacer(5));

            // ── EXPORT ────────────────────────────────────────
            // Export menu entry hidden per request — page code kept below for later re-enable.
            // _sideMenuPanel.Controls.Add(AddMenuButton("Export", (s, e) =>
            // {
            //     ShowExportPage();
            //     ToggleMenu();
            // }));
            //
            // _sideMenuPanel.Controls.Add(Spacer(5));

            // ── CARTRIDGE DISPOSED / SOLD ────────────────────────────────────────
            _sideMenuPanel.Controls.Add(AddMenuButton("Cartridge Disposed/Sold", (s, e) =>
            {
                ShowCartridgeDisposedSoldPage();
                ToggleMenu();
            }));

            _sideMenuPanel.Controls.Add(Spacer(5));

            // ── E-DOCUMENTS ──────────────────────────────────────────────────
            _sideMenuPanel.Controls.Add(AddMenuButton("E-Documents", (s, e) =>
            {
                ShowEDocumentsPage();
                ToggleMenu();
            }));

            _sideMenuPanel.Controls.Add(Spacer(5));

            // ── VIEW RENEWALS (GROUPED) ──────────────────────────────────────────
            _sideMenuPanel.Controls.Add(AddMenuButton("View Renewals (Grouped)", (s, e) =>
            {
                ShowViewRenewalsGroupedPage();
                ToggleMenu();
            }));

            _sideMenuPanel.Controls.Add(Spacer(5));

            // ── VIEW RENEWALS ────────────────────────────────────────────────────
            _sideMenuPanel.Controls.Add(AddMenuButton("View Renewals", (s, e) =>
            {
                ShowViewRenewalsPage();
                ToggleMenu();
            }));

            _sideMenuPanel.Controls.Add(Spacer(5));

            // ── VIEW SETS ────────────────────────────────────────────────────────
            _sideMenuPanel.Controls.Add(AddMenuButton("View Sets", (s, e) =>
            {
                ShowViewSetsPage();
                ToggleMenu();
            }));

            _sideMenuPanel.Controls.Add(Spacer(5));

            // ── VIEW INVOICE ─────────────────────────────────────────────────────
            _sideMenuPanel.Controls.Add(AddMenuButton("View Invoice", (s, e) =>
            {
                ShowViewInvoicePage();
                ToggleMenu();
            }));

            _sideMenuPanel.Controls.Add(Spacer(5));

            // ── SEPARATOR ────────────────────────────────────────────────────────
            _sideMenuPanel.Controls.Add(new Panel
            {
                Height = 2,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(230, 230, 230)
            });

            _sideMenuPanel.Controls.Add(Spacer(10));

            // ── HEADER LABEL ─────────────────────────────────────────────────────
            _sideMenuPanel.Controls.Add(new Label
            {
                Text = "REPORT MONITORING",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Height = 40,
                AutoSize = false,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0)
            });

            // Top padding spacer — added LAST so it appears at the very top
            _sideMenuPanel.Controls.Add(Spacer(20));
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
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 240, 240);

            button.Click += onClick;
            return button;
        }

        /// <summary>Creates a plain white spacer panel with DockStyle.Top.</summary>
        private static Panel Spacer(int height) =>
            new Panel { Height = height, Dock = DockStyle.Top, BackColor = Color.White };

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

        private void ShowPage(UserControl page)
        {
            contentPanel.Controls.Clear();
            page.Dock = DockStyle.Fill;
            contentPanel.Controls.Add(page);
        }

        private void ShowViewInvoicePage()
        {
            if (!PermissionResolver.HasPageAccess("ViewInvoicePage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowPage(new Yakult.Inventory.App.Pages.Invoice.ViewInvoiceReportPage());
        }

        private void ShowViewSetsPage()
        {
            if (!PermissionResolver.HasPageAccess("ViewSetsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowPage(new Yakult.Inventory.App.Pages.Set.ViewSetPage());
        }

        private void ShowViewRenewalsPage()
        {
            if (!PermissionResolver.HasPageAccess("ViewRenewalPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowPage(new Yakult.Inventory.App.Pages.Renewal.ViewRenewalPage());
        }

        private void ShowViewRenewalsGroupedPage()
        {
            if (!PermissionResolver.HasPageAccess("ViewRenewalGroupPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowPage(new Yakult.Inventory.App.Pages.Renewal.ViewRenewalGroupPage());
        }

        private void ShowExportPage()
        {
            if (!PermissionResolver.HasPageAccess("ExportReportPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowPage(new Yakult.Inventory.App.Pages.Export.ReportPickerPage());
        }

        private void ShowCartridgeDisposedSoldPage()
        {
            if (!PermissionResolver.HasPageAccess("OutboundBatchesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowPage(new Yakult.Inventory.App.Pages.Cartridge.OutboundBatchesPage());
        }

        private void ShowEDocumentsPage()
        {
            if (!PermissionResolver.HasPageAccess("EDocumentsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShowPage(new Yakult.Inventory.App.Pages.Reports.EDocumentsPage());
        }
    }
}
