using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Item
{
    internal sealed class MobileSerialItem
    {
        public string SerialNumber { get; set; }
        public string CellPhoneNumber { get; set; }
        public string IMEI1 { get; set; }
        public string IMEI2 { get; set; }

        public bool HasPhoneDetails => !string.IsNullOrWhiteSpace(CellPhoneNumber)
            || !string.IsNullOrWhiteSpace(IMEI1)
            || !string.IsNullOrWhiteSpace(IMEI2);
    }

    internal sealed class MobileSerialsPreviewDialog : Form
    {
        private enum DialogThemeMode
        {
            Light,
            Dark
        }

        private enum DialogButtonRole
        {
            Primary,
            Secondary,
            Toggle
        }

        private static readonly Color DarkPageBack = Color.FromArgb(20, 24, 31);
        private static readonly Color DarkSurfaceBack = Color.FromArgb(31, 38, 49);
        private static readonly Color DarkSurfaceAltBack = Color.FromArgb(24, 30, 39);
        private static readonly Color DarkBorder = Color.FromArgb(64, 75, 92);
        private static readonly Color DarkTextPrimary = Color.FromArgb(236, 241, 247);
        private static readonly Color DarkTextSecondary = Color.FromArgb(162, 174, 190);
        private static readonly Color DarkAccentBlue = Color.FromArgb(46, 111, 255);
        private static readonly Color DarkAccentBluePressed = Color.FromArgb(34, 88, 205);
        private static readonly Color DarkAccentSlate = Color.FromArgb(79, 92, 114);
        private static readonly Color DarkAccentSlatePressed = Color.FromArgb(61, 71, 89);
        private static readonly Color DarkGridHeaderBack = Color.FromArgb(27, 33, 43);
        private static readonly Color DarkGridRowBack = Color.FromArgb(34, 41, 53);
        private static readonly Color DarkGridAltRowBack = Color.FromArgb(29, 36, 47);
        private static readonly Color DarkGridSelectionBack = Color.FromArgb(44, 83, 160);
        private static readonly Color DarkInvalidRowBack = Color.FromArgb(59, 44, 44);
        private static readonly Color DarkInvalidRowSelectionBack = Color.FromArgb(92, 60, 60);
        private static readonly Color DarkDuplicateRowBack = Color.FromArgb(52, 49, 39);
        private static readonly Color DarkDuplicateRowSelectionBack = Color.FromArgb(85, 79, 54);

        private static readonly Color LightPageBack = Color.FromArgb(244, 247, 251);
        private static readonly Color LightSurfaceBack = Color.White;
        private static readonly Color LightSurfaceAltBack = Color.FromArgb(248, 250, 253);
        private static readonly Color LightBorder = Color.FromArgb(206, 214, 224);
        private static readonly Color LightTextPrimary = Color.FromArgb(29, 41, 57);
        private static readonly Color LightTextSecondary = Color.FromArgb(98, 112, 130);
        private static readonly Color LightAccentBlue = Color.FromArgb(46, 111, 255);
        private static readonly Color LightAccentBluePressed = Color.FromArgb(34, 88, 205);
        private static readonly Color LightAccentSlate = Color.FromArgb(236, 240, 246);
        private static readonly Color LightAccentSlatePressed = Color.FromArgb(213, 220, 231);
        private static readonly Color LightGridHeaderBack = Color.FromArgb(241, 245, 250);
        private static readonly Color LightGridRowBack = Color.White;
        private static readonly Color LightGridAltRowBack = Color.FromArgb(248, 250, 253);
        private static readonly Color LightGridSelectionBack = Color.FromArgb(218, 232, 255);
        private static readonly Color LightGridSelectionFore = Color.FromArgb(20, 45, 90);
        private static readonly Color LightInvalidRowBack = Color.FromArgb(255, 238, 238);
        private static readonly Color LightInvalidRowSelectionBack = Color.FromArgb(247, 211, 211);
        private static readonly Color LightDuplicateRowBack = Color.FromArgb(255, 249, 230);
        private static readonly Color LightDuplicateRowSelectionBack = Color.FromArgb(245, 233, 184);

        private readonly TableLayoutPanel _root;
        private readonly TextBox _filterTextBox;
        private readonly DataGridView _grid;
        private readonly Label _titleLabel;
        private readonly Label _summaryLabel;
        private readonly Label _filterLabel;
        private readonly Button _okButton;
        private readonly Button _cancelButton;
        private readonly Button _selectAllButton;
        private readonly Button _selectNoneButton;
        private readonly Button _copyButton;
        private readonly Button _themeToggleButton;

        private readonly List<SerialEntry> _allEntries;
        private readonly List<SerialEntry> _visibleEntries = new List<SerialEntry>();

        private DialogThemeMode _themeMode = DialogThemeMode.Dark;

        public List<string> SelectedSerials { get; private set; } = new List<string>();
        public List<MobileSerialItem> SelectedItems { get; private set; } = new List<MobileSerialItem>();

        public MobileSerialsPreviewDialog(IReadOnlyList<string> serials, int receivedCount = 0)
            : this(serials?.Select(s => new MobileSerialItem { SerialNumber = s }).ToList(), receivedCount)
        {
        }

        public MobileSerialsPreviewDialog(IReadOnlyList<MobileSerialItem> serials, int receivedCount = 0)
        {
            if (serials == null) throw new ArgumentNullException(nameof(serials));

            var rawReceived = receivedCount <= 0 ? serials.Count : receivedCount;
            _allEntries = BuildEntries(serials);

            Text = "Review Mobile Serials";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            MinimumSize = new Size(560, 420);
            ClientSize = GetInitialSize(_allEntries.Count);
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);

            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 7
            };
            _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _titleLabel = new Label
            {
                Text = "These serial numbers were received from the mobile app.",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold)
            };

            _summaryLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 8)
            };

            var filterRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 8)
            };
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _filterLabel = new Label
            {
                Text = "Filter:",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                AutoSize = true
            };

            _filterTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle
            };
            _filterTextBox.TextChanged += (s, e) => ApplyFilter();
            filterRow.Controls.Add(_filterLabel, 0, 0);
            filterRow.Controls.Add(_filterTextBox, 1, 0);

            SetTextBoxCueBanner(_filterTextBox, "Filter serials...");

            _grid = BuildGrid();
            _grid.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (_grid.Columns[e.ColumnIndex].Name != "colSelect") return;
                UpdateSummary(rawReceived);
            };
            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;

                var row = _grid.Rows[e.RowIndex];
                var entry = row.Tag as SerialEntry;
                if (entry == null) return;

                ApplyRowTheme(row, entry, e.RowIndex);
            };

            var quickActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 8, 0, 0)
            };

            _selectAllButton = new Button { Text = "Select all", AutoSize = true };
            _selectAllButton.Click += (s, e) =>
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    var entry = row.Tag as SerialEntry;
                    if (entry == null || entry.Status != SerialStatus.Ok) continue;
                    row.Cells["colSelect"].Value = true;
                }
                UpdateSummary(rawReceived);
            };

            _selectNoneButton = new Button { Text = "Select none", AutoSize = true };
            _selectNoneButton.Click += (s, e) =>
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    var entry = row.Tag as SerialEntry;
                    if (entry == null || entry.Status != SerialStatus.Ok) continue;
                    row.Cells["colSelect"].Value = false;
                }
                UpdateSummary(rawReceived);
            };

            _copyButton = new Button { Text = "Copy selected", AutoSize = true };
            _copyButton.Click += (s, e) =>
            {
                var selected = GetCheckedSerials();
                if (selected.Count == 0)
                {
                    MessageBox.Show("No serials selected to copy.", "Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Clipboard.SetText(string.Join(Environment.NewLine, selected));
                MessageBox.Show("Selected serials copied to clipboard.", "Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            _themeToggleButton = new Button { AutoSize = true };
            _themeToggleButton.Click += (s, e) =>
            {
                _themeMode = _themeMode == DialogThemeMode.Dark ? DialogThemeMode.Light : DialogThemeMode.Dark;
                ApplyTheme();
            };

            quickActions.Controls.Add(_selectAllButton);
            quickActions.Controls.Add(_selectNoneButton);
            quickActions.Controls.Add(_copyButton);
            quickActions.Controls.Add(_themeToggleButton);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 6, 0, 0)
            };

            _cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32 };
            _okButton = new Button { Text = "Add selected", DialogResult = DialogResult.OK, Width = 110, Height = 32 };

            AcceptButton = _okButton;
            CancelButton = _cancelButton;

            _okButton.Click += (s, e) =>
            {
                SelectedItems = GetCheckedItems();
                SelectedSerials = SelectedItems.Select(i => i.SerialNumber).ToList();
                if (SelectedSerials.Count == 0)
                {
                    MessageBox.Show("Please select at least one serial number.", "Nothing Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
            };

            buttons.Controls.Add(_okButton);
            buttons.Controls.Add(_cancelButton);

            _root.Controls.Add(_titleLabel, 0, 0);
            _root.Controls.Add(_summaryLabel, 0, 1);
            _root.Controls.Add(filterRow, 0, 2);
            _root.Controls.Add(_grid, 0, 3);
            _root.Controls.Add(quickActions, 0, 4);
            _root.Controls.Add(new Panel { Height = 4, Dock = DockStyle.Top, BackColor = Color.Transparent }, 0, 5);
            _root.Controls.Add(buttons, 0, 6);

            Controls.Add(_root);

            ApplyFilter();
            UpdateSummary(rawReceived);
            ApplyTheme();
        }

        private List<string> GetCheckedSerials()
        {
            return GetCheckedItems().Select(i => i.SerialNumber).ToList();
        }

        private List<MobileSerialItem> GetCheckedItems()
        {
            var list = new List<MobileSerialItem>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var entry = row.Tag as SerialEntry;
                if (entry == null || entry.Status != SerialStatus.Ok) continue;

                var isCheckedObj = row.Cells["colSelect"].Value;
                var isChecked = isCheckedObj is bool b && b;
                if (!isChecked) continue;

                if (!string.IsNullOrWhiteSpace(entry.Serial))
                    list.Add(entry.ToMobileItem());
            }

            return list
                .GroupBy(i => i.SerialNumber, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        private void ApplyFilter()
        {
            var term = (_filterTextBox.Text ?? string.Empty).Trim();

            IEnumerable<SerialEntry> filtered = _allEntries;
            if (!string.IsNullOrWhiteSpace(term))
            {
                filtered = filtered.Where(e =>
                    (!string.IsNullOrWhiteSpace(e.Serial) && e.Serial.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrWhiteSpace(e.CellPhoneNumber) && e.CellPhoneNumber.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrWhiteSpace(e.IMEI1) && e.IMEI1.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (!string.IsNullOrWhiteSpace(e.IMEI2) && e.IMEI2.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            _visibleEntries.Clear();
            _visibleEntries.AddRange(filtered);

            _grid.SuspendLayout();
            try
            {
                _grid.Rows.Clear();
                foreach (var entry in _visibleEntries)
                {
                    var idx = _grid.Rows.Add();
                    var row = _grid.Rows[idx];
                    row.Tag = entry;

                    var selectable = entry.Status == SerialStatus.Ok;
                    row.Cells["colSelect"].Value = selectable;
                    row.Cells["colIndex"].Value = entry.Index;
                    row.Cells["colSerial"].Value = entry.DisplaySerial;
                    row.Cells["colPhone"].Value = entry.CellPhoneNumber;
                    row.Cells["colImei1"].Value = entry.IMEI1;
                    row.Cells["colImei2"].Value = entry.IMEI2;
                    row.Cells["colStatus"].Value = entry.StatusText;
                    row.Cells["colSelect"].ReadOnly = !selectable;
                    row.ReadOnly = false;

                    ApplyRowTheme(row, entry, idx);
                }
            }
            finally
            {
                _grid.ResumeLayout();
            }
        }

        private void UpdateSummary(int receivedCount)
        {
            var invalidCount = _allEntries.Count(e => e.Status == SerialStatus.Invalid);
            var dupCount = _allEntries.Count(e => e.Status == SerialStatus.Duplicate);
            var okUnique = _allEntries.Count(e => e.Status == SerialStatus.Ok);
            var selected = GetCheckedSerials().Count;

            _summaryLabel.Text =
                $"Received: {receivedCount}  |  Unique: {okUnique}  |  Selected: {selected}  |  Hidden duplicates: {dupCount}  |  Invalid: {invalidCount}";

            _okButton.Text = selected > 0 ? $"Add selected ({selected})" : "Add selected";
        }

        private void ApplyTheme()
        {
            var isDark = _themeMode == DialogThemeMode.Dark;

            BackColor = isDark ? DarkPageBack : LightPageBack;
            ForeColor = isDark ? DarkTextPrimary : LightTextPrimary;
            _root.BackColor = isDark ? DarkPageBack : LightPageBack;
            _titleLabel.ForeColor = isDark ? DarkTextPrimary : LightTextPrimary;
            _summaryLabel.ForeColor = isDark ? DarkTextSecondary : LightTextSecondary;
            _filterLabel.ForeColor = isDark ? DarkTextSecondary : LightTextSecondary;
            _filterTextBox.BackColor = isDark ? DarkSurfaceAltBack : LightSurfaceAltBack;
            _filterTextBox.ForeColor = isDark ? DarkTextPrimary : LightTextPrimary;

            _grid.BackgroundColor = isDark ? DarkSurfaceBack : LightSurfaceBack;
            _grid.GridColor = isDark ? DarkBorder : LightBorder;
            _grid.DefaultCellStyle.BackColor = isDark ? DarkGridRowBack : LightGridRowBack;
            _grid.DefaultCellStyle.ForeColor = isDark ? DarkTextPrimary : LightTextPrimary;
            _grid.DefaultCellStyle.SelectionBackColor = isDark ? DarkGridSelectionBack : LightGridSelectionBack;
            _grid.DefaultCellStyle.SelectionForeColor = isDark ? Color.White : LightGridSelectionFore;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = isDark ? DarkGridAltRowBack : LightGridAltRowBack;
            _grid.AlternatingRowsDefaultCellStyle.ForeColor = isDark ? DarkTextPrimary : LightTextPrimary;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = isDark ? DarkGridHeaderBack : LightGridHeaderBack;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = isDark ? DarkTextPrimary : LightTextPrimary;
            _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = isDark ? DarkGridHeaderBack : LightGridHeaderBack;
            _grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = isDark ? DarkTextPrimary : LightTextPrimary;
            _grid.RowTemplate.DefaultCellStyle.BackColor = isDark ? DarkGridRowBack : LightGridRowBack;
            _grid.RowTemplate.DefaultCellStyle.ForeColor = isDark ? DarkTextPrimary : LightTextPrimary;

            StyleButton(_selectAllButton, DialogButtonRole.Secondary);
            StyleButton(_selectNoneButton, DialogButtonRole.Secondary);
            StyleButton(_copyButton, DialogButtonRole.Secondary);
            StyleButton(_cancelButton, DialogButtonRole.Secondary);
            StyleButton(_okButton, DialogButtonRole.Primary);
            StyleButton(_themeToggleButton, DialogButtonRole.Toggle);
            _themeToggleButton.Text = isDark ? "Light Mode" : "Dark Mode";

            foreach (DataGridViewRow row in _grid.Rows)
            {
                var entry = row.Tag as SerialEntry;
                if (entry != null)
                    ApplyRowTheme(row, entry, row.Index);
            }

            _grid.Invalidate();
        }

        private void ApplyRowTheme(DataGridViewRow row, SerialEntry entry, int rowIndex)
        {
            var isDark = _themeMode == DialogThemeMode.Dark;

            if (entry.Status == SerialStatus.Invalid)
            {
                row.DefaultCellStyle.ForeColor = isDark ? Color.FromArgb(245, 207, 207) : LightTextPrimary;
                row.DefaultCellStyle.BackColor = isDark ? DarkInvalidRowBack : LightInvalidRowBack;
                row.DefaultCellStyle.SelectionBackColor = isDark ? DarkInvalidRowSelectionBack : LightInvalidRowSelectionBack;
                row.DefaultCellStyle.SelectionForeColor = isDark ? Color.White : LightTextPrimary;
                return;
            }

            if (entry.Status == SerialStatus.Duplicate)
            {
                row.DefaultCellStyle.ForeColor = isDark ? Color.FromArgb(245, 226, 173) : LightTextPrimary;
                row.DefaultCellStyle.BackColor = isDark ? DarkDuplicateRowBack : LightDuplicateRowBack;
                row.DefaultCellStyle.SelectionBackColor = isDark ? DarkDuplicateRowSelectionBack : LightDuplicateRowSelectionBack;
                row.DefaultCellStyle.SelectionForeColor = isDark ? Color.White : LightTextPrimary;
                return;
            }

            row.DefaultCellStyle.ForeColor = isDark ? DarkTextPrimary : LightTextPrimary;
            row.DefaultCellStyle.BackColor = rowIndex % 2 == 0
                ? (isDark ? DarkGridRowBack : LightGridRowBack)
                : (isDark ? DarkGridAltRowBack : LightGridAltRowBack);
            row.DefaultCellStyle.SelectionBackColor = isDark ? DarkGridSelectionBack : LightGridSelectionBack;
            row.DefaultCellStyle.SelectionForeColor = isDark ? Color.White : LightGridSelectionFore;
        }

        private void StyleButton(Button button, DialogButtonRole role)
        {
            var isDark = _themeMode == DialogThemeMode.Dark;
            var backColor = isDark
                ? (role == DialogButtonRole.Primary ? DarkAccentBlue : DarkAccentSlate)
                : (role == DialogButtonRole.Primary ? LightAccentBlue : LightAccentSlate);
            var pressedColor = isDark
                ? (role == DialogButtonRole.Primary ? DarkAccentBluePressed : DarkAccentSlatePressed)
                : (role == DialogButtonRole.Primary ? LightAccentBluePressed : LightAccentSlatePressed);
            var foreColor = role == DialogButtonRole.Primary
                ? Color.White
                : (isDark ? Color.White : LightTextPrimary);

            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = role == DialogButtonRole.Primary ? 0 : 1;
            button.FlatAppearance.BorderColor = isDark ? DarkBorder : LightBorder;
            button.FlatAppearance.MouseOverBackColor = Blend(backColor, Color.White, 0.08);
            button.FlatAppearance.MouseDownBackColor = pressedColor;
            button.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.Padding = new Padding(8, 0, 8, 0);
        }

        private static Size GetInitialSize(int entryCount)
        {
            const int minW = 560;
            const int baseH = 260;
            const int rowH = 22;
            const int maxH = 640;

            var desired = baseH + Math.Min(16, Math.Max(4, entryCount)) * rowH;
            return new Size(minW, Math.Min(maxH, desired));
        }

        private static DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                ReadOnly = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = DarkSurfaceBack,
                BorderStyle = BorderStyle.FixedSingle,
                EnableHeadersVisualStyles = false,
                GridColor = DarkBorder
            };

            grid.RowTemplate.Height = 22;
            grid.RowTemplate.DefaultCellStyle.BackColor = DarkGridRowBack;
            grid.RowTemplate.DefaultCellStyle.ForeColor = DarkTextPrimary;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            grid.DefaultCellStyle.BackColor = DarkGridRowBack;
            grid.DefaultCellStyle.ForeColor = DarkTextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = DarkGridSelectionBack;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.BackColor = DarkGridAltRowBack;
            grid.AlternatingRowsDefaultCellStyle.ForeColor = DarkTextPrimary;
            grid.ColumnHeadersDefaultCellStyle.BackColor = DarkGridHeaderBack;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = DarkTextPrimary;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = DarkGridHeaderBack;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = DarkTextPrimary;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.ColumnHeadersHeight = 34;

            var colSelect = new DataGridViewCheckBoxColumn
            {
                Name = "colSelect",
                HeaderText = "",
                Width = 40,
                FillWeight = 10,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };

            var colIndex = new DataGridViewTextBoxColumn
            {
                Name = "colIndex",
                HeaderText = "#",
                Width = 52,
                FillWeight = 15,
                ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            };

            var colSerial = new DataGridViewTextBoxColumn
            {
                Name = "colSerial",
                HeaderText = "Serial",
                FillWeight = 55,
                ReadOnly = true,
                DefaultCellStyle = { Font = new Font("Consolas", 10f, FontStyle.Regular) }
            };

            var colPhone = new DataGridViewTextBoxColumn
            {
                Name = "colPhone",
                HeaderText = "Phone",
                FillWeight = 35,
                ReadOnly = true
            };

            var colImei1 = new DataGridViewTextBoxColumn
            {
                Name = "colImei1",
                HeaderText = "IMEI 1",
                FillWeight = 35,
                ReadOnly = true
            };

            var colImei2 = new DataGridViewTextBoxColumn
            {
                Name = "colImei2",
                HeaderText = "IMEI 2",
                FillWeight = 35,
                ReadOnly = true
            };

            var colStatus = new DataGridViewTextBoxColumn
            {
                Name = "colStatus",
                HeaderText = "Status",
                FillWeight = 30,
                ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
            };

            grid.Columns.AddRange(colSelect, colIndex, colSerial, colPhone, colImei1, colImei2, colStatus);
            return grid;
        }

        private static List<SerialEntry> BuildEntries(IReadOnlyList<MobileSerialItem> rawSerials)
        {
            var list = new List<SerialEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var displayIndex = 0;

            foreach (var raw in rawSerials)
            {
                var trimmed = (raw?.SerialNumber ?? string.Empty).Trim();
                var isInvalid = string.IsNullOrWhiteSpace(trimmed);

                if (isInvalid)
                {
                    displayIndex++;
                    list.Add(new SerialEntry(displayIndex, raw, SerialStatus.Invalid));
                    continue;
                }

                if (seen.Contains(trimmed))
                {
                    displayIndex++;
                    list.Add(new SerialEntry(displayIndex, raw, SerialStatus.Duplicate));
                    continue;
                }

                seen.Add(trimmed);
                displayIndex++;
                list.Add(new SerialEntry(displayIndex, raw, SerialStatus.Ok));
            }

            return list;
        }

        private enum SerialStatus
        {
            Ok = 0,
            Duplicate = 1,
            Invalid = 2
        }

        private sealed class SerialEntry
        {
            public int Index { get; }
            public string Serial { get; }
            public string CellPhoneNumber { get; }
            public string IMEI1 { get; }
            public string IMEI2 { get; }
            public SerialStatus Status { get; }

            public SerialEntry(int index, MobileSerialItem item, SerialStatus status)
            {
                Index = index;
                Serial = (item?.SerialNumber ?? string.Empty).Trim();
                CellPhoneNumber = (item?.CellPhoneNumber ?? string.Empty).Trim();
                IMEI1 = (item?.IMEI1 ?? string.Empty).Trim();
                IMEI2 = (item?.IMEI2 ?? string.Empty).Trim();
                Status = status;
            }

            public string DisplaySerial => Status == SerialStatus.Invalid ? "(empty)" : Serial;

            public MobileSerialItem ToMobileItem()
            {
                return new MobileSerialItem
                {
                    SerialNumber = Serial,
                    CellPhoneNumber = string.IsNullOrWhiteSpace(CellPhoneNumber) ? null : CellPhoneNumber,
                    IMEI1 = string.IsNullOrWhiteSpace(IMEI1) ? null : IMEI1,
                    IMEI2 = string.IsNullOrWhiteSpace(IMEI2) ? null : IMEI2
                };
            }

            public string StatusText
            {
                get
                {
                    switch (Status)
                    {
                        case SerialStatus.Ok: return "OK";
                        case SerialStatus.Duplicate: return "Duplicate";
                        case SerialStatus.Invalid: return "Invalid";
                        default: return "Unknown";
                    }
                }
            }
        }

        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private static void SetTextBoxCueBanner(TextBox textBox, string cueText)
        {
            if (textBox == null) return;
            if (!textBox.IsHandleCreated)
            {
                textBox.HandleCreated += (s, e) => SetTextBoxCueBanner(textBox, cueText);
                return;
            }

            try
            {
                SendMessage(textBox.Handle, EM_SETCUEBANNER, IntPtr.Zero, cueText ?? string.Empty);
            }
            catch
            {
            }
        }

        private static Color Blend(Color source, Color target, double amount)
        {
            var ratio = Math.Max(0D, Math.Min(1D, amount));
            var r = (int)Math.Round(source.R + ((target.R - source.R) * ratio));
            var g = (int)Math.Round(source.G + ((target.G - source.G) * ratio));
            var b = (int)Math.Round(source.B + ((target.B - source.B) * ratio));
            return Color.FromArgb(r, g, b);
        }
    }
}
