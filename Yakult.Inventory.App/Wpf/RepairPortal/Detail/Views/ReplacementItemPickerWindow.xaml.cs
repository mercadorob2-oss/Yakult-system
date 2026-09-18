using System;
using System.Windows;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Detail.Views
{
    /// <summary>Search-and-pick dialog for the "Replace" disposition — a paginated DataGrid (Item
    /// ID, Name, Model Number, Serial Number, Date Created, Created By) matching the Items page's
    /// table styling (ListDataGridChrome). A blank search shows the default browse list: active
    /// same-category items not currently assigned to any set (i.e. free stock); typing searches
    /// Name/Serial/Model within the category. Excludes the broken item itself.</summary>
    public partial class ReplacementItemPickerWindow : Window
    {
        private const int PageSize = 25;

        private readonly IRepairTicketRepository _repository;
        private readonly int _excludeItemId;

        private int _currentPage = 1;
        private int _totalPages = 1;
        private int _searchToken;

        public int? SelectedItemId { get; private set; }

        public ReplacementItemPickerWindow(IRepairTicketRepository repository, int excludeItemId)
        {
            InitializeComponent();

            _repository = repository ?? new RepairTicketRepository();
            _excludeItemId = excludeItemId;

            PagingBar.FirstPageCommand = new RelayCommand(() => { _currentPage = 1; _ = LoadPageAsync(); }, () => _currentPage > 1);
            PagingBar.PrevPageCommand = new RelayCommand(() => { _currentPage--; _ = LoadPageAsync(); }, () => _currentPage > 1);
            PagingBar.NextPageCommand = new RelayCommand(() => { _currentPage++; _ = LoadPageAsync(); }, () => _currentPage < _totalPages);
            PagingBar.LastPageCommand = new RelayCommand(() => { _currentPage = _totalPages; _ = LoadPageAsync(); }, () => _currentPage < _totalPages);

            Loaded += (s, e) => _ = LoadPageAsync();
        }

        private async void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _currentPage = 1;
            await LoadPageAsync();
        }

        private async System.Threading.Tasks.Task LoadPageAsync()
        {
            var token = ++_searchToken;
            var query = string.IsNullOrWhiteSpace(SearchBox.Text) ? null : SearchBox.Text.Trim();

            ResultsGrid.ItemsSource = null;
            OkButton.IsEnabled = false;

            RepairableItemLookupPage page;
            try
            {
                page = await _repository.SearchReplacementCandidatesAsync(query, _excludeItemId, _currentPage, PageSize);
            }
            catch
            {
                // Non-critical — the grid just stays empty if a search fails.
                return;
            }

            if (token != _searchToken) return; // A newer keystroke's search has since superseded this one.

            ResultsGrid.ItemsSource = page.Items;
            _totalPages = Math.Max(1, (int)Math.Ceiling(page.TotalCount / (double)PageSize));
            PagingBar.PageInfoText = $"Page {_currentPage} / {_totalPages} • {page.TotalCount} item(s)";
        }

        private void ResultsGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            OkButton.IsEnabled = ResultsGrid.SelectedItem is RepairableItemLookup;
        }

        private void ResultsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ResultsGrid.SelectedItem is RepairableItemLookup)
                Ok_Click(sender, e);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsGrid.SelectedItem is RepairableItemLookup item)
                SelectedItemId = item.ItemId;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
