using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Yakult.Inventory.App.WPF.RequestPortal.WalkThrough
{
    /// <summary>
    /// Defines one step in the WPF Request Portal guided tour.
    /// Either TargetWpf or TargetWinForms resolves the element to spotlight;
    /// when both are null the popover appears centred with a full dark overlay.
    /// </summary>
    public class WpfPortalTourStep
    {
        public string Title { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// Optional list of colored bullet rows rendered below the description.
        /// Use instead of emoji in Description for status legend steps.
        /// </summary>
        public List<TourBulletItem> ColoredBullets { get; set; }

        /// <summary>WPF FrameworkElement to spotlight (resolved lazily so the view is loaded first).</summary>
        public Func<FrameworkElement> TargetWpf { get; set; }

        /// <summary>
        /// Optional WPF element used only for scroll positioning.
        /// When set, the viewport scrolls to show this element (typically a small section header)
        /// while TargetWpf is still used for the spotlight rect.
        /// Use when the spotlight target is too tall to fit within the topMargin budget.
        /// </summary>
        public Func<FrameworkElement> ScrollAnchorWpf { get; set; }

        /// <summary>WinForms Control to spotlight (resolved lazily).</summary>
        public Func<System.Windows.Forms.Control> TargetWinForms { get; set; }

        /// <summary>Which side of the spotlight the popover appears on. Values: bottom, top, left, right, over.</summary>
        public string Side { get; set; } = "bottom";

        /// <summary>Alignment of the popover along the target edge. Values: start, end, center.</summary>
        public string Align { get; set; } = "start";

        /// <summary>Override the popover card width for this step. Defaults to 400 when null.</summary>
        public double? PopoverWidth { get; set; }

        /// <summary>Padding (logical pixels) added around the target rect to widen the spotlight.</summary>
        public double SpotlightPadding { get; set; } = 10.0;

        /// <summary>Corner radius of the spotlight rectangle.</summary>
        public double SpotlightCornerRadius { get; set; } = 6.0;

        /// <summary>Called once when the tour advances TO this step (before the overlay is drawn).</summary>
        public Action OnEnter { get; set; }

        /// <summary>Called once when the tour advances AWAY from this step (before the next OnEnter).</summary>
        public Action OnLeave { get; set; }
    }

    /// <summary>
    /// A colored dot + label row used in status legend steps.
    /// </summary>
    public class TourBulletItem
    {
        public Brush Dot  { get; set; }
        public string Text { get; set; }

        public static TourBulletItem Make(string hex, string text)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            return new TourBulletItem { Dot = new SolidColorBrush(color), Text = text };
        }
    }
}
