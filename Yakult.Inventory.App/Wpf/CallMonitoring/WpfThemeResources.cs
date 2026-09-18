using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    /// <summary>
    /// Shared WPF theme resources used across all ITCM workspace controls.
    /// Centralizes scrollbar styling and the GlassCard factory to prevent design drift.
    /// </summary>
    internal static class WpfThemeResources
    {
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
