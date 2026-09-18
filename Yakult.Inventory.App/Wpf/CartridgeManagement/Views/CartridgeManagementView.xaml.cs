using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Yakult.Inventory.App.Forms.CartridgeManagement;
using Yakult.Inventory.App.WPF.CartridgeManagement.Services;
using Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Views
{
    /// <summary>
    /// Minimal code-behind for CartridgeManagementView.
    /// All business logic lives in CartridgeManagementViewModel.
    /// This file handles only:
    ///   - ViewModel wiring and initial async load
    ///   - DataGrid row click → SelectRequestCommand delegation
    ///   - DataGrid double-click → OpenRequestCommand delegation
    /// </summary>
    public partial class CartridgeManagementView : UserControl
    {
        private CartridgeManagementViewModel _vm;

        public CartridgeManagementView()
        {
            InitializeComponent();

            // Merge the IntegerSpinner resource dictionary into this UserControl's resources
            // after InitializeComponent so the pack URI scheme is registered.
            // We avoid Application.Current because it is null when hosted in WinForms via ElementHost.
            var spinnerDict = new System.Windows.ResourceDictionary
            {
                Source = new System.Uri(
                    "pack://application:,,,/Yakult.Inventory.App;component/WPF/CartridgeManagement/Controls/IntegerSpinner.xaml",
                    System.UriKind.Absolute)
            };
            Resources.MergedDictionaries.Add(spinnerDict);

            _vm = new CartridgeManagementViewModel();
            DataContext = _vm;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await _vm.InitializeAsync();
            WireSessionBlockEvents();
            _vm.FulfillmentCompleted += OnFulfillmentCompleted;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_vm != null)
                _vm.FulfillmentCompleted -= OnFulfillmentCompleted;
            _vm?.Dispose();
        }

        private void OnFulfillmentCompleted(object sender, CartridgeTransmittalViewModel transmittalVm)
        {
            // Step 1: Show print transmittal dialog (blocks until user prints or skips).
            // Cancel/Skip/closing the Prepare Transmittal form itself returns false —
            // stay on this page instead of jumping to Send Notifications.
            bool reachedPrintPreview = TransmittalPrintService.ShowPrintDialog(transmittalVm);
            if (!reachedPrintPreview) return;

            // Step 2: Navigate to Send Notifications on the portal form
            var portalForm = System.Windows.Forms.Application.OpenForms
                .OfType<CartridgeManagementPortalForm>()
                .FirstOrDefault();
            portalForm?.NavigateToSendNotifications();
        }

        // ── Wire DataGrid row clicks inside session blocks ────────────────────
        // Because the session DataGrids are inside an ItemsControl DataTemplate
        // we use the Bubbled MouseDown event to find the row and request DTO.
        private void WireSessionBlockEvents()
        {
            // DataGrids inside the ItemsControl raise mouse events that bubble up.
            // We listen at the ItemsControl level using event routing.
            AddHandler(DataGrid.MouseDownEvent,
                new MouseButtonEventHandler(OnSessionGridMouseDown), handledEventsToo: true);

            AddHandler(DataGrid.MouseDoubleClickEvent,
                new MouseButtonEventHandler(OnSessionGridDoubleClick), handledEventsToo: true);
        }

        private void OnSessionGridMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                var row = FindVisualParent<DataGridRow>(source);
                if (row?.DataContext is CartridgeRequestDto dto)
                {
                    if (_vm.SelectRequestCommand.CanExecute(dto))
                        _vm.SelectRequestCommand.Execute(dto);
                }
            }
        }

        private void OnSessionGridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                var row = FindVisualParent<DataGridRow>(source);
                if (row?.DataContext is CartridgeRequestDto)
                {
                    if (_vm.OpenRequestCommand.CanExecute(null))
                        _vm.OpenRequestCommand.Execute(null);
                }
            }
        }

        // ── Visual tree helper ────────────────────────────────────────────────
        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T t) return t;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}
