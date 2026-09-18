using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Item
{
    /// <summary>
    /// One IMEI1-keyed cellphone-number change. The repository deliberately accepts this
    /// narrow DTO so the bulk operation cannot accidentally overwrite the other Item fields.
    /// </summary>
    public sealed class CellPhoneNumberUpdate
    {
        public string IMEI1 { get; set; }
        public string CellPhoneNumber { get; set; }
    }

    /// <summary>
    /// Paste two Excel columns (IMEI1 and Cellphone Number), review the matches, and apply only
    /// CellPhoneNumber changes. Previewing performs no database writes.
    /// </summary>
    internal sealed class BulkCellphoneNumberUpdateDialog : Form
    {
        private readonly TextBox _input;
        private readonly DataGridView _previewGrid;
        private readonly Label _status;
        private readonly Button _previewButton;
        private readonly Button _applyButton;
        private readonly ItemRepository _repo = new ItemRepository();
        private List<PreviewRow> _previewRows = new List<PreviewRow>();

        public List<CellPhoneNumberUpdate> AppliedUpdates { get; private set; } = new List<CellPhoneNumberUpdate>();

        private sealed class PreviewRow
        {
            public int LineNumber { get; set; }
            public string IMEI1 { get; set; }
            public string NewNumber { get; set; }
            public string ExistingNumber { get; set; }
            public string Item { get; set; }
            public string Status { get; set; }
            public bool IsValid { get; set; }
        }

        public BulkCellphoneNumberUpdateDialog()
        {

            Text = "Bulk Update Cellphone Numbers";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(850, 560);
            ClientSize = new Size(1050, 680);
            Font = new Font("Segoe UI", 9.5F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14),
                ColumnCount = 1,
                RowCount = 6
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 145));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var heading = new Label
            {
                AutoSize = true,
                Text = "Paste IMEI1 and the new cellphone number from Excel",
                Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 5)
            };

            var instructions = new Label
            {
                AutoSize = true,
                Text = "Use two columns: IMEI1<TAB>Cellphone Number. A header row is optional. Preview checks matches before anything is saved.",
                ForeColor = Color.FromArgb(90, 100, 115),
                Margin = new Padding(0, 0, 0, 7)
            };

            _input = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                AcceptsTab = true,
                WordWrap = false,
                Font = new Font("Consolas", 9.5F),
                BackColor = Color.White
            };
            _input.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.Enter)
                {
                    PreviewInput();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            _input.TextChanged += (s, e) =>
            {
                _previewRows = new List<PreviewRow>();
                _previewGrid.DataSource = null;
                _applyButton.Enabled = false;
                _status.Text = "Paste or edit the data, then click Preview.";
                _status.ForeColor = Color.FromArgb(90, 100, 115);
            };

            var actionRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0, 7, 0, 7)
            };

            var pasteButton = new Button { Text = "Paste from Clipboard", Width = 145, Height = 30 };
            pasteButton.Click += (s, e) =>
            {
                if (Clipboard.ContainsText())
                {
                    _input.Text = Clipboard.GetText();
                    _input.SelectionStart = _input.TextLength;
                    _input.Focus();
                    PreviewInput();
                }
                else
                {
                    MessageBox.Show("The clipboard does not contain text.", "Paste Data", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            _previewButton = new Button { Text = "Preview", Width = 95, Height = 30, Margin = new Padding(8, 0, 0, 0) };
            _previewButton.Click += (s, e) => PreviewInput();

            actionRow.Controls.Add(pasteButton);
            actionRow.Controls.Add(_previewButton);

            _status = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(90, 100, 115),
                Padding = new Padding(0, 2, 0, 4)
            };

            _previewGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
            };
            _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Line", DataPropertyName = "LineNumber", Width = 55 });
            _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "IMEI1", DataPropertyName = "IMEI1", Width = 170 });
            _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "New Cellphone Number", DataPropertyName = "NewNumber", Width = 180 });
            _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Current Number", DataPropertyName = "ExistingNumber", Width = 170 });
            _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Item", DataPropertyName = "Item", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 150 });
            _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status", Width = 190 });
            _previewGrid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _previewRows.Count) return;

                var row = _previewRows[e.RowIndex];
                if (!row.IsValid)
                    e.CellStyle.BackColor = Color.FromArgb(255, 232, 232);
                else if (string.Equals(row.Status, "No change", StringComparison.OrdinalIgnoreCase))
                    e.CellStyle.BackColor = Color.FromArgb(255, 248, 220);
                else
                    e.CellStyle.BackColor = Color.FromArgb(232, 247, 236);
            };

            _applyButton = new Button
            {
                Text = "Apply Updates",
                Width = 125,
                Height = 32,
                Enabled = false,
                DialogResult = DialogResult.None
            };
            _applyButton.Click += ApplyButton_Click;

            var cancelButton = new Button
            {
                Text = "Cancel",
                Width = 90,
                Height = 32,
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(8, 0, 0, 0)
            };

            var bottomRow = new Panel { Dock = DockStyle.Fill, Height = 38 };
            bottomRow.Controls.Add(cancelButton);
            bottomRow.Controls.Add(_applyButton);
            cancelButton.Dock = DockStyle.Right;
            _applyButton.Dock = DockStyle.Right;

            root.Controls.Add(heading, 0, 0);
            root.Controls.Add(instructions, 0, 1);
            root.Controls.Add(_input, 0, 2);
            root.Controls.Add(actionRow, 0, 3);
            root.Controls.Add(_previewGrid, 0, 4);
            root.Controls.Add(bottomRow, 0, 5);

            Controls.Add(root);
            AcceptButton = _previewButton;
            CancelButton = cancelButton;
        }

        private void PreviewInput()
        {
            IReadOnlyList<ItemDto> currentItems;
            try
            {
                // Refresh at preview time so unsaved edits/exclusions in the legacy grid and
                // concurrent database changes cannot make the preview claim a false match.
                currentItems = _repo.GetCellPhoneItems();
            }
            catch (Exception ex)
            {
                _previewRows = new List<PreviewRow>();
                _previewGrid.DataSource = null;
                _applyButton.Enabled = false;
                _status.Text = $"Could not load current cellphone items: {ex.Message}";
                _status.ForeColor = Color.FromArgb(170, 55, 55);
                return;
            }

            _previewRows = BuildPreviewRows(_input.Text, currentItems);
            _previewGrid.DataSource = null;
            _previewGrid.DataSource = _previewRows;

            int validCount = _previewRows.Count(r => r.IsValid && !string.Equals(r.Status, "No change", StringComparison.OrdinalIgnoreCase));
            int noChangeCount = _previewRows.Count(r => r.IsValid && string.Equals(r.Status, "No change", StringComparison.OrdinalIgnoreCase));
            int errorCount = _previewRows.Count(r => !r.IsValid);
            _applyButton.Enabled = _previewRows.Count > 0 && errorCount == 0 && validCount > 0;

            _status.Text = errorCount == 0
                ? $"{_previewRows.Count} row(s) checked: {validCount} change(s), {noChangeCount} already had that number."
                : $"{_previewRows.Count} row(s) checked: {errorCount} error(s). Fix the red rows before applying.";
            _status.ForeColor = errorCount == 0 ? Color.FromArgb(35, 110, 60) : Color.FromArgb(170, 55, 55);
        }

        private void ApplyButton_Click(object sender, EventArgs e)
        {
            var updates = _previewRows
                .Where(r => r.IsValid && !string.Equals(r.Status, "No change", StringComparison.OrdinalIgnoreCase))
                .Select(r => new CellPhoneNumberUpdate { IMEI1 = r.IMEI1, CellPhoneNumber = r.NewNumber })
                .ToList();

            if (updates.Count == 0)
            {
                MessageBox.Show("There are no cellphone-number changes to apply.", "No Changes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirmation = MessageBox.Show(
                $"Apply {updates.Count} cellphone-number change(s)?\n\nOnly dbo.Item.CellPhoneNumber will be changed, matched by IMEI1. The operation is all-or-nothing.",
                "Confirm Bulk Update",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirmation != DialogResult.Yes)
                return;

            try
            {
                _applyButton.Enabled = false;
                int updated = _repo.UpdateCellPhoneNumbers(updates, AppSession.CurrentUserId);
                AppliedUpdates = updates;
                MessageBox.Show($"Updated {updated} cellphone number(s).", "Bulk Update Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _applyButton.Enabled = true;
                MessageBox.Show($"Bulk update was not applied. No changes were committed.\n\n{ex.Message}", "Bulk Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private List<PreviewRow> BuildPreviewRows(string text, IReadOnlyList<ItemDto> existingItems)
        {
            var rows = new List<PreviewRow>();
            var sourceRows = ParseInput(text);
            var existingByImei = (existingItems ?? new List<ItemDto>())
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.IMEI1))
                .GroupBy(i => i.IMEI1.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            var seen = new Dictionary<string, PreviewRow>(StringComparer.OrdinalIgnoreCase);

            foreach (var source in sourceRows)
            {
                var row = new PreviewRow
                {
                    LineNumber = source.LineNumber,
                    IMEI1 = source.IMEI1,
                    NewNumber = source.CellPhoneNumber,
                    Status = source.Error,
                    IsValid = string.IsNullOrEmpty(source.Error)
                };

                if (row.IsValid && seen.TryGetValue(row.IMEI1, out var previous))
                {
                    row.IsValid = false;
                    row.Status = "Duplicate IMEI1 in pasted data.";
                    previous.IsValid = false;
                    previous.Status = "Duplicate IMEI1 in pasted data.";
                }
                else if (row.IsValid)
                {
                    seen[row.IMEI1] = row;

                    if (!existingByImei.TryGetValue(row.IMEI1, out var matches))
                    {
                        row.IsValid = false;
                        row.Status = "IMEI1 not found in active CellPhone items.";
                    }
                    else if (matches.Count != 1)
                    {
                        row.IsValid = false;
                        row.Status = "IMEI1 matches more than one item.";
                    }
                    else
                    {
                        var item = matches[0];
                        row.ExistingNumber = item.CellPhoneNumber ?? string.Empty;
                        row.Item = $"#{item.ItemId} {item.Name}";
                        row.Status = string.Equals(row.ExistingNumber.Trim(), row.NewNumber, StringComparison.OrdinalIgnoreCase)
                            ? "No change"
                            : "Ready to update";
                    }
                }

                rows.Add(row);
            }

            return rows;
        }

        private sealed class ParsedRow
        {
            public int LineNumber { get; set; }
            public string IMEI1 { get; set; }
            public string CellPhoneNumber { get; set; }
            public string Error { get; set; }
        }

        private static List<ParsedRow> ParseInput(string text)
        {
            var result = new List<ParsedRow>();
            if (string.IsNullOrWhiteSpace(text))
                return result;

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            bool firstDataRow = true;

            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                var fields = lines[i].Split('\t').Select(v => v.Trim()).ToList();
                if (fields.Count == 1 && fields[0].Contains(","))
                    fields = fields[0].Split(',').Select(v => v.Trim()).ToList();

                if (firstDataRow && fields.Count == 2 && IsHeader(fields))
                {
                    firstDataRow = false;
                    continue;
                }
                firstDataRow = false;

                var row = new ParsedRow { LineNumber = i + 1 };
                if (fields.Count != 2)
                {
                    row.Error = "Expected exactly two columns: IMEI1 and cellphone number.";
                }
                else
                {
                    row.IMEI1 = fields[0];
                    row.CellPhoneNumber = fields[1];

                    if (string.IsNullOrWhiteSpace(row.IMEI1))
                        row.Error = "IMEI1 is blank.";
                    else if (row.IMEI1.Length != 15)
                        row.Error = "IMEI1 must contain exactly 15 digits.";
                    else if (!Regex.IsMatch(row.IMEI1, @"^\d{15}$"))
                        row.Error = "IMEI1 must contain digits only.";
                    else if (string.IsNullOrWhiteSpace(row.CellPhoneNumber))
                        row.Error = "Cellphone number is blank.";
                    else if (row.CellPhoneNumber.Length > 50)
                        row.Error = "Cellphone number cannot exceed 50 characters.";
                }

                result.Add(row);
            }

            return result;
        }

        private static bool IsHeader(IReadOnlyList<string> fields)
        {
            if (fields == null || fields.Count != 2)
                return false;

            string first = fields[0] ?? string.Empty;
            string second = fields[1] ?? string.Empty;
            return first.IndexOf("imei", StringComparison.OrdinalIgnoreCase) >= 0
                && (second.IndexOf("phone", StringComparison.OrdinalIgnoreCase) >= 0
                    || second.IndexOf("mobile", StringComparison.OrdinalIgnoreCase) >= 0
                    || second.IndexOf("cell", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
