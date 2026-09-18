using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using HopeButton = ReaLTaiizor.Controls.HopeButton;

namespace Yakult.Inventory.App.Pages.Admin.ReferenceData
{
    public partial class DepartmentAcronymPage : UserControl
    {
        private DefaultListPageLayout _layout;
        private DataGridView _dgv;
        private HopeButton _btnEdit;
        private System.Windows.Forms.Button _btnFirst, _btnPrev, _btnNext, _btnLast;
        private Label _lblPageInfo;

        private List<DepartmentAcronymDto> _all;
        private List<DepartmentAcronymDto> _filtered;
        private readonly string _connectionString;
        private int _currentPage = 1;
        private int _preservedPage = 0;
        private const int PageSize = 15;
        private string _sortCol;
        private bool _sortAsc = true;

        public DepartmentAcronymPage()
        {
            _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;
            InitializeComponent();
            BuildUI();
            LoadData();
        }

        private void InitializeComponent() { }

        private void BuildUI()
        {
            SuspendLayout();
            Controls.Clear();
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            _layout = DefaultListPageTemplate.Create(
                "Department Acronyms",
                "Search by department name, section, or acronym...",
                (s, e) => ApplyFilters(),
                () => { LoadData(); });

            DefaultListPageTemplate.HideSortDropdown(_layout);

            const int extraHeaderY = 10;
            foreach (Control c in _layout.HeaderPanel.Controls)
            {
                if (c == _layout.TitleLabel) continue;
                c.Location = new Point(c.Location.X, c.Location.Y + extraHeaderY);
            }
            _layout.HeaderPanel.Height += extraHeaderY;

            if (_layout.FilterByComboBox != null)
            {
                _layout.FilterByComboBox.Items.Clear();
                _layout.FilterByComboBox.Items.AddRange(new object[] { "All", "Active Only", "Inactive Only" });
                _layout.FilterByComboBox.SelectedIndex = 0;
                _layout.FilterByComboBox.SelectedIndexChanged += (s, e) => ApplyFilters();
            }

            _btnEdit = new HopeButton
            {
                Text = "Edit Acronym",
                Font = UiTheme.Fonts.Button,
                Size = new Size(185, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigurePillHopeButton(_btnEdit, UiTheme.Colors.Primary, UiTheme.Colors.PrimaryHover);
            _btnEdit.Click += BtnEdit_Click;

            DefaultListPageTemplate.AddButtons(_layout.ButtonLeftFlow, new[] { _btnEdit });

            if (_layout.SummaryPanel != null)
                _layout.SummaryPanel.Visible = false;

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
            };
            UiFactory.StyleGrid(_dgv);
            _dgv.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) BtnEdit_Click(s, e); };
            _dgv.ColumnHeaderMouseClick += Dgv_ColumnHeaderMouseClick;
            _dgv.CellPainting += Dgv_CellPainting;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "DeptId",  Visible = false, DataPropertyName = "DeptId" });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name",    HeaderText = "Department Name", DataPropertyName = "Name",    FillWeight = 35, SortMode = DataGridViewColumnSortMode.Programmatic });
            //_dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Section", HeaderText = "Section",         DataPropertyName = "Section", FillWeight = 25, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Acronym", HeaderText = "Acronym",         DataPropertyName = "Acronym", FillWeight = 15, SortMode = DataGridViewColumnSortMode.Programmatic });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status",  HeaderText = "Status",          DataPropertyName = "Status",  FillWeight = 10, SortMode = DataGridViewColumnSortMode.Programmatic });

            if (_layout.GridCard != null)
            {
                _layout.GridCard.Controls.Clear();
                _layout.GridCard.Controls.Add(_dgv);
            }

            _btnFirst = new System.Windows.Forms.Button { Text = "<<", Width = 45, Height = 25, Left = 0,   Top = 5 };
            _btnPrev  = new System.Windows.Forms.Button { Text = "<",  Width = 45, Height = 25, Left = 50,  Top = 5 };
            _lblPageInfo = new Label { AutoSize = false, Width = 240, Left = 100, Top = 10, Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.MiddleLeft };
            _btnNext  = new System.Windows.Forms.Button { Text = ">",  Width = 45, Height = 25, Left = 350, Top = 5 };
            _btnLast  = new System.Windows.Forms.Button { Text = ">>", Width = 45, Height = 25, Left = 400, Top = 5 };

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

            DefaultListPageTemplate.SetupInitialPageFocus(_layout.SearchBox, _dgv, _btnEdit);
        }

        private async void LoadData()
        {
            try
            {
                _all = new List<DepartmentAcronymDto>();
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    const string sql = @"
                        SELECT DeptId, Name, Section, Acronym, Active
                        FROM   dbo.Department
                        ORDER  BY Name";

                    using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 60 })
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _all.Add(new DepartmentAcronymDto
                            {
                                DeptId  = reader.GetInt32(0),
                                Name    = reader.GetString(1),
                                Section = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Acronym = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Active  = reader.GetBoolean(4)
                            });
                        }
                    }
                }

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load departments: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyFilters()
        {
            if (_all == null) { _filtered = new List<DepartmentAcronymDto>(); UpdateGrid(); return; }

            var q = (_layout.SearchBox?.Text ?? "").Trim().ToLower();
            var filter = _layout.FilterByComboBox?.SelectedItem?.ToString() ?? "All";

            _filtered = _all.Where(d =>
            {
                if (!string.IsNullOrEmpty(q))
                {
                    bool match = d.Name.ToLower().Contains(q)
                        || (d.Section ?? "").ToLower().Contains(q)
                        || (d.Acronym ?? "").ToLower().Contains(q);
                    if (!match) return false;
                }
                if (filter == "Active Only"   && !d.Active) return false;
                if (filter == "Inactive Only" &&  d.Active) return false;

                return true;
            }).ToList();

            _currentPage = _preservedPage > 0 ? _preservedPage : 1;
            _preservedPage = 0;
            UpdateGrid();
        }

        private void UpdateGrid()   
        {
            if (_filtered == null) { _dgv.DataSource = null; return; }

            IEnumerable<DepartmentAcronymDto> sorted = _filtered;
            switch (_sortCol)
            {
                case "Name":    sorted = _sortAsc ? sorted.OrderBy(d => d.Name    ?? "") : sorted.OrderByDescending(d => d.Name    ?? ""); break;
                case "Section": sorted = _sortAsc ? sorted.OrderBy(d => d.Section ?? "") : sorted.OrderByDescending(d => d.Section ?? ""); break;
                case "Acronym": sorted = _sortAsc ? sorted.OrderBy(d => d.Acronym ?? "") : sorted.OrderByDescending(d => d.Acronym ?? ""); break;
                case "Status":  sorted = _sortAsc ? sorted.OrderBy(d => d.Active)        : sorted.OrderByDescending(d => d.Active);        break;
            }

            int totalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
            if (_currentPage > totalPages) _currentPage = totalPages;

            _dgv.DataSource = sorted
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .Select(d => new
                {
                    d.DeptId,
                    d.Name,
                    d.Section,
                    d.Acronym,
                    Status = d.Active ? "Active" : "Inactive"
                })
                .ToList();

            if (_lblPageInfo != null)
                _lblPageInfo.Text = $"Page {_currentPage} of {totalPages} ({_filtered.Count} departments)";

            if (_btnFirst != null) _btnFirst.Enabled = _currentPage > 1;
            if (_btnPrev  != null) _btnPrev.Enabled  = _currentPage > 1;
            if (_btnNext  != null) _btnNext.Enabled  = _currentPage < totalPages;
            if (_btnLast  != null) _btnLast.Enabled  = _currentPage < totalPages;

            DefaultListPageTemplate.DisableDefaultRowHighlight(_dgv, _btnEdit);
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            if (_dgv.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a department to edit.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int deptId = Convert.ToInt32(_dgv.SelectedRows[0].Cells["DeptId"].Value);
            var dept = _all.FirstOrDefault(d => d.DeptId == deptId);
            if (dept == null) return;

            using (var dlg = new EditDepartmentAcronymDialog(dept.Name, dept.Section, dept.Acronym))
            {
                if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
                    SaveAcronym(deptId, dlg.NewAcronym);
            }
        }

        private async void SaveAcronym(int deptId, string acronym)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    const string sql = "UPDATE dbo.Department SET Acronym = @Acronym WHERE DeptId = @DeptId";
                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@Acronym", string.IsNullOrWhiteSpace(acronym) ? (object)DBNull.Value : acronym.Trim());
                        cmd.Parameters.AddWithValue("@DeptId", deptId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                _preservedPage = _currentPage;
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save acronym: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Dgv_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = _dgv.Columns[e.ColumnIndex];
            if (col.SortMode != DataGridViewColumnSortMode.Programmatic) return;

            if (_sortCol == col.Name) _sortAsc = !_sortAsc;
            else { _sortCol = col.Name; _sortAsc = true; }

            foreach (DataGridViewColumn c in _dgv.Columns)
                c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            col.HeaderCell.SortGlyphDirection = _sortAsc ? System.Windows.Forms.SortOrder.Ascending : System.Windows.Forms.SortOrder.Descending;

            ApplyFilters();
        }

        private void Dgv_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 || e.ColumnIndex < 0) return;

            var column = _dgv.Columns[e.ColumnIndex];
            if (column == null) return;

            e.Handled = true;

            e.Graphics.FillRectangle(Brushes.White, e.CellBounds);

            using (var brush = new SolidBrush(e.CellStyle.BackColor))
                e.Graphics.FillRectangle(brush, new Rectangle(e.CellBounds.X, e.CellBounds.Y, e.CellBounds.Width - 1, e.CellBounds.Height - 1));

            using (var pen = new Pen(Color.White, 1))
            {
                e.Graphics.DrawLine(pen, e.CellBounds.Right - 1, e.CellBounds.Top,      e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                e.Graphics.DrawLine(pen, e.CellBounds.Left,      e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }

            using (var textBrush = new SolidBrush(Color.White))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var  textRect   = e.CellBounds;
                bool isSortable = column.SortMode == DataGridViewColumnSortMode.Programmatic;

                if (isSortable)
                {
                    textRect.Width -= 34;

                    int glyphX = e.CellBounds.Right - 32;
                    int glyphY = e.CellBounds.Y + (e.CellBounds.Height - 8) / 2;

                    Color arrowColor = column.HeaderCell.SortGlyphDirection != System.Windows.Forms.SortOrder.None
                        ? Color.White
                        : Color.FromArgb(160, 255, 255, 255);

                    using (var arrowPen = new Pen(arrowColor, 2))
                    {
                        if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Ascending)
                        {
                            e.Graphics.DrawLine(arrowPen, glyphX,     glyphY + 6, glyphX + 5,  glyphY);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY,     glyphX + 10, glyphY + 6);
                        }
                        else if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Descending)
                        {
                            e.Graphics.DrawLine(arrowPen, glyphX,     glyphY,     glyphX + 5,  glyphY + 6);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY + 6, glyphX + 10, glyphY);
                        }
                        else
                        {
                            e.Graphics.DrawLine(arrowPen, glyphX + 2, glyphY + 2, glyphX + 5, glyphY - 1);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY - 1, glyphX + 8, glyphY + 2);
                            e.Graphics.DrawLine(arrowPen, glyphX + 2, glyphY + 4, glyphX + 5, glyphY + 7);
                            e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY + 7, glyphX + 8, glyphY + 4);
                        }
                    }
                }

                e.Graphics.DrawString(column.HeaderText, e.CellStyle.Font ?? _dgv.Font, textBrush, textRect, fmt);
            }
        }
    }

    internal class DepartmentAcronymDto
    {
        public int    DeptId  { get; set; }
        public string Name    { get; set; }
        public string Section { get; set; }
        public string Acronym { get; set; }
        public bool   Active  { get; set; }
    }

    internal sealed class EditDepartmentAcronymDialog : Form
    {
        public string NewAcronym { get; private set; }

        private TextBox _txtAcronym;

        public EditDepartmentAcronymDialog(string deptName, string section, string currentAcronym)
        {
            Text            = "Edit Acronym";
            Size            = new Size(520, 240);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;

            int y = 30;
            Controls.Add(new Label { Text = "Department:", Location = new Point(25, y),  Width = 120, Font = new Font("Segoe UI", 10F) });
            Controls.Add(new Label { Text = deptName,      Location = new Point(155, y), Width = 330, Font = new Font("Segoe UI", 10F, FontStyle.Bold) });
            y += 34;

            Controls.Add(new Label { Text = "Section:",    Location = new Point(25, y),  Width = 120, Font = new Font("Segoe UI", 10F) });
            Controls.Add(new Label { Text = section ?? "", Location = new Point(155, y), Width = 330, Font = new Font("Segoe UI", 10F) });
            y += 34;

            Controls.Add(new Label { Text = "Acronym:",    Location = new Point(25, y),      Width = 120, Font = new Font("Segoe UI", 10F) });
            _txtAcronym = new TextBox { Location = new Point(155, y - 3), Width = 200, Font = new Font("Segoe UI", 10F), Text = currentAcronym ?? "" };
            Controls.Add(_txtAcronym);
            y += 44;

            var btnSave   = new Button { Text = "Save",   Location = new Point(295, y), Width = 90, Height = 30, DialogResult = DialogResult.None };
            var btnCancel = new Button { Text = "Cancel", Location = new Point(395, y), Width = 90, Height = 30, DialogResult = DialogResult.Cancel };

            btnSave.Click += (s, e) =>
            {
                NewAcronym = _txtAcronym.Text.Trim();
                DialogResult = DialogResult.OK;
            };

            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }
    }
}
