using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Pages.Renewal;
using Yakult.Inventory.App.WPF.Renewal.RenewalDetail.Views;
using Yakult.Inventory.App.WPF.Renewal.RenewalGroups.ViewModels;
using Yakult.Inventory.App.WPF.Renewal.RenewalWorkspace.Views;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalGroups.Views
{
    public partial class RenewalGroupPageView : UserControl
    {
        private readonly RenewalGroupPageViewModel _vm;
        private readonly System.Collections.Generic.Dictionary<string, Button> _columnHeaderButtons = new System.Collections.Generic.Dictionary<string, Button>();

        public RenewalGroupPageView()
        {
            InitializeComponent();

            _vm = new RenewalGroupPageViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.RequestViewRenewalDetail += setId =>
            {
                new RenewalDetailWindow(setId).ShowDialog();
                _vm.LoadGroups();
            };
            _vm.RequestManageRenewalItems += setId =>
            {
                var workspace = new RenewalWorkspaceWindow(setId);
                var owner = GetOwner();
                if (owner != null)
                    new System.Windows.Interop.WindowInteropHelper(workspace).Owner = owner.Handle;
                workspace.ShowDialog();
                _vm.LoadGroups();
            };
            _vm.RequestShowHistory += group =>
            {
                var win = new RenewalHistoryWindow(group, _vm.GetItemNames);
                win.ShowDialog();
            };
            _vm.RequestShowReportOptionsDialog += OnRequestShowReportOptionsDialog;
            _vm.RequestGenerateReport += (rootSetIds, filter, includeOriginal, excludedSetIds) =>
            {
                if (rootSetIds != null) ReportLauncher.GenerateRenewalReport(rootSetIds, filter, includeOriginal, excludedSetIds);
                else ReportLauncher.GenerateRenewalReport(includeOriginal);
            };

            RegisterColumnHeaderButtons();
            _vm.LoadGroups();
        }

        public void ApplyInitialSearch(string query) => _vm.ApplyInitialSearch(query);

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void MoreDocumentDates_Click(object sender, MouseButtonEventArgs e)
        {
            var dlg = new Yakult.Inventory.App.WPF.Shared.Views.DocumentDateOverflowWindow(_vm.DocumentDateFilterRows, _vm.AddDocumentDateRowCommand);
            var owner = GetOwner();
            if (owner != null)
                new System.Windows.Interop.WindowInteropHelper(dlg).Owner = owner.Handle;
            dlg.ShowDialog();
        }

        private void RegisterColumnHeaderButtons()
        {
            foreach (var button in FindColumnHeaderButtons(this))
                _columnHeaderButtons[(string)button.Tag] = button;
        }

        private static System.Collections.Generic.IEnumerable<Button> FindColumnHeaderButtons(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is Button btn && btn.Tag is string)
                    yield return btn;
                foreach (var nested in FindColumnHeaderButtons(child))
                    yield return nested;
            }
        }

        private void OnRequestShowReportOptionsDialog()
        {
            using (var opts = new RenewalReportOptionsDialog())
            {
                if (opts.ShowDialog(GetOwner()) != WinForms.DialogResult.OK) return;
                _vm.CommitGenerateReport(opts.IncludeOriginalSets);
            }
        }

        private void CardHeader_Click(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is RenewalGroupRow row)
                _vm.ToggleExpandCommand.Execute(row);
        }

        /// <summary>Ticking/unticking a chain-row checkbox implicitly opts its whole chain into
        /// the report, since a chain whose root card checkbox was never manually checked is
        /// invisible to CommitGenerateReport, which then falls back to including every chain
        /// (the root cause of reports showing every group instead of the checked selection).</summary>
        private void ChildCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject d && FindAncestor<ItemsControl>(d)?.DataContext is RenewalGroupRow group)
                group.Selected = true;
        }

        private static T FindAncestor<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
                parent = VisualTreeHelper.GetParent(parent);
            return parent as T;
        }

        private void ColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var columnKey = button?.Tag as string;
            if (string.IsNullOrEmpty(columnKey)) return;

            var distinctValues = _vm.GetDistinctColumnValues(columnKey);
            var currentFilter = _vm.GetCurrentColumnFilter(columnKey);

            using (var popup = new ColumnFilterPopup(button.Content?.ToString()?.TrimEnd(' ', '▾'), distinctValues, currentFilter))
            {
                popup.Location = WinForms.Cursor.Position;
                popup.ShowDialog(GetOwner());

                switch (popup.Action)
                {
                    case ColumnFilterPopup.PopupAction.SortAscending:
                        _vm.ApplyColumnSort(columnKey, true);
                        break;
                    case ColumnFilterPopup.PopupAction.SortDescending:
                        _vm.ApplyColumnSort(columnKey, false);
                        break;
                    case ColumnFilterPopup.PopupAction.Filter:
                        _vm.ApplyColumnFilter(columnKey, popup.SelectedValues, distinctValues.Count);
                        break;
                }
            }

            UpdateColumnHeaderColor(columnKey);
        }

        private void UpdateColumnHeaderColor(string columnKey)
        {
            if (_columnHeaderButtons.TryGetValue(columnKey, out var button))
                button.Foreground = _vm.HasColumnFilter(columnKey)
                    ? new SolidColorBrush(Color.FromRgb(59, 130, 246))
                    : (Brush)FindResource("TextMutedBrush");
        }

        private void GroupCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // Set explicitly from the checkbox's own (guaranteed-current) IsChecked rather than
            // trusting the TwoWay binding already pushed it by the time this event fires — see
            // RequestPageView's RowSelectCheckBox_Changed for the full explanation.
            if (sender is CheckBox cb && cb.DataContext is RenewalGroupRow group)
                group.Selected = cb.IsChecked == true;

            _vm.RefreshSelectionState();
        }

        private void SelectedBadge_Click(object sender, RoutedEventArgs e)
        {
            var summaries = _vm.GetSelectedGroups().Select(g => new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowSummary
            {
                Id = g.RootSetId,
                Values = new[] { g.RootSetId.ToString(), g.RootSetCode, g.CompanyName, g.OverallStatus }
            });

            var dialog = new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowsReviewDialog(
                "Selected Renewal Groups", "renewal groups",
                new[] { "Root ID", "Set Code", "Company", "Status" },
                summaries);
            SetWpfOwner(dialog);
            dialog.ShowDialog();

            if (dialog.RemovedIds.Count > 0)
            {
                var removed = new System.Collections.Generic.HashSet<int>(dialog.RemovedIds);
                foreach (var group in _vm.GetSelectedGroups())
                    if (removed.Contains(group.RootSetId))
                        group.Selected = false;
            }
        }

        private static void SetWpfOwner(Window window)
        {
            var owner = GetOwner();
            if (owner != null) new System.Windows.Interop.WindowInteropHelper(window).Owner = owner.Handle;
        }
    }

    /// <summary>Maps SetLevelStatus/ExpiryStatus text to its foreground color — same palette as
    /// the original StatusColor() helper.</summary>
    internal sealed class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
        {
            switch (value as string)
            {
                case "Expired": return new SolidColorBrush(Color.FromRgb(185, 28, 28));
                case "Expiring Soon": return new SolidColorBrush(Color.FromRgb(180, 83, 0));
                case "Warning": return new SolidColorBrush(Color.FromRgb(146, 109, 0));
                case "Active": return new SolidColorBrush(Color.FromRgb(21, 128, 61));
                case "Fully Renewed":
                case "Partially Renewed": return new SolidColorBrush(Color.FromRgb(29, 78, 216));
                default: return new SolidColorBrush(Color.FromRgb(71, 85, 105));
            }
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture)
            => throw new System.NotSupportedException();
    }

    /// <summary>Expanded card header gets a light-blue tint, matching the original's ClrHdrBgEx.</summary>
    internal sealed class BoolToExpandedBrushConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? new SolidColorBrush(Color.FromRgb(239, 246, 255)) : new SolidColorBrush(Color.FromRgb(248, 250, 252));

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture)
            => throw new System.NotSupportedException();
    }

    internal sealed class BoolToWeightConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? FontWeights.Bold : FontWeights.Normal;

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture)
            => throw new System.NotSupportedException();
    }
}
