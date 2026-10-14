using System.Drawing;

namespace Yakult.Inventory.App.Helpers
{
    internal static class UiTheme
    {
        internal static class Colors
        {
            internal static readonly Color HeaderBack = ColorTranslator.FromHtml("#F6F5F2");
            internal static readonly Color CardBack = Color.White;
            internal static readonly Color NavigationBack = ColorTranslator.FromHtml("#182235");
            internal static readonly Color NavigationHover = ColorTranslator.FromHtml("#29364D");
            internal static readonly Color Border = ColorTranslator.FromHtml("#E5E6E8");
            internal static readonly Color InputBorder = ColorTranslator.FromHtml("#CDD0D6");

            internal static readonly Color Primary = ColorTranslator.FromHtml("#B92335");
            internal static readonly Color PrimaryHover = ColorTranslator.FromHtml("#9E1E2E");

            internal static readonly Color Danger = Color.FromArgb(231, 76, 60);
            internal static readonly Color DangerHoverBack = Color.FromArgb(255, 240, 240);

            internal static readonly Color OutlineHoverBack = ColorTranslator.FromHtml("#FCF0F1");

            internal static readonly Color TextDark = ColorTranslator.FromHtml("#202635");
            internal static readonly Color TextMuted = ColorTranslator.FromHtml("#677081");

            internal static readonly Color GridHeaderBack = NavigationBack;
            internal static readonly Color GridAltRowBack = ColorTranslator.FromHtml("#F5F6F8");
            internal static readonly Color GridSelectionBack = ColorTranslator.FromHtml("#F8DDE2");
            internal static readonly Color GridSelectionFore = TextDark;
        }

        internal static class WpfColors
        {
            internal static readonly System.Windows.Media.Color Primary = Convert(Colors.Primary);
            internal static readonly System.Windows.Media.Color PrimaryHover = Convert(Colors.PrimaryHover);
            internal static readonly System.Windows.Media.Color PageBackground = Convert(Colors.HeaderBack);
            internal static readonly System.Windows.Media.Color Border = Convert(Colors.Border);
            internal static readonly System.Windows.Media.Color InputBorder = Convert(Colors.InputBorder);
            internal static readonly System.Windows.Media.Color TextDark = Convert(Colors.TextDark);
            internal static readonly System.Windows.Media.Color TextMuted = Convert(Colors.TextMuted);
            internal static readonly System.Windows.Media.Color AccentTint = Convert(Colors.OutlineHoverBack);
            internal static readonly System.Windows.Media.Color Selection = Convert(Colors.GridSelectionBack);
            internal static readonly System.Windows.Media.Color SurfaceMuted = Convert(Colors.GridAltRowBack);
            private static System.Windows.Media.Color Convert(Color color) => System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
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
