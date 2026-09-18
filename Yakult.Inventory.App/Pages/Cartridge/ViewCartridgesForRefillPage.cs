using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ReaLTaiizor.Controls;
using WinForms = System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    // IMPORTANT:
    // Cartridge ModelNumber must NEVER be used for lookup, grouping, or auto-matching.
    // Model is NOT unique; multiple cartridges can share the same model.
    // All inbound and refill operations must explicitly target a cartridge
    // by SerialNumber or ItemId only.

    /// <summary>
    /// Page for viewing and managing cartridge returns for refill.
    /// Data is sourced from the Request Portal (Request table).
    /// Returns are quantity-based with NO serial numbers.
    /// </summary>
    public partial class ViewCartridgesForRefillPage : UserControl
    {
        private DefaultListPageLayout _layout;
        private HopeButton _btnRecordReturn;
        private HopeButton _btnExportExcel;
        private HopeButton _btnMarkRefilling;
        private HopeButton _btnMarkRefilled;
        private HopeButton _btnMarkReady;

        private MaterialCard _cardTotal;
        private MaterialCard _cardForRefill;
        private MaterialCard _cardRefilling;

        private WinForms.Label _lblTotalCount;
        private WinForms.Label _lblForRefillCount;
        private WinForms.Label _lblRefillingCount;

        private PoisonDataGridView dgvCartridges;
        private WinForms.ComboBox cmbStatusFilter;
        private WinForms.CheckBox _selectAllCheckBox;
        private bool _suppressSelectAllCheckBoxChanged;

        // Data source: CartridgeRepository queries Request table for portal returns
        private CartridgeRepository _repository;
        private List<CartridgeReturnForRefillDto> _allCartridges;
        private List<CartridgeReturnForRefillDto> _filteredCartridges;

        // Sorting state
        private string _sortColumnName;
        private System.Windows.Forms.SortOrder _sortOrder = System.Windows.Forms.SortOrder.None;
        private bool _sortByDropdownInitialized = false;

        private WinForms.Button btnFirstPage, btnPrevPage, btnNextPage, btnLastPage;
        private WinForms.Label lblPageInfo;
        private int _currentPage = 1;
        private int _pageSize = 10;

        public ViewCartridgesForRefillPage()
        {
            _repository = new CartridgeRepository();

            InitializeComponent();
            BuildUi();
            LoadCartridges();
        }

        private void BuildUi()
        {
            SuspendLayout();
            Controls.Clear();

            Dock = DockStyle.Fill;
            BackColor = Color.White;

            _layout = DefaultListPageTemplate.Create(
                "Cartridges For Refill — Operational Queue",
                "Search by Name, Employee, Branch...",
                (s, e) => ApplyFilter(),
                () => LoadCartridges());

            // Filter options - NO serial/model numbers (quantity-based returns)
            _layout.FilterByComboBox.Items.Clear();
            _layout.FilterByComboBox.Items.AddRange(new object[] { "All", "Name", "Employee", "Branch" });
            _layout.FilterByComboBox.SelectedIndex = 0;
            _layout.FilterByComboBox.SelectedIndexChanged += (s, e) => ApplyFilter();

            var lblStatusFilter = new WinForms.Label
            {
                Text = "Status:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = UiTheme.Colors.TextMuted,
                Location = new Point(910, 62)
            };

            cmbStatusFilter = new WinForms.ComboBox
            {
                Location = new Point(960, 58),
                Width = 150,
                Font = new Font("Segoe UI", 9F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat
            };
            cmbStatusFilter.Items.AddRange(new object[] { "All Status", "For Refill", "Refilling" });
            cmbStatusFilter.SelectedIndex = 0;
            cmbStatusFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

            _layout.HeaderPanel.Controls.Add(lblStatusFilter);
            _layout.HeaderPanel.Controls.Add(cmbStatusFilter);

            // NEW: Record Return button
            _btnRecordReturn = new HopeButton
            {
                Text = "\U0001F4E5 Record Return",
                Font = UiTheme.Fonts.Button,
                Size = new Size(150, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(_btnRecordReturn, Color.FromArgb(52, 152, 219), Color.FromArgb(220, 240, 255));
            _btnRecordReturn.Click += BtnRecordReturn_Click;

            _btnExportExcel = new HopeButton
            {
                Text = "\U0001F4CA Export to Excel",
                Font = UiTheme.Fonts.Button,
                Size = new Size(160, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(_btnExportExcel, Color.FromArgb(46, 204, 113), Color.FromArgb(230, 245, 230));
            _btnExportExcel.Click += BtnExportExcel_Click;

            _btnMarkRefilling = new HopeButton
            {
                Text = "\u2699\uFE0F Mark Refilling",
                Font = UiTheme.Fonts.Button,
                Size = new Size(150, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(_btnMarkRefilling, Color.FromArgb(241, 196, 15), Color.FromArgb(255, 248, 220));
            _btnMarkRefilling.Click += BtnMarkRefilling_Click;

            _btnMarkRefilled = new HopeButton
            {
                Text = "\u2705 Mark Refilled",
                Font = UiTheme.Fonts.Button,
                Size = new Size(150, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(_btnMarkRefilled, Color.FromArgb(46, 204, 113), Color.FromArgb(230, 245, 230));
            _btnMarkRefilled.Click += BtnMarkRefilled_Click;

            _btnMarkReady = new HopeButton
            {
                Text = "\U0001F504 Mark Ready",
                Font = UiTheme.Fonts.Button,
                Size = new Size(140, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(_btnMarkReady, UiTheme.Colors.Primary, UiTheme.Colors.PrimaryHover);
            _btnMarkReady.Click += BtnMarkReady_Click;

            _layout.ButtonLeftFlow.Controls.Add(_btnRecordReturn);
            _layout.ButtonLeftFlow.Controls.Add(_btnExportExcel);
            _layout.ButtonLeftFlow.Controls.Add(_btnMarkRefilling);
            _layout.ButtonLeftFlow.Controls.Add(_btnMarkRefilled);
            _layout.ButtonLeftFlow.Controls.Add(_btnMarkReady);

            _cardTotal = UiFactory.CreateSummaryCard("Total Cartridges", out _lblTotalCount, Color.FromArgb(52, 152, 219));
            _cardForRefill = UiFactory.CreateSummaryCard("For Refill", out _lblForRefillCount, Color.FromArgb(231, 76, 60));
            _cardRefilling = UiFactory.CreateSummaryCard("Refilling", out _lblRefillingCount, Color.FromArgb(241, 196, 15));

            _layout.SummaryFlow.Controls.Add(_cardTotal);
            _layout.SummaryFlow.Controls.Add(_cardForRefill);
            _layout.SummaryFlow.Controls.Add(_cardRefilling);

            dgvCartridges = new PoisonDataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 40,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(224, 224, 224),
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = false,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowTemplate = { Height = 35 },
                EditMode = DataGridViewEditMode.EditOnEnter
            };

            dgvCartridges.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 73, 94);
            dgvCartridges.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvCartridges.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dgvCartridges.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dgvCartridges.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);

            dgvCartridges.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgvCartridges.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvCartridges.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgvCartridges.DefaultCellStyle.Padding = new Padding(10, 5, 10, 5);

            dgvCartridges.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);

            var chkCol = new DataGridViewCheckBoxColumn
            {
                Name = "Selected",
                HeaderText = "",
                Width = 40,
                ReadOnly = false
            };
            dgvCartridges.Columns.Add(chkCol);
            dgvCartridges.Columns["Selected"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvCartridges.Columns["Selected"].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // Columns for quantity-based returns (NO Serial/Model numbers)
            dgvCartridges.Columns.Add(new DataGridViewTextBoxColumn { Name = "ReqId", HeaderText = "ID", Width = 60, ReadOnly = true });
            dgvCartridges.Columns.Add(new DataGridViewTextBoxColumn { Name = "CartridgeName", HeaderText = "Cartridge Name", Width = 200, ReadOnly = true });
            dgvCartridges.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "Qty", Width = 60, ReadOnly = true });
            dgvCartridges.Columns.Add(new DataGridViewTextBoxColumn { Name = "RefillStatus", HeaderText = "Refill Status", Width = 100, ReadOnly = true });
            dgvCartridges.Columns.Add(new DataGridViewTextBoxColumn { Name = "DateReturned", HeaderText = "Date Returned", Width = 140, ReadOnly = true });
            dgvCartridges.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", HeaderText = "Employee", Width = 150, ReadOnly = true });
            dgvCartridges.Columns.Add(new DataGridViewTextBoxColumn { Name = "BranchName", HeaderText = "Branch", Width = 120, ReadOnly = true });
            dgvCartridges.Columns.Add(new DataGridViewTextBoxColumn { Name = "DepartmentName", HeaderText = "Department", Width = 120, ReadOnly = true });
            dgvCartridges.Columns.Add(new DataGridViewTextBoxColumn { Name = "Remarks", HeaderText = "Remarks", Width = 200, ReadOnly = true });

            dgvCartridges.Columns["ReqId"].Visible = false;

            foreach (DataGridViewColumn col in dgvCartridges.Columns)
            {
                if (col is DataGridViewCheckBoxColumn)
                {
                    col.SortMode = DataGridViewColumnSortMode.NotSortable;
                    continue;
                }

                col.SortMode = DataGridViewColumnSortMode.Programmatic;
            }

            // Apply proper styling and color coding
            UiFactory.StyleGrid(dgvCartridges);
            DefaultListPageTemplate.AttachVendorsPillCellPainting(dgvCartridges, c =>
            {
                if (c == null) return false;
                var p = c.Name;
                return string.Equals(p, "Quantity", StringComparison.OrdinalIgnoreCase);
            });

            // Add column header click for sorting
            dgvCartridges.ColumnHeaderMouseClick += DgvCartridges_ColumnHeaderMouseClick;

            _selectAllCheckBox = new WinForms.CheckBox
            {
                Size = new Size(15, 15),
                BackColor = Color.FromArgb(52, 73, 94)
            };
            _selectAllCheckBox.CheckedChanged += SelectAllCheckBox_CheckedChanged;

            var headerCell = dgvCartridges.Columns[0].HeaderCell;
            var headerRect = dgvCartridges.GetCellDisplayRectangle(0, -1, true);
            _selectAllCheckBox.Location = new Point(headerRect.Left + 12, headerRect.Top + 12);
            dgvCartridges.Controls.Add(_selectAllCheckBox);

            PositionSelectAllCheckBox();

            dgvCartridges.Scroll += (s, e) => PositionSelectAllCheckBox();
            dgvCartridges.ColumnWidthChanged += (s, e) => PositionSelectAllCheckBox();
            dgvCartridges.SizeChanged += (s, e) => PositionSelectAllCheckBox();

            dgvCartridges.CellContentClick += DgvCartridges_CellContentClick;
            dgvCartridges.CurrentCellDirtyStateChanged += DgvCartridges_CurrentCellDirtyStateChanged;
            dgvCartridges.CellValueChanged += DgvCartridges_CellValueChanged;

            dgvCartridges.CellFormatting += DgvCartridges_CellFormatting;

            _layout.GridCard.Controls.Add(dgvCartridges);

            var paginationFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true
            };

            btnFirstPage = new WinForms.Button { Text = "\u23EE First", Width = 80, Height = 35 };
            btnPrevPage = new WinForms.Button { Text = "\u25C0 Prev", Width = 80, Height = 35 };
            lblPageInfo = new WinForms.Label { Text = "Page 1 of 1", AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(10, 8, 10, 0) };
            btnNextPage = new WinForms.Button { Text = "Next \u25B6", Width = 80, Height = 35 };
            btnLastPage = new WinForms.Button { Text = "Last \u23ED", Width = 80, Height = 35 };

            btnFirstPage.Click += (s, e) => { _currentPage = 1; UpdatePagination(); };
            btnPrevPage.Click += (s, e) => { if (_currentPage > 1) { _currentPage--; UpdatePagination(); } };
            btnNextPage.Click += (s, e) => { if (_currentPage < GetTotalPages()) { _currentPage++; UpdatePagination(); } };
            btnLastPage.Click += (s, e) => { _currentPage = GetTotalPages(); UpdatePagination(); };

            paginationFlow.Controls.Add(btnFirstPage);
            paginationFlow.Controls.Add(btnPrevPage);
            paginationFlow.Controls.Add(lblPageInfo);
            paginationFlow.Controls.Add(btnNextPage);
            paginationFlow.Controls.Add(btnLastPage);

            _layout.PaginationPanel.Controls.Add(paginationFlow);

            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.SummaryPanel);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_layout.HeaderPanel);

            ResumeLayout();
        }

        private void LoadCartridges()
        {
            try
            {
                // Load from Request Portal data (Request table)
                // Quantity-based returns with NO serial numbers
                _allCartridges = _repository.GetPortalCartridgeReturnsForRefill();
                ApplyFilter();
                UpdateSummaryCards();

                // Initialize Sort By dropdown after first load
                if (!_sortByDropdownInitialized && _layout?.SortByComboBox != null && dgvCartridges != null && dgvCartridges.Columns.Count > 0)
                {
                    DefaultListPageTemplate.SetupSortByDropdown(
                        _layout.SortByComboBox,
                        dgvCartridges,
                        (columnKey, direction) =>
                        {
                            _sortColumnName = columnKey;
                            _sortOrder = direction;

                            if (_layout.FilterByComboBox != null && _layout.FilterByComboBox.SelectedIndex != 0)
                            {
                                _layout.FilterByComboBox.SelectedIndex = 0;
                            }

                            ApplyFilter();
                        },
                        defaultColumnKey: "DateReturned",
                        defaultDirection: System.Windows.Forms.SortOrder.Descending
                    );

                    _sortByDropdownInitialized = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load cartridges for refill: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvCartridges_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = dgvCartridges.Columns[e.ColumnIndex];
            if (col is DataGridViewCheckBoxColumn || col is DataGridViewButtonColumn || col is DataGridViewImageColumn)
                return;

            string columnName = col.Name;

            // Toggle sort order
            if (_sortColumnName == columnName)
            {
                _sortOrder = _sortOrder == System.Windows.Forms.SortOrder.Ascending ? System.Windows.Forms.SortOrder.Descending : System.Windows.Forms.SortOrder.Ascending;
            }
            else
            {
                _sortColumnName = columnName;
                _sortOrder = System.Windows.Forms.SortOrder.Ascending;
            }

            // Sync the Sort By dropdown with the column header sort
            if (_layout?.SortByComboBox != null && !string.IsNullOrEmpty(_sortColumnName))
            {
                var matchingColumn = dgvCartridges.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => c.Name == _sortColumnName);
                if (matchingColumn != null)
                {
                    DefaultListPageTemplate.SyncSortByDropdown(_layout.SortByComboBox, matchingColumn, _sortOrder);
                }
            }

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_allCartridges == null) return;

            var filtered = _allCartridges.AsEnumerable();

            string searchText = _layout.SearchBox.Text?.Trim().ToLower() ?? "";
            string filterBy = _layout.FilterByComboBox.SelectedItem?.ToString() ?? "All";
            string statusFilter = cmbStatusFilter.SelectedItem?.ToString() ?? "All Status";

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                if (filterBy == "Name")
                {
                    filtered = filtered.Where(c => c.CartridgeName != null && c.CartridgeName.ToLower().Contains(searchText));
                }
                else if (filterBy == "Employee")
                {
                    filtered = filtered.Where(c => c.EmployeeName != null && c.EmployeeName.ToLower().Contains(searchText));
                }
                else if (filterBy == "Branch")
                {
                    filtered = filtered.Where(c => c.BranchName != null && c.BranchName.ToLower().Contains(searchText));
                }
                else
                {
                    // Search all fields
                    filtered = filtered.Where(c =>
                        (c.CartridgeName != null && c.CartridgeName.ToLower().Contains(searchText)) ||
                        (c.EmployeeName != null && c.EmployeeName.ToLower().Contains(searchText)) ||
                        (c.BranchName != null && c.BranchName.ToLower().Contains(searchText)) ||
                        (c.DepartmentName != null && c.DepartmentName.ToLower().Contains(searchText)));
                }
            }

            if (statusFilter != "All Status")
            {
                filtered = filtered.Where(c => c.RefillStatus == statusFilter);
            }

            // Apply sorting
            if (!string.IsNullOrEmpty(_sortColumnName))
            {
                bool ascending = _sortOrder == System.Windows.Forms.SortOrder.Ascending;

                if (_sortColumnName == "CartridgeName")
                {
                    filtered = ascending ? filtered.OrderBy(c => c.CartridgeName) : filtered.OrderByDescending(c => c.CartridgeName);
                }
                else if (_sortColumnName == "Quantity")
                {
                    filtered = ascending ? filtered.OrderBy(c => c.Quantity) : filtered.OrderByDescending(c => c.Quantity);
                }
                else if (_sortColumnName == "RefillStatus")
                {
                    filtered = ascending ? filtered.OrderBy(c => c.RefillStatus) : filtered.OrderByDescending(c => c.RefillStatus);
                }
                else if (_sortColumnName == "DateReturned")
                {
                    filtered = ascending ? filtered.OrderBy(c => c.DateReturned) : filtered.OrderByDescending(c => c.DateReturned);
                }
                else if (_sortColumnName == "EmployeeName")
                {
                    filtered = ascending ? filtered.OrderBy(c => c.EmployeeName) : filtered.OrderByDescending(c => c.EmployeeName);
                }
                else if (_sortColumnName == "BranchName")
                {
                    filtered = ascending ? filtered.OrderBy(c => c.BranchName) : filtered.OrderByDescending(c => c.BranchName);
                }
                else if (_sortColumnName == "DepartmentName")
                {
                    filtered = ascending ? filtered.OrderBy(c => c.DepartmentName) : filtered.OrderByDescending(c => c.DepartmentName);
                }

                // Clear all column glyphs
                foreach (DataGridViewColumn col in dgvCartridges.Columns)
                {
                    col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                }

                // Apply glyph to sorted column
                var sortedColumn = dgvCartridges.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => c.Name == _sortColumnName);
                if (sortedColumn != null)
                {
                    sortedColumn.HeaderCell.SortGlyphDirection = _sortOrder;
                }
            }

            _filteredCartridges = filtered.ToList();
            _currentPage = 1;
            UpdatePagination();
        }

        private void UpdatePagination()
        {
            if (_filteredCartridges == null || _filteredCartridges.Count == 0)
            {
                dgvCartridges.Rows.Clear();
                lblPageInfo.Text = "No cartridges found";
                btnFirstPage.Enabled = btnPrevPage.Enabled = btnNextPage.Enabled = btnLastPage.Enabled = false;
                return;
            }

            int totalPages = GetTotalPages();
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1) _currentPage = 1;

            var pagedData = _filteredCartridges
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            dgvCartridges.Rows.Clear();
            foreach (var cartridge in pagedData)
            {
                int rowIndex = dgvCartridges.Rows.Add();
                var row = dgvCartridges.Rows[rowIndex];

                row.Cells["Selected"].Value = false;
                row.Cells["ReqId"].Value = cartridge.ReqId;
                row.Cells["CartridgeName"].Value = cartridge.CartridgeName;
                row.Cells["Quantity"].Value = cartridge.Quantity;
                row.Cells["RefillStatus"].Value = cartridge.RefillStatus;
                row.Cells["DateReturned"].Value = cartridge.DateReturned.ToString("yyyy-MM-dd HH:mm");
                row.Cells["EmployeeName"].Value = cartridge.EmployeeName ?? "";
                row.Cells["BranchName"].Value = cartridge.BranchName ?? "";
                row.Cells["DepartmentName"].Value = cartridge.DepartmentName ?? "";
                row.Cells["Remarks"].Value = cartridge.Remarks ?? "";

                row.Tag = cartridge;
            }

            lblPageInfo.Text = $"Page {_currentPage} of {totalPages} ({_filteredCartridges.Count} total)";
            btnFirstPage.Enabled = btnPrevPage.Enabled = _currentPage > 1;
            btnNextPage.Enabled = btnLastPage.Enabled = _currentPage < totalPages;
        }

        private int GetTotalPages()
        {
            if (_filteredCartridges == null || _filteredCartridges.Count == 0) return 1;
            return (int)Math.Ceiling((double)_filteredCartridges.Count / _pageSize);
        }

        private void UpdateSummaryCards()
        {
            if (_allCartridges == null) return;

            int total = _allCartridges.Count;
            int forRefill = _allCartridges.Count(c => c.RefillStatus == "For Refill");
            int refilling = _allCartridges.Count(c => c.RefillStatus == "Refilling");

            _lblTotalCount.Text = total.ToString();
            _lblForRefillCount.Text = forRefill.ToString();
            _lblRefillingCount.Text = refilling.ToString();
        }

        private void SelectAllCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressSelectAllCheckBoxChanged)
                return;

            foreach (DataGridViewRow row in dgvCartridges.Rows)
            {
                row.Cells["Selected"].Value = _selectAllCheckBox.Checked;
            }
        }

        private void DgvCartridges_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvCartridges.CurrentCell is DataGridViewCheckBoxCell && dgvCartridges.IsCurrentCellDirty)
            {
                dgvCartridges.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void DgvCartridges_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            if (dgvCartridges.Columns[e.ColumnIndex].Name == "Selected")
            {
                dgvCartridges.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void DgvCartridges_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            if (dgvCartridges.Columns[e.ColumnIndex].Name == "Selected")
            {
                SyncSelectAllCheckBoxFromRows();
            }
        }

        private void SyncSelectAllCheckBoxFromRows()
        {
            if (_selectAllCheckBox == null)
                return;

            bool hasRows = dgvCartridges.Rows.Count > 0;
            bool allChecked = hasRows;

            foreach (DataGridViewRow row in dgvCartridges.Rows)
            {
                bool isChecked = row.Cells["Selected"].Value is bool b && b;
                if (!isChecked)
                {
                    allChecked = false;
                    break;
                }
            }

            try
            {
                _suppressSelectAllCheckBoxChanged = true;
                _selectAllCheckBox.Checked = hasRows && allChecked;
            }
            finally
            {
                _suppressSelectAllCheckBoxChanged = false;
            }
        }

        private void PositionSelectAllCheckBox()
        {
            if (_selectAllCheckBox == null || dgvCartridges == null || dgvCartridges.Columns.Count == 0)
                return;

            var headerRect = dgvCartridges.GetCellDisplayRectangle(0, -1, true);
            int x = headerRect.Left + (headerRect.Width - _selectAllCheckBox.Width) / 2;
            int y = headerRect.Top + (headerRect.Height - _selectAllCheckBox.Height) / 2;
            _selectAllCheckBox.Location = new Point(Math.Max(0, x), Math.Max(0, y));
        }

        private void DgvCartridges_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (dgvCartridges.Columns[e.ColumnIndex].Name == "RefillStatus")
            {
                string status = e.Value?.ToString() ?? "";
                if (status == "For Refill")
                    e.CellStyle.ForeColor = Color.FromArgb(231, 76, 60);
                else if (status == "Refilling")
                    e.CellStyle.ForeColor = Color.FromArgb(241, 196, 15);
            }
        }

        /// <summary>
        /// Opens the CartridgeReturnDialog to record a cartridge return.
        /// Quantity-based, no serial numbers required.
        /// </summary>
        private void BtnRecordReturn_Click(object sender, EventArgs e)
        {
            using (var dialog = new CartridgeReturnDialog())
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    // Refresh the list after recording a return
                    LoadCartridges();
                }
            }
        }

        private void BtnExportExcel_Click(object sender, EventArgs e)
        {
            try
            {
                if (_filteredCartridges == null || _filteredCartridges.Count == 0)
                {
                    MessageBox.Show("No data to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                    sfd.FileName = $"CartridgesForRefill_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    sfd.Title = "Export Cartridges to CSV";

                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        var csv = new StringBuilder();
                        csv.AppendLine("Request ID,Cartridge Name,Quantity,Refill Status,Date Returned,Employee,Branch,Department,Remarks");

                        foreach (var cartridge in _filteredCartridges)
                        {
                            csv.AppendLine($"{cartridge.ReqId}," +
                                         $"\"{cartridge.CartridgeName}\"," +
                                         $"{cartridge.Quantity}," +
                                         $"\"{cartridge.RefillStatus}\"," +
                                         $"\"{cartridge.DateReturned:yyyy-MM-dd HH:mm}\"," +
                                         $"\"{cartridge.EmployeeName ?? ""}\"," +
                                         $"\"{cartridge.BranchName ?? ""}\"," +
                                         $"\"{cartridge.DepartmentName ?? ""}\"," +
                                         $"\"{cartridge.Remarks?.Replace("\"", "\"\"") ?? ""}\"");
                        }

                        File.WriteAllText(sfd.FileName, csv.ToString(), Encoding.UTF8);
                        MessageBox.Show($"Exported {_filteredCartridges.Count} cartridges successfully!", "Export Complete",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting to Excel: {ex.Message}", "Export Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnMarkRefilling_Click(object sender, EventArgs e)
        {
            UpdateSelectedCartridgesStatus("Refilling");
        }

        private void BtnMarkRefilled_Click(object sender, EventArgs e)
        {
            UpdateSelectedCartridgesStatus("Refilled");
        }

        private void BtnMarkReady_Click(object sender, EventArgs e)
        {
            var selectedIds = GetSelectedRequestIds();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show("Please select at least one cartridge return.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Mark {selectedIds.Count} cartridge return(s) as Ready (Completed)?\n\nThis will mark the refill process as complete.",
                "Confirm Mark Ready",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    foreach (int reqId in selectedIds)
                    {
                        _repository.UpdatePortalReturnRefillStatus(reqId, "Refilled", AppSession.CurrentUserId);
                    }

                    MessageBox.Show($"{selectedIds.Count} cartridge return(s) marked as Ready!", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadCartridges();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error updating cartridge returns: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void UpdateSelectedCartridgesStatus(string newStatus)
        {
            var selectedRows = GetSelectedRowsWithStatus();
            if (selectedRows.Count == 0)
            {
                MessageBox.Show("Please select at least one cartridge return.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Determine required previous status for valid transition
            string requiredPrevious = null;
            if (newStatus == "Refilling") requiredPrevious = "For Refill";
            else if (newStatus == "Refilled") requiredPrevious = "Refilling";

            // Validate transitions before confirming
            if (requiredPrevious != null)
            {
                var invalidRows = selectedRows.Where(r => r.Value != requiredPrevious).ToList();
                if (invalidRows.Count > 0)
                {
                    MessageBox.Show(
                        $"Cannot transition to '{newStatus}': {invalidRows.Count} selected row(s) are not in '{requiredPrevious}' status.\n\n" +
                        $"Only cartridges with status '{requiredPrevious}' can be moved to '{newStatus}'.",
                        "Invalid Transition",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            var result = MessageBox.Show(
                $"Update {selectedRows.Count} cartridge return(s) to status '{newStatus}'?",
                "Confirm Status Update",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                int successCount = 0;
                var errors = new List<string>();

                foreach (var kvp in selectedRows)
                {
                    try
                    {
                        _repository.UpdatePortalReturnRefillStatus(kvp.Key, newStatus, AppSession.CurrentUserId);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"ReqId {kvp.Key}: {ex.Message}");
                    }
                }

                if (errors.Count == 0)
                {
                    MessageBox.Show($"{successCount} cartridge return(s) updated to '{newStatus}'!", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"{successCount} succeeded, {errors.Count} failed.\n\n{string.Join("\n", errors.Take(5))}",
                        "Partial Update",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                LoadCartridges();
            }
        }

        /// <summary>
        /// Gets selected rows as ReqId → current RefillStatus pairs.
        /// </summary>
        private Dictionary<int, string> GetSelectedRowsWithStatus()
        {
            var selected = new Dictionary<int, string>();
            foreach (DataGridViewRow row in dgvCartridges.Rows)
            {
                if (row.Cells["Selected"].Value is bool isSelected && isSelected)
                {
                    if (row.Cells["ReqId"].Value != null)
                    {
                        int reqId = Convert.ToInt32(row.Cells["ReqId"].Value);
                        string status = row.Cells["RefillStatus"].Value?.ToString() ?? "For Refill";
                        selected[reqId] = status;
                    }
                }
            }
            return selected;
        }

        private List<int> GetSelectedRequestIds()
        {
            var selectedIds = new List<int>();
            foreach (DataGridViewRow row in dgvCartridges.Rows)
            {
                if (row.Cells["Selected"].Value is bool isSelected && isSelected)
                {
                    if (row.Cells["ReqId"].Value != null)
                    {
                        selectedIds.Add(Convert.ToInt32(row.Cells["ReqId"].Value));
                    }
                }
            }
            return selectedIds;
        }
    }
}

