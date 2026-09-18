using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Yakult.Inventory.App.WPF.NotificationCenter.Controls
{
    [ContentProperty(nameof(CardContent))]
    public partial class NotificationCardControl : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty CardTitleProperty =
            DependencyProperty.Register(nameof(CardTitle), typeof(string), typeof(NotificationCardControl),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(NotificationCardControl),
                new PropertyMetadata(Brushes.SteelBlue));

        public static readonly DependencyProperty ViewAllCommandProperty =
            DependencyProperty.Register(nameof(ViewAllCommand), typeof(ICommand), typeof(NotificationCardControl));

        public static readonly DependencyProperty ExtraHeaderTextProperty =
            DependencyProperty.Register(nameof(ExtraHeaderText), typeof(string), typeof(NotificationCardControl),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ExtraHeaderColorProperty =
            DependencyProperty.Register(nameof(ExtraHeaderColor), typeof(Brush), typeof(NotificationCardControl),
                new PropertyMetadata(Brushes.DarkGreen));

        public static readonly DependencyProperty ExtraHeaderVisibleProperty =
            DependencyProperty.Register(nameof(ExtraHeaderVisible), typeof(bool), typeof(NotificationCardControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty CardContentProperty =
            DependencyProperty.Register(nameof(CardContent), typeof(object), typeof(NotificationCardControl));

        public string   CardTitle        { get => (string)GetValue(CardTitleProperty);        set => SetValue(CardTitleProperty, value); }
        public Brush    AccentBrush      { get => (Brush)GetValue(AccentBrushProperty);       set => SetValue(AccentBrushProperty, value); }
        public ICommand ViewAllCommand   { get => (ICommand)GetValue(ViewAllCommandProperty); set => SetValue(ViewAllCommandProperty, value); }
        public string   ExtraHeaderText  { get => (string)GetValue(ExtraHeaderTextProperty);  set => SetValue(ExtraHeaderTextProperty, value); }
        public Brush    ExtraHeaderColor { get => (Brush)GetValue(ExtraHeaderColorProperty);  set => SetValue(ExtraHeaderColorProperty, value); }
        public bool     ExtraHeaderVisible { get => (bool)GetValue(ExtraHeaderVisibleProperty); set => SetValue(ExtraHeaderVisibleProperty, value); }
        public object   CardContent      { get => GetValue(CardContentProperty);               set => SetValue(CardContentProperty, value); }

        public NotificationCardControl()
        {
            InitializeComponent();
        }
    }
}
