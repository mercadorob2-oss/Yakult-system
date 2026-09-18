using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Invoice
{
    /// <summary>
    /// WPF replacement for the legacy WinForms BatchAddInvoiceItemsDialog — same namespace,
    /// class name, and constructor signature, with WinForms-compatible ShowDialog()
    /// overloads, so the existing call site (ViewInvoiceDetailPage.BtnBulkAddItems_Click)
    /// keeps working unchanged. Adds an optional Sub-Type Group section so items can be
    /// tagged with a Contract/Subscription/License/Services group at add-time, instead of
    /// requiring a separate trip through the Items Page's "Add to Group" flow first.
    /// </summary>
    public partial class BatchAddInvoiceItemsDialog : Window, IDisposable
    {
        private readonly string _connectionString;
        private readonly int _setId;

        private List<InvoiceItemRow> _allRows;
        private List<ExistingGroupInfo> _existingGroups;
        private bool _suppressGroupSelectionEvents;

        public List<(int ItemId, int Quantity, string Description)> SelectedItems { get; private set; }

        /// <summary>Null when no Sub-Type Group was selected — the caller should insert the
        /// items ungrouped in that case.</summary>
        public string SubType { get; private set; }
        public string ReferenceCode { get; private set; }
        public DateTime? BeginDate { get; private set; }
        public DateTime? EndDate { get; private set; }

        public BatchAddInvoiceItemsDialog(string connectionString, int setId)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string is required.", nameof(connectionString));
            if (setId <= 0)
                throw new ArgumentOutOfRangeException(nameof(setId));

            InitializeComponent();

            _connectionString = connectionString;
            _setId = setId;

            CmbSubType.Items.Add("(None)");
            foreach (var subType in ItemSubTypeCatalog.ValidSubTypes)
                CmbSubType.Items.Add(subType);
            CmbSubType.SelectedIndex = 0;

            DtpStart.SelectedDate = DateTime.Today;
            DtpEnd.SelectedDate = DateTime.Today.AddYears(1);

            Loaded += async (s, e) => await LoadItemsAsync();
        }

        private sealed class InvoiceItemRow : INotifyPropertyChanged
        {
            public int ItemId { get; set; }
            public string ItemType { get; set; }
            public string ItemName { get; set; }
            public string ModelNumber { get; set; }
            public string SerialNumber { get; set; }
            public string UnitOfMeasure { get; set; }
            public string Category { get; set; }
            public decimal Amount { get; set; }

            private bool _selected;
            public bool Selected
            {
                get => _selected;
                set { _selected = value; OnPropertyChanged(nameof(Selected)); }
            }

            private int _quantity = 1;
            public int Quantity
            {
                get => _quantity;
                set { _quantity = value; OnPropertyChanged(nameof(Quantity)); }
            }

            public string Description { get; set; }

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

        // WinForms-compatible ShowDialog overloads (same pattern as SoftwareServiceSetDialog).
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

        private async System.Threading.Tasks.Task LoadItemsAsync()
        {
            try
            {
                DatabaseConfig.EnsureConfigured();

                var table = new DataTable();
                using (var con = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(@"
SELECT
    i.ItemId,
    i.ItemType,
    i.Name AS ItemName,
    i.ModelNumber,
    i.SerialNumber,
    i.UnitOfMeasure,
    i.Category,
    i.Amount
FROM dbo.Item i
LEFT JOIN dbo.ArchiveStatus arch
    ON arch.EntityType = 'Item'
   AND arch.EntityId = i.ItemId
   AND arch.IsArchived = 1
WHERE i.Active = 1
  AND arch.EntityId IS NULL
  AND NOT EXISTS (
      SELECT 1 FROM dbo.SetItem si
      WHERE si.SetId = @SetId
        AND si.ItemId = i.ItemId
  )
  AND NOT EXISTS (
      -- Exclude items already grouped in ANY non-archived Set (including Invoice sets)
      SELECT 1
      FROM dbo.SetItem siAny
      INNER JOIN dbo.[Set] sAny ON sAny.SetId = siAny.SetId
      LEFT JOIN dbo.ArchiveStatus archSet
          ON archSet.EntityType = 'Set'
         AND archSet.EntityId = sAny.SetId
         AND archSet.IsArchived = 1
      WHERE siAny.ItemId = i.ItemId
        AND archSet.EntityId IS NULL
  )
ORDER BY i.ItemType, i.Name, i.ModelNumber, i.SerialNumber;", con))
                {
                    cmd.Parameters.AddWithValue("@SetId", _setId);
                    await con.OpenAsync();
                    using (var adapter = new SqlDataAdapter(cmd))
                        adapter.Fill(table);
                }

                _allRows = table.Rows.Cast<DataRow>().Select(r => new InvoiceItemRow
                {
                    ItemId = Convert.ToInt32(r["ItemId"]),
                    ItemType = r["ItemType"] == DBNull.Value ? null : r["ItemType"].ToString(),
                    ItemName = r["ItemName"] == DBNull.Value ? null : r["ItemName"].ToString(),
                    ModelNumber = r["ModelNumber"] == DBNull.Value ? null : r["ModelNumber"].ToString(),
                    SerialNumber = r["SerialNumber"] == DBNull.Value ? null : r["SerialNumber"].ToString(),
                    UnitOfMeasure = r["UnitOfMeasure"] == DBNull.Value ? null : r["UnitOfMeasure"].ToString(),
                    Category = r["Category"] == DBNull.Value ? null : r["Category"].ToString(),
                    Amount = r["Amount"] == DBNull.Value ? 0 : Convert.ToDecimal(r["Amount"]),
                    Description = r["ItemName"] == DBNull.Value ? null : r["ItemName"].ToString()
                }).ToList();

                foreach (var row in _allRows)
                    row.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(InvoiceItemRow.Selected)) UpdateSelectedCount(); };

                await LoadExistingGroupsAsync();

                LoadCategoryOptions();
                ApplyFilter();
                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load eligible items.\n\n{ex.Message}", "Bulk Add Invoice Items",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
            }
        }

        private void ApplyFilter()
        {
            if (_allRows == null)
                return;

            var q = (TxtSearch?.Text ?? string.Empty).Trim();
            var category = CmbCategory?.SelectedItem as string;
            bool hasCategory = !string.IsNullOrWhiteSpace(category) && !string.Equals(category, "(All)", StringComparison.OrdinalIgnoreCase);

            IEnumerable<InvoiceItemRow> filtered = _allRows;

            if (!string.IsNullOrWhiteSpace(q))
            {
                filtered = filtered.Where(r =>
                    (r.ItemName != null && r.ItemName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (r.ModelNumber != null && r.ModelNumber.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (r.SerialNumber != null && r.SerialNumber.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            if (hasCategory)
                filtered = filtered.Where(r => string.Equals(r.Category, category, StringComparison.OrdinalIgnoreCase));

            Grid.ItemsSource = new ObservableCollection<InvoiceItemRow>(filtered);
        }

        private void LoadCategoryOptions()
        {
            if (CmbCategory == null || _allRows == null)
                return;

            CmbCategory.Items.Clear();
            CmbCategory.Items.Add("(All)");

            var categories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in _allRows)
            {
                if (string.IsNullOrWhiteSpace(r.Category)) continue;
                categories.Add(r.Category.Trim());
            }

            foreach (var c in categories)
                CmbCategory.Items.Add(c);

            CmbCategory.SelectedIndex = 0;
        }

        private void UpdateSelectedCount()
        {
            var count = _allRows?.Count(r => r.Selected) ?? 0;
            TxtSelectedCount.Text = $"{count} item(s) selected";
        }

        private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyFilter();
        private void CmbCategory_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => ApplyFilter();

        /// <summary>Loads the Sub-Type Groups already on this invoice, so the user can add
        /// checked items to one of them instead of only ever creating a brand new group.</summary>
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

        private void CmbSubType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var subType = CmbSubType.SelectedItem as string;
            bool grouped = !string.IsNullOrEmpty(subType) && subType != "(None)";

            LblReferenceCode.Text = ItemSubTypeCatalog.GetReferenceCodeLabel(grouped ? subType : null);

            _suppressGroupSelectionEvents = true;
            CmbExistingGroup.Items.Clear();
            CmbExistingGroup.Items.Add("(New Group)");
            if (grouped && _existingGroups != null)
            {
                foreach (var g in _existingGroups.Where(g => g.SubType == subType))
                    CmbExistingGroup.Items.Add(g);
            }
            CmbExistingGroup.SelectedIndex = 0;
            CmbExistingGroup.IsEnabled = grouped;
            _suppressGroupSelectionEvents = false;

            TxtReferenceCode.IsEnabled = grouped;
            TxtReferenceCode.Text = string.Empty;
            DtpStart.IsEnabled = grouped;
            DtpEnd.IsEnabled = grouped;
            DtpStart.SelectedDate = DateTime.Today;
            DtpEnd.SelectedDate = DateTime.Today.AddYears(1);
        }

        /// <summary>Picking an existing group locks Reference Code/dates to that group's
        /// stored values — they must match exactly for AddInvoiceItemsAsync's find-or-create
        /// lookup to resolve the same dbo.SetItemSubTypeGroup row instead of creating a new
        /// one. Picking "(New Group)" reverts to blank, freely-editable fields.</summary>
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

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            var selected = _allRows?.Where(r => r.Selected).ToList() ?? new List<InvoiceItemRow>();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Please check at least one item to add.", "Bulk Add Invoice Items",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var invalid = selected.Where(r => r.Quantity <= 0).ToList();
            if (invalid.Count > 0)
            {
                MessageBox.Show(this, $"Quantity must be greater than 0 for: {string.Join(", ", invalid.Select(r => r.ItemName))}",
                    "Bulk Add Invoice Items", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedItems = selected
                .Select(r => (r.ItemId, r.Quantity, string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim()))
                .ToList();

            var subType = CmbSubType.SelectedItem as string;
            if (!string.IsNullOrEmpty(subType) && subType != "(None)")
            {
                SubType = subType;
                ReferenceCode = string.IsNullOrWhiteSpace(TxtReferenceCode.Text) ? null : TxtReferenceCode.Text.Trim();
                BeginDate = DtpStart.SelectedDate;
                EndDate = DtpEnd.SelectedDate;
            }

            DialogResult = true;
        }
    }
}
