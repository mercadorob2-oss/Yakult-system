using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ReaLTaiizor.Controls;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    public partial class ViewCartridgeTrackingPage : UserControl
    {
        private DefaultListPageLayout _layout;
        private PoisonDataGridView dgvTracking;
        private CartridgeRepository _repository;

        private ComboBox cmbItem;
        private ComboBox cmbEmployee;
        private ComboBox cmbMovementType;

        private DataTable _table;
        private DataView _view;

        // Pagination
        private System.Windows.Forms.Button btnFirstPage, btnPrevPage, btnNextPage, btnLastPage;
        private Label lblPageInfo;
        private int _currentPage = 1;
        private int _pageSize = 20;

        // Sorting state
        private DataGridViewColumn _sortColumn;
        private System.Windows.Forms.SortOrder _sortOrder = System.Windows.Forms.SortOrder.None;
        private bool _sortByDropdownInitialized = false;

        public ViewCartridgeTrackingPage()
        {
            _repository = new CartridgeRepository();
            InitializeComponent();
            BuildUi();
            _ = LoadLookupsAndDataAsync();
        }

        private void BuildUi()
        {
            SuspendLayout();
            Controls.Clear();

            Dock = DockStyle.Fill;
            BackColor = Color.White;

            _layout = DefaultListPageTemplate.Create(
                "Cartridge Tracking",
                "Search by item name...",
                (s, e) => ApplySearchFilter(),
                () => { _ = LoadLookupsAndDataAsync(); });

            // Hook up Filter By event
            if (_layout.FilterByComboBox != null)
            {
                _layout.FilterByComboBox.SelectedIndexChanged += (s, e) =>
                {
                    _sortColumn = null;
                    _sortOrder = System.Windows.Forms.SortOrder.None;
                    ApplySearchFilter();
                };
            }

            cmbItem = new ComboBox
            {
                Width = 220,
                Font = new Font("Segoe UI", 9F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 20, 0)
            };
            cmbItem.SelectedIndexChanged += (s, e) => _ = LoadDataAsync();

            cmbEmployee = new ComboBox
            {
                Width = 200,
                Font = new Font("Segoe UI", 9F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 20, 0)
            };
            cmbEmployee.SelectedIndexChanged += (s, e) => _ = LoadDataAsync();

            cmbMovementType = new ComboBox
            {
                Width = 180,
                Font = new Font("Segoe UI", 9F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat
            };
            cmbMovementType.SelectedIndexChanged += (s, e) => _ = LoadDataAsync();

            // Filter row — separate panel below the header so it never overlaps the title or search bar
            var pnlFilters = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                BackColor = UiTheme.Colors.HeaderBack,
                Padding = new Padding(16, 8, 16, 6)
            };

            var filterFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            Label MakeFilterLabel(string text) => new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = UiTheme.Colors.TextMuted,
                Margin = new Padding(0, 5, 6, 0)
            };

            filterFlow.Controls.Add(MakeFilterLabel("Item:"));
            filterFlow.Controls.Add(cmbItem);
            filterFlow.Controls.Add(MakeFilterLabel("Employee:"));
            filterFlow.Controls.Add(cmbEmployee);
            filterFlow.Controls.Add(MakeFilterLabel("Movement:"));
            filterFlow.Controls.Add(cmbMovementType);

            pnlFilters.Controls.Add(filterFlow);

            dgvTracking = new PoisonDataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = true,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                RowHeadersVisible = false
            };

            dgvTracking.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CreatedAt",
                HeaderText = "Date / Time",
                MinimumWidth = 140,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" }
            });

            dgvTracking.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ItemName",
                HeaderText = "Cartridge Item",
                MinimumWidth = 180,
                FillWeight = 25
            });

            dgvTracking.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "MovementType",
                HeaderText = "Movement",
                MinimumWidth = 100,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            });

            dgvTracking.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Quantity",
                HeaderText = "Qty",
                MinimumWidth = 60,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            });

            dgvTracking.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CartridgeCategory",
                HeaderText = "Category",
                MinimumWidth = 100,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            });

            dgvTracking.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "EmployeeName",
                HeaderText = "Employee",
                MinimumWidth = 140,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            });

            dgvTracking.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "BranchName",
                HeaderText = "Branch",
                MinimumWidth = 120,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            });

            dgvTracking.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "DepartmentName",
                HeaderText = "Department",
                MinimumWidth = 120,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            });

            dgvTracking.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ConditionType",
                HeaderText = "Condition",
                MinimumWidth = 90,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            });

            dgvTracking.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "PerformedBy",
                HeaderText = "Performed By",
                MinimumWidth = 110,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            });

            dgvTracking.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Remarks",
                HeaderText = "Remarks",
                MinimumWidth = 150,
                FillWeight = 20
            });

            foreach (DataGridViewColumn col in dgvTracking.Columns)
            {
                col.SortMode = DataGridViewColumnSortMode.Programmatic;
            }

            // Apply proper styling and color coding
            UiFactory.StyleGrid(dgvTracking);
            DefaultListPageTemplate.AttachVendorsPillCellPainting(dgvTracking, c =>
            {
                if (c == null) return false;
                var p = c.DataPropertyName;
                return string.Equals(p, "Quantity", StringComparison.OrdinalIgnoreCase);
            });

            // Add column header click for sorting
            dgvTracking.ColumnHeaderMouseClick += DgvTracking_ColumnHeaderMouseClick;

            _layout.GridCard.Controls.Add(dgvTracking);

            // --- Pagination ---
            if (_layout.SummaryPanel != null)
                _layout.SummaryPanel.Visible = false;
            if (_layout.PaginationPanel != null)
                _layout.PaginationPanel.Visible = true;

            btnFirstPage = new System.Windows.Forms.Button { Text = "<<", Width = 45, Height = 25, Left = 0,   Top = 5 };
            btnPrevPage  = new System.Windows.Forms.Button { Text = "<",  Width = 45, Height = 25, Left = 50,  Top = 5 };
            lblPageInfo  = new Label  { Width = 280, Height = 25, Left = 100, Top = 8, TextAlign = ContentAlignment.MiddleCenter };
            btnNextPage  = new System.Windows.Forms.Button { Text = ">",  Width = 45, Height = 25, Left = 385, Top = 5 };
            btnLastPage  = new System.Windows.Forms.Button { Text = ">>", Width = 45, Height = 25, Left = 435, Top = 5 };

            btnFirstPage.Click += (s, e) => { _currentPage = 1; UpdatePagination(); };
            btnPrevPage.Click  += (s, e) => { if (_currentPage > 1) { _currentPage--; UpdatePagination(); } };
            btnNextPage.Click  += (s, e) =>
            {
                if (_view == null) return;
                int totalPages = (int)Math.Ceiling((double)_view.Count / _pageSize);
                if (_currentPage < totalPages) { _currentPage++; UpdatePagination(); }
            };
            btnLastPage.Click  += (s, e) =>
            {
                if (_view == null) return;
                int totalPages = (int)Math.Ceiling((double)_view.Count / _pageSize);
                _currentPage = totalPages;
                UpdatePagination();
            };

            if (_layout.PaginationPanel != null)
            {
                _layout.PaginationPanel.Controls.Clear();
                _layout.PaginationPanel.Controls.AddRange(new Control[] { btnFirstPage, btnPrevPage, lblPageInfo, btnNextPage, btnLastPage });
            }

            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.SummaryPanel);
            Controls.Add(pnlFilters);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_layout.HeaderPanel);

            ResumeLayout(true);

            DefaultListPageTemplate.SetupInitialPageFocus(_layout.SearchBox, dgvTracking, _layout.RefreshButton);
        }

        private async Task LoadLookupsAndDataAsync()
        {
            await LoadLookupsAsync();
            await LoadDataAsync();
        }

        private async Task LoadLookupsAsync()
        {
            try
            {
                var items = await Task.Run(() =>
                {
                    var list = new List<LookupItem> { new LookupItem { Id = 0, Name = "All" } };
                    list.AddRange(_repository.GetCartridgeMovementItemsLookup());
                    return list;
                });

                var employees = await Task.Run(() =>
                {
                    var list = new List<LookupItem> { new LookupItem { Id = 0, Name = "All" } };
                    list.AddRange(_repository.GetCartridgeMovementEmployeeLookup());
                    return list;
                });

                // Back on UI thread
                cmbItem.DataSource = null;
                cmbItem.DisplayMember = "Name";
                cmbItem.ValueMember = "Id";
                cmbItem.DataSource = items;

                cmbEmployee.DataSource = null;
                cmbEmployee.DisplayMember = "Name";
                cmbEmployee.ValueMember = "Id";
                cmbEmployee.DataSource = employees;

                cmbMovementType.Items.Clear();
                cmbMovementType.Items.AddRange(new object[] { "All", "StockIn", "Issued", "Returned", "RefillIn", "Adjustment" });
                if (cmbMovementType.Items.Count > 0 && cmbMovementType.SelectedIndex < 0)
                    cmbMovementType.SelectedIndex = 0;

                if (cmbItem.Items.Count > 0 && cmbItem.SelectedIndex < 0)
                    cmbItem.SelectedIndex = 0;

                if (cmbEmployee.Items.Count > 0 && cmbEmployee.SelectedIndex < 0)
                    cmbEmployee.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load tracking filters: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                int? itemId = null;
                int? employeeId = null;
                string movementType = null;

                if (cmbItem?.SelectedValue is int selectedItemId && selectedItemId > 0)
                    itemId = selectedItemId;

                if (cmbEmployee?.SelectedValue is int selectedEmployeeId && selectedEmployeeId > 0)
                    employeeId = selectedEmployeeId;

                var movement = cmbMovementType?.SelectedItem?.ToString();
                if (!string.IsNullOrWhiteSpace(movement) && !string.Equals(movement, "All", StringComparison.OrdinalIgnoreCase))
                    movementType = movement;

                var table = await Task.Run(() =>
                    _repository.GetCartridgeMovementHistory(itemId, employeeId, movementType));

                // Back on UI thread
                _table = table;
                _view = new DataView(_table);
                _currentPage = 1;
                ApplySearchFilter();

                // Initialize Sort By dropdown after first load
                if (!_sortByDropdownInitialized && _layout?.SortByComboBox != null && dgvTracking != null && dgvTracking.Columns.Count > 0)
                {
                    DefaultListPageTemplate.SetupSortByDropdown(
                        _layout.SortByComboBox,
                        dgvTracking,
                        (columnKey, direction) =>
                        {
                            var col = dgvTracking.Columns.Cast<DataGridViewColumn>()
                                .FirstOrDefault(c => c.DataPropertyName == columnKey || c.Name == columnKey);

                            if (col != null)
                            {
                                _sortColumn = col;
                                _sortOrder = direction;

                                if (_layout.FilterByComboBox != null && _layout.FilterByComboBox.SelectedIndex != 0)
                                    _layout.FilterByComboBox.SelectedIndex = 0;

                                ApplySearchFilter();
                            }
                        },
                        defaultColumnKey: "CreatedAt",
                        defaultDirection: System.Windows.Forms.SortOrder.Descending
                    );

                    _sortByDropdownInitialized = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load cartridge tracking data: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvTracking_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = dgvTracking.Columns[e.ColumnIndex];
            if (col is DataGridViewCheckBoxColumn || col is DataGridViewButtonColumn || col is DataGridViewImageColumn)
                return;

            // Toggle sort order
            if (_sortColumn == col)
            {
                _sortOrder = _sortOrder == System.Windows.Forms.SortOrder.Ascending ? System.Windows.Forms.SortOrder.Descending : System.Windows.Forms.SortOrder.Ascending;
            }
            else
            {
                _sortColumn = col;
                _sortOrder = System.Windows.Forms.SortOrder.Ascending;
            }

            // Sync the Sort By dropdown with the column header sort
            if (_layout?.SortByComboBox != null && _sortColumn != null)
            {
                DefaultListPageTemplate.SyncSortByDropdown(_layout.SortByComboBox, _sortColumn, _sortOrder);
            }

            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            if (_view == null)
                return;

            var q = (_layout.SearchBox.Text ?? string.Empty).Trim().Replace("'", "''");
            if (string.IsNullOrWhiteSpace(q) || q == "Search by item name...")
                _view.RowFilter = string.Empty;
            else
                _view.RowFilter = $"ItemName LIKE '%{q}%'";

            if (_sortColumn != null && !string.IsNullOrEmpty(_sortColumn.DataPropertyName))
            {
                string sortDir = _sortOrder == System.Windows.Forms.SortOrder.Ascending ? "ASC" : "DESC";
                _view.Sort = $"{_sortColumn.DataPropertyName} {sortDir}";
            }
            else
            {
                _view.Sort = string.Empty;
            }

            _currentPage = 1;
            UpdatePagination();
        }

        private void UpdatePagination()
        {
            if (_view == null || _table == null)
                return;

            int total = _view.Count;
            int totalPages = total == 0 ? 1 : (int)Math.Ceiling((double)total / _pageSize);
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1) _currentPage = 1;

            // Extract the current page rows from the filtered/sorted DataView into a new DataTable
            var pagedTable = _table.Clone();
            int start = (_currentPage - 1) * _pageSize;
            int end = Math.Min(start + _pageSize, total);
            for (int i = start; i < end; i++)
                pagedTable.ImportRow(_view[i].Row);

            dgvTracking.DataSource = pagedTable;

            DefaultListPageTemplate.DisableDefaultRowHighlight(dgvTracking, _layout.RefreshButton);

            // Reapply sort glyphs (lost on DataSource rebind)
            foreach (DataGridViewColumn col in dgvTracking.Columns)
                col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            if (_sortColumn != null)
            {
                var col = dgvTracking.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => c.DataPropertyName == _sortColumn.DataPropertyName);
                if (col != null)
                    col.HeaderCell.SortGlyphDirection = _sortOrder;
            }

            if (lblPageInfo != null)
                lblPageInfo.Text = $"Page {_currentPage} of {totalPages}  ({total} records)";

            if (btnFirstPage != null) btnFirstPage.Enabled = _currentPage > 1;
            if (btnPrevPage  != null) btnPrevPage.Enabled  = _currentPage > 1;
            if (btnNextPage  != null) btnNextPage.Enabled  = _currentPage < totalPages;
            if (btnLastPage  != null) btnLastPage.Enabled  = _currentPage < totalPages;
        }
    }
}
