using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Yakult.Inventory.App.WPF.RequestPortal.WalkThrough
{
    public partial class WpfPortalTourWindow : Window
    {
        // Delegate properties instead of events.
        // Assignment (=) overwrites the previous handler so reusing the window across
        // multiple tours never accumulates stale subscriptions.
        public Action OnNextClicked { get; set; }
        public Action OnPrevClicked { get; set; }
        public Action OnSkipClicked { get; set; }

        private const double PopoverWidth  = 400.0;
        private const double PopoverMargin = 16.0;

        public WpfPortalTourWindow()
        {
            InitializeComponent();
            Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            RootCanvas.Width  = ActualWidth;
            RootCanvas.Height = ActualHeight;
            SizeChanged += (s, args) =>
            {
                RootCanvas.Width  = ActualWidth;
                RootCanvas.Height = ActualHeight;
            };
        }

        // ── Tour lifecycle ──────────────────────────────────────────────────────

        /// <summary>
        /// Makes the overlay visible and interactive.
        /// Calls Show() on the very first use; subsequent calls are lightweight Opacity/hit-test toggles.
        /// </summary>
        public void ShowTour()
        {
            if (!IsVisible) Show();
            Opacity          = 1.0;
            IsHitTestVisible = true;
        }

        /// <summary>
        /// Hides the overlay visually without destroying the HWND.
        ///
        /// Opacity = 0 + IsHitTestVisible = false is used instead of Hide() or Close():
        ///
        ///   Hide() sets WS_VISIBLE = 0 in Win32, which causes Win32 to synchronously post
        ///   WM_ACTIVATE to the owner window (RequesterPortalForm).  WinForms focus-restoration
        ///   then calls back into WPF via ElementHost and tries to restore keyboard focus to the
        ///   last element that had it — which may be inside this overlay.  In this app
        ///   Application.Current is always null (WPF is hosted inside a WinForms process with no
        ///   Application object), so there is no DispatcherUnhandledException handler.  The
        ///   resulting WPF dispatcher exception escapes to AppDomain with IsTerminating = true
        ///   and the process dies without ever firing FormClosing.
        ///
        ///   Close() is worse: it destroys the HWND entirely and triggers the same cascade.
        ///
        ///   Opacity = 0 keeps WS_VISIBLE set so Win32 never posts WM_ACTIVATE.  The window is
        ///   invisible and non-interactive, but the HWND stays alive, focus-restoration targets a
        ///   valid window, and no exception is thrown.
        /// </summary>
        public void HideTour()
        {
            Keyboard.ClearFocus();
            Opacity          = 0.0;
            IsHitTestVisible = false;
        }

        // Parses **bold** markers in description text and sets Inlines on the TextBlock.
        // Segments between ** pairs are rendered as Bold runs; all other text is normal.
        private static void SetDescriptionInlines(System.Windows.Controls.TextBlock tb, string text)
        {
            tb.Inlines.Clear();
            if (string.IsNullOrEmpty(text)) return;

            var parts = text.Split(new[] { "**" }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                if (i % 2 == 1)
                    tb.Inlines.Add(new Run(parts[i]) { FontWeight = FontWeights.Bold });
                else
                    tb.Inlines.Add(new Run(parts[i]));
            }
        }

        /// <summary>
        /// Clears spotlight, ring, and banner back to a neutral state between tours.
        /// Called before the first ApplyStep() of each tour so stale geometry from a previous
        /// tour is not briefly visible during the show transition.
        /// </summary>
        public void ResetVisuals()
        {
            OverlayPath.Data         = null;
            HighlightRing.Visibility = Visibility.Collapsed;
            BulletList.ItemsSource   = null;
            BulletList.Visibility    = Visibility.Collapsed;
            DemoBanner.Visibility    = Visibility.Collapsed;
        }

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Applies the step to the overlay window.
        ///
        /// targetRect: bounds for the spotlight hole (the clear area in the dark overlay).
        ///             Null → full-screen dark overlay, centred popover.
        ///
        /// ringRect: optional secondary highlight. When provided, the blue HighlightRing and
        ///           the popover card are both anchored to this rect instead of targetRect.
        ///           Used for dialog sub-steps: targetRect = full dialog (so the dialog stays
        ///           fully visible), ringRect = the specific control being explained (so the
        ///           blue ring and tour card highlight the right section within the dialog).
        ///
        /// showDemoBanner: show the empty-state guidance banner.
        /// </summary>
        public void ApplyStep(WpfPortalTourStep step, int index, int total,
                              Rect? targetRect, Rect? ringRect = null, bool showDemoBanner = false)
        {
            TitleText.Text = step.Title;
            SetDescriptionInlines(DescriptionText, step.Description);
            StepCounterText.Text  = $"{index + 1} of {total}";
            DemoBanner.Visibility = showDemoBanner ? Visibility.Visible : Visibility.Collapsed;
            PopoverBorder.Width   = step.PopoverWidth ?? PopoverWidth;

            // Colored bullet list — visible only when the step provides status legend items
            if (step.ColoredBullets != null && step.ColoredBullets.Count > 0)
            {
                BulletList.ItemsSource = step.ColoredBullets;
                BulletList.Visibility  = Visibility.Visible;
            }
            else
            {
                BulletList.ItemsSource = null;
                BulletList.Visibility  = Visibility.Collapsed;
            }

            PrevButton.IsEnabled   = index > 0;
            NextButton.Content    = "Next →";
            NextButton.Visibility = index == total - 1 ? Visibility.Collapsed : Visibility.Visible;
            SkipButton.Visibility = Visibility.Visible; // "Close" is always available

            if (targetRect.HasValue)
            {
                var padded = Inflate(targetRect.Value, step.SpotlightPadding);
                UpdateSpotlight(padded, step.SpotlightCornerRadius);

                // When a secondary ring target is given, anchor both the ring and the card
                // to it so the specific control within the spotlight hole is highlighted.
                // Otherwise fall back to the spotlight bounds for both.
                var ringPadded = ringRect.HasValue
                    ? Inflate(ringRect.Value, step.SpotlightPadding)
                    : padded;
                PositionHighlightRing(ringPadded, step.SpotlightCornerRadius);
                PositionPopover(ringPadded, step.Side, step.Align);
            }
            else
            {
                // No target — cover everything and centre the popover
                OverlayPath.Data         = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
                HighlightRing.Visibility = Visibility.Collapsed;
                CenterPopover();
            }
        }

        // ── Geometry helpers ────────────────────────────────────────────────────

        private void UpdateSpotlight(Rect target, double cornerRadius)
        {
            var screen = new Rect(0, 0, ActualWidth, ActualHeight);
            OverlayPath.Data = new CombinedGeometry(
                GeometryCombineMode.Exclude,
                new RectangleGeometry(screen),
                new RectangleGeometry(target, cornerRadius, cornerRadius));
        }

        private void PositionHighlightRing(Rect target, double cornerRadius)
        {
            HighlightRing.Visibility = Visibility.Visible;
            HighlightRing.RadiusX    = cornerRadius;
            HighlightRing.RadiusY    = cornerRadius;
            HighlightRing.Width      = target.Width;
            HighlightRing.Height     = target.Height;
            System.Windows.Controls.Canvas.SetLeft(HighlightRing, target.Left);
            System.Windows.Controls.Canvas.SetTop(HighlightRing,  target.Top);
        }

        private void PositionPopover(Rect target, string side, string align)
        {
            // Ensure layout pass has run so we know PopoverBorder.ActualHeight
            PopoverBorder.UpdateLayout();
            double ph = Math.Max(PopoverBorder.ActualHeight, 180.0);
            double pw = PopoverBorder.Width;

            double left, top;

            switch (side)
            {
                case "top":
                    top  = target.Top - ph - PopoverMargin;
                    left = AlignX(target, align, pw);
                    break;

                case "left":
                    left = target.Left - pw - PopoverMargin;
                    top  = AlignY(target, align, ph);
                    break;

                case "right":
                    // At high DPI the target (typically the tour dialog, ~820 WinForms units)
                    // can be wide enough that placing the card to its right would push it past
                    // the screen edge. The standard clamp then forces the card's left edge back
                    // INSIDE the target, hiding the target's right content behind the white card.
                    // Fix: if the card's natural position would be clamped to overlap the target,
                    // fall back to placing the card ABOVE the target instead.
                    double rightCandidateLeft = target.Right + PopoverMargin;
                    double rightClampMax      = ActualWidth - pw - 12;
                    if (rightCandidateLeft > rightClampMax && rightClampMax < target.Right)
                    {
                        // Not enough room to the right — float the card above the target,
                        // aligned to the target's right edge so it stays near the action.
                        top  = target.Top - ph - PopoverMargin;
                        left = Math.Max(12, Math.Min(target.Right - pw, rightClampMax));
                        System.Diagnostics.Debug.WriteLine(
                            $"[Tour] PositionPopover 'right' → fell back to 'top' (rightCandidateLeft={rightCandidateLeft:F0} > clampMax={rightClampMax:F0})");
                    }
                    else
                    {
                        left = rightCandidateLeft;
                        top  = AlignY(target, align, ph);
                    }
                    break;

                case "over":
                    left = (ActualWidth  - pw) / 2.0;
                    top  = (ActualHeight - ph) / 2.0;
                    break;

                case "inner":
                    // Card sits inside the spotlight hole near the bottom edge of the target,
                    // leaving the top of the target (e.g. demo rows) clearly visible.
                    top  = target.Bottom - ph - PopoverMargin;
                    left = AlignX(target, align, pw);
                    break;

                default: // "bottom"
                    top  = target.Bottom + PopoverMargin;
                    left = AlignX(target, align, pw);
                    break;
            }

            // Clamp so the popover stays within the window
            left = Math.Max(12, Math.Min(left, ActualWidth  - pw  - 12));
            top  = Math.Max(12, Math.Min(top,  ActualHeight - ph  - 12));

            System.Windows.Controls.Canvas.SetLeft(PopoverBorder, left);
            System.Windows.Controls.Canvas.SetTop(PopoverBorder,  top);
        }

        private void CenterPopover()
        {
            PopoverBorder.UpdateLayout();
            double ph = Math.Max(PopoverBorder.ActualHeight, 180.0);
            System.Windows.Controls.Canvas.SetLeft(PopoverBorder, (ActualWidth  - PopoverBorder.Width) / 2.0);
            System.Windows.Controls.Canvas.SetTop(PopoverBorder,  (ActualHeight - ph)           / 2.0);
        }

        // ── Alignment helpers ───────────────────────────────────────────────────

        private static double AlignX(Rect target, string align, double pw)
        {
            switch (align)
            {
                case "end":    return target.Right - pw;
                case "center": return target.Left + (target.Width - pw) / 2.0;
                default:       return target.Left; // "start"
            }
        }

        private static double AlignY(Rect target, string align, double ph)
        {
            switch (align)
            {
                case "end":    return target.Bottom - ph;
                case "center": return target.Top + (target.Height - ph) / 2.0;
                default:       return target.Top; // "start"
            }
        }

        private static Rect Inflate(Rect r, double padding)
            => new Rect(r.Left - padding, r.Top - padding,
                        r.Width + 2 * padding, r.Height + 2 * padding);

        // ── DPI conversion ──────────────────────────────────────────────────────

        /// <summary>
        /// Converts a physical screen rect (from WinForms RectangleToScreen or WPF PointToScreen)
        /// into the logical pixel space used by this WPF window.
        /// </summary>
        public Rect PhysicalToLogical(double physLeft, double physTop, double physWidth, double physHeight)
        {
            var src = PresentationSource.FromVisual(this);
            double sx = src?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
            double sy = src?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;
            return new Rect(physLeft * sx, physTop * sy, physWidth * sx, physHeight * sy);
        }

        // ── Event handlers ──────────────────────────────────────────────────────

        private void NextButton_Click(object sender, RoutedEventArgs e) => OnNextClicked?.Invoke();
        private void PrevButton_Click(object sender, RoutedEventArgs e) => OnPrevClicked?.Invoke();
        private void SkipButton_Click(object sender, RoutedEventArgs e) => OnSkipClicked?.Invoke();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                OnSkipClicked?.Invoke();
        }

        private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }
}
