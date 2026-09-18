using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    /// <summary>
    /// Two-step wizard for assigning unassigned empty cartridges to a DISPOSE or SELL outbound batch.
    ///
    /// STEP 1 — Select Cartridges:
    ///   Lists all non-refillable and DAMAGED returned cartridges with VendorBatchId IS NULL.
    ///
    /// STEP 2 — Batch Details:
    ///   User selects purpose (DISPOSE or SELL) and matching vendor.
    ///   Creates a VendorCartridgeBatch with BatchPurpose set accordingly,
    ///   then assigns selected cartridges to it.
    /// </summary>
    public class OutboundBatchAssignmentDialog : Form
    {
        // ── Services ──────────────────────────────────────────────────────────
        private readonly CartridgeRefillService _refillService;

        // ── Step state ────────────────────────────────────────────────────────
        private int _currentStep = 1;
        private List<UnassignedReturnDto> _allReturns = new List<UnassignedReturnDto>();

        // ── Step indicator labels ─────────────────────────────────────────────
        private Label _lblStep1Circle;
        private Label _lblStep2Circle;
        private Label _lblStep1Text;
        private Label _lblStep2Text;

        // ── Step panels ───────────────────────────────────────────────────────
        private Panel _step1Panel;
        private Panel _step2Panel;

        // ── Step 1 controls ───────────────────────────────────────────────────
        private CheckState _headerCheckState = CheckState.Unchecked;
        private ComboBox   _cmbModelFilter;
        private readonly List<int>    _modelFilterIds   = new List<int>();
        private readonly List<string> _modelFilterNames = new List<string>();
        private DataGridView _dgvReturns;
        private Label        _lblSelStatus;

        // ── Step 2 controls ───────────────────────────────────────────────────
        private List<ModelGroup> _modelGroups = new List<ModelGroup>();
        private Panel            _step2ContentPanel;
        private Label            _lblStep2Summary;
        private RadioButton      _radNewBatch;
        private RadioButton      _radExistingBatch;
        private Panel            _pnlNewBatch;
        private Panel            _pnlExistingBatch;
        private RadioButton      _radDispose;
        private RadioButton      _radSell;
        private ComboBox         _cmbVendor;
        private TextBox          _txtRemarks;
        private Label            _lblDamagedWarn;
        private ComboBox         _cmbExistingBatch;

        private readonly List<int>    _vendorIds      = new List<int>();
        private readonly List<string> _vendorNames    = new List<string>();
        private readonly List<int>    _activeBatchIds = new List<int>();

        // ── Navigation buttons ────────────────────────────────────────────────
        private Button _btnBack;
        private Button _btnNext;
        private Button _btnConfirm;
        private Button _btnCancel;

        // ── Accent colors ─────────────────────────────────────────────────────
        private static readonly Color Teal   = Color.FromArgb(0, 150, 136);
        private static readonly Color Gray   = Color.FromArgb(189, 189, 189);
        private static readonly Color Orange = Color.FromArgb(230, 126, 34);
        private static readonly Color Green  = Color.FromArgb(39, 174, 96);

        // ── Per-model group ───────────────────────────────────────────────────
        private class ModelGroup
        {
            public int       CartridgeModelId  { get; set; }
            public string    CartridgeModel    { get; set; }
            public List<int> EmptyCartridgeIds { get; set; } = new List<int>();
            public int       TotalQty          { get; set; }
        }

        // ─────────────────────────────────────────────────────────────────────
        private readonly bool _nonRefillableOnly;
        private readonly bool _damagedOnly;

        public OutboundBatchAssignmentDialog(bool nonRefillableOnly = false, bool damagedOnly = false)
        {
            _nonRefillableOnly = nonRefillableOnly;
            _damagedOnly       = damagedOnly;
            _refillService = new CartridgeRefillService();
            BuildUi();
        }

        // ═════════════════════════════════════════════════════════════════════
        // UI CONSTRUCTION
        // ═════════════════════════════════════════════════════════════════════

        private void BuildUi()
        {
            Text            = "Assign to Outbound Batch";
            Size            = new Size(1000, 680);
            MinimumSize     = new Size(860, 580);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = true;
            MinimizeBox     = false;
            BackColor       = Color.White;

            var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Orange };
            header.Controls.Add(new Label
            {
                Text      = "Assign to Outbound Batch (Dispose / Sell)",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(16, 12)
            });

            var stepBar     = BuildStepBar();
            var contentArea = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            _step1Panel = BuildStep1Panel();
            _step2Panel = BuildStep2Panel();
            _step2Panel.Visible = false;

            contentArea.Controls.Add(_step2Panel);
            contentArea.Controls.Add(_step1Panel);

            var btnBar = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 55,
                BackColor = Color.FromArgb(245, 247, 250)
            };

            _btnCancel = new Button
            {
                Text      = "Cancel",
                Width     = 90,
                Height    = 34,
                BackColor = Color.FromArgb(149, 165, 166),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9F),
                Cursor    = Cursors.Hand,
                Location  = new Point(12, 10)
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            _btnBack = new Button
            {
                Text      = "← Back",
                Width     = 90,
                Height    = 34,
                BackColor = Color.FromArgb(149, 165, 166),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                Visible   = false
            };
            _btnBack.FlatAppearance.BorderSize = 0;
            _btnBack.Click += BtnBack_Click;

            _btnNext = new Button
            {
                Text      = "Next →",
                Width     = 100,
                Height    = 34,
                BackColor = Orange,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            _btnNext.FlatAppearance.BorderSize = 0;
            _btnNext.Click += BtnNext_Click;

            _btnConfirm = new Button
            {
                Text      = "Create Batch",
                Width     = 140,
                Height    = 34,
                BackColor = Orange,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                Visible   = false
            };
            _btnConfirm.FlatAppearance.BorderSize = 0;
            _btnConfirm.Click += async (s, e) => await BtnConfirm_ClickAsync();

            btnBar.Controls.Add(_btnCancel);
            btnBar.Controls.Add(_btnBack);
            btnBar.Controls.Add(_btnNext);
            btnBar.Controls.Add(_btnConfirm);

            btnBar.Resize += (s, e) => PositionNavButtons(btnBar);
            PositionNavButtons(btnBar);

            Controls.Add(contentArea);
            Controls.Add(stepBar);
            Controls.Add(btnBar);
            Controls.Add(header);

            CancelButton = _btnCancel;
            Shown += async (s, e) => await LoadStep1Async();
        }

        private void PositionNavButtons(Panel bar)
        {
            int x = bar.ClientSize.Width - 12;
            if (_btnConfirm.Visible) { x -= _btnConfirm.Width; _btnConfirm.Location = new Point(x, 10); x -= 8; }
            if (_btnNext.Visible)    { x -= _btnNext.Width;    _btnNext.Location    = new Point(x, 10); x -= 8; }
            if (_btnBack.Visible)    { x -= _btnBack.Width;    _btnBack.Location    = new Point(x, 10); }
        }

        // ── Step indicator bar ────────────────────────────────────────────────
        private Panel BuildStepBar()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Color.FromArgb(250, 250, 250) };

            _lblStep1Circle = MakeCircleLabel("1", new Point(30, 14), Orange);
            _lblStep1Text   = new Label { Text = "Select Cartridges", Location = new Point(8, 52), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Orange };

            var connector   = new Panel  { Size = new Size(110, 2), Location = new Point(65, 29), BackColor = Gray };

            _lblStep2Circle = MakeCircleLabel("2", new Point(180, 14), Gray);
            _lblStep2Text   = new Label { Text = "Batch Details", Location = new Point(158, 52), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(90, 90, 90) };

            bar.Controls.Add(_lblStep1Circle);
            bar.Controls.Add(_lblStep1Text);
            bar.Controls.Add(connector);
            bar.Controls.Add(_lblStep2Circle);
            bar.Controls.Add(_lblStep2Text);
            return bar;
        }

        private static Label MakeCircleLabel(string text, Point location, Color backColor)
        {
            var lbl = new Label
            {
                Text      = text,
                Size      = new Size(30, 30),
                Location  = location,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = backColor
            };
            lbl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(lbl.BackColor))
                    g.FillEllipse(b, 0, 0, lbl.Width - 1, lbl.Height - 1);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                using (var tb = new SolidBrush(lbl.ForeColor))
                    g.DrawString(lbl.Text, lbl.Font, tb, new RectangleF(0, 0, lbl.Width, lbl.Height), sf);
            };
            return lbl;
        }

        private void UpdateStepIndicator()
        {
            _lblStep1Circle.BackColor = _currentStep >= 1 ? Orange : Gray;
            _lblStep1Text.ForeColor   = _currentStep >= 1 ? Orange : Color.FromArgb(90, 90, 90);
            _lblStep2Circle.BackColor = _currentStep >= 2 ? Orange : Gray;
            _lblStep2Text.ForeColor   = _currentStep >= 2 ? Orange : Color.FromArgb(90, 90, 90);
            _lblStep1Circle.Invalidate();
            _lblStep2Circle.Invalidate();
        }

        // ── Step 1 panel ─────────────────────────────────────────────────────
        private Panel BuildStep1Panel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 6) };
            int y = 10;

            panel.Controls.Add(new Label
            {
                Text      = "Non-Refillable / Damaged Empty Cartridges (Unassigned)",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location  = new Point(0, y),
                AutoSize  = true
            });
            y += 28;

            panel.Controls.Add(new Label { Text = "Filter by model:", Location = new Point(0, y + 4), AutoSize = true, Font = new Font("Segoe UI", 9F) });

            _cmbModelFilter = new ComboBox
            {
                Location      = new Point(140, y),
                Width         = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9F)
            };
            _cmbModelFilter.SelectedIndexChanged += CmbModelFilter_SelectedIndexChanged;
            panel.Controls.Add(_cmbModelFilter);
            y += 36;

            _dgvReturns = new DataGridView
            {
                Location              = new Point(0, y),
                Anchor                = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                AutoGenerateColumns   = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly              = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = true,
                BackgroundColor       = Color.White,
                BorderStyle           = BorderStyle.FixedSingle,
                GridColor             = Color.FromArgb(224, 224, 224),
                RowHeadersVisible     = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars            = ScrollBars.Both
            };
            _dgvReturns.ColumnHeadersDefaultCellStyle.BackColor = Orange;
            _dgvReturns.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _dgvReturns.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            _dgvReturns.EnableHeadersVisualStyles               = false;
            _dgvReturns.ColumnHeadersHeightSizeMode             = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _dgvReturns.ColumnHeadersHeight                     = 34;
            _dgvReturns.DefaultCellStyle.SelectionBackColor     = Color.FromArgb(255, 220, 180);
            _dgvReturns.DefaultCellStyle.SelectionForeColor     = Color.Black;
            _dgvReturns.DefaultCellStyle.Font                   = new Font("Segoe UI", 9F);
            _dgvReturns.RowTemplate.Height                      = 26;

            _dgvReturns.Columns.Add(new DataGridViewCheckBoxColumn { Name = "chkSelect",   HeaderText = "",           Width = 30, ReadOnly = false, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colId",       HeaderText = "ID",         DataPropertyName = "EmptyCartridgeId", Width = 55,  ReadOnly = true });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colModel",    HeaderText = "Model",      DataPropertyName = "CartridgeModel",   Width = 160, ReadOnly = true });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colCondition",HeaderText = "Condition",  DataPropertyName = "ConditionStatus",  Width = 90,  ReadOnly = true });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colQty",      HeaderText = "Qty",        DataPropertyName = "Quantity",         Width = 55,  ReadOnly = true });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colDate",     HeaderText = "Returned",   DataPropertyName = "ReturnedAt",       Width = 130, ReadOnly = true });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colRemarks",  HeaderText = "Remarks",    DataPropertyName = "Remarks",          Width = 200, ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            _dgvReturns.CellFormatting               += DgvReturns_CellFormatting;
            _dgvReturns.CellValueChanged             += DgvReturns_CellValueChanged;
            _dgvReturns.CurrentCellDirtyStateChanged += DgvReturns_DirtyStateChanged;
            _dgvReturns.CellPainting                 += DgvReturns_HeaderCellPainting;
            _dgvReturns.CellMouseClick               += DgvReturns_HeaderCellMouseClick;
            panel.Controls.Add(_dgvReturns);

            _lblSelStatus = new Label
            {
                Text      = "0 rows selected • 0 units",
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoSize  = true,
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left
            };
            panel.Controls.Add(_lblSelStatus);

            panel.Resize += (s, e) =>
            {
                _dgvReturns.Width  = panel.ClientSize.Width  - 32;
                _dgvReturns.Height = panel.ClientSize.Height - y - 28;
                _lblSelStatus.Location = new Point(0, panel.ClientSize.Height - 24);
            };
            return panel;
        }

        // ── Step 2 panel ─────────────────────────────────────────────────────
        private Panel BuildStep2Panel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 6), Visible = false };
            const int lx = 24; // left margin for all content
            int y = 10;

            panel.Controls.Add(new Label
            {
                Text      = "Batch Details",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location  = new Point(lx, y),
                AutoSize  = true
            });
            y += 26;

            panel.Controls.Add(new Label
            {
                Text      = "Select the purpose and matching vendor for this outbound batch.",
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location  = new Point(lx, y),
                AutoSize  = true
            });
            y += 22;

            _lblStep2Summary = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location  = new Point(lx, y),
                AutoSize  = true
            };
            panel.Controls.Add(_lblStep2Summary);
            y += 26;

            panel.Controls.Add(new Panel
            {
                Location  = new Point(lx, y),
                Size      = new Size(760, 1),
                BackColor = Color.FromArgb(220, 220, 220)
            });
            y += 10;

            // ── Batch mode: Create New / Use Existing ─────────────────────────
            _radNewBatch = new RadioButton
            {
                Text      = "Create New Batch",
                Location  = new Point(lx, y + 2),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Checked   = true
            };
            _radExistingBatch = new RadioButton
            {
                Text     = "Use Existing Active Batch",
                Location = new Point(lx + 260, y + 2),
                AutoSize = true,
                Font     = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            panel.Controls.Add(_radNewBatch);
            panel.Controls.Add(_radExistingBatch);
            _radNewBatch.CheckedChanged     += (s, e) => { if (_radNewBatch.Checked)     ToggleStep2Mode(); };
            _radExistingBatch.CheckedChanged += (s, e) => { if (_radExistingBatch.Checked) ToggleStep2Mode(); };
            y += 34;

            // ── "Create New" panel ────────────────────────────────────────────
            _pnlNewBatch = new Panel
            {
                Location  = new Point(lx, y),
                Width     = 760,
                Height    = 156,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            int py = 0;
            _pnlNewBatch.Controls.Add(new Label { Text = "Purpose:", Location = new Point(0, py + 4), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
            _radDispose = new RadioButton { Text = "DISPOSE", Location = new Point(120, py + 2), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Checked = true, ForeColor = Color.FromArgb(200, 80, 0) };
            _radSell    = new RadioButton { Text = "SELL",    Location = new Point(260, py + 2), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Green };
            _pnlNewBatch.Controls.Add(_radDispose);
            _pnlNewBatch.Controls.Add(_radSell);
            _radDispose.CheckedChanged += (s, e) => { if (_radDispose.Checked) RefreshVendorList(); };
            _radSell.CheckedChanged    += (s, e) => { if (_radSell.Checked)    RefreshVendorList(); };
            py += 34;

            _pnlNewBatch.Controls.Add(new Label { Text = "Vendor:", Location = new Point(0, py + 4), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
            _cmbVendor = new ComboBox { Location = new Point(120, py), Width = 340, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _pnlNewBatch.Controls.Add(_cmbVendor);
            py += 34;

            _pnlNewBatch.Controls.Add(new Label { Text = "Remarks (optional):", Location = new Point(0, py + 4), AutoSize = true, Font = new Font("Segoe UI", 9F) });
            _txtRemarks = new TextBox { Location = new Point(160, py), Width = 560, Height = 52, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 9F) };
            _pnlNewBatch.Controls.Add(_txtRemarks);
            py += 60;

            _lblDamagedWarn = new Label { Text = "⚠  Selection includes DAMAGED cartridges.", Font = new Font("Segoe UI", 8.5F, FontStyle.Italic), ForeColor = Color.FromArgb(180, 80, 0), Location = new Point(0, py), AutoSize = true, Visible = false };
            _pnlNewBatch.Controls.Add(_lblDamagedWarn);

            panel.Controls.Add(_pnlNewBatch);

            // ── "Use Existing" panel ──────────────────────────────────────────
            _pnlExistingBatch = new Panel
            {
                Location  = new Point(lx, y),
                Width     = 760,
                Height    = 60,
                Visible   = false,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _pnlExistingBatch.Controls.Add(new Label { Text = "Active Batch:", Location = new Point(0, 6), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
            _cmbExistingBatch = new ComboBox { Location = new Point(120, 2), Width = 460, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _pnlExistingBatch.Controls.Add(_cmbExistingBatch);
            _pnlExistingBatch.Controls.Add(new Label { Text = "Selected cartridges will be added to the chosen batch without creating a new one.", Location = new Point(0, 36), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Italic), ForeColor = Color.FromArgb(100, 100, 100) });
            panel.Controls.Add(_pnlExistingBatch);

            y += 160;

            panel.Controls.Add(new Panel
            {
                Location  = new Point(lx, y),
                Size      = new Size(760, 1),
                BackColor = Color.FromArgb(220, 220, 220)
            });
            y += 10;

            // ── Model summary ─────────────────────────────────────────────────
            panel.Controls.Add(new Label
            {
                Text      = "Models to assign:",
                Location  = new Point(lx, y),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold)
            });
            y += 24;

            _step2ContentPanel = new Panel
            {
                Location   = new Point(lx, y),
                Anchor     = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                AutoScroll = true,
                BackColor  = Color.White
            };
            panel.Controls.Add(_step2ContentPanel);

            panel.Resize += (s, e) =>
            {
                int w = panel.ClientSize.Width - 32 - lx;
                _step2ContentPanel.Width  = w;
                _step2ContentPanel.Height = panel.ClientSize.Height - y - 6;
                if (_pnlNewBatch      != null) _pnlNewBatch.Width      = w;
                if (_pnlExistingBatch != null) _pnlExistingBatch.Width = w;
                if (_txtRemarks       != null) _txtRemarks.Width       = w - 160;
                if (_cmbVendor        != null) _cmbVendor.Width        = Math.Min(340, w - 120);
                if (_cmbExistingBatch != null) _cmbExistingBatch.Width = Math.Min(460, w - 120);
            };
            return panel;
        }

        // ═════════════════════════════════════════════════════════════════════
        // DATA LOADING
        // ═════════════════════════════════════════════════════════════════════

        private async Task LoadStep1Async()
        {
            try
            {
                _btnNext.Enabled = false;
                _allReturns = await _refillService.GetUnassignedOutboundCartridgesAsync(_nonRefillableOnly, _damagedOnly);

                _modelFilterIds.Clear();
                _modelFilterNames.Clear();
                _cmbModelFilter.Items.Clear();

                _modelFilterIds.Add(0);
                _modelFilterNames.Add("(All Models)");
                _cmbModelFilter.Items.Add("(All Models)");

                foreach (var g in _allReturns.GroupBy(r => r.CartridgeModelId).OrderBy(g => g.First().CartridgeModel))
                {
                    _modelFilterIds.Add(g.Key);
                    _modelFilterNames.Add(g.First().CartridgeModel);
                    _cmbModelFilter.Items.Add(g.First().CartridgeModel);
                }

                _cmbModelFilter.SelectedIndex = 0;
                BindFilteredReturns();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading cartridges: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("OutboundBatchAssignmentDialog.LoadStep1Async failed", ex);
            }
            finally
            {
                _btnNext.Enabled = true;
            }
        }

        private void BindFilteredReturns()
        {
            int filterModelId = _cmbModelFilter.SelectedIndex >= 0
                ? _modelFilterIds[_cmbModelFilter.SelectedIndex]
                : 0;

            var filtered = filterModelId == 0
                ? _allReturns
                : _allReturns.Where(r => r.CartridgeModelId == filterModelId).ToList();

            // Assign directly — do NOT set to null first; resetting DataSource to null
            // can leave column state inconsistent in WinForms, breaking InvalidateCell.
            _dgvReturns.DataSource = filtered;

            foreach (DataGridViewRow row in _dgvReturns.Rows)
                row.Cells["chkSelect"].Value = false;

            UpdateSelectionStatus();
            UpdateHeaderCheckState();
        }

        private async Task LoadStep2Async()
        {
            _step2ContentPanel.Controls.Clear();
            _vendorIds.Clear();
            _vendorNames.Clear();
            _cmbVendor.Items.Clear();

            // Build model groups from selected rows.
            // Use DataBoundItem — correct regardless of which filter is currently active.
            _modelGroups = _dgvReturns.Rows
                .Cast<DataGridViewRow>()
                .Select(r => new { Row = r, Dto = r.DataBoundItem as UnassignedReturnDto })
                .Where(x => x.Dto != null && true == (x.Row.Cells["chkSelect"].Value as bool?))
                .GroupBy(x => x.Dto.CartridgeModelId)
                .Select(g => new ModelGroup
                {
                    CartridgeModelId  = g.Key,
                    CartridgeModel    = g.First().Dto.CartridgeModel,
                    EmptyCartridgeIds = g.Select(x => x.Dto.EmptyCartridgeId).ToList(),
                    TotalQty          = g.Sum(x => x.Dto.Quantity)
                })
                .ToList();

            int totalUnits = _modelGroups.Sum(m => m.TotalQty);
            _lblStep2Summary.Text = $"{_modelGroups.Count} model(s)  •  {totalUnits} unit(s) selected";

            // Check for DAMAGED cartridges in selection
            bool hasDamaged = _dgvReturns.Rows
                .Cast<DataGridViewRow>()
                .Where(r => true == (r.Cells["chkSelect"].Value as bool?))
                .Select(r => r.DataBoundItem as UnassignedReturnDto)
                .Any(dto => dto != null && dto.ConditionStatus == "DAMAGED");

            _lblDamagedWarn.Visible = hasDamaged;
            _radSell.Enabled        = true;

            await RefreshVendorListAsync();

            // Populate existing active outbound batches for "Use Existing" mode
            _activeBatchIds.Clear();
            _cmbExistingBatch.Items.Clear();
            try
            {
                var activeBatches = await _refillService.GetOutboundBatchesAsync();
                foreach (var b in activeBatches)
                {
                    _activeBatchIds.Add(b.BatchId);
                    _cmbExistingBatch.Items.Add($"Batch #{b.BatchId} — {b.BatchPurpose} — {b.VendorName} ({b.TotalQty} units)");
                }
                if (_cmbExistingBatch.Items.Count > 0)
                    _cmbExistingBatch.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Logger.LogError("OutboundBatchAssignmentDialog: failed to load active batches", ex);
            }

            // Populate model summary cards
            int cardY = 4;
            foreach (var group in _modelGroups)
            {
                var card = new Panel
                {
                    Location  = new Point(0, cardY),
                    Size      = new Size(_step2ContentPanel.ClientSize.Width - 4, 52),
                    BackColor = Color.FromArgb(250, 250, 250),
                    Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };
                card.Controls.Add(new Label
                {
                    Text      = $"  {group.CartridgeModel}",
                    Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(44, 62, 80),
                    Location  = new Point(4, 8),
                    AutoSize  = true
                });
                card.Controls.Add(new Label
                {
                    Text      = $"{group.TotalQty} unit(s)  •  {group.EmptyCartridgeIds.Count} row(s)",
                    Font      = new Font("Segoe UI", 8.5F),
                    ForeColor = Color.FromArgb(120, 120, 120),
                    Location  = new Point(4, 28),
                    AutoSize  = true
                });
                _step2ContentPanel.Controls.Add(card);
                cardY += 56;
            }
        }

        private void RefreshVendorList()
        {
            _ = RefreshVendorListAsync();
        }

        private async Task RefreshVendorListAsync()
        {
            _vendorIds.Clear();
            _vendorNames.Clear();
            _cmbVendor.Items.Clear();

            string purpose = _radDispose.Checked ? "DISPOSE" : "SELL";
            string flagCol = purpose == "DISPOSE" ? "IsDisposer" : "IsBuyer";

            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    await con.OpenAsync();

                    string vendorSql = $@"
                        SELECT v.VendorID, v.VendorName
                        FROM dbo.Vendor v
                        LEFT JOIN dbo.ArchiveStatus arc
                            ON arc.EntityType = 'Vendor' AND arc.EntityId = v.VendorID AND arc.IsArchived = 1
                        WHERE v.IsActive = 1 AND v.{flagCol} = 1 AND arc.ArchiveId IS NULL
                        ORDER BY v.VendorName";

                    using (var cmd = new SqlCommand(vendorSql, con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _vendorIds.Add(reader.GetInt32(0));
                            _vendorNames.Add(reader.GetString(1));
                            _cmbVendor.Items.Add(reader.GetString(1));
                        }
                    }
                }

                if (_cmbVendor.Items.Count > 0)
                    _cmbVendor.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Logger.LogError("OutboundBatchAssignmentDialog.RefreshVendorListAsync failed", ex);
            }
        }

        private void ToggleStep2Mode()
        {
            bool isNew = _radNewBatch.Checked;
            _pnlNewBatch.Visible      = isNew;
            _pnlExistingBatch.Visible = !isNew;
            _btnConfirm.Text          = isNew ? "Create Batch" : "Add to Batch";
            PositionNavButtons(_btnConfirm.Parent as Panel);
        }

        // ═════════════════════════════════════════════════════════════════════
        // NAVIGATION
        // ═════════════════════════════════════════════════════════════════════

        private void BtnBack_Click(object sender, EventArgs e)
        {
            _currentStep = 1;
            _step1Panel.Visible = true;
            _step2Panel.Visible = false;
            _btnBack.Visible    = false;
            _btnNext.Visible    = true;
            _btnConfirm.Visible = false;
            UpdateStepIndicator();
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            var selectedIds = GetSelectedEmptyCartridgeIds();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show("Select at least one cartridge to continue.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _ = GoToStep2Async();
        }

        private async Task GoToStep2Async()
        {
            _btnNext.Enabled = false;
            try
            {
                await LoadStep2Async();
                _currentStep = 2;
                _step1Panel.Visible = false;
                _step2Panel.Visible = true;
                _btnBack.Visible    = true;
                _btnNext.Visible    = false;
                _btnConfirm.Visible = true;
                UpdateStepIndicator();
                PositionNavButtons(_btnConfirm.Parent as Panel);
            }
            finally
            {
                _btnNext.Enabled = true;
            }
        }

        private async Task BtnConfirm_ClickAsync()
        {
            if (_modelGroups.Count == 0)
            {
                MessageBox.Show("No model groups to assign.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
            var allIds = _modelGroups.SelectMany(g => g.EmptyCartridgeIds).ToList();

            if (_radExistingBatch.Checked)
            {
                // ── Add to existing batch ──────────────────────────────────────
                if (_cmbExistingBatch.SelectedIndex < 0 || _activeBatchIds.Count == 0)
                {
                    MessageBox.Show("Please select an active batch.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int existingBatchId = _activeBatchIds[_cmbExistingBatch.SelectedIndex];

                _btnConfirm.Enabled = false;
                try
                {
                    int assigned = await _refillService.AssignReturnsToBatchAsync(existingBatchId, allIds, userId);

                    MessageBox.Show(
                        $"Added {assigned} unit(s) to batch #{existingBatchId}.",
                        "Added to Batch",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error adding to batch: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Logger.LogError("OutboundBatchAssignmentDialog.BtnConfirm_ClickAsync (existing) failed", ex);
                }
                finally
                {
                    _btnConfirm.Enabled = true;
                }
                return;
            }

            // ── Create new batch ───────────────────────────────────────────────
            if (_cmbVendor.SelectedIndex < 0 || _vendorIds.Count == 0)
            {
                MessageBox.Show("Please select a vendor.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string purpose  = _radDispose.Checked ? "DISPOSE" : "SELL";
            int    vendorId = _vendorIds[_cmbVendor.SelectedIndex];
            string remarks  = _txtRemarks.Text.Trim();

            _btnConfirm.Enabled = false;
            try
            {
                int batchId = await _refillService.CreateOutboundBatchAsync(
                    vendorId,
                    _modelGroups.Select(g => new { g.CartridgeModelId, g.TotalQty }).ToList(),
                    purpose,
                    userId,
                    string.IsNullOrWhiteSpace(remarks) ? null : remarks);

                await _refillService.AssignReturnsToBatchAsync(batchId, allIds, userId);

                MessageBox.Show(
                    $"Outbound batch #{batchId} created ({purpose}).\n" +
                    $"{allIds.Count} row(s) assigned to vendor: {_vendorNames[_cmbVendor.SelectedIndex]}.",
                    "Batch Created",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating batch: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("OutboundBatchAssignmentDialog.BtnConfirm_ClickAsync failed", ex);
            }
            finally
            {
                _btnConfirm.Enabled = true;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // GRID HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private List<int> GetSelectedEmptyCartridgeIds()
        {
            var ids = new List<int>();
            foreach (DataGridViewRow row in _dgvReturns.Rows)
            {
                var dto = row.DataBoundItem as UnassignedReturnDto;
                if (dto != null && true == (row.Cells["chkSelect"].Value as bool?))
                    ids.Add(dto.EmptyCartridgeId);
            }
            return ids;
        }

        private void UpdateSelectionStatus()
        {
            int rows  = 0;
            int units = 0;
            foreach (DataGridViewRow row in _dgvReturns.Rows)
            {
                var dto = row.DataBoundItem as UnassignedReturnDto;
                if (dto != null && true == (row.Cells["chkSelect"].Value as bool?))
                {
                    rows++;
                    units += dto.Quantity;
                }
            }
            _lblSelStatus.Text = $"{rows} row(s) selected  •  {units} unit(s)";
        }

        private void UpdateHeaderCheckState()
        {
            int total    = _dgvReturns.Rows.Count;
            int selected = _dgvReturns.Rows.Cast<DataGridViewRow>()
                .Count(r => true == (r.Cells["chkSelect"].Value as bool?));

            _headerCheckState = selected == 0     ? CheckState.Unchecked
                              : selected == total  ? CheckState.Checked
                                                   : CheckState.Indeterminate;

            if (_dgvReturns.Columns.Count > 0)
                _dgvReturns.InvalidateCell(0, -1);
        }

        private void CmbModelFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            BindFilteredReturns();
        }

        private void DgvReturns_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _dgvReturns.Rows.Count) return;
            var dto = _dgvReturns.Rows[e.RowIndex].DataBoundItem as UnassignedReturnDto;
            if (dto == null) return;
            if (e.ColumnIndex < 0 || e.ColumnIndex >= _dgvReturns.Columns.Count) return;
            var col = _dgvReturns.Columns[e.ColumnIndex];

            if (col.DataPropertyName == "ReturnedAt" && e.Value is DateTime dt)
            {
                e.Value = dt.ToString("yyyy-MM-dd HH:mm");
                e.FormattingApplied = true;
            }

            if (col.Name == "colCondition")
            {
                bool isDamaged = dto.ConditionStatus == "DAMAGED";
                e.Value = isDamaged ? "DAMAGED" : "GOOD";
                e.CellStyle.BackColor  = isDamaged ? Color.FromArgb(255, 224, 178) : Color.FromArgb(200, 230, 201);
                e.CellStyle.ForeColor  = isDamaged ? Color.FromArgb(180, 80, 0)    : Color.FromArgb(27, 94, 32);
                e.FormattingApplied = true;
            }
        }

        private void DgvReturns_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == _dgvReturns.Columns["chkSelect"].Index && e.RowIndex >= 0)
            {
                UpdateSelectionStatus();
                UpdateHeaderCheckState();
            }
        }

        private void DgvReturns_DirtyStateChanged(object sender, EventArgs e)
        {
            if (_dgvReturns.IsCurrentCellDirty)
                _dgvReturns.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void DgvReturns_HeaderCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex != 0) return;

            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
            CheckBoxRenderer.DrawCheckBox(e.Graphics,
                new Point(e.CellBounds.Left + 6, e.CellBounds.Top + 7),
                _headerCheckState == CheckState.Checked      ? CheckBoxState.CheckedNormal
              : _headerCheckState == CheckState.Indeterminate ? CheckBoxState.MixedNormal
                                                              : CheckBoxState.UncheckedNormal);
            e.Handled = true;
        }

        private void DgvReturns_HeaderCellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex != 0) return;

            bool setChecked = _headerCheckState != CheckState.Checked;

            _dgvReturns.EndEdit();
            _dgvReturns.CommitEdit(DataGridViewDataErrorContexts.Commit);
            _dgvReturns.CurrentCell = null;

            foreach (DataGridViewRow row in _dgvReturns.Rows)
            {
                if (row.IsNewRow || !row.Visible) continue;
                row.Cells["chkSelect"].Value = setChecked;
            }

            _dgvReturns.EndEdit();
            _dgvReturns.CommitEdit(DataGridViewDataErrorContexts.Commit);
            UpdateSelectionStatus();
            UpdateHeaderCheckState();
        }
    }
}
