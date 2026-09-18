using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs
{
    public partial class ExportFilterDialog : Window
    {
        public class FilterItem : INotifyPropertyChanged
        {
            public string Label { get; set; }

            private bool _isChecked;
            public bool IsChecked
            {
                get => _isChecked;
                set { _isChecked = value; OnPropertyChanged(); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public HashSet<string> SelectedBranches { get; private set; }
        public bool            IncludeArchived  { get; private set; }

        private readonly List<FilterItem>                _allBranchItems;
        private readonly ObservableCollection<FilterItem> _displayItems = new ObservableCollection<FilterItem>();
        private readonly int _totalEmployees;
        private readonly int _archivedEmployees;

        public ExportFilterDialog(List<string> allBranches, int totalEmployees, int archivedEmployees)
        {
            InitializeComponent();

            _totalEmployees    = totalEmployees;
            _archivedEmployees = archivedEmployees;

            _allBranchItems = allBranches
                .OrderBy(b => b)
                .Select(b => new FilterItem { Label = b, IsChecked = true })
                .ToList();

            BranchList.ItemsSource = _displayItems;
            RefreshDisplayList(string.Empty);
            UpdateCounts();
        }

        private void RefreshDisplayList(string search)
        {
            var trimmed = (search ?? "").Trim().ToLowerInvariant();
            _displayItems.Clear();
            foreach (var item in _allBranchItems)
            {
                if (string.IsNullOrEmpty(trimmed) || item.Label.ToLowerInvariant().Contains(trimmed))
                    _displayItems.Add(item);
            }
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            int selectedBranches = _allBranchItems.Count(i => i.IsChecked);
            TxtBranchCount.Text = $"{selectedBranches} of {_allBranchItems.Count} branches selected";

            TxtMatchCount.Text = ChkIncludeArchived != null && ChkIncludeArchived.IsChecked == true
                ? $"{_totalEmployees} employee(s) total, including {_archivedEmployees} archived."
                : $"{_totalEmployees - _archivedEmployees} active employee(s) will be considered (archived excluded).";
        }

        private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => RefreshDisplayList(TxtSearch.Text);

        private void BtnSelectAllBranches_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _displayItems) item.IsChecked = true;
            UpdateCounts();
        }

        private void BtnClearAllBranches_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _displayItems) item.IsChecked = false;
            UpdateCounts();
        }

        private void AnyCheckBox_Changed(object sender, RoutedEventArgs e)
            => UpdateCounts();

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            SelectedBranches = new HashSet<string>(
                _allBranchItems.Where(i => i.IsChecked).Select(i => i.Label),
                System.StringComparer.OrdinalIgnoreCase);
            IncludeArchived = ChkIncludeArchived.IsChecked == true;

            if (SelectedBranches.Count == 0)
            {
                MessageBox.Show("Select at least one branch to export.", "No Branches Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
        }
    }
}
