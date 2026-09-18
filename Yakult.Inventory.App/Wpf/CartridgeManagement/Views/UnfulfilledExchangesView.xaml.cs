using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Views
{
    public partial class UnfulfilledExchangesView : UserControl
    {
        private UnfulfilledExchangesViewModel _vm;

        public UnfulfilledExchangesView()
        {
            InitializeComponent();

            _vm = new UnfulfilledExchangesViewModel();
            DataContext = _vm;

            // Filter by ComboBox
            FilterByBox.SelectionChanged += (s, e) =>
            {
                if (FilterByBox.SelectedItem is ComboBoxItem item)
                    _vm.FilterBy = item.Content?.ToString() ?? "All Fields";
            };
            FilterByBox.SelectedIndex = 0;

            // Sort by ComboBox
            SortByBox.SelectionChanged += (s, e) =>
            {
                if (SortByBox.SelectedItem is ComboBoxItem item)
                    _vm.SortBy = item.Content?.ToString() ?? "Set # (A→Z)";
            };
            SortByBox.SelectedIndex = 0;

            // Status filter ComboBox — default to "Pending" (index 1)
            StatusFilterBox.SelectionChanged += (s, e) =>
            {
                if (StatusFilterBox.SelectedItem is ComboBoxItem item)
                    _vm.StatusFilter = item.Content?.ToString() ?? "Pending";
            };
            StatusFilterBox.SelectedIndex = 1; // "Pending"

            _vm.FulfillRequested += OnFulfillRequested;

            Loaded   += OnLoaded;
            Unloaded += OnUnloaded;

            // Bubble DataGrid selection up to the VM
            AddHandler(DataGrid.SelectionChangedEvent,
                new SelectionChangedEventHandler(OnGridSelectionChanged),
                handledEventsToo: true);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _vm.LoadData();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null)
                _vm.FulfillRequested -= OnFulfillRequested;
            _vm?.Dispose();
        }

        private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is PfExchangeRowViewModel row)
                _vm.SelectedRow = row;
        }

        private void OnFulfillRequested(List<PfExchangeRowViewModel> pendingRows)
        {
            var states = _vm.BuildFulfillStates(pendingRows);

            var dialog = new FulfillExchangeDialog(states)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true) return;

            if (dialog.AllStatesEmpty)
            {
                MessageBox.Show("No quantity entered to issue.", "Nothing to Issue",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                _vm.CommitFulfillment(states);
                MessageBox.Show("Fulfillment completed successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                _vm.SelectedRow = null;
                _vm.LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fulfilling exchange:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
