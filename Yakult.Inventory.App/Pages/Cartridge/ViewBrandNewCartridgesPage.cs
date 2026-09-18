using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ReaLTaiizor.Controls;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    public partial class ViewBrandNewCartridgesPage : UserControl
    {
        private DefaultListPageLayout _layout;
        private PoisonDataGridView dgvBrandNew;
        private CartridgeRepository _repository;
        private DataTable _table;
        private DataView _view;

        private System.Windows.Forms.Button btnFirstPage, btnPrevPage, btnNextPage, btnLastPage;
        private Label lblPageInfo;
        private int _currentPage = 1;
        private int _pageSize = 10;

        // Sorting state
        private DataGridViewColumn _sortColumn;
        private System.Windows.Forms.SortOrder _sortOrder = System.Windows.Forms.SortOrder.None;
        private bool _sortByDropdownInitialized = false;

        public ViewBrandNewCartridgesPage()
        {
            _repository = new CartridgeRepository();
            InitializeComponent();
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            SuspendLayout();
            Controls.Clear();

            Dock = DockStyle.Fill;
            BackColor = Color.White;

            _layout = DefaultListPageTemplate.Create(
                "Brand New Cartridges",
                "Search by item name, category...",
                (s, e) => ApplyFilter(),
                () => LoadData());

            // Hook up Filter By event
            if (_layout.FilterByComboBox != null)
            {
                _layout.FilterByComboBox.SelectedIndexChanged += (s, e) =>
                {
                    _sortColumn = null;
                    _sortOrder = System.Windows.Forms.SortOrder.None;
                    ApplyFilter();
                };
            }

            dgvBrandNew = new PoisonDataGridView
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
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false
            };

            dgvBrandNew.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ItemName",
                HeaderText = "Item Name",
                MinimumWidth = 250,
                FillWeight = 60
            });

            dgvBrandNew.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "AvailableQuantity",
                HeaderText = "Available Quantity",
                MinimumWidth = 140,
                FillWeight = 15,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dgvBrandNew.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CartridgeCategory",
                HeaderText = "Cartridge Category",
                MinimumWidth = 160,
                FillWeight = 15
            });

            dgvBrandNew.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "VendorName",
                HeaderText = "Vendor",
                MinimumWidth = 150,
                FillWeight = 10
            });

            foreach (DataGridViewColumn col in dgvBrandNew.Columns)
            {
                col.SortMode = DataGridViewColumnSortMode.Programmatic;
            }

            // Apply proper styling and color coding
            UiFactory.StyleGrid(dgvBrandNew);
            DefaultListPageTemplate.AttachVendorsPillCellPainting(dgvBrandNew, c =>
            {
                if (c == null) return false;
                var p = c.DataPropertyName;
                return string.Equals(p, "AvailableQuantity", StringComparison.OrdinalIgnoreCase);
            });

            // Add column header click for sorting
            dgvBrandNew.ColumnHeaderMouseClick += DgvBrandNew_ColumnHeaderMouseClick;
            dgvBrandNew.CellPainting += DgvBrandNew_CellPainting;

            _layout.GridCard.Controls.Add(dgvBrandNew);

            BuildPaginationControls();

            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.SummaryPanel);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_layout.HeaderPanel);

            ResumeLayout(true);

            DefaultListPageTemplate.SetupInitialPageFocus(_layout.SearchBox, dgvBrandNew, _layout.RefreshButton);
        }

        private void ApplyGridFillSizing()
        {
            if (dgvBrandNew == null)
                return;

            dgvBrandNew.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            foreach (DataGridViewColumn col in dgvBrandNew.Columns)
            {
                if (col == null) continue;
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
        }

        private void BuildPaginationControls()
        {
            if (_layout?.PaginationPanel == null)
                return;

            _layout.PaginationPanel.Visible = true;

            btnFirstPage = new System.Windows.Forms.Button { Text = "<<", Width = 45, Height = 25, Left = 0, Top = 5 };
            btnPrevPage = new System.Windows.Forms.Button { Text = "<", Width = 45, Height = 25, Left = 50, Top = 5 };
            lblPageInfo = new Label { AutoSize = false, Width = 260, Left = 100, Top = 10, Font = new Font("Segoe UI", 9F, FontStyle.Regular), TextAlign = ContentAlignment.MiddleLeft };
            btnNextPage = new System.Windows.Forms.Button { Text = ">", Width = 45, Height = 25, Left = 370, Top = 5 };
            btnLastPage = new System.Windows.Forms.Button { Text = ">>", Width = 45, Height = 25, Left = 420, Top = 5 };

            btnFirstPage.Click += BtnFirstPage_Click;
            btnPrevPage.Click += BtnPrevPage_Click;
            btnNextPage.Click += BtnNextPage_Click;
            btnLastPage.Click += BtnLastPage_Click;

            _layout.PaginationPanel.Controls.Clear();
            _layout.PaginationPanel.Controls.AddRange(new Control[]
            {
                btnFirstPage, btnPrevPage, lblPageInfo, btnNextPage, btnLastPage
            });
        }

        private void LoadData()
        {
            try
            {
                var dt = _repository.GetBrandNewCartridgesAvailability();

                _table = dt;
                _view = new DataView(_table);
                _currentPage = 1;
                DefaultListPageTemplate.DisableDefaultRowHighlight(dgvBrandNew, _layout.RefreshButton);
                ApplyFilter();

                // Initialize Sort By dropdown after first load
                if (!_sortByDropdownInitialized && _layout?.SortByComboBox != null && dgvBrandNew != null && dgvBrandNew.Columns.Count > 0)
                {
                    DefaultListPageTemplate.SetupSortByDropdown(
                        _layout.SortByComboBox,
                        dgvBrandNew,
                        (columnKey, direction) =>
                        {
                            var col = dgvBrandNew.Columns.Cast<DataGridViewColumn>()
                                .FirstOrDefault(c => c.DataPropertyName == columnKey || c.Name == columnKey);

                            if (col != null)
                            {
                                _sortColumn = col;
                                _sortOrder = direction;

                                if (_layout.FilterByComboBox != null && _layout.FilterByComboBox.SelectedIndex != 0)
                                {
                                    _layout.FilterByComboBox.SelectedIndex = 0;
                                }

                                ApplyFilter();
                            }
                        },
                        defaultColumnKey: "ItemName",
                        defaultDirection: System.Windows.Forms.SortOrder.Ascending
                    );

                    _sortByDropdownInitialized = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load Brand New cartridges: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvBrandNew_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = dgvBrandNew.Columns[e.ColumnIndex];
            if (col is DataGridViewCheckBoxColumn || col is DataGridViewButtonColumn || col is DataGridViewImageColumn)
                return;

            // Toggle sort order if same column, otherwise set to Ascending
            if (_sortColumn == col)
            {
                _sortOrder = _sortOrder == System.Windows.Forms.SortOrder.Ascending
                    ? System.Windows.Forms.SortOrder.Descending
                    : System.Windows.Forms.SortOrder.Ascending;
            }
            else
            {
                _sortColumn = col;
                _sortOrder = System.Windows.Forms.SortOrder.Ascending;
            }

            // Reset ALL columns to None (double arrows), then set active column
            foreach (DataGridViewColumn c in dgvBrandNew.Columns)
            {
                if (c?.HeaderCell != null)
                    c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            }
            _sortColumn.HeaderCell.SortGlyphDirection = _sortOrder;

            // Sync the Sort By dropdown with the column header sort
            if (_layout?.SortByComboBox != null && _sortColumn != null)
            {
                DefaultListPageTemplate.SyncSortByDropdown(_layout.SortByComboBox, _sortColumn, _sortOrder);
            }

            // Force immediate repaint of headers to show updated arrows
            dgvBrandNew.Invalidate();
            dgvBrandNew.Refresh();

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_view == null)
                return;

            var q = (_layout.SearchBox.Text ?? string.Empty).Trim().Replace("'", "''");
            if (string.IsNullOrWhiteSpace(q) || q == "Search by item name...")
            {
                _view.RowFilter = string.Empty;
            }
            else
            {
                _view.RowFilter = $"ItemName LIKE '%{q}%' OR CartridgeCategory LIKE '%{q}%'";
            }

            // Apply sorting
            if (_sortColumn != null && !string.IsNullOrEmpty(_sortColumn.DataPropertyName))
            {
                string sortDirection = _sortOrder == System.Windows.Forms.SortOrder.Ascending ? "ASC" : "DESC";
                _view.Sort = $"{_sortColumn.DataPropertyName} {sortDirection}";
            }
            else
            {
                _view.Sort = string.Empty;
            }

            _currentPage = 1;
            UpdatePagination();

            // Reapply sort glyphs after pagination (to persist after data rebinding)
            if (_sortColumn != null && _sortOrder != System.Windows.Forms.SortOrder.None)
            {
                foreach (DataGridViewColumn c in dgvBrandNew.Columns)
                {
                    if (c?.HeaderCell != null)
                        c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                }
                _sortColumn.HeaderCell.SortGlyphDirection = _sortOrder;
                dgvBrandNew.Invalidate();
            }
        }

        private void DgvBrandNew_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null) return;

            if (e.ColumnIndex < 0) return;

            var column = grid.Columns[e.ColumnIndex];
            if (column == null) return;

            // Header painting (sharp rectangles and always-visible arrows)
            if (e.RowIndex < 0)
            {
                e.Handled = true;

                // Fill background with white for border separation
                e.Graphics.FillRectangle(Brushes.White, e.CellBounds);

                // Fill header cell with header color (leaving 1px border)
                Rectangle r = new Rectangle(
                    e.CellBounds.X,
                    e.CellBounds.Y,
                    e.CellBounds.Width - 1,  // Leave 1px for right border
                    e.CellBounds.Height - 1  // Leave 1px for bottom border
                );

                using (var brush = new SolidBrush(Color.FromArgb(52, 152, 219)))
                {
                    e.Graphics.FillRectangle(brush, r);
                }

                // Draw white vertical border on the right edge
                using (var borderPen = new Pen(Color.White, 1))
                {
                    e.Graphics.DrawLine(borderPen,
                        e.CellBounds.Right - 1, e.CellBounds.Top,
                        e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                }

                // Draw white horizontal border on the bottom edge
                using (var borderPen = new Pen(Color.White, 1))
                {
                    e.Graphics.DrawLine(borderPen,
                        e.CellBounds.Left, e.CellBounds.Bottom - 1,
                        e.CellBounds.Right, e.CellBounds.Bottom - 1);
                }

                // Draw header text
                using (var textBrush = new SolidBrush(Color.White))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    // Adjust text rectangle to make room for sort glyph
                    var textRect = e.CellBounds;
                    
                    // Draw Sort Glyph on ALL sortable columns (always visible)
                    bool isSortable = column.SortMode == DataGridViewColumnSortMode.Programmatic;
                    
                    if (isSortable)
                    {
                        // Reduce text width to avoid overlap
                        textRect.Width -= 16;
                        
                        var glyphX = e.CellBounds.Right - 18;
                        var glyphY = e.CellBounds.Y + (e.CellBounds.Height - 8) / 2;
                        
                        // Determine arrow color - bright white for active sort, dimmed for inactive
                        Color arrowColor = column.HeaderCell.SortGlyphDirection != System.Windows.Forms.SortOrder.None 
                            ? Color.White 
                            : Color.FromArgb(180, 255, 255, 255); // Semi-transparent white
                        
                        // Draw arrow
                        using (var pen = new Pen(arrowColor, 2))
                        {
                            if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Ascending)
                            {
                                // Up arrow (active sort)
                                e.Graphics.DrawLine(pen, glyphX, glyphY + 6, glyphX + 5, glyphY);
                                e.Graphics.DrawLine(pen, glyphX + 5, glyphY, glyphX + 10, glyphY + 6);
                            }
                            else if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Descending)
                            {
                                // Down arrow (active sort)
                                e.Graphics.DrawLine(pen, glyphX, glyphY, glyphX + 5, glyphY + 6);
                                e.Graphics.DrawLine(pen, glyphX + 5, glyphY + 6, glyphX + 10, glyphY);
                            }
                            else
                            {
                                // Default: show both arrows (inactive state)
                                // Up arrow
                                e.Graphics.DrawLine(pen, glyphX + 2, glyphY + 2, glyphX + 5, glyphY - 1);
                                e.Graphics.DrawLine(pen, glyphX + 5, glyphY - 1, glyphX + 8, glyphY + 2);
                                // Down arrow
                                e.Graphics.DrawLine(pen, glyphX + 2, glyphY + 4, glyphX + 5, glyphY + 7);
                                e.Graphics.DrawLine(pen, glyphX + 5, glyphY + 7, glyphX + 8, glyphY + 4);
                            }
                        }
                    }

                    e.Graphics.DrawString(column.HeaderText, e.CellStyle.Font ?? grid.Font, textBrush, textRect, format);
                }

                return;
            }

            // Skip custom row painting for BrandNew page (keep default pill styling)
        }

        private void UpdatePagination()
        {
            if (_view == null)
                return;

            int totalItems = _view.Count;
            int totalPages = (int)Math.Ceiling((double)totalItems / _pageSize);
            if (totalPages < 1) totalPages = 1;
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1) _currentPage = 1;

            int skip = (_currentPage - 1) * _pageSize;
            var pageTable = _table.Clone();

            var rows = _view.Cast<DataRowView>()
                .Skip(skip)
                .Take(_pageSize);

            foreach (var r in rows)
            {
                pageTable.ImportRow(r.Row);
            }

            dgvBrandNew.DataSource = pageTable;
            ApplyGridFillSizing();

            if (lblPageInfo != null)
                lblPageInfo.Text = $"Page {_currentPage} of {totalPages} ({totalItems} items)";

            if (btnFirstPage != null) btnFirstPage.Enabled = _currentPage > 1;
            if (btnPrevPage != null) btnPrevPage.Enabled = _currentPage > 1;
            if (btnNextPage != null) btnNextPage.Enabled = _currentPage < totalPages;
            if (btnLastPage != null) btnLastPage.Enabled = _currentPage < totalPages;
        }

        private void BtnFirstPage_Click(object sender, System.EventArgs e)
        {
            _currentPage = 1;
            UpdatePagination();
        }

        private void BtnPrevPage_Click(object sender, System.EventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                UpdatePagination();
            }
        }

        private void BtnNextPage_Click(object sender, System.EventArgs e)
        {
            if (_view == null) return;

            int totalPages = (int)Math.Ceiling((double)_view.Count / _pageSize);
            if (_currentPage < totalPages)
            {
                _currentPage++;
                UpdatePagination();
            }
        }

        private void BtnLastPage_Click(object sender, System.EventArgs e)
        {
            if (_view == null) return;

            int totalPages = (int)Math.Ceiling((double)_view.Count / _pageSize);
            if (totalPages < 1) totalPages = 1;
            _currentPage = totalPages;
            UpdatePagination();
        }
    }
}
