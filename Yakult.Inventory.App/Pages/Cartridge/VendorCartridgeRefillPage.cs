using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Pages.Cartridge;

namespace Yakult.Inventory.App.Pages
{
    /// <summary>
    /// Vendor Cartridge Refill Page - Batch-level vendor refill tracking
    /// This page is strictly for vendor refill batching and dispatch
    /// Does NOT show individual cartridges or affect issuing operations
    /// </summary>
    public partial class VendorCartridgeRefillPage : UserControl
    {
        private DataGridView dgvAuditTrail;  // Audit trail table tied to selected vendor batch (READ-ONLY)
        private DataGridView dgvBatches;
        private Button btnRefresh;
        private Button btnSendToVendor;
        private Button btnEditBatch;  // Combined modal for editing threshold + vendor
        private Button btnCreateBatch;  // Explicit batch creation — never triggered by return logic
        private Button btnAssignReturns; // Manual assignment of unassigned returns to a batch
        private Button btnDeleteBatch;  // Delete a refill batch (with confirmation if Active)

        private Panel _damagedNoticePanel;
        private Label _lblDamagedNotice;

        private ContextMenuStrip _auditContextMenu;
        private ToolStripMenuItem _menuTransfer;
        private ToolStripMenuItem _menuRemove;

        private readonly CartridgeRefillService _refillService;
        private List<VendorBatchAuditTrailDto> _auditTrail;  // Audit trail data
        private List<RefillEligibilityResult> _batches;

        // ── Pagination ────────────────────────────────────────────────────────
        private int    _currentPage = 1;
        private const int PageSize  = 15;
        private int    _totalPages  = 1;
        private Label  _lblPageInfo;
        private Button _btnPrevPage;
        private Button _btnNextPage;

        public VendorCartridgeRefillPage()
        {
            _refillService = new CartridgeRefillService();
            _auditTrail = new List<VendorBatchAuditTrailDto>();
            _batches = new List<RefillEligibilityResult>();
            BuildUi();
            _ = LoadDataAsync();
        }

        private void BuildUi()
        {
            // Set UserControl properties
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;

            // 1) Header Panel - add to this.Controls FIRST (will dock at top)
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(0, 150, 136),
                Padding = new Padding(15, 10, 15, 10)
            };

            var lblTitle = new Label
            {
                Text = "Vendor Cartridge Refill",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(15, 10)
            };

            headerPanel.Controls.Add(lblTitle);

            // 2) Action Bar Panel
            var actionBarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(10, 7, 10, 7)
            };

            btnRefresh = new Button
            {
                Text = "⟳ Refresh",
                Width = 100,
                Height = 32,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(10, 6)
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += async (s, e) => await LoadDataAsync();

            btnEditBatch = new Button
            {
                Text = "Edit Batch",
                Width = 120,
                Height = 32,
                BackColor = Color.FromArgb(243, 156, 18),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Enabled = false,
                Location = new Point(120, 6)
            };
            btnEditBatch.FlatAppearance.BorderSize = 0;
            btnEditBatch.Click += BtnEditBatch_Click;

            btnSendToVendor = new Button
            {
                Text = "Send to Vendor",
                Width = 140,
                Height = 32,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Enabled = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(250, 6)
            };
            btnSendToVendor.FlatAppearance.BorderSize = 0;
            btnSendToVendor.Click += BtnSendToVendor_Click;

            btnCreateBatch = new Button
            {
                Text = "+ Create Batch",
                Width = 130,
                Height = 32,
                BackColor = Color.FromArgb(39, 174, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(400, 6)
            };
            btnCreateBatch.FlatAppearance.BorderSize = 0;
            btnCreateBatch.Click += BtnCreateBatch_Click;

            btnAssignReturns = new Button
            {
                Text      = "↩ Assign Returns",
                Width     = 140,
                Height    = 32,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location  = new Point(540, 6)
            };
            btnAssignReturns.FlatAppearance.BorderSize = 0;
            btnAssignReturns.Click += BtnAssignReturns_Click;

            btnDeleteBatch = new Button
            {
                Text      = "🗑 Delete Batch",
                Width     = 130,
                Height    = 32,
                BackColor = Color.FromArgb(192, 57, 43),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Enabled   = false,
                Location  = new Point(690, 6)
            };
            btnDeleteBatch.FlatAppearance.BorderSize = 0;
            btnDeleteBatch.Click += async (s, e) => await BtnDeleteBatch_ClickAsync();

            actionBarPanel.Controls.Add(btnRefresh);
            actionBarPanel.Controls.Add(btnEditBatch);
            actionBarPanel.Controls.Add(btnSendToVendor);
            actionBarPanel.Controls.Add(btnCreateBatch);
            actionBarPanel.Controls.Add(btnAssignReturns);
            actionBarPanel.Controls.Add(btnDeleteBatch);

            _damagedNoticePanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.FromArgb(255, 243, 205),
                Visible = false,
                Padding = new Padding(10, 8, 10, 8)
            };

            _lblDamagedNotice = new Label
            {
                AutoSize = false,
                ForeColor = Color.FromArgb(133, 77, 14),
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _damagedNoticePanel.Controls.Add(_lblDamagedNotice);

            // 3) Audit Trail Section - shows request-level contributions to selected vendor batch (READ-ONLY)
            var auditPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 370,
                BackColor = Color.White,
                Padding = new Padding(0)
            };

            var auditHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding(10, 10, 10, 10)
            };

            var lblAuditTitle = new Label
            {
                Text = "Vendor Batch Audit Trail (Read-Only)",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 2)
            };
            auditHeaderPanel.Controls.Add(lblAuditTitle);

            var lblAuditSubtitle = new Label
            {
                Text = "Request-level rows that contributed to the selected vendor batch • Select a batch below to view audit trail",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(149, 165, 166),
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 4)
            };
            auditHeaderPanel.Controls.Add(lblAuditSubtitle);

            dgvAuditTrail = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,  // 100% READ-ONLY
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                ColumnHeadersVisible = true,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 36,
                RowHeadersVisible = false
            };

            dgvAuditTrail.EnableHeadersVisualStyles = false;
            dgvAuditTrail.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 152, 219);
            dgvAuditTrail.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvAuditTrail.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvAuditTrail.ColumnHeadersDefaultCellStyle.Padding = new Padding(5);
            dgvAuditTrail.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(232, 240, 248);
            dgvAuditTrail.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
            dgvAuditTrail.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvAuditTrail.DefaultCellStyle.Font = new Font("Segoe UI", 9F);

            // NO CHECKBOX COLUMN - purely read-only
            dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "RequestId",
                HeaderText = "Request ID",
                Width = 90
            });

            dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "RequestDate",
                HeaderText = "Request Date",
                Width = 140,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "yyyy-MM-dd HH:mm"
                }
            });

            dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ReturnedQty",
                HeaderText = "Returned Qty",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                }
            });

            dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CartridgeModel",
                HeaderText = "Cartridge Model",
                FillWeight = 20
            });

            dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Vendor",
                HeaderText = "Vendor",
                FillWeight = 20
            });

            dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "BatchId",
                HeaderText = "Batch ID",
                Width = 80
            });

            dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ReturnedByName",
                HeaderText = "Returned By",
                FillWeight = 25,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    WrapMode = DataGridViewTriState.True
                }
            });

            dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Remarks",
                HeaderText = "Remarks",
                FillWeight = 25,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    ForeColor = Color.FromArgb(127, 140, 141)
                }
            });

            // Right-click context menu for transfer / remove operations
            _auditContextMenu = new ContextMenuStrip();
            _menuTransfer = new ToolStripMenuItem("\u21c4  Transfer to Another Batch");
            _menuRemove   = new ToolStripMenuItem("\u21a9  Remove from Batch (Return to Step 1)");
            _menuRemove.ForeColor = Color.FromArgb(192, 0, 0);
            _auditContextMenu.Items.Add(_menuTransfer);
            _auditContextMenu.Items.Add(new ToolStripSeparator());
            _auditContextMenu.Items.Add(_menuRemove);
            _menuTransfer.Click += async (s, e) => await HandleTransferToBatchAsync();
            _menuRemove.Click   += async (s, e) => await HandleRemoveFromBatchAsync();
            dgvAuditTrail.MouseDown += DgvAuditTrail_MouseDown;

            // IMPORTANT: Ensure docking order reserves space for the header panel.
            auditPanel.Controls.Add(dgvAuditTrail);
            auditPanel.Controls.Add(auditHeaderPanel);

            // 4) Separator
            var separator = new Panel
            {
                Dock = DockStyle.Top,
                Height = 2,
                BackColor = Color.FromArgb(224, 224, 224)
            };

            // 5) Batches Section Title
            var batchesTitlePanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.White,
                Padding = new Padding(10, 10, 10, 0)
            };

            var lblBatchesTitle = new Label
            {
                Text = "Vendor Batches (System-Level Tracking)",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true
            };
            batchesTitlePanel.Controls.Add(lblBatchesTitle);

            // 6) DataGridView for Batches (Dock=Fill, added LAST so it fills remaining space)
            dgvBatches = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = true,
                ReadOnly = false,  // Allow checkbox interaction - text columns are individually read-only
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                RowHeadersVisible = false
            };

            // Style grid
            dgvBatches.EnableHeadersVisualStyles = false;
            dgvBatches.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 150, 136);
            dgvBatches.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvBatches.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvBatches.ColumnHeadersDefaultCellStyle.Padding = new Padding(5);
            dgvBatches.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
            dgvBatches.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 150, 136);
            dgvBatches.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvBatches.DefaultCellStyle.Font = new Font("Segoe UI", 9F);

            // Define columns (all read-only)
            // Checkbox column for row selection
            var chkColumnBatches = new DataGridViewCheckBoxColumn
            {
                Name = "chkSelectBatch",
                HeaderText = "",
                Width = 30,
                MinimumWidth = 30,
                ReadOnly = false,
                Resizable = DataGridViewTriState.False
            };
            dgvBatches.Columns.Add(chkColumnBatches);

            dgvBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "BatchId",
                HeaderText = "Batch ID",
                Width = 60,
                MinimumWidth = 50,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dgvBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "VendorName",
                HeaderText = "Vendor",
                FillWeight = 30,
                MinimumWidth = 100,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True }
            });

            dgvBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CartridgeModel",
                HeaderText = "Cartridge Model/s",
                FillWeight = 30,
                MinimumWidth = 100,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True }
            });

            dgvBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "OriginalQty",
                HeaderText = "Original Qty",
                Width = 80,
                MinimumWidth = 60,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dgvBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ReturnedQty",
                HeaderText = "Returned Qty",
                Width = 90,
                MinimumWidth = 70,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dgvBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "QtyPerCartridge",
                HeaderText = "Qty Per Cartridge",
                FillWeight = 25,
                MinimumWidth = 110,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True }
            });

            dgvBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Status",
                HeaderText = "Status",
                Width = 100,
                MinimumWidth = 80,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                }
            });

            // Add context-sensitive Action button column
            var btnActionColumn = new DataGridViewButtonColumn
            {
                Name = "btnAction",
                HeaderText = "Action",
                UseColumnTextForButtonValue = false,
                Width = 140,
                MinimumWidth = 100,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Padding = new Padding(5)
                }
            };
            dgvBatches.Columns.Add(btnActionColumn);

            // Wire up events
            dgvBatches.SelectionChanged += async (s, e) =>
            {
                if (dgvBatches.SelectedRows.Count == 1)
                {
                    var batch = dgvBatches.SelectedRows[0].DataBoundItem as RefillEligibilityResult;
                    btnSendToVendor.Enabled = batch != null && batch.Status == "Active" && batch.IsRefillable;

                    // Load audit trail for selected batch
                    if (batch != null)
                    {
                        await LoadAuditTrailAsync(batch.BatchId);
                    }
                }
                else
                {
                    btnSendToVendor.Enabled = false;
                    // Clear audit trail when no batch is selected
                    _auditTrail.Clear();
                    dgvAuditTrail.DataSource = null;
                }
            };

            // Handle button clicks in the grid
            dgvBatches.CellContentClick += DgvBatches_CellContentClick;

            // Handle checkbox clicks for selection tracking
            dgvBatches.CellContentClick += (s, e) =>
            {
                // Handle checkbox column clicks
                if (e.ColumnIndex >= 0 && dgvBatches.Columns[e.ColumnIndex].Name == "chkSelectBatch" && e.RowIndex >= 0)
                {
                    dgvBatches.EndEdit();  // Commit the checkbox change
                    UpdateBatchActionButtonStates();
                }
            };

            dgvBatches.CurrentCellDirtyStateChanged += (s, e) =>
            {
                // Commit checkbox changes immediately
                if (dgvBatches.CurrentCell is DataGridViewCheckBoxCell)
                {
                    dgvBatches.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            dgvBatches.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= dgvBatches.Rows.Count)
                    return;

                var batch = dgvBatches.Rows[e.RowIndex].DataBoundItem as RefillEligibilityResult;
                if (batch == null)
                    return;

                var columnName = dgvBatches.Columns[e.ColumnIndex].DataPropertyName;

                if (columnName == "Status" && e.Value != null)
                {
                    switch (e.Value.ToString())
                    {
                        case "Active":
                            e.CellStyle.ForeColor = Color.FromArgb(52, 152, 219);
                            break;
                        case "SentForRefill":
                            e.CellStyle.ForeColor = Color.Gray;
                            break;
                    }
                }

                // Handle context-sensitive Action button column
                if (dgvBatches.Columns[e.ColumnIndex].Name == "btnAction")
                {
                    // Non-refillable models have no action in this page
                    if (!batch.IsRefillable)
                    {
                        dgvBatches.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = "";
                        e.CellStyle.BackColor = Color.FromArgb(245, 247, 250);
                        e.CellStyle.SelectionBackColor = Color.FromArgb(245, 247, 250);
                    }
                    else
                    {
                        switch (batch.Status)
                        {
                            case "Active":
                                dgvBatches.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = "Send to Vendor";
                                e.CellStyle.BackColor = Color.FromArgb(52, 152, 219); // Blue
                                e.CellStyle.ForeColor = Color.White;
                                e.CellStyle.Font = new Font(e.CellStyle.Font, FontStyle.Bold);
                                break;

                            case "SentForRefill":
                                dgvBatches.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = "Close Batch";
                                e.CellStyle.BackColor = Color.FromArgb(149, 165, 166); // Gray
                                e.CellStyle.ForeColor = Color.White;
                                e.CellStyle.Font = new Font(e.CellStyle.Font, FontStyle.Bold);
                                break;

                            default:
                                dgvBatches.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = "";
                                e.CellStyle.BackColor = Color.FromArgb(245, 247, 250);
                                e.CellStyle.SelectionBackColor = Color.FromArgb(245, 247, 250);
                                break;
                        }
                    }
                }
            };

            // ── Pagination bar (Dock=Bottom) ─────────────────────────────────
            var paginationPanel = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 38,
                BackColor = Color.FromArgb(245, 247, 250)
            };

            _btnPrevPage = new Button
            {
                Text      = "◀ Prev",
                Width     = 75,
                Height    = 26,
                Location  = new Point(10, 6),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 150, 136),
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                Enabled   = false
            };
            _btnPrevPage.FlatAppearance.BorderSize = 0;
            _btnPrevPage.Click += (s, e) => { _currentPage--; ApplyPage(); };

            _lblPageInfo = new Label
            {
                Text      = "Page 1 of 1  (0 records)",
                AutoSize  = false,
                Width     = 220,
                Height    = 26,
                Location  = new Point(94, 6),
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 8.5F),
                BackColor = Color.Transparent
            };

            _btnNextPage = new Button
            {
                Text      = "Next ▶",
                Width     = 75,
                Height    = 26,
                Location  = new Point(323, 6),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 150, 136),
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                Enabled   = false
            };
            _btnNextPage.FlatAppearance.BorderSize = 0;
            _btnNextPage.Click += (s, e) => { _currentPage++; ApplyPage(); };

            paginationPanel.Controls.Add(_btnPrevPage);
            paginationPanel.Controls.Add(_lblPageInfo);
            paginationPanel.Controls.Add(_btnNextPage);

            // ADD CONTROLS TO this.Controls IN CORRECT ORDER FOR DOCKING
            // Fill control LAST, Top controls FIRST (in reverse visual order)
            this.Controls.Add(dgvBatches);          // Fill   - added first, docks last
            this.Controls.Add(paginationPanel);     // Bottom - must be before Top controls
            this.Controls.Add(batchesTitlePanel);   // Top - batches title
            this.Controls.Add(separator);           // Top - separator line
            this.Controls.Add(auditPanel);          // Top - audit trail section
            this.Controls.Add(_damagedNoticePanel); // Top - warning notice
            this.Controls.Add(actionBarPanel);      // Top - action bar
            this.Controls.Add(headerPanel);         // Top - added last, docks first (appears at top)
        }

        /// <summary>
        /// Slices _batches to the current page and updates the pagination bar.
        /// </summary>
        private void ApplyPage()
        {
            _totalPages  = _batches.Count == 0 ? 1 : (int)Math.Ceiling(_batches.Count / (double)PageSize);
            _currentPage = Math.Max(1, Math.Min(_currentPage, _totalPages));

            var page = _batches
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            dgvBatches.DataSource = page;
            dgvBatches.ClearSelection();
            btnSendToVendor.Enabled = false;

            int from = _batches.Count == 0 ? 0 : (_currentPage - 1) * PageSize + 1;
            int to   = Math.Min(_currentPage * PageSize, _batches.Count);
            _lblPageInfo.Text = $"Page {_currentPage} of {_totalPages}  ({from}–{to} of {_batches.Count})";

            _btnPrevPage.Enabled = _currentPage > 1;
            _btnNextPage.Enabled = _currentPage < _totalPages;
        }

        /// <summary>
        /// Loads batch data from service
        /// Batches: Shows system-level vendor batch tracking
        /// </summary>
        private async Task LoadDataAsync()
        {
            try
            {
                btnRefresh.Enabled = false;
                btnSendToVendor.Enabled = false;

                // Re-evaluate batch eligibility
                await _refillService.ProcessEligibleBatchesAsync();

                // Load batch data
                _batches = await _refillService.GetRefillEligibilityAsync();
                _currentPage = 1;
                ApplyPage();

                // Clear audit trail
                _auditTrail.Clear();
                dgvAuditTrail.DataSource = null;

                var damaged = await _refillService.GetDamagedUnassignedReturnsAsync();
                int damagedCount = damaged.Sum(d => d.Quantity);
                if (damagedCount > 0)
                {
                    _lblDamagedNotice.Text =
                        $"⚠  {damagedCount} DAMAGED returned cartridge(s) are excluded from refill batches " +
                        "— process them via Cartridge Dispose / Sell Batch.";
                    _damagedNoticePanel.Visible = true;
                }
                else
                {
                    _damagedNoticePanel.Visible = false;
                }

                if (dgvBatches.Rows.Count == 0)
                    btnSendToVendor.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading vendor refill data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("VendorCartridgeRefillPage.LoadDataAsync failed", ex);
            }
            finally
            {
                btnRefresh.Enabled = true;
                // Don't re-enable action buttons here - let SelectionChanged handlers manage them
            }
        }

        /// <summary>
        /// Loads audit trail data for a specific vendor batch
        /// Shows request-level rows that contributed to the batch (READ-ONLY)
        /// </summary>
        private async Task LoadAuditTrailAsync(int batchId)
        {
            try
            {
                var rawAuditTrail = await _refillService.GetBatchAuditTrailAsync(batchId);

                _auditTrail = rawAuditTrail
                    .GroupBy(x => new { x.RequestId, x.CartridgeModelId, x.CartridgeModel, x.Vendor, x.BatchId })
                    .Select(g => new VendorBatchAuditTrailDto
                    {
                        EmptyCartridgeId = g.Max(x => x.EmptyCartridgeId),
                        RequestId        = g.Key.RequestId,
                        RequestDate      = g.Min(x => x.RequestDate),
                        ReturnedQty      = g.Sum(x => x.ReturnedQty),
                        CartridgeModelId = g.Key.CartridgeModelId,
                        CartridgeModel   = g.Key.CartridgeModel,
                        Vendor           = g.Key.Vendor,
                        BatchId          = g.Key.BatchId,
                        ReturnedByName   = g.Select(x => x.ReturnedByName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count() == 1
                            ? g.Select(x => x.ReturnedByName).FirstOrDefault()
                            : "Multiple",
                        Remarks = g.Select(x => x.Remarks).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count() == 1
                            ? g.Select(x => x.Remarks).FirstOrDefault()
                            : string.Empty
                    })
                    .OrderByDescending(x => x.RequestDate)
                    .ThenByDescending(x => x.RequestId ?? 0)
                    .ToList();

                dgvAuditTrail.DataSource = _auditTrail;
                dgvAuditTrail.ClearSelection();
                dgvAuditTrail.CurrentCell = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading audit trail: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("VendorCartridgeRefillPage.LoadAuditTrailAsync failed", ex);
            }
        }

        /// <summary>
        /// Updates action button states based on batch selection
        /// Edit Threshold: Enabled when exactly one row selected and status is Active
        /// </summary>
        private void UpdateBatchActionButtonStates()
        {
            // Count selected batches
            int selectedCount = 0;
            RefillEligibilityResult selectedBatch = null;

            foreach (DataGridViewRow row in dgvBatches.Rows)
            {
                if (row.Cells["chkSelectBatch"].Value != null && (bool)row.Cells["chkSelectBatch"].Value)
                {
                    selectedCount++;
                    selectedBatch = row.DataBoundItem as RefillEligibilityResult;
                }
            }

            // Edit Batch button: Enabled when exactly one selected and batch is Active.
            // SentForRefill/Closed batches are immutable.
            btnEditBatch.Enabled = selectedCount == 1 &&
                                   selectedBatch != null &&
                                   selectedBatch.Status == "Active";

            // Delete Batch button: Enabled when exactly one batch is selected.
            // SentForRefill batches are blocked at click time with an informational message.
            btnDeleteBatch.Enabled = selectedCount == 1 && selectedBatch != null;
        }

        /// <summary>
        /// Opens combined modal dialog to edit batch threshold and vendor
        /// </summary>
        private async void BtnEditBatch_Click(object sender, EventArgs e)
        {
            try
            {
                // Find the selected batch
                RefillEligibilityResult selectedBatch = null;
                foreach (DataGridViewRow row in dgvBatches.Rows)
                {
                    if (row.Cells["chkSelectBatch"].Value != null && (bool)row.Cells["chkSelectBatch"].Value)
                    {
                        selectedBatch = row.DataBoundItem as RefillEligibilityResult;
                        break;
                    }
                }

                if (selectedBatch == null)
                {
                    MessageBox.Show(
                        "Please select exactly one batch to edit.",
                        "No Selection",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                if (selectedBatch.Status != "Active")
                {
                    MessageBox.Show(
                        "Can only edit Active batches.",
                        "Editing Blocked",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                var batchDetails = await _refillService.GetBatchByIdAsync(selectedBatch.BatchId);
                if (batchDetails == null)
                {
                    MessageBox.Show("Batch not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using (var dialog = new EditBatchDialog(batchDetails))
                {
                    var result = dialog.ShowDialog();

                    if (result == DialogResult.OK && dialog.SaveClicked)
                    {
                        try
                        {
                            btnEditBatch.Enabled = false;
                            btnRefresh.Enabled = false;

                            // Save vendor if changed
                            if (dialog.VendorChanged)
                            {
                                await _refillService.UpdateBatchVendorAsync(batchDetails.BatchId, dialog.SelectedVendorId);
                                Logger.LogInfo($"VendorCartridgeRefillPage: Updated vendor for batch {batchDetails.BatchId} to vendor {dialog.SelectedVendorId}");
                            }

                            await LoadDataAsync();

                            MessageBox.Show(
                                $"Batch updated successfully!\n\n" +
                                $"Batch ID: {batchDetails.BatchId}\n" +
                                (dialog.VendorChanged ? $"Vendor ID: {dialog.SelectedVendorId}" : "No changes."),
                                "Batch Updated",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"Error updating batch: {ex.Message}",
                                "Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                            Logger.LogError($"VendorCartridgeRefillPage.BtnEditBatch_Click failed for batch {batchDetails.BatchId}", ex);
                        }
                        finally
                        {
                            btnRefresh.Enabled = true;
                            UpdateBatchActionButtonStates();
                        }
                    }
                    else if (dialog.NeedsRefresh)
                    {
                        // Cartridges were transferred — reload the grid even if no vendor change was saved
                        await LoadDataAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unexpected error: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Logger.LogError("VendorCartridgeRefillPage.BtnEditBatch_Click failed", ex);
            }
        }

        /// <summary>
        /// Formats cells based on status and values
        /// </summary>
        private void DgvBatches_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvBatches.Rows.Count)
                return;

            var batch = dgvBatches.Rows[e.RowIndex].DataBoundItem as RefillEligibilityResult;
            if (batch == null)
                return;

            var columnName = dgvBatches.Columns[e.ColumnIndex].DataPropertyName;

            // Status column formatting
            if (columnName == "Status" && e.Value != null)
            {
                var status = e.Value.ToString();
                switch (status)
                {
                    case "Active":
                        e.CellStyle.ForeColor = Color.FromArgb(52, 152, 219); // Blue
                        break;
                    case "SentForRefill":
                        e.CellStyle.ForeColor = Color.FromArgb(230, 126, 34); // Orange
                        break;
                    case "Closed":
                        e.CellStyle.ForeColor = Color.FromArgb(149, 165, 166); // Gray
                        break;
                }
            }
        }

        /// <summary>
        /// Disable row selection if Status IN ('SentForRefill', 'Closed')
        /// </summary>
        private void DgvBatches_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvBatches.Rows.Count)
                return;

            var batch = dgvBatches.Rows[e.RowIndex].DataBoundItem as RefillEligibilityResult;
            if (batch == null)
                return;

            // Disable selection for SentForRefill and Closed
            if (batch.Status == "SentForRefill" || batch.Status == "Closed")
            {
                dgvBatches.Rows[e.RowIndex].DefaultCellStyle.SelectionBackColor = dgvBatches.Rows[e.RowIndex].DefaultCellStyle.BackColor;
                dgvBatches.Rows[e.RowIndex].DefaultCellStyle.SelectionForeColor = dgvBatches.Rows[e.RowIndex].DefaultCellStyle.ForeColor;
            }
        }

        /// <summary>
        /// Handles button clicks in the DataGridView (Context-sensitive action buttons)
        /// </summary>
        private async void DgvBatches_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // Check if the click is on the button column
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            if (dgvBatches.Columns[e.ColumnIndex].Name != "btnAction")
                return;

            var batch = dgvBatches.Rows[e.RowIndex].DataBoundItem as RefillEligibilityResult;
            if (batch == null)
                return;

            // Route to appropriate handler based on batch status
            switch (batch.Status)
            {
                case "Active":
                    await HandleSendToVendorFromGridAsync(batch);
                    break;

                case "SentForRefill":
                    await HandleCloseBatchAsync(batch);
                    break;

                default:
                    return;
            }
        }

        /// <summary>
        /// Handles inline "Send to Vendor" button click (Active batch row).
        /// Confirmation dialog → SendBatchForRefillAsync → status becomes SentForRefill.
        /// </summary>
        private async Task HandleSendToVendorFromGridAsync(RefillEligibilityResult batch)
        {
            if (!batch.IsRefillable)
            {
                MessageBox.Show(
                    $"This cartridge model is marked as non-refillable and cannot be sent to a vendor for refill.\n\n" +
                    $"Model: {batch.CartridgeModel}\n\n" +
                    $"Non-refillable cartridges must be disposed of or sold internally by IT.",
                    "Non-Refillable Model",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var confirmResult = MessageBox.Show(
                $"Send this batch to {batch.VendorName} for refill?\n\n" +
                $"Batch ID : {batch.BatchId}\n" +
                $"Vendor   : {batch.VendorName}\n" +
                $"Model    : {batch.CartridgeModel}\n" +
                $"Returned : {batch.ReturnedQty} cartridge(s)\n\n" +
                $"The batch status will change to 'SentForRefill'.\n" +
                $"Proceed?",
                "Confirm Send to Vendor",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
                return;

            try
            {
                btnRefresh.Enabled = false;

                var batchDetails = await _refillService.GetBatchByIdAsync(batch.BatchId);
                if (batchDetails == null)
                {
                    MessageBox.Show("Batch not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                await _refillService.SendBatchForRefillAsync(
                    batch.BatchId,
                    batchDetails.VendorId,
                    batch.ReturnedQty,
                    AppSession.CurrentUserId,
                    $"Sent {batch.ReturnedQty} empty cartridge(s) to {batch.VendorName} for refill");

                MessageBox.Show(
                    $"Batch {batch.BatchId} has been sent to {batch.VendorName} for refill.\n\n" +
                    $"Quantity: {batch.ReturnedQty} cartridge(s)\n" +
                    $"Status: SentForRefill",
                    "Sent Successfully",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending batch to vendor: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("VendorCartridgeRefillPage.HandleSendToVendorFromGridAsync failed", ex);
            }
            finally
            {
                btnRefresh.Enabled = true;
            }
        }

        /// <summary>
        /// Handles "Mark For Return" button click
        /// Changes batch status to ForReturn (no inventory changes)
        /// </summary>
        private async Task HandleMarkForReturnAsync(RefillEligibilityResult batch)
        {
            // Confirmation dialog
            var confirmResult = MessageBox.Show(
                $"Mark this batch as ready to be sent to the vendor?\n\n" +
                $"Batch ID: {batch.BatchId}\n" +
                $"Vendor: {batch.VendorName}\n" +
                $"Model: {batch.CartridgeModel}\n" +
                $"Returned Qty: {batch.ReturnedQty}\n\n" +
                $"⚠ Inventory will NOT be affected.\n" +
                $"This marks the batch as ready for physical return to vendor.",
                "Confirm Mark For Return",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
                return;

            try
            {
                btnRefresh.Enabled = false;

                // Call backend service
                await _refillService.MarkBatchForReturnAsync(batch.BatchId, AppSession.CurrentUserId);

                MessageBox.Show(
                    $"Batch marked for return successfully!\n\n" +
                    $"Batch ID: {batch.BatchId}\n" +
                    $"Status: ForReturn\n\n" +
                    $"The batch is now ready to be physically sent to {batch.VendorName}.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Refresh grid
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error marking batch for return: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("VendorCartridgeRefillPage.HandleMarkForReturnAsync failed", ex);
            }
            finally
            {
                btnRefresh.Enabled = true;
            }
        }

        private async Task HandleCloseBatchAsync(RefillEligibilityResult batch)
        {
            var confirmResult = MessageBox.Show(
                $"⚠ CLOSE BATCH — THIS CANNOT BE UNDONE\n\n" +
                $"You are closing this refill batch as a completed audit record.\n" +
                $"The empty cartridges assigned to it will be marked as Closed.\n\n" +
                $"Batch ID : {batch.BatchId}\n" +
                $"Vendor   : {batch.VendorName}\n" +
                $"Model    : {batch.CartridgeModel}\n\n" +
                $"To add refilled stock back to inventory, use Batch Add Item\n" +
                $"and select Origin = Refilled.\n\n" +
                $"Proceed?",
                "Close Batch",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirmResult != DialogResult.Yes)
                return;

            try
            {
                btnRefresh.Enabled = false;

                await _refillService.CloseRefillBatchAsync(batch.BatchId, AppSession.CurrentUserId);

                MessageBox.Show(
                    $"Batch #{batch.BatchId} has been closed.\n\n" +
                    $"All assigned empty cartridges have been marked as Closed.",
                    "Batch Closed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error closing batch: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("VendorCartridgeRefillPage.HandleCloseBatchAsync failed", ex);
            }
            finally
            {
                btnRefresh.Enabled = true;
            }
        }

        /// <summary>
        /// Opens a dialog to create a new multi-model refill batch.
        ///
        /// User picks ONE refill vendor and ONE OR MORE cartridge models.
        /// A single VendorCartridgeBatch is created with one VendorCartridgeBatchLine per model,
        /// matching the same batch structure used by the Manual Batch Assignment wizard.
        ///
        /// WHY this is a deliberate button action and not triggered automatically:
        ///   Batch creation is a procurement decision — it selects a refill vendor and
        ///   formally starts a vendor engagement.  The system must never create a batch
        ///   implicitly (e.g. on cartridge return).  This button is the sole entry-point
        ///   for batch creation in the UI.
        /// </summary>
        private async void BtnCreateBatch_Click(object sender, EventArgs e)
        {
            // ── Selection dialog: vendor + models + remarks ───────────────────────
            int    selectedVendorId   = 0;
            string selectedVendorName = null;
            string remarks            = null;
            var    selectedModelIds   = new List<int>();
            var    selectedModelNames = new List<string>();

            var vendorIds   = new List<int>();
            var vendorNames = new List<string>();
            var modelIds    = new List<int>();
            var modelNames  = new List<string>();

            using (var selectForm = new Form())
            {
                selectForm.Text            = "New Refill Batch — Select Vendor & Models";
                selectForm.Size            = new Size(580, 520);
                selectForm.StartPosition   = FormStartPosition.CenterParent;
                selectForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                selectForm.MaximizeBox     = false;
                selectForm.MinimizeBox     = false;

                var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 15, 24, 10) };
                int x = 16;
                int y = 10;

                panel.Controls.Add(new Label
                {
                    Text      = "Refill Vendor:",
                    Location  = new Point(x, y),
                    AutoSize  = true,
                    BackColor = Color.Transparent,
                    Font      = new Font("Segoe UI", 9F, FontStyle.Bold)
                });
                y += 26;

                var cmbVendor = new ComboBox
                {
                    Location      = new Point(x, y),
                    Width         = 510,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                panel.Controls.Add(cmbVendor);
                y += 44;

                panel.Controls.Add(new Label
                {
                    Text      = "Cartridge Models (check all that apply):",
                    Location  = new Point(x, y),
                    AutoSize  = true,
                    BackColor = Color.Transparent,
                    Font      = new Font("Segoe UI", 9F, FontStyle.Bold)
                });
                y += 26;

                var clbModels = new CheckedListBox
                {
                    Location     = new Point(x, y),
                    Size         = new Size(510, 140),
                    CheckOnClick = true
                };
                panel.Controls.Add(clbModels);
                y += 160;

                panel.Controls.Add(new Label
                {
                    Text      = "Remarks (optional):",
                    Location  = new Point(x, y),
                    AutoSize  = true,
                    BackColor = Color.Transparent,
                    Font      = new Font("Segoe UI", 9F, FontStyle.Bold)
                });
                y += 26;

                var txtRemarks = new TextBox
                {
                    Location   = new Point(x, y),
                    Width      = 510,
                    Height     = 50,
                    Multiline  = true,
                    ScrollBars = ScrollBars.Vertical
                };
                panel.Controls.Add(txtRemarks);
                y += 72;

                var btnCreate = new Button
                {
                    Text      = "Create Batch",
                    Location  = new Point(x, y),
                    Width     = 130,
                    Height    = 32,
                    BackColor = Color.FromArgb(39, 174, 96),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                    Cursor    = Cursors.Hand
                };
                btnCreate.FlatAppearance.BorderSize = 0;
                btnCreate.Click += (s2, e2) =>
                {
                    if (cmbVendor.SelectedIndex < 0)
                    {
                        MessageBox.Show("Please select a vendor.", "Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (clbModels.CheckedIndices.Count == 0)
                    {
                        MessageBox.Show("Please select at least one cartridge model.", "Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    selectForm.DialogResult = DialogResult.OK;
                    selectForm.Close();
                };

                var btnCancelSelect = new Button
                {
                    Text         = "Cancel",
                    Location     = new Point(x + 140, y),
                    Width        = 80,
                    Height       = 32,
                    BackColor    = Color.FromArgb(149, 165, 166),
                    ForeColor    = Color.White,
                    FlatStyle    = FlatStyle.Flat,
                    Cursor       = Cursors.Hand,
                    DialogResult = DialogResult.Cancel
                };
                btnCancelSelect.FlatAppearance.BorderSize = 0;

                panel.Controls.Add(btnCreate);
                panel.Controls.Add(btnCancelSelect);
                selectForm.Controls.Add(panel);
                selectForm.CancelButton = btnCancelSelect;

                // Load vendors and models from DB
                try
                {
                    using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                    {
                        con.Open();

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
                                vendorIds.Add(reader.GetInt32(0));
                                vendorNames.Add(reader.GetString(1));
                                cmbVendor.Items.Add(reader.GetString(1));
                            }
                        }

                        const string modelSql = @"
                            SELECT CartridgeModelId, ModelNumber
                            FROM dbo.CartridgeModel
                            WHERE IsActive = 1
                            ORDER BY ModelNumber";

                        using (var cmd = new SqlCommand(modelSql, con))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                modelIds.Add(reader.GetInt32(0));
                                modelNames.Add(reader.GetString(1));
                                clbModels.Items.Add(reader.GetString(1));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading vendors/models: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Logger.LogError("VendorCartridgeRefillPage.BtnCreateBatch_Click: failed to load vendors/models", ex);
                    return;
                }

                if (vendorIds.Count == 0)
                {
                    MessageBox.Show("No active refill vendors found.", "No Vendors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (modelIds.Count == 0)
                {
                    MessageBox.Show("No active cartridge models found.", "No Models", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (selectForm.ShowDialog() != DialogResult.OK)
                    return;

                int vi = cmbVendor.SelectedIndex;
                if (vi < 0) return;

                selectedVendorId   = vendorIds[vi];
                selectedVendorName = vendorNames[vi];
                remarks            = txtRemarks.Text.Trim();

                foreach (int ci in clbModels.CheckedIndices)
                {
                    selectedModelIds.Add(modelIds[ci]);
                    selectedModelNames.Add(modelNames[ci]);
                }
            }

            if (selectedModelIds.Count == 0) return;

            // ── Create multi-model batch ──────────────────────────────────────────
            // Build model groups with TotalQty = 0 — returns are assigned separately
            // via the "Assign Returns" button after the batch is created.
            var modelGroups = selectedModelIds
                .Select((id, i) => new { CartridgeModelId = id, CartridgeModel = selectedModelNames[i], TotalQty = 0 })
                .ToList();

            string modelSummary = string.Join(", ", selectedModelNames);

            var confirm = MessageBox.Show(
                $"Create a new refill batch?\n\n" +
                $"Vendor : {selectedVendorName}\n" +
                $"Models : {modelSummary}\n\n" +
                "Returns can be linked via 'Assign Returns' after the batch is created.\n\n" +
                "Proceed?",
                "Confirm Batch Creation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                btnCreateBatch.Enabled = false;
                btnRefresh.Enabled     = false;

                int newBatchId = await _refillService.CreateMultiModelRefillBatchAsync(
                    selectedVendorId,
                    modelGroups,
                    AppSession.CurrentUserId,
                    string.IsNullOrEmpty(remarks) ? null : remarks);

                Logger.LogInfo(
                    $"VendorCartridgeRefillPage: Created multi-model batch {newBatchId} — " +
                    $"vendor={selectedVendorId}, models={selectedModelIds.Count}");

                MessageBox.Show(
                    $"Refill batch created successfully!\n\n" +
                    $"Batch ID : {newBatchId}\n" +
                    $"Vendor   : {selectedVendorName}\n" +
                    $"Models   : {modelSummary}\n\n" +
                    "Use 'Assign Returns' to link returned cartridges to this batch.",
                    "Batch Created",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                await LoadDataAsync();
            }
            catch (InvalidOperationException ioe)
            {
                MessageBox.Show(ioe.Message, "Cannot Create Batch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Logger.LogError("VendorCartridgeRefillPage.BtnCreateBatch_Click: batch already exists", ioe);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating batch: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("VendorCartridgeRefillPage.BtnCreateBatch_Click failed", ex);
            }
            finally
            {
                btnCreateBatch.Enabled = true;
                btnRefresh.Enabled     = true;
            }
        }

        /// <summary>
        /// Opens the two-step manual batch-assignment wizard.
        ///
        /// Step 1 — user selects which unassigned returned cartridges to process.
        /// Step 2 — user picks an existing Active batch or creates a new one.
        ///
        /// The wizard calls AssignReturnsToBatchAsync (and optionally CreateRefillBatchAsync)
        /// on the service; this page only refreshes the grid on success.
        /// </summary>
        private async void BtnAssignReturns_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dialog = new ManualBatchAssignmentDialog())
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        // Refresh to reflect new ReturnedQty on affected batch(es)
                        await LoadDataAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("VendorCartridgeRefillPage.BtnAssignReturns_Click failed", ex);
            }
        }

        private async Task BtnDeleteBatch_ClickAsync()
        {
            // Find selected batch
            RefillEligibilityResult selectedBatch = null;
            foreach (DataGridViewRow row in dgvBatches.Rows)
            {
                if (row.Cells["chkSelectBatch"].Value != null && (bool)row.Cells["chkSelectBatch"].Value)
                {
                    selectedBatch = row.DataBoundItem as RefillEligibilityResult;
                    break;
                }
            }

            if (selectedBatch == null) return;

            // Block deletion if already sent to vendor
            if (selectedBatch.Status == "SentForRefill")
            {
                MessageBox.Show(
                    $"Batch #{selectedBatch.BatchId} has already been sent to the vendor and cannot be deleted.\n\n" +
                    "Only Active, Eligible, or Completed batches can be deleted.",
                    "Cannot Delete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Build confirmation message — stronger warning when batch is still Active
            string confirmText;
            if (selectedBatch.Status == "Active")
            {
                confirmText =
                    $"⚠  Batch #{selectedBatch.BatchId} is still Active.\n\n" +
                    $"  • Model:   {selectedBatch.CartridgeModel}\n" +
                    $"  • Vendor:  {selectedBatch.VendorName}\n" +
                    $"  • Qty:     {selectedBatch.ReturnedQty} assigned cartridge(s)\n\n" +
                    "Deleting this batch will unassign all linked cartridges and return them to Pending status.\n\n" +
                    "Are you sure you want to delete this batch?";
            }
            else
            {
                confirmText =
                    $"Delete Batch #{selectedBatch.BatchId}? (Status: {selectedBatch.Status})\n\n" +
                    $"  • Model:  {selectedBatch.CartridgeModel}\n" +
                    $"  • Vendor: {selectedBatch.VendorName}\n\n" +
                    "This action cannot be undone.";
            }

            if (MessageBox.Show(confirmText, $"Confirm Delete Batch #{selectedBatch.BatchId}",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;

            btnDeleteBatch.Enabled = false;
            btnRefresh.Enabled     = false;
            btnEditBatch.Enabled   = false;
            try
            {
                await _refillService.DeleteRefillBatchAsync(selectedBatch.BatchId, userId);

                MessageBox.Show(
                    $"Batch #{selectedBatch.BatchId} deleted successfully.",
                    "Deleted",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting batch: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError($"VendorCartridgeRefillPage.BtnDeleteBatch_ClickAsync(batchId={selectedBatch.BatchId}) failed", ex);
            }
            finally
            {
                btnRefresh.Enabled = true;
                // Selection will have cleared after reload — UpdateBatchActionButtonStates handles re-enabling
            }
        }


        /// <summary>
        /// Sends selected batch to vendor for refill
        /// Only enabled when exactly one Active batch is selected
        /// </summary>
        private async void BtnSendToVendor_Click(object sender, EventArgs e)
        {
            if (dgvBatches.SelectedRows.Count != 1)
                return;

            var batch = dgvBatches.SelectedRows[0].DataBoundItem as RefillEligibilityResult;
            if (batch == null || batch.Status != "Active")
                return;

            // Confirmation dialog
            var confirmResult = MessageBox.Show(
                $"Send this batch to vendor for refill?\n\n" +
                $"Batch ID: {batch.BatchId}\n" +
                $"Vendor: {batch.VendorName}\n" +
                $"Model: {batch.CartridgeModel}\n" +
                $"Quantity: {batch.ReturnedQty} cartridges\n\n" +
                $"This will mark the batch as 'SentForRefill'.",
                "Confirm Send to Vendor",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
                return;

            try
            {
                btnSendToVendor.Enabled = false;
                btnRefresh.Enabled = false;

                // Get full batch details
                var batchDetails = await _refillService.GetBatchByIdAsync(batch.BatchId);
                if (batchDetails == null)
                {
                    MessageBox.Show("Batch not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Send to vendor via service
                await _refillService.SendBatchForRefillAsync(
                    batch.BatchId,
                    batchDetails.VendorId,
                    batch.ReturnedQty,
                    AppSession.CurrentUserId,
                    $"Sent {batch.ReturnedQty} empty cartridges to {batch.VendorName} for refill");

                MessageBox.Show(
                    $"Successfully sent batch {batch.BatchId} to {batch.VendorName} for refill.\n\n" +
                    $"Quantity: {batch.ReturnedQty} cartridges",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Reload batches
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending batch to vendor: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("VendorCartridgeRefillPage.BtnSendToVendor_Click failed", ex);
            }
            finally
            {
                btnSendToVendor.Enabled = true;
                btnRefresh.Enabled = true;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // AUDIT TRAIL — RIGHT-CLICK CONTEXT MENU (Transfer / Remove)
        // ─────────────────────────────────────────────────────────────────────

        private void DgvAuditTrail_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            var hit = dgvAuditTrail.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0) return;

            // Select the right-clicked row
            dgvAuditTrail.ClearSelection();
            dgvAuditTrail.Rows[hit.RowIndex].Selected = true;
            dgvAuditTrail.CurrentCell = dgvAuditTrail.Rows[hit.RowIndex].Cells[0];

            // Only allow operations when the selected batch is Active
            var selectedBatch = dgvBatches.SelectedRows.Count == 1
                ? dgvBatches.SelectedRows[0].DataBoundItem as RefillEligibilityResult
                : null;

            bool batchIsActive = selectedBatch?.Status == "Active";
            _menuTransfer.Enabled = batchIsActive;
            _menuRemove.Enabled   = batchIsActive;

            _auditContextMenu.Show(dgvAuditTrail, e.Location);
        }

        private async Task HandleRemoveFromBatchAsync()
        {
            if (dgvAuditTrail.SelectedRows.Count != 1) return;
            var row = dgvAuditTrail.SelectedRows[0].DataBoundItem as VendorBatchAuditTrailDto;
            if (row == null) return;

            var selectedBatch = dgvBatches.SelectedRows.Count == 1
                ? dgvBatches.SelectedRows[0].DataBoundItem as RefillEligibilityResult
                : null;
            if (selectedBatch == null) return;

            string reqInfo   = row.RequestId.HasValue ? $"Request #{row.RequestId}" : $"Empty Cartridge #{row.EmptyCartridgeId}";
            string modelInfo = $"{row.CartridgeModel}  ×  {row.ReturnedQty} unit(s)";

            var confirm = MessageBox.Show(
                $"Remove from Batch #{selectedBatch.BatchId}?\n\n" +
                $"  {reqInfo}\n  {modelInfo}\n\n" +
                "The cartridge(s) will be returned to the unassigned pool\n" +
                "and can be re-assigned via Assign Returns to Refill Batch.",
                "Remove from Batch",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes) return;

            try
            {
                int qty = await _refillService.RemoveFromBatchAsync(
                    selectedBatch.BatchId,
                    row.RequestId,
                    row.CartridgeModelId,
                    row.EmptyCartridgeId,
                    AppSession.CurrentUserId);

                MessageBox.Show(
                    $"Removed {qty} unit(s) from Batch #{selectedBatch.BatchId}.\n" +
                    "They are now available in Assign Returns to Refill Batch.",
                    "Removed Successfully",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                await LoadDataAsync();
                await LoadAuditTrailAsync(selectedBatch.BatchId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing from batch: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task HandleTransferToBatchAsync()
        {
            if (dgvAuditTrail.SelectedRows.Count != 1) return;
            var row = dgvAuditTrail.SelectedRows[0].DataBoundItem as VendorBatchAuditTrailDto;
            if (row == null) return;

            var selectedBatch = dgvBatches.SelectedRows.Count == 1
                ? dgvBatches.SelectedRows[0].DataBoundItem as RefillEligibilityResult
                : null;
            if (selectedBatch == null) return;

            // Load active batches excluding the current one
            List<VendorCartridgeBatchDto> activeBatches;
            try
            {
                var all = await _refillService.GetAllActiveBatchesAsync();
                activeBatches = all.Where(b => b.BatchId != selectedBatch.BatchId).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading batches: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (activeBatches.Count == 0)
            {
                MessageBox.Show(
                    "No other Active refill batches are available to transfer to.\n" +
                    "Create a new batch first via the '+ Create Batch' button.",
                    "No Target Batches",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var targetBatch = ShowTransferBatchDialog(activeBatches, row, selectedBatch.BatchId);
            if (targetBatch == null) return;

            try
            {
                int qty = await _refillService.TransferToBatchAsync(
                    selectedBatch.BatchId,
                    targetBatch.BatchId,
                    row.RequestId,
                    row.CartridgeModelId,
                    row.EmptyCartridgeId,
                    AppSession.CurrentUserId);

                MessageBox.Show(
                    $"Transferred {qty} unit(s)\n" +
                    $"from Batch #{selectedBatch.BatchId}  \u2192  Batch #{targetBatch.BatchId}  ({targetBatch.VendorName}).",
                    "Transferred Successfully",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                await LoadDataAsync();
                await LoadAuditTrailAsync(selectedBatch.BatchId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error transferring: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private VendorCartridgeBatchDto ShowTransferBatchDialog(
            List<VendorCartridgeBatchDto> batches,
            VendorBatchAuditTrailDto selectedRow,
            int currentBatchId)
        {
            using (var form = new Form())
            {
                form.Text            = "Transfer to Another Batch";
                form.Size            = new Size(500, 230);
                form.StartPosition   = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox     = false;
                form.MinimizeBox     = false;
                form.BackColor       = Color.White;

                var lblInfo = new Label
                {
                    Text     = $"Transferring:  {selectedRow.CartridgeModel}  \u00d7  {selectedRow.ReturnedQty} unit(s)\n" +
                               $"From Batch #{currentBatchId}  \u2192  select target batch:",
                    Location = new Point(16, 16),
                    Size     = new Size(460, 40),
                    Font     = new Font("Segoe UI", 9.5F)
                };

                var combo = new ComboBox
                {
                    Location      = new Point(16, 70),
                    Size          = new Size(460, 28),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font          = new Font("Segoe UI", 9.5F)
                };

                foreach (var b in batches)
                    combo.Items.Add(new BatchComboItem(b));

                if (combo.Items.Count > 0)
                    combo.SelectedIndex = 0;

                var btnOk = new Button
                {
                    Text        = "Transfer",
                    Location    = new Point(264, 158),
                    Size        = new Size(100, 32),
                    DialogResult = DialogResult.OK,
                    Font        = new Font("Segoe UI", 9F),
                    BackColor   = Color.FromArgb(46, 125, 50),
                    ForeColor   = Color.White,
                    FlatStyle   = FlatStyle.Flat
                };
                btnOk.FlatAppearance.BorderSize = 0;

                var btnCancel = new Button
                {
                    Text         = "Cancel",
                    Location     = new Point(372, 158),
                    Size         = new Size(100, 32),
                    DialogResult = DialogResult.Cancel,
                    Font         = new Font("Segoe UI", 9F)
                };

                form.Controls.AddRange(new Control[] { lblInfo, combo, btnOk, btnCancel });
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                return form.ShowDialog(this) == DialogResult.OK
                    ? (combo.SelectedItem as BatchComboItem)?.Batch
                    : null;
            }
        }

        private sealed class BatchComboItem
        {
            public VendorCartridgeBatchDto Batch { get; }
            public BatchComboItem(VendorCartridgeBatchDto batch) => Batch = batch;
            public override string ToString() =>
                $"Batch #{Batch.BatchId}  —  {Batch.VendorName}  |  {Batch.CartridgeModel}  ({Batch.ReturnedQty} units)";
        }
    }
}
