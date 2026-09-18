using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Views
{
    /// <summary>
    /// Code-behind for PartiallyFulfilledView.
    /// Handles VM wiring, filter/sort ComboBox two-way sync (done via SelectionChanged
    /// since the items are ComboBoxItems, not raw strings), DataGrid row-click bubbling,
    /// and the fulfill dialog launch.
    /// </summary>
    public partial class PartiallyFulfilledView : UserControl
    {
        private PartiallyFulfilledViewModel _vm;

        public PartiallyFulfilledView()
        {
            InitializeComponent();

            _vm = new PartiallyFulfilledViewModel();
            DataContext = _vm;

            // Sync filter/sort combo boxes to the VM
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

            // Subscribe to the fulfill event so we can open the WPF dialog
            _vm.FulfillRequested += OnFulfillRequested;

            Loaded   += OnLoaded;
            Unloaded += OnUnloaded;

            // DataGrid selection bubbles up — listen at the UserControl level
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

        // ── DataGrid row selection ────────────────────────────────────────────────
        private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Only update when a row is being added to the selection (not cleared)
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is PfExchangeRowViewModel row)
                _vm.SelectedRow = row;
        }

        // ── Fulfill dialog ────────────────────────────────────────────────────────
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
