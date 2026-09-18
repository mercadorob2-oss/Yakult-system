using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace Yakult.Inventory.App.Forms.ConsumableManagement
{
    public enum RequestSetManagementExitAction
    {
        None = 0,
        BackToPortal = 1,
        Logout = 2
    }

    /// <summary>
    /// Card 2 of the Consumable Management Portal. A trimmed-down side-menu shell mirroring
    /// MainForm.cs's Request/Set navigation, scoped to only: Batch Add Request, View Requests,
    /// View Sets, Batch Add Item, Unfulfilled Requests, Partially Fulfilled Requests,
    /// Send Notifications (Sets), and View Archive. Each item's Show*() method reuses the exact
    /// class and navigation pattern MainForm.cs already uses for it (ShowPage embedding for
    /// UserControls, modal ShowDialog(this) for the two WPF dialogs).
    /// </summary>
    public partial class RequestSetManagementPortalForm : Form
    {
        private const int MENU_WIDTH = 320;

        private Panel _contentPanel;
        private Panel _menuOverlay;
        private Panel _sideMenuPanel;
        private Button _hamburgerButton;
        private bool _isMenuOpen;

        private Button _addMasterDataButton;
        private Panel _addMasterDataPanel;
        private bool _isAddMasterDataExpanded;

        private Button _viewTransactionsButton;
        private Panel _viewTransactionsPanel;
        private bool _isViewTransactionsExpanded;

        private Button _viewMasterDataButton;
        private Panel _viewMasterDataPanel;
        private bool _isViewMasterDataExpanded;

        private Button _viewHistoryButton;
        private Panel _viewHistoryPanel;
        private bool _isViewHistoryExpanded;

        private Button _backToPortalButton;
        private Button _logoutButton;
        private Button _homeButton;

        public RequestSetManagementExitAction ExitAction { get; private set; } = RequestSetManagementExitAction.None;

        public RequestSetManagementPortalForm(int? highlightSetId = null)
        {
            InitializeComponent();
            BuildUi();

            // BuildUi() already lands on the Home search view — when a specific Set needs to be
            // highlighted (e.g. redirected here right after an auto-grouped Request Portal
            // submission), replace that with the Manage Sets grid, pre-scrolled/selected to it.
            if (highlightSetId.HasValue)
                ShowViewSetsPage(highlightSetId);

            // Same mouse-capture-stuck-on-Alt+Tab workaround CartridgeManagementPortalForm
            // applies to itself — see ConsumableManagementPortalShell for why this is re-applied
            // at the outer WPF shell level once this form is embedded there.
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
            Text = "Request & Set Management";
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

            _hamburgerButton = new Button
            {
                Text = "≡",
                Font = new Font("Segoe UI", 16F),
                Size = new Size(50, 50),
                Location = new Point(10, 10),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _hamburgerButton.FlatAppearance.BorderSize = 0;
            _hamburgerButton.Click += (s, e) => ToggleMenu();
            topBar.Controls.Add(_hamburgerButton);

            var titleLabel = new Label
            {
                Text = "Request & Set Management",
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(70, 20)
            };
            topBar.Controls.Add(titleLabel);

            Controls.Add(topBar);

            _contentPanel = new Panel
            {
                Location = new Point(0, topBar.Height),
                Size = new Size(ClientSize.Width, ClientSize.Height - topBar.Height),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White
            };
            Controls.Add(_contentPanel);

            // Transparent click-catcher over the content area only (starts at MENU_WIDTH, not
            // the whole form) — closes the menu when clicking outside it. Matches
            // CartridgeManagementPortalForm's _menuOverlay exactly; it must be Transparent, not
            // a visible fill color, since a solid ARGB alpha color doesn't actually render
            // translucently in WinForms — it paints as an opaque block instead.
            _menuOverlay = new Panel
            {
                Location = new Point(MENU_WIDTH, topBar.Height),
                Size = new Size(ClientSize.Width - MENU_WIDTH, ClientSize.Height - topBar.Height),
                BackColor = Color.Transparent,
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _menuOverlay.Click += (s, e) => ToggleMenu();
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
            _sideMenuPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(200, 200, 200), 1))
                    e.Graphics.DrawLine(pen, _sideMenuPanel.Width - 1, 0, _sideMenuPanel.Width - 1, _sideMenuPanel.Height);
            };

            PopulateSideMenu();

            Controls.Add(_sideMenuPanel);
            _sideMenuPanel.BringToFront();

            topBar.BringToFront();

            ShowHomeView();
        }

        private void PopulateSideMenu()
        {
            _sideMenuPanel.Controls.Clear();

            // NOTE: Controls are added in BOTTOM-TO-TOP visual order (matches
            // CartridgeManagementPortalForm's PopulateSideMenu convention).
            int buttonHeight = 50;

            _logoutButton = AddMenuButton("Logout", (s, e) =>
            {
                ExitAction = RequestSetManagementExitAction.Logout;
                Yakult.Inventory.App.Session.AppSession.Clear();
                Close();
            }, isPrimary: true, backColor: Color.FromArgb(220, 53, 69), foreColor: Color.White);
            _sideMenuPanel.Controls.Add(_logoutButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 10, Dock = DockStyle.Top, BackColor = Color.White });

            _backToPortalButton = AddMenuButton("Back to Portal", (s, e) =>
            {
                ExitAction = RequestSetManagementExitAction.BackToPortal;
                Close();
            }, isPrimary: true, backColor: Color.FromArgb(52, 152, 219), foreColor: Color.White);
            _sideMenuPanel.Controls.Add(_backToPortalButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 8, Dock = DockStyle.Top, BackColor = Color.FromArgb(245, 245, 245) });

            // The three sections below, and which items live in each, mirror MainForm.cs's own
            // PopulateSideMenu() exactly (View History / View Transactions / Add Master Data),
            // trimmed to only the 8 items in scope for this card. Source order preserved so the
            // visual stacking matches MainForm's.

            // ── VIEW HISTORY ─────────────────────────────────────────────────────────
            _viewHistoryPanel = new Panel
            {
                Height = 1 * buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            AddSubMenuButton(_viewHistoryPanel, "View Archive", (s, e) => { ShowViewArchivePage(); ToggleMenu(); });
            _sideMenuPanel.Controls.Add(_viewHistoryPanel);

            _viewHistoryButton = AddCollapsibleSection("View History");
            _viewHistoryButton.Click += (s, e) => ToggleSection(ref _isViewHistoryExpanded, _viewHistoryPanel, _viewHistoryButton, "View History");
            _sideMenuPanel.Controls.Add(_viewHistoryButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── VIEW TRANSACTIONS ────────────────────────────────────────────────────
            _viewTransactionsPanel = new Panel
            {
                Height = 5 * buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            AddSubMenuButton(_viewTransactionsPanel, "View Sets", (s, e) => { ShowViewSetsPage(); ToggleMenu(); });
            AddSubMenuButton(_viewTransactionsPanel, "Send Notifications (Sets)", (s, e) => { ShowSetDispatchNotificationPage(); ToggleMenu(); });
            AddSubMenuButton(_viewTransactionsPanel, "Unfulfilled Requests", (s, e) => { ShowUnfulfilledRequestsPage(); ToggleMenu(); });
            AddSubMenuButton(_viewTransactionsPanel, "Partially Fulfilled Requests", (s, e) => { ShowPartiallyFulfilledRequestsPage(); ToggleMenu(); });
            AddSubMenuButton(_viewTransactionsPanel, "View Requests", (s, e) => { ShowViewRequestsPage(); ToggleMenu(); });
            _sideMenuPanel.Controls.Add(_viewTransactionsPanel);

            _viewTransactionsButton = AddCollapsibleSection("View Transactions");
            _viewTransactionsButton.Click += (s, e) => ToggleSection(ref _isViewTransactionsExpanded, _viewTransactionsPanel, _viewTransactionsButton, "View Transactions");
            _sideMenuPanel.Controls.Add(_viewTransactionsButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── VIEW MASTER DATA ─────────────────────────────────────────────────────
            _viewMasterDataPanel = new Panel
            {
                Height = 1 * buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            AddSubMenuButton(_viewMasterDataPanel, "View Items", (s, e) => { ShowViewItemsPage(); ToggleMenu(); });
            _sideMenuPanel.Controls.Add(_viewMasterDataPanel);

            _viewMasterDataButton = AddCollapsibleSection("View Master Data");
            _viewMasterDataButton.Click += (s, e) => ToggleSection(ref _isViewMasterDataExpanded, _viewMasterDataPanel, _viewMasterDataButton, "View Master Data");
            _sideMenuPanel.Controls.Add(_viewMasterDataButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            // ── ADD MASTER DATA ──────────────────────────────────────────────────────
            _addMasterDataPanel = new Panel
            {
                Height = 2 * buttonHeight,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(250, 250, 250),
                Visible = false
            };
            AddSubMenuButton(_addMasterDataPanel, "Add Request", (s, e) => { ShowBatchAddRequestDialog(); ToggleMenu(); });
            AddSubMenuButton(_addMasterDataPanel, "Add Item", (s, e) => { ShowBatchAddItemDialog(); ToggleMenu(); });
            _sideMenuPanel.Controls.Add(_addMasterDataPanel);

            _addMasterDataButton = AddCollapsibleSection("Add Master Data");
            _addMasterDataButton.Click += (s, e) => ToggleSection(ref _isAddMasterDataExpanded, _addMasterDataPanel, _addMasterDataButton, "Add Master Data");
            _sideMenuPanel.Controls.Add(_addMasterDataButton);

            _sideMenuPanel.Controls.Add(new Panel { Height = 5, Dock = DockStyle.Top, BackColor = Color.White });

            _homeButton = AddMenuButton("🏠 Home", (s, e) => { ShowHomeView(); ToggleMenu(); }, isPrimary: false, backColor: Color.White, foreColor: Color.FromArgb(60, 60, 60));
            _sideMenuPanel.Controls.Add(_homeButton);
        }

        // ====================================================================
        // NAVIGATION TARGETS — same class + pattern as MainForm.cs
        // ====================================================================

        private void ShowBatchAddRequestDialog()
        {
            using (var dialog = new Yakult.Inventory.App.Pages.Request.BatchAddRequestDialog())
            {
                dialog.ShowDialog(this);
            }
        }

        private static readonly string[] ConsumableCategories = { "Cartridge", "Ink", "Printhead", "Toner" };

        private void ShowViewRequestsPage()
        {
            ShowPage(new Yakult.Inventory.App.Pages.Request.ViewRequestsPage(ConsumableCategories, restrictToRequestSetManagementWorkflow: true));
        }

        private void ShowViewSetsPage(int? highlightSetId = null)
        {
            var page = new Yakult.Inventory.App.Pages.Set.ViewSetPage(restrictToConsumablesByDefault: true);
            if (highlightSetId.HasValue)
                page.HighlightSet(highlightSetId.Value);
            ShowPage(page);
        }

        private void ShowBatchAddItemDialog()
        {
            using (var dialog = new Yakult.Inventory.App.Pages.Item.BatchAddItemDialog())
            {
                dialog.ShowDialog(this);
            }
        }

        private void ShowUnfulfilledRequestsPage()
        {
            ShowPage(new Yakult.Inventory.App.Forms.Request.UnfulfilledRequestsWpfHost());
        }

        private void ShowPartiallyFulfilledRequestsPage()
        {
            ShowPage(new Yakult.Inventory.App.Forms.Request.PartiallyFulfilledRequestsWpfHost());
        }

        private void ShowSetDispatchNotificationPage()
        {
            ShowPage(new Yakult.Inventory.App.Pages.Set.SetDispatchNotificationPage());
        }

        private void ShowViewArchivePage()
        {
            ShowPage(new Yakult.Inventory.App.Pages.Archive.ViewArchivePage());
        }

        private void ShowViewItemsPage()
        {
            ShowPage(new Yakult.Inventory.App.Pages.Item.ViewItemsPage());
        }

        /// <summary>Opens a single Set's detail view (used by search results — Requests have
        /// no equivalent single-record detail view in this codebase, so Request results route
        /// to the general View Requests page instead).</summary>
        private void ShowViewSetDetail(int setId)
        {
            using (var detail = new Yakult.Inventory.App.Pages.Set.ViewSetDetailPage(setId))
            {
                detail.ShowDialog(this);
            }
        }

        // ====================================================================
        // HOME VIEW — search across Requests and Sets
        // ====================================================================
        // Hosts Wpf\RequestSetSearch\Views\RequestSetSearchView via ElementHost — a WPF view
        // whose layout/styling directly mirrors the Inventory System's own homepage search
        // (Wpf\Search\Views\SearchView.xaml): same pill search box, same card design, same
        // Initial/Loading/NoResults/Results state machine. Same embedding technique already
        // used by Pages\Request\ViewRequestsPage (ElementHost wrapping a WPF UserControl).

        private void ShowHomeView()
        {
            foreach (Control c in _contentPanel.Controls) c.Dispose();
            _contentPanel.Controls.Clear();

            var view = new Yakult.Inventory.App.Wpf.RequestSetSearch.Views.RequestSetSearchView();
            view.CardSelected += r =>
            {
                if (r.IsSet)
                    ShowViewSetDetail(r.Id);
                else
                    ShowViewRequestsPage();
            };

            var host = new ElementHost { Dock = DockStyle.Fill, Child = view };
            _contentPanel.Controls.Add(host);
        }

        // ====================================================================
        // MENU / LAYOUT HELPERS (mirrors CartridgeManagementPortalForm)
        // ====================================================================

        private void ShowPage(UserControl page)
        {
            foreach (Control c in _contentPanel.Controls) c.Dispose();
            _contentPanel.Controls.Clear();

            page.Dock = DockStyle.Fill;
            _contentPanel.Controls.Add(page);
        }

        private Button AddMenuButton(string text, EventHandler onClick, bool isPrimary, Color backColor, Color foreColor)
        {
            var button = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 10F, isPrimary ? FontStyle.Bold : FontStyle.Regular),
                Height = 50,
                Dock = DockStyle.Top,
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0)
            };
            button.FlatAppearance.BorderSize = 0;
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
                Padding = new Padding(20, 0, 0, 0)
            };
            button.FlatAppearance.BorderSize = 0;
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

        private void ToggleMenu()
        {
            _isMenuOpen = !_isMenuOpen;

            if (_isMenuOpen)
            {
                _menuOverlay.Visible = true;
                _menuOverlay.BringToFront();
                // WinForms doesn't composite sibling controls, so a "transparent" overlay only
                // shows what's underneath if the content stays on top of it in z-order — without
                // this, the currently active page gets hidden behind the overlay's paint area
                // (rendering as the Form's own white background). Matches
                // CartridgeManagementPortalForm's ToggleMenu() exactly.
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
    }
}
