using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using System.Configuration;
using System.Data.SqlClient;
using Yakult.Inventory.App.Core;


namespace Yakult.Inventory.App.Pages.Invoice
{
    public partial class ViewInvoicePage : Form
    {
        private readonly InvoiceRepository _invoiceRepository;
        private DataTable _invoicesData;

        // Sorting state for Sort By dropdown
        private DataGridViewColumn _sortColumn;
        private System.Windows.Forms.SortOrder _sortOrder = System.Windows.Forms.SortOrder.None;
        private bool _sortByDropdownInitialized = false;

        // Date range filters keyed by column name ("StartDate", "EndDate")
        private Dictionary<string, Tuple<DateTime?, DateTime?>> _dateFilters
            = new Dictionary<string, Tuple<DateTime?, DateTime?>>();

        // Text search term (kept in sync with txtSearch)
        private string _searchText = "";

        public ViewInvoicePage()
        {
            InitializeComponent();
            string connectionString = DatabaseConfig.ConnectionString;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                MessageBox.Show("Connection string not configured.", "Config Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _invoiceRepository = new InvoiceRepository(connectionString);
        }

        private async void ViewInvoicePage_Load(object sender, EventArgs e)
        {
            await LoadInvoicesAsync();
        }

        private async Task LoadInvoicesAsync()
        {
            try
            {
                _invoicesData = await _invoiceRepository.GetAllInvoicesAsync();
                dgvInvoices.DataSource = _invoicesData;

                // Configure DataGridView columns
                ConfigureDataGridView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading invoices: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ConfigureDataGridView()
        {
            if (dgvInvoices.Columns.Count == 0) return;

            // Hide columns
            if (dgvInvoices.Columns.Contains("SetId"))
                dgvInvoices.Columns["SetId"].Visible = false;
            if (dgvInvoices.Columns.Contains("CreatedBy"))
                dgvInvoices.Columns["CreatedBy"].Visible = false;
            if (dgvInvoices.Columns.Contains("ComId"))
                dgvInvoices.Columns["ComId"].Visible = false;
            if (dgvInvoices.Columns.Contains("CompanyDescription"))
                dgvInvoices.Columns["CompanyDescription"].Visible = false;
            if (dgvInvoices.Columns.Contains("DaysUntilExpiry"))
                dgvInvoices.Columns["DaysUntilExpiry"].Visible = false;
            if (dgvInvoices.Columns.Contains("CreatedAt"))
            {
                dgvInvoices.Columns["CreatedAt"].HeaderText = "Date Added";
                dgvInvoices.Columns["CreatedAt"].DefaultCellStyle.Format = "yyyy-MM-dd";
                dgvInvoices.Columns["CreatedAt"].FillWeight = 10;
                dgvInvoices.Columns["CreatedAt"].MinimumWidth = 100;
            }

            // Set column headers with proper names from the view
            if (dgvInvoices.Columns.Contains("SetCode"))
                dgvInvoices.Columns["SetCode"].HeaderText = "Invoice Code";
            
            if (dgvInvoices.Columns.Contains("DocumentNumber"))
                dgvInvoices.Columns["DocumentNumber"].HeaderText = "Invoice No.";
            
            if (dgvInvoices.Columns.Contains("ReferenceNumber"))
                dgvInvoices.Columns["ReferenceNumber"].HeaderText = "PO No.";
            
            if (dgvInvoices.Columns.Contains("InvoiceDate"))
                dgvInvoices.Columns["InvoiceDate"].HeaderText = "Invoice Date";
            
            if (dgvInvoices.Columns.Contains("Site"))
                dgvInvoices.Columns["Site"].HeaderText = "Site";
            
            if (dgvInvoices.Columns.Contains("CompanyName"))
                dgvInvoices.Columns["CompanyName"].HeaderText = "Company";
            
            if (dgvInvoices.Columns.Contains("Status"))
                dgvInvoices.Columns["Status"].HeaderText = "Status";
            
            if (dgvInvoices.Columns.Contains("StartDate"))
                dgvInvoices.Columns["StartDate"].HeaderText = "Start Date";
            
            if (dgvInvoices.Columns.Contains("EndDate"))
                dgvInvoices.Columns["EndDate"].HeaderText = "End Date";
            
            if (dgvInvoices.Columns.Contains("TotalAmountDue"))
                dgvInvoices.Columns["TotalAmountDue"].HeaderText = "Total Amount";
            
            if (dgvInvoices.Columns.Contains("ExpiryStatus"))
                dgvInvoices.Columns["ExpiryStatus"].HeaderText = "License Status";
            
            if (dgvInvoices.Columns.Contains("CreatedByName"))
                dgvInvoices.Columns["CreatedByName"].HeaderText = "Prepared By";

            // Format currency columns
            if (dgvInvoices.Columns.Contains("Subtotal"))
                dgvInvoices.Columns["Subtotal"].DefaultCellStyle.Format = "N2";
            if (dgvInvoices.Columns.Contains("VatAmount"))
                dgvInvoices.Columns["VatAmount"].DefaultCellStyle.Format = "N2";
            if (dgvInvoices.Columns.Contains("WhtAmount"))
                dgvInvoices.Columns["WhtAmount"].DefaultCellStyle.Format = "N2";
            if (dgvInvoices.Columns.Contains("DiscountAmount"))
                dgvInvoices.Columns["DiscountAmount"].DefaultCellStyle.Format = "N2";
            if (dgvInvoices.Columns.Contains("TotalAmountDue"))
                dgvInvoices.Columns["TotalAmountDue"].DefaultCellStyle.Format = "N2";

            // Format date columns
            if (dgvInvoices.Columns.Contains("InvoiceDate"))
                dgvInvoices.Columns["InvoiceDate"].DefaultCellStyle.Format = "yyyy-MM-dd";
            if (dgvInvoices.Columns.Contains("StartDate"))
                dgvInvoices.Columns["StartDate"].DefaultCellStyle.Format = "yyyy-MM-dd";
            if (dgvInvoices.Columns.Contains("EndDate"))
                dgvInvoices.Columns["EndDate"].DefaultCellStyle.Format = "yyyy-MM-dd";

            // Color code expiry status
            if (dgvInvoices.Columns.Contains("ExpiryStatus"))
            {
                foreach (DataGridViewRow row in dgvInvoices.Rows)
                {
                    if (row.Cells["ExpiryStatus"].Value != null)
                    {
                        string status = row.Cells["ExpiryStatus"].Value.ToString();
                        if (status == "Expired")
                        {
                            row.Cells["ExpiryStatus"].Style.BackColor = Color.LightCoral;
                            row.Cells["ExpiryStatus"].Style.ForeColor = Color.DarkRed;
                        }
                        else if (status == "Expiring Soon")
                        {
                            row.Cells["ExpiryStatus"].Style.BackColor = Color.LightYellow;
                            row.Cells["ExpiryStatus"].Style.ForeColor = Color.DarkGoldenrod;
                        }
                        else if (status == "Active")
                        {
                            row.Cells["ExpiryStatus"].Style.BackColor = Color.LightGreen;
                            row.Cells["ExpiryStatus"].Style.ForeColor = Color.DarkGreen;
                        }
                    }
                }
            }

            // Set column weights for Fill mode
            if (dgvInvoices.Columns.Contains("SetCode"))
            {
                dgvInvoices.Columns["SetCode"].FillWeight = 10;
                dgvInvoices.Columns["SetCode"].MinimumWidth = 100;
            }
            if (dgvInvoices.Columns.Contains("DocumentNumber"))
            {
                dgvInvoices.Columns["DocumentNumber"].FillWeight = 12;
                dgvInvoices.Columns["DocumentNumber"].MinimumWidth = 120;
            }
            if (dgvInvoices.Columns.Contains("InvoiceDate"))
            {
                dgvInvoices.Columns["InvoiceDate"].FillWeight = 10;
                dgvInvoices.Columns["InvoiceDate"].MinimumWidth = 100;
            }
            if (dgvInvoices.Columns.Contains("CompanyName"))
            {
                dgvInvoices.Columns["CompanyName"].FillWeight = 18;
                dgvInvoices.Columns["CompanyName"].MinimumWidth = 150;
            }
            if (dgvInvoices.Columns.Contains("Site"))
            {
                dgvInvoices.Columns["Site"].FillWeight = 14;
                dgvInvoices.Columns["Site"].MinimumWidth = 120;
            }
            if (dgvInvoices.Columns.Contains("TotalAmountDue"))
            {
                dgvInvoices.Columns["TotalAmountDue"].FillWeight = 12;
                dgvInvoices.Columns["TotalAmountDue"].MinimumWidth = 120;
            }
            if (dgvInvoices.Columns.Contains("ExpiryStatus"))
            {
                dgvInvoices.Columns["ExpiryStatus"].FillWeight = 10;
                dgvInvoices.Columns["ExpiryStatus"].MinimumWidth = 100;
            }

            // Auto size mode
            dgvInvoices.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvInvoices.AllowUserToResizeColumns = true;
            dgvInvoices.AllowUserToAddRows = false;
            dgvInvoices.ReadOnly = true;
            dgvInvoices.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            // Enable sorting glyphs
            DefaultListPageTemplate.EnableSortingGlyphs(dgvInvoices);
            dgvInvoices.ColumnHeaderMouseClick -= DgvInvoices_ColumnHeaderMouseClick;
            dgvInvoices.ColumnHeaderMouseClick += DgvInvoices_ColumnHeaderMouseClick;
            dgvInvoices.CellPainting -= DgvInvoices_HeaderCellPainting;
            dgvInvoices.CellPainting += DgvInvoices_HeaderCellPainting;

            // Initialize Sort By dropdown with fixed options (only once)
            if (!_sortByDropdownInitialized && cmbSortBy != null)
            {
                cmbSortBy.Items.Clear();
                cmbSortBy.Items.Add("Default");
                cmbSortBy.Items.Add("Most Recently Added");
                cmbSortBy.Items.Add("Oldest Added");
                cmbSortBy.SelectedIndex = 0;
                cmbSortBy.SelectedIndexChanged += CmbSortBy_SelectedIndexChanged;
                _sortByDropdownInitialized = true;
            }
        }

        private void DgvInvoices_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (dgvInvoices == null || e.ColumnIndex < 0 || e.ColumnIndex >= dgvInvoices.Columns.Count)
                return;

            var clickedColumn = dgvInvoices.Columns[e.ColumnIndex];

            // Don't sort non-sortable columns
            if (clickedColumn.SortMode == DataGridViewColumnSortMode.NotSortable)
                return;

            // Date filter popup for Start Date / End Date
            if (clickedColumn.Name == "StartDate" || clickedColumn.Name == "EndDate")
            {
                ShowDateRangeFilterPopup(clickedColumn);
                return;
            }

            // Determine new sort direction
            System.Windows.Forms.SortOrder newSortOrder;
            if (clickedColumn.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.None ||
                clickedColumn.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Descending)
            {
                newSortOrder = System.Windows.Forms.SortOrder.Ascending;
            }
            else
            {
                newSortOrder = System.Windows.Forms.SortOrder.Descending;
            }

            // Apply sort
            string propertyName = clickedColumn.DataPropertyName;
            if (!string.IsNullOrEmpty(propertyName) && _invoicesData != null)
            {
                _sortColumn = clickedColumn;
                _sortOrder = newSortOrder;

                // Apply sort using DataView
                var dv = _invoicesData.DefaultView;
                string sortExpression = $"{propertyName} {(newSortOrder == System.Windows.Forms.SortOrder.Ascending ? "ASC" : "DESC")}";
                dv.Sort = sortExpression;
                dgvInvoices.DataSource = dv;

                // Clear all glyphs
                foreach (DataGridViewColumn col in dgvInvoices.Columns)
                {
                    col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                }

                // Set glyph on sorted column
                clickedColumn.HeaderCell.SortGlyphDirection = newSortOrder;
                dgvInvoices.Invalidate();

                ConfigureDataGridView();
            }
        }

        private void ShowDateRangeFilterPopup(DataGridViewColumn col)
        {
            _dateFilters.TryGetValue(col.Name, out var current);
            var headerRect = dgvInvoices.GetColumnDisplayRectangle(col.Index, true);
            var screenPt   = dgvInvoices.PointToScreen(new Point(headerRect.Left, headerRect.Bottom));

            using (var popup = new DateRangeFilterPopup(col.HeaderText, current?.Item1, current?.Item2))
            {
                popup.Location = screenPt;
                popup.ShowDialog(this);

                switch (popup.Action)
                {
                    case DateRangeFilterPopup.PopupAction.SortAscending:
                        if (_invoicesData != null)
                        {
                            _invoicesData.DefaultView.Sort = $"{col.Name} ASC";
                            dgvInvoices.DataSource = _invoicesData.DefaultView;
                        }
                        break;

                    case DateRangeFilterPopup.PopupAction.SortDescending:
                        if (_invoicesData != null)
                        {
                            _invoicesData.DefaultView.Sort = $"{col.Name} DESC";
                            dgvInvoices.DataSource = _invoicesData.DefaultView;
                        }
                        break;

                    case DateRangeFilterPopup.PopupAction.Filter:
                        if (popup.DateFrom.HasValue || popup.DateTo.HasValue)
                            _dateFilters[col.Name] = Tuple.Create(popup.DateFrom, popup.DateTo);
                        else
                            _dateFilters.Remove(col.Name);
                        ApplyAllFilters();
                        break;

                    case DateRangeFilterPopup.PopupAction.Clear:
                        _dateFilters.Remove(col.Name);
                        ApplyAllFilters();
                        break;
                }

                dgvInvoices.Invalidate();
            }
        }

        private void DgvInvoices_HeaderCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex != -1) return;
            if (e.ColumnIndex < 0) return;

            var col = dgvInvoices.Columns[e.ColumnIndex];
            if (col.Name != "StartDate" && col.Name != "EndDate") return;
            if (!_dateFilters.ContainsKey(col.Name)) return;

            e.Paint(e.CellBounds, DataGridViewPaintParts.All);

            const string glyph = "▼";
            using (var f = new Font("Segoe UI", 7F, FontStyle.Bold))
            using (var b = new SolidBrush(Color.Goldenrod))
            {
                var sz = e.Graphics.MeasureString(glyph, f);
                var pt = new PointF(
                    e.CellBounds.Right - sz.Width - 2,
                    e.CellBounds.Top + 2);
                e.Graphics.DrawString(glyph, f, b, pt);
            }

            e.Handled = true;
        }

        private void CmbSortBy_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_invoicesData == null) return;

            var dv = _invoicesData.DefaultView;

            switch (cmbSortBy.SelectedIndex)
            {
                case 1: // Most Recently Added
                    dv.Sort = "CreatedAt DESC";
                    break;
                case 2: // Oldest Added
                    dv.Sort = "CreatedAt ASC";
                    break;
                default: // Default
                    dv.Sort = "InvoiceDate DESC";
                    break;
            }

            dgvInvoices.DataSource = dv;

            // Clear all column sort glyphs since we're using the dropdown filter now
            foreach (DataGridViewColumn col in dgvInvoices.Columns)
                col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            dgvInvoices.Invalidate();

            ConfigureDataGridView();
        }

        private void btnAddInvoice_Click(object sender, EventArgs e)
        {
            // TODO: Open AddInvoiceDialog
            MessageBox.Show("Add Invoice functionality will be implemented next.", "Info",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void btnViewDetails_Click(object sender, EventArgs e)
        {
            if (dgvInvoices.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an invoice to view details.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int setId = Convert.ToInt32(dgvInvoices.SelectedRows[0].Cells["SetId"].Value);

            var detailForm = new ViewInvoiceDetailPage(setId);
            detailForm.ShowDialog();

            // Refresh the list after viewing details (in case of updates)
            await LoadInvoicesAsync();
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            await LoadInvoicesAsync();
        }

        private async void btnArchive_Click(object sender, EventArgs e)
        {
            if (dgvInvoices.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an invoice to archive.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int setId = Convert.ToInt32(dgvInvoices.SelectedRows[0].Cells["SetId"].Value);
            string setCode = dgvInvoices.SelectedRows[0].Cells["SetCode"].Value?.ToString() ?? "N/A";
            string documentNumber = dgvInvoices.SelectedRows[0].Cells["DocumentNumber"].Value?.ToString() ?? "N/A";
            string companyName = dgvInvoices.SelectedRows[0].Cells["CompanyName"].Value?.ToString() ?? "N/A";

            // Get invoice items for this set
            DataTable invoiceItems = await _invoiceRepository.GetInvoiceItemsAsync(setId);

            // Archive confirmation dialog
            using (var archiveDialog = new Form())
            {
                archiveDialog.Text = "Archive Invoice";
                archiveDialog.Size = new Size(500, 320);
                archiveDialog.StartPosition = FormStartPosition.CenterParent;
                archiveDialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                archiveDialog.MaximizeBox = false;
                archiveDialog.MinimizeBox = false;

                var lblMessage = new Label
                {
                    Text = $"Are you sure you want to archive this invoice?\n\n" +
                           $"Invoice Code: {setCode}\n" +
                           $"Invoice No.: {documentNumber}\n" +
                           $"Company: {companyName}\n" +
                           $"Total Items: {invoiceItems.Rows.Count}\n\n" +
                           $"The invoice will be moved to the archive.",
                    AutoSize = false,
                    Size = new Size(460, 120),
                    Location = new Point(10, 10)
                };

                var lblReason = new Label
                {
                    Text = "Reason for archiving:",
                    AutoSize = true,
                    Location = new Point(10, 135)
                };

                var txtReason = new TextBox
                {
                    Size = new Size(460, 20),
                    Location = new Point(10, 155)
                };

                var chkArchiveItems = new CheckBox
                {
                    Text = $"Also archive associated items ({invoiceItems.Rows.Count} items)",
                    AutoSize = true,
                    Location = new Point(10, 185),
                    Checked = false
                };

                var lblNote = new Label
                {
                    Text = "Note: This will archive all items in the invoice's SetItem table",
                    AutoSize = true,
                    Location = new Point(10, 210),
                    ForeColor = Color.Gray,
                    Font = new Font(DefaultFont, FontStyle.Italic)
                };

                var btnArchive = new Button
                {
                    Text = "Archive",
                    DialogResult = DialogResult.OK,
                    Location = new Point(290, 245),
                    Size = new Size(90, 25)
                };

                var btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(390, 245),
                    Size = new Size(90, 25)
                };

                archiveDialog.Controls.AddRange(new Control[] {
                    lblMessage, lblReason, txtReason, chkArchiveItems, lblNote, btnArchive, btnCancel
                });
                archiveDialog.AcceptButton = btnArchive;
                archiveDialog.CancelButton = btnCancel;

                if (archiveDialog.ShowDialog() == DialogResult.OK)
                {
                    string reason = string.IsNullOrWhiteSpace(txtReason.Text) ? "No reason provided" : txtReason.Text;
                    bool archiveItems = chkArchiveItems.Checked;
                    await ArchiveInvoice(setId, invoiceItems, reason, archiveItems);
                }
            }
        }

        private async Task ArchiveInvoice(int setId, DataTable invoiceItems, string reason, bool archiveItems)
        {
            string connectionString = DatabaseConfig.ConnectionString;

            try
            {
                using (var con = new SqlConnection(connectionString))
                {
                    await con.OpenAsync();
                    using (var transaction = con.BeginTransaction())
                    {
                        try
                        {
                            // Archive the Invoice (Set)
                            string insertArchiveSql = @"
                                INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                VALUES ('Set', @SetId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                            using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@SetId", setId);
                                cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                cmd.Parameters.AddWithValue("@ArchiveReason", reason);
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // Optionally archive all associated items
                            if (archiveItems && invoiceItems.Rows.Count > 0)
                            {
                                foreach (DataRow row in invoiceItems.Rows)
                                {
                                    if (row["ItemId"] != DBNull.Value)
                                    {
                                        int itemId = Convert.ToInt32(row["ItemId"]);

                                        string archiveItemSql = @"
                                            INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                            VALUES ('Item', @ItemId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                                        using (var cmd = new SqlCommand(archiveItemSql, con, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@ItemId", itemId);
                                            cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                            cmd.Parameters.AddWithValue("@ArchiveReason", $"Archived with Invoice {setId}: {reason}");
                                            await cmd.ExecuteNonQueryAsync();
                                        }
                                    }
                                }
                            }

                            transaction.Commit();

                            string message = archiveItems && invoiceItems.Rows.Count > 0
                                ? $"Invoice and {invoiceItems.Rows.Count} associated items archived successfully!\n\nYou can view archived invoices in the Archive page."
                                : "Invoice archived successfully!\n\nYou can view archived invoices in the Archive page.";

                            MessageBox.Show(message,
                                "Success",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            await LoadInvoicesAsync();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to archive invoice: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            _searchText = txtSearch?.Text.Trim() ?? "";
            ApplyAllFilters();
        }

        private void ApplyAllFilters()
        {
            if (_invoicesData == null) return;

            var parts = new List<string>();

            // Text search (OR across searchable columns)
            if (!string.IsNullOrEmpty(_searchText))
            {
                var orClauses = new List<string>();
                if (_invoicesData.Columns.Contains("SetCode"))
                    orClauses.Add($"SetCode LIKE '%{_searchText}%'");
                if (_invoicesData.Columns.Contains("CompanyName"))
                    orClauses.Add($"CompanyName LIKE '%{_searchText}%'");
                if (_invoicesData.Columns.Contains("DocumentNumber"))
                    orClauses.Add($"DocumentNumber LIKE '%{_searchText}%'");
                if (_invoicesData.Columns.Contains("ReferenceNumber"))
                    orClauses.Add($"ReferenceNumber LIKE '%{_searchText}%'");
                if (_invoicesData.Columns.Contains("Site"))
                    orClauses.Add($"Site LIKE '%{_searchText}%'");
                if (_invoicesData.Columns.Contains("Status"))
                    orClauses.Add($"Status LIKE '%{_searchText}%'");
                if (orClauses.Count > 0)
                    parts.Add("(" + string.Join(" OR ", orClauses) + ")");
            }

            // Date range filters (AND)
            foreach (var kvp in _dateFilters)
            {
                string colName = kvp.Key;
                if (!_invoicesData.Columns.Contains(colName)) continue;
                var from = kvp.Value.Item1;
                var to   = kvp.Value.Item2;
                if (from.HasValue)
                    parts.Add($"{colName} >= #{from.Value:yyyy-MM-dd}#");
                if (to.HasValue)
                    parts.Add($"{colName} <= #{to.Value:yyyy-MM-dd}#");
            }

            var dv = _invoicesData.DefaultView;
            dv.RowFilter = parts.Count > 0 ? string.Join(" AND ", parts) : "";
            dgvInvoices.DataSource = dv;
            ConfigureDataGridView();
        }

        private async void btnPermanentDelete_Click(object sender, EventArgs e)
        {
            if (dgvInvoices.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select an invoice to delete.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int setId = Convert.ToInt32(dgvInvoices.SelectedRows[0].Cells["SetId"].Value);
            string setCode = dgvInvoices.SelectedRows[0].Cells["SetCode"].Value?.ToString() ?? "N/A";
            string documentNumber = dgvInvoices.SelectedRows[0].Cells["DocumentNumber"].Value?.ToString() ?? "N/A";
            string companyName = dgvInvoices.SelectedRows[0].Cells["CompanyName"].Value?.ToString() ?? "N/A";

            // Get invoice items count
            DataTable invoiceItems = await _invoiceRepository.GetInvoiceItemsAsync(setId);
            int itemCount = invoiceItems.Rows.Count;

            // Show warning dialog
            var result = MessageBox.Show(
                $"⚠️ PERMANENT DELETE WARNING ⚠️\n\n" +
                $"This will PERMANENTLY delete this invoice and restore stock:\n\n" +
                $"Invoice Code: {setCode}\n" +
                $"Invoice No.: {documentNumber}\n" +
                $"Company: {companyName}\n" +
                $"Total Items: {itemCount}\n\n" +
                $"All items in this invoice will have their stock restored.\n" +
                $"Related inventory entries will also be deleted.\n\n" +
                $"This action CANNOT be undone!\n\n" +
                $"⚠️ Only proceed if this was a DATA ENTRY ERROR.\n" +
                $"⚠️ Use 'Archive' button instead for normal records.\n\n" +
                $"Are you absolutely sure you want to permanently delete?",
                "Confirm Permanent Deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2  // Default to "No"
            );

            if (result == DialogResult.Yes)
            {
                // Double confirmation for safety
                var doubleCheck = MessageBox.Show(
                    "FINAL CONFIRMATION\n\n" +
                    "This invoice will be permanently deleted and cannot be recovered.\n" +
                    "Stock will be restored for all items in this invoice.\n\n" +
                    "Are you absolutely certain?",
                    "Final Confirmation",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button2
                );

                if (doubleCheck == DialogResult.Yes)
                {
                    try
                    {
                        bool success = await _invoiceRepository.DeleteInvoiceAndRestoreStock(setId);

                        if (success)
                        {
                            MessageBox.Show(
                                $"Invoice permanently deleted!\n\n" +
                                $"✓ Invoice removed from database\n" +
                                $"✓ Stock restored for {itemCount} items\n" +
                                $"✓ Related inventory entries deleted",
                                "Deleted Successfully",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            await LoadInvoicesAsync();  // Refresh
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting invoice:\n\n{ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void dgvInvoices_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                btnViewDetails_Click(sender, e);
            }
        }
    }
}
