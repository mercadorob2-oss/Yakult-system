using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.RequestManagement.ViewModels;
using Yakult.Inventory.App.WPF.Set.RequisitionForm.Services;
using Yakult.Inventory.App.WPF.Set.RequisitionForm.ViewModels;

namespace Yakult.Inventory.App.WPF.RequestManagement.Views
{
    /// <summary>
    /// Code-behind for MixedRequestExchangeView. Business logic lives in
    /// MixedRequestExchangeViewModel; this file only wires the view model, loads the data
    /// and forwards a click on a request row to the view model's SelectGroupCommand.
    /// </summary>
    public partial class MixedRequestExchangeView : UserControl
    {
        private readonly MixedRequestExchangeViewModel _vm;

        public MixedRequestExchangeView()
        {
            InitializeComponent();

            // The number spinner's styles live in a resource dictionary. Merge it after
            // InitializeComponent (Application.Current is null when hosted in WinForms via ElementHost).
            Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new System.Uri(
                    "pack://application:,,,/Yakult.Inventory.App;component/WPF/CartridgeManagement/Controls/IntegerSpinner.xaml",
                    System.UriKind.Absolute)
            });

            _vm = new MixedRequestExchangeViewModel();
            DataContext = _vm;

            Loaded   += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Request rows live inside the ItemsControl data template, so listen for clicks here.
            AddHandler(DataGrid.MouseDownEvent,
                new MouseButtonEventHandler(OnGridMouseDown), handledEventsToo: true);

            _vm.FulfillmentCompleted += OnFulfillmentCompleted;

            await _vm.InitializeAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            RemoveHandler(DataGrid.MouseDownEvent, new MouseButtonEventHandler(OnGridMouseDown));
            if (_vm != null)
                _vm.FulfillmentCompleted -= OnFulfillmentCompleted;
            _vm?.Dispose();
        }

        // After fulfilling, show the same printable requisition form the Set Details "Requisition"
        // button opens: prepare the signatories, then the print preview.
        private async void OnFulfillmentCompleted(int? setId, Guid sessionId)
        {
            if (!setId.HasValue)
            {
                MessageBox.Show(
                    "This request is not linked to a set yet, so there is no requisition form to print.",
                    "No Requisition Form", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var repo   = new SetRepository();
                var setDto = await repo.GetSetByIdAsync(setId.Value);
                if (setDto == null)
                {
                    MessageBox.Show("Set not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var requests = await repo.GetSetRequestsAsync(setId.Value);
                var employeeDetail = requests.Count > 0
                    ? await repo.GetEmployeeDetailsForRequestAsync(requests.OrderBy(r => r.ReqId).First().ReqId)
                    : await repo.GetEmployeeDetailsForSetAsync(setId.Value);
                var documentItems = requests.Count > 0
                    ? requests
                    : await repo.GetSetItemsAsRequestsAsync(setId.Value);

                var form = RequisitionFormViewModel.FromSet(setDto, documentItems, employeeDetail);

                // Noted By defaults to whoever approved this submission (the manager or
                // supervisor). Best effort: if it cannot be resolved the field just starts blank.
                try
                {
                    var approver = await new CartridgeAuthorizationRepository().GetApproverForSessionAsync(sessionId);
                    if (approver.HasValue)
                    {
                        form.NotedByName     = approver.Value.Name;
                        form.NotedByPosition = approver.Value.Position;
                    }
                }
                catch { }

                Mouse.OverrideCursor = null;

                // No WPF owner window here (this page is hosted in WinForms). Own the dialogs by
                // the hosting portal form explicitly: relying on Form.ActiveForm left the modal
                // dialog ownerless (and hidden behind the disabled portal) whenever the portal
                // was not the active window right after the success message, freezing the app.
                RequisitionFormPrintService.ShowPrintDialog(form, GetHostFormHandle());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open requisition form:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Handle of the top-level WinForms form hosting this page through its ElementHost
        /// (e.g. the Request &amp; Set Management portal), or IntPtr.Zero if it cannot be found.
        /// </summary>
        private IntPtr GetHostFormHandle()
        {
            if (!(PresentationSource.FromVisual(this) is System.Windows.Interop.HwndSource source))
                return IntPtr.Zero;

            var control = System.Windows.Forms.Control.FromChildHandle(source.Handle);
            var topLevel = control?.TopLevelControl ?? control?.FindForm();
            return topLevel != null && topLevel.IsHandleCreated ? topLevel.Handle : IntPtr.Zero;
        }

        private void OnGridMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!(e.OriginalSource is DependencyObject source)) return;

            var row = FindVisualParent<DataGridRow>(source);
            if (row?.DataContext is MixedLineViewModel line && _vm.SelectGroupCommand.CanExecute(line.Group))
                _vm.SelectGroupCommand.Execute(line.Group);
        }

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
