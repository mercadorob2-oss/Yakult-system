using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using HopeButton = ReaLTaiizor.Controls.HopeButton;

namespace Yakult.Inventory.App.Pages.Admin.ReferenceData
{
    public partial class MasterDataUpdatePage : UserControl
    {
        // ── Layout & controls ────────────────────────────────────────────────
        private DefaultListPageLayout _layout;
        private DataGridView _dgv;
        private HopeButton _btnEdit, _btnSave, _btnCancel, _btnExport;
        private ComboBox _cboCategoryFilter, _cboItemTypeFilter, _cboActiveFilter;
        private Button _btnFirst, _btnPrev, _btnNext, _btnLast;
        private Label _lblPageInfo, _lblDirtyCount;

        // ── Data state ───────────────────────────────────────────────────────
        private List<ItemMasterDto> _all      = new List<ItemMasterDto>();
        private List<ItemMasterDto> _filtered  = new List<ItemMasterDto>();
        private Dictionary<int, ItemMasterDto> _originals = new Dictionary<int, ItemMasterDto>();
        private Dictionary<int, ItemMasterDto> _editSnapshot = new Dictionary<int, ItemMasterDto>();
        private HashSet<int> _dirtyIds = new HashSet<int>();
        private BindingList<ItemMasterDto> _displayList;
        private bool _isEditing;
        private DataGridViewCell _lastClickedCell;

        private readonly string _connectionString;
        private int _currentPage = 1;
        private int _preservedPage;
        private const int PageSize = 25;
        private string _sortCol;
        private bool _sortAsc = true;

        private static readonly string[] ValidItemTypes = { "Hardware", "Software/License", "Services" };

        public MasterDataUpdatePage()
        {
            _connectionString = DatabaseConfig.ConnectionString;
            InitializeComponent();
            BuildUI();
            LoadData();
        }

        private void InitializeComponent() { }

        // ── UI construction ──────────────────────────────────────────────────

        private void BuildUI()
        {
            SuspendLayout();
            Controls.Clear();
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            _layout = DefaultListPageTemplate.Create(
                "Master Data Update",
                "Search by Item ID, Name, Model Number, or Description...",
                (s, e) => ApplyFilters(),
                () => { if (!_isEditing || ConfirmDiscardEdits()) LoadData(); });

            DefaultListPageTemplate.HideSortDropdown(_layout);

            // Extra filter row appended below the standard header controls
            int filterY = _layout.HeaderPanel.Height - 2;
            _layout.HeaderPanel.Height += 44;

            _cboCategoryFilter = new ComboBox
            {
                Location = new Point(10, filterY),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            _cboCategoryFilter.Items.Add("All Categories");
            _cboCategoryFilter.SelectedIndex = 0;
            _cboCategoryFilter.SelectedIndexChanged += (s, e) => ApplyFilters();
            _layout.HeaderPanel.Controls.Add(_cboCategoryFilter);

            _cboItemTypeFilter = new ComboBox
            {
                Location = new Point(220, filterY),
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            _cboItemTypeFilter.Items.AddRange(new object[] { "All Types", "Hardware", "Software/License", "Services" });
            _cboItemTypeFilter.SelectedIndex = 0;
            _cboItemTypeFilter.SelectedIndexChanged += (s, e) => ApplyFilters();
            _layout.HeaderPanel.Controls.Add(_cboItemTypeFilter);

            _cboActiveFilter = new ComboBox
            {
                Location = new Point(410, filterY),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            _cboActiveFilter.Items.AddRange(new object[] { "All", "Active Only", "Inactive Only" });
            _cboActiveFilter.SelectedIndex = 0;
            _cboActiveFilter.SelectedIndexChanged += (s, e) => ApplyFilters();
            _layout.HeaderPanel.Controls.Add(_cboActiveFilter);

            _lblDirtyCount = new Label
            {
                Location = new Point(575, filterY + 5),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 100, 0)
            };
            _layout.HeaderPanel.Controls.Add(_lblDirtyCount);

            // ── Action buttons ─────────────────────────────────────────────
            _btnEdit = new HopeButton
            {
                Text = "Edit Records",
                Font = UiTheme.Fonts.Button,
                Size = new Size(155, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0),
                Enabled = false   // enabled once data loads
            };
            UiFactory.ConfigurePillHopeButton(_btnEdit, Color.FromArgb(0, 150, 136), Color.FromArgb(0, 120, 110));
            _btnEdit.Click += BtnEdit_Click;

            _btnSave = new HopeButton
            {
                Text = "Save Changes",
                Font = UiTheme.Fonts.Button,
                Size = new Size(160, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0),
                Visible = false
            };
            UiFactory.ConfigurePillHopeButton(_btnSave, UiTheme.Colors.Primary, UiTheme.Colors.PrimaryHover);
            _btnSave.Click += BtnSave_Click;

            _btnCancel = new HopeButton
            {
                Text = "Cancel",
                Font = UiTheme.Fonts.Button,
                Size = new Size(120, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0),
                Visible = false
            };
            UiFactory.ConfigurePillHopeButton(_btnCancel, Color.FromArgb(108, 117, 125), Color.FromArgb(86, 94, 100));
            _btnCancel.Click += BtnCancel_Click;

            _btnExport = new HopeButton
            {
                Text = "Export View",
                Font = UiTheme.Fonts.Button,
                Size = new Size(145, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigurePillHopeButton(_btnExport, Color.FromArgb(39, 174, 96), Color.FromArgb(30, 140, 76));
            _btnExport.Click += BtnExport_Click;

            DefaultListPageTemplate.AddButtons(_layout.ButtonLeftFlow, new[] { _btnEdit, _btnSave, _btnCancel, _btnExport });

            if (_layout.SummaryPanel != null)
                _layout.SummaryPanel.Visible = false;

            // ── DataGridView (read-only until Edit is clicked) ─────────────
            _dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells,
                EditMode = DataGridViewEditMode.EditOnKeystroke,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable
            };
            _dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            UiFactory.StyleGrid(_dgv);
            _dgv.ColumnHeaderMouseClick += Dgv_ColumnHeaderMouseClick;
            _dgv.CellPainting += Dgv_CellPainting;
            _dgv.RowPrePaint += Dgv_RowPrePaint;
            _dgv.CellValueChanged += Dgv_CellValueChanged;
            _dgv.CurrentCellDirtyStateChanged += Dgv_CurrentCellDirtyStateChanged;
            _dgv.DataError += (s, e) => e.Cancel = true;
            // Commit the active cell edit when focus leaves (e.g. clicking Save/Cancel)
            // so the button's Click fires on the first click rather than the second
            _dgv.Leave += (s, e) =>
            {
                if (_isEditing && _dgv.IsCurrentCellInEditMode)
                    _dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            // Track the exact cell the user clicked so Ctrl+C copies the right value
            _dgv.CellMouseClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                    _lastClickedCell = _dgv.Rows[e.RowIndex].Cells[e.ColumnIndex];
            };
            // Copy the clicked cell's value, not the full selected row
            _dgv.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.C)
                {
                    var cell = _lastClickedCell ?? _dgv.CurrentCell;
                    if (cell != null)
                    {
                        var val = cell.Value?.ToString() ?? "";
                        if (val.Length > 0) Clipboard.SetText(val);
                    }
                    e.Handled = true;
                }
            };

            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ItemId", DataPropertyName = "ItemId", Visible = false, ReadOnly = true
            });

            var cboType = new DataGridViewComboBoxColumn
            {
                Name = "ItemType",
                HeaderText = "Item Type",
                DataPropertyName = "ItemType",
                FillWeight = 35,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            };
            cboType.Items.AddRange(ValidItemTypes);
            _dgv.Columns.Add(cboType);

            AddTextCol("Name",         "Name",         "Name",         60);
            AddTextCol("Description",  "Description",  "Description",  80);
            AddTextCol("ModelNumber",  "ModelNumber",  "Model Number", 55);
            AddTextCol("SerialNumber", "SerialNumber", "Serial #",     35);
            AddTextCol("Category",     "Category",     "Category",     30);
            AddTextCol("Remarks",      "Remarks",      "Remarks",      50);

            _dgv.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "IsPhysicalAsset",
                HeaderText = "Physical Asset",
                DataPropertyName = "IsPhysicalAsset",
                FillWeight = 22,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });

            _dgv.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "Active",
                HeaderText = "Active",
                DataPropertyName = "Active",
                FillWeight = 18,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });

            if (_layout.GridCard != null)
            {
                _layout.GridCard.Controls.Clear();
                _layout.GridCard.Controls.Add(_dgv);
            }

            // ── Pagination controls ────────────────────────────────────────
            _btnFirst    = new Button { Text = "<<", Width = 45, Height = 25, Left = 0,   Top = 5 };
            _btnPrev     = new Button { Text = "<",  Width = 45, Height = 25, Left = 50,  Top = 5 };
            _lblPageInfo = new Label  { AutoSize = false, Width = 270, Left = 100, Top = 10, Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.MiddleLeft };
            _btnNext     = new Button { Text = ">",  Width = 45, Height = 25, Left = 378, Top = 5 };
            _btnLast     = new Button { Text = ">>", Width = 45, Height = 25, Left = 428, Top = 5 };

            _btnFirst.Click += (s, e) => { _currentPage = 1; UpdateGrid(); };
            _btnPrev.Click  += (s, e) => { if (_currentPage > 1) { _currentPage--; UpdateGrid(); } };
            _btnNext.Click  += (s, e) =>
            {
                int total = (int)Math.Ceiling((_filtered?.Count ?? 0) / (double)PageSize);
                if (_currentPage < total) { _currentPage++; UpdateGrid(); }
            };
            _btnLast.Click += (s, e) =>
            {
                _currentPage = (int)Math.Ceiling((_filtered?.Count ?? 0) / (double)PageSize);
                UpdateGrid();
            };

            if (_layout.PaginationPanel != null)
            {
                _layout.PaginationPanel.Controls.Clear();
                _layout.PaginationPanel.Controls.AddRange(new Control[] { _btnFirst, _btnPrev, _lblPageInfo, _btnNext, _btnLast });
            }

            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.SummaryPanel);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_layout.HeaderPanel);
            ResumeLayout(false);

            DefaultListPageTemplate.SetupInitialPageFocus(_layout.SearchBox, _dgv, _btnEdit, _btnSave, _btnCancel, _btnExport);
        }

        private void AddTextCol(string name, string prop, string header, float weight)
        {
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                DataPropertyName = prop,
                HeaderText = header,
                FillWeight = weight,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
        }

        // ── Edit-mode flow ───────────────────────────────────────────────────

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            // Snapshot all current values so Cancel can restore without a DB hit
            _editSnapshot.Clear();
            foreach (var dto in _all)
                _editSnapshot[dto.ItemId] = dto.Clone();

            _dgv.ReadOnly = false;
            _dgv.Columns["ItemId"].ReadOnly = true;
            _isEditing = true;

            _btnEdit.Visible   = false;
            _btnSave.Visible   = true;
            _btnCancel.Visible = true;
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            _dgv.CancelEdit();

            // Restore every DTO in _all from the snapshot (no DB round-trip)
            foreach (var dto in _all)
            {
                if (!_editSnapshot.TryGetValue(dto.ItemId, out var snap)) continue;
                dto.Name            = snap.Name;
                dto.Description     = snap.Description;
                dto.ModelNumber     = snap.ModelNumber;
                dto.Category        = snap.Category;
                dto.ItemType        = snap.ItemType;
                dto.SerialNumber    = snap.SerialNumber;
                dto.Remarks         = snap.Remarks;
                dto.Active          = snap.Active;
                dto.IsPhysicalAsset = snap.IsPhysicalAsset;
            }

            ExitEditMode();
            _preservedPage = _currentPage;
            ApplyFilters();
        }

        private void ExitEditMode()
        {
            _dgv.ReadOnly = true;
            _isEditing = false;
            _dirtyIds.Clear();
            UpdateDirtyLabel();

            _btnSave.Enabled   = true;
            _btnCancel.Enabled = true;
            _btnSave.Visible   = false;
            _btnCancel.Visible = false;
            _btnEdit.Visible   = true;
        }

        private bool ConfirmDiscardEdits()
        {
            if (_dirtyIds.Count == 0) return true;
            return MessageBox.Show(
                $"You have {_dirtyIds.Count} unsaved change{(_dirtyIds.Count == 1 ? "" : "s")}. Discard and reload?",
                "Discard Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        // ── Data loading ─────────────────────────────────────────────────────

        private async void LoadData()
        {
            try
            {
                _all = new List<ItemMasterDto>();
                _originals = new Dictionary<int, ItemMasterDto>();

                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    const string sql = @"
                        SELECT ItemId, Name, Description, ModelNumber, Category,
                               ItemType, SerialNumber, Remarks, Active, IsPhysicalAsset
                        FROM   dbo.Item
                        ORDER  BY Name";

                    using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 60 })
                    using (var rdr = await cmd.ExecuteReaderAsync())
                    {
                        while (await rdr.ReadAsync())
                        {
                            var dto = new ItemMasterDto
                            {
                                ItemId          = rdr.GetInt32(0),
                                Name            = rdr.GetString(1),
                                Description     = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                                ModelNumber     = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                                Category        = rdr.IsDBNull(4) ? "" : rdr.GetString(4),
                                ItemType        = rdr.GetString(5),
                                SerialNumber    = rdr.IsDBNull(6) ? "" : rdr.GetString(6),
                                Remarks         = rdr.IsDBNull(7) ? "" : rdr.GetString(7),
                                Active          = rdr.GetBoolean(8),
                                IsPhysicalAsset = rdr.GetBoolean(9)
                            };
                            _all.Add(dto);
                            _originals[dto.ItemId] = dto.Clone();
                        }
                    }
                }

                // Reset edit mode state on reload
                _isEditing = false;
                _dirtyIds.Clear();
                _dgv.ReadOnly = true;
                _btnEdit.Enabled = true;
                _btnSave.Visible   = false;
                _btnCancel.Visible = false;
                _btnEdit.Visible   = true;

                RefreshCategoryDropdown();
                UpdateDirtyLabel();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load items: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshCategoryDropdown()
        {
            var prev = _cboCategoryFilter?.SelectedItem?.ToString();
            _cboCategoryFilter.Items.Clear();
            _cboCategoryFilter.Items.Add("All Categories");
            _all.Select(i => i.Category ?? "")
                .Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList()
                .ForEach(c => _cboCategoryFilter.Items.Add(c));

            int idx = prev != null ? _cboCategoryFilter.Items.IndexOf(prev) : 0;
            _cboCategoryFilter.SelectedIndex = idx >= 0 ? idx : 0;
        }

        // ── Filtering & display ──────────────────────────────────────────────

        private void ApplyFilters()
        {
            if (_all == null) { _filtered = new List<ItemMasterDto>(); UpdateGrid(); return; }

            var q            = (_layout.SearchBox?.Text ?? "").Trim().ToLower();
            var catFilter    = _cboCategoryFilter?.SelectedItem?.ToString()  ?? "All Categories";
            var typeFilter   = _cboItemTypeFilter?.SelectedItem?.ToString()  ?? "All Types";
            var activeFilter = _cboActiveFilter?.SelectedItem?.ToString()    ?? "All";

            _filtered = _all.Where(i =>
            {
                if (q.Length > 0)
                {
                    bool match = i.Name.ToLower().Contains(q)
                        || (i.ModelNumber  ?? "").ToLower().Contains(q)
                        || (i.Description  ?? "").ToLower().Contains(q)
                        || i.ItemId.ToString().Contains(q);
                    if (!match) return false;
                }
                if (catFilter  != "All Categories" && !string.Equals(i.Category ?? "", catFilter, StringComparison.OrdinalIgnoreCase)) return false;
                if (typeFilter != "All Types"       && !string.Equals(i.ItemType,        typeFilter, StringComparison.OrdinalIgnoreCase)) return false;
                if (activeFilter == "Active Only"   && !i.Active) return false;
                if (activeFilter == "Inactive Only" &&  i.Active) return false;
                return true;
            }).ToList();

            _currentPage = _preservedPage > 0 ? _preservedPage : 1;
            _preservedPage = 0;
            UpdateGrid();
        }

        private void UpdateGrid()
        {
            if (_filtered == null) { _dgv.DataSource = null; return; }

            IEnumerable<ItemMasterDto> sorted = _filtered;
            switch (_sortCol)
            {
                case "Name":         sorted = _sortAsc ? sorted.OrderBy(i => i.Name         ?? "") : sorted.OrderByDescending(i => i.Name         ?? ""); break;
                case "ModelNumber":  sorted = _sortAsc ? sorted.OrderBy(i => i.ModelNumber   ?? "") : sorted.OrderByDescending(i => i.ModelNumber   ?? ""); break;
                case "Description":  sorted = _sortAsc ? sorted.OrderBy(i => i.Description   ?? "") : sorted.OrderByDescending(i => i.Description   ?? ""); break;
                case "Category":     sorted = _sortAsc ? sorted.OrderBy(i => i.Category      ?? "") : sorted.OrderByDescending(i => i.Category      ?? ""); break;
                case "ItemType":     sorted = _sortAsc ? sorted.OrderBy(i => i.ItemType)             : sorted.OrderByDescending(i => i.ItemType);            break;
                case "SerialNumber": sorted = _sortAsc ? sorted.OrderBy(i => i.SerialNumber  ?? "") : sorted.OrderByDescending(i => i.SerialNumber  ?? ""); break;
                case "Active":          sorted = _sortAsc ? sorted.OrderBy(i => i.Active)          : sorted.OrderByDescending(i => i.Active);          break;
                case "IsPhysicalAsset": sorted = _sortAsc ? sorted.OrderBy(i => i.IsPhysicalAsset) : sorted.OrderByDescending(i => i.IsPhysicalAsset); break;
            }

            int totalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
            if (_currentPage > totalPages) _currentPage = totalPages;

            var page = sorted.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();
            _displayList = new BindingList<ItemMasterDto>(page);
            _dgv.DataSource = _displayList;

            // Re-apply read-only state after rebind (DataSource reset clears column overrides)
            if (_isEditing)
                _dgv.Columns["ItemId"].ReadOnly = true;

            if (_lblPageInfo != null)
                _lblPageInfo.Text = $"Page {_currentPage} of {totalPages}  ({_filtered.Count} items)";

            _btnFirst.Enabled = _currentPage > 1;
            _btnPrev.Enabled  = _currentPage > 1;
            _btnNext.Enabled  = _currentPage < totalPages;
            _btnLast.Enabled  = _currentPage < totalPages;
        }

        // ── Dirty-state tracking ─────────────────────────────────────────────

        private void Dgv_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (!_isEditing) return;
            if (_dgv.CurrentCell is DataGridViewCheckBoxCell || _dgv.CurrentCell is DataGridViewComboBoxCell)
                _dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void Dgv_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (!_isEditing || e.RowIndex < 0 || _displayList == null || e.RowIndex >= _displayList.Count) return;
            MarkDirtyIfChanged(_displayList[e.RowIndex].ItemId);
        }

        private void MarkDirtyIfChanged(int itemId)
        {
            if (!_originals.TryGetValue(itemId, out var orig)) return;
            var curr = _all.FirstOrDefault(i => i.ItemId == itemId);
            if (curr == null) return;

            bool changed = curr.Name            != orig.Name
                        || curr.Description     != orig.Description
                        || curr.ModelNumber     != orig.ModelNumber
                        || curr.Category        != orig.Category
                        || curr.ItemType        != orig.ItemType
                        || curr.SerialNumber    != orig.SerialNumber
                        || curr.Remarks         != orig.Remarks
                        || curr.Active          != orig.Active
                        || curr.IsPhysicalAsset != orig.IsPhysicalAsset;

            if (changed) _dirtyIds.Add(itemId);
            else         _dirtyIds.Remove(itemId);

            UpdateDirtyLabel();
            int rowIdx = GetRowIndex(itemId);
            if (rowIdx >= 0) _dgv.InvalidateRow(rowIdx);
        }

        private int GetRowIndex(int itemId)
        {
            for (int r = 0; r < _dgv.Rows.Count; r++)
            {
                if (_dgv.Rows[r].Cells["ItemId"].Value is int id && id == itemId)
                    return r;
            }
            return -1;
        }

        private void UpdateDirtyLabel()
        {
            if (_lblDirtyCount == null) return;
            _lblDirtyCount.Text = _dirtyIds.Count == 0
                ? ""
                : $"{_dirtyIds.Count} unsaved change{(_dirtyIds.Count == 1 ? "" : "s")}";
        }

        private void Dgv_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || _displayList == null || e.RowIndex >= _displayList.Count) return;
            var dto = _displayList[e.RowIndex];
            var row = _dgv.Rows[e.RowIndex];
            if (_dirtyIds.Contains(dto.ItemId))
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 248, 220);
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 225, 130);
            }
            else
            {
                row.DefaultCellStyle.BackColor = e.RowIndex % 2 == 0 ? Color.White : Color.FromArgb(248, 249, 250);
                row.DefaultCellStyle.SelectionBackColor = UiTheme.Colors.GridSelectionBack;
            }
        }

        // ── Save ─────────────────────────────────────────────────────────────

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            if (_dirtyIds.Count == 0)
            {
                ExitEditMode();
                return;
            }

            _dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
            _dgv.EndEdit();

            var dirtyItems = _all.Where(i => _dirtyIds.Contains(i.ItemId)).ToList();

            foreach (var item in dirtyItems)
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    MessageBox.Show($"Item ID {item.ItemId}: Name cannot be blank.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(item.ItemType) || !ValidItemTypes.Contains(item.ItemType))
                {
                    MessageBox.Show(
                        $"Item ID {item.ItemId}: Item Type must be Hardware, Software/License, or Services.",
                        "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (item.IsPhysicalAsset && string.IsNullOrWhiteSpace(item.ModelNumber))
                {
                    MessageBox.Show(
                        $"Item ID {item.ItemId} \"{item.Name}\": Model Number is required for Physical Assets.",
                        "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            _btnSave.Enabled   = false;
            _btnCancel.Enabled = false;
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    using (var tx = con.BeginTransaction())
                    {
                        try
                        {
                            const string sql = @"
                                UPDATE dbo.Item
                                SET    Name            = @Name,
                                       Description     = @Description,
                                       ModelNumber     = @ModelNumber,
                                       Category        = @Category,
                                       ItemType        = @ItemType,
                                       SerialNumber    = @SerialNumber,
                                       Remarks         = @Remarks,
                                       Active          = @Active,
                                       IsPhysicalAsset = @IsPhysicalAsset,
                                       DateModified    = SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time',
                                       ModifiedBy      = @ModifiedBy
                                WHERE  ItemId = @ItemId";

                            foreach (var item in dirtyItems)
                            {
                                using (var cmd = new SqlCommand(sql, con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@Name",            item.Name.Trim());
                                    cmd.Parameters.AddWithValue("@Description",     NullOrTrim(item.Description));
                                    cmd.Parameters.AddWithValue("@ModelNumber",     NullOrTrim(item.ModelNumber));
                                    cmd.Parameters.AddWithValue("@Category",        NullOrTrim(item.Category));
                                    cmd.Parameters.AddWithValue("@ItemType",        item.ItemType);
                                    cmd.Parameters.AddWithValue("@SerialNumber",    NullOrTrim(item.SerialNumber));
                                    cmd.Parameters.AddWithValue("@Remarks",         NullOrTrim(item.Remarks));
                                    cmd.Parameters.AddWithValue("@Active",          item.Active);
                                    cmd.Parameters.AddWithValue("@IsPhysicalAsset", item.IsPhysicalAsset);
                                    cmd.Parameters.AddWithValue("@ModifiedBy",      AppSession.CurrentUserId);
                                    cmd.Parameters.AddWithValue("@ItemId",          item.ItemId);
                                    await cmd.ExecuteNonQueryAsync();
                                }

                                ItemAuditTrailWriter.TryLog(con, tx, new ItemAuditTrailDto
                                {
                                    ItemId = item.ItemId,
                                    SerialNumber = NullOrTrim(item.SerialNumber) as string,
                                    Action = "Item Bulk Updated",
                                    ActionTime = DateTime.Now,
                                    Status = "Completed",
                                    ReferenceType = "Item",
                                    ReferenceId = item.ItemId,
                                    Notes = "Item updated via master data bulk edit.",
                                    CreatedBy = AppSession.CurrentUserName ?? "System"
                                });
                            }
                            tx.Commit();
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }
                }

                int count = dirtyItems.Count;
                foreach (var item in dirtyItems)
                    _originals[item.ItemId] = item.Clone();

                ExitEditMode();
                _dgv.Invalidate();

                MessageBox.Show($"Saved {count} item{(count == 1 ? "" : "s")} successfully.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _btnSave.Enabled   = true;
                _btnCancel.Enabled = true;
                MessageBox.Show($"Failed to save: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static object NullOrTrim(string value) =>
            string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : (object)value.Trim();

        // ── Export ───────────────────────────────────────────────────────────

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (_filtered == null || _filtered.Count == 0)
            {
                MessageBox.Show("Nothing to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new SaveFileDialog
            {
                Filter   = "CSV files (*.csv)|*.csv",
                FileName = $"ItemMasterData_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                Title    = "Export Current View"
            })
            {
                if (dlg.ShowDialog(this.FindForm()) != DialogResult.OK) return;
                try
                {
                    using (var w = new StreamWriter(dlg.FileName, false, Encoding.UTF8))
                    {
                        w.WriteLine("ItemId,Name,ModelNumber,Description,Category,ItemType,SerialNumber,Remarks,IsPhysicalAsset,Active");
                        foreach (var i in _filtered)
                            w.WriteLine($"{i.ItemId},{Csv(i.Name)},{Csv(i.ModelNumber)},{Csv(i.Description)},{Csv(i.Category)},{Csv(i.ItemType)},{Csv(i.SerialNumber)},{Csv(i.Remarks)},{i.IsPhysicalAsset},{i.Active}");
                    }
                    MessageBox.Show($"Exported {_filtered.Count} rows.\n{dlg.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string Csv(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            return (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
                ? $"\"{v.Replace("\"", "\"\"")}\""
                : v;
        }

        // ── Sorting & column painting ────────────────────────────────────────

        private void Dgv_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = _dgv.Columns[e.ColumnIndex];
            if (col.SortMode != DataGridViewColumnSortMode.Programmatic) return;

            if (_sortCol == col.Name) _sortAsc = !_sortAsc;
            else { _sortCol = col.Name; _sortAsc = true; }

            foreach (DataGridViewColumn c in _dgv.Columns)
                c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            col.HeaderCell.SortGlyphDirection = _sortAsc
                ? System.Windows.Forms.SortOrder.Ascending
                : System.Windows.Forms.SortOrder.Descending;

            ApplyFilters();
        }

        private void Dgv_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 || e.ColumnIndex < 0) return;
            var col = _dgv.Columns[e.ColumnIndex];
            if (col == null) return;

            e.Handled = true;
            e.Graphics.FillRectangle(Brushes.White, e.CellBounds);
            using (var b = new SolidBrush(e.CellStyle.BackColor))
                e.Graphics.FillRectangle(b, new Rectangle(e.CellBounds.X, e.CellBounds.Y, e.CellBounds.Width - 1, e.CellBounds.Height - 1));
            using (var p = new Pen(Color.White, 1))
            {
                e.Graphics.DrawLine(p, e.CellBounds.Right - 1, e.CellBounds.Top,        e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                e.Graphics.DrawLine(p, e.CellBounds.Left,      e.CellBounds.Bottom - 1, e.CellBounds.Right,     e.CellBounds.Bottom - 1);
            }

            using (var tb  = new SolidBrush(Color.White))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var textRect = e.CellBounds;
                if (col.SortMode == DataGridViewColumnSortMode.Programmatic)
                {
                    textRect.Width -= 20;
                    int gx = e.CellBounds.Right - 18;
                    int gy = e.CellBounds.Y + (e.CellBounds.Height - 8) / 2;
                    bool hasSortDir = col.HeaderCell.SortGlyphDirection != System.Windows.Forms.SortOrder.None;
                    Color ac = hasSortDir ? Color.White : Color.FromArgb(160, 255, 255, 255);
                    using (var ap = new Pen(ac, 2))
                    {
                        if (col.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Ascending)
                        {
                            e.Graphics.DrawLine(ap, gx,     gy + 6, gx + 5,  gy);
                            e.Graphics.DrawLine(ap, gx + 5, gy,     gx + 10, gy + 6);
                        }
                        else if (col.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Descending)
                        {
                            e.Graphics.DrawLine(ap, gx,     gy,     gx + 5,  gy + 6);
                            e.Graphics.DrawLine(ap, gx + 5, gy + 6, gx + 10, gy);
                        }
                        else
                        {
                            e.Graphics.DrawLine(ap, gx + 2, gy + 2, gx + 5, gy - 1);
                            e.Graphics.DrawLine(ap, gx + 5, gy - 1, gx + 8, gy + 2);
                            e.Graphics.DrawLine(ap, gx + 2, gy + 4, gx + 5, gy + 7);
                            e.Graphics.DrawLine(ap, gx + 5, gy + 7, gx + 8, gy + 4);
                        }
                    }
                }
                e.Graphics.DrawString(col.HeaderText, e.CellStyle.Font ?? _dgv.Font, tb, textRect, fmt);
            }
        }
    }

    // ── DTO ──────────────────────────────────────────────────────────────────

    internal class ItemMasterDto
    {
        public int    ItemId          { get; set; }
        public string Name            { get; set; }
        public string Description     { get; set; }
        public string ModelNumber     { get; set; }
        public string Category        { get; set; }
        public string ItemType        { get; set; }
        public string SerialNumber    { get; set; }
        public string Remarks         { get; set; }
        public bool   Active          { get; set; }
        public bool   IsPhysicalAsset { get; set; }

        public ItemMasterDto Clone() => new ItemMasterDto
        {
            ItemId          = ItemId,
            Name            = Name,
            Description     = Description,
            ModelNumber     = ModelNumber,
            Category        = Category,
            ItemType        = ItemType,
            SerialNumber    = SerialNumber,
            Remarks         = Remarks,
            Active          = Active,
            IsPhysicalAsset = IsPhysicalAsset
        };
    }
}
