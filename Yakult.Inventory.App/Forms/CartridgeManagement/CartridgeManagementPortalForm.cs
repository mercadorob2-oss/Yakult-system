using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Cartridge;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Security;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Forms.CartridgeManagement
{
    public enum CartridgeManagementExitAction
    {
        None = 0,
        BackToPortal = 1,
        Logout = 2
    }

    public partial class CartridgeManagementPortalForm : Form
    {
        private const int MENU_WIDTH = 320;

        private Panel _contentPanel;
        private Panel _menuOverlay;
        private Panel _sideMenuPanel;
        private Button _hamburgerButton;
        private bool _isMenuOpen;

        private Button _homeButton;
        private Button _userManualButton;
        private Button _itFulfillmentButton;
        private Button _cartridgeRequestsButton;

        private Button _masterDataButton;
        private Panel _masterDataPanel;
        private bool _isMasterDataExpanded;

        private Button _fulfillmentButton;
        private Panel _fulfillmentPanel;
        private bool _isFulfillmentExpanded;

        private Button _historyMonitoringButton;
        private Panel _historyMonitoringPanel;
        private bool _isHistoryMonitoringExpanded;

        private Button _batchOpsButton;
        private Panel _batchOpsPanel;
        private bool _isBatchOpsExpanded;

        private Button _disposalCategoriesButton;
        private Panel _disposalCategoriesPanel;
        private bool _isDisposalCategoriesExpanded;

        private Button _backToPortalButton;
        private Button _logoutButton;

        public CartridgeManagementExitAction ExitAction { get; private set; } = CartridgeManagementExitAction.None;

        public CartridgeManagementPortalForm()
        {
            InitializeComponent();
            BuildUi();

            // A WPF element hosted via ElementHost (e.g. the Fulfilled Cartridges DataGrid's
            // scrollbar thumb or a column-resize drag) can end up holding Win32 mouse capture
            // if the mouse button is released outside this app (Alt+Tab / clicking another
            // window mid-drag). While that stray capture is held, ALL mouse input on this
            // thread — including clicks on the native title-bar buttons — routes to the
            // capturing element instead of being hit-tested normally, making Close/Minimize/
            // Maximize appear dead even though the rest of the app still responds. Releasing
            // capture whenever this form is deactivated or reactivated clears that stuck state.
            Deactivate += (s, e) => { try { System.Windows.Input.Mouse.Capture(null); } catch { } };
            Activated  += (s, e) => { try { System.Windows.Input.Mouse.Capture(null); } catch { } };
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1400, 800);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.Sizable;
            Text = "Cartridge Management Portal";
            BackColor = Color.White;
            ResumeLayout(false);
        }

        private void BuildUi()
        {
            Controls.Clear();

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
                string logoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "yakult_Name.png");
                if (System.IO.File.Exists(logoPath))
                    logoBox.Image = Image.FromFile(logoPath);
            }
            catch { }
            topBar.Controls.Add(logoBox);

            var titleLabel = new Label
            {
                Text = "Cartridge Management System",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(315, 14),
                AutoSize = true
            };
            topBar.Controls.Add(titleLabel);

            _contentPanel = new Panel
            {
                Location = new Point(0, topBar.Height),
                Size = new Size(ClientSize.Width, ClientSize.Height - topBar.Height),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White
            };
            Controls.Add(_contentPanel);

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

            // Scroll/Resize handlers removed: DockStyle.Top handles repositioning automatically.

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
            Shown += (s, e) => ShowItFulfillmentPage();
        }

        private void PopulateSideMenu()
        {
            _sideMenuPanel.Controls.Clear();

            // NOTE: Controls are added in BOTTOM-TO-TOP visual order.
            // With DockStyle.Top, the last control added ends up at the top.
            // Sub-buttons inside sub-panels are also added in reverse (last item first).

            int buttonHeight = 50;

            // ── LOGOUT (very bottom) ─────────────────────────────────────────────────
            _logoutButton = AddMenuButton("Logout", (s, e) =>
            {
                ExitAction = CartridgeManagementExitAction.Logout;
                AppSession.Clear();
                Close();
            }, isPrimary: true, backColor: Color.FromArgb(220, 53, 69), foreColor: Color.White);
            _sideMenuPanel.Controls.Add(_logoutButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 10, Dock = DockStyle.Top, BackColor = Color.White });

            // ── BACK TO PORTAL ───────────────────────────────────────────────────────
            _backToPortalButton = AddMenuButton("Back to Portal", (s, e) =>
            {
                ExitAction = CartridgeManagementExitAction.BackToPortal;
                Close();
            }, isPrimary: true, backColor: Color.FromArgb(52, 152, 219), foreColor: Color.White);
            _sideMenuPanel.Controls.Add(_backToPortalButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 8, Dock = DockStyle.Top, BackColor = Color.FromArgb(245, 245, 245) });

            // ── GROUP 5: DISPOSAL CATEGORIES ────────────────────────────────────────
            _disposalCategoriesPanel = new Panel
            {
                Height = 2 * buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            // Sub-buttons added in reverse visual order (last item first = visually last)
            AddSubMenuButton(_disposalCategoriesPanel, "Damaged Empty Cartridges", (s, e) => { ShowDamagedEmptyCartridgesPage(); ToggleMenu(); });
            AddSubMenuButton(_disposalCategoriesPanel, "Non-Refillable Cartridges", (s, e) => { ShowNonRefillableCartridgesPage(); ToggleMenu(); });
            _sideMenuPanel.Controls.Add(_disposalCategoriesPanel);

            _disposalCategoriesButton = AddCollapsibleSection("Disposal Categories");
            _disposalCategoriesButton.Click += (s, e) => ToggleSection(ref _isDisposalCategoriesExpanded, _disposalCategoriesPanel, _disposalCategoriesButton, "Disposal Categories");
            _sideMenuPanel.Controls.Add(_disposalCategoriesButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 2, Dock = DockStyle.Top, BackColor = Color.FromArgb(235, 235, 235) });

            // ── GROUP 4: BATCH OPERATIONS ────────────────────────────────────────────
            _batchOpsPanel = new Panel
            {
                Height = 2 * buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            AddSubMenuButton(_batchOpsPanel, "Dispose / Sell Cartridges", (s, e) => { ShowOutboundBatchesPage(); ToggleMenu(); });
            AddSubMenuButton(_batchOpsPanel, "Cartridge Refill Batch", (s, e) => { ShowVendorCartridgeRefillPage(); ToggleMenu(); });
            _sideMenuPanel.Controls.Add(_batchOpsPanel);

            _batchOpsButton = AddCollapsibleSection("Batch Operations");
            _batchOpsButton.Click += (s, e) => ToggleSection(ref _isBatchOpsExpanded, _batchOpsPanel, _batchOpsButton, "Batch Operations");
            _sideMenuPanel.Controls.Add(_batchOpsButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 2, Dock = DockStyle.Top, BackColor = Color.FromArgb(235, 235, 235) });

            // ── GROUP 3: CARTRIDGE HISTORY & MONITORING ──────────────────────────────
            _historyMonitoringPanel = new Panel
            {
                Height = 5 * buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            AddSubMenuButton(_historyMonitoringPanel, "Sold Cartridges", (s, e) => { ShowSoldCartridgesPage(); ToggleMenu(); });
            AddSubMenuButton(_historyMonitoringPanel, "Disposed Cartridges", (s, e) => { ShowDisposedCartridgesPage(); ToggleMenu(); });
            AddSubMenuButton(_historyMonitoringPanel, "Cartridge Tracking", (s, e) => { ShowCartridgeTrackingPage(); ToggleMenu(); });
            AddSubMenuButton(_historyMonitoringPanel, "View Cartridge Sets", (s, e) => { ShowViewCartridgeSetsPage(); ToggleMenu(); });
            AddSubMenuButton(_historyMonitoringPanel, "Authorization Monitor", (s, e) => {
                if (!PermissionResolver.HasPageAccess("AuthorizationMonitorPage"))
                {
                    MessageBox.Show("Access denied. You do not have permission to view this page.",
                        "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                ShowPage(new AuthorizationMonitorWpfHost()); ToggleMenu();
            });
            // HIDDEN: Approval Status belongs to the Requester Portal web app, not this WinForms portal.
            // AddSubMenuButton(_historyMonitoringPanel, "Approval Status", (s, e) => { ShowApprovalPendingPage(); ToggleMenu(); });
            _sideMenuPanel.Controls.Add(_historyMonitoringPanel);

            _historyMonitoringButton = AddCollapsibleSection("History && Monitoring");
            _historyMonitoringButton.Click += (s, e) => ToggleSection(ref _isHistoryMonitoringExpanded, _historyMonitoringPanel, _historyMonitoringButton, "History && Monitoring");
            _sideMenuPanel.Controls.Add(_historyMonitoringButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 2, Dock = DockStyle.Top, BackColor = Color.FromArgb(235, 235, 235) });

            // ── GROUP 2: CARTRIDGE FULFILLMENT ───────────────────────────────────────
            _fulfillmentPanel = new Panel
            {
                Height = 5 * buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            AddSubMenuButton(_fulfillmentPanel, "Fulfilled Cartridges", (s, e) => { ShowFulfilledCartridgesPage(); ToggleMenu(); });
            AddSubMenuButton(_fulfillmentPanel, "Partially Fulfilled", (s, e) => { ShowPartiallyFulfilledCartridgePage(); ToggleMenu(); });
            AddSubMenuButton(_fulfillmentPanel, "Unfulfilled Cartridges", (s, e) => { ShowUnfulfilledCartridgeExchangesPage(); ToggleMenu(); });
            AddSubMenuButton(_fulfillmentPanel, "Cartridge Exchange", (s, e) => { ShowItFulfillmentPage(); ToggleMenu(); });
            AddSubMenuButton(_fulfillmentPanel, "Mixed Request Exchange", (s, e) => { ShowMixedRequestExchangePage(); ToggleMenu(); });
            _sideMenuPanel.Controls.Add(_fulfillmentPanel);

            _fulfillmentButton = AddCollapsibleSection("Cartridge Fulfillment");
            _fulfillmentButton.Click += (s, e) => ToggleSection(ref _isFulfillmentExpanded, _fulfillmentPanel, _fulfillmentButton, "Cartridge Fulfillment");
            _sideMenuPanel.Controls.Add(_fulfillmentButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 2, Dock = DockStyle.Top, BackColor = Color.FromArgb(235, 235, 235) });

            // ── GROUP 1: CARTRIDGE MASTER DATA ───────────────────────────────────────
            _masterDataPanel = new Panel
            {
                Height = 2 * buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            AddSubMenuButton(_masterDataPanel, "Cartridge Models", (s, e) => { ShowCartridgeModelsPage(); ToggleMenu(); });
            AddSubMenuButton(_masterDataPanel, "View Cartridges", (s, e) => { ShowViewCartridgesPage(); ToggleMenu(); });
            // HIDDEN: Brand New Cartridges page - preserved for future use
            // AddSubMenuButton(_masterDataPanel, "Brand New Cartridges", (s, e) => { ShowBrandNewCartridgesPage(); ToggleMenu(); });
            _sideMenuPanel.Controls.Add(_masterDataPanel);

            _masterDataButton = AddCollapsibleSection("Cartridge Master Data");
            _masterDataButton.Click += (s, e) => ToggleSection(ref _isMasterDataExpanded, _masterDataPanel, _masterDataButton, "Cartridge Master Data");
            _sideMenuPanel.Controls.Add(_masterDataButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 8, Dock = DockStyle.Top, BackColor = Color.FromArgb(245, 245, 245) });

            // ── SEND NOTIFICATIONS ────────────────────────────────────────────────────
            var sendNotifButton = AddMenuButton("Send Notifications", (s, e) =>
            {
                ShowFulfillmentNotificationPage();
                ToggleMenu();
            });
            _sideMenuPanel.Controls.Add(sendNotifButton);

            // ── USER MANUAL ──────────────────────────────────────────────────────────
            //_userManualButton = AddMenuButton("User Manual", (s, e) =>
            //{
            //    OpenUserManual();
            //    ToggleMenu();
            //});
            //_sideMenuPanel.Controls.Add(_userManualButton);

            // ── HOME ─────────────────────────────────────────────────────────────────
            //_homeButton = AddMenuButton("Home", (s, e) =>
            //{
            //    ShowCartridgeRequestsPage();
            //    ToggleMenu();
            //});
            //_sideMenuPanel.Controls.Add(_homeButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── SEPARATOR ────────────────────────────────────────────────────────────
            _sideMenuPanel.Controls.Add(new Panel { Height = 2, Dock = DockStyle.Top, BackColor = Color.FromArgb(220, 220, 220) });

            _sideMenuPanel.Controls.Add(new Panel { Height = 10, Dock = DockStyle.Top, BackColor = Color.White });

            // ── HEADER LABEL (top of menu) ───────────────────────────────────────────
            var menuHeaderLabel = new Label
            {
                Text = "CARTRIDGE MANAGEMENT",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 154, 252),
                Height = 40,
                AutoSize = false,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            _sideMenuPanel.Controls.Add(menuHeaderLabel);

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
                Text = title + "   ▶",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(78, 154, 252),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
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

        private void ToggleSection(ref bool isExpanded, Panel panel, Button headerButton, string title)
        {
            isExpanded = !isExpanded;
            if (panel != null)
                panel.Visible = isExpanded;
            if (headerButton != null)
                headerButton.Text = title + "   " + (isExpanded ? "▼" : "▶");
        }

        private void ReflowSideMenu()
        {
            // No-op: DockStyle.Top on all _sideMenuPanel children handles repositioning automatically.
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

                ReflowSideMenu();
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
            {
                ToggleMenu();
            }
        }

        private void ShowPage(UserControl page)
        {
            foreach (Control c in _contentPanel.Controls) c.Dispose();
            _contentPanel.Controls.Clear();

            page.Dock = DockStyle.Fill;
            _contentPanel.Controls.Add(page);
        }

        private void ShowForm(Form form)
        {
            foreach (Control c in _contentPanel.Controls) c.Dispose();
            _contentPanel.Controls.Clear();

            form.TopLevel = false;
            form.FormBorderStyle = FormBorderStyle.None;
            form.Dock = DockStyle.Fill;

            _contentPanel.Controls.Add(form);
            form.Show();
        }

        private void ShowItFulfillmentPage()
        {
            if (!PermissionResolver.HasPageAccess("CartridgeExchangePage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowForm(new CartridgeManagementWpfHost());
        }

        // Same page-access key as Cartridge Exchange: it is the counterpart for mixed submissions.
        private void ShowMixedRequestExchangePage()
        {
            if (!PermissionResolver.HasPageAccess("CartridgeExchangePage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new MixedRequestExchangeWpfHost());
        }

        internal void NavigateToSendNotifications()
        {
            ShowFulfillmentNotificationPage();
        }

        private void ShowFulfillmentNotificationPage()
        {
            if (!PermissionResolver.HasPageAccess("CartridgeFulfillmentNotificationPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowForm(new SendNotificationsWpfHost());
        }

        private void ShowCartridgeRequestsPage()
        {
            ShowPage(new ViewCartridgeRequestsPage());
        }

        private void ShowCartridgeModelsPage()
        {
            if (!PermissionResolver.HasPageAccess("CartridgeModelsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new ViewCartridgeModelsWpfHost());
        }

        private void ShowBrandNewCartridgesPage()
        {
            ShowPage(new ViewBrandNewCartridgesPage());
        }

        private void ShowCartridgesForRefillPage()
        {
            ShowPage(new ViewCartridgesForRefillPage());
        }

        private void ShowCartridgeTrackingPage()
        {
            if (!PermissionResolver.HasPageAccess("CartridgeTrackingPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new CartridgeTrackingWpfHost());
        }

        private void ShowUnfulfilledCartridgeExchangesPage()
        {
            if (!PermissionResolver.HasPageAccess("UnfulfilledCartridgeExchangesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new Yakult.Inventory.App.Forms.CartridgeManagement.UnfulfilledExchangesWpfHost());
        }

        private void ShowFulfilledCartridgesPage()
        {
            if (!PermissionResolver.HasPageAccess("FulfilledCartridgesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new FulfilledCartridgesWpfHost());
        }

        private void ShowPartiallyFulfilledCartridgePage()
        {
            if (!PermissionResolver.HasPageAccess("PartiallyFulfilledCartridgePage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new Yakult.Inventory.App.Forms.CartridgeManagement.PartiallyFulfilledWpfHost());
        }

        private void ShowViewCartridgeSetsPage()
        {
            if (!PermissionResolver.HasPageAccess("ViewCartridgeSetsPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new ViewCartridgeSetsWpfHost());
        }

        private void ShowVendorCartridgeRefillPage()
        {
            if (!PermissionResolver.HasPageAccess("VendorCartridgeRefillPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new VendorCartridgeRefillWpfHost());
        }

        private void ShowNonRefillableCartridgesPage()
        {
            if (!PermissionResolver.HasPageAccess("NonRefillableCartridgesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new NonRefillableWpfHost());
        }

        private void ShowOutboundBatchesPage()
        {
            if (!PermissionResolver.HasPageAccess("OutboundBatchesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new OutboundBatchesWpfHost());
        }

        private void ShowDamagedEmptyCartridgesPage()
        {
            if (!PermissionResolver.HasPageAccess("DamagedEmptyCartridgesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new DamagedEmptyCartridgesWpfHost());
        }

        private void ShowDisposedCartridgesPage()
        {
            if (!PermissionResolver.HasPageAccess("DisposedCartridgesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new ClosedCartridgesWpfHost("Disposed"));
        }

        private void ShowSoldCartridgesPage()
        {
            if (!PermissionResolver.HasPageAccess("SoldCartridgesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new ClosedCartridgesWpfHost("Sold"));
        }

        private void ShowViewCartridgesPage()
        {
            if (!PermissionResolver.HasPageAccess("ViewCartridgesPage"))
            {
                MessageBox.Show("Access denied. You do not have permission to view this page.",
                    "Unauthorized", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowPage(new ViewCartridgesWpfHost());
        }

        private async void ShowApprovalPendingPage()
        {
            if (!AppSession.CurrentEmployeeId.HasValue)
            {
                if (AppSession.IsDeveloper)
                {
                    // No employee profile — show a preview with sample data for UI testing
                    var preview = new CartridgeApprovalDto
                    {
                        ApprovalId     = 0,
                        Status         = "Pending",
                        EmployeeName   = AppSession.CurrentUserName ?? "Developer",
                        SupervisorName = "(No Supervisor — Developer Preview)",
                        CompanyName    = "N/A",
                        BranchName     = "N/A",
                        DepartmentName = "N/A",
                        RequestedAt    = DateTime.Now
                    };
                    ShowPage(new ApprovalPendingPage(preview));
                    return;
                }

                MessageBox.Show(
                    "No employee profile is linked to your account.\nPlease contact an administrator.",
                    "Approval Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var repo     = new CartridgeApprovalRepository();
                var approval = await repo.CheckOrCreateAsync(AppSession.CurrentEmployeeId.Value);

                if (approval == null)
                {
                    MessageBox.Show(
                        "No approval record found for your account.",
                        "Approval Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                ShowPage(new ApprovalPendingPage(approval));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load approval status:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void OpenUserManual()
        {
            const string manualUrl = "https://mercadorob2-oss.github.io/Yakult-inventory-monitoring-system-User-manual/";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = manualUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }
    }
}
