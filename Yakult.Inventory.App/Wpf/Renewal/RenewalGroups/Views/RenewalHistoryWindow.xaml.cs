using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Yakult.Inventory.App.WPF.Renewal.RenewalGroups.ViewModels;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalGroups.Views
{
    public partial class RenewalHistoryWindow : Window
    {
        public string HeaderTitle { get; }
        public string HeaderSubtitle { get; }
        public List<RenewalHistoryEntryVm> Entries { get; }

        public RenewalHistoryWindow(RenewalGroupRow group, System.Func<int, List<string>> itemNamesGetter)
        {
            InitializeComponent();

            Title = $"Renewal History — {group.RootSetCode}";
            HeaderTitle = $"Renewal Chain for  {group.RootSetCode}  ·  {group.CompanyName}";
            HeaderSubtitle = $"{group.SetsCountDisplay} in chain  ·  {group.SetType}";
            Entries = RenewalHistoryEntryVm.BuildFor(group, itemNamesGetter);

            DataContext = this;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }

    // StatusToBrushConverter is defined once in RenewalGroupPageView.xaml.cs (same namespace) and
    // reused here via the shared local: xmlns prefix — no need to redefine it for this Window.

    /// <summary>True → accent blue (e.g. "Latest" / active), False → muted gray.</summary>
    internal sealed class BoolToMutedBrushConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? new SolidColorBrush(Color.FromRgb(59, 130, 246)) : new SolidColorBrush(Color.FromRgb(148, 163, 184));

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture)
            => throw new System.NotSupportedException();
    }

    internal sealed class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
            => (value is int count && count > 0) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture)
            => throw new System.NotSupportedException();
    }
}
