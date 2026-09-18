using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Invoice
{
    /// <summary>
    /// Assigns already-invoiced, still-Ungrouped SetItem rows to a Sub-Type group — for invoices
    /// created before Sub-Type grouping existed, whose items were never tagged. Shows every item
    /// on the invoice with a checkbox per row (only Ungrouped rows are checkable), so the user can
    /// check a batch, pick its Sub-Type/Reference Code/dates (or an existing group on this
    /// invoice), and commit it with "Add Checked Items to Group" — without closing the dialog.
    /// The grid then refreshes in place so the next batch can be picked for a different group,
    /// repeating as many times as this invoice needs. Financial Details (VAT/WHT/Discount) for
    /// each resulting group are edited afterward on ViewInvoiceDetailPage's group cards, not here.
    /// </summary>
    public partial class ConvertToSubTypeGroupDialog : Window, IDisposable
    {
        private readonly string _connectionString;
        private readonly int _setId;
        private readonly InvoiceRepository _invoiceRepository;

        private List<ExistingGroupInfo> _existingGroups;
        private bool _suppressGroupSelectionEvents;

        /// <summary>True once at least one batch was successfully converted, so the caller
        /// knows the invoice's groups/financial cards changed.</summary>
        public bool AnyChanges { get; private set; }

        public ConvertToSubTypeGroupDialog(string connectionString, int setId)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string is required.", nameof(connectionString));
            if (setId <= 0)
                throw new ArgumentOutOfRangeException(nameof(setId));

            InitializeComponent();

            _connectionString = connectionString;
            _setId = setId;
            _invoiceRepository = new InvoiceRepository(connectionString);

            foreach (var subType in ItemSubTypeCatalog.ValidSubTypes)
                CmbSubType.Items.Add(subType);

            DtpStart.SelectedDate = DateTime.Today;
            DtpEnd.SelectedDate = DateTime.Today.AddYears(1);

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
            public string CurrentSubType { get; set; }
            public string CurrentReferenceCode { get; set; }

            public bool IsUngrouped => string.IsNullOrWhiteSpace(CurrentSubType);
            public string GroupLabel => IsUngrouped ? "Ungrouped" : $"{CurrentSubType} #{CurrentReferenceCode ?? "(none)"}";

            private bool _selected;
            public bool Selected
            {
                get => _selected;
                set { _selected = value; OnPropertyChanged(nameof(Selected)); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private sealed class ExistingGroupInfo
        {
            public int GroupId { get; set; }
            public string SubType { get; set; }
            public string ReferenceCode { get; set; }
            public DateTime? BeginDate { get; set; }
            public DateTime? EndDate { get; set; }

            public override string ToString() => $"{SubType} #{ReferenceCode ?? "(none)"}";
        }

        // WinForms-compatible ShowDialog overloads (same pattern as BatchAddInvoiceItemsDialog).
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

        /// <summary>Re-fetches items and existing groups from the DB — called on load and again
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
                    CurrentSubType = r["SubType"] == DBNull.Value ? null : r["SubType"].ToString(),
                    CurrentReferenceCode = r["ReferenceCode"] == DBNull.Value ? null : r["ReferenceCode"].ToString()
                }).ToList();

                Grid.ItemsSource = new ObservableCollection<ConvertItemRow>(rows);
                ChkSelectAll.IsChecked = false;

                int ungroupedCount = rows.Count(r => r.IsUngrouped);
                TxtStatus.Text = $"{rows.Count} item(s) total, {ungroupedCount} still Ungrouped.";

                await LoadExistingGroupsAsync();
                RefreshExistingGroupOptions();
                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load invoice items.\n\n{ex.Message}", "Convert to Sub-Type Group",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadExistingGroupsAsync()
        {
            _existingGroups = new List<ExistingGroupInfo>();

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(@"
                SELECT GroupId, SubType, ReferenceCode, BeginDate, EndDate
                FROM dbo.SetItemSubTypeGroup
                WHERE SetId = @SetId
                ORDER BY SubType, ReferenceCode", con))
            {
                cmd.Parameters.AddWithValue("@SetId", _setId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        _existingGroups.Add(new ExistingGroupInfo
                        {
                            GroupId = reader.GetInt32(0),
                            SubType = reader.GetString(1),
                            ReferenceCode = reader.IsDBNull(2) ? null : reader.GetString(2),
                            BeginDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                            EndDate = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4)
                        });
                    }
                }
            }
        }

        private IEnumerable<ConvertItemRow> AllRows =>
            (Grid.ItemsSource as ObservableCollection<ConvertItemRow>) ?? Enumerable.Empty<ConvertItemRow>();

        private void UpdateSelectedCount()
        {
            int count = AllRows.Count(r => r.Selected);
            TxtSelectedCount.Text = $"{count} Ungrouped item(s) checked";
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool check = ChkSelectAll.IsChecked == true;
            foreach (var row in AllRows.Where(r => r.IsUngrouped))
                row.Selected = check;
            UpdateSelectedCount();
        }

        private void CmbSubType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var subType = CmbSubType.SelectedItem as string;
            LblReferenceCode.Text = ItemSubTypeCatalog.GetReferenceCodeLabel(subType);
            RefreshExistingGroupOptions();
        }

        private void RefreshExistingGroupOptions()
        {
            var subType = CmbSubType.SelectedItem as string;

            _suppressGroupSelectionEvents = true;
            CmbExistingGroup.Items.Clear();
            CmbExistingGroup.Items.Add("(New Group)");
            if (!string.IsNullOrEmpty(subType) && _existingGroups != null)
            {
                foreach (var g in _existingGroups.Where(g => g.SubType == subType))
                    CmbExistingGroup.Items.Add(g);
            }
            CmbExistingGroup.SelectedIndex = 0;
            CmbExistingGroup.IsEnabled = !string.IsNullOrEmpty(subType);
            _suppressGroupSelectionEvents = false;
        }

        /// <summary>Picking an existing group locks Reference Code/dates to that group's stored
        /// values — they must match exactly for ConvertItemsToSubTypeGroupAsync's find-or-create
        /// lookup to resolve the same dbo.SetItemSubTypeGroup row instead of creating a new one.</summary>
        private void CmbExistingGroup_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_suppressGroupSelectionEvents) return;

            if (CmbExistingGroup.SelectedItem is ExistingGroupInfo existing)
            {
                TxtReferenceCode.Text = existing.ReferenceCode ?? string.Empty;
                DtpStart.SelectedDate = existing.BeginDate;
                DtpEnd.SelectedDate = existing.EndDate;
                TxtReferenceCode.IsEnabled = false;
                DtpStart.IsEnabled = false;
                DtpEnd.IsEnabled = false;
            }
            else
            {
                TxtReferenceCode.Text = string.Empty;
                DtpStart.SelectedDate = DateTime.Today;
                DtpEnd.SelectedDate = DateTime.Today.AddYears(1);
                TxtReferenceCode.IsEnabled = true;
                DtpStart.IsEnabled = true;
                DtpEnd.IsEnabled = true;
            }
        }

        private async void BtnAddToGroup_Click(object sender, RoutedEventArgs e)
        {
            var subType = CmbSubType.SelectedItem as string;
            if (string.IsNullOrEmpty(subType))
            {
                MessageBox.Show(this, "Please choose a Sub-Type.", "Convert to Sub-Type Group",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selected = AllRows.Where(r => r.Selected && r.IsUngrouped).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Please check at least one Ungrouped item.", "Convert to Sub-Type Group",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var setItemIds = selected.Select(r => r.SetItemId).Where(id => id > 0).ToList();
            var referenceCode = string.IsNullOrWhiteSpace(TxtReferenceCode.Text) ? null : TxtReferenceCode.Text.Trim();
            var beginDate = DtpStart.SelectedDate;
            var endDate = DtpEnd.SelectedDate;

            BtnAddToGroup.IsEnabled = false;
            try
            {
                await _invoiceRepository.ConvertItemsToSubTypeGroupAsync(
                    _setId, setItemIds, subType, referenceCode, beginDate, endDate, AppSession.CurrentUserId);

                AnyChanges = true;

                await ReloadAsync();

                MessageBox.Show(this, $"{setItemIds.Count} item(s) added to the {subType} group.\n\nPick the next batch, or Close when done.",
                    "Convert to Sub-Type Group", MessageBoxButton.OK, MessageBoxImage.Information);
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
