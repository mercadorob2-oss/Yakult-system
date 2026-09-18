using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Dialogs
{
    public partial class PrepareTransmittalDialog : Window
    {
        public PrepareTransmittalViewModel ViewModel { get; }

        private bool _suppressDropDown;

        // Filtering + drop-down re-open is deferred until typing pauses briefly. Running
        // it on every keystroke against a large employee list (Received By pulls the full
        // active roster) was blocking the UI thread long enough that keystrokes appeared
        // dropped and the caret-restore in SetDropDownOpenPreservingCaret raced against
        // the next character, making the caret appear to "lock".
        private static readonly TimeSpan SearchDebounceDelay = TimeSpan.FromMilliseconds(150);
        private readonly DispatcherTimer _notedByDebounce;
        private readonly DispatcherTimer _approvedByDebounce;
        private readonly DispatcherTimer _receivedByDebounce;

        public PrepareTransmittalDialog(CartridgeTransmittalViewModel transmittal)
        {
            ViewModel   = new PrepareTransmittalViewModel(transmittal);
            DataContext = ViewModel;
            _suppressDropDown = true;   // hold open until Loaded fires
            InitializeComponent();

            _notedByDebounce     = new DispatcherTimer { Interval = SearchDebounceDelay };
            _approvedByDebounce  = new DispatcherTimer { Interval = SearchDebounceDelay };
            _receivedByDebounce  = new DispatcherTimer { Interval = SearchDebounceDelay };
            _notedByDebounce.Tick    += (_, __) => { _notedByDebounce.Stop();    ApplyNotedByFilter(); };
            _approvedByDebounce.Tick += (_, __) => { _approvedByDebounce.Stop(); ApplyApprovedByFilter(); };
            _receivedByDebounce.Tick += (_, __) => { _receivedByDebounce.Stop(); ApplyReceivedByFilter(); };

            // Seed the editable ComboBox text without triggering the drop-down open logic.
            if (ViewModel.SelectedReceivedBy != null)
                _receivedByCombo.Text = ViewModel.SelectedReceivedBy.Name;
            else if (!string.IsNullOrWhiteSpace(ViewModel.ReceivedBySearch))
                _receivedByCombo.Text = ViewModel.ReceivedBySearch;

            if (ViewModel.SelectedNotedBy != null)
                _notedByCombo.Text = ViewModel.SelectedNotedBy.Name;
            else if (!string.IsNullOrWhiteSpace(ViewModel.NotedBySearch))
                _notedByCombo.Text = ViewModel.NotedBySearch;

            if (ViewModel.SelectedApprovedBy != null)
                _approvedByCombo.Text = ViewModel.SelectedApprovedBy.Name;
            else if (!string.IsNullOrWhiteSpace(ViewModel.ApprovedBySearch))
                _approvedByCombo.Text = ViewModel.ApprovedBySearch;

            // Release the guard only after WPF has finished its full initialization
            // pass (binding evaluation, focus, layout). Without this, deferred
            // TextChanged notifications fire after _suppressDropDown is already false.
            Loaded += (_, __) =>
            {
                _receivedByCombo.IsDropDownOpen = false;
                _notedByCombo.IsDropDownOpen    = false;
                _approvedByCombo.IsDropDownOpen = false;
                _suppressDropDown = false;
            };
        }

        // ── Searchable ComboBox text handlers ──────────────────────────────────
        // Filtering is view-specific behaviour — kept in code-behind intentionally.

        private void OnNotedByTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressDropDown) return;
            _notedByDebounce.Stop();
            _notedByDebounce.Start();
        }

        private void OnApprovedByTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressDropDown) return;
            _approvedByDebounce.Stop();
            _approvedByDebounce.Start();
        }

        private void OnReceivedByTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressDropDown) return;
            _receivedByDebounce.Stop();
            _receivedByDebounce.Start();
        }

        private void ApplyNotedByFilter()
        {
            if (DataContext is PrepareTransmittalViewModel vm)
            {
                vm.NotedBySearch = _notedByCombo.Text;
                SetDropDownOpenPreservingCaret(_notedByCombo, !string.IsNullOrEmpty(_notedByCombo.Text));
            }
        }

        private void ApplyApprovedByFilter()
        {
            if (DataContext is PrepareTransmittalViewModel vm)
            {
                vm.ApprovedBySearch = _approvedByCombo.Text;
                SetDropDownOpenPreservingCaret(_approvedByCombo, !string.IsNullOrEmpty(_approvedByCombo.Text));
            }
        }

        private void ApplyReceivedByFilter()
        {
            if (DataContext is PrepareTransmittalViewModel vm)
            {
                vm.ReceivedBySearch = _receivedByCombo.Text;
                SetDropDownOpenPreservingCaret(_receivedByCombo, !string.IsNullOrEmpty(_receivedByCombo.Text));
            }
        }

        // Opening/closing the drop-down re-templates the ComboBox's internal editable
        // TextBox, which resets its caret to the start and selects all its text. If that
        // happens mid-keystroke (e.g. the very first character, which transitions the
        // drop-down from closed to open), the next character the user types overwrites
        // the selection instead of appending — so the first character is silently lost.
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

        private void OnContinue(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void OnSkip(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
