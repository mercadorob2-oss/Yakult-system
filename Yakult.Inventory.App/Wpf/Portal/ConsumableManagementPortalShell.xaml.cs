using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Yakult.Inventory.App.Forms.CartridgeManagement;
using Yakult.Inventory.App.Forms.ConsumableManagement;

namespace Yakult.Inventory.App.Wpf.Portal
{
    /// <summary>
    /// Landing shell for the Consumable Management Portal. Card 1 ("Cartridge Management")
    /// embeds the existing <c>CartridgeManagementPortalForm</c> directly into this window's
    /// content area via <see cref="WindowsFormsHost"/> — no separate popup window is opened.
    /// The embedding technique (TopLevel=false, FormBorderStyle=None, added to a Panel) mirrors
    /// CartridgeManagementPortalForm's own ShowForm() method, which already embeds sub-forms
    /// (e.g. CartridgeManagementWpfHost) the same way — this is an established, proven pattern
    /// in this codebase, just bridged one level higher into WPF.
    ///
    /// Future consumable categories (Ink, Printhead, Toner Cartridge) will get their own card
    /// here alongside Card 1.
    /// </summary>
    public partial class ConsumableManagementPortalShell : Window
    {
        private static readonly Color TextPrimary   = Color.FromRgb(15,  23,  42);
        private static readonly Color TextSecondary = Color.FromRgb(100, 116, 139);
        private static readonly Color ACartridge    = Color.FromRgb(249, 115,  22);
        private static readonly Color ARequestSet   = Color.FromRgb(37,  99, 235);

        private readonly UIElement _cardGridContent;

        private CartridgeManagementPortalForm _embeddedCartridgeForm;
        private bool _isShowingCartridgeManagement;

        private RequestSetManagementPortalForm _embeddedRequestSetForm;
        private bool _isShowingRequestSetManagement;

        /// <summary>True when the embedded system requested a full application logout.</summary>
        public bool LogoutRequested { get; private set; }

        private readonly int? _highlightSetId;

        public ConsumableManagementPortalShell(
            bool openCartridgeManagementDirectly = false,
            bool openRequestSetManagementDirectly = false,
            int? highlightSetId = null)
        {
            InitializeComponent();

            _highlightSetId = highlightSetId;
            _cardGridContent = BuildCardGrid();
            ContentHost.Content = _cardGridContent;

            if (openCartridgeManagementDirectly)
                ShowCartridgeManagementEmbedded();
            else if (openRequestSetManagementDirectly)
                ShowRequestSetManagementEmbedded();

            // The mouse-capture-stuck-on-Alt+Tab fix that CartridgeManagementPortalForm applies
            // to itself (see its constructor) only fires while it is a genuine top-level window.
            // Once embedded here, this shell is the true top-level window, so the same fix is
            // re-applied at this level to cover the embedded content too.
            Activated   += (_, __) => { try { System.Windows.Input.Mouse.Capture(null); } catch { } };
            Deactivated += (_, __) => { try { System.Windows.Input.Mouse.Capture(null); } catch { } };

            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    GoBack();
                    e.Handled = true;
                }
            };

            Closing += (_, __) => CleanupEmbeddedForm();
        }

        private void BackBtn_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => GoBack();

        private void GoBack()
        {
            if (_isShowingCartridgeManagement)
                _embeddedCartridgeForm?.Close(); // triggers FormClosed -> ShowCardGrid()
            else if (_isShowingRequestSetManagement)
                _embeddedRequestSetForm?.Close(); // triggers FormClosed -> ShowCardGrid()
            else
                Close();
        }

        // ====================================================================
        // CARD 1 — EMBED CARTRIDGE MANAGEMENT PORTAL
        // ====================================================================

        private void ShowCartridgeManagementEmbedded()
        {
            _embeddedCartridgeForm = new CartridgeManagementPortalForm
            {
                TopLevel        = false,
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.None,
                Dock            = System.Windows.Forms.DockStyle.Fill,
                // CartridgeManagementPortalForm.InitializeComponent() sets AutoScaleMode.Font,
                // which rescales its whole layout against runtime font metrics — correct for a
                // genuine top-level Form (proper DPI awareness from Windows), but WindowsFormsHost
                // doesn't propagate DPI context the same way, so the scale factor ends up wrong
                // and the content renders larger than this host panel, clipping on the right.
                // Disabling auto-scale here leaves Dock=Fill as the only thing sizing it.
                AutoScaleMode   = System.Windows.Forms.AutoScaleMode.None
            };

            var panel = new System.Windows.Forms.Panel { Dock = System.Windows.Forms.DockStyle.Fill };
            panel.Controls.Add(_embeddedCartridgeForm);

            _embeddedCartridgeForm.FormClosed += (s, e) => Dispatcher.Invoke(OnEmbeddedCartridgeManagementClosed);

            var host = new WindowsFormsHost { Child = panel };

            // Defer Show() until WPF has actually measured/arranged this host with real pixel
            // dimensions. Calling Show() synchronously (before the host is part of a laid-out
            // visual tree) leaves the embedded Form's Dock=Fill content (side menu, content
            // panel) laid out against a 0x0 parent — only fixed-size controls like the top bar
            // (Height=70, not Fill) render correctly in that case.
            host.Loaded += (s, e) => _embeddedCartridgeForm?.Show();

            ContentHost.Content = host;
            _isShowingCartridgeManagement = true;

            // Cartridge Management has its own top bar and side-menu navigation (including its
            // own "Back to Portal"/"Logout" buttons), so the outer Consumable Management header
            // is redundant while it's shown — collapse it so the embedded content uses that space.
            HeaderRow.Height = new GridLength(0);
            HeaderBorder.Visibility = Visibility.Collapsed;
        }

        private void OnEmbeddedCartridgeManagementClosed()
        {
            bool logout = _embeddedCartridgeForm != null &&
                          _embeddedCartridgeForm.ExitAction == CartridgeManagementExitAction.Logout;

            _embeddedCartridgeForm?.Dispose();
            _embeddedCartridgeForm = null;
            _isShowingCartridgeManagement = false;

            if (logout)
            {
                LogoutRequested = true;
                Close();
                return;
            }

            ShowCardGrid();
        }

        private void ShowCardGrid()
        {
            ContentHost.Content = _cardGridContent;
            BackBtnText.Text = "← Back";
            HeaderRow.Height = new GridLength(90);
            HeaderBorder.Visibility = Visibility.Visible;
        }

        private void CleanupEmbeddedForm()
        {
            if (_embeddedCartridgeForm != null)
            {
                try { _embeddedCartridgeForm.Dispose(); } catch { }
                _embeddedCartridgeForm = null;
            }
            if (_embeddedRequestSetForm != null)
            {
                try { _embeddedRequestSetForm.Dispose(); } catch { }
                _embeddedRequestSetForm = null;
            }
        }

        // ====================================================================
        // CARD 2 — EMBED REQUEST & SET MANAGEMENT PORTAL
        // ====================================================================

        private void ShowRequestSetManagementEmbedded()
        {
            _embeddedRequestSetForm = new RequestSetManagementPortalForm(_highlightSetId)
            {
                TopLevel        = false,
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.None,
                Dock            = System.Windows.Forms.DockStyle.Fill,
                // Same AutoScaleMode/WindowsFormsHost DPI mismatch fix applied to Card 1 —
                // see ShowCartridgeManagementEmbedded() for the full explanation.
                AutoScaleMode   = System.Windows.Forms.AutoScaleMode.None
            };

            var panel = new System.Windows.Forms.Panel { Dock = System.Windows.Forms.DockStyle.Fill };
            panel.Controls.Add(_embeddedRequestSetForm);

            _embeddedRequestSetForm.FormClosed += (s, e) => Dispatcher.Invoke(OnEmbeddedRequestSetManagementClosed);

            var host = new WindowsFormsHost { Child = panel };

            // Same Loaded-deferred Show() fix as Card 1 — see ShowCartridgeManagementEmbedded().
            host.Loaded += (s, e) => _embeddedRequestSetForm?.Show();

            ContentHost.Content = host;
            _isShowingRequestSetManagement = true;

            HeaderRow.Height = new GridLength(0);
            HeaderBorder.Visibility = Visibility.Collapsed;
        }

        private void OnEmbeddedRequestSetManagementClosed()
        {
            bool logout = _embeddedRequestSetForm != null &&
                          _embeddedRequestSetForm.ExitAction == RequestSetManagementExitAction.Logout;

            _embeddedRequestSetForm?.Dispose();
            _embeddedRequestSetForm = null;
            _isShowingRequestSetManagement = false;

            if (logout)
            {
                LogoutRequested = true;
                Close();
                return;
            }

            ShowCardGrid();
        }

        // ====================================================================
        // CARD GRID (LANDING CONTENT)
        // ====================================================================

        private UIElement BuildCardGrid()
        {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(36, 32, 36, 32) };
            var panel = new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = 1100 };

            panel.Children.Add(MakeCard(
                "Cartridge Management",
                "Manage cartridge exchange, refills, and fulfillment.",
                "\U0001F5A8", ACartridge,
                ShowCartridgeManagementEmbedded));

            panel.Children.Add(MakeCard(
                "Request & Set Management",
                "Batch requests, view requests and sets, fulfillment tracking, and archives.",
                "\U0001F4CB", ARequestSet,
                ShowRequestSetManagementEmbedded));

            scroll.Content = panel;
            return scroll;
        }

        private Border MakeCard(string title, string desc, string icon, Color accent, System.Action onClick)
        {
            var normalShadow = new DropShadowEffect { BlurRadius = 8,  ShadowDepth = 2, Opacity = 0.07, Direction = 270, Color = Colors.Black };
            var hoverShadow  = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 4, Opacity = 0.15, Direction = 270, Color = Colors.Black };

            var card = new Border
            {
                Width           = 340,
                Margin          = new Thickness(8, 0, 8, 20),
                Background      = Brushes.White,
                BorderBrush     = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(12),
                Cursor          = Cursors.Hand,
                Effect          = normalShadow
            };

            var innerGrid = new Grid();
            innerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
            innerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var accentBar = new Border { Background = new SolidColorBrush(accent), CornerRadius = new CornerRadius(10, 10, 0, 0) };
            Grid.SetRow(accentBar, 0);
            innerGrid.Children.Add(accentBar);

            var body = new StackPanel { Margin = new Thickness(22, 20, 22, 20) };

            var iconBox = new Border
            {
                Background          = new SolidColorBrush(Color.FromArgb(32, accent.R, accent.G, accent.B)),
                CornerRadius        = new CornerRadius(12),
                Width               = 52,
                Height              = 52,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin              = new Thickness(0, 0, 0, 14)
            };
            iconBox.Child = new TextBlock
            {
                Text                = icon,
                FontSize            = 24,
                FontFamily          = new FontFamily("Segoe UI Emoji"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            body.Children.Add(iconBox);

            body.Children.Add(new TextBlock
            {
                Text         = title,
                FontSize     = 15,
                FontWeight   = FontWeights.Bold,
                Foreground   = new SolidColorBrush(TextPrimary),
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 0, 0, 6)
            });

            body.Children.Add(new TextBlock
            {
                Text         = desc,
                FontSize     = 12,
                Foreground   = new SolidColorBrush(TextSecondary),
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 0, 0, 16)
            });

            body.Children.Add(new TextBlock
            {
                Text       = "Open System →",
                FontSize   = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(accent)
            });

            Grid.SetRow(body, 1);
            innerGrid.Children.Add(body);
            card.Child = innerGrid;

            card.MouseEnter += (_, __) => { card.Effect = hoverShadow; card.BorderBrush = new SolidColorBrush(accent); };
            card.MouseLeave += (_, __) => { card.Effect = normalShadow; card.BorderBrush = new SolidColorBrush(Color.FromRgb(225, 230, 236)); };
            card.MouseLeftButtonUp += (_, __) => onClick?.Invoke();

            return card;
        }
    }
}
