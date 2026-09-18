using System;
using System.Windows.Controls;
using Yakult.Inventory.App.WPF.RequestPortal.AssistedRequest.ViewModels;

namespace Yakult.Inventory.App.WPF.RequestPortal.AssistedRequest.Views
{
    public partial class AssistedRequestView : UserControl
    {
        public AssistedRequestViewModel ViewModel { get; }

        // ── Tour targets — used by WpfPortalTourService to spotlight UI elements ──
        internal System.Windows.FrameworkElement TourTarget_AssistedBanner       => tourAssistedBanner;
        internal System.Windows.FrameworkElement TourTarget_TargetEmployeeCard   => tourTargetEmployeeCard;
        internal System.Windows.FrameworkElement TourTarget_EmpFilterRow         => tourEmpFilterRow;
        internal System.Windows.FrameworkElement TourTarget_CartridgeSelection   => tourCartridgeSelectionCard;
        internal System.Windows.FrameworkElement TourTarget_RequestItemsCard     => tourRequestItemsCard;
        internal System.Windows.FrameworkElement TourTarget_DestinationCard      => tourDestinationCard;
        internal System.Windows.FrameworkElement TourTarget_RbFilterRow          => tourRbFilterRow;
        internal System.Windows.FrameworkElement TourTarget_ManualAuthCard       => tourManualAuthCard;
        internal System.Windows.FrameworkElement TourTarget_AuthorizedByRow      => tourAuthorizedByRow;
        internal System.Windows.FrameworkElement TourTarget_DecisionRow          => tourDecisionRow;
        internal System.Windows.FrameworkElement TourTarget_AuthRemarksRow       => tourAuthRemarksRow;
        internal System.Windows.FrameworkElement TourTarget_ActionBar            => tourActionBar;

        private bool _suppressDropDown;

        public AssistedRequestView(AssistedRequestViewModel vm)
        {
            InitializeComponent();
            ViewModel   = vm;
            DataContext = vm;
            Loaded     += async (s, e) => await vm.LoadDataAsync();
        }

        public AssistedRequestView() : this(new AssistedRequestViewModel()) { }

        // ── Searchable ComboBox text handlers ──────────────────────────────────
        // WPF's built-in ComboBox TextSearch only prefix-matches against the start
        // of DisplayMemberPath, so typing anything but the employee's first name
        // fails to filter. IsTextSearchEnabled is disabled on these ComboBoxes and
        // replaced with substring/multi-term matching via SearchTextHelper in the
        // view model (see EmployeeSearchText / ReceivedBySearchText / ApproverSearchText).

        private void OnSelectEmployeeTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressDropDown) return;
            if (sender is ComboBox cb && DataContext is AssistedRequestViewModel vm)
            {
                vm.EmployeeSearchText = cb.Text;
                SetDropDownOpenPreservingCaret(cb, !string.IsNullOrEmpty(cb.Text));
            }
        }

        private void OnReceivedByTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressDropDown) return;
            if (sender is ComboBox cb && DataContext is AssistedRequestViewModel vm)
            {
                vm.ReceivedBySearchText = cb.Text;
                SetDropDownOpenPreservingCaret(cb, !string.IsNullOrEmpty(cb.Text));
            }
        }

        private void OnAuthorizedByTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressDropDown) return;
            if (sender is ComboBox cb && DataContext is AssistedRequestViewModel vm)
            {
                vm.ApproverSearchText = cb.Text;
                SetDropDownOpenPreservingCaret(cb, !string.IsNullOrEmpty(cb.Text));
            }
        }

        // Opening/closing the drop-down re-templates the ComboBox's internal editable
        // TextBox, which resets its caret to the start and selects all its text. If that
        // happens mid-keystroke, the next character the user types overwrites the
        // selection instead of appending — so the first character is silently lost.
        // Deferring the caret/selection restore to after the drop-down toggle has been
        // fully processed fixes this without changing the search/filter behavior.
        private static void SetDropDownOpenPreservingCaret(ComboBox cb, bool open)
        {
            if (cb.IsDropDownOpen == open) return;

            int caret = cb.Text?.Length ?? 0;
            cb.IsDropDownOpen = open;

            cb.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (cb.Template?.FindName("PART_EditableTextBox", cb) is TextBox editableTextBox)
                {
                    editableTextBox.CaretIndex = caret;
                    editableTextBox.SelectionLength = 0;
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }
}
