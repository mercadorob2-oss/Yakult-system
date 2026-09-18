using Yakult.Inventory.App.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Data.SqlClient;
using Yakult.Inventory.App.Core;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Request
{
    public partial class EditRequestDialog : Window, IDisposable
    {
        private static readonly Brush LockedBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240));
        private static readonly Brush UnlockedBrush = Brushes.White;
        private static readonly Brush UnlockedFieldHighlight = new SolidColorBrush(Color.FromRgb(144, 238, 144)); // LightGreen

        private RequestDto _request;
        private readonly List<CategoryItem> _itemCategories = new List<CategoryItem>();
        private readonly List<ItemItem> _allItems = new List<ItemItem>();

        // Dept-level mode
        private bool _isDeptLevel;
        private readonly List<OrgEditItem> _orgCompanies = new List<OrgEditItem>();
        private readonly List<OrgEditItem> _orgDepts = new List<OrgEditItem>();
        private readonly List<OrgEditItem> _orgBranches = new List<OrgEditItem>();

        // Track original values
        private int _originalItemId;
        private int _originalQuantity;

        public EditRequestDialog(RequestDto request)
        {
            _request = request;
            _originalItemId = request.ItemId;
            _originalQuantity = request.Quantity;

            InitializeComponent();

            LoadData();
            LockAllFields(); // Lock everything on load
        }

        // WinForms-compatible ShowDialog overloads (same pattern as ConvertToParentTagGroupDialog).
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

        private void LockAllFields()
        {
            // Lock all editable fields by default
            CmbEmployee.IsEnabled = false;
            CmbEmployee.Background = LockedBrush;

            CmbItem.IsEnabled = false;
            CmbItem.Background = LockedBrush;

            CmbItemCategory.IsEnabled = false;
            CmbItemCategory.Background = LockedBrush;

            TxtQuantity.IsReadOnly = true;
            TxtQuantity.Background = LockedBrush;

            DtDateRequested.IsEnabled = false;

            CmbStatus.IsEnabled = false;
            CmbStatus.Background = LockedBrush;
        }

        private void ToggleFieldLock(Control control, Button btnLock)
        {
            if (control is ComboBox cmb)
            {
                cmb.IsEnabled = !cmb.IsEnabled;
                cmb.Background = cmb.IsEnabled ? UnlockedBrush : LockedBrush;
                btnLock.Content = cmb.IsEnabled ? "\U0001F513" : "\U0001F512";
                btnLock.Background = cmb.IsEnabled ? UnlockedFieldHighlight : LockedBrush;
            }
            else if (control is TextBox txt)
            {
                txt.IsReadOnly = !txt.IsReadOnly;
                txt.Background = txt.IsReadOnly ? LockedBrush : UnlockedBrush;
                btnLock.Content = txt.IsReadOnly ? "\U0001F512" : "\U0001F513";
                btnLock.Background = txt.IsReadOnly ? LockedBrush : UnlockedFieldHighlight;
            }
            else if (control is DatePicker dtp)
            {
                dtp.IsEnabled = !dtp.IsEnabled;
                btnLock.Content = dtp.IsEnabled ? "\U0001F513" : "\U0001F512";
                btnLock.Background = dtp.IsEnabled ? UnlockedFieldHighlight : LockedBrush;
            }
        }

        private void BtnEditEmployee_Click(object sender, RoutedEventArgs e) => ToggleFieldLock(CmbEmployee, BtnEditEmployee);
        private void BtnEditQuantity_Click(object sender, RoutedEventArgs e) => ToggleFieldLock(TxtQuantity, BtnEditQuantity);
        private void BtnEditDate_Click(object sender, RoutedEventArgs e) => ToggleFieldLock(DtDateRequested, BtnEditDate);
        private void BtnEditStatus_Click(object sender, RoutedEventArgs e) => ToggleFieldLock(CmbStatus, BtnEditStatus);

        private void BtnEditItem_Click(object sender, RoutedEventArgs e)
        {
            // Unlocking (not re-locking) while the request already belongs to a Set is risky:
            // SetItem rows are a one-time snapshot copied from Request.ItemId (see
            // SetRepository.MaterializeSetItemsFromRequestsAsync) and are never re-synced after
            // the Set is materialized/dispatched. Swapping the item here would desync the
            // Request from the Set's paperwork, audit trail, and warranty records.
            if (!CmbItem.IsEnabled && _request.SetId.HasValue)
            {
                string setLabel = string.IsNullOrWhiteSpace(_request.SetCode) ? $"Set #{_request.SetId.Value}" : _request.SetCode;
                var result = System.Windows.MessageBox.Show(this,
                    $"This request is already part of {setLabel}.\n\n" +
                    "Changing the item here will NOT update the Set's records (paperwork, audit trail, " +
                    "warranty tracking) if that Set has already been materialized/dispatched - the Request " +
                    "and the Set would then show different items.\n\n" +
                    "Only proceed if you know the Set has not been dispatched yet, or if you will manually " +
                    "correct the Set afterward.\n\nContinue anyway?",
                    "Item is Tied to a Set",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            ToggleFieldLock(CmbItem, BtnEditItem);
            CmbItemCategory.IsEnabled = CmbItem.IsEnabled;
            CmbItemCategory.Background = CmbItem.IsEnabled ? UnlockedBrush : LockedBrush;
        }

        private void CmbItemCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Only re-filter if the field is unlocked; avoid clobbering the selection during initial load.
            if (CmbItemCategory.IsEnabled)
            {
                RefreshFilteredItems(preserveSelection: false);
            }
        }

        private void CmbItem_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Only recalculate if the field is unlocked
            if (CmbItem.IsEnabled)
            {
                UpdateEntryTypePreview();
            }
        }

        private void TxtQuantity_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Only recalculate if the field is unlocked
            if (!TxtQuantity.IsReadOnly)
            {
                UpdateEntryTypePreview();
            }
        }

        private void UpdateEntryTypePreview()
        {
            // Only update if we have valid item and quantity
            if (!(CmbItem.SelectedItem is ItemItem itemItem) || !itemItem.Id.HasValue)
            {
                TxtEntryType.Text = "";
                TxtEntryType.Background = LockedBrush;
                return;
            }

            if (!int.TryParse(TxtQuantity.Text, out int quantity) || quantity <= 0)
            {
                TxtEntryType.Text = "";
                TxtEntryType.Background = LockedBrush;
                return;
            }

            // Requests are ALWAYS "Negative" because they subtract from stock
            string entryType = "Negative";

            // Get current stock for display purposes only
            int stockOnHand = GetItemStockOnHand(itemItem.Id.Value);

            // If same item, add back original quantity
            if (itemItem.Id.Value == _originalItemId)
            {
                stockOnHand += _originalQuantity;
            }

            int remainingStock = stockOnHand - quantity;

            // Update display
            TxtEntryType.Text = entryType;

            // Color code based on stock availability (for visual feedback only)
            if (remainingStock >= 0)
            {
                // Sufficient stock available
                TxtEntryType.Foreground = Brushes.DarkGreen;
                TxtEntryType.Background = Brushes.LightGreen;
            }
            else
            {
                // Insufficient stock
                TxtEntryType.Foreground = Brushes.Red;
                TxtEntryType.Background = Brushes.LightPink;
            }
        }

        private void CmbEmployee_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbEmployee.SelectedItem is EmployeeItem empItem)
            {
                LoadEmployeeDetails(empItem.Id);
            }
            else
            {
                TxtEmployeeNumber.Clear();
                TxtCompany.Clear();
                TxtDepartment.Clear();
                TxtBranch.Clear();
            }
        }

        private void LoadEmployeeDetails(int empId)
        {
            try
            {
                var cs = DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT
                            e.EmployeeNumber,
                            c.Name AS CompanyName,
                            d.Name AS DepartmentName,
                            b.Name AS BranchName
                        FROM dbo.Employee e
                        LEFT JOIN dbo.Company c ON e.ComId = c.ComId
                        LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
                        LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
                        WHERE e.EmpId = @EmpId", con))
                    {
                        cmd.Parameters.AddWithValue("@EmpId", empId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                TxtEmployeeNumber.Text = reader.IsDBNull(0) ? "" : reader.GetString(0);
                                TxtCompany.Text = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                TxtDepartment.Text = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                TxtBranch.Text = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, $"Failed to load employee details: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadData()
        {
            LoadEmployees();
            LoadItemCategories();
            LoadItems();
            PopulateRequestDetails();

            TxtQuantity.Text = _request.Quantity.ToString();
            TxtUnitPrice.Text = _request.UnitPrice.ToString("0.00");
            DtDateRequested.SelectedDate = _request.DateRequested ?? _request.DateCreated;
            TxtDescription.Text = _request.Description ?? string.Empty;
            TxtRemarks.Text = _request.Remarks ?? string.Empty;
            TxtEntryType.Text = "Negative";  // Requests are always Negative

            // Set EntryType color - always show as Negative (red) for requests
            TxtEntryType.Foreground = Brushes.Red;
            TxtEntryType.Background = Brushes.LightPink;

            if (!string.IsNullOrEmpty(_request.Status))
            {
                for (int i = 0; i < CmbStatus.Items.Count; i++)
                {
                    if (string.Equals(CmbStatus.Items[i] as string, _request.Status, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbStatus.SelectedIndex = i;
                        break;
                    }
                }
            }

            for (int i = 0; i < CmbEmployee.Items.Count; i++)
            {
                if (CmbEmployee.Items[i] is EmployeeItem empItem && empItem.Id == _request.EmpId)
                {
                    CmbEmployee.SelectedIndex = i;
                    break;
                }
            }

            // Preselect the category that owns the current item, then filter/select the item itself.
            var currentItem = _allItems.FirstOrDefault(x => x.Id == _request.ItemId);
            if (currentItem?.CategoryId != null)
            {
                foreach (var cat in _itemCategories)
                {
                    if (cat.CategoryId == currentItem.CategoryId.Value)
                    {
                        CmbItemCategory.SelectedItem = cat;
                        break;
                    }
                }
            }
            RefreshFilteredItems(preserveSelection: false);

            for (int i = 0; i < CmbItem.Items.Count; i++)
            {
                if (CmbItem.Items[i] is ItemItem itemItem && itemItem.Id == _request.ItemId)
                {
                    CmbItem.SelectedIndex = i;
                    break;
                }
            }

            // Detect dept-level request on load
            if (!_request.EmpId.HasValue || _request.EmpId.Value <= 0)
            {
                LoadOrgUnits();
                SwitchToDeptMode();
            }
        }

        private void PopulateRequestDetails()
        {
            LblReqIdValue.Text = $"#{_request.ReqId}";

            string model = string.IsNullOrWhiteSpace(_request.ModelNumber) ? "N/A" : _request.ModelNumber;
            string serial = string.IsNullOrWhiteSpace(_request.SerialNumber) ? "N/A" : _request.SerialNumber;
            LblModelSerialValue.Text = $"{model} / {serial}";

            LblConditionValue.Text = string.IsNullOrWhiteSpace(_request.ConditionName) ? "–" : _request.ConditionName;

            LblSetValue.Text = _request.SetId.HasValue
                ? (string.IsNullOrWhiteSpace(_request.SetCode) ? $"Set #{_request.SetId.Value}" : _request.SetCode)
                : "Not part of a Set";

            LblSourceValue.Text = string.IsNullOrWhiteSpace(_request.RequestSource) ? "INTERNAL" : _request.RequestSource;

            LblReceivedByValue.Text = string.IsNullOrWhiteSpace(_request.ReceivedByName) ? "–" : _request.ReceivedByName;

            LblCreatedValue.Text = $"{_request.CreatedByName} on {_request.DateCreated:MMM d, yyyy h:mm tt}";

            LblSubmissionValue.Text = _request.SubmissionSessionId.HasValue
                ? _request.SubmissionSessionId.Value.ToString()
                : "–";
        }

        private void LoadEmployees()
        {
            CmbEmployee.Items.Clear();

            try
            {
                var cs = DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT EmpId, Name, EmployeeNumber FROM dbo.Employee ORDER BY Name", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CmbEmployee.Items.Add(new EmployeeItem
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                EmployeeNumber = reader.IsDBNull(2) ? null : reader.GetString(2)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, $"Failed to load employees: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Loads the Category filter combo. Mirrors BatchAddRequestDialog.LoadCategories().
        private void LoadItemCategories()
        {
            CmbItemCategory.Items.Clear();
            _itemCategories.Clear();

            try
            {
                var cs = DatabaseConfig.ConnectionString;
                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT CategoryId, Name FROM dbo.ItemCategory WHERE Active = 1 ORDER BY Name", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var cat = new CategoryItem
                            {
                                CategoryId = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            };
                            _itemCategories.Add(cat);
                            CmbItemCategory.Items.Add(cat);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, $"Failed to load item categories: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Loads the full item catalog once. Mirrors BatchAddRequestDialog.LoadItems().
        private void LoadItems()
        {
            _allItems.Clear();

            try
            {
                var cs = DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    const string sql = @"
                        SELECT i.ItemId, i.Name, i.StockOnHand, i.CategoryId, i.Category,
                               i.SerialNumber, i.ModelNumber, i.Amount, i.IsTrackedAsset
                        FROM dbo.Item i WHERE i.Active = 1 ORDER BY i.Name";
                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _allItems.Add(new ItemItem
                            {
                                Id = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0),
                                Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                StockOnHand = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                CategoryId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                                CategoryName = reader.IsDBNull(4) ? null : reader.GetString(4),
                                SerialNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                                ModelNumber = reader.IsDBNull(6) ? null : reader.GetString(6),
                                Amount = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                                IsTrackedAsset = !reader.IsDBNull(8) && reader.GetBoolean(8)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, $"Failed to load items: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Refills CmbItem from _allItems, filtered to the selected category (mirrors
        // RequestRowVm.RefreshFilteredItems in BatchAddRequestDialog). When preserveSelection is
        // true, tries to keep the currently selected item highlighted after the refresh.
        private void RefreshFilteredItems(bool preserveSelection)
        {
            int? currentItemId = preserveSelection && CmbItem.SelectedItem is ItemItem current ? current.Id : null;

            CmbItem.Items.Clear();

            var category = CmbItemCategory.SelectedItem as CategoryItem;
            var matches = category == null
                ? _allItems
                : _allItems.Where(x => x.CategoryId == category.CategoryId);

            foreach (var item in matches)
                CmbItem.Items.Add(item);

            if (currentItemId.HasValue)
            {
                for (int i = 0; i < CmbItem.Items.Count; i++)
                {
                    if (CmbItem.Items[i] is ItemItem itemItem && itemItem.Id == currentItemId.Value)
                    {
                        CmbItem.SelectedIndex = i;
                        return;
                    }
                }
            }

            CmbItem.SelectedIndex = -1;
            CmbItem.Text = string.Empty;
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_request.Status == "Submitted")
            {
                var warningResult = System.Windows.MessageBox.Show(this,
                    $"⚠️ WARNING: This request has status 'Submitted'!\n\n" +
                    $"Deleting this request will ALSO:" +
                    $"\n  • Delete the Inventory entry (ledger record)" +
                    $"\n  • Restore stock quantity to the item automatically\n\n" +
                    $"Item: {_request.ItemName}\n" +
                    $"Quantity to restore: {_request.Quantity}\n\n" +
                    $"Are you SURE you want to delete this request?",
                    "Delete Submitted Request?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (warningResult != MessageBoxResult.Yes)
                    return;
            }

            var result = System.Windows.MessageBox.Show(this,
                $"Are you sure you want to DELETE this request?\n\n" +
                $"Employee: {_request.EmployeeName}\n" +
                $"Item: {_request.ItemName}\n" +
                $"Quantity: {_request.Quantity}\n" +
                $"Status: {_request.Status}\n\n" +
                $"⚠️ This action cannot be undone!\n" +
                (_request.Status == "Submitted" ? "✔ Stock will be restored automatically" : ""),
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    BtnDelete.IsEnabled = false;
                    BtnDelete.Content = "Deleting...";
                    Mouse.OverrideCursor = Cursors.Wait;

                    var repo = new Repositories.RequestRepository();
                    await repo.DeleteRequestAndRestoreStock(_request.ReqId);

                    Mouse.OverrideCursor = null;

                    string message = _request.Status == "Submitted"
                        ? $"Request deleted successfully!\n\nStock restored: {_request.Quantity} units of {_request.ItemName}"
                        : "Request deleted successfully.";

                    System.Windows.MessageBox.Show(this, message, "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    this.Tag = "DELETED";
                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    Mouse.OverrideCursor = null;
                    BtnDelete.IsEnabled = true;
                    BtnDelete.Content = "Delete";

                    System.Windows.MessageBox.Show(this, $"Failed to delete request: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validate requester section based on current mode
            EmployeeItem empItem = null;
            int? saveComId = null, saveDeptId = null, saveBranchId = null;

            if (_isDeptLevel)
            {
                if (!(CmbCompanyEdit.SelectedItem is OrgEditItem selCompany) || selCompany.Id == 0)
                {
                    System.Windows.MessageBox.Show(this, "Please select a Company.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                saveComId = selCompany.Id;
                if (CmbDeptEdit.SelectedItem is OrgEditItem selDept && selDept.Id > 0)
                    saveDeptId = selDept.Id;
                if (CmbBranchEdit.SelectedItem is OrgEditItem selBranch && selBranch.Id > 0)
                    saveBranchId = selBranch.Id;
            }
            else
            {
                empItem = CmbEmployee.SelectedItem as EmployeeItem;
                if (empItem == null)
                {
                    System.Windows.MessageBox.Show(this, "Please select an Employee.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (!(CmbItem.SelectedItem is ItemItem itemItem) || !itemItem.Id.HasValue)
            {
                System.Windows.MessageBox.Show(this, "Please select an Item.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(TxtQuantity.Text, out int quantity) || quantity <= 0)
            {
                System.Windows.MessageBox.Show(this, "Please enter a valid Quantity (must be greater than 0).", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (quantity > 3)
            {
                System.Windows.MessageBox.Show(this, "Maximum quantity per request is 3.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (CmbStatus.SelectedItem == null)
            {
                System.Windows.MessageBox.Show(this, "Please select a Status.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string entryType = "Negative"; // Always negative for requests

            bool itemWasEdited = CmbItem.IsEnabled;
            bool quantityWasEdited = !TxtQuantity.IsReadOnly;
            string selectedStatus = CmbStatus.SelectedItem.ToString();

            // ⚠️ Only perform stock validation if:
            // - The status is "Submitted"
            // - The item or quantity was edited
            if (selectedStatus == "Submitted" && (itemWasEdited || quantityWasEdited))
            {
                int stockOnHand = GetItemStockOnHand(itemItem.Id.Value);

                // If editing the same item, restore original quantity first
                if (itemItem.Id.Value == _originalItemId)
                {
                    stockOnHand += _originalQuantity;
                }

                int remainingStock = stockOnHand - quantity;

                if (remainingStock < 0)
                {
                    var result = System.Windows.MessageBox.Show(this,
                        $"⚠️ INSUFFICIENT STOCK!\n\n" +
                        $"Current Stock: {stockOnHand}\n" +
                        $"Requested: {quantity}\n" +
                        $"Shortage: {Math.Abs(remainingStock)}\n\n" +
                        $"EntryType will be 'Negative'.\n\n" +
                        $"Do you want to continue?",
                        "Stock Warning",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                        return;
                }
            }
            // Parse UnitPrice
            if (!decimal.TryParse(TxtUnitPrice.Text, out decimal unitPrice))
            {
                unitPrice = 0;
            }

            // Update requester fields based on mode
            if (_isDeptLevel)
            {
                _request.EmpId = null;
                _request.EmployeeName = null;
                _request.ComId = saveComId;
                _request.DeptId = saveDeptId;
                _request.BranchId = saveBranchId;
                _request.CompanyName = (CmbCompanyEdit.SelectedItem as OrgEditItem)?.Name;
                _request.DepartmentName = saveDeptId.HasValue ? (CmbDeptEdit.SelectedItem as OrgEditItem)?.Name : null;
                _request.BranchName = saveBranchId.HasValue ? (CmbBranchEdit.SelectedItem as OrgEditItem)?.Name : null;
            }
            else
            {
                _request.EmpId = empItem.Id;
                _request.EmployeeName = empItem.Name;
                _request.ComId = null;
                _request.DeptId = null;
                _request.BranchId = null;
            }

            if (itemWasEdited)
            {
                _request.ItemId = itemItem.Id.Value;
                _request.ItemName = itemItem.Name;
            }

            if (quantityWasEdited)
            {
                _request.Quantity = quantity;
            }

            _request.DateRequested = DtDateRequested.SelectedDate ?? _request.DateCreated;
            _request.Description = TxtDescription.Text.Trim();
            _request.Remarks = TxtRemarks.Text.Trim();
            _request.Status = selectedStatus;
            _request.EntryType = entryType;

            DialogResult = true;
            Close();
        }

        private int GetItemStockOnHand(int itemId)
        {
            try
            {
                var cs = DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs))
                    return 0;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT StockOnHand FROM dbo.Item WHERE ItemId = @ItemId", con))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", itemId);
                        var result = cmd.ExecuteScalar();
                        return result != null ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, $"Failed to get stock info: {ex.Message}", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return 0;
            }
        }

        private void LoadOrgUnits()
        {
            _orgCompanies.Clear(); _orgDepts.Clear(); _orgBranches.Clear();
            CmbCompanyEdit.Items.Clear(); CmbDeptEdit.Items.Clear(); CmbBranchEdit.Items.Clear();

            try
            {
                var cs = DatabaseConfig.ConnectionString;
                if (string.IsNullOrWhiteSpace(cs)) return;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand("SELECT ComId, Name FROM dbo.Company ORDER BY Name", con))
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            var item = new OrgEditItem { Id = rdr.GetInt32(0), Name = rdr.GetString(1) };
                            _orgCompanies.Add(item);
                            CmbCompanyEdit.Items.Add(item);
                        }
                    }
                    CmbDeptEdit.Items.Add(new OrgEditItem { Id = 0, Name = "-- None --" });
                    using (var cmd = new SqlCommand("SELECT DeptId, Name FROM dbo.Department ORDER BY Name", con))
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            var item = new OrgEditItem { Id = rdr.GetInt32(0), Name = rdr.GetString(1) };
                            _orgDepts.Add(item);
                            CmbDeptEdit.Items.Add(item);
                        }
                    }
                    CmbBranchEdit.Items.Add(new OrgEditItem { Id = 0, Name = "-- None --" });
                    using (var cmd = new SqlCommand("SELECT BranchId, Name FROM dbo.Branch ORDER BY Name", con))
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            var item = new OrgEditItem { Id = rdr.GetInt32(0), Name = rdr.GetString(1) };
                            _orgBranches.Add(item);
                            CmbBranchEdit.Items.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, $"Failed to load org units: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pre-select existing values
            if (_request.ComId.HasValue)
                foreach (var item in CmbCompanyEdit.Items)
                    if (item is OrgEditItem oi && oi.Id == _request.ComId.Value) { CmbCompanyEdit.SelectedItem = item; break; }
            if (_request.DeptId.HasValue)
                foreach (var item in CmbDeptEdit.Items)
                    if (item is OrgEditItem oi && oi.Id == _request.DeptId.Value) { CmbDeptEdit.SelectedItem = item; break; }
            if (_request.BranchId.HasValue)
                foreach (var item in CmbBranchEdit.Items)
                    if (item is OrgEditItem oi && oi.Id == _request.BranchId.Value) { CmbBranchEdit.SelectedItem = item; break; }
        }

        private void SwitchToDeptMode()
        {
            _isDeptLevel = true;
            CmbEmployee.Visibility = Visibility.Collapsed;
            BtnEditEmployee.Visibility = Visibility.Collapsed;
            LblDeptLevelTag.Visibility = Visibility.Visible;
            LblEmployeeNumber.Visibility = Visibility.Collapsed;
            TxtEmployeeNumber.Visibility = Visibility.Collapsed;
            TxtCompany.Visibility = Visibility.Collapsed;    CmbCompanyEdit.Visibility = Visibility.Visible;
            TxtDepartment.Visibility = Visibility.Collapsed; CmbDeptEdit.Visibility = Visibility.Visible;
            TxtBranch.Visibility = Visibility.Collapsed;     CmbBranchEdit.Visibility = Visibility.Visible;
            LblEmployee.Text = "Requester";
            LblCompany.Text = "Company *";
            BtnSwitchMode.Content = "Switch to Employee";
            BtnSwitchMode.Background = new SolidColorBrush(Color.FromRgb(230, 255, 240));
            BtnSwitchMode.Foreground = new SolidColorBrush(Color.FromRgb(20, 140, 60));
            BtnSwitchMode.BorderBrush = new SolidColorBrush(Color.FromRgb(20, 140, 60));
        }

        private void SwitchToEmployeeMode()
        {
            _isDeptLevel = false;
            CmbEmployee.Visibility = Visibility.Visible;
            BtnEditEmployee.Visibility = Visibility.Visible;
            LblDeptLevelTag.Visibility = Visibility.Collapsed;
            LblEmployeeNumber.Visibility = Visibility.Visible;
            TxtEmployeeNumber.Visibility = Visibility.Visible;
            TxtCompany.Visibility = Visibility.Visible;    CmbCompanyEdit.Visibility = Visibility.Collapsed;
            TxtDepartment.Visibility = Visibility.Visible; CmbDeptEdit.Visibility = Visibility.Collapsed;
            TxtBranch.Visibility = Visibility.Visible;     CmbBranchEdit.Visibility = Visibility.Collapsed;
            LblEmployee.Text = "Employee *";
            LblCompany.Text = "Company";
            BtnSwitchMode.Content = "Switch to Dept. Level";
            BtnSwitchMode.Background = new SolidColorBrush(Color.FromRgb(230, 240, 255));
            BtnSwitchMode.Foreground = new SolidColorBrush(Color.FromRgb(30, 100, 200));
            BtnSwitchMode.BorderBrush = new SolidColorBrush(Color.FromRgb(180, 200, 240));
            // Ensure employee combo is locked when switching back
            if (CmbEmployee.IsEnabled)
                ToggleFieldLock(CmbEmployee, BtnEditEmployee);
            // Refresh org fields from selected employee
            if (CmbEmployee.SelectedItem is EmployeeItem empItem)
                LoadEmployeeDetails(empItem.Id);
            else
            {
                TxtCompany.Clear(); TxtDepartment.Clear();
                TxtBranch.Clear(); TxtEmployeeNumber.Clear();
            }
        }

        private void BtnSwitchMode_Click(object sender, RoutedEventArgs e)
        {
            if (!_isDeptLevel)
            {
                LoadOrgUnits();
                SwitchToDeptMode();
            }
            else
            {
                SwitchToEmployeeMode();
            }
        }

        private class EmployeeItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string EmployeeNumber { get; set; }
            public override string ToString() => Name;
        }

        private class CategoryItem
        {
            public int CategoryId { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private class ItemItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public int StockOnHand { get; set; }
            public int? CategoryId { get; set; }
            public string CategoryName { get; set; }
            public string SerialNumber { get; set; }
            public string ModelNumber { get; set; }
            public decimal Amount { get; set; }
            public bool IsTrackedAsset { get; set; }

            public string DisplayName =>
                string.IsNullOrWhiteSpace(SerialNumber) ? Name : $"{Name} (SN: {SerialNumber})";

            public override string ToString() => DisplayName;
        }

        private class OrgEditItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }
    }
}
