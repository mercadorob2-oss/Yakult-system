using System;
using System.Windows;
using System.Windows.Input;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Wpf.RepairPortal.NewTicketIntake.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Wpf.RepairPortal.NewTicketIntake.Views
{
    public partial class NewRepairTicketDialog : Window
    {
        private readonly NewRepairTicketViewModel _vm;
        private readonly IRepairTicketRepository _repository;

        public event Action<RepairTicketListItem> TicketCreated;

        /// <summary>The created ticket is exposed to IT Call Monitoring after the operator selects
        /// Create Ticket, so the parent call is never marked forwarded when the intake is cancelled.</summary>
        public RepairTicketListItem CreatedTicket { get; private set; }

        public NewRepairTicketDialog(IRepairTicketRepository repository)
        {
            InitializeComponent();

            _repository = repository;
            _vm = new NewRepairTicketViewModel(repository);
            DataContext = _vm;

            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.TicketCreated += ticket =>
            {
                CreatedTicket = ticket;
                TicketCreated?.Invoke(ticket);
            };
            _vm.RequestClose += result => { DialogResult = result; Close(); };

            InitDateReceivedPicker();
        }

        /// <summary>Switches this standard intake window into IT Call forwarding mode and fills
        /// its existing item, problem, priority, and Requested By fields before it is shown.</summary>
        public async System.Threading.Tasks.Task PrefillForCallTicketForwardingAsync(NewRepairTicketRequest request)
        {
            Title = "Forward IT Call to Repair";
            await _vm.PrefillForCallTicketForwardingAsync(request);
        }

        /// <summary>Opens the real inventory-system Add Item dialog (not a stripped-down copy) so
        /// a newly logged damaged item is properly recorded system-wide — full fields, warranty,
        /// vendor, etc. — not just visible from the Repair Portal. On success, the newest item it
        /// created is looked up and selected in this dialog's item picker automatically.</summary>
        private async void AddItemButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Yakult.Inventory.App.Pages.Item.BatchAddItemDialog { Owner = this };
            if (dialog.ShowDialog() == WinForms.DialogResult.OK && dialog.AddedItemIds.Count > 0)
            {
                var lookup = await _repository.GetItemLookupByIdAsync(dialog.AddedItemIds[dialog.AddedItemIds.Count - 1]);
                if (lookup != null)
                    _vm.SetSelectedItemFromCreation(lookup);
            }
        }

        /// <summary>Clicking the already-selected item toggles it back off — a plain ListBox never
        /// lets you deselect by clicking the current selection again, it just re-fires the same
        /// selection. Intercepted at the tunneling (Preview) phase, before the ListBoxItem's own
        /// selection logic runs, so we can override it for this one case only.</summary>
        private void ItemRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is RepairableItemLookup item
                && ReferenceEquals(_vm.SelectedItem, item))
            {
                _vm.SelectedItem = null;
                e.Handled = true;
            }
        }

        /// <summary>WPF's DatePicker is date-only; Date Received needs a time component too
        /// (matches CompletionDatePromptForm's DateTimePicker format/config).</summary>
        private void InitDateReceivedPicker()
        {
            var picker = new WinForms.DateTimePicker
            {
                Format = WinForms.DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy  h:mm tt",
                ShowUpDown = false,
                Value = _vm.SelectedDateReceived,
                Dock = WinForms.DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9.5F)
            };
            picker.ValueChanged += (s, e) => _vm.SelectedDateReceived = picker.Value;

            var host = new System.Windows.Forms.Integration.WindowsFormsHost { Child = picker };
            DateReceivedHost.Child = host;

            // WindowsFormsHost can show a segment as "selected" on click (WPF logical focus)
            // without actually transferring real Win32 keyboard focus to the hosted control, so
            // typed digits never reach it even though the segment highlights. Force real focus
            // explicitly on both WPF-level focus and every click.
            host.GotFocus += (s, e) => picker.Focus();
            host.PreviewMouseDown += (s, e) => picker.Focus();
        }
    }
}
