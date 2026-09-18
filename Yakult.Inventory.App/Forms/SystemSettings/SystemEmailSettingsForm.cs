using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using System.Drawing.Drawing2D;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Forms.SystemSettings
{
    /// <summary>
    /// Email settings tab selection
    /// </summary>
    public enum EmailSettingsTab
    {
        EmailAddresses,
        SmtpProfiles,
        EmailTemplates
    }

    /// <summary>
    /// System Email Settings for Inventory, Cartridge Management, and Request Portal
    /// </summary>
    public sealed class SystemEmailSettingsForm : Form
    {
        private readonly EmailRepository _emailRepo;
        private readonly SystemEmailNotificationService _emailService;

        private TabControl tabControl;
        private TabPage tabEmailAddresses;
        private TabPage tabSmtpProfiles;
        private TabPage tabTemplates;

        // Email Addresses tab
        private DataGridView gridEmailAddresses;
        private List<EmailAddressDto> _allEmailAddresses;
        private List<EmailAddressDto> _filteredEmailAddresses;
        private TextBox _searchBoxEmailAddresses;
        private Dictionary<string, HashSet<string>> _columnFiltersEmail = new Dictionary<string, HashSet<string>>();
        private string _sortColumnNameEmail;
        private bool _sortAscendingEmail = true;
        private int _emailCurrentPage = 1;
        private const int EmailPageSize = 20;
        private Button _btnEmailFirst, _btnEmailPrev, _btnEmailNext, _btnEmailLast;
        private Label _lblEmailPageInfo;
        private Button btnAddEmailAddress;
        private Button btnEditEmailAddresses;
        private Button btnSaveEmailAddresses;
        private Button btnDeleteEmailAddress;
        private Button btnRefreshEmailAddresses;

        // SMTP Profiles tab
        private DataGridView gridSmtpProfiles;
        private List<SystemSmtpProfileDto> _allSmtpProfiles;
        private int _smtpCurrentPage = 1;
        private const int SmtpPageSize = 20;
        private Button _btnSmtpFirst, _btnSmtpPrev, _btnSmtpNext, _btnSmtpLast;
        private Label _lblSmtpPageInfo;
        private Button btnAddSmtpProfile;
        private Button btnEditSmtpProfile;
        private Button btnRefreshSmtpProfiles;

        // Templates tab
        private DataGridView gridTemplates;
        private List<EmailTemplateDto> _allTemplates;
        private int _templatesCurrentPage = 1;
        private const int TemplatesPageSize = 20;
        private Button _btnTplFirst, _btnTplPrev, _btnTplNext, _btnTplLast;
        private Label _lblTplPageInfo;
        private Button btnAddTemplate;
        private Button btnEditTemplate;
        private Button btnRefreshTemplates;


        // Test email button
        private Button btnSendTestEmail;

        public SystemEmailSettingsForm(EmailSettingsTab initialTab = EmailSettingsTab.EmailAddresses)
        {
            _emailRepo = new EmailRepository();
            _emailService = new SystemEmailNotificationService(_emailRepo);

            InitializeComponent();
            SelectTab(initialTab);
            _ = LoadAllDataAsync();
        }

        private void SelectTab(EmailSettingsTab tab)
        {
            switch (tab)
            {
                case EmailSettingsTab.EmailAddresses:
                    tabControl.SelectedTab = tabEmailAddresses;
                    break;
                case EmailSettingsTab.SmtpProfiles:
                    tabControl.SelectedTab = tabSmtpProfiles;
                    break;
                case EmailSettingsTab.EmailTemplates:
                    tabControl.SelectedTab = tabTemplates;
                    break;
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Text = "Email Configuration";
            this.Size = new Size(1400, 850);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(1000, 700);
            this.Font = new Font("Segoe UI", 9.5F);

            // Main tab control
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(12, 6)
            };

            // Create tabs
            tabEmailAddresses = new TabPage("Email Addresses");
            tabSmtpProfiles = new TabPage("SMTP Profiles");
            tabTemplates = new TabPage("Email Templates");

            // Build each tab
            BuildEmailAddressesTab();
            BuildSmtpProfilesTab();
            BuildTemplatesTab();

            // Add tabs to control
            tabControl.TabPages.Add(tabEmailAddresses);
            tabControl.TabPages.Add(tabSmtpProfiles);
            tabControl.TabPages.Add(tabTemplates);

            // Bottom toolbar
            var bottomToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(12, 14, 12, 14),
                AutoSize = false
            };

            btnSendTestEmail = new Button
            {
                Text = "Send Test Email",
                Width = 140,
                Height = 32,
                Margin = new Padding(0, 0, 0, 0)
            };
            btnSendTestEmail.Click += BtnSendTestEmail_Click;

            bottomToolbar.Controls.Add(btnSendTestEmail);

            this.Controls.Add(tabControl);
            this.Controls.Add(bottomToolbar);

            this.ResumeLayout(false);
        }

        #region Email Addresses Tab

        private void BuildEmailAddressesTab()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };

            gridEmailAddresses = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
            };

            UiFactory.StyleGrid(gridEmailAddresses);

            gridEmailAddresses.CellBorderStyle = DataGridViewCellBorderStyle.SingleVertical;
            gridEmailAddresses.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            gridEmailAddresses.GridColor = Color.FromArgb(200, 200, 200);
            gridEmailAddresses.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            gridEmailAddresses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colId",
                HeaderText = "ID",
                DataPropertyName = "EmailId",
                Width = 60,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    BackColor = Color.FromArgb(245, 245, 245),
                    ForeColor = Color.Gray
                }
            });
            gridEmailAddresses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colEmailAddress",
                HeaderText = "Email Address",
                DataPropertyName = "EmailAddress",
                FillWeight = 45,
                MinimumWidth = 200,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });
            gridEmailAddresses.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDisplayName",
                HeaderText = "Display Name",
                DataPropertyName = "DisplayName",
                FillWeight = 35,
                MinimumWidth = 150,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });
            gridEmailAddresses.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "colActive",
                HeaderText = "Active",
                DataPropertyName = "IsActive",
                Width = 80,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            gridEmailAddresses.CellPainting += DgvEmailAddressesCellPainting;
            gridEmailAddresses.ColumnHeaderMouseClick += DgvEmailAddressesColumnHeaderMouseClick;

            gridEmailAddresses.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 60,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 14, 0, 14),
                AutoSize = false
            };

            btnAddEmailAddress = new Button
            {
                Text = "Add Email",
                Width = 110,
                Height = 32,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnAddEmailAddress.Click += BtnAddEmailAddress_Click;

            btnEditEmailAddresses = new Button
            {
                Text = "Edit",
                Width = 90,
                Height = 32,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnEditEmailAddresses.Click += BtnEditEmailAddresses_Click;

            btnSaveEmailAddresses = new Button
            {
                Text = "Save Changes",
                Width = 120,
                Height = 32,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnSaveEmailAddresses.Click += BtnSaveEmailAddresses_Click;

            btnDeleteEmailAddress = new Button
            {
                Text = "Deactivate",
                Width = 110,
                Height = 32,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnDeleteEmailAddress.Click += BtnDeleteEmailAddress_Click;

            btnRefreshEmailAddresses = new Button
            {
                Text = "Refresh",
                Width = 100,
                Height = 32,
                Margin = new Padding(0, 0, 0, 0)
            };
            btnRefreshEmailAddresses.Click += (s, e) => _ = LoadEmailAddressesAsync();

            toolbar.Controls.Add(btnAddEmailAddress);
            toolbar.Controls.Add(btnEditEmailAddresses);
            toolbar.Controls.Add(btnSaveEmailAddresses);
            toolbar.Controls.Add(btnDeleteEmailAddress);
            toolbar.Controls.Add(btnRefreshEmailAddresses);

            // Search bar
            var searchPanel = new Panel { Dock = DockStyle.Top, Height = 36 };

            var searchLabel = new Label
            {
                Text = "Search:",
                AutoSize = false,
                Width = 80,
                Height = 24,
                Location = new Point(0, 8),
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(90, 90, 90),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _searchBoxEmailAddresses = new TextBox
            {
                Location = new Point(84, 6),
                Width = 300,
                Height = 24,
                Font = new Font("Segoe UI", 9.5F)
            };
            _searchBoxEmailAddresses.TextChanged += (s, e) => ApplyEmailFilters();

            searchPanel.Controls.Add(searchLabel);
            searchPanel.Controls.Add(_searchBoxEmailAddresses);

            // Pagination bar lives at the TabPage level (outside the padded panel)
            var emailPagBar = BuildPaginationPanel(
                out _btnEmailFirst, out _btnEmailPrev, out _lblEmailPageInfo,
                out _btnEmailNext, out _btnEmailLast);
            _btnEmailFirst.Click += (s, e) => { _emailCurrentPage = 1; UpdateEmailDataGridView(); };
            _btnEmailPrev.Click  += (s, e) => { if (_emailCurrentPage > 1) { _emailCurrentPage--; UpdateEmailDataGridView(); } };
            _btnEmailNext.Click  += (s, e) =>
            {
                int tp = TotalPages(_filteredEmailAddresses?.Count ?? 0, EmailPageSize);
                if (_emailCurrentPage < tp) { _emailCurrentPage++; UpdateEmailDataGridView(); }
            };
            _btnEmailLast.Click  += (s, e) =>
            {
                _emailCurrentPage = TotalPages(_filteredEmailAddresses?.Count ?? 0, EmailPageSize);
                UpdateEmailDataGridView();
            };

            panel.Controls.Add(gridEmailAddresses);
            panel.Controls.Add(searchPanel);
            panel.Controls.Add(toolbar);

            // Add panel first (Fill), then pagination bar last (Bottom = processed first = docked at bottom)
            tabEmailAddresses.Controls.Add(panel);
            tabEmailAddresses.Controls.Add(emailPagBar);
        }

        private async Task LoadEmailAddressesAsync()
        {
            try
            {
                // Commit any pending edit before reloading
                gridEmailAddresses.CommitEdit(DataGridViewDataErrorContexts.Commit);

                var addresses = await _emailRepo.GetEmailAddressesAsync(activeOnly: false);
                _allEmailAddresses = addresses;
                ApplyEmailFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading email addresses: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAddEmailAddress_Click(object sender, EventArgs e)
        {
            _ = AddEmailAddressesAsync();
        }

        private void BtnEditEmailAddresses_Click(object sender, EventArgs e)
        {
            _ = OpenBulkEditDialogAsync();
        }

        private async Task OpenBulkEditDialogAsync()
        {
            try
            {
                var addresses = await _emailRepo.GetEmailAddressesAsync(activeOnly: false);
                using (var dlg = new BulkEditEmailAddressDialog(addresses, _emailRepo))
                {
                    dlg.ShowDialog(this);
                    await LoadEmailAddressesAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening edit dialog: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task AddEmailAddressesAsync()
        {
            int nextId = (_allEmailAddresses != null && _allEmailAddresses.Count > 0)
                ? _allEmailAddresses.Max(x => x.EmailId) + 1
                : 1;

            using (var dlg = new BulkAddEmailAddressDialog(nextId))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    foreach (var item in dlg.NewEmails)
                    {
                        item.CreatedByUserId = AppSession.CurrentUserId;
                        await _emailRepo.SaveEmailAddressAsync(item);
                    }

                    MessageBox.Show($"{dlg.NewEmails.Count} email address(es) added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await LoadEmailAddressesAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving email addresses: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnSaveEmailAddresses_Click(object sender, EventArgs e)
        {
            _ = SaveEmailAddressesAsync();
        }

        private async Task SaveEmailAddressesAsync()
        {
            // Commit any in-progress cell edit
            gridEmailAddresses.CommitEdit(DataGridViewDataErrorContexts.Commit);
            gridEmailAddresses.EndEdit();

            if (_allEmailAddresses == null || _allEmailAddresses.Count == 0)
                return;

            var errors = new List<string>();
            var toSave = new List<EmailAddressDto>();

            for (int i = 0; i < _allEmailAddresses.Count; i++)
            {
                var item = _allEmailAddresses[i];
                var emailRaw = (item.EmailAddress ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(emailRaw))
                {
                    errors.Add($"Row {i + 1}: Email address is required.");
                    continue;
                }

                try
                {
                    var parsed = new MailAddress(emailRaw);
                    item.EmailAddress = parsed.Address;
                }
                catch
                {
                    errors.Add($"Row {i + 1}: \"{emailRaw}\" is not a valid email address.");
                    continue;
                }

                item.DisplayName = string.IsNullOrWhiteSpace(item.DisplayName) ? null : item.DisplayName.Trim();
                toSave.Add(item);
            }

            if (errors.Count > 0)
            {
                MessageBox.Show(
                    "Please fix the following errors before saving:\n\n" + string.Join("\n", errors),
                    "Validation Errors",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                btnSaveEmailAddresses.Enabled = false;
                int saved = 0;

                foreach (var item in toSave)
                {
                    if (item.EmailId == 0)
                        item.CreatedByUserId = AppSession.CurrentUserId;

                    await _emailRepo.SaveEmailAddressAsync(item);
                    saved++;
                }

                MessageBox.Show($"{saved} email address(es) saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await LoadEmailAddressesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving email addresses: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSaveEmailAddresses.Enabled = true;
            }
        }

        private void BtnDeleteEmailAddress_Click(object sender, EventArgs e)
        {
            _ = DeleteEmailAddressAsync();
        }

        private async Task DeleteEmailAddressAsync()
        {
            if (gridEmailAddresses.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a row to deactivate.", "Deactivate Email Address", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selected = gridEmailAddresses.SelectedRows[0].DataBoundItem as EmailAddressDto;
            if (selected == null)
                return;

            var result = MessageBox.Show(
                $"Are you sure you want to deactivate:\n\n{selected.EmailAddress}\n\nThis will mark it as inactive.",
                "Confirm Deactivation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            try
            {
                var emailToDeactivate = await _emailRepo.GetEmailAddressByIdAsync(selected.EmailId);
                if (emailToDeactivate == null)
                {
                    MessageBox.Show("Email address not found.", "Deactivate", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                emailToDeactivate.IsActive = false;
                await _emailRepo.SaveEmailAddressAsync(emailToDeactivate);

                MessageBox.Show("Email address deactivated successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await LoadEmailAddressesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deactivating email address: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyEmailFilters()
        {
            if (_allEmailAddresses == null)
            {
                gridEmailAddresses.DataSource = null;
                return;
            }

            var searchText = (_searchBoxEmailAddresses?.Text ?? "").Trim().ToLowerInvariant();

            _filteredEmailAddresses = _allEmailAddresses.Where(a =>
            {
                if (!string.IsNullOrEmpty(searchText))
                {
                    bool matches =
                        (a.EmailAddress ?? "").ToLowerInvariant().Contains(searchText) ||
                        (a.DisplayName  ?? "").ToLowerInvariant().Contains(searchText);
                    if (!matches) return false;
                }
                return true;
            }).ToList();

            foreach (var kvp in _columnFiltersEmail)
            {
                var selected = kvp.Value;
                switch (kvp.Key)
                {
                    case "colEmailAddress":
                        _filteredEmailAddresses = _filteredEmailAddresses.Where(r => selected.Contains(r.EmailAddress ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                    case "colDisplayName":
                        _filteredEmailAddresses = _filteredEmailAddresses.Where(r => selected.Contains(r.DisplayName  ?? "", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                    case "colActive":
                        _filteredEmailAddresses = _filteredEmailAddresses.Where(r => selected.Contains(r.IsActive ? "Active" : "Inactive", StringComparer.OrdinalIgnoreCase)).ToList();
                        break;
                }
            }

            ApplySortToFilteredEmails();
            _emailCurrentPage = 1;
            UpdateEmailDataGridView();
        }

        private void UpdateEmailDataGridView()
        {
            if (_filteredEmailAddresses == null)
            {
                gridEmailAddresses.DataSource = null;
                UpdatePageInfo(_lblEmailPageInfo, 0, 1, EmailPageSize);
                return;
            }

            int totalCount = _filteredEmailAddresses.Count;
            int totalPages = TotalPages(totalCount, EmailPageSize);
            if (_emailCurrentPage > totalPages) _emailCurrentPage = totalPages;
            if (_emailCurrentPage < 1) _emailCurrentPage = 1;

            var paged = _filteredEmailAddresses
                .Skip((_emailCurrentPage - 1) * EmailPageSize)
                .Take(EmailPageSize)
                .ToList();

            gridEmailAddresses.DataSource = new BindingList<EmailAddressDto>(paged);
            UpdatePageInfo(_lblEmailPageInfo, totalCount, _emailCurrentPage, EmailPageSize);

            if (_btnEmailFirst != null) _btnEmailFirst.Enabled = _emailCurrentPage > 1;
            if (_btnEmailPrev  != null) _btnEmailPrev.Enabled  = _emailCurrentPage > 1;
            if (_btnEmailNext  != null) _btnEmailNext.Enabled  = _emailCurrentPage < totalPages;
            if (_btnEmailLast  != null) _btnEmailLast.Enabled  = _emailCurrentPage < totalPages;
        }

        private void ApplySortToFilteredEmails()
        {
            if (_filteredEmailAddresses == null || _sortColumnNameEmail == null) return;

            if (_sortColumnNameEmail == "colId")
            {
                _filteredEmailAddresses = _sortAscendingEmail
                    ? _filteredEmailAddresses.OrderBy(r => r.EmailId).ToList()
                    : _filteredEmailAddresses.OrderByDescending(r => r.EmailId).ToList();
                return;
            }

            Func<EmailAddressDto, string> key;
            switch (_sortColumnNameEmail)
            {
                case "colEmailAddress": key = r => r.EmailAddress ?? ""; break;
                case "colDisplayName":  key = r => r.DisplayName  ?? ""; break;
                case "colActive":       key = r => r.IsActive ? "Active" : "Inactive"; break;
                default: return;
            }

            _filteredEmailAddresses = _sortAscendingEmail
                ? _filteredEmailAddresses.OrderBy(key, StringComparer.OrdinalIgnoreCase).ToList()
                : _filteredEmailAddresses.OrderByDescending(key, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static readonly HashSet<string> _emailSortOnlyColumns = new HashSet<string> { "colId" };

        private void DgvEmailAddressesColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var col = gridEmailAddresses.Columns[e.ColumnIndex];
            if (col.SortMode != DataGridViewColumnSortMode.Programmatic) return;

            // Sort-only columns: entire header click = sort, no filter popup
            if (!_emailSortOnlyColumns.Contains(col.Name) && e.X >= col.Width - 18)
            {
                ShowEmailColumnFilterPopup(col);
                return;
            }

            if (_sortColumnNameEmail == col.Name)
                _sortAscendingEmail = !_sortAscendingEmail;
            else
            {
                _sortColumnNameEmail = col.Name;
                _sortAscendingEmail = true;
            }

            foreach (DataGridViewColumn c in gridEmailAddresses.Columns)
                c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            col.HeaderCell.SortGlyphDirection = _sortAscendingEmail ? System.Windows.Forms.SortOrder.Ascending : System.Windows.Forms.SortOrder.Descending;

            ApplyEmailFilters();
        }

        private void ShowEmailColumnFilterPopup(DataGridViewColumn col)
        {
            if (_allEmailAddresses == null) return;

            Func<EmailAddressDto, string> getter;
            switch (col.Name)
            {
                case "colEmailAddress": getter = r => r.EmailAddress ?? ""; break;
                case "colDisplayName":  getter = r => r.DisplayName  ?? ""; break;
                case "colActive":       getter = r => r.IsActive ? "Active" : "Inactive"; break;
                default: return;
            }

            var distinctValues = _allEmailAddresses
                .Select(getter)
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _columnFiltersEmail.TryGetValue(col.Name, out var currentFilter);

            var colRect  = gridEmailAddresses.GetColumnDisplayRectangle(col.Index, false);
            var screenPt = gridEmailAddresses.PointToScreen(new Point(colRect.Left, gridEmailAddresses.ColumnHeadersHeight));

            using (var popup = new ColumnFilterPopup(col.HeaderText, distinctValues, currentFilter))
            {
                popup.Location = screenPt;

                var screen = Screen.FromPoint(screenPt).WorkingArea;
                if (popup.Right  > screen.Right)  popup.Left = Math.Max(screen.Left, screen.Right - popup.Width);
                if (popup.Bottom > screen.Bottom) popup.Top  = Math.Max(screen.Top,  screenPt.Y - popup.Height - gridEmailAddresses.ColumnHeadersHeight);

                popup.ShowDialog(this);

                if (popup.Action == ColumnFilterPopup.PopupAction.SortAscending)
                {
                    _sortColumnNameEmail = col.Name;
                    _sortAscendingEmail  = true;
                    foreach (DataGridViewColumn c in gridEmailAddresses.Columns)
                        c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                    col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Ascending;
                    ApplyEmailFilters();
                }
                else if (popup.Action == ColumnFilterPopup.PopupAction.SortDescending)
                {
                    _sortColumnNameEmail = col.Name;
                    _sortAscendingEmail  = false;
                    foreach (DataGridViewColumn c in gridEmailAddresses.Columns)
                        c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                    col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.Descending;
                    ApplyEmailFilters();
                }
                else if (popup.Action == ColumnFilterPopup.PopupAction.Filter)
                {
                    if (popup.SelectedValues == null || popup.SelectedValues.Count == 0
                        || popup.SelectedValues.Count >= distinctValues.Count)
                        _columnFiltersEmail.Remove(col.Name);
                    else
                        _columnFiltersEmail[col.Name] = popup.SelectedValues;

                    ApplyEmailFilters();
                }
            }
        }

        private void DgvEmailAddressesCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 || e.ColumnIndex < 0) return;

            var col = gridEmailAddresses.Columns[e.ColumnIndex];
            if (col == null || col.SortMode != DataGridViewColumnSortMode.Programmatic) return;

            bool sortOnly = _emailSortOnlyColumns.Contains(col.Name);

            e.Handled = true;

            using (var brush = new SolidBrush(e.CellStyle.BackColor))
                e.Graphics.FillRectangle(brush, e.CellBounds);

            var headerRect = new Rectangle(e.CellBounds.X, e.CellBounds.Y, e.CellBounds.Width - 1, e.CellBounds.Height - 1);
            using (var brush = new SolidBrush(e.CellStyle.BackColor))
                e.Graphics.FillRectangle(brush, headerRect);

            using (var pen = new Pen(Color.White, 1))
            {
                e.Graphics.DrawLine(pen, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }

            // Sort-only: reserve space only for the arrow (no filter ▼)
            var textRect = e.CellBounds;
            textRect.Width -= sortOnly ? 18 : 34;

            int glyphX = sortOnly ? e.CellBounds.Right - 16 : e.CellBounds.Right - 32;
            int glyphY = e.CellBounds.Y + (e.CellBounds.Height - 8) / 2;

            Color arrowColor = col.HeaderCell.SortGlyphDirection != System.Windows.Forms.SortOrder.None
                ? Color.White
                : Color.FromArgb(160, 255, 255, 255);

            using (var arrowPen = new Pen(arrowColor, 2))
            {
                if (col.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Ascending)
                {
                    e.Graphics.DrawLine(arrowPen, glyphX,      glyphY + 6, glyphX + 5,  glyphY);
                    e.Graphics.DrawLine(arrowPen, glyphX + 5,  glyphY,     glyphX + 10, glyphY + 6);
                }
                else if (col.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Descending)
                {
                    e.Graphics.DrawLine(arrowPen, glyphX,      glyphY,     glyphX + 5,  glyphY + 6);
                    e.Graphics.DrawLine(arrowPen, glyphX + 5,  glyphY + 6, glyphX + 10, glyphY);
                }
                else
                {
                    e.Graphics.DrawLine(arrowPen, glyphX + 2, glyphY + 2, glyphX + 5, glyphY - 1);
                    e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY - 1, glyphX + 8, glyphY + 2);
                    e.Graphics.DrawLine(arrowPen, glyphX + 2, glyphY + 4, glyphX + 5, glyphY + 7);
                    e.Graphics.DrawLine(arrowPen, glyphX + 5, glyphY + 7, glyphX + 8, glyphY + 4);
                }
            }

            // Filter ▼ — only for filterable columns
            if (!sortOnly)
            {
                bool hasFilter = _columnFiltersEmail.ContainsKey(col.Name);
                int  filterX    = e.CellBounds.Right - 14;
                int  filterMidY = e.CellBounds.Y + e.CellBounds.Height / 2;
                var  filterPts  = new PointF[]
                {
                    new PointF(filterX,     filterMidY - 4),
                    new PointF(filterX + 9, filterMidY - 4),
                    new PointF(filterX + 4, filterMidY + 3)
                };
                using (var filterBrush = new SolidBrush(hasFilter ? Color.FromArgb(255, 230, 80) : Color.FromArgb(140, 255, 255, 255)))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillPolygon(filterBrush, filterPts);
                    e.Graphics.SmoothingMode = SmoothingMode.Default;
                }
            }

            using (var textBrush = new SolidBrush(Color.White))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                e.Graphics.DrawString(col.HeaderText, e.CellStyle.Font ?? gridEmailAddresses.Font, textBrush, textRect, fmt);
            }
        }

        internal sealed class BulkEditEmailAddressDialog : Form
        {
            private readonly EmailRepository _repo;
            private DataGridView gridEmails;
            private BindingList<EmailAddressDto> _list;
            private Button btnDeactivateSelected;
            private Button btnDeleteSelected;
            private bool _headerSelectChecked = false;
            private bool _headerActiveChecked = false;
            private const int SelectColIdx = 0;
            private const int ActiveColIdx = 4;

            public BulkEditEmailAddressDialog(List<EmailAddressDto> emails, EmailRepository repo)
            {
                _repo = repo;
                _list = new BindingList<EmailAddressDto>(emails);
                InitializeComponent();
            }

            private void InitializeComponent()
            {
                Text = "Edit Email Addresses";
                FormBorderStyle = FormBorderStyle.Sizable;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = true;
                MinimizeBox = false;
                ShowInTaskbar = false;
                Size = new Size(950, 560);
                MinimumSize = new Size(750, 420);
                Font = new Font("Segoe UI", 9.5F);

                gridEmails = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AutoGenerateColumns = false,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    ReadOnly = false,
                    SelectionMode = DataGridViewSelectionMode.CellSelect,
                    MultiSelect = false,
                    EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
                };

                UiFactory.StyleGrid(gridEmails);
                gridEmails.CellBorderStyle = DataGridViewCellBorderStyle.SingleVertical;
                gridEmails.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
                gridEmails.GridColor = Color.FromArgb(200, 200, 200);
                gridEmails.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
                gridEmails.DefaultCellStyle.SelectionBackColor = Color.FromArgb(180, 210, 240);
                gridEmails.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 20, 20);

                gridEmails.Columns.Add(new DataGridViewCheckBoxColumn
                {
                    HeaderText = "",
                    Name = "colSelect",
                    Width = 40,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
                });
                gridEmails.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "ID",
                    DataPropertyName = "EmailId",
                    Width = 60,
                    ReadOnly = true,
                    HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleCenter,
                        BackColor = Color.FromArgb(245, 245, 245),
                        ForeColor = Color.Gray
                    }
                });
                gridEmails.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "Email Address",
                    DataPropertyName = "EmailAddress",
                    FillWeight = 50,
                    MinimumWidth = 300,
                    HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
                });
                gridEmails.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "Display Name",
                    DataPropertyName = "DisplayName",
                    FillWeight = 35,
                    MinimumWidth = 200,
                    HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
                });
                gridEmails.Columns.Add(new DataGridViewCheckBoxColumn
                {
                    HeaderText = "Active",
                    DataPropertyName = "IsActive",
                    Width = 80,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
                });

                gridEmails.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                gridEmails.CellPainting += GridEmails_CellPainting;
                gridEmails.ColumnHeaderMouseClick += GridEmails_ColumnHeaderMouseClick;
                gridEmails.DataSource = _list;

                // Toolbar
                var toolbar = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    Height = 50,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Padding = new Padding(0, 10, 0, 8),
                    AutoSize = false
                };

                btnDeactivateSelected = new Button
                {
                    Text = "Deactivate Selected",
                    Width = 150,
                    Height = 32,
                    Margin = new Padding(0, 0, 10, 0)
                };
                btnDeactivateSelected.Click += BtnDeactivateSelected_Click;

                btnDeleteSelected = new Button
                {
                    Text = "Delete Selected",
                    Width = 130,
                    Height = 32,
                    Margin = new Padding(0, 0, 0, 0)
                };
                btnDeleteSelected.Click += BtnDeleteSelected_Click;

                toolbar.Controls.Add(btnDeactivateSelected);
                toolbar.Controls.Add(btnDeleteSelected);

                // Bottom buttons
                var bottomPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 55,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    Padding = new Padding(0, 12, 0, 0),
                    AutoSize = false
                };

                var btnCancel = new Button
                {
                    Text = "Cancel",
                    Width = 90,
                    Height = 32,
                    Margin = new Padding(10, 0, 0, 0)
                };
                var btnSave = new Button
                {
                    Text = "Save Changes",
                    Width = 120,
                    Height = 32,
                    Margin = new Padding(0, 0, 0, 0)
                };

                btnSave.Click += BtnSave_Click;
                btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

                bottomPanel.Controls.Add(btnCancel);
                bottomPanel.Controls.Add(btnSave);

                var contentPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 0, 15, 0) };
                contentPanel.Controls.Add(gridEmails);
                contentPanel.Controls.Add(toolbar);

                Controls.Add(contentPanel);
                Controls.Add(bottomPanel);

                CancelButton = btnCancel;
            }

            private void GridEmails_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
            {
                if (e.RowIndex != -1)
                    return;

                if (e.ColumnIndex == SelectColIdx)
                {
                    e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

                    var state = _headerSelectChecked
                        ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                        : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
                    var size = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
                    var pt = new Point(
                        e.CellBounds.X + (e.CellBounds.Width - size.Width) / 2,
                        e.CellBounds.Y + (e.CellBounds.Height - size.Height) / 2);
                    CheckBoxRenderer.DrawCheckBox(e.Graphics, pt, state);
                    e.Handled = true;
                }
                else if (e.ColumnIndex == ActiveColIdx)
                {
                    e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

                    var state = _headerActiveChecked
                        ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                        : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
                    var checkSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
                    const string label = "Active";
                    var textSize = TextRenderer.MeasureText(e.Graphics, label, gridEmails.Font);

                    int totalWidth = checkSize.Width + 4 + textSize.Width;
                    int startX = e.CellBounds.X + (e.CellBounds.Width - totalWidth) / 2;
                    int checkY = e.CellBounds.Y + (e.CellBounds.Height - checkSize.Height) / 2;

                    CheckBoxRenderer.DrawCheckBox(e.Graphics, new Point(startX, checkY), state);
                    TextRenderer.DrawText(
                        e.Graphics, label, gridEmails.Font,
                        new Point(startX + checkSize.Width + 4, e.CellBounds.Y + (e.CellBounds.Height - textSize.Height) / 2),
                        e.CellStyle.ForeColor);
                    e.Handled = true;
                }
            }

            private void GridEmails_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
            {
                if (e.ColumnIndex == SelectColIdx)
                {
                    _headerSelectChecked = !_headerSelectChecked;
                    for (int i = 0; i < gridEmails.Rows.Count; i++)
                        gridEmails.Rows[i].Cells[SelectColIdx].Value = _headerSelectChecked;
                    gridEmails.RefreshEdit();
                    gridEmails.Invalidate();
                }
                else if (e.ColumnIndex == ActiveColIdx)
                {
                    _headerActiveChecked = !_headerActiveChecked;
                    for (int i = 0; i < gridEmails.Rows.Count; i++)
                        gridEmails.Rows[i].Cells[ActiveColIdx].Value = _headerActiveChecked;
                    gridEmails.RefreshEdit();
                    gridEmails.Invalidate();
                }
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (keyData == Keys.Enter && gridEmails.ContainsFocus)
                {
                    var cell = gridEmails.CurrentCell;
                    if (cell != null && cell.RowIndex >= 0 &&
                        (cell.ColumnIndex == SelectColIdx || cell.ColumnIndex == ActiveColIdx))
                    {
                        cell.Value = !(cell.Value as bool? ?? false);
                        gridEmails.RefreshEdit();
                        return true;
                    }
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            private void BtnDeactivateSelected_Click(object sender, EventArgs e)
            {
                _ = DeactivateSelectedAsync();
            }

            private List<EmailAddressDto> GetCheckedRows()
            {
                gridEmails.CommitEdit(DataGridViewDataErrorContexts.Commit);
                var result = new List<EmailAddressDto>();
                for (int i = 0; i < gridEmails.Rows.Count; i++)
                {
                    if (gridEmails.Rows[i].Cells[SelectColIdx].Value as bool? == true)
                    {
                        var item = gridEmails.Rows[i].DataBoundItem as EmailAddressDto;
                        if (item != null) result.Add(item);
                    }
                }
                return result;
            }

            private async Task DeactivateSelectedAsync()
            {
                var selected = GetCheckedRows();
                if (selected.Count == 0)
                {
                    MessageBox.Show("Check one or more rows to deactivate.", "Deactivate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Deactivate {selected.Count} email address(es)?",
                    "Confirm Deactivate",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                    return;

                try
                {
                    btnDeactivateSelected.Enabled = false;
                    foreach (var item in selected)
                    {
                        item.IsActive = false;
                        await _repo.SaveEmailAddressAsync(item);
                    }

                    for (int i = 0; i < gridEmails.Rows.Count; i++)
                    {
                        var item = gridEmails.Rows[i].DataBoundItem as EmailAddressDto;
                        if (item != null && selected.Contains(item))
                        {
                            gridEmails.Rows[i].Cells[ActiveColIdx].Value = false;
                            gridEmails.Rows[i].Cells[SelectColIdx].Value = false;
                        }
                    }

                    _headerSelectChecked = false;
                    gridEmails.Invalidate();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deactivating: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    btnDeactivateSelected.Enabled = true;
                }
            }

            private void BtnDeleteSelected_Click(object sender, EventArgs e)
            {
                _ = DeleteSelectedAsync();
            }

            private async Task DeleteSelectedAsync()
            {
                var selected = GetCheckedRows();
                if (selected.Count == 0)
                {
                    MessageBox.Show("Check one or more rows to delete.", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Permanently delete {selected.Count} email address(es)? This cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;

                try
                {
                    btnDeleteSelected.Enabled = false;
                    foreach (var item in selected)
                    {
                        await _repo.DeleteEmailAddressAsync(item.EmailId);
                        _list.Remove(item);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    btnDeleteSelected.Enabled = true;
                }
            }

            private async void BtnSave_Click(object sender, EventArgs e)
            {
                gridEmails.CommitEdit(DataGridViewDataErrorContexts.Commit);
                gridEmails.EndEdit();

                var errors = new List<string>();
                var toSave = new List<EmailAddressDto>();

                for (int i = 0; i < _list.Count; i++)
                {
                    var item = _list[i];
                    var emailRaw = (item.EmailAddress ?? string.Empty).Trim();

                    if (string.IsNullOrWhiteSpace(emailRaw))
                    {
                        errors.Add($"Row {i + 1} (ID {item.EmailId}): Email address is required.");
                        continue;
                    }

                    try
                    {
                        var parsed = new MailAddress(emailRaw);
                        item.EmailAddress = parsed.Address;
                    }
                    catch
                    {
                        errors.Add($"Row {i + 1} (ID {item.EmailId}): \"{emailRaw}\" is not a valid email address.");
                        continue;
                    }

                    item.DisplayName = string.IsNullOrWhiteSpace(item.DisplayName) ? null : item.DisplayName.Trim();
                    toSave.Add(item);
                }

                if (errors.Count > 0)
                {
                    MessageBox.Show(
                        "Please fix the following errors:\n\n" + string.Join("\n", errors),
                        "Validation Errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    foreach (var item in toSave)
                        await _repo.SaveEmailAddressAsync(item);

                    MessageBox.Show($"{toSave.Count} email address(es) saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        internal sealed class BulkAddEmailAddressDialog : Form
        {
            private readonly int _nextId;
            private DataGridView gridNewEmails;
            private BindingList<EmailAddressDto> _newEmailList;
            private Button btnAddRow;
            private Button btnRemoveRow;
            private Button btnSave;
            private Button btnCancel;
            private bool _headerActiveChecked = false;
            private const int ActiveColumnIndex = 3;

            public List<EmailAddressDto> NewEmails { get; private set; }

            public BulkAddEmailAddressDialog(int nextId)
            {
                _nextId = nextId;
                InitializeComponent();
            }

            private void InitializeComponent()
            {
                Text = "Add Email Addresses";
                FormBorderStyle = FormBorderStyle.Sizable;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = true;
                MinimizeBox = false;
                ShowInTaskbar = false;
                Size = new Size(950, 560);
                MinimumSize = new Size(750, 420);
                Font = new Font("Segoe UI", 9.5F);

                gridNewEmails = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AutoGenerateColumns = false,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    ReadOnly = false,
                    SelectionMode = DataGridViewSelectionMode.CellSelect,
                    MultiSelect = false,
                    EditMode = DataGridViewEditMode.EditOnEnter
                };

                UiFactory.StyleGrid(gridNewEmails);
                gridNewEmails.CellBorderStyle = DataGridViewCellBorderStyle.SingleVertical;
                gridNewEmails.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
                gridNewEmails.GridColor = Color.FromArgb(200, 200, 200);
                gridNewEmails.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
                gridNewEmails.DefaultCellStyle.SelectionBackColor = Color.FromArgb(180, 210, 240);
                gridNewEmails.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 20, 20);

                gridNewEmails.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "ID",
                    DataPropertyName = "EmailId",
                    Width = 60,
                    ReadOnly = true,
                    HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleCenter,
                        BackColor = Color.FromArgb(245, 245, 245),
                        ForeColor = Color.Gray
                    }
                });
                gridNewEmails.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "Email Address",
                    DataPropertyName = "EmailAddress",
                    FillWeight = 50,
                    MinimumWidth = 300,
                    HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
                });
                gridNewEmails.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "Display Name",
                    DataPropertyName = "DisplayName",
                    FillWeight = 35,
                    MinimumWidth = 200,
                    HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
                });
                gridNewEmails.Columns.Add(new DataGridViewCheckBoxColumn
                {
                    HeaderText = "Active",
                    DataPropertyName = "IsActive",
                    Width = 80,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
                });

                gridNewEmails.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

                gridNewEmails.CellPainting += GridNewEmails_CellPainting;
                gridNewEmails.ColumnHeaderMouseClick += GridNewEmails_ColumnHeaderMouseClick;

                _newEmailList = new BindingList<EmailAddressDto>();
                gridNewEmails.DataSource = _newEmailList;

                // Start with one blank row
                AddNewRow();

                // Top toolbar
                var toolbar = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    Height = 50,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Padding = new Padding(0, 10, 0, 8),
                    AutoSize = false
                };

                btnAddRow = new Button
                {
                    Text = "Add Row",
                    Width = 100,
                    Height = 32,
                    Margin = new Padding(0, 0, 10, 0)
                };
                btnAddRow.Click += (s, e) => AddNewRow();

                btnRemoveRow = new Button
                {
                    Text = "Remove Row",
                    Width = 110,
                    Height = 32,
                    Margin = new Padding(0, 0, 0, 0)
                };
                btnRemoveRow.Click += (s, e) => RemoveSelectedRow();

                toolbar.Controls.Add(btnAddRow);
                toolbar.Controls.Add(btnRemoveRow);

                // Bottom buttons
                var bottomPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 55,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    Padding = new Padding(0, 12, 0, 0),
                    AutoSize = false
                };

                btnCancel = new Button
                {
                    Text = "Cancel",
                    Width = 90,
                    Height = 32,
                    Margin = new Padding(10, 0, 0, 0)
                };
                btnSave = new Button
                {
                    Text = "Save",
                    Width = 90,
                    Height = 32,
                    Margin = new Padding(0, 0, 0, 0)
                };

                btnSave.Click += BtnSave_Click;
                btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

                bottomPanel.Controls.Add(btnCancel);
                bottomPanel.Controls.Add(btnSave);

                var contentPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 0, 15, 0) };
                contentPanel.Controls.Add(gridNewEmails);
                contentPanel.Controls.Add(toolbar);

                Controls.Add(contentPanel);
                Controls.Add(bottomPanel);

                AcceptButton = btnSave;
                CancelButton = btnCancel;
            }

            private void AddNewRow()
            {
                var projected = _nextId + _newEmailList.Count;
                var newEntry = new EmailAddressDto
                {
                    EmailId = projected,
                    EmailAddress = string.Empty,
                    DisplayName = string.Empty,
                    IsActive = true
                };
                _newEmailList.Add(newEntry);

                var newRowIndex = gridNewEmails.Rows.Count - 1;
                if (newRowIndex >= 0)
                {
                    gridNewEmails.ClearSelection();
                    gridNewEmails.Rows[newRowIndex].Selected = true;
                    gridNewEmails.CurrentCell = gridNewEmails.Rows[newRowIndex].Cells[1]; // Email column
                    gridNewEmails.BeginEdit(true);
                }
            }

            private void RemoveSelectedRow()
            {
                var cell = gridNewEmails.CurrentCell;
                if (cell == null || cell.RowIndex < 0)
                    return;

                var item = gridNewEmails.Rows[cell.RowIndex].DataBoundItem as EmailAddressDto;
                if (item != null)
                    _newEmailList.Remove(item);
            }

            private void GridNewEmails_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
            {
                if (e.RowIndex != -1 || e.ColumnIndex != ActiveColumnIndex)
                    return;

                e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

                var checkState = _headerActiveChecked
                    ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                    : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;

                var checkSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, checkState);
                const string label = "Active";
                var textSize = TextRenderer.MeasureText(e.Graphics, label, gridNewEmails.Font);

                int totalWidth = checkSize.Width + 4 + textSize.Width;
                int startX = e.CellBounds.X + (e.CellBounds.Width - totalWidth) / 2;
                int checkY = e.CellBounds.Y + (e.CellBounds.Height - checkSize.Height) / 2;

                CheckBoxRenderer.DrawCheckBox(e.Graphics, new Point(startX, checkY), checkState);

                TextRenderer.DrawText(
                    e.Graphics,
                    label,
                    gridNewEmails.Font,
                    new Point(startX + checkSize.Width + 4, e.CellBounds.Y + (e.CellBounds.Height - textSize.Height) / 2),
                    e.CellStyle.ForeColor);

                e.Handled = true;
            }

            private void GridNewEmails_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
            {
                if (e.ColumnIndex != ActiveColumnIndex)
                    return;

                _headerActiveChecked = !_headerActiveChecked;
                for (int i = 0; i < gridNewEmails.Rows.Count; i++)
                    gridNewEmails.Rows[i].Cells[ActiveColumnIndex].Value = _headerActiveChecked;

                gridNewEmails.RefreshEdit();
                gridNewEmails.Invalidate();
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (keyData == Keys.Enter && gridNewEmails.ContainsFocus)
                {
                    var cell = gridNewEmails.CurrentCell;
                    if (cell != null && cell.ColumnIndex == ActiveColumnIndex && cell.RowIndex >= 0)
                    {
                        var current = cell.Value as bool? ?? false;
                        cell.Value = !current;
                        gridNewEmails.RefreshEdit();
                        return true;
                    }
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }

            private void BtnSave_Click(object sender, EventArgs e)
            {
                gridNewEmails.CommitEdit(DataGridViewDataErrorContexts.Commit);
                gridNewEmails.EndEdit();

                var errors = new List<string>();
                var toSave = new List<EmailAddressDto>();

                for (int i = 0; i < _newEmailList.Count; i++)
                {
                    var item = _newEmailList[i];
                    var emailRaw = (item.EmailAddress ?? string.Empty).Trim();

                    if (string.IsNullOrWhiteSpace(emailRaw))
                    {
                        errors.Add($"Row {i + 1}: Email address is required.");
                        continue;
                    }

                    try
                    {
                        var parsed = new MailAddress(emailRaw);
                        item.EmailAddress = parsed.Address;
                    }
                    catch
                    {
                        errors.Add($"Row {i + 1}: \"{emailRaw}\" is not a valid email address.");
                        continue;
                    }

                    item.DisplayName = string.IsNullOrWhiteSpace(item.DisplayName) ? null : item.DisplayName.Trim();
                    item.EmailId = 0; // Reset so DB assigns the real ID
                    toSave.Add(item);
                }

                if (errors.Count > 0)
                {
                    MessageBox.Show(
                        "Please fix the following errors before saving:\n\n" + string.Join("\n", errors),
                        "Validation Errors",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (toSave.Count == 0)
                {
                    MessageBox.Show("No valid email addresses to save.", "Add Email Addresses", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                NewEmails = toSave;
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        #endregion

        #region SMTP Profiles Tab

        private void BuildSmtpProfilesTab()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };

            gridSmtpProfiles = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };

            UiFactory.StyleGrid(gridSmtpProfiles);

            // Add vertical borders for better column distinction
            gridSmtpProfiles.CellBorderStyle = DataGridViewCellBorderStyle.SingleVertical;
            gridSmtpProfiles.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            gridSmtpProfiles.GridColor = Color.FromArgb(200, 200, 200);

            // Ensure header alignment matches cell alignment
            gridSmtpProfiles.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            gridSmtpProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "ID",
                DataPropertyName = "ProfileId",
                Width = 80,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            gridSmtpProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Profile Name",
                DataPropertyName = "ProfileName",
                FillWeight = 20,
                MinimumWidth = 150,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });
            gridSmtpProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "SMTP Server",
                DataPropertyName = "SmtpServer",
                FillWeight = 25,
                MinimumWidth = 150,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });
            gridSmtpProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Port",
                DataPropertyName = "SmtpPort",
                Width = 80,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            gridSmtpProfiles.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "SSL",
                DataPropertyName = "UseSsl",
                Width = 80,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            gridSmtpProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "From Email",
                DataPropertyName = "FromEmailAddress",
                FillWeight = 30,
                MinimumWidth = 180,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });
            gridSmtpProfiles.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "Active",
                DataPropertyName = "IsActive",
                Width = 100,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            gridSmtpProfiles.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            gridSmtpProfiles.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                    BtnEditSmtpProfile_Click(s, e);
            };

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 60,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 14, 0, 14),
                AutoSize = false
            };

            btnAddSmtpProfile = new Button
            {
                Text = "Add Profile",
                Width = 120,
                Height = 32,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnAddSmtpProfile.Click += BtnAddSmtpProfile_Click;

            btnEditSmtpProfile = new Button
            {
                Text = "Edit Profile",
                Width = 120,
                Height = 32,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnEditSmtpProfile.Click += BtnEditSmtpProfile_Click;

            btnRefreshSmtpProfiles = new Button
            {
                Text = "Refresh",
                Width = 100,
                Height = 32,
                Margin = new Padding(0, 0, 0, 0)
            };
            btnRefreshSmtpProfiles.Click += (s, e) => _ = LoadSmtpProfilesAsync();

            toolbar.Controls.Add(btnAddSmtpProfile);
            toolbar.Controls.Add(btnEditSmtpProfile);
            toolbar.Controls.Add(btnRefreshSmtpProfiles);

            var smtpPagBar = BuildPaginationPanel(
                out _btnSmtpFirst, out _btnSmtpPrev, out _lblSmtpPageInfo,
                out _btnSmtpNext, out _btnSmtpLast);
            _btnSmtpFirst.Click += (s, e) => { _smtpCurrentPage = 1; UpdateSmtpDataGridView(); };
            _btnSmtpPrev.Click  += (s, e) => { if (_smtpCurrentPage > 1) { _smtpCurrentPage--; UpdateSmtpDataGridView(); } };
            _btnSmtpNext.Click  += (s, e) =>
            {
                int tp = TotalPages(_allSmtpProfiles?.Count ?? 0, SmtpPageSize);
                if (_smtpCurrentPage < tp) { _smtpCurrentPage++; UpdateSmtpDataGridView(); }
            };
            _btnSmtpLast.Click  += (s, e) =>
            {
                _smtpCurrentPage = TotalPages(_allSmtpProfiles?.Count ?? 0, SmtpPageSize);
                UpdateSmtpDataGridView();
            };

            panel.Controls.Add(gridSmtpProfiles);
            panel.Controls.Add(toolbar);

            tabSmtpProfiles.Controls.Add(panel);
            tabSmtpProfiles.Controls.Add(smtpPagBar);
        }

        private async Task LoadSmtpProfilesAsync()
        {
            try
            {
                _allSmtpProfiles = await _emailRepo.GetSmtpProfilesAsync(activeOnly: false);
                _smtpCurrentPage = 1;
                UpdateSmtpDataGridView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading SMTP profiles: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateSmtpDataGridView()
        {
            if (_allSmtpProfiles == null)
            {
                gridSmtpProfiles.DataSource = null;
                UpdatePageInfo(_lblSmtpPageInfo, 0, 1, SmtpPageSize);
                return;
            }

            int totalCount = _allSmtpProfiles.Count;
            int totalPages = TotalPages(totalCount, SmtpPageSize);
            if (_smtpCurrentPage > totalPages) _smtpCurrentPage = totalPages;
            if (_smtpCurrentPage < 1) _smtpCurrentPage = 1;

            var paged = _allSmtpProfiles
                .Skip((_smtpCurrentPage - 1) * SmtpPageSize)
                .Take(SmtpPageSize)
                .ToList();

            gridSmtpProfiles.DataSource = paged;
            UpdatePageInfo(_lblSmtpPageInfo, totalCount, _smtpCurrentPage, SmtpPageSize);

            if (_btnSmtpFirst != null) _btnSmtpFirst.Enabled = _smtpCurrentPage > 1;
            if (_btnSmtpPrev  != null) _btnSmtpPrev.Enabled  = _smtpCurrentPage > 1;
            if (_btnSmtpNext  != null) _btnSmtpNext.Enabled  = _smtpCurrentPage < totalPages;
            if (_btnSmtpLast  != null) _btnSmtpLast.Enabled  = _smtpCurrentPage < totalPages;
        }

        private async void BtnAddSmtpProfile_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dlg = new SystemSmtpProfileDialog(null, _emailRepo))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        await _emailRepo.SaveSmtpProfileAsync(dlg.Profile);
                        MessageBox.Show("SMTP Profile saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        await LoadSmtpProfilesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving SMTP profile: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnEditSmtpProfile_Click(object sender, EventArgs e)
        {
            if (gridSmtpProfiles.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a profile to edit.", "Edit Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var selected = (SystemSmtpProfileDto)gridSmtpProfiles.SelectedRows[0].DataBoundItem;
                var profile = await _emailRepo.GetSmtpProfileByIdAsync(selected.ProfileId);

                using (var dlg = new SystemSmtpProfileDialog(profile, _emailRepo))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        await _emailRepo.SaveSmtpProfileAsync(dlg.Profile);
                        MessageBox.Show("SMTP Profile updated successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        await LoadSmtpProfilesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating SMTP profile: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Templates Tab

        private void BuildTemplatesTab()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };

            gridTemplates = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };

            UiFactory.StyleGrid(gridTemplates);

            // Add vertical borders for better column distinction
            gridTemplates.CellBorderStyle = DataGridViewCellBorderStyle.SingleVertical;
            gridTemplates.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            gridTemplates.GridColor = Color.FromArgb(200, 200, 200);

            // Ensure header alignment matches cell alignment
            gridTemplates.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            gridTemplates.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "ID",
                DataPropertyName = "TemplateId",
                Width = 80,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            gridTemplates.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Template Key",
                DataPropertyName = "TemplateKey",
                FillWeight = 25,
                MinimumWidth = 180,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });
            gridTemplates.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Subject",
                DataPropertyName = "SubjectTemplate",
                FillWeight = 50,
                MinimumWidth = 250,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });
            gridTemplates.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "HTML",
                DataPropertyName = "IsHtml",
                Width = 100,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            gridTemplates.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "Active",
                DataPropertyName = "IsActive",
                Width = 100,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            gridTemplates.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            gridTemplates.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                    BtnEditTemplate_Click(s, e);
            };

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 60,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 14, 0, 14),
                AutoSize = false
            };

            btnAddTemplate = new Button
            {
                Text = "Add Template",
                Width = 120,
                Height = 32,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnAddTemplate.Click += BtnAddTemplate_Click;

            btnEditTemplate = new Button
            {
                Text = "Edit Template",
                Width = 120,
                Height = 32,
                Margin = new Padding(0, 0, 10, 0)
            };
            btnEditTemplate.Click += BtnEditTemplate_Click;

            btnRefreshTemplates = new Button
            {
                Text = "Refresh",
                Width = 100,
                Height = 32,
                Margin = new Padding(0, 0, 0, 0)
            };
            btnRefreshTemplates.Click += (s, e) => _ = LoadTemplatesAsync();

            toolbar.Controls.Add(btnAddTemplate);
            toolbar.Controls.Add(btnEditTemplate);
            toolbar.Controls.Add(btnRefreshTemplates);

            var tplPagBar = BuildPaginationPanel(
                out _btnTplFirst, out _btnTplPrev, out _lblTplPageInfo,
                out _btnTplNext, out _btnTplLast);
            _btnTplFirst.Click += (s, e) => { _templatesCurrentPage = 1; UpdateTemplatesDataGridView(); };
            _btnTplPrev.Click  += (s, e) => { if (_templatesCurrentPage > 1) { _templatesCurrentPage--; UpdateTemplatesDataGridView(); } };
            _btnTplNext.Click  += (s, e) =>
            {
                int tp = TotalPages(_allTemplates?.Count ?? 0, TemplatesPageSize);
                if (_templatesCurrentPage < tp) { _templatesCurrentPage++; UpdateTemplatesDataGridView(); }
            };
            _btnTplLast.Click  += (s, e) =>
            {
                _templatesCurrentPage = TotalPages(_allTemplates?.Count ?? 0, TemplatesPageSize);
                UpdateTemplatesDataGridView();
            };

            panel.Controls.Add(gridTemplates);
            panel.Controls.Add(toolbar);

            tabTemplates.Controls.Add(panel);
            tabTemplates.Controls.Add(tplPagBar);
        }

        private async Task LoadTemplatesAsync()
        {
            try
            {
                _allTemplates = await _emailRepo.GetEmailTemplatesAsync(activeOnly: false);
                _templatesCurrentPage = 1;
                UpdateTemplatesDataGridView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading templates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateTemplatesDataGridView()
        {
            if (_allTemplates == null)
            {
                gridTemplates.DataSource = null;
                UpdatePageInfo(_lblTplPageInfo, 0, 1, TemplatesPageSize);
                return;
            }

            int totalCount = _allTemplates.Count;
            int totalPages = TotalPages(totalCount, TemplatesPageSize);
            if (_templatesCurrentPage > totalPages) _templatesCurrentPage = totalPages;
            if (_templatesCurrentPage < 1) _templatesCurrentPage = 1;

            var paged = _allTemplates
                .Skip((_templatesCurrentPage - 1) * TemplatesPageSize)
                .Take(TemplatesPageSize)
                .ToList();

            gridTemplates.DataSource = paged;
            UpdatePageInfo(_lblTplPageInfo, totalCount, _templatesCurrentPage, TemplatesPageSize);

            if (_btnTplFirst != null) _btnTplFirst.Enabled = _templatesCurrentPage > 1;
            if (_btnTplPrev  != null) _btnTplPrev.Enabled  = _templatesCurrentPage > 1;
            if (_btnTplNext  != null) _btnTplNext.Enabled  = _templatesCurrentPage < totalPages;
            if (_btnTplLast  != null) _btnTplLast.Enabled  = _templatesCurrentPage < totalPages;
        }

        private async void BtnAddTemplate_Click(object sender, EventArgs e)
        {
            try
            {
                var profiles = await _emailRepo.GetSmtpProfilesAsync(activeOnly: false);
                using (var dlg = new EmailTemplateEditorDialog(null, profiles))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        await _emailRepo.SaveEmailTemplateAsync(dlg.Template);
                        MessageBox.Show("Template saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        await LoadTemplatesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving template: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnEditTemplate_Click(object sender, EventArgs e)
        {
            if (gridTemplates.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a template to edit.", "Edit Template", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var selected = (EmailTemplateDto)gridTemplates.SelectedRows[0].DataBoundItem;
                var profiles = await _emailRepo.GetSmtpProfilesAsync(activeOnly: false);

                using (var dlg = new EmailTemplateEditorDialog(selected, profiles))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        await _emailRepo.SaveEmailTemplateAsync(dlg.Template);
                        MessageBox.Show("Template updated successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        await LoadTemplatesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating template: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Pagination Helpers

        private static int TotalPages(int count, int pageSize) =>
            count == 0 ? 1 : (int)Math.Ceiling(count / (double)pageSize);

        private static void UpdatePageInfo(Label lbl, int total, int page, int pageSize)
        {
            if (lbl == null) return;
            int totalPages = TotalPages(total, pageSize);
            lbl.Text = total == 0
                ? "Page 0 of 0 (0 records)"
                : $"Page {page} of {totalPages} ({total} records)";
        }

        private static Panel BuildPaginationPanel(
            out Button btnFirst, out Button btnPrev, out Label lblInfo,
            out Button btnNext,  out Button btnLast)
        {
            const int left = 15;  // aligns with the 15px-padded content panel above

            var bar = new Panel { Dock = DockStyle.Bottom, Height = 34 };

            btnFirst = new Button { Text = "<<", Width = 42, Height = 26, Left = left,        Top = 4, Font = new Font("Segoe UI", 8.5F) };
            btnPrev  = new Button { Text = "<",  Width = 36, Height = 26, Left = left + 46,   Top = 4, Font = new Font("Segoe UI", 8.5F) };
            lblInfo  = new Label  { AutoSize = false, Width = 230, Height = 26, Left = left + 86,  Top = 8, Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.MiddleLeft };
            btnNext  = new Button { Text = ">",  Width = 36, Height = 26, Left = left + 320,  Top = 4, Font = new Font("Segoe UI", 8.5F) };
            btnLast  = new Button { Text = ">>", Width = 42, Height = 26, Left = left + 360,  Top = 4, Font = new Font("Segoe UI", 8.5F) };

            bar.Controls.AddRange(new Control[] { btnFirst, btnPrev, lblInfo, btnNext, btnLast });
            return bar;
        }

        #endregion

        #region Test Email

        private async void BtnSendTestEmail_Click(object sender, EventArgs e)
        {
            try
            {
                // Load profiles and templates
                var profiles = await _emailRepo.GetSmtpProfilesAsync(activeOnly: true);
                var templates = await _emailRepo.GetEmailTemplatesAsync(activeOnly: true);

                if (profiles.Count == 0)
                {
                    MessageBox.Show("No active SMTP profiles found. Please add an SMTP profile first.", "Send Test Email", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (templates.Count == 0)
                {
                    MessageBox.Show("No active email templates found. Please add a template first.", "Send Test Email", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Show test email dialog
                using (var dlg = new SendTestEmailDialog(profiles, templates, _emailService))
                {
                    dlg.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Send Test Email", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        private async Task LoadAllDataAsync()
        {
            await LoadEmailAddressesAsync();
            await LoadSmtpProfilesAsync();
            await LoadTemplatesAsync();
        }
    }
}
