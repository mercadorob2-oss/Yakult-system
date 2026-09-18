using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ReaLTaiizor.Controls;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Renewal
{
    /// <summary>
    /// Dialog for creating a new renewal entry by selecting a Service SKU + Asset.
    ///
    /// Flow:
    ///   1. Pick Service SKU (ItemType = Services / Software/License).
    ///   2. Pick existing Asset (searchable) — or create one inline.
    ///   3. Set renewal dates.
    ///   4. Save → inserts dbo.Renewals row with ItemId + AssetId.
    ///
    /// Constraints enforced:
    ///   - Duplicate detection: same ItemId + AssetId with overlapping date range.
    ///   - AssetId stored in Renewals only — SetItem is NOT touched here.
    ///   - AssetId is optional (null allowed for non-asset renewals).
    /// </summary>
    public class AddRenewalDialog : Form
    {
        // ── result exposed to caller ──────────────────────────────────────────
        public int SavedRenewalId { get; private set; }

        // ── repos ─────────────────────────────────────────────────────────────
        private readonly RenewalRepository _renewalRepo = new RenewalRepository();
        private readonly AssetRepository   _assetRepo   = new AssetRepository();

        // ── data caches ───────────────────────────────────────────────────────
        private List<LookupItem>  _serviceItems = new List<LookupItem>();
        private List<AssetDto>    _allAssets    = new List<AssetDto>();
        private List<AssetDto>    _filteredAssets = new List<AssetDto>();

        // ── controls ──────────────────────────────────────────────────────────
        private ComboBox      _cmbServiceSku;
        private HopeTextBox   _txtAssetSearch;
        private ListBox       _lstAssets;
        private Label         _lblSelectedAsset;
        private System.Windows.Forms.Button        _btnClearAsset;
        private System.Windows.Forms.Button        _btnNewAsset;
        private DateTimePicker _dtpStart;
        private DateTimePicker _dtpEnd;
        private NumericUpDown  _numYears;
        private TextBox        _txtAmount;
        private TextBox        _txtNotes;
        private Label          _lblError;
        private System.Windows.Forms.Button         _btnSave;
        private System.Windows.Forms.Button         _btnCancel;

        // ── selected asset ────────────────────────────────────────────────────
        private AssetDto _selectedAsset;

        // ── .ctor ─────────────────────────────────────────────────────────────
        public AddRenewalDialog()
        {
            BuildUi();
            LoadData();
        }

        // ── data loading ──────────────────────────────────────────────────────

        private void LoadData()
        {
            try
            {
                _serviceItems = _renewalRepo.GetServiceItems() ?? new List<LookupItem>();
                _cmbServiceSku.Items.Clear();
                _cmbServiceSku.Items.Add("(Select Service SKU)");
                foreach (var item in _serviceItems)
                    _cmbServiceSku.Items.Add(item.Name);
                _cmbServiceSku.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                ShowError($"Failed to load service items: {ex.Message}");
            }

            try
            {
                _allAssets = _assetRepo.GetAllAssets()
                    .Where(a => a.IsActive)
                    .OrderBy(a => a.ModelNumber ?? "")
                    .ThenBy(a => a.SerialNumber ?? "")
                    .ToList();
                ApplyAssetFilter();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to load assets: {ex.Message}");
            }
        }

        private void ApplyAssetFilter()
        {
            string q = _txtAssetSearch?.Text?.Trim() ?? string.Empty;
            _filteredAssets = string.IsNullOrEmpty(q)
                ? _allAssets
                : _allAssets.Where(a =>
                    (!string.IsNullOrEmpty(a.ModelNumber)  && a.ModelNumber.IndexOf(q,  StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(a.SerialNumber) && a.SerialNumber.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(a.Description)  && a.Description.IndexOf(q,  StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();

            _lstAssets.DataSource    = null;
            _lstAssets.DataSource    = _filteredAssets;
            _lstAssets.DisplayMember = "DisplayName";
            _lstAssets.ValueMember   = "AssetId";
        }

        // ── event handlers ────────────────────────────────────────────────────

        private void NumYears_ValueChanged(object sender, EventArgs e)
        {
            _dtpEnd.Value = _dtpStart.Value.AddYears((int)_numYears.Value);
        }

        private void DtpStart_ValueChanged(object sender, EventArgs e)
        {
            _dtpEnd.Value = _dtpStart.Value.AddYears((int)_numYears.Value);
        }

        private void LstAssets_SelectedIndexChanged(object sender, EventArgs e)
        {
            _selectedAsset = _lstAssets.SelectedItem as AssetDto;
            UpdateSelectedAssetLabel();
        }

        private void BtnClearAsset_Click(object sender, EventArgs e)
        {
            _selectedAsset = null;
            _lstAssets.ClearSelected();
            UpdateSelectedAssetLabel();
        }

        private void BtnNewAsset_Click(object sender, EventArgs e)
        {
            using (var dlg = new Asset.AddEditAssetDialog())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SavedAsset != null)
                {
                    // Reload and pre-select the newly created asset
                    try { _allAssets = _assetRepo.GetAllAssets().Where(a => a.IsActive).ToList(); } catch { }
                    ApplyAssetFilter();
                    var newAsset = _filteredAssets.FirstOrDefault(a => a.AssetId == dlg.SavedAsset.AssetId);
                    if (newAsset != null)
                    {
                        _lstAssets.SelectedItem = newAsset;
                        _selectedAsset = newAsset;
                    }
                    UpdateSelectedAssetLabel();
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            _lblError.Text = string.Empty;

            // ── validation ────────────────────────────────────────────────────
            if (_cmbServiceSku.SelectedIndex <= 0)
            {
                ShowError("Please select a Service SKU.");
                return;
            }

            var serviceItem = _serviceItems[_cmbServiceSku.SelectedIndex - 1]; // -1 for "(Select ...)" sentinel

            DateTime startDate = _dtpStart.Value.Date;
            DateTime endDate   = _dtpEnd.Value.Date;

            if (endDate <= startDate)
            {
                ShowError("End Date must be after Start Date.");
                return;
            }

            decimal? amount = null;
            if (!string.IsNullOrWhiteSpace(_txtAmount.Text))
            {
                if (!decimal.TryParse(_txtAmount.Text.Trim(), out decimal parsedAmount) || parsedAmount < 0)
                {
                    ShowError("Amount must be a valid positive number.");
                    return;
                }
                amount = parsedAmount;
            }

            int? assetId = _selectedAsset?.AssetId;

            // ── duplicate check ───────────────────────────────────────────────
            try
            {
                if (_renewalRepo.HasOverlappingRenewal(serviceItem.Id, assetId, startDate, endDate))
                {
                    string assetDesc = _selectedAsset != null ? $" for asset \"{_selectedAsset.DisplayName}\"" : "";
                    var confirm = MessageBox.Show(
                        $"An active renewal for \"{serviceItem.Name}\"{assetDesc} already exists " +
                        $"with an overlapping date range.\n\nDo you want to create it anyway?",
                        "Duplicate Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (confirm != DialogResult.Yes) return;
                }
            }
            catch (Exception ex)
            {
                ShowError($"Duplicate check failed: {ex.Message}");
                return;
            }

            // ── save ──────────────────────────────────────────────────────────
            try
            {
                int renewalId = _renewalRepo.CreateRenewalForAsset(
                    itemId:        serviceItem.Id,
                    assetId:       assetId,
                    renewalStatus: "Renewed",
                    newStartDate:  startDate,
                    newEndDate:    endDate,
                    renewalYears:  (int)_numYears.Value,
                    renewalAmount: amount,
                    renewalNotes:  string.IsNullOrWhiteSpace(_txtNotes.Text) ? null : _txtNotes.Text.Trim(),
                    createdBy:     AppSession.CurrentUserId);

                SavedRenewalId = renewalId;
                DialogResult   = DialogResult.OK;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private void ShowError(string msg) => _lblError.Text = msg;

        private void UpdateSelectedAssetLabel()
        {
            if (_selectedAsset != null)
            {
                _lblSelectedAsset.Text      = $"Selected: {_selectedAsset.DisplayName}";
                _lblSelectedAsset.ForeColor = Color.FromArgb(39, 174, 96);
            }
            else
            {
                _lblSelectedAsset.Text      = "No asset selected (optional)";
                _lblSelectedAsset.ForeColor = Color.Gray;
            }
        }

        // ── UI construction ───────────────────────────────────────────────────

        private void BuildUi()
        {
            Text            = "Add Renewal";
            Size            = new Size(560, 580);
            MinimumSize     = new Size(520, 560);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = Color.White;
            Font            = new Font("Segoe UI", 9.5f);

            // ── title banner ─────────────────────────────────────────────────
            var banner = new System.Windows.Forms.Panel { Dock = DockStyle.Top, Height = 52,
                BackColor = Color.FromArgb(41, 128, 185) };
            var lblTitle = new Label { Text = "Add Renewal", ForeColor = Color.White,
                AutoSize = false, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Padding = new Padding(16, 0, 0, 0) };
            banner.Controls.Add(lblTitle);
            Controls.Add(banner);

            int x0 = 20, lw = 130, fw = 360, y = 65, rh = 46;

            // ── Service SKU ──────────────────────────────────────────────────
            Controls.Add(MkLabel("Service SKU: *", x0, y));
            _cmbServiceSku = new ComboBox { Location = new Point(x0 + lw, y - 3),
                Width = fw, DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(_cmbServiceSku);
            y += rh;

            // ── Asset search ─────────────────────────────────────────────────
            Controls.Add(MkLabel("Search Asset:", x0, y));
            _txtAssetSearch = new HopeTextBox
            {
                Location = new Point(x0 + lw, y - 3), Size = new Size(fw, 28),
                BackColor = Color.White, BaseColor = Color.White,
                BorderColorA = Color.FromArgb(200, 200, 200),
                BorderColorB = Color.FromArgb(200, 200, 200),
                Hint = "Part#, Serial#, or Description…",
                Font = new Font("Segoe UI", 9f)
            };
            _txtAssetSearch.TextChanged += (s, e) => ApplyAssetFilter();
            Controls.Add(_txtAssetSearch);
            y += rh;

            // ── Asset list ───────────────────────────────────────────────────
            Controls.Add(MkLabel("Asset:", x0, y));
            _lstAssets = new ListBox { Location = new Point(x0 + lw, y - 3),
                Size = new Size(fw, 90), DisplayMember = "DisplayName", ValueMember = "AssetId" };
            _lstAssets.SelectedIndexChanged += LstAssets_SelectedIndexChanged;
            Controls.Add(_lstAssets);
            y += 96;

            // ── Selected asset + actions ─────────────────────────────────────
            _lblSelectedAsset = new Label { Location = new Point(x0 + lw, y),
                Size = new Size(fw - 90, 20), AutoSize = false, ForeColor = Color.Gray,
                Text = "No asset selected (optional)" };
            Controls.Add(_lblSelectedAsset);

            _btnClearAsset = new System.Windows.Forms.Button { Text = "Clear", Location = new Point(x0 + lw + fw - 85, y - 2),
                Size = new Size(60, 22), FlatStyle = FlatStyle.Flat };
            _btnClearAsset.FlatAppearance.BorderColor = Color.Silver;
            _btnClearAsset.Click += BtnClearAsset_Click;
            Controls.Add(_btnClearAsset);

            _btnNewAsset = new System.Windows.Forms.Button { Text = "+ New Asset", Location = new Point(x0 + lw, y + 24),
                Size = new Size(100, 24), FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219), ForeColor = Color.White };
            _btnNewAsset.FlatAppearance.BorderSize = 0;
            _btnNewAsset.Click += BtnNewAsset_Click;
            Controls.Add(_btnNewAsset);
            y += 54;

            // ── Renewal period ───────────────────────────────────────────────
            Controls.Add(MkLabel("Years:", x0, y));
            _numYears = new NumericUpDown { Location = new Point(x0 + lw, y - 3),
                Width = 80, Minimum = 1, Maximum = 20, Value = 1 };
            _numYears.ValueChanged += NumYears_ValueChanged;
            Controls.Add(_numYears);
            y += rh;

            Controls.Add(MkLabel("Start Date:", x0, y));
            _dtpStart = new DateTimePicker { Location = new Point(x0 + lw, y - 3),
                Width = 160, Format = DateTimePickerFormat.Short, Value = DateTime.Today };
            _dtpStart.ValueChanged += DtpStart_ValueChanged;
            Controls.Add(_dtpStart);
            y += rh;

            Controls.Add(MkLabel("End Date:", x0, y));
            _dtpEnd = new DateTimePicker { Location = new Point(x0 + lw, y - 3),
                Width = 160, Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddYears(1) };
            Controls.Add(_dtpEnd);
            y += rh;

            // ── Amount ───────────────────────────────────────────────────────
            Controls.Add(MkLabel("Amount:", x0, y));
            _txtAmount = new TextBox { Location = new Point(x0 + lw, y - 3), Width = 160 };
            Controls.Add(_txtAmount);
            y += rh;

            // ── Notes ────────────────────────────────────────────────────────
            Controls.Add(MkLabel("Notes:", x0, y));
            _txtNotes = new TextBox { Location = new Point(x0 + lw, y - 3),
                Width = fw, Height = 50, Multiline = true, ScrollBars = ScrollBars.Vertical };
            Controls.Add(_txtNotes);
            y += 60;

            // ── Error label ──────────────────────────────────────────────────
            _lblError = new Label { Location = new Point(x0, y),
                Size = new Size(500, 36), ForeColor = Color.Firebrick,
                Font = new Font("Segoe UI", 9f), Text = string.Empty, AutoSize = false };
            Controls.Add(_lblError);

            // ── Buttons ──────────────────────────────────────────────────────
            _btnSave = new System.Windows.Forms.Button { Text = "Save", Width = 100, Height = 36,
                BackColor = Color.FromArgb(41, 128, 185), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(ClientSize.Width - 230, ClientSize.Height - 52) };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += BtnSave_Click;

            _btnCancel = new System.Windows.Forms.Button { Text = "Cancel", Width = 90, Height = 36,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(ClientSize.Width - 116, ClientSize.Height - 52) };
            _btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

            Controls.Add(_btnSave);
            Controls.Add(_btnCancel);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;
        }

        private static Label MkLabel(string text, int x, int y) =>
            new Label { Text = text, Location = new Point(x, y + 4), AutoSize = true,
                        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    }
}
