using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Detail.Views
{
    /// <summary>Reported Problem section: renders ObservationCardViewModel rows (inline
    /// Edit/Save/Cancel, Delete, Move Up/Down) plus a single-click "+ Add Observation" box.
    /// Bound explicitly via dependency properties so it's reusable/testable like the other
    /// Detail-window controls.</summary>
    public partial class ReportedProblemControl : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource), typeof(IEnumerable), typeof(ReportedProblemControl), new PropertyMetadata(null));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty HasObservationsProperty = DependencyProperty.Register(
            nameof(HasObservations), typeof(bool), typeof(ReportedProblemControl), new PropertyMetadata(true));

        public bool HasObservations
        {
            get => (bool)GetValue(HasObservationsProperty);
            set => SetValue(HasObservationsProperty, value);
        }

        public static readonly DependencyProperty NewObservationTextProperty = DependencyProperty.Register(
            nameof(NewObservationText), typeof(string), typeof(ReportedProblemControl), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string NewObservationText
        {
            get => (string)GetValue(NewObservationTextProperty);
            set => SetValue(NewObservationTextProperty, value);
        }

        public static readonly DependencyProperty AddObservationCommandProperty = DependencyProperty.Register(
            nameof(AddObservationCommand), typeof(ICommand), typeof(ReportedProblemControl), new PropertyMetadata(null));

        public ICommand AddObservationCommand
        {
            get => (ICommand)GetValue(AddObservationCommandProperty);
            set => SetValue(AddObservationCommandProperty, value);
        }

        public ReportedProblemControl()
        {
            InitializeComponent();
        }

        /// <summary>Clicking "Edit" just swaps the card's Visibility bindings (read-only text ->
        /// edit TextBox) — WPF does not move keyboard focus into a newly-visible control on its
        /// own, so without this the TextBox renders (with a blinking caret) but never actually
        /// receives keystrokes. Focus it (and select all, so typing replaces the existing text)
        /// the moment it becomes visible.</summary>
        private void EditTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox tb && tb.IsVisible)
            {
                // DispatcherPriority.Input can still run before the Edit button's own click
                // finishes assigning focus to itself, leaving the button (not this TextBox) with
                // keyboard focus despite Focus() having "succeeded" moments earlier. Use
                // ContextIdle — after all pending input/layout has settled — so this genuinely
                // runs last and wins. Also explicitly re-activate the owning Window: WPF's logical
                // Keyboard.Focus() only translates into actually-typeable characters if the Window
                // itself currently has Win32 activation.
                //
                // ClearFocus() before re-establishing focus works around a WPF quirk where logical
                // focus (FocusManager) and keyboard focus (Keyboard.FocusedElement) can get out of
                // sync after a rapid show/hide + reactivate sequence — the caret renders and the
                // control visually looks focused (IsKeyboardFocusWithin true) but WM_CHAR-driven
                // TextInput doesn't actually route to it, so typed characters silently do nothing
                // (only KeyDown-level edits like Delete/Backspace, which don't depend on that same
                // TextInput routing, appear to work). Forcing a clear-then-refocus resets both to
                // point at the same element.
                tb.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    var window = Window.GetWindow(tb);
                    if (window != null && !window.IsActive) window.Activate();

                    Keyboard.ClearFocus();
                    FocusManager.SetFocusedElement(FocusManager.GetFocusScope(tb), tb);
                    Keyboard.Focus(tb);
                    tb.Focus();
                    tb.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        }
    }
}
