using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs
{
    public partial class ColumnFilterWindow : Window
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

        private readonly List<FilterItem>                 _allItems;
        private readonly ObservableCollection<FilterItem> _displayItems = new ObservableCollection<FilterItem>();

        public bool            ClearFilter    { get; private set; }
        public HashSet<string> SelectedValues { get; private set; }

        public ColumnFilterWindow(string columnName, List<string> allValues, HashSet<string> currentFilter)
        {
            InitializeComponent();
            TxtHeader.Text = "Filter: " + columnName;

            _allItems = allValues
                .Select(v => new FilterItem
                {
                    Label     = v,
                    IsChecked = currentFilter == null || currentFilter.Contains(v)
                })
                .ToList();

            FilterList.ItemsSource = _displayItems;
            RefreshDisplayList(string.Empty);
            UpdateCount();
        }

        private void RefreshDisplayList(string search)
        {
            var trimmed = (search ?? "").Trim().ToLowerInvariant();
            _displayItems.Clear();
            foreach (var item in _allItems)
            {
                if (string.IsNullOrEmpty(trimmed) || item.Label.ToLowerInvariant().Contains(trimmed))
                    _displayItems.Add(item);
            }
            UpdateCount();
        }

        private void UpdateCount()
        {
            int visibleSelected = _displayItems.Count(i => i.IsChecked);
            TxtCount.Text = $"{visibleSelected} of {_displayItems.Count} selected";
        }

        private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => RefreshDisplayList(TxtSearch.Text);

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _displayItems) item.IsChecked = true;
            UpdateCount();
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _displayItems) item.IsChecked = false;
            UpdateCount();
        }

        private void AnyCheckBox_Changed(object sender, RoutedEventArgs e)
            => UpdateCount();

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            SelectedValues = new HashSet<string>(
                _displayItems.Where(i => i.IsChecked).Select(i => i.Label),
                System.StringComparer.OrdinalIgnoreCase);
            DialogResult = true;
        }

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            ClearFilter  = true;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
