using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages.Software;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.WPF.Renewal.History
{
    public partial class AddLicenseHistoricalPeriodWindow : Window
    {
        private readonly int _setId;
        private readonly LicenseHistoricalPeriodContext _context;
        private readonly LicenseHistoricalPeriodRepository _repository = new LicenseHistoricalPeriodRepository();
        private readonly ObservableCollection<HistoricalItemRow> _items = new ObservableCollection<HistoricalItemRow>();
        private bool _saving;

        public AddLicenseHistoricalPeriodWindow(int setId, LicenseHistoricalPeriodContext context)
        {
            _setId = setId;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            InitializeComponent();
            ItemsGrid.ItemsSource = _items;
            CoverageHint.Text = $"Enter coverage before {context.FirstRecordedStartDate:MMM d, yyyy} and the items included during that period.";
            if (context.FirstRecordedStartDate.HasValue && context.FirstRecordedStartDate.Value.Year > 1)
            {
                StartDatePicker.DisplayDateEnd = context.FirstRecordedStartDate.Value.Date.AddDays(-1);
                EndDatePicker.DisplayDateEnd = StartDatePicker.DisplayDateEnd;
                EndDatePicker.SelectedDate = context.FirstRecordedStartDate.Value.Date.AddDays(-1);
                StartDatePicker.SelectedDate = context.FirstRecordedStartDate.Value.Date.AddYears(-1);
            }
            CopyInvoiceItems();
            UpdateSummary();
            Closing += OnClosing;
        }

        private void OnCopyItems(object sender, RoutedEventArgs e)
        {
            if (_items.Count > 0 && MessageBox.Show(this, "Replace the draft item list with this invoice's items?",
                    "Copy Invoice Items", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            CopyInvoiceItems();
        }

        private void CopyInvoiceItems()
        {
            foreach (var row in _items) row.PropertyChanged -= OnItemChanged;
            _items.Clear();
            foreach (var item in _context.CurrentItems)
                AddRow(new HistoricalItemRow(item));
            UpdateSummary();
        }

        private void AddRow(HistoricalItemRow row)
        {
            row.PropertyChanged += OnItemChanged;
            _items.Add(row);
            UpdateSummary();
        }

        private void OnItemChanged(object sender, PropertyChangedEventArgs e) => UpdateSummary();

        private void OnCheckAll(object sender, RoutedEventArgs e)
        {
            foreach (var row in _items) row.Included = true;
        }

        private void OnUncheckAll(object sender, RoutedEventArgs e)
        {
            foreach (var row in _items) row.Included = false;
        }

        private void OnCoverageChanged(object sender, SelectionChangedEventArgs e) => UpdateSummary();
        private void OnAmountChanged(object sender, TextChangedEventArgs e) => UpdateSummary();

        private void OnCalculateEndDate(object sender, RoutedEventArgs e)
        {
            ValidationMessage.Text = "";
            if (!StartDatePicker.SelectedDate.HasValue || !int.TryParse(YearsBox.Text, out int years) || years <= 0)
            {
                ValidationMessage.Text = "Enter a coverage start date and a positive whole number of years.";
                return;
            }
            var start = StartDatePicker.SelectedDate.Value.Date;
            if (years > 9999 - start.Year)
            {
                ValidationMessage.Text = "The number of years is outside the supported date range.";
                return;
            }
            var end = start.AddYears(years).AddDays(-1);
            if (_context.FirstRecordedStartDate.HasValue && end >= _context.FirstRecordedStartDate.Value.Date)
            {
                ValidationMessage.Text = $"Coverage must end before {_context.FirstRecordedStartDate:MMM d, yyyy}.";
                return;
            }
            EndDatePicker.SelectedDate = end;
        }

        private void UpdateSummary()
        {
            // XAML change events can fire before all named controls are initialized.
            if (PeriodSummary == null || ItemSelectionSummary == null || AmountSummary == null) return;
            var included = _items.Where(row => row.Included).ToList();
            decimal quantity = 0m;
            bool overflow = false;
            try { quantity = included.Sum(row => row.Quantity); }
            catch (OverflowException) { overflow = true; }
            ItemSelectionSummary.Text = $"{included.Count} of {_items.Count} items checked" +
                (overflow ? ". Check quantities for values outside the supported range." : $" · Total quantity: {quantity:N2}");
            PeriodSummary.Text = StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue
                ? $"{StartDatePicker.SelectedDate:MMM d, yyyy} – {EndDatePicker.SelectedDate:MMM d, yyyy}\n{included.Count} covered items"
                : $"Choose coverage dates\n{included.Count} covered items";
            AmountSummary.Text = string.IsNullOrWhiteSpace(AmountBox.Text) ? "Amount not recorded"
                : decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amount)
                    ? $"₱{amount:N2}" : "Enter a valid amount";
        }

        private void OnAddTextItem(object sender, RoutedEventArgs e)
        {
            var item = new HistoricalItemRow(new LicenseHistoricalPeriodItemDto());
            AddRow(item);
            ItemsGrid.SelectedItem = item;
            ItemsGrid.ScrollIntoView(item);
            ItemsGrid.CurrentCell = new DataGridCellInfo(item, ItemsGrid.Columns[2]);
            ItemsGrid.Focus();
            ItemsGrid.BeginEdit();
        }

        private void OnRemoveItems(object sender, RoutedEventArgs e)
        {
            foreach (var item in ItemsGrid.SelectedItems.Cast<HistoricalItemRow>().ToList())
            {
                item.PropertyChanged -= OnItemChanged;
                _items.Remove(item);
            }
            UpdateSummary();
        }

        private void OnAddCatalogItems(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var picker = new SoftwareItemPickerDialog(_items.Where(i => i.ItemId.HasValue).Select(i => i.ItemId.Value)))
                {
                    picker.Owner = this;
                    if (picker.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    foreach (var item in picker.SelectedItems)
                        AddRow(new HistoricalItemRow(new LicenseHistoricalPeriodItemDto
                        {
                            ItemId = item.ItemId,
                            ItemName = string.IsNullOrWhiteSpace(item.Description) ? item.Name : item.Description,
                            Quantity = 1m
                        }));
                }
            }
            catch (Exception ex) { ValidationMessage.Text = "Unable to select catalog items: " + ex.Message; }
        }

        private async void OnSave(object sender, RoutedEventArgs e)
        {
            if (_saving) return;
            ValidationMessage.Text = "";
            if (!ItemsGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true))
            {
                ValidationMessage.Text = "Correct the highlighted item values before saving.";
                return;
            }
            if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
            {
                ValidationMessage.Text = "Enter both coverage dates.";
                return;
            }
            decimal? amount = null;
            if (!string.IsNullOrWhiteSpace(AmountBox.Text))
            {
                if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed))
                {
                    ValidationMessage.Text = "Enter a valid amount, or leave it blank when unknown.";
                    return;
                }
                amount = parsed;
            }
            var period = new LicenseHistoricalPeriodDto
            {
                StartDate = StartDatePicker.SelectedDate.Value.Date,
                EndDate = EndDatePicker.SelectedDate.Value.Date,
                ReferenceNumber = ReferenceBox.Text,
                Amount = amount,
                Notes = NotesBox.Text,
                Items = _items.Where(row => row.Included).Select(row => row.ToSnapshot()).ToList()
            };
            try { LicenseHistoricalPeriodRepository.Validate(period); }
            catch (Exception ex)
            {
                ValidationMessage.Text = ex.Message;
                return;
            }
            _saving = true;
            Editor.IsEnabled = false;
            SaveButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            SaveButton.Content = "Saving…";
            try
            {
                int userId = AppSession.CurrentUserId;
                try { await Task.Run(() => _repository.Create(_setId, period, userId)); }
                catch (HistoricalPeriodOverlapException ex)
                {
                    if (MessageBox.Show(this, ex.Message, "Overlapping Coverage", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                    await Task.Run(() => _repository.Create(_setId, period, userId, allowOverlap: true));
                }
                _saving = false;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ValidationMessage.Text = "Unable to save the past period: " + ex.Message;
                System.Diagnostics.Debug.WriteLine("[AddLicenseHistoricalPeriod] " + ex);
            }
            finally
            {
                _saving = false;
                Editor.IsEnabled = true;
                SaveButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
                SaveButton.Content = "Save Previous Renewal";
            }
        }

        private void OnClosing(object sender, CancelEventArgs e) { if (_saving) e.Cancel = true; }

        public sealed class HistoricalItemRow : INotifyPropertyChanged
        {
            private bool _included = true;
            private string _itemName;
            private string _itemCode;
            private decimal _quantity;

            public HistoricalItemRow(LicenseHistoricalPeriodItemDto item)
            {
                ItemId = item.ItemId;
                _itemName = item.ItemName;
                _itemCode = item.ItemCode;
                _quantity = item.Quantity > 0 ? item.Quantity : 1m;
            }

            public int? ItemId { get; }
            public string SourceDisplay => ItemId.HasValue ? "Catalog item" : "Historical item";
            public bool Included
            {
                get => _included;
                set { if (_included == value) return; _included = value; Changed(nameof(Included)); }
            }
            public string ItemName
            {
                get => _itemName;
                set { if (_itemName == value) return; _itemName = value; Changed(nameof(ItemName)); }
            }
            public string ItemCode
            {
                get => _itemCode;
                set { if (_itemCode == value) return; _itemCode = value; Changed(nameof(ItemCode)); }
            }
            public decimal Quantity
            {
                get => _quantity;
                set { if (_quantity == value) return; _quantity = value; Changed(nameof(Quantity)); }
            }
            public LicenseHistoricalPeriodItemDto ToSnapshot() => new LicenseHistoricalPeriodItemDto
            {
                ItemId = ItemId, ItemName = ItemName, ItemCode = ItemCode, Quantity = Quantity
            };

            public event PropertyChangedEventHandler PropertyChanged;
            private void Changed(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
