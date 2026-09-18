using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages
{
    public class SupervisorApprovalPage : UserControl
    {
        // ── Dependencies ─────────────────────────────────────────────────────
        private readonly int                              _supervisorUserId;
        private readonly CartridgeAuthorizationRepository _repo;

        // ── State ─────────────────────────────────────────────────────────────
        private List<CartridgeAuthorizationModel> _pending;
        private CartridgeAuthorizationModel       _selected;

        // ── Left panel ────────────────────────────────────────────────────────
        private AuthQueueListBox _lstQueue;
        private Label            _lblQueueStatus;
        private Button           _btnRefresh;

        // ── Right panel ───────────────────────────────────────────────────────
        private Panel  _scrollOuter;
        private Label  _lblEmpName;
        private Label  _lblDept;
        private Label  _lblBranch;
        private Label  _lblPosition;
        private Label  _lblRequested;
        private Label  _lblModels;

        // Auth Statement Preview
        private Panel        _pnlPreview;
        private RichTextBox  _lblPreviewStatement;
        private DataGridView _dgvPreviewModels;
        private Label        _lblNotedBy;
        private PictureBox   _pbPreviewSig;
        private Label        _lblPreviewSigPlaceholder;
        private Panel        _pnlSigLine;
        private Label        _lblSignerName;
        private Label        _lblSignerPos;
        private Label        _lblSignerDeptBranch;
        private Label        _lblPreviewDate;

        // Signature
        private TabControl        _tabSignature;
        private DoubleBufferPanel _canvasPanel;
        private Bitmap            _canvasBitmap;
        private Point             _lastPoint;
        private bool              _isDrawing;

        private PictureBox _uploadPreview;
        private Button     _btnDeleteUpload;
        private byte[]     _uploadedBytes;
        private string     _uploadedMime;

        // Action area
        private TextBox _txtNotes;
        private Button  _btnApprove;
        private Button  _btnReject;
        private Label   _lblStatus;
        private Panel   _pnlApprovedBanner;
        private Label   _lblApprovedMsg;

        // Flex-width controls that resize with the right panel
        private readonly List<Control> _flexControls = new List<Control>();

        // ── Constructor ───────────────────────────────────────────────────────

        public SupervisorApprovalPage(
            int supervisorUserId,
            CartridgeAuthorizationRepository repo)
        {
            _supervisorUserId = supervisorUserId;
            _repo             = repo ?? throw new ArgumentNullException(nameof(repo));

            Dock      = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 250, 252);

            BuildUi();
            _ = LoadQueueAsync();
        }

        // ── UI ───────────────────────────────────────────────────────────────

        private void BuildUi()
        {
            var split = new SplitContainer
            {
                Dock          = DockStyle.Fill,
                Padding       = new Padding(12),
                SplitterWidth = 6,
                BackColor     = Color.FromArgb(248, 250, 252)
            };
            split.SizeChanged += (s, e) =>
            {
                const int p1Min = 260;
                const int p2Min = 420;
                if (split.Width <= p1Min + p2Min) return;
                split.Panel1MinSize   = 0;
                split.Panel2MinSize   = 0;
                split.SplitterDistance = Math.Max(p1Min, Math.Min(380, split.Width - p2Min));
                split.Panel1MinSize   = p1Min;
                split.Panel2MinSize   = p2Min;
            };
            Controls.Add(split);

            BuildQueuePanel(split.Panel1);
            BuildDetailPanel(split.Panel2);
        }

        // ── Left: queue ───────────────────────────────────────────────────────

        private void BuildQueuePanel(SplitterPanel panel)
        {
            var lbl = new Label
            {
                Text      = "Pending Authorizations",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize  = true,
                Location  = new Point(0, 3)
            };
            panel.Controls.Add(lbl);

            _btnRefresh = new Button
            {
                Text      = "⟳ Refresh",
                Font      = new Font("Segoe UI", 8F),
                FlatStyle = FlatStyle.Flat,
                Size      = new Size(80, 28),
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(239, 246, 255),
                ForeColor = Color.FromArgb(27, 58, 107)
            };
            _btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(191, 219, 254);
            _btnRefresh.Click += async (s, e) => await LoadQueueAsync();
            panel.Controls.Add(_btnRefresh);

            _lstQueue = new AuthQueueListBox
            {
                Location       = new Point(0, 30),
                BorderStyle    = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            _lstQueue.SelectedIndexChanged += OnQueueSelectionChanged;
            panel.Controls.Add(_lstQueue);

            _lblQueueStatus = new Label
            {
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location  = new Point(0, 32)
            };
            panel.Controls.Add(_lblQueueStatus);

            panel.Resize += (s, e) =>
            {
                _btnRefresh.Location = new Point(panel.Width - _btnRefresh.Width, 2);
                _lstQueue.Size       = new Size(panel.Width, panel.Height - 32);
            };
        }

        // ── Right: detail ─────────────────────────────────────────────────────

        private void BuildDetailPanel(SplitterPanel panel)
        {
            // Already-approved banner — docked to bottom of the right panel so it is
            // always visible without scrolling when another approver signs first.
            _pnlApprovedBanner = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 88,
                BackColor = Color.FromArgb(220, 252, 231),
                Padding   = new Padding(16, 14, 16, 14),
                Visible   = false
            };
            _pnlApprovedBanner.Paint += (s, e) =>
                e.Graphics.DrawLine(
                    new Pen(Color.FromArgb(34, 197, 94), 3),
                    0, 0, _pnlApprovedBanner.Width, 0);
            _lblApprovedMsg = new Label
            {
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(21, 128, 61),
                Text      = ""
            };
            _pnlApprovedBanner.Controls.Add(_lblApprovedMsg);

            _scrollOuter = new Panel
            {
                Dock       = DockStyle.Fill,
                AutoScroll = true,
                BackColor  = Color.White,
                Padding    = new Padding(16)
            };
            // Add Fill first, then Bottom — WinForms docks in reverse add order
            panel.Controls.Add(_scrollOuter);
            panel.Controls.Add(_pnlApprovedBanner);

            // When the right panel is resized, update all flex-width controls
            _scrollOuter.Resize += (s, e) => ApplyFlexWidths();

            int y        = 16;
            const int lx = 16;
            const int vx = 150;

            Label Lbl(string text, Font font, Color color)
            {
                var l = new Label
                {
                    Text      = text,
                    Font      = font,
                    ForeColor = color,
                    AutoSize  = true,
                    Location  = new Point(lx, y)
                };
                _scrollOuter.Controls.Add(l);
                return l;
            }

            _lblEmpName = Lbl("Select a request from the list",
                new Font("Segoe UI", 12F, FontStyle.Bold), Color.FromArgb(30, 41, 59));
            y += 34;

            void InfoRow(string caption, ref Label val)
            {
                _scrollOuter.Controls.Add(new Label
                {
                    Text      = caption,
                    Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Location  = new Point(lx, y),
                    Size      = new Size(130, 20)
                });
                val = new Label
                {
                    Text      = "—",
                    Font      = new Font("Segoe UI", 8.5F),
                    ForeColor = Color.FromArgb(30, 41, 59),
                    AutoSize  = true,
                    Location  = new Point(vx, y)
                };
                _scrollOuter.Controls.Add(val);
                y += 22;
            }

            InfoRow("Department:",   ref _lblDept);
            InfoRow("Branch:",       ref _lblBranch);
            InfoRow("Position:",     ref _lblPosition);
            InfoRow("Requested On:", ref _lblRequested);

            y += 4;
            AddSep(lx, y); y += 12;

            Lbl("Requested Cartridges:",
                new Font("Segoe UI", 8.5F, FontStyle.Bold), Color.FromArgb(100, 116, 139));
            y += 20;

            _lblModels = new Label
            {
                Text      = "—",
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(71, 85, 105),
                AutoSize  = false,
                Location  = new Point(lx, y),
                Size      = new Size(FlexWidth(), 64)
            };
            _scrollOuter.Controls.Add(_lblModels);
            _flexControls.Add(_lblModels);
            y += 74;

            // ── Auth Statement Preview ────────────────────────────────────────
            AddSep(lx, y); y += 10;

            _scrollOuter.Controls.Add(new Label
            {
                Text      = "AUTHORIZATION STATEMENT PREVIEW",
                Font      = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize  = true,
                Location  = new Point(lx, y)
            });
            y += 24;

            _pnlPreview = new Panel
            {
                Location  = new Point(lx, y),
                Size      = new Size(FlexWidth(), 420),
                BackColor = Color.FromArgb(249, 250, 251),
                Padding   = new Padding(12)
            };
            _pnlPreview.Paint += (s, e) =>
                e.Graphics.DrawRectangle(new Pen(Color.FromArgb(209, 213, 219)),
                    0, 0, _pnlPreview.Width - 1, _pnlPreview.Height - 1);
            _scrollOuter.Controls.Add(_pnlPreview);
            _flexControls.Add(_pnlPreview);

            // Statement prose — signer identity + requester intro lines only
            _lblPreviewStatement = new RichTextBox
            {
                ReadOnly    = true,
                BorderStyle = BorderStyle.None,
                BackColor   = Color.FromArgb(249, 250, 251),
                Font        = new Font("Segoe UI", 8.5F),
                ForeColor   = Color.FromArgb(50, 50, 50),
                ScrollBars  = RichTextBoxScrollBars.None,
                Width       = PreviewInnerWidth(),
                Height      = 120,
                Location    = new Point(12, 10),
                WordWrap    = true,
                Text        = "(select a request to see the authorization statement)"
            };
            _pnlPreview.Controls.Add(_lblPreviewStatement);

            // Cartridge table — populated by BuildPreviewStatement()
            _dgvPreviewModels = new DataGridView
            {
                Location              = new Point(12, 135),
                Width                 = PreviewInnerWidth(),
                Height                = 78,
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible     = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                AutoGenerateColumns   = false,
                BackgroundColor       = Color.White,
                BorderStyle           = BorderStyle.None,
                GridColor             = Color.FromArgb(200, 200, 200),
                Font                  = new Font("Segoe UI", 8F),
                ColumnHeadersHeight   = 30,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = false,
                ShowCellToolTips      = false
            };
            _dgvPreviewModels.ColumnHeadersDefaultCellStyle.BackColor  = Color.White;
            _dgvPreviewModels.ColumnHeadersDefaultCellStyle.ForeColor  = Color.Black;
            _dgvPreviewModels.ColumnHeadersDefaultCellStyle.Font       = new Font("Segoe UI", 8F, FontStyle.Bold);
            _dgvPreviewModels.DefaultCellStyle.BackColor                = Color.White;
            _dgvPreviewModels.DefaultCellStyle.ForeColor                = Color.Black;
            _dgvPreviewModels.DefaultCellStyle.SelectionBackColor       = Color.White;
            _dgvPreviewModels.DefaultCellStyle.SelectionForeColor       = Color.Black;
            _dgvPreviewModels.EnableHeadersVisualStyles                 = false;
            _dgvPreviewModels.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "colModel",   HeaderText = "Cartridge Model",      FillWeight = 40 },
                new DataGridViewTextBoxColumn { Name = "colQty",     HeaderText = "Qty",                  FillWeight = 12 },
                new DataGridViewTextBoxColumn { Name = "colGood",    HeaderText = "Submitted (Good)",   FillWeight = 24 },
                new DataGridViewTextBoxColumn { Name = "colDamaged", HeaderText = "Submitted (Damaged)", FillWeight = 24 },
            });
            _dgvPreviewModels.SelectionChanged += (s, e) =>
            {
                _dgvPreviewModels.ClearSelection();
                _dgvPreviewModels.CurrentCell = null;
            };
            _pnlPreview.Controls.Add(_dgvPreviewModels);

            // "Noted by:" label
            _lblNotedBy = new Label
            {
                Text      = "Noted by:",
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = Color.FromArgb(160, 160, 160),
                AutoSize  = true,
                Location  = new Point(12, 228)
            };
            _pnlPreview.Controls.Add(_lblNotedBy);

            // Signature placeholder
            _lblPreviewSigPlaceholder = new Label
            {
                Text      = "(signature will appear here)",
                Font      = new Font("Segoe UI", 7.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(200, 200, 200),
                AutoSize  = true,
                Location  = new Point(14, 246)
            };
            _pnlPreview.Controls.Add(_lblPreviewSigPlaceholder);

            // Signature image (no border — floats above the line)
            _pbPreviewSig = new PictureBox
            {
                Location    = new Point(12, 242),
                Size        = new Size(240, 48),
                SizeMode    = PictureBoxSizeMode.Zoom,
                BackColor   = Color.Transparent,
                BorderStyle = BorderStyle.None,
                Visible     = false
            };
            _pnlPreview.Controls.Add(_pbPreviewSig);

            // Separator line under signature
            _pnlSigLine = new Panel
            {
                Location  = new Point(12, 293),
                Size      = new Size(240, 1),
                BackColor = Color.FromArgb(30, 30, 30)
            };
            _pnlPreview.Controls.Add(_pnlSigLine);

            // Signer name (bold)
            _lblSignerName = new Label
            {
                Text      = "—",
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 20, 20),
                AutoSize  = true,
                Location  = new Point(12, 297)
            };
            _pnlPreview.Controls.Add(_lblSignerName);

            // Signer position (italic)
            _lblSignerPos = new Label
            {
                Text      = "—",
                Font      = new Font("Segoe UI", 7.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(90, 90, 90),
                AutoSize  = true,
                Location  = new Point(12, 319)
            };
            _pnlPreview.Controls.Add(_lblSignerPos);

            // Signer dept · branch · company
            _lblSignerDeptBranch = new Label
            {
                Text      = "—",
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = Color.FromArgb(90, 90, 90),
                AutoSize  = true,
                Location  = new Point(12, 341)
            };
            _pnlPreview.Controls.Add(_lblSignerDeptBranch);

            _lblPreviewDate = new Label
            {
                Text      = $"Date Signed: {DateTime.Now:MMMM dd, yyyy}",
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = Color.FromArgb(107, 114, 128),
                AutoSize  = true,
                Location  = new Point(12, 363)
            };
            _pnlPreview.Controls.Add(_lblPreviewDate);
            y += 434;

            // ── Electronic Signature ──────────────────────────────────────────
            AddSep(lx, y); y += 10;

            _scrollOuter.Controls.Add(new Label
            {
                Text      = "Electronic Signature",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(27, 58, 107),
                AutoSize  = true,
                Location  = new Point(lx, y)
            });
            y += 22;

            _tabSignature = new TabControl
            {
                Location = new Point(lx, y),
                Size     = new Size(FlexWidth(), 230),
                Font     = new Font("Segoe UI", 8.5F)
            };
            _scrollOuter.Controls.Add(_tabSignature);
            _flexControls.Add(_tabSignature);
            y += 240;

            BuildDrawTab();
            BuildUploadTab();

            // ── Notes ─────────────────────────────────────────────────────────
            _scrollOuter.Controls.Add(new Label
            {
                Text      = "Notes (optional for approval; required for rejection):",
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize  = true,
                Location  = new Point(lx, y)
            });
            y += 24;

            _txtNotes = new TextBox
            {
                Location    = new Point(lx, y),
                Size        = new Size(FlexWidth(), 52),
                Multiline   = true,
                Font        = new Font("Segoe UI", 8.5F),
                BorderStyle = BorderStyle.FixedSingle
            };
            _scrollOuter.Controls.Add(_txtNotes);
            _flexControls.Add(_txtNotes);
            y += 62;

            // ── Buttons ───────────────────────────────────────────────────────
            _btnApprove = new Button
            {
                Text      = "Approve & Sign",
                Location  = new Point(lx, y),
                Size      = new Size(140, 34),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(21, 128, 61),
                Cursor    = Cursors.Hand,
                Enabled   = false
            };
            _btnApprove.FlatAppearance.BorderSize = 0;
            _btnApprove.Click += async (s, e) => await OnApproveClickAsync();
            _scrollOuter.Controls.Add(_btnApprove);

            _btnReject = new Button
            {
                Text      = "Reject",
                Location  = new Point(lx + 150, y),
                Size      = new Size(100, 34),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(185, 28, 28),
                BackColor = Color.FromArgb(254, 242, 242),
                Cursor    = Cursors.Hand,
                Enabled   = false
            };
            _btnReject.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
            _btnReject.FlatAppearance.BorderSize  = 1;
            _btnReject.Click += async (s, e) => await OnRejectClickAsync();
            _scrollOuter.Controls.Add(_btnReject);

            y += 44;
            _lblStatus = new Label
            {
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location  = new Point(lx, y)
            };
            _scrollOuter.Controls.Add(_lblStatus);
        }

        // Returns available flex width based on current scroll panel client width
        private int FlexWidth() =>
            Math.Max(200, _scrollOuter?.ClientSize.Width - 32 ?? 500);

        // Returns the inner width available inside _pnlPreview
        private int PreviewInnerWidth() =>
            Math.Max(150, (_pnlPreview?.Width ?? 500) - 28);

        private void ApplyFlexWidths()
        {
            int w = FlexWidth();
            foreach (var c in _flexControls)
                c.Width = w;

            // Reflow the preview statement and table widths
            if (_lblPreviewStatement != null)
                _lblPreviewStatement.Width = PreviewInnerWidth();
            if (_dgvPreviewModels != null)
                _dgvPreviewModels.Width = PreviewInnerWidth();

            // Reflow separator lines
            foreach (Control c in _scrollOuter.Controls)
                if (c is Panel p && p.Height == 1)
                    p.Width = w;
        }

        private void AddSep(int x, int y)
        {
            _scrollOuter.Controls.Add(new Panel
            {
                Location  = new Point(x, y),
                Size      = new Size(FlexWidth(), 1),
                BackColor = Color.FromArgb(226, 232, 240)
            });
        }

        // ── Draw Tab ──────────────────────────────────────────────────────────

        private void BuildDrawTab()
        {
            var tab = new TabPage("Draw Signature");
            _tabSignature.TabPages.Add(tab);

            _canvasBitmap = new Bitmap(490, 155);
            using (var g = Graphics.FromImage(_canvasBitmap))
                g.Clear(Color.White);

            _canvasPanel = new DoubleBufferPanel
            {
                Location    = new Point(6, 8),
                Size        = new Size(490, 155),
                BackColor   = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor      = Cursors.Cross
            };
            _canvasPanel.Paint      += (s, e) => e.Graphics.DrawImage(_canvasBitmap, Point.Empty);
            _canvasPanel.MouseDown  += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) { _isDrawing = true; _lastPoint = e.Location; }
            };
            _canvasPanel.MouseMove  += CanvasPanel_MouseMove;
            _canvasPanel.MouseUp    += (s, e) => { _isDrawing = false; UpdateSignaturePreview(); };
            _canvasPanel.MouseLeave += (s, e) => _isDrawing = false;
            tab.Controls.Add(_canvasPanel);

            var btnClear = new Button
            {
                Text      = "Clear",
                Location  = new Point(6, 168),
                Size      = new Size(70, 26),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            btnClear.Click += (s, e) => ClearCanvas();
            tab.Controls.Add(btnClear);

            tab.Controls.Add(new Label
            {
                Text      = "Draw your signature above using mouse or stylus.",
                Font      = new Font("Segoe UI", 7.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize  = true,
                Location  = new Point(82, 173)
            });
        }

        private void CanvasPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing) return;
            using (var g = Graphics.FromImage(_canvasBitmap))
            using (var pen = new Pen(Color.FromArgb(27, 58, 107), 2f)
            {
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap   = System.Drawing.Drawing2D.LineCap.Round
            })
            {
                g.DrawLine(pen, _lastPoint, e.Location);
            }
            _lastPoint = e.Location;
            _canvasPanel.Invalidate();
        }

        private void ClearCanvas()
        {
            using (var g = Graphics.FromImage(_canvasBitmap))
                g.Clear(Color.White);
            _canvasPanel.Invalidate();
            UpdateSignaturePreview();
        }

        private bool IsCanvasBlank()
        {
            for (int x = 0; x < _canvasBitmap.Width; x += 4)
                for (int y = 0; y < _canvasBitmap.Height; y += 4)
                    if (_canvasBitmap.GetPixel(x, y).ToArgb() != Color.White.ToArgb())
                        return false;
            return true;
        }

        private string GetCanvasBase64()
        {
            // Crop to bounding box of drawn pixels before export (matches web portal getCroppedDataURL).
            int w = _canvasBitmap.Width, h = _canvasBitmap.Height;
            int minX = w, maxX = 0, minY = h, maxY = 0;
            bool found = false;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var px = _canvasBitmap.GetPixel(x, y);
                    if (px.ToArgb() != Color.White.ToArgb() && px.A > 10)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        found = true;
                    }
                }
            }

            Bitmap bmp = _canvasBitmap;
            if (found)
            {
                const int pad = 4;
                int sx = Math.Max(0, minX - pad);
                int sy = Math.Max(0, minY - pad);
                int sw = Math.Min(w, maxX + pad + 1) - sx;
                int sh = Math.Min(h, maxY + pad + 1) - sy;
                bmp = new Bitmap(sw, sh);
                using (var g = Graphics.FromImage(bmp))
                    g.DrawImage(_canvasBitmap, -sx, -sy);
            }

            try
            {
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
                }
            }
            finally
            {
                if (found) bmp.Dispose();
            }
        }

        private void UpdateSignaturePreview()
        {
            if (_pnlPreview == null) return;
            bool useCanvas = _tabSignature?.SelectedIndex == 0;

            if (useCanvas)
            {
                if (IsCanvasBlank())
                {
                    _pbPreviewSig.Visible            = false;
                    _lblPreviewSigPlaceholder.Visible = true;
                    _pbPreviewSig.Image               = null;
                }
                else
                {
                    _pbPreviewSig.Image               = (Bitmap)_canvasBitmap.Clone();
                    _pbPreviewSig.Visible            = true;
                    _lblPreviewSigPlaceholder.Visible = false;
                }
            }
            else
            {
                if (_uploadedBytes?.Length > 0)
                {
                    using (var ms = new MemoryStream(_uploadedBytes))
                        _pbPreviewSig.Image = Image.FromStream(ms);
                    _pbPreviewSig.Visible            = true;
                    _lblPreviewSigPlaceholder.Visible = false;
                }
                else
                {
                    _pbPreviewSig.Visible            = false;
                    _lblPreviewSigPlaceholder.Visible = true;
                    _pbPreviewSig.Image               = null;
                }
            }
        }

        // ── Upload Tab ────────────────────────────────────────────────────────

        private void BuildUploadTab()
        {
            var tab = new TabPage("Upload Image");
            _tabSignature.TabPages.Add(tab);

            _uploadPreview = new PictureBox
            {
                Location    = new Point(6, 8),
                Size        = new Size(490, 145),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode    = PictureBoxSizeMode.Zoom,
                BackColor   = Color.FromArgb(248, 250, 252),
                TabStop     = false
            };
            tab.Controls.Add(_uploadPreview);

            var btnBrowse = new Button
            {
                Text      = "Browse…",
                Location  = new Point(6, 158),
                Size      = new Size(90, 28),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 8.5F),
                BackColor = Color.FromArgb(239, 246, 255),
                ForeColor = Color.FromArgb(27, 58, 107)
            };
            btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(191, 219, 254);
            btnBrowse.Click += OnBrowseSignatureClick;
            tab.Controls.Add(btnBrowse);

            _btnDeleteUpload = new Button
            {
                Text      = "Delete",
                Location  = new Point(102, 158),
                Size      = new Size(70, 28),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 8.5F),
                BackColor = Color.FromArgb(255, 240, 240),
                ForeColor = Color.FromArgb(185, 28, 28),
                Visible   = false
            };
            _btnDeleteUpload.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
            _btnDeleteUpload.Click += OnDeleteUploadClick;
            tab.Controls.Add(_btnDeleteUpload);

            tab.Controls.Add(new Label
            {
                Text      = "Supported formats: PNG, JPG  (max 2 MB)",
                Font      = new Font("Segoe UI", 7.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize  = true,
                Location  = new Point(178, 164)
            });

            _tabSignature.SelectedIndexChanged += (s, e) =>
            {
                if (_tabSignature.SelectedIndex == 0)
                    _canvasPanel?.Invalidate();
                UpdateSignaturePreview();
            };
        }

        private void OnBrowseSignatureClick(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Title  = "Select Signature Image",
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;

                var fi = new FileInfo(dlg.FileName);
                if (fi.Length > 2 * 1024 * 1024)
                {
                    MessageBox.Show("File must be smaller than 2 MB.",
                        "File Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _uploadedBytes = File.ReadAllBytes(dlg.FileName);
                _uploadedMime  = dlg.FileName.ToLowerInvariant().EndsWith(".jpg") ||
                                 dlg.FileName.ToLowerInvariant().EndsWith(".jpeg")
                                 ? "image/jpeg" : "image/png";

                using (var ms = new MemoryStream(_uploadedBytes))
                    _uploadPreview.Image = Image.FromStream(ms);

                _btnDeleteUpload.Visible = true;
                UpdateSignaturePreview();
            }
        }

        private void OnDeleteUploadClick(object sender, EventArgs e)
        {
            _uploadedBytes           = null;
            _uploadedMime            = null;
            _uploadPreview.Image     = null;
            _btnDeleteUpload.Visible = false;
            UpdateSignaturePreview();
        }

        // ── Queue loading ─────────────────────────────────────────────────────

        private async Task LoadQueueAsync()
        {
            if (_btnRefresh != null) _btnRefresh.Enabled = false;
            _lblQueueStatus.Text = "Loading…";
            try
            {
                _pending = await _repo.GetPendingByScopeAsync(
                    null,
                    null,
                    AppSession.CurrentDepartmentId,
                    AppSession.CurrentEmployeeId);

                // Detect if the currently-displayed record was approved by someone else
                int? staleId = null;
                if (_selected != null && (_pending == null || !_pending.Any(p => p.AuthorizationId == _selected.AuthorizationId)))
                    staleId = _selected.AuthorizationId;

                _lstQueue.Items.Clear();

                if (_pending == null || _pending.Count == 0)
                {
                    _lblQueueStatus.Text = "No pending authorizations.";
                    SetDetailEnabled(false);
                    if (staleId.HasValue) { _selected = null; await ShowStaleApprovedAsync(staleId.Value); }
                    return;
                }

                _lblQueueStatus.Text = "";
                foreach (var a in _pending)
                    _lstQueue.Items.Add(a);

                if (staleId.HasValue) { _selected = null; await ShowStaleApprovedAsync(staleId.Value); }
            }
            catch (Exception ex)
            {
                _lblQueueStatus.Text = $"Error: {ex.Message}";
            }
            finally
            {
                if (_btnRefresh != null) _btnRefresh.Enabled = true;
            }
        }

        /// <summary>
        /// Reloads the queue and auto-selects the given authorization record.
        /// Called from RequesterPortalForm after a self-sign submission.
        /// </summary>
        public async Task LoadAndSelectAsync(int authorizationId)
        {
            await LoadQueueAsync();
            for (int i = 0; i < _lstQueue.Items.Count; i++)
            {
                if (_lstQueue.Items[i] is CartridgeAuthorizationModel a && a.AuthorizationId == authorizationId)
                {
                    _lstQueue.SelectedIndex = i;
                    break;
                }
            }
        }

        private void OnQueueSelectionChanged(object sender, EventArgs e)
        {
            _selected = _lstQueue.SelectedItem as CartridgeAuthorizationModel;
            if (_selected == null) return;

            HideApprovedBanner();
            _lblStatus.Text = "";
            PopulateDetail(_selected);
            SetDetailEnabled(true);
            _lblStatus.Text = "";
            ClearCanvas();
            _uploadPreview.Image     = null;
            _uploadedBytes           = null;
            _uploadedMime            = null;
            _btnDeleteUpload.Visible = false;
            UpdateSignaturePreview();
        }

        private void PopulateDetail(CartridgeAuthorizationModel a)
        {
            _lblEmpName.Text   = a.EmployeeName     ?? "—";
            _lblDept.Text      = a.DepartmentName   ?? "—";
            _lblBranch.Text    = a.BranchName        ?? "—";
            _lblPosition.Text  = a.EmployeePosition  ?? "—";
            _lblRequested.Text = a.CreatedDate.ToString("MMMM dd, yyyy  h:mm tt");
            _lblModels.Text    = FormatModelsText(a.RequestedModels);

            string signerName = AppSession.CurrentEmployeeName ?? AppSession.CurrentUserName ?? "—";
            string signerPos  = AppSession.CurrentEmployeePosition ?? "—";
            string signerCo   = AppSession.CurrentCompanyName      ?? "—";
            string signerDept = AppSession.CurrentDepartmentName   ?? "—";
            string signerBr   = AppSession.CurrentBranchName       ?? "—";

            BuildPreviewStatement(
                signerName, signerPos, signerCo, signerDept, signerBr,
                a.EmployeeName ?? "—", a.EmployeePosition ?? "—",
                a.RequestedModels);

            _lblSignerName.Text        = signerName;
            _lblSignerPos.Text         = signerPos;
            _lblSignerDeptBranch.Text  = $"{signerDept}  ·  {signerBr}  ·  {signerCo}";
            _lblPreviewDate.Text       = $"Date Signed: {DateTime.Now:MMMM dd, yyyy}";
            _txtNotes.Clear();
        }

        private void SetDetailEnabled(bool enabled)
        {
            _btnApprove.Enabled   = enabled;
            _btnReject.Enabled    = enabled;
            _tabSignature.Enabled = enabled;
            _txtNotes.Enabled     = enabled;
        }

        // ── Approve ───────────────────────────────────────────────────────────

        private async Task OnApproveClickAsync()
        {
            if (_selected == null) return;

            bool useCanvas = _tabSignature.SelectedIndex == 0;
            string sigData, sigSource;

            if (useCanvas)
            {
                if (IsCanvasBlank())
                {
                    MessageBox.Show("Please draw your signature before approving.",
                        "Signature Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                sigData   = GetCanvasBase64();
                sigSource = "draw";
            }
            else
            {
                if (_uploadedBytes == null || _uploadedBytes.Length == 0)
                {
                    MessageBox.Show("Please upload a signature image before approving.",
                        "Signature Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                sigData   = "data:" + _uploadedMime + ";base64," +
                            Convert.ToBase64String(_uploadedBytes);
                sigSource = "upload";
            }

            if (MessageBox.Show(
                $"Approve cartridge authorization for {_selected.EmployeeName}?\n\n" +
                "This will allow them to submit cartridge requests.",
                "Confirm Approval", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                != DialogResult.Yes) return;

            _btnApprove.Enabled = false;
            _btnReject.Enabled  = false;
            _lblStatus.Text     = "Processing…";
            _lblStatus.ForeColor = Color.FromArgb(100, 116, 139);

            try
            {
                bool ok = await _repo.ApproveWithSignatureAsync(
                    _selected.AuthorizationId,
                    _supervisorUserId,
                    sigData, sigSource,
                    AppSession.CurrentEmployeePosition ?? "",
                    AppSession.CurrentCompanyName      ?? "",
                    AppSession.CurrentBranchName       ?? "");

                if (ok)
                {
                    _lblStatus.Text      = "Authorization approved successfully.";
                    _lblStatus.ForeColor = Color.FromArgb(21, 128, 61);

                    // Send in-app Authorization Status notification to the requester.
                    // Uses GetUserIdByAuthorizationId so the email-chain fallback fires when
                    // User.EmpId is not populated (same path as the reject flow below).
                    int approvedAuthId = _selected.AuthorizationId;
                    int approvedEmpId  = _selected.EmployeeId;
                    try
                    {
                        var notifRepo    = new NotificationRepository();
                        int? notifUserId = notifRepo.GetUserIdByAuthorizationId(approvedAuthId)
                                        ?? notifRepo.GetUserIdByEmployeeId(approvedEmpId);
                        if (notifUserId.HasValue)
                        {
                            notifRepo.Create(new NotificationCreateDto
                            {
                                UserId           = notifUserId.Value,
                                Title            = "Authorization Approved",
                                Message          = "Your cartridge authorization request has been approved and signed.",
                                NotificationType = NotificationType.AuthorizationApproved,
                                ReferenceId      = approvedAuthId
                            });
                            Debug.WriteLine($"[SupervisorApprovalPage] Notification sent: UserId={notifUserId}, AuthId={approvedAuthId}");
                        }
                        else
                        {
                            Debug.WriteLine($"[SupervisorApprovalPage] No user found for EmpId={approvedEmpId} — notification skipped");
                        }
                    }
                    catch (Exception notifEx)
                    {
                        Debug.WriteLine($"[SupervisorApprovalPage] Notification error: {notifEx.Message}");
                    }

                    RemoveSelectedFromQueue();
                }
                else
                {
                    int approvedId = _selected.AuthorizationId;
                    _lblStatus.Text = "";
                    RemoveFromQueueListOnly();
                    await ShowStaleApprovedAsync(approvedId);
                }
            }
            catch (Exception ex)
            {
                _lblStatus.Text      = $"Error: {ex.Message}";
                _lblStatus.ForeColor = Color.FromArgb(185, 28, 28);
                _btnApprove.Enabled  = true;
                _btnReject.Enabled   = true;
            }
        }

        // ── Reject ────────────────────────────────────────────────────────────

        private async Task OnRejectClickAsync()
        {
            if (_selected == null) return;

            string reason = _txtNotes.Text.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                MessageBox.Show("Please enter a reason in the Notes field.",
                    "Reason Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(
                $"Reject this authorization for {_selected.EmployeeName}?\n\nReason: {reason}",
                "Confirm Rejection", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                != DialogResult.Yes) return;

            _btnApprove.Enabled = false;
            _btnReject.Enabled  = false;
            _lblStatus.Text     = "Processing…";

            try
            {
                int rejectedAuthId = _selected.AuthorizationId;
                int rejectedEmpId  = _selected.EmployeeId;
                await _repo.RejectAuthorizationAsync(rejectedAuthId);
                _lblStatus.Text      = "Authorization rejected.";
                _lblStatus.ForeColor = Color.FromArgb(185, 28, 28);

                try
                {
                    var notifRepo    = new NotificationRepository();
                    int? notifUserId = notifRepo.GetUserIdByAuthorizationId(rejectedAuthId)
                                    ?? notifRepo.GetUserIdByEmployeeId(rejectedEmpId);
                    if (notifUserId.HasValue)
                    {
                        notifRepo.Create(new NotificationCreateDto
                        {
                            UserId           = notifUserId.Value,
                            Title            = "Authorization Rejected",
                            Message          = "Your cartridge authorization request has been rejected. Please contact your supervisor for details.",
                            NotificationType = NotificationType.AuthorizationRejected,
                            ReferenceId      = rejectedAuthId
                        });
                        Debug.WriteLine($"[SupervisorApprovalPage] Rejection notification sent: UserId={notifUserId}, AuthId={rejectedAuthId}");
                    }
                    else
                    {
                        Debug.WriteLine($"[SupervisorApprovalPage] No user found for EmpId={rejectedEmpId} — notification skipped");
                    }
                }
                catch (Exception notifEx)
                {
                    Debug.WriteLine($"[SupervisorApprovalPage] Notification error: {notifEx.Message}");
                }

                RemoveSelectedFromQueue();
            }
            catch (Exception ex)
            {
                _lblStatus.Text      = $"Error: {ex.Message}";
                _lblStatus.ForeColor = Color.FromArgb(185, 28, 28);
                _btnApprove.Enabled  = true;
                _btnReject.Enabled   = true;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ShowApprovedBanner(string signerName, DateTime? signedDate)
        {
            string who  = !string.IsNullOrWhiteSpace(signerName) ? signerName : "another approver";
            string when = signedDate.HasValue
                ? signedDate.Value.ToString("MMMM dd, yyyy  h:mm tt")
                : string.Empty;
            _lblApprovedMsg.Text = $"✓  Already approved by {who}"
                                 + (string.IsNullOrEmpty(when) ? string.Empty : $"\r\n    Signed: {when}");
            _pnlApprovedBanner.Visible = true;
            _btnApprove.Visible        = false;
            _btnReject.Visible         = false;
            SetDetailEnabled(false);
        }

        private void HideApprovedBanner()
        {
            _pnlApprovedBanner.Visible = false;
            _btnApprove.Visible        = true;
            _btnReject.Visible         = true;
        }

        private void RemoveFromQueueListOnly()
        {
            int idx = _lstQueue.SelectedIndex;
            if (idx >= 0) _lstQueue.Items.RemoveAt(idx);
            _selected = null;
            if (_lstQueue.Items.Count == 0)
                _lblQueueStatus.Text = "No pending authorizations.";
        }

        private async Task ShowStaleApprovedAsync(int authorizationId)
        {
            try
            {
                var record = await _repo.GetByIdAsync(authorizationId);
                ShowApprovedBanner(record?.SignedByName, record?.SignedDate);
            }
            catch
            {
                ShowApprovedBanner(null, null);
            }
        }

        private void RemoveSelectedFromQueue()
        {
            int idx = _lstQueue.SelectedIndex;
            if (idx >= 0) _lstQueue.Items.RemoveAt(idx);

            _selected = null;
            SetDetailEnabled(false);
            _lblEmpName.Text  = "Select a request from the list";
            _lblDept.Text = _lblBranch.Text = _lblPosition.Text =
                _lblRequested.Text = _lblModels.Text = "—";
            _lblPreviewStatement.Clear();
            _lblPreviewStatement.AppendText("(select a request to see the authorization statement)");
            _dgvPreviewModels.Rows.Clear();
            _lblSignerName.Text        = "—";
            _lblSignerPos.Text         = "—";
            _lblSignerDeptBranch.Text  = "—";
            _pbPreviewSig.Visible            = false;
            _lblPreviewSigPlaceholder.Visible = true;
            _txtNotes.Clear();
            ClearCanvas();

            if (_lstQueue.Items.Count == 0)
                _lblQueueStatus.Text = "No pending authorizations.";
        }

        // ── JSON model helpers ────────────────────────────────────────────────

        private static List<(string Model, int Qty, int Good, int Damaged)> ParseModelsJson(string json)
        {
            var result = new List<(string Model, int Qty, int Good, int Damaged)>();
            if (string.IsNullOrWhiteSpace(json)) return result;
            try
            {
                var arr = JArray.Parse(json);
                foreach (JObject item in arr)
                {
                    result.Add((
                        item["model"]?.ToString()        ?? "",
                        item["qty"]?.Value<int>()        ?? 0,
                        item["good"]?.Value<int>()       ?? 0,
                        item["damaged"]?.Value<int>()    ?? 0
                    ));
                }
            }
            catch { /* ignore malformed JSON */ }
            return result;
        }

        private static string FormatModelsText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "(no cartridge details)";
            var parsed = ParseModelsJson(raw);
            if (parsed.Count == 0) return raw.Trim();   // plain-text fallback
            return string.Join("\r\n", parsed.Select((m, i) =>
                $"{i + 1}. {m.Model}  –  Qty: {m.Qty}  (Submitted Good: {m.Good}, Submitted Damaged: {m.Damaged})"));
        }

        /// <summary>
        /// Populates the preview RichTextBox with mixed bold/normal formatting.
        /// Bold = signer name, position, org, requester name/position, and each cartridge model name.
        /// Normal = connecting prose.
        /// </summary>
        private void BuildPreviewStatement(
            string signerName, string signerPos, string signerCo,
            string signerDept, string signerBr,
            string requesterName, string requesterPos,
            string rawModels)
        {
            var rtb    = _lblPreviewStatement;
            var normal = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            var bold   = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            var fg     = Color.FromArgb(50, 50, 50);

            void Append(string text, bool isBold = false)
            {
                rtb.SelectionStart  = rtb.TextLength;
                rtb.SelectionLength = 0;
                rtb.SelectionFont   = isBold ? bold : normal;
                rtb.SelectionColor  = fg;
                rtb.AppendText(text);
            }

            rtb.Clear();

            // Line 1: "I, [SIGNER NAME], the [POSITION]"
            Append("I, ");
            Append(signerName, true);
            Append(", the ");
            Append(signerPos, true);
            Append("\r\n");

            // Line 2: "of [COMPANY] – [DEPARTMENT], [BRANCH],"
            Append("of ");
            Append($"{signerCo} \u2013 {signerDept}, {signerBr}", true);
            Append(",\r\n");

            // Line 3: "hereby authorize the Cartridge Refill Request/s"
            Append("hereby authorize the Cartridge Refill Request/s\r\n");

            // Line 4: "submitted by [REQUESTER] ([POSITION])"
            Append("submitted by ");
            Append(requesterName, true);
            Append(" (");
            Append(requesterPos, true);
            Append(") for the following cartridges:");

            // Cartridge table — populate the DataGridView below the prose
            _dgvPreviewModels.Rows.Clear();
            var parsed = ParseModelsJson(rawModels);
            if (parsed.Count == 0)
            {
                _dgvPreviewModels.Rows.Add("(no cartridge details)", "—", "—", "—");
            }
            else
            {
                foreach (var (model, qty, good, damaged) in parsed)
                    _dgvPreviewModels.Rows.Add(model, qty, good, damaged);
            }
            // Resize grid height to fit rows exactly
            int rowH  = _dgvPreviewModels.RowTemplate.Height > 0 ? _dgvPreviewModels.RowTemplate.Height : 22;
            _dgvPreviewModels.Height = _dgvPreviewModels.ColumnHeadersHeight + (parsed.Count > 0 ? parsed.Count : 1) * rowH + 2;

            // Clear selection so no column header gets the blue highlight
            _dgvPreviewModels.ClearSelection();
            _dgvPreviewModels.CurrentCell = null;
        }

        /// <summary>Formats parsed models as a plain-text bullet list for the preview statement.</summary>
        private static string BuildModelsListText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "  (no cartridge details)";
            var parsed = ParseModelsJson(raw);
            if (parsed.Count == 0) return "  " + raw.Trim();   // plain-text fallback
            return string.Join("\r\n", parsed.Select(m =>
                $"  {m.Model}  x{m.Qty}  (Good: {m.Good}, Damaged: {m.Damaged})"));
        }

        // ══════════════════════════════════════════════════════════════════════
        // Nested helper controls
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Double-buffered panel — eliminates canvas flicker.</summary>
        private class DoubleBufferPanel : Panel
        {
            public DoubleBufferPanel()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint            |
                         ControlStyles.OptimizedDoubleBuffer, true);
                UpdateStyles();
            }
        }

        /// <summary>
        /// Owner-drawn ListBox — each item is a multiline card:
        ///   Line 1 — Employee name  (bold)
        ///   Line 2 — Company · Department
        ///   Line 3 — Date submitted
        /// </summary>
        private class AuthQueueListBox : ListBox
        {
            private static readonly Font _fName = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            private static readonly Font _fSub  = new Font("Segoe UI", 8F);
            private const int ItemH = 82;

            public AuthQueueListBox()
            {
                DrawMode       = DrawMode.OwnerDrawFixed;
                ItemHeight     = ItemH;
                Font           = new Font("Segoe UI", 9F);
                BackColor      = Color.White;
                BorderStyle    = BorderStyle.None;
            }

            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= Items.Count) return;

                var  a   = Items[e.Index] as CartridgeAuthorizationModel;
                bool sel = (e.State & DrawItemState.Selected) != 0;

                Color bg  = sel ? Color.FromArgb(219, 234, 254) : Color.White;
                Color fg  = sel ? Color.FromArgb(30, 64, 175)   : Color.FromArgb(30, 41, 59);
                Color sub = sel ? Color.FromArgb(71, 107, 210)  : Color.FromArgb(100, 116, 139);

                e.Graphics.FillRectangle(new SolidBrush(bg), e.Bounds);

                if (sel)
                    e.Graphics.FillRectangle(
                        new SolidBrush(Color.FromArgb(59, 130, 246)),
                        new Rectangle(e.Bounds.X, e.Bounds.Y, 4, e.Bounds.Height));

                int tx = e.Bounds.X + 12;
                int ty = e.Bounds.Y + 12;
                int tw = e.Bounds.Width - 16;

                // Name
                e.Graphics.DrawString(a?.EmployeeName ?? "—", _fName,
                    new SolidBrush(fg), new RectangleF(tx, ty, tw, 22));
                ty += 24;

                // Company · Department
                string org = string.Join(" · ",
                    new[] { a?.CompanyName, a?.DepartmentName }
                        .Where(v => !string.IsNullOrWhiteSpace(v)));
                if (string.IsNullOrEmpty(org)) org = a?.DepartmentName ?? "—";
                e.Graphics.DrawString(org, _fSub,
                    new SolidBrush(sub), new RectangleF(tx, ty, tw, 20));
                ty += 20;

                // Date
                e.Graphics.DrawString(a?.CreatedDate.ToString("MM/dd/yyyy") ?? "", _fSub,
                    new SolidBrush(sub), new RectangleF(tx, ty, tw, 18));

                // Bottom divider
                e.Graphics.DrawLine(new Pen(Color.FromArgb(243, 244, 246)),
                    e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
        }
    }
}
