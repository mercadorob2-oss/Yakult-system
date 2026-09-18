using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Software
{
    public partial class SoftwareServiceSetDialog : Window, IDisposable
    {
        private readonly ServiceSetRepository _repository;
        private readonly RenewalRepository _renewalRepository;
        private readonly ObservableCollection<ServiceSetItemRow> _itemRows;
        private bool _isEditMode;
        private int _editSetId;

        /// <summary>Set after Save creates a new invoice (null in edit mode, or if Save
        /// hasn't run yet) — read by callers such as the Invoice Preparation page that
        /// need to know which invoice was generated from the pre-filled group items.</summary>
        public int? CreatedSetId { get; private set; }

        // ── Constructors ─────────────────────────────────────────────────────
        public SoftwareServiceSetDialog()
        {
            InitializeComponent();
            _repository = new ServiceSetRepository();
            _renewalRepository = new RenewalRepository();
            _itemRows = new ObservableCollection<ServiceSetItemRow>();

            Loaded += OnLoaded;
        }

        public SoftwareServiceSetDialog(int setId) : this()
        {
            _isEditMode = true;
            _editSetId = setId;
        }

        // ── WinForms-compatible ShowDialog overloads ─────────────────────────
        public new WinForms.DialogResult ShowDialog()
        {
            bool? result = base.ShowDialog();
            return result == true ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }

        public WinForms.DialogResult ShowDialog(WinForms.IWin32Window owner)
        {
            if (owner != null)
                new System.Windows.Interop.WindowInteropHelper(this).Owner = owner.Handle;
            return ShowDialog();
        }

        // ── IDisposable (no-op — satisfies WinForms using-block callers) ────
        public void Dispose() { }

        // ── Loaded ───────────────────────────────────────────────────────────
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var itemsView = System.Windows.Data.CollectionViewSource.GetDefaultView(_itemRows);
            itemsView.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(ServiceSetItemRow.GroupLabel)));
            ItemsGrid.ItemsSource = itemsView;

            // Hook format before setting dates so the handler fires on every change
            HookDateFormat(DtpDocumentDate);
            HookDateFormat(DtpStartDate);
            HookDateFormat(DtpEndDate);

            DtpDocumentDate.SelectedDate = DateTime.Today;
            DtpStartDate.SelectedDate    = DateTime.Today;
            DtpEndDate.SelectedDate      = DateTime.Today.AddYears(1);
            ChkHasContractPeriod.IsChecked = true;

            LoadCompanyDropdown();
            LoadDistributorDropdown();

            if (_isEditMode)
                LoadExistingSet(_editSetId);
        }

        // ── Date format (MM/dd/yyyy with leading zeros) ───────────────────────
        private void HookDateFormat(System.Windows.Controls.DatePicker dp)
        {
            dp.SelectedDateChanged += (s, e) => ApplyDateFormat(dp);
        }

        private void ApplyDateFormat(System.Windows.Controls.DatePicker dp)
        {
            if (!dp.SelectedDate.HasValue) return;
            string text = dp.SelectedDate.Value.ToString("MM/dd/yyyy");
            Dispatcher.BeginInvoke((Action)(() =>
            {
                if (dp.Template?.FindName("PART_TextBox", dp) is
                    System.Windows.Controls.Primitives.DatePickerTextBox tb)
                    tb.Text = text;
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // ── Header drag / chrome ─────────────────────────────────────────────
        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void OnMinimizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Maximized)
            {
                var wa = SystemParameters.WorkArea;
                MaxWidth = wa.Width;
                MaxHeight = wa.Height;
                Left = wa.Left;
                Top = wa.Top;
                BtnMaximizeGlyph.Text = "❐";
            }
            else
            {
                MaxWidth = double.PositiveInfinity;
                MaxHeight = double.PositiveInfinity;
                BtnMaximizeGlyph.Text = "□";
            }
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            DialogResult = false;
        }

        // ── Company dropdown ─────────────────────────────────────────────────
        private void LoadCompanyDropdown()
        {
            try
            {
                var companies = _repository.GetAllCompanies();
                CmbCompany.Items.Clear();
                CmbCompany.Items.Add(new CompanyItem { ComId = 0, CompanyName = "(None)" });
                foreach (var c in companies)
                    CmbCompany.Items.Add(new CompanyItem { ComId = c.ComId, CompanyName = c.CompanyName });

                if (CmbCompany.Items.Count > 0)
                    CmbCompany.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading companies: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Distributor dropdown ─────────────────────────────────────────────
        private void LoadDistributorDropdown()
        {
            try
            {
                var distributors = _repository.GetAllDistributors();
                CmbDistributor.Items.Clear();
                CmbDistributor.Items.Add(new DistributorItem { DistributorId = 0, Name = "(None)" });
                foreach (var d in distributors)
                    CmbDistributor.Items.Add(new DistributorItem { DistributorId = d.DistributorId, Name = d.Name });

                if (CmbDistributor.Items.Count > 0)
                    CmbDistributor.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading distributors: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Contract period toggle ───────────────────────────────────────────
        private void ChkContractPeriod_Changed(object sender, RoutedEventArgs e)
        {
            bool enabled = ChkHasContractPeriod.IsChecked == true;
            DtpStartDate.IsEnabled = enabled;
            DtpEndDate.IsEnabled = enabled;
        }

        // ── Grid selection ───────────────────────────────────────────────────
        private void ItemsGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            BtnRemoveItem.IsEnabled = ItemsGrid.SelectedItem != null;
        }

        // ── Add Item ─────────────────────────────────────────────────────────
        private void BtnAddItem_Click(object sender, RoutedEventArgs e)
        {
            var existingIds = _itemRows.Select(r => r.ItemId).Where(id => id > 0).ToList();

            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var owner = new Win32WindowWrapper(hwnd);

            using (var picker = new SoftwareItemPickerDialog(existingIds, false))
            {
                if (picker.ShowDialog(owner) == WinForms.DialogResult.OK)
                {
                    foreach (var item in picker.SelectedItems)
                        AddItemToGrid(item);
                }
            }
        }

        /// <summary>Pre-fills the item grid from Sub-Type Group items (Invoice Preparation
        /// page's "Create an Invoice From Them") — bypasses <see cref="AddItemToGrid"/>
        /// since a group item is already resolved (name/description/quantity/price), not
        /// a raw <see cref="ItemDto"/> pick.</summary>
        public void PrefillItems(IEnumerable<Models.InvoicePreparationItemDto> items)
        {
            var itemList = items.ToList();

            foreach (var item in itemList)
            {
                if (_itemRows.Any(r => r.ItemId == item.ItemId)) continue;

                _itemRows.Add(new ServiceSetItemRow
                {
                    ItemId = item.ItemId,
                    ItemName = item.ItemName,
                    ItemCode = item.ItemId.ToString(),
                    PartNumber = item.PartNumber,
                    SubType = item.SubType,
                    ReferenceCode = item.ReferenceCode,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Uom = string.IsNullOrWhiteSpace(item.UnitOfMeasure) ? "Unit" : item.UnitOfMeasure,
                    DurationStart = item.BeginDate,
                    DurationEnd = item.EndDate
                });
            }
        }

        private void AddItemToGrid(ItemDto item)
        {
            if (_itemRows.Any(r => r.ItemId == item.ItemId))
            {
                var existing = _itemRows.First(r => r.ItemId == item.ItemId);
                ItemsGrid.SelectedItem = existing;
                ItemsGrid.ScrollIntoView(existing);
                return;
            }

            var defaultQty = item.StockOnHand > 0 ? (decimal)item.StockOnHand : 1m;

            _itemRows.Add(new ServiceSetItemRow
            {
                ItemId = item.ItemId,
                ItemName = item.Name,
                ItemCode = string.IsNullOrWhiteSpace(item.ModelNumber) ? item.ItemId.ToString() : item.ModelNumber,
                PartNumber = _renewalRepository.LoadPartNumberFromRenewals(item.ItemId),
                Description = item.Description,
                Quantity = defaultQty,
                UnitPrice = 0m,
                Uom = string.IsNullOrWhiteSpace(item.UnitOfMeasure) ? "Unit" : item.UnitOfMeasure,
                DurationStart = item.StartDate,
                DurationEnd = item.EndDate
            });
        }

        // ── Remove Item ──────────────────────────────────────────────────────
        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsGrid.SelectedItem is ServiceSetItemRow row)
                _itemRows.Remove(row);
        }

        // ── Load existing set (edit mode) ────────────────────────────────────
        private void LoadExistingSet(int setId)
        {
            try
            {
                var existing = _repository.GetSoftwareServiceSetById(setId);
                if (existing == null)
                {
                    MessageBox.Show("Service set not found. It may have been deleted.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    DialogResult = false;
                    return;
                }

                TxtDocumentNumber.Text = existing.DocumentNumber ?? string.Empty;
                TxtReferenceNumber.Text = existing.ReferenceNumber ?? string.Empty;

                if (existing.DocumentDate != default(DateTime))
                    DtpDocumentDate.SelectedDate = existing.DocumentDate;

                if (existing.StartDate.HasValue)
                    DtpStartDate.SelectedDate = existing.StartDate.Value;

                if (existing.EndDate.HasValue)
                    DtpEndDate.SelectedDate = existing.EndDate.Value;

                ChkHasContractPeriod.IsChecked = existing.StartDate.HasValue || existing.EndDate.HasValue;

                if (existing.ComId.HasValue)
                {
                    for (int i = 0; i < CmbCompany.Items.Count; i++)
                    {
                        if (CmbCompany.Items[i] is CompanyItem c && c.ComId == existing.ComId.Value)
                        {
                            CmbCompany.SelectedIndex = i;
                            break;
                        }
                    }
                }

                if (existing.DistributorId.HasValue)
                {
                    for (int i = 0; i < CmbDistributor.Items.Count; i++)
                    {
                        if (CmbDistributor.Items[i] is DistributorItem d && d.DistributorId == existing.DistributorId.Value)
                        {
                            CmbDistributor.SelectedIndex = i;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(existing.Status))
                {
                    for (int i = 0; i < CmbStatus.Items.Count; i++)
                    {
                        if (CmbStatus.Items[i] is System.Windows.Controls.ComboBoxItem item &&
                            string.Equals(item.Content?.ToString(), existing.Status, StringComparison.OrdinalIgnoreCase))
                        {
                            CmbStatus.SelectedIndex = i;
                            break;
                        }
                    }
                    // If no item matched, leave unselected — save will preserve the raw text via Tag
                    CmbStatus.Tag = existing.Status;
                }

                _itemRows.Clear();
                if (existing.Items != null)
                {
                    foreach (var item in existing.Items)
                    {
                        _itemRows.Add(new ServiceSetItemRow
                        {
                            ItemId = item.ItemId,
                            ItemName = item.Description ?? item.ItemCode,
                            ItemCode = item.ItemCode,
                            PartNumber = item.PartNumber,
                            SubType = item.SubType,
                            ReferenceCode = item.ReferenceCode,
                            Description = item.Description,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            Uom = item.UnitOfMeasure,
                            DurationStart = item.LineStartDate,
                            DurationEnd = item.LineEndDate
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load service set: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Save ─────────────────────────────────────────────────────────────
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime? startDate = ChkHasContractPeriod.IsChecked == true
                    ? DtpStartDate.SelectedDate
                    : null;
                DateTime? endDate = ChkHasContractPeriod.IsChecked == true
                    ? DtpEndDate.SelectedDate
                    : null;

                string statusText = (CmbStatus.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString()
                    ?? CmbStatus.Tag?.ToString()
                    ?? string.Empty;

                var dto = new Models.ServiceSetDto
                {
                    DocumentNumber = TxtDocumentNumber.Text.Trim(),
                    ReferenceNumber = TxtReferenceNumber.Text.Trim(),
                    DocumentDate = DtpDocumentDate.SelectedDate ?? DateTime.Today,
                    StartDate = startDate,
                    EndDate = endDate,
                    Notes = string.Empty,
                    ComId = GetSelectedCompanyId(),
                    DistributorId = GetSelectedDistributorId(),
                    Status = string.IsNullOrWhiteSpace(statusText) ? "Draft" : statusText,
                    CreatedByUserId = AppSession.CurrentUserId,
                    DateCreated = DateTime.Now,
                    Items = new List<Models.ServiceSetItemDto>()
                };

                foreach (var row in _itemRows)
                {
                    dto.Items.Add(new Models.ServiceSetItemDto
                    {
                        ItemId = row.ItemId,
                        ItemCode = row.ItemCode,
                        SubType = row.SubType,
                        ReferenceCode = row.ReferenceCode,
                        Description = row.Description,
                        Quantity = (int)row.Quantity,
                        UnitOfMeasure = row.Uom,
                        UnitPrice = row.UnitPrice,
                        Amount = row.Amount,
                        LineStartDate = row.DurationStart,
                        LineEndDate = row.DurationEnd
                    });
                }

                if (_isEditMode)
                {
                    dto.Id = _editSetId;
                    _repository.UpdateSoftwareServiceSet(dto);
                    MessageBox.Show("Service set updated successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    int newId = _repository.CreateSoftwareServiceSet(dto);
                    if (newId <= 0) return;
                    CreatedSetId = newId;
                    MessageBox.Show("Service set saved successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving service set: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int? GetSelectedCompanyId()
        {
            if (CmbCompany.SelectedItem is CompanyItem c && c.ComId > 0)
                return c.ComId;
            return null;
        }

        private int? GetSelectedDistributorId()
        {
            if (CmbDistributor.SelectedItem is DistributorItem d && d.DistributorId > 0)
                return d.DistributorId;
            return null;
        }

        // ── Print ────────────────────────────────────────────────────────────
        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Print functionality will be implemented later.", "Print",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Cancel ───────────────────────────────────────────────────────────
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // ── Inner types ───────────────────────────────────────────────────────
        private class CompanyItem
        {
            public int ComId { get; set; }
            public string CompanyName { get; set; }
            public override string ToString() => CompanyName;
        }

        private class DistributorItem
        {
            public int DistributorId { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private class Win32WindowWrapper : WinForms.IWin32Window
        {
            private readonly IntPtr _handle;
            public Win32WindowWrapper(IntPtr handle) { _handle = handle; }
            public IntPtr Handle => _handle;
        }
    }

    // ── Observable row model ──────────────────────────────────────────────────
    public class ServiceSetItemRow : INotifyPropertyChanged
    {
        private int _itemId;
        private string _itemName;
        private string _itemCode;
        private string _partNumber;
        private string _subType;
        private string _referenceCode;
        private string _description;
        private decimal _quantity;
        private decimal _unitPrice;
        private string _uom;
        private DateTime? _durationStart;
        private DateTime? _durationEnd;

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public int ItemId
        {
            get => _itemId;
            set { _itemId = value; Notify(nameof(ItemId)); }
        }
        public string ItemName
        {
            get => _itemName;
            set { _itemName = value; Notify(nameof(ItemName)); }
        }
        public string ItemCode
        {
            get => _itemCode;
            set { _itemCode = value; Notify(nameof(ItemCode)); }
        }
        /// <summary>The item's Part# (sourced from dbo.Renewals.PartNumber — see
        /// RenewalRepository), read-only here.</summary>
        public string PartNumber
        {
            get => _partNumber;
            set { _partNumber = value; Notify(nameof(PartNumber)); }
        }
        /// <summary>Which Contract/Subscription/License/Services group this item came
        /// from (Invoice Preparation page) — null/blank for a manually-added item.</summary>
        public string SubType
        {
            get => _subType;
            set { _subType = value; Notify(nameof(SubType)); Notify(nameof(GroupLabel)); }
        }
        /// <summary>The owning group's own Reference Code (Contract Code/Subscription ID/
        /// License ID/Service ID) — typed on the Items Page's group dialog, null for a
        /// manually-added item.</summary>
        public string ReferenceCode
        {
            get => _referenceCode;
            set { _referenceCode = value; Notify(nameof(ReferenceCode)); Notify(nameof(GroupLabel)); }
        }

        /// <summary>Grouping key for the items grid's full-width group header row —
        /// "Contract: CT-0001", or "No Sub-Type" for a plain item.</summary>
        public string GroupLabel => string.IsNullOrWhiteSpace(SubType)
            ? "No Sub-Type"
            : $"{SubType}: {(string.IsNullOrWhiteSpace(ReferenceCode) ? "(no reference code)" : ReferenceCode)}";
        public string Description
        {
            get => _description;
            set { _description = value; Notify(nameof(Description)); }
        }
        public decimal Quantity
        {
            get => _quantity;
            set { _quantity = value; Notify(nameof(Quantity)); Notify(nameof(Amount)); }
        }
        public decimal UnitPrice
        {
            get => _unitPrice;
            set { _unitPrice = value; Notify(nameof(UnitPrice)); Notify(nameof(Amount)); }
        }
        public string Uom
        {
            get => _uom;
            set { _uom = value; Notify(nameof(Uom)); }
        }
        public DateTime? DurationStart
        {
            get => _durationStart;
            set { _durationStart = value; Notify(nameof(DurationStart)); }
        }
        public DateTime? DurationEnd
        {
            get => _durationEnd;
            set { _durationEnd = value; Notify(nameof(DurationEnd)); }
        }

        public decimal Amount => _quantity * _unitPrice;
    }
}
