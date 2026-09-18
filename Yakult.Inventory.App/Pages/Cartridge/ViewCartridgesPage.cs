using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    public partial class ViewCartridgesPage : UserControl
    {
        private DefaultListPageLayout _layout;
        private DataGridView dgvItems;
        private CheckBox chkShowInactive;
        private TextBox txtSerialFilter;
        private FlowLayoutPanel _inactiveLegendPanel;

        // Pagination
        private Button btnFirstPage, btnPrevPage, btnNextPage, btnLastPage;
        private Label lblPageInfo;
        private int _currentPage = 1;
        private int _pageSize = 10;

        // Data
        private string _connectionString;
        private List<ItemDto> _allItems;
        private List<ItemDto> _filteredItems;

        // Sorting state
        private DataGridViewColumn _sortColumn;
        private System.Windows.Forms.SortOrder _sortOrder = System.Windows.Forms.SortOrder.None;
        private bool _sortByDropdownInitialized = false;

        public ViewCartridgesPage()
        {
            _connectionString = DatabaseConfig.ConnectionString;
            InitializeComponent();
            BuildUiWithTemplate();
            LoadItems();
        }

        private void InitializeComponent()
        {
        }

        private void BuildUiWithTemplate()
        {
            SuspendLayout();
            Controls.Clear();

            Dock = DockStyle.Fill;
            BackColor = Color.White;

            _layout = DefaultListPageTemplate.Create(
                "View Cartridges",
                "Search by Name, Model, Serial, Vendor, Condition, Status...",
                (s, e) => ApplyFilters(),
                () => { LoadItems(); });

            // Hook up Filter By event
            if (_layout.FilterByComboBox != null)
            {
                _layout.FilterByComboBox.SelectedIndexChanged += (s, e) =>
                {
                    _sortColumn = null;
                    _sortOrder = System.Windows.Forms.SortOrder.None;
                    ApplyFilters();
                };
            }

            // No action buttons for this read-only view

            if (_layout.SummaryPanel != null)
                _layout.SummaryPanel.Visible = false;
            if (_layout.PaginationPanel != null)
                _layout.PaginationPanel.Visible = true;

            // --- Filter controls ---
            var lblSerialFilter = new Label
            {
                Text = "Serial #:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = UiTheme.Colors.TextMuted,
                Margin = new Padding(0, 10, 6, 0)
            };

            txtSerialFilter = new TextBox
            {
                Width = 180,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 6, 16, 0)
            };
            txtSerialFilter.TextChanged += (s, e) => ApplyFilters();

            chkShowInactive = new CheckBox
            {
                Text = "Show Inactive",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = UiTheme.Colors.TextMuted,
                Margin = new Padding(0, 10, 12, 0)
            };
            chkShowInactive.CheckedChanged += (s, e) => LoadItems();

            var filterHost = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.Transparent,
                Padding = new Padding(6, 4, 6, 4)
            };

            var filterFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            //filterFlow.Controls.Add(lblSerialFilter);
            //filterFlow.Controls.Add(txtSerialFilter);
            filterFlow.Controls.Add(chkShowInactive);
            filterHost.Controls.Add(filterFlow);

            // --- DataGridView ---
            dgvItems = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false
            };
            UiFactory.StyleGrid(dgvItems);
            dgvItems.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvItems.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            DefaultListPageTemplate.AttachVendorsPillCellPainting(dgvItems, c =>
            {
                if (c == null) return false;
                var p = c.DataPropertyName;
                return string.Equals(p, "ItemId", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p, "StockOnHand", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p, "RepairCount", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p, "WarrantyYears", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p, "Active", StringComparison.OrdinalIgnoreCase);
            });

            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ItemId", HeaderText = "ID", FillWeight = 8, MinimumWidth = 60, AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Name", FillWeight = 20, MinimumWidth = 150 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Description", HeaderText = "Description", FillWeight = 25, MinimumWidth = 180 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ItemType", HeaderText = "Type", Width = 100 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ModelNumber", HeaderText = "Model Number", Width = 130 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SerialNumber", HeaderText = "Serial Number", Width = 130 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StockOnHand", HeaderText = "Stock On Hand", Width = 100 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LatestStatus", HeaderText = "Status", Width = 100 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LatestRemark", HeaderText = "Last Remark", Width = 200 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RepairCount", HeaderText = "Repairs", Width = 100 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LastRepairAction", HeaderText = "Last Repair", Width = 200 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ConditionName", HeaderText = "Condition", Width = 100 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "VendorName", HeaderText = "Vendor", Width = 150 });
            dgvItems.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Active", HeaderText = "Active", Width = 70 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFixedAsset", DataPropertyName = "IsTrackedAsset", HeaderText = "Fixed Asset", Width = 90, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AcquisitionType", HeaderText = "Acquisition Type", Width = 120 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UnitOfMeasure", HeaderText = "Unit", Width = 80 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DateCreated", HeaderText = "Date Created", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy" } });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreatedByName", HeaderText = "Created By", Width = 120 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DateModified", HeaderText = "Date Modified", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy" } });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ModifiedByName", HeaderText = "Modified By", Width = 120 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "WarrantyYears", HeaderText = "Warranty (Years)", Width = 120 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "WarrantyStartDate", HeaderText = "Warranty Start", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy" } });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "WarrantyEndDate", HeaderText = "Warranty End", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy" } });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DatePurchased", HeaderText = "Date Purchased", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy" } });

            dgvItems.CellFormatting += DgvItems_CellFormatting;
            dgvItems.ColumnHeaderMouseClick += DgvItems_ColumnHeaderMouseClick;

            var itemsHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            itemsHost.Controls.Add(dgvItems);
            itemsHost.Controls.Add(filterHost);

            _layout.GridCard.Controls.Clear();
            _layout.GridCard.Controls.Add(itemsHost);

            // --- Pagination ---
            btnFirstPage = new Button { Text = "<<", Width = 45, Height = 25, Left = 0, Top = 5 };
            btnPrevPage = new Button { Text = "<", Width = 45, Height = 25, Left = 50, Top = 5 };
            lblPageInfo = new Label { AutoSize = false, Width = 260, Left = 100, Top = 10, Font = new Font("Segoe UI", 9F, FontStyle.Regular), TextAlign = ContentAlignment.MiddleLeft };
            btnNextPage = new Button { Text = ">", Width = 45, Height = 25, Left = 370, Top = 5 };
            btnLastPage = new Button { Text = ">>", Width = 45, Height = 25, Left = 420, Top = 5 };

            btnFirstPage.Click += (s, e) => { _currentPage = 1; UpdatePagination(); };
            btnPrevPage.Click += (s, e) => { if (_currentPage > 1) { _currentPage--; UpdatePagination(); } };
            btnNextPage.Click += (s, e) =>
            {
                if (_filteredItems == null) return;
                int totalPages = (int)Math.Ceiling((double)_filteredItems.Count / _pageSize);
                if (_currentPage < totalPages) { _currentPage++; UpdatePagination(); }
            };
            btnLastPage.Click += (s, e) =>
            {
                if (_filteredItems == null) return;
                int totalPages = (int)Math.Ceiling((double)_filteredItems.Count / _pageSize);
                _currentPage = totalPages; UpdatePagination();
            };

            // Inactive legend
            _inactiveLegendPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0),
                Margin = new Padding(0),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            var purpleSwatch = new Panel { Width = 12, Height = 12, BackColor = Color.FromArgb(230, 220, 255), Margin = new Padding(0, 6, 6, 0) };
            var purpleLabel = new Label { AutoSize = true, Text = "Inactive \u2013 Unavailable", Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(90, 90, 90), Margin = new Padding(0, 5, 14, 0) };
            var graySwatch = new Panel { Width = 12, Height = 12, BackColor = Color.FromArgb(235, 235, 235), Margin = new Padding(0, 6, 6, 0) };
            var grayLabel = new Label { AutoSize = true, Text = "Inactive \u2013 No Activity", Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(90, 90, 90), Margin = new Padding(0, 5, 0, 0) };

            _inactiveLegendPanel.Controls.Add(purpleSwatch);
            _inactiveLegendPanel.Controls.Add(purpleLabel);
            _inactiveLegendPanel.Controls.Add(graySwatch);
            _inactiveLegendPanel.Controls.Add(grayLabel);

            if (_layout.PaginationPanel != null)
            {
                _layout.PaginationPanel.Controls.Clear();
                _layout.PaginationPanel.Controls.AddRange(new Control[] { btnFirstPage, btnPrevPage, lblPageInfo, btnNextPage, btnLastPage });

                _inactiveLegendPanel.Location = new Point(_layout.PaginationPanel.Width - _inactiveLegendPanel.Width - 12, 5);
                _layout.PaginationPanel.Controls.Add(_inactiveLegendPanel);
                _layout.PaginationPanel.Resize += (s, e) =>
                {
                    if (_inactiveLegendPanel != null)
                        _inactiveLegendPanel.Location = new Point(_layout.PaginationPanel.Width - _inactiveLegendPanel.Width - 12, 5);
                };
            }

            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.SummaryPanel);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_layout.HeaderPanel);

            ResumeLayout(true);

            DefaultListPageTemplate.SetupInitialPageFocus(_layout.SearchBox, dgvItems);
        }

        private void DgvItems_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var columnName = dgvItems.Columns[e.ColumnIndex].Name ?? dgvItems.Columns[e.ColumnIndex].HeaderText;

            // Highlight rows where a cartridge item was assigned the default 'Unassigned' CartridgeModel during import
            if (dgvItems.Rows[e.RowIndex].DataBoundItem is ItemDto rowItem && rowItem.UsedDefaultCartridgeModel)
            {
                e.CellStyle.BackColor = Color.Orange;
                e.CellStyle.SelectionBackColor = Color.DarkOrange;
            }

            // Format the Fixed Asset column
            if (columnName == "colFixedAsset" && e.RowIndex >= 0)
            {
                try
                {
                    var row = dgvItems.Rows[e.RowIndex];
                    if (row.DataBoundItem is ItemDto item)
                    {
                        e.Value = item.IsTrackedAsset ? "Yes" : "No";
                        e.FormattingApplied = true;
                    }
                }
                catch { }
            }

            // Format the Active column
            if (dgvItems.Columns[e.ColumnIndex].HeaderText == "Active" && e.RowIndex >= 0)
            {
                try
                {
                    var row = dgvItems.Rows[e.RowIndex];
                    if (row.DataBoundItem is ItemDto item)
                    {
                        if (item.Active)
                        {
                            if (string.Equals(item.ItemType, "Hardware", StringComparison.OrdinalIgnoreCase))
                            {
                                row.Cells[e.ColumnIndex].Style.ForeColor = Color.Green;
                            }
                            else if (string.Equals(item.ItemType, "Software/License", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(item.ItemType, "Service", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(item.ItemType, "Services", StringComparison.OrdinalIgnoreCase))
                            {
                                row.Cells[e.ColumnIndex].Style.ForeColor = SystemColors.ControlText;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private void LoadItems()
        {
            try
            {
                var items = new List<ItemDto>();
                string serialFilter = txtSerialFilter?.Text;

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    var whereClauses = new List<string>();
                    whereClauses.Add("1 = 1");

                    // CRITICAL: Only cartridge items
                    whereClauses.Add("i.Category = 'Cartridge'");

                    // Exclude archived items
                    whereClauses.Add("arch.EntityId IS NULL");

                    // Filter by Active status unless Show Inactive is checked
                    if (chkShowInactive == null || !chkShowInactive.Checked)
                    {
                        whereClauses.Add("i.Active = 1");
                    }

                    if (!string.IsNullOrWhiteSpace(serialFilter))
                    {
                        whereClauses.Add("i.SerialNumber LIKE @Serial");
                    }

                    string sqlQuery = $@"
-- Cartridge items (physical stock only; no request-based reservation)
SELECT
        i.ItemId,
        i.Name,
        i.Description,
        i.Category,
        i.ItemType,
        i.SerialNumber,
        i.ModelNumber,
        i.Active AS DbActive,
        i.UnitOfMeasure,
        ISNULL(i.StockOnHand, 0) AS StockOnHand,
        i.DateCreated,
        u1.Name AS CreatedByName,
        i.DateModified,
        u2.Name AS ModifiedByName,
        i.Active AS EffectiveActive,
        i.Amount,
        c.ConditionName,
        i.ConditionId,
        i.Remarks,
        i.WarrantyYears,
        i.WarrantyStartDate,
        i.WarrantyEndDate,
        i.DatePurchased,
        CASE
            WHEN i.ConditionId = 1 THEN 'Good'
            WHEN i.ConditionId = 2 THEN 'Damaged'
            ELSE NULL
        END AS LatestStatus,
        su.Remark AS LatestRemark,
        ISNULL(rh.RepairCount, 0) AS RepairCount,
        lr.RepairAction AS LastRepairAction,
        i.VendorId,
        v.VendorName,
        i.IsTrackedAsset,
        i.AcquisitionType,
        CASE WHEN cm.ModelNumber = 'Unassigned' THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS UsedDefaultCartridgeModel
FROM dbo.Item i
LEFT JOIN dbo.[User] u1 ON i.CreatedBy = u1.UserId
LEFT JOIN dbo.[User] u2 ON i.ModifiedBy = u2.UserId
LEFT JOIN dbo.Condition c ON i.ConditionId = c.ConditionId
LEFT JOIN dbo.Vendor v ON i.VendorId = v.VendorID
LEFT JOIN dbo.CartridgeModel cm ON cm.CartridgeModelId = i.CartridgeModelId
LEFT JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1
LEFT JOIN (
    SELECT SerialNumber, MAX(CreatedAt) AS LatestCreatedAt
    FROM dbo.SetItemUpdate WHERE Processed = 1 GROUP BY SerialNumber
) lu ON lu.SerialNumber = i.SerialNumber
LEFT JOIN dbo.SetItemUpdate su
    ON su.SerialNumber = i.SerialNumber AND su.CreatedAt = lu.LatestCreatedAt AND su.Processed = 1
LEFT JOIN (
    SELECT SerialNumber, COUNT(*) AS RepairCount, MAX(CreatedAt) AS LastRepairAt
    FROM dbo.ItemRepairHistory GROUP BY SerialNumber
) rh ON rh.SerialNumber = i.SerialNumber
LEFT JOIN dbo.ItemRepairHistory lr
    ON lr.SerialNumber = i.SerialNumber AND lr.CreatedAt = rh.LastRepairAt
WHERE {string.Join(" AND ", whereClauses)}
ORDER BY DateCreated DESC, ItemId DESC";

                    using (var cmd = new SqlCommand(sqlQuery, con))
                    {
                        if (!string.IsNullOrWhiteSpace(serialFilter))
                        {
                            cmd.Parameters.AddWithValue("@Serial", $"%{serialFilter}%");
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                items.Add(new ItemDto
                                {
                                    ItemId = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    Category = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    ItemType = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    SerialNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    ModelNumber = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    DbActive = reader.GetBoolean(7),
                                    Active = reader.GetBoolean(14),
                                    UnitOfMeasure = reader.IsDBNull(8) ? null : reader.GetString(8),
                                    StockOnHand = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                                    DateCreated = reader.IsDBNull(10) ? DateTime.MinValue : reader.GetDateTime(10),
                                    CreatedByName = reader.IsDBNull(11) ? "N/A" : reader.GetString(11),
                                    DateModified = reader.IsDBNull(12) ? (DateTime?)null : reader.GetDateTime(12),
                                    ModifiedByName = reader.IsDBNull(13) ? null : reader.GetString(13),
                                    Amount = reader.IsDBNull(15) ? 0m : reader.GetDecimal(15),
                                    ConditionName = reader.IsDBNull(16) ? null : reader.GetString(16),
                                    ConditionId = reader.IsDBNull(17) ? 1 : reader.GetInt32(17),
                                    Remarks = reader.IsDBNull(18) ? null : reader.GetString(18),
                                    WarrantyYears = reader.GetInt32(19),
                                    WarrantyStartDate = reader.IsDBNull(20) ? (DateTime?)null : reader.GetDateTime(20),
                                    WarrantyEndDate = reader.IsDBNull(21) ? (DateTime?)null : reader.GetDateTime(21),
                                    DatePurchased = reader.IsDBNull(22) ? (DateTime?)null : reader.GetDateTime(22),
                                    LatestStatus = reader.IsDBNull(23) ? null : reader.GetString(23),
                                    LatestRemark = reader.IsDBNull(24) ? null : reader.GetString(24),
                                    RepairCount = reader.IsDBNull(25) ? 0 : reader.GetInt32(25),
                                    LastRepairAction = reader.IsDBNull(26) ? null : reader.GetString(26),
                                    VendorId = reader.IsDBNull(27) ? (int?)null : reader.GetInt32(27),
                                    VendorName = reader.IsDBNull(28) ? null : reader.GetString(28),
                                    IsTrackedAsset = reader.IsDBNull(29) ? false : reader.GetBoolean(29),
                                    AcquisitionType = reader.IsDBNull(30) ? null : reader.GetString(30),
                                    UsedDefaultCartridgeModel = !reader.IsDBNull(31) && reader.GetBoolean(31)
                                });
                            }
                        }
                    }
                }

                _allItems = items;
                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load cartridge items: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyFilters()
        {
            if (_allItems == null)
                return;

            var filtered = _allItems.AsEnumerable();

            // Search filter
            string searchText = _layout?.SearchBox?.Text?.Trim().ToLower();
            if (!string.IsNullOrWhiteSpace(searchText) &&
                searchText != "search by name, model, serial, vendor, condition, status...")
            {
                filtered = filtered.Where(item =>
                    (item.Name != null && item.Name.ToLower().Contains(searchText)) ||
                    (item.Description != null && item.Description.ToLower().Contains(searchText)) ||
                    (item.ModelNumber != null && item.ModelNumber.ToLower().Contains(searchText)) ||
                    (item.SerialNumber != null && item.SerialNumber.ToLower().Contains(searchText)) ||
                    (item.UnitOfMeasure != null && item.UnitOfMeasure.ToLower().Contains(searchText)) ||
                    (item.ConditionName != null && item.ConditionName.ToLower().Contains(searchText)) ||
                    (item.ItemType != null && item.ItemType.ToLower().Contains(searchText)) ||
                    (item.LatestStatus != null && item.LatestStatus.ToLower().Contains(searchText)) ||
                    (item.LatestRemark != null && item.LatestRemark.ToLower().Contains(searchText)) ||
                    (item.LastRepairAction != null && item.LastRepairAction.ToLower().Contains(searchText)) ||
                    (item.VendorName != null && item.VendorName.ToLower().Contains(searchText))
                );
            }

            // Serial filter
            string serialFilter = txtSerialFilter?.Text?.Trim().ToLower();
            if (!string.IsNullOrWhiteSpace(serialFilter))
            {
                filtered = filtered.Where(item =>
                    item.SerialNumber != null && item.SerialNumber.ToLower().Contains(serialFilter)
                );
            }

            // Active status filter
            if (chkShowInactive != null && !chkShowInactive.Checked)
            {
                filtered = filtered.Where(item => item.Active);
            }

            _filteredItems = filtered.ToList();

            // --- Sorting ---
            string filterBy = _layout?.FilterByComboBox?.SelectedItem?.ToString();

            if (_sortColumn != null)
            {
                var prop = _sortColumn.DataPropertyName;
                var propInfo = typeof(ItemDto).GetProperty(prop);

                if (propInfo != null)
                {
                    if (_sortOrder == System.Windows.Forms.SortOrder.Ascending)
                        _filteredItems = _filteredItems.OrderBy(x => propInfo.GetValue(x, null)).ToList();
                    else
                        _filteredItems = _filteredItems.OrderByDescending(x => propInfo.GetValue(x, null)).ToList();
                }
            }
            else
            {
                if (filterBy == "Most Recently Added")
                {
                    _filteredItems = _filteredItems.OrderByDescending(x => x.DateCreated).ToList();
                }
                else if (filterBy == "Oldest Added")
                {
                    _filteredItems = _filteredItems.OrderBy(x => x.DateCreated).ToList();
                }
                else
                {
                    _filteredItems = _filteredItems.OrderByDescending(x => x.DateCreated).ThenByDescending(x => x.ItemId).ToList();
                }
            }

            _currentPage = 1;
            UpdatePagination();

            // Initialize Sort By dropdown after first load
            if (!_sortByDropdownInitialized && _layout?.SortByComboBox != null && dgvItems != null && dgvItems.Columns.Count > 0)
            {
                DefaultListPageTemplate.SetupSortByDropdown(
                    _layout.SortByComboBox,
                    dgvItems,
                    (columnKey, direction) =>
                    {
                        var col = dgvItems.Columns.Cast<DataGridViewColumn>()
                            .FirstOrDefault(c => c.DataPropertyName == columnKey || c.Name == columnKey);

                        if (col != null)
                        {
                            _sortColumn = col;
                            _sortOrder = direction;

                            if (_layout.FilterByComboBox != null && _layout.FilterByComboBox.SelectedIndex != 0)
                            {
                                _layout.FilterByComboBox.SelectedIndex = 0;
                            }

                            ApplyFilters();
                        }
                    },
                    defaultColumnKey: "ItemId",
                    defaultDirection: System.Windows.Forms.SortOrder.Ascending
                );

                _sortByDropdownInitialized = true;
            }
        }

        private void DgvItems_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = dgvItems.Columns[e.ColumnIndex];
            if (col is DataGridViewCheckBoxColumn || col is DataGridViewButtonColumn || col is DataGridViewImageColumn)
                return;

            if (_sortColumn == col)
            {
                _sortOrder = _sortOrder == System.Windows.Forms.SortOrder.Ascending ? System.Windows.Forms.SortOrder.Descending : System.Windows.Forms.SortOrder.Ascending;
            }
            else
            {
                _sortColumn = col;
                _sortOrder = System.Windows.Forms.SortOrder.Ascending;
            }

            if (_layout?.SortByComboBox != null && _sortColumn != null)
            {
                DefaultListPageTemplate.SyncSortByDropdown(_layout.SortByComboBox, _sortColumn, _sortOrder);
            }

            ApplyFilters();
        }

        private void UpdatePagination()
        {
            if (_filteredItems == null || _filteredItems.Count == 0)
            {
                DefaultListPageTemplate.PreserveColumnWidthsOnUpdate(dgvItems, () =>
                {
                    dgvItems.DataSource = new List<ItemDto>();
                    DefaultListPageTemplate.EnableSortingGlyphs(dgvItems);

                    var createdDateCol = DefaultListPageTemplate.ResolveCreatedDateColumn(dgvItems);
                    if (createdDateCol != null)
                    {
                        createdDateCol.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Descending;
                    }
                });
                if (lblPageInfo != null)
                    lblPageInfo.Text = "Page 0 of 0 (0 items)";
                if (btnFirstPage != null) btnFirstPage.Enabled = false;
                if (btnPrevPage != null) btnPrevPage.Enabled = false;
                if (btnNextPage != null) btnNextPage.Enabled = false;
                if (btnLastPage != null) btnLastPage.Enabled = false;
                return;
            }

            int totalPages = (int)Math.Ceiling((double)_filteredItems.Count / _pageSize);
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1) _currentPage = 1;

            var pagedData = _filteredItems
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            DefaultListPageTemplate.PreserveColumnWidthsOnUpdate(dgvItems, () =>
            {
                dgvItems.DataSource = pagedData;

                DefaultListPageTemplate.EnableSortingGlyphs(dgvItems);

                foreach (DataGridViewColumn col in dgvItems.Columns)
                {
                    col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                }

                if (_sortColumn != null)
                {
                    _sortColumn.HeaderCell.SortGlyphDirection = _sortOrder;
                }
                else
                {
                    string filterBy = _layout?.FilterByComboBox?.SelectedItem?.ToString();
                    var createdDateCol = DefaultListPageTemplate.ResolveCreatedDateColumn(dgvItems);

                    if (createdDateCol != null)
                    {
                        if (filterBy == "Most Recently Added" || filterBy == "Default")
                            createdDateCol.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Descending;
                        else if (filterBy == "Oldest Added")
                            createdDateCol.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Ascending;
                    }
                }

                DefaultListPageTemplate.DisableDefaultRowHighlight(dgvItems);
            });

            if (lblPageInfo != null)
                lblPageInfo.Text = $"Page {_currentPage} of {totalPages} ({_filteredItems.Count} cartridges)";

            if (btnFirstPage != null) btnFirstPage.Enabled = _currentPage > 1;
            if (btnPrevPage != null) btnPrevPage.Enabled = _currentPage > 1;
            if (btnNextPage != null) btnNextPage.Enabled = _currentPage < totalPages;
            if (btnLastPage != null) btnLastPage.Enabled = _currentPage < totalPages;
        }
    }
}
