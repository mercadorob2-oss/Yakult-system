using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    /// <summary>
    /// Shared WPF theme resources used across all ITCM workspace controls.
    /// Centralizes scrollbar styling and the GlassCard factory to prevent design drift.
    /// </summary>
    internal static class WpfThemeResources
    {
        // Shared with the inventory shell. Keep ticket states and chart series separate.
        public static readonly Color Primary = UiTheme.WpfColors.Primary;
        public static readonly Color PrimaryHover = UiTheme.WpfColors.PrimaryHover;
        public static readonly Color Navigation = Color.FromRgb(24, 34, 53);
        public static readonly Color NavigationHover = Color.FromRgb(41, 54, 77);
        public static readonly Color NavigationMuted = Color.FromRgb(184, 195, 212);
        public static readonly Color PageBackground = UiTheme.WpfColors.PageBackground;
        public static readonly Color SurfaceMuted = UiTheme.WpfColors.SurfaceMuted;
        public static readonly Color AccentTint = UiTheme.WpfColors.AccentTint;
        public static readonly Color AccentBorder = Color.FromRgb(231, 184, 193);
        public static readonly Color Selection = UiTheme.WpfColors.Selection;
        public static readonly Color Border = UiTheme.WpfColors.Border;
        public static readonly Color TextDark = UiTheme.WpfColors.TextDark;
        public static readonly Color TextMuted = UiTheme.WpfColors.TextMuted;

        public static readonly SolidColorBrush PrimaryBrush = new SolidColorBrush(Primary);
        public static readonly SolidColorBrush PrimaryHoverBrush = new SolidColorBrush(PrimaryHover);
        public static readonly SolidColorBrush PageBackgroundBrush = new SolidColorBrush(PageBackground);
        public static readonly SolidColorBrush SurfaceMutedBrush = new SolidColorBrush(SurfaceMuted);
        public static readonly SolidColorBrush AccentTintBrush = new SolidColorBrush(AccentTint);
        public static readonly SolidColorBrush AccentBorderBrush = new SolidColorBrush(AccentBorder);
        public static readonly SolidColorBrush SelectionBrush = new SolidColorBrush(Selection);
        public static readonly SolidColorBrush BorderBrush = new SolidColorBrush(Border);
        public static readonly SolidColorBrush TextDarkBrush = new SolidColorBrush(TextDark);
        public static readonly SolidColorBrush TextMutedBrush = new SolidColorBrush(TextMuted);

        // --------------------------------------------------------------------------
        // Scrollbar
        // --------------------------------------------------------------------------

        private static ResourceDictionary _scrollBarStyle;

        /// <summary>
        /// Returns a <see cref="ResourceDictionary"/> containing a slim, modern scrollbar style.
        /// The dictionary is created once and reused — call
        /// <c>Resources.MergedDictionaries.Add(WpfThemeResources.GetScrollBarStyle())</c>
        /// inside each workspace constructor.
        /// </summary>
        public static ResourceDictionary GetScrollBarStyle()
        {
            if (_scrollBarStyle != null)
                return _scrollBarStyle;

            const string xaml = @"
                <ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                    <Style TargetType=""ScrollBar"">
                        <Setter Property=""Background"" Value=""Transparent""/>
                        <Setter Property=""Width"" Value=""10""/>
                        <Setter Property=""Template"">
                            <Setter.Value>
                                <ControlTemplate TargetType=""ScrollBar"">
                                    <Border Background=""Transparent"">
                                        <Track x:Name=""PART_Track"" IsDirectionReversed=""true"">
                                            <Track.Thumb>
                                                <Thumb>
                                                    <Thumb.Template>
                                                        <ControlTemplate TargetType=""Thumb"">
                                                            <Border Background=""#cbd5e1"" CornerRadius=""3"" Margin=""2,0,2,0""/>
                                                        </ControlTemplate>
                                                    </Thumb.Template>
                                                </Thumb>
                                            </Track.Thumb>
                                        </Track>
                                    </Border>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>
                </ResourceDictionary>";

            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            _scrollBarStyle = (ResourceDictionary)XamlReader.Parse(xaml, ctx);
            return _scrollBarStyle;
        }

        // --------------------------------------------------------------------------
        // GlassCard
        // --------------------------------------------------------------------------

        /// <summary>
        /// Creates a standard white GlassCard border with rounded corners, a subtle border,
        /// and a soft drop shadow. Use this as the outer container for all dashboard cards.
        /// </summary>
        public static Border CreateGlassCard(double padding = 22)
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(248, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(223, 229, 236)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(24),
                Padding = new Thickness(padding),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 18,
                    Color = Color.FromArgb(30, 15, 23, 42),
                    ShadowDepth = 0,
                    Opacity = 0.2
                }
            };
        }

        // --------------------------------------------------------------------------
        // Color helper
        // --------------------------------------------------------------------------

        public static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
            => new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}
