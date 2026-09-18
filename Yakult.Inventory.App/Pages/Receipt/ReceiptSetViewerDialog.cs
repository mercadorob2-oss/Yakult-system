using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Pages.Set;
using Yakult.Inventory.App.Pages.Invoice;

namespace Yakult.Inventory.App.Pages.Receipt
{
         public class ReceiptSetViewerDialog : Form
         {
            public enum ReceiptSlot
            {
                SI = 0,
                DR = 1,
                PO = 2
            }

             private readonly ReceiptSetRepository _repository;
             private int? _setId;
             private int _receiptSetId;

        private Label _lblTitle;
        private Label _lblImageInfo;
        private Label _lblSlotName;
        private Label _lblSupplier;
        private Label _lblSiNumber;
            private Label _lblDrNumber;
            private Label _lblPoNumber;

            private ComboBox _cmbSupplier;
            private TextBox _txtSiNumber;
            private TextBox _txtDrNumber;
            private TextBox _txtPoNumber;

        private PictureBox _picPreview;
        private Panel _rightPanel;
        private TabControl _tabSlots;
        private TabControl _tabPeriods;
        private TabPage _tpCurrent;
        private TabPage _tpPrevious;
        private TabPage _tpHistory;
        private Panel _receiptHostPanel;
        private ListView _lvHistory;
        private Panel _historyDetailsPanel;
        private Label _lblHistorySupplierValue;
        private Label _lblHistoryDocsValue;
        private Label _lblHistoryCreatedValue;
        private Label _lblHistoryModifiedValue;
        private Label _lblHistoryUserValue;
        private Label _lblHistoryChangesValue;
        private PictureBox _histThumbSi;
        private PictureBox _histThumbDr;
        private PictureBox _histThumbPo;
        private ContextMenuStrip _historyMenu;
        private ToolStripMenuItem _miHistoryOpen;
        private ToolStripMenuItem _miHistoryCopy;
        private ToolStripMenuItem _miHistoryMakeCurrent;
        private Dictionary<int, string> _historyUserNames = new Dictionary<int, string>();
        private Label _lblCoverage;
        private Label _lblRenewalIndicator;
        private bool _updatingTabs;
        private PictureBox _thumbSi;
        private PictureBox _thumbDr;
        private PictureBox _thumbPo;

        private Button _btnAttachSi;
        private Button _btnAttachDr;
        private Button _btnAttachPo;
        private ContextMenuStrip _imageMenu;
        private ToolStripMenuItem _miImageOpen;
        private ToolStripMenuItem _miImageCopy;
        private ToolStripMenuItem _miImageCopyPath;
        private ToolStripMenuItem _miImageSaveAs;
        private int _imageMenuSlotIndex;
        private Button _btnFullscreen;
        private Button _btnNewReceipt;
        private Button _btnRevert;
        private Button _btnSave;
        private Button _btnPrev;
        private Button _btnNext;
        private Button _btnClose;

        private Button _btnOpenLinkedSet;
        private Button _btnUnlinkSet;
        private Button _btnCopyLocalToDb;
        private ComboBox _cmbLinkedSets;
        private Label _lblLinkedSet;
        private LinkLabel _lnkInvoiceReport;

        private Panel _previewScrollPanel;
        private Panel _emptyStatePanel;
        private Label _emptyStateTitle;
        private Label _emptyStateHint;

        private string _siImagePath;
        private string _drImagePath;
        private string _poImagePath;
        private byte[] _siImageBytes;
        private byte[] _drImageBytes;
        private byte[] _poImageBytes;
        private int _currentSlotIndex; // 0 = SI, 1 = DR, 2 = PO
        private bool _readOnly;
        private bool _forceReadOnly;
        private bool _isDirty;
        private bool _suppressDirty;
        private double _zoom = 1.0;
        private bool _pendingZoomFit;
        private bool _panning;
        private Point _panMouseOrigin;
        private Point _panScrollOrigin;
        private ToolTip _toolTip;
        private readonly List<string> _tempImageFiles = new List<string>();
        private bool _vendorsLoaded;

        private ReceiptSetRepository.CoveragePeriod _currentCoverage;
        private ReceiptSetRepository.CoveragePeriod _previousCoverage;
        private List<ReceiptSetRepository.CoveragePeriod> _historyCoverages = new List<ReceiptSetRepository.CoveragePeriod>();
        private ReceiptSetRepository.CoveragePeriod _activeCoverage;
        private bool _coveragePromptShown;
        private int _lastPeriodTabIndex;
        private bool _coverageTabsEnabled;

        private int? _pendingRenewedFromReceiptSetId;
        private int? _unlinkedHeadReceiptSetId;
        private int? _unlinkedPreviousReceiptSetId;
        private List<ReceiptSetDto> _unlinkedChainReceipts = new List<ReceiptSetDto>();
        private List<ReceiptSetDto> _unlinkedHistoryReceipts = new List<ReceiptSetDto>();

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public ReceiptSetViewerDialog() : this((int?)null)
        {
        }

        public ReceiptSetViewerDialog(int? setId, ReceiptSlot? initialSlot = null)
         {
             _repository = new ReceiptSetRepository();
             _setId = setId;
             _readOnly = false;
                 BuildUi();
              Shown -= ReceiptSetViewerDialog_Shown;
              Shown += ReceiptSetViewerDialog_Shown;
              Resize -= ReceiptSetViewerDialog_Resize;
              Resize += ReceiptSetViewerDialog_Resize;
 
              if (_setId.HasValue)
              {
                  LoadExistingForSet(_setId.Value);
              }
             else
             {
                 _currentSlotIndex = 0;
             ShowCurrentImage();
            }

            if (initialSlot.HasValue)
            {
                _currentSlotIndex = (int)initialSlot.Value;
                ShowCurrentImage();
            }
         }

        public ReceiptSetViewerDialog(ReceiptSetDto existing, ReceiptSlot? initialSlot = null)
         {
             _repository = new ReceiptSetRepository();
             _readOnly = false;

            // Resolve Set context BEFORE BuildUi so period tabs are visible when a Set is linked.
            _receiptSetId = existing != null ? existing.ReceiptSetId : 0;
            _setId = existing != null ? existing.SetId : null;

            if (!_setId.HasValue && _receiptSetId > 0)
            {
                try
                {
                    var linked = _repository.GetLinkedSets(_receiptSetId);
                    if (linked != null && linked.Count > 0)
                        _setId = linked[0].SetId;
                }
                catch
                {
                    // ignore lookup failures; viewer still works without period tabs
                }
            }

             BuildUi();
            Shown -= ReceiptSetViewerDialog_Shown;
            Shown += ReceiptSetViewerDialog_Shown;
            Resize -= ReceiptSetViewerDialog_Resize;
            Resize += ReceiptSetViewerDialog_Resize;

            // Refresh from DB by ReceiptSetId so entrypoints that pass a "light" DTO still show images.
            ReceiptSetDto source = existing;
            if (_receiptSetId > 0)
            {
                try
                {
                    var fresh = _repository.GetByReceiptSetId(_receiptSetId);
                    if (fresh != null)
                    {
                        fresh.SetId = fresh.SetId ?? existing?.SetId ?? _setId;
                        fresh.SetCode = string.IsNullOrWhiteSpace(fresh.SetCode) ? existing?.SetCode : fresh.SetCode;

                        fresh.SiImagePath = string.IsNullOrWhiteSpace(fresh.SiImagePath) ? existing?.SiImagePath : fresh.SiImagePath;
                        fresh.DrImagePath = string.IsNullOrWhiteSpace(fresh.DrImagePath) ? existing?.DrImagePath : fresh.DrImagePath;
                        fresh.PoImagePath = string.IsNullOrWhiteSpace(fresh.PoImagePath) ? existing?.PoImagePath : fresh.PoImagePath;

                        fresh.SiImage = (fresh.SiImage == null || fresh.SiImage.Length == 0) ? existing?.SiImage : fresh.SiImage;
                        fresh.DrImage = (fresh.DrImage == null || fresh.DrImage.Length == 0) ? existing?.DrImage : fresh.DrImage;
                        fresh.PoImage = (fresh.PoImage == null || fresh.PoImage.Length == 0) ? existing?.PoImage : fresh.PoImage;

                        source = fresh;
                    }
                }
                catch
                {
                    source = existing;
                }
            }

            _setId = source != null ? (source.SetId ?? _setId) : _setId;
            _receiptSetId = source != null ? source.ReceiptSetId : _receiptSetId;

            if (source != null)
            {
                _suppressDirty = true;
                _cmbSupplier.Text = source.Supplier ?? string.Empty;
                _txtSiNumber.Text = source.SiNumber ?? string.Empty;
                _txtDrNumber.Text = source.DrNumber ?? string.Empty;
                _txtPoNumber.Text = source.PoNumber ?? string.Empty;

                _siImagePath = source.SiImagePath;
                _drImagePath = source.DrImagePath;
                _poImagePath = source.PoImagePath;
                _siImageBytes = source.SiImage;
                _drImageBytes = source.DrImage;
                _poImageBytes = source.PoImage;
                _suppressDirty = false;
                SetDirty(false);
                RefreshLinkedSetsUi();
                UpdateSlotActionButtons();

                if (HasImageForSlot(0))
                {
                    _currentSlotIndex = 0;
                }
                else if (HasImageForSlot(1))
                {
                    _currentSlotIndex = 1;
                }
                else if (HasImageForSlot(2))
                {
                    _currentSlotIndex = 2;
                }
                else
                {
                    _currentSlotIndex = 0;
                }
            }
             else
             {
                 _currentSlotIndex = 0;
             }

            if (initialSlot.HasValue)
            {
                _currentSlotIndex = (int)initialSlot.Value;
            }

              ShowCurrentImage();

            if (_setId.HasValue)
            {
                InitializeCoverageTabsForSet(_setId.Value);
            }
            else if (_receiptSetId > 0)
            {
                InitializeUnlinkedRenewalTabsForReceiptSet(_receiptSetId);
            }

          }

        public ReceiptSetViewerDialog(ReceiptSetDto existing, bool readOnly, ReceiptSlot? initialSlot = null)
            : this(existing, initialSlot)
         {
             _forceReadOnly = readOnly;
             _readOnly = readOnly;
             ApplyReadOnlyState();
        }

        private void BuildUi()
        {
            Text = "Receipt Set Viewer";
            Width = 1100;
            Height = 720;
            MinimumSize = new Size(1100, 720);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            MinimizeBox = false;
            MaximizeBox = false;

            var appBg = Color.FromArgb(245, 247, 250);
            var cardBg = Color.White;
            var surfaceBg = Color.FromArgb(250, 251, 252);
            var border = Color.FromArgb(215, 223, 233);
            var textColor = Color.FromArgb(18, 22, 28);
            var muted = Color.FromArgb(90, 100, 110);
            var accent = Color.FromArgb(45, 120, 210);

            BackColor = appBg;

            void ApplyRoundedRegion()
            {
                var handle = CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 22, 22);
                try
                {
                    Region = Region.FromHrgn(handle);
                }
                finally
                {
                    DeleteObject(handle);
                }
            }

            ApplyRoundedRegion();
            SizeChanged += (s, e) => ApplyRoundedRegion();

            void EnableDrag(Control target)
            {
                if (target == null)
                    return;

                target.MouseDown += (s, ev) =>
                {
                    if (ev.Button != MouseButtons.Left)
                        return;

                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                };
            }

            var rootCard = new ReaLTaiizor.Controls.Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                BackColor = appBg,
                EdgeColor = appBg,
                SmoothingType = SmoothingMode.HighQuality
            };

            var cardSurface = new ReaLTaiizor.Controls.Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                BackColor = cardBg,
                EdgeColor = border,
                SmoothingType = SmoothingMode.HighQuality
            };
            rootCard.Controls.Add(cardSurface);

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(24, 18, 24, 18),
                Margin = new Padding(0)
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            cardSurface.Controls.Add(rootLayout);

            var headerPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                BackColor = cardBg,
                Margin = new Padding(0, 0, 0, 10)
            };
            rootLayout.Controls.Add(headerPanel, 0, 0);

            EnableDrag(headerPanel);
            headerPanel.Cursor = Cursors.SizeAll;

            headerPanel.Paint += (s, ev) =>
            {
                using (var pen = new Pen(border, 1f))
                {
                    ev.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
                }
            };

            var iconBox = new System.Windows.Forms.Panel
            {
                Size = new Size(46, 46),
                Location = new Point(0, 18),
                BackColor = surfaceBg
            };
            iconBox.Paint += (s, ev) =>
            {
                ev.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(accent, 2f))
                {
                    ev.Graphics.DrawRectangle(pen, 1, 1, iconBox.Width - 3, iconBox.Height - 3);
                }
            };
            var iconLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "🧾",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = accent,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold)
            };
            iconBox.Controls.Add(iconLabel);
            headerPanel.Controls.Add(iconBox);
            EnableDrag(iconBox);

            _lblTitle = new Label
            {
                Text = "Receipt Set Viewer",
                AutoSize = true,
                Location = new Point(60, 18),
                ForeColor = textColor,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold)
            };
            var subtitleLabel = new Label
            {
                Text = "View receipt set and attached documents",
                AutoSize = true,
                Location = new Point(62, 50),
                ForeColor = muted,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
            };
            headerPanel.Controls.Add(_lblTitle);
            headerPanel.Controls.Add(subtitleLabel);
            EnableDrag(_lblTitle);
            EnableDrag(subtitleLabel);

            var btnCloseX = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "✕",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Size = new Size(38, 38),
                Margin = new Padding(0)
            };
            btnCloseX.PrimaryColor = surfaceBg;
            btnCloseX.DefaultColor = surfaceBg;
            btnCloseX.BorderColor = border;
            btnCloseX.TextColor = textColor;
            btnCloseX.HoverTextColor = textColor;
            btnCloseX.Cursor = Cursors.Hand;
            btnCloseX.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCloseX.Location = new Point(ClientSize.Width - 24 - btnCloseX.Width, 22);
            btnCloseX.Click += (s, ev) => Close();
            headerPanel.Controls.Add(btnCloseX);
            headerPanel.Resize += (s, ev) =>
            {
                btnCloseX.Location = new Point(headerPanel.Width - btnCloseX.Width, 22);
            };

            var bodyPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            rootLayout.Controls.Add(bodyPanel, 0, 1);

            // Period tabs are always visible so users understand the feature exists.
            // Previous/History are enabled only when a receipt set is linked to a Set (SetId).
            Control receiptContainer = bodyPanel;

            _tabPeriods = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(10, 4)
            };

            _tpCurrent = new TabPage("Current");
            _tpPrevious = new TabPage("Previous");
            _tpHistory = new TabPage("History");

            _tabPeriods.TabPages.Add(_tpCurrent);
            _tabPeriods.TabPages.Add(_tpPrevious);
            _tabPeriods.TabPages.Add(_tpHistory);
            _tabPeriods.SelectedIndexChanged += (s, e) => OnPeriodTabChanged();

            bodyPanel.Controls.Add(_tabPeriods);

            _receiptHostPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _tpCurrent.Controls.Add(_receiptHostPanel);
            receiptContainer = _receiptHostPanel;

            // History tab content (list of older renewal periods)
            var historyRoot = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(16)
            };

            var lblHistoryTitle = new Label
            {
                Text = "Receipt History",
                AutoSize = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(18, 22, 28),
                Dock = DockStyle.Top,
                Height = 28
            };

            var lblHistoryHint = new Label
            {
                Text = "Select an item to view details. Double-click to open (read-only). Right-click for actions.",
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(90, 100, 110)
            };

            BuildHistoryContextMenu();

            var historySplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6
            };
            historySplit.HandleCreated += (s, e) => ConfigureHistorySplit(historySplit, desiredPanel1Min: 420, desiredPanel2Min: 320, preferredPanel1: 640);
            historySplit.SizeChanged += (s, e) => ConfigureHistorySplit(historySplit, desiredPanel1Min: 420, desiredPanel2Min: 320, preferredPanel1: 640);
            ConfigureHistorySplit(historySplit, desiredPanel1Min: 420, desiredPanel2Min: 320, preferredPanel1: 640);

            _lvHistory = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                BorderStyle = BorderStyle.FixedSingle
            };
            _lvHistory.Columns.Add("Version / Period", 140);
            _lvHistory.Columns.Add("Saved", 140);
            _lvHistory.Columns.Add("Supplier", 140);
            _lvHistory.Columns.Add("Doc #", 180);
            _lvHistory.Columns.Add("Imgs", 110);
            _lvHistory.Columns.Add("By", 120);
            _lvHistory.Columns.Add("Changed", 220);
            _lvHistory.DoubleClick += (s, e) => OpenSelectedHistoryEntry();
            _lvHistory.SelectedIndexChanged += (s, e) => UpdateHistoryDetailsFromSelection();
            _lvHistory.ContextMenuStrip = _historyMenu;

            historySplit.Panel1.Padding = new Padding(0);
            historySplit.Panel1.Controls.Add(_lvHistory);

            _historyDetailsPanel = BuildHistoryDetailsPanel();
            historySplit.Panel2.Padding = new Padding(0);
            historySplit.Panel2.Controls.Add(_historyDetailsPanel);

            historyRoot.Controls.Add(historySplit);
            historyRoot.Controls.Add(lblHistoryHint);
            historyRoot.Controls.Add(lblHistoryTitle);
            _tpHistory.Controls.Add(historyRoot);

            _coverageTabsEnabled = true;
            if (!_setId.HasValue)
                DisableCoverageTabsUi();

            var bodyLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            // Top card contains: image info + actions + 3-row fields (incl. Contract) so it needs more vertical space.
            // Top card contains: image info + actions + 2-row fields. Keep this compact to avoid large blank space.
            bodyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 225F));
            bodyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            receiptContainer.Controls.Add(bodyLayout);

            // Top info + fields + buttons block on a light background with a white card
            var topPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 150,
                BackColor = Color.Transparent,
                Padding = new Padding(0)
            };

            var topCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = cardBg,
                Padding = new Padding(12, 10, 12, 10)
            };
            topCard.Paint += (s, ev) =>
            {
                using (var pen = new Pen(border, 1f))
                {
                    ev.Graphics.DrawRectangle(pen, 0, 0, topCard.Width - 1, topCard.Height - 1);
                }
            };

            // Row 1: Current image text
            var headerRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 26,
                ColumnCount = 3,
                RowCount = 1
            };
            headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _lblImageInfo = new Label
            {
                Text = "Current image: (none)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = muted
            };

            _lblCoverage = new Label
            {
                Text = string.Empty,
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(45, 120, 210),
                Margin = new Padding(10, 0, 10, 0)
            };

            _lblRenewalIndicator = new Label
            {
                Text = string.Empty,
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = muted,
                Margin = new Padding(0, 0, 0, 0)
            };

            headerRow.Controls.Add(_lblImageInfo, 0, 0);
            headerRow.Controls.Add(_lblCoverage, 1, 0);
            headerRow.Controls.Add(_lblRenewalIndicator, 2, 0);

            // Row 2: Fields row (2 rows layout)
            var fieldsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 70,
                ColumnCount = 4,
                RowCount = 2,
                AutoSize = false,
                Margin = new Padding(0, 6, 0, 0)
            };

            fieldsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Label
            fieldsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); // Value
            fieldsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Label
            fieldsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); // Value

            fieldsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            fieldsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));

            _lblSupplier = new Label
            {
                Text = "Supplier*:",
                AutoSize = true,
                Font = new Font("Montserrat", 9F, FontStyle.Bold),
                ForeColor = muted,
                Margin = new Padding(0, 6, 8, 0)
            };

            _cmbSupplier = new ComboBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 3, 24, 6),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                BackColor = Color.White,
                ForeColor = textColor,
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            _cmbSupplier.TextChanged += (s, e) => MarkDirty();
            _cmbSupplier.SelectedIndexChanged += (s, e) => MarkDirty();

            _lblPoNumber = new Label
            {
                Text = "PO #:",
                AutoSize = true,
                Font = new Font("Montserrat", 9F, FontStyle.Bold),
                ForeColor = muted,
                Margin = new Padding(0, 6, 8, 0)
            };

            _txtPoNumber = new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 3, 0, 6),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                BackColor = Color.White,
                ForeColor = textColor
            };
            _txtPoNumber.TextChanged += (s, e) => MarkDirty();

            _lblSiNumber = new Label
            {
                Text = "SI #:",
                AutoSize = true,
                Font = new Font("Montserrat", 9F, FontStyle.Bold),
                ForeColor = muted,
                Margin = new Padding(0, 6, 8, 0)
            };

            _txtSiNumber = new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 3, 24, 0),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                BackColor = Color.White,
                ForeColor = textColor
            };
            _txtSiNumber.TextChanged += (s, e) => MarkDirty();

            _lblDrNumber = new Label
            {
                Text = "DR #:",
                AutoSize = true,
                Font = new Font("Montserrat", 9F, FontStyle.Bold),
                ForeColor = muted,
                Margin = new Padding(0, 6, 8, 0)
            };

            _txtDrNumber = new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 3, 0, 0),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                BackColor = Color.White,
                ForeColor = textColor
            };
            _txtDrNumber.TextChanged += (s, e) => MarkDirty();

            fieldsLayout.Controls.Add(_lblSupplier, 0, 0);
            fieldsLayout.Controls.Add(_cmbSupplier, 1, 0);
            fieldsLayout.Controls.Add(_lblPoNumber, 2, 0);
            fieldsLayout.Controls.Add(_txtPoNumber, 3, 0);

            fieldsLayout.Controls.Add(_lblSiNumber, 0, 1);
            fieldsLayout.Controls.Add(_txtSiNumber, 1, 1);
            fieldsLayout.Controls.Add(_lblDrNumber, 2, 1);
            fieldsLayout.Controls.Add(_txtDrNumber, 3, 1);

            _toolTip = new ToolTip();
            BuildImageContextMenu();

            // Actions Row
            var actionsHost = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 88,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 6, 0, 0)
            };
            actionsHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            actionsHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            actionsHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));

            var leftActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };

            var rightActions = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };

            void ConfigBtn(Button b, string txt, EventHandler click, bool primary = false, int? width = null)
            {
                b.Text = txt;
                b.Width = width ?? ((txt.Length > 10) ? 120 : 90);
                b.Height = 36;
                b.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                b.Cursor = Cursors.Hand;
                b.Margin = new Padding(0, 0, 10, 0);
                b.Click += click;
                ApplyRoundedButtonStyle(b, primary);
            }

            _btnNewReceipt = new Button();
            ConfigBtn(_btnNewReceipt, "Renew", BtnNewReceipt_Click, false, 90);
            _toolTip.SetToolTip(_btnNewReceipt, "Create a new receipt set for the current period (renewal). Keeps Previous/History.");

            _btnRevert = new Button();
            ConfigBtn(_btnRevert, "Revert", BtnRevert_Click, false, 90);
            _btnRevert.Enabled = false;
            _toolTip.SetToolTip(_btnRevert, "Swap Current with Previous (undo the last saved receipt set).");

            _btnSave = new Button();
            ConfigBtn(_btnSave, "Save", BtnSave_Click, true, 90);
            _btnSave.Enabled = false;
            _toolTip.SetToolTip(_btnSave, "Save changes (Supplier is required).");

            _btnAttachSi = new Button();
            ConfigBtn(_btnAttachSi, "Attach SI", (s, e) => AttachImageForSlot(0), false, 100);

            _btnAttachDr = new Button();
            ConfigBtn(_btnAttachDr, "Attach DR", (s, e) => AttachImageForSlot(1), false, 100);

            _btnAttachPo = new Button();
            ConfigBtn(_btnAttachPo, "Attach PO", (s, e) => AttachImageForSlot(2), false, 100);

            _btnCopyLocalToDb = new Button();
            ConfigBtn(_btnCopyLocalToDb, "Copy Local Receipt to DB", (s, e) => OpenCopyLocalReceiptToDbDialog(), false, 170);
            _toolTip.SetToolTip(_btnCopyLocalToDb, "Copy this receipt's local file(s) on THIS machine into the database.");

            leftActions.Controls.Add(_btnNewReceipt);
            leftActions.Controls.Add(_btnRevert);
            leftActions.Controls.Add(_btnSave);
            leftActions.Controls.Add(new Panel { Width = 6, Height = 1 });
            leftActions.Controls.Add(_btnAttachSi);
            leftActions.Controls.Add(_btnAttachDr);
            leftActions.Controls.Add(_btnAttachPo);
            leftActions.Controls.Add(new Panel { Width = 6, Height = 1 });
            leftActions.Controls.Add(_btnCopyLocalToDb);

            _lblLinkedSet = new Label
            {
                Text = "Linked Set:",
                AutoSize = true,
                ForeColor = muted,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(0, 8, 8, 0)
            };

            _cmbLinkedSets = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 170,
                Margin = new Padding(0, 6, 10, 0)
            };
            _cmbLinkedSets.SelectedIndexChanged += (s, e) => OnLinkedSetSelectionChanged();

            _btnOpenLinkedSet = new Button();
            ConfigBtn(_btnOpenLinkedSet, "Open", (s, e) => OpenLinkedSet(), false, 70);
            _btnOpenLinkedSet.BackColor = surfaceBg;
            _btnOpenLinkedSet.ForeColor = muted;

            _btnUnlinkSet = new Button();
            ConfigBtn(_btnUnlinkSet, "Unlink", (s, e) => UnlinkFromSet(), false, 80);
            _btnUnlinkSet.BackColor = surfaceBg;
            _btnUnlinkSet.ForeColor = Color.IndianRed;

            ApplyRoundedButtonStyle(_btnOpenLinkedSet);
            ApplyRoundedButtonStyle(_btnUnlinkSet);
            _btnOpenLinkedSet.FlatAppearance.BorderColor = border;
            _btnUnlinkSet.FlatAppearance.BorderColor = border;

            _lnkInvoiceReport = new LinkLabel
            {
                Text = "Link Invoice report",
                AutoSize = true,
                LinkColor = accent,
                ActiveLinkColor = accent,
                VisitedLinkColor = accent,
                Margin = new Padding(12, 10, 0, 0)
            };
            _lnkInvoiceReport.LinkClicked += (s, e) => HandleInvoiceReportLinkClicked();

            rightActions.Controls.Add(_lblLinkedSet);
            rightActions.Controls.Add(_cmbLinkedSets);
            rightActions.Controls.Add(_btnOpenLinkedSet);
            rightActions.Controls.Add(_btnUnlinkSet);
            rightActions.Controls.Add(_lnkInvoiceReport);

            var rightActionsHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            rightActions.Dock = DockStyle.Right;
            rightActionsHost.Controls.Add(rightActions);

            actionsHost.Controls.Add(leftActions, 0, 0);
            actionsHost.Controls.Add(rightActionsHost, 0, 1);

            topCard.Controls.Add(fieldsLayout);
            topCard.Controls.Add(actionsHost);
            topCard.Controls.Add(headerRow);
            topPanel.Controls.Add(topCard);

            // Main preview area with left thumbnails and tabs
            var previewPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 0),
                BackColor = Color.Transparent
            };

            _lblSlotName = new Label
            {
                Text = string.Empty,
                Dock = DockStyle.Top,
                Height = 20,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = muted
            };

            _tabSlots = new TabControl
            {
                Dock = DockStyle.Top,
                Height = 28
            };
            _tabSlots.TabPages.Add("SI (0)");
            _tabSlots.TabPages.Add("DR (0)");
            _tabSlots.TabPages.Add("PO (0)");
            _tabSlots.SelectedIndexChanged += (s, e) =>
            {
                if (_updatingTabs) return;
                if (_tabSlots.SelectedIndex < 0 || _tabSlots.SelectedIndex > 2) return;
                _currentSlotIndex = _tabSlots.SelectedIndex;
                ShowCurrentImage();
            };

            _previewScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _previewScrollPanel.MouseDown += Preview_MouseDown;
            _previewScrollPanel.MouseMove += Preview_MouseMove;
            _previewScrollPanel.MouseUp += Preview_MouseUp;
            _previewScrollPanel.MouseWheel += Preview_MouseWheel;
            _previewScrollPanel.MouseEnter += (s, e) => _previewScrollPanel.Focus();
            _previewScrollPanel.AllowDrop = true;
            _previewScrollPanel.DragEnter += Preview_DragEnter;
            _previewScrollPanel.DragDrop += Preview_DragDrop;
            _previewScrollPanel.ContextMenuStrip = _imageMenu;

            _picPreview = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.White,
                Location = new Point(0, 0)
            };
            _picPreview.MouseWheel += Preview_MouseWheel;
            _picPreview.MouseDown += Preview_MouseDown;
            _picPreview.MouseMove += Preview_MouseMove;
            _picPreview.MouseUp += Preview_MouseUp;
            _picPreview.AllowDrop = true;
            _picPreview.DragEnter += Preview_DragEnter;
            _picPreview.DragDrop += Preview_DragDrop;
            _picPreview.ContextMenuStrip = _imageMenu;

            _emptyStatePanel = BuildEmptyStatePanel(surfaceBg, border, textColor, muted);
            _emptyStatePanel.Dock = DockStyle.Fill;
            _emptyStatePanel.Visible = false;
            _emptyStatePanel.AllowDrop = true;
            _emptyStatePanel.DragEnter += Preview_DragEnter;
            _emptyStatePanel.DragDrop += Preview_DragDrop;
            _emptyStatePanel.ContextMenuStrip = _imageMenu;

            _previewScrollPanel.Controls.Add(_picPreview);
            _previewScrollPanel.Controls.Add(_emptyStatePanel);

            // Left thumbnails
            var leftPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 0, 8, 0),
                BackColor = Color.FromArgb(250, 251, 252)
            };

            var thumbsLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };

            _thumbSi = new PictureBox
            {
                Width = 140,
                Height = 100,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };
            _thumbSi.Click += (s, e) =>
            {
                _currentSlotIndex = 0;
                ShowCurrentImage();
            };
            _thumbSi.Tag = 0;
            _thumbSi.ContextMenuStrip = _imageMenu;

            _thumbDr = new PictureBox
            {
                Width = 140,
                Height = 100,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };
            _thumbDr.Click += (s, e) =>
            {
                _currentSlotIndex = 1;
                ShowCurrentImage();
            };
            _thumbDr.Tag = 1;
            _thumbDr.ContextMenuStrip = _imageMenu;

            _thumbPo = new PictureBox
            {
                Width = 140,
                Height = 100,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };
            _thumbPo.Click += (s, e) =>
            {
                _currentSlotIndex = 2;
                ShowCurrentImage();
            };
            _thumbPo.Tag = 2;
            _thumbPo.ContextMenuStrip = _imageMenu;

            thumbsLayout.Controls.Add(_thumbSi);
            thumbsLayout.Controls.Add(_thumbDr);
            thumbsLayout.Controls.Add(_thumbPo);
            leftPanel.Controls.Add(thumbsLayout);

            // Right: slot label, tabs, main preview
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill
            };
            rightPanel.Controls.Add(_previewScrollPanel);
            rightPanel.Controls.Add(BuildTabsHeaderRow(surfaceBg, border, muted));
            rightPanel.Controls.Add(_lblSlotName);

            var contentLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            contentLayout.Controls.Add(leftPanel, 0, 0);
            contentLayout.Controls.Add(rightPanel, 1, 0);

            var previewCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = cardBg,
                Padding = new Padding(12)
            };
            previewCard.Paint += (s, ev) =>
            {
                using (var pen = new Pen(border, 1f))
                {
                    ev.Graphics.DrawRectangle(pen, 0, 0, previewCard.Width - 1, previewCard.Height - 1);
                }
            };
            previewCard.Controls.Add(contentLayout);
            previewPanel.Controls.Add(previewCard);

            topPanel.Controls.Add(topCard);

            bodyLayout.Controls.Add(topPanel, 0, 0);
            bodyLayout.Controls.Add(previewPanel, 0, 1);

            Controls.Clear();
            Controls.Add(rootCard);

            RefreshLinkedSetsUi();
            UpdateSlotActionButtons();
            SetDirty(false);

            UpdateNavigationButtons();
            UpdateSlotTabs();
            UpdateThumbnails();

            FormClosed += (s, e) =>
            {
                if (_picPreview.Image != null)
                {
                    _picPreview.Image.Dispose();
                    _picPreview.Image = null;
                }

                foreach (var f in _tempImageFiles.ToList())
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(f) && File.Exists(f))
                            File.Delete(f);
                    }
                    catch
                    {
                    }
                }
            };
        }

        private Control BuildTabsHeaderRow(Color surfaceBg, Color border, Color muted)
        {
            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 28,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            host.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _btnFullscreen = new Button
            {
                Text = "Full",
                Width = 60,
                Height = 26,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnFullscreen.Click += (s, e) => OpenCurrentImageFullscreen();
            _btnFullscreen.BackColor = surfaceBg;
            _btnFullscreen.ForeColor = muted;
            _btnFullscreen.Margin = new Padding(8, 0, 0, 0);
            ApplyRoundedButtonStyle(_btnFullscreen);
            _btnFullscreen.FlatAppearance.BorderColor = border;
            if (_toolTip != null)
                _toolTip.SetToolTip(_btnFullscreen, "Full screen (F11)");

            host.Controls.Add(_tabSlots, 0, 0);
            host.Controls.Add(_btnFullscreen, 1, 0);

            return host;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F11)
            {
                OpenCurrentImageFullscreen();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void AttachImageForSlot(int slotIndex)
        {
            if (_readOnly)
            {
                return;
            }

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select image";
                dialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff|All Files|*.*";
                dialog.Multiselect = false;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                string path = dialog.FileName;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return;
                }

                var bytes = File.ReadAllBytes(path);
                AttachImageForSlotFromBytes(slotIndex, bytes, originalPath: path);
            }
        }

        private void BtnPrev_Click(object sender, EventArgs e)
        {
            if (!TryChangeSlot(-1))
            {
                MessageBox.Show(this, "No other images attached yet.", "Receipt Set", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            if (!TryChangeSlot(1))
            {
                MessageBox.Show(this, "No other images attached yet.", "Receipt Set", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (_readOnly)
            {
                return;
            }

            if (!_isDirty)
            {
                return;
            }

            if (!ValidateBeforeSave(showMessage: true))
            {
                return;
            }

            try
            {
                var dto = new ReceiptSetDto
                {
                    ReceiptSetId = _receiptSetId,
                    RenewedFromReceiptSetId = _receiptSetId <= 0 ? _pendingRenewedFromReceiptSetId : null,
                    Supplier = string.IsNullOrWhiteSpace(_cmbSupplier.Text) ? null : _cmbSupplier.Text.Trim(),
                    SiNumber = string.IsNullOrWhiteSpace(_txtSiNumber.Text) ? null : _txtSiNumber.Text.Trim(),
                    DrNumber = string.IsNullOrWhiteSpace(_txtDrNumber.Text) ? null : _txtDrNumber.Text.Trim(),
                    PoNumber = string.IsNullOrWhiteSpace(_txtPoNumber.Text) ? null : _txtPoNumber.Text.Trim(),
                    SiImagePath = _siImagePath,
                    DrImagePath = _drImagePath,
                    PoImagePath = _poImagePath,
                    SiImage = _siImageBytes,
                    DrImage = _drImageBytes,
                    PoImage = _poImageBytes
                };

                int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                int id = _repository.Save(dto, userId);
                _receiptSetId = id;
                _pendingRenewedFromReceiptSetId = null;

                if (_setId.HasValue)
                {
                    var cov = _activeCoverage ?? _repository.ResolveDefaultCoverageForSet(_setId.Value);
                    _repository.AttachReceiptSetToSet(_receiptSetId, _setId.Value, cov?.StartDate, cov?.EndDate, userId);
                }

                MessageBox.Show(this, "Receipt set saved successfully.", "Receipt Set",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                RefreshLinkedSetsUi();

                if (!_setId.HasValue && _receiptSetId > 0)
                {
                    InitializeUnlinkedRenewalTabsForReceiptSet(_receiptSetId);
                }

                SetDirty(false);
                UpdateSlotActionButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to save receipt set: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRevert_Click(object sender, EventArgs e)
        {
            if (_forceReadOnly || _readOnly)
            {
                MessageBox.Show(this,
                    "This receipt viewer is in read-only mode.",
                    "Receipt Set",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (_receiptSetId <= 0)
            {
                MessageBox.Show(this,
                    "Please save the receipt set first.",
                    "Receipt Set",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (_isDirty)
            {
                var confirm = MessageBox.Show(this,
                    "You have unsaved changes.\n\nRevert anyway and discard changes?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm != DialogResult.Yes)
                    return;
            }

            // Revert always operates from the Current tab context.
            if (_tabPeriods != null && _tpCurrent != null && _tabPeriods.SelectedTab != _tpCurrent)
            {
                _updatingTabs = true;
                try
                {
                    _tabPeriods.SelectedTab = _tpCurrent;
                }
                finally
                {
                    _updatingTabs = false;
                }
            }

            if (_setId.HasValue && _setId.Value > 0)
            {
                RevertLinkedReceiptToPrevious();
            }
            else
            {
                RevertUnlinkedReceiptToPrevious();
            }
        }

        private void RevertUnlinkedReceiptToPrevious()
        {
            int headId = _unlinkedHeadReceiptSetId ?? _receiptSetId;

            if (!_unlinkedPreviousReceiptSetId.HasValue)
            {
                MessageBox.Show(this,
                    "No previous receipt set exists to revert to.",
                    "Receipt Set",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(this,
                "Revert to the previous receipt set?\n\n" +
                "The previous receipt will become Current, and the current receipt will move to Previous.",
                "Revert Receipt Set",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                int newHeadId = _repository.RevertUnlinkedReceiptSetToPrevious(headId, userId);

                var dto = _repository.GetByReceiptSetId(newHeadId);
                if (dto != null)
                {
                    LoadFromDto(dto);
                }
                _unlinkedHeadReceiptSetId = newHeadId;
                InitializeUnlinkedRenewalTabsForReceiptSet(newHeadId);

                if (_tabPeriods != null && _tpCurrent != null)
                    _tabPeriods.SelectedTab = _tpCurrent;

                MessageBox.Show(this, "Reverted to the previous receipt set.", "Receipt Set",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Failed to revert receipt set: " + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void RevertLinkedReceiptToPrevious()
        {
            if (!_setId.HasValue || _setId.Value <= 0)
                return;

            if (_currentCoverage == null || _previousCoverage == null)
            {
                MessageBox.Show(this,
                    "No previous coverage period exists to revert to.",
                    "Receipt Set",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            ReceiptSetDto currentDto = null;
            ReceiptSetDto prevDto = null;

            try
            {
                currentDto = _repository.GetBySetIdCoverage(_setId.Value, _currentCoverage.StartDate, _currentCoverage.EndDate);
                prevDto = _repository.GetBySetIdCoverage(_setId.Value, _previousCoverage.StartDate, _previousCoverage.EndDate);
            }
            catch
            {
                currentDto = null;
                prevDto = null;
            }

            if (currentDto == null || currentDto.ReceiptSetId <= 0)
            {
                MessageBox.Show(this,
                    "No current receipt set is saved for the current coverage period.",
                    "Receipt Set",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (prevDto == null || prevDto.ReceiptSetId <= 0)
            {
                MessageBox.Show(this,
                    "No previous receipt set is saved for the previous coverage period.",
                    "Receipt Set",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var labelCurrent = $"{_currentCoverage.StartDate:yyyy}-{_currentCoverage.EndDate:yyyy}";
            var labelPrev = $"{_previousCoverage.StartDate:yyyy}-{_previousCoverage.EndDate:yyyy}";

            var confirm = MessageBox.Show(this,
                "Revert to the previous receipt set?\n\n" +
                $"This will swap receipts between coverage periods:\n\n" +
                $"Current: {labelCurrent}\n" +
                $"Previous: {labelPrev}",
                "Revert Receipt Set",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                _repository.SwapReceiptSetCoverageLinks(
                    _setId.Value,
                    currentDto.ReceiptSetId, _currentCoverage.StartDate, _currentCoverage.EndDate,
                    prevDto.ReceiptSetId, _previousCoverage.StartDate, _previousCoverage.EndDate,
                    userId);

                InitializeCoverageTabsForSet(_setId.Value);

                MessageBox.Show(this, "Reverted to the previous receipt set.", "Receipt Set",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Failed to revert receipt set: " + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void BtnNewReceipt_Click(object sender, EventArgs e)
        {
            if (_forceReadOnly)
            {
                MessageBox.Show(this,
                    "This receipt viewer is in read-only mode.\n\nOpen the receipt in edit mode to create a new receipt set.",
                    "Receipt Set",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            _pendingRenewedFromReceiptSetId = null;

            if (_isDirty)
            {
                var confirm = MessageBox.Show(this,
                    "You have unsaved changes.\n\nCreate a new receipt set and discard current changes?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm != DialogResult.Yes)
                {
                    return;
                }
            }

            bool allowRenewWithoutLinkedSet = false;
            if (!_setId.HasValue || _setId.Value <= 0)
            {
                var decision = PromptRenewWithoutLinkedSetDecision();
                if (decision == RenewWithoutLinkedSetDecision.Cancel)
                    return;

                if (decision == RenewWithoutLinkedSetDecision.TryAutoLinkAndRenew)
                {
                    if (!TryAutoLinkCurrentReceiptToInvoiceSet())
                    {
                        var fallback = MessageBox.Show(this,
                            "Auto-link failed.\n\nRenew anyway as an unlinked receipt?\n\n" +
                            "Note: Previous/History tabs require a linked Set.",
                            "Receipt Set",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        if (fallback != DialogResult.Yes)
                            return;

                        allowRenewWithoutLinkedSet = true;
                    }
                }
                else
                {
                    allowRenewWithoutLinkedSet = true;
                }
            }

            // Renewal receipts are created in the Current tab context.
            if (_tabPeriods != null && _tpCurrent != null && _tabPeriods.SelectedTab != _tpCurrent)
            {
                _updatingTabs = true;
                try
                {
                    _tabPeriods.SelectedTab = _tpCurrent;
                }
                finally
                {
                    _updatingTabs = false;
                }
            }

            // Target the current coverage period and avoid duplicates (linked Set only).
            if (!allowRenewWithoutLinkedSet && _setId.HasValue && _setId.Value > 0)
            {
                try
                {
                    InitializeCoverageTabsForSet(_setId.Value);

                    var cov = _currentCoverage ?? _repository.ResolveDefaultCoverageForSet(_setId.Value);
                    if (cov != null)
                    {
                        var existing = _repository.GetBySetIdCoverage(_setId.Value, cov.StartDate, cov.EndDate);
                        if (existing != null && existing.ReceiptSetId > 0)
                        {
                            var openExisting = MessageBox.Show(this,
                                "A receipt set already exists for the current period.\n\nOpen the existing receipt instead?",
                                "Receipt Set",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Information);

                            if (openExisting == DialogResult.Yes)
                            {
                                _activeCoverage = cov;
                                LoadFromDto(existing);
                                return;
                            }
                        }

                        _activeCoverage = cov;
                    }
                }
                catch
                {
                    // If period metadata isn't available, still allow creating a new draft.
                }
            }
            else
            {
                _pendingRenewedFromReceiptSetId = _receiptSetId > 0 ? (int?)_receiptSetId : null;
                _activeCoverage = null;
                DisableCoverageTabsUi();
            }

            ClearReceiptUi();
            SetDirty(false);
            _currentSlotIndex = 0;
            ShowCurrentImage();

            if (_cmbSupplier != null)
                _cmbSupplier.Focus();
        }

        private enum RenewWithoutLinkedSetDecision
        {
            Cancel = 0,
            RenewUnlinked = 1,
            TryAutoLinkAndRenew = 2
        }

        private RenewWithoutLinkedSetDecision PromptRenewWithoutLinkedSetDecision()
        {
            // Keep this lightweight: MessageBox with explicit instructions.
            // When possible, offer an auto-link path so renewals can still populate Previous/History.
            var hasAnyDocNo =
                !string.IsNullOrWhiteSpace(_txtSiNumber?.Text) ||
                !string.IsNullOrWhiteSpace(_txtDrNumber?.Text) ||
                !string.IsNullOrWhiteSpace(_txtPoNumber?.Text);

            if (_receiptSetId > 0 && hasAnyDocNo)
            {
                var result = MessageBox.Show(this,
                    "This receipt is not linked to a Set.\n\n" +
                    "Renew normally requires a linked Set so it can track Previous/History by coverage period.\n\n" +
                    "Yes = Try to auto-link using SI/DR/PO and continue.\n" +
                    "No = Renew anyway (unlinked).\n" +
                    "Cancel = Do nothing.",
                    "Receipt Set",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes) return RenewWithoutLinkedSetDecision.TryAutoLinkAndRenew;
                if (result == DialogResult.No) return RenewWithoutLinkedSetDecision.RenewUnlinked;
                return RenewWithoutLinkedSetDecision.Cancel;
            }

            var confirm = MessageBox.Show(this,
                "This receipt is not linked to a Set.\n\n" +
                "Renew anyway as an unlinked receipt?\n\n" +
                "Note: Previous/History tabs require a linked Set.",
                "Receipt Set",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            return confirm == DialogResult.Yes
                ? RenewWithoutLinkedSetDecision.RenewUnlinked
                : RenewWithoutLinkedSetDecision.Cancel;
        }

        private bool TryAutoLinkCurrentReceiptToInvoiceSet()
        {
            if (_readOnly)
                return false;

            if (_receiptSetId <= 0)
            {
                MessageBox.Show(this,
                    "Please save the receipt set first before auto-linking.",
                    "Receipt Set",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            string[] candidates =
            {
                _txtSiNumber?.Text,
                _txtDrNumber?.Text,
                _txtPoNumber?.Text
            };

            ReceiptSetLinkedSetDto match = null;
            string matchedBy = null;

            foreach (var raw in candidates)
            {
                var docNo = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
                if (string.IsNullOrWhiteSpace(docNo))
                    continue;

                try
                {
                    match = _repository.FindInvoiceSetByDocumentNumber(docNo);
                }
                catch
                {
                    match = null;
                }

                if (match != null && match.SetId > 0)
                {
                    matchedBy = docNo;
                    break;
                }
            }

            if (match == null || match.SetId <= 0)
            {
                MessageBox.Show(this,
                    "No invoice report Set was found for the current SI/DR/PO numbers.\n\n" +
                    "Tip: Enter the correct SI #, save, then click 'Link Invoice report' first.",
                    "Receipt Set",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            try
            {
                int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                _repository.AttachReceiptSetToSet(_receiptSetId, match.SetId, userId);
                _setId = match.SetId;
                _coveragePromptShown = false;

                RefreshLinkedSetsUi();
                UpdateLinkedSetButtons();

                InitializeCoverageTabsForSet(match.SetId);

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Failed to auto-link receipt set: " + ex.Message,
                    "Receipt Set",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        private bool TryChangeSlot(int delta)
        {
            int[] slots = { 0, 1, 2 };
            for (int i = 0; i < slots.Length; i++)
            {
                _currentSlotIndex = (_currentSlotIndex + delta + 3) % 3;
                if (HasImageForSlot(_currentSlotIndex))
                {
                    ShowCurrentImage();
                    return true;
                }
            }

            return false;
        }

        private void ShowCurrentImage()
        {
            string path = GetImagePathForSlot(_currentSlotIndex);
            string slotName = GetSlotName(_currentSlotIndex);
            byte[] bytes = GetImageBytesForSlot(_currentSlotIndex);

            _lblSlotName.Text = slotName;

            if (!HasAnyData())
            {
                _lblImageInfo.Text = "No receipt data. Please add supplier info and attach images.";
                ShowEmptyState("No receipt data yet", "Add supplier info and attach images.\nYou can also drop an image here.");
                UpdateNavigationButtons();
                UpdateSlotTabs();
                UpdateThumbnails();
                UpdateSlotActionButtons();
                UpdateImageInfoSuffix();
                UpdateImageTooltips();
                return;
            }

            var hasImage = (bytes != null && bytes.Length > 0) || (!string.IsNullOrWhiteSpace(path) && File.Exists(path));
            if (!hasImage)
            {
                _lblImageInfo.Text = $"Current image: ({slotName}) none";
                ShowEmptyState($"Drop {slotName} image here", $"Or click '{slotName} ✓/Attach {slotName}'.\nTip: Ctrl + Mouse Wheel to zoom.");
                UpdateNavigationButtons();
                UpdateSlotTabs();
                UpdateThumbnails();
                UpdateSlotActionButtons();
                UpdateImageInfoSuffix();
                UpdateImageTooltips();
                return;
            }

            HideEmptyState();

            if (_picPreview.Image != null)
            {
                _picPreview.Image.Dispose();
                _picPreview.Image = null;
            }

            try
            {
                if (bytes != null && bytes.Length > 0)
                    _picPreview.Image = CreateImageFromBytes(bytes);
                else
                    _picPreview.Image = LoadImageNoLock(path);

                if (_picPreview.Image != null)
                {
                    QueueDefaultFit();
                }

                string fileName = !string.IsNullOrWhiteSpace(path) ? Path.GetFileName(path) : slotName;
                _lblImageInfo.Text = $"Current image: {slotName} - {fileName}";
            }
            catch (Exception ex)
            {
                _lblImageInfo.Text = $"Current image: ({slotName}) failed to load";
                MessageBox.Show(this, "Failed to load image: " + ex.Message, "Receipt Set", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateNavigationButtons();
            UpdateSlotTabs();
            UpdateThumbnails();
            UpdateSlotActionButtons();
            UpdateImageInfoSuffix();
            UpdateImageTooltips();
        }

        private static Image CreateImageFromBytes(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            using (var img = Image.FromStream(ms))
            {
                return new Bitmap(img);
            }
        }

        private static Image LoadImageNoLock(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var img = Image.FromStream(fs))
            {
                return new Bitmap(img);
            }
        }

        private void ReceiptSetViewerDialog_Resize(object sender, EventArgs e)
        {
            TryApplyPendingFit();
        }

        private void QueueDefaultFit()
        {
            _pendingZoomFit = true;
            TryApplyPendingFit();
        }

        private void TryApplyPendingFit()
        {
            if (!_pendingZoomFit)
                return;

            if (IsDisposed || _previewScrollPanel == null || _picPreview == null || _picPreview.Image == null)
                return;

            // Avoid Invoke/BeginInvoke until the handle exists.
            if (!IsHandleCreated)
                return;

            // ClientSize can be 0 during initial layout; wait for a real size.
            var client = _previewScrollPanel.ClientSize;
            if (client.Width <= 20 || client.Height <= 20)
                return;

            _pendingZoomFit = false;
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || _previewScrollPanel == null || _picPreview == null || _picPreview.Image == null)
                    return;
                SetZoomToFit();
            }));
        }

        private Panel BuildEmptyStatePanel(Color surfaceBg, Color border, Color textColor, Color muted)
        {
            var p = new Panel
            {
                BackColor = Color.White
            };

            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(border, 1f))
                {
                    var rect = new Rectangle(8, 8, p.Width - 16, p.Height - 16);
                    e.Graphics.DrawRectangle(pen, rect);
                }
            };

            _emptyStateTitle = new Label
            {
                Text = "Drop an image here",
                AutoSize = true,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = textColor,
                BackColor = Color.Transparent,
                Location = new Point(24, 26)
            };

            _emptyStateHint = new Label
            {
                Text = "Tip: Drag & drop a file, or use Attach buttons.\nSupported: JPG, PNG, BMP, TIFF.",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = muted,
                BackColor = Color.Transparent,
                Location = new Point(26, 62)
            };

            var badge = new Panel
            {
                Size = new Size(56, 56),
                BackColor = surfaceBg,
                Location = new Point(24, 120)
            };
            badge.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(border, 1f))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, badge.Width - 1, badge.Height - 1);
                }
            };
            var badgeIcon = new Label
            {
                Dock = DockStyle.Fill,
                Text = "⬇",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = textColor
            };
            badge.Controls.Add(badgeIcon);

            p.Controls.Add(_emptyStateTitle);
            p.Controls.Add(_emptyStateHint);
            p.Controls.Add(badge);

            return p;
        }

        private void ShowEmptyState(string title, string hint)
        {
            if (_emptyStateTitle != null) _emptyStateTitle.Text = title ?? string.Empty;
            if (_emptyStateHint != null) _emptyStateHint.Text = hint ?? string.Empty;

            if (_emptyStatePanel != null)
                _emptyStatePanel.Visible = true;

            if (_picPreview != null)
            {
                if (_picPreview.Image != null)
                {
                    _picPreview.Image.Dispose();
                    _picPreview.Image = null;
                }
                _picPreview.Size = new Size(1, 1);
            }

            if (_previewScrollPanel != null)
                _previewScrollPanel.AutoScrollPosition = new Point(0, 0);
        }

        private void HideEmptyState()
        {
            if (_emptyStatePanel != null)
                _emptyStatePanel.Visible = false;
        }

        private void MarkDirty()
        {
            if (_readOnly || _suppressDirty)
                return;

            SetDirty(true);
        }

        private void SetDirty(bool dirty)
        {
            _isDirty = dirty;
            UpdateSaveButtonEnabled();
            UpdateSlotActionButtons();
            UpdateImageInfoSuffix();
        }

        private void UpdateSaveButtonEnabled()
        {
            if (_btnSave == null)
                return;

            var canSave = !_readOnly && _isDirty && ValidateBeforeSave(showMessage: false);
            _btnSave.Enabled = canSave;
        }

        private void UpdateRevertButtonEnabled()
        {
            if (_btnRevert == null)
                return;

            if (_forceReadOnly || _readOnly)
            {
                _btnRevert.Enabled = false;
                return;
            }

            if (_receiptSetId <= 0)
            {
                _btnRevert.Enabled = false;
                return;
            }

            if (_setId.HasValue && _setId.Value > 0)
            {
                _btnRevert.Enabled = _previousCoverage != null;
                return;
            }

            _btnRevert.Enabled = _unlinkedPreviousReceiptSetId.HasValue;
        }

        private bool ValidateBeforeSave(bool showMessage)
        {
            if (!HasAnyData())
                return false;

            var supplier = (_cmbSupplier?.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(supplier))
            {
                SetSupplierValidationState(isValid: false);
                if (showMessage)
                {
                    MessageBox.Show(this, "Supplier is required before saving.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _cmbSupplier?.Focus();
                }
                return false;
            }

            SetSupplierValidationState(isValid: true);
            return true;
        }

        private void SetSupplierValidationState(bool isValid)
        {
            if (_cmbSupplier == null)
                return;

            _cmbSupplier.BackColor = isValid ? Color.White : Color.FromArgb(255, 240, 240);
        }

        private bool HasAnyData()
        {
            return !string.IsNullOrWhiteSpace(_cmbSupplier?.Text) ||
                   !string.IsNullOrWhiteSpace(_txtSiNumber?.Text) ||
                   !string.IsNullOrWhiteSpace(_txtDrNumber?.Text) ||
                   !string.IsNullOrWhiteSpace(_txtPoNumber?.Text) ||
                   HasImageForSlot(0) || HasImageForSlot(1) || HasImageForSlot(2);
        }

        private void UpdateImageInfoSuffix()
        {
            if (_lblImageInfo == null)
                return;

            var baseText = _lblImageInfo.Text ?? string.Empty;
            var trimmed = baseText;

            var idx = baseText.IndexOf(" • ", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                trimmed = baseText.Substring(0, idx);

            if (_isDirty)
            {
                _lblImageInfo.Text = trimmed + " • Unsaved changes";
                _lblImageInfo.ForeColor = Color.FromArgb(192, 57, 43);
            }
            else
            {
                _lblImageInfo.Text = trimmed + (_receiptSetId > 0 ? " • Saved" : string.Empty);
                _lblImageInfo.ForeColor = Color.FromArgb(90, 100, 110);
            }
        }

        private void RefreshLinkedSetsUi()
        {
            if (_cmbLinkedSets == null)
                return;

            try
            {
                _cmbLinkedSets.DataSource = null;
                _cmbLinkedSets.Items.Clear();

                if (_receiptSetId <= 0)
                {
                    // Draft/new receipt set (not yet saved) can still be created for a Set.
                    // Show the selected Set so users understand where it will attach on Save.
                    if (_setId.HasValue && _setId.Value > 0)
                    {
                        var info = _repository.GetSetLinkInfo(_setId.Value)
                                   ?? new ReceiptSetLinkedSetDto { SetId = _setId.Value, SetCode = $"Set #{_setId.Value}" };

                        _cmbLinkedSets.DisplayMember = "SetCode";
                        _cmbLinkedSets.ValueMember = "SetId";
                        _cmbLinkedSets.DataSource = new List<ReceiptSetLinkedSetDto> { info };
                        _cmbLinkedSets.Enabled = true;
                        UpdateLinkedSetButtons();
                        return;
                    }

                    _cmbLinkedSets.Items.Add("(not linked)");
                    _cmbLinkedSets.SelectedIndex = 0;
                    _cmbLinkedSets.Enabled = false;
                    UpdateLinkedSetButtons();
                    return;
                }

                var linked = _repository.GetLinkedSets(_receiptSetId) ?? new List<ReceiptSetLinkedSetDto>();
                if (linked.Count == 0)
                {
                    _cmbLinkedSets.Items.Add("(not linked)");
                    _cmbLinkedSets.SelectedIndex = 0;
                    _cmbLinkedSets.Enabled = false;
                    UpdateLinkedSetButtons();
                    return;
                }

                _cmbLinkedSets.DisplayMember = "SetCode";
                _cmbLinkedSets.ValueMember = "SetId";
                _cmbLinkedSets.DataSource = linked;
                _cmbLinkedSets.Enabled = true;

                if (_setId.HasValue)
                {
                    var match = linked.FirstOrDefault(x => x.SetId == _setId.Value);
                    if (match != null)
                        _cmbLinkedSets.SelectedItem = match;
                }
            }
            catch
            {
                _cmbLinkedSets.DataSource = null;
                _cmbLinkedSets.Items.Clear();
                _cmbLinkedSets.Items.Add("(not linked)");
                _cmbLinkedSets.SelectedIndex = 0;
                _cmbLinkedSets.Enabled = false;
            }
            finally
            {
                UpdateLinkedSetButtons();
            }
        }

        private void OnLinkedSetSelectionChanged()
        {
            UpdateLinkedSetButtons();

            if (_cmbLinkedSets == null || _cmbLinkedSets.SelectedItem == null)
                return;

            if (!(_cmbLinkedSets.SelectedItem is ReceiptSetLinkedSetDto dto))
                return;

            if (_setId.HasValue && _setId.Value == dto.SetId)
                return;

            _setId = dto.SetId;
            _coveragePromptShown = false;

            if (_setId.HasValue)
            {
                InitializeCoverageTabsForSet(_setId.Value);
            }
        }

        private void UpdateLinkedSetButtons()
        {
            bool hasLinked = HasLinkedInvoiceSet();

            if (_btnOpenLinkedSet != null) _btnOpenLinkedSet.Enabled = hasLinked;
            if (_btnUnlinkSet != null) _btnUnlinkSet.Enabled = !_readOnly && hasLinked && _receiptSetId > 0;
            UpdateInvoiceReportLinkUi(hasLinked);
        }

        private bool HasLinkedInvoiceSet()
        {
            if (_setId.HasValue && _setId.Value > 0)
                return true;

            if (_cmbLinkedSets != null && _cmbLinkedSets.SelectedItem is ReceiptSetLinkedSetDto dto)
                return dto.SetId > 0;

            return false;
        }

        private void UpdateInvoiceReportLinkUi(bool hasLinked)
        {
            if (_lnkInvoiceReport == null)
                return;

            if (hasLinked)
            {
                _lnkInvoiceReport.Text = "Open Invoice report";
                _lnkInvoiceReport.Enabled = true; // must work in View mode too
            }
            else
            {
                _lnkInvoiceReport.Text = "Link Invoice report";
                _lnkInvoiceReport.Enabled = !_readOnly && _receiptSetId > 0 && !string.IsNullOrWhiteSpace(_txtSiNumber.Text);
            }
        }

        private void HandleInvoiceReportLinkClicked()
        {
            if (HasLinkedInvoiceSet())
            {
                OpenLinkedInvoiceReport();
                return;
            }

            LinkInvoiceReportSet();
        }

        private void UpdateSlotActionButtons()
        {
            if (_btnAttachSi != null) _btnAttachSi.Text = HasImageForSlot(0) ? "SI ✓" : "Attach SI";
            if (_btnAttachDr != null) _btnAttachDr.Text = HasImageForSlot(1) ? "DR ✓" : "Attach DR";
            if (_btnAttachPo != null) _btnAttachPo.Text = HasImageForSlot(2) ? "PO ✓" : "Attach PO";

            bool hasCurrentImage = HasImageForSlot(_currentSlotIndex);
            if (_btnFullscreen != null) _btnFullscreen.Enabled = hasCurrentImage;

            UpdateRevertButtonEnabled();
            UpdateLinkedSetButtons();
        }

        private void AttachImageForSlotFromBytes(int slotIndex, byte[] bytes, string originalPath)
        {
            if (_readOnly)
                return;

            if (bytes == null || bytes.Length == 0)
                return;

            switch (slotIndex)
            {
                case 0:
                    _siImageBytes = bytes;
                    _siImagePath = originalPath;
                    break;
                case 1:
                    _drImageBytes = bytes;
                    _drImagePath = originalPath;
                    break;
                case 2:
                    _poImageBytes = bytes;
                    _poImagePath = originalPath;
                    break;
            }

            _currentSlotIndex = slotIndex;
            MarkDirty();
            ShowCurrentImage();
        }

        private void PasteFromClipboardToCurrentSlot()
        {
            if (_readOnly)
                return;

            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    if (files != null && files.Count > 0)
                    {
                        var path = files[0];
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        {
                            var bytes = File.ReadAllBytes(path);
                            AttachImageForSlotFromBytes(_currentSlotIndex, bytes, originalPath: path);
                            return;
                        }
                    }
                }

                if (!Clipboard.ContainsImage())
                {
                    MessageBox.Show(this, "Clipboard does not contain an image.", "Paste",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var img = Clipboard.GetImage())
                {
                    if (img == null)
                        return;

                    using (var ms = new MemoryStream())
                    {
                        img.Save(ms, ImageFormat.Png);
                        AttachImageForSlotFromBytes(_currentSlotIndex, ms.ToArray(), originalPath: null);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Paste failed: " + ex.Message, "Paste",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RotateCurrentSlot()
        {
            if (_readOnly)
                return;

            try
            {
                var bytes = GetImageBytesForSlot(_currentSlotIndex);
                var path = GetImagePathForSlot(_currentSlotIndex);

                if ((bytes == null || bytes.Length == 0) && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    bytes = File.ReadAllBytes(path);

                if (bytes == null || bytes.Length == 0)
                    return;

                var rotated = RotateImageBytes(bytes, RotateFlipType.Rotate90FlipNone);
                AttachImageForSlotFromBytes(_currentSlotIndex, rotated, originalPath: null);

                // Once rotated, prefer bytes over original file path to avoid mismatched content.
                switch (_currentSlotIndex)
                {
                    case 0: _siImagePath = null; break;
                    case 1: _drImagePath = null; break;
                    case 2: _poImagePath = null; break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Rotate failed: " + ex.Message, "Rotate",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static byte[] RotateImageBytes(byte[] bytes, RotateFlipType rotateFlip)
        {
            using (var ms = new MemoryStream(bytes))
            using (var img = Image.FromStream(ms))
            {
                img.RotateFlip(rotateFlip);
                using (var outMs = new MemoryStream())
                {
                    img.Save(outMs, ImageFormat.Png);
                    return outMs.ToArray();
                }
            }
        }

        private void OpenCurrentImageExternal()
        {
            OpenImageExternalForSlot(_currentSlotIndex);
        }

        private void OpenImageExternalForSlot(int slotIndex)
        {
            try
            {
                var bytes = GetImageBytesForSlot(slotIndex);
                var path = GetImagePathForSlot(slotIndex);

                string fileToOpen = null;

                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path) && (bytes == null || bytes.Length == 0))
                {
                    fileToOpen = path;
                }
                else if (bytes != null && bytes.Length > 0)
                {
                    var temp = Path.Combine(Path.GetTempPath(), $"receipt_{_receiptSetId}_{GetSlotName(slotIndex)}_{DateTime.Now:yyyyMMdd_HHmmssfff}.png");
                    File.WriteAllBytes(temp, bytes);
                    _tempImageFiles.Add(temp);
                    fileToOpen = temp;
                }
                else if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    fileToOpen = path;
                }

                if (string.IsNullOrWhiteSpace(fileToOpen) || !File.Exists(fileToOpen))
                    return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = fileToOpen,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Open failed: " + ex.Message, "Open Image",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenCurrentImageFullscreen()
        {
            try
            {
                var bytes = GetImageBytesForSlot(_currentSlotIndex);
                var path = GetImagePathForSlot(_currentSlotIndex);

                if ((bytes == null || bytes.Length == 0) && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    bytes = File.ReadAllBytes(path);
                }

                if (bytes == null || bytes.Length == 0)
                {
                    MessageBox.Show(this, "No image to view.", "Receipt Set Viewer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Image img;
                using (var ms = new MemoryStream(bytes))
                using (var loaded = Image.FromStream(ms))
                {
                    img = (Image)loaded.Clone();
                }

                using (var viewer = new FullscreenImageViewerForm(img, $"{GetSlotName(_currentSlotIndex)} - Full Screen"))
                {
                    viewer.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to open full screen view: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private sealed class FullscreenImageViewerForm : Form
        {
            private readonly Panel _scrollPanel;
            private readonly Panel _canvas;
            private readonly PictureBox _picture;
            private readonly Panel _topBar;
            private readonly Label _titleLabel;
            private readonly Label _hint;
            private readonly Button _btnClose;
            private Image _image;
            private double _zoom = 1.0;
            private bool _panning;
            private Point _panMouseOrigin;
            private Point _panScrollOrigin;

            public FullscreenImageViewerForm(Image image, string title)
            {
                _image = image;
                Text = string.IsNullOrWhiteSpace(title) ? "Image" : title;

                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
                BackColor = Color.Black;
                KeyPreview = true;
                StartPosition = FormStartPosition.CenterParent;

                _scrollPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    BackColor = Color.Black
                };

                _canvas = new Panel
                {
                    Location = new Point(0, 0),
                    BackColor = Color.Black
                };

                _picture = new PictureBox
                {
                    BackColor = Color.Black,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Image = _image
                };

                _canvas.Controls.Add(_picture);
                _scrollPanel.Controls.Add(_canvas);

                _topBar = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 44,
                    BackColor = Color.FromArgb(220, 0, 0, 0),
                    Padding = new Padding(14, 8, 14, 8)
                };

                _titleLabel = new Label
                {
                    AutoSize = true,
                    Text = Text,
                    ForeColor = Color.White,
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    Dock = DockStyle.Left
                };

                _btnClose = new Button
                {
                    Text = "✕",
                    Width = 44,
                    Height = 28,
                    Dock = DockStyle.Right,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(30, 255, 255, 255),
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand
                };
                _btnClose.FlatAppearance.BorderSize = 0;
                _btnClose.Click += (s, e) => Close();

                _topBar.Controls.Add(_btnClose);
                _topBar.Controls.Add(_titleLabel);

                _hint = new Label
                {
                    AutoSize = true,
                    Text = "ESC to close • Mouse wheel to zoom • Drag to pan",
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(200, 0, 0, 0),
                    Padding = new Padding(10, 6, 10, 6),
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    Location = new Point(14, 58)
                };

                Controls.Add(_scrollPanel);
                Controls.Add(_topBar);
                Controls.Add(_hint);

                KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Escape)
                        Close();
                };
                MouseDoubleClick += (s, e) => Close();
                _picture.DoubleClick += (s, e) => Close();

                _scrollPanel.MouseWheel += Zoom_MouseWheel;
                _picture.MouseWheel += Zoom_MouseWheel;
                _scrollPanel.MouseEnter += (s, e) => _scrollPanel.Focus();

                _scrollPanel.MouseDown += Pan_MouseDown;
                _scrollPanel.MouseMove += Pan_MouseMove;
                _scrollPanel.MouseUp += Pan_MouseUp;
                _picture.MouseDown += Pan_MouseDown;
                _picture.MouseMove += Pan_MouseMove;
                _picture.MouseUp += Pan_MouseUp;

                Shown += (s, e) => FitToWindow();
                Resize += (s, e) => FitToWindow();
                Layout += (s, e) =>
                {
                    _topBar.BringToFront();
                    _hint.BringToFront();
                };
            }

            private void FitToWindow()
            {
                if (_image == null || _scrollPanel == null)
                    return;

                var client = _scrollPanel.ClientSize;
                if (client.Width <= 0 || client.Height <= 0)
                    return;

                var scaleX = client.Width / (double)_image.Width;
                var scaleY = client.Height / (double)_image.Height;
                _zoom = Math.Min(scaleX, scaleY);
                _zoom = Math.Max(0.05, Math.Min(_zoom, 10.0));

                ApplyZoom();
                _scrollPanel.AutoScrollPosition = new Point(0, 0);
            }

            private void ApplyZoom()
            {
                if (_image == null || _picture == null || _scrollPanel == null || _canvas == null)
                    return;

                var w = (int)Math.Max(1, Math.Round(_image.Width * _zoom));
                var h = (int)Math.Max(1, Math.Round(_image.Height * _zoom));
                _picture.Size = new Size(w, h);

                var client = _scrollPanel.ClientSize;
                var canvasW = Math.Max(client.Width, w);
                var canvasH = Math.Max(client.Height, h);

                _canvas.Size = new Size(Math.Max(1, canvasW), Math.Max(1, canvasH));
                _scrollPanel.AutoScrollMinSize = _canvas.Size;

                _picture.Location = new Point(
                    Math.Max(0, (canvasW - w) / 2),
                    Math.Max(0, (canvasH - h) / 2));
            }

            private void Zoom_MouseWheel(object sender, MouseEventArgs e)
            {
                if (_image == null)
                    return;

                var factor = e.Delta > 0 ? 1.1 : 0.9;
                _zoom *= factor;
                _zoom = Math.Max(0.05, Math.Min(_zoom, 10.0));
                ApplyZoom();
            }

            private void Pan_MouseDown(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left || _scrollPanel == null)
                    return;

                _panning = true;
                _panMouseOrigin = e.Location;
                _panScrollOrigin = new Point(-_scrollPanel.AutoScrollPosition.X, -_scrollPanel.AutoScrollPosition.Y);
                Cursor = Cursors.Hand;
            }

            private void Pan_MouseMove(object sender, MouseEventArgs e)
            {
                if (!_panning || _scrollPanel == null)
                    return;

                var dx = e.Location.X - _panMouseOrigin.X;
                var dy = e.Location.Y - _panMouseOrigin.Y;
                _scrollPanel.AutoScrollPosition = new Point(_panScrollOrigin.X - dx, _panScrollOrigin.Y - dy);
            }

            private void Pan_MouseUp(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left)
                    return;

                _panning = false;
                Cursor = Cursors.Default;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (_picture != null)
                        _picture.Image = null;

                    if (_image != null)
                    {
                        _image.Dispose();
                        _image = null;
                    }
                }

                base.Dispose(disposing);
            }
        }

        private void SaveCurrentImageAs()
        {
            SaveImageAsForSlot(_currentSlotIndex);
        }

        private void SaveImageAsForSlot(int slotIndex)
        {
            try
            {
                var bytes = GetImageBytesForSlot(slotIndex);
                var path = GetImagePathForSlot(slotIndex);

                if ((bytes == null || bytes.Length == 0) && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    bytes = File.ReadAllBytes(path);
                }

                if (bytes == null || bytes.Length == 0)
                    return;

                using (var sfd = new SaveFileDialog())
                {
                    sfd.Title = "Save Image As";
                    sfd.Filter = "PNG Image|*.png|JPEG Image|*.jpg;*.jpeg|Bitmap|*.bmp|All Files|*.*";
                    sfd.FileName = $"{GetSlotName(slotIndex)}_{_receiptSetId}.png";
                    if (sfd.ShowDialog(this) != DialogResult.OK)
                        return;

                    File.WriteAllBytes(sfd.FileName, bytes);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Save As failed: " + ex.Message, "Save Image As",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BuildImageContextMenu()
        {
            if (_imageMenu != null)
                return;

            _imageMenuSlotIndex = 0;
            _imageMenu = new ContextMenuStrip { ShowImageMargin = false };

            _miImageOpen = new ToolStripMenuItem("Open image");
            _miImageOpen.Click += (s, e) => OpenImageExternalForSlot(_imageMenuSlotIndex);

            _miImageCopy = new ToolStripMenuItem("Copy image");
            _miImageCopy.Click += (s, e) => CopyImageToClipboard(_imageMenuSlotIndex);

            _miImageCopyPath = new ToolStripMenuItem("Copy image file path");
            _miImageCopyPath.Click += (s, e) => CopyImagePathToClipboard(_imageMenuSlotIndex);

            _miImageSaveAs = new ToolStripMenuItem("Save image as…");
            _miImageSaveAs.Click += (s, e) => SaveImageAsForSlot(_imageMenuSlotIndex);

            _imageMenu.Items.Add(_miImageOpen);
            _imageMenu.Items.Add(_miImageCopy);
            _imageMenu.Items.Add(new ToolStripSeparator());
            _imageMenu.Items.Add(_miImageCopyPath);
            _imageMenu.Items.Add(_miImageSaveAs);

            _imageMenu.Opening += (s, e) =>
            {
                int slotIndex = _currentSlotIndex;
                var source = _imageMenu.SourceControl;
                if (source != null && source.Tag is int idx && idx >= 0 && idx <= 2)
                    slotIndex = idx;

                _imageMenuSlotIndex = slotIndex;

                bool hasImage = HasImageForSlot(slotIndex);
                var slotName = GetSlotName(slotIndex);

                _miImageOpen.Text = $"Open {slotName} image";
                _miImageCopy.Text = $"Copy {slotName} image";
                _miImageSaveAs.Text = $"Save {slotName} image as…";

                _miImageOpen.Enabled = hasImage;
                _miImageCopy.Enabled = hasImage;
                _miImageSaveAs.Enabled = hasImage;

                var path = GetImagePathForSlot(slotIndex);
                bool hasPath = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
                _miImageCopyPath.Enabled = hasPath;
                _miImageCopyPath.ToolTipText = hasPath
                    ? path
                    : "No file path available (image may be stored in the database).";
            };
        }

        private void BuildHistoryContextMenu()
        {
            if (_historyMenu != null)
                return;

            _historyMenu = new ContextMenuStrip { ShowImageMargin = false };

            _miHistoryOpen = new ToolStripMenuItem("Open (read-only)");
            _miHistoryOpen.Click += (s, e) => OpenSelectedHistoryEntry();

            _miHistoryCopy = new ToolStripMenuItem("Copy details");
            _miHistoryCopy.Click += (s, e) => CopySelectedHistoryDetailsToClipboard();

            _miHistoryMakeCurrent = new ToolStripMenuItem("Make current");
            _miHistoryMakeCurrent.Click += (s, e) => MakeSelectedHistoryCurrent();

            _historyMenu.Items.Add(_miHistoryOpen);
            _historyMenu.Items.Add(_miHistoryCopy);
            _historyMenu.Items.Add(new ToolStripSeparator());
            _historyMenu.Items.Add(_miHistoryMakeCurrent);

            _historyMenu.Opening += (s, e) =>
            {
                var entry = GetSelectedHistoryEntry();
                bool hasEntry = entry != null && entry.ReceiptSetId > 0;
                _miHistoryOpen.Enabled = hasEntry;
                _miHistoryCopy.Enabled = hasEntry;
                _miHistoryMakeCurrent.Enabled = hasEntry && entry.Kind == HistoryEntryKind.UnlinkedVersion && !_setId.HasValue;
            };
        }

        private Panel BuildHistoryDetailsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(12)
            };

            var title = new Label
            {
                Text = "Details",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(18, 22, 28)
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 0,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            Label AddRow(string label, out Label value)
            {
                int row = grid.RowCount++;
                grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                var lbl = new Label
                {
                    Text = label,
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(90, 100, 110),
                    Margin = new Padding(0, 6, 8, 0)
                };

                value = new Label
                {
                    Text = "—",
                    AutoSize = true,
                    MaximumSize = new Size(520, 0),
                    Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                    ForeColor = Color.FromArgb(18, 22, 28),
                    Margin = new Padding(0, 6, 0, 0)
                };

                grid.Controls.Add(lbl, 0, row);
                grid.Controls.Add(value, 1, row);
                return lbl;
            }

            AddRow("Supplier", out _lblHistorySupplierValue);
            AddRow("Docs", out _lblHistoryDocsValue);
            AddRow("User", out _lblHistoryUserValue);
            AddRow("Created", out _lblHistoryCreatedValue);
            AddRow("Modified", out _lblHistoryModifiedValue);
            AddRow("Changes", out _lblHistoryChangesValue);

            var thumbsTitle = new Label
            {
                Text = "Images",
                Dock = DockStyle.Top,
                Height = 20,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(90, 100, 110),
                Margin = new Padding(0, 12, 0, 4)
            };

            var thumbs = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 170,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.White,
                Margin = new Padding(0)
            };

            Panel MakeThumb(string label, out PictureBox pb)
            {
                var host = new Panel { Width = 130, Height = 168, Margin = new Padding(0, 0, 10, 0) };
                pb = new PictureBox
                {
                    Width = 130,
                    Height = 148,
                    BorderStyle = BorderStyle.FixedSingle,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.White
                };

                var lbl = new Label
                {
                    Text = label,
                    Dock = DockStyle.Bottom,
                    Height = 18,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                    ForeColor = Color.FromArgb(90, 100, 110)
                };

                host.Controls.Add(pb);
                host.Controls.Add(lbl);
                return host;
            }

            thumbs.Controls.Add(MakeThumb("SI", out _histThumbSi));
            thumbs.Controls.Add(MakeThumb("DR", out _histThumbDr));
            thumbs.Controls.Add(MakeThumb("PO", out _histThumbPo));

            var spacer = new Panel { Dock = DockStyle.Top, Height = 6 };

            panel.Controls.Add(thumbs);
            panel.Controls.Add(thumbsTitle);
            panel.Controls.Add(spacer);
            panel.Controls.Add(grid);
            panel.Controls.Add(title);

            ClearHistoryDetails();
            return panel;
        }

        private static void ConfigureHistorySplit(SplitContainer split, int desiredPanel1Min, int desiredPanel2Min, int preferredPanel1)
        {
            if (split == null)
                return;

            int total = split.Orientation == Orientation.Vertical ? split.Width : split.Height;
            if (total <= 1)
                return;

            int available = total - split.SplitterWidth;
            if (available <= 1)
                return;

            int min1 = Math.Max(0, desiredPanel1Min);
            int min2 = Math.Max(0, desiredPanel2Min);

            // If the window is too small, shrink minimums proportionally to avoid exceptions.
            if (min1 + min2 > available)
            {
                // Keep at least 140px per panel if possible.
                int floor = Math.Min(140, Math.Max(0, available / 2));
                min1 = Math.Max(floor, available - min2);
                min2 = Math.Max(floor, available - min1);

                if (min1 + min2 > available)
                {
                    // Final fallback: split evenly.
                    min1 = Math.Max(0, available / 2);
                    min2 = Math.Max(0, available - min1);
                }
            }

            int maxDistance = Math.Max(min1, available - min2);
            int desiredDistance = preferredPanel1 > 0 ? preferredPanel1 : (int)(available * 0.62);
            if (desiredDistance < min1) desiredDistance = min1;
            if (desiredDistance > maxDistance) desiredDistance = maxDistance;

            try
            {
                // Reset to safe mins before applying new constraints.
                split.Panel1MinSize = 0;
                split.Panel2MinSize = 0;

                // Ensure splitter distance is valid under zero mins.
                int safeDistance = Math.Max(0, Math.Min(desiredDistance, available));
                if (split.SplitterDistance != safeDistance)
                    split.SplitterDistance = safeDistance;

                // Apply min sizes in an order that keeps SplitterDistance valid.
                split.Panel1MinSize = min1;
                if (split.SplitterDistance < min1)
                    split.SplitterDistance = min1;

                split.Panel2MinSize = min2;
                int maxAfter = Math.Max(min1, available - min2);
                if (split.SplitterDistance > maxAfter)
                    split.SplitterDistance = maxAfter;

                if (split.SplitterDistance != desiredDistance)
                    split.SplitterDistance = desiredDistance;
            }
            catch
            {
                // Ignore: layout can temporarily report invalid sizes during initialization.
            }
        }

        private void ClearHistoryDetails()
        {
            if (_lblHistorySupplierValue != null) _lblHistorySupplierValue.Text = "—";
            if (_lblHistoryDocsValue != null) _lblHistoryDocsValue.Text = "—";
            if (_lblHistoryUserValue != null) _lblHistoryUserValue.Text = "—";
            if (_lblHistoryCreatedValue != null) _lblHistoryCreatedValue.Text = "—";
            if (_lblHistoryModifiedValue != null) _lblHistoryModifiedValue.Text = "—";
            if (_lblHistoryChangesValue != null) _lblHistoryChangesValue.Text = "—";

            SetThumbnailImage(_histThumbSi, null, null);
            SetThumbnailImage(_histThumbDr, null, null);
            SetThumbnailImage(_histThumbPo, null, null);
        }

        private void UpdateHistoryDetailsFromSelection()
        {
            var entry = GetSelectedHistoryEntry();
            if (entry == null || entry.Metadata == null)
            {
                ClearHistoryDetails();
                return;
            }

            var dto = entry.Metadata;

            if (_lblHistorySupplierValue != null) _lblHistorySupplierValue.Text = string.IsNullOrWhiteSpace(dto.Supplier) ? "—" : dto.Supplier.Trim();
            if (_lblHistoryDocsValue != null) _lblHistoryDocsValue.Text = BuildDocsMultiline(dto);
            if (_lblHistoryUserValue != null) _lblHistoryUserValue.Text = entry.CreatedByName ?? ResolveUserName(dto.CreatedBy);
            if (_lblHistoryCreatedValue != null) _lblHistoryCreatedValue.Text = $"{dto.CreatedAt:yyyy-MM-dd HH:mm}";
            if (_lblHistoryModifiedValue != null) _lblHistoryModifiedValue.Text = dto.ModifiedAt.HasValue ? $"{dto.ModifiedAt.Value:yyyy-MM-dd HH:mm}" : "—";
            if (_lblHistoryChangesValue != null) _lblHistoryChangesValue.Text = string.IsNullOrWhiteSpace(entry.ChangeSummary) ? "—" : entry.ChangeSummary;

            try
            {
                ReceiptSetDto full = null;
                if (entry.Kind == HistoryEntryKind.LinkedPeriod && _setId.HasValue && entry.Period != null)
                {
                    full = _repository.GetBySetIdCoverage(_setId.Value, entry.Period.StartDate, entry.Period.EndDate);
                }
                else if (entry.ReceiptSetId > 0)
                {
                    full = _repository.GetByReceiptSetId(entry.ReceiptSetId);
                }

                if (full != null)
                {
                    SetThumbnailImage(_histThumbSi, full.SiImagePath, full.SiImage);
                    SetThumbnailImage(_histThumbDr, full.DrImagePath, full.DrImage);
                    SetThumbnailImage(_histThumbPo, full.PoImagePath, full.PoImage);
                }
                else
                {
                    SetThumbnailImage(_histThumbSi, dto.SiImagePath, null);
                    SetThumbnailImage(_histThumbDr, dto.DrImagePath, null);
                    SetThumbnailImage(_histThumbPo, dto.PoImagePath, null);
                }
            }
            catch
            {
                // Non-fatal: keep details visible even if images fail to load.
            }
        }

        private string BuildDocsMultiline(ReceiptSetDto dto)
        {
            if (dto == null)
                return "—";

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(dto.SiNumber)) parts.Add("SI: " + dto.SiNumber.Trim());
            if (!string.IsNullOrWhiteSpace(dto.DrNumber)) parts.Add("DR: " + dto.DrNumber.Trim());
            if (!string.IsNullOrWhiteSpace(dto.PoNumber)) parts.Add("PO: " + dto.PoNumber.Trim());
            if (parts.Count == 0) return "—";
            return string.Join("   ", parts);
        }

        private void CopySelectedHistoryDetailsToClipboard()
        {
            var entry = GetSelectedHistoryEntry();
            if (entry == null || entry.Metadata == null)
                return;

            var dto = entry.Metadata;
            var text = string.Join("\n", new[]
            {
                $"Version/Period: {entry.VersionLabel}",
                $"ReceiptSetId: {dto.ReceiptSetId}",
                $"Supplier: {dto.Supplier}",
                $"SI #: {dto.SiNumber}",
                $"DR #: {dto.DrNumber}",
                $"PO #: {dto.PoNumber}",
                $"Created: {dto.CreatedAt:yyyy-MM-dd HH:mm} ({ResolveUserName(dto.CreatedBy)})",
                $"Modified: {(dto.ModifiedAt.HasValue ? dto.ModifiedAt.Value.ToString("yyyy-MM-dd HH:mm") : "—")} ({ResolveUserName(dto.ModifiedBy)})",
                $"Images: {FormatImageIndicators(dto)}",
                $"Changed: {entry.ChangeSummary}"
            });

            try
            {
                Clipboard.SetText(text);
            }
            catch
            {
                // Ignore clipboard errors (RDP/permissions).
            }
        }

        private void MakeSelectedHistoryCurrent()
        {
            var entry = GetSelectedHistoryEntry();
            if (entry == null)
                return;

            if (entry.Kind != HistoryEntryKind.UnlinkedVersion || _setId.HasValue)
            {
                MessageBox.Show(this,
                    "Make current is available only for unlinked receipt versions.",
                    "Receipt History",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!_unlinkedHeadReceiptSetId.HasValue || _unlinkedHeadReceiptSetId.Value <= 0)
                return;

            if (entry.ReceiptSetId <= 0 || entry.ReceiptSetId == _unlinkedHeadReceiptSetId.Value)
                return;

            if (MessageBox.Show(this,
                    "Make this version the current receipt?\n\nThis will move newer versions into history.",
                    "Receipt History",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                int head = _unlinkedHeadReceiptSetId.Value;
                int target = entry.ReceiptSetId;
                int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;

                int guard = 100;
                while (head != target && guard-- > 0)
                {
                    head = _repository.RevertUnlinkedReceiptSetToPrevious(head, userId);
                }

                _unlinkedHeadReceiptSetId = head;

                var dto = _repository.GetByReceiptSetId(head);
                if (dto != null)
                    LoadFromDto(dto);

                InitializeUnlinkedRenewalTabsForReceiptSet(head);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Failed to make version current.\n\n" + ex.Message,
                    "Receipt History",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void CopyImageToClipboard(int slotIndex)
        {
            try
            {
                var bytes = GetImageBytesForSlot(slotIndex);
                var path = GetImagePathForSlot(slotIndex);

                if ((bytes == null || bytes.Length == 0) && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    bytes = File.ReadAllBytes(path);
                }

                if (bytes == null || bytes.Length == 0)
                {
                    MessageBox.Show(this, "No image to copy.", "Copy Image", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var ms = new MemoryStream(bytes))
                using (var img = Image.FromStream(ms))
                {
                    Clipboard.SetImage(new Bitmap(img));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Copy failed: " + ex.Message, "Copy Image", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CopyImagePathToClipboard(int slotIndex)
        {
            try
            {
                var path = GetImagePathForSlot(slotIndex);
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    MessageBox.Show(this, "No image file path available.", "Copy Image Path", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Clipboard.SetText(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Copy path failed: " + ex.Message, "Copy Image Path", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateImageTooltips()
        {
            if (_toolTip == null)
                return;

            _toolTip.SetToolTip(_thumbSi, BuildImageTooltipText(0));
            _toolTip.SetToolTip(_thumbDr, BuildImageTooltipText(1));
            _toolTip.SetToolTip(_thumbPo, BuildImageTooltipText(2));

            var currentText = BuildImageTooltipText(_currentSlotIndex, includePathIfAvailable: true);
            if (_picPreview != null) _toolTip.SetToolTip(_picPreview, currentText);
            if (_previewScrollPanel != null) _toolTip.SetToolTip(_previewScrollPanel, currentText);
            if (_emptyStatePanel != null) _toolTip.SetToolTip(_emptyStatePanel, currentText);
            if (_lblImageInfo != null) _toolTip.SetToolTip(_lblImageInfo, currentText);
        }

        private string BuildImageTooltipText(int slotIndex, bool includePathIfAvailable = false)
        {
            string slotName = GetSlotName(slotIndex);
            var bytes = GetImageBytesForSlot(slotIndex);
            var path = GetImagePathForSlot(slotIndex);

            bool hasFile = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
            bool hasBytes = bytes != null && bytes.Length > 0;
            if (!hasBytes && !hasFile)
                return $"{slotName}: No image attached.";

            long sizeBytes = 0;
            string source = hasBytes ? "Stored" : "File";

            if (hasBytes)
                sizeBytes = bytes.Length;
            else
                sizeBytes = hasFile ? new FileInfo(path).Length : 0;

            int w = 0, h = 0;
            if (slotIndex == _currentSlotIndex && _picPreview?.Image != null)
            {
                w = _picPreview.Image.Width;
                h = _picPreview.Image.Height;
            }
            else
            {
                try
                {
                    if (hasBytes)
                    {
                        using (var ms = new MemoryStream(bytes))
                        using (var img = Image.FromStream(ms))
                        {
                            w = img.Width;
                            h = img.Height;
                        }
                    }
                    else if (hasFile)
                    {
                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var img = Image.FromStream(fs))
                        {
                            w = img.Width;
                            h = img.Height;
                        }
                    }
                }
                catch
                {
                    w = 0;
                    h = 0;
                }
            }

            var name = hasFile ? Path.GetFileName(path) : $"{slotName} image";
            var dim = (w > 0 && h > 0) ? $"{w}×{h}px" : "Unknown size";
            var text = $"{slotName}: {dim} • {FormatBytes(sizeBytes)} • {source}\n{name}";

            if (includePathIfAvailable && hasFile)
                text += "\n" + path;

            return text;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            if (bytes < 1024) return bytes + " B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return $"{kb:0.#} KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return $"{mb:0.#} MB";
            double gb = mb / 1024.0;
            return $"{gb:0.#} GB";
        }

        private void Preview_DragEnter(object sender, DragEventArgs e)
        {
            if (_readOnly)
                return;

            if (e.Data != null && (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Bitmap)))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void Preview_DragDrop(object sender, DragEventArgs e)
        {
            if (_readOnly)
                return;

            try
            {
                if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0 && File.Exists(files[0]))
                    {
                        var bytes = File.ReadAllBytes(files[0]);
                        AttachImageForSlotFromBytes(_currentSlotIndex, bytes, originalPath: files[0]);
                        return;
                    }
                }

                if (e.Data != null && e.Data.GetDataPresent(DataFormats.Bitmap))
                {
                    var bmp = e.Data.GetData(DataFormats.Bitmap) as Image;
                    if (bmp != null)
                    {
                        using (bmp)
                        using (var ms = new MemoryStream())
                        {
                            bmp.Save(ms, ImageFormat.Png);
                            AttachImageForSlotFromBytes(_currentSlotIndex, ms.ToArray(), originalPath: null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Drop failed: " + ex.Message, "Attach Image",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Preview_MouseWheel(object sender, MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) != Keys.Control)
                return;

            if (e.Delta > 0)
                ZoomBy(1.1);
            else
                ZoomBy(0.9);
        }

        private void ZoomBy(double factor)
        {
            SetZoom(_zoom * factor);
        }

        private void ZoomFit()
        {
            SetZoomToFit();
        }

        private void Zoom100()
        {
            SetZoom(1.0);
        }

        private void SetZoom(double zoom)
        {
            _zoom = Math.Max(0.1, Math.Min(6.0, zoom));
            ApplyZoom();
        }

        private void SetZoomToFit()
        {
            if (_previewScrollPanel == null || _picPreview == null || _picPreview.Image == null)
                return;

            var client = _previewScrollPanel.ClientSize;
            if (client.Width <= 20 || client.Height <= 20)
                return;

            var img = _picPreview.Image;
            var scaleX = (double)(client.Width - 20) / img.Width;
            var scaleY = (double)(client.Height - 20) / img.Height;
            SetZoom(Math.Min(scaleX, scaleY));
        }

        private void ApplyZoom()
        {
            if (_previewScrollPanel == null || _picPreview == null || _picPreview.Image == null)
                return;

            var img = _picPreview.Image;
            var w = Math.Max(1, (int)Math.Round(img.Width * _zoom));
            var h = Math.Max(1, (int)Math.Round(img.Height * _zoom));
            _picPreview.Size = new Size(w, h);

            // Reset scroll so zoom doesn't jump unpredictably
            _previewScrollPanel.AutoScrollPosition = new Point(0, 0);
            CenterPreviewIfSmaller();
        }

        private void CenterPreviewIfSmaller()
        {
            if (_previewScrollPanel == null || _picPreview == null)
                return;

            var client = _previewScrollPanel.ClientSize;
            var x = _picPreview.Width < client.Width ? (client.Width - _picPreview.Width) / 2 : 0;
            var y = _picPreview.Height < client.Height ? (client.Height - _picPreview.Height) / 2 : 0;
            _picPreview.Location = new Point(Math.Max(0, x), Math.Max(0, y));
        }

        private void Preview_MouseDown(object sender, MouseEventArgs e)
        {
            if (_previewScrollPanel == null)
                return;

            if (e.Button != MouseButtons.Left)
                return;

            _panning = true;
            _panMouseOrigin = e.Location;
            _panScrollOrigin = new Point(-_previewScrollPanel.AutoScrollPosition.X, -_previewScrollPanel.AutoScrollPosition.Y);
            Cursor = Cursors.Hand;
        }

        private void Preview_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_panning || _previewScrollPanel == null)
                return;

            var dx = e.Location.X - _panMouseOrigin.X;
            var dy = e.Location.Y - _panMouseOrigin.Y;
            _previewScrollPanel.AutoScrollPosition = new Point(_panScrollOrigin.X - dx, _panScrollOrigin.Y - dy);
        }

        private void Preview_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            _panning = false;
            Cursor = Cursors.Default;
        }

        private string GetImagePathForSlot(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0:
                    return _siImagePath;
                case 1:
                    return _drImagePath;
                case 2:
                    return _poImagePath;
                default:
                    return null;
            }
        }

        private string GetSlotName(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0:
                    return "SI";
                case 1:
                    return "DR";
                case 2:
                    return "PO";
                default:
                    return "Unknown";
            }
        }

        private void ApplyReadOnlyMode()
        {
            _readOnly = true;
            ApplyReadOnlyState();
        }

        private void ApplyReadOnlyState()
        {
            bool ro = _forceReadOnly || _readOnly;
            _readOnly = ro;

            if (_cmbSupplier != null) _cmbSupplier.Enabled = !ro;
            if (_txtSiNumber != null) _txtSiNumber.ReadOnly = ro;
            if (_txtDrNumber != null) _txtDrNumber.ReadOnly = ro;
            if (_txtPoNumber != null) _txtPoNumber.ReadOnly = ro;

            if (_btnAttachSi != null) _btnAttachSi.Enabled = !ro;
            if (_btnAttachDr != null) _btnAttachDr.Enabled = !ro;
            if (_btnAttachPo != null) _btnAttachPo.Enabled = !ro;
            if (_btnUnlinkSet != null) _btnUnlinkSet.Enabled = !ro;
            if (_btnNewReceipt != null) _btnNewReceipt.Enabled = !ro;

            UpdateSaveButtonEnabled();
            UpdateSlotActionButtons();
            UpdateLinkedSetButtons();
        }

        private void OpenCopyLocalReceiptToDbDialog()
        {
            if (_receiptSetId <= 0)
            {
                MessageBox.Show(this, "Save this receipt set before copying its local files to the database.",
                    "Receipt Set", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int? targetSetId = _setId;
            if (!targetSetId.HasValue && _cmbLinkedSets != null && _cmbLinkedSets.SelectedItem is ReceiptSetLinkedSetDto selectedFromList)
            {
                targetSetId = selectedFromList.SetId;
            }

            ReceiptSetLinkedSetDto linkedTarget = null;
            bool linkedTargetIsInvoice = false;

            if (targetSetId.HasValue)
            {
                var info = _repository.GetSetLinkInfo(targetSetId.Value);
                if (info != null)
                {
                    linkedTarget = info;
                    linkedTargetIsInvoice = info.IsInvoice;
                }
            }

            using (var dialog = new CopyLocalReceiptToDbDialog(_repository, _receiptSetId, linkedTarget, linkedTargetIsInvoice))
            {
                dialog.ShowDialog(this);
            }
        }

        private void OpenLinkedSet()
        {
            int? setIdToOpen = _setId;

            if (!setIdToOpen.HasValue && _cmbLinkedSets != null && _cmbLinkedSets.SelectedItem is ReceiptSetLinkedSetDto selectedFromList)
            {
                setIdToOpen = selectedFromList.SetId;
            }

            if (!setIdToOpen.HasValue && _receiptSetId > 0)
            {
                var selected = PromptSelectLinkedSet();
                if (selected != null)
                {
                    setIdToOpen = selected.SetId;
                }
            }

            if (!setIdToOpen.HasValue)
            {
                MessageBox.Show(this, "This receipt set is not linked to any set.", "Receipt Set",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new ViewSetDetailPage(setIdToOpen.Value))
            {
                dialog.ShowDialog(this);
            }
        }

        private void UnlinkFromSet()
        {
            if (_readOnly)
            {
                return;
            }

            if (_receiptSetId <= 0)
            {
                return;
            }

            int? setIdToUnlink = _setId;

            if (!setIdToUnlink.HasValue)
            {
                if (_cmbLinkedSets != null && _cmbLinkedSets.SelectedItem is ReceiptSetLinkedSetDto selectedFromList)
                {
                    setIdToUnlink = selectedFromList.SetId;
                }
                else
                {
                    var selected = PromptSelectLinkedSet();
                    if (selected == null)
                    {
                        return;
                    }

                    setIdToUnlink = selected.SetId;
                }
            }

            var confirm = MessageBox.Show(this,
                "Unlink this receipt set from the current set?\n\nThe receipt set will remain saved as an unlinked receipt.",
                "Unlink Receipt Set",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
            _repository.UnlinkReceiptSet(_receiptSetId, setIdToUnlink.Value, userId);

            var remaining = _repository.GetLinkedSets(_receiptSetId);
            if (remaining != null && remaining.Count == 1)
            {
                _setId = remaining[0].SetId;
            }
            else
            {
                _setId = null;
            }

            if (_btnOpenLinkedSet != null) _btnOpenLinkedSet.Enabled = remaining != null && remaining.Count > 0;
            if (_btnUnlinkSet != null) _btnUnlinkSet.Enabled = remaining != null && remaining.Count > 0;
            RefreshLinkedSetsUi();

            if (_setId.HasValue)
            {
                InitializeCoverageTabsForSet(_setId.Value);
            }
            else
            {
                _activeCoverage = null;
                if (_receiptSetId > 0)
                    InitializeUnlinkedRenewalTabsForReceiptSet(_receiptSetId);
                else
                    DisableCoverageTabsUi();
                if (_tabPeriods != null && _tpCurrent != null)
                    _tabPeriods.SelectedTab = _tpCurrent;
                UpdateCoverageHeader();
                UpdateRenewalIndicator();
            }

            MessageBox.Show(this, "Receipt set has been unlinked from the set.", "Receipt Set",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private ReceiptSetLinkedSetDto PromptSelectLinkedSet()
        {
            if (_receiptSetId <= 0)
            {
                return null;
            }

            var linked = _repository.GetLinkedSets(_receiptSetId);
            if (linked == null || linked.Count == 0)
            {
                return null;
            }

            if (linked.Count == 1)
            {
                return linked[0];
            }

            using (var dialog = new SelectSetDialog(linked))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    return dialog.SelectedSet;
                }
                return null;
            }
        }

        private void LoadExistingForSet(int setId)
        {
            try
            {
                var existing = _repository.GetBySetId(setId);
                if (existing == null)
                {
                    _currentSlotIndex = 0;
                    ShowCurrentImage();
                    InitializeCoverageTabsForSet(setId);
                    return;
                }

                _suppressDirty = true;
                _receiptSetId = existing.ReceiptSetId;
                _setId = existing.SetId;

                _cmbSupplier.Text = existing.Supplier ?? string.Empty;
                _txtSiNumber.Text = existing.SiNumber ?? string.Empty;
                _txtDrNumber.Text = existing.DrNumber ?? string.Empty;
                _txtPoNumber.Text = existing.PoNumber ?? string.Empty;

                _siImagePath = existing.SiImagePath;
                _drImagePath = existing.DrImagePath;
                _poImagePath = existing.PoImagePath;
                _siImageBytes = existing.SiImage;
                _drImageBytes = existing.DrImage;
                _poImageBytes = existing.PoImage;

                _suppressDirty = false;
                SetDirty(false);
                RefreshLinkedSetsUi();
                UpdateSlotActionButtons();

                if (HasImageForSlot(0))
                {
                    _currentSlotIndex = 0;
                }
                else if (HasImageForSlot(1))
                {
                    _currentSlotIndex = 1;
                }
                else if (HasImageForSlot(2))
                {
                    _currentSlotIndex = 2;
                }
                else
                {
                    _currentSlotIndex = 0;
                }

                ShowCurrentImage();
                InitializeCoverageTabsForSet(setId);
            }
            catch (Exception ex)
            {
                _suppressDirty = false;
                MessageBox.Show(this,
                    "Failed to load existing receipt set: " + ex.Message,
                    "Receipt Set",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private enum HistoryEntryKind
        {
            UnlinkedVersion,
            LinkedPeriod
        }

        private sealed class HistoryEntry
        {
            public HistoryEntryKind Kind { get; set; }
            public int ReceiptSetId { get; set; }
            public ReceiptSetRepository.CoveragePeriod Period { get; set; }
            public int DepthFromHead { get; set; } // 1 = Previous, 2 = older, ...
            public ReceiptSetDto Metadata { get; set; }
            public string CreatedByName { get; set; }
            public string ModifiedByName { get; set; }
            public string ChangeSummary { get; set; }
            public string VersionLabel { get; set; }
            public DateTime SavedAt { get; set; }
        }

        private void DisableCoverageTabsUi()
        {
            if (_tpCurrent != null) _tpCurrent.Text = "Receipt";
            if (_tpPrevious != null) _tpPrevious.Enabled = false;
            if (_tpHistory != null) _tpHistory.Enabled = false;
        }

        private void InitializeUnlinkedRenewalTabsForReceiptSet(int headReceiptSetId)
        {
            if (_tabPeriods == null || !_coverageTabsEnabled)
                return;

            DisableCoverageTabsUi();

            _unlinkedHeadReceiptSetId = headReceiptSetId > 0 ? (int?)headReceiptSetId : null;
            _unlinkedPreviousReceiptSetId = null;
            _unlinkedHistoryReceipts = new List<ReceiptSetDto>();

            if (!_unlinkedHeadReceiptSetId.HasValue)
            {
                PopulateUnlinkedHistoryList();
                return;
            }

            try
            {
                var chain = _repository.GetReceiptRenewalChainMetadata(_unlinkedHeadReceiptSetId.Value) ?? new List<ReceiptSetDto>();
                _unlinkedChainReceipts = chain ?? new List<ReceiptSetDto>();
                if (chain.Count > 1 && chain[1] != null && chain[1].ReceiptSetId > 0)
                    _unlinkedPreviousReceiptSetId = chain[1].ReceiptSetId;

                if (chain.Count > 1)
                {
                    _unlinkedHistoryReceipts = chain
                        .Skip(1) // include Previous + older versions in History list
                        .Where(x => x != null && x.ReceiptSetId > 0)
                        .ToList();
                }

                if (_tpPrevious != null) _tpPrevious.Enabled = _unlinkedPreviousReceiptSetId.HasValue;
                if (_tpHistory != null) _tpHistory.Enabled = _unlinkedHistoryReceipts != null && _unlinkedHistoryReceipts.Count > 0;

                PopulateUnlinkedHistoryList();
            }
            catch
            {
                PopulateUnlinkedHistoryList();
            }
            finally
            {
                UpdateRevertButtonEnabled();
            }
        }

        private void PopulateUnlinkedHistoryList()
        {
            if (_lvHistory == null)
                return;

            _lvHistory.BeginUpdate();
            try
            {
                _lvHistory.Items.Clear();

                if (_unlinkedHistoryReceipts == null || _unlinkedHistoryReceipts.Count == 0)
                    return;

                var entries = BuildUnlinkedHistoryEntries();
                PopulateHistoryListView(entries);
            }
            finally
            {
                _lvHistory.EndUpdate();
            }
        }

        private List<HistoryEntry> BuildUnlinkedHistoryEntries()
        {
            var entries = new List<HistoryEntry>();

            if (_unlinkedChainReceipts == null || _unlinkedChainReceipts.Count == 0)
                _unlinkedChainReceipts = new List<ReceiptSetDto>(_unlinkedHistoryReceipts);

            // _unlinkedHistoryReceipts = chain.Skip(1) so align via index to get the "newer" version for change summary.
            for (int i = 0; i < _unlinkedHistoryReceipts.Count; i++)
            {
                var older = _unlinkedHistoryReceipts[i];
                if (older == null || older.ReceiptSetId <= 0)
                    continue;

                ReceiptSetDto newer = null;
                int newerIndex = i; // newer sits one position before in the full chain
                if (_unlinkedChainReceipts != null && _unlinkedChainReceipts.Count > newerIndex && newerIndex - 0 >= 0)
                {
                    // If _unlinkedChainReceipts includes head, the newer version for history[i] is chain[i] (head=0, prev=1, ...)
                    newer = _unlinkedChainReceipts.Count > (i + 0) ? _unlinkedChainReceipts[i] : null;
                }

                var savedAt = (older.ModifiedAt ?? older.CreatedAt);
                entries.Add(new HistoryEntry
                {
                    Kind = HistoryEntryKind.UnlinkedVersion,
                    ReceiptSetId = older.ReceiptSetId,
                    DepthFromHead = i + 1,
                    Metadata = older,
                    VersionLabel = i == 0 ? "Prev" : $"v{i + 1}",
                    SavedAt = savedAt,
                    ChangeSummary = BuildChangeSummary(newer, older)
                });
            }

            return entries;
        }

        private void PopulateHistoryListView(List<HistoryEntry> entries)
        {
            if (_lvHistory == null)
                return;

            _lvHistory.Items.Clear();

            if (entries == null || entries.Count == 0)
            {
                ClearHistoryDetails();
                return;
            }

            var ids = entries
                .Where(e => e?.Metadata != null)
                .SelectMany(e =>
                {
                    var list = new List<int>();
                    if (e.Metadata.CreatedBy.HasValue) list.Add(e.Metadata.CreatedBy.Value);
                    if (e.Metadata.ModifiedBy.HasValue) list.Add(e.Metadata.ModifiedBy.Value);
                    return list;
                })
                .ToList();

            try
            {
                _historyUserNames = _repository.GetUserNamesByIds(ids) ?? new Dictionary<int, string>();
            }
            catch
            {
                _historyUserNames = new Dictionary<int, string>();
            }

            IEnumerable<HistoryEntry> ordered;
            if (entries.All(e => e.Kind == HistoryEntryKind.UnlinkedVersion))
                ordered = entries.OrderBy(e => e.DepthFromHead);
            else if (entries.All(e => e.Kind == HistoryEntryKind.LinkedPeriod))
                ordered = entries.OrderByDescending(e => e.Period?.StartDate ?? DateTime.MinValue);
            else
                ordered = entries.OrderByDescending(e => e.SavedAt);

            foreach (var entry in ordered)
            {
                var meta = entry.Metadata;
                entry.CreatedByName = ResolveUserName(meta?.CreatedBy);
                entry.ModifiedByName = ResolveUserName(meta?.ModifiedBy);

                var item = new ListViewItem(entry.VersionLabel ?? "—");
                item.SubItems.Add(entry.SavedAt == default ? "—" : entry.SavedAt.ToString("yyyy-MM-dd HH:mm"));
                item.SubItems.Add(string.IsNullOrWhiteSpace(meta?.Supplier) ? "—" : meta.Supplier.Trim());
                item.SubItems.Add(FormatDocColumn(meta));
                item.SubItems.Add(FormatImageIndicators(meta));
                item.SubItems.Add(entry.CreatedByName);
                item.SubItems.Add(string.IsNullOrWhiteSpace(entry.ChangeSummary) ? "—" : entry.ChangeSummary);
                item.Tag = entry;
                _lvHistory.Items.Add(item);
            }

            if (_lvHistory.Items.Count > 0)
                _lvHistory.Items[0].Selected = true;
        }

        private string ResolveUserName(int? userId)
        {
            if (!userId.HasValue || userId.Value <= 0)
                return "—";

            if (_historyUserNames != null
                && _historyUserNames.TryGetValue(userId.Value, out var name)
                && !string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }

            return "#" + userId.Value;
        }

        private string FormatDocColumn(ReceiptSetDto dto)
        {
            if (dto == null)
                return "—";

            var si = (dto.SiNumber ?? string.Empty).Trim();
            var dr = (dto.DrNumber ?? string.Empty).Trim();
            var po = (dto.PoNumber ?? string.Empty).Trim();

            bool hasSi = !string.IsNullOrWhiteSpace(si);
            bool hasDr = !string.IsNullOrWhiteSpace(dr);
            bool hasPo = !string.IsNullOrWhiteSpace(po);

            if (!hasSi && !hasDr && !hasPo)
                return "—";

            if (hasSi && hasDr && hasPo
                && string.Equals(si, dr, StringComparison.OrdinalIgnoreCase)
                && string.Equals(si, po, StringComparison.OrdinalIgnoreCase))
            {
                return si;
            }

            var parts = new List<string>();
            if (hasSi) parts.Add("SI:" + si);
            if (hasDr) parts.Add("DR:" + dr);
            if (hasPo) parts.Add("PO:" + po);
            return string.Join("  ", parts);
        }

        private string FormatImageIndicators(ReceiptSetDto dto)
        {
            if (dto == null)
                return "—";

            bool si = !string.IsNullOrWhiteSpace(dto.SiImagePath);
            bool dr = !string.IsNullOrWhiteSpace(dto.DrImagePath);
            bool po = !string.IsNullOrWhiteSpace(dto.PoImagePath);

            string Mark(bool has) => has ? "✓" : "—";
            return $"SI{Mark(si)} DR{Mark(dr)} PO{Mark(po)}";
        }

        private string BuildChangeSummary(ReceiptSetDto newer, ReceiptSetDto older)
        {
            if (older == null)
                return null;

            if (newer == null)
                return "—";

            bool Eq(string a, string b) => string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
            bool Has(string x) => !string.IsNullOrWhiteSpace(x);

            var changes = new List<string>();
            if (!Eq(newer.Supplier, older.Supplier)) changes.Add("Supplier");
            if (!Eq(newer.SiNumber, older.SiNumber)) changes.Add("SI#");
            if (!Eq(newer.DrNumber, older.DrNumber)) changes.Add("DR#");
            if (!Eq(newer.PoNumber, older.PoNumber)) changes.Add("PO#");
            if (Has(newer.SiImagePath) != Has(older.SiImagePath)) changes.Add("SI img");
            if (Has(newer.DrImagePath) != Has(older.DrImagePath)) changes.Add("DR img");
            if (Has(newer.PoImagePath) != Has(older.PoImagePath)) changes.Add("PO img");

            if (changes.Count == 0)
                return "—";

            if (changes.Count <= 3)
                return string.Join(", ", changes);

            return string.Join(", ", changes.Take(3)) + $" +{changes.Count - 3}";
        }

        private void InitializeCoverageTabsForSet(int setId)
        {
            if (_tabPeriods == null || !_coverageTabsEnabled)
                return;

            try
            {
                if (_tpCurrent != null)
                    _tpCurrent.Text = "Current";

                _coveragePromptShown = false;
                var renewedPeriods = _repository.GetRenewedCoveragePeriodsForSet(setId) ?? new List<ReceiptSetRepository.CoveragePeriod>();
                var setPeriod = _repository.GetSetCoveragePeriod(setId);

                if (renewedPeriods.Count > 0)
                {
                    _currentCoverage = renewedPeriods[0];
                    _previousCoverage = renewedPeriods.Count > 1 ? renewedPeriods[1] : null;
                    _historyCoverages = renewedPeriods.Count > 2 ? renewedPeriods.Skip(2).ToList() : new List<ReceiptSetRepository.CoveragePeriod>();
                }
                else
                {
                    _currentCoverage = setPeriod;
                    _previousCoverage = null;
                    _historyCoverages = new List<ReceiptSetRepository.CoveragePeriod>();
                }

                _activeCoverage = _currentCoverage;

                if (_tpPrevious != null) _tpPrevious.Enabled = _previousCoverage != null;
                if (_tpHistory != null) _tpHistory.Enabled = _historyCoverages != null && _historyCoverages.Count > 0;

                PopulateHistoryList();
                UpdateCoverageHeader();
                UpdateRenewalIndicator();

                if (_tabPeriods.SelectedIndex < 0)
                    _tabPeriods.SelectedIndex = 0;

                _lastPeriodTabIndex = _tabPeriods.SelectedIndex;
                OnPeriodTabChanged();
            }
            catch
            {
                // If renewals tables/columns are missing, keep viewer usable without period tabs.
            }
        }

        private void PopulateHistoryList()
        {
            if (_lvHistory == null)
                return;

            _lvHistory.BeginUpdate();
            try
            {
                _lvHistory.Items.Clear();

                if (_historyCoverages == null || _historyCoverages.Count == 0)
                    return;

                var entries = BuildLinkedHistoryEntries();
                PopulateHistoryListView(entries);
            }
            finally
            {
                _lvHistory.EndUpdate();
            }
        }

        private List<HistoryEntry> BuildLinkedHistoryEntries()
        {
            var entries = new List<HistoryEntry>();
            if (!_setId.HasValue)
                return entries;

            var currentMeta = new ReceiptSetDto
            {
                Supplier = _cmbSupplier?.Text,
                SiNumber = _txtSiNumber?.Text,
                DrNumber = _txtDrNumber?.Text,
                PoNumber = _txtPoNumber?.Text,
                SiImagePath = _siImagePath,
                DrImagePath = _drImagePath,
                PoImagePath = _poImagePath,
                CreatedAt = DateTime.Now
            };

            foreach (var p in _historyCoverages)
            {
                ReceiptSetDto meta = null;
                try
                {
                    meta = _repository.GetMetadataBySetIdCoverage(_setId.Value, p.StartDate, p.EndDate);
                }
                catch
                {
                    meta = null;
                }

                if (meta == null)
                    continue;

                entries.Add(new HistoryEntry
                {
                    Kind = HistoryEntryKind.LinkedPeriod,
                    ReceiptSetId = meta.ReceiptSetId,
                    Period = p,
                    Metadata = meta,
                    VersionLabel = $"{p.StartDate:yyyy-MM-dd}→{p.EndDate:yyyy-MM-dd}",
                    SavedAt = (meta.ModifiedAt ?? meta.CreatedAt),
                    ChangeSummary = BuildChangeSummary(currentMeta, meta)
                });
            }

            return entries;
        }

        private string FormatCoverageLabel(ReceiptSetRepository.CoveragePeriod period)
        {
            if (period == null)
                return "(unknown period)";

            var yy = $"{period.StartDate:yyyy}-{period.EndDate:yyyy}";
            return $"{yy}  ({period.StartDate:yyyy-MM-dd} → {period.EndDate:yyyy-MM-dd})";
        }

        private void UpdateCoverageHeader()
        {
            if (_lblCoverage == null)
                return;

            if (_activeCoverage == null)
            {
                _lblCoverage.Text = string.Empty;
                return;
            }

            _lblCoverage.Text = $"{_activeCoverage.StartDate:yyyy}-{_activeCoverage.EndDate:yyyy}";
            if (_toolTip != null)
                _toolTip.SetToolTip(_lblCoverage, $"Coverage: {_activeCoverage.StartDate:yyyy-MM-dd} → {_activeCoverage.EndDate:yyyy-MM-dd}");
        }

        private void UpdateRenewalIndicator()
        {
            if (_lblRenewalIndicator == null || !_setId.HasValue)
                return;

            try
            {
                var summary = _repository.GetRenewalSummaryForSet(_setId.Value) ?? new ReceiptSetRepository.RenewalSummary();
                _lblRenewalIndicator.Text = $"Renewed: {summary.RenewedItems}/{summary.TotalItems}";
            }
            catch
            {
                _lblRenewalIndicator.Text = string.Empty;
            }
        }

        private void OnPeriodTabChanged()
        {
            if (_updatingTabs || _tabPeriods == null || !_coverageTabsEnabled)
                return;

            if (_isDirty && _tabPeriods.SelectedIndex != _lastPeriodTabIndex)
            {
                _updatingTabs = true;
                try
                {
                    var confirm = MessageBox.Show(this,
                        "You have unsaved changes in the current receipt.\n\nSwitch periods and discard changes?",
                        "Unsaved Changes",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (confirm != DialogResult.Yes)
                    {
                        _tabPeriods.SelectedIndex = _lastPeriodTabIndex;
                        return;
                    }

                    SetDirty(false);
                }
                finally
                {
                    _updatingTabs = false;
                }
            }

            _lastPeriodTabIndex = _tabPeriods.SelectedIndex;

            if (!_setId.HasValue)
            {
                int headId = _unlinkedHeadReceiptSetId ?? (_receiptSetId > 0 ? (int?)_receiptSetId : null) ?? 0;
                if (_unlinkedHeadReceiptSetId == null && headId > 0)
                {
                    InitializeUnlinkedRenewalTabsForReceiptSet(headId);
                }

                if (_tabPeriods.SelectedTab == _tpCurrent)
                {
                    MoveReceiptHostTo(_tpCurrent);
                    _readOnly = false;
                    ApplyReadOnlyState();

                    if (headId > 0 && _receiptSetId != headId)
                    {
                        try
                        {
                            var dto = _repository.GetByReceiptSetId(headId);
                            if (dto != null)
                            {
                                _unlinkedHeadReceiptSetId = headId;
                                LoadFromDto(dto);
                            }
                        }
                        catch
                        {
                            // Non-fatal
                        }
                    }
                }
                else if (_tabPeriods.SelectedTab == _tpPrevious)
                {
                    MoveReceiptHostTo(_tpPrevious);
                    _readOnly = true;
                    ApplyReadOnlyState();

                    if (_unlinkedPreviousReceiptSetId.HasValue && _receiptSetId != _unlinkedPreviousReceiptSetId.Value)
                    {
                        try
                        {
                            var dto = _repository.GetByReceiptSetId(_unlinkedPreviousReceiptSetId.Value);
                            if (dto != null)
                            {
                                LoadFromDto(dto);
                            }
                        }
                        catch
                        {
                            // Non-fatal
                        }
                    }
                }
                else if (_tabPeriods.SelectedTab == _tpHistory)
                {
                    // Keep host in whichever tab it was; history opens receipts in a separate read-only viewer.
                }

                _activeCoverage = null;
                if (_lblCoverage != null) _lblCoverage.Text = string.Empty;
                if (_lblRenewalIndicator != null) _lblRenewalIndicator.Text = string.Empty;
                return;
            }

            if (_tabPeriods.SelectedTab == _tpCurrent)
            {
                MoveReceiptHostTo(_tpCurrent);
                _activeCoverage = _currentCoverage;
                _readOnly = false;
                ApplyReadOnlyState();
                LoadReceiptForActiveCoverage(promptCreateIfMissing: true);
            }
            else if (_tabPeriods.SelectedTab == _tpPrevious)
            {
                MoveReceiptHostTo(_tpPrevious);
                _activeCoverage = _previousCoverage;
                _readOnly = true;
                ApplyReadOnlyState();
                LoadReceiptForActiveCoverage(promptCreateIfMissing: false);
            }
            else if (_tabPeriods.SelectedTab == _tpHistory)
            {
                // Keep host in whichever tab it was; history opens receipts in a separate read-only viewer.
            }

            UpdateCoverageHeader();
            UpdateRenewalIndicator();
        }

        private void MoveReceiptHostTo(TabPage tabPage)
        {
            if (_receiptHostPanel == null || tabPage == null)
                return;

            if (!ReferenceEquals(_receiptHostPanel.Parent, tabPage))
            {
                _receiptHostPanel.Parent?.Controls.Remove(_receiptHostPanel);
                tabPage.Controls.Add(_receiptHostPanel);
                _receiptHostPanel.Dock = DockStyle.Fill;
                _receiptHostPanel.BringToFront();
            }
        }

        private void LoadReceiptForActiveCoverage(bool promptCreateIfMissing)
        {
            if (!_setId.HasValue)
                return;

            // If no known period, fallback to current behavior.
            var period = _activeCoverage;
            ReceiptSetDto dto = null;

            if (period != null)
            {
                try
                {
                    dto = _repository.GetBySetIdCoverage(_setId.Value, period.StartDate, period.EndDate);
                }
                catch
                {
                    dto = null;
                }
            }

            if (dto == null && promptCreateIfMissing)
            {
                // Fallback for older links that haven't been migrated/backfilled yet (Current tab only)
                try
                {
                    dto = _repository.GetBySetId(_setId.Value);
                }
                catch
                {
                    dto = null;
                }
            }

            if (dto != null)
            {
                LoadFromDto(dto);
                return;
            }

            // No receipt set exists for the selected period.
            if (_readOnly)
            {
                ClearReceiptUi();
                ShowCurrentImage();
                return;
            }

            if (promptCreateIfMissing && !_coveragePromptShown && _activeCoverage != null)
            {
                _coveragePromptShown = true;

                // Only prompt when there is at least one renewal period known (i.e., renewed periods exist).
                var renewedPeriods = _repository.GetRenewedCoveragePeriodsForSet(_setId.Value) ?? new List<ReceiptSetRepository.CoveragePeriod>();
                bool isRenewedPeriod = renewedPeriods.Any(p => p.StartDate == _activeCoverage.StartDate && p.EndDate == _activeCoverage.EndDate);

                if (isRenewedPeriod)
                {
                    var label = $"{_activeCoverage.StartDate:yyyy}-{_activeCoverage.EndDate:yyyy}";
                    var confirm = MessageBox.Show(this,
                        $"This set was renewed for {label}.\n\nNo receipt set exists for this period yet.\n\nCreate a new receipt set now?",
                        "New Renewal Period",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (confirm != DialogResult.Yes)
                    {
                        ClearReceiptUi();
                        ShowCurrentImage();
                        return;
                    }
                }
            }

            // Prepare a new unsaved receipt set for this period.
            ClearReceiptUi();
            SetDirty(false);
            ShowCurrentImage();
        }

        private void ClearReceiptUi()
        {
            _suppressDirty = true;
            try
            {
                _receiptSetId = 0;

                if (_cmbSupplier != null) _cmbSupplier.Text = string.Empty;
                if (_txtSiNumber != null) _txtSiNumber.Text = string.Empty;
                if (_txtDrNumber != null) _txtDrNumber.Text = string.Empty;
                if (_txtPoNumber != null) _txtPoNumber.Text = string.Empty;

                _siImagePath = null;
                _drImagePath = null;
                _poImagePath = null;
                _siImageBytes = null;
                _drImageBytes = null;
                _poImageBytes = null;

                RefreshLinkedSetsUi();
                UpdateSlotActionButtons();
            }
            finally
            {
                _suppressDirty = false;
            }
        }

        private void LoadFromDto(ReceiptSetDto dto)
        {
            if (dto == null)
                return;

            _suppressDirty = true;
            try
            {
                _receiptSetId = dto.ReceiptSetId;
                _setId = dto.SetId ?? _setId;

                if (_cmbSupplier != null) _cmbSupplier.Text = dto.Supplier ?? string.Empty;
                if (_txtSiNumber != null) _txtSiNumber.Text = dto.SiNumber ?? string.Empty;
                if (_txtDrNumber != null) _txtDrNumber.Text = dto.DrNumber ?? string.Empty;
                if (_txtPoNumber != null) _txtPoNumber.Text = dto.PoNumber ?? string.Empty;

                _siImagePath = dto.SiImagePath;
                _drImagePath = dto.DrImagePath;
                _poImagePath = dto.PoImagePath;
                _siImageBytes = dto.SiImage;
                _drImageBytes = dto.DrImage;
                _poImageBytes = dto.PoImage;

                SetDirty(false);
                RefreshLinkedSetsUi();
                UpdateSlotActionButtons();
            }
            finally
            {
                _suppressDirty = false;
            }

            // Pick a slot with an image if available.
            if (HasImageForSlot(0))
                _currentSlotIndex = 0;
            else if (HasImageForSlot(1))
                _currentSlotIndex = 1;
            else if (HasImageForSlot(2))
                _currentSlotIndex = 2;
            else
                _currentSlotIndex = 0;

            ShowCurrentImage();

            if (!_setId.HasValue && _tabPeriods != null && _tpCurrent != null && _tabPeriods.SelectedTab == _tpCurrent && _receiptSetId > 0)
            {
                InitializeUnlinkedRenewalTabsForReceiptSet(_receiptSetId);
            }
        }

        private HistoryEntry GetSelectedHistoryEntry()
        {
            if (_lvHistory == null || _lvHistory.SelectedItems == null || _lvHistory.SelectedItems.Count == 0)
                return null;

            return _lvHistory.SelectedItems[0]?.Tag as HistoryEntry;
        }

        private void OpenSelectedHistoryEntry()
        {
            var entry = GetSelectedHistoryEntry();
            if (entry == null)
                return;

            try
            {
                ReceiptSetDto dto = null;

                if (entry.Kind == HistoryEntryKind.LinkedPeriod && _setId.HasValue && entry.Period != null)
                {
                    dto = _repository.GetBySetIdCoverage(_setId.Value, entry.Period.StartDate, entry.Period.EndDate);
                }
                else if (entry.ReceiptSetId > 0)
                {
                    dto = _repository.GetByReceiptSetId(entry.ReceiptSetId);
                }

                if (dto == null)
                {
                    MessageBox.Show(this,
                        "No receipt set is saved for this history entry.",
                        "Receipt History",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                using (var viewer = new ReceiptSetViewerDialog(dto, readOnly: true))
                {
                    viewer.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Failed to open receipt history.\n\n" + ex.Message,
                    "Receipt History",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async void ReceiptSetViewerDialog_Shown(object sender, EventArgs e)
        {
            TryApplyPendingFit();

            if (_vendorsLoaded || _cmbSupplier == null)
                return;

            try
            {
                var current = (_cmbSupplier.Text ?? string.Empty).Trim();

                var repo = new VendorRepository();
                var vendors = await repo.GetAllVendorsAsync();

                var names = vendors
                    .Where(v => v != null && v.IsActive && !string.IsNullOrWhiteSpace(v.VendorName))
                    .Select(v => v.VendorName.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var prevSuppress = _suppressDirty;
                try
                {
                    _suppressDirty = true;
                    _cmbSupplier.BeginUpdate();
                    try
                    {
                        _cmbSupplier.Items.Clear();
                        foreach (var n in names)
                            _cmbSupplier.Items.Add(n);
                    }
                    finally
                    {
                        _cmbSupplier.EndUpdate();
                    }

                    if (!string.IsNullOrWhiteSpace(current))
                        _cmbSupplier.Text = current;

                    _vendorsLoaded = true;
                }
                finally
                {
                    _suppressDirty = prevSuppress;
                }
            }
            catch
            {
                // Non-fatal; supplier remains editable text.
            }
        }

        private void OpenLinkedInvoiceReport()
        {
            int? setIdToOpen = _setId;

            if (!setIdToOpen.HasValue && _cmbLinkedSets != null && _cmbLinkedSets.SelectedItem is ReceiptSetLinkedSetDto selectedFromList)
            {
                setIdToOpen = selectedFromList.SetId;
            }

            if (!setIdToOpen.HasValue && _receiptSetId > 0)
            {
                var selected = PromptSelectLinkedSet();
                if (selected != null)
                {
                    setIdToOpen = selected.SetId;
                }
            }

            if (!setIdToOpen.HasValue)
            {
                MessageBox.Show(this, "This receipt set is not linked to any set.", "Receipt Set",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                using (var page = new ViewInvoiceDetailPage(setIdToOpen.Value))
                {
                    page.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to open invoice report: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LinkInvoiceReportSet()
        {
            if (_readOnly)
            {
                return;
            }

            if (_receiptSetId <= 0)
            {
                MessageBox.Show(this, "Please save the receipt set first.", "Receipt Set",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var docNo = string.IsNullOrWhiteSpace(_txtSiNumber.Text) ? null : _txtSiNumber.Text.Trim();
            if (string.IsNullOrWhiteSpace(docNo))
            {
                MessageBox.Show(this, "Please enter the SI # first.", "Receipt Set",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var match = _repository.FindInvoiceSetByDocumentNumber(docNo);
                if (match == null || match.SetId <= 0)
                {
                    MessageBox.Show(this, $"No invoice report set found for SI # {docNo}.", "Receipt Set",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                _repository.AttachReceiptSetToSet(_receiptSetId, match.SetId, userId);
                _setId = match.SetId;

                RefreshLinkedSetsUi();
                UpdateLinkedSetButtons();

                OpenLinkedInvoiceReport();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to link invoice report: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private int[] GetAttachedSlots()
        {
            var list = new System.Collections.Generic.List<int>();

            if (HasImageForSlot(0))
            {
                list.Add(0);
            }
            if (HasImageForSlot(1))
            {
                list.Add(1);
            }
            if (HasImageForSlot(2))
            {
                list.Add(2);
            }

            return list.ToArray();
        }

        private void UpdateNavigationButtons()
        {
            var attached = GetAttachedSlots();
            bool hasMultiple = attached.Length > 1;
            if (_btnPrev != null)
            {
                _btnPrev.Enabled = hasMultiple;
            }

            if (_btnNext != null)
            {
                _btnNext.Enabled = hasMultiple;
            }
        }

        private void UpdateSlotTabs()
        {
            if (_tabSlots == null)
                return;

            bool hasSi = HasImageForSlot(0);
            bool hasDr = HasImageForSlot(1);
            bool hasPo = HasImageForSlot(2);

            _updatingTabs = true;
            try
            {
                if (_tabSlots.TabPages.Count == 3)
                {
                    _tabSlots.TabPages[0].Text = $"SI ({(hasSi ? 1 : 0)})";
                    _tabSlots.TabPages[1].Text = $"DR ({(hasDr ? 1 : 0)})";
                    _tabSlots.TabPages[2].Text = $"PO ({(hasPo ? 1 : 0)})";
                }

                if (_currentSlotIndex >= 0 && _currentSlotIndex < _tabSlots.TabPages.Count)
                {
                    _tabSlots.SelectedIndex = _currentSlotIndex;
                }
            }
            finally
            {
                _updatingTabs = false;
            }
        }

        private void UpdateThumbnails()
        {
            if (_thumbSi == null || _thumbDr == null || _thumbPo == null)
            {
                return;
            }

            SetThumbnailImage(_thumbSi, _siImagePath, _siImageBytes);
            SetThumbnailImage(_thumbDr, _drImagePath, _drImageBytes);
            SetThumbnailImage(_thumbPo, _poImagePath, _poImageBytes);

            // Highlight the currently selected slot
            _thumbSi.BackColor = _currentSlotIndex == 0 ? Color.FromArgb(224, 240, 255) : Color.White;
            _thumbDr.BackColor = _currentSlotIndex == 1 ? Color.FromArgb(224, 240, 255) : Color.White;
            _thumbPo.BackColor = _currentSlotIndex == 2 ? Color.FromArgb(224, 240, 255) : Color.White;

            UpdateImageTooltips();
        }

        private void SetThumbnailImage(PictureBox thumb, string path, byte[] bytes)
        {
            if (thumb == null)
            {
                return;
            }

            // Reset existing image/location
            if (thumb.Image != null)
            {
                var old = thumb.Image;
                thumb.Image = null;
                old.Dispose();
            }
            thumb.ImageLocation = null;

            if (bytes != null && bytes.Length > 0)
            {
                try
                {
                    thumb.Image = CreateImageFromBytes(bytes);
                }
                catch
                {
                    thumb.Image = null;
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Image.FromStream(fs))
                {
                    thumb.Image = new Bitmap(img);
                }
            }
            catch
            {
                thumb.Image = null;
            }
        }

        private byte[] GetImageBytesForSlot(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0:
                    return _siImageBytes;
                case 1:
                    return _drImageBytes;
                case 2:
                    return _poImageBytes;
                default:
                    return null;
            }
        }

        private bool HasImageForSlot(int slotIndex)
        {
            var bytes = GetImageBytesForSlot(slotIndex);
            if (bytes != null && bytes.Length > 0)
            {
                return true;
            }

            string path = GetImagePathForSlot(slotIndex);
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        private void ApplyRoundedButtonStyle(Button button, bool primary = false)
        {
            if (button == null)
                return;

            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;

            Color backColor;
            Color foreColor;
            Color borderColor;

            if (primary)
            {
                backColor = Color.FromArgb(52, 152, 219);
                foreColor = Color.White;
                borderColor = Color.FromArgb(41, 128, 185);
            }
            else
            {
                backColor = Color.White;
                foreColor = Color.FromArgb(41, 128, 185);
                borderColor = Color.FromArgb(41, 128, 185);
            }

            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.FlatAppearance.BorderColor = borderColor;

            button.Resize += (s, e) =>
            {
                var rect = button.ClientRectangle;
                rect.Inflate(-1, -1);
                int radius = 8;

                using (var path = new GraphicsPath())
                {
                    path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                    path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
                    path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
                    path.CloseFigure();
                    button.Region = new Region(path);
                }
            };
        }

        private string GetImageCounterText()
        {
            var attached = GetAttachedSlots();
            if (attached.Length <= 1)
            {
                return string.Empty;
            }

            int index = Array.IndexOf(attached, _currentSlotIndex);
            if (index < 0)
            {
                return string.Empty;
            }

            return $"({index + 1}/{attached.Length})";
        }
    }
}
