using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Wpf.Receipt
{
    /// <summary>
    /// WPF replacement for the legacy WinForms ReceiptSetViewerDialog. Supports multiple images
    /// per document type (SI/DR/PO), matching the standard WPF visual language used elsewhere
    /// in the app (see Wpf\CartridgeManagement\Dialogs).
    /// </summary>
    public partial class ReceiptSetViewerWindow : Window
    {
        private readonly ReceiptSetRepository _repository = new ReceiptSetRepository();
        private int _receiptSetId;
        private int? _setId;
        private string _currentDocType = "SI";

        private readonly Dictionary<string, List<GalleryImageItem>> _imagesByType =
            new Dictionary<string, List<GalleryImageItem>>(StringComparer.OrdinalIgnoreCase)
            {
                ["SI"] = new List<GalleryImageItem>(),
                ["DR"] = new List<GalleryImageItem>(),
                ["PO"] = new List<GalleryImageItem>(),
                ["PDF"] = new List<GalleryImageItem>()
            };

        private readonly List<int> _pendingDeleteImageIds = new List<int>();

        private bool _isDirty;

        // ─── Period / History fields ───
        private enum HistoryEntryKind { UnlinkedVersion, LinkedPeriod }

        private sealed class HistoryEntry
        {
            public HistoryEntryKind Kind { get; set; }
            public int ReceiptSetId { get; set; }
            public ReceiptSetRepository.CoveragePeriod Period { get; set; }
            public int DepthFromHead { get; set; }
            public ReceiptSetDto Metadata { get; set; }
            public string CreatedByName { get; set; }
            public string ModifiedByName { get; set; }
            public string ChangeSummary { get; set; }
            public string VersionLabel { get; set; }
            public DateTime SavedAt { get; set; }

            // Display properties for DataGrid binding
            public string SavedAtText => SavedAt == default ? "—" : SavedAt.ToString("yyyy-MM-dd HH:mm");
            public string Supplier => Metadata?.Supplier ?? "—";
            public string DocText => FormatDocColumn(Metadata);
            public string ImageIndicators => FormatImageIndicators(Metadata, null);

            private static string FormatDocColumn(ReceiptSetDto dto)
            {
                if (dto == null) return "—";
                var si = (dto.SiNumber ?? string.Empty).Trim();
                var dr = (dto.DrNumber ?? string.Empty).Trim();
                var po = (dto.PoNumber ?? string.Empty).Trim();
                bool hasSi = !string.IsNullOrWhiteSpace(si);
                bool hasDr = !string.IsNullOrWhiteSpace(dr);
                bool hasPo = !string.IsNullOrWhiteSpace(po);
                if (!hasSi && !hasDr && !hasPo) return "—";
                if (hasSi && hasDr && hasPo && string.Equals(si, dr, StringComparison.OrdinalIgnoreCase) && string.Equals(si, po, StringComparison.OrdinalIgnoreCase))
                    return si;
                var parts = new List<string>();
                if (hasSi) parts.Add("SI:" + si);
                if (hasDr) parts.Add("DR:" + dr);
                if (hasPo) parts.Add("PO:" + po);
                return string.Join("  ", parts);
            }

            internal static string FormatImageIndicators(ReceiptSetDto dto, Dictionary<string, int> imageCounts)
            {
                if (dto == null) return "—";
                if (imageCounts != null)
                {
                    int siCount, drCount, poCount;
                    imageCounts.TryGetValue("SI", out siCount);
                    imageCounts.TryGetValue("DR", out drCount);
                    imageCounts.TryGetValue("PO", out poCount);
                    return $"SI{siCount} DR{drCount} PO{poCount}";
                }
                bool si = !string.IsNullOrWhiteSpace(dto.SiImagePath) || (dto.SiImage != null && dto.SiImage.Length > 0);
                bool dr = !string.IsNullOrWhiteSpace(dto.DrImagePath) || (dto.DrImage != null && dto.DrImage.Length > 0);
                bool po = !string.IsNullOrWhiteSpace(dto.PoImagePath) || (dto.PoImage != null && dto.PoImage.Length > 0);
                string Mark(bool has) => has ? "✓" : "—";
                return $"SI{Mark(si)} DR{Mark(dr)} PO{Mark(po)}";
            }
        }

        private ReceiptSetRepository.CoveragePeriod _currentCoverage;
        private ReceiptSetRepository.CoveragePeriod _previousCoverage;
        private List<ReceiptSetRepository.CoveragePeriod> _historyCoverages = new List<ReceiptSetRepository.CoveragePeriod>();
        private List<HistoryEntry> _historyEntries = new List<HistoryEntry>();
        private int? _unlinkedHeadReceiptSetId;
        private int? _unlinkedPreviousReceiptSetId;
        private List<ReceiptSetDto> _unlinkedHistoryReceipts = new List<ReceiptSetDto>();
        private List<ReceiptSetDto> _unlinkedChainReceipts = new List<ReceiptSetDto>();
        private bool _readOnly;
        private bool _coveragePromptShown;
        private int _lastPeriodTabIndex;
        private List<string> _supplierNames;

        public ReceiptSetViewerWindow(ReceiptSetDto existing)
        {
            InitializeComponent();

            _receiptSetId = existing?.ReceiptSetId ?? 0;
            _setId = existing?.SetId;

            Loaded += (s, e) =>
            {
                try
                {
                    LoadData(existing);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this,
                        $"Error loading receipt data:\n{ex.GetType().Name}: {ex.Message}\n\nStack:\n{ex.StackTrace}",
                        "LoadData Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        private readonly string _initialSupplier;

        public ReceiptSetViewerWindow(int setId, string initialSupplier = null)
        {
            InitializeComponent();
            _setId = setId > 0 ? (int?)setId : null;
            _initialSupplier = initialSupplier;

            Loaded += (s, e) =>
            {
                try
                {
                    ReceiptSetDto existing = null;
                    try
                    {
                        if (_setId.HasValue)
                            existing = _repository.GetBySetId(_setId.Value);
                    }
                    catch (Exception ex2)
                    {
                        MessageBox.Show(this,
                            $"Error fetching receipt by setId={_setId}:\n{ex2.GetType().Name}: {ex2.Message}",
                            "GetBySetId Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        existing = null;
                    }

                    LoadData(existing);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this,
                        $"Error loading receipt data:\n{ex.GetType().Name}: {ex.Message}\n\nStack:\n{ex.StackTrace}",
                        "LoadData Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        private void LoadData(ReceiptSetDto dto)
        {
            TxtSubtitle.Text = _receiptSetId > 0 ? $"Receipt Set #{_receiptSetId}" : "New Receipt Set";

            LoadSupplierSuggestions();

            if (dto != null)
            {
                _receiptSetId = dto.ReceiptSetId;
                _setId = dto.SetId ?? _setId;

                CmbSupplier.Text = dto.Supplier ?? string.Empty;
                TxtSiNumber.Text = dto.SiNumber ?? string.Empty;
                TxtDrNumber.Text = dto.DrNumber ?? string.Empty;
                TxtPoNumber.Text = dto.PoNumber ?? string.Empty;
            }
            else if (!string.IsNullOrWhiteSpace(_initialSupplier))
            {
                // No receipt exists for this Set yet -- prefill Supplier from the caller (e.g. the
                // Invoice's already-selected Distributor) so it doesn't need retyping.
                CmbSupplier.Text = _initialSupplier;
            }

            LoadImagesForAllTypes();
            RenderGalleryForCurrentTab();
            _isDirty = false;

            InitializePeriodTabs();
        }

        private async void LoadSupplierSuggestions()
        {
            try
            {
                var repo = new VendorRepository();
                var vendors = await repo.GetAllVendorsAsync().ConfigureAwait(true);
                var names = vendors
                    .Where(v => v != null && v.IsActive && !string.IsNullOrWhiteSpace(v.VendorName))
                    .Select(v => v.VendorName.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _supplierNames = names;
                CmbSupplier.ItemsSource = names;
            }
            catch
            {
                // If the vendor list can't be loaded, leave _supplierNames null so
                // ValidateBeforeSave skips the exists-in-list check rather than blocking every save.
                _supplierNames = null;
            }
        }

        private void LoadImagesForAllTypes()
        {
            _pendingDeleteImageIds.Clear();

            foreach (var docType in new[] { "SI", "DR", "PO", "PDF" })
            {
                _imagesByType[docType].Clear();

                if (_receiptSetId <= 0)
                    continue;

                List<ReceiptSetImageDto> rows;
                try
                {
                    rows = _repository.GetImagesForReceiptSet(_receiptSetId, docType);
                }
                catch
                {
                    rows = new List<ReceiptSetImageDto>();
                }

                foreach (var row in rows)
                {
                    _imagesByType[docType].Add(new GalleryImageItem
                    {
                        ImageId = row.ImageId,
                        ImageBytes = row.ImageBytes,
                        ImagePath = row.ImagePath,
                        MimeType = row.MimeType,
                        IsNew = false
                    });
                }
            }
        }

        private void DocTab_Checked(object sender, RoutedEventArgs e)
        {
            if (!(sender is RadioButton rb) || rb.Tag == null)
                return;

            _currentDocType = rb.Tag.ToString();
            ApplyDocumentState();
            RenderGalleryForCurrentTab();
        }

        private void ApplyDocumentState()
        {
            var hasPdf = _imagesByType["PDF"].Count > 0 && _imagesByType["PDF"].Any(i => !i.MarkedForDeletion);

            BtnAddImages.IsEnabled = !hasPdf && !_readOnly;

            if (_currentDocType != "PDF")
            {
                var items = _imagesByType.TryGetValue(_currentDocType, out var list) ? list : new List<GalleryImageItem>();
                TxtGalleryHint.Text = hasPdf
                    ? $"PDF attached — replaces SI, DR, and PO. ({(items.Count > 0 ? $"{items.Count} existing image(s) shown below" : "no images to display")})"
                    : items.Count == 0
                        ? $"No {_currentDocType} images yet. Drag and drop images here, or use Add Image(s)."
                        : $"{items.Count} image(s) attached for {_currentDocType}. Drag and drop to add more.";
            }
        }

        private void RenderGalleryForCurrentTab()
        {
            var items = _imagesByType.TryGetValue(_currentDocType, out var list) ? list : new List<GalleryImageItem>();

            ImageGallery.ItemsSource = null;

            if (_currentDocType == "PDF")
            {
                var cards = items.Select(item => BuildPdfCard(item)).ToList();
                ImageGallery.ItemsSource = cards;

                TxtGalleryHint.Text = items.Count == 0
                    ? "No PDF attached yet. Click Add PDF to attach a combined document (replaces SI/DR/PO)."
                    : "PDF attached — replaces SI, DR, and PO.";

                EmptyGalleryState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (items.Count == 0)
                {
                    EmptyStateIcon.Text = "📕";
                    EmptyStateTitle.Text = "No PDF attached";
                    EmptyStateHint.Text = "Click Add PDF to attach a combined document that replaces SI, DR, and PO.";
                }
                UpdateTabBadges();
                return;
            }

            var imageCards = items.Select((item, index) => BuildImageCard(item, index)).ToList();
            ImageGallery.ItemsSource = imageCards;

            EmptyGalleryState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (items.Count == 0)
            {
                EmptyStateIcon.Text = "🖼";
                EmptyStateTitle.Text = "No images attached";
                EmptyStateHint.Text = "Drag and drop image files here, or click Add Image(s) above.";
            }

            UpdateTabBadges();
        }

        private void UpdateTabBadges()
        {
            TxtCountSi.Text = _imagesByType["SI"].Count.ToString();
            TxtCountDr.Text = _imagesByType["DR"].Count.ToString();
            TxtCountPo.Text = _imagesByType["PO"].Count.ToString();
            TxtCountPdf.Text = _imagesByType["PDF"].Count.ToString();
        }

        private Border BuildImageCard(GalleryImageItem item, int index)
        {
            var card = new Border
            {
                Width = 178,
                Height = 216,
                Margin = new Thickness(0, 0, 14, 14),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xEE)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 10,
                    ShadowDepth = 2,
                    Opacity = 0.08,
                    Direction = 270,
                    Color = Color.FromRgb(0x0A, 0x1A, 0x2A)
                }
            };

            var stack = new Grid();
            stack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            stack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var imgHost = new Border
            {
                Margin = new Thickness(8, 8, 8, 6),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xF9)),
                ClipToBounds = true
            };

            if (item.IsPdf)
            {
                // WPF's BitmapImage can't decode a PDF -- show a recognizable placeholder instead
                // of silently failing to render (which used to look like a blank/broken thumbnail).
                var pdfPlaceholder = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand
                };
                pdfPlaceholder.Children.Add(new TextBlock
                {
                    Text = "📄",
                    FontSize = 40,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                pdfPlaceholder.Children.Add(new TextBlock
                {
                    Text = "PDF — click to open",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x7A, 0x8A)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 6, 0, 0)
                });
                pdfPlaceholder.MouseLeftButtonUp += (s, e) => OpenFullscreen(item);
                imgHost.Child = pdfPlaceholder;
            }
            else
            {
                var img = new Image
                {
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Margin = new Thickness(4),
                    Cursor = Cursors.Hand
                };
                img.Source = TryLoadThumbnail(item);
                img.MouseLeftButtonUp += (s, e) => OpenFullscreen(item);
                imgHost.Child = img;
            }
            Grid.SetRow(imgHost, 0);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var lblIndex = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xF0, 0xF4)),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = $"#{index + 1}",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x7A, 0x8A))
                }
            };

            var btnUp = MakeIconButton("↑", () => MoveImage(item, -1));
            var btnDown = MakeIconButton("↓", () => MoveImage(item, 1));
            var btnDelete = MakeIconButton("🗑", () => DeleteImage(item), danger: true);

            footer.Children.Add(lblIndex);
            footer.Children.Add(btnUp);
            footer.Children.Add(btnDown);
            footer.Children.Add(btnDelete);
            Grid.SetRow(footer, 1);

            stack.Children.Add(imgHost);
            stack.Children.Add(footer);
            card.Child = stack;

            return card;
        }

        private static Button MakeIconButton(string glyph, Action onClick, bool danger = false)
        {
            var normalBg = new SolidColorBrush(Color.FromRgb(0xEC, 0xF0, 0xF4));
            var dangerBg = new SolidColorBrush(Color.FromRgb(0xFB, 0xE8, 0xE8));
            var normalHoverBg = new SolidColorBrush(Color.FromRgb(0xDC, 0xE4, 0xEC));
            var dangerHoverBg = new SolidColorBrush(Color.FromRgb(0xF6, 0xCF, 0xCC));

            var btn = new Button
            {
                Content = glyph,
                Width = 28,
                Height = 28,
                Margin = new Thickness(3, 0, 0, 0),
                FontSize = 11.5,
                Cursor = Cursors.Hand,
                Background = danger ? dangerBg : normalBg,
                Foreground = danger ? new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B)) : new SolidColorBrush(Color.FromRgb(0x3A, 0x4A, 0x5A)),
                BorderThickness = new Thickness(0)
            };

            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border), "Bd");
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;
            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, danger ? dangerHoverBg : normalHoverBg, "Bd"));
            template.Triggers.Add(hoverTrigger);
            btn.Template = template;

            btn.Click += (s, e) => onClick?.Invoke();
            return btn;
        }

        private static BitmapImage TryLoadThumbnail(GalleryImageItem item)
        {
            try
            {
                byte[] bytes = item.ImageBytes;
                if ((bytes == null || bytes.Length == 0) && !string.IsNullOrWhiteSpace(item.ImagePath) && File.Exists(item.ImagePath))
                    bytes = File.ReadAllBytes(item.ImagePath);

                if (bytes == null || bytes.Length == 0)
                    return null;

                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(bytes))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                }
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private Border BuildPdfCard(GalleryImageItem item)
        {
            var card = new Border
            {
                Width = 280,
                Height = 200,
                Margin = new Thickness(0, 0, 14, 14),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xEE)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 10,
                    ShadowDepth = 2,
                    Opacity = 0.08,
                    Direction = 270,
                    Color = Color.FromRgb(0x0A, 0x1A, 0x2A)
                }
            };

            var stack = new Grid();
            stack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            stack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var iconHost = new Border
            {
                Margin = new Thickness(8, 8, 8, 6),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(0xFD, 0xF0, 0xE0)),
                ClipToBounds = true
            };

            var fileName = !string.IsNullOrWhiteSpace(item.ImagePath)
                ? System.IO.Path.GetFileName(item.ImagePath)
                : "document.pdf";

            var infoText = new TextBlock
            {
                Text = fileName,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0x4A, 0x5A)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(8, 0, 8, 2)
            };

            if (item.ImageBytes != null && item.ImageBytes.Length > 0)
            {
                var sizeKb = item.ImageBytes.Length / 1024.0;
                var sizeText = sizeKb >= 1024
                    ? $"{sizeKb / 1024.0:F1} MB"
                    : $"{sizeKb:F0} KB";

                infoText.Inlines.Add(new LineBreak());
                infoText.Inlines.Add(new Run
                {
                    Text = sizeText,
                    FontSize = 10,
                    FontWeight = FontWeights.Normal,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x98, 0xA8))
                });
            }

            iconHost.Child = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = "📕",
                        FontSize = 56,
                        TextAlignment = TextAlignment.Center
                    },
                    infoText
                }
            };

            Grid.SetRow(iconHost, 0);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var btnOpen = MakeIconButton("▶", () => OpenPdf(item));
            var btnDownload = MakeIconButton("💾", () => DownloadPdf(item));
            var btnDelete = MakeIconButton("🗑", () => DeleteImage(item), danger: true);

            footer.Children.Add(btnOpen);
            footer.Children.Add(btnDownload);
            footer.Children.Add(btnDelete);

            Grid.SetRow(footer, 1);

            stack.Children.Add(iconHost);
            stack.Children.Add(footer);
            card.Child = stack;

            return card;
        }

        private void OpenPdf(GalleryImageItem item)
        {
            try
            {
                // A per-open subfolder (rather than a GUID in the filename itself) keeps the
                // browser tab/title showing the clean name (e.g. "Invoice_1233-SI.pdf") while
                // still avoiding collisions if the same document is opened twice.
                var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                var tempPath = Path.Combine(tempDir, $"{BuildDocumentFileNameBase(item)}.pdf");

                File.WriteAllBytes(tempPath, item.ImageBytes);
                Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to open PDF:\n{ex.Message}", "Open PDF",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DownloadPdf(GalleryImageItem item)
        {
            var dialog = new SaveFileDialog
            {
                FileName = $"{BuildDocumentFileNameBase(item)}.pdf",
                Filter = "PDF Document (*.pdf)|*.pdf"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllBytes(dialog.FileName, item.ImageBytes);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to save PDF: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenFullscreen(GalleryImageItem item)
        {
            try
            {
                if (item.IsPdf)
                {
                    if (item.ImageBytes == null || item.ImageBytes.Length == 0)
                        return;

                    OpenPdf(item);
                    return;
                }

                var bmp = TryLoadThumbnail(item);
                if (bmp == null)
                    return;

                var fileName = !string.IsNullOrWhiteSpace(item.ImagePath)
                    ? System.IO.Path.GetFileName(item.ImagePath)
                    : $"{_currentDocType} image";

                var win = new Window
                {
                    Title = $"{_currentDocType} — {fileName}",
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Width = 900,
                    Height = 750,
                    Background = Brushes.White,
                    WindowStyle = WindowStyle.SingleBorderWindow,
                    ResizeMode = ResizeMode.CanResize,
                    MinWidth = 500,
                    MinHeight = 400
                };

                var root = new Grid();
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                var headerBar = new Border
                {
                    Height = 42,
                    Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x2A, 0x3A)),
                    Padding = new Thickness(14, 0, 8, 0)
                };
                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var titleText = new TextBlock
                {
                    Text = fileName,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(titleText, 0);

                var btnClose = new Button
                {
                    Content = new TextBlock
                    {
                        Text = "✕",
                        FontSize = 14,
                        Foreground = Brushes.White
                    },
                    Width = 32,
                    Height = 32,
                    Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x4A)),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = "Close"
                };
                btnClose.Click += (s, e) => win.Close();
                Grid.SetColumn(btnClose, 1);

                headerGrid.Children.Add(titleText);
                headerGrid.Children.Add(btnClose);
                headerBar.Child = headerGrid;
                Grid.SetRow(headerBar, 0);

                var viewerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xF9)),
                    Child = new Viewbox
                    {
                        Stretch = System.Windows.Media.Stretch.Uniform,
                        Child = new Image
                        {
                            Source = bmp,
                            Stretch = System.Windows.Media.Stretch.Uniform,
                            MaxWidth = 900,
                            MaxHeight = 680
                        }
                    }
                };
                Grid.SetRow(viewerBorder, 1);

                root.Children.Add(headerBar);
                root.Children.Add(viewerBorder);
                win.Content = root;
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to open image: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Header subtitle: "Set SET-1352" normally, or "Set SET-1352  ·  Doc # INV-00123" when
        // the linked Set is itself an invoice (IsInvoice = 1) and has a Document Number — so the
        // invoice's own document number is visible right beside the Set Code, not just the SetId.
        private string BuildSetSubtitle(int setId)
        {
            var linkInfo = _repository.GetSetLinkInfo(setId);
            string setLabel = !string.IsNullOrWhiteSpace(linkInfo?.SetCode) ? linkInfo.SetCode : $"#{setId}";

            return (linkInfo != null && linkInfo.IsInvoice && !string.IsNullOrWhiteSpace(linkInfo.DocumentNumber))
                ? $"Set {setLabel}  ·  Doc # {linkInfo.DocumentNumber}"
                : $"Set {setLabel}";
        }

        // "Invoice_1233-SI" / "Set_SET-1340-DR" -- Invoice DocumentNumber or Set SetCode
        // (whichever applies), plus the doc type currently open. Falls back to the raw SetId if
        // GetSetLinkInfo can't resolve a code (e.g. Set was deleted after this window opened).
        private string BuildDocumentFileNameBase(GalleryImageItem item)
        {
            string prefix = "Set";
            string identifier = _setId.HasValue ? _setId.Value.ToString() : "Unknown";

            if (_setId.HasValue)
            {
                var linkInfo = _repository.GetSetLinkInfo(_setId.Value);
                if (linkInfo != null)
                {
                    if (linkInfo.IsInvoice)
                    {
                        prefix = "Invoice";
                        identifier = !string.IsNullOrWhiteSpace(linkInfo.DocumentNumber) ? linkInfo.DocumentNumber : _setId.Value.ToString();
                    }
                    else
                    {
                        prefix = "Set";
                        identifier = !string.IsNullOrWhiteSpace(linkInfo.SetCode) ? linkInfo.SetCode : _setId.Value.ToString();
                    }
                }
            }

            return $"{prefix}_{SanitizeForFileName(identifier)}-{_currentDocType}";
        }

        private static string SanitizeForFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Unknown";
            var invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        private void MoveImage(GalleryImageItem item, int delta)
        {
            var list = _imagesByType[_currentDocType];
            int idx = list.IndexOf(item);
            int newIdx = idx + delta;
            if (idx < 0 || newIdx < 0 || newIdx >= list.Count)
                return;

            list.RemoveAt(idx);
            list.Insert(newIdx, item);
            _isDirty = true;
            RenderGalleryForCurrentTab();
        }

        private void DeleteImage(GalleryImageItem item)
        {
            var confirm = MessageBox.Show(this,
                $"Remove this {_currentDocType} image?",
                "Remove Image",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            _imagesByType[_currentDocType].Remove(item);
            item.MarkedForDeletion = true;
            if (item.ImageId > 0)
                _pendingDeleteImageIds.Add(item.ImageId);
            _isDirty = true;
            RenderGalleryForCurrentTab();
        }

        private void BtnAddImages_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select image(s)",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff|All Files|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog(this) != true)
                return;

            AddImagesFromFiles(dialog.FileNames);
        }

        private void BtnAddPdf_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select PDF document",
                Filter = "PDF Files|*.pdf|All Files|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
                return;

            var path = dialog.FileName;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            var existing = _imagesByType["PDF"].FirstOrDefault(i => !i.MarkedForDeletion);
            if (existing != null)
            {
                var replace = MessageBox.Show(this,
                    "A PDF is already attached to this receipt set.\n\nReplace it with the new one?",
                    "Replace PDF", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (replace != MessageBoxResult.Yes)
                    return;

                _imagesByType["PDF"].Remove(existing);
                if (existing.ImageId > 0)
                    _pendingDeleteImageIds.Add(existing.ImageId);
            }

            try
            {
                var bytes = File.ReadAllBytes(path);
                _imagesByType["PDF"].Add(new GalleryImageItem
                {
                    ImageId = 0,
                    ImageBytes = bytes,
                    ImagePath = Path.GetFileName(path),
                    IsNew = true
                });
                _isDirty = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to add PDF:\n{ex.Message}", "Add PDF",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            RenderGalleryForCurrentTab();
            ApplyDocumentState();
        }

        private void AddImagesFromFiles(IEnumerable<string> filePaths)
        {
            if (filePaths == null)
                return;

            foreach (var path in filePaths)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        continue;

                    var bytes = File.ReadAllBytes(path);
                    _imagesByType[_currentDocType].Add(new GalleryImageItem
                    {
                        ImageId = 0,
                        ImageBytes = bytes,
                        ImagePath = path,
                        IsNew = true
                    });
                    _isDirty = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to add {Path.GetFileName(path)}:\n{ex.Message}", "Add Image",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            RenderGalleryForCurrentTab();
        }

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            if (_currentDocType == "PDF")
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = files.Any(f => ".pdf".Equals(System.IO.Path.GetExtension(f), StringComparison.OrdinalIgnoreCase))
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
                return;
            }

            e.Effects = DragDropEffects.Copy;
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (_currentDocType == "PDF")
            {
                foreach (var file in files)
                {
                    var ext = System.IO.Path.GetExtension(file)?.ToLowerInvariant();
                    if (ext == ".pdf")
                    {
                        var existing = _imagesByType["PDF"].FirstOrDefault(i => !i.MarkedForDeletion);
                        if (existing != null)
                        {
                            _imagesByType["PDF"].Remove(existing);
                            if (existing.ImageId > 0)
                                _pendingDeleteImageIds.Add(existing.ImageId);
                        }

                        try
                        {
                            var bytes = File.ReadAllBytes(file);
                            _imagesByType["PDF"].Add(new GalleryImageItem
                            {
                                ImageId = 0,
                                ImageBytes = bytes,
                                ImagePath = System.IO.Path.GetFileName(file),
                                IsNew = true
                            });
                            _isDirty = true;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, $"Failed to add PDF:\n{ex.Message}", "Add PDF",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }

                RenderGalleryForCurrentTab();
                ApplyDocumentState();
                return;
            }

            AddImagesFromFiles(files);
        }

        private bool ValidateBeforeSave()
        {
            if (string.IsNullOrWhiteSpace(CmbSupplier.Text))
            {
                MessageBox.Show(this, "Supplier is required before saving.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbSupplier.Focus();
                return false;
            }

            // Only enforce "must match an existing Vendor" when the list actually loaded --
            // _supplierNames is null (not empty) if LoadSupplierSuggestions failed, so a network/DB
            // hiccup degrades to free text instead of blocking every save.
            if (_supplierNames != null &&
                !_supplierNames.Any(n => string.Equals(n, CmbSupplier.Text.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "Supplier must be selected from the existing vendor list. Start typing to search, then pick a match.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbSupplier.Focus();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Error fallback for saving a receipt set whose SI number already exists on a different
        /// receipt set. There is no database-level uniqueness constraint on SiNumber, so this is
        /// an application-side guard: warns the user and lets them cancel or save anyway (e.g.
        /// legitimate re-deliveries can reuse an SI number).
        /// </summary>
        private bool ConfirmNoDuplicateSiNumber(ReceiptSetDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.SiNumber))
                return true;

            ReceiptSetDto existing;
            try
            {
                existing = _repository.FindReceiptSetBySiNumber(dto.SiNumber, dto.ReceiptSetId);
            }
            catch (Exception ex)
            {
                // Non-fatal: if the duplicate check itself fails, don't block the whole save —
                // just let the user know it couldn't be verified.
                MessageBox.Show(this,
                    "Could not verify whether this SI number already exists:\n" + ex.Message +
                    "\n\nContinuing without the duplicate check.",
                    "Receipt Set", MessageBoxButton.OK, MessageBoxImage.Warning);
                return true;
            }

            if (existing == null)
                return true;

            var confirm = MessageBox.Show(this,
                $"SI number \"{dto.SiNumber.Trim()}\" is already used by another receipt set " +
                $"(Receipt #{existing.ReceiptSetId}, Supplier: {(string.IsNullOrWhiteSpace(existing.Supplier) ? "(none)" : existing.Supplier.Trim())}, " +
                $"Created: {existing.CreatedAt:yyyy-MM-dd}).\n\n" +
                "Save this receipt set anyway?",
                "Duplicate SI Number",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            return confirm == MessageBoxResult.Yes;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateBeforeSave())
                return;

            try
            {
                int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;

                var dto = new ReceiptSetDto
                {
                    ReceiptSetId = _receiptSetId,
                    Supplier = CmbSupplier.Text.Trim(),
                    SiNumber = string.IsNullOrWhiteSpace(TxtSiNumber.Text) ? null : TxtSiNumber.Text.Trim(),
                    DrNumber = string.IsNullOrWhiteSpace(TxtDrNumber.Text) ? null : TxtDrNumber.Text.Trim(),
                    PoNumber = string.IsNullOrWhiteSpace(TxtPoNumber.Text) ? null : TxtPoNumber.Text.Trim(),
                    // Legacy single-image columns: keep in sync with the first image of each type
                    // so older code paths (PDF export, list thumbnails) still show something.
                    SiImage = _imagesByType["SI"].FirstOrDefault()?.ImageBytes,
                    SiImagePath = _imagesByType["SI"].FirstOrDefault()?.ImagePath,
                    DrImage = _imagesByType["DR"].FirstOrDefault()?.ImageBytes,
                    DrImagePath = _imagesByType["DR"].FirstOrDefault()?.ImagePath,
                    PoImage = _imagesByType["PO"].FirstOrDefault()?.ImageBytes,
                    PoImagePath = _imagesByType["PO"].FirstOrDefault()?.ImagePath
                };

                if (!ConfirmNoDuplicateSiNumber(dto))
                    return;

                _receiptSetId = _repository.Save(dto, userId);

                if (_setId.HasValue)
                {
                    _repository.AttachReceiptSetToSet(_receiptSetId, _setId.Value, userId);
                }

                SaveImagesForType("SI", userId);
                SaveImagesForType("DR", userId);
                SaveImagesForType("PO", userId);
                SaveImagesForType("PDF", userId);

                foreach (var imageId in _pendingDeleteImageIds.Distinct().ToList())
                {
                    _repository.DeleteImage(imageId);
                }
                _pendingDeleteImageIds.Clear();

                _isDirty = false;

                MessageBox.Show(this, "Receipt set saved successfully.", "Receipt Set",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // If this receipt isn't linked to an invoice Set yet, offer to link it now when its
                // SI/DR/PO number matches an existing invoice's document number.
                if (!_setId.HasValue || _setId.Value <= 0)
                {
                    OfferAutoLinkToMatchingInvoice(userId);
                }

                // Reload to reflect real ImageIds assigned by the database.
                LoadImagesForAllTypes();
                RenderGalleryForCurrentTab();
                UpdateCoverageTabState();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to save receipt set:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// After saving an unlinked receipt, checks whether its SI/DR/PO number matches an
        /// existing invoice's document number and, if so, offers to link the two now instead of
        /// requiring a separate manual "Link Receipt Set" step later.
        /// </summary>
        private void OfferAutoLinkToMatchingInvoice(int? userId)
        {
            string[] candidates =
            {
                TxtSiNumber.Text,
                TxtDrNumber.Text,
                TxtPoNumber.Text
            };

            ReceiptSetLinkedSetDto match = null;
            string matchedDocNo = null;

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
                    matchedDocNo = docNo;
                    break;
                }
            }

            if (match == null || match.SetId <= 0)
                return;

            var confirm = MessageBox.Show(this,
                $"An invoice with document number \"{matchedDocNo}\" already exists (Set #{match.SetCode ?? match.SetId.ToString()}).\n\n" +
                "Link this receipt set to that invoice now?",
                "Matching Invoice Found",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                _repository.AttachReceiptSetToSet(_receiptSetId, match.SetId, userId);
                _setId = match.SetId;
                _coveragePromptShown = false;

                MessageBox.Show(this, "Receipt set linked to the matching invoice.", "Receipt Set",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to link receipt set:\n" + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveImagesForType(string docType, int? userId)
        {
            var items = _imagesByType[docType];

            // New images: insert.
            foreach (var item in items.Where(i => i.IsNew && i.ImageId <= 0))
            {
                item.ImageId = _repository.AddImage(_receiptSetId, docType, item.ImageBytes, item.ImagePath, userId);
                item.IsNew = false;
            }

            // Persist final display order for all existing (now-saved) images.
            var orderedIds = items.Where(i => i.ImageId > 0).Select(i => i.ImageId).ToList();
            if (orderedIds.Count > 0)
            {
                _repository.ReorderImages(_receiptSetId, docType, orderedIds, userId);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_isDirty)
            {
                var confirm = MessageBox.Show(this,
                    "You have unsaved changes.\n\nClose anyway and discard changes?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            Close();
        }

        // Reloads the header (Set Code / Doc #) and the currently active period tab's receipt
        // documents straight from the database — e.g. after someone else attached a receipt to
        // this Set from elsewhere while this window was already open.
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_isDirty)
            {
                var confirm = MessageBox.Show(this,
                    "You have unsaved changes.\n\nRefresh and discard changes?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                    return;
                _isDirty = false;
            }

            InitializePeriodTabs();

            RadioButton activeTab = TabPeriodCurrent.IsChecked == true ? TabPeriodCurrent
                                   : TabPeriodPrevious.IsChecked == true ? TabPeriodPrevious
                                   : TabPeriodHistory;
            // promptCreateIfMissing: false — a refresh should just reload silently, not pop up
            // the "create a new receipt set for this period?" prompt that a genuine tab switch does.
            LoadPeriodTab(activeTab, promptCreateIfMissing: false);
        }

        // ─── Period / History / Revert ──────────────────────────────────────

        private void PeriodTab_Checked(object sender, RoutedEventArgs e)
        {
            if (!(sender is RadioButton rb) || rb.Tag == null)
                return;
            LoadPeriodTab(rb, promptCreateIfMissing: true);
        }

        private void LoadPeriodTab(RadioButton rb, bool promptCreateIfMissing)
        {
            if (_isDirty && _lastPeriodTabIndex >= 0)
            {
                var confirm = MessageBox.Show(this,
                    "You have unsaved changes.\n\nSwitch periods and discard changes?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                    return;
                _isDirty = false;
            }

            string tab = rb.Tag.ToString();
            _lastPeriodTabIndex = tab == "Current" ? 0 : tab == "Previous" ? 1 : 2;

            GalleryContent.Visibility = tab == "History" ? Visibility.Collapsed : Visibility.Visible;
            HistoryContent.Visibility = tab == "History" ? Visibility.Visible : Visibility.Collapsed;

            if (tab == "Current")
            {
                ApplyReadOnlyState(false);
                BtnRevert.Visibility = Visibility.Collapsed;
                LoadReceiptForCoverage(_currentCoverage, promptCreateIfMissing: promptCreateIfMissing);
            }
            else if (tab == "Previous")
            {
                ApplyReadOnlyState(true);
                BtnRevert.Visibility = Visibility.Visible;

                bool loaded = false;
                if (_setId.HasValue && _previousCoverage != null)
                {
                    LoadReceiptForCoverage(_previousCoverage, promptCreateIfMissing: false);
                    loaded = true;
                }
                else if (_unlinkedPreviousReceiptSetId.HasValue)
                {
                    var dto = _repository.GetByReceiptSetId(_unlinkedPreviousReceiptSetId.Value);
                    if (dto != null)
                    {
                        LoadFromDto(dto);
                        loaded = true;
                    }
                }

                if (!loaded)
                {
                    ClearFields();
                    TxtSubtitle.Text = "No previous coverage period exists.";
                }
            }
            else if (tab == "History")
            {
                PopulateHistoryGrid();
            }
        }

        private void InitializePeriodTabs()
        {
            _historyCoverages.Clear();
            _unlinkedHistoryReceipts.Clear();
            _unlinkedChainReceipts.Clear();
            _unlinkedHeadReceiptSetId = null;
            _unlinkedPreviousReceiptSetId = null;
            _coveragePromptShown = false;

            if (_setId.HasValue && _setId.Value > 0)
            {
                TxtSubtitle.Text = BuildSetSubtitle(_setId.Value);
                InitializeCoverageTabsForSet(_setId.Value);
            }
            else if (_receiptSetId > 0)
            {
                TxtSubtitle.Text = $"Init: unlinked receipt #{_receiptSetId}";
                InitializeUnlinkedRenewalTabsForReceiptSet(_receiptSetId);
            }
            else
            {
                TxtSubtitle.Text = "Init: no setId or receiptSetId";
                GalleryContent.Visibility = Visibility.Visible;
                HistoryContent.Visibility = Visibility.Collapsed;
            }
        }

        private void InitializeCoverageTabsForSet(int setId)
        {
            try
            {
                var renewedPeriods = _repository.GetRenewedCoveragePeriodsForSet(setId) ?? new List<ReceiptSetRepository.CoveragePeriod>();
                var setPeriod = _repository.GetSetCoveragePeriod(setId);
                var linkPeriods = _repository.GetLinkCoveragePeriodsForSet(setId);

                // Priority 1: renewed periods from Renewals table
                if (renewedPeriods.Count > 0)
                {
                    _currentCoverage = renewedPeriods[0];
                    _previousCoverage = renewedPeriods.Count > 1 ? renewedPeriods[1] : null;
                    _historyCoverages = renewedPeriods.Count > 2 ? renewedPeriods.Skip(2).ToList() : new List<ReceiptSetRepository.CoveragePeriod>();
                }
                // Priority 2: link records (ReceiptSetLink) — may have multiple periods
                else if (linkPeriods.Count > 0)
                {
                    _currentCoverage = linkPeriods[0];
                    _previousCoverage = linkPeriods.Count > 1 ? linkPeriods[1] : null;
                    _historyCoverages = linkPeriods.Count > 2 ? linkPeriods.Skip(2).ToList() : new List<ReceiptSetRepository.CoveragePeriod>();
                }
                // Priority 3: Set's own StartDate/EndDate
                else if (setPeriod != null)
                {
                    _currentCoverage = setPeriod;
                    _previousCoverage = null;
                    _historyCoverages = new List<ReceiptSetRepository.CoveragePeriod>();
                }
                else
                {
                    _currentCoverage = null;
                    _previousCoverage = null;
                    _historyCoverages = new List<ReceiptSetRepository.CoveragePeriod>();
                }

                BuildHistoryEntries();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeCoverageTabsForSet error: {ex.Message}");
            }
        }

        private void InitializeUnlinkedRenewalTabsForReceiptSet(int headReceiptSetId)
        {
            _unlinkedHeadReceiptSetId = headReceiptSetId > 0 ? (int?)headReceiptSetId : null;
            _unlinkedPreviousReceiptSetId = null;
            _unlinkedHistoryReceipts = new List<ReceiptSetDto>();

            if (!_unlinkedHeadReceiptSetId.HasValue)
                return;

            try
            {
                var chain = _repository.GetReceiptRenewalChainMetadata(_unlinkedHeadReceiptSetId.Value) ?? new List<ReceiptSetDto>();
                _unlinkedChainReceipts = chain;
                if (chain.Count > 1 && chain[1] != null && chain[1].ReceiptSetId > 0)
                    _unlinkedPreviousReceiptSetId = chain[1].ReceiptSetId;

                if (chain.Count > 1)
                {
                    _unlinkedHistoryReceipts = chain
                        .Skip(1)
                        .Where(x => x != null && x.ReceiptSetId > 0)
                        .ToList();
                }

                _currentCoverage = null;
                _previousCoverage = null;

                BuildHistoryEntries();
            }
            catch
            {
                // Non-fatal
            }
        }

        private void LoadReceiptForCoverage(ReceiptSetRepository.CoveragePeriod period, bool promptCreateIfMissing)
        {
            if (period == null || !_setId.HasValue)
                return;

            ReceiptSetDto dto = null;
            try
            {
                dto = _repository.GetBySetIdCoverage(_setId.Value, period.StartDate, period.EndDate);
            }
            catch
            {
                dto = null;
            }

            if (dto != null)
            {
                LoadFromDto(dto);
                return;
            }

            if (_readOnly)
            {
                ClearFields();
                RenderGalleryForCurrentTab();
                return;
            }

            if (promptCreateIfMissing && !_coveragePromptShown)
            {
                _coveragePromptShown = true;
                var renewedPeriods = _repository.GetRenewedCoveragePeriodsForSet(_setId.Value) ?? new List<ReceiptSetRepository.CoveragePeriod>();
                bool isRenewedPeriod = renewedPeriods.Any(p => p.StartDate == period.StartDate && p.EndDate == period.EndDate);
                if (isRenewedPeriod)
                {
                    var label = $"{period.StartDate:yyyy}-{period.EndDate:yyyy}";
                    var confirm = MessageBox.Show(this,
                        $"This set was renewed for {label}.\n\nNo receipt set exists for this period yet.\n\nCreate a new receipt set now?",
                        "New Renewal Period",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);
                    if (confirm != MessageBoxResult.Yes)
                        return;
                }
            }

            ClearFields();
            _isDirty = false;
            RenderGalleryForCurrentTab();
        }

        private void LoadFromDto(ReceiptSetDto dto)
        {
            if (dto == null) return;

            _receiptSetId = dto.ReceiptSetId;
            _setId = dto.SetId ?? _setId;

            CmbSupplier.Text = dto.Supplier ?? string.Empty;
            TxtSiNumber.Text = dto.SiNumber ?? string.Empty;
            TxtDrNumber.Text = dto.DrNumber ?? string.Empty;
            TxtPoNumber.Text = dto.PoNumber ?? string.Empty;

            _isDirty = false;
            LoadImagesForAllTypes();
            RenderGalleryForCurrentTab();
        }

        private void ClearFields()
        {
            CmbSupplier.Text = string.Empty;
            TxtSiNumber.Text = string.Empty;
            TxtDrNumber.Text = string.Empty;
            TxtPoNumber.Text = string.Empty;
            _receiptSetId = 0;
            _imagesByType["SI"].Clear();
            _imagesByType["DR"].Clear();
            _imagesByType["PO"].Clear();
            _pendingDeleteImageIds.Clear();
            RenderGalleryForCurrentTab();
        }

        private void ApplyReadOnlyState(bool readOnly)
        {
            _readOnly = readOnly;
            CmbSupplier.IsEnabled = !readOnly;
            TxtSiNumber.IsReadOnly = readOnly;
            TxtDrNumber.IsReadOnly = readOnly;
            TxtPoNumber.IsReadOnly = readOnly;
            BtnSave.IsEnabled = !readOnly;
            BtnAddImages.IsEnabled = !readOnly;
            BtnAddPdf.IsEnabled = !readOnly;
        }

        private void BuildHistoryEntries()
        {
            _historyEntries.Clear();

            if (_historyCoverages.Count > 0 && _setId.HasValue)
            {
                // Linked coverage periods
                foreach (var p in _historyCoverages)
                {
                    ReceiptSetDto meta = null;
                    try { meta = _repository.GetMetadataBySetIdCoverage(_setId.Value, p.StartDate, p.EndDate); }
                    catch { meta = null; }
                    if (meta == null) continue;

                    _historyEntries.Add(new HistoryEntry
                    {
                        Kind = HistoryEntryKind.LinkedPeriod,
                        ReceiptSetId = meta.ReceiptSetId,
                        Period = p,
                        Metadata = meta,
                        VersionLabel = $"{p.StartDate:yyyy-MM-dd}→{p.EndDate:yyyy-MM-dd}",
                        SavedAt = meta.ModifiedAt ?? meta.CreatedAt,
                        ChangeSummary = BuildChangeSummary(CurrentDto(), meta)
                    });
                }
            }
            else if (_unlinkedHistoryReceipts.Count > 0)
            {
                // Unlinked renewal chain
                for (int i = 0; i < _unlinkedHistoryReceipts.Count; i++)
                {
                    var older = _unlinkedHistoryReceipts[i];
                    if (older == null || older.ReceiptSetId <= 0) continue;

                    ReceiptSetDto newer = null;
                    if (_unlinkedChainReceipts.Count > i)
                        newer = _unlinkedChainReceipts[i];

                    _historyEntries.Add(new HistoryEntry
                    {
                        Kind = HistoryEntryKind.UnlinkedVersion,
                        ReceiptSetId = older.ReceiptSetId,
                        DepthFromHead = i + 1,
                        Metadata = older,
                        VersionLabel = i == 0 ? "Previous" : $"v.{_unlinkedHistoryReceipts.Count - i}",
                        SavedAt = older.ModifiedAt ?? older.CreatedAt,
                        ChangeSummary = BuildChangeSummary(newer, older)
                    });
                }
            }
        }

        private ReceiptSetDto CurrentDto()
        {
            return new ReceiptSetDto
            {
                Supplier = CmbSupplier.Text,
                SiNumber = TxtSiNumber.Text,
                DrNumber = TxtDrNumber.Text,
                PoNumber = TxtPoNumber.Text,
            };
        }

        private string BuildChangeSummary(ReceiptSetDto newer, ReceiptSetDto older)
        {
            if (older == null) return "—";
            if (newer == null) return "—";
            bool Eq(string a, string b) => string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
            bool Has(string x) => !string.IsNullOrWhiteSpace(x);
            var changes = new List<string>();
            if (!Eq(newer.Supplier, older.Supplier)) changes.Add("Supplier");
            if (!Eq(newer.SiNumber, older.SiNumber)) changes.Add("SI#");
            if (!Eq(newer.DrNumber, older.DrNumber)) changes.Add("DR#");
            if (!Eq(newer.PoNumber, older.PoNumber)) changes.Add("PO#");
            if (Has(newer.SiNumber) != Has(older.SiNumber)) changes.Add("SI img");
            if (Has(newer.DrNumber) != Has(older.DrNumber)) changes.Add("DR img");
            if (Has(newer.PoNumber) != Has(older.PoNumber)) changes.Add("PO img");
            if (changes.Count == 0) return "—";
            if (changes.Count <= 3) return string.Join(", ", changes);
            return string.Join(", ", changes.Take(3)) + $" +{changes.Count - 3}";
        }

        private void PopulateHistoryGrid()
        {
            HistoryGrid.ItemsSource = null;
            HistoryGrid.ItemsSource = _historyEntries;
            if (_historyEntries.Count > 0)
                HistoryGrid.SelectedIndex = 0;
            UpdateHistoryDetails();
        }

        private void HistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateHistoryDetails();
        }

        private void UpdateHistoryDetails()
        {
            var entry = HistoryGrid.SelectedItem as HistoryEntry;
            if (entry == null)
            {
                HistDetailVersion.Text = "—";
                HistDetailSaved.Text = "—";
                HistDetailSupplier.Text = "—";
                HistDetailDoc.Text = "—";
                HistDetailChanged.Text = "—";
                HistDetailImgs.Text = "—";
                BtnHistOpen.IsEnabled = false;
                BtnHistMakeCurrent.IsEnabled = false;
                return;
            }

            HistDetailVersion.Text = entry.VersionLabel;
            HistDetailSaved.Text = entry.SavedAtText;
            HistDetailSupplier.Text = entry.Supplier;
            HistDetailDoc.Text = entry.DocText;
            HistDetailChanged.Text = entry.ChangeSummary;
            HistDetailImgs.Text = entry.ImageIndicators;
            BtnHistOpen.IsEnabled = entry.ReceiptSetId > 0;
            BtnHistMakeCurrent.IsEnabled = entry.Kind == HistoryEntryKind.UnlinkedVersion && !_setId.HasValue;
        }

        private void HistoryGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSelectedHistoryEntry();
        }

        private void BtnHistOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedHistoryEntry();
        }

        private void OpenSelectedHistoryEntry()
        {
            var entry = HistoryGrid.SelectedItem as HistoryEntry;
            if (entry == null) return;

            try
            {
                ReceiptSetDto dto = null;
                if (entry.Kind == HistoryEntryKind.LinkedPeriod && _setId.HasValue && entry.Period != null)
                    dto = _repository.GetBySetIdCoverage(_setId.Value, entry.Period.StartDate, entry.Period.EndDate);
                else if (entry.ReceiptSetId > 0)
                    dto = _repository.GetByReceiptSetId(entry.ReceiptSetId);

                if (dto == null)
                {
                    MessageBox.Show(this, "No receipt set is saved for this history entry.",
                        "Receipt History", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var viewer = new ReceiptSetViewerWindow(dto) { Title = Title + " (Read-Only)" };
                viewer.ApplyReadOnlyState(true);
                viewer._setId = _setId;
                viewer.Owner = this;
                viewer.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to open receipt history.\n\n" + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnHistMakeCurrent_Click(object sender, RoutedEventArgs e)
        {
            var entry = HistoryGrid.SelectedItem as HistoryEntry;
            if (entry == null || entry.ReceiptSetId <= 0) return;

            var confirm = MessageBox.Show(this,
                "Make this version the current receipt set?\n\n" +
                "The current receipt will become a previous version.",
                "Make Current",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
            int headId = _unlinkedHeadReceiptSetId ?? _receiptSetId;

            try
            {
                int newHeadId = _repository.RevertUnlinkedReceiptSetToPrevious(headId, userId);
                var dto = _repository.GetByReceiptSetId(newHeadId);
                if (dto != null) LoadFromDto(dto);
                _unlinkedHeadReceiptSetId = newHeadId;
                InitializeUnlinkedRenewalTabsForReceiptSet(newHeadId);

                TabPeriodCurrent.IsChecked = true;
                MessageBox.Show(this, "Reverted to the previous receipt set.",
                    "Receipt Set", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to revert: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRevert_Click(object sender, RoutedEventArgs e)
        {
            RevertToPrevious();
        }

        private void RevertToPrevious()
        {
            if (_receiptSetId <= 0)
            {
                MessageBox.Show(this, "Please save the receipt set first.",
                    "Receipt Set", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
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

        private void RevertLinkedReceiptToPrevious()
        {
            if (!_setId.HasValue || _currentCoverage == null || _previousCoverage == null)
            {
                MessageBox.Show(this, "No previous coverage period exists to revert to.",
                    "Receipt Set", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ReceiptSetDto currentDto = null;
            ReceiptSetDto prevDto = null;
            try
            {
                currentDto = _repository.GetBySetIdCoverage(_setId.Value, _currentCoverage.StartDate, _currentCoverage.EndDate);
                prevDto = _repository.GetBySetIdCoverage(_setId.Value, _previousCoverage.StartDate, _previousCoverage.EndDate);
            }
            catch { }

            if (currentDto == null || currentDto.ReceiptSetId <= 0)
            {
                MessageBox.Show(this, "No current receipt set saved for the current coverage period.",
                    "Receipt Set", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (prevDto == null || prevDto.ReceiptSetId <= 0)
            {
                MessageBox.Show(this, "No previous receipt set saved for the previous coverage period.",
                    "Receipt Set", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var labelCurrent = $"{_currentCoverage.StartDate:yyyy}-{_currentCoverage.EndDate:yyyy}";
            var labelPrev = $"{_previousCoverage.StartDate:yyyy}-{_previousCoverage.EndDate:yyyy}";
            var confirm = MessageBox.Show(this,
                "Revert to the previous receipt set?\n\n" +
                $"This will swap receipts between coverage periods:\n" +
                $"Current: {labelCurrent}\nPrevious: {labelPrev}",
                "Revert Receipt Set",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                _repository.SwapReceiptSetCoverageLinks(
                    _setId.Value,
                    currentDto.ReceiptSetId, _currentCoverage.StartDate, _currentCoverage.EndDate,
                    prevDto.ReceiptSetId, _previousCoverage.StartDate, _previousCoverage.EndDate,
                    userId);

                InitializeCoverageTabsForSet(_setId.Value);
                TabPeriodCurrent.IsChecked = true;
                MessageBox.Show(this, "Reverted to the previous receipt set.",
                    "Receipt Set", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to revert receipt set: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RevertUnlinkedReceiptToPrevious()
        {
            int headId = _unlinkedHeadReceiptSetId ?? _receiptSetId;

            if (!_unlinkedPreviousReceiptSetId.HasValue)
            {
                MessageBox.Show(this, "No previous receipt set exists to revert to.",
                    "Receipt Set", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(this,
                "Revert to the previous receipt set?\n\n" +
                "The previous receipt will become Current, and the current receipt will move to Previous.",
                "Revert Receipt Set",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                int newHeadId = _repository.RevertUnlinkedReceiptSetToPrevious(headId, userId);

                var dto = _repository.GetByReceiptSetId(newHeadId);
                if (dto != null) LoadFromDto(dto);
                _unlinkedHeadReceiptSetId = newHeadId;
                InitializeUnlinkedRenewalTabsForReceiptSet(newHeadId);

                TabPeriodCurrent.IsChecked = true;
                MessageBox.Show(this, "Reverted to the previous receipt set.",
                    "Receipt Set", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to revert receipt set: " + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateCoverageTabState()
        {
            if (_setId.HasValue && _setId.Value > 0)
                InitializeCoverageTabsForSet(_setId.Value);
            else if (_receiptSetId > 0)
                InitializeUnlinkedRenewalTabsForReceiptSet(_receiptSetId);
        }

        private sealed class GalleryImageItem
        {
            public int ImageId { get; set; }
            public byte[] ImageBytes { get; set; }
            public string ImagePath { get; set; }
            public string MimeType { get; set; }
            public bool IsNew { get; set; }
            public bool MarkedForDeletion { get; set; }

            public bool IsPdf => string.Equals(MimeType, "application/pdf", StringComparison.OrdinalIgnoreCase);
        }
    }
}
