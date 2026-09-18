using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Forms.CartridgeManagement
{
    // IMPORTANT:
    // IT selects ONLY the cartridges being ISSUED (FULL).
    // Returned EMPTY cartridges are auto-recorded based on request quantity.
    // This matches real-world exchange behavior and prevents inventory mistakes.
    // Serial numbers for empties are not required at fulfillment time.
    //
    // Cartridge Management strictly follows the Request → Set lifecycle
    // used by ViewSetDetailPage.cs (Upgrade Lifecycle).
    // Requests represent intent; fulfillment records physical cartridge movements.
    // Each cartridge exchange is recorded as individual CartridgeMovement rows.
    //
    // SUBMISSION-DRIVEN SESSION BLOCKS:
    // Portal submissions create one dbo.Request row per model (atomic design).
    // Each SubmissionSessionId renders as a persistent session block with:
    // - Label: Shows "Multi Model" or "Single Model" (never changes after fulfillment)
    // - Color: Blue = Multi Model, Peach = Single Model, Green overlay = Fully Fulfilled
    // - Table: All request rows with ReqId always visible
    // During fulfillment, related rows are linked to a single shared Set via
    // EnsureSharedSetForPortalCartridgeGroup using SubmissionSessionId.

    /// <summary>
    /// IT Fulfillment UI for cartridge exchange.
    /// Submission-driven session blocks group requests by SubmissionSessionId.
    /// Each block persists through fulfillment with immutable session type labels.
    /// ReqId always visible, no row merging, no arbitrary Item Name fallback.
    /// </summary>
    public partial class CartridgeManagementForm : Form
    {
        private readonly CartridgeManagementRepository _repository;
        private readonly RequestRepository _requestRepository;

        // UI Components - Left Panel (Session Blocks)
        private Panel pnlLeft;
        private Label lblPendingTitle;
        private FlowLayoutPanel flowSessionBlocks;  // Container for session blocks
        private Button btnOpenRequest;
        private Button btnRefresh;

        // UI Components - Right Panel (Exchange Panel)
        private Panel pnlRight;
        private Label lblExchangeTitle;
        private Panel pnlRequestSummary;
        private Label lblReqId, lblRequester, lblModel, lblQuantity, lblCondition, lblCompany, lblBranch, lblDepartment;
        private TextBox txtReqId, txtRequester, txtModel, txtQuantity, txtCondition, txtGoodEmptyQty, txtDamagedEmptyQty, txtCompany, txtBranch, txtDepartment;
        private TextBox txtDistributionMethod, txtReceivedBy, txtAdditionalRemarks;

        // Exchange Section
        private Label lblReturnedTitle, lblIssuedTitle;
        private FlowLayoutPanel pnlReturnedCartridges;
        private FlowLayoutPanel pnlIssuedCartridges;
        private Button btnFulfill;
        private Button btnClear;
        private Button btnForceDelete;

        private NumericUpDown nudIssuedBrandNewQty;
        private NumericUpDown nudIssuedRefilledQty;
        private Label lblIssuedStock;
        private Label lblReturnedEmptyQty;
        private Label lblIssuedFullQty;
        private Label lblUnfulfilledQty;
        private Label lblFulfillmentStatus;
        private TextBox txtAutoRemarks;
        private int _availableBrandNewStock;
        private int _availableRefilledStock;

        // State
        private CartridgeRequestDto _selectedRequest;
        private List<CartridgeRequestDto> _allRequests;
        private Dictionary<string, RequestSessionGroup> _sessionGroups;
        private List<ComboBox> _issuedDropdowns = new List<ComboBox>();
        private bool _isLoadingData = false;
        private bool _sortAscending = true;
        // Incremented each time a new exchange build starts; builders bail out if superseded.
        private int _exchangeBuildVersion = 0;
        private Label _lblSortIndicator;

        // Pagination
        private int _currentPage = 1;
        private const int PageSize = 5;
        private Button _btnFirstPage, _btnPrevPage, _btnNextPage, _btnLastPage;
        private Label _lblPageInfo;
        private Panel _pnlLeftPagination;

        // Multi-model state
        private bool _isMultiModelMode = false;
        private Dictionary<int, MultiModelRowState> _multiModelState;
        private List<CartridgeRequestDto> _multiModelRequests;

        // Root layout component
        private SplitContainer splitMain;

        public CartridgeManagementForm()
        {
            _repository = new CartridgeManagementRepository();
            _requestRepository = new RequestRepository();

            InitializeComponent();
            BuildUI();

            // Add load handler to set initial splitter distance
            this.Load += CartridgeManagementForm_Load;

            // Add resize handler with debouncing
            this.Resize += CartridgeManagementForm_Resize;

            // Dispose auto-refresh timer when the form closes or its handle is destroyed.
            // HandleDestroyed is needed because FormClosed does not fire reliably for embedded
            // (non-top-level) child forms — the handle can be torn down without FormClosed firing,
            // leaving the timer running and causing BeginInvoke to throw.
            this.FormClosed += (s, e) =>
            {
                _autoRefreshTimer?.Stop();
                _autoRefreshTimer?.Dispose();
                _autoRefreshTimer = null;
            };
            this.HandleDestroyed += (s, e) =>
            {
                _autoRefreshTimer?.Stop();
                _autoRefreshTimer?.Dispose();
                _autoRefreshTimer = null;
            };
        }

        private void CartridgeManagementForm_Load(object sender, EventArgs e)
        {
            // Calculate SplitterDistance as 65% of form width (35% for right panel)
            if (splitMain != null)
            {
                SplitContainerUtil.SetSafeSplitterDistance(splitMain, (int)(this.ClientSize.Width * 0.65));
            }

            LoadPendingCartridgeRequests();
            StartAutoRefreshTimer();
        }

        private Timer _resizeTimer;
        private Timer _autoRefreshTimer;
        private bool _fulfillmentInProgress;
        private Label _lblRefreshCountdown;
        private int _autoRefreshSecondsLeft = 30;

        private void CartridgeManagementForm_Resize(object sender, EventArgs e)
        {
            // Debounce resize events to prevent excessive processing
            if (_resizeTimer != null)
            {
                _resizeTimer.Stop();
                _resizeTimer.Dispose();
            }

            _resizeTimer = new Timer { Interval = 100 }; // 100ms debounce
            _resizeTimer.Tick += (s, args) =>
            {
                _resizeTimer.Stop();
                PerformResize();
            };
            _resizeTimer.Start();
        }

        private void PerformResize()
        {
            // Resize session blocks when window is resized, but preserve NumericUpDown functionality
            if (flowSessionBlocks != null && flowSessionBlocks.Controls.Count > 0)
            {
                foreach (Control control in flowSessionBlocks.Controls)
                {
                    if (control is Panel blockPanel)
                    {
                        // Update block width only
                        blockPanel.Width = flowSessionBlocks.Width - 40;
                        
                        // Update label width
                        if (blockPanel.Controls.Count > 0 && blockPanel.Controls[0] is Label lblSession)
                        {
                            lblSession.Width = blockPanel.Width - 2;
                        }
                        
                        // Update DataGridView width
                        if (blockPanel.Controls.Count > 1 && blockPanel.Controls[1] is DataGridView dgvRequests)
                        {
                            dgvRequests.Width = blockPanel.Width - 4;
                        }
                    }
                }
            }
            
            // IMPORTANT: Do NOT touch the right panel controls during resize
            // This preserves the NumericUpDown control and its event handlers
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1400, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.Text = "Cartridge Management - IT Fulfillment";
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.MinimumSize = new Size(1200, 700);
            this.ResumeLayout(false);
        }

        private void BuildUI()
        {
            // Split container for left/right panels - responsive layout
            splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel2,  // Lock right panel by ratio
                IsSplitterFixed = true,           // Prevent manual resizing
                BackColor = Color.FromArgb(220, 225, 230),
                SplitterWidth = 6
            };

            // === LEFT PANEL - Pending Requests (Single Grid with Visual Grouping) ===
            pnlLeft = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(15)
            };

            lblPendingTitle = new Label
            {
                Text = "📋 Pending Cartridge Requests (Grouped by Submission)",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 152, 219),
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 4)
            };

            _lblSortIndicator = new Label
            {
                Text = "↑ Asc — Oldest First (Priority Order)",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(39, 174, 96),
                Cursor = Cursors.Hand,
                Dock = DockStyle.Top,
                Padding = new Padding(2, 0, 0, 8)
            };
            _lblSortIndicator.Click += (s, e) =>
            {
                _sortAscending = !_sortAscending;
                UpdateSortIndicator();
                RenderSessionBlocks();
            };

            // FlowLayoutPanel to hold session blocks
            flowSessionBlocks = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoScroll = true,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(10),
                WrapContents = false,
                AutoSize = false
            };
            // Session blocks will be dynamically created in LoadPendingCartridgeRequests

            // Button panel for left side
            var pnlLeftButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = Color.Transparent
            };

            btnOpenRequest = CreateButton("📂 Open Request", Color.FromArgb(52, 152, 219));
            btnOpenRequest.Click += BtnOpenRequest_Click;

            btnRefresh = CreateButton("🔄 Refresh", Color.FromArgb(46, 204, 113));
            btnRefresh.Click += (s, e) => { LoadPendingCartridgeRequests(); ResetAutoRefreshTimer(); };

            _lblRefreshCountdown = new Label
            {
                Text = "⏱ 30s",
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.FromArgb(130, 130, 130),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(6, 10, 0, 0)
            };

            pnlLeftButtons.Controls.Add(btnOpenRequest);
            pnlLeftButtons.Controls.Add(btnRefresh);
            pnlLeftButtons.Controls.Add(_lblRefreshCountdown);

            // Pagination panel — sits just above the action buttons (same style as AccountPermissionsPage)
            _pnlLeftPagination = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                BackColor = Color.FromArgb(245, 247, 250)
            };

            _btnFirstPage = new System.Windows.Forms.Button { Text = "<<", Width = 45, Height = 25, Left = 0,   Top = 5 };
            _btnPrevPage  = new System.Windows.Forms.Button { Text = "<",  Width = 45, Height = 25, Left = 50,  Top = 5 };
            _lblPageInfo  = new Label { AutoSize = false, Width = 220, Left = 100, Top = 10, Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.MiddleLeft, Text = "Page 1 of 1" };
            _btnNextPage  = new System.Windows.Forms.Button { Text = ">",  Width = 45, Height = 25, Left = 326, Top = 5 };
            _btnLastPage  = new System.Windows.Forms.Button { Text = ">>", Width = 45, Height = 25, Left = 376, Top = 5 };

            _btnFirstPage.Click += (s, e) => { _currentPage = 1; RenderSessionBlocks(); };
            _btnPrevPage.Click  += (s, e) => { if (_currentPage > 1) { _currentPage--; RenderSessionBlocks(); } };
            _btnNextPage.Click  += (s, e) =>
            {
                int total = _sessionGroups != null ? (int)Math.Ceiling((double)_sessionGroups.Count / PageSize) : 1;
                if (_currentPage < total) { _currentPage++; RenderSessionBlocks(); }
            };
            _btnLastPage.Click  += (s, e) =>
            {
                if (_sessionGroups != null)
                    _currentPage = (int)Math.Ceiling((double)_sessionGroups.Count / PageSize);
                RenderSessionBlocks();
            };

            _pnlLeftPagination.Controls.AddRange(new Control[] { _btnFirstPage, _btnPrevPage, _lblPageInfo, _btnNextPage, _btnLastPage });

            pnlLeft.Controls.Add(flowSessionBlocks);
            // Bottom controls: first added = absolute bottom; second = above first
            pnlLeft.Controls.Add(pnlLeftButtons);
            pnlLeft.Controls.Add(_pnlLeftPagination);
            // DockStyle.Top controls stack in reverse-add order; _lblSortIndicator added before
            // lblPendingTitle so the title lands at the very top, indicator just below it.
            pnlLeft.Controls.Add(_lblSortIndicator);
            pnlLeft.Controls.Add(lblPendingTitle);

            // === RIGHT PANEL - Exchange Panel ===
            pnlRight = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(15),
                AutoScroll = true
            };

            // Use TableLayoutPanel for right panel structure - auto-sizes to allow pnlRight scrolling
            var rightPanelLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,  // Single column only
                RowCount = 5,     // Header, Summary, Step 3A, Step 3B, Buttons
                BackColor = Color.Transparent,
                Padding = new Padding(15)
            };
            rightPanelLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rightPanelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Header
            rightPanelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Request Info
            rightPanelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Step 3A - only takes needed height
            rightPanelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Step 3B - auto-size so content overflows for scroll
            rightPanelLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Buttons

            lblExchangeTitle = new Label
            {
                Text = "🔄 Cartridge Exchange",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(230, 126, 34),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 0, 0, 10)
            };

            // Request Summary Panel - responsive
            pnlRequestSummary = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                BackColor = Color.FromArgb(250, 251, 252),
                Padding = new Padding(15),
                Margin = new Padding(0, 0, 0, 10),
                MinimumSize = new Size(0, 120)
            };
            pnlRequestSummary.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(200, 210, 220), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, pnlRequestSummary.Width - 1, pnlRequestSummary.Height - 1);
                }
            };

            BuildRequestSummaryPanel();

            // Step 3A: Issued (Full) Cartridges - Compact auto-sized panel
            var pnlIssuedSection = BuildExchangeSection(
                "Step 3A: 📤 Issued (Full) Cartridges",
                Color.FromArgb(46, 204, 113),
                out lblIssuedTitle,
                out pnlIssuedCartridges,
                isCompact: true);  // Step 3A uses compact mode (auto-size, no collapse)

            // Step 3B: Returned (Empty) Cartridges - Fill remaining space
            var pnlReturnedSection = BuildExchangeSection(
                "Step 3B: 📥 Returned (Empty) Cartridges",
                Color.FromArgb(231, 76, 60),
                out lblReturnedTitle,
                out pnlReturnedCartridges,
                isCompact: false);  // Step 3B fills remaining space

            // Button panel for right side - responsive
            var pnlRightButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 15, 0, 0),
                BackColor = Color.Transparent,
                WrapContents = true
            };

            btnFulfill = CreateButton("✅ Fulfill Request", Color.FromArgb(46, 204, 113));
            btnFulfill.AutoSize = true;
            btnFulfill.MinimumSize = new Size(140, 38);
            btnFulfill.Click += BtnFulfill_Click;
            btnFulfill.Enabled = false;

            btnClear = CreateButton("🗑 Clear", Color.FromArgb(149, 165, 166));
            btnClear.AutoSize = true;
            btnClear.MinimumSize = new Size(100, 38);
            btnClear.Click += BtnClear_Click;

            btnForceDelete = CreateButton("🚨 Force Delete", Color.FromArgb(231, 76, 60));
            btnForceDelete.AutoSize = true;
            btnForceDelete.MinimumSize = new Size(140, 38);
            btnForceDelete.Click += BtnForceDelete_Click;
            btnForceDelete.Enabled = false;

            pnlRightButtons.Controls.Add(btnFulfill);
            pnlRightButtons.Controls.Add(btnClear);
            pnlRightButtons.Controls.Add(btnForceDelete);

            // Add all sections to right panel layout - vertical stack
            rightPanelLayout.Controls.Add(lblExchangeTitle, 0, 0);
            rightPanelLayout.Controls.Add(pnlRequestSummary, 0, 1);
            rightPanelLayout.Controls.Add(pnlIssuedSection, 0, 2);
            rightPanelLayout.Controls.Add(pnlReturnedSection, 0, 3);
            rightPanelLayout.Controls.Add(pnlRightButtons, 0, 4);

            // Add layout to right panel
            pnlRight.Controls.Add(rightPanelLayout);

            // Add panels to split container
            splitMain.Panel1.Controls.Add(pnlLeft);
            splitMain.Panel2.Controls.Add(pnlRight);

            this.Controls.Add(splitMain);
        }

        private void BuildRequestSummaryPanel()
        {
            var summaryLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,  // Strict 2-column layout
                RowCount = 9,
                BackColor = Color.Transparent,
                AutoSize = true
            };

            // Two equal-width columns for consistent alignment
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));  // Column 1
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));  // Column 2

            // Explicit row definitions
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Row 0: Requester
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Row 1: Model
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Row 2: Request # | Quantity
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Row 3: With Cartridge | Company
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Row 4: Branch | Department
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Row 5: Good | Damaged
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Row 6: Distribution Method
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Row 7: Received By
            summaryLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Row 8: Additional Remarks

            // Row 0: Requester (spans both columns)
            var pnlRequester = CreateSummaryFieldPanel("Requester:", out txtRequester);
            summaryLayout.Controls.Add(pnlRequester, 0, 0);
            summaryLayout.SetColumnSpan(pnlRequester, 2);

            // Row 1: Model (spans both columns)
            var pnlModel = CreateSummaryFieldPanel("Model:", out txtModel);
            summaryLayout.Controls.Add(pnlModel, 0, 1);
            summaryLayout.SetColumnSpan(pnlModel, 2);

            // Row 2: Request # (Column 1) | Quantity (Column 2)
            var pnlReqId = CreateSummaryFieldPanel("Request #:", out txtReqId);
            var pnlQuantity = CreateSummaryFieldPanel("Quantity:", out txtQuantity);
            summaryLayout.Controls.Add(pnlReqId, 0, 2);
            summaryLayout.Controls.Add(pnlQuantity, 1, 2);

            // Row 3: Condition (Column 1) | Company (Column 2)
            var pnlCondition = CreateSummaryFieldPanel("With Cartridge:", out txtCondition);
            var pnlCompany = CreateSummaryFieldPanel("Company:", out txtCompany);
            summaryLayout.Controls.Add(pnlCondition, 0, 3);
            summaryLayout.Controls.Add(pnlCompany, 1, 3);

            // Row 4: Branch (Column 1) | Department (Column 2)
            var pnlBranch = CreateSummaryFieldPanel("Branch:", out txtBranch);
            var pnlDepartment = CreateSummaryFieldPanel("Department:", out txtDepartment);
            summaryLayout.Controls.Add(pnlBranch, 0, 4);
            summaryLayout.Controls.Add(pnlDepartment, 1, 4);

            // Row 5: Good (Column 1) | Damaged (Column 2)
            var pnlGoodEmptyQty = CreateSummaryFieldPanel("Good:", out txtGoodEmptyQty);
            var pnlDamagedEmptyQty = CreateSummaryFieldPanel("Damaged:", out txtDamagedEmptyQty);
            summaryLayout.Controls.Add(pnlGoodEmptyQty, 0, 5);
            summaryLayout.Controls.Add(pnlDamagedEmptyQty, 1, 5);

            // Row 6: Distribution Method (spans both columns)
            var pnlDistributionMethod = CreateSummaryFieldPanel("Distribution Method:", out txtDistributionMethod);
            summaryLayout.Controls.Add(pnlDistributionMethod, 0, 6);
            summaryLayout.SetColumnSpan(pnlDistributionMethod, 2);

            // Row 7: Received By (spans both columns)
            var pnlReceivedBy = CreateSummaryFieldPanel("Received By:", out txtReceivedBy);
            summaryLayout.Controls.Add(pnlReceivedBy, 0, 7);
            summaryLayout.SetColumnSpan(pnlReceivedBy, 2);

            // Row 8: Additional Remarks (spans both columns)
            var pnlAdditionalRemarks = CreateSummaryFieldPanel("Additional Remarks:", out txtAdditionalRemarks);
            summaryLayout.Controls.Add(pnlAdditionalRemarks, 0, 8);
            summaryLayout.SetColumnSpan(pnlAdditionalRemarks, 2);

            pnlRequestSummary.Controls.Add(summaryLayout);
        }

        private Panel CreateSummaryFieldPanel(string labelText, out TextBox textBox)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(2),
                Padding = new Padding(0)
            };

            var innerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            innerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            innerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Label
            innerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // TextBox

            var label = new Label
            {
                Text = labelText,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 90, 100),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(2, 2, 2, 2)
            };

            textBox = new TextBox
            {
                ReadOnly = true,
                BackColor = Color.FromArgb(240, 242, 245),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 3)
            };

            innerLayout.Controls.Add(label, 0, 0);
            innerLayout.Controls.Add(textBox, 0, 1);
            panel.Controls.Add(innerLayout);

            return panel;
        }

        private Panel BuildExchangeSection(string title, Color accentColor, out Label lblTitle, out FlowLayoutPanel pnlCartridges, bool isCompact)
        {
            Panel section;

            if (isCompact)
            {
                // Step 3A: Compact mode - auto-size to content, never collapse
                section = new Panel
                {
                    Dock = DockStyle.Top,  // CRITICAL: Use Top for AutoSize rows
                    AutoSize = true,       // Auto-size based on content
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,  // Shrink to fit content
                    BackColor = Color.FromArgb(250, 251, 252),
                    Padding = new Padding(10),
                    Margin = new Padding(0, 5, 0, 5),
                    AutoScroll = false  // No scrolling - always show all content
                };
            }
            else
            {
                // Step 3B: Auto-size so pnlRight's AutoScroll handles overflow
                section = new Panel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    BackColor = Color.FromArgb(250, 251, 252),
                    Padding = new Padding(10),
                    Margin = new Padding(0, 5, 0, 5),
                    MinimumSize = new Size(0, 200),  // Prevent collapse when empty
                    AutoScroll = false  // pnlRight handles scrolling
                };
            }

            section.Paint += (s, e) =>
            {
                using (var pen = new Pen(accentColor, 2))
                {
                    e.Graphics.DrawLine(pen, 0, 0, section.Width, 0);
                }
            };

            lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = accentColor,
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 5, 0, 10)
            };

            pnlCartridges = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,  // Top-dock so AutoSize parent can measure it
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,  // pnlRight handles scrolling
                BackColor = Color.Transparent,
                AutoSize = true
            };

            section.Controls.Add(pnlCartridges);
            section.Controls.Add(lblTitle);

            return section;
        }

        private Label CreateSummaryLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 90, 100),
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5)
            };
        }

        private TextBox CreateSummaryTextBox()
        {
            return new TextBox
            {
                ReadOnly = true,
                BackColor = Color.FromArgb(240, 242, 245),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 10, 5),
                AutoSize = false
            };
        }

        private Button CreateButton(string text, Color backColor)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(0, 0, 10, 0),
                Padding = new Padding(15, 8, 15, 8)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        #region Lifecycle Methods (Mirrors ViewSetDetailPage.cs Upgrade Lifecycle)

        /// <summary>
        /// LoadPendingCartridgeRequests - Loads and renders session blocks.
        /// Each SubmissionSessionId renders as a persistent block with label and table.
        /// </summary>
        private void LoadPendingCartridgeRequests()
        {
            _isLoadingData = true;
            try
            {
                _allRequests = _repository.GetPendingCartridgeRequests();

                // DEBUG: Log each loaded DTO to confirm what the SQL/repository layer returns
                System.Diagnostics.Debug.WriteLine($"[DEBUG] LoadPendingCartridgeRequests: {_allRequests?.Count ?? 0} request(s) loaded.");
                if (_allRequests != null)
                {
                    foreach (var r in _allRequests)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[DEBUG]   ReqId={r.ReqId} | ItemId={r.ItemId} | CartridgeModelId={r.CartridgeModelId?.ToString() ?? "null"} | ModelNumber={r.ModelNumber ?? "null"} | TypedModelNumber={r.TypedModelNumber ?? "null"}");
                    }
                }

                // Group requests into sessions
                _sessionGroups = GroupRequestsIntoSessions(_allRequests);
                _currentPage = 1;

                _isLoadingData = false;
                RenderSessionBlocks();
            }
            catch (Exception ex)
            {
                _isLoadingData = false;
                MessageBox.Show($"Error loading pending requests:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateSortIndicator()
        {
            if (_lblSortIndicator == null) return;
            if (_sortAscending)
            {
                _lblSortIndicator.Text = "↑ Asc — Oldest First (Priority Order)";
                _lblSortIndicator.ForeColor = Color.FromArgb(39, 174, 96);
            }
            else
            {
                _lblSortIndicator.Text = "↓ Desc — Newest First";
                _lblSortIndicator.ForeColor = Color.FromArgb(52, 152, 219);
            }
        }

        private void RenderSessionBlocks()
        {
            flowSessionBlocks.Controls.Clear();

            if (_sessionGroups == null || _sessionGroups.Count == 0)
            {
                UpdatePageInfo(0, 0);
                ClearExchangePanel();
                return;
            }

            var ordered = _sortAscending
                ? _sessionGroups.Values.Where(s => s != null).OrderBy(s => s.GroupIndex)
                : (IEnumerable<RequestSessionGroup>)_sessionGroups.Values.Where(s => s != null).OrderByDescending(s => s.GroupIndex);

            var allSessions = ordered.ToList();
            int totalGroups = allSessions.Count;
            int totalPages = (int)Math.Ceiling((double)totalGroups / PageSize);

            if (_currentPage > totalPages) _currentPage = Math.Max(1, totalPages);
            if (_currentPage < 1) _currentPage = 1;

            var pagedSessions = allSessions
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize);

            foreach (var session in pagedSessions)
            {
                try
                {
                    var sessionBlock = CreateSessionBlock(session);
                    flowSessionBlocks.Controls.Add(sessionBlock);
                }
                catch (Exception sessionEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating session block: {sessionEx.Message}");
                }
            }

            UpdatePageInfo(totalGroups, totalPages);
            ClearExchangePanel();

            // Defer clearing auto-selection until after WinForms finishes rendering all blocks.
            // Guard with IsHandleCreated — BeginInvoke throws if called before the window handle exists.
            Action clearSelections = () =>
            {
                foreach (Control block in flowSessionBlocks.Controls)
                    foreach (Control inner in block.Controls)
                        if (inner is DataGridView dgv)
                            dgv.ClearSelection();
            };
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(clearSelections);
            else if (!IsDisposed)
                clearSelections();
        }

        private void UpdatePageInfo(int totalGroups, int totalPages)
        {
            if (_lblPageInfo == null) return;

            if (totalGroups == 0)
            {
                _lblPageInfo.Text = "No pending requests";
                _btnFirstPage.Enabled = _btnPrevPage.Enabled = _btnNextPage.Enabled = _btnLastPage.Enabled = false;
                return;
            }

            _lblPageInfo.Text = $"Page {_currentPage} of {totalPages}  ({totalGroups} groups)";
            _btnFirstPage.Enabled = _btnPrevPage.Enabled = _currentPage > 1;
            _btnNextPage.Enabled  = _btnLastPage.Enabled  = _currentPage < totalPages;
        }

        /// <summary>
        /// Groups requests into sessions for visual styling (not for hiding rows).
        /// Returns dictionary keyed by SessionKey with group metadata.
        /// </summary>
        private Dictionary<string, RequestSessionGroup> GroupRequestsIntoSessions(List<CartridgeRequestDto> requests)
        {
            var sessions = new Dictionary<string, RequestSessionGroup>();
            int groupIndex = 0;

            var sortedRequests = requests.OrderBy(r => r.DateCreated).ThenBy(r => r.ReqId).ToList();
            var processed = new HashSet<int>();

            foreach (var req in sortedRequests)
            {
                if (processed.Contains(req.ReqId))
                    continue;

                // Use SubmissionSessionId for grouping if available (portal requests)
                if (req.SubmissionSessionId.HasValue)
                {
                    // Find all CURRENT (non-processed) requests with the same SubmissionSessionId
                    var relatedRequests = requests.Where(r =>
                        r.SubmissionSessionId.HasValue &&
                        r.SubmissionSessionId.Value == req.SubmissionSessionId.Value &&
                        !processed.Contains(r.ReqId)
                    ).OrderBy(r => r.ReqId).ToList();

                    if (relatedRequests.Any())
                    {
                        // OriginalSubmissionCount is computed by the SQL query (counts ALL CRM rows
                        // for this SubmissionSessionId, including already-fulfilled ones).
                        // Take the max across related requests in case any row has a higher value.
                        int originalSubmissionCount = relatedRequests.Max(r => r.OriginalSubmissionCount);

                        var sessionKey = $"G{req.SubmissionSessionId.Value:N}";
                        var session = new RequestSessionGroup
                        {
                            SessionKey = sessionKey,
                            GroupIndex = groupIndex++,
                            EmpId = req.EmpId,
                            EmployeeName = req.EmployeeName,
                            BranchName = req.BranchName,
                            DepartmentName = req.DepartmentName,
                            DateCreated = req.DateCreated,
                            Requests = relatedRequests,
                            // Multi-model if the original submission had more than one model.
                            IsMultiModel = originalSubmissionCount > 1
                        };

                        session.BranchDept = $"{session.BranchName ?? "Unknown Branch"} / {session.DepartmentName ?? "Unknown Department"}";
                        // ModelCount shows CURRENT remaining count (for display)
                        session.ModelCount = session.Requests?.Count ?? 0;
                        session.TotalQuantity = session.Requests?.Sum(r => r?.Quantity ?? 0) ?? 0;

                        sessions[sessionKey] = session;

                        foreach (var r in relatedRequests)
                            processed.Add(r.ReqId);
                    }
                }
                else
                {
                    // Legacy request with NULL SubmissionSessionId - treat as standalone
                    var sessionKey = $"L{req.ReqId}";
                    var session = new RequestSessionGroup
                    {
                        SessionKey = sessionKey,
                        GroupIndex = groupIndex++,
                        EmpId = req.EmpId,
                        EmployeeName = req.EmployeeName,
                        BranchName = req.BranchName,
                        DepartmentName = req.DepartmentName,
                        DateCreated = req.DateCreated,
                        Requests = new List<CartridgeRequestDto> { req },
                        IsMultiModel = false
                    };

                    session.BranchDept = $"{session.BranchName ?? "Unknown Branch"} / {session.DepartmentName ?? "Unknown Department"}";
                    session.ModelCount = 1;
                    session.TotalQuantity = req.Quantity;

                    sessions[sessionKey] = session;
                    processed.Add(req.ReqId);
                }
            }

            return sessions;
        }

        private string GetSessionKeyForRequest(CartridgeRequestDto request)
        {
            if (request.SubmissionSessionId.HasValue)
            {
                return $"G{request.SubmissionSessionId.Value:N}";
            }
            else
            {
                return $"L{request.ReqId}";
            }
        }

        /// <summary>
        /// Creates a session block with label and table for a SubmissionSessionId group.
        /// Color coding: Blue = Multi Model, Peach = Single Model, Green overlay = Fully Fulfilled.
        /// </summary>
        private Panel CreateSessionBlock(RequestSessionGroup session)
        {
            var blockPanel = new Panel
            {
                Width = flowSessionBlocks.Width - 40,
                Height = 80 + ((session.Requests?.Count ?? 0) * 48), // Label height + row heights + padding (48px per row for wrap)
                AutoSize = false,
                Margin = new Padding(0, 0, 0, 15),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Check if all requests in session are fulfilled (with null checks)
            bool allFulfilled = session.Requests != null && session.Requests.All(r => 
                r != null && !string.IsNullOrWhiteSpace(r.Status) &&
                (r.Status.Equals("Submitted", StringComparison.OrdinalIgnoreCase) ||
                 r.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)));

            // Determine base color: Blue = Multi Model, Peach = Single Model
            Color baseColor = session.IsMultiModel 
                ? Color.FromArgb(52, 152, 219)   // Blue for Multi Model
                : Color.FromArgb(255, 218, 185); // Peach for Single Model

            // Apply green overlay if fully fulfilled
            Color labelColor = allFulfilled 
                ? Color.FromArgb(46, 204, 113)   // Green overlay for Fully Fulfilled
                : baseColor;

            // Session label with null checks
            string employeeName = session.EmployeeName ?? "Unknown Employee";
            string branchDept = session.BranchDept ?? "Unknown Location";
            
            var lblSession = new Label
            {
                Text = session.IsMultiModel 
                    ? $"📦 Multi Model Session ({session.ModelCount} models) - {employeeName} - {branchDept}"
                    : $"📄 Single Model - {employeeName} - {branchDept}",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = labelColor,
                AutoSize = false,
                Height = 40,
                Width = blockPanel.Width - 2,
                Location = new Point(1, 1),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };

            // DataGridView for request rows
            var dgvRequests = new DataGridView
            {
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                ScrollBars = ScrollBars.Vertical,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(230, 230, 230),
                    ForeColor = Color.Black,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    WrapMode = DataGridViewTriState.False
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    SelectionBackColor = Color.FromArgb(93, 173, 226),
                    SelectionForeColor = Color.White,
                    Font = new Font("Segoe UI", 9F),
                    WrapMode = DataGridViewTriState.True
                },
                Width = blockPanel.Width - 4,
                Height = 35 + (session.Requests.Count * 48), // Header height + row heights (48px per row for wrap)
                Location = new Point(2, 42)
            };

            // Define columns - ReqId ALWAYS VISIBLE
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ReqId",
                DataPropertyName = "ReqId",
                HeaderText = "Req #",
                FillWeight = 15,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(52, 73, 94)
                }
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ModelNumber",
                DataPropertyName = "ModelNumber",
                HeaderText = "Model",
                FillWeight = 25,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                }
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Quantity",
                DataPropertyName = "Quantity",
                HeaderText = "Qty",
                FillWeight = 10
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "WithCartridge",
                DataPropertyName = "WithCartridgeDisplay",
                HeaderText = "With Cartridge",
                FillWeight = 20
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "GoodEmptyQty",
                DataPropertyName = "GoodEmptyQty",
                HeaderText = "Good",
                FillWeight = 10,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, WrapMode = DataGridViewTriState.True }
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DamagedEmptyQty",
                DataPropertyName = "DamagedEmptyQty",
                HeaderText = "Damaged",
                FillWeight = 10,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, WrapMode = DataGridViewTriState.True }
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Status",
                DataPropertyName = "Status",
                HeaderText = "Status",
                FillWeight = 15
            });
            // GroupIndex 0 = oldest submission (highest FCFS priority) → green; later → blue
            Color dateTextColor = session.GroupIndex == 0
                ? Color.FromArgb(39, 174, 96)
                : Color.FromArgb(52, 152, 219);

            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DateCreated",
                DataPropertyName = "DateCreated",
                HeaderText = "Created",
                FillWeight = 15,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "yyyy-MM-dd HH:mm",
                    ForeColor = dateTextColor,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                }
            });

            // Bind data
            dgvRequests.DataSource = session.Requests;

            // Handle selection - FIXED: Multiple handlers ensure reliable selection tracking
            dgvRequests.SelectionChanged += (s, e) =>
            {
                if (_isLoadingData) return;
                if (dgvRequests.SelectedRows.Count > 0)
                {
                    var selectedRequest = dgvRequests.SelectedRows[0].DataBoundItem as CartridgeRequestDto;
                    if (selectedRequest != null)
                    {
                        // Clear selections from other session blocks
                        ClearOtherSessionSelections(dgvRequests);
                        OnCartridgeRequestSelected(selectedRequest);
                    }
                }
            };

            // CRITICAL FIX: Add CellClick handler to ensure selection works reliably
            dgvRequests.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    var selectedRequest = dgvRequests.Rows[e.RowIndex].DataBoundItem as CartridgeRequestDto;
                    if (selectedRequest != null)
                    {
                        // Clear selections from all other session blocks
                        ClearOtherSessionSelections(dgvRequests);

                        // Ensure this row is selected
                        dgvRequests.ClearSelection();
                        dgvRequests.Rows[e.RowIndex].Selected = true;

                        // Update the selected request state
                        OnCartridgeRequestSelected(selectedRequest);
                    }
                }
            };

            dgvRequests.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    var selectedRequest = dgvRequests.Rows[e.RowIndex].DataBoundItem as CartridgeRequestDto;
                    if (selectedRequest != null)
                    {
                        BtnOpenRequest_Click(s, e);
                    }
                }
            };

            blockPanel.Controls.Add(dgvRequests);
            blockPanel.Controls.Add(lblSession);

            // Ensure proper z-order (label on top)
            lblSession.BringToFront();

            return blockPanel;
        }

        /// <summary>
        /// Clears selections from all DataGridViews in session blocks except the specified one.
        /// Ensures only one request is selected at a time across all session blocks.
        /// </summary>
        private void ClearOtherSessionSelections(DataGridView exceptThisGrid)
        {
            if (flowSessionBlocks == null)
                return;

            foreach (Control control in flowSessionBlocks.Controls)
            {
                if (control is Panel blockPanel)
                {
                    foreach (Control innerControl in blockPanel.Controls)
                    {
                        if (innerControl is DataGridView dgv && dgv != exceptThisGrid)
                        {
                            dgv.ClearSelection();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// OnCartridgeRequestSelected - Handles request selection and populates exchange panel.
        /// IMPORTANT: Model display ALWAYS comes from ModelNumber (portal typed model) or Description tag,
        /// NEVER from arbitrary Item.Name fallback (prevents "rx 310 cart" issue).
        /// </summary>
        private async void OnCartridgeRequestSelected(CartridgeRequestDto request)
        {
            // CellClick sets .Selected = true which fires SelectionChanged, causing a double-call
            // for the same request. Stamp a version so only the latest builder writes to the panels.
            int myVersion = ++_exchangeBuildVersion;

            _selectedRequest = request;

            // Find session for this request to determine if multi-model
            string sessionKey = GetSessionKeyForRequest(request);
            RequestSessionGroup session = null;
            _sessionGroups?.TryGetValue(sessionKey, out session);

            bool isMulti = session != null && session.IsMultiModel
                           && session.Requests != null && session.Requests.Count > 1;

            // Disable fulfill while stock is loading to prevent premature submission
            btnFulfill.Enabled = false;
            if (btnForceDelete != null)
                btnForceDelete.Enabled = false;

            if (isMulti)
            {
                // Multi-model: show session-level summary
                var reqIds = string.Join(", ", session.Requests.Select(r => r.ReqId.ToString()));
                txtReqId.Text = reqIds;
                txtRequester.Text = request.EmployeeName ?? "N/A";
                txtModel.Text = $"Multi-Model ({session.Requests.Count} models)";
                txtQuantity.Text = session.Requests.Sum(r => r.Quantity).ToString();
                txtCondition.Text = "Yes";
                txtGoodEmptyQty.Text = session.Requests.Sum(r => r.GoodEmptyQty).ToString();
                txtDamagedEmptyQty.Text = session.Requests.Sum(r => r.DamagedEmptyQty).ToString();
                txtCompany.Text = request.CompanyName ?? "N/A";
                txtBranch.Text = request.BranchName ?? "N/A";
                txtDepartment.Text = request.DepartmentName ?? "N/A";
                txtDistributionMethod.Text = request.DistributionMethod ?? "N/A";
                txtReceivedBy.Text = request.ReceivedByName ?? "N/A";
                txtAdditionalRemarks.Text = request.AdditionalRemarks ?? "";

                await BuildMultiModelExchangeDropdownsAsync(session, myVersion);
            }
            else
            {
                // Single-model: existing behaviour
                // CRITICAL: Use the correct model source - NEVER fallback to ItemName
                string displayModel = request.TypedModelNumber ?? request.ModelNumber;
                if (string.IsNullOrWhiteSpace(displayModel))
                    displayModel = "Unknown Model";

                txtReqId.Text = request.ReqId.ToString();
                txtRequester.Text = request.EmployeeName ?? "N/A";
                txtModel.Text = displayModel;
                txtQuantity.Text = request.Quantity.ToString();
                txtCondition.Text = request.ConditionType == "With Cartridge" ? "Yes"
                                  : request.ConditionType == "Without Cartridge" ? "No"
                                  : request.ConditionType ?? "N/A";
                txtGoodEmptyQty.Text = request.GoodEmptyQty.ToString();
                txtDamagedEmptyQty.Text = request.DamagedEmptyQty.ToString();
                txtCompany.Text = request.CompanyName ?? "N/A";
                txtBranch.Text = request.BranchName ?? "N/A";
                txtDepartment.Text = request.DepartmentName ?? "N/A";
                txtDistributionMethod.Text = request.DistributionMethod ?? "N/A";
                txtReceivedBy.Text = request.ReceivedByName ?? "N/A";
                txtAdditionalRemarks.Text = request.AdditionalRemarks ?? "";

                await BuildExchangeDropdownsAsync(request, myVersion);
            }

            // Only commit UI state if this call wasn't superseded by a later selection
            if (myVersion == _exchangeBuildVersion)
            {
                btnFulfill.Enabled = true;
                if (btnForceDelete != null)
                    btnForceDelete.Enabled = true;
            }
        }

        private async Task BuildExchangeDropdownsAsync(CartridgeRequestDto request, int version)
        {
            _isMultiModelMode = false;
            _multiModelState = null;
            _multiModelRequests = null;

            // DEBUG: Log incoming request fields before any resolution
            System.Diagnostics.Debug.WriteLine($"[DEBUG] BuildExchangeDropdownsAsync called for ReqId={request?.ReqId}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG]   request.TypedModelNumber={request?.TypedModelNumber ?? "null"}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG]   request.ModelNumber={request?.ModelNumber ?? "null"}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG]   request.CartridgeModelId={request?.CartridgeModelId?.ToString() ?? "null"}");

            // Clear existing controls
            pnlReturnedCartridges.Controls.Clear();
            pnlIssuedCartridges.Controls.Clear();
            _issuedDropdowns.Clear();

            _availableBrandNewStock = 0;
            _availableRefilledStock = 0;

            // Offload the 3 synchronous DB calls to a thread-pool thread so the UI stays responsive.
            // Resolve CartridgeModelId strictly from the requested model name.
            // CartridgeModelId in the DTO comes from the placeholder Item (SELECT TOP 1 from portal),
            // which always points to the first/default inventory record — NOT the requested model.
            // Name-based lookup (TypedModelNumber > ModelNumber) is always authoritative.
            // Fall back to the DTO's CartridgeModelId only when no model name is available at all.
            string cartridgeModelStr = request.TypedModelNumber ?? request.ModelNumber;
            int? dtoCmid = request.CartridgeModelId;
            try
            {
                var (brandNew, refilled) = await Task.Run(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG]   cartridgeModelStr = \"{cartridgeModelStr ?? "null"}\"");

                    int? resolvedModelId = !string.IsNullOrWhiteSpace(cartridgeModelStr)
                        ? _repository.GetCartridgeModelIdByModelNumber(cartridgeModelStr)
                        : null;

                    System.Diagnostics.Debug.WriteLine($"[DEBUG]   resolvedModelId after name lookup = {resolvedModelId?.ToString() ?? "null"}");

                    if (!resolvedModelId.HasValue && dtoCmid.HasValue && dtoCmid.Value > 0)
                        resolvedModelId = dtoCmid;

                    System.Diagnostics.Debug.WriteLine($"[DEBUG]   resolvedModelId (final) = {resolvedModelId?.ToString() ?? "null"}");

                    int bn = _repository.GetAvailableIssuableStockByCondition(resolvedModelId, "Brand New");
                    int rf = _repository.GetAvailableIssuableStockByCondition(resolvedModelId, "Refilled");
                    System.Diagnostics.Debug.WriteLine($"[DEBUG]   brandNew={bn}, refilled={rf}");
                    return (bn, rf);
                });

                _availableBrandNewStock = brandNew;
                _availableRefilledStock = refilled;
            }
            catch (Exception dbgEx)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG]   EXCEPTION in BuildExchangeDropdownsAsync: {dbgEx.Message}");
                _availableBrandNewStock = 0;
                _availableRefilledStock = 0;
            }

            // Back on UI thread: bail out if a newer selection already started its build
            if (version != _exchangeBuildVersion)
                return;

            pnlReturnedCartridges.Controls.Clear();
            pnlIssuedCartridges.Controls.Clear();
            pnlIssuedCartridges.Controls.Add(CreateIssuedQuantityPanel(request.Quantity, _availableBrandNewStock, _availableRefilledStock));
            pnlReturnedCartridges.Controls.Add(CreateReturnedQuantityPanel(request.Quantity));
            UpdateStep3BDisplay();
        }

        private Panel CreateIssuedQuantityPanel(int requestedQty, int availableBrandNewStock, int availableRefilledStock)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10),
                BackColor = Color.Transparent,
                Padding = new Padding(10)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = Color.Transparent,
                Padding = new Padding(5)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Row 0: Model
            var lblModel = new Label
            {
                Text = "Cartridge Model:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 8, 10, 5),
                Margin = new Padding(0)
            };

            string displayModel = _selectedRequest?.TypedModelNumber ?? _selectedRequest?.ModelNumber ?? "Unknown Model";
            var txtModelValue = new TextBox
            {
                Text = displayModel,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 5, 0, 5)
            };

            layout.Controls.Add(lblModel, 0, 0);
            layout.Controls.Add(txtModelValue, 1, 0);

            // Row 1: Brand New Quantity
            var lblBrandNewQty = new Label
            {
                Text = "Issued Brand New:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 8, 10, 5),
                Margin = new Padding(0)
            };

            nudIssuedBrandNewQty = new NumericUpDown
            {
                Minimum = 0,
                Maximum = Math.Min(requestedQty, availableBrandNewStock),
                Value = 0,
                Dock = DockStyle.Left,
                Width = 120,
                TabStop = true,
                Enabled = true,
                Font = new Font("Segoe UI", 10F),
                TabIndex = 0,
                InterceptArrowKeys = true,
                ReadOnly = false,
                DecimalPlaces = 0,
                ThousandsSeparator = false,
                TextAlign = HorizontalAlignment.Left,
                Margin = new Padding(0, 5, 0, 5)
            };

            nudIssuedBrandNewQty.ValueChanged += (s, e) => UpdateStep3BDisplay();
            nudIssuedBrandNewQty.Enter += (s, e) => nudIssuedBrandNewQty.Select(0, nudIssuedBrandNewQty.Text.Length);

            layout.Controls.Add(lblBrandNewQty, 0, 1);
            layout.Controls.Add(nudIssuedBrandNewQty, 1, 1);

            // Row 2: Refilled Quantity
            var lblRefilledQty = new Label
            {
                Text = "Issued Refilled:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 8, 10, 5),
                Margin = new Padding(0)
            };

            nudIssuedRefilledQty = new NumericUpDown
            {
                Minimum = 0,
                Maximum = Math.Min(requestedQty, availableRefilledStock),
                Value = 0,
                Dock = DockStyle.Left,
                Width = 120,
                TabStop = true,
                Enabled = true,
                Font = new Font("Segoe UI", 10F),
                TabIndex = 1,
                InterceptArrowKeys = true,
                ReadOnly = false,
                DecimalPlaces = 0,
                ThousandsSeparator = false,
                TextAlign = HorizontalAlignment.Left,
                Margin = new Padding(0, 5, 0, 5)
            };

            nudIssuedRefilledQty.ValueChanged += (s, e) => UpdateStep3BDisplay();
            nudIssuedRefilledQty.Enter += (s, e) => nudIssuedRefilledQty.Select(0, nudIssuedRefilledQty.Text.Length);

            layout.Controls.Add(lblRefilledQty, 0, 2);
            layout.Controls.Add(nudIssuedRefilledQty, 1, 2);

            // Row 3: Stock info
            var lblStockTitle = new Label
            {
                Text = "",
                AutoSize = true
            };

            lblIssuedStock = new Label
            {
                Text = $"Stock: Brand New={availableBrandNewStock}, Refilled={availableRefilledStock}   (Requested: {requestedQty})",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.FromArgb(90, 90, 90),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 5, 0, 10),
                Margin = new Padding(0)
            };

            layout.Controls.Add(lblStockTitle, 0, 3);
            layout.Controls.Add(lblIssuedStock, 1, 3);

            panel.Controls.Add(layout);
            return panel;
        }

        private Panel CreateReturnedQuantityPanel(int requestedQty)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10),
                BackColor = Color.FromArgb(250, 252, 255),
                Padding = new Padding(10)
            };

            // Outer table: 2 equal columns — left = qty metrics, right = status + remarks
            var outerTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            outerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F)); // Left
            outerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F)); // Right (wider for remarks)
            outerTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // ── LEFT COLUMN: Returned Empty Qty, Issued Full Qty, Pending ────────
            var leftTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };
            leftTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            leftTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            leftTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Row 0: Returned Empty Qty
            var lblReturnedTitle = new Label
            {
                Text = "Returned Empty Qty:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 5, 10, 5),
                Margin = new Padding(0)
            };
            lblReturnedEmptyQty = new Label
            {
                Text = requestedQty.ToString(),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(231, 76, 60),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 5, 0, 5),
                Margin = new Padding(0)
            };
            leftTable.Controls.Add(lblReturnedTitle, 0, 0);
            leftTable.Controls.Add(lblReturnedEmptyQty, 1, 0);

            // Row 1: Issued Full Qty
            var lblIssuedTitle = new Label
            {
                Text = "Issued Full Qty:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 5, 10, 5),
                Margin = new Padding(0)
            };
            lblIssuedFullQty = new Label
            {
                Text = "0",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(46, 204, 113),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 5, 0, 5),
                Margin = new Padding(0)
            };
            leftTable.Controls.Add(lblIssuedTitle, 0, 1);
            leftTable.Controls.Add(lblIssuedFullQty, 1, 1);

            // Row 2: Pending (Unfulfilled)
            var lblUnfulfilledTitle = new Label
            {
                Text = "Pending (Unfulfilled):",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 5, 10, 5),
                Margin = new Padding(0)
            };
            lblUnfulfilledQty = new Label
            {
                Text = requestedQty.ToString(),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(230, 126, 34),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 5, 0, 5),
                Margin = new Padding(0)
            };
            leftTable.Controls.Add(lblUnfulfilledTitle, 0, 2);
            leftTable.Controls.Add(lblUnfulfilledQty, 1, 2);

            // ── RIGHT COLUMN: Status + Remarks ────────────────────────────────────
            var rightTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent,
                Padding = new Padding(8, 0, 0, 0)  // left indent to separate from left column
            };
            rightTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // "Status:" label
            rightTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Status value
            rightTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Row 0: Status
            rightTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Row 1: Remarks label
            rightTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // Row 2: Remarks TextBox

            // Row 0: Status
            var lblStatusTitle = new Label
            {
                Text = "Status:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 5, 10, 5),
                Margin = new Padding(0)
            };
            lblFulfillmentStatus = new Label
            {
                Text = "Unfulfilled",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(231, 76, 60),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 5, 0, 5),
                Margin = new Padding(0)
            };
            rightTable.Controls.Add(lblStatusTitle, 0, 0);
            rightTable.Controls.Add(lblFulfillmentStatus, 1, 0);

            // Row 1: Remarks label (spans both columns)
            var lblRemarksTitle = new Label
            {
                Text = "Remarks:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize = true,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 5, 0, 2),
                Margin = new Padding(0)
            };
            rightTable.Controls.Add(lblRemarksTitle, 0, 1);
            rightTable.SetColumnSpan(lblRemarksTitle, 2);

            // Row 2: Remarks TextBox (spans both columns, full width)
            txtAutoRemarks = new TextBox
            {
                Text = "",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100),
                BackColor = Color.FromArgb(250, 252, 255),
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = true,
                WordWrap = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, 80),
                Margin = new Padding(0, 0, 0, 5),
                Padding = new Padding(5)
            };
            rightTable.Controls.Add(txtAutoRemarks, 0, 2);
            rightTable.SetColumnSpan(txtAutoRemarks, 2);

            outerTable.Controls.Add(leftTable, 0, 0);
            outerTable.Controls.Add(rightTable, 1, 0);

            panel.Controls.Add(outerTable);
            return panel;
        }

        private void UpdateStep3BDisplay()
        {
            if (_selectedRequest == null || nudIssuedBrandNewQty == null || nudIssuedRefilledQty == null)
                return;

            int requestedQty = _selectedRequest.Quantity;
            int returnedEmptyQty = requestedQty;
            int issuedBrandNewQty = (int)nudIssuedBrandNewQty.Value;
            int issuedRefilledQty = (int)nudIssuedRefilledQty.Value;
            int issuedFullQty = issuedBrandNewQty + issuedRefilledQty;
            int unfulfilledQty = returnedEmptyQty - issuedFullQty;

            // Use the correct model source (never "rx 310 cart" fallback)
            string cartridgeModel = _selectedRequest.TypedModelNumber ?? _selectedRequest.ModelNumber ?? "N/A";

            // Update display labels
            if (lblReturnedEmptyQty != null)
                lblReturnedEmptyQty.Text = returnedEmptyQty.ToString();

            if (lblIssuedFullQty != null)
                lblIssuedFullQty.Text = issuedFullQty.ToString();

            if (lblUnfulfilledQty != null)
            {
                // Display unfulfilled quantity with warning for negative values (over-fulfillment)
                lblUnfulfilledQty.Text = unfulfilledQty < 0
                    ? $"{unfulfilledQty} ⚠️ INVALID"
                    : unfulfilledQty.ToString();
                lblUnfulfilledQty.ForeColor = unfulfilledQty < 0
                    ? Color.FromArgb(231, 76, 60)  // Red for invalid negative
                    : (unfulfilledQty > 0
                        ? Color.FromArgb(230, 126, 34)
                    : Color.FromArgb(46, 204, 113));
            }

            // Update fulfillment status
            if (lblFulfillmentStatus != null)
            {
                string status;
                Color statusColor;

                if (issuedFullQty == 0)
                {
                    status = "Unfulfilled";
                    statusColor = Color.FromArgb(231, 76, 60);
                }
                else if (issuedFullQty < returnedEmptyQty)
                {
                    status = "Partially Fulfilled";
                    statusColor = Color.FromArgb(230, 126, 34);
                }
                else
                {
                    status = "Fulfilled";
                    statusColor = Color.FromArgb(46, 204, 113);
                }

                lblFulfillmentStatus.Text = status;
                lblFulfillmentStatus.ForeColor = statusColor;
            }

            // Update auto-generated remarks
            if (txtAutoRemarks != null)
            {
                txtAutoRemarks.Text = CartridgeExchangeRemarks.GenerateRemarks(
                    issuedFullQty, returnedEmptyQty, cartridgeModel);
            }

            // Update Maximum constraints dynamically to enforce sum rule
            // Maximum for each field = RequestedQty - OtherFieldValue
            int maxBrandNew = Math.Min(requestedQty - issuedRefilledQty, _availableBrandNewStock);
            int maxRefilled = Math.Min(requestedQty - issuedBrandNewQty, _availableRefilledStock);
            
            if (nudIssuedBrandNewQty.Maximum != maxBrandNew && maxBrandNew >= 0)
                nudIssuedBrandNewQty.Maximum = Math.Max(maxBrandNew, nudIssuedBrandNewQty.Value);
            
            if (nudIssuedRefilledQty.Maximum != maxRefilled && maxRefilled >= 0)
                nudIssuedRefilledQty.Maximum = Math.Max(maxRefilled, nudIssuedRefilledQty.Value);
        }

        // ── Multi-Model Fulfillment ───────────────────────────────────────────────

        private async Task BuildMultiModelExchangeDropdownsAsync(RequestSessionGroup session, int version)
        {
            _isMultiModelMode = true;
            _multiModelRequests = session.Requests;
            _multiModelState = new Dictionary<int, MultiModelRowState>();

            pnlReturnedCartridges.Controls.Clear();
            pnlIssuedCartridges.Controls.Clear();
            _issuedDropdowns.Clear();

            // Null out single-model fields — multi-model uses per-row state instead
            nudIssuedBrandNewQty = null;
            nudIssuedRefilledQty = null;
            lblReturnedEmptyQty = null;
            lblIssuedFullQty = null;
            lblUnfulfilledQty = null;
            lblFulfillmentStatus = null;
            txtAutoRemarks = null;
            _availableBrandNewStock = 0;
            _availableRefilledStock = 0;

            // Run all stock lookups in parallel — one Task per model
            var stockTasks = session.Requests.Select(req => Task.Run(() =>
            {
                int brandNew = 0, refilled = 0;
                try
                {
                    string modelStr = req.TypedModelNumber ?? req.ModelNumber;
                    int? resolvedId = !string.IsNullOrWhiteSpace(modelStr)
                        ? _repository.GetCartridgeModelIdByModelNumber(modelStr)
                        : null;
                    if (!resolvedId.HasValue && req.CartridgeModelId.HasValue && req.CartridgeModelId.Value > 0)
                        resolvedId = req.CartridgeModelId;
                    brandNew = _repository.GetAvailableIssuableStockByCondition(resolvedId, "Brand New");
                    refilled = _repository.GetAvailableIssuableStockByCondition(resolvedId, "Refilled");
                }
                catch { }
                return (ReqId: req.ReqId, BrandNew: brandNew, Refilled: refilled);
            })).ToList();

            var stockResults = await Task.WhenAll(stockTasks);
            var stockByReqId = stockResults.ToDictionary(r => r.ReqId, r => (r.BrandNew, r.Refilled));

            // Back on UI thread: bail out if a newer selection already started its build,
            // or if _multiModelState was nulled by a single-model selection during the await.
            if (version != _exchangeBuildVersion || _multiModelState == null)
                return;

            pnlReturnedCartridges.Controls.Clear();
            pnlIssuedCartridges.Controls.Clear();

            // Build panels with resolved stock values
            foreach (var req in session.Requests)
            {
                stockByReqId.TryGetValue(req.ReqId, out var s);
                int availBrandNew = s.BrandNew;
                int availRefilled = s.Refilled;

                var rowState = new MultiModelRowState
                {
                    RequestedQty      = req.Quantity,
                    AvailableBrandNew = availBrandNew,
                    AvailableRefilled = availRefilled
                };

                pnlIssuedCartridges.Controls.Add(CreateMultiModelIssuedPanel(req, availBrandNew, availRefilled, rowState));
                pnlReturnedCartridges.Controls.Add(CreateMultiModelReturnedPanel(req, rowState));

                _multiModelState[req.ReqId] = rowState;
                UpdateMultiModelRow(req.ReqId);
            }
        }

        private Panel CreateMultiModelIssuedPanel(CartridgeRequestDto req, int availBrandNew, int availRefilled, MultiModelRowState rowState)
        {
            string displayModel = req.TypedModelNumber ?? req.ModelNumber ?? "Unknown Model";
            int requestedQty = req.Quantity;

            var panel = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10),
                BackColor = Color.FromArgb(245, 248, 255),
                Padding = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = Color.Transparent,
                Padding = new Padding(5)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Row 0: Req # + Model header
            var lblHeader = new Label
            {
                Text = $"Req #{req.ReqId} — {displayModel}",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Padding = new Padding(5, 3, 10, 5),
                Margin = new Padding(0)
            };
            layout.Controls.Add(lblHeader, 0, 0);
            layout.SetColumnSpan(lblHeader, 2);

            // Row 1: Brand New NUD
            layout.Controls.Add(new Label
            {
                Text = "Issued Brand New:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 8, 10, 5),
                Margin = new Padding(0)
            }, 0, 1);

            int capturedReqId = req.ReqId;
            var nudBrandNew = new NumericUpDown
            {
                Minimum = 0,
                Maximum = Math.Min(requestedQty, availBrandNew),
                Value = 0,
                Dock = DockStyle.Left,
                Width = 120,
                Font = new Font("Segoe UI", 10F),
                DecimalPlaces = 0,
                Margin = new Padding(0, 5, 0, 5)
            };
            rowState.NudBrandNew = nudBrandNew;
            nudBrandNew.ValueChanged += (s, e) => UpdateMultiModelRow(capturedReqId);
            layout.Controls.Add(nudBrandNew, 1, 1);

            // Row 2: Refilled NUD
            layout.Controls.Add(new Label
            {
                Text = "Issued Refilled:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 8, 10, 5),
                Margin = new Padding(0)
            }, 0, 2);

            var nudRefilled = new NumericUpDown
            {
                Minimum = 0,
                Maximum = Math.Min(requestedQty, availRefilled),
                Value = 0,
                Dock = DockStyle.Left,
                Width = 120,
                Font = new Font("Segoe UI", 10F),
                DecimalPlaces = 0,
                Margin = new Padding(0, 5, 0, 5)
            };
            rowState.NudRefilled = nudRefilled;
            nudRefilled.ValueChanged += (s, e) => UpdateMultiModelRow(capturedReqId);
            layout.Controls.Add(nudRefilled, 1, 2);

            // Row 3: Stock info
            layout.Controls.Add(new Label { Text = "", AutoSize = true }, 0, 3);
            layout.Controls.Add(new Label
            {
                Text = $"Stock: Brand New={availBrandNew}, Refilled={availRefilled}   (Requested: {requestedQty})",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.FromArgb(90, 90, 90),
                AutoSize = true,
                Padding = new Padding(0, 5, 0, 10),
                Margin = new Padding(0)
            }, 1, 3);

            panel.Controls.Add(layout);
            return panel;
        }

        private Panel CreateMultiModelReturnedPanel(CartridgeRequestDto req, MultiModelRowState rowState)
        {
            string displayModel = req.TypedModelNumber ?? req.ModelNumber ?? "Unknown Model";
            int requestedQty = req.Quantity;

            var panel = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10),
                BackColor = Color.FromArgb(250, 252, 255),
                Padding = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Root: 2 rows — header, then content side-by-side
            // Using Dock=Fill so percent columns have a real width reference.
            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // header
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // content

            // Row 0: Header
            rootLayout.Controls.Add(new Label
            {
                Text = $"Req #{req.ReqId} — {displayModel}",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 0, 0, 6)
            }, 0, 0);

            // Row 1: Content — left metrics, right status+remarks
            var outerTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            outerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            outerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            outerTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Left: Returned / Issued Full / Pending
            var leftTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            leftTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            leftTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            leftTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            leftTable.Controls.Add(new Label { Text = "Returned Empty:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(80, 80, 80), AutoSize = true, Padding = new Padding(5, 5, 10, 5) }, 0, 0);
            leftTable.Controls.Add(new Label { Text = requestedQty.ToString(), Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(231, 76, 60), AutoSize = true, Padding = new Padding(0, 5, 0, 5) }, 1, 0);

            leftTable.Controls.Add(new Label { Text = "Issued Full Qty:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(80, 80, 80), AutoSize = true, Padding = new Padding(5, 5, 10, 5) }, 0, 1);
            var lblIssuedFull = new Label { Text = "0", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(46, 204, 113), AutoSize = true, Padding = new Padding(0, 5, 0, 5) };
            rowState.LblIssuedFull = lblIssuedFull;
            leftTable.Controls.Add(lblIssuedFull, 1, 1);

            leftTable.Controls.Add(new Label { Text = "Pending:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(80, 80, 80), AutoSize = true, Padding = new Padding(5, 5, 10, 5) }, 0, 2);
            var lblUnfulfilled = new Label { Text = requestedQty.ToString(), Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(230, 126, 34), AutoSize = true, Padding = new Padding(0, 5, 0, 5) };
            rowState.LblUnfulfilled = lblUnfulfilled;
            leftTable.Controls.Add(lblUnfulfilled, 1, 2);

            // Right: Status + Remarks
            var rightTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.Transparent,
                Padding = new Padding(8, 0, 0, 0)
            };
            rightTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            rightTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rightTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            rightTable.Controls.Add(new Label { Text = "Status:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(80, 80, 80), AutoSize = true, Padding = new Padding(0, 5, 10, 5) }, 0, 0);
            var lblStatus = new Label { Text = "Unfulfilled", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(231, 76, 60), AutoSize = true, Padding = new Padding(0, 5, 0, 5) };
            rowState.LblStatus = lblStatus;
            rightTable.Controls.Add(lblStatus, 1, 0);

            var lblRemarksTitle = new Label { Text = "Remarks:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(80, 80, 80), AutoSize = true, Padding = new Padding(0, 5, 0, 2) };
            rightTable.Controls.Add(lblRemarksTitle, 0, 1);
            rightTable.SetColumnSpan(lblRemarksTitle, 2);

            var txtRemarks = new TextBox
            {
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100),
                BackColor = Color.FromArgb(250, 252, 255),
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                MinimumSize = new Size(0, 60),
                Padding = new Padding(5)
            };
            rowState.TxtRemarks = txtRemarks;
            rightTable.Controls.Add(txtRemarks, 0, 2);
            rightTable.SetColumnSpan(txtRemarks, 2);

            outerTable.Controls.Add(leftTable, 0, 0);
            outerTable.Controls.Add(rightTable, 1, 0);

            rootLayout.Controls.Add(outerTable, 0, 1);
            panel.Controls.Add(rootLayout);
            return panel;
        }

        private void UpdateMultiModelRow(int reqId)
        {
            if (_multiModelState == null || !_multiModelState.TryGetValue(reqId, out var rowState))
                return;
            if (rowState.NudBrandNew == null || rowState.NudRefilled == null)
                return;

            int requestedQty = rowState.RequestedQty;
            int issuedBrandNew = (int)rowState.NudBrandNew.Value;
            int issuedRefilled = (int)rowState.NudRefilled.Value;
            int issuedFull = issuedBrandNew + issuedRefilled;
            int unfulfilled = requestedQty - issuedFull;

            string reqModel = _multiModelRequests?.FirstOrDefault(r => r.ReqId == reqId)
                              ?.TypedModelNumber
                          ?? _multiModelRequests?.FirstOrDefault(r => r.ReqId == reqId)?.ModelNumber
                          ?? "N/A";

            if (rowState.LblIssuedFull != null)
                rowState.LblIssuedFull.Text = issuedFull.ToString();

            if (rowState.LblUnfulfilled != null)
            {
                rowState.LblUnfulfilled.Text = unfulfilled < 0 ? $"{unfulfilled} ⚠️" : unfulfilled.ToString();
                rowState.LblUnfulfilled.ForeColor = unfulfilled < 0 ? Color.FromArgb(231, 76, 60)
                    : unfulfilled > 0 ? Color.FromArgb(230, 126, 34) : Color.FromArgb(46, 204, 113);
            }

            if (rowState.LblStatus != null)
            {
                string status;
                Color statusColor;
                if (issuedFull == 0) { status = "Unfulfilled"; statusColor = Color.FromArgb(231, 76, 60); }
                else if (issuedFull < requestedQty) { status = "Partially Fulfilled"; statusColor = Color.FromArgb(230, 126, 34); }
                else { status = "Fulfilled"; statusColor = Color.FromArgb(46, 204, 113); }
                rowState.LblStatus.Text = status;
                rowState.LblStatus.ForeColor = statusColor;
            }

            if (rowState.TxtRemarks != null)
                rowState.TxtRemarks.Text = CartridgeExchangeRemarks.GenerateRemarks(issuedFull, requestedQty, reqModel);

            // Enforce sum ≤ requestedQty across Brand New and Refilled
            int maxBrandNew = Math.Min(requestedQty - issuedRefilled, rowState.AvailableBrandNew);
            int maxRefilled = Math.Min(requestedQty - issuedBrandNew, rowState.AvailableRefilled);
            if (rowState.NudBrandNew.Maximum != maxBrandNew && maxBrandNew >= 0)
                rowState.NudBrandNew.Maximum = Math.Max(maxBrandNew, rowState.NudBrandNew.Value);
            if (rowState.NudRefilled.Maximum != maxRefilled && maxRefilled >= 0)
                rowState.NudRefilled.Maximum = Math.Max(maxRefilled, rowState.NudRefilled.Value);
        }

        // ─────────────────────────────────────────────────────────────────────────

        private bool ValidateCartridgeExchange()
        {
            if (_selectedRequest == null)
            {
                MessageBox.Show("No request selected.", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            // Multi-model validation
            if (_isMultiModelMode)
            {
                if (_multiModelState == null || _multiModelRequests == null || !_multiModelRequests.Any())
                {
                    MessageBox.Show("No models found for this session.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                var multiErrors = new List<string>();
                foreach (var req in _multiModelRequests)
                {
                    if (!_multiModelState.TryGetValue(req.ReqId, out var rowState)) continue;
                    int issuedBN = rowState.NudBrandNew != null ? (int)rowState.NudBrandNew.Value : 0;
                    int issuedR  = rowState.NudRefilled != null ? (int)rowState.NudRefilled.Value : 0;
                    int total = issuedBN + issuedR;
                    string model = req.TypedModelNumber ?? req.ModelNumber ?? "Unknown";
                    if (issuedBN < 0) multiErrors.Add($"Req #{req.ReqId} {model}: Brand New qty cannot be negative.");
                    if (issuedR  < 0) multiErrors.Add($"Req #{req.ReqId} {model}: Refilled qty cannot be negative.");
                    if (total > req.Quantity)
                        multiErrors.Add($"Req #{req.ReqId} {model}: Issued ({total}) exceeds requested ({req.Quantity}).");
                }

                if (multiErrors.Any())
                {
                    MessageBox.Show("Validation failed:\n\n• " + string.Join("\n• ", multiErrors),
                        "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                return true;
            }

            // Single-model validation
            var errors = new List<string>();
            int requiredQty = _selectedRequest.Quantity;
            int issuedBrandNewQty = nudIssuedBrandNewQty != null ? (int)nudIssuedBrandNewQty.Value : 0;
            int issuedRefilledQty = nudIssuedRefilledQty != null ? (int)nudIssuedRefilledQty.Value : 0;
            int totalIssuedQty = issuedBrandNewQty + issuedRefilledQty;

            // Validation rules
            if (issuedBrandNewQty < 0)
                errors.Add("Brand New quantity cannot be negative.");

            if (issuedRefilledQty < 0)
                errors.Add("Refilled quantity cannot be negative.");

            if (totalIssuedQty > requiredQty)
                errors.Add($"Total issued quantity ({totalIssuedQty}) must not exceed requested quantity ({requiredQty}).");

            if (errors.Any())
            {
                MessageBox.Show(
                    "Validation failed:\n\n• " + string.Join("\n• ", errors),
                    "Validation Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        /// <summary>
        /// CommitCartridgeExchange - Records movements and updates statuses.
        /// IMPORTANT: Multi-model grouping ensures all requests from the same
        /// portal submission are linked to a single shared Set via
        /// EnsureSharedSetForPortalCartridgeGroup() in the repository.
        /// </summary>
        private bool CommitCartridgeExchange()
        {
            try
            {
                int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;

                if (_isMultiModelMode)
                    return CommitMultiModelExchange(userId);

                int issuedBrandNewQty = nudIssuedBrandNewQty != null ? (int)nudIssuedBrandNewQty.Value : 0;
                int issuedRefilledQty = nudIssuedRefilledQty != null ? (int)nudIssuedRefilledQty.Value : 0;
                int totalIssuedQty = issuedBrandNewQty + issuedRefilledQty;
                int requestedQty = _selectedRequest.Quantity;
                int returnedEmptyQty = requestedQty;

                // Backend guard: Prevent over-fulfillment
                if (totalIssuedQty > requestedQty)
                {
                    MessageBox.Show(
                        $"Cannot issue more than requested.\n\n" +
                        $"Requested: {requestedQty}\n" +
                        $"Attempting to issue: {totalIssuedQty} (Brand New: {issuedBrandNewQty}, Refilled: {issuedRefilledQty})\n\n" +
                        "Please reduce the issued quantity.",
                        "Invalid Quantity",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }

                // Use correct model source (portal typed model has priority)
                string cartridgeModel = _selectedRequest.TypedModelNumber ?? _selectedRequest.ModelNumber ?? "N/A";

                // Resolve CartridgeModelId
                int? resolvedModelId = null;
                try
                {
                    resolvedModelId = !string.IsNullOrWhiteSpace(cartridgeModel)
                        ? _repository.GetCartridgeModelIdByModelNumber(cartridgeModel)
                        : null;
                    if (!resolvedModelId.HasValue && _selectedRequest.CartridgeModelId.HasValue && _selectedRequest.CartridgeModelId.Value > 0)
                        resolvedModelId = _selectedRequest.CartridgeModelId;
                }
                catch
                {
                    resolvedModelId = null;
                }

                // Get issuable item IDs by condition
                var issuedBrandNewIds = new List<int>();
                var issuedRefilledIds = new List<int>();

                if (issuedBrandNewQty > 0)
                {
                    var candidateIds = _repository.GetIssuableItemIdsByCondition(
                        resolvedModelId, "Brand New", issuedBrandNewQty);
                    if (candidateIds != null)
                        issuedBrandNewIds = candidateIds;
                }

                if (issuedRefilledQty > 0)
                {
                    var candidateIds = _repository.GetIssuableItemIdsByCondition(
                        resolvedModelId, "Refilled", issuedRefilledQty);
                    if (candidateIds != null)
                        issuedRefilledIds = candidateIds;
                }

                string fulfillmentRemarks = CartridgeExchangeRemarks.GenerateRemarks(
                    totalIssuedQty, returnedEmptyQty, cartridgeModel);

                // Call repository with separate Brand New and Refilled quantities
                int setId = _repository.FulfillCartridgeExchangeByCondition(
                    _selectedRequest.ReqId,
                    returnedEmptyQty,
                    issuedBrandNewIds,
                    issuedRefilledIds,
                    issuedBrandNewQty,
                    issuedRefilledQty,
                    userId,
                    _selectedRequest.RequestModelId,
                    fulfillmentRemarks
                );

                // Fulfillment committed. Notification is sent from the Send Notifications page.
                MessageBox.Show(
                    "Fulfillment committed successfully.\n\nTo send the notification email to the requester, open 'Send Notifications' from the side menu.",
                    "Fulfillment Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error committing exchange:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private bool CommitMultiModelExchange(int userId)
        {
            int lastSetId = 0;

            foreach (var req in _multiModelRequests ?? new List<CartridgeRequestDto>())
            {
                if (!_multiModelState.TryGetValue(req.ReqId, out var rowState))
                    continue;

                int issuedBrandNewQty = rowState.NudBrandNew != null ? (int)rowState.NudBrandNew.Value : 0;
                int issuedRefilledQty = rowState.NudRefilled != null ? (int)rowState.NudRefilled.Value : 0;
                int totalIssuedQty = issuedBrandNewQty + issuedRefilledQty;
                int requestedQty = req.Quantity;
                int returnedEmptyQty = requestedQty;

                if (totalIssuedQty > requestedQty)
                {
                    string m = req.TypedModelNumber ?? req.ModelNumber ?? "Unknown";
                    MessageBox.Show(
                        $"Cannot issue more than requested for model {m}.\n\n" +
                        $"Requested: {requestedQty}, Attempting: {totalIssuedQty}",
                        "Invalid Quantity", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                string cartridgeModel = req.TypedModelNumber ?? req.ModelNumber ?? "N/A";
                int? resolvedModelId = null;
                try
                {
                    resolvedModelId = !string.IsNullOrWhiteSpace(cartridgeModel)
                        ? _repository.GetCartridgeModelIdByModelNumber(cartridgeModel)
                        : null;
                    if (!resolvedModelId.HasValue && req.CartridgeModelId.HasValue && req.CartridgeModelId.Value > 0)
                        resolvedModelId = req.CartridgeModelId;
                }
                catch { }

                var issuedBrandNewIds = new List<int>();
                var issuedRefilledIds = new List<int>();

                if (issuedBrandNewQty > 0)
                {
                    var ids = _repository.GetIssuableItemIdsByCondition(resolvedModelId, "Brand New", issuedBrandNewQty);
                    if (ids != null) issuedBrandNewIds = ids;
                }
                if (issuedRefilledQty > 0)
                {
                    var ids = _repository.GetIssuableItemIdsByCondition(resolvedModelId, "Refilled", issuedRefilledQty);
                    if (ids != null) issuedRefilledIds = ids;
                }

                string fulfillmentRemarks = CartridgeExchangeRemarks.GenerateRemarks(
                    totalIssuedQty, returnedEmptyQty, cartridgeModel);

                lastSetId = _repository.FulfillCartridgeExchangeByCondition(
                    req.ReqId,
                    returnedEmptyQty,
                    issuedBrandNewIds,
                    issuedRefilledIds,
                    issuedBrandNewQty,
                    issuedRefilledQty,
                    userId,
                    req.RequestModelId,
                    fulfillmentRemarks
                );
            }

            if (lastSetId > 0 && _selectedRequest != null)
            {
                // Fulfillment committed. Notification is sent from the Send Notifications page.
                MessageBox.Show(
                    "Fulfillment committed successfully.\n\nTo send the notification email to the requester, open 'Send Notifications' from the side menu.",
                    "Fulfillment Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return true;
        }

        /// <summary>
        /// Sends a Set-level Cartridge fulfillment email.
        /// CRITICAL: Set-centric, NOT request-centric. Uses ONLY dbo.SetItem data.
        /// Email is based on Set, not individual Request. Subject uses SetCode, body shows all SetItems.
        /// </summary>
        /// <summary>
        /// Sends a fulfillment notification email for the given Set.
        /// Called from the CartridgeFulfillmentNotificationPage after IT confirms the receiver.
        /// All data (employee, branch, set details) is queried from the DB using the setId.
        /// </summary>
        /// <summary>
        /// Returns null on success, or a human-readable skip/error message on failure.
        /// Awaitable — caller gets real feedback instead of fire-and-forget.
        /// </summary>
        /// <summary>
        /// Holds a rendered email subject + body and the SMTP parameters needed to send it.
        /// Produced by <see cref="GenerateEmailContentForSet"/> and consumed by
        /// <see cref="SendFulfillmentEmailWithPreview"/> or by the EmailPreviewForm edit flow.
        /// </summary>
        public sealed class CartridgeEmailPreview
        {
            public int    SetId     { get; set; }
            public string Subject   { get; set; }
            public string Body      { get; set; }
            public int    ProfileId { get; set; }
            public int?   EmpId     { get; set; }
            public int?   BranchId  { get; set; }
            /// <summary>Non-null when generation failed; contains the human-readable reason.</summary>
            public string Error     { get; set; }
        }

        /// <summary>
        /// Generates the rendered email subject and body for a set without sending.
        /// Use the returned <see cref="CartridgeEmailPreview"/> to optionally edit the content
        /// before passing it to <see cref="SendFulfillmentEmailWithPreview"/>.
        /// </summary>
        public static async Task<CartridgeEmailPreview> GenerateEmailContentForSet(int setId)
        {
            if (setId <= 0)
                return new CartridgeEmailPreview { Error = "Invalid Set ID." };

            var emailRepo    = new EmailRepository();
            var setRepo      = new SetRepository();

            await EnsureCartridgeEmailTemplatesAsync(emailRepo);

            var profiles      = await emailRepo.GetSmtpProfilesAsync(activeOnly: true);
            var activeProfile = profiles.FirstOrDefault();
            if (activeProfile == null)
                return new CartridgeEmailPreview { Error = "No active SMTP profile found. Configure one in Email Settings." };

            var setDto = await setRepo.GetSetByIdAsync(setId);
            if (setDto == null)
                return new CartridgeEmailPreview { Error = $"Set {setId} not found in the database." };

            var setItems = await setRepo.GetCartridgeSetItemsAsync(setId);
            if (setItems == null || !setItems.Any())
                return new CartridgeEmailPreview { Error = "No SetItems found for this Set — fulfillment may not have been committed yet." };

            int firstReqId = 0;
            string receivedByName = string.Empty;
            using (var con = new System.Data.SqlClient.SqlConnection(Core.DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var cmd = new System.Data.SqlClient.SqlCommand(
                    @"SELECT TOP 1 r.ReqId, ISNULL(e.Name, '') AS ReceivedByName
                      FROM dbo.Request r
                      LEFT JOIN dbo.Employee e ON e.EmpId = r.ReceivedById
                      WHERE r.SetId = @SetId
                      ORDER BY r.ReqId", con))
                {
                    cmd.Parameters.AddWithValue("@SetId", setId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            firstReqId     = reader.GetInt32(0);
                            receivedByName = reader.GetString(1);
                        }
                    }
                }
            }

            var employeeDetail = firstReqId > 0 ? await setRepo.GetEmployeeDetailsForRequestAsync(firstReqId) : null;
            int? empId    = employeeDetail?.EmpId;
            if (!empId.HasValue || empId.Value <= 0) empId = null;

            int? branchId      = empId.HasValue ? await GetEmployeeBranchIdAsync(empId.Value) : (int?)null;
            var  setRequests   = await setRepo.GetSetRequestsAsync(setId);
            var  modelSummaries = await setRepo.GetCartridgeExchangeModelSummariesBySetAsync(setId);

            // ── Build the same placeholders as SendCartridgeSetFulfillmentEmail ────────
            string setCode           = setDto.SetCode ?? "N/A";
            string createdAt         = setDto.CreatedAt.ToString("MM/dd/yyyy HH:mm");
            // Use effective status: DB may say "Pending" even when cartridges were partially issued
            int _issuedCheck = (setDto.IssuedBrandNewQty ?? 0) + (setDto.IssuedRefilledQty ?? 0);
            string setStatus = (setDto.Status == "Pending" && _issuedCheck > 0) ? "Partial"
                             : (setDto.Status ?? "N/A");
            string requesterName     = employeeDetail?.EmployeeName ?? "N/A";
            string requesterTitle    = employeeDetail?.TitleDescription ?? string.Empty;
            string requesterDisplayName = string.IsNullOrWhiteSpace(requesterTitle)
                ? requesterName
                : $"{requesterTitle} {requesterName}";
            string companyName       = employeeDetail?.CompanyName  ?? "N/A";
            string branchName        = employeeDetail?.BranchName   ?? "N/A";
            string departmentName    = employeeDetail?.DepartmentName ?? "N/A";
            string distributionMethod = DetermineCartridgeDistributionMethod(setRequests);

            var summaryByModel  = new Dictionary<string, CartridgeExchangeModelSummaryDto>(StringComparer.OrdinalIgnoreCase);
            if (modelSummaries != null)
            {
                foreach (var s in modelSummaries)
                {
                    var key = string.IsNullOrWhiteSpace(s?.CartridgeModel) ? "N/A" : s.CartridgeModel.Trim();
                    if (summaryByModel.TryGetValue(key, out var existing))
                    {
                        existing.ReturnedEmptyQty += s.ReturnedEmptyQty;
                        existing.IssuedFullQty    += s.IssuedFullQty;
                        existing.UnfulfilledQty   += s.UnfulfilledQty;
                    }
                    else
                    {
                        summaryByModel[key] = new CartridgeExchangeModelSummaryDto
                        {
                            CartridgeModel    = key,
                            ReturnedEmptyQty  = s.ReturnedEmptyQty,
                            IssuedFullQty     = s.IssuedFullQty,
                            UnfulfilledQty    = s.UnfulfilledQty
                        };
                    }
                }
            }

            var requestByModel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (setRequests != null)
            {
                foreach (var req in setRequests)
                {
                    if (req == null) continue;
                    var modelKey = !string.IsNullOrWhiteSpace(req.ModelNumber)
                        ? req.ModelNumber.Trim()
                        : (!string.IsNullOrWhiteSpace(req.ItemName) ? req.ItemName.Trim() : "N/A");
                    if (requestByModel.ContainsKey(modelKey)) requestByModel[modelKey] += req.Quantity;
                    else requestByModel[modelKey] = req.Quantity;
                }
            }

            int setIssuedBrandNewQty = setDto.IssuedBrandNewQty ?? 0;
            int setIssuedRefilledQty = setDto.IssuedRefilledQty ?? 0;
            int totalBrandNew = 0, totalRefilled = 0, totalReturnedEmpty = 0, totalPending = 0;

            var modelRowsHtml = new System.Text.StringBuilder();
            foreach (var kvp in requestByModel.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                var model        = string.IsNullOrWhiteSpace(kvp.Key) ? "N/A" : kvp.Key;
                int requestedQty = kvp.Value;
                summaryByModel.TryGetValue(model, out var movementSummary);
                // Returned Empty always equals requested — empties are handed in upfront
                int returnedEmptyQty = requestedQty;
                int brandNewQty, refilledQty;
                if (requestByModel.Count == 1)
                { brandNewQty = setIssuedBrandNewQty; refilledQty = setIssuedRefilledQty; }
                else
                { brandNewQty = movementSummary != null ? movementSummary.IssuedFullQty : 0; refilledQty = 0; }
                int totalIssued = brandNewQty + refilledQty;
                int pendingQty  = Math.Max(0, requestedQty - totalIssued);
                totalReturnedEmpty += returnedEmptyQty;
                totalBrandNew      += brandNewQty;
                totalRefilled      += refilledQty;
                totalPending       += pendingQty;
                modelRowsHtml.AppendLine(
                    $"<tr>" +
                    $"<td style=\"border:1px solid #ddd; padding:10px;\">{model}</td>" +
                    $"<td style=\"border:1px solid #ddd; padding:10px; text-align:center;\">{returnedEmptyQty}</td>" +
                    $"<td style=\"border:1px solid #ddd; padding:10px; text-align:center;\">{brandNewQty}</td>" +
                    $"<td style=\"border:1px solid #ddd; padding:10px; text-align:center;\">{refilledQty}</td>" +
                    $"<td style=\"border:1px solid #ddd; padding:10px; text-align:center;\">{pendingQty}</td>" +
                    $"</tr>");
            }

            int totalIssuedFull = totalBrandNew + totalRefilled;
            string distributionStatus;
            string templateKey;
            if (totalIssuedFull == 0)
            { distributionStatus = "Pending Distribution";    templateKey = "CARTRIDGE_EXCHANGE_UNFULFILLED"; }
            else if (totalPending > 0)
            { distributionStatus = "Partially Distributed";   templateKey = "CARTRIDGE_EXCHANGE_PARTIAL"; }
            else
            { distributionStatus = "Distributed and Dispatched"; templateKey = "CARTRIDGE_EXCHANGE_FULFILLED"; }

            var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "SetCode",            setCode },
                { "CreatedBy",          "IT Department" },
                { "CreatedAt",          createdAt },
                { "SetStatus",          setStatus },
                { "SetRemarks",         setDto.Remarks ?? "" },
                { "ItemCount",          requestByModel.Count.ToString() },
                { "EmployeeName",       requesterName },
                { "RequesterName",        requesterName },
                { "RequesterDisplayName", requesterDisplayName },
                { "RequesterTitle",       requesterTitle },
                { "CompanyName",          companyName },
                { "BranchName",         branchName },
                { "DepartmentName",     departmentName },
                { "DistributionMethod", string.IsNullOrWhiteSpace(distributionMethod) ? "N/A" : distributionMethod },
                { "DistributionStatus", distributionStatus },
                { "ReceivedBy",         string.IsNullOrWhiteSpace(receivedByName) ? "N/A" : receivedByName },
                { "ModelRows",          modelRowsHtml.ToString() },
                { "TotalReturnedEmpty", totalReturnedEmpty.ToString() },
                { "TotalBrandNew",      totalBrandNew.ToString() },
                { "TotalRefilled",      totalRefilled.ToString() },
                { "TotalPending",       totalPending.ToString() }
            };

            // ── Fetch template and render ─────────────────────────────────────────────
            var template = await emailRepo.GetEmailTemplateByKeyAsync(templateKey);
            if (template == null)
                return new CartridgeEmailPreview { Error = $"Email template '{templateKey}' not found." };

            string renderedSubject = SystemEmailNotificationService.Render(template.SubjectTemplate ?? string.Empty, placeholders);
            string renderedBody    = SystemEmailNotificationService.Render(template.BodyTemplate    ?? string.Empty, placeholders);

            return new CartridgeEmailPreview
            {
                SetId     = setId,
                Subject   = renderedSubject,
                Body      = renderedBody,
                ProfileId = activeProfile.ProfileId,
                EmpId     = empId,
                BranchId  = branchId
            };
        }

        /// <summary>
        /// Sends a <see cref="CartridgeEmailPreview"/> (which may have been edited by the user).
        /// Returns null on success, or a human-readable error string on failure.
        /// </summary>
        public static async Task<string> SendFulfillmentEmailWithPreview(CartridgeEmailPreview preview)
        {
            if (preview == null)          return "No email preview data.";
            if (preview.Error != null)    return preview.Error;

            var emailRepo    = new EmailRepository();
            var emailService = new SystemEmailNotificationService(emailRepo);

            var result = await emailService.SendRenderedEmailAsync(
                subject:       preview.Subject,
                body:          preview.Body,
                profileId:     preview.ProfileId,
                empId:         preview.EmpId,
                branchId:      preview.BranchId,
                entityType:    "Set",
                entityId:      preview.SetId,
                sentByUserId:  AppSession.CurrentUserId);

            if (result == null)              return "Email service returned no result.";
            if (result.WasSkipped)           return $"Email was skipped: {result.Message}";
            if (!result.SentSuccessfully)    return $"Email failed: {result.Message}";
            return null; // success
        }

        public static async Task<string> SendFulfillmentEmailForSet(int setId)
        {
            if (setId <= 0)
                return "Invalid Set ID.";

            var emailRepo    = new EmailRepository();
            var emailService = new SystemEmailNotificationService(emailRepo);
            var setRepo      = new SetRepository();

            await EnsureCartridgeEmailTemplatesAsync(emailRepo);

            var profiles      = await emailRepo.GetSmtpProfilesAsync(activeOnly: true);
            var activeProfile = profiles.FirstOrDefault();
            if (activeProfile == null)
            {
                try
                {
                    await emailRepo.LogEmailAsync(new SystemEmailLogDto
                    {
                        TemplateKey  = "CARTRIDGE_EXCHANGE_FULFILLED",
                        Status       = "Skipped",
                        ErrorMessage = "No active SMTP profile found.",
                        ProfileId    = null,
                        EntityType   = "Set",
                        EntityId     = setId,
                        SentDate     = DateTime.Now,
                        SentByUserId = AppSession.CurrentUserId
                    });
                }
                catch { }
                return "No active SMTP profile found. Configure one in Email Settings.";
            }

            var setDto = await setRepo.GetSetByIdAsync(setId);
            if (setDto == null)
                return $"Set {setId} not found in the database.";

            var setItems = await setRepo.GetCartridgeSetItemsAsync(setId);
            if (setItems == null || !setItems.Any())
            {
                try
                {
                    await emailRepo.LogEmailAsync(new SystemEmailLogDto
                    {
                        TemplateKey  = "CARTRIDGE_EXCHANGE_FULFILLED",
                        Status       = "Skipped",
                        ErrorMessage = "No SetItems found for Set.",
                        ProfileId    = activeProfile.ProfileId,
                        EntityType   = "Set",
                        EntityId     = setId,
                        SentDate     = DateTime.Now,
                        SentByUserId = AppSession.CurrentUserId
                    });
                }
                catch { }
                return "No SetItems found for this Set — fulfillment may not have been committed yet.";
            }

            // Resolve first ReqId, ReceivedById, and receiver name for employee details
            int firstReqId = 0;
            string receivedByName = string.Empty;
            using (var con = new System.Data.SqlClient.SqlConnection(Core.DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();
                using (var cmd = new System.Data.SqlClient.SqlCommand(
                    @"SELECT TOP 1 r.ReqId, ISNULL(e.Name, '') AS ReceivedByName
                      FROM dbo.Request r
                      LEFT JOIN dbo.Employee e ON e.EmpId = r.ReceivedById
                      WHERE r.SetId = @SetId
                      ORDER BY r.ReqId", con))
                {
                    cmd.Parameters.AddWithValue("@SetId", setId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            firstReqId    = reader.GetInt32(0);
                            receivedByName = reader.GetString(1);
                        }
                    }
                }
            }

            var employeeDetail = firstReqId > 0
                ? await setRepo.GetEmployeeDetailsForRequestAsync(firstReqId)
                : null;

            int? empId   = employeeDetail?.EmpId;
            if (!empId.HasValue || empId.Value <= 0) empId = null;

            int? branchId      = empId.HasValue ? await GetEmployeeBranchIdAsync(empId.Value) : (int?)null;
            var  setRequests   = await setRepo.GetSetRequestsAsync(setId);
            var  modelSummaries = await setRepo.GetCartridgeExchangeModelSummariesBySetAsync(setId);

            try
            {
                var reqIds = setRequests != null
                    ? string.Join(",", setRequests.Select(r => r.ReqId).Distinct()) : "";
                Logger.LogInfo($"Cartridge email: SetId={setId}, SetCode={setDto?.SetCode ?? ""}, ReqIds=[{reqIds}]");
            }
            catch { }

            var emailResult = await SendCartridgeSetFulfillmentEmail(
                emailService, activeProfile, setDto, setItems,
                setRequests, modelSummaries, employeeDetail, empId, branchId, receivedByName);

            if (emailResult == null)
                return "Email service returned no result.";
            if (emailResult.WasSkipped)
                return $"Email was skipped: {emailResult.Message}";
            if (!emailResult.SentSuccessfully)
                return $"Email failed: {emailResult.Message}";

            return null; // null = success
        }

        /// <summary>
        /// Sends a Set-level Cartridge fulfillment email.
        /// CRITICAL: Set-centric, NOT request-centric. Uses ONLY dbo.SetItem data.
        /// Does NOT reference dbo.Request. Item details come from dbo.SetItem joined with dbo.Item.
        /// </summary>
        private static async Task<SystemEmailNotificationService.EmailSendResult> SendCartridgeSetFulfillmentEmail(
            SystemEmailNotificationService emailService,
            dynamic activeProfile,
            SetDto setDto,
            List<CartridgeSetItemDto> setItems,
            List<SetDetailRequestDto> setRequests,
            List<CartridgeExchangeModelSummaryDto> modelSummaries,
            EmployeeDetailDto employeeDetail,
            int? empId,
            int? branchId,
            string receivedByName)
        {
            // Build Set-level data
            string setCode = setDto.SetCode ?? "N/A";
            string createdBy = "IT Department";
            string createdAt = setDto.CreatedAt.ToString("MM/dd/yyyy HH:mm");
            string setStatus = setDto.Status ?? "N/A";
            string setRemarks = setDto.Remarks ?? "";

            // Employee/Organization data from employee profile (single source of truth)
            string requesterName = employeeDetail?.EmployeeName ?? "N/A";
            string requesterTitle = employeeDetail?.TitleDescription ?? string.Empty;
            string requesterDisplayName = string.IsNullOrWhiteSpace(requesterTitle)
                ? requesterName
                : $"{requesterTitle} {requesterName}";
            string companyName = employeeDetail?.CompanyName ?? "N/A";
            string branchName = employeeDetail?.BranchName ?? "N/A";
            string departmentName = employeeDetail?.DepartmentName ?? "N/A";

            string distributionMethod = DetermineCartridgeDistributionMethod(setRequests);

            var summaryByModel = new Dictionary<string, CartridgeExchangeModelSummaryDto>(StringComparer.OrdinalIgnoreCase);
            if (modelSummaries != null)
            {
                foreach (var s in modelSummaries)
                {
                    var key = string.IsNullOrWhiteSpace(s?.CartridgeModel) ? "N/A" : s.CartridgeModel.Trim();
                    if (summaryByModel.TryGetValue(key, out var existing))
                    {
                        existing.ReturnedEmptyQty += s.ReturnedEmptyQty;
                        existing.IssuedFullQty += s.IssuedFullQty;
                        existing.UnfulfilledQty += s.UnfulfilledQty;
                    }
                    else
                    {
                        summaryByModel[key] = new CartridgeExchangeModelSummaryDto
                        {
                            CartridgeModel = key,
                            ReturnedEmptyQty = s.ReturnedEmptyQty,
                            IssuedFullQty = s.IssuedFullQty,
                            UnfulfilledQty = s.UnfulfilledQty
                        };
                    }
                }
            }

            var requestByModel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (setRequests != null)
            {
                foreach (var req in setRequests)
                {
                    if (req == null)
                        continue;

                    var modelKey = !string.IsNullOrWhiteSpace(req.ModelNumber)
                        ? req.ModelNumber.Trim()
                        : (!string.IsNullOrWhiteSpace(req.ItemName) ? req.ItemName.Trim() : "N/A");

                    if (requestByModel.ContainsKey(modelKey))
                        requestByModel[modelKey] += req.Quantity;
                    else
                        requestByModel[modelKey] = req.Quantity;
                }
            }

            int itemCount = requestByModel.Count;
            int totalReturnedEmpty = 0;
            int totalBrandNew = 0;
            int totalRefilled = 0;
            int totalPending = 0;

            // Get issued quantities by condition from dbo.Set
            int setIssuedBrandNewQty = setDto.IssuedBrandNewQty ?? 0;
            int setIssuedRefilledQty = setDto.IssuedRefilledQty ?? 0;
            int setTotalIssued = setIssuedBrandNewQty + setIssuedRefilledQty;

            var modelRowsHtml = new StringBuilder();
            foreach (var kvp in requestByModel.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                var model = string.IsNullOrWhiteSpace(kvp.Key) ? "N/A" : kvp.Key;
                int requestedQty = kvp.Value;

                summaryByModel.TryGetValue(model, out var movementSummary);

                // Returned Empty always equals requested — empties are handed in upfront
                int returnedEmptyQty = requestedQty;

                // For single-model requests, use Set-level quantities directly
                // For multi-model, distribute proportionally (or use movement summary if available)
                int brandNewQty = 0;
                int refilledQty = 0;

                if (requestByModel.Count == 1)
                {
                    // Single model - use Set quantities directly
                    brandNewQty = setIssuedBrandNewQty;
                    refilledQty = setIssuedRefilledQty;
                }
                else
                {
                    // Multi-model - use movement summary if available
                    int issuedFullQty = movementSummary != null ? movementSummary.IssuedFullQty : 0;
                    // For multi-model, we can't split by condition without additional data
                    // Show total in Brand New column, 0 in Refilled
                    brandNewQty = issuedFullQty;
                    refilledQty = 0;
                }

                int totalIssued = brandNewQty + refilledQty;
                int pendingQty = requestedQty - totalIssued;
                if (pendingQty < 0)
                    pendingQty = 0;

                totalReturnedEmpty += returnedEmptyQty;
                totalBrandNew += brandNewQty;
                totalRefilled += refilledQty;
                totalPending += pendingQty;

                modelRowsHtml.AppendLine(
                    $"<tr>" +
                    $"<td style=\"border:1px solid #ddd; padding:10px;\">{model}</td>" +
                    $"<td style=\"border:1px solid #ddd; padding:10px; text-align:center;\">{returnedEmptyQty}</td>" +
                    $"<td style=\"border:1px solid #ddd; padding:10px; text-align:center;\">{brandNewQty}</td>" +
                    $"<td style=\"border:1px solid #ddd; padding:10px; text-align:center;\">{refilledQty}</td>" +
                    $"<td style=\"border:1px solid #ddd; padding:10px; text-align:center;\">{pendingQty}</td>" +
                    $"</tr>");
            }

            // CRITICAL: Determine distribution status based on ACTUAL exchange results (issued vs pending)
            // NOT based on Set.Status - use actual cartridge exchange transaction data
            int totalIssuedFull = totalBrandNew + totalRefilled;
            string distributionStatus;
            string templateKey;

            if (totalIssuedFull == 0)
            {
                // No items were issued at all
                distributionStatus = "Pending Distribution";
                templateKey = "CARTRIDGE_EXCHANGE_UNFULFILLED";
            }
            else if (totalPending > 0)
            {
                // Some items issued, some pending
                distributionStatus = "Partially Distributed";
                templateKey = "CARTRIDGE_EXCHANGE_PARTIAL";
            }
            else
            {
                // All items issued (totalPending == 0 and totalIssuedFull > 0)
                distributionStatus = "Distributed and Dispatched";
                templateKey = "CARTRIDGE_EXCHANGE_FULFILLED";
            }

            // Build placeholders using Set-level data ONLY
            var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "SetCode", setCode },
                { "CreatedBy", createdBy },
                { "CreatedAt", createdAt },
                { "SetStatus", setStatus },
                { "SetRemarks", setRemarks },
                { "ItemCount", itemCount.ToString() },
                { "EmployeeName", requesterName },
                { "RequesterName", requesterName },
                { "RequesterDisplayName", requesterDisplayName },
                { "RequesterTitle", requesterTitle },
                { "CompanyName", companyName },
                { "BranchName", branchName },
                { "DepartmentName", departmentName },
                { "DistributionMethod", string.IsNullOrWhiteSpace(distributionMethod) ? "N/A" : distributionMethod },
                { "DistributionStatus", distributionStatus },
                { "ReceivedBy", string.IsNullOrWhiteSpace(receivedByName) ? "N/A" : receivedByName },
                { "ModelRows", modelRowsHtml.ToString() },
                { "TotalReturnedEmpty", totalReturnedEmpty.ToString() },
                { "TotalBrandNew", totalBrandNew.ToString() },
                { "TotalRefilled", totalRefilled.ToString() },
                { "TotalPending", totalPending.ToString() }
            };

            return await emailService.SendEmailAsync(
                templateKey: templateKey,
                placeholders: placeholders,
                profileId: activeProfile.ProfileId,
                recipientEmails: null,
                empId: empId,
                branchId: branchId,
                entityType: "Set",
                entityId: setDto.SetId,
                sentByUserId: AppSession.CurrentUserId
            );
        }

        private static async Task<int?> GetEmployeeBranchIdAsync(int empId)
        {
            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    await con.OpenAsync();
                    using (var cmd = new SqlCommand("SELECT BranchId FROM dbo.Employee WHERE EmpId = @EmpId", con))
                    {
                        cmd.Parameters.AddWithValue("@EmpId", empId);
                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                            return Convert.ToInt32(result);
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static async Task EnsureCartridgeEmailTemplatesAsync(EmailRepository emailRepo)
        {
            await EnsureTemplateAsync(emailRepo,
                "CARTRIDGE_EXCHANGE_FULFILLED",
                "Cartridge Exchange Fulfilled – {SetCode}",
                BuildSetCartridgeEmailBody("Cartridge Exchange – Fulfilled", "#2ecc71", "✅ All cartridges have been successfully issued."));

            await EnsureTemplateAsync(emailRepo,
                "CARTRIDGE_EXCHANGE_PARTIAL",
                "Cartridge Exchange Partially Fulfilled – {SetCode}",
                BuildSetCartridgeEmailBody("Cartridge Exchange – Partially Fulfilled", "#f39c12", "⚠️ Some cartridges are pending fulfillment due to insufficient stock."));

            await EnsureTemplateAsync(emailRepo,
                "CARTRIDGE_EXCHANGE_UNFULFILLED",
                "Cartridge Exchange Pending – {SetCode}",
                BuildSetCartridgeEmailBody("Cartridge Exchange – Pending Fulfillment", "#e74c3c", "❌ No cartridges were issued due to insufficient stock. All returned cartridges are pending fulfillment."));
        }

        private static async Task EnsureTemplateAsync(EmailRepository emailRepo, string key, string subject, string body)
        {
            var existing = await emailRepo.GetEmailTemplateByKeyAsync(key);

            if (existing != null)
            {
                existing.SubjectTemplate = subject;
                existing.BodyTemplate = body;
                existing.IsHtml = true;
                existing.IsActive = true;
                await emailRepo.SaveEmailTemplateAsync(existing);
                return;
            }

            await emailRepo.SaveEmailTemplateAsync(new EmailTemplateDto
            {
                TemplateId = 0,
                TemplateKey = key,
                SubjectTemplate = subject,
                BodyTemplate = body,
                IsHtml = true,
                IsActive = true
            });
        }

        /// <summary>
        /// Builds a Set-level HTML email body for Cartridge exchanges.
        /// CRITICAL: Set-centric, NOT request-centric. Displays SetItems ONLY.
        /// Does NOT reference dbo.Request. All item data from dbo.SetItem + dbo.Item.
        /// </summary>
        private static string BuildSetCartridgeEmailBody(string title, string accentColor, string statusMessage)
        {
            return
                "<!DOCTYPE html>" +
                "<html>" +
                "<head><meta charset=\"UTF-8\"></head>" +
                "<body style=\"margin:0; padding:20px; font-family:'Segoe UI',Arial,sans-serif; font-size:14px; color:#333; background:#f5f7fa;\">" +

                "<!-- Container -->" +
                "<div style=\"max-width:900px; margin:0 auto; background:#ffffff; border-radius:8px; box-shadow:0 2px 8px rgba(0,0,0,0.1); overflow:hidden;\">" +

                "<!-- Header -->" +
                $"<div style=\"background:{accentColor}; color:#ffffff; padding:24px; text-align:center;\">" +
                $"<h1 style=\"margin:0; font-size:24px; font-weight:600;\">{title}</h1>" +
                $"<p style=\"margin:8px 0 0 0; font-size:16px; opacity:0.9;\">{{SetCode}}</p>" +
                "</div>" +

                "<!-- Content -->" +
                "<div style=\"padding:32px;\">" +

                "<!-- Status Message -->" +
                $"<div style=\"margin:0 0 24px 0; padding:16px 20px; background:#f8f9fa; border-left:5px solid {accentColor}; border-radius:4px;\">" +
                $"<p style=\"margin:0; font-size:15px; line-height:1.5;\">{statusMessage}</p>" +
                "</div>" +

                "<!-- Set Details Section -->" +
                "<div style=\"margin:0 0 28px 0;\">" +
                "<h2 style=\"margin:0 0 16px 0; font-size:18px; color:#444; border-bottom:2px solid #e0e0e0; padding-bottom:8px;\">Set Details</h2>" +
                "<table style=\"width:100%; border-spacing:0;\">" +
                "<tr><td style=\"padding:8px 0; color:#666; width:180px;\"><strong>Set Code:</strong></td><td style=\"padding:8px 0;\">{SetCode}</td></tr>" +
                "<tr><td style=\"padding:8px 0; color:#666;\"><strong>Distribution Method:</strong></td><td style=\"padding:8px 0;\">{DistributionMethod}</td></tr>" +
                "<tr><td style=\"padding:8px 0; color:#666;\"><strong>Received By:</strong></td><td style=\"padding:8px 0;\">{ReceivedBy}</td></tr>" +
                "<tr><td style=\"padding:8px 0; color:#666;\"><strong>Employee:</strong></td><td style=\"padding:8px 0;\">{RequesterName}</td></tr>" +
                "<tr><td style=\"padding:8px 0; color:#666;\"><strong>Title:</strong></td><td style=\"padding:8px 0;\">{RequesterTitle}</td></tr>" +
                "<tr><td style=\"padding:8px 0; color:#666;\"><strong>Company:</strong></td><td style=\"padding:8px 0;\">{CompanyName}</td></tr>" +
                "<tr><td style=\"padding:8px 0; color:#666;\"><strong>Branch:</strong></td><td style=\"padding:8px 0;\">{BranchName}</td></tr>" +
                "<tr><td style=\"padding:8px 0; color:#666;\"><strong>Department:</strong></td><td style=\"padding:8px 0;\">{DepartmentName}</td></tr>" +
                "<tr><td style=\"padding:8px 0; color:#666;\"><strong>Item Count:</strong></td><td style=\"padding:8px 0;\">{ItemCount}</td></tr>" +
                "<tr><td style=\"padding:8px 0; color:#666;\"><strong>Created By:</strong></td><td style=\"padding:8px 0;\">{CreatedBy}</td></tr>" +
                "<tr><td style=\"padding:8px 0; color:#666;\"><strong>Created At:</strong></td><td style=\"padding:8px 0;\">{CreatedAt}</td></tr>" +
                "<tr><td style=\"padding:8px 0; color:#666;\"><strong>Set Status:</strong></td><td style=\"padding:8px 0;\">{SetStatus}</td></tr>" +
                "<tr><td style=\"padding:8px 0; color:#666;\"><strong>Distribution Status:</strong></td><td style=\"padding:8px 0;\"><strong>{DistributionStatus}</strong></td></tr>" +
                "<tr><td style=\"padding:8px 0; color:#666;\"><strong>Remarks:</strong></td><td style=\"padding:8px 0;\">{SetRemarks}</td></tr>" +
                "{NotesRow}" +
                "</table>" +
                "</div>" +

                "<!-- Cartridge Summary -->" +
                "<div style=\"margin:0 0 28px 0;\">" +
                "<h2 style=\"margin:0 0 16px 0; font-size:18px; color:#444; border-bottom:2px solid #e0e0e0; padding-bottom:8px;\">Cartridge Summary</h2>" +
                "<div style=\"overflow-x:auto;\">" +
                "<table style=\"width:100%; border-collapse:collapse; font-size:13px;\">" +
                "<thead>" +
                "<tr style=\"background:#f1f3f5;\">" +
                "<th style=\"border:1px solid #ddd; padding:10px; text-align:left;\">Cartridge Model</th>" +
                "<th style=\"border:1px solid #ddd; padding:10px; text-align:center;\">Returned Empty</th>" +
                "<th style=\"border:1px solid #ddd; padding:10px; text-align:center;\">Issued Brand New</th>" +
                "<th style=\"border:1px solid #ddd; padding:10px; text-align:center;\">Issued Refilled</th>" +
                "<th style=\"border:1px solid #ddd; padding:10px; text-align:center;\">Pending</th>" +
                "</tr>" +
                "</thead>" +
                "<tbody>" +
                "{ModelRows}" +
                "</tbody>" +
                "<tfoot>" +
                "<tr style=\"background:#fafbfc; font-weight:600;\">" +
                "<td style=\"border:1px solid #ddd; padding:10px;\">TOTAL</td>" +
                "<td style=\"border:1px solid #ddd; padding:10px; text-align:center;\">{TotalReturnedEmpty}</td>" +
                "<td style=\"border:1px solid #ddd; padding:10px; text-align:center;\">{TotalBrandNew}</td>" +
                "<td style=\"border:1px solid #ddd; padding:10px; text-align:center;\">{TotalRefilled}</td>" +
                "<td style=\"border:1px solid #ddd; padding:10px; text-align:center;\">{TotalPending}</td>" +
                "</tr>" +
                "</tfoot>" +
                "</table>" +
                "</div>" +
                "</div>" +

                "<!-- Footer Note -->" +
                "<div style=\"margin:28px 0 0 0; padding:16px; background:#f0f4f8; border-radius:4px; font-size:13px; color:#666; line-height:1.6;\">" +
                "<p style=\"margin:0;\">If you have any questions, please contact IT support.</p>" +
                "</div>" +

                "</div>" + // End content
                "</div>" + // End container
                "</body>" +
                "</html>";
        }

        private static string DetermineCartridgeDistributionMethod(List<SetDetailRequestDto> setRequests)
        {
            if (setRequests == null || setRequests.Count == 0)
                return null;

            var descriptions = setRequests
                .Select(r => r.Description)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .ToList();

            if (descriptions.Any(d => d.IndexOf("PICKUP", StringComparison.OrdinalIgnoreCase) >= 0))
                return "PICKUP";

            if (descriptions.Any(d => d.IndexOf("DELIVERY", StringComparison.OrdinalIgnoreCase) >= 0))
                return "DELIVERY";

            return null;
        }

        #region Auto-Refresh Timer

        private void StartAutoRefreshTimer()
        {
            _autoRefreshSecondsLeft = 30;
            _autoRefreshTimer = new Timer { Interval = 1000 }; // 1-second ticks for countdown display
            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
            _autoRefreshTimer.Start();
        }

        private void ResetAutoRefreshTimer()
        {
            if (_autoRefreshTimer != null)
            {
                _autoRefreshSecondsLeft = 30;
                UpdateRefreshCountdownLabel();
                _autoRefreshTimer.Stop();
                _autoRefreshTimer.Start();
            }
        }

        private void AutoRefreshTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Skip if form is not visible or is minimized
                if (!this.Visible || this.WindowState == FormWindowState.Minimized)
                    return;

                // Pause while a request is selected or fulfillment is in progress
                if (_selectedRequest != null || _fulfillmentInProgress)
                {
                    UpdateRefreshCountdownLabel(paused: true);
                    return;
                }

                _autoRefreshSecondsLeft--;
                UpdateRefreshCountdownLabel();

                if (_autoRefreshSecondsLeft <= 0)
                {
                    _autoRefreshSecondsLeft = 30;
                    LoadPendingCartridgeRequests();
                }
            }
            catch
            {
                // Never let exceptions escape from the timer tick handler
            }
        }

        private void UpdateRefreshCountdownLabel(bool paused = false)
        {
            if (_lblRefreshCountdown == null)
                return;

            if (paused)
            {
                _lblRefreshCountdown.Text = "⏸ paused";
                _lblRefreshCountdown.ForeColor = Color.FromArgb(160, 120, 40);
            }
            else
            {
                _lblRefreshCountdown.Text = $"⏱ {_autoRefreshSecondsLeft}s";
                _lblRefreshCountdown.ForeColor = _autoRefreshSecondsLeft <= 5
                    ? Color.FromArgb(200, 70, 50)   // Red-ish when imminent
                    : Color.FromArgb(130, 130, 130);
            }
        }

        #endregion

        private void FinalizeCartridgeRequest()
        {
            if (_isMultiModelMode)
            {
                FinalizeMultiModelRequest();
                return;
            }

            int issuedBrandNewQty = nudIssuedBrandNewQty != null ? (int)nudIssuedBrandNewQty.Value : 0;
            int issuedRefilledQty = nudIssuedRefilledQty != null ? (int)nudIssuedRefilledQty.Value : 0;
            int issuedQty = issuedBrandNewQty + issuedRefilledQty;
            int requestedQty = _selectedRequest.Quantity;
            int returnedEmptyQty = requestedQty;
            int unfulfilledQty = returnedEmptyQty - issuedQty;

            string statusMessage;
            string title;
            MessageBoxIcon icon;

            if (issuedQty == 0)
            {
                statusMessage = "Request recorded as UNFULFILLED.\nAll returned cartridges are pending fulfillment.";
                title = "Exchange Recorded - Unfulfilled";
                icon = MessageBoxIcon.Warning;
            }
            else if (unfulfilledQty > 0)
            {
                statusMessage = "Request PARTIALLY FULFILLED.\nRemaining cartridges are pending fulfillment.";
                title = "Exchange Recorded - Partially Fulfilled";
                icon = MessageBoxIcon.Information;
            }
            else
            {
                statusMessage = "Request FULLY FULFILLED.";
                title = "Fulfillment Complete";
                icon = MessageBoxIcon.Information;
            }

            string displayModel = _selectedRequest.TypedModelNumber ?? _selectedRequest.ModelNumber ?? "Unknown Model";

            MessageBox.Show(
                $"{statusMessage}\n\n" +
                $"• Requester: {_selectedRequest.EmployeeName}\n" +
                $"• Model: {displayModel}\n" +
                $"• Returned Empty: {returnedEmptyQty}\n" +
                $"• Issued Full: {issuedQty}\n" +
                $"• Pending: {unfulfilledQty}",
                title,
                MessageBoxButtons.OK,
                icon);

            LoadPendingCartridgeRequests();
            ClearExchangePanel();
        }

        private void FinalizeMultiModelRequest()
        {
            int totalRequested = _multiModelRequests?.Sum(r => r.Quantity) ?? 0;
            int totalIssued = _multiModelRequests?.Sum(r =>
                _multiModelState != null && _multiModelState.TryGetValue(r.ReqId, out var rs)
                    ? (rs.NudBrandNew != null ? (int)rs.NudBrandNew.Value : 0)
                      + (rs.NudRefilled != null ? (int)rs.NudRefilled.Value : 0)
                    : 0) ?? 0;
            int totalUnfulfilled = totalRequested - totalIssued;

            string statusMessage;
            string title;
            MessageBoxIcon icon;

            if (totalIssued == 0)
            {
                statusMessage = "All requests recorded as UNFULFILLED.\nAll returned cartridges are pending.";
                title = "Exchange Recorded - Unfulfilled";
                icon = MessageBoxIcon.Warning;
            }
            else if (totalUnfulfilled > 0)
            {
                statusMessage = "Requests PARTIALLY FULFILLED.\nRemaining cartridges are pending.";
                title = "Exchange Recorded - Partially Fulfilled";
                icon = MessageBoxIcon.Information;
            }
            else
            {
                statusMessage = "All requests FULLY FULFILLED.";
                title = "Fulfillment Complete";
                icon = MessageBoxIcon.Information;
            }

            var sb = new StringBuilder();
            sb.AppendLine(statusMessage);
            sb.AppendLine();
            sb.AppendLine($"• Requester: {_selectedRequest?.EmployeeName ?? "N/A"}");
            sb.AppendLine($"• Models: {_multiModelRequests?.Count ?? 0}");
            sb.AppendLine();

            if (_multiModelRequests != null && _multiModelState != null)
            {
                foreach (var req in _multiModelRequests)
                {
                    string model = req.TypedModelNumber ?? req.ModelNumber ?? "Unknown";
                    int issued = 0;
                    if (_multiModelState.TryGetValue(req.ReqId, out var rs))
                        issued = (rs.NudBrandNew != null ? (int)rs.NudBrandNew.Value : 0)
                               + (rs.NudRefilled != null ? (int)rs.NudRefilled.Value : 0);
                    int pending = req.Quantity - issued;
                    sb.AppendLine($"  Req #{req.ReqId} {model}: Issued={issued}, Pending={pending}");
                }
            }

            MessageBox.Show(sb.ToString(), title, MessageBoxButtons.OK, icon);

            LoadPendingCartridgeRequests();
            ClearExchangePanel();
        }

        #endregion

        #region Event Handlers

        // Resize handler is now in the constructor with debouncing

        // Old event handlers removed - session blocks handle selection internally

        private void BtnOpenRequest_Click(object sender, EventArgs e)
        {
            if (_selectedRequest == null)
            {
                MessageBox.Show("Please select a request to open.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Request already selected via session block - just confirm it's loaded
            OnCartridgeRequestSelected(_selectedRequest);
        }

        private void BtnFulfill_Click(object sender, EventArgs e)
        {
            // CRITICAL FIX: Don't call Focus() or Validate() on NumericUpDown
            // These calls can interfere with the value and cause resets to 0
            // The value is already committed when the user clicks the button
            ProcessFulfillment();
        }

        private void ProcessFulfillment()
        {
            if (!ValidateCartridgeExchange())
                return;

            _fulfillmentInProgress = true;
            try
            {
                string confirmMessage;
                if (_isMultiModelMode && _multiModelRequests != null && _multiModelRequests.Count > 1)
                {
                    var modelList = string.Join("\n", _multiModelRequests.Select(r =>
                    {
                        string model = r.TypedModelNumber ?? r.ModelNumber ?? "Unknown";
                        int issued = 0;
                        if (_multiModelState != null && _multiModelState.TryGetValue(r.ReqId, out var rs))
                            issued = (rs.NudBrandNew != null ? (int)rs.NudBrandNew.Value : 0)
                                   + (rs.NudRefilled != null ? (int)rs.NudRefilled.Value : 0);
                        return $"  • Req #{r.ReqId} {model}: {issued} / {r.Quantity} pcs";
                    }));
                    confirmMessage =
                        $"Fulfill all {_multiModelRequests.Count} models in this session?\n\n" +
                        $"{modelList}\n\n" +
                        "This will record cartridge movements for all models above.";
                }
                else
                {
                    confirmMessage =
                        $"Fulfill request #{_selectedRequest.ReqId}?\n\n" +
                        $"This will:\n" +
                        $"• Record cartridge movements\n" +
                        $"• Update cartridge statuses\n" +
                        $"• Mark request as fulfilled\n" +
                        $"• Group with related multi-model requests (if applicable)";
                }

                var result = MessageBox.Show(confirmMessage, "Confirm Fulfillment",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                    return;

                if (CommitCartridgeExchange())
                {
                    FinalizeCartridgeRequest();
                }
            }
            finally
            {
                _fulfillmentInProgress = false;
                ResetAutoRefreshTimer();
            }
        }

        private void BtnClear_Click(object sender, EventArgs e)
        {
            ClearExchangePanel();
        }

        private void BtnForceDelete_Click(object sender, EventArgs e)
        {
            if (_selectedRequest == null)
            {
                MessageBox.Show("No request selected.", "Force Delete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var finalWarning = MessageBox.Show(
                $"🚨 FINAL WARNING - FORCE DELETE 🚨\n\n" +
                $"This will PERMANENTLY DELETE cartridge request #{_selectedRequest.ReqId}.\n\n" +
                $"This will also:\n" +
                $"• Restore issued cartridge stock (if any)\n" +
                $"• Remove auto-registered returned empty cartridges (if any)\n" +
                $"• Delete child rows in dbo.CartridgeRequestModel (if any)\n\n" +
                $"THIS CANNOT BE UNDONE!\n\n" +
                $"Only proceed if this was a DATA ENTRY ERROR.\n\n" +
                $"Click 'OK' to confirm.",
                "⚠️ FORCE DELETE CONFIRMATION ⚠️",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Stop,
                MessageBoxDefaultButton.Button2);

            if (finalWarning != DialogResult.OK)
                return;

            int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
            var (success, message) = _repository.ForceDeleteCartridgeRequest(_selectedRequest.ReqId, userId);

            if (!success)
            {
                MessageBox.Show(message, "Force Delete Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show(message, "Force Delete Completed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);

            LoadPendingCartridgeRequests();
            ClearExchangePanel();
        }

        private void ClearExchangePanel()
        {
            _selectedRequest = null;
            _isMultiModelMode = false;
            _multiModelState = null;
            _multiModelRequests = null;

            txtReqId.Text = "";
            txtRequester.Text = "";
            txtModel.Text = "";
            txtQuantity.Text = "";
            txtCondition.Text = "";
            txtGoodEmptyQty.Text = "";
            txtDamagedEmptyQty.Text = "";
            txtCompany.Text = "";
            txtBranch.Text = "";
            txtDepartment.Text = "";
            txtDistributionMethod.Text = "";
            txtReceivedBy.Text = "";
            txtAdditionalRemarks.Text = "";

            pnlReturnedCartridges.Controls.Clear();
            pnlIssuedCartridges.Controls.Clear();
            _issuedDropdowns.Clear();

            nudIssuedBrandNewQty = null;
            nudIssuedRefilledQty = null;
            lblIssuedStock = null;
            lblReturnedEmptyQty = null;
            lblIssuedFullQty = null;
            lblUnfulfilledQty = null;
            lblFulfillmentStatus = null;
            txtAutoRemarks = null;
            _availableBrandNewStock = 0;
            _availableRefilledStock = 0;

            btnFulfill.Enabled = false;
            if (btnForceDelete != null)
                btnForceDelete.Enabled = false;
        }

        #endregion

        // Per-model state for multi-model fulfillment mode
        private class MultiModelRowState
        {
            public int RequestedQty;
            public int AvailableBrandNew;
            public int AvailableRefilled;
            public NumericUpDown NudBrandNew;
            public NumericUpDown NudRefilled;
            public Label LblIssuedFull;
            public Label LblUnfulfilled;
            public Label LblStatus;
            public TextBox TxtRemarks;
        }
    }

    /// <summary>
    /// DTO for cartridge requests displayed in the grid.
    /// Each row shows its own ReqId.
    /// </summary>
    public class CartridgeRequestDto
    {
        public int ReqId { get; set; }
        public int ItemId { get; set; }
        public int? CartridgeModelId { get; set; }  // FK to dbo.CartridgeModel (from Item table)
        public string ItemName { get; set; }
        public string ModelNumber { get; set; }
        public int? RequestModelId { get; set; }
        public string TypedModelNumber { get; set; }
        public int Quantity { get; set; }
        public string ConditionType { get; set; }
        public string PhysicalCondition { get; set; }
        public int GoodEmptyQty { get; set; }
        public int DamagedEmptyQty { get; set; }

        // Raw request description (e.g. "[PORTAL] [MODEL:HP 83A] PICKUP → Manila, IT")
        // Used to determine fulfillment method (PICKUP / DELIVERY).
        public string Description { get; set; }

        // Computed display properties for grid binding
        public string WithCartridgeDisplay =>
            ConditionType != null && ConditionType.Contains("Without Cartridge") ? "No" :
            ConditionType != null && ConditionType.Contains("With Cartridge") ? "Yes" :
            ConditionType ?? "N/A";

        public string Status { get; set; }
        public DateTime DateCreated { get; set; }
        public int EmpId { get; set; }
        public string EmployeeName { get; set; }
        public string CompanyName { get; set; }  // Added Company field
        public string BranchName { get; set; }
        public string DepartmentName { get; set; }

        // SubmissionSessionId from Request Portal (NULL for legacy requests)
        public Guid? SubmissionSessionId { get; set; }

        // Total number of models in the original submission (including fulfilled ones).
        // Populated by the SQL query so no separate round-trip is needed.
        // Used by GroupRequestsIntoSessions to determine IsMultiModel reliably.
        public int OriginalSubmissionCount { get; set; }

        // Request fulfillment details (from portal submission)
        public string DistributionMethod { get; set; }
        public string ReceivedByName { get; set; }
        public string AdditionalRemarks { get; set; }

        // Visual grouping properties (not displayed, used for styling)
        public string SessionKey { get; set; }
        public int SessionGroupIndex { get; set; }
    }

    /// <summary>
    /// Groups related cartridge requests for visual styling (not for hiding rows).
    /// </summary>
    public class RequestSessionGroup
    {
        public string SessionKey { get; set; }
        public int GroupIndex { get; set; }
        public int EmpId { get; set; }
        public string EmployeeName { get; set; }
        public string CompanyName { get; set; }
        public string BranchName { get; set; }
        public string DepartmentName { get; set; }
        public string BranchDept { get; set; }
        public DateTime DateCreated { get; set; }
        public int ModelCount { get; set; }
        public int TotalQuantity { get; set; }
        public List<CartridgeRequestDto> Requests { get; set; }
        public bool IsMultiModel { get; set; }
    }

    public class CartridgeSelectionItem
    {
        public int ItemId { get; set; }
        public string SerialNumber { get; set; }
        public string ModelNumber { get; set; }
        public string DisplayName { get; set; }
    }
}
