using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Invoice
{
    /// <summary>
    /// Assigns already-invoiced SetItem rows to a Parent Tag group — a free-text label (e.g.
    /// "Cisco") that clusters related items for readability, independent of Sub-Type grouping
    /// (ConvertToSubTypeGroupDialog). Shows every item on the invoice with a checkbox per row
    /// (all rows are checkable — unlike Sub-Type, an item can be re-tagged with a different
    /// Parent Tag at any time), so the user can check a batch, type or pick a label, and commit
    /// it with "Add Checked Items to Group" — without closing the dialog. The grid then
    /// refreshes in place so the next batch can be picked for a different tag. Parent Tag has no
    /// financial semantics of its own — no Financial Details editing here or anywhere else.
    /// </summary>
    public partial class ConvertToParentTagGroupDialog : Window, IDisposable
    {
        private readonly string _connectionString;
        private readonly int _setId;
        private readonly InvoiceRepository _invoiceRepository;

        /// <summary>True once at least one batch was successfully converted, so the caller
        /// knows the invoice's Parent Tag groups changed.</summary>
        public bool AnyChanges { get; private set; }

        public ConvertToParentTagGroupDialog(string connectionString, int setId)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string is required.", nameof(connectionString));
            if (setId <= 0)
                throw new ArgumentOutOfRangeException(nameof(setId));

            InitializeComponent();

            _connectionString = connectionString;
            _setId = setId;
            _invoiceRepository = new InvoiceRepository(connectionString);

            Loaded += async (s, e) => await ReloadAsync();
        }

        private sealed class ConvertItemRow : INotifyPropertyChanged
        {
            public int SetItemId { get; set; }
            public string ItemName { get; set; }
            public string ModelNumber { get; set; }
            public string SerialNumber { get; set; }
            public decimal Quantity { get; set; }
            public decimal Amount { get; set; }
            public string CurrentParentTag { get; set; }

            public string GroupLabel => string.IsNullOrWhiteSpace(CurrentParentTag) ? "Ungrouped" : CurrentParentTag;

            private bool _selected;
            public bool Selected
            {
                get => _selected;
                set { _selected = value; OnPropertyChanged(nameof(Selected)); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // WinForms-compatible ShowDialog overloads (same pattern as ConvertToSubTypeGroupDialog).
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

        public void Dispose() { }

        /// <summary>Re-fetches items and known labels from the DB — called on load and again
        /// after every successful "Add Checked Items to Group", so the grid always reflects what
        /// just changed and the next batch starts from a clean set of checkboxes.</summary>
        private async System.Threading.Tasks.Task ReloadAsync()
        {
            try
            {
                var itemsTable = await _invoiceRepository.GetInvoiceItemsAsync(_setId);

                var rows = itemsTable.Rows.Cast<DataRow>().Select(r => new ConvertItemRow
                {
                    SetItemId = r["SetItemId"] == DBNull.Value ? 0 : Convert.ToInt32(r["SetItemId"]),
                    ItemName = r["ItemName"] == DBNull.Value ? null : r["ItemName"].ToString(),
                    ModelNumber = r["ModelNumber"] == DBNull.Value ? null : r["ModelNumber"].ToString(),
                    SerialNumber = r["SerialNumber"] == DBNull.Value ? null : r["SerialNumber"].ToString(),
                    Quantity = r["Quantity"] == DBNull.Value ? 0 : Convert.ToDecimal(r["Quantity"]),
                    Amount = r["Amount"] == DBNull.Value ? 0 : Convert.ToDecimal(r["Amount"]),
                    CurrentParentTag = r.Table.Columns.Contains("ParentTag") && r["ParentTag"] != DBNull.Value
                        ? r["ParentTag"].ToString() : null
                }).ToList();

                Grid.ItemsSource = new ObservableCollection<ConvertItemRow>(rows);
                ChkSelectAll.IsChecked = false;

                TxtStatus.Text = $"{rows.Count} item(s) total.";

                var labels = await _invoiceRepository.GetDistinctParentTagLabelsAsync();
                CmbParentTag.ItemsSource = labels;

                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load invoice items.\n\n{ex.Message}", "Convert to Parent Tag Group",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private IEnumerable<ConvertItemRow> AllRows =>
            (Grid.ItemsSource as ObservableCollection<ConvertItemRow>) ?? Enumerable.Empty<ConvertItemRow>();

        private void UpdateSelectedCount()
        {
            int count = AllRows.Count(r => r.Selected);
            TxtSelectedCount.Text = $"{count} item(s) checked";
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool check = ChkSelectAll.IsChecked == true;
            foreach (var row in AllRows)
                row.Selected = check;
            UpdateSelectedCount();
        }

        private async void BtnAddToGroup_Click(object sender, RoutedEventArgs e)
        {
            var label = CmbParentTag.Text;
            if (string.IsNullOrWhiteSpace(label))
            {
                MessageBox.Show(this, "Please type or choose a Parent Tag.", "Convert to Parent Tag Group",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selected = AllRows.Where(r => r.Selected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Please check at least one item.", "Convert to Parent Tag Group",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var setItemIds = selected.Select(r => r.SetItemId).Where(id => id > 0).ToList();

            BtnAddToGroup.IsEnabled = false;
            try
            {
                await _invoiceRepository.ConvertItemsToParentTagGroupAsync(
                    _setId, setItemIds, label.Trim(), AppSession.CurrentUserId);

                AnyChanges = true;

                await ReloadAsync();

                MessageBox.Show(this, $"{setItemIds.Count} item(s) added to the \"{label.Trim()}\" tag.\n\nPick the next batch, or Close when done.",
                    "Convert to Parent Tag Group", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to convert items: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnAddToGroup.IsEnabled = true;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = AnyChanges;
            Close();
        }
    }
}
