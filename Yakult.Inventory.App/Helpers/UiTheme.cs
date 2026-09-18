using System.Drawing;

namespace Yakult.Inventory.App.Helpers
{
    internal static class UiTheme
    {
        internal static class Colors
        {
            internal static readonly Color HeaderBack = Color.FromArgb(245, 247, 250);
            internal static readonly Color CardBack = Color.White;

            internal static readonly Color Primary = Color.FromArgb(52, 152, 219);
            internal static readonly Color PrimaryHover = Color.FromArgb(41, 128, 185);

            internal static readonly Color Danger = Color.FromArgb(231, 76, 60);
            internal static readonly Color DangerHoverBack = Color.FromArgb(255, 240, 240);

            internal static readonly Color OutlineHoverBack = Color.FromArgb(235, 245, 255);

            internal static readonly Color TextDark = Color.FromArgb(40, 40, 40);
            internal static readonly Color TextMuted = Color.FromArgb(90, 90, 90);

            internal static readonly Color GridHeaderBack = Color.FromArgb(52, 152, 219);
            internal static readonly Color GridAltRowBack = Color.FromArgb(232, 240, 248);
            internal static readonly Color GridSelectionBack = Color.FromArgb(41, 128, 185);
            internal static readonly Color GridSelectionFore = Color.White;
        }

        internal static class Fonts
        {
            internal static readonly Font Title = new Font("Segoe UI", 16F, FontStyle.Bold);
            internal static readonly Font Search = new Font("Segoe UI", 9F, FontStyle.Regular);
            internal static readonly Font Button = new Font("Segoe UI Emoji", 10.5F, FontStyle.Bold);
            internal static readonly Font RefreshIcon = new Font("Segoe UI", 18F, FontStyle.Bold);
            internal static readonly Font SummaryTitle = new Font("Segoe UI", 9F, FontStyle.Bold);
            internal static readonly Font SummaryValue = new Font("Segoe UI", 18F, FontStyle.Bold);
            internal static readonly Font GridHeader = new Font("Segoe UI", 9F, FontStyle.Bold);
            internal static readonly Font GridCell = new Font("Segoe UI", 9F, FontStyle.Regular);
        }

        internal static class Sizes
        {
            internal const int HeaderHeight = 110;
            internal const int ButtonBarHeight = 94;
            internal const int SummaryRowHeight = 90;

            internal const int PillButtonHeight = 46;
            internal static readonly Size RefreshButton = new Size(58, 58);

            internal static readonly Size SummaryCard = new Size(210, 70);
        }

        internal static class Padding
        {
            internal static readonly System.Windows.Forms.Padding Header = new System.Windows.Forms.Padding(12, 10, 12, 10);
            internal static readonly System.Windows.Forms.Padding ButtonBar = new System.Windows.Forms.Padding(12, 0, 12, 10);
            internal static readonly System.Windows.Forms.Padding CardInner = new System.Windows.Forms.Padding(14, 12, 14, 12);
            internal static readonly System.Windows.Forms.Padding SummaryPanel = new System.Windows.Forms.Padding(12, 8, 12, 8);
            internal static readonly System.Windows.Forms.Padding BodyPanel = new System.Windows.Forms.Padding(15, 12, 15, 15);
            internal static readonly System.Windows.Forms.Padding GridCard = new System.Windows.Forms.Padding(10);
            internal static readonly System.Windows.Forms.Padding Pagination = new System.Windows.Forms.Padding(12, 5, 12, 5);
        }
    }
}
