using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.WPF.RequestManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.RequestManagement.Views
{
    /// <summary>
    /// Code-behind for PartiallyFulfilledRequestsView.
    /// Handles VM wiring, filter/sort ComboBox two-way sync, DataGrid row-click
    /// bubbling, and the fulfill dialog launch.
    /// </summary>
    public partial class PartiallyFulfilledRequestsView : UserControl
    {
        private PartiallyFulfilledRequestsViewModel _vm;

        public PartiallyFulfilledRequestsView()
        {
            InitializeComponent();

            _vm = new PartiallyFulfilledRequestsViewModel();
            DataContext = _vm;

            FilterByBox.SelectionChanged += (s, e) =>
            {
                if (FilterByBox.SelectedItem is ComboBoxItem item)
                    _vm.FilterBy = item.Content?.ToString() ?? "All Fields";
            };
            FilterByBox.SelectedIndex = 0;

            SortByBox.SelectionChanged += (s, e) =>
            {
                if (SortByBox.SelectedItem is ComboBoxItem item)
                    _vm.SortBy = item.Content?.ToString() ?? "Set # (A→Z)";
            };
            SortByBox.SelectedIndex = 0;

            _vm.FiltersReset += () =>
            {
                FilterByBox.SelectedIndex = 0;
                SortByBox.SelectedIndex = 0;
            };

            _vm.FulfillRequested += OnFulfillRequested;

            Loaded   += OnLoaded;
            Unloaded += OnUnloaded;

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
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is RequestFulfillmentRowViewModel row)
                _vm.SelectedRow = row;
        }

        private void OnFulfillRequested(List<RequestFulfillmentRowViewModel> pendingRows)
        {
            var states = _vm.BuildFulfillStates(pendingRows);

            var dialog = new FulfillRequestDialog(states)
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
                MessageBox.Show($"Error fulfilling request:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
