using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Pages.Item
{
    // Opened from the "Include Excluded" button on UpdateCellphoneDetailsDialog. Lets the user
    // search/filter the items they previously excluded and check off which ones to bring back,
    // rather than an all-or-nothing "Include" that restores every excluded item at once.
    public class IncludeExcludedCellphonesDialog : Form
    {
        private readonly TextBox _txtSearch;
        private readonly DataGridView _grid;
        private readonly Button _btnInclude;
        private readonly Button _btnCancel;
        private readonly Label _lblStatus;
        private readonly List<ItemDto> _sourceItems;
        private BindingList<ItemDto> _displayedRows;
        private bool _headerChecked;

        /// <summary>Items the user checked and confirmed via "Include Selected".</summary>
        public List<ItemDto> IncludedItems { get; } = new List<ItemDto>();

        public IncludeExcludedCellphonesDialog(List<ItemDto> excludedItems)
        {
            _sourceItems = excludedItems ?? new List<ItemDto>();

            Text = "Include Excluded Cellphones";
            Size = new Size(1000, 620);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(700, 400);
            Font = new Font("Segoe UI", 9.5F);

            var searchPanel = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(16, 10, 16, 8) };
            var lblSearch = new Label
            {
                Text = "Search:",
                AutoSize = true,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 6, 8, 0)
            };
            _txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F)
            };
            _txtSearch.TextChanged += (s, e) => ApplyFilter();
            searchPanel.Controls.Add(_txtSearch);
            searchPanel.Controls.Add(lblSearch);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
            };
            UiFactory.StyleGrid(_grid);

            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "Selected",
                HeaderText = "",
                DataPropertyName = "Selected",
                Width = 40,
                Resizable = DataGridViewTriState.False,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            });
            _grid.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "Selected")
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ItemId",
                HeaderText = "Item ID",
                DataPropertyName = "ItemId",
                ReadOnly = true,
                Width = 80,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Name", DataPropertyName = "Name", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 220 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description", DataPropertyName = "Description", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 280 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ModelNumber", HeaderText = "Model Number", DataPropertyName = "ModelNumber", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SerialNumber", HeaderText = "Serial Number", DataPropertyName = "SerialNumber", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CellPhoneNumber", HeaderText = "Cellphone Number", DataPropertyName = "CellPhoneNumber", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 160 });

            SetupSelectAllHeaderCheckbox();

            var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(16, 12, 16, 12) };

            _lblStatus = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.Colors.TextMuted,
                Padding = new Padding(0, 8, 0, 0)
            };

            _btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Height = 32,
                Dock = DockStyle.Right
            };

            _btnInclude = new Button
            {
                Text = "Include Selected",
                Width = 140,
                Height = 32,
                Dock = DockStyle.Right,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnInclude.Click += BtnInclude_Click;

            buttonPanel.Controls.Add(_lblStatus);
            buttonPanel.Controls.Add(_btnInclude);
            buttonPanel.Controls.Add(_btnCancel);

            Controls.Add(_grid);
            Controls.Add(searchPanel);
            Controls.Add(buttonPanel);
            CancelButton = _btnCancel;

            Load += (s, e) => ApplyFilter();
        }

        private void SetupSelectAllHeaderCheckbox()
        {
            _grid.CellPainting += (s, e) =>
            {
                if (e.RowIndex != -1 || _grid.Columns[e.ColumnIndex].Name != "Selected")
                    return;

                e.PaintBackground(e.ClipBounds, true);
                var checkBoxState = _headerChecked
                    ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                    : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
                var glyphSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, checkBoxState);
                var glyphLocation = new Point(
                    e.CellBounds.Left + (e.CellBounds.Width - glyphSize.Width) / 2,
                    e.CellBounds.Top + (e.CellBounds.Height - glyphSize.Height) / 2);
                CheckBoxRenderer.DrawCheckBox(e.Graphics, glyphLocation, checkBoxState);
                e.Handled = true;
            };

            _grid.CellMouseClick += (s, e) =>
            {
                if (e.RowIndex != -1 || _grid.Columns[e.ColumnIndex].Name != "Selected" || _displayedRows == null)
                    return;

                _headerChecked = !_headerChecked;
                foreach (var item in _displayedRows)
                    item.Selected = _headerChecked;

                _grid.Refresh();
            };
        }

        private void ApplyFilter()
        {
            string term = (_txtSearch.Text ?? string.Empty).Trim();

            IEnumerable<ItemDto> filtered = _sourceItems;
            if (!string.IsNullOrEmpty(term))
            {
                filtered = _sourceItems.Where(i =>
                    (i.Name != null && i.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (i.Description != null && i.Description.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (i.ModelNumber != null && i.ModelNumber.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (i.SerialNumber != null && i.SerialNumber.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (i.CellPhoneNumber != null && i.CellPhoneNumber.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    i.ItemId.ToString().IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            _headerChecked = false;
            _displayedRows = new BindingList<ItemDto>(filtered.ToList());
            _grid.DataSource = _displayedRows;
            _lblStatus.Text = $"{_displayedRows.Count} of {_sourceItems.Count} excluded item(s) shown.";
        }

        private void BtnInclude_Click(object sender, EventArgs e)
        {
            _grid.EndEdit();

            IncludedItems.Clear();
            IncludedItems.AddRange(_sourceItems.Where(i => i.Selected));

            if (IncludedItems.Count == 0)
            {
                MessageBox.Show("Check at least one row to include.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var item in IncludedItems)
                item.Selected = false;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
