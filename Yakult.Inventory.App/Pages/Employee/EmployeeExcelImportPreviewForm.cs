using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Employee
{
    internal class EmployeeImportRow
    {
        public int RowNumber { get; set; }
        public string EmployeeNumber { get; set; }
        public string Name { get; set; }
        public string TitleCode { get; set; }
        public string Position { get; set; }
        public string Description { get; set; }
        public string CompanyName { get; set; }
        public string BranchName { get; set; }
        public string DepartmentName { get; set; }

        // Resolved DB IDs
        public int? EmpId { get; set; }
        public int CompanyId { get; set; }
        public int? DepartmentId { get; set; }
        public int BranchId { get; set; }
        public int? TitleId { get; set; }

        public List<string> Errors { get; set; } = new List<string>();
        public bool IsValid => Errors.Count == 0;
        public string ErrorSummary => string.Join("; ", Errors);
    }

    internal class EmployeeArchiveCandidate
    {
        public int EmpId { get; set; }
        public string Name { get; set; }
        public string EmployeeNumber { get; set; }
        public string Position { get; set; }
        public string CompanyName { get; set; }
    }

    internal class EmployeeImportBatch
    {
        public List<EmployeeImportRow> NewRecords { get; set; } = new List<EmployeeImportRow>();
        public List<EmployeeImportRow> UpdatedRecords { get; set; } = new List<EmployeeImportRow>();
        public List<EmployeeArchiveCandidate> ArchiveCandidates { get; set; } = new List<EmployeeArchiveCandidate>();
        public List<EmployeeImportRow> InvalidRows { get; set; } = new List<EmployeeImportRow>();
    }

    internal class EmployeeExcelImportPreviewForm : Form
    {
        private readonly EmployeeImportBatch _batch;
        public bool IncludeArchiving { get; private set; }

        private CheckBox _chkArchive;
        private Button _btnConfirm;
        private Button _btnCancel;

        public EmployeeExcelImportPreviewForm(EmployeeImportBatch batch)
        {
            _batch = batch;
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Import Preview — Employee Master Data";
            Width = 940;
            Height = 650;
            MinimumSize = new Size(700, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.FromArgb(240, 248, 255),
                Padding = new Padding(16, 8, 16, 8)
            };

            var lblTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(41, 128, 185),
                Text = "Import Preview",
                TextAlign = ContentAlignment.MiddleLeft
            };

            int totalValid = _batch.NewRecords.Count + _batch.UpdatedRecords.Count;
            var lblCounts = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(52, 73, 94),
                Text = $"  New: {_batch.NewRecords.Count}   |   Updated: {_batch.UpdatedRecords.Count}   |   To Archive: {_batch.ArchiveCandidates.Count}   |   Invalid: {_batch.InvalidRows.Count}",
                TextAlign = ContentAlignment.MiddleLeft
            };

            headerPanel.Controls.Add(lblCounts);
            headerPanel.Controls.Add(lblTitle);

            var tabs = new TabControl { Dock = DockStyle.Fill };

            var tabNew = new TabPage($"New ({_batch.NewRecords.Count})");
            tabNew.Controls.Add(BuildImportGrid(_batch.NewRecords, false));

            var tabUpdated = new TabPage($"Updated ({_batch.UpdatedRecords.Count})");
            tabUpdated.Controls.Add(BuildImportGrid(_batch.UpdatedRecords, false));

            var tabArchive = new TabPage($"To Archive ({_batch.ArchiveCandidates.Count})");
            tabArchive.Controls.Add(BuildArchiveGrid(_batch.ArchiveCandidates));

            var tabInvalid = new TabPage($"Invalid ({_batch.InvalidRows.Count})");
            tabInvalid.Controls.Add(BuildImportGrid(_batch.InvalidRows, true));

            tabs.TabPages.AddRange(new[] { tabNew, tabUpdated, tabArchive, tabInvalid });

            if (_batch.InvalidRows.Count > 0 && totalValid == 0)
                tabs.SelectedTab = tabInvalid;

            var footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = Color.FromArgb(248, 249, 250)
            };

            _chkArchive = new CheckBox
            {
                Text = _batch.ArchiveCandidates.Count > 0
                    ? $"Archive {_batch.ArchiveCandidates.Count} employee(s) not present in this file"
                    : "Archive employees not in this file (none found)",
                AutoSize = true,
                Checked = false,
                Enabled = _batch.ArchiveCandidates.Count > 0,
                Font = new Font("Segoe UI", 9F),
                Left = 12,
                Top = 18
            };

            bool canConfirm = totalValid > 0 || _batch.ArchiveCandidates.Count > 0;
            _btnConfirm = new Button
            {
                Text = !canConfirm ? "Nothing to Import"
                    : (_batch.InvalidRows.Count > 0 ? "Import Valid Rows Only" : "Confirm Import"),
                Width = 190,
                Height = 36,
                Top = 10,
                Enabled = canConfirm,
                BackColor = canConfirm ? Color.FromArgb(39, 174, 96) : Color.FromArgb(189, 195, 199),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnConfirm.FlatAppearance.BorderSize = 0;
            _btnConfirm.Click += (s, e) =>
            {
                IncludeArchiving = _chkArchive.Checked;
                DialogResult = DialogResult.OK;
                Close();
            };

            _btnCancel = new Button
            {
                Text = "Cancel",
                Width = 100,
                Height = 36,
                Top = 10,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            footerPanel.Controls.AddRange(new Control[] { _chkArchive, _btnConfirm, _btnCancel });
            footerPanel.Resize += (s, e) =>
            {
                _btnConfirm.Left = footerPanel.ClientSize.Width - _btnConfirm.Width - 12;
                _btnCancel.Left = _btnConfirm.Left - _btnCancel.Width - 8;
            };

            Controls.Add(tabs);
            Controls.Add(footerPanel);
            Controls.Add(headerPanel);
        }

        private DataGridView BuildImportGrid(List<EmployeeImportRow> rows, bool showErrors)
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(224, 224, 224),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RowNumber", HeaderText = "Row", FillWeight = 5 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EmployeeNumber", HeaderText = "Emp #", FillWeight = 10 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Name", FillWeight = 20 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TitleCode", HeaderText = "Title", FillWeight = 7 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Position", HeaderText = "Position", FillWeight = 14 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CompanyName", HeaderText = "Company", FillWeight = 14 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BranchName", HeaderText = "Branch", FillWeight = 12 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DepartmentName", HeaderText = "Department", FillWeight = 12 });

            if (showErrors)
                dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ErrorSummary", HeaderText = "Errors", FillWeight = 30 });

            dgv.DataSource = rows;

            if (showErrors)
            {
                dgv.RowPrePaint += (s, e) =>
                {
                    if (e.RowIndex >= 0 && e.RowIndex < rows.Count)
                        dgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 238);
                };
            }

            return dgv;
        }

        private DataGridView BuildArchiveGrid(List<EmployeeArchiveCandidate> rows)
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(224, 224, 224),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EmpId", HeaderText = "ID", FillWeight = 7 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EmployeeNumber", HeaderText = "Emp #", FillWeight = 12 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Name", FillWeight = 25 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Position", HeaderText = "Position", FillWeight = 20 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CompanyName", HeaderText = "Company", FillWeight = 25 });

            dgv.DataSource = rows;

            dgv.RowPrePaint += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < rows.Count)
                    dgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 243, 205);
            };

            return dgv;
        }
    }
}
