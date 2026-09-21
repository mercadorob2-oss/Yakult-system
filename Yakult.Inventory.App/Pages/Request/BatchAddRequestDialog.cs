using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages.Employee;

namespace Yakult.Inventory.App.Pages.Request
{
    // Single-file form (no .Designer needed). If you already have a Designer file,
    // either delete it or convert this class to "public partial class BatchAddRequestDialog : Form".
    public partial class BatchAddRequestDialog : Form
    {
        private DataGridView dgv;
        private Button btnAddRow, btnRemoveRow, btnSave, btnCancel, btnAddEmployee;
        private Label lblInfo, lblEmployee;
        private ComboBox cmbEmployee;
        private readonly List<CategoryItem> _categories = new List<CategoryItem>();
        private readonly List<ItemItem> _allItems = new List<ItemItem>();
        private readonly List<EmployeeItem> _employees = new List<EmployeeItem>();

        // "No Employee" mode — org-unit dropdowns
        private CheckBox _chkNoEmployee;
        private Panel _empRowPanel;
        private Panel _orgUnitPanel;
        private ComboBox _cmbCompany, _cmbDepartment, _cmbBranch, _cmbDistributor;
        private readonly List<OrgItem> _companies = new List<OrgItem>();
        private readonly List<OrgItem> _departments = new List<OrgItem>();
        private readonly List<OrgItem> _branches = new List<OrgItem>();
        private readonly List<OrgItem> _distributors = new List<OrgItem>();

        // Autocomplete support
        private ListBox _autocompleteList;
        private string _lastAutocompleteColumn;
        private bool _isAutocompleteActive;
        private bool _suppressCellEndEdit;
        private int _autocompleteRowIndex = -1; // Track which row autocomplete is for

        // Select All checkbox support
        private CheckBox _selectAllCheckBox;
        private bool _isSelectAllCheckBoxUpdating = false;

        // Employee search popup support
        private ListBox _employeeSearchList;
        private bool _suppressEmployeeFilter = false;

        // Tooltip for grid hints
        private ToolTip _gridToolTip;

        // Details panel – bound to the currently selected DataGridView row
        private TextBox _txtDescription;
        private TextBox _txtRemarks;
        private bool _suppressDetailsPanelSync;

        // Validation error highlight color
        private static readonly Color InvalidCellColor = Color.FromArgb(255, 230, 230);
        private static readonly Color DisabledCellColor = Color.FromArgb(235, 235, 235);

        public BatchAddRequestDialog()
        {
            Text = "Batch Add Requests";
            // Make dialog bigger to accommodate long model numbers and serial numbers
            Width = 1900;
            Height = 850;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            BackColor = Color.FromArgb(245, 246, 250);
            Font = new Font("Segoe UI", 9F);

            _gridToolTip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 400,
                ReshowDelay = 200,
                ShowAlways = true
            };

            InitializeComponents();
            LoadLookups();
            SetupGrid();
            SetupEmployeeSearchPopup();
            AddRow(); // start with 1 row

            // Start with Remove Row disabled (no rows checked)
            btnRemoveRow.Enabled = false;

            // Hook into Shown event to ensure positioning after form is fully loaded
            this.Shown += (s, e) => PositionSelectAllCheckBox();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // When autocomplete popup is open, route navigation keys to it before
            // anything else — otherwise the DataGridView consumes Up/Down first.
            if (_isAutocompleteActive && _autocompleteList != null && _autocompleteList.Visible)
            {
                switch (keyData)
                {
                    case Keys.Down:
                        if (_autocompleteList.Items.Count > 0)
                            _autocompleteList.SelectedIndex = Math.Min(
                                _autocompleteList.SelectedIndex + 1,
                                _autocompleteList.Items.Count - 1);
                        if (_autocompleteList.SelectedIndex < 0 && _autocompleteList.Items.Count > 0)
                            _autocompleteList.SelectedIndex = 0;
                        return true;

                    case Keys.Up:
                        if (_autocompleteList.Items.Count > 0 && _autocompleteList.SelectedIndex > 0)
                            _autocompleteList.SelectedIndex--;
                        return true;

                    case Keys.Enter:
                        if (_autocompleteList.SelectedItem != null)
                            ApplyAutocompleteSuggestion(_autocompleteList.SelectedItem.ToString());
                        return true;

                    case Keys.Escape:
                        HideAutocomplete();
                        return true;
                }
            }

            // Handle keyboard navigation for DataGridView
            if (dgv.Focused || (dgv.EditingControl != null && dgv.EditingControl.Focused))
            {
                var currentCell = dgv.CurrentCell;
                if (currentCell == null) return base.ProcessCmdKey(ref msg, keyData);

                var column = dgv.Columns[currentCell.ColumnIndex];

                // Handle Enter key
                if (keyData == Keys.Enter)
                {
                    // For checkbox column - toggle the checkbox
                    // CRITICAL: Check RowIndex >= 0 to distinguish row checkbox from header
                    if (column.Name == "colSelect" && currentCell.RowIndex >= 0)
                    {
                        // Toggle the row checkbox ONLY - suppress all header logic
                        bool newValue;
                        if (currentCell.Value is bool currentValue)
                        {
                            newValue = !currentValue;
                        }
                        else
                        {
                            // Handle null/uninitialized checkbox
                            newValue = true;
                        }

                        // Set the value and force immediate visual update
                        currentCell.Value = newValue;

                        // Commit edit immediately to persist the change
                        dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);

                        // End edit to force visual update and exit edit mode
                        dgv.EndEdit();

                        // Notify that the current cell is no longer dirty
                        dgv.NotifyCurrentCellDirty(false);

                        // Force immediate refresh of the cell and row
                        dgv.InvalidateCell(currentCell);
                        dgv.InvalidateRow(currentCell.RowIndex);
                        dgv.Refresh();

                        // Update select-all checkbox state
                        UpdateSelectAllCheckBoxState();

                        return true; // Handled - prevent any further processing
                    }

                    // For ComboBox columns - open dropdown or commit selection
                    if (column is DataGridViewComboBoxColumn)
                    {
                        if (!currentCell.IsInEditMode)
                        {
                            // Enter edit mode and open dropdown
                            dgv.BeginEdit(true);
                            if (dgv.EditingControl is ComboBox combo)
                            {
                                combo.DroppedDown = true;
                            }
                            return true; // Handled
                        }
                        else if (dgv.EditingControl is ComboBox combo && combo.DroppedDown)
                        {
                            // Close dropdown and commit selection, then advance
                            combo.DroppedDown = false;
                            dgv.EndEdit();
                            MoveToNextEditableCell(currentCell.RowIndex, currentCell.ColumnIndex);
                            return true; // Handled
                        }
                        else
                        {
                            // ComboBox in edit mode but dropdown closed — commit and advance
                            dgv.EndEdit();
                            MoveToNextEditableCell(currentCell.RowIndex, currentCell.ColumnIndex);
                            return true;
                        }
                    }

                    // For text cells — commit and advance to next editable cell
                    if (column is DataGridViewTextBoxColumn && !column.ReadOnly)
                    {
                        dgv.EndEdit();
                        MoveToNextEditableCell(currentCell.RowIndex, currentCell.ColumnIndex);
                        return true;
                    }
                }

                // Handle Escape key - close dropdown or exit edit mode
                if (keyData == Keys.Escape)
                {
                    if (dgv.EditingControl is ComboBox combo && combo.DroppedDown)
                    {
                        combo.DroppedDown = false;
                        return true; // Handled
                    }
                }

                // Prevent arrow keys from moving grid focus when ComboBox dropdown is open
                if ((keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Left || keyData == Keys.Right) &&
                    dgv.EditingControl is ComboBox comboBox && comboBox.DroppedDown)
                {
                    // Let the ComboBox handle arrow keys, don't move grid selection
                    return false; // Let ComboBox handle it
                }
            }

            // Handle Enter/Space on select-all checkbox when it has focus
            if (_selectAllCheckBox != null && _selectAllCheckBox.Focused &&
                (keyData == Keys.Enter || keyData == Keys.Space))
            {
                // Toggle select-all checkbox - this will trigger SelectAllCheckBox_CheckedChanged
                _selectAllCheckBox.Checked = !_selectAllCheckBox.Checked;
                return true; // Handled
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Logical tab order for keyboard-first data entry (column names in order)
        private static readonly string[] _tabOrder = new[]
        {
            "colCategory", "colItem", "colSerialNumber", "colModelNumber",
            "colQuantity", "colDateRequested", "colStatus"
        };

        private void MoveToNextEditableCell(int rowIndex, int currentColIndex)
        {
            string currentColName = dgv.Columns[currentColIndex].Name;

            // Find current position in tab order
            int tabPos = Array.IndexOf(_tabOrder, currentColName);

            if (tabPos >= 0 && tabPos < _tabOrder.Length - 1)
            {
                // Move to next column in tab order on the same row
                string nextColName = _tabOrder[tabPos + 1];
                int nextColIdx = dgv.Columns[nextColName].Index;

                // Skip Item cell if it's still on placeholder (no category selected)
                if (nextColName == "colItem")
                {
                    var row = dgv.Rows[rowIndex];
                    var itemCell = row.Cells["colItem"] as DataGridViewComboBoxCell;
                    var ds = itemCell?.DataSource as List<ItemItem>;
                    // If only placeholder exists, skip to next
                    if (ds != null && ds.Count <= 1 && ds[0]?.Id == null)
                    {
                        nextColName = _tabOrder[tabPos + 2 < _tabOrder.Length ? tabPos + 2 : _tabOrder.Length - 1];
                        nextColIdx = dgv.Columns[nextColName].Index;
                    }
                }

                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            dgv.CurrentCell = dgv.Rows[rowIndex].Cells[nextColIdx];
                            dgv.BeginEdit(true);
                        }
                        catch { }
                    }));
                }
            }
            else if (tabPos == _tabOrder.Length - 1)
            {
                // Last editable column — add a new row and focus its Category cell
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            AddRow();
                            int newRowIdx = dgv.Rows.Count - 1;
                            int catColIdx = dgv.Columns["colCategory"].Index;
                            dgv.CurrentCell = dgv.Rows[newRowIdx].Cells[catColIdx];
                            dgv.BeginEdit(true);
                            if (dgv.EditingControl is ComboBox catCombo)
                            {
                                catCombo.DroppedDown = true;
                            }
                        }
                        catch { }
                    }));
                }
            }
        }

        private void InitializeComponents()
        {
            lblInfo = new Label
            {
                Text = "Batch add requests. Select an employee for this batch, then fill each row and click Save.",
                AutoSize = false,
                Height = 36,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(60, 60, 60),
                Padding = new Padding(10, 8, 10, 8),
                BackColor = Color.FromArgb(230, 240, 255),
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8)
            };

            lblEmployee = new Label
            {
                Text = "Employee:",
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(60, 60, 60),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                Margin = new Padding(0, 6, 8, 0)
            };

            cmbEmployee = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.None,
                Width = 400,
                Margin = new Padding(0, 0, 8, 0)
            };
            cmbEmployee.TextChanged += CmbEmployee_TextChanged;
            cmbEmployee.KeyDown += CmbEmployee_KeyDown;
            cmbEmployee.PreviewKeyDown += CmbEmployee_PreviewKeyDown;

            dgv = new DataGridView
            {
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            btnAddRow = new Button
            {
                Text = "+ Add Row",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 36),
                Padding = new Padding(14, 0, 14, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(52, 152, 219),
                Cursor = Cursors.Hand
            };
            btnAddRow.FlatAppearance.BorderColor = Color.FromArgb(52, 152, 219);
            btnAddRow.FlatAppearance.BorderSize = 1;

            btnRemoveRow = new Button
            {
                Text = "Remove Row",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 36),
                Padding = new Padding(14, 0, 14, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(192, 57, 43),
                Cursor = Cursors.Hand
            };
            btnRemoveRow.FlatAppearance.BorderColor = Color.FromArgb(192, 57, 43);
            btnRemoveRow.FlatAppearance.BorderSize = 1;

            btnAddEmployee = new Button
            {
                Text = "+ Employee",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 36),
                Padding = new Padding(12, 0, 12, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(52, 152, 219),
                Cursor = Cursors.Hand
            };
            btnAddEmployee.FlatAppearance.BorderColor = Color.FromArgb(52, 152, 219);
            btnAddEmployee.FlatAppearance.BorderSize = 1;

            btnSave = new Button
            {
                Text = "Save",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 36),
                Padding = new Padding(18, 0, 18, 0),
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderColor = Color.FromArgb(41, 128, 185);
            btnSave.FlatAppearance.BorderSize = 1;

            btnCancel = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 36),
                Padding = new Padding(18, 0, 18, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(80, 80, 80),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            btnCancel.FlatAppearance.BorderSize = 1;

            btnAddRow.Click += (s, e) => AddRow();
            btnRemoveRow.Click += BtnRemoveRow_Click;
            btnSave.Click += (s, e) => SaveBatch();
            btnCancel.Click += (s, e) => Close();
            btnAddEmployee.Click += BtnAddEmployee_Click;

            Controls.Clear();

            // ── Root TableLayoutPanel: info banner / employee bar / grid / bottom ──
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(16),
                BackColor = BackColor
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // 0 – info banner
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // 1 – employee bar
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // 2 – DataGridView (fills)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // 3 – details + buttons

            // Row 0: info banner
            root.Controls.Add(lblInfo, 0, 0);

            // Row 1: selector section (employee row + org-unit row, stacked vertically)
            var selectorSection = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 4, 0, 8)
            };
            selectorSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            selectorSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            selectorSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // ── "No Employee" checkbox ────────────────────────────────────────
            _chkNoEmployee = new CheckBox
            {
                Text = "No Employee (Dept. Level)",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(60, 60, 60),
                Margin = new Padding(16, 0, 0, 0),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            _chkNoEmployee.CheckedChanged += ChkNoEmployee_CheckedChanged;

            // ── Employee row  [Employee:] [cmbEmployee 400px] [+Employee] [☐ No Employee] ──
            _empRowPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            lblEmployee.AutoSize = true;
            lblEmployee.TextAlign = ContentAlignment.MiddleLeft;
            lblEmployee.Margin = new Padding(0, 6, 8, 0);
            cmbEmployee.Width = 400;
            cmbEmployee.Margin = new Padding(0, 4, 8, 0);
            btnAddEmployee.Margin = new Padding(0, 4, 16, 0);
            _chkNoEmployee.Margin = new Padding(0, 7, 0, 0);
            _empRowPanel.Controls.Add(lblEmployee);
            _empRowPanel.Controls.Add(cmbEmployee);
            _empRowPanel.Controls.Add(btnAddEmployee);
            _empRowPanel.Controls.Add(_chkNoEmployee);
            selectorSection.Controls.Add(_empRowPanel, 0, 0);

            // ── Org-unit row  [Company:] [cmbCompany] [Department:] [cmbDept] [Branch:] [cmbBranch] ──
            _cmbCompany = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                Width = 220,
                Margin = new Padding(0, 0, 12, 0)
            };
            _cmbCompany.SelectedIndexChanged += CmbCompany_SelectedIndexChanged;

            _cmbDepartment = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                Width = 220,
                Margin = new Padding(0, 0, 12, 0)
            };

            _cmbBranch = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                Width = 220,
                Margin = new Padding(0, 0, 12, 0)
            };

            // Independent sales distributor (dbo.Distributor) — not under any
            // Company/Department/Branch. Shown for dept-level batches only.
            _cmbDistributor = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                Width = 220,
                Margin = new Padding(0, 0, 0, 0)
            };

            Label MakeOrgLabel(string text) => new Label
            {
                Text = text,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(60, 60, 60),
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 0, 6, 0),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };

            var orgRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 4, 0, 0)
            };
            orgRow.Controls.Add(MakeOrgLabel("Company:"));
            orgRow.Controls.Add(_cmbCompany);
            orgRow.Controls.Add(MakeOrgLabel("Department:"));
            orgRow.Controls.Add(_cmbDepartment);
            orgRow.Controls.Add(MakeOrgLabel("Branch:"));
            orgRow.Controls.Add(_cmbBranch);
            orgRow.Controls.Add(MakeOrgLabel("Distributor:"));
            orgRow.Controls.Add(_cmbDistributor);

            _orgUnitPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Visible = false,
                Padding = new Padding(0)
            };
            _orgUnitPanel.Controls.Add(orgRow);
            selectorSection.Controls.Add(_orgUnitPanel, 0, 1);

            root.Controls.Add(selectorSection, 0, 1);

            // Row 2: DataGridView
            dgv.Margin = new Padding(0);
            root.Controls.Add(dgv, 0, 2);

            // Row 3: bottom section (details panel + button bar, stacked)
            var bottomSection = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0)
            };
            bottomSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottomSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // details panel
            bottomSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // button bar

            bottomSection.Controls.Add(BuildDetailsPanel(), 0, 0);

            // Button bar: left = Add/Remove, right = Save/Cancel
            var leftBar = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            btnAddRow.Margin = new Padding(0, 0, 8, 0);
            btnRemoveRow.Margin = new Padding(0);
            leftBar.Controls.Add(btnAddRow);
            leftBar.Controls.Add(btnRemoveRow);

            var rightBar = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            btnCancel.Margin = new Padding(0);
            btnSave.Margin = new Padding(8, 0, 0, 0);
            rightBar.Controls.Add(btnCancel);
            rightBar.Controls.Add(btnSave);

            var buttonBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 8, 0, 0)
            };
            buttonBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttonBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttonBar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            buttonBar.Controls.Add(leftBar, 0, 0);
            buttonBar.Controls.Add(rightBar, 1, 0);

            bottomSection.Controls.Add(buttonBar, 0, 1);
            root.Controls.Add(bottomSection, 0, 3);

            Controls.Add(root);

            // Tooltips
            _gridToolTip.SetToolTip(btnRemoveRow, "Remove selected rows (check rows first)");
            _gridToolTip.SetToolTip(btnAddRow, "Add a new empty request row");
            _gridToolTip.SetToolTip(cmbEmployee, "Type to search by name, employee number, or position");

            // Grid events (identical to original)
            dgv.CellValueChanged += Dgv_CellValueChanged;
            dgv.CellEndEdit += Dgv_CellEndEdit;
            dgv.CurrentCellDirtyStateChanged += (s, e) => { if (dgv.IsCurrentCellDirty) dgv.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            dgv.DataError += (s, e) => { e.ThrowException = false; };
            dgv.EditingControlShowing += Dgv_EditingControlShowing;
            dgv.CellEnter += Dgv_CellEnter;
            dgv.CellContentClick += Dgv_CellContentClick;
            dgv.CellClick += Dgv_CellClick;
            dgv.ColumnHeaderMouseClick += Dgv_ColumnHeaderMouseClick;
            dgv.CellPainting += Dgv_CellPainting;
            // Bind details panel whenever the selected row changes
            dgv.SelectionChanged += (s, e) => BindDetailsPanel(dgv.CurrentRow);

            CancelButton = btnCancel;
        }

        private void SetupGrid()
        {
            dgv.Columns.Clear();

            // Checkbox column for row selection (left-most)
            var colSelect = new DataGridViewCheckBoxColumn
            {
                Name = "colSelect",
                HeaderText = "",
                Width = 28,
                ReadOnly = false,
                FalseValue = false,
                TrueValue = true
            };
            dgv.Columns.Add(colSelect);

            // Category
            var colCategory = new DataGridViewComboBoxColumn
            {
                Name = "colCategory",
                HeaderText = "Category *",
                DisplayMember = "Name",
                ValueMember = null,
                ValueType = typeof(object),
                Width = 150,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            dgv.Columns.Add(colCategory);

            // Item (per-row combo cell assigned)
            var colItem = new DataGridViewComboBoxColumn
            {
                Name = "colItem",
                HeaderText = "Item *",
                DisplayMember = "DisplayName",
                ValueMember = null,
                ValueType = typeof(object),
                Width = 500,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            dgv.Columns.Add(colItem);

            // Serial Number
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colSerialNumber",
                HeaderText = "Serial Number",
                ReadOnly = false,
                Width = 250, // Increased width
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            // Model Number
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colModelNumber",
                HeaderText = "Model Number",
                ReadOnly = false,
                Width = 250, // Increased width
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            // Quantity (editable)
            dgv.Columns.Add(new DataGridViewTextBoxColumn 
            { 
                Name = "colQuantity",
                HeaderText = "Qty *",
                ValueType = typeof(int),
                Width = 45,
                ReadOnly = false,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            // UnitPrice (read-only - auto-filled from item)
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnitPrice", HeaderText = "Unit Price", ValueType = typeof(decimal), Width = 100, ReadOnly = true });

            // DateRequested (calendar cell)
            dgv.Columns.Add(new CalendarColumn { Name = "colDateRequested", HeaderText = "Date Requested", Width = 120 });

            // Description (read-only - auto-filled from item)
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDescription", HeaderText = "Description", Width = 200, ReadOnly = true });

            // Fixed Asset (read-only - auto-filled from item)
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFixedAsset", HeaderText = "Fixed Asset", Width = 90, ReadOnly = true });

            // Remarks (editable)
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRemarks", HeaderText = "Remarks", Width = 250, ReadOnly = false, SortMode = DataGridViewColumnSortMode.NotSortable });

            // Hide long-text columns — values are edited via the details panel below the grid
            dgv.Columns["colDescription"].Visible = false;
            dgv.Columns["colRemarks"].Visible = false;

            // Status
            var colStatus = new DataGridViewComboBoxColumn
            {
                Name = "colStatus",
                HeaderText = "Status",
                DataSource = new string[] { "Under Review", "Submitted", "On Hold" },
                ValueType = typeof(string),
                Width = 110,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            dgv.Columns.Add(colStatus);

            // Allow user to resize columns
            dgv.AllowUserToResizeColumns = true;

            // Bind lookups to column-level where safe (category)
            var categoryColumn = (DataGridViewComboBoxColumn)dgv.Columns["colCategory"];
            categoryColumn.DataSource = _categories;
            categoryColumn.DisplayMember = "Name";

            // ── Visual styling (ViewRequestPage-style) ──
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgv.ColumnHeadersHeight = 40;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 152, 219);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.RowTemplate.Height = 52;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(41, 128, 185);
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv.DefaultCellStyle.Padding = new Padding(8, 6, 8, 6);
            dgv.GridColor = Color.White;
            dgv.BackgroundColor = Color.White;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.None;

            // Add Select All checkbox to the header
            AddSelectAllCheckBox();
        }

        private void AddSelectAllCheckBox()
        {
            // Create the Select All checkbox (binary only - no indeterminate)
            _selectAllCheckBox = new CheckBox
            {
                Size = new Size(15, 15),
                BackColor = Color.Transparent,
                ThreeState = false,
                Checked = false
            };

            _selectAllCheckBox.CheckedChanged += SelectAllCheckBox_CheckedChanged;

            // Position the checkbox in the header of colSelect
            if (dgv.Controls.Contains(_selectAllCheckBox))
            {
                dgv.Controls.Remove(_selectAllCheckBox);
            }

            dgv.Controls.Add(_selectAllCheckBox);
            _selectAllCheckBox.BringToFront();

            // Attach event handlers for repositioning
            dgv.Layout += (s, e) => PositionSelectAllCheckBox();
            dgv.ColumnWidthChanged += (s, e) => PositionSelectAllCheckBox();
            dgv.Scroll += (s, e) => PositionSelectAllCheckBox();

            // Initial positioning — always defer via dgv.BeginInvoke so the form
            // handle doesn't need to exist yet (this is called from the constructor).
            if (dgv.IsHandleCreated)
            {
                dgv.BeginInvoke(new Action(() => PositionSelectAllCheckBox()));
            }
            else
            {
                // Defer positioning until dgv handle is created
                EventHandler handleCreated = null;
                handleCreated = (s, e) =>
                {
                    PositionSelectAllCheckBox();
                    dgv.HandleCreated -= handleCreated;
                };
                dgv.HandleCreated += handleCreated;
            }
        }

        private void PositionSelectAllCheckBox()
        {
            if (_selectAllCheckBox == null || dgv == null || !dgv.IsHandleCreated)
                return;

            try
            {
                // Get the header cell bounds for the first column (checkbox column at index 0)
                var rect = dgv.GetCellDisplayRectangle(0, -1, true);

                // Only position if we got valid bounds
                if (rect.Width > 0 && rect.Height > 0)
                {
                    // Center the checkbox in the header cell
                    int x = rect.Left + (rect.Width - _selectAllCheckBox.Width) / 2;
                    int y = rect.Top + (rect.Height - _selectAllCheckBox.Height) / 2;

                    _selectAllCheckBox.Location = new Point(x, y);
                    _selectAllCheckBox.Visible = true;
                }
            }
            catch
            {
                // Ignore positioning errors
            }
        }

        private void LoadLookups()
        {
            _categories.Clear();
            _employees.Clear();
            _allItems.Clear();

            // Use the refactored loaders
            LoadEmployees();
            LoadCategories();
            LoadItems();
            LoadOrgUnits();
        }

        private void LoadEmployees()
        {
            _employees.Clear();
            // placeholder
            _employees.Add(new EmployeeItem { Id = null, Name = "-- Select Employee --", Position = string.Empty });

            var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(cs))
            {
                MessageBox.Show("Connection string not configured.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                using (var con = new SqlConnection(cs))
                {
                    con.Open();
                    using (var cmd = new SqlCommand("SELECT EmpId, Name, EmployeeNumber, Position FROM dbo.Employee WHERE Active = 1 ORDER BY Name", con))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            _employees.Add(new EmployeeItem
                            {
                                Id = r.IsDBNull(0) ? (int?)null : r.GetInt32(0),
                                Name = r.IsDBNull(1) ? string.Empty : r.GetString(1),
                                EmployeeNumber = r.IsDBNull(2) ? null : r.GetString(2),
                                Position = r.IsDBNull(3) ? string.Empty : r.GetString(3)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load employees: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Bind to top-level combo and apply to existing rows
            if (cmbEmployee != null)
            {
                cmbEmployee.DataSource = null;
                cmbEmployee.DataSource = _employees;
                cmbEmployee.DisplayMember = "DisplayText";
                cmbEmployee.SelectedIndex = 0;
            }

            ApplySelectedEmployeeToAllRows();
        }

        private void LoadCategories()
        {
            _categories.Clear();

            var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(cs))
            {
                MessageBox.Show("Connection string not configured.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                using (var con = new SqlConnection(cs))
                {
                    con.Open();

                    using (var cmd = new SqlCommand("SELECT CategoryId, Name FROM dbo.ItemCategory WHERE Active = 1 ORDER BY Name", con))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            _categories.Add(new CategoryItem { CategoryId = r.GetInt32(0), Name = r.GetString(1) });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load categories: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // refresh column datasource if grid exists
            if (dgv != null && dgv.Columns.Contains("colCategory"))
            {
                var col = dgv.Columns["colCategory"] as DataGridViewComboBoxColumn;
                if (col != null)
                {
                    col.DataSource = null;
                    col.DataSource = _categories;
                    col.DisplayMember = "Name";
                }
            }
        }

        private void LoadItems()
        {
            _allItems.Clear();

            var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(cs))
            {
                MessageBox.Show("Connection string not configured.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                using (var con = new SqlConnection(cs))
                {
                    con.Open();

                    // An item is unavailable if it has ANY request (regardless of inventory entries)
                    // Also load SerialNumber so we can auto-fill it in the grid.
                    const string sqlItems = @"
                        SELECT 
                            i.ItemId, 
                            i.Name, 
                            i.StockOnHand, 
                            i.CategoryId,
                            i.Category,
                            i.SerialNumber,
                            i.ModelNumber,
                            i.Amount,
                            CASE
                                WHEN EXISTS (
                                    SELECT 1 FROM dbo.Request r
                                    WHERE r.ItemId = i.ItemId
                                ) THEN 0
                                ELSE 1
                            END AS IsAvailable,
                            i.IsTrackedAsset,
                            i.ConsumableModelId
                        FROM dbo.Item i
                        WHERE i.Active = 1
                        ORDER BY i.Name";

                    using (var cmd = new SqlCommand(sqlItems, con))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            _allItems.Add(new ItemItem
                            {
                                Id = r.IsDBNull(0) ? (int?)null : r.GetInt32(0),
                                Name = r.IsDBNull(1) ? string.Empty : r.GetString(1),
                                StockOnHand = r.IsDBNull(2) ? 0 : r.GetInt32(2),
                                CategoryId = r.IsDBNull(3) ? (int?)null : r.GetInt32(3),
                                CategoryName = r.IsDBNull(4) ? null : r.GetString(4),
                                SerialNumber = r.IsDBNull(5) ? null : r.GetString(5),
                                ModelNumber = r.IsDBNull(6) ? null : r.GetString(6),
                                Amount = r.IsDBNull(7) ? 0 : r.GetDecimal(7),
                                IsAvailable = r.IsDBNull(8) ? true : r.GetInt32(8) == 1,
                                IsTrackedAsset = r.IsDBNull(9) ? false : r.GetBoolean(9),
                                ConsumableModelId = r.IsDBNull(10) ? (int?)null : r.GetInt32(10)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load items: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadOrgUnits()
        {
            var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs)) return;

            try
            {
                using (var con = new SqlConnection(cs))
                {
                    con.Open();

                    _companies.Clear();
                    _companies.Add(new OrgItem { Id = 0, Name = "-- Select Company --" });
                    using (var cmd = new SqlCommand("SELECT ComId, Name FROM dbo.Company WHERE Active = 1 ORDER BY Name", con))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            _companies.Add(new OrgItem { Id = r.GetInt32(0), Name = r.GetString(1) });

                    _departments.Clear();
                    _departments.Add(new OrgItem { Id = 0, Name = "-- Any --" });
                    using (var cmd = new SqlCommand("SELECT DeptId, Name FROM dbo.Department WHERE Active = 1 ORDER BY Name", con))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            _departments.Add(new OrgItem { Id = r.GetInt32(0), Name = r.GetString(1) });

                    _branches.Clear();
                    _branches.Add(new OrgItem { Id = 0, Name = "-- Any --" });
                    // Flag distributor-type branches with a suffix (same convention as
                    // Call Monitoring ticket lists). Guarded for DBs predating the flag.
                    using (var cmd = new SqlCommand(
                        "SELECT BranchId, Name, CASE WHEN COL_LENGTH('dbo.Branch', 'IsDistributor') IS NULL THEN 0 ELSE ISNULL(IsDistributor, 0) END AS IsDistributor FROM dbo.Branch WHERE Active = 1 ORDER BY Name", con))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            string branchName = r.GetString(1);
                            if (Convert.ToInt32(r.GetValue(2)) == 1)
                                branchName += " (Distributor)";
                            _branches.Add(new OrgItem { Id = r.GetInt32(0), Name = branchName });
                        }

                    // Independent sales distributors (dbo.Distributor). Missing table or
                    // columns on older DBs simply leaves the "(None)" default.
                    _distributors.Clear();
                    _distributors.Add(new OrgItem { Id = 0, Name = "(None)" });
                    try
                    {
                        using (var cmd = new SqlCommand(
                            "IF OBJECT_ID('dbo.Distributor', 'U') IS NOT NULL SELECT DistributorId, Name FROM dbo.Distributor WHERE IsActive = 1 ORDER BY SortOrder, Name", con))
                        using (var r = cmd.ExecuteReader())
                            while (r.Read())
                                _distributors.Add(new OrgItem { Id = r.GetInt32(0), Name = r.GetString(1) });
                    }
                    catch
                    {
                        // Distributor catalog unavailable — keep "(None)" only.
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load org units: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (_cmbCompany != null)
            {
                _cmbCompany.DataSource = null;
                _cmbCompany.DataSource = _companies;
                _cmbCompany.DisplayMember = "Name";
                _cmbCompany.SelectedIndex = 0;
            }
            if (_cmbDepartment != null)
            {
                _cmbDepartment.DataSource = null;
                _cmbDepartment.DataSource = _departments;
                _cmbDepartment.DisplayMember = "Name";
                _cmbDepartment.SelectedIndex = 0;
            }
            if (_cmbBranch != null)
            {
                _cmbBranch.DataSource = null;
                _cmbBranch.DataSource = _branches;
                _cmbBranch.DisplayMember = "Name";
                _cmbBranch.SelectedIndex = 0;
            }
            if (_cmbDistributor != null)
            {
                _cmbDistributor.DataSource = null;
                _cmbDistributor.DataSource = _distributors;
                _cmbDistributor.DisplayMember = "Name";
                _cmbDistributor.SelectedIndex = 0;
            }
        }

        private void ChkNoEmployee_CheckedChanged(object sender, EventArgs e)
        {
            bool noEmp = _chkNoEmployee.Checked;
            // Toggle employee controls
            cmbEmployee.Visible = !noEmp;
            btnAddEmployee.Visible = !noEmp;
            lblEmployee.Visible = !noEmp;
            // Toggle org-unit panel
            _orgUnitPanel.Visible = noEmp;

            // Reset backgrounds when switching modes
            if (!noEmp)
            {
                cmbEmployee.BackColor = SystemColors.Window;
            }
            else
            {
                _cmbCompany.BackColor = SystemColors.Window;
                _cmbDepartment.BackColor = SystemColors.Window;
                _cmbBranch.BackColor = SystemColors.Window;
                if (_cmbDistributor != null)
                    _cmbDistributor.BackColor = SystemColors.Window;
            }

            // Update the info banner text
            lblInfo.Text = noEmp
                ? "Batch add requests. Select a Company or a Distributor (at least one required), plus optional Department and Branch, then fill each row and click Save."
                : "Batch add requests. Select an employee for this batch, then fill each row and click Save.";
        }

        private void CmbCompany_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Reset company background when user makes a selection
            if (_cmbCompany?.SelectedItem is OrgItem c && c.Id > 0)
                _cmbCompany.BackColor = SystemColors.Window;
        }

        private void AddRow()
        {
            int idx = dgv.Rows.Add();
            var row = dgv.Rows[idx];

            // Default checkbox to unchecked
            row.Cells["colSelect"].Value = false;

            // Default to first real category (ensure LoadCategories was called)
            row.Cells["colCategory"].Value = _categories.FirstOrDefault();

            // Build item cell with only placeholder initially
            var placeholderItem = new ItemItem 
            { 
                Id = null, 
                Name = "-- Select Category First --", 
                StockOnHand = 0, 
                Amount = 0, 
                IsAvailable = false,
                SerialNumber = null  // Ensure SerialNumber is null so DisplayName just returns Name
            };
            var itemsForCell = new List<ItemItem> { placeholderItem };

            var itemCell = new DataGridViewComboBoxCell
            {
                DisplayMember = "DisplayName",
                ValueMember = null,
                ValueType = typeof(object),
                DataSource = itemsForCell
            };
            itemCell.Value = itemsForCell[0];
            row.Cells["colItem"] = itemCell;

            // Default quantity
            row.Cells["colQuantity"].Value = 1;

            // Default serial/model number empty
            if (dgv.Columns.Contains("colSerialNumber"))
            {
                row.Cells["colSerialNumber"].Value = string.Empty;
            }
            if (dgv.Columns.Contains("colModelNumber"))
            {
                row.Cells["colModelNumber"].Value = string.Empty;
            }

            // Default date requested to today (CalendarCell will show)
            // Do not strip time — database requires full timestamp
            row.Cells["colDateRequested"].Value = DateTime.Now;

            // Default status
            row.Cells["colStatus"].Value = "Under Review";
        }

        // Batch-level employee is now selected via cmbEmployee; this helper remains for
        // backward compatibility with earlier calls but does not need to update any cells.
        private void ApplySelectedEmployeeToAllRows()
        {
            // no-op: employee is held at dialog level only
        }

        #region Employee Search Popup

        private void SetupEmployeeSearchPopup()
        {
            _employeeSearchList = new ListBox
            {
                Font = new Font("Segoe UI", 9.5F),
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false,
                TabStop = false,
                Visible = false,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 22
            };
            _employeeSearchList.DrawItem += EmployeeSearchList_DrawItem;
            _employeeSearchList.MouseClick += EmployeeSearchList_MouseClick;
            _employeeSearchList.MouseDown += EmployeeSearchList_MouseDown;

            // Add to the form so it overlays other controls
            Controls.Add(_employeeSearchList);
            _employeeSearchList.BringToFront();
        }

        private void EmployeeSearchList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();

            var emp = _employeeSearchList.Items[e.Index] as EmployeeItem;
            if (emp == null) return;

            string text = emp.DisplayText;
            var color = (emp.Id == null) ? Color.Gray : e.ForeColor;
            using (var brush = new SolidBrush(color))
            {
                e.Graphics.DrawString(text, e.Font, brush, e.Bounds.X + 4, e.Bounds.Y + 2);
            }
            e.DrawFocusRectangle();
        }

        private void EmployeeSearchList_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int index = _employeeSearchList.IndexFromPoint(e.Location);
                if (index >= 0 && index < _employeeSearchList.Items.Count)
                    _employeeSearchList.SelectedIndex = index;
            }
        }

        private void EmployeeSearchList_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _employeeSearchList.SelectedItem is EmployeeItem emp && emp.Id.HasValue)
            {
                SelectEmployee(emp);
            }
        }

        private void SelectEmployee(EmployeeItem emp)
        {
            _suppressEmployeeFilter = true;
            cmbEmployee.Text = emp.DisplayText;
            cmbEmployee.Tag = emp; // Store selected employee in Tag for reliable retrieval
            _suppressEmployeeFilter = false;
            HideEmployeeSearch();
            // Move focus to the grid
            if (dgv.Rows.Count > 0)
            {
                dgv.Focus();
                dgv.CurrentCell = dgv.Rows[0].Cells["colCategory"];
            }
        }

        private void CmbEmployee_TextChanged(object sender, EventArgs e)
        {
            if (_suppressEmployeeFilter) return;

            // Clear the Tag since user is typing (invalidates previous selection)
            cmbEmployee.Tag = null;

            string filter = cmbEmployee.Text.Trim();
            if (string.IsNullOrEmpty(filter) || filter == "-- Select Employee --")
            {
                HideEmployeeSearch();
                return;
            }

            // Filter employees (skip placeholder)
            var matches = _employees
                .Where(emp => emp.Id.HasValue &&
                    (emp.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     (emp.EmployeeNumber != null && emp.EmployeeNumber.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                     (emp.Position != null && emp.Position.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)))
                .Take(15)
                .ToList();

            if (matches.Count == 0)
            {
                HideEmployeeSearch();
                return;
            }

            ShowEmployeeSearch(matches);
        }

        private void CmbEmployee_KeyDown(object sender, KeyEventArgs e)
        {
            if (_employeeSearchList == null || !_employeeSearchList.Visible)
            {
                // Esc clears the text when popup is not visible
                if (e.KeyCode == Keys.Escape)
                {
                    _suppressEmployeeFilter = true;
                    cmbEmployee.Text = string.Empty;
                    cmbEmployee.Tag = null;
                    _suppressEmployeeFilter = false;
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.Down:
                    if (_employeeSearchList.Items.Count > 0)
                    {
                        _employeeSearchList.SelectedIndex = Math.Min(
                            _employeeSearchList.SelectedIndex + 1,
                            _employeeSearchList.Items.Count - 1);
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;

                case Keys.Up:
                    if (_employeeSearchList.Items.Count > 0)
                    {
                        _employeeSearchList.SelectedIndex = Math.Max(
                            _employeeSearchList.SelectedIndex - 1, 0);
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;

                case Keys.Enter:
                    if (_employeeSearchList.SelectedItem is EmployeeItem emp && emp.Id.HasValue)
                    {
                        SelectEmployee(emp);
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;

                case Keys.Escape:
                    HideEmployeeSearch();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
            }
        }

        private void CmbEmployee_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (_employeeSearchList != null && _employeeSearchList.Visible)
            {
                if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Escape)
                    e.IsInputKey = true;
            }
        }

        private void ShowEmployeeSearch(List<EmployeeItem> matches)
        {
            _employeeSearchList.Items.Clear();
            foreach (var m in matches)
                _employeeSearchList.Items.Add(m);

            // Position below the cmbEmployee control
            var loc = cmbEmployee.Parent.PointToScreen(cmbEmployee.Location);
            var formLoc = this.PointToClient(loc);

            _employeeSearchList.Location = new Point(formLoc.X, formLoc.Y + cmbEmployee.Height);
            _employeeSearchList.Width = Math.Max(cmbEmployee.Width, 450);
            _employeeSearchList.Height = Math.Min(matches.Count * _employeeSearchList.ItemHeight + 4, 220);
            _employeeSearchList.Visible = true;
            _employeeSearchList.BringToFront();

            if (_employeeSearchList.Items.Count > 0)
                _employeeSearchList.SelectedIndex = 0;
        }

        private void HideEmployeeSearch()
        {
            if (_employeeSearchList != null)
                _employeeSearchList.Visible = false;
        }

        #endregion

        private void Dgv_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var colName = dgv.Columns[e.ColumnIndex].Name;

            if (colName == "colCategory")
            {
                // When category changes, filter the item list for that row
                var row = dgv.Rows[e.RowIndex];

                // robustly resolve category (can be CategoryItem, string, DataRowView etc.)
                CategoryItem catObj = row.Cells["colCategory"].Value as CategoryItem;
                if (catObj == null)
                {
                    var raw = row.Cells["colCategory"].Value?.ToString();
                    if (!string.IsNullOrEmpty(raw))
                        catObj = _categories.FirstOrDefault(c => string.Equals(c.Name, raw, StringComparison.OrdinalIgnoreCase));
                }

                List<ItemItem> itemsForRow;
                if (catObj == null)
                {
                    // No valid category selected
                    itemsForRow = new List<ItemItem>();
                }
                else
                {
                    // Filter items by selected category only
                    itemsForRow = _allItems.Where(x => x.CategoryId == catObj.CategoryId).ToList();
                }

                var placeholderItem = new ItemItem 
                { 
                    Id = null, 
                    Name = "-- Select Item --", 
                    StockOnHand = 0, 
                    Amount = 0, 
                    IsAvailable = false,
                    SerialNumber = null
                };
                var itemsDataSource = new List<ItemItem> { placeholderItem };
                itemsDataSource.AddRange(itemsForRow);

                // Update the item combo cell for this row
                var colIndex = dgv.Columns["colItem"].Index;

                var newItemCell = new DataGridViewComboBoxCell
                {
                    DisplayMember = "DisplayName",
                    ValueMember = null,
                    ValueType = typeof(object),
                    DataSource = itemsDataSource
                };

                newItemCell.Value = itemsDataSource[0];
                row.Cells[colIndex] = newItemCell;

                // Force the grid to refresh this cell/row
                dgv.InvalidateCell(colIndex, e.RowIndex);
                dgv.InvalidateRow(e.RowIndex);

                // Auto-focus the Item cell after Category is selected
            }
        }

        private void Dgv_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var colName = dgv.Columns[e.ColumnIndex].Name;

            // Handle Model Number and Serial Number typing-based shortcut
            if ((colName == "colModelNumber" || colName == "colSerialNumber") && !_suppressCellEndEdit)
            {
                HideAutocomplete();

                // Check if Model Number was cleared - if so, reset all auto-filled fields
                if (colName == "colModelNumber")
                {
                    var row = dgv.Rows[e.RowIndex];
                    var modelValue = row.Cells["colModelNumber"].Value?.ToString() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(modelValue))
                    {
                        // Model Number was cleared - reset all auto-filled fields
                        ClearAutoFilledFields(e.RowIndex);
                        return;
                    }
                }

                ResolveItemFromTypedValue(e.RowIndex, colName);
                return;
            }

            if (colName == "colCategory")
            {
                // Handle category change when editing completes
                var row = dgv.Rows[e.RowIndex];

                CategoryItem catObj = row.Cells["colCategory"].Value as CategoryItem;
                if (catObj == null)
                {
                    var raw = row.Cells["colCategory"].Value?.ToString();
                    if (!string.IsNullOrEmpty(raw))
                        catObj = _categories.FirstOrDefault(x => string.Equals(x.Name, raw, StringComparison.OrdinalIgnoreCase));
                }

                List<ItemItem> itemsForRow = catObj == null
                    ? new List<ItemItem>()
                    : _allItems.Where(x => x.CategoryId == catObj.CategoryId).ToList();

                var placeholderItem = new ItemItem 
                { 
                    Id = null, 
                    Name = "-- Select Item --", 
                    StockOnHand = 0, 
                    Amount = 0, 
                    IsAvailable = false,
                    SerialNumber = null
                };
                var itemsDataSource = new List<ItemItem> { placeholderItem };
                itemsDataSource.AddRange(itemsForRow);

                // Update the item combo cell for this row
                var colIndex = dgv.Columns["colItem"].Index;

                var newItemCell = new DataGridViewComboBoxCell
                {
                    DisplayMember = "DisplayName",
                    ValueMember = null,
                    ValueType = typeof(object),
                    DataSource = itemsDataSource
                };

                newItemCell.Value = itemsDataSource[0];
                row.Cells[colIndex] = newItemCell;

                // Force refresh
                dgv.RefreshEdit();
                dgv.InvalidateCell(colIndex, e.RowIndex);
            }
            else if (colName == "colItem")
            {
                // When item changes, update UnitPrice, SerialNumber and ModelNumber from Item data
                var row = dgv.Rows[e.RowIndex];
                ItemItem itemObj = row.Cells["colItem"].Value as ItemItem;

                if (itemObj == null)
                {
                    // Sometimes the value is just the display text; try to resolve
                    var itemDisplay = row.Cells["colItem"].Value?.ToString();
                    if (!string.IsNullOrEmpty(itemDisplay))
                    {
                        string itemName = itemDisplay;
                        string extractedSerial = null;

                        if (itemDisplay.Contains(" (SN: ") && itemDisplay.EndsWith(")"))
                        {
                            var parts = itemDisplay.Split(new[] { " (SN: " }, StringSplitOptions.None);
                            if (parts.Length == 2)
                            {
                                itemName = parts[0];
                                extractedSerial = parts[1].TrimEnd(')');
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(extractedSerial))
                        {
                            itemObj = _allItems.FirstOrDefault(x =>
                                string.Equals(x.SerialNumber ?? string.Empty, extractedSerial, StringComparison.OrdinalIgnoreCase));
                        }

                        if (itemObj == null)
                        {
                            itemObj = _allItems.FirstOrDefault(x =>
                                string.Equals(x.Name, itemName, StringComparison.OrdinalIgnoreCase));
                        }
                    }
                }

                if (itemObj != null && itemObj.Id.HasValue)
                {
                    row.Cells["colUnitPrice"].Value = itemObj.Amount;

                    if (dgv.Columns.Contains("colSerialNumber"))
                    {
                        row.Cells["colSerialNumber"].Value = itemObj.SerialNumber ?? string.Empty;
                    }

                    if (dgv.Columns.Contains("colModelNumber"))
                    {
                        row.Cells["colModelNumber"].Value = itemObj.ModelNumber ?? string.Empty;
                    }

                    if (dgv.Columns.Contains("colDescription"))
                    {
                        row.Cells["colDescription"].Value = itemObj.Name ?? string.Empty;
                    }

                    if (dgv.Columns.Contains("colFixedAsset"))
                    {
                        row.Cells["colFixedAsset"].Value = itemObj.IsTrackedAsset ? "Yes" : "No";
                    }
                }
                else
                {
                    // Only clear when the selection is effectively cleared/placeholder
                    row.Cells["colUnitPrice"].Value = 0m;

                    if (dgv.Columns.Contains("colSerialNumber"))
                    {
                        row.Cells["colSerialNumber"].Value = string.Empty;
                    }

                    if (dgv.Columns.Contains("colModelNumber"))
                    {
                        row.Cells["colModelNumber"].Value = string.Empty;
                    }

                    if (dgv.Columns.Contains("colDescription"))
                    {
                        row.Cells["colDescription"].Value = string.Empty;
                    }

                    if (dgv.Columns.Contains("colFixedAsset"))
                    {
                        row.Cells["colFixedAsset"].Value = string.Empty;
                    }
                }

                // Refresh details panel to reflect auto-filled Description
                if (IsHandleCreated)
                    BeginInvoke(new Action(() => BindDetailsPanel(dgv.CurrentRow)));
            }
        }

        private void Dgv_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (dgv.CurrentCell == null) return;

            string colName = dgv.Columns[dgv.CurrentCell.ColumnIndex].Name;

            // Configure ComboBox columns for searchability and single-click
            if (e.Control is ComboBox combo)
            {
                // Match the editing control background to the pill row color so it
                // doesn't appear as a stark white box overlaid on the styled grid.
                int rowIdx = dgv.CurrentCell?.RowIndex ?? 0;
                Color rowBg = (rowIdx % 2 == 0)
                    ? Color.FromArgb(227, 242, 253)
                    : Color.FromArgb(232, 245, 233);
                combo.BackColor = rowBg;
                combo.ForeColor = Color.FromArgb(30, 30, 30);
                e.CellStyle.BackColor = rowBg;
                e.CellStyle.ForeColor = Color.FromArgb(30, 30, 30);

                // Make dropdowns searchable with type-ahead
                combo.DropDownStyle = ComboBoxStyle.DropDown;
                combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                combo.AutoCompleteSource = AutoCompleteSource.ListItems;

                // Maintain visual selection when ComboBox takes focus
                // Force the current cell to remain selected
                var currentCell = dgv.CurrentCell;
                if (currentCell != null)
                {
                    currentCell.Selected = true;

                    // Add event handlers to maintain selection during editing
                    combo.GotFocus -= ComboBox_GotFocus;
                    combo.LostFocus -= ComboBox_LostFocus;
                    combo.DropDown -= ComboBox_DropDown;
                    combo.DropDownClosed -= ComboBox_DropDownClosed;

                    combo.GotFocus += ComboBox_GotFocus;
                    combo.LostFocus += ComboBox_LostFocus;
                    combo.DropDown += ComboBox_DropDown;
                    combo.DropDownClosed += ComboBox_DropDownClosed;
                }

                // Hook into Item combobox to do owner-draw and selection prevention for out-of-stock
                if (colName == "colItem")
                {
                    combo.DrawMode = DrawMode.OwnerDrawFixed;
                    combo.DrawItem -= Combo_DrawItem;
                    combo.SelectionChangeCommitted -= Combo_SelectionChangeCommitted;

                    combo.DrawItem += Combo_DrawItem;
                    combo.SelectionChangeCommitted += Combo_SelectionChangeCommitted;
                }
                else
                {
                    // For other combos, use normal drawing
                    combo.DrawMode = DrawMode.Normal;
                }
            }
            // Autocomplete for Model Number and Serial Number
            else if (e.Control is TextBox textBox)
            {
                // Always detach first to avoid stale handlers from a previous column
                textBox.TextChanged -= AutocompleteTextBox_TextChanged;
                textBox.KeyDown -= AutocompleteTextBox_KeyDown;
                textBox.PreviewKeyDown -= AutocompleteTextBox_PreviewKeyDown;
                textBox.Leave -= AutocompleteTextBox_Leave;

                if (colName == "colModelNumber" || colName == "colSerialNumber")
                {
                    // Attach handlers only for autocomplete-eligible columns
                    textBox.TextChanged += AutocompleteTextBox_TextChanged;
                    textBox.KeyDown += AutocompleteTextBox_KeyDown;
                    textBox.PreviewKeyDown += AutocompleteTextBox_PreviewKeyDown;
                    textBox.Leave += AutocompleteTextBox_Leave;

                    _lastAutocompleteColumn = colName;
                }
                else
                {
                    // Hide any leftover autocomplete popup
                    HideAutocomplete();
                }
            }
        }

        private void Dgv_CellEnter(object sender, DataGridViewCellEventArgs e)
        {
            // Hide autocomplete when entering a different cell
            HideAutocomplete();

            // Block entering a disabled Item cell — redirect to Category
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && dgv.Columns[e.ColumnIndex].Name == "colItem")
            {
                var row = dgv.Rows[e.RowIndex];
                var itemCell = row.Cells["colItem"] as DataGridViewComboBoxCell;
                var ds = itemCell?.DataSource as List<ItemItem>;
                bool isDisabled = ds != null && ds.Count <= 1 && (ds.Count == 0 || ds[0]?.Id == null);

                if (isDisabled && IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            dgv.CurrentCell = dgv.Rows[e.RowIndex].Cells["colCategory"];
                            ShowNonBlockingWarning("Select a category first before choosing an item.");
                        }
                        catch { }
                    }));
                }
            }
        }

        private void ComboBox_GotFocus(object sender, EventArgs e)
        {
            // Maintain visual selection when ComboBox gets focus
            if (dgv.CurrentCell != null)
            {
                dgv.CurrentCell.Selected = true;
                dgv.InvalidateCell(dgv.CurrentCell);
            }
        }

        private void ComboBox_LostFocus(object sender, EventArgs e)
        {
            // Refresh cell when ComboBox loses focus
            if (dgv.CurrentCell != null)
            {
                dgv.InvalidateCell(dgv.CurrentCell);
            }
        }

        private void ComboBox_DropDown(object sender, EventArgs e)
        {
            // Ensure cell remains selected when dropdown opens
            if (dgv.CurrentCell != null)
            {
                dgv.CurrentCell.Selected = true;
                dgv.InvalidateCell(dgv.CurrentCell);
            }
        }

        private void ComboBox_DropDownClosed(object sender, EventArgs e)
        {
            // Maintain selection when dropdown closes
            if (dgv.CurrentCell != null)
            {
                dgv.CurrentCell.Selected = true;
                dgv.InvalidateCell(dgv.CurrentCell);
            }
        }

        private void Dgv_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            var g = e.Graphics;
            var bounds = e.CellBounds;

            // ── Header row ──────────────────────────────────────────────────────────
            if (e.RowIndex == -1 && e.ColumnIndex >= 0)
            {
                using (var headerBrush = new SolidBrush(Color.FromArgb(52, 152, 219)))
                    g.FillRectangle(headerBrush, bounds);

                // White right + bottom border
                using (var whitePen = new Pen(Color.White))
                {
                    g.DrawLine(whitePen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
                    g.DrawLine(whitePen, bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
                }

                // Draw header text (skip colSelect — floating CheckBox sits on top)
                if (dgv.Columns[e.ColumnIndex].Name != "colSelect")
                {
                    using (var textBrush = new SolidBrush(Color.White))
                    using (var headerFont = new Font("Segoe UI", 9F, FontStyle.Bold))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
                        var textRect = new RectangleF(bounds.X + 8, bounds.Y, bounds.Width - 16, bounds.Height);
                        g.DrawString(e.Value?.ToString() ?? string.Empty, headerFont, textBrush, textRect, sf);
                    }
                }

                e.Handled = true;
                return;
            }

            // ── Data rows ───────────────────────────────────────────────────────────
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            // Check disabled colItem state
            bool isDisabledItem = false;
            if (dgv.Columns[e.ColumnIndex].Name == "colItem")
            {
                var itemRow = dgv.Rows[e.RowIndex];
                var itemCell = itemRow.Cells["colItem"] as DataGridViewComboBoxCell;
                var ds = itemCell?.DataSource as List<ItemItem>;
                isDisabledItem = ds != null && ds.Count <= 1 && (ds.Count == 0 || ds[0]?.Id == null);
                // Don't treat actively-edited cell as disabled
                if (dgv.CurrentCell?.RowIndex == e.RowIndex && dgv.CurrentCell?.ColumnIndex == e.ColumnIndex)
                    isDisabledItem = false;
            }

            bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
            bool isCurrentComboCell = dgv.CurrentCell != null &&
                dgv.CurrentCell.RowIndex == e.RowIndex &&
                dgv.CurrentCell.ColumnIndex == e.ColumnIndex &&
                dgv.EditingControl is ComboBox &&
                dgv.Columns[e.ColumnIndex] is DataGridViewComboBoxColumn;

            // Determine pill fill color
            Color pillColor;
            if (isSelected && !isCurrentComboCell)
                pillColor = Color.FromArgb(41, 128, 185);
            else if (isDisabledItem)
                pillColor = DisabledCellColor;
            else if (e.CellStyle.BackColor == InvalidCellColor)
                pillColor = Color.FromArgb(255, 210, 210);
            else
                pillColor = (e.RowIndex % 2 == 0)
                    ? Color.FromArgb(227, 242, 253)
                    : Color.FromArgb(232, 245, 233);

            // White outer fill
            using (var whiteBrush = new SolidBrush(Color.White))
                g.FillRectangle(whiteBrush, bounds);

            // Pill inner rectangle (3px top/bottom padding)
            var pillRect = new Rectangle(bounds.X, bounds.Y + 3, bounds.Width, bounds.Height - 6);
            using (var pillBrush = new SolidBrush(pillColor))
                g.FillRectangle(pillBrush, pillRect);

            // White right separator
            using (var whitePen = new Pen(Color.White, 2))
                g.DrawLine(whitePen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom);

            // ── colSelect: draw checkbox manually ───────────────────────────────────
            if (dgv.Columns[e.ColumnIndex].Name == "colSelect")
            {
                bool chkValue = e.Value is bool bv && bv;
                var chkState = chkValue
                    ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                    : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
                var chkSize = CheckBoxRenderer.GetGlyphSize(g, chkState);
                var chkPoint = new Point(
                    bounds.X + (bounds.Width - chkSize.Width) / 2,
                    bounds.Y + (bounds.Height - chkSize.Height) / 2);
                CheckBoxRenderer.DrawCheckBox(g, chkPoint, chkState);
                e.Handled = true;
                return;
            }

            // ── Disabled colItem: placeholder text ──────────────────────────────────
            if (isDisabledItem)
            {
                using (var textBrush = new SolidBrush(Color.FromArgb(160, 160, 160)))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                    var textRect = new RectangleF(bounds.X + 8, bounds.Y, bounds.Width - 16, bounds.Height);
                    g.DrawString("Select category first", dgv.Font, textBrush, textRect, sf);
                }
                e.Handled = true;
                return;
            }

            // ── Active ComboBox cell: the editing control paints itself; just draw a
            //    subtle accent border so the active cell is clear. ──────────────────
            if (isCurrentComboCell)
            {
                // Row color pill (editing control will sit on top matching this color)
                using (var pillBrush = new SolidBrush(pillColor))
                {
                    var pr = new Rectangle(bounds.X, bounds.Y + 3, bounds.Width, bounds.Height - 6);
                    g.FillRectangle(pillBrush, pr);
                }
                // Accent border to indicate active cell
                using (var borderPen = new Pen(Color.FromArgb(52, 152, 219), 2))
                {
                    var br = bounds;
                    br.Inflate(-1, -1);
                    g.DrawRectangle(borderPen, br);
                }
                e.Handled = true;
                return;
            }

            // ── ComboBox cell (not in edit mode): draw text + custom arrow ──────────
            if (dgv.Columns[e.ColumnIndex] is DataGridViewComboBoxColumn)
            {
                string displayText = e.FormattedValue?.ToString() ?? "";
                Color textColor = isSelected ? Color.White : Color.FromArgb(40, 40, 40);

                using (var textBrush = new SolidBrush(textColor))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.NoWrap,
                        Trimming = StringTrimming.EllipsisCharacter
                    };
                    var textRect = new RectangleF(bounds.X + 8, bounds.Y, bounds.Width - 24, bounds.Height);
                    g.DrawString(displayText, dgv.Font, textBrush, textRect, sf);
                }

                // Draw a subtle dropdown arrow
                Color arrowColor = isSelected ? Color.White : Color.FromArgb(100, 100, 100);
                int ax = bounds.Right - 11;
                int ay = bounds.Y + bounds.Height / 2;
                using (var arrowBrush = new SolidBrush(arrowColor))
                {
                    g.FillPolygon(arrowBrush, new Point[]
                    {
                        new Point(ax - 4, ay - 2),
                        new Point(ax + 4, ay - 2),
                        new Point(ax,     ay + 3)
                    });
                }

                e.Handled = true;
                return;
            }

            // ── All other cells: WinForms paints content only (no background) ────────
            if (isSelected)
            {
                e.CellStyle.ForeColor = Color.White;
                e.CellStyle.SelectionForeColor = Color.White;
            }
            e.Paint(bounds, DataGridViewPaintParts.All
                & ~DataGridViewPaintParts.Background
                & ~DataGridViewPaintParts.SelectionBackground);
            e.Handled = true;
        }

        private void Dgv_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // Handle checkbox click for individual rows
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && dgv.Columns[e.ColumnIndex].Name == "colSelect")
            {
                // Commit the checkbox change immediately
                dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);

                // Update the Select All checkbox state after a brief delay
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(() => UpdateSelectAllCheckBoxState()));
                }
                else
                {
                    UpdateSelectAllCheckBoxState();
                }
            }
        }

        private void Dgv_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // Enable single-click dropdown for ComboBox columns
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var column = dgv.Columns[e.ColumnIndex];
            if (column is DataGridViewComboBoxColumn && column.Name != "colSelect")
            {
                // Enter edit mode immediately on click for combo cells
                if (dgv.CurrentCell != null && !dgv.CurrentCell.IsInEditMode)
                {
                    dgv.BeginEdit(true);

                    // Automatically drop down the combo box after a brief delay to ensure edit control is ready
                    if (dgv.EditingControl is ComboBox combo)
                    {
                        // Use BeginInvoke to ensure the combo is fully initialized before dropping down
                        BeginInvoke(new Action(() =>
                        {
                            if (combo != null && !combo.IsDisposed)
                            {
                                combo.DroppedDown = true;
                            }
                        }));
                    }
                }
            }
        }

        private void Dgv_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            // Prevent sorting when clicking the checkbox column header
            if (e.ColumnIndex >= 0 && dgv.Columns[e.ColumnIndex].Name == "colSelect")
            {
                // The checkbox will handle its own click event
            }
        }

        private void SelectAllCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_isSelectAllCheckBoxUpdating)
                return;

            // Check or uncheck all selectable rows (excluding new row)
            bool newCheckState = _selectAllCheckBox.Checked;

            // Store current cell info to restore focus after updates
            var currentCell = dgv.CurrentCell;
            int currentRowIndex = currentCell?.RowIndex ?? -1;
            int currentColIndex = currentCell?.ColumnIndex ?? -1;

            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                var row = dgv.Rows[i];
                if (row.IsNewRow) continue; // Skip the new row placeholder

                if (dgv.Columns.Contains("colSelect"))
                {
                    row.Cells["colSelect"].Value = newCheckState;
                }
            }

            // If we have a valid current cell in the checkbox column, commit the edit
            if (currentRowIndex >= 0 && currentColIndex >= 0 &&
                dgv.Columns[currentColIndex].Name == "colSelect")
            {
                dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
                dgv.NotifyCurrentCellDirty(false);
            }

            dgv.Refresh();
        }

        private void UpdateSelectAllCheckBoxState()
        {
            if (_selectAllCheckBox == null)
                return;

            _isSelectAllCheckBoxUpdating = true;

            try
            {
                int totalRows = 0;
                int checkedRows = 0;

                for (int i = 0; i < dgv.Rows.Count; i++)
                {
                    var row = dgv.Rows[i];
                    if (row.IsNewRow) continue;

                    totalRows++;

                    var checkValue = row.Cells["colSelect"].Value;
                    bool isChecked = checkValue != null && (bool)checkValue == true;

                    if (isChecked)
                        checkedRows++;
                }

                // Binary checkbox: checked only if ALL rows are checked
                if (totalRows > 0 && checkedRows == totalRows)
                {
                    _selectAllCheckBox.Checked = true;
                }
                else
                {
                    _selectAllCheckBox.Checked = false;
                }

                // Enable/disable Remove Row button based on selection
                btnRemoveRow.Enabled = checkedRows > 0;
            }
            finally
            {
                _isSelectAllCheckBoxUpdating = false;
            }
        }

        #region Autocomplete Support

        private void AutocompleteTextBox_TextChanged(object sender, EventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null || dgv.CurrentCell == null) return;

            string typedText = textBox.Text;
            string colName = _lastAutocompleteColumn;
            int rowIndex = dgv.CurrentCell.RowIndex;

            if (string.IsNullOrWhiteSpace(typedText))
            {
                HideAutocomplete();
                return;
            }

            // Get filtered suggestions (with model context for serial numbers)
            List<string> suggestions = GetAutocompleteSuggestions(colName, typedText, rowIndex);

            if (suggestions.Count == 0)
            {
                HideAutocomplete();
                return;
            }

            // Show autocomplete list
            _autocompleteRowIndex = rowIndex;
            ShowAutocomplete(suggestions, textBox);
        }

        private List<string> GetAutocompleteSuggestions(string columnName, string filter, int rowIndex)
        {
            var suggestions = new List<string>();

            // Scope suggestions to the category already chosen in this row
            CategoryItem selectedCategory = null;
            if (rowIndex >= 0 && rowIndex < dgv.Rows.Count)
            {
                selectedCategory = dgv.Rows[rowIndex].Cells["colCategory"].Value as CategoryItem;
                if (selectedCategory == null)
                {
                    var raw = dgv.Rows[rowIndex].Cells["colCategory"].Value?.ToString();
                    if (!string.IsNullOrEmpty(raw))
                        selectedCategory = _categories.FirstOrDefault(c =>
                            string.Equals(c.Name, raw, StringComparison.OrdinalIgnoreCase));
                }
            }

            IEnumerable<ItemItem> categoryItems = _allItems;
            if (selectedCategory != null && selectedCategory.CategoryId > 0)
                categoryItems = _allItems.Where(x => x.CategoryId == selectedCategory.CategoryId);

            if (columnName == "colModelNumber")
            {
                suggestions = categoryItems
                    .Where(x => !string.IsNullOrWhiteSpace(x.ModelNumber) &&
                                x.ModelNumber.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 &&
                                x.IsAvailable)
                    .Select(x => x.ModelNumber)
                    .Distinct()
                    .OrderBy(x => x)
                    .Take(10)
                    .ToList();
            }
            else if (columnName == "colSerialNumber")
            {
                // Also narrow by model number if one is already entered in this row
                string contextModelNumber = (rowIndex >= 0 && rowIndex < dgv.Rows.Count)
                    ? dgv.Rows[rowIndex].Cells["colModelNumber"].Value?.ToString()
                    : null;

                IEnumerable<ItemItem> itemsToSearch = categoryItems;
                if (!string.IsNullOrWhiteSpace(contextModelNumber))
                    itemsToSearch = categoryItems.Where(x =>
                        string.Equals(x.ModelNumber, contextModelNumber, StringComparison.OrdinalIgnoreCase));

                suggestions = itemsToSearch
                    .Where(x => !string.IsNullOrWhiteSpace(x.SerialNumber) &&
                                x.SerialNumber.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 &&
                                x.IsAvailable)
                    .Select(x => x.SerialNumber)
                    .Distinct()
                    .OrderBy(x => x)
                    .Take(10)
                    .ToList();
            }

            return suggestions;
        }

        private void ShowAutocomplete(List<string> suggestions, TextBox textBox)
        {
            if (_autocompleteList == null)
            {
                _autocompleteList = new ListBox
                {
                    Font = dgv.Font,
                    BorderStyle = BorderStyle.FixedSingle,
                    IntegralHeight = false,
                    TabStop = false
                };
                _autocompleteList.MouseClick += AutocompleteList_MouseClick;
                _autocompleteList.MouseDown += AutocompleteList_MouseDown;
                dgv.Controls.Add(_autocompleteList);
            }

            _autocompleteList.Items.Clear();
            foreach (var suggestion in suggestions)
            {
                _autocompleteList.Items.Add(suggestion);
            }

            // Position below the current cell
            var cellRect = dgv.GetCellDisplayRectangle(dgv.CurrentCell.ColumnIndex, dgv.CurrentCell.RowIndex, false);
            int listHeight = Math.Min(suggestions.Count * _autocompleteList.ItemHeight + 4, 150);

            _autocompleteList.Location = new Point(cellRect.Left, cellRect.Bottom);
            _autocompleteList.Size = new Size(cellRect.Width, listHeight);
            _autocompleteList.BringToFront();
            _autocompleteList.Visible = true;
            _isAutocompleteActive = true;

            if (_autocompleteList.Items.Count > 0)
            {
                _autocompleteList.SelectedIndex = 0;
            }
        }

        private void HideAutocomplete()
        {
            if (_autocompleteList != null)
            {
                _autocompleteList.Visible = false;
                _isAutocompleteActive = false;
                _autocompleteRowIndex = -1;
            }
        }

        private void AutocompleteTextBox_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (!_isAutocompleteActive || _autocompleteList == null || !_autocompleteList.Visible)
                return;

            // Mark these keys as input keys so the DataGridView doesn't process them
            switch (e.KeyCode)
            {
                case Keys.Down:
                case Keys.Up:
                case Keys.Enter:
                case Keys.Tab:
                case Keys.Escape:
                    e.IsInputKey = true;
                    break;
            }
        }

        private void AutocompleteTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isAutocompleteActive || _autocompleteList == null || !_autocompleteList.Visible)
                return;

            // Handle navigation and selection keys
            bool handled = false;

            switch (e.KeyCode)
            {
                case Keys.Down:
                    if (_autocompleteList.Items.Count > 0)
                    {
                        if (_autocompleteList.SelectedIndex < _autocompleteList.Items.Count - 1)
                        {
                            _autocompleteList.SelectedIndex++;
                        }
                        else
                        {
                            _autocompleteList.SelectedIndex = _autocompleteList.Items.Count - 1;
                        }
                        _autocompleteList.Invalidate();
                    }
                    handled = true;
                    break;

                case Keys.Up:
                    if (_autocompleteList.Items.Count > 0)
                    {
                        if (_autocompleteList.SelectedIndex > 0)
                        {
                            _autocompleteList.SelectedIndex--;
                        }
                        else
                        {
                            _autocompleteList.SelectedIndex = 0;
                        }
                        _autocompleteList.Invalidate();
                    }
                    handled = true;
                    break;

                case Keys.Enter:
                    if (_autocompleteList.SelectedItem != null)
                    {
                        string selectedValue = _autocompleteList.SelectedItem.ToString();
                        ApplyAutocompleteSuggestion(selectedValue);
                        handled = true;
                    }
                    break;

                case Keys.Tab:
                    if (_autocompleteList.SelectedItem != null)
                    {
                        string selectedValue = _autocompleteList.SelectedItem.ToString();
                        ApplyAutocompleteSuggestion(selectedValue);

                        // Move to next cell after applying suggestion
                        if (IsHandleCreated)
                        {
                            BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    if (dgv.CurrentCell != null)
                                    {
                                        int nextCol = dgv.CurrentCell.ColumnIndex + 1;
                                        int currentRow = dgv.CurrentCell.RowIndex;

                                        if (nextCol < dgv.Columns.Count)
                                        {
                                            dgv.CurrentCell = dgv.Rows[currentRow].Cells[nextCol];
                                    }
                                    else if (currentRow + 1 < dgv.Rows.Count)
                                    {
                                        // Move to first cell of next row
                                        dgv.CurrentCell = dgv.Rows[currentRow + 1].Cells[0];
                                    }
                                }
                            }
                            catch { }
                        }));
                        }

                        handled = true;
                    }
                    break;

                case Keys.Escape:
                    HideAutocomplete();
                    // Don't change the cell value, just dismiss the dropdown
                    handled = true;
                    break;
            }

            if (handled)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void AutocompleteTextBox_Leave(object sender, EventArgs e)
        {
            // Delay hiding to allow click events to fire
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    // Only hide if the autocomplete list doesn't have focus
                    if (_autocompleteList != null && !_autocompleteList.Focused)
                    {
                        HideAutocomplete();
                    }
                }));
            }
            else
            {
                // Only hide if the autocomplete list doesn't have focus
                if (_autocompleteList != null && !_autocompleteList.Focused)
                {
                    HideAutocomplete();
                }
            }
        }

        private void AutocompleteList_MouseDown(object sender, MouseEventArgs e)
        {
            // Prevent the listbox from stealing focus from the textbox
            if (e.Button == MouseButtons.Left)
            {
                int index = _autocompleteList.IndexFromPoint(e.Location);
                if (index >= 0 && index < _autocompleteList.Items.Count)
                {
                    _autocompleteList.SelectedIndex = index;
                }
            }
        }

        private void AutocompleteList_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int index = _autocompleteList.IndexFromPoint(e.Location);
                if (index >= 0 && index < _autocompleteList.Items.Count)
                {
                    _autocompleteList.SelectedIndex = index;
                    if (_autocompleteList.SelectedItem != null)
                    {
                        ApplyAutocompleteSuggestion(_autocompleteList.SelectedItem.ToString());
                    }
                }
            }
        }

        private void ApplyAutocompleteSuggestion(string suggestion)
        {
            if (dgv.CurrentCell == null) return;

            // If currently editing, update the TextBox text first
            if (dgv.IsCurrentCellInEditMode && dgv.EditingControl is TextBox textBox)
            {
                textBox.Text = suggestion;
                textBox.SelectionStart = suggestion.Length;
                textBox.SelectionLength = 0;
            }

            // Set the cell value
            dgv.CurrentCell.Value = suggestion;
            HideAutocomplete();

            // End edit to commit the value
            _suppressCellEndEdit = false;

            try
            {
                dgv.EndEdit();
            }
            catch
            {
                // Ignore errors during EndEdit
            }
        }

        #endregion

        #region Item Resolution from Typed Model/Serial

        private void ClearAutoFilledFields(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgv.Rows.Count) return;

            var row = dgv.Rows[rowIndex];

            // Keep Category — the user may have chosen it independently; don't reset it.
            // Rebuild the Item dropdown to match the still-selected category (or show
            // the "Select Category First" placeholder if no category is chosen).
            CategoryItem currentCat = row.Cells["colCategory"].Value as CategoryItem;
            if (currentCat == null)
            {
                var raw = row.Cells["colCategory"].Value?.ToString();
                if (!string.IsNullOrEmpty(raw))
                    currentCat = _categories.FirstOrDefault(c =>
                        string.Equals(c.Name, raw, StringComparison.OrdinalIgnoreCase));
            }

            var placeholderItem = new ItemItem
            {
                Id = null,
                Name = currentCat != null ? "-- Select Item --" : "-- Select Category First --",
                StockOnHand = 0,
                Amount = 0,
                IsAvailable = false,
                SerialNumber = null
            };
            var itemsForCell = new List<ItemItem> { placeholderItem };
            if (currentCat != null)
                itemsForCell.AddRange(_allItems.Where(x => x.CategoryId == currentCat.CategoryId));

            var itemCell = new DataGridViewComboBoxCell
            {
                DisplayMember = "DisplayName",
                ValueMember = null,
                ValueType = typeof(object),
                DataSource = itemsForCell
            };
            itemCell.Value = itemsForCell[0];
            row.Cells["colItem"] = itemCell;

            // Clear only the auto-filled item-specific fields
            row.Cells["colSerialNumber"].Value = string.Empty;
            row.Cells["colUnitPrice"].Value = 0m;
            row.Cells["colDescription"].Value = string.Empty;
            if (dgv.Columns.Contains("colFixedAsset"))
                row.Cells["colFixedAsset"].Value = string.Empty;

            // Keep Quantity — user may have already set it deliberately.
            // Do NOT touch Status, Date Requested, Remarks, checkbox (colSelect).

            dgv.InvalidateRow(rowIndex);

            if (rowIndex == dgv.CurrentRow?.Index && IsHandleCreated)
                BeginInvoke(new Action(() => BindDetailsPanel(dgv.CurrentRow)));
        }

        private void ResolveItemFromTypedValue(int rowIndex, string columnName)
        {
            if (rowIndex < 0 || rowIndex >= dgv.Rows.Count) return;

            var row = dgv.Rows[rowIndex];
            string modelNumber = row.Cells["colModelNumber"].Value?.ToString() ?? string.Empty;
            string serialNumber = row.Cells["colSerialNumber"].Value?.ToString() ?? string.Empty;

            // Don't process if both are empty
            if (string.IsNullOrWhiteSpace(modelNumber) && string.IsNullOrWhiteSpace(serialNumber))
                return;

            // Only search within the category the user already picked for this row
            CategoryItem selectedCategory = row.Cells["colCategory"].Value as CategoryItem;
            if (selectedCategory == null)
            {
                var raw = row.Cells["colCategory"].Value?.ToString();
                if (!string.IsNullOrEmpty(raw))
                    selectedCategory = _categories.FirstOrDefault(c =>
                        string.Equals(c.Name, raw, StringComparison.OrdinalIgnoreCase));
            }
            IEnumerable<ItemItem> categoryItems = _allItems;
            if (selectedCategory != null && selectedCategory.CategoryId > 0)
                categoryItems = _allItems.Where(x => x.CategoryId == selectedCategory.CategoryId);

            ItemItem resolvedItem = null;
            string warningMessage = null;

            // When the user edits the MODEL NUMBER field, resolve by model — ignore any
            // stale serial that may be left over from a previous item selection.
            // When the user edits the SERIAL NUMBER field, resolve by serial (original behaviour).
            if (columnName == "colModelNumber")
            {
                // Model-first: fall through to the model-number resolution block below.
                // Treat serial as empty so the serial-first branch is skipped.
                serialNumber = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(serialNumber))
            {
                resolvedItem = categoryItems.FirstOrDefault(x =>
                    string.Equals(x.SerialNumber, serialNumber, StringComparison.OrdinalIgnoreCase));

                if (resolvedItem == null)
                {
                    warningMessage = selectedCategory != null
                        ? $"Serial Number not found in category \"{selectedCategory.Name}\""
                        : "Serial Number not found";
                }
                else if (!string.IsNullOrWhiteSpace(modelNumber) &&
                         !string.Equals(resolvedItem.ModelNumber, modelNumber, StringComparison.OrdinalIgnoreCase))
                {
                    warningMessage = "Serial Number does not match the selected model";
                }
            }
            else if (!string.IsNullOrWhiteSpace(modelNumber))
            {
                var matchingItems = categoryItems
                    .Where(x => string.Equals(x.ModelNumber, modelNumber, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matchingItems.Count == 0)
                {
                    warningMessage = selectedCategory != null
                        ? $"Model Number not found in category \"{selectedCategory.Name}\""
                        : "Model Number not found";
                }
                else if (matchingItems.Count == 1)
                {
                    resolvedItem = matchingItems[0];
                }
                else
                {
                    // Multiple items with same model - check distinct serial numbers (only available items)
                    var distinctSerials = matchingItems
                        .Where(x => !string.IsNullOrWhiteSpace(x.SerialNumber) && x.IsAvailable)  // Only available items
                        .Select(x => x.SerialNumber)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (distinctSerials.Count == 0)
                    {
                        // No available serial numbers - check if there's at least one available item without serial
                        var availableItem = matchingItems.FirstOrDefault(x => x.IsAvailable);
                        if (availableItem != null)
                        {
                            resolvedItem = availableItem;
                        }
                        else
                        {
                            warningMessage = "No available items for this model (all requested or archived)";
                        }
                    }
                    else if (distinctSerials.Count == 1)
                    {
                        // Only one distinct available serial number, safe to auto-fill
                        resolvedItem = matchingItems.First(x =>
                            !string.IsNullOrWhiteSpace(x.SerialNumber) &&
                            string.Equals(x.SerialNumber, distinctSerials[0], StringComparison.OrdinalIgnoreCase) &&
                            x.IsAvailable);
                    }
                    else
                    {
                        // Multiple distinct available serial numbers - DO NOT auto-fill Serial Number
                        // Auto-fill other fields based on first matching available item, but leave Serial Number empty
                        resolvedItem = matchingItems.FirstOrDefault(x => x.IsAvailable);

                        if (resolvedItem != null)
                        {
                            // Mark that we should NOT auto-fill serial number and should show autocomplete
                            warningMessage = "Cannot auto-fill serial number: more than one serial number is associated with this model";

                            // Auto-fill other fields but NOT Serial Number
                            AutoFillFromResolvedItemExceptSerial(row, resolvedItem);

                            // Show autocomplete for Serial Number and move focus there (only available serials)
                            ShowSerialNumberAutocompleteForModel(rowIndex, modelNumber, distinctSerials);

                            // Return early since we've handled this case specially
                            return;
                        }
                        else
                        {
                            warningMessage = "No available items for this model (all requested or archived)";
                        }
                    }
                }
            }

            // Auto-fill if item resolved
            if (resolvedItem != null && resolvedItem.Id.HasValue)
            {
                AutoFillFromResolvedItem(row, resolvedItem);
            }

            // Show non-blocking warning
            if (!string.IsNullOrWhiteSpace(warningMessage))
            {
                ShowNonBlockingWarning(warningMessage);
            }
        }

        private void AutoFillFromResolvedItem(DataGridViewRow row, ItemItem item)
        {
            // Auto-fill Category — suppress the "auto-open Item dropdown" side-effect
            // that fires from Dgv_CellValueChanged when setting category programmatically.
            var category = _categories.FirstOrDefault(c => c.CategoryId == item.CategoryId);
            if (category != null)
            {
                row.Cells["colCategory"].Value = category;

                // Update Item dropdown for this row
                var itemsForRow = _allItems.Where(x => x.CategoryId == category.CategoryId).ToList();
                var placeholderItem = new ItemItem
                {
                    Id = null,
                    Name = "-- Select Item --",
                    StockOnHand = 0,
                    Amount = 0,
                    IsAvailable = false,
                    SerialNumber = null
                };
                var itemsDataSource = new List<ItemItem> { placeholderItem };
                itemsDataSource.AddRange(itemsForRow);

                var colIndex = dgv.Columns["colItem"].Index;
                var newItemCell = new DataGridViewComboBoxCell
                {
                    DisplayMember = "DisplayName",
                    ValueMember = null,
                    ValueType = typeof(object),
                    DataSource = itemsDataSource
                };
                newItemCell.Value = item;
                row.Cells[colIndex] = newItemCell;
            }

            // Auto-fill Item
            row.Cells["colItem"].Value = item;

            // Auto-fill Quantity (default to 1 if empty)
            if (row.Cells["colQuantity"].Value == null ||
                string.IsNullOrWhiteSpace(row.Cells["colQuantity"].Value.ToString()) ||
                Convert.ToInt32(row.Cells["colQuantity"].Value) == 0)
            {
                row.Cells["colQuantity"].Value = 1;
            }

            // Auto-fill Unit Price
            row.Cells["colUnitPrice"].Value = item.Amount;

            // Always overwrite both serial and model from the resolved item so the
            // lookup works like Excel — type either field and the other auto-fills.
            // Setting cell values programmatically does NOT trigger Dgv_CellEndEdit,
            // so there is no cascading re-resolution risk.
            row.Cells["colSerialNumber"].Value = item.SerialNumber ?? string.Empty;
            row.Cells["colModelNumber"].Value = item.ModelNumber ?? string.Empty;

            // Auto-fill Description (from item name)
            row.Cells["colDescription"].Value = item.Name;

            // Auto-fill Fixed Asset
            if (dgv.Columns.Contains("colFixedAsset"))
            {
                row.Cells["colFixedAsset"].Value = item.IsTrackedAsset ? "Yes" : "No";
            }

            // Never auto-fill Status (user must select manually)

            dgv.InvalidateRow(row.Index);

            // Sync details panel if this is the current row
            if (row.Index == dgv.CurrentRow?.Index && IsHandleCreated)
                BeginInvoke(new Action(() => BindDetailsPanel(dgv.CurrentRow)));
        }

        private void AutoFillFromResolvedItemExceptSerial(DataGridViewRow row, ItemItem item)
        {
            // Auto-fill Category — suppress auto-open side-effect
            var category = _categories.FirstOrDefault(c => c.CategoryId == item.CategoryId);
            if (category != null)
            {
                row.Cells["colCategory"].Value = category;

                // Update Item dropdown for this row
                var itemsForRow = _allItems.Where(x => x.CategoryId == category.CategoryId).ToList();
                var placeholderItem = new ItemItem
                {
                    Id = null,
                    Name = "-- Select Item --",
                    StockOnHand = 0,
                    Amount = 0,
                    IsAvailable = false,
                    SerialNumber = null
                };
                var itemsDataSource = new List<ItemItem> { placeholderItem };
                itemsDataSource.AddRange(itemsForRow);

                var colIndex = dgv.Columns["colItem"].Index;
                var newItemCell = new DataGridViewComboBoxCell
                {
                    DisplayMember = "DisplayName",
                    ValueMember = null,
                    ValueType = typeof(object),
                    DataSource = itemsDataSource
                };

                // Set a placeholder item instead of the actual item since we don't know which serial to use
                newItemCell.Value = placeholderItem;
                row.Cells[colIndex] = newItemCell;
            }

            // Auto-fill Quantity (default to 1 if empty)
            if (row.Cells["colQuantity"].Value == null ||
                string.IsNullOrWhiteSpace(row.Cells["colQuantity"].Value.ToString()) ||
                Convert.ToInt32(row.Cells["colQuantity"].Value) == 0)
            {
                row.Cells["colQuantity"].Value = 1;
            }

            // Auto-fill Unit Price (use the item's price, assuming same across serial numbers)
            row.Cells["colUnitPrice"].Value = item.Amount;

            // Auto-fill Model Number if not already set (keep user's typed value)
            if (string.IsNullOrWhiteSpace(row.Cells["colModelNumber"].Value?.ToString()))
            {
                row.Cells["colModelNumber"].Value = item.ModelNumber ?? string.Empty;
            }

            // DO NOT auto-fill Serial Number - leave it empty for user to select
            row.Cells["colSerialNumber"].Value = string.Empty;

            // Auto-fill Description (from item name)
            row.Cells["colDescription"].Value = item.Name;

            // Auto-fill Fixed Asset
            if (dgv.Columns.Contains("colFixedAsset"))
            {
                row.Cells["colFixedAsset"].Value = item.IsTrackedAsset ? "Yes" : "No";
            }

            // Never auto-fill Status (user must select manually)

            dgv.InvalidateRow(row.Index);

            // Sync details panel if this is the current row
            if (row.Index == dgv.CurrentRow?.Index && IsHandleCreated)
                BeginInvoke(new Action(() => BindDetailsPanel(dgv.CurrentRow)));
        }

        private void ShowSerialNumberAutocompleteForModel(int rowIndex, string modelNumber, List<string> serialNumbers)
        {
            if (rowIndex < 0 || rowIndex >= dgv.Rows.Count) return;

            // Show non-blocking warning first
            ShowNonBlockingWarning("Cannot auto-fill serial number: more than one serial number is associated with this model");

            // Move focus to Serial Number cell and trigger edit mode
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var serialColIndex = dgv.Columns["colSerialNumber"].Index;
                        dgv.CurrentCell = dgv.Rows[rowIndex].Cells[serialColIndex];
                        dgv.BeginEdit(true);

                        // Get the editing control (TextBox)
                        if (dgv.EditingControl is TextBox textBox)
                        {
                            // Show autocomplete with filtered serial numbers for this model
                            if (serialNumbers != null && serialNumbers.Count > 0)
                            {
                                _autocompleteRowIndex = rowIndex;
                                _lastAutocompleteColumn = "colSerialNumber";
                                ShowAutocomplete(serialNumbers, textBox);
                            }
                        }
                    }
                    catch
                    {
                        // Ignore errors if focus/edit fails
                    }
                }));
            }
        }

        private void ShowNonBlockingWarning(string message)
        {
            // Use a tooltip-style notification instead of blocking MessageBox
            System.Media.SystemSounds.Beep.Play();

            // You could also use a ToolTip or status label here
            // For now, we'll use a brief, non-modal message
            var tooltip = new ToolTip
            {
                ToolTipIcon = ToolTipIcon.Warning,
                ToolTipTitle = "Warning",
                IsBalloon = true
            };

            if (dgv.CurrentCell != null)
            {
                var cellRect = dgv.GetCellDisplayRectangle(dgv.CurrentCell.ColumnIndex, dgv.CurrentCell.RowIndex, false);
                var screenPoint = dgv.PointToScreen(new Point(cellRect.Left, cellRect.Bottom));
                tooltip.Show(message, dgv, dgv.PointToClient(screenPoint), 3000);
            }
        }

        #endregion

        private void Combo_DrawItem(object sender, DrawItemEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo == null) return;

            if (e.Index < 0)
            {
                e.DrawBackground();
                return;
            }

            var obj = combo.Items[e.Index] as ItemItem;
            string text = obj?.DisplayName ?? obj?.Name ?? combo.Items[e.Index].ToString();

            // Check aggregate stock across all serials of the same model
            bool isOutOfStock = false;
            if (obj != null && obj.Id.HasValue)
            {
                var totalStock = _allItems
                    .Where(x => x.Name == obj.Name && x.CategoryId == obj.CategoryId && x.StockOnHand > 0)
                    .Sum(x => x.StockOnHand);
                isOutOfStock = totalStock == 0;
            }

            if (isOutOfStock)
            {
                text += " [Out of Stock]";
            }
            else if (obj != null && obj.Id.HasValue && !obj.IsAvailable)
            {
                text += " [Already Requested]";
            }

            e.DrawBackground();
            var color = (isOutOfStock || (obj != null && obj.Id.HasValue && !obj.IsAvailable)) ? Color.Gray : e.ForeColor;
            using (var brush = new SolidBrush(color))
            {
                e.Graphics.DrawString(text, e.Font, brush, e.Bounds);
            }
            e.DrawFocusRectangle();
        }

        private void Combo_SelectionChangeCommitted(object sender, EventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo == null) return;

            if (combo.SelectedItem is ItemItem sel && sel.Id.HasValue)
            {
                // Optional warning when the item has already been requested, but do not block selection.
                if (!sel.IsAvailable)
                {
                    // Show warning but allow selection
                    var result = MessageBox.Show(
                        $"Warning: Item '{sel.Name}' has already been requested.\n\n" +
                        "Do you still want to request it again?\n\n" +
                        "Note: This may fail if the database doesn't allow duplicate requests.",
                        "Already Requested",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    // User can still choose to back out by manually changing the selection.
                }

                // Always update the bound row's UnitPrice, SerialNumber, ModelNumber, and Description when an item is selected
                if (dgv.CurrentCell != null)
                {
                    var row = dgv.Rows[dgv.CurrentCell.RowIndex];

                    if (dgv.Columns.Contains("colUnitPrice"))
                    {
                        row.Cells["colUnitPrice"].Value = sel.Amount;
                    }

                    if (dgv.Columns.Contains("colSerialNumber"))
                    {
                        row.Cells["colSerialNumber"].Value = sel.SerialNumber ?? string.Empty;
                    }

                    if (dgv.Columns.Contains("colModelNumber"))
                    {
                        row.Cells["colModelNumber"].Value = sel.ModelNumber ?? string.Empty;
                    }

                    if (dgv.Columns.Contains("colDescription"))
                    {
                        row.Cells["colDescription"].Value = sel.Name ?? string.Empty;
                    }

                    // Refresh the cells to ensure immediate display
                    dgv.InvalidateRow(dgv.CurrentCell.RowIndex);

                    // Sync details panel
                    if (IsHandleCreated)
                        BeginInvoke(new Action(() => BindDetailsPanel(dgv.CurrentRow)));
                }
            }
        }

        private void BtnRemoveRow_Click(object sender, EventArgs e)
        {
            // Find all checked rows (excluding new row)
            var checkedRows = new List<DataGridViewRow>();

            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                var row = dgv.Rows[i];
                if (row.IsNewRow) continue;

                var checkValue = row.Cells["colSelect"].Value;
                bool isChecked = checkValue != null && (bool)checkValue == true;

                if (isChecked)
                {
                    checkedRows.Add(row);
                }
            }

            // If no rows are checked, show warning
            if (checkedRows.Count == 0)
            {
                MessageBox.Show(
                    "Please select at least one row to remove.",
                    "No Rows Selected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Confirm deletion
            string message = checkedRows.Count == 1
                ? "Are you sure you want to remove the selected row?"
                : $"Are you sure you want to remove the {checkedRows.Count} selected rows?";

            var result = MessageBox.Show(
                message,
                "Confirm Deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                return;
            }

            // Remove checked rows (iterate backwards to avoid index issues)
            for (int i = checkedRows.Count - 1; i >= 0; i--)
            {
                dgv.Rows.Remove(checkedRows[i]);
            }

            // Uncheck all remaining rows
            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                var row = dgv.Rows[i];
                if (!row.IsNewRow && dgv.Columns.Contains("colSelect"))
                {
                    row.Cells["colSelect"].Value = false;
                }
            }

            // Update the Select All checkbox state
            UpdateSelectAllCheckBoxState();

            dgv.Refresh();
        }

        private void BtnAddEmployee_Click(object sender, EventArgs e)
        {
            try
            {
                // Try to reuse your existing quick add dialog. Replace name if your add-employee dialog has a different class.
                using (var dialog = new QuickAddEmployeeDialog())
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        // Attempt to read new id if dialog exposes it
                        object newIdObj = null;
                        try
                        {
                            var prop = dialog.GetType().GetProperty("NewEmployeeId");
                            if (prop != null) newIdObj = prop.GetValue(dialog);
                        }
                        catch { /* ignore reflection issues */ }

                        // reload employees and rebind
                        LoadEmployees();

                        if (newIdObj is int newId)
                        {
                            // Try to select this employee in the focused row's employee cell, or the first row
                            var col = dgv.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.Name == "colEmployee") as DataGridViewComboBoxColumn;
                            if (col != null)
                            {
                                // find the EmployeeItem in our list
                                var match = _employees.FirstOrDefault(x => x.Id.HasValue && x.Id.Value == newId);
                                if (match != null)
                                {
                                    // choose current row if exists else first row
                                    var target = dgv.CurrentRow ?? (dgv.Rows.Count > 0 ? dgv.Rows[0] : null);
                                    if (target != null)
                                    {
                                        target.Cells["colEmployee"].Value = match;
                                    }
                                }
                            }
                            else
                            {
                                // if you had a standalone ComboBox named cmbEmployee, set it
                                var cmbControl = this.Controls.Find("cmbEmployee", true).FirstOrDefault() as ComboBox;
                                if (cmbControl != null)
                                {
                                    for (int i = 0; i < cmbControl.Items.Count; i++)
                                    {
                                        var it = cmbControl.Items[i] as EmployeeItem;
                                        if (it != null && it.Id.HasValue && it.Id.Value == newId)
                                        {
                                            cmbControl.SelectedIndex = i;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.IO.FileNotFoundException)
            {
                // QuickAddEmployeeDialog class not found in this build \u2014 show a friendly message.
                MessageBox.Show("Add Employee dialog not found in this project. Please ensure QuickAddEmployeeDialog exists or update BtnAddEmployee_Click to open your add-employee form.", "Not Implemented", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add employee: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearValidationHighlights()
        {
            for (int r = 0; r < dgv.Rows.Count; r++)
            {
                var row = dgv.Rows[r];
                if (row.IsNewRow) continue;
                foreach (DataGridViewCell cell in row.Cells)
                {
                    cell.Style.BackColor = Color.Empty;
                }
            }
            dgv.InvalidateColumn(dgv.Columns["colCategory"].Index);
            dgv.InvalidateColumn(dgv.Columns["colItem"].Index);
            dgv.InvalidateColumn(dgv.Columns["colQuantity"].Index);
        }

        private void HighlightCell(DataGridViewCell cell)
        {
            cell.Style.BackColor = InvalidCellColor;
        }

        private void SaveBatch()
        {
            // 1) Commit any in-progress edit so the latest selection is available in cells
            try
            {
                dgv.EndEdit();
                dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
            catch
            {
                // swallow - EndEdit may throw if control not in edit-mode, not critical
            }

            // Clear previous validation highlights
            ClearValidationHighlights();

            // 2) Ensure serial numbers are populated from selected items before validation
            for (int r = 0; r < dgv.Rows.Count; r++)
            {
                var row = dgv.Rows[r];
                if (row.IsNewRow) continue;

                var itemCellVal = row.Cells["colItem"].Value;
                var itemObj = itemCellVal as ItemItem;
                
                // If we have a valid item selected, ensure its serial number is in the Serial Number column
                if (itemObj != null && itemObj.Id.HasValue && dgv.Columns.Contains("colSerialNumber"))
                {
                    row.Cells["colSerialNumber"].Value = itemObj.SerialNumber ?? string.Empty;
                }
            }

            var requests = new List<RequestDto>();
            var validationErrors = new List<string>();
            DataGridViewCell firstInvalidCell = null;

            // ── Resolve batch-level requester (employee OR org-unit) ─────────
            EmployeeItem batchEmp = null;
            int? batchComId = null, batchDeptId = null, batchBranchId = null;
            int? batchDistributorId = null;
            string batchCompanyName = null, batchDeptName = null, batchBranchName = null;
            string batchDistributorName = null;

            if (_chkNoEmployee != null && _chkNoEmployee.Checked)
            {
                // Org-unit mode: Company OR Distributor required (distributors are
                // independent of Company/Department/Branch). Dept/Branch optional.
                var selCompany = _cmbCompany?.SelectedItem as OrgItem;
                if (selCompany != null && selCompany.Id > 0)
                {
                    batchComId = selCompany.Id;
                    batchCompanyName = selCompany.Name;
                }

                var selDept = _cmbDepartment?.SelectedItem as OrgItem;
                if (selDept != null && selDept.Id > 0)
                {
                    batchDeptId = selDept.Id;
                    batchDeptName = selDept.Name;
                }

                var selBranch = _cmbBranch?.SelectedItem as OrgItem;
                if (selBranch != null && selBranch.Id > 0)
                {
                    batchBranchId = selBranch.Id;
                    batchBranchName = selBranch.Name;
                }

                var selDistributor = _cmbDistributor?.SelectedItem as OrgItem;
                if (selDistributor != null && selDistributor.Id > 0)
                {
                    batchDistributorId = selDistributor.Id;
                    batchDistributorName = selDistributor.Name;
                }

                if (!batchComId.HasValue && !batchDistributorId.HasValue)
                {
                    _cmbCompany.BackColor = InvalidCellColor;
                    if (_cmbDistributor != null)
                        _cmbDistributor.BackColor = InvalidCellColor;
                    MessageBox.Show("Please select a Company or a Distributor for this batch.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _cmbCompany?.Focus();
                    return;
                }
                _cmbCompany.BackColor = SystemColors.Window;
                if (_cmbDistributor != null)
                    _cmbDistributor.BackColor = SystemColors.Window;
            }
            else
            {
                // Employee mode: employee required
                batchEmp = cmbEmployee?.Tag as EmployeeItem;
                if (batchEmp == null || !batchEmp.Id.HasValue)
                    batchEmp = cmbEmployee?.SelectedItem as EmployeeItem;
                if (batchEmp == null || !batchEmp.Id.HasValue)
                {
                    var typedText = cmbEmployee?.Text?.Trim();
                    if (!string.IsNullOrEmpty(typedText))
                        batchEmp = _employees.FirstOrDefault(emp => emp.Id.HasValue &&
                            string.Equals(emp.DisplayText, typedText, StringComparison.OrdinalIgnoreCase));
                }
                if (batchEmp == null || !batchEmp.Id.HasValue)
                {
                    cmbEmployee.BackColor = InvalidCellColor;
                    MessageBox.Show("Please select an employee for this batch.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cmbEmployee?.Focus();
                    return;
                }
                cmbEmployee.BackColor = SystemColors.Window;
            }

            for (int r = 0; r < dgv.Rows.Count; r++)
            {
                var row = dgv.Rows[r];

                // Skip the new row (if AllowUserToAddRows was true elsewhere)
                if (row.IsNewRow) continue;

                // Get cell values defensively (the cell.Value could be the helper object, or sometimes string)
                object catCellVal = row.Cells["colCategory"].Value;
                object itemCellVal = row.Cells["colItem"].Value;
                object qtyObj = row.Cells["colQuantity"].Value;
                object unitPriceObj = row.Cells["colUnitPrice"].Value;
                object dateRequestedObj = row.Cells["colDateRequested"].Value;
                string serialNumberCell = dgv.Columns.Contains("colSerialNumber")
                    ? row.Cells["colSerialNumber"].Value?.ToString() ?? string.Empty
                    : string.Empty;
                string desc = row.Cells["colDescription"].Value?.ToString() ?? string.Empty;
                string remarks = row.Cells["colRemarks"].Value?.ToString() ?? string.Empty;
                string status = row.Cells["colStatus"].Value?.ToString() ?? "Under Review";

                bool rowHasError = false;

                // Category
                CategoryItem cat = catCellVal as CategoryItem;
                if (cat == null)
                {
                    var catName = catCellVal?.ToString();
                    if (!string.IsNullOrEmpty(catName))
                        cat = _categories.FirstOrDefault(x => string.Equals(x.Name, catName, StringComparison.OrdinalIgnoreCase));
                }

                if (cat == null)
                {
                    HighlightCell(row.Cells["colCategory"]);
                    validationErrors.Add($"Row {r + 1}: Category is required.");
                    if (firstInvalidCell == null) firstInvalidCell = row.Cells["colCategory"];
                    rowHasError = true;
                }

                // Item
                ItemItem item = itemCellVal as ItemItem;
                if (item == null)
                {
                    var itemDisplay = itemCellVal?.ToString();
                    if (!string.IsNullOrEmpty(itemDisplay))
                    {
                        // Parse DisplayName format: "Name (SN: SerialNumber)" or just "Name"
                        string itemName = itemDisplay;
                        string extractedSerial = null;
                        
                        // Check if DisplayName format with serial number
                        if (itemDisplay.Contains(" (SN: ") && itemDisplay.EndsWith(")"))
                        {
                            var parts = itemDisplay.Split(new[] { " (SN: " }, StringSplitOptions.None);
                            if (parts.Length == 2)
                            {
                                itemName = parts[0];
                                extractedSerial = parts[1].TrimEnd(')');
                            }
                        }
                        
                        // Prefer to resolve by SerialNumber when available
                        if (!string.IsNullOrWhiteSpace(extractedSerial))
                        {
                            item = _allItems.FirstOrDefault(x =>
                                string.Equals(x.SerialNumber ?? string.Empty, extractedSerial, StringComparison.OrdinalIgnoreCase));
                        }
                        
                        // Also try the serialNumberCell if available
                        if (item == null && !string.IsNullOrWhiteSpace(serialNumberCell))
                        {
                            item = _allItems.FirstOrDefault(x =>
                                string.Equals(x.SerialNumber ?? string.Empty, serialNumberCell, StringComparison.OrdinalIgnoreCase));
                        }

                        // If no serial match, fall back to name + category
                        if (item == null && cat != null)
                        {
                            item = _allItems.FirstOrDefault(x =>
                                string.Equals(x.Name, itemName, StringComparison.OrdinalIgnoreCase) &&
                                x.CategoryId == cat.CategoryId);
                        }

                        // Final fallback: name only
                        if (item == null)
                        {
                            item = _allItems.FirstOrDefault(x =>
                                string.Equals(x.Name, itemName, StringComparison.OrdinalIgnoreCase));
                        }
                    }
                }

                if (item == null || !item.Id.HasValue)
                {
                    HighlightCell(row.Cells["colItem"]);
                    validationErrors.Add($"Row {r + 1}: Item is required.");
                    if (firstInvalidCell == null) firstInvalidCell = row.Cells["colItem"];
                    rowHasError = true;
                }

                // Quantity
                if (!int.TryParse(qtyObj?.ToString() ?? "0", out int qty) || qty <= 0)
                {
                    HighlightCell(row.Cells["colQuantity"]);
                    validationErrors.Add($"Row {r + 1}: Quantity must be > 0.");
                    if (firstInvalidCell == null) firstInvalidCell = row.Cells["colQuantity"];
                    rowHasError = true;
                }

                if (rowHasError) continue; // Skip building DTO for invalid rows

                // Available stock at the moment of encoding is the only reliable signal we
                // have for how much of this request can actually be issued — there is no
                // separate portal/fulfillment step for non-cartridge requests. Items are
                // still allowed through with 0 available stock (common for Ink/Toner/Print
                // Head consumables) — they're just recorded as fully unfulfilled.
                // Prefer the formal ConsumableModel grouping when the item has one assigned
                // (Ink/Toner/Print Head) — summing by Name+CategoryId alone silently pools
                // stock across any accidental duplicate catalog rows for the same product.
                var availableStock = item.ConsumableModelId.HasValue
                    ? _allItems.Where(x => x.ConsumableModelId == item.ConsumableModelId && x.StockOnHand > 0).Sum(x => x.StockOnHand)
                    : _allItems.Where(x => x.Name == item.Name && x.CategoryId == item.CategoryId && x.StockOnHand > 0).Sum(x => x.StockOnHand);

                // UnitPrice
                if (!decimal.TryParse(unitPriceObj?.ToString() ?? "0", out decimal unitPrice))
                {
                    unitPrice = 0;
                }
                DateTime dateRequested = dateRequestedObj is DateTime dt ? dt : DateTime.Now;

                string entryType = "Negative"; // Always Negative for new requests

                var dto = new RequestDto
                {
                    EmpId = batchEmp?.Id,
                    EmployeeName = batchEmp?.Name,
                    EmployeePosition = batchEmp?.Position,
                    ComId = batchComId,
                    DeptId = batchDeptId,
                    BranchId = batchBranchId,
                    DistributorId = batchDistributorId,
                    CompanyName = batchCompanyName,
                    DepartmentName = batchDeptName,
                    BranchName = batchBranchName,
                    DistributorName = batchDistributorName,
                    ItemId = item.Id.Value,
                    ItemName = item.Name,
                    Quantity = qty,
                    IssuedQty = Math.Min(qty, availableStock),
                    UnitPrice = unitPrice,
                    DateRequested = dateRequested,
                    Description = desc,
                    Remarks = remarks,
                    Status = status,
                    EntryType = entryType,
                    DateCreated = DateTime.Now,
                    CreatedByUserId = AppSession.CurrentUserId,
                    ModifiedByUserId = AppSession.CurrentUserId,
                    CreatedByName = AppSession.CurrentUserName,
                    SerialNumber = serialNumberCell
                };

                requests.Add(dto);
            }

            // Show all validation errors at once with inline highlights
            if (validationErrors.Count > 0)
            {
                dgv.Refresh(); // Ensure highlights are visible
                if (firstInvalidCell != null)
                {
                    try { dgv.CurrentCell = firstInvalidCell; } catch { }
                }
                string summary = string.Join("\n", validationErrors);
                MessageBox.Show($"Please fix the following issues:\n\n{summary}",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (requests.Count == 0)
            {
                MessageBox.Show("No requests to save.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Persist requests
            try
            {
                Cursor = Cursors.WaitCursor;
                var repo = new RequestRepository();

                var insertedIds = new List<int>();
                foreach (var r in requests)
                {
                    int newId = repo.AddRequest(r); // AddRequest returns inserted ReqId via SCOPE_IDENTITY()
                    if (newId <= 0)
                    {
                        // If add returned 0 or -1 treat as failure for diagnostics
                        MessageBox.Show($"Insert returned unexpected id = {newId} for item {r.ItemName}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        insertedIds.Add(newId);
                    }
                }

                Cursor = Cursors.Default;

                if (insertedIds.Count > 0)
                {
                    MessageBox.Show($"Successfully inserted {insertedIds.Count} request(s).", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("No requests were inserted. Check the data and try again.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show($"Failed saving requests: {ex.Message}\n{ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }


        // ── Details Panel ──────────────────────────────────────────────────────────

        private Panel BuildDetailsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 249, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8, 6, 8, 6),
                Margin = new Padding(0, 8, 0, 0),
                Height = 78
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                AutoSize = false
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var lblDesc = new Label
            {
                Text = "Description:",
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80),
                Margin = new Padding(0, 0, 8, 0),
                Dock = DockStyle.Fill
            };

            _txtDescription = new TextBox
            {
                ReadOnly = true,
                Multiline = true,
                ScrollBars = ScrollBars.None,
                BackColor = Color.FromArgb(240, 240, 240),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 16, 0),
                TabStop = false
            };

            var lblRemarks = new Label
            {
                Text = "Remarks:",
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80),
                Margin = new Padding(0, 0, 8, 0),
                Dock = DockStyle.Fill
            };

            _txtRemarks = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.None,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Enabled = false
            };
            _txtRemarks.TextChanged += TxtRemarks_TextChanged;

            tbl.Controls.Add(lblDesc, 0, 0);
            tbl.Controls.Add(_txtDescription, 1, 0);
            tbl.Controls.Add(lblRemarks, 2, 0);
            tbl.Controls.Add(_txtRemarks, 3, 0);

            panel.Controls.Add(tbl);
            return panel;
        }

        /// <summary>
        /// Loads Description and Remarks from the given DataGridView row into the details panel.
        /// Pass null or a new-row to clear and disable.
        /// </summary>
        private void BindDetailsPanel(DataGridViewRow row)
        {
            if (_txtDescription == null || _txtRemarks == null) return;

            _suppressDetailsPanelSync = true;
            try
            {
                if (row == null || row.IsNewRow)
                {
                    _txtDescription.Text = string.Empty;
                    _txtRemarks.Text = string.Empty;
                    _txtRemarks.Enabled = false;
                }
                else
                {
                    _txtDescription.Text = row.Cells["colDescription"].Value?.ToString() ?? string.Empty;
                    _txtRemarks.Text = row.Cells["colRemarks"].Value?.ToString() ?? string.Empty;
                    _txtRemarks.Enabled = true;
                }
            }
            finally
            {
                _suppressDetailsPanelSync = false;
            }
        }

        private void TxtRemarks_TextChanged(object sender, EventArgs e)
        {
            if (_suppressDetailsPanelSync) return;
            var row = dgv.CurrentRow;
            if (row == null || row.IsNewRow) return;
            row.Cells["colRemarks"].Value = _txtRemarks.Text;
        }

        #region Helper classes and repo stub

        private class OrgItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private class EmployeeItem
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public string EmployeeNumber { get; set; }
            public string Position { get; set; }

            public string DisplayText
            {
                get
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(EmployeeNumber))
                        parts.Add(EmployeeNumber);
                    parts.Add(Name ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(Position))
                        parts.Add(Position);
                    return string.Join(" – ", parts);
                }
            }

            public override string ToString() => DisplayText;
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
            public string CategoryName { get; set; }  // Category field from Item table
            public string SerialNumber { get; set; }   // Serial number from Item table
            public string ModelNumber { get; set; }    // Model number from Item table
            public decimal Amount { get; set; }        // Item price/amount
            public bool IsAvailable { get; set; }      // Can this item be requested?
            public bool IsTrackedAsset { get; set; }   // Fixed Asset flag
            public int? ConsumableModelId { get; set; } // Ink/Toner/Print Head model grouping

            public string DisplayName
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(SerialNumber))
                        return Name;
                    return $"{Name} (SN: {SerialNumber})";
                }
            }
            
            public override string ToString() => DisplayName;
        }

        // Minimal RequestRepository stub
        // (Assumes your existing RequestRepository implementation remains the same and is available in the project)
        #endregion

        #region CalendarColumn for DateTimePicker in DataGridView

        // Small calendar column implementation so DateRequested can be edited inline.
        private class CalendarColumn : DataGridViewColumn
        {
            public CalendarColumn() : base(new CalendarCell()) { }
            public override DataGridViewCell CellTemplate
            {
                get => base.CellTemplate;
                set
                {
                    if (value != null && !value.GetType().IsAssignableFrom(typeof(CalendarCell)))
                        throw new InvalidCastException("Must be a CalendarCell");
                    base.CellTemplate = value;
                }
            }
        }

        private class CalendarCell : DataGridViewTextBoxCell
        {
            public CalendarCell() : base() { Style.Format = "yyyy-MM-dd"; }

            public override void InitializeEditingControl(int rowIndex, object initialFormattedValue,
                DataGridViewCellStyle dataGridViewCellStyle)
            {
                base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
                var ctl = DataGridView.EditingControl as CalendarEditingControl;
                // Do not strip time — database requires full timestamp
                DateTime value = this.Value == null ? DateTime.Now : (DateTime)this.Value;
                ctl.Value = value;
            }

            public override Type EditType => typeof(CalendarEditingControl);
            public override Type ValueType => typeof(DateTime);
            // Do not strip time — database requires full timestamp
            public override object DefaultNewRowValue => DateTime.Now;
        }

        private class CalendarEditingControl : DateTimePicker, IDataGridViewEditingControl
        {
            DataGridView _grid;
            bool _valueChanged = false;
            int _rowIndex;

            public CalendarEditingControl()
            {
                Format = DateTimePickerFormat.Custom;
                CustomFormat = "yyyy-MM-dd";
                ShowUpDown = false;
            }

            public object EditingControlFormattedValue
            {
                get => Value.ToString("yyyy-MM-dd");
                set
                {
                    if (value is string s && DateTime.TryParse(s, out DateTime dt))
                        Value = dt;
                }
            }

            public object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context) => EditingControlFormattedValue;

            public void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle)
            {
                Font = dataGridViewCellStyle.Font;
                CalendarForeColor = dataGridViewCellStyle.ForeColor;
                CalendarMonthBackground = dataGridViewCellStyle.BackColor;
            }

            public int EditingControlRowIndex { get => _rowIndex; set => _rowIndex = value; }

            public bool EditingControlWantsInputKey(Keys key, bool dataGridViewWantsInputKey)
            {
                switch (key & Keys.KeyCode)
                {
                    case Keys.Left:
                    case Keys.Up:
                    case Keys.Down:
                    case Keys.Right:
                    case Keys.Home:
                    case Keys.End:
                    case Keys.PageDown:
                    case Keys.PageUp:
                        return true;
                    default:
                        return !dataGridViewWantsInputKey;
                }
            }

            public void PrepareEditingControlForEdit(bool selectAll) { }
            public bool RepositionEditingControlOnValueChange => false;

            public DataGridView EditingControlDataGridView { get => _grid; set => _grid = value; }

            public bool EditingControlValueChanged { get => _valueChanged; set => _valueChanged = value; }

            protected override void OnValueChanged(EventArgs eventargs)
            {
                _valueChanged = true;
                this.EditingControlDataGridView?.NotifyCurrentCellDirty(true);
                base.OnValueChanged(eventargs);
            }

            public Cursor EditingPanelCursor => base.Cursor;
        }

        #endregion
    }

    // Your DTO already exists in DTOs.cs; included here only for compile-time convenience if needed.

}

