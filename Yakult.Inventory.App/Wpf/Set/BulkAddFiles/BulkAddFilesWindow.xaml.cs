using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Set;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.Set.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Wpf.Set.BulkAddFiles
{
    /// <summary>
    /// Batch workspace for linking new Receipt Sets and/or Set Images to Sets without opening
    /// each Set's full detail window. Add as many collapsible groups as needed — each targets its
    /// own Set and attaches independently, matching the collapsible-row style of
    /// Wpf\Renewal\RenewalGroups\Views\RenewalGroupPageView.xaml (header Border toggles a body
    /// StackPanel's visibility; no Expander control).
    /// </summary>
    public partial class BulkAddFilesWindow : Window, IDisposable
    {
        private readonly ReceiptSetRepository _receiptRepository = new ReceiptSetRepository();
        private readonly SetRepository _setRepository = new SetRepository();

        private readonly List<BulkGroupRow> _groups = new List<BulkGroupRow>();
        private readonly HashSet<string> _selectedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private List<SetRow> _allSets = new List<SetRow>();
        private int _groupCounter;
        // Null while still loading (or if the load failed) -- AttachGroupAsync skips the
        // exists-in-list check in that case rather than blocking every attach on a DB hiccup.
        private List<string> _supplierNames;

        public BulkAddFilesWindow(IEnumerable<SetRow> knownSets)
        {
            InitializeComponent();

            // Reuse whatever the caller (Sets list) already has loaded instead of re-querying the DB.
            _allSets = knownSets?.ToList() ?? new List<SetRow>();
            BuildCategoryFilterPanel();
            ApplySetFilter();
            LoadSupplierSuggestions();

            AddGroup();
        }

        private async void LoadSupplierSuggestions()
        {
            try
            {
                var repo = new VendorRepository();
                var vendors = await repo.GetAllVendorsAsync().ConfigureAwait(true);
                _supplierNames = vendors
                    .Where(v => v != null && v.IsActive && !string.IsNullOrWhiteSpace(v.VendorName))
                    .Select(v => v.VendorName.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                RenderGroups();
            }
            catch
            {
                _supplierNames = null;
            }
        }

        // ── Category filter (multiselect) ───────────────────────────────────────────

        private void BuildCategoryFilterPanel()
        {
            CategoryFilterPanel.Children.Clear();

            var categories = _allSets
                .SelectMany(s => s.Categories ?? Array.Empty<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var category in categories)
            {
                var cb = new CheckBox
                {
                    Content = category,
                    FontSize = 11.5,
                    Margin = new Thickness(0, 0, 12, 6),
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                cb.Checked += (s, e) => { _selectedCategories.Add(category); UpdateCategoryFilterButtonText(); ApplySetFilter(); };
                cb.Unchecked += (s, e) => { _selectedCategories.Remove(category); UpdateCategoryFilterButtonText(); ApplySetFilter(); };
                CategoryFilterPanel.Children.Add(cb);
            }

            UpdateCategoryFilterButtonText();
        }

        private void UpdateCategoryFilterButtonText()
        {
            BtnCategoryFilter.Content = _selectedCategories.Count > 0
                ? $"Category ({_selectedCategories.Count}) ▾"
                : "Category ▾";
        }

        private void BtnCategoryFilter_Click(object sender, RoutedEventArgs e)
        {
            CategoryFilterPopup.IsOpen = !CategoryFilterPopup.IsOpen;
        }

        // ── IDisposable (no-op for WinForms `using` callers) ─────────────────────────────
        public void Dispose() { }

        // ── WinForms ShowDialog compatibility (matches ViewSetDetailPage's own pattern) ────
        public new WinForms.DialogResult ShowDialog()
        {
            bool? r = base.ShowDialog();
            return r == true ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }

        public WinForms.DialogResult ShowDialog(WinForms.IWin32Window owner)
        {
            if (owner != null)
                new WindowInteropHelper(this).Owner = owner.Handle;
            return ShowDialog();
        }

        private sealed class StagedImage
        {
            public byte[] ImageBytes { get; set; }
            public string ImagePath { get; set; }
        }

        private enum GroupStatus { Pending, Attached, Failed }

        /// <summary>One collapsible group card's state. Built/rendered imperatively (see
        /// BuildGroupCard) rather than via a WPF DataTemplate, since the per-card doc-type tabs,
        /// file pickers, and thumbnail galleries need direct closures over this instance.</summary>
        private sealed class BulkGroupRow
        {
            public string GroupKey { get; } = Guid.NewGuid().ToString("N");
            public string Label { get; set; }
            public bool IsExpanded { get; set; } = true;
            public GroupStatus Status { get; set; } = GroupStatus.Pending;
            public string ErrorMessage { get; set; }
            public SetRow AssignedSet { get; set; }
            public string CurrentDocType { get; set; } = "SI";

            public string Supplier { get; set; } = string.Empty;
            public string SiNumber { get; set; } = string.Empty;
            public string DrNumber { get; set; } = string.Empty;
            public string PoNumber { get; set; } = string.Empty;

            public Dictionary<string, List<StagedImage>> ReceiptImagesByType { get; } =
                new Dictionary<string, List<StagedImage>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SI"] = new List<StagedImage>(),
                    ["DR"] = new List<StagedImage>(),
                    ["PO"] = new List<StagedImage>()
                };

            public List<StagedImage> SetImages { get; } = new List<StagedImage>();

            public bool HasReceiptFiles => ReceiptImagesByType.Values.Any(l => l.Count > 0);
            public bool HasSetImageFiles => SetImages.Count > 0;
        }

        // ── Set picker (right panel) ────────────────────────────────────────────────

        private void BindSetsGrid(IEnumerable<SetRow> sets) => SetsGrid.ItemsSource = sets;

        private void TxtSetSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplySetFilter();

        private void ApplySetFilter()
        {
            IEnumerable<SetRow> filtered = _allSets;

            var terms = SearchTextHelper.SplitTerms(TxtSetSearch?.Text);
            if (terms.Length > 0)
            {
                filtered = filtered.Where(s => SearchTextHelper.MatchesAllTerms(terms, new List<string>
                {
                    s.SetCode, s.SetType, s.SetStatus, s.CurrentEmployeeName, s.CurrentCompanyName,
                    s.CurrentBranchName, s.CurrentDepartmentName, s.Remarks
                }));
            }

            if (_selectedCategories.Count > 0)
            {
                filtered = filtered.Where(s => (s.Categories ?? Array.Empty<string>())
                    .Any(c => _selectedCategories.Contains(c)));
            }

            BindSetsGrid(filtered.ToList());
        }

        private void SetsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(SetsGrid.SelectedItem is SetRow row))
                return;

            using (var detail = new ViewSetDetailPage(row.SetId))
            {
                detail.ShowDialog(new WpfWin32Window(this));
            }
        }

        // ── Group management ────────────────────────────────────────────────────────

        private void BtnAddGroup_Click(object sender, RoutedEventArgs e) => AddGroup();

        private void AddGroup()
        {
            _groupCounter++;
            _groups.Add(new BulkGroupRow { Label = $"Group {_groupCounter}", IsExpanded = true });
            RenderGroups();
        }

        private void RemoveGroup(BulkGroupRow group)
        {
            _groups.Remove(group);
            RenderGroups();
        }

        private void RenderGroups()
        {
            GroupsPanel.ItemsSource = null;
            GroupsPanel.ItemsSource = _groups.Select(BuildGroupCard).ToList();
        }

        // ── Card construction ───────────────────────────────────────────────────────

        private Border BuildGroupCard(BulkGroupRow group)
        {
            var outer = new Border
            {
                Style = (Style)FindResource("CardBorder"),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ── Header (click to expand/collapse) ──
            var headerBg = group.Status == GroupStatus.Attached
                ? Color.FromRgb(0xE8, 0xF7, 0xEE)
                : group.IsExpanded ? Color.FromRgb(0xEF, 0xF6, 0xFF) : Color.FromRgb(0xF8, 0xFA, 0xFC);

            var header = new Border
            {
                Background = new SolidColorBrush(headerBg),
                CornerRadius = group.IsExpanded ? new CornerRadius(10, 10, 0, 0) : new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10),
                Cursor = Cursors.Hand
            };
            header.MouseLeftButtonUp += (s, e) =>
            {
                group.IsExpanded = !group.IsExpanded;
                RenderGroups();
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var chevron = new TextBlock
            {
                Text = group.IsExpanded ? "▾" : "▸",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x7A, 0x8D)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(chevron, 0);

            var labelBox = new TextBox
            {
                Text = group.Label,
                Style = (Style)FindResource("FieldBox"),
                VerticalAlignment = VerticalAlignment.Center
            };
            labelBox.PreviewMouseLeftButtonDown += (s, e) => e.Handled = true; // don't toggle expand when editing label
            labelBox.PreviewMouseLeftButtonUp += (s, e) => e.Handled = true;
            labelBox.LostFocus += (s, e) => group.Label = string.IsNullOrWhiteSpace(labelBox.Text) ? group.Label : labelBox.Text.Trim();
            Grid.SetColumn(labelBox, 1);

            var summary = new TextBlock
            {
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x7A, 0x8A)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0),
                Text = BuildSummaryText(group)
            };
            Grid.SetColumn(summary, 2);

            var statusBadge = new Border
            {
                Background = new SolidColorBrush(group.Status == GroupStatus.Attached
                    ? Color.FromRgb(0xD6, 0xF5, 0xE6)
                    : group.Status == GroupStatus.Failed
                        ? Color.FromRgb(0xFB, 0xE8, 0xE8)
                        : Color.FromRgb(0xEC, 0xF0, 0xF4)),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(9, 3, 9, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                Child = new TextBlock
                {
                    Text = group.Status == GroupStatus.Attached ? "✅ Attached" : group.Status == GroupStatus.Failed ? "⚠ Failed" : "Pending",
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(group.Status == GroupStatus.Attached
                        ? Color.FromRgb(0x27, 0xAE, 0x60)
                        : group.Status == GroupStatus.Failed
                            ? Color.FromRgb(0xC0, 0x39, 0x2B)
                            : Color.FromRgb(0x6A, 0x7A, 0x8A))
                }
            };
            Grid.SetColumn(statusBadge, 3);

            var deleteBtn = new Button
            {
                Content = "🗑",
                Width = 26,
                Height = 26,
                FontSize = 11,
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(0xFB, 0xE8, 0xE8)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B)),
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            ApplyFlatButtonTemplate(deleteBtn);
            deleteBtn.Click += (s, e) => RemoveGroup(group);
            Grid.SetColumn(deleteBtn, 4);

            headerGrid.Children.Add(chevron);
            headerGrid.Children.Add(labelBox);
            headerGrid.Children.Add(summary);
            headerGrid.Children.Add(statusBadge);
            headerGrid.Children.Add(deleteBtn);
            header.Child = headerGrid;
            Grid.SetRow(header, 0);

            rootGrid.Children.Add(header);

            // ── Body (only built when expanded) ──
            if (group.IsExpanded)
            {
                var body = BuildGroupBody(group);
                Grid.SetRow(body, 1);
                rootGrid.Children.Add(body);
            }

            outer.Child = rootGrid;
            return outer;
        }

        private static void ApplyFlatButtonTemplate(Button btn)
        {
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;
            btn.Template = template;
        }

        private static string BuildSummaryText(BulkGroupRow group)
        {
            var parts = new List<string>();
            int receiptCount = group.ReceiptImagesByType.Values.Sum(l => l.Count);
            if (receiptCount > 0)
                parts.Add($"{receiptCount} receipt img{(receiptCount == 1 ? "" : "s")}");
            if (group.SetImages.Count > 0)
                parts.Add($"{group.SetImages.Count} set img{(group.SetImages.Count == 1 ? "" : "s")}");
            string files = parts.Count > 0 ? string.Join(", ", parts) : "no files yet";
            string set = group.AssignedSet != null ? group.AssignedSet.SetCode : "no Set assigned";
            return $"{files} · {set}";
        }

        private Grid BuildGroupBody(BulkGroupRow group)
        {
            var body = new Grid { Margin = new Thickness(16, 14, 16, 16) };
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // fields
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // receipt doc tabs + gallery
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // set images
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // assign set + attach

            // ── Supplier/SI/DR/PO fields ──
            var fieldsGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            for (int i = 0; i < 7; i++)
                fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = i % 2 == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(12) });

            fieldsGrid.Children.Add(BuildSupplierField(group, 0));
            fieldsGrid.Children.Add(BuildLabeledField("SI #", group.SiNumber, v => group.SiNumber = v, 2));
            fieldsGrid.Children.Add(BuildLabeledField("DR #", group.DrNumber, v => group.DrNumber = v, 4));
            fieldsGrid.Children.Add(BuildLabeledField("PO #", group.PoNumber, v => group.PoNumber = v, 6));
            Grid.SetRow(fieldsGrid, 0);
            body.Children.Add(fieldsGrid);

            // ── Receipt group: doc tabs + gallery ──
            var receiptSection = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xEE)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var receiptGrid = new Grid();
            receiptGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            receiptGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var docTabRow = new Grid { Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xF9)) };
            docTabRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            docTabRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tabsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 4, 0, 0) };
            foreach (var docType in new[] { ("SI", "Sales Invoice"), ("DR", "Delivery Receipt"), ("PO", "Purchase Order") })
            {
                var rb = new RadioButton
                {
                    GroupName = $"DocTabs_{group.GroupKey}",
                    Style = (Style)FindResource("DocTab"),
                    Content = docType.Item2,
                    Tag = docType.Item1,
                    IsChecked = group.CurrentDocType == docType.Item1,
                    Margin = new Thickness(2, 0, 0, 0)
                };
                rb.Checked += (s, e) => { group.CurrentDocType = docType.Item1; RenderGroups(); };
                tabsPanel.Children.Add(rb);
            }
            Grid.SetColumn(tabsPanel, 0);

            var addReceiptBtn = new Button
            {
                Content = "＋ Add Image(s)",
                Style = (Style)FindResource("Btn"),
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x9E, 0x5E)),
                Height = 30, MinWidth = 0, Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 6, 6, 6),
                VerticalAlignment = VerticalAlignment.Center
            };
            addReceiptBtn.Click += (s, e) => PickReceiptImages(group);
            Grid.SetColumn(addReceiptBtn, 1);

            docTabRow.Children.Add(tabsPanel);
            docTabRow.Children.Add(addReceiptBtn);
            Grid.SetRow(docTabRow, 0);

            var receiptImages = group.ReceiptImagesByType.TryGetValue(group.CurrentDocType, out var rlist) ? rlist : new List<StagedImage>();
            var receiptGallery = BuildGalleryPanel(receiptImages,
                $"No {group.CurrentDocType} images yet.",
                img => { group.ReceiptImagesByType[group.CurrentDocType].Remove(img); RenderGroups(); });
            Grid.SetRow(receiptGallery, 1);

            receiptGrid.Children.Add(docTabRow);
            receiptGrid.Children.Add(receiptGallery);
            receiptSection.Child = receiptGrid;
            Grid.SetRow(receiptSection, 1);
            body.Children.Add(receiptSection);

            // ── Set Images section ──
            var setImagesSection = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xEE)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var setImagesGrid = new Grid();
            setImagesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            setImagesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var setImagesHeader = new Grid { Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xF9)) };
            setImagesHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            setImagesHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var setImagesLabel = new TextBlock
            {
                Text = "📷  Set Images",
                FontSize = 12.5, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x20, 0x30, 0x3F)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            Grid.SetColumn(setImagesLabel, 0);
            var addSetImagesBtn = new Button
            {
                Content = "＋ Add Image(s)",
                Style = (Style)FindResource("Btn"),
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x9E, 0x5E)),
                Height = 30, MinWidth = 0, Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 6, 6, 6)
            };
            addSetImagesBtn.Click += (s, e) => PickSetImages(group);
            Grid.SetColumn(addSetImagesBtn, 1);
            setImagesHeader.Children.Add(setImagesLabel);
            setImagesHeader.Children.Add(addSetImagesBtn);
            Grid.SetRow(setImagesHeader, 0);

            var setImagesGallery = BuildGalleryPanel(group.SetImages, "No set images yet.",
                img => { group.SetImages.Remove(img); RenderGroups(); });
            Grid.SetRow(setImagesGallery, 1);

            setImagesGrid.Children.Add(setImagesHeader);
            setImagesGrid.Children.Add(setImagesGallery);
            setImagesSection.Child = setImagesGrid;
            Grid.SetRow(setImagesSection, 2);
            body.Children.Add(setImagesSection);

            // ── Assign Set + Attach ──
            var actionRow = new Grid();
            actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var useSetBtn = new Button
            {
                Content = "🔗  Use Selected Set",
                Style = (Style)FindResource("SecondaryBtn"),
                Height = 32
            };
            useSetBtn.Click += (s, e) =>
            {
                if (SetsGrid.SelectedItem is SetRow selected)
                {
                    group.AssignedSet = selected;
                    RenderGroups();
                }
                else
                {
                    MessageBox.Show(this, "Select a Set in the right-hand list first.", "No Set Selected",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            Grid.SetColumn(useSetBtn, 0);

            var assignedText = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x7A, 0x8A)),
                Text = group.AssignedSet != null ? $"Target: {group.AssignedSet.SetCode}" : "No Set assigned yet."
            };
            Grid.SetColumn(assignedText, 1);

            var attachBtn = new Button
            {
                Content = group.Status == GroupStatus.Attached ? "✅  Attached" : "✅  Attach This Group",
                Style = (Style)FindResource("Btn"),
                Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x8E, 0xF6)),
                MinWidth = 190,
                IsEnabled = group.Status != GroupStatus.Attached && group.AssignedSet != null && (group.HasReceiptFiles || group.HasSetImageFiles)
            };
            attachBtn.Click += async (s, e) => await AttachGroupAsync(group);
            Grid.SetColumn(attachBtn, 2);

            actionRow.Children.Add(useSetBtn);
            actionRow.Children.Add(assignedText);
            actionRow.Children.Add(attachBtn);
            Grid.SetRow(actionRow, 3);
            body.Children.Add(actionRow);

            if (!string.IsNullOrWhiteSpace(group.ErrorMessage))
            {
                var errText = new TextBlock
                {
                    Text = group.ErrorMessage,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B)),
                    FontSize = 11.5,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                Grid.SetRow(errText, 3);
                body.Children.Add(errText);
            }

            return body;
        }

        private FrameworkElement BuildLabeledField(string label, string value, Action<string> onChanged, int column)
        {
            var stack = new StackPanel();
            Grid.SetColumn(stack, column);
            stack.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("FieldLabel") });
            var box = new TextBox { Text = value, Style = (Style)FindResource("FieldBox") };
            box.LostFocus += (s, e) => onChanged(box.Text?.Trim() ?? string.Empty);
            stack.Children.Add(box);
            return stack;
        }

        private FrameworkElement BuildSupplierField(BulkGroupRow group, int column)
        {
            var stack = new StackPanel();
            Grid.SetColumn(stack, column);
            stack.Children.Add(new TextBlock { Text = "SUPPLIER *", Style = (Style)FindResource("FieldLabel") });

            var combo = new ComboBox
            {
                IsEditable = true,
                Text = group.Supplier,
                ItemsSource = _supplierNames
            };
            combo.LostFocus += (s, e) => group.Supplier = combo.Text?.Trim() ?? string.Empty;
            stack.Children.Add(combo);
            return stack;
        }

        private Border BuildGalleryPanel(List<StagedImage> images, string emptyMessage, Action<StagedImage> onDelete)
        {
            var panel = new Border
            {
                Margin = new Thickness(10),
                MinHeight = 110,
                MaxHeight = 260,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0xDC, 0xE4)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(0xFA, 0xFB, 0xFD))
            };

            if (images.Count == 0)
            {
                panel.Child = new TextBlock
                {
                    Text = emptyMessage,
                    FontSize = 11.5,
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA6, 0xB4)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                return panel;
            }

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(6) };
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6) };
            for (int i = 0; i < images.Count; i++)
                wrap.Children.Add(BuildImageCard(images[i], i, onDelete));
            scroll.Content = wrap;
            panel.Child = scroll;
            return panel;
        }

        // ── File pickers ────────────────────────────────────────────────────────────

        private void PickReceiptImages(BulkGroupRow group)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select receipt image(s)",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff|All Files|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog(this) != true)
                return;

            foreach (var path in dialog.FileNames)
            {
                try
                {
                    group.ReceiptImagesByType[group.CurrentDocType].Add(new StagedImage { ImageBytes = File.ReadAllBytes(path), ImagePath = path });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to add {Path.GetFileName(path)}:\n{ex.Message}", "Add Image",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            RenderGroups();
        }

        private void PickSetImages(BulkGroupRow group)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select set image(s)",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff|All Files|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog(this) != true)
                return;

            foreach (var path in dialog.FileNames)
            {
                try
                {
                    group.SetImages.Add(new StagedImage { ImageBytes = File.ReadAllBytes(path), ImagePath = path });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to add {Path.GetFileName(path)}:\n{ex.Message}", "Add Image",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            RenderGroups();
        }

        // ── Thumbnail card (shared by receipt + set-image galleries) ──────────────────

        private static Border BuildImageCard(StagedImage item, int index, Action<StagedImage> onDelete)
        {
            var card = new Border
            {
                Width = 116,
                Height = 138,
                Margin = new Thickness(0, 0, 10, 10),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE7, 0xEE)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var imgHost = new Border
            {
                Margin = new Thickness(6),
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xF9)),
                ClipToBounds = true
            };
            var img = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(3) };
            img.Source = TryLoadThumbnail(item);
            imgHost.Child = img;
            Grid.SetRow(imgHost, 0);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            footer.Children.Add(new TextBlock
            {
                Text = $"#{index + 1}",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x7A, 0x8A)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            var btnDelete = new Button
            {
                Content = "🗑",
                Width = 24,
                Height = 24,
                FontSize = 10.5,
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(0xFB, 0xE8, 0xE8)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B)),
                BorderThickness = new Thickness(0)
            };
            ApplyFlatButtonTemplate(btnDelete);
            btnDelete.Click += (s, e) => onDelete(item);
            footer.Children.Add(btnDelete);
            Grid.SetRow(footer, 1);

            grid.Children.Add(imgHost);
            grid.Children.Add(footer);
            card.Child = grid;
            return card;
        }

        private static BitmapImage TryLoadThumbnail(StagedImage item)
        {
            try
            {
                if (item.ImageBytes == null || item.ImageBytes.Length == 0)
                    return null;

                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(item.ImageBytes))
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

        // ── Attach ───────────────────────────────────────────────────────────────────

        private async Task AttachGroupAsync(BulkGroupRow group)
        {
            if (group.AssignedSet == null)
                return;

            if (!group.HasReceiptFiles && !group.HasSetImageFiles)
                return;

            if (group.HasReceiptFiles && string.IsNullOrWhiteSpace(group.Supplier))
            {
                MessageBox.Show(this, "Supplier is required when adding receipt images.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Only enforce "must match an existing Vendor" when the list actually loaded --
            // _supplierNames is null if LoadSupplierSuggestions is still running or failed.
            if (group.HasReceiptFiles && _supplierNames != null &&
                !_supplierNames.Any(n => string.Equals(n, group.Supplier.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "Supplier must be selected from the existing vendor list. Start typing to search, then pick a match.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;

                if (group.HasReceiptFiles)
                {
                    var dto = new ReceiptSetDto
                    {
                        ReceiptSetId = 0,
                        Supplier = group.Supplier.Trim(),
                        SiNumber = string.IsNullOrWhiteSpace(group.SiNumber) ? null : group.SiNumber.Trim(),
                        DrNumber = string.IsNullOrWhiteSpace(group.DrNumber) ? null : group.DrNumber.Trim(),
                        PoNumber = string.IsNullOrWhiteSpace(group.PoNumber) ? null : group.PoNumber.Trim(),
                        SiImage = group.ReceiptImagesByType["SI"].FirstOrDefault()?.ImageBytes,
                        SiImagePath = group.ReceiptImagesByType["SI"].FirstOrDefault()?.ImagePath,
                        DrImage = group.ReceiptImagesByType["DR"].FirstOrDefault()?.ImageBytes,
                        DrImagePath = group.ReceiptImagesByType["DR"].FirstOrDefault()?.ImagePath,
                        PoImage = group.ReceiptImagesByType["PO"].FirstOrDefault()?.ImageBytes,
                        PoImagePath = group.ReceiptImagesByType["PO"].FirstOrDefault()?.ImagePath
                    };

                    int receiptSetId = await Task.Run(() => _receiptRepository.Save(dto, userId));

                    foreach (var docType in new[] { "SI", "DR", "PO" })
                    {
                        foreach (var img in group.ReceiptImagesByType[docType])
                            await Task.Run(() => _receiptRepository.AddImage(receiptSetId, docType, img.ImageBytes, img.ImagePath, userId));
                    }

                    await Task.Run(() => _receiptRepository.AttachReceiptSetToSet(receiptSetId, group.AssignedSet.SetId, userId));
                }

                if (group.HasSetImageFiles)
                {
                    foreach (var img in group.SetImages)
                        await _setRepository.AddSetImageFromFileAsync(group.AssignedSet.SetId, img.ImagePath, "Photo", AppSession.CurrentUserName);
                }

                group.Status = GroupStatus.Attached;
                group.ErrorMessage = null;
                group.IsExpanded = false;
            }
            catch (Exception ex)
            {
                group.Status = GroupStatus.Failed;
                group.ErrorMessage = ex.Message;
            }
            finally
            {
                RenderGroups();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
