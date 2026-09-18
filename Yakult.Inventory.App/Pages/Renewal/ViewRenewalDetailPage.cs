using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Core;


namespace Yakult.Inventory.App.Pages.Renewal
{
    public partial class ViewRenewalDetailPage : Form
    {
        private readonly RenewalRepository _repository;
        private readonly int _setId;
        private int _itemId;
        private RenewalDetailDto _renewalDetail;
        private bool _isFirstTimeRenewal = false; // True if item has no renewal history yet

        /// <summary>
        /// Callback action to execute after successful renewal creation
        /// Used to navigate to ViewRenewalsPage after creating first renewal from Warranty page
        /// </summary>
        public Action OnRenewalCreated { get; set; }

        private ComboBox cboVendorName;
        private List<VendorDto> _vendors;
        private bool _isVendorComboInitializing;
        private DataGridView _dgvSetItems;
        private List<SetItemRenewalDto> _setItems;
        private List<SetItemRenewalDto> _allSetItems;
        private Label _lblItemCounts;
        private bool _suppressSelectionChange;

        // Set header edit controls (document number, reference number, document date)
        private TextBox _txtDocNum;
        private TextBox _txtRefNum;
        private DateTimePicker _dtpDocDate;

        // Renewal chain strip
        private Panel _pnlChain;
        private List<RenewalChainDto> _renewalChain;

        // Italic "countdown paused" note shown in panelTop when all items are renewed
        private Label _lblPausedNote;

        // Financial summary strip (Renewal Details tab bottom)
        private Label _lblFinancialStrip;
        // Financial summary strip (Invoice Items tab bottom)
        private Label _lblInvoiceItemsStrip;

        // All-items grid in Renewal Details tab (replaces single-item groupBoxItemInfo)
        private DataGridView _dgvRenewalItems;

        private sealed class SetDateRange
        {
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }
        }

        /// <summary>
        /// Constructor that accepts SetId (from ViewRenewalPage)
        /// </summary>
        public ViewRenewalDetailPage(int setId)
        {
            InitializeComponent();
            _repository = new RenewalRepository();
            _setId = setId;
            BuildSetItemsTab();
            BuildSetHeaderPanel();
            BuildChainStripPanel();
        }

        /// <summary>
        /// Constructor that accepts ItemId directly (from ViewWarrantyPage for first-time renewal)
        /// Use this for items that don't have a Set yet or are not part of a renewal Set
        /// </summary>
        public ViewRenewalDetailPage(int itemId, bool isItemIdConstructor)
        {
            InitializeComponent();
            _repository = new RenewalRepository();
            _itemId = itemId;
            _setId = 0; // No Set yet for items coming from Warranty
        }

        private async void ViewRenewalDetailPage_Load(object sender, EventArgs e)
        {
            await LoadRenewalDetailsAsync();
        }

        private async Task LoadRenewalDetailsAsync()
        {
            try
            {
                // If ItemId is not already set (from ItemId constructor), get it from SetId
                if (_itemId == 0 && _setId > 0)
                {
                    // First, get the ItemId from the SetId
                    // Query the first item in the set (sets for renewals typically have software/license or service items)
                    _itemId = await Task.Run(() => GetItemIdFromSet(_setId));

                    if (_itemId == 0)
                    {
                        MessageBox.Show("No items found in this set.", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        this.Close();
                        return;
                    }
                }
                else if (_itemId == 0)
                {
                    MessageBox.Show("Invalid item reference.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.Close();
                    return;
                }

                // Load renewal details from database
                _renewalDetail = await Task.Run(() => _repository.GetRenewalDetailsByItemId(_itemId));

                if (_renewalDetail == null)
                {
                    // FIRST-TIME RENEWAL MODE: Item has no renewal history yet
                    // Load basic item data instead of throwing error
                    _isFirstTimeRenewal = true;
                    _renewalDetail = await Task.Run(() => LoadItemDataWithoutRenewal(_itemId));

                    if (_renewalDetail == null)
                    {
                        MessageBox.Show("Item details not found.", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        this.Close();
                        return;
                    }
                }
                else
                {
                    // EXISTING RENEWAL MODE: Item has renewal history
                    _isFirstTimeRenewal = false;
                }

                if (_isFirstTimeRenewal)
                {
                    ApplyFirstTimeInvoiceBaselineAmount();
                }
                else
                {
                    ApplySubsequentRenewalBaselineAmount();
                }

                ApplyAuthoritativeDates();

                await EnsureVendorDropdownAsync();

                // Load PartNumber from dbo.Renewals for this item
                _renewalDetail.PartNumber = await Task.Run(() => LoadPartNumberFromRenewals(_itemId));

                // Populate fields
                PopulateFields();

                // Load all set items into the Invoice Items tab
                if (_setId > 0)
                {
                    await LoadSetItemsAsync();
                    await LoadSetHeaderAsync();
                    await LoadChainAsync();
                }

                // Load renewal history only if renewals exist
                if (!_isFirstTimeRenewal)
                {
                    await LoadRenewalHistoryAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading renewal details: {ex.Message}\n\n{ex.StackTrace}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Loads basic item data for first-time renewals (no renewal history exists)
        /// Uses the same query structure as RenewalRepository.GetRenewalDetailsByItemId_FallbackQuery
        /// but optimized for items from Warranty page that may not be in a Set yet
        /// </summary>
        private RenewalDetailDto LoadItemDataWithoutRenewal(int itemId)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
            {
                connection.Open();

                const string query = @"
SELECT
    i.ItemId,
    i.Name AS ItemName,
    i.Description,
    i.ItemType,
    i.SerialNumber,
    i.ModelNumber,
    i.LicenseNumber,
    i.Amount,
    i.StartDate,
    i.EndDate,
    i.DateCreated,
    i.DateModified,
    i.Category,
    i.Remarks AS ItemRemarks,
    i.Active,

    ic.Name AS CategoryName,

    i.VendorId AS VendorId,
    v.VendorName AS VendorName,
    v.Address AS VendorAddress,
    v.TIN AS VendorTIN,

    cond.ConditionName AS ConditionName,

    CASE
        WHEN i.EndDate IS NULL THEN NULL
        ELSE DATEDIFF(DAY, GETDATE(), i.EndDate)
    END AS DaysUntilExpiry,

    CASE
        WHEN i.EndDate IS NULL THEN 'No Expiry Date'
        WHEN DATEDIFF(DAY, GETDATE(), i.EndDate) < 0 THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), i.EndDate) <= 30 THEN 'Expiring Soon'
        WHEN DATEDIFF(DAY, GETDATE(), i.EndDate) <= 90 THEN 'Warning'
        ELSE 'Active'
    END AS ExpiryStatus,

    u.Name AS CreatedByUsername,

    NULL AS CurrentRenewalStatus,

    ISNULL(s.Subtotal, 0) AS Subtotal,
    ISNULL(s.VatAmount, 0) AS VatAmount,
    ISNULL(s.WhtAmount, 0) AS WhtAmount,
    ISNULL(s.DiscountAmount, 0) AS DiscountAmount,
    ISNULL(s.TotalAmountDue, 0) AS TotalAmountDue,

    s.CurrentBranchId,
    ISNULL(b.Name, 'N/A') AS CurrentBranchName,
    s.CurrentDepartmentId,
    ISNULL(d.Name, 'N/A') AS CurrentDepartmentName,
    s.Site AS SiteName

FROM dbo.Item i
LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.Vendor v ON i.VendorId = v.VendorID
LEFT JOIN dbo.Condition cond ON i.ConditionID = cond.ConditionID
LEFT JOIN dbo.[User] u ON i.CreatedBy = u.UserId
LEFT JOIN dbo.SetItem si ON i.ItemId = si.ItemId
LEFT JOIN dbo.[Set] s ON si.SetId = s.SetId AND s.IsInvoice = 1
LEFT JOIN dbo.Branch b ON s.CurrentBranchId = b.BranchId
LEFT JOIN dbo.Department d ON s.CurrentDepartmentId = d.DeptId
WHERE i.ItemId = @ItemId";

                using (var command = new System.Data.SqlClient.SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ItemId", itemId);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;

                        return new RenewalDetailDto
                        {
                            ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                            ItemName = reader.IsDBNull(reader.GetOrdinal("ItemName")) ? null : reader.GetString(reader.GetOrdinal("ItemName")),
                            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                            ItemType = reader.IsDBNull(reader.GetOrdinal("ItemType")) ? null : reader.GetString(reader.GetOrdinal("ItemType")),
                            SerialNumber = reader.IsDBNull(reader.GetOrdinal("SerialNumber")) ? null : reader.GetString(reader.GetOrdinal("SerialNumber")),
                            ModelNumber = reader.IsDBNull(reader.GetOrdinal("ModelNumber")) ? null : reader.GetString(reader.GetOrdinal("ModelNumber")),
                            LicenseNumber = reader.IsDBNull(reader.GetOrdinal("LicenseNumber")) ? null : reader.GetString(reader.GetOrdinal("LicenseNumber")),
                            Amount = reader.IsDBNull(reader.GetOrdinal("Amount")) ? 0m : reader.GetDecimal(reader.GetOrdinal("Amount")),
                            StartDate = reader.IsDBNull(reader.GetOrdinal("StartDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("StartDate")),
                            EndDate = reader.IsDBNull(reader.GetOrdinal("EndDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("EndDate")),
                            DateCreated = reader.IsDBNull(reader.GetOrdinal("DateCreated")) ? DateTime.Now : reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                            DateModified = reader.IsDBNull(reader.GetOrdinal("DateModified")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DateModified")),
                            Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
                            ItemRemarks = reader.IsDBNull(reader.GetOrdinal("ItemRemarks")) ? null : reader.GetString(reader.GetOrdinal("ItemRemarks")),
                            Active = reader.GetBoolean(reader.GetOrdinal("Active")),
                            CategoryName = reader.IsDBNull(reader.GetOrdinal("CategoryName")) ? null : reader.GetString(reader.GetOrdinal("CategoryName")),
                            VendorId = reader.IsDBNull(reader.GetOrdinal("VendorId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("VendorId")),
                            VendorName = reader.IsDBNull(reader.GetOrdinal("VendorName")) ? null : reader.GetString(reader.GetOrdinal("VendorName")),
                            VendorAddress = reader.IsDBNull(reader.GetOrdinal("VendorAddress")) ? null : reader.GetString(reader.GetOrdinal("VendorAddress")),
                            VendorTIN = reader.IsDBNull(reader.GetOrdinal("VendorTIN")) ? null : reader.GetString(reader.GetOrdinal("VendorTIN")),
                            ConditionName = reader.IsDBNull(reader.GetOrdinal("ConditionName")) ? null : reader.GetString(reader.GetOrdinal("ConditionName")),
                            DaysUntilExpiry = reader.IsDBNull(reader.GetOrdinal("DaysUntilExpiry")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("DaysUntilExpiry")),
                            ExpiryStatus = reader.IsDBNull(reader.GetOrdinal("ExpiryStatus")) ? null : reader.GetString(reader.GetOrdinal("ExpiryStatus")),
                            CreatedByUsername = reader.IsDBNull(reader.GetOrdinal("CreatedByUsername")) ? null : reader.GetString(reader.GetOrdinal("CreatedByUsername")),
                            CurrentRenewalStatus = reader.IsDBNull(reader.GetOrdinal("CurrentRenewalStatus")) ? null : reader.GetString(reader.GetOrdinal("CurrentRenewalStatus")),
                            Subtotal = reader.GetDecimal(reader.GetOrdinal("Subtotal")),
                            VatAmount = reader.GetDecimal(reader.GetOrdinal("VatAmount")),
                            WhtAmount = reader.GetDecimal(reader.GetOrdinal("WhtAmount")),
                            DiscountAmount = reader.GetDecimal(reader.GetOrdinal("DiscountAmount")),
                            TotalAmountDue = reader.GetDecimal(reader.GetOrdinal("TotalAmountDue")),
                            CurrentBranchId = reader.IsDBNull(reader.GetOrdinal("CurrentBranchId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("CurrentBranchId")),
                            CurrentBranchName = reader.IsDBNull(reader.GetOrdinal("CurrentBranchName")) ? null : reader.GetString(reader.GetOrdinal("CurrentBranchName")),
                            CurrentDepartmentId = reader.IsDBNull(reader.GetOrdinal("CurrentDepartmentId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("CurrentDepartmentId")),
                            CurrentDepartmentName = reader.IsDBNull(reader.GetOrdinal("CurrentDepartmentName")) ? null : reader.GetString(reader.GetOrdinal("CurrentDepartmentName")),
                            SiteName = reader.IsDBNull(reader.GetOrdinal("SiteName")) ? null : reader.GetString(reader.GetOrdinal("SiteName"))
                        };
                    }
                }
            }
        }

        private void ApplyFirstTimeInvoiceBaselineAmount()
        {
            if (_renewalDetail == null)
                return;

            var invoice = GetLatestInvoiceFinancialsByItemId(_itemId);
            if (invoice == null)
            {
                // Fallback only when there is genuinely no invoice/set financial data.
                if (_renewalDetail.Amount > 0m)
                {
                    _renewalDetail.Subtotal = _renewalDetail.Amount;
                    _renewalDetail.VatAmount = 0m;
                    _renewalDetail.WhtAmount = 0m;
                    _renewalDetail.DiscountAmount = 0m;
                    _renewalDetail.TotalAmountDue = _renewalDetail.Amount;
                }
                System.Diagnostics.Debug.WriteLine($"[Renewal] No invoice/set financials found for ItemId {_itemId}. Fallback to item Amount {_renewalDetail.Amount}");
                return;
            }

            _renewalDetail.Subtotal = invoice.Subtotal;
            _renewalDetail.VatAmount = invoice.VatAmount;
            _renewalDetail.WhtAmount = invoice.WhtAmount;
            _renewalDetail.DiscountAmount = invoice.DiscountAmount;
            _renewalDetail.TotalAmountDue = invoice.TotalAmountDue;

            // In renewal context, treat "Amount" as the payable baseline (what was paid), not Item.Amount.
            _renewalDetail.Amount = invoice.TotalAmountDue;

            System.Diagnostics.Debug.WriteLine($"[Renewal] Loaded invoice financials for ItemId {_itemId}: Subtotal={invoice.Subtotal}, Vat={invoice.VatAmount}, Wht={invoice.WhtAmount}, Discount={invoice.DiscountAmount}, Total={invoice.TotalAmountDue}");
        }

        private void ApplySubsequentRenewalBaselineAmount()
        {
            if (_renewalDetail == null)
                return;

            var lastRenewalAmount = GetLatestRenewalAmountByItemId(_itemId);
            var invoice = GetLatestInvoiceFinancialsByItemId(_itemId);

            // Always prefer invoice-set financials for baseline fields when available.
            if (invoice != null)
            {
                _renewalDetail.Subtotal = invoice.Subtotal;
                _renewalDetail.VatAmount = invoice.VatAmount;
                _renewalDetail.WhtAmount = invoice.WhtAmount;
                _renewalDetail.DiscountAmount = invoice.DiscountAmount;
                _renewalDetail.TotalAmountDue = invoice.TotalAmountDue;
                _renewalDetail.Amount = invoice.TotalAmountDue;
            }

            // If there is a prior renewal amount, treat that as the "renew same amount" baseline.
            if (lastRenewalAmount.HasValue && lastRenewalAmount.Value > 0m)
            {
                _renewalDetail.TotalAmountDue = lastRenewalAmount.Value;
                _renewalDetail.Amount = lastRenewalAmount.Value;
            }
            else if (invoice == null && _renewalDetail.Amount > 0m && _renewalDetail.TotalAmountDue <= 0m)
            {
                // Ultimate fallback: no invoice and no renewal history.
                _renewalDetail.Subtotal = _renewalDetail.Amount;
                _renewalDetail.VatAmount = 0m;
                _renewalDetail.WhtAmount = 0m;
                _renewalDetail.DiscountAmount = 0m;
                _renewalDetail.TotalAmountDue = _renewalDetail.Amount;
            }
        }

        private sealed class InvoiceFinancialSnapshot
        {
            public decimal Subtotal { get; set; }
            public decimal VatAmount { get; set; }
            public decimal WhtAmount { get; set; }
            public decimal DiscountAmount { get; set; }
            public decimal TotalAmountDue { get; set; }
        }

        private InvoiceFinancialSnapshot GetLatestInvoiceFinancialsByItemId(int itemId)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
            {
                connection.Open();

                // Priority: Use the same invoice-set financial fields shown in ViewInvoiceDetailPage.
                // Get invoice financial information using IsInvoice flag
                const string sql = @"
SELECT TOP 1
    s.Subtotal,
    s.VatAmount,
    s.WhtAmount,
    s.DiscountAmount,
    s.TotalAmountDue
FROM dbo.[Set] s
INNER JOIN dbo.SetItem si ON s.SetId = si.SetId
WHERE s.IsInvoice = 1
  AND si.ItemId = @ItemId
ORDER BY ISNULL(s.DispatchDate, s.CreatedAt) DESC";

                using (var command = new System.Data.SqlClient.SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@ItemId", itemId);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            System.Diagnostics.Debug.WriteLine($"[Renewal] No Invoice/Software/Service/Pending Set found for ItemId {itemId}");
                            return null;
                        }

                        var snapshot = new InvoiceFinancialSnapshot
                        {
                            Subtotal = reader.IsDBNull(0) ? 0m : reader.GetDecimal(0),
                            VatAmount = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
                            WhtAmount = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2),
                            DiscountAmount = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                            TotalAmountDue = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4)
                        };

                        // Only accept invoice data if it has a meaningful paid amount.
                        if (snapshot.TotalAmountDue <= 0m && snapshot.Subtotal <= 0m)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Renewal] Invoice Set found for ItemId {itemId} but financials are zero/empty");
                            return null;
                        }

                        return snapshot;
                    }
                }
            }
        }

        private decimal? GetLatestRenewalAmountByItemId(int itemId)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
            {
                connection.Open();

                const string sql = @"
SELECT TOP 1 r.RenewalAmount
FROM dbo.Renewals r
WHERE r.ItemId = @ItemId
  AND r.RenewalAmount IS NOT NULL
ORDER BY r.CreatedAt DESC";

                using (var command = new System.Data.SqlClient.SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@ItemId", itemId);
                    var result = command.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                        return null;

                    return Convert.ToDecimal(result);
                }
            }
        }

        private string LoadPartNumberFromRenewals(int itemId)
        {
            try
            {
                using (var connection = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
                {
                    connection.Open();
                    const string sql = @"
                        SELECT TOP 1 PartNumber
                        FROM dbo.Renewals
                        WHERE ItemId = @ItemId
                          AND IsArchived = 0";
                    using (var command = new System.Data.SqlClient.SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@ItemId", itemId);
                        var result = command.ExecuteScalar();
                        return (result == null || result == DBNull.Value) ? null : result.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RenewalDetail] Failed to load PartNumber: {ex.Message}");
                return null;
            }
        }

        // ── Set Header (Document Number / Reference Number / Document Date) ──────────

        private void BuildSetHeaderPanel()
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(245, 248, 252),
                Padding = new Padding(4, 8, 4, 8)
            };
            pnl.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(200, 210, 220)), 0, pnl.Height - 1, pnl.Width, pnl.Height - 1);

            int x = 10, y = 10, ctlH = 26;
            var lblFont = new Font("Segoe UI", 8.5F);
            var ctlFont = new Font("Segoe UI", 9F);
            var lblColor = Color.FromArgb(70, 85, 100);

            Label MakeLbl(string text, int px)
            {
                var lbl = new Label { Text = text, Location = new Point(px, y + 4), AutoSize = true, Font = lblFont, ForeColor = lblColor };
                pnl.Controls.Add(lbl);
                return lbl;
            }

            // Document Number
            var lblDocNum = MakeLbl("Document #:", x);
            x += lblDocNum.PreferredWidth + 8;
            _txtDocNum = new TextBox { Location = new Point(x, y), Width = 160, Height = ctlH, Font = ctlFont };
            pnl.Controls.Add(_txtDocNum);
            x += 168;

            // Reference Number
            var lblRefNum = MakeLbl("Reference #:", x);
            x += lblRefNum.PreferredWidth + 8;
            _txtRefNum = new TextBox { Location = new Point(x, y), Width = 160, Height = ctlH, Font = ctlFont };
            pnl.Controls.Add(_txtRefNum);
            x += 168;

            // Document Date
            var lblDocDate = MakeLbl("Document Date:", x);
            x += lblDocDate.PreferredWidth + 8;
            _dtpDocDate = new DateTimePicker
            {
                Location = new Point(x, y), Width = 140, Font = ctlFont,
                Format = DateTimePickerFormat.Custom, CustomFormat = "MM/dd/yyyy", Value = DateTime.Today
            };
            pnl.Controls.Add(_dtpDocDate);
            x += 148;

            // Save button
            var btnSave = new Button
            {
                Text = "Save Header",
                Location = new Point(x, y), Size = new Size(100, ctlH),
                BackColor = Color.FromArgb(52, 152, 219), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += async (s, ev) => await SaveSetHeaderAsync();
            pnl.Controls.Add(btnSave);

            panelMain.Controls.Add(pnl);
        }

        private async Task LoadSetHeaderAsync()
        {
            if (_setId <= 0 || _txtDocNum == null) return;
            try
            {
                var header = await Task.Run(() => _repository.GetSetHeader(_setId));
                if (header == null) return;
                _txtDocNum.Text   = header.DocumentNumber  ?? "";
                _txtRefNum.Text   = header.ReferenceNumber ?? "";
                _dtpDocDate.Value = header.DocumentDate?.Date ?? DateTime.Today;

                // Update panelTop header to show set-level identity instead of a single item name
                lblItemName.Text =
                    $"{header.SetCode ?? "—"}  ·  {header.DocumentNumber ?? "(no doc #)"}";
                string refDisplay = string.IsNullOrWhiteSpace(header.ReferenceNumber) ? "—" : header.ReferenceNumber;
                lblItemType.Text =
                    $"Ref: {refDisplay}  |  Date: {header.DocumentDate:MM/dd/yyyy}  |  By: {header.CreatedByUsername}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SetHeader] Load failed: {ex.Message}");
            }
        }

        private async Task SaveSetHeaderAsync()
        {
            if (_setId <= 0) return;
            try
            {
                string docNum = _txtDocNum.Text.Trim();
                string refNum = _txtRefNum.Text.Trim();
                DateTime docDate = _dtpDocDate.Value.Date;

                await Task.Run(() => _repository.UpdateSetHeader(
                    _setId,
                    string.IsNullOrEmpty(docNum) ? null : docNum,
                    string.IsNullOrEmpty(refNum) ? null : refNum,
                    docDate));

                MessageBox.Show("Invoice header saved.", "Saved",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save header:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Renewal chain strip ────────────────────────────────────────────────────

        private void BuildChainStripPanel()
        {
            _pnlChain = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Color.FromArgb(237, 241, 247),
                Padding = new Padding(8, 6, 8, 4)
            };
            _pnlChain.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(200, 210, 220)),
                    0, _pnlChain.Height - 1, _pnlChain.Width, _pnlChain.Height - 1);
            panelMain.Controls.Add(_pnlChain);
        }

        private async Task LoadChainAsync()
        {
            if (_setId == 0 || _pnlChain == null) return;
            try
            {
                _renewalChain = await Task.Run(() => _repository.GetRenewalChain(_setId));
                BuildChainStripControls();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChainStrip] Load failed: {ex.Message}");
            }
        }

        private void BuildChainStripControls()
        {
            if (_pnlChain == null || _renewalChain == null) return;

            _pnlChain.Controls.Clear();

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };

            var displayChain = Enumerable.Reverse(_renewalChain).ToList();
            for (int i = 0; i < displayChain.Count; i++)
            {
                var entry = displayChain[i];
                bool isCurrent = entry.SetId == _setId;

                var lnk = new LinkLabel
                {
                    Text = $"#{entry.RenewalNumber} {entry.SetCode}",
                    AutoSize = true,
                    Font = isCurrent
                        ? new Font("Segoe UI", 8.5F, FontStyle.Bold)
                        : new Font("Segoe UI", 8.5F),
                    Margin = new Padding(0, 2, 0, 0),
                    LinkBehavior = LinkBehavior.HoverUnderline
                };

                if (isCurrent)
                {
                    lnk.LinkColor = Color.FromArgb(21, 67, 130);
                    lnk.ActiveLinkColor = Color.FromArgb(21, 67, 130);
                    lnk.DisabledLinkColor = Color.FromArgb(21, 67, 130);
                    lnk.Enabled = false;  // current page — no navigation needed
                }
                else
                {
                    int capturedSetId = entry.SetId;
                    lnk.LinkColor = Color.FromArgb(41, 128, 185);
                    lnk.Click += (s, ev) => new ViewRenewalDetailPage(capturedSetId).Show(this);
                }

                flow.Controls.Add(lnk);

                if (i < displayChain.Count - 1)
                {
                    flow.Controls.Add(new Label
                    {
                        Text = "→",
                        AutoSize = true,
                        ForeColor = Color.Gray,
                        Margin = new Padding(2, 2, 2, 0),
                        Font = new Font("Segoe UI", 8.5F)
                    });
                }
            }

            _pnlChain.Controls.Add(flow);
        }

        // ── Invoice Items tab ──────────────────────────────────────────────────────

        private void BuildSetItemsTab()
        {
            _dgvSetItems = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(230, 230, 230),
                BackgroundColor = Color.White,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 36,
                RowTemplate = { Height = 55 }
            };

            _dgvSetItems.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(52, 73, 94),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 8, 0)
            };
            _dgvSetItems.EnableHeadersVisualStyles = false;
            _dgvSetItems.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(248, 249, 252)
            };
            _dgvSetItems.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            DataGridViewCellStyle rightAlign = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
            DataGridViewCellStyle centerAlign = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };

            _dgvSetItems.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            _dgvSetItems.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "colDesc",   DataPropertyName = "Description",  HeaderText = "Description",  FillWeight = 28, MinimumWidth = 140, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True, Padding = new Padding(8, 4, 8, 4) } },
                new DataGridViewTextBoxColumn { Name = "colCode",   DataPropertyName = "ItemCode",      HeaderText = "Item Code",    FillWeight = 12, MinimumWidth = 90  },
                new DataGridViewTextBoxColumn { Name = "colQty",    DataPropertyName = "Quantity",      HeaderText = "Qty",          FillWeight = 7,  MinimumWidth = 55,  DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Format = "N0" } },
                new DataGridViewTextBoxColumn { Name = "colUoM",    DataPropertyName = "UnitOfMeasure", HeaderText = "UoM",          FillWeight = 7,  MinimumWidth = 55  },
                new DataGridViewTextBoxColumn { Name = "colPrice",  DataPropertyName = "UnitPrice",     HeaderText = "Unit Price",   FillWeight = 11, MinimumWidth = 85,  DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } },
                new DataGridViewTextBoxColumn { Name = "colAmount", DataPropertyName = "Amount",        HeaderText = "Amount",       FillWeight = 11, MinimumWidth = 85,  DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } },
                new DataGridViewTextBoxColumn { Name = "colStart",  DataPropertyName = "LineStartDate", HeaderText = "Line Start",   FillWeight = 10, MinimumWidth = 90,  DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy" } },
                new DataGridViewTextBoxColumn { Name = "colEnd",    DataPropertyName = "LineEndDate",   HeaderText = "Line End",     FillWeight = 10, MinimumWidth = 90,  DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy" } },
                new DataGridViewTextBoxColumn { Name = "colStatus", DataPropertyName = "DisplayStatus", HeaderText = "Status",       FillWeight = 9,  MinimumWidth = 75  },
            });

            _dgvSetItems.CellFormatting   += DgvSetItems_CellFormatting;
            _dgvSetItems.CellContentClick += DgvSetItems_CellContentClick;
            _dgvSetItems.SelectionChanged += DgvSetItems_SelectionChanged;

            _lblItemCounts = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(90, 90, 90),
                Padding = new Padding(8, 4, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Loading items..."
            };

            var btnRenewItems = new Button
            {
                Text = "Renew Items",
                Dock = DockStyle.Right,
                Width = 130,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnRenewItems.FlatAppearance.BorderSize = 0;
            btnRenewItems.Click += (s, e) =>
            {
                var renewable = (_allSetItems ?? new List<SetItemRenewalDto>()).Where(x => x.CanRenew).ToList();
                if (renewable.Count == 0)
                {
                    MessageBox.Show("No active items available to renew.", "Renew Items",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                PromptAndRenewItem(renewable);
            };

            // Same "create-or-open" viewer ViewInvoiceDetailPage.AttachReceiptDirectly() uses:
            // loads the existing linked ReceiptSet for editing if one exists, or starts a blank
            // one that auto-links to this Set on Save if not.
            var btnAttachReceipt = new Button
            {
                Text = "Attach Receipt",
                Dock = DockStyle.Right,
                Width = 130,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnAttachReceipt.FlatAppearance.BorderSize = 0;
            btnAttachReceipt.Click += (s, e) =>
            {
                try
                {
                    Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForSet(this, _setId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to open receipt set viewer: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var pnlItemsHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32
            };
            pnlItemsHeader.Controls.Add(_lblItemCounts);
            pnlItemsHeader.Controls.Add(btnRenewItems);
            pnlItemsHeader.Controls.Add(btnAttachReceipt);

            _lblInvoiceItemsStrip = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = Color.FromArgb(240, 242, 248),
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(55, 55, 55),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 12, 0),
                BorderStyle = BorderStyle.FixedSingle,
                Text = ""
            };

            var tabPageItems = new TabPage("Invoice Items");
            tabPageItems.Controls.Add(_dgvSetItems);         // Fill  (index 0)
            tabPageItems.Controls.Add(pnlItemsHeader);       // Top   (index 1)
            tabPageItems.Controls.Add(_lblInvoiceItemsStrip);// Bottom(index 2)

            // Re-add tabs in desired order (Invoice Items first, then existing Designer tabs)
            var existingTabs = tabControl.TabPages.Cast<TabPage>().ToList();
            tabControl.TabPages.Clear();
            tabControl.TabPages.Add(tabPageItems);
            foreach (var tab in existingTabs)
                tabControl.TabPages.Add(tab);
            tabControl.SelectedIndex = 0;

            // ── Replace single-item groupBoxItemInfo with a full-width DataGridView
            //    showing ALL set items. groupBoxItemInfo is hidden to reclaim its space.
            groupBoxItemInfo.Visible = false;

            // ── 3-column layout below the items grid (y=370):
            //    Col 1 (x=10):  Archive + Financial  (original 450px width)
            //    Col 2 (x=470): Renewal Info         (original 450px width)
            //    Col 3 (x=930): Vendor Info + Site   (original 450px width)
            groupBoxArchive.Location       = new Point(10,  370);
            groupBoxFinancialInfo.Location = new Point(10,  500);

            groupBoxRenewalInfo.Location   = new Point(470, 370);

            groupBoxVendorInfo.Location    = new Point(930, 370);
            groupBoxSiteInfo.Location      = new Point(930, 600);

            _dgvRenewalItems = new DataGridView
            {
                Location  = new Point(10, 10),
                Size      = new Size(1370, 350),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly  = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(220, 220, 220),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.Vertical,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 32,
                RowTemplate = { Height = 50 }
            };
            _dgvRenewalItems.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(52, 73, 94), ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0)
            };
            _dgvRenewalItems.EnableHeadersVisualStyles = false;
            _dgvRenewalItems.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 249, 252) };
            _dgvRenewalItems.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
            _dgvRenewalItems.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "riDesc",   DataPropertyName = "Description",  HeaderText = "Description",  FillWeight = 32, MinimumWidth = 140, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } },
                new DataGridViewTextBoxColumn { Name = "riCode",   DataPropertyName = "ItemCode",      HeaderText = "Item Code",    FillWeight = 12, MinimumWidth = 90,  DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } },
                new DataGridViewTextBoxColumn { Name = "riQty",    DataPropertyName = "Quantity",      HeaderText = "Qty",          FillWeight = 6,  MinimumWidth = 50,  DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Format = "N0" } },
                new DataGridViewTextBoxColumn { Name = "riUoM",    DataPropertyName = "UnitOfMeasure", HeaderText = "UoM",          FillWeight = 7,  MinimumWidth = 55  },
                new DataGridViewTextBoxColumn { Name = "riPrice",  DataPropertyName = "UnitPrice",     HeaderText = "Unit Price",   FillWeight = 10, MinimumWidth = 80,  DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } },
                new DataGridViewTextBoxColumn { Name = "riAmount", DataPropertyName = "Amount",        HeaderText = "Amount",       FillWeight = 10, MinimumWidth = 80,  DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } },
                new DataGridViewTextBoxColumn { Name = "riStart",  DataPropertyName = "LineStartDate", HeaderText = "Start",        FillWeight = 10, MinimumWidth = 85,  DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy" } },
                new DataGridViewTextBoxColumn { Name = "riEnd",    DataPropertyName = "LineEndDate",   HeaderText = "End",          FillWeight = 10, MinimumWidth = 85,  DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy" } },
                new DataGridViewTextBoxColumn { Name = "riStatus", DataPropertyName = "DisplayStatus", HeaderText = "Status",       FillWeight = 8,  MinimumWidth = 70  },
            });
            panelDetails.Controls.Add(_dgvRenewalItems);

            // ── Financial summary strip pinned to the bottom of the Renewal Details tab,
            //    outside the scrollable panelDetails so it's always visible.
            _lblFinancialStrip = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = Color.FromArgb(240, 242, 248),
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(55, 55, 55),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 12, 0),
                BorderStyle = BorderStyle.FixedSingle,
                Text = ""
            };
            tabPageDetails.Controls.Add(_lblFinancialStrip);

            tabControl.Refresh();
        }

        private async Task LoadSetItemsAsync()
        {
            try
            {
                var items = await Task.Run(() => _repository.GetSetItemsForRenewal(_setId));
                _suppressSelectionChange = true;
                _allSetItems = items;
                _setItems = items;
                _dgvSetItems.DataSource = null;
                _dgvSetItems.DataSource = _setItems;
                if (_dgvRenewalItems != null)
                {
                    _dgvRenewalItems.DataSource = null;
                    _dgvRenewalItems.DataSource = _setItems;
                }
                _suppressSelectionChange = false;
                UpdateItemCountsLabel();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SetItems] {ex.Message}");
            }
        }

        private void UpdateItemCountsLabel()
        {
            if (_allSetItems == null || _lblItemCounts == null) return;
            _lblItemCounts.Text =
                $"Total: {_allSetItems.Count}   |   " +
                $"Active: {_allSetItems.Count(x => string.IsNullOrEmpty(x.RenewalStatus))}   |   " +
                $"Renewed: {_allSetItems.Count(x => x.RenewalStatus == "Renewed")}   |   " +
                $"Archived: {_allSetItems.Count(x => x.RenewalStatus == "Archived")}";
        }

        private void DgvSetItems_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var item = _dgvSetItems.Rows[e.RowIndex].DataBoundItem as SetItemRenewalDto;
            if (item == null) return;

            if (_dgvSetItems.Columns[e.ColumnIndex].Name == "colStatus")
            {
                switch (item.DisplayStatus)
                {
                    case "Active":
                        e.CellStyle.ForeColor = Color.FromArgb(39, 174, 96);
                        e.CellStyle.Font = new Font(_dgvSetItems.Font.FontFamily, _dgvSetItems.Font.Size, FontStyle.Bold);
                        break;
                    case "Expired":
                        e.CellStyle.ForeColor = Color.FromArgb(192, 57, 43);
                        e.CellStyle.Font = new Font(_dgvSetItems.Font.FontFamily, _dgvSetItems.Font.Size, FontStyle.Bold);
                        break;
                    case "Renewed":
                        e.CellStyle.ForeColor = Color.FromArgb(41, 128, 185);
                        break;
                    case "Archived":
                        e.CellStyle.ForeColor = Color.FromArgb(149, 165, 166);
                        break;
                }
            }

        }

        private void DgvSetItems_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void DgvSetItems_SelectionChanged(object sender, EventArgs e)
        {
            // Row selection in the Invoice Items grid is for visual navigation only.
            // The header (Set-Code, Document #, etc.) reflects the set, not the selected item.
        }

        private async Task SwitchToItemAsync(int itemId)
        {
            _itemId = itemId;
            _renewalDetail = await Task.Run(() => _repository.GetRenewalDetailsByItemId(_itemId));
            if (_renewalDetail == null)
            {
                _isFirstTimeRenewal = true;
                _renewalDetail = await Task.Run(() => LoadItemDataWithoutRenewal(_itemId));
                if (_renewalDetail == null) return;
            }
            else
            {
                _isFirstTimeRenewal = false;
            }

            if (_isFirstTimeRenewal) ApplyFirstTimeInvoiceBaselineAmount();
            else ApplySubsequentRenewalBaselineAmount();
            ApplyAuthoritativeDates();
            await EnsureVendorDropdownAsync();
            _renewalDetail.PartNumber = await Task.Run(() => LoadPartNumberFromRenewals(_itemId));
            PopulateFields();

            // History tab is set-level — loaded once on page open, not per-item.
        }

        private async void PromptAndRenewItem(List<SetItemRenewalDto> items)
        {
            if (items == null || items.Count == 0) return;

            List<ItemCatalogDto> catalog;
            try { catalog = _repository.GetRenewableItemCatalog(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load item catalog:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            decimal baseSubtotal = (_renewalDetail?.Subtotal > 0 ? _renewalDetail.Subtotal : _renewalDetail?.TotalAmountDue) ?? 0m;
            decimal baseVatPct   = (_renewalDetail?.Subtotal > 0m) ? Math.Min(100m, Math.Max(0m, decimal.Round((_renewalDetail.VatAmount / _renewalDetail.Subtotal) * 100m, 2))) : 0m;
            decimal baseWhtPct   = (_renewalDetail?.Subtotal > 0m) ? Math.Min(100m, Math.Max(0m, decimal.Round((_renewalDetail.WhtAmount / _renewalDetail.Subtotal) * 100m, 2))) : 0m;
            decimal baseDiscount = _renewalDetail?.DiscountAmount ?? 0m;
            decimal originalTotal = _renewalDetail?.TotalAmountDue ?? 0m;

            string title = items.Count == 1
                ? $"Renew Item — {items[0].Description}"
                : $"Renew Items ({items.Count})";

            // The WPF window owns the whole renewal now (CreateRenewalSet + RenewSingleItem loop
            // + success/error messaging + the "attach scanned documents" prompt) — this WinForms
            // caller just shows it (owned via WindowInteropHelper) and reloads its own grids
            // afterward. Keeping the window open on failure (instead of only finding out after it
            // already closed) lets the user retry without re-entering everything.
            var dlg = new Yakult.Inventory.App.WPF.Renewal.RenewItems.Views.RenewItemsWindow(
                _setId, title, items, catalog, baseSubtotal, baseVatPct, baseWhtPct, baseDiscount, originalTotal);
            new System.Windows.Interop.WindowInteropHelper(dlg).Owner = this.Handle;
            if (dlg.ShowDialog() != true) return;

            await LoadSetItemsAsync();
            await LoadChainAsync();
            await LoadRenewalHistoryAsync();
        }

        // ── End Invoice Items tab ──────────────────────────────────────────────────

        /// <summary>
        /// Gets the first ItemId from a SetId (for renewal sets)
        /// </summary>
        private int GetItemIdFromSet(int setId)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
            {
                connection.Open();
                var query = @"
                    SELECT TOP 1 ItemId
                    FROM dbo.SetItem
                    WHERE SetId = @SetId
                    ORDER BY ItemCode";

                using (var command = new System.Data.SqlClient.SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SetId", setId);
                    var result = command.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
            }
        }

        private SetDateRange GetSetDateRangeIfSoftwareOrService(int setId)
        {
            using (var connection = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
            {
                connection.Open();

                var query = @"
                    SELECT StartDate, EndDate
                    FROM dbo.[Set]
                    WHERE SetId = @SetId
                      AND IsInvoice = 1";

                using (var command = new System.Data.SqlClient.SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SetId", setId);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;

                        return new SetDateRange
                        {
                            StartDate = reader.IsDBNull(0) ? (DateTime?)null : reader.GetDateTime(0),
                            EndDate = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1)
                        };
                    }
                }
            }
        }

        private void ApplyAuthoritativeDates()
        {
            if (_renewalDetail == null)
                return;

            var setDates = GetSetDateRangeIfSoftwareOrService(_setId);
            if (setDates == null)
                return;

            _renewalDetail.StartDate = setDates.StartDate;
            _renewalDetail.EndDate = setDates.EndDate;

            if (!_renewalDetail.EndDate.HasValue)
            {
                _renewalDetail.DaysUntilExpiry = null;
                _renewalDetail.ExpiryStatus = "No Expiry Date";
                return;
            }

            int daysUntilExpiry = (int)(_renewalDetail.EndDate.Value.Date - DateTime.Today).TotalDays;
            _renewalDetail.DaysUntilExpiry = daysUntilExpiry;

            if (daysUntilExpiry < 0)
                _renewalDetail.ExpiryStatus = "Expired";
            else if (daysUntilExpiry <= 30)
                _renewalDetail.ExpiryStatus = "Expiring Soon";
            else if (daysUntilExpiry <= 90)
                _renewalDetail.ExpiryStatus = "Warning";
            else
                _renewalDetail.ExpiryStatus = "Active";
        }

        /// <summary>
        /// Gets the first valid UserId from the User table as a fallback
        /// </summary>
        private int GetFirstValidUserId()
        {
            try
            {
                using (var connection = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
                {
                    connection.Open();
                    var query = "SELECT TOP 1 UserId FROM dbo.[User] ORDER BY UserId";

                    using (var command = new System.Data.SqlClient.SqlCommand(query, connection))
                    {
                        var result = command.ExecuteScalar();
                        return result != null ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        private void PopulateFields()
        {
            if (_renewalDetail == null) return;

            // Update header
            lblItemName.Text = _renewalDetail.ItemName ?? "N/A";
            lblItemType.Text = _renewalDetail.ItemType ?? "N/A";

            // Item Information
            txtItemName.Text = _renewalDetail.ItemName ?? "";
            txtDescription.Text = _renewalDetail.Description ?? "";
            txtItemType.Text = _renewalDetail.ItemType ?? "";
            txtCategory.Text = _renewalDetail.CategoryName ?? "";
            txtSerialNumber.Text = _renewalDetail.SerialNumber ?? "";
            txtModelNumber.Text = _renewalDetail.ModelNumber ?? "";
            txtLicenseNumber.Text = _renewalDetail.LicenseNumber ?? "";
            txtPartNumber.Text = _renewalDetail.PartNumber ?? "";
            txtAmount.Text = _renewalDetail.TotalAmountDue.ToString("N2");


            // Renewal Information
            txtRenewalStatus.Text = _renewalDetail.CurrentRenewalStatus ?? "None";

            if (_renewalDetail.StartDate.HasValue)
                dtpStartDate.Value = _renewalDetail.StartDate.Value;

            if (_renewalDetail.EndDate.HasValue)
                dtpEndDate.Value = _renewalDetail.EndDate.Value;

            // Calculate and display days left
            if (_renewalDetail.DaysUntilExpiry.HasValue)
            {
                txtDaysLeft.Text = _renewalDetail.DaysUntilExpiry.Value.ToString();

                // Set color based on days left
                if (_renewalDetail.DaysUntilExpiry.Value < 0)
                {
                    txtDaysLeft.ForeColor = Color.Red;
                }
                else if (_renewalDetail.DaysUntilExpiry.Value <= 30)
                {
                    txtDaysLeft.ForeColor = Color.Orange;
                }
                else if (_renewalDetail.DaysUntilExpiry.Value <= 90)
                {
                    txtDaysLeft.ForeColor = Color.Goldenrod;
                }
                else
                {
                    txtDaysLeft.ForeColor = Color.Green;
                }
            }
            else
            {
                txtDaysLeft.Text = "N/A";
                txtDaysLeft.ForeColor = Color.Gray;
            }

            // Update expiry status labels
            UpdateExpiryStatusLabels();

            // Vendor Information (Editable: VendorName, Address, TIN)
            txtVendorAddress.Text = _renewalDetail.VendorAddress ?? "";
            txtVendorTIN.Text = _renewalDetail.VendorTIN ?? "";

            ApplyVendorSelectionFromRenewalDetail();

            // Enable/disable vendor edit based on archive status
            bool isArchived = !_renewalDetail.Active;
            if (cboVendorName != null)
                cboVendorName.Enabled = !isArchived;
            txtVendorName.ReadOnly = true;
            txtVendorAddress.ReadOnly = isArchived;
            txtVendorTIN.ReadOnly = isArchived;
            btnSaveVendor.Enabled = !isArchived;

            // Site Information
            txtSiteDisplay.Text = _renewalDetail.SiteDisplay ?? "N/A";

            // Financial Information
            txtSubtotal.Text = _renewalDetail.Subtotal.ToString("N2");
            txtVatAmount.Text = _renewalDetail.VatAmount.ToString("N2");
            txtWhtAmount.Text = _renewalDetail.WhtAmount.ToString("N2");
            txtDiscountAmount.Text = _renewalDetail.DiscountAmount.ToString("N2");
            txtTotalAmountDue.Text = _renewalDetail.TotalAmountDue.ToString("N2");

            string financialText =
                $"Subtotal: {_renewalDetail.Subtotal:N2}  |  " +
                $"VAT Amount: {_renewalDetail.VatAmount:N2}  |  " +
                $"WHT Amount: {_renewalDetail.WhtAmount:N2}  |  " +
                $"Discount: {_renewalDetail.DiscountAmount:N2}  |  " +
                $"Total Amount: {_renewalDetail.TotalAmountDue:N2}";
            if (_lblFinancialStrip != null)
                _lblFinancialStrip.Text = financialText;
            if (_lblInvoiceItemsStrip != null)
                _lblInvoiceItemsStrip.Text = financialText;

            // Archive Information
            chkArchived.Checked = !_renewalDetail.Active;
            txtArchiveReason.Text = ""; // Archive reason not stored in Item table, only in Renewals

            // Enable/disable archive controls based on status
            chkArchived.Enabled = !isArchived; // Can only check, not uncheck once archived
            txtArchiveReason.ReadOnly = isArchived; // Can edit reason if not archived

            // Metadata
            lblCreatedBy.Text = $"Created By: {_renewalDetail.CreatedByUsername ?? "Unknown"}";
            lblCreatedAt.Text = $"Created: {_renewalDetail.DateCreated:yyyy-MM-dd}";

            // Hide actions when already archived
            if (btnArchive != null) btnArchive.Visible = _renewalDetail.Active;
            // btnCreateRenewal is replaced by the combined "Renew All" button in BuildSetItemsTab
            if (btnCreateRenewal != null) btnCreateRenewal.Visible = false;
        }

        private void UpdateExpiryStatusLabels()
        {
            if (_renewalDetail == null) return;

            // If this set has been renewed (another set points to it via RenewalOfSetId),
            // stop counting expiry. Detected via _renewalChain: if this set is not the last
            // in the chain, a newer renewal set exists.
            bool hasBeenRenewed = _renewalChain != null
                && _renewalChain.Count > 1
                && _renewalChain.Last().SetId != _setId;

            if (hasBeenRenewed)
            {
                // Freeze the countdown at the date the next renewal started so
                // "countdown paused" doesn't keep ticking with the live GETDATE() value.
                DateTime frozenDate = DateTime.Today;
                int idx = _renewalChain.FindIndex(c => c.SetId == _setId);
                if (idx >= 0 && idx + 1 < _renewalChain.Count && _renewalChain[idx + 1].StartDate.HasValue)
                    frozenDate = _renewalChain[idx + 1].StartDate.Value.Date;
                DateTime? ownEndDate = idx >= 0 ? _renewalChain[idx].EndDate : _renewalDetail.EndDate;
                int daysLeftForRenewed = ownEndDate.HasValue
                    ? (int)(ownEndDate.Value.Date - frozenDate).TotalDays
                    : 0;
                // Build the normal expiry text then append (Renewed)
                string baseText;
                if (daysLeftForRenewed < 0)
                    baseText = $"EXPIRED ({Math.Abs(daysLeftForRenewed)} days ago)";
                else if (daysLeftForRenewed <= 30)
                    baseText = $"EXPIRING IN {daysLeftForRenewed} DAYS";
                else if (daysLeftForRenewed <= 90)
                    baseText = $"Warning: {daysLeftForRenewed} days remaining";
                else
                    baseText = $"Active ({daysLeftForRenewed} days remaining)";

                lblExpiryStatus.Text = baseText + "  (Renewed)";
                lblExpiryStatus.ForeColor = Color.White;

                // Lazy-create the italic "countdown paused" note in panelTop
                if (_lblPausedNote == null)
                {
                    _lblPausedNote = new Label
                    {
                        AutoSize = true,
                        Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                        ForeColor = Color.FromArgb(220, 230, 240),
                        Text = "countdown paused"
                    };
                    panelTop.Controls.Add(_lblPausedNote);
                }
                // Position just below lblExpiryStatus
                _lblPausedNote.Location = new Point(
                    lblExpiryStatus.Left,
                    lblExpiryStatus.Bottom + 2);
                _lblPausedNote.Visible = true;

                lblExpiryWarning.Text = "This set has been renewed";
                lblExpiryWarning.ForeColor = Color.FromArgb(39, 174, 96);
                return;
            }

            // Hide the paused note when not all items are renewed
            if (_lblPausedNote != null) _lblPausedNote.Visible = false;

            string statusMessage;
            Color statusColor;

            if (!_renewalDetail.DaysUntilExpiry.HasValue)
            {
                statusMessage = "No End Date Set";
                statusColor = Color.Gray;
                lblExpiryStatus.Text = statusMessage;
                lblExpiryStatus.ForeColor = Color.White;
                lblExpiryWarning.Text = statusMessage;
                lblExpiryWarning.ForeColor = statusColor;
                return;
            }

            int daysLeft = _renewalDetail.DaysUntilExpiry.Value;

            // Top header status
            if (daysLeft < 0)
            {
                statusMessage = $"EXPIRED ({Math.Abs(daysLeft)} days ago)";
                statusColor = Color.Red;
            }
            else if (daysLeft <= 30)
            {
                statusMessage = $"EXPIRING IN {daysLeft} DAYS";
                statusColor = Color.Orange;
            }
            else if (daysLeft <= 90)
            {
                statusMessage = $"Warning: {daysLeft} days remaining";
                statusColor = Color.Goldenrod;
            }
            else
            {
                statusMessage = $"Active ({daysLeft} days remaining)";
                statusColor = Color.Green;
            }

            lblExpiryStatus.Text = statusMessage;
            lblExpiryStatus.ForeColor = Color.White;

            // Detailed warning message
            string warningMessage;
            Color warningColor;

            if (daysLeft < 0)
            {
                warningMessage = "LICENSE EXPIRED";
                warningColor = Color.Red;
            }
            else if (daysLeft < 30)
            {
                warningMessage = "Less than 1 month before expiry";
                warningColor = Color.Red;
            }
            else if (daysLeft < 60)
            {
                warningMessage = "1-2 months before expiry";
                warningColor = Color.Orange;
            }
            else if (daysLeft < 90)
            {
                warningMessage = "2-3 months before expiry";
                warningColor = Color.Goldenrod;
            }
            else if (daysLeft < 120)
            {
                warningMessage = "3-4 months before expiry";
                warningColor = Color.DarkOrange;
            }
            else
            {
                warningMessage = "More than 4 months before expiry";
                warningColor = Color.Green;
            }

            lblExpiryWarning.Text = warningMessage;
            lblExpiryWarning.ForeColor = warningColor;
        }

        private async Task LoadRenewalHistoryAsync()
        {
            try
            {
                bool isSetLevel = _setId > 0;
                var history = isSetLevel
                    ? await Task.Run(() => _repository.GetRenewalHistoryBySetId(_setId))
                    : await Task.Run(() => _repository.GetRenewalHistoryByItemId(_itemId));

                dgvRenewalHistory.DataSource = history;

                if (dgvRenewalHistory.Columns.Count > 0)
                {
                    // Hide ID/audit columns
                    if (dgvRenewalHistory.Columns.Contains("RenewalId"))
                        dgvRenewalHistory.Columns["RenewalId"].Visible = false;
                    if (dgvRenewalHistory.Columns.Contains("ItemId"))
                        dgvRenewalHistory.Columns["ItemId"].Visible = false;
                    if (dgvRenewalHistory.Columns.Contains("CreatedBy"))
                        dgvRenewalHistory.Columns["CreatedBy"].Visible = false;
                    if (dgvRenewalHistory.Columns.Contains("ModifiedBy"))
                        dgvRenewalHistory.Columns["ModifiedBy"].Visible = false;
                    if (dgvRenewalHistory.Columns.Contains("ModifiedByUsername"))
                        dgvRenewalHistory.Columns["ModifiedByUsername"].Visible = false;
                    if (dgvRenewalHistory.Columns.Contains("ModifiedAt"))
                        dgvRenewalHistory.Columns["ModifiedAt"].Visible = false;

                    // ItemName and ItemCode — visible only for set-level history, pinned to front
                    if (dgvRenewalHistory.Columns.Contains("ItemName"))
                    {
                        dgvRenewalHistory.Columns["ItemName"].Visible = isSetLevel;
                        if (isSetLevel)
                        {
                            dgvRenewalHistory.Columns["ItemName"].HeaderText = "Item Name";
                            dgvRenewalHistory.Columns["ItemName"].DisplayIndex = 0;
                        }
                    }
                    if (dgvRenewalHistory.Columns.Contains("ItemCode"))
                    {
                        dgvRenewalHistory.Columns["ItemCode"].Visible = isSetLevel;
                        if (isSetLevel)
                        {
                            dgvRenewalHistory.Columns["ItemCode"].HeaderText = "Item Code";
                            dgvRenewalHistory.Columns["ItemCode"].DisplayIndex = 1;
                        }
                    }

                    // Set headers
                    if (dgvRenewalHistory.Columns.Contains("RenewalStatus"))
                        dgvRenewalHistory.Columns["RenewalStatus"].HeaderText = "Status";
                    if (dgvRenewalHistory.Columns.Contains("OnHoldDate"))
                        dgvRenewalHistory.Columns["OnHoldDate"].HeaderText = "On Hold Date";
                    if (dgvRenewalHistory.Columns.Contains("RenewedDate"))
                        dgvRenewalHistory.Columns["RenewedDate"].HeaderText = "Renewed Date";
                    if (dgvRenewalHistory.Columns.Contains("RenewalCount"))
                        dgvRenewalHistory.Columns["RenewalCount"].HeaderText = "Renewal #";
                    if (dgvRenewalHistory.Columns.Contains("NewStartDate"))
                        dgvRenewalHistory.Columns["NewStartDate"].HeaderText = "New Start";
                    if (dgvRenewalHistory.Columns.Contains("NewEndDate"))
                        dgvRenewalHistory.Columns["NewEndDate"].HeaderText = "New End";
                    if (dgvRenewalHistory.Columns.Contains("RenewalYears"))
                        dgvRenewalHistory.Columns["RenewalYears"].HeaderText = "Years";
                    if (dgvRenewalHistory.Columns.Contains("RenewalAmount"))
                        dgvRenewalHistory.Columns["RenewalAmount"].HeaderText = "Amount";
                    if (dgvRenewalHistory.Columns.Contains("RenewalNotes"))
                        dgvRenewalHistory.Columns["RenewalNotes"].HeaderText = "Notes";
                    if (dgvRenewalHistory.Columns.Contains("CreatedByUsername"))
                        dgvRenewalHistory.Columns["CreatedByUsername"].HeaderText = "Created By";
                    if (dgvRenewalHistory.Columns.Contains("CreatedAt"))
                        dgvRenewalHistory.Columns["CreatedAt"].HeaderText = "Created";
                    if (dgvRenewalHistory.Columns.Contains("IsArchived"))
                        dgvRenewalHistory.Columns["IsArchived"].HeaderText = "Archived";
                    if (dgvRenewalHistory.Columns.Contains("ArchivedDate"))
                        dgvRenewalHistory.Columns["ArchivedDate"].HeaderText = "Archived Date";
                    if (dgvRenewalHistory.Columns.Contains("ArchiveReason"))
                        dgvRenewalHistory.Columns["ArchiveReason"].HeaderText = "Archive Reason";

                    // Format date columns
                    if (dgvRenewalHistory.Columns.Contains("OnHoldDate"))
                        dgvRenewalHistory.Columns["OnHoldDate"].DefaultCellStyle.Format = "yyyy-MM-dd";
                    if (dgvRenewalHistory.Columns.Contains("RenewedDate"))
                        dgvRenewalHistory.Columns["RenewedDate"].DefaultCellStyle.Format = "yyyy-MM-dd";
                    if (dgvRenewalHistory.Columns.Contains("NewStartDate"))
                        dgvRenewalHistory.Columns["NewStartDate"].DefaultCellStyle.Format = "yyyy-MM-dd";
                    if (dgvRenewalHistory.Columns.Contains("NewEndDate"))
                        dgvRenewalHistory.Columns["NewEndDate"].DefaultCellStyle.Format = "yyyy-MM-dd";
                    if (dgvRenewalHistory.Columns.Contains("CreatedAt"))
                        dgvRenewalHistory.Columns["CreatedAt"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
                    if (dgvRenewalHistory.Columns.Contains("ArchivedDate"))
                        dgvRenewalHistory.Columns["ArchivedDate"].DefaultCellStyle.Format = "yyyy-MM-dd";

                    // Format amount columns
                    if (dgvRenewalHistory.Columns.Contains("RenewalAmount"))
                        dgvRenewalHistory.Columns["RenewalAmount"].DefaultCellStyle.Format = "N2";
                }

                dgvRenewalHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading renewal history: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnCreateRenewal_Click(object sender, EventArgs e)
        {
            try
            {
                if (_renewalDetail == null) return;

                // Create renewal dialog
                using (var renewDialog = new Form())
                {
                    renewDialog.Text = "Create New Renewal";
                    renewDialog.ClientSize = new Size(580, 700);
                    renewDialog.MinimumSize = new Size(590, 740);
                    renewDialog.StartPosition = FormStartPosition.CenterParent;
                    renewDialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    renewDialog.MaximizeBox = false;
                    renewDialog.MinimizeBox = false;
                    renewDialog.AutoScaleMode = AutoScaleMode.Font;

                    var lblInfo = new Label
                    {
                        Text = $"Item: {_renewalDetail.ItemName}\n" +
                               $"Type: {_renewalDetail.ItemType}\n\n" +
                               $"Current Start Date: {(_renewalDetail.StartDate.HasValue ? _renewalDetail.StartDate.Value.ToString("MM/dd/yyyy") : "Not Set")}\n" +
                               $"Current End Date: {(_renewalDetail.EndDate.HasValue ? _renewalDetail.EndDate.Value.ToString("MM/dd/yyyy") : "Not Set")}",
                        AutoSize = false,
                        Size = new Size(530, 100),
                        Location = new Point(15, 15),
                        Font = new Font("Segoe UI", 9F)
                    };

                    var lblStatus = new Label
                    {
                        Text = "Renewal Status:",
                        AutoSize = true,
                        Location = new Point(15, 130),
                        Margin = new Padding(0, 5, 0, 0)
                    };

                    var cboStatus = new ComboBox
                    {
                        Location = new Point(180, 127),
                        Width = 330,
                        DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    cboStatus.Items.AddRange(new object[] { "None", "On Hold", "Renewed" });
                    cboStatus.SelectedIndex = 2; // Default to "Renewed"

                    var lblYears = new Label
                    {
                        Text = "Renewal Period (Years):",
                        AutoSize = true,
                        Location = new Point(15, 168),
                        Margin = new Padding(0, 5, 0, 0)
                    };

                    var numYears = new NumericUpDown
                    {
                        Location = new Point(180, 165),
                        Width = 100,
                        Minimum = 1,
                        Maximum = 10,
                        Value = 1
                    };

                    var lblNewStart = new Label
                    {
                        Text = "New Start Date:",
                        AutoSize = true,
                        Location = new Point(15, 206),
                        Margin = new Padding(0, 5, 0, 0)
                    };

                    var dtpNewStart = new DateTimePicker
                    {
                        Location = new Point(180, 203),
                        Width = 200,
                        Format = DateTimePickerFormat.Short,
                        Value = DateTime.Today
                    };

                    var lblNewEnd = new Label
                    {
                        Text = "New End Date (Auto):",
                        AutoSize = true,
                        Location = new Point(15, 244),
                        Margin = new Padding(0, 5, 0, 0)
                    };

                    var lblCalcEnd = new Label
                    {
                        Text = DateTime.Today.AddYears(1).ToString("MM/dd/yyyy"),
                        AutoSize = true,
                        Location = new Point(180, 244),
                        Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                        ForeColor = Color.FromArgb(41, 128, 185)
                    };

                    EventHandler updateEndDate = (s, ev) =>
                    {
                        DateTime newEnd = dtpNewStart.Value.AddYears((int)numYears.Value);
                        lblCalcEnd.Text = newEnd.ToString("MM/dd/yyyy");
                    };
                    numYears.ValueChanged += updateEndDate;
                    dtpNewStart.ValueChanged += updateEndDate;

                    // Financial Calculator Group Box
                    var grpFinancialCalc = new GroupBox
                    {
                        Text = "Financial Calculator",
                        Location = new Point(15, 280),
                        Size = new Size(530, 310)
                    };

                    // Original Amount Reference
                    var lblOriginalAmountCaption = new Label
                    {
                        Text = "Original Total Amount:",
                        AutoSize = true,
                        Location = new Point(15, 28),
                        Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                    };

                    var lblOriginalAmount = new Label
                    {
                        Text = _renewalDetail.TotalAmountDue.ToString("N2"),
                        AutoSize = true,
                        Location = new Point(200, 28),
                        Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                        ForeColor = Color.FromArgb(41, 128, 185)
                    };

                    var lblSeparator = new Label
                    {
                        Text = "──────────────────────────────────────────",
                        AutoSize = true,
                        Location = new Point(15, 53),
                        ForeColor = Color.Gray
                    };

                    // Subtotal
                    var lblSubtotalCaption = new Label
                    {
                        Text = "Subtotal:",
                        AutoSize = true,
                        Location = new Point(15, 83),
                        Margin = new Padding(0, 5, 0, 0)
                    };

                    var txtSubtotal = new TextBox
                    {
                        Location = new Point(200, 80),
                        Width = 150,
                        Text = _renewalDetail.Subtotal > 0 ? _renewalDetail.Subtotal.ToString("F2") : _renewalDetail.TotalAmountDue.ToString("F2")
                    };

                    // VAT %
                    var lblVatPercentCaption = new Label
                    {
                        Text = "VAT %:",
                        AutoSize = true,
                        Location = new Point(15, 121),
                        Margin = new Padding(0, 5, 0, 0)
                    };

                    var numVatPercent = new NumericUpDown
                    {
                        Location = new Point(200, 118),
                        Width = 80,
                        Minimum = 0,
                        Maximum = 100,
                        DecimalPlaces = 2,
                        Value = _renewalDetail.Subtotal > 0m
                            ? Math.Min(100m, Math.Max(0m, decimal.Round((_renewalDetail.VatAmount / _renewalDetail.Subtotal) * 100m, 2)))
                            : 0m
                    };

                    var lblVatAmount = new Label
                    {
                        Text = "0.00",
                        AutoSize = true,
                        Location = new Point(290, 121),
                        ForeColor = Color.FromArgb(52, 152, 219)
                    };

                    // WHT %
                    var lblWhtPercentCaption = new Label
                    {
                        Text = "WHT %:",
                        AutoSize = true,
                        Location = new Point(15, 159),
                        Margin = new Padding(0, 5, 0, 0)
                    };

                    var numWhtPercent = new NumericUpDown
                    {
                        Location = new Point(200, 156),
                        Width = 80,
                        Minimum = 0,
                        Maximum = 100,
                        DecimalPlaces = 2,
                        Value = _renewalDetail.Subtotal > 0m
                            ? Math.Min(100m, Math.Max(0m, decimal.Round((_renewalDetail.WhtAmount / _renewalDetail.Subtotal) * 100m, 2)))
                            : 0m
                    };

                    var lblWhtAmount = new Label
                    {
                        Text = "0.00",
                        AutoSize = true,
                        Location = new Point(290, 159),
                        ForeColor = Color.FromArgb(231, 76, 60)
                    };

                    // Discount
                    var lblDiscountCaption = new Label
                    {
                        Text = "Discount:",
                        AutoSize = true,
                        Location = new Point(15, 197),
                        Margin = new Padding(0, 5, 0, 0)
                    };

                    var txtDiscount = new TextBox
                    {
                        Location = new Point(200, 194),
                        Width = 150,
                        Text = _renewalDetail.DiscountAmount > 0 ? _renewalDetail.DiscountAmount.ToString("F2") : "0.00"
                    };

                    // Total
                    var lblTotalCaption = new Label
                    {
                        Text = "Total Amount:",
                        AutoSize = true,
                        Location = new Point(15, 237),
                        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                        Margin = new Padding(0, 8, 0, 0)
                    };

                    var lblTotalAmount = new Label
                    {
                        Text = "0.00",
                        AutoSize = true,
                        Location = new Point(200, 237),
                        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                        ForeColor = Color.FromArgb(46, 204, 113)
                    };

                    // Comparison to Original
                    var lblComparison = new Label
                    {
                        Text = "",
                        AutoSize = true,
                        Location = new Point(15, 270),
                        Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                        ForeColor = Color.Gray,
                        MaximumSize = new Size(500, 0)
                    };

                    // Financial Calculator Logic
                    Action calculateTotal = () =>
                    {
                        decimal subtotal = 0;
                        decimal.TryParse(txtSubtotal.Text, out subtotal);

                        decimal vatPercent = numVatPercent.Value;
                        decimal whtPercent = numWhtPercent.Value;

                        decimal discount = 0;
                        decimal.TryParse(txtDiscount.Text, out discount);

                        decimal vatAmount = subtotal * (vatPercent / 100);
                        decimal whtAmount = subtotal * (whtPercent / 100);
                        decimal total = subtotal + vatAmount - whtAmount - discount;

                        lblVatAmount.Text = vatAmount.ToString("N2");
                        lblWhtAmount.Text = whtAmount.ToString("N2");
                        lblTotalAmount.Text = total.ToString("N2");

                        // Comparison to original
                        decimal original = _renewalDetail.TotalAmountDue;
                        decimal difference = total - original;
                        if (Math.Abs(difference) < 0.01m)
                        {
                            lblComparison.Text = "Same as original amount";
                            lblComparison.ForeColor = Color.Gray;
                        }
                        else if (difference > 0)
                        {
                            lblComparison.Text = $"Increase by {difference:N2} from original";
                            lblComparison.ForeColor = Color.FromArgb(231, 76, 60);
                        }
                        else
                        {
                            lblComparison.Text = $"Decrease by {Math.Abs(difference):N2} from original";
                            lblComparison.ForeColor = Color.FromArgb(46, 204, 113);
                        }
                    };

                    txtSubtotal.TextChanged += (s, ev) => calculateTotal();
                    numVatPercent.ValueChanged += (s, ev) => calculateTotal();
                    numWhtPercent.ValueChanged += (s, ev) => calculateTotal();
                    txtDiscount.TextChanged += (s, ev) => calculateTotal();

                    // Initial calculation
                    calculateTotal();

                    // Add all financial calculator controls to group box
                    grpFinancialCalc.Controls.AddRange(new Control[] {
                        lblOriginalAmountCaption, lblOriginalAmount, lblSeparator,
                        lblSubtotalCaption, txtSubtotal,
                        lblVatPercentCaption, numVatPercent, lblVatAmount,
                        lblWhtPercentCaption, numWhtPercent, lblWhtAmount,
                        lblDiscountCaption, txtDiscount,
                        lblTotalCaption, lblTotalAmount, lblComparison
                    });

                    var lblNotes = new Label
                    {
                        Text = "Notes:",
                        AutoSize = true,
                        Location = new Point(15, 605),
                        Margin = new Padding(0, 5, 0, 0)
                    };

                    var txtNotes = new TextBox
                    {
                        Location = new Point(80, 602),
                        Width = 480,
                        Height = 60,
                        Multiline = true,
                        ScrollBars = ScrollBars.Vertical
                    };

                    // ========================================
                    // CONTENT PANEL - Scrollable container for all content
                    // ========================================
                    var panelContent = new Panel
                    {
                        Dock = DockStyle.Fill,
                        AutoScroll = true,
                        Padding = new Padding(0, 0, 0, 10),
                        BackColor = SystemColors.Control
                    };

                    // Add all content controls to scrollable panel
                    panelContent.Controls.AddRange(new Control[] {
                        lblInfo, lblStatus, cboStatus,
                        lblYears, numYears,
                        lblNewStart, dtpNewStart,
                        lblNewEnd, lblCalcEnd,
                        grpFinancialCalc,
                        lblNotes, txtNotes
                    });

                    // ========================================
                    // BUTTON PANEL - Fixed bottom container for buttons
                    // ========================================
                    var panelButtons = new Panel
                    {
                        Dock = DockStyle.Bottom,
                        Height = 60,
                        Padding = new Padding(10),
                        BackColor = SystemColors.Control
                    };

                    var btnCreate = new Button
                    {
                        Text = "Create",
                        DialogResult = DialogResult.OK,
                        Size = new Size(120, 35),
                        BackColor = Color.FromArgb(46, 204, 113),
                        ForeColor = Color.White,
                        Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                        FlatStyle = FlatStyle.Flat,
                        Anchor = AnchorStyles.Top | AnchorStyles.Right
                    };

                    var btnCancel = new Button
                    {
                        Text = "Cancel",
                        DialogResult = DialogResult.Cancel,
                        Size = new Size(120, 35),
                        Anchor = AnchorStyles.Top | AnchorStyles.Right
                    };

                    // Position buttons from right edge (will be recalculated on resize)
                    EventHandler positionButtons = (s, ev) =>
                    {
                        btnCancel.Location = new Point(panelButtons.ClientSize.Width - btnCancel.Width - 20, 12);
                        btnCreate.Location = new Point(btnCancel.Left - btnCreate.Width - 10, 12);
                    };

                    panelButtons.Resize += positionButtons;

                    // Add buttons to button panel
                    panelButtons.Controls.AddRange(new Control[] { btnCreate, btnCancel });

                    // Initial button positioning
                    positionButtons(null, null);

                    // Add panels to form (CRITICAL ORDER: Bottom panel first, then Fill panel)
                    renewDialog.Controls.Add(panelButtons);
                    renewDialog.Controls.Add(panelContent);

                    renewDialog.AcceptButton = btnCreate;
                    renewDialog.CancelButton = btnCancel;

                    if (renewDialog.ShowDialog() == DialogResult.OK)
                    {
                        // Do not strip time — database requires full timestamp
                        DateTime newStartDate = dtpNewStart.Value;
                        DateTime newEndDate = newStartDate.AddYears((int)numYears.Value);
                        decimal renewalAmount = 0;

                        if (!decimal.TryParse(lblTotalAmount.Text.Replace(",", ""), out renewalAmount))
                        {
                            MessageBox.Show("Please enter valid financial information.", "Validation Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        // Confirm creation
                        var confirmResult = MessageBox.Show(
                            $"Create Renewal:\n\n" +
                            $"Status: {cboStatus.SelectedItem}\n" +
                            $"New Start Date: {newStartDate:MM/dd/yyyy}\n" +
                            $"New End Date: {newEndDate:MM/dd/yyyy}\n" +
                            $"Period: {numYears.Value} year(s)\n" +
                            $"Amount: {renewalAmount:N2}\n\n" +
                            $"Continue?",
                            "Confirm Renewal Creation",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (confirmResult == DialogResult.Yes)
                        {
                            // Validate user ID before proceeding
                            int createdBy = SessionContext.CurrentUserId;
                            if (createdBy <= 0)
                            {
                                // Get the first valid user ID from the database as fallback
                                createdBy = GetFirstValidUserId();
                                if (createdBy <= 0)
                                {
                                    MessageBox.Show("Unable to determine current user. Please ensure you are logged in.", "Error",
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return;
                                }
                            }

                            int finalCreatedBy = createdBy;
                            await Task.Run(() =>
                            {
                                _repository.CreateRenewal(
                                    _itemId,
                                    cboStatus.SelectedItem.ToString(),
                                    newStartDate,
                                    newEndDate,
                                    (int)numYears.Value,
                                    renewalAmount,
                                    txtNotes.Text.Trim(),
                                    finalCreatedBy,
                                    _setId
                                );
                            });

                            System.Diagnostics.Debug.WriteLine($"[Renewal] Created renewal for ItemId {_itemId}, Amount {renewalAmount}, Status {cboStatus.SelectedItem}, SetId {_setId}");

                            MessageBox.Show(
                                $"Renewal created successfully!\n\n" +
                                $"Status: {cboStatus.SelectedItem}\n" +
                                $"Period: {newStartDate:MM/dd/yyyy} - {newEndDate:MM/dd/yyyy}",
                                "Success",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            // Reload data
                            await LoadRenewalDetailsAsync();

                            // Trigger callback if set (e.g., navigate to ViewRenewalsPage after first renewal)
                            OnRenewalCreated?.Invoke();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create renewal:\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Saves vendor information changes to the database
        /// </summary>
        private async void btnSaveVendor_Click(object sender, EventArgs e)
        {
            try
            {
                if (_renewalDetail == null)
                {
                    MessageBox.Show("No item loaded.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (cboVendorName == null)
                {
                    MessageBox.Show("Vendor dropdown is not available.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                int selectedVendorId = 0;
                if (cboVendorName.SelectedValue != null)
                    int.TryParse(cboVendorName.SelectedValue.ToString(), out selectedVendorId);

                if (selectedVendorId <= 0)
                {
                    MessageBox.Show("Please select a Vendor.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cboVendorName.Focus();
                    return;
                }

                var selectedVendor = cboVendorName.SelectedItem as VendorDto;

                var vendorRepository = new VendorRepository();

                // Persist the selected vendor association for this item
                await vendorRepository.UpdateItemVendorAsync(_itemId, selectedVendorId);

                // Create vendor object with updated values
                var vendorToUpdate = new VendorDto
                {
                    VendorId = selectedVendorId,
                    VendorName = selectedVendor?.VendorName ?? string.Empty,
                    Address = string.IsNullOrWhiteSpace(txtVendorAddress.Text) ? null : txtVendorAddress.Text.Trim(),
                    TIN = string.IsNullOrWhiteSpace(txtVendorTIN.Text) ? null : txtVendorTIN.Text.Trim(),
                    IsActive = selectedVendor?.IsActive ?? true,
                    CreatedDate = selectedVendor?.CreatedDate ?? DateTime.Now
                };

                // Optionally persist edited vendor address/TIN
                await vendorRepository.UpdateVendorAsync(vendorToUpdate);

                _renewalDetail.VendorId = selectedVendorId;
                _renewalDetail.VendorName = vendorToUpdate.VendorName;
                _renewalDetail.VendorAddress = vendorToUpdate.Address;
                _renewalDetail.VendorTIN = vendorToUpdate.TIN;

                MessageBox.Show("Vendor information updated successfully.", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Reload data to reflect changes
                await LoadRenewalDetailsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save vendor information:\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task EnsureVendorDropdownAsync()
        {
            if (groupBoxVendorInfo == null)
                return;

            if (cboVendorName == null)
            {
                cboVendorName = new ComboBox
                {
                    Name = "cboVendorName",
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = txtVendorName.Location,
                    Size = txtVendorName.Size,
                    Anchor = txtVendorName.Anchor,
                    TabIndex = txtVendorName.TabIndex
                };

                cboVendorName.SelectedIndexChanged += (s, e) =>
                {
                    if (_isVendorComboInitializing)
                        return;

                    var selected = cboVendorName.SelectedItem as VendorDto;
                    if (selected == null || selected.VendorId <= 0)
                    {
                        txtVendorAddress.Text = string.Empty;
                        txtVendorTIN.Text = string.Empty;
                        return;
                    }

                    txtVendorAddress.Text = selected.Address ?? string.Empty;
                    txtVendorTIN.Text = selected.TIN ?? string.Empty;
                };

                groupBoxVendorInfo.Controls.Add(cboVendorName);
                groupBoxVendorInfo.Controls.SetChildIndex(cboVendorName, groupBoxVendorInfo.Controls.GetChildIndex(txtVendorName));

                txtVendorName.Visible = false;
                txtVendorName.Enabled = false;
            }

            if (_vendors == null)
            {
                var vendorRepository = new VendorRepository();
                var all = await vendorRepository.GetAllVendorsAsync();

                _vendors = new List<VendorDto>
                {
                    new VendorDto { VendorId = 0, VendorName = "-- Select Vendor --", IsActive = true, CreatedDate = DateTime.Now }
                };

                _vendors.AddRange(all.Where(v => v != null && v.IsActive));
            }

            _isVendorComboInitializing = true;
            try
            {
                cboVendorName.DataSource = null;
                cboVendorName.DataSource = _vendors;
                cboVendorName.DisplayMember = nameof(VendorDto.VendorName);
                cboVendorName.ValueMember = nameof(VendorDto.VendorId);
            }
            finally
            {
                _isVendorComboInitializing = false;
            }
        }

        private void ApplyVendorSelectionFromRenewalDetail()
        {
            if (cboVendorName == null || _renewalDetail == null)
                return;

            _isVendorComboInitializing = true;
            try
            {
                int currentVendorId = _renewalDetail.VendorId ?? 0;

                if (currentVendorId > 0)
                {
                    cboVendorName.SelectedValue = currentVendorId;
                }
                else if (!string.IsNullOrWhiteSpace(_renewalDetail.VendorName) && _vendors != null)
                {
                    var match = _vendors.FirstOrDefault(v =>
                        v != null &&
                        v.VendorId > 0 &&
                        string.Equals(v.VendorName?.Trim(), _renewalDetail.VendorName.Trim(), StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                        cboVendorName.SelectedValue = match.VendorId;
                    else
                        cboVendorName.SelectedValue = 0;
                }
                else
                {
                    cboVendorName.SelectedValue = 0;
                }
            }
            finally
            {
                _isVendorComboInitializing = false;
            }
        }

        private async void btnArchive_Click(object sender, EventArgs e)
        {
            try
            {
                if (_renewalDetail == null || !_renewalDetail.Active)
                {
                    MessageBox.Show("This renewal is already archived.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Get the latest renewal record for this item
                // NOTE: Fix - we check if renewals exist, but we should archive based on the Item itself
                var history = await Task.Run(() => _repository.GetRenewalHistoryByItemId(_itemId));
                var latestRenewal = history.OrderByDescending(h => h.CreatedAt).FirstOrDefault();

                // Fixed logic: If no renewal history exists, create an initial one before archiving
                if (latestRenewal == null)
                {
                    // Create an initial "On Hold" renewal record for this item
                    int renewalId = await Task.Run(() => _repository.CreateRenewal(
                        itemId: _itemId,
                        renewalStatus: "On Hold",
                        newStartDate: _renewalDetail.StartDate ?? DateTime.Now,
                        newEndDate: _renewalDetail.EndDate ?? DateTime.Now.AddYears(1),
                        renewalYears: 0,
                        renewalAmount: _renewalDetail.TotalAmountDue,
                        renewalNotes: "Initial renewal record created for archiving",
                        createdBy: SessionContext.CurrentUserId > 0 ? SessionContext.CurrentUserId : 1,
                        setIdOverride: _setId
                    ));

                    // Re-fetch to get the newly created renewal
                    history = await Task.Run(() => _repository.GetRenewalHistoryByItemId(_itemId));
                    latestRenewal = history.OrderByDescending(h => h.CreatedAt).FirstOrDefault();
                }

                // Confirm archive
                var result = MessageBox.Show(
                    $"Archive this renewal and item?\n\n" +
                    $"Item: {_renewalDetail.ItemName}\n" +
                    $"Type: {_renewalDetail.ItemType}\n\n" +
                    $"This will:\n" +
                    $"- Archive the renewal record\n" +
                    $"- Archive the item\n" +
                    $"- Mark the item as inactive\n\n" +
                    $"This action creates archive entries but can be restored later.\n\n" +
                    $"Continue?",
                    "Confirm Archive",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    // Create input dialog for archive reason
                    string archiveReason = ShowInputDialog("Please enter the reason for archiving:", "Archive Reason", "Manual archive from Renewal Detail page");

                    if (string.IsNullOrWhiteSpace(archiveReason))
                    {
                        MessageBox.Show("Archive reason is required.", "Validation Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    await Task.Run(() =>
                    {
                        _repository.ArchiveRenewal(
                            latestRenewal.RenewalId,
                            _itemId,
                            archiveReason,
                            SessionContext.CurrentUserName ?? "Unknown"
                        );
                    });

                    MessageBox.Show("Renewal and item archived successfully.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Reload data
                    await LoadRenewalDetailsAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to archive renewal:\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Shows a simple input dialog for text entry
        /// </summary>
        private string ShowInputDialog(string text, string caption, string defaultValue = "")
        {
            Form prompt = new Form()
            {
                Width = 500,
                Height = 180,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label textLabel = new Label()
            {
                Left = 20,
                Top = 20,
                Width = 440,
                Text = text,
                Font = new Font("Segoe UI", 9F)
            };

            TextBox textBox = new TextBox()
            {
                Left = 20,
                Top = 50,
                Width = 440,
                Text = defaultValue,
                Font = new Font("Segoe UI", 9F)
            };

            Button confirmation = new Button()
            {
                Text = "OK",
                Left = 280,
                Width = 80,
                Top = 90,
                DialogResult = DialogResult.OK,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            Button cancel = new Button()
            {
                Text = "Cancel",
                Left = 370,
                Width = 80,
                Top = 90,
                DialogResult = DialogResult.Cancel,
                Font = new Font("Segoe UI", 9F)
            };

            confirmation.Click += (sender, e) => { prompt.Close(); };
            cancel.Click += (sender, e) => { prompt.Close(); };

            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(cancel);
            prompt.AcceptButton = confirmation;
            prompt.CancelButton = cancel;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : null;
        }
    }
}
