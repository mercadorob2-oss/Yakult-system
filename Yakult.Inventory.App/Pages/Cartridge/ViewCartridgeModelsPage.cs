using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using HopeButton = ReaLTaiizor.Controls.HopeButton;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    public partial class ViewCartridgeModelsPage : UserControl
    {
        private DefaultListPageLayout _layout;
        private DataGridView dgvModels;
        private HopeButton _btnAdd, _btnArchive, _btnDelete;

        private readonly CartridgeModelRepository _repository;
        private List<CartridgeModelDto> _allModels = new List<CartridgeModelDto>();
        private List<CartridgeModelDto> _filteredModels = new List<CartridgeModelDto>();

        // Sorting state
        private DataGridViewColumn _sortColumn;
        private SortOrder _sortOrder = SortOrder.None;
        private bool _sortByDropdownInitialized = false;

        // Select-all header checkbox state
        private bool _selectAllChecked = false;

        public ViewCartridgeModelsPage()
        {
            _repository = new CartridgeModelRepository();
            InitializeComponent();
            BuildUiWithTemplate();
            LoadModelsAsync();
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
                "Cartridge Models",
                "Search by Model Number...",
                (s, e) => ApplyFilters(),
                () => { LoadModelsAsync(); });

            // Hook up Filter By event
            if (_layout.FilterByComboBox != null)
            {
                _layout.FilterByComboBox.SelectedIndexChanged += (s, e) =>
                {
                    _sortColumn = null;
                    _sortOrder = SortOrder.None;
                    ApplyFilters();
                };
            }

            _btnAdd = new HopeButton
            {
                Text = "➕  Add Model",
                Font = UiTheme.Fonts.Button,
                Size = new Size(170, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigurePillHopeButton(_btnAdd, UiTheme.Colors.Primary, UiTheme.Colors.PrimaryHover);
            _btnAdd.Click += BtnAdd_Click;

            _btnArchive = new HopeButton
            {
                Text = "📦  Archive",
                Font = UiTheme.Fonts.Button,
                Size = new Size(140, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(_btnArchive, UiTheme.Colors.PrimaryHover, UiTheme.Colors.OutlineHoverBack);
            _btnArchive.Click += BtnArchive_Click;

            _btnDelete = new HopeButton
            {
                Text = "🗑  Delete",
                Font = UiTheme.Fonts.Button,
                Size = new Size(130, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(_btnDelete, UiTheme.Colors.Danger, UiTheme.Colors.DangerHoverBack);
            _btnDelete.Click += BtnDelete_Click;

            DefaultListPageTemplate.AddButtons(_layout.ButtonLeftFlow, new[] { _btnAdd, _btnArchive, _btnDelete });

            if (_layout.SummaryPanel != null)
                _layout.SummaryPanel.Visible = false;

            if (_layout.PaginationPanel != null)
                _layout.PaginationPanel.Visible = false;

            dgvModels = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                RowHeadersVisible = false
            };

            UiFactory.StyleGrid(dgvModels);
            dgvModels.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvModels.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // Checkbox column for archive/delete multi-select
            dgvModels.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "colSelect",
                HeaderText = "",
                Width = 36,
                MinimumWidth = 36,
                Resizable = DataGridViewTriState.False,
                FillWeight = 5
            });

            dgvModels.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CartridgeModelId",
                HeaderText = "ID",
                FillWeight = 10,
                MinimumWidth = 60,
                ReadOnly = true
            });

            dgvModels.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ModelNumber",
                HeaderText = "Model Number",
                FillWeight = 22,
                MinimumWidth = 160,
                ReadOnly = true
            });

            dgvModels.Columns.Add(new DataGridViewCheckBoxColumn
            {
                DataPropertyName = "IsRequestable",
                HeaderText = "Requestable",
                FillWeight = 12,
                MinimumWidth = 100,
                ReadOnly = true
            });

            dgvModels.Columns.Add(new DataGridViewCheckBoxColumn
            {
                DataPropertyName = "IsRefillable",
                HeaderText = "Refillable",
                FillWeight = 12,
                MinimumWidth = 100,
                ReadOnly = true
            });

            dgvModels.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CreatedAt",
                HeaderText = "Created At",
                FillWeight = 14,
                MinimumWidth = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy" },
                ReadOnly = true
            });

            dgvModels.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CreatedByName",
                HeaderText = "Created By",
                FillWeight = 14,
                MinimumWidth = 120,
                ReadOnly = true
            });

            // Edit button column
            var editButtonColumn = new DataGridViewButtonColumn
            {
                Name = "EditButton",
                HeaderText = "Action",
                Text = "Edit",
                UseColumnTextForButtonValue = true,
                FillWeight = 10,
                MinimumWidth = 80
            };
            dgvModels.Columns.Add(editButtonColumn);

            // Add click event handler for edit button and checkbox column
            dgvModels.CellClick += DgvModels_CellClick;

            // Pill cell painting must be registered BEFORE the select-all checkbox painter.
            // AttachVendorsPillCellPainting paints ALL header cells (RowIndex < 0), so our
            // checkbox handler must run AFTER it to draw on top of the already-painted header.
            DefaultListPageTemplate.AttachVendorsPillCellPainting(dgvModels, c =>
            {
                if (c == null) return false;
                var p = c.DataPropertyName;
                return string.Equals(p, "CartridgeModelId", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p, "IsRequestable", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p, "IsRefillable", StringComparison.OrdinalIgnoreCase);
            });

            // Paint native select-all checkbox in colSelect header — registered AFTER the pill
            // painter so it draws on top of the already-painted header background.
            dgvModels.CellPainting += DgvModels_CellPainting;

            // Column header click sorting
            dgvModels.ColumnHeaderMouseClick += DgvModels_ColumnHeaderMouseClick;

            _layout.GridCard.Controls.Clear();
            _layout.GridCard.Controls.Add(dgvModels);

            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.SummaryPanel);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_layout.HeaderPanel);

            ResumeLayout(true);

            DefaultListPageTemplate.SetupInitialPageFocus(_layout.SearchBox, dgvModels, _btnAdd);
        }

        private async void LoadModelsAsync()
        {
            try
            {
                var models = await _repository.GetAllActiveModelsAsync();
                _allModels = models ?? new List<CartridgeModelDto>();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading cartridge models:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyFilters()
        {
            if (_layout?.SearchBox == null)
                return;

            string q = (_layout.SearchBox.Text ?? "").Trim();

            IEnumerable<CartridgeModelDto> query = _allModels;

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(x =>
                    !string.IsNullOrWhiteSpace(x.ModelNumber) && x.ModelNumber.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            _filteredModels = query.ToList();

            // --- Sorting ---
            string filterBy = _layout?.FilterByComboBox?.SelectedItem?.ToString();

            if (_sortColumn != null)
            {
                var prop = _sortColumn.DataPropertyName;
                var propInfo = typeof(CartridgeModelDto).GetProperty(prop);

                if (propInfo != null)
                {
                    if (_sortOrder == SortOrder.Ascending)
                        _filteredModels = _filteredModels.OrderBy(x => propInfo.GetValue(x, null)).ToList();
                    else
                        _filteredModels = _filteredModels.OrderByDescending(x => propInfo.GetValue(x, null)).ToList();
                }
            }
            else
            {
                if (filterBy == "Most Recently Added")
                {
                    _filteredModels = _filteredModels.OrderByDescending(x => x.CreatedAt).ToList();
                }
                else if (filterBy == "Oldest Added")
                {
                    _filteredModels = _filteredModels.OrderBy(x => x.CreatedAt).ToList();
                }
                else
                {
                    _filteredModels = _filteredModels
                        .OrderBy(x => x.ModelNumber ?? "", StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }

            dgvModels.DataSource = null;
            dgvModels.DataSource = _filteredModels;

            // DataSource rebind clears all cell values — reset select-all state accordingly
            _selectAllChecked = false;
            dgvModels.InvalidateColumn(dgvModels.Columns["colSelect"].Index);

            DefaultListPageTemplate.DisableDefaultRowHighlight(dgvModels, _btnAdd);

            // Initialize Sort By dropdown after first load
            if (!_sortByDropdownInitialized && _layout?.SortByComboBox != null && dgvModels != null && dgvModels.Columns.Count > 0)
            {
                DefaultListPageTemplate.SetupSortByDropdown(
                    _layout.SortByComboBox,
                    dgvModels,
                    (columnKey, direction) =>
                    {
                        var col = dgvModels.Columns.Cast<DataGridViewColumn>()
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
                    defaultColumnKey: "CartridgeModelId",
                    defaultDirection: SortOrder.Ascending
                );

                _sortByDropdownInitialized = true;
            }
        }

        private void DgvModels_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = dgvModels.Columns[e.ColumnIndex];

            // Select-all toggle for the colSelect checkbox column header
            if (col.Name == "colSelect")
            {
                _selectAllChecked = !_selectAllChecked;
                foreach (DataGridViewRow row in dgvModels.Rows)
                {
                    if (!row.IsNewRow)
                        row.Cells["colSelect"].Value = _selectAllChecked;
                }
                dgvModels.InvalidateColumn(e.ColumnIndex);
                return;
            }

            if (col is DataGridViewCheckBoxColumn || col is DataGridViewButtonColumn || col is DataGridViewImageColumn)
                return;

            if (_sortColumn == col)
            {
                _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                _sortColumn = col;
                _sortOrder = SortOrder.Ascending;
            }

            if (_layout?.SortByComboBox != null && _sortColumn != null)
            {
                DefaultListPageTemplate.SyncSortByDropdown(_layout.SortByComboBox, _sortColumn, _sortOrder);
            }

            ApplyFilters();
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            using (var dialog = CreateAddCartridgeModelDialog())
            {
                var result = dialog.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    LoadModelsAsync();
                }
            }
        }

        private void DgvModels_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // Ignore header row clicks
            if (e.RowIndex < 0)
                return;

            // Single-click checkbox toggle — bypass edit mode entirely by setting Value directly.
            // Root cause of the double-click bug: DataGridView requires a first click to select
            // the row (FullRowSelect) and a second click to enter edit mode before a checkbox
            // cell becomes interactive. Bypassing edit mode with a direct cell.Value assignment
            // commits immediately and updates the visual state in one click.
            if (dgvModels.Columns[e.ColumnIndex].Name == "colSelect")
            {
                var cell = dgvModels.Rows[e.RowIndex].Cells["colSelect"];
                cell.Value = !(bool)(cell.Value ?? false);
                UpdateSelectAllHeaderState();
                return;
            }

            // Check if the Edit button column was clicked
            if (dgvModels.Columns[e.ColumnIndex].Name == "EditButton")
            {
                var selectedModel = dgvModels.Rows[e.RowIndex].DataBoundItem as CartridgeModelDto;
                if (selectedModel != null)
                {
                    using (var dialog = new EditCartridgeModelDialog(selectedModel.CartridgeModelId))
                    {
                        var result = dialog.ShowDialog(this);
                        if (result == DialogResult.OK)
                        {
                            LoadModelsAsync();
                        }
                    }
                }
            }
        }

        // Draws a native OS checkbox in the colSelect column header.
        // CheckBoxRenderer uses visual styles so it matches the system theme.
        private void DgvModels_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex != dgvModels.Columns["colSelect"].Index)
                return;

            // Background already painted by the pill painter — just draw the checkbox on top.
            var state = _selectAllChecked
                ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;

            var glyphSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
            var pt = new Point(
                e.CellBounds.X + (e.CellBounds.Width  - glyphSize.Width)  / 2,
                e.CellBounds.Y + (e.CellBounds.Height - glyphSize.Height) / 2
            );

            CheckBoxRenderer.DrawCheckBox(e.Graphics, pt, state);
            e.Handled = true;
        }

        // Recalculates the select-all header checkbox state based on actual row values
        // and invalidates the header cell so it repaints.
        private void UpdateSelectAllHeaderState()
        {
            int total = 0, ticked = 0;
            foreach (DataGridViewRow row in dgvModels.Rows)
            {
                if (row.IsNewRow) continue;
                total++;
                if ((bool)(row.Cells["colSelect"].Value ?? false)) ticked++;
            }

            bool shouldBeChecked = total > 0 && ticked == total;
            if (_selectAllChecked != shouldBeChecked)
            {
                _selectAllChecked = shouldBeChecked;
                dgvModels.InvalidateColumn(dgvModels.Columns["colSelect"].Index);
            }
        }

        private List<CartridgeModelDto> GetCheckedModels()
        {
            var selected = new List<CartridgeModelDto>();
            for (int i = 0; i < dgvModels.Rows.Count; i++)
            {
                var row = dgvModels.Rows[i];
                if (row.IsNewRow) continue;
                var checkValue = row.Cells["colSelect"].Value;
                if (checkValue != null && (bool)checkValue == true && row.DataBoundItem is CartridgeModelDto model)
                    selected.Add(model);
            }
            return selected;
        }

        private void BtnArchive_Click(object sender, EventArgs e)
        {
            var selected = GetCheckedModels();

            if (selected.Count == 0)
            {
                MessageBox.Show("Please select at least one model to archive.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string message = selected.Count == 1
                ? $"Are you sure you want to archive \"{selected[0].ModelNumber}\"?"
                : $"Are you sure you want to archive the {selected.Count} selected cartridge models?";

            using (var archiveDialog = new Form())
            {
                archiveDialog.Text = "Archive Cartridge Model";
                archiveDialog.Size = new Size(500, 280);
                archiveDialog.StartPosition = FormStartPosition.CenterParent;
                archiveDialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                archiveDialog.MaximizeBox = false;
                archiveDialog.MinimizeBox = false;

                var lblMessage = new Label
                {
                    Text = message + "\n\nThe model(s) will be moved to the archive and will no longer appear in active lists.\n" +
                           "This action can be reviewed in the Archive page.",
                    AutoSize = false,
                    Size = new Size(460, 80),
                    Location = new Point(10, 10)
                };

                var lblReason = new Label
                {
                    Text = "Reason for archiving:",
                    AutoSize = true,
                    Location = new Point(10, 100)
                };

                var txtReason = new TextBox
                {
                    Size = new Size(460, 20),
                    Location = new Point(10, 120)
                };

                var chkDeactivate = new CheckBox
                {
                    Text = "Also mark as inactive (hide from dropdowns and requests)",
                    AutoSize = true,
                    Location = new Point(10, 150),
                    Checked = true
                };

                var btnConfirm = new Button
                {
                    Text = "Archive",
                    DialogResult = DialogResult.OK,
                    Location = new Point(290, 185),
                    Size = new Size(90, 28)
                };

                var btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(390, 185),
                    Size = new Size(90, 28)
                };

                archiveDialog.Controls.AddRange(new Control[] { lblMessage, lblReason, txtReason, chkDeactivate, btnConfirm, btnCancel });
                archiveDialog.AcceptButton = btnConfirm;
                archiveDialog.CancelButton = btnCancel;

                if (archiveDialog.ShowDialog() == DialogResult.OK)
                {
                    string reason = string.IsNullOrWhiteSpace(txtReason.Text) ? "No reason provided" : txtReason.Text.Trim();
                    bool deactivate = chkDeactivate.Checked;
                    ArchiveCartridgeModels(selected, reason, deactivate);
                }
            }
        }

        private async void ArchiveCartridgeModels(List<CartridgeModelDto> models, string reason, bool deactivate)
        {
            try
            {
                string archivedBy = AppSession.CurrentUserName ?? "System";
                foreach (var model in models)
                    await _repository.ArchiveAsync(model.CartridgeModelId, reason, deactivate, archivedBy);

                string msg = models.Count == 1
                    ? "Cartridge model archived successfully!"
                    : $"{models.Count} cartridge models archived successfully!";

                MessageBox.Show(msg + "\n\nYou can view archived records in the Archive page.",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                LoadModelsAsync();
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                MessageBox.Show($"Database error while archiving:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error archiving cartridge model:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnDelete_Click(object sender, EventArgs e)
        {
            var selected = GetCheckedModels();

            if (selected.Count == 0)
            {
                MessageBox.Show("Please select at least one model to delete.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Check dependencies for each selected model
            var withDeps = new List<(CartridgeModelDto model, int itemCount)>();
            var withoutDeps = new List<CartridgeModelDto>();
            var hardBlocked = new List<(CartridgeModelDto model, int emptyCartridgeCount, int batchLineCount)>();

            foreach (var model in selected)
            {
                var (hasItems, itemCount, emptyCartridgeCount, batchLineCount) = await _repository.CheckDependenciesAsync(model.CartridgeModelId);
                if (emptyCartridgeCount > 0 || batchLineCount > 0)
                    hardBlocked.Add((model, emptyCartridgeCount, batchLineCount));
                else if (hasItems)
                    withDeps.Add((model, itemCount));
                else
                    withoutDeps.Add(model);
            }

            if (hardBlocked.Count > 0)
            {
                string blockedList = string.Join("\n", hardBlocked.Select(x =>
                    $"  \"{x.model.ModelNumber}\" — {x.emptyCartridgeCount} EmptyCartridge record(s), {x.batchLineCount} vendor batch line record(s)"));

                MessageBox.Show(
                    $"{hardBlocked.Count} of the selected model(s) cannot be permanently deleted:\n\n{blockedList}\n\n" +
                    "These represent physical inventory / audit history and cannot be auto-unlinked. " +
                    "Resolve those records first (dispose, sell, or return them), or mark the model as Inactive instead.\n\n" +
                    (withDeps.Count + withoutDeps.Count > 0
                        ? $"The remaining {withDeps.Count + withoutDeps.Count} model(s) you selected are not affected — re-run delete with just those selected if you want to proceed with them."
                        : "None of the other selected models are eligible for deletion either."),
                    "Cannot Delete — Linked Inventory Records", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (withDeps.Count > 0)
            {
                // Some models have linked items — show choice dialog
                using (var choiceDialog = new Form())
                {
                    choiceDialog.Text = "Models Have Linked Items";
                    choiceDialog.Size = new Size(700, 510);
                    choiceDialog.StartPosition = FormStartPosition.CenterParent;
                    choiceDialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    choiceDialog.MaximizeBox = false;
                    choiceDialog.MinimizeBox = false;

                    int totalItems = withDeps.Sum(x => x.itemCount);

                    var lblWarning = new Label
                    {
                        Text = $"⚠️ WARNING: {withDeps.Count} selected model(s) have linked item records ⚠️\n\n" +
                               $"Selected models: {selected.Count} total\n" +
                               $"  {withoutDeps.Count} model(s) with NO linked items\n" +
                               $"  {withDeps.Count} model(s) WITH linked items ({totalItems} item record(s) total)\n\n" +
                               $"Deleting a model that has linked items will unlink those items\n" +
                               $"(their CartridgeModel field will be cleared, but items will NOT be deleted).\n\n" +
                               $"How do you want to proceed?",
                        AutoSize = false,
                        Size = new Size(660, 215),
                        Location = new Point(15, 10),
                        Font = new Font("Segoe UI", 9F)
                    };

                    var btnInactive = new Button
                    {
                        Text = "Option A: Mark as Inactive (Safe)",
                        Location = new Point(15, 230),
                        Size = new Size(660, 50),
                        BackColor = Color.FromArgb(52, 152, 219),
                        ForeColor = Color.White,
                        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                        TextAlign = ContentAlignment.MiddleLeft,
                        Padding = new Padding(10, 0, 0, 0)
                    };
                    btnInactive.Click += (s, ev) => { choiceDialog.Tag = "INACTIVE"; choiceDialog.DialogResult = DialogResult.OK; };

                    var lblInactiveDesc = new Label
                    {
                        Text = "✓ Keeps all data intact  ✓ Maintains audit trail  ✓ Reversible via Edit",
                        Location = new Point(25, 285),
                        Size = new Size(640, 20),
                        ForeColor = Color.Gray,
                        Font = new Font("Segoe UI", 8F)
                    };

                    var btnForceDelete = new Button
                    {
                        Text = "Option B: Delete Permanently (Unlinks Linked Items)",
                        Location = new Point(15, 315),
                        Size = new Size(660, 50),
                        BackColor = Color.FromArgb(231, 76, 60),
                        ForeColor = Color.White,
                        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                        TextAlign = ContentAlignment.MiddleLeft,
                        Padding = new Padding(10, 0, 0, 0)
                    };
                    btnForceDelete.Click += (s, ev) => { choiceDialog.Tag = "FORCE_DELETE"; choiceDialog.DialogResult = DialogResult.OK; };

                    var lblForceDesc = new Label
                    {
                        Text = "⚠️ Permanently removes the model  ⚠️ Clears CartridgeModel on linked items  ⚠️ Cannot be undone",
                        Location = new Point(25, 370),
                        Size = new Size(640, 20),
                        ForeColor = Color.DarkRed,
                        Font = new Font("Segoe UI", 8F)
                    };

                    var btnCancelChoice = new Button
                    {
                        Text = "Cancel",
                        DialogResult = DialogResult.Cancel,
                        Location = new Point(590, 405),
                        Size = new Size(90, 28)
                    };

                    choiceDialog.Controls.AddRange(new Control[]
                    {
                        lblWarning, btnInactive, lblInactiveDesc,
                        btnForceDelete, lblForceDesc, btnCancelChoice
                    });
                    choiceDialog.CancelButton = btnCancelChoice;

                    var choiceResult = choiceDialog.ShowDialog();
                    if (choiceResult != DialogResult.OK) return;

                    string choice = choiceDialog.Tag as string;

                    if (choice == "INACTIVE")
                    {
                        // Mark all selected (with and without deps) as inactive
                        try
                        {
                            foreach (var m in selected)
                            {
                                var dto = await _repository.GetByIdAsync(m.CartridgeModelId);
                                if (dto == null) continue;
                                dto.IsActive = false;
                                await _repository.UpdateAsync(dto);
                            }
                            MessageBox.Show($"{selected.Count} model(s) marked as inactive.",
                                "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            LoadModelsAsync();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error marking models inactive:\n\n{ex.Message}",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        return;
                    }

                    if (choice == "FORCE_DELETE")
                    {
                        // Final warning before force delete
                        string finalMsg = selected.Count == 1
                            ? $"🚨 FINAL WARNING 🚨\n\nYou are about to permanently delete:\n  \"{selected[0].ModelNumber}\"\n\n" +
                              $"This will also unlink {totalItems} item record(s) from this model.\n\nThis CANNOT be undone. Proceed?"
                            : $"🚨 FINAL WARNING 🚨\n\nYou are about to permanently delete {selected.Count} cartridge models.\n\n" +
                              $"This will unlink {totalItems} item record(s). This CANNOT be undone. Proceed?";

                        var confirm = MessageBox.Show(finalMsg, "Final Warning — Cannot Be Undone",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Stop, MessageBoxDefaultButton.Button2);

                        if (confirm != DialogResult.Yes) return;

                        await DeleteModelsAsync(selected);
                        return;
                    }
                }
                return;
            }

            // No dependencies — simple confirmation
            string simpleMsg = selected.Count == 1
                ? $"Permanently delete \"{selected[0].ModelNumber}\"?\n\nThis cannot be undone."
                : $"Permanently delete {selected.Count} cartridge models?\n\nThis cannot be undone.";

            if (MessageBox.Show(simpleMsg, "Confirm Delete",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                await DeleteModelsAsync(selected);
            }
        }

        private async Task DeleteModelsAsync(List<CartridgeModelDto> models)
        {
            try
            {
                foreach (var model in models)
                    await _repository.DeleteAsync(model.CartridgeModelId);

                string msg = models.Count == 1
                    ? "Cartridge model deleted."
                    : $"{models.Count} cartridge models deleted.";

                MessageBox.Show(msg, "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadModelsAsync();
            }
            catch (System.Data.SqlClient.SqlException ex)
            {
                MessageBox.Show($"Database error while deleting:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting cartridge model:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static Form CreateAddCartridgeModelDialog()
        {
            var dialog = new Form
            {
                Text = "Add Cartridge Model",
                Size = new Size(420, 270),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(20)
            };
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            dialog.Controls.Add(mainPanel);

            var lblModelNumber = new Label { Text = "Model Number *", Dock = DockStyle.Fill };
            var txtModelNumber = new TextBox { Dock = DockStyle.Fill };
            mainPanel.Controls.Add(lblModelNumber, 0, 0);
            mainPanel.Controls.Add(txtModelNumber, 1, 0);

            var chkRequestable = new CheckBox { Text = "Is Requestable", Checked = true, Dock = DockStyle.Fill };
            mainPanel.Controls.Add(new Label(), 0, 1);
            mainPanel.Controls.Add(chkRequestable, 1, 1);

            var chkRefillable = new CheckBox { Text = "Is Refillable", Checked = true, Dock = DockStyle.Fill };
            mainPanel.Controls.Add(new Label(), 0, 2);
            mainPanel.Controls.Add(chkRefillable, 1, 2);

            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true
            };

            var btnSave = new Button
            {
                Text = "Save",
                Width = 80,
                DialogResult = DialogResult.None
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Width = 80,
                DialogResult = DialogResult.Cancel
            };

            btnSave.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtModelNumber.Text))
                {
                    MessageBox.Show("Please enter a model number.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    btnSave.Enabled = false;
                    btnSave.Text = "Saving...";

                    var repository = new CartridgeModelRepository();
                    var existing = await repository.FindByModelNumberAsync(txtModelNumber.Text.Trim());
                    if (existing != null)
                    {
                        MessageBox.Show($"Model number '{txtModelNumber.Text.Trim()}' already exists.",
                            "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        btnSave.Enabled = true;
                        btnSave.Text = "Save";
                        return;
                    }

                    var model = new CartridgeModelDto
                    {
                        ModelNumber = txtModelNumber.Text.Trim(),
                        IsRequestable = chkRequestable.Checked,
                        IsRefillable = chkRefillable.Checked,
                        IsActive = true,
                        CreatedBy = AppSession.CurrentUserId,
                        CreatedAt = DateTime.Now
                    };

                    await repository.CreateAsync(model);
                    MessageBox.Show("Cartridge model added successfully!", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    dialog.DialogResult = DialogResult.OK;
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnSave.Enabled = true;
                    btnSave.Text = "Save";
                }
            };

            buttonPanel.Controls.Add(btnSave);
            buttonPanel.Controls.Add(btnCancel);
            mainPanel.Controls.Add(buttonPanel, 0, 3);
            mainPanel.SetColumnSpan(buttonPanel, 2);

            dialog.AcceptButton = btnSave;
            dialog.CancelButton = btnCancel;

            return dialog;
        }

    }
}
