using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using Dapper;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Asset
{
    /// <summary>
    /// Modal dialog for creating or editing a dbo.Asset record.
    ///
    /// Rules enforced:
    ///   - At least one of Part Number / Serial Number is required.
    ///   - Both may be provided simultaneously.
    ///   - Serial Number must be unique (duplicate surfaced as a friendly error).
    ///   - Vendor selection is optional.
    ///
    /// After Save the caller reads <see cref="SavedAsset"/> to get the resulting DTO.
    /// </summary>
    public class AddEditAssetDialog : Form
    {
        // ── result exposed to caller ──────────────────────────────────────
        public AssetDto SavedAsset { get; private set; }

        // ── repos / services ─────────────────────────────────────────────
        private readonly AssetRepository _repo = new AssetRepository();
        private readonly AssetDto _editing; // null = add mode

        // ── controls ─────────────────────────────────────────────────────
        private TextBox _txtModelNumber;
        private TextBox _txtSerialNumber;
        private TextBox _txtDescription;
        private ComboBox _cmbVendor;
        private CheckBox _chkActive;
        private Label _lblError;
        private Button _btnSave;
        private Button _btnCancel;

        // ── vendor lookup ─────────────────────────────────────────────────
        private List<(int Id, string Name)> _vendors = new List<(int, string)>();

        // ── ctor ──────────────────────────────────────────────────────────

        /// <param name="assetToEdit">Pass null to open in Add mode; pass an existing AssetDto to Edit.</param>
        public AddEditAssetDialog(AssetDto assetToEdit = null)
        {
            _editing = assetToEdit;
            BuildUi();
            LoadVendors();
            if (_editing != null)
                PopulateFields();
        }

        // ── UI construction ───────────────────────────────────────────────

        private void BuildUi()
        {
            Text         = _editing == null ? "Add Asset" : "Edit Asset";
            Size         = new Size(500, 460);
            MinimumSize  = new Size(450, 440);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox  = false;
            MinimizeBox  = false;
            BackColor    = Color.White;
            Font         = new Font("Segoe UI", 9.5f);

            int labelW = 130, fieldW = 290, rowH = 50, x0 = 20, y = 18;

            // Title banner
            var banner = new Panel { Dock = DockStyle.Top, Height = 52,
                BackColor = Color.FromArgb(41, 128, 185) };
            var lblTitle = new Label {
                Text = Text, ForeColor = Color.White, AutoSize = false,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Padding = new Padding(16, 0, 0, 0) };
            banner.Controls.Add(lblTitle);
            Controls.Add(banner);
            y = 70;

            // Part Number
            Controls.Add(MakeLabel("Part Number:", x0, y));
            _txtModelNumber = MakeTextBox(x0 + labelW + 8, y, fieldW);
            Controls.Add(_txtModelNumber);
            y += rowH;

            // Serial Number
            Controls.Add(MakeLabel("Serial Number:", x0, y));
            _txtSerialNumber = MakeTextBox(x0 + labelW + 8, y, fieldW);
            Controls.Add(_txtSerialNumber);
            y += rowH;

            // Description
            Controls.Add(MakeLabel("Description:", x0, y));
            _txtDescription = MakeTextBox(x0 + labelW + 8, y, fieldW);
            Controls.Add(_txtDescription);
            y += rowH;

            // Vendor
            Controls.Add(MakeLabel("Vendor:", x0, y));
            _cmbVendor = new ComboBox {
                Location = new Point(x0 + labelW + 8, y - 3),
                Width = fieldW, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbVendor.Items.Add("(None)");
            _cmbVendor.SelectedIndex = 0;
            Controls.Add(_cmbVendor);
            y += rowH;

            // Active (edit-mode only)
            _chkActive = new CheckBox {
                Text = "Active", Location = new Point(x0 + labelW + 8, y),
                AutoSize = true, Checked = true };
            Controls.Add(_chkActive);
            if (_editing == null) _chkActive.Visible = false;
            y += 36;

            // Error label
            _lblError = new Label {
                Location = new Point(x0, y), Size = new Size(440, 36),
                ForeColor = Color.Firebrick, Font = new Font("Segoe UI", 9f),
                Text = "", AutoSize = false };
            Controls.Add(_lblError);

            // Buttons (bottom)
            _btnSave = new Button {
                Text = "Save", Width = 100, Height = 36,
                BackColor = Color.FromArgb(41, 128, 185), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 230, ClientSize.Height - 52) };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += BtnSave_Click;

            _btnCancel = new Button {
                Text = "Cancel", Width = 90, Height = 36,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 116, ClientSize.Height - 52) };
            _btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

            Controls.Add(_btnSave);
            Controls.Add(_btnCancel);

            AcceptButton = _btnSave;
            CancelButton = _btnCancel;
        }

        private static Label MakeLabel(string text, int x, int y) =>
            new Label { Text = text, Location = new Point(x, y + 4), AutoSize = true,
                        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };

        private static TextBox MakeTextBox(int x, int y, int w) =>
            new TextBox { Location = new Point(x, y), Width = w };

        // ── data ──────────────────────────────────────────────────────────

        private void LoadVendors()
        {
            try
            {
                DatabaseConfig.EnsureConfigured();
                const string sql = "SELECT VendorID AS Id, VendorName AS Name FROM dbo.Vendor WHERE Active = 1 ORDER BY VendorName";
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                    _vendors = new List<(int, string)>(con.Query<(int Id, string Name)>(sql));

                foreach (var v in _vendors)
                    _cmbVendor.Items.Add(v.Name);
            }
            catch { /* ignore vendor load failures — field is optional */ }
        }

        private void PopulateFields()
        {
            _txtModelNumber.Text  = _editing.ModelNumber  ?? "";
            _txtSerialNumber.Text = _editing.SerialNumber ?? "";
            _txtDescription.Text  = _editing.Description  ?? "";
            _chkActive.Checked    = _editing.IsActive;

            if (_editing.VendorId.HasValue)
            {
                int idx = _vendors.FindIndex(v => v.Id == _editing.VendorId.Value);
                if (idx >= 0) _cmbVendor.SelectedIndex = idx + 1; // +1 for "(None)"
            }
        }

        // ── save ──────────────────────────────────────────────────────────

        private void BtnSave_Click(object sender, EventArgs e)
        {
            _lblError.Text = "";

            bool hasModel  = !string.IsNullOrWhiteSpace(_txtModelNumber.Text);
            bool hasSerial = !string.IsNullOrWhiteSpace(_txtSerialNumber.Text);

            if (!hasModel && !hasSerial)
            {
                _lblError.Text = "At least one of Part Number or Serial Number is required.";
                return;
            }

            int? vendorId = null;
            if (_cmbVendor.SelectedIndex > 0 && _cmbVendor.SelectedIndex - 1 < _vendors.Count)
                vendorId = _vendors[_cmbVendor.SelectedIndex - 1].Id;

            var dto = new AssetDto
            {
                AssetId       = _editing?.AssetId ?? 0,
                ModelNumber   = hasModel  ? _txtModelNumber.Text.Trim()  : null,
                SerialNumber  = hasSerial ? _txtSerialNumber.Text.Trim() : null,
                Description   = string.IsNullOrWhiteSpace(_txtDescription.Text) ? null : _txtDescription.Text.Trim(),
                VendorId      = vendorId,
                IsActive      = _chkActive.Checked
            };

            try
            {
                int userId = AppSession.CurrentUserId;
                if (_editing == null)
                {
                    int newId = _repo.AddAsset(dto, userId);
                    dto.AssetId = newId;
                }
                else
                {
                    _repo.UpdateAsset(dto);
                }

                SavedAsset = dto;
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                _lblError.Text = ex.Message;
            }
        }
    }
}
