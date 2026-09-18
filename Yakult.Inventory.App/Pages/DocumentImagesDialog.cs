using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages.Receipt;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages
{
    public partial class DocumentImagesDialog : Form
    {
        private readonly int _setId;
        private readonly SetRepository _repository;
        private TabControl tabControl;
        private TabPage receiptsTab;
        private TabPage setImagesTab;

        private FlowLayoutPanel _setImagesPanel;
        private Label _setImagesStatusLabel;
        private Button _btnUploadSetImage;
        private Button _btnRefreshSetImages;

        private static readonly Color AppBg = Color.FromArgb(248, 249, 250);
        private static readonly Color CardBg = Color.White;
        private static readonly Color Border = Color.FromArgb(222, 226, 230);
        private static readonly Color Primary = Color.FromArgb(23, 162, 184);
        private static readonly Color Danger = Color.FromArgb(220, 53, 69);
        private static readonly Color TextMain = Color.FromArgb(33, 37, 41);
        private static readonly Color TextMuted = Color.FromArgb(108, 117, 125);
        
        public DocumentImagesDialog(int setId)
        {
            _setId = setId;
            _repository = new SetRepository();
            InitializeComponent();
            LoadDataAsync();
        }

        private void InitializeComponent()
        {
            this.Text = "Document Images - Set Details";
            this.Size = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(800, 500);
            this.BackColor = AppBg;

            // Create tab control
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F)
            };

            // Create tabs
            receiptsTab = new TabPage("📄 Receipts")
            {
                BackColor = Color.FromArgb(248, 249, 250)
            };
            
            setImagesTab = new TabPage("📷 Set Images")
            {
                BackColor = Color.FromArgb(248, 249, 250)
            };

            tabControl.TabPages.Add(receiptsTab);
            tabControl.TabPages.Add(setImagesTab);

            this.Controls.Add(tabControl);

            // Setup receipts tab (existing functionality)
            SetupReceiptsTab();
            
            // Setup set images tab (new functionality)
            SetupSetImagesTab();
        }

        private void SetupReceiptsTabLegacy()
        {
            // When refreshing, make sure we don't stack multiple Dock=Fill panels.
            receiptsTab.Controls.Clear();

            // Legacy layout retained for reference; not used by default.
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(20),
                WrapContents = false
            };

            // Use the original receipt logic - get receipts linked to this set
            try
            {
                var receiptRepo = new ReceiptSetRepository();
                var existing = receiptRepo.GetBySetId(_setId);
                
                if (existing != null)
                {
                    // Show receipt set viewer button
                    var viewReceiptsButton = new Button
                    {
                        Text = $"📄 View Receipt Set ({existing.Supplier})",
                        Size = new Size(300, 50),
                        BackColor = Color.FromArgb(23, 162, 184),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Cursor = Cursors.Hand,
                        Font = new Font("Segoe UI", 10F, FontStyle.Bold)
                     };
                     viewReceiptsButton.FlatAppearance.BorderSize = 0;
                     viewReceiptsButton.Click += (s, e) => 
                     {
                        // Reload at click-time so the viewer doesn't depend on a potentially stale/light DTO.
                        ReceiptSetDto latest;
                        try
                        {
                            latest = receiptRepo.GetBySetId(_setId);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, "Failed to load receipt set.\n\n" + ex.Message, "Receipts",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
#if DEBUG
                        MessageBox.Show(this, $"DEBUG: Opening receipt set\n" +
                            $"ReceiptSetId: {latest?.ReceiptSetId}\n" +
                            $"Supplier: {latest?.Supplier}\n" +
                            $"Has SI Image: {(latest?.SiImage != null && latest.SiImage.Length > 0)}\n" +
                            $"Has DR Image: {(latest?.DrImage != null && latest.DrImage.Length > 0)}\n" +
                            $"Has PO Image: {(latest?.PoImage != null && latest.PoImage.Length > 0)}", "Debug");
#endif

                        Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForSet(this, _setId);
                     };
                    
                    panel.Controls.Add(viewReceiptsButton);
                    
                    // Add unlink option
                    var unlinkButton = new Button
                    {
                        Text = "🔗 Unlink Receipt Set",
                        Size = new Size(200, 35),
                        BackColor = Color.FromArgb(220, 53, 69),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Cursor = Cursors.Hand,
                        Font = new Font("Segoe UI", 9F)
                    };
                    unlinkButton.FlatAppearance.BorderSize = 0;
                    unlinkButton.Margin = new Padding(0, 10, 0, 0);
                    unlinkButton.Click += (s, e) => 
                    {
                        var confirm = MessageBox.Show(
                            "Unlink this receipt set from the current set?",
                            "Confirm Unlink",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);
                            
                        if (confirm == DialogResult.Yes)
                        {
                            receiptRepo.UnlinkReceiptSet(existing.ReceiptSetId, _setId, null);
                            BeginInvoke(new Action(SetupReceiptsTab)); // Refresh the tab
                        }
                    };
                    
                    panel.Controls.Add(unlinkButton);
                }
                else
                {
                    // No linked receipts - show option to link
                    var linkButton = new Button
                    {
                        Text = "🔗 Link Receipt Set",
                        Size = new Size(200, 50),
                        BackColor = Color.FromArgb(40, 167, 69),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Cursor = Cursors.Hand,
                        Font = new Font("Segoe UI", 10F, FontStyle.Bold)
                    };
                    linkButton.FlatAppearance.BorderSize = 0;
                    linkButton.Click += (s, e) => 
                    {
                        using (var dialog = new AttachReceiptSetDialog(_setId))
                        {
                            if (dialog.ShowDialog(this) == DialogResult.OK)
                            {
                                if (dialog.SelectedReceiptSetId.HasValue)
                                {
                                    try
                                    {
                                        int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                                        receiptRepo.AttachReceiptSetToSet(dialog.SelectedReceiptSetId.Value, _setId, userId);
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show(this, "Failed to link receipt set.\n\n" + ex.Message, "Receipts",
                                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        return;
                                    }
                                }

                                BeginInvoke(new Action(SetupReceiptsTab)); // Refresh the tab
                            }
                        }
                    };
                    
                    panel.Controls.Add(linkButton);
                    
                    var noDataLabel = new Label
                    {
                        Text = "No receipts linked to this set.\n\nClick 'Link Receipt Set' to associate receipts with this set.",
                        AutoSize = true,
                        Font = new Font("Segoe UI", 11F),
                        ForeColor = Color.FromArgb(108, 117, 125),
                        Location = new Point(20, 70),
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    panel.Controls.Add(noDataLabel);
                }
            }
            catch (Exception ex)
            {
                var errorLabel = new Label
                {
                    Text = $"Error loading receipts: {ex.Message}",
                    AutoSize = true,
                    Font = new Font("Segoe UI", 12F),
                    ForeColor = Color.FromArgb(220, 53, 69),
                    Location = new Point(20, 20)
                };
                panel.Controls.Add(errorLabel);
            }

            receiptsTab.Controls.Add(panel);
        }

        private void SetupReceiptsTab()
        {
            receiptsTab.Controls.Clear();

            var root = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppBg,
                Padding = new Padding(22)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 12)
            };

            var lblTitle = new Label
            {
                Text = "Receipts",
                AutoSize = true,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = TextMain,
                Location = new Point(0, 0)
            };

            var lblSubtitle = new Label
            {
                Text = "Linked receipt set for this set",
                AutoSize = true,
                Font = new Font("Segoe UI", 10F),
                ForeColor = TextMuted,
                Location = new Point(0, 34)
            };

            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSubtitle);
            layout.Controls.Add(header, 0, 0);

            layout.Controls.Add(BuildReceiptsContent(), 0, 1);

            root.Controls.Add(layout);
            receiptsTab.Controls.Add(root);
        }

        private Control BuildReceiptsContent()
        {
            var receiptRepo = new ReceiptSetRepository();

            ReceiptSetDto existing;
            try
            {
                existing = receiptRepo.GetBySetId(_setId);
            }
            catch (Exception ex)
            {
                return BuildErrorCard("Error loading receipts", ex.Message);
            }

            return existing == null
                ? BuildEmptyReceiptsCard(receiptRepo)
                : BuildLinkedReceiptsCard(existing, receiptRepo);
        }

        private Control BuildLinkedReceiptsCard(ReceiptSetDto existing, ReceiptSetRepository receiptRepo)
        {
            var card = CreateCardPanel();
            card.Padding = new Padding(18);

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));

            var left = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0, 0, 12, 0)
            };
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var topRow = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.Transparent };
            var lblSupplier = new Label
            {
                Text = string.IsNullOrWhiteSpace(existing.Supplier) ? "Unknown Supplier" : existing.Supplier,
                AutoSize = true,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = TextMain,
                Location = new Point(0, 3)
            };

            var statusChip = new Label
            {
                AutoSize = true,
                Text = "LINKED",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 81, 50),
                BackColor = Color.FromArgb(209, 231, 221),
                Padding = new Padding(8, 3, 8, 3),
                Location = new Point(0, 6)
            };

            topRow.Controls.Add(lblSupplier);
            topRow.Controls.Add(statusChip);
            topRow.Resize += (s, e) =>
            {
                statusChip.Location = new Point(lblSupplier.Right + 12, 6);
            };
            statusChip.Location = new Point(lblSupplier.Right + 12, 6);

            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 10, 0, 0)
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            AddField(fields, "SI Number", existing.SiNumber);
            AddField(fields, "DR Number", existing.DrNumber);
            AddField(fields, "PO Number", existing.PoNumber);

            var thumbs = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0, 14, 0, 0),
                Padding = new Padding(0)
            };
            Dictionary<string, int> imageCounts;
            try
            {
                imageCounts = receiptRepo.GetImageCountsForReceiptSet(existing.ReceiptSetId);
            }
            catch
            {
                imageCounts = new Dictionary<string, int>();
            }

            thumbs.Controls.Add(CreateReceiptThumb("SI", existing.SiImage, existing.SiImagePath, () => OpenViewer(existing), imageCounts.TryGetValue("SI", out var siCount) ? siCount : -1));
            thumbs.Controls.Add(CreateReceiptThumb("DR", existing.DrImage, existing.DrImagePath, () => OpenViewer(existing), imageCounts.TryGetValue("DR", out var drCount) ? drCount : -1));
            thumbs.Controls.Add(CreateReceiptThumb("PO", existing.PoImage, existing.PoImagePath, () => OpenViewer(existing), imageCounts.TryGetValue("PO", out var poCount) ? poCount : -1));

            left.Controls.Add(topRow, 0, 0);
            left.Controls.Add(fields, 0, 1);
            left.Controls.Add(thumbs, 0, 2);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0),
                Margin = new Padding(0),
                BackColor = Color.Transparent
            };

            var btnView = new Button
            {
                Text = "View Receipt Set",
                Height = 44,
                Width = 240,
                Cursor = Cursors.Hand
            };
            StyleButton(btnView, primary: true);
            btnView.Click += (s, e) =>
            {
                ReceiptSetDto latest;
                try
                {
                    latest = receiptRepo.GetBySetId(_setId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to load receipt set.\n\n" + ex.Message, "Receipts",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForSet(this, _setId);
            };

            var btnUnlink = new Button
            {
                Text = "Unlink Receipt Set",
                Height = 40,
                Width = 240,
                Cursor = Cursors.Hand
            };
            StyleButton(btnUnlink, danger: true);
            btnUnlink.Click += (s, e) =>
            {
                var confirm = MessageBox.Show(
                    "Unlink this receipt set from the current set?",
                    "Confirm Unlink",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm != DialogResult.Yes)
                    return;

                try
                {
                    int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                    receiptRepo.UnlinkReceiptSet(existing.ReceiptSetId, _setId, userId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to unlink receipt set.\n\n" + ex.Message, "Receipts",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                BeginInvoke(new Action(SetupReceiptsTab));
            };

            var hint = new Label
            {
                Text = "Tip: click a thumbnail to open the viewer.",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = TextMuted,
                Margin = new Padding(2, 10, 0, 0)
            };

            actions.Controls.Add(btnView);
            actions.Controls.Add(btnUnlink);
            actions.Controls.Add(hint);

            grid.Controls.Add(left, 0, 0);
            grid.Controls.Add(actions, 1, 0);
            card.Controls.Add(grid);

            return card;
        }

        private Control BuildEmptyReceiptsCard(ReceiptSetRepository receiptRepo)
        {
            var card = CreateCardPanel();
            card.Padding = new Padding(18);

            var lbl = new Label
            {
                Text = "No receipt set linked yet.",
                AutoSize = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TextMain,
                Location = new Point(0, 0)
            };

            var sub = new Label
            {
                Text = "Link an existing receipt set to view SI/DR/PO images here.",
                AutoSize = true,
                Font = new Font("Segoe UI", 10F),
                ForeColor = TextMuted,
                Location = new Point(0, 34)
            };

            var btn = new Button
            {
                Text = "Link Receipt Set",
                Height = 44,
                Width = 220,
                Cursor = Cursors.Hand,
                Location = new Point(0, 72)
            };
            StyleButton(btn, primary: true);
            btn.Click += (s, e) =>
            {
                using (var dialog = new AttachReceiptSetDialog(_setId))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    if (!dialog.SelectedReceiptSetId.HasValue)
                        return;

                    try
                    {
                        int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                        receiptRepo.AttachReceiptSetToSet(dialog.SelectedReceiptSetId.Value, _setId, userId);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Failed to link receipt set.\n\n" + ex.Message, "Receipts",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                BeginInvoke(new Action(SetupReceiptsTab));
            };

            card.Controls.Add(lbl);
            card.Controls.Add(sub);
            card.Controls.Add(btn);
            return card;
        }

        private Control BuildErrorCard(string title, string message)
        {
            var card = CreateCardPanel();
            card.Padding = new Padding(18);

            var lblTitle = new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Danger,
                Location = new Point(0, 0)
            };

            var lblMsg = new Label
            {
                Text = message ?? string.Empty,
                AutoSize = true,
                MaximumSize = new Size(720, 0),
                Font = new Font("Segoe UI", 10F),
                ForeColor = TextMuted,
                Location = new Point(0, 34)
            };

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblMsg);
            return card;
        }

        private Panel CreateCardPanel()
        {
            return new Panel
            {
                BackColor = CardBg,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = false,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                MinimumSize = new Size(0, 280),
                Height = 280
            };
        }

        private void AddField(TableLayoutPanel table, string label, string value)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lbl = new Label
            {
                Text = label,
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = TextMuted,
                Margin = new Padding(0, 2, 10, 2)
            };
            var val = new Label
            {
                Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
                AutoSize = true,
                Font = new Font("Segoe UI", 10F),
                ForeColor = TextMain,
                Margin = new Padding(0, 2, 0, 2)
            };

            table.Controls.Add(lbl, 0, row);
            table.Controls.Add(val, 1, row);
        }

        private Control CreateReceiptThumb(string caption, byte[] bytes, string path, Action onClick, int imageCount = -1)
        {
            var container = new Panel
            {
                Width = 120,
                Height = 96,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 12, 0)
            };

            var pic = new PictureBox
            {
                Width = 120,
                Height = 72,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Location = new Point(0, 0)
            };

            var lbl = new Label
            {
                Text = imageCount >= 0 ? $"{caption} ({imageCount})" : caption,
                AutoSize = false,
                Width = 120,
                Height = 20,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = TextMuted,
                Location = new Point(0, 74),
                Cursor = Cursors.Hand
            };

            pic.Click += (s, e) => onClick?.Invoke();
            lbl.Click += (s, e) => onClick?.Invoke();

            pic.Image = LoadImageOrNull(bytes, path);
            if (pic.Image == null)
            {
                pic.BackColor = AppBg;
            }

            container.Disposed += (s, e) =>
            {
                if (pic.Image != null)
                {
                    var old = pic.Image;
                    pic.Image = null;
                    old.Dispose();
                }
            };

            container.Controls.Add(pic);
            container.Controls.Add(lbl);
            return container;
        }

        private Image LoadImageOrNull(byte[] bytes, string path)
        {
            try
            {
                if (bytes != null && bytes.Length > 0)
                {
                    using (var ms = new MemoryStream(bytes))
                    using (var img = Image.FromStream(ms))
                    {
                        return new Bitmap(img);
                    }
                }

                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var img = Image.FromStream(fs))
                    {
                        return new Bitmap(img);
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private void OpenViewer(ReceiptSetDto dto)
        {
            if (dto == null)
                return;

            Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForReceipt(this, dto);
        }

        private void StyleButton(Button button, bool primary = false, bool danger = false)
        {
            if (button == null) return;

            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

            Color baseColor;
            if (danger)
            {
                baseColor = Danger;
                button.BackColor = baseColor;
                button.ForeColor = Color.White;
            }
            else if (primary)
            {
                baseColor = Primary;
                button.BackColor = baseColor;
                button.ForeColor = Color.White;
            }
            else
            {
                baseColor = CardBg;
                button.BackColor = baseColor;
                button.ForeColor = TextMain;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = Border;
            }

            button.MouseEnter += (s, e) =>
            {
                button.BackColor = Darken(baseColor, 0.06f);
            };
            button.MouseLeave += (s, e) =>
            {
                button.BackColor = baseColor;
            };
        }

        private static Color Darken(Color color, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            int r = (int)(color.R * (1f - amount));
            int g = (int)(color.G * (1f - amount));
            int b = (int)(color.B * (1f - amount));
            return Color.FromArgb(color.A, r, g, b);
        }

        private void SetupSetImagesTab()
        {
            setImagesTab.Controls.Clear();

            var root = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppBg,
                Padding = new Padding(22)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 12)
            };

            var title = new Label
            {
                Text = "Set Images",
                AutoSize = true,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = TextMain,
                Location = new Point(0, 0)
            };

            var subtitle = new Label
            {
                Text = "Images uploaded from mobile or added from this PC",
                AutoSize = true,
                Font = new Font("Segoe UI", 10F),
                ForeColor = TextMuted,
                Location = new Point(0, 34)
            };

            _btnUploadSetImage = new Button
            {
                Text = "Upload (Desktop)",
                Height = 38,
                Width = 160,
                Cursor = Cursors.Hand
            };
            StyleButton(_btnUploadSetImage, primary: true);
            _btnUploadSetImage.Click += async (s, e) => await UploadSetImagesFromDesktopAsync();

            _btnRefreshSetImages = new Button
            {
                Text = "Refresh",
                Height = 38,
                Width = 110,
                Cursor = Cursors.Hand
            };
            StyleButton(_btnRefreshSetImages);
            _btnRefreshSetImages.Click += (s, e) => LoadDataAsync();

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(0, 18),
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            actions.Controls.Add(_btnUploadSetImage);
            actions.Controls.Add(new Panel { Width = 10, Height = 1 });
            actions.Controls.Add(_btnRefreshSetImages);

            _setImagesStatusLabel = new Label
            {
                Text = " ",
                AutoSize = false,
                Width = 420,
                Height = 18,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 9F),
                Location = new Point(0, 52)
            };

            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(actions);
            header.Controls.Add(_setImagesStatusLabel);
            header.Resize += (s, e) => { actions.Location = new Point(header.Width - actions.Width, 18); };
            actions.Location = new Point(header.Width - actions.Width, 18);

            _setImagesPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(0),
                WrapContents = true,
                BackColor = Color.Transparent
            };
            _setImagesPanel.Controls.Add(new Label
            {
                Text = "Loading images...",
                AutoSize = true,
                Font = new Font("Segoe UI", 12F),
                ForeColor = TextMuted,
                Margin = new Padding(0)
            });

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(_setImagesPanel, 0, 1);
            root.Controls.Add(layout);
            setImagesTab.Controls.Add(root);
        }

        private async void LoadDataAsync()
        {
            try
            {
                // Load set images
                var images = await _repository.GetSetImagesAsync(_setId);
                
                // Clear loading label and add images
                var panel = _setImagesPanel ?? (setImagesTab.Controls.Count > 0 ? setImagesTab.Controls[0] as FlowLayoutPanel : null);
                if (panel == null) return;
                panel.Controls.Clear();

                if (_setImagesStatusLabel != null)
                {
                    _setImagesStatusLabel.Text = images.Count == 0 ? "No images yet." : $"{images.Count} image(s)";
                }

                if (images.Count == 0)
                {
                    var noDataLabel = new Label
                    {
                        Text = "📷 No images found for this set.\n\nImages uploaded from mobile will appear here.",
                        AutoSize = true,
                        Font = new Font("Segoe UI", 12F, FontStyle.Italic),
                        ForeColor = Color.FromArgb(108, 117, 125),
                        Location = new Point(20, 20)
                    };
                    panel.Controls.Add(noDataLabel);
                }
                else
                {
                    foreach (var image in images)
                    {
                        var imageCard = CreateImageCard(image);
                        panel.Controls.Add(imageCard);
                    }
                }
            }
            catch (Exception ex)
            {
                var panel = _setImagesPanel ?? (setImagesTab.Controls.Count > 0 ? setImagesTab.Controls[0] as FlowLayoutPanel : null);
                if (panel == null) return;
                panel.Controls.Clear();

                if (_setImagesStatusLabel != null)
                {
                    _setImagesStatusLabel.Text = "Failed to load images.";
                }
                 
                var errorLabel = new Label
                {
                    Text = $"Error loading images: {ex.Message}",
                    AutoSize = true,
                    Font = new Font("Segoe UI", 12F),
                    ForeColor = Color.FromArgb(220, 53, 69),
                    Location = new Point(20, 20)
                };
                panel.Controls.Add(errorLabel);
            }
        }

        private async Task UploadSetImagesFromDesktopAsync()
        {
            using (var ofd = new OpenFileDialog
            {
                Title = "Select image(s) to upload",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files|*.*",
                Multiselect = true,
                CheckFileExists = true
            })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    if (_btnUploadSetImage != null) _btnUploadSetImage.Enabled = false;
                    if (_btnRefreshSetImages != null) _btnRefreshSetImages.Enabled = false;
                    if (_setImagesStatusLabel != null) _setImagesStatusLabel.Text = "Uploading...";

                    int successCount = 0;
                    foreach (var file in ofd.FileNames ?? Array.Empty<string>())
                    {
                        try
                        {
                            string uploadedBy = string.IsNullOrWhiteSpace(AppSession.CurrentUserName)
                                ? "Desktop"
                                : $"{AppSession.CurrentUserName} (Desktop)";

                            await _repository.AddSetImageFromFileAsync(_setId, file, imageType: "Photo", uploadedBy: uploadedBy);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, $"Failed to upload:\n{Path.GetFileName(file)}\n\n{ex.Message}", "Upload Failed",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    if (_setImagesStatusLabel != null)
                    {
                        _setImagesStatusLabel.Text = successCount > 0 ? $"Uploaded {successCount} image(s)." : "No images uploaded.";
                    }
                }
                finally
                {
                    if (_btnUploadSetImage != null) _btnUploadSetImage.Enabled = true;
                    if (_btnRefreshSetImages != null) _btnRefreshSetImages.Enabled = true;
                }

                LoadDataAsync();
            }
        }

        private Control CreateImageCard(SetImageDto image)
        {
            var card = new Panel
            {
                Size = new Size(220, 280),
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0, 0, 15, 15),
                Cursor = Cursors.Hand
            };

            try
            {
                // Image preview
                var pictureBox = new PictureBox
                {
                    Size = new Size(200, 150),
                    Location = new Point(10, 10),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Color.FromArgb(248, 249, 250)
                };

                if (image.ImageData != null && image.ImageData.Length > 0)
                {
                    using (var ms = new MemoryStream(image.ImageData))
                    using (var loaded = Image.FromStream(ms))
                    {
                        pictureBox.Image = new Bitmap(loaded);
                    }
                    pictureBox.Click += (s, e) => ViewFullImage(image);
                }
                else
                {
                    string resolvedPath = ResolveImagePath(image.ImagePath);
                    if (File.Exists(resolvedPath))
                    {
                        pictureBox.Image = Image.FromFile(resolvedPath);
                        pictureBox.Click += (s, e) => ViewFullImage(image);
                    }
                    else
                    {
                        // Show placeholder if file not found
                        pictureBox.Image = CreatePlaceholderImage();
                    }
                }

                // Image info
                var nameLabel = new Label
                {
                    Text = image.FileName,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    Location = new Point(10, 170),
                    Size = new Size(200, 15),
                    ForeColor = Color.FromArgb(33, 37, 41)
                };

                var dateLabel = new Label
                {
                    Text = image.UploadDateFormatted,
                    Font = new Font("Segoe UI", 8F),
                    Location = new Point(10, 190),
                    Size = new Size(200, 15),
                    ForeColor = Color.FromArgb(108, 117, 125)
                };

                var uploadedByLabel = new Label
                {
                    Text = $"By: {image.UploadedBy}",
                    Font = new Font("Segoe UI", 8F),
                    Location = new Point(10, 205),
                    Size = new Size(200, 15),
                    ForeColor = Color.FromArgb(108, 117, 125)
                };

                // Mobile/Desktop source indicator badge
                bool isMobileUpload = image.ImagePath?.StartsWith("~/") == true || 
                                      image.UploadedBy?.Contains("Mobile") == true ||
                                      image.ImageType == "MobileUpload";
                var sourceBadge = new Label
                {
                    Text = isMobileUpload ? "📱 Mobile" : "💻 Desktop",
                    Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                    Location = new Point(150, 170),
                    Size = new Size(60, 18),
                    ForeColor = isMobileUpload ? Color.FromArgb(23, 162, 184) : Color.FromArgb(108, 117, 125),
                    BackColor = isMobileUpload ? Color.FromArgb(232, 245, 248) : Color.FromArgb(248, 249, 250),
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    BorderStyle = BorderStyle.FixedSingle
                };

                // Delete button
                var deleteButton = new Button
                {
                    Text = "🗑️ Delete",
                    Location = new Point(10, 230),
                    Size = new Size(80, 25),
                    BackColor = Color.FromArgb(220, 53, 69),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Font = new Font("Segoe UI", 8F)
                };
                deleteButton.FlatAppearance.BorderSize = 0;
                deleteButton.Click += async (s, e) => 
                {
                    await DeleteImage(image.ImageId, image.ImagePath, card);
                };

                // View button
                var viewButton = new Button
                {
                    Text = "👁️ View",
                    Location = new Point(100, 230),
                    Size = new Size(80, 25),
                    BackColor = Color.FromArgb(23, 162, 184),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Font = new Font("Segoe UI", 8F)
                };
                viewButton.FlatAppearance.BorderSize = 0;
                viewButton.Click += (s, e) => ViewFullImage(image);

                card.Controls.AddRange(new Control[] { pictureBox, nameLabel, dateLabel, uploadedByLabel, sourceBadge, deleteButton, viewButton });

                // Hover effect
                card.MouseEnter += (s, e) => card.BackColor = Color.FromArgb(248, 249, 250);
                card.MouseLeave += (s, e) => card.BackColor = Color.White;
            }
            catch (Exception ex)
            {
                // Show error card if image loading fails
                card.Controls.Clear();
                var errorLabel = new Label
                {
                    Text = $"Error loading image:\n{ex.Message}",
                    Font = new Font("Segoe UI", 8F),
                    ForeColor = Color.FromArgb(220, 53, 69),
                    Location = new Point(10, 10),
                    Size = new Size(200, 100)
                };
                card.Controls.Add(errorLabel);
            }

            return card;
        }

        private Bitmap CreatePlaceholderImage()
        {
            var bitmap = new Bitmap(200, 150);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.FromArgb(248, 249, 250));
                g.DrawString("📷", new Font("Segoe UI", 48), Brushes.Gray, 60, 40);
                g.DrawString("Image not found", new Font("Segoe UI", 10), Brushes.Gray, 45, 100);
            }
            return bitmap;
        }

        /// <summary>
        /// Resolves image path to actual file system path.
        /// Mobile uploads store paths as ~/App_Data/SetImages/filename.jpg
        /// </summary>
        private string ResolveImagePath(string imagePath) => Helpers.LocalImageFileHelper.ResolveImagePath(imagePath);

        private void ViewFullImage(SetImageDto image)
        {
            try
            {
                if (image.ImageData != null && image.ImageData.Length > 0)
                {
                    var viewer = new ImageViewerDialog(image.ImageData, image.FileName);
                    viewer.ShowDialog(this);
                    return;
                }

                string resolvedPath = ResolveImagePath(image.ImagePath);
                if (File.Exists(resolvedPath))
                {
                    var viewer = new ImageViewerDialog(resolvedPath);
                    viewer.ShowDialog(this);
                }
                else
                {
                    MessageBox.Show("Image file not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening image: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DeleteImage(int imageId, string imagePath, Control card)
        {
            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to delete this image?",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    bool success = await _repository.DeleteSetImageAsync(imageId);
                    if (success)
                    {
                        TryDeleteLocalSetImageFile(imagePath);

                        // Remove card from UI
                        card.Parent?.Controls.Remove(card);
                         
                        // Refresh the tab
                        LoadDataAsync();
                    }
                    else
                    {
                        MessageBox.Show("Failed to delete image.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting image: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TryDeleteLocalSetImageFile(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return;

            try
            {
                if (!File.Exists(imagePath))
                    return;

                string setImagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SetImages");
                string fullDir = Path.GetFullPath(setImagesDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string fullPath = Path.GetFullPath(imagePath);

                if (!fullPath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase))
                    return;

                File.Delete(fullPath);
            }
            catch
            {
                // Ignore best-effort cleanup
            }
        }
    }
}
