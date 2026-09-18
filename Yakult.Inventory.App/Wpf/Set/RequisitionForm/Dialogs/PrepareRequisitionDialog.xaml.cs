using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Yakult.Inventory.App.WPF.Set.RequisitionForm.ViewModels;

namespace Yakult.Inventory.App.WPF.Set.RequisitionForm.Dialogs
{
    public partial class PrepareRequisitionDialog : Window
    {
        public PrepareRequisitionViewModel ViewModel { get; }

        private bool _suppressDropDown;

        public PrepareRequisitionDialog(RequisitionFormViewModel requisition)
        {
            ViewModel   = new PrepareRequisitionViewModel(requisition);
            DataContext = ViewModel;
            _suppressDropDown = true;   // hold open until Loaded fires
            InitializeComponent();

            // Seed each editable ComboBox's text without triggering the drop-down open
            // logic (guarded by _suppressDropDown above).
            if (ViewModel.SelectedPreparedBy != null)
                _preparedByCombo.Text = ViewModel.SelectedPreparedBy.Name;
            if (ViewModel.SelectedNotedBy != null)
                _notedByCombo.Text = ViewModel.SelectedNotedBy.Name;
            if (ViewModel.SelectedApprovedBy != null)
                _approvedByCombo.Text = ViewModel.SelectedApprovedBy.Name;
            if (ViewModel.SelectedReceivedBy != null)
                _receivedByCombo.Text = ViewModel.SelectedReceivedBy.Name;
            else if (!string.IsNullOrWhiteSpace(ViewModel.ReceivedBySearch))
                _receivedByCombo.Text = ViewModel.ReceivedBySearch;

            // Release the guard only after WPF has finished its full initialization
            // pass (binding evaluation, focus, layout). Without this, deferred
            // TextChanged notifications fire after _suppressDropDown is already false.
            Loaded += (_, __) =>
            {
                _preparedByCombo.IsDropDownOpen = false;
                _notedByCombo.IsDropDownOpen = false;
                _approvedByCombo.IsDropDownOpen = false;
                _receivedByCombo.IsDropDownOpen = false;
                _suppressDropDown = false;
            };
        }

        // ── Searchable ComboBox text handlers ──────────────────────────────────
        // Filtering is view-specific behaviour — kept in code-behind intentionally.

        private void OnPreparedByTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressDropDown) return;
            if (sender is ComboBox cb && DataContext is PrepareRequisitionViewModel vm)
                HandleSearchTextChanged(cb, text => vm.PreparedBySearch = text);
        }

        private void OnNotedByTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressDropDown) return;
            if (sender is ComboBox cb && DataContext is PrepareRequisitionViewModel vm)
                HandleSearchTextChanged(cb, text => vm.NotedBySearch = text);
        }

        private void OnApprovedByTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressDropDown) return;
            if (sender is ComboBox cb && DataContext is PrepareRequisitionViewModel vm)
                HandleSearchTextChanged(cb, text => vm.ApprovedBySearch = text);
        }

        private void OnReceivedByTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressDropDown) return;
            if (sender is ComboBox cb && DataContext is PrepareRequisitionViewModel vm)
                HandleSearchTextChanged(cb, text => vm.ReceivedBySearch = text);
        }

        // Re-opening (or re-closing) a ComboBox's popup on every single keystroke —
        // even when it's already in the state being requested — is what causes two
        // symptoms: the editable TextBox part select-alls its text as part of the
        // popup's open sequence (so the next keystroke types over/erases everything
        // instead of appending), and rapid popup open/close thrash can leave the
        // mouse cursor looking "locked" while the Win32 popup grabs/releases capture.
        //
        // Fixing both: only touch IsDropDownOpen on an actual empty <-> non-empty
        // transition, and defer that toggle (plus the caret fix) to run after the
        // current keystroke has finished being processed rather than fighting it
        // synchronously inside the TextChanged handler.
        private void HandleSearchTextChanged(ComboBox cb, Action<string> applySearch)
        {
            string text = cb.Text;

            // WPF syncs an editable ComboBox's Text to match SelectedItem whenever
            // selection changes — including arrow-key navigation through the already
            // open, filtered list, which is not the user typing. Feeding that synced
            // Text back into the search filter re-filters down to just that one
            // person, wiping out every other result the user was still browsing and
            // making it look like the field is "stuck" on Backspace. Detect it: if
            // Text now exactly matches the current SelectedItem's name, this change
            // came from selection, not typing, so leave the search text alone.
            if (cb.SelectedItem is EmployeeOption selected &&
                string.Equals(selected.Name, text, StringComparison.Ordinal))
            {
                return;
            }

            // A real edit (not the selection-sync echo caught above) invalidates whatever
            // was previously selected — otherwise ApplyTo() keeps reading the stale
            // SelectedItem instead of the edited/cleared text, making it look like erasing
            // the field (e.g. clearing a preset Noted By / Approved By name) has no effect.
            if (cb.SelectedItem != null)
                cb.SelectedItem = null;

            applySearch(text);

            bool shouldOpen = !string.IsNullOrEmpty(text);
            if (cb.IsDropDownOpen == shouldOpen) return;

            var textBox = cb.Template?.FindName("PART_EditableTextBox", cb) as TextBox;
            int caret = text.Length;

            cb.Dispatcher.BeginInvoke(new Action(() =>
            {
                cb.IsDropDownOpen = shouldOpen;
                if (textBox != null)
                {
                    textBox.SelectionStart  = caret;
                    textBox.SelectionLength = 0;
                }
            }), DispatcherPriority.Background);
        }

        // The employee list can be large enough that fully realizing it (even
        // virtualized, the popup still has to lay out and measure) stalls the UI for
        // a moment — so the drop-down should only open on a deliberate click of the
        // arrow button, not on every click into the text area just to place the caret
        // or give it focus. WPF's own click-to-open logic can't tell those apart, so
        // undo an open that didn't originate from the toggle button.
        //
        // Popup content (the dropdown's ComboBoxItems) is stitched into the ComboBox's
        // logical tree, so PreviewMouseLeftButtonDown on an item bubbles/tunnels through
        // this same handler too — not just clicks on the text field. Forcing the popup
        // closed on that click was cancelling the selection before it could commit,
        // which is why clicking a result did nothing. Only suppress-open for clicks
        // that land in the editable text area itself, not on an item in the open list.
        private void OnComboFieldPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is ComboBox cb)) return;
            if (e.OriginalSource is DependencyObject source &&
                (FindAncestor<ToggleButton>(source) != null || FindAncestor<ComboBoxItem>(source) != null))
                return; // arrow button, or a click on a dropdown item — let it commit normally

            cb.Dispatcher.BeginInvoke(new Action(() => cb.IsDropDownOpen = false), DispatcherPriority.Input);
        }

        // ComboBox's built-in key handling moves SelectedItem (via Up/Down) even while
        // the popup is closed — landing on whatever item is first/next in the list,
        // completely independent of what the user just typed to filter it. That's what
        // makes an unrelated name "keep showing" after pressing Down, and once
        // SelectedItem snaps like that the resulting Text fights further typing and
        // Backspace/Delete. If the popup is already open, arrowing through the visible
        // (filtered) list to pick an item is normal and expected, so only intercept the
        // very first Up/Down press that would otherwise open it.
        private void OnComboFieldPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is ComboBox cb)) return;
            if ((e.Key != Key.Down && e.Key != Key.Up) || cb.IsDropDownOpen) return;

            cb.IsDropDownOpen = true;
            e.Handled = true;
        }

        private static T FindAncestor<T>(DependencyObject node) where T : DependencyObject
        {
            while (node != null)
            {
                if (node is T match) return match;
                node = (node is Visual || node is System.Windows.Media.Media3D.Visual3D)
                    ? VisualTreeHelper.GetParent(node)
                    : LogicalTreeHelper.GetParent(node);
            }
            return null;
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
