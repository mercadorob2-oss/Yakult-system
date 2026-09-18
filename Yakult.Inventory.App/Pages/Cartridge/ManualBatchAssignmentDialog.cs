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
    /// Two-step wizard for manually assigning unassigned returned cartridges to refill batches.
    ///
    /// STEP 1 — Select Returns:
    ///   Lists all returned cartridges with VendorBatchId IS NULL.
    ///   Mixed cartridge models are allowed in a single selection.
    ///
    /// STEP 2 — Assign to Single Refill Batch:
    ///   User selects ONE refill vendor for all selected models.
    ///   Creates a single VendorCartridgeBatch with multiple VendorCartridgeBatchLine rows
    ///   (one per model), accurately representing one physical shipment to the refiller.
    ///
    ///   All selected returned cartridges are assigned to this single batch,
    ///   regardless of their original supplier or cartridge model.
    ///
    /// BUSINESS RULES:
    ///   - Only Active batches may receive assignments (validated server-side).
    ///   - Existing assignments are never overwritten (null-guard in repository).
    ///   - Batch creation is an explicit user action — never implicit.
    /// </summary>
    public class ManualBatchAssignmentDialog : Form
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
        private ComboBox _cmbModelFilter;
        private readonly List<int>    _modelFilterIds   = new List<int>();
        private readonly List<string> _modelFilterNames = new List<string>();
        private DataGridView _dgvReturns;
        private Label        _lblSelStatus;

        // ── Step 2 — single batch for all models ──────────────────────────────
        // One ModelGroup per distinct CartridgeModelId in the user's selection.
        // All groups share a SINGLE vendor selection — one batch for all models.
        private List<ModelGroup> _modelGroups = new List<ModelGroup>();
        private Panel            _step2ContentPanel;   // scrollable; populated in LoadStep2Async
        private Label            _lblStep2Summary;
        private ComboBox         _cmbVendor;           // single vendor for entire batch
        private TextBox          _txtRemarks;          // single remarks field

        // Vendor list — loaded ONCE for the single vendor dropdown.
        private readonly List<int>    _vendorIds   = new List<int>();
        private readonly List<string> _vendorNames = new List<string>();

        // ── Step 2 — mode toggle (Create New / Use Existing) ─────────────────
        private RadioButton       _radNewBatch;
        private RadioButton       _radExistingBatch;
        private Panel             _pnlNewBatch;
        private Panel             _pnlExistingBatch;
        private ComboBox          _cmbExistingBatch;
        private readonly List<int>    _activeBatchIds    = new List<int>();
        private readonly List<string> _activeBatchLabels = new List<string>();

        // ── Navigation buttons ────────────────────────────────────────────────
        private Button _btnBack;
        private Button _btnNext;
        private Button _btnConfirm;
        private Button _btnCancel;

        // ── Accent colors ─────────────────────────────────────────────────────
        private static readonly Color Teal = Color.FromArgb(0, 150, 136);
        private static readonly Color Gray = Color.FromArgb(189, 189, 189);

        // ── Per-model group (data only; no per-model UI controls) ─────────────
        private class ModelGroup
        {
            public int       CartridgeModelId  { get; set; }
            public string    CartridgeModel    { get; set; }
            public List<int> EmptyCartridgeIds { get; set; } = new List<int>();
            public int       TotalQty          { get; set; }
        }

        // ─────────────────────────────────────────────────────────────────────
        public ManualBatchAssignmentDialog()
        {
            _refillService = new CartridgeRefillService();
            BuildUi();
        }

        // ═════════════════════════════════════════════════════════════════════
        // UI CONSTRUCTION
        // ═════════════════════════════════════════════════════════════════════

        private void BuildUi()
        {
            Text            = "Assign Returns to Refill Batches";
            Size            = new Size(1000, 680);
            MinimumSize     = new Size(860, 580);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = true;
            MinimizeBox     = false;
            BackColor       = Color.White;

            // ── Header ──────────────────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Teal };
            header.Controls.Add(new Label
            {
                Text      = "Assign Returns to Refill Batches",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(16, 12)
            });

            // ── Step indicator bar ───────────────────────────────────────────
            var stepBar = BuildStepBar();

            // ── Content area ─────────────────────────────────────────────────
            var contentArea = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            _step1Panel = BuildStep1Panel();
            _step2Panel = BuildStep2Panel();
            _step2Panel.Visible = false;

            contentArea.Controls.Add(_step2Panel);
            contentArea.Controls.Add(_step1Panel);

            // ── Button bar ───────────────────────────────────────────────────
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
                BackColor = Teal,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            _btnNext.FlatAppearance.BorderSize = 0;
            _btnNext.Click += BtnNext_Click;

            _btnConfirm = new Button
            {
                Text      = "Confirm Assignment",
                Width     = 160,
                Height    = 34,
                BackColor = Color.FromArgb(46, 204, 113),
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

            _lblStep1Circle = MakeCircleLabel("1", new Point(30, 14), Teal);
            _lblStep1Text   = new Label { Text = "Select Returns", Location = new Point(8, 52), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Teal };

            var connector   = new Panel  { Size = new Size(110, 2), Location = new Point(65, 29), BackColor = Gray };

            _lblStep2Circle = MakeCircleLabel("2", new Point(180, 14), Gray);
            _lblStep2Text   = new Label { Text = "Assign Batch", Location = new Point(158, 52), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(90, 90, 90) };

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
            _lblStep1Circle.BackColor = _currentStep >= 1 ? Teal : Gray;
            _lblStep1Text.ForeColor   = _currentStep >= 1 ? Teal : Color.FromArgb(120, 120, 120);
            _lblStep2Circle.BackColor = _currentStep >= 2 ? Teal : Gray;
            _lblStep2Text.ForeColor   = _currentStep >= 2 ? Teal : Color.FromArgb(120, 120, 120);
            _lblStep1Circle.Invalidate();
            _lblStep2Circle.Invalidate();
        }

        // ── Step 1 panel ─────────────────────────────────────────────────────
        private Panel BuildStep1Panel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };

            // ── Status bar (Bottom) ───────────────────────────────────────────
            var statusBar = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 24,
                BackColor = Color.White,
                Padding   = new Padding(16, 0, 16, 0)
            };
            _lblSelStatus = new Label
            {
                Text      = "0 rows selected • 0 units",
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoSize  = true,
                Dock      = DockStyle.Left,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            statusBar.Controls.Add(_lblSelStatus);

            // ── Top bar: title + filter row (Top) ────────────────────────────
            var topBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 90,
                BackColor = Color.White,
                Padding   = new Padding(16, 10, 16, 6)
            };

            topBar.Controls.Add(new Label
            {
                Text      = "Returned Empty Cartridges (Unassigned)",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location  = new Point(16, 10),
                AutoSize  = true
            });

            topBar.Controls.Add(new Label
            {
                Text      = "Filter by model:",
                Location  = new Point(16, 56),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9F),
                BackColor = Color.Transparent
            });

            _cmbModelFilter = new ComboBox
            {
                Location      = new Point(155, 52),
                Width         = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9F)
            };
            _cmbModelFilter.SelectedIndexChanged += CmbModelFilter_SelectedIndexChanged;
            topBar.Controls.Add(_cmbModelFilter);

            // ── DataGridView (Fill) ───────────────────────────────────────────
            _dgvReturns = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                AutoGenerateColumns   = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly              = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = true,
                BackgroundColor       = Color.White,
                BorderStyle           = BorderStyle.None,
                GridColor             = Color.FromArgb(224, 224, 224),
                RowHeadersVisible     = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars            = ScrollBars.Both,
                Margin                = new Padding(16, 0, 16, 0)
            };
            _dgvReturns.ColumnHeadersDefaultCellStyle.BackColor = Teal;
            _dgvReturns.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _dgvReturns.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            _dgvReturns.EnableHeadersVisualStyles               = false;
            _dgvReturns.ColumnHeadersHeightSizeMode             = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _dgvReturns.ColumnHeadersHeight                     = 34;
            _dgvReturns.DefaultCellStyle.SelectionBackColor     = Color.FromArgb(178, 235, 242);
            _dgvReturns.DefaultCellStyle.SelectionForeColor     = Color.Black;
            _dgvReturns.DefaultCellStyle.Font                   = new Font("Segoe UI", 9F);
            _dgvReturns.RowTemplate.Height                      = 26;

            _dgvReturns.Columns.Add(new DataGridViewCheckBoxColumn { Name = "chkSelect", HeaderText = "", Width = 30, ReadOnly = false, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colId",      HeaderText = "ID",       DataPropertyName = "EmptyCartridgeId", Width = 55,  ReadOnly = true });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colModel",   HeaderText = "Model",    DataPropertyName = "CartridgeModel",   Width = 160, ReadOnly = true });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colSupplier",HeaderText = "Supplier", DataPropertyName = "SupplierName",     Width = 160, ReadOnly = true });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colQty",     HeaderText = "Qty",      DataPropertyName = "Quantity",         Width = 55,  ReadOnly = true });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colDate",    HeaderText = "Returned", DataPropertyName = "ReturnedAt",       Width = 130, ReadOnly = true });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colRemarks", HeaderText = "Remarks",  DataPropertyName = "Remarks",          Width = 200, ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            _dgvReturns.CellFormatting               += DgvReturns_CellFormatting;
            _dgvReturns.CellValueChanged             += DgvReturns_CellValueChanged;
            _dgvReturns.CurrentCellDirtyStateChanged += DgvReturns_DirtyStateChanged;
            _dgvReturns.CellPainting                 += DgvReturns_HeaderCellPainting;
            _dgvReturns.CellMouseClick               += DgvReturns_HeaderCellMouseClick;

            // Add in reverse dock order: Fill last so Top/Bottom claim space first
            panel.Controls.Add(_dgvReturns);
            panel.Controls.Add(statusBar);
            panel.Controls.Add(topBar);

            return panel;
        }

        // ── Step 2 panel — container only; content built in LoadStep2Async ───
        private Panel BuildStep2Panel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Visible = false };

            // ── Scrollable model list (Fill) ──────────────────────────────────
            _step2ContentPanel = new Panel
            {
                Dock       = DockStyle.Fill,
                AutoScroll = true,
                BackColor  = Color.White
            };

            // ── Fixed top section (Top) ───────────────────────────────────────
            var topSection = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 262,
                BackColor = Color.White,
                Padding   = new Padding(16, 10, 16, 0)
            };

            int ty = 10;

            topSection.Controls.Add(new Label
            {
                Text      = "Assign to Refill Batch",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location  = new Point(16, ty),
                AutoSize  = true
            });
            ty += 26;

            topSection.Controls.Add(new Label
            {
                Text      = "Create a new batch for all selected models, or add them to an existing Active batch.",
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location  = new Point(16, ty),
                AutoSize  = true
            });
            ty += 22;

            _lblStep2Summary = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location  = new Point(16, ty),
                AutoSize  = true
            };
            topSection.Controls.Add(_lblStep2Summary);
            ty += 26;

            topSection.Controls.Add(new Panel
            {
                Location  = new Point(16, ty),
                Size      = new Size(800, 1),
                BackColor = Color.FromArgb(220, 220, 220),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });
            ty += 10;

            // ── Mode radio buttons ────────────────────────────────────────────
            _radNewBatch = new RadioButton
            {
                Text     = "Create New Batch",
                Location = new Point(16, ty + 2),
                AutoSize = true,
                Font     = new Font("Segoe UI", 9F, FontStyle.Bold),
                Checked  = true
            };
            _radExistingBatch = new RadioButton
            {
                Text     = "Use Existing Active Batch",
                Location = new Point(235, ty + 2),
                AutoSize = true,
                Font     = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            topSection.Controls.Add(_radNewBatch);
            topSection.Controls.Add(_radExistingBatch);
            _radNewBatch.CheckedChanged      += (s, e) => ToggleStep2Mode();
            _radExistingBatch.CheckedChanged += (s, e) => ToggleStep2Mode();
            ty += 32;

            // ── Create-New sub-panel ──────────────────────────────────────────
            _pnlNewBatch = new Panel
            {
                Location = new Point(16, ty),
                Height   = 100,
                Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Visible  = true
            };

            _pnlNewBatch.Controls.Add(new Label
            {
                Text     = "Refill Vendor:",
                Location = new Point(0, 6),
                AutoSize = true,
                Font     = new Font("Segoe UI", 9F, FontStyle.Bold)
            });
            _cmbVendor = new ComboBox
            {
                Location      = new Point(130, 2),
                Width         = 340,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9F)
            };
            _pnlNewBatch.Controls.Add(_cmbVendor);

            _pnlNewBatch.Controls.Add(new Label
            {
                Text      = "Remarks (optional):",
                Location  = new Point(0, 48),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9F),
                BackColor = Color.Transparent
            });
            _txtRemarks = new TextBox
            {
                Location   = new Point(160, 44),
                Height     = 46,
                Multiline  = true,
                ScrollBars = ScrollBars.Vertical,
                Font       = new Font("Segoe UI", 9F),
                Anchor     = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _pnlNewBatch.Controls.Add(_txtRemarks);
            topSection.Controls.Add(_pnlNewBatch);

            // ── Use-Existing sub-panel ────────────────────────────────────────
            _pnlExistingBatch = new Panel
            {
                Location = new Point(16, ty),
                Height   = 90,
                Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Visible  = false
            };

            _pnlExistingBatch.Controls.Add(new Label
            {
                Text     = "Active Batch:",
                Location = new Point(0, 6),
                AutoSize = true,
                Font     = new Font("Segoe UI", 9F, FontStyle.Bold)
            });
            _cmbExistingBatch = new ComboBox
            {
                Location      = new Point(130, 2),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9F),
                Anchor        = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _pnlExistingBatch.Controls.Add(_cmbExistingBatch);

            _pnlExistingBatch.Controls.Add(new Label
            {
                Text      = "Selected returns will be added to the chosen batch without creating a new one.",
                Location  = new Point(0, 40),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100)
            });
            topSection.Controls.Add(_pnlExistingBatch);
            ty += 100;

            topSection.Controls.Add(new Panel
            {
                Location  = new Point(16, ty),
                Size      = new Size(800, 1),
                BackColor = Color.FromArgb(220, 220, 220),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });
            ty += 10;

            // ── "Models to assign:" label ─────────────────────────────────────
            topSection.Controls.Add(new Label
            {
                Text      = "Models to assign:",
                Location  = new Point(16, ty),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold)
            });

            // Wire Resize to keep sub-panels and textbox widths correct
            topSection.Resize += (s, e) =>
            {
                int w = topSection.ClientSize.Width - 32;
                _pnlNewBatch.Width      = w;
                _pnlExistingBatch.Width = w;
                if (_txtRemarks       != null) _txtRemarks.Width       = Math.Max(0, w - 160);
                if (_cmbExistingBatch != null) _cmbExistingBatch.Width = Math.Max(0, w - 130);
            };

            // Add in reverse dock order: Fill first, then Top
            panel.Controls.Add(_step2ContentPanel);
            panel.Controls.Add(topSection);

            return panel;
        }

        private void ToggleStep2Mode()
        {
            bool isNew = _radNewBatch.Checked;
            _pnlNewBatch.Visible      = isNew;
            _pnlExistingBatch.Visible = !isNew;
        }

        // ═════════════════════════════════════════════════════════════════════
        // DATA LOADING
        // ═════════════════════════════════════════════════════════════════════

        private async Task LoadStep1Async()
        {
            try
            {
                _btnNext.Enabled = false;
                _allReturns = await _refillService.GetUnassignedReturnsAsync();

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
                MessageBox.Show($"Error loading returns: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("ManualBatchAssignmentDialog.LoadStep1Async failed", ex);
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

            _dgvReturns.DataSource = null;
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
            _activeBatchIds.Clear();
            _activeBatchLabels.Clear();
            _cmbExistingBatch.Items.Clear();

            // Reset to "Create New" mode each time Step 2 loads
            _radNewBatch.Checked = true;

            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();

                    // Load vendor list once for the single vendor dropdown
                    const string vendorSql = @"
                        SELECT v.VendorID, v.VendorName
                        FROM dbo.Vendor v
                        LEFT JOIN dbo.ArchiveStatus arc
                            ON arc.EntityType = 'Vendor' AND arc.EntityId = v.VendorID AND arc.IsArchived = 1
                        WHERE v.IsActive = 1 AND v.IsRefiller = 1 AND arc.ArchiveId IS NULL
                        ORDER BY v.VendorName";

                    using (var cmd = new SqlCommand(vendorSql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _vendorIds.Add(reader.GetInt32(0));
                            _vendorNames.Add(reader.GetString(1));
                            _cmbVendor.Items.Add(reader.GetString(1));
                        }
                    }

                    if (_cmbVendor.Items.Count > 0)
                        _cmbVendor.SelectedIndex = 0;
                }

                // Load all active batches for "Use Existing" mode
                var activeBatches = await _refillService.GetAllActiveBatchesAsync();
                foreach (var b in activeBatches)
                {
                    string label = $"Batch #{b.BatchId} — {b.VendorName} — {b.CartridgeModel} ({b.ReturnedQty} returned)";
                    _activeBatchIds.Add(b.BatchId);
                    _activeBatchLabels.Add(label);
                    _cmbExistingBatch.Items.Add(label);
                }

                if (_cmbExistingBatch.Items.Count > 0)
                {
                    _cmbExistingBatch.SelectedIndex = 0;
                    _radExistingBatch.Enabled = true;
                }
                else
                {
                    // No active batches — disable the radio so the user can't select it
                    _radExistingBatch.Enabled = false;
                }

                RebuildStep2ModelCards();
                UpdateStep2SummaryAndConfirmState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading vendor data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("ManualBatchAssignmentDialog.LoadStep2Async failed", ex);
            }
        }

        private void RebuildStep2ModelCards()
        {
            _step2ContentPanel.Controls.Clear();

            int yOffset = 8;
            foreach (var group in _modelGroups)
            {
                var card = new Panel
                {
                    Location    = new Point(8, yOffset),
                    Size        = new Size(720, 62),
                    BackColor   = Color.FromArgb(248, 249, 250),
                    BorderStyle = BorderStyle.FixedSingle
                };

                var lblTitle = new Label
                {
                    Text      = group.CartridgeModel,
                    Location  = new Point(12, 10),
                    Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(44, 62, 80),
                    AutoSize  = true,
                    BackColor = Color.Transparent
                };
                card.Controls.Add(lblTitle);

                card.Controls.Add(new Label
                {
                    Text      = $"{group.EmptyCartridgeIds.Count} row(s)  •  {group.TotalQty} unit(s)",
                    Location  = new Point(12, 34),
                    Font      = new Font("Segoe UI", 8.5F),
                    ForeColor = Color.FromArgb(100, 100, 100),
                    AutoSize  = true,
                    BackColor = Color.Transparent
                });

                var btnRemove = new Button
                {
                    Text      = "Remove",
                    Width     = 90,
                    Height    = 28,
                    FlatStyle = FlatStyle.Flat,
                    Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(192, 57, 43),
                    BackColor = Color.White,
                    Cursor    = Cursors.Hand,
                    Tag       = group.CartridgeModelId
                };
                btnRemove.FlatAppearance.BorderColor = Color.FromArgb(192, 57, 43);
                btnRemove.FlatAppearance.BorderSize  = 1;
                btnRemove.Click += (s, e) =>
                {
                    int modelId = (int)((Button)s).Tag;
                    RemoveModelGroup(modelId);
                };
                card.Controls.Add(btnRemove);

                card.Resize += (s, e) =>
                {
                    btnRemove.Location = new Point(card.ClientSize.Width - btnRemove.Width - 12, (card.ClientSize.Height - btnRemove.Height) / 2);
                };
                btnRemove.Location = new Point(card.ClientSize.Width - btnRemove.Width - 12, (card.ClientSize.Height - btnRemove.Height) / 2);

                _step2ContentPanel.Controls.Add(card);
                yOffset += 70;
            }

            _step2ContentPanel.AutoScrollMinSize = new Size(0, yOffset + 8);
        }

        private void RemoveModelGroup(int cartridgeModelId)
        {
            _modelGroups = _modelGroups
                .Where(g => g.CartridgeModelId != cartridgeModelId)
                .OrderBy(g => g.CartridgeModel)
                .ToList();

            RebuildStep2ModelCards();
            UpdateStep2SummaryAndConfirmState();
        }

        private void UpdateStep2SummaryAndConfirmState()
        {
            int totalModels = _modelGroups.Count;
            int totalUnits  = _modelGroups.Sum(g => g.TotalQty);

            _lblStep2Summary.Text = totalModels == 0
                ? "0 model(s)  •  0 total units"
                : $"{totalModels} model(s)  •  {totalUnits} total units  •  " +
                  string.Join(", ", _modelGroups.Select(g => $"{g.CartridgeModel} ({g.TotalQty})"));

            _btnConfirm.Enabled = totalModels > 0;
        }

        // ═════════════════════════════════════════════════════════════════════
        // STEP 1 EVENT HANDLERS
        // ═════════════════════════════════════════════════════════════════════

        private void CmbModelFilter_SelectedIndexChanged(object sender, EventArgs e) => BindFilteredReturns();

        private void DgvReturns_DirtyStateChanged(object sender, EventArgs e)
        {
            if (_dgvReturns.IsCurrentCellDirty)
                _dgvReturns.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void DgvReturns_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _dgvReturns.Columns[e.ColumnIndex].Name != "chkSelect") return;
            UpdateSelectionStatus();
            UpdateHeaderCheckState();
        }

        private void DgvReturns_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_dgvReturns.Columns[e.ColumnIndex].Name == "colDate" && e.Value is DateTime dt)
                e.Value = dt.ToString("yyyy-MM-dd HH:mm");
        }

        private void UpdateSelectionStatus()
        {
            int rowCount = 0;
            int totalQty = 0;
            var modelIds = new HashSet<int>();

            foreach (DataGridViewRow row in _dgvReturns.Rows)
            {
                if (row.Cells["chkSelect"].Value is bool b && b)
                {
                    var item = row.DataBoundItem as UnassignedReturnDto;
                    if (item == null) continue;
                    rowCount++;
                    totalQty += item.Quantity;
                    modelIds.Add(item.CartridgeModelId);
                }
            }

            if (rowCount == 0)
            {
                _lblSelStatus.Text      = "0 rows selected • 0 units";
                _lblSelStatus.ForeColor = Color.FromArgb(100, 100, 100);
            }
            else
            {
                // Mixed models are allowed — inform the user they will be grouped into a single batch
                string modelNote = modelIds.Count > 1
                    ? $"  •  {modelIds.Count} models — Step 2 will create one batch for all models"
                    : "";
                _lblSelStatus.Text      = $"{rowCount} rows selected • {totalQty} units{modelNote}";
                _lblSelStatus.ForeColor = Color.FromArgb(39, 174, 96);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // NAVIGATION
        // ═════════════════════════════════════════════════════════════════════

        private async void BtnNext_Click(object sender, EventArgs e)
        {
            if (_currentStep != 1) return;

            var selected = GetSelectedReturns();
            if (selected.Count == 0)
            {
                MessageBox.Show(
                    "Please select at least one returned cartridge row to assign.",
                    "No Selection",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var selectedIds = selected.Select(r => r.EmptyCartridgeId).ToList();
            var eligibleIds = await _refillService.GetEligibleRefillEmptyCartridgeIdsAsync(selectedIds);
            var eligibleSet = new HashSet<int>(eligibleIds);

            var filtered = selected.Where(r => eligibleSet.Contains(r.EmptyCartridgeId)).ToList();
            if (filtered.Count == 0)
            {
                MessageBox.Show(
                    "None of the selected returns are eligible for refill batching. " +
                    "Only refillable cartridge models can be assigned to a refill batch.",
                    "No Eligible Models",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (filtered.Count != selected.Count)
            {
                var removedSummary = selected
                    .Where(r => !eligibleSet.Contains(r.EmptyCartridgeId))
                    .GroupBy(r => r.CartridgeModel)
                    .OrderBy(g => g.Key)
                    .Select(g => $"{g.Key} ({g.Sum(x => x.Quantity)} unit(s))")
                    .ToList();

                MessageBox.Show(
                    "Some selected returns are not eligible for refill batching and will be excluded.\n\n" +
                    "Only refillable cartridge models can be assigned to a refill batch.\n\n" +
                    string.Join("\n", removedSummary),
                    "Ineligible Models Excluded",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            _modelGroups = filtered
                .GroupBy(r => r.CartridgeModelId)
                .OrderBy(g => g.First().CartridgeModel)
                .Select(g => new ModelGroup
                {
                    CartridgeModelId  = g.Key,
                    CartridgeModel    = g.First().CartridgeModel,
                    EmptyCartridgeIds = g.Select(r => r.EmptyCartridgeId).ToList(),
                    TotalQty          = g.Sum(r => r.Quantity)
                })
                .ToList();

            UpdateStep2SummaryAndConfirmState();

            _btnNext.Enabled = false;
            try   { await LoadStep2Async(); }
            finally { _btnNext.Enabled = true; }

            GoToStep(2);
        }

        private void BtnBack_Click(object sender, EventArgs e)
        {
            if (_currentStep == 2) GoToStep(1);
        }

        private void GoToStep(int step)
        {
            _currentStep = step;
            _step1Panel.Visible = step == 1;
            _step2Panel.Visible = step == 2;
            _btnBack.Visible    = step > 1;
            _btnNext.Visible    = step < 2;
            _btnConfirm.Visible = step == 2;

            var bar = _btnBack.Parent as Panel;
            if (bar != null) PositionNavButtons(bar);
            UpdateStepIndicator();
        }

        // ═════════════════════════════════════════════════════════════════════
        // CONFIRM — Create New or Use Existing batch
        // ═════════════════════════════════════════════════════════════════════

        private async Task BtnConfirm_ClickAsync()
        {
            if (_modelGroups.Count == 0)
            {
                MessageBox.Show(
                    "No eligible models remain to assign. Please go back and select eligible returns.",
                    "Nothing to Confirm",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (_radExistingBatch.Checked)
            {
                await BtnConfirm_UseExistingAsync();
                return;
            }

            // ── Create New Batch (original flow) ─────────────────────────────

            // Validate vendor selection
            if (_cmbVendor.SelectedIndex < 0 || _vendorIds.Count == 0)
            {
                MessageBox.Show(
                    "Please select a refill vendor.",
                    "No Vendor Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int vendorId = _vendorIds[_cmbVendor.SelectedIndex];
            string vendorName = _vendorNames[_cmbVendor.SelectedIndex];
            string remarks = _txtRemarks.Text.Trim();

            // Build confirmation summary
            var summaryLines = _modelGroups.Select(g =>
                $"  • [{g.CartridgeModel}]  {g.TotalQty} unit(s)  ({g.EmptyCartridgeIds.Count} row(s))");

            int totalUnits = _modelGroups.Sum(g => g.TotalQty);

            var confirm = MessageBox.Show(
                $"Create one refill batch for {_modelGroups.Count} model(s)?\n\n" +
                $"Vendor: {vendorName}\n" +
                $"Total units: {totalUnits}\n\n" +
                string.Join("\n", summaryLines) + "\n\n" +
                "All models will be grouped into a single batch (one physical shipment).\n\n" +
                "Proceed?",
                "Confirm Batch Creation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            _btnConfirm.Enabled = false;
            _btnBack.Enabled    = false;

            try
            {
                // Create one batch with multiple model lines
                int batchId = await _refillService.CreateMultiModelRefillBatchAsync(
                    vendorId,
                    _modelGroups,
                    AppSession.CurrentUserId,
                    string.IsNullOrEmpty(remarks) ? null : remarks);

                Logger.LogInfo(
                    $"ManualBatchAssignmentDialog: Created multi-model batch {batchId} — " +
                    $"vendor={vendorId}, models={_modelGroups.Count}");

                // Assign all returned cartridges to the single batch
                var allEmptyIds = _modelGroups.SelectMany(g => g.EmptyCartridgeIds).ToList();
                int assignedQty = await _refillService.AssignReturnsToBatchAsync(
                    batchId,
                    allEmptyIds,
                    AppSession.CurrentUserId);

                Logger.LogInfo(
                    $"ManualBatchAssignmentDialog: Assigned {assignedQty} units to batch {batchId}");

                var resultLines = _modelGroups.Select(g =>
                    $"  • [{g.CartridgeModel}]  {g.TotalQty} unit(s)");

                MessageBox.Show(
                    $"Batch #{batchId} created successfully!\n\n" +
                    $"Vendor: {vendorName}\n" +
                    $"Total units assigned: {assignedQty}\n\n" +
                    string.Join("\n", resultLines) + "\n\n" +
                    "All selected returns are now linked to this refill batch.",
                    "Assignment Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (InvalidOperationException ioe)
            {
                MessageBox.Show(ioe.Message, "Cannot Create Batch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Logger.LogError("ManualBatchAssignmentDialog.BtnConfirm_ClickAsync: operation blocked", ioe);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during batch creation: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("ManualBatchAssignmentDialog.BtnConfirm_ClickAsync failed", ex);
            }
            finally
            {
                _btnConfirm.Enabled = true;
                _btnBack.Enabled    = true;
            }
        }

        private async Task BtnConfirm_UseExistingAsync()
        {
            if (_modelGroups.Count == 0)
            {
                MessageBox.Show(
                    "No eligible models remain to assign. Please go back and select eligible returns.",
                    "Nothing to Confirm",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (_cmbExistingBatch.SelectedIndex < 0 || _activeBatchIds.Count == 0)
            {
                MessageBox.Show(
                    "Please select an existing active batch.",
                    "No Batch Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int existingBatchId    = _activeBatchIds[_cmbExistingBatch.SelectedIndex];
            string existingBatchLabel = _activeBatchLabels[_cmbExistingBatch.SelectedIndex];

            var allEmptyIds = _modelGroups.SelectMany(g => g.EmptyCartridgeIds).ToList();
            int totalUnits  = _modelGroups.Sum(g => g.TotalQty);

            var summaryLines = _modelGroups.Select(g =>
                $"  • [{g.CartridgeModel}]  {g.TotalQty} unit(s)  ({g.EmptyCartridgeIds.Count} row(s))");

            var confirm = MessageBox.Show(
                $"Add {totalUnits} unit(s) to existing batch?\n\n" +
                $"Batch: {existingBatchLabel}\n\n" +
                string.Join("\n", summaryLines) + "\n\n" +
                "Proceed?",
                "Confirm Assignment",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            _btnConfirm.Enabled = false;
            _btnBack.Enabled    = false;

            try
            {
                int assignedQty = await _refillService.AssignReturnsToBatchAsync(
                    existingBatchId,
                    allEmptyIds,
                    AppSession.CurrentUserId);

                Logger.LogInfo(
                    $"ManualBatchAssignmentDialog: Assigned {assignedQty} units to existing batch {existingBatchId}");

                MessageBox.Show(
                    $"Assignment successful!\n\n" +
                    $"Batch: {existingBatchLabel}\n" +
                    $"Total units assigned: {assignedQty}\n\n" +
                    "All selected returns are now linked to this refill batch.",
                    "Assignment Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (InvalidOperationException ioe)
            {
                MessageBox.Show(ioe.Message, "Cannot Assign", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Logger.LogError("ManualBatchAssignmentDialog.BtnConfirm_UseExistingAsync: operation blocked", ioe);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during assignment: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("ManualBatchAssignmentDialog.BtnConfirm_UseExistingAsync failed", ex);
            }
            finally
            {
                _btnConfirm.Enabled = true;
                _btnBack.Enabled    = true;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Paints a visual checkbox in the chkSelect column header cell.
        /// </summary>
        private void DgvReturns_HeaderCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex != _dgvReturns.Columns["chkSelect"].Index)
                return;

            e.PaintBackground(e.ClipBounds, true);

            var pt = new Point(
                e.CellBounds.X + (e.CellBounds.Width  - 13) / 2,
                e.CellBounds.Y + (e.CellBounds.Height - 13) / 2);

            CheckBoxState state;
            switch (_headerCheckState)
            {
                case CheckState.Checked:       state = CheckBoxState.CheckedNormal;   break;
                case CheckState.Indeterminate: state = CheckBoxState.MixedNormal;     break;
                default:                       state = CheckBoxState.UncheckedNormal; break;
            }

            CheckBoxRenderer.DrawCheckBox(e.Graphics, pt, state);
            e.Handled = true;
        }

        /// <summary>
        /// Toggles all visible row checkboxes when the chkSelect header cell is clicked.
        /// </summary>
        private void DgvReturns_HeaderCellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex != _dgvReturns.Columns["chkSelect"].Index)
                return;

            bool newValue = _headerCheckState != CheckState.Checked;

            _dgvReturns.CellValueChanged -= DgvReturns_CellValueChanged;
            try
            {
                foreach (DataGridViewRow row in _dgvReturns.Rows)
                    row.Cells["chkSelect"].Value = newValue;
            }
            finally
            {
                _dgvReturns.CellValueChanged += DgvReturns_CellValueChanged;
            }

            _dgvReturns.EndEdit();
            _headerCheckState = newValue ? CheckState.Checked : CheckState.Unchecked;
            _dgvReturns.InvalidateCell(_dgvReturns.Columns["chkSelect"].Index, -1);
            UpdateSelectionStatus();
        }

        /// <summary>
        /// Recomputes _headerCheckState (Checked / Unchecked / Indeterminate) from current row states
        /// and invalidates the header cell so it repaints.
        /// </summary>
        private void UpdateHeaderCheckState()
        {
            int total = _dgvReturns.Rows.Count;
            if (total == 0)
            {
                _headerCheckState = CheckState.Unchecked;
            }
            else
            {
                int checkedCount = 0;
                foreach (DataGridViewRow row in _dgvReturns.Rows)
                    if (row.Cells["chkSelect"].Value is bool b && b) checkedCount++;

                _headerCheckState = checkedCount == 0     ? CheckState.Unchecked    :
                                    checkedCount == total  ? CheckState.Checked      :
                                                             CheckState.Indeterminate;
            }

            _dgvReturns.InvalidateCell(_dgvReturns.Columns["chkSelect"].Index, -1);
        }

        private List<UnassignedReturnDto> GetSelectedReturns()
        {
            var result = new List<UnassignedReturnDto>();
            foreach (DataGridViewRow row in _dgvReturns.Rows)
            {
                if (row.Cells["chkSelect"].Value is bool b && b)
                {
                    var item = row.DataBoundItem as UnassignedReturnDto;
                    if (item != null)
                        result.Add(item);
                }
            }
            return result;
        }
    }
}
