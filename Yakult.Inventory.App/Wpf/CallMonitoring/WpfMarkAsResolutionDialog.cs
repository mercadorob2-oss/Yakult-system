using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed class WpfMarkAsResolutionDialog : Window
    {
        private const string SpareRepairAction = "Repaired - Spare inventory";

        private readonly List<ItemLookupDto> _outHardwareItems;
        private readonly List<ItemLookupDto> _stockHardwareItems;
        private readonly List<ItemCategoryDto> _categories;
        private readonly List<ConditionDto> _conditions;

        private readonly RadioButton _serviceOnlyRadio;
        private readonly RadioButton _replacementRadio;
        private readonly RadioButton _oldListedRadio;
        private readonly RadioButton _oldUnlistedRadio;
        private readonly RadioButton _oldActionRepairedRadio;
        private readonly RadioButton _oldActionUnrepairedRadio;
        private readonly RadioButton _oldActionSpareRadio;
        private readonly ComboBox _oldCategoryFilter;
        private readonly ComboBox _oldItemCombo;
        private readonly ComboBox _oldCategoryCombo;
        private readonly TextBox _oldNameBox;
        private readonly TextBox _oldModelBox;
        private readonly TextBox _oldSerialBox;
        private readonly ComboBox _oldUnitCombo;
        private readonly TextBox _oldDescriptionBox;
        private readonly ComboBox _oldConditionCombo;
        private readonly TextBox _conditionRemarksBox;
        private readonly ComboBox _newCategoryFilter;
        private readonly ComboBox _newItemCombo;
        private readonly TextBlock _newStockHint;
        private readonly TextBox _qtyBox;
        private readonly CheckBox _temporaryCheck;
        private readonly CheckBox _temporaryServiceCheck;
        private readonly TextBlock _typeHelpText;
        private readonly TextBlock _remarksTitle;
        private readonly TextBox _remarksBox;
        private readonly TextBox _previewBox;
        private readonly Button _backButton;
        private readonly Button _nextButton;
        private readonly Button _confirmButton;
        private readonly TextBlock _dialogTitle;
        private readonly TextBlock _dialogSubtitle;
        private readonly TextBlock _stepTitle;
        private readonly Border _statusChip;
        private readonly TextBlock _statusChipText;
        private readonly TextBlock _validationText;
        private readonly FrameworkElement _typeStep;
        private readonly FrameworkElement _replacementStep;
        private readonly FrameworkElement _previewStep;
        private readonly FrameworkElement _remarksSection;

        private int _wizardStep;
        private bool _oldActionTouched;
        private bool _suppressOldActionTouched;

        public string ResolutionType { get; private set; }
        public int? OldItemId { get; private set; }
        public int? NewItemId { get; private set; }
        public int Quantity { get; private set; } = 1;
        public string Remarks { get; private set; }
        public bool IsTemporaryReplacement { get; private set; }
        public bool UseUnlistedOldItem { get; private set; }
        public string UnlistedOldItemName { get; private set; }
        public string UnlistedOldItemDescription { get; private set; }
        public int UnlistedOldItemCategoryId { get; private set; }
        public string UnlistedOldItemCategoryName { get; private set; }
        public string UnlistedOldItemSerialNumber { get; private set; }
        public string UnlistedOldItemModelNumber { get; private set; }
        public string UnlistedOldItemUnitOfMeasure { get; private set; }
        public int OldItemConditionId { get; private set; }
        public string OldItemConditionRemarks { get; private set; }
        public string OldItemRepairAction { get; private set; }

        public WpfMarkAsResolutionDialog(
            List<ItemLookupDto> outHardwareItems,
            List<ItemLookupDto> stockHardwareItems,
            List<ItemCategoryDto> categories,
            IList<ConditionDto> conditions)
        {
            _outHardwareItems = (outHardwareItems ?? new List<ItemLookupDto>()).Where(x => x != null).OrderBy(x => x.DisplayText).ToList();
            _stockHardwareItems = (stockHardwareItems ?? new List<ItemLookupDto>()).Where(x => x != null).OrderBy(x => x.DisplayText).ToList();
            _categories = (categories ?? new List<ItemCategoryDto>()).Where(x => x != null).OrderBy(x => x.Name).ToList();
            _conditions = (conditions ?? new List<ConditionDto>()).Where(x => x != null).OrderBy(x => x.ConditionId).ToList();

            Title = "Mark Ticket Resolution";
            Width = 980;
            Height = 760;
            MinWidth = 820;
            MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            Background = BrushFromRgb(245, 247, 250);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, BrushFromRgb(30, 41, 59));
            Content = root;

            var headerBorder = new Border
            {
                Background = Brushes.White,
                Padding = new Thickness(22, 16, 22, 12)
            };
            Grid.SetRow(headerBorder, 0);
            root.Children.Add(headerBorder);

            var header = new StackPanel();
            headerBorder.Child = header;

            _dialogTitle = new TextBlock
            {
                Text = "Resolution Details",
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42)
            };
            header.Children.Add(_dialogTitle);
            _dialogSubtitle = new TextBlock
            {
                Text = "Capture the resolution details before changing the ticket status.",
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = BrushFromRgb(71, 85, 105)
            };
            header.Children.Add(_dialogSubtitle);
            _stepTitle = new TextBlock
            {
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105)
            };
            header.Children.Add(_stepTitle);

            var footer = new DockPanel
            {
                Height = 62,
                Background = Brushes.White,
                LastChildFill = false,
                Margin = new Thickness(0)
            };
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 10, 18, 10)
            };
            DockPanel.SetDock(buttonRow, Dock.Right);
            footer.Children.Add(buttonRow);

            _validationText = new TextBlock
            {
                Margin = new Thickness(22, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(185, 28, 28),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            DockPanel.SetDock(_validationText, Dock.Left);
            footer.Children.Add(_validationText);

            _backButton = CreateButton("Back", BrushFromRgb(100, 116, 139));
            _backButton.Click += (_, __) => GoBack();
            var cancelButton = CreateButton("Cancel", BrushFromRgb(71, 85, 105));
            cancelButton.IsCancel = true;
            cancelButton.Click += (_, __) => DialogResult = false;
            _nextButton = CreateButton("Next", BrushFromRgb(37, 99, 235));
            _nextButton.Click += (_, __) => GoNext();
            _confirmButton = CreateButton("Confirm", BrushFromRgb(22, 163, 74));
            _confirmButton.Click += (_, __) => Confirm();

            buttonRow.Children.Add(_backButton);
            buttonRow.Children.Add(cancelButton);
            buttonRow.Children.Add(_nextButton);
            buttonRow.Children.Add(_confirmButton);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var body = new StackPanel
            {
                Margin = new Thickness(22),
                Orientation = Orientation.Vertical
            };
            scroll.Content = body;

            var typeCard = CreateCard();
            _typeStep = typeCard;
            typeCard.Children.Add(CreateSectionTitle("Resolution type"));
            var typeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            _serviceOnlyRadio = new RadioButton { Content = "Service Only (No Parts)", GroupName = "ResolutionType", IsChecked = true, Margin = new Thickness(0, 0, 28, 0), FontWeight = FontWeights.SemiBold };
            _replacementRadio = new RadioButton { Content = "Replacement (Parts Used)", GroupName = "ResolutionType", FontWeight = FontWeights.SemiBold };
            _serviceOnlyRadio.Checked += (_, __) => UpdateWizardStep();
            _replacementRadio.Checked += (_, __) => UpdateWizardStep();
            typeRow.Children.Add(_serviceOnlyRadio);
            typeRow.Children.Add(_replacementRadio);
            typeCard.Children.Add(typeRow);
            _typeHelpText = new TextBlock
            {
                Margin = new Thickness(0, 12, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = BrushFromRgb(71, 85, 105),
                FontSize = 12.5
            };
            typeCard.Children.Add(_typeHelpText);
            _temporaryServiceCheck = new CheckBox
            {
                Content = "Temporary service (mark as Resolved (Temporary))",
                Margin = new Thickness(0, 10, 0, 0),
                FontWeight = FontWeights.SemiBold
            };
            _temporaryServiceCheck.Checked += (_, __) => UpdateWizardStep();
            _temporaryServiceCheck.Unchecked += (_, __) => UpdateWizardStep();
            typeCard.Children.Add(_temporaryServiceCheck);
            body.Children.Add(typeCard);

            var replacementCard = CreateCard();
            _replacementStep = replacementCard;
            replacementCard.Children.Add(CreateSectionTitle("Old item / new item selection"));

            var oldModeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 12) };
            _oldListedRadio = new RadioButton { Content = "Listed in Inventory", GroupName = "OldItemMode", IsChecked = true, Margin = new Thickness(0, 0, 22, 0) };
            _oldUnlistedRadio = new RadioButton { Content = "Not Listed", GroupName = "OldItemMode" };
            _oldListedRadio.Checked += (_, __) => UpdateOldItemMode();
            _oldUnlistedRadio.Checked += (_, __) => UpdateOldItemMode();
            oldModeRow.Children.Add(_oldListedRadio);
            oldModeRow.Children.Add(_oldUnlistedRadio);
            replacementCard.Children.Add(oldModeRow);

            var oldListedGrid = CreateTwoColumnGrid();
            oldListedGrid.Tag = "Listed";
            _oldCategoryFilter = CreateCombo();
            _oldItemCombo = CreateItemCombo(showStock: false);
            AddField(oldListedGrid, 0, 0, "Old Category", _oldCategoryFilter);
            AddField(oldListedGrid, 0, 1, "Old Item", _oldItemCombo);
            replacementCard.Children.Add(oldListedGrid);

            var oldUnlistedGrid = CreateTwoColumnGrid();
            _oldCategoryCombo = CreateCombo();
            _oldNameBox = CreateTextBox();
            _oldModelBox = CreateTextBox();
            _oldSerialBox = CreateTextBox();
            _oldUnitCombo = CreateCombo();
            _oldDescriptionBox = CreateTextBox();
            AddField(oldUnlistedGrid, 0, 0, "Old Name", _oldNameBox);
            AddField(oldUnlistedGrid, 0, 1, "Old Category", _oldCategoryCombo);
            AddField(oldUnlistedGrid, 1, 0, "Old Model", _oldModelBox);
            AddField(oldUnlistedGrid, 1, 1, "Serial", _oldSerialBox);
            AddField(oldUnlistedGrid, 2, 0, "Unit", _oldUnitCombo);
            AddField(oldUnlistedGrid, 2, 1, "Description", _oldDescriptionBox);
            oldUnlistedGrid.Tag = "Unlisted";
            replacementCard.Children.Add(oldUnlistedGrid);

            var conditionGrid = CreateTwoColumnGrid();
            _oldConditionCombo = CreateCombo();
            _conditionRemarksBox = CreateTextBox();
            AddField(conditionGrid, 0, 0, "Old Condition", _oldConditionCombo);
            AddField(conditionGrid, 0, 1, "Condition Remarks", _conditionRemarksBox);
            replacementCard.Children.Add(conditionGrid);

            var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 12) };
            actionRow.Children.Add(new TextBlock { Text = "Old Item Action", Width = 130, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(71, 85, 105) });
            _oldActionRepairedRadio = new RadioButton { Content = "Repaired", GroupName = "OldItemAction", IsChecked = true, Margin = new Thickness(0, 0, 22, 0) };
            _oldActionUnrepairedRadio = new RadioButton { Content = "Unrepaired", GroupName = "OldItemAction", Margin = new Thickness(0, 0, 22, 0) };
            _oldActionSpareRadio = new RadioButton { Content = "Repaired - Spare inventory", GroupName = "OldItemAction" };
            _oldActionRepairedRadio.Checked += (_, __) => { TouchOldAction(); RefreshPreviewIfNeeded(); };
            _oldActionUnrepairedRadio.Checked += (_, __) => { TouchOldAction(); RefreshPreviewIfNeeded(); };
            _oldActionSpareRadio.Checked += (_, __) => { TouchOldAction(); RefreshPreviewIfNeeded(); };
            actionRow.Children.Add(_oldActionRepairedRadio);
            actionRow.Children.Add(_oldActionUnrepairedRadio);
            actionRow.Children.Add(_oldActionSpareRadio);
            replacementCard.Children.Add(actionRow);

            var newGrid = CreateTwoColumnGrid();
            _newCategoryFilter = CreateCombo();
            _newItemCombo = CreateItemCombo(showStock: true);
            _qtyBox = CreateTextBox();
            _qtyBox.Text = "1";
            HookNumericQuantityInput(_qtyBox);
            _qtyBox.LostFocus += (_, __) => ClampQuantityToSelectedStock();
            _newStockHint = new TextBlock { Margin = new Thickness(0, 4, 0, 0), Foreground = BrushFromRgb(100, 116, 139) };
            var newItemStack = new StackPanel();
            newItemStack.Children.Add(_newItemCombo);
            newItemStack.Children.Add(_newStockHint);
            AddField(newGrid, 0, 0, "New Category", _newCategoryFilter);
            AddField(newGrid, 0, 1, "Replacement Item", newItemStack);
            AddField(newGrid, 1, 0, "Quantity", _qtyBox);
            _temporaryCheck = new CheckBox { Content = "Temporary replacement", Margin = new Thickness(0, 27, 0, 0) };
            AddField(newGrid, 1, 1, "Follow-up", _temporaryCheck);
            replacementCard.Children.Add(newGrid);
            body.Children.Add(replacementCard);

            var remarksCard = CreateCard();
            _remarksSection = remarksCard;
            _remarksTitle = CreateSectionTitle("Service notes");
            remarksCard.Children.Add(_remarksTitle);
            _remarksBox = CreateTextBox();
            _remarksBox.MinHeight = 130;
            _remarksBox.AcceptsReturn = true;
            _remarksBox.TextWrapping = TextWrapping.Wrap;
            _remarksBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            remarksCard.Children.Add(_remarksBox);
            body.Children.Add(remarksCard);

            var previewCard = CreateCard();
            _previewStep = previewCard;
            var previewHeader = new DockPanel { LastChildFill = true };
            _statusChipText = new TextBlock
            {
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            _statusChip = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4),
                Background = BrushFromRgb(22, 163, 74),
                Child = _statusChipText,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_statusChip, Dock.Right);
            previewHeader.Children.Add(_statusChip);
            previewHeader.Children.Add(CreateSectionTitle("Confirmation and stock impact preview"));
            previewCard.Children.Add(previewHeader);
            _previewBox = CreateTextBox();
            _previewBox.MinHeight = 260;
            _previewBox.AcceptsReturn = true;
            _previewBox.TextWrapping = TextWrapping.Wrap;
            _previewBox.IsReadOnly = true;
            previewCard.Children.Add(_previewBox);
            body.Children.Add(previewCard);

            PopulateLookups();
            HookPreviewRefresh();
            ApplyAvailabilityGuards();
            UpdateOldItemMode();
            UpdateWizardStep();
            Loaded += (_, __) =>
            {
                _wizardStep = 0;
                UpdateOldItemMode();
                UpdateWizardStep();
                _remarksBox.Focus();
            };
        }

        private static bool IsConsumableCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return false;
            var n = category.Trim();
            return n.Equals("Cartridge", StringComparison.OrdinalIgnoreCase)
                || n.Equals("Ink", StringComparison.OrdinalIgnoreCase)
                || n.Equals("Toner", StringComparison.OrdinalIgnoreCase)
                || n.Equals("Printhead", StringComparison.OrdinalIgnoreCase);
        }

        private void PopulateLookups()
        {
            PopulateCategoryFilter(_oldCategoryFilter, _outHardwareItems);
            // New category — show only categories with loose stock, with counts (fastest: in-memory groupby, no extra DB)
            var stockForCategory = _stockHardwareItems.Where(x => !IsConsumableCategory(x.Category)).ToList();
            var counts = stockForCategory.GroupBy(x => (x.Category ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            if (stockForCategory.Count == 0)
            {
                _newCategoryFilter.ItemsSource = new[] { "No stock available (0)" };
                _newCategoryFilter.SelectedIndex = 0;
                _newCategoryFilter.IsEnabled = false;
                _newItemCombo.IsEnabled = false;
                _qtyBox.IsEnabled = false;
                _newStockHint.Text = "No loose stock — add stock or choose Service Only.";
                _newStockHint.Foreground = BrushFromRgb(185, 28, 28);
            }
            else
            {
                var cats = counts.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(c => $"{c} ({counts[c]})").ToList();
                var allCount = stockForCategory.Count;
                cats.Insert(0, $"All Categories ({allCount})");
                _newCategoryFilter.ItemsSource = cats;
                _newCategoryFilter.SelectedIndex = 0;
                _newCategoryFilter.IsEnabled = true;
                _newItemCombo.IsEnabled = true;
                _qtyBox.IsEnabled = true;
                // Keep raw category value for filtering via Tag
                _newCategoryFilter.Tag = counts;
            }
            _oldCategoryFilter.SelectionChanged += (_, __) => ApplyOldFilter();
            _newCategoryFilter.SelectionChanged += (_, __) => ApplyNewFilter();

            _oldCategoryCombo.ItemsSource = _categories;
            _oldCategoryCombo.DisplayMemberPath = "Name";
            _oldCategoryCombo.SelectedValuePath = "CategoryId";
            if (_oldCategoryCombo.Items.Count > 0) _oldCategoryCombo.SelectedIndex = 0;

            _oldUnitCombo.ItemsSource = new[] { "Unit", "Piece", "Set", "Box" };
            _oldUnitCombo.SelectedIndex = 0;

            _oldConditionCombo.ItemsSource = _conditions;
            _oldConditionCombo.DisplayMemberPath = "ConditionName";
            _oldConditionCombo.SelectedValuePath = "ConditionId";
            if (_oldConditionCombo.Items.Count > 0) _oldConditionCombo.SelectedIndex = 0;
            _oldConditionCombo.SelectionChanged += (_, __) => AutoSelectOldActionFromCondition();

            ApplyOldFilter();
            ApplyNewFilter();
        }

        private void ApplyAvailabilityGuards()
        {
            // Keep Replacement selectable even when stock is low — show a warning instead of forcing Service Only.
            // This matches the repair portal pattern where a section is hidden, not disabled, when empty.
            if (_stockHardwareItems.Count == 0)
            {
                _typeHelpText.Text = "No replacement stock items are currently available — Service Only is recommended. You can still choose Replacement if you will add stock first.";
            }

            if (_categories.Count == 0)
            {
                _oldUnlistedRadio.IsEnabled = false;
                _oldListedRadio.IsChecked = true;
            }
        }

        private void HookPreviewRefresh()
        {
            _oldCategoryFilter.SelectionChanged += (_, __) => RefreshValidationState();
            _oldItemCombo.SelectionChanged += (_, __) => RefreshValidationState();
            _oldCategoryCombo.SelectionChanged += (_, __) => RefreshValidationState();
            _oldNameBox.TextChanged += (_, __) => RefreshValidationState();
            _oldModelBox.TextChanged += (_, __) => RefreshValidationState();
            _oldUnitCombo.SelectionChanged += (_, __) => RefreshValidationState();
            _oldConditionCombo.SelectionChanged += (_, __) => RefreshValidationState();
            _newCategoryFilter.SelectionChanged += (_, __) => RefreshValidationState();
            _newItemCombo.SelectionChanged += (_, __) => { UpdateNewItemStockHint(); RefreshPreviewIfNeeded(); RefreshValidationState(); };
            _qtyBox.TextChanged += (_, __) => { RefreshPreviewIfNeeded(); RefreshValidationState(); };
            _temporaryCheck.Checked += (_, __) => { RefreshPreviewIfNeeded(); RefreshValidationState(); };
            _temporaryCheck.Unchecked += (_, __) => { RefreshPreviewIfNeeded(); RefreshValidationState(); };
            _remarksBox.TextChanged += (_, __) => { RefreshPreviewIfNeeded(); RefreshValidationState(); };
            _conditionRemarksBox.TextChanged += (_, __) => { RefreshPreviewIfNeeded(); RefreshValidationState(); };
        }

        private void PopulateCategoryFilter(ComboBox combo, IEnumerable<ItemLookupDto> items)
        {
            var categories = (items ?? Enumerable.Empty<ItemLookupDto>())
                .Where(x => !string.IsNullOrWhiteSpace(x.Category))
                .Select(x => x.Category.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            categories.Insert(0, "All Categories");
            combo.ItemsSource = categories;
            combo.SelectedIndex = 0;
        }

        private void ApplyOldFilter()
        {
            var previousId = (_oldItemCombo.SelectedItem as ItemLookupDto)?.ItemId;
            var selectedCategory = _oldCategoryFilter.SelectedItem as string;
            var rows = FilterByCategory(_outHardwareItems, selectedCategory).ToList();
            _oldItemCombo.ItemsSource = rows;
            _oldItemCombo.SelectedValuePath = "ItemId";
            SelectItemByIdOrFirst(_oldItemCombo, rows, previousId);
        }

        private static string ParseCategoryDisplay(string display)
        {
            if (string.IsNullOrWhiteSpace(display)) return display;
            var t = display.Trim();
            if (t.StartsWith("No stock available", StringComparison.OrdinalIgnoreCase)) return t;
            if (t.StartsWith("All Categories", StringComparison.OrdinalIgnoreCase)) return "All Categories";
            var idx = t.LastIndexOf(" (", StringComparison.Ordinal);
            if (idx > 0 && t.EndsWith(")", StringComparison.Ordinal))
            {
                var inner = t.Substring(idx + 2, t.Length - idx - 3).Trim();
                if (inner.Length > 0 && inner.All(c => char.IsDigit(c) || c == ',' || c == ' '))
                    return t.Substring(0, idx).Trim();
            }
            return t;
        }

        private void ApplyNewFilter()
        {
            var previousId = (_newItemCombo.SelectedItem as ItemLookupDto)?.ItemId;
            var rawDisplay = _newCategoryFilter.SelectedItem as string;
            var selectedCategory = ParseCategoryDisplay(rawDisplay);
            // Handle "No stock available" hint (covers "No stock available (0)")
            if (selectedCategory != null && selectedCategory.StartsWith("No stock available", StringComparison.OrdinalIgnoreCase))
            {
                _newItemCombo.ItemsSource = new List<ItemLookupDto>();
                _newItemCombo.SelectedIndex = -1;
                _newStockHint.Text = "No loose stock — add stock or choose Service Only. Old = deployed/in-use (e.g., A4Tech); New = loose stock only.";
                _newStockHint.Foreground = BrushFromRgb(185, 28, 28);
                return;
            }
            var rows = FilterByCategory(_stockHardwareItems, selectedCategory).ToList();
            _newItemCombo.ItemsSource = rows;
            _newItemCombo.SelectedValuePath = "ItemId";
            SelectItemByIdOrFirst(_newItemCombo, rows, previousId);
            UpdateNewItemStockHint();
            // Show category-specific empty hint (use raw name without count)
            if (rows.Count == 0 && !string.IsNullOrWhiteSpace(selectedCategory) && !selectedCategory.Equals("All Categories", StringComparison.OrdinalIgnoreCase))
            {
                var total = _stockHardwareItems.Count;
                var suffix = total > 0 ? $" — try All Categories ({total}) or add stock." : " — add stock or choose Service Only.";
                _newStockHint.Text = $"No {selectedCategory} loose stock (all deployed or no loose units){suffix}";
                _newStockHint.Foreground = BrushFromRgb(185, 28, 28);
            }
        }

        private IEnumerable<ItemLookupDto> FilterByCategory(IEnumerable<ItemLookupDto> source, string category)
        {
            var rows = source ?? Enumerable.Empty<ItemLookupDto>();
            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All Categories", StringComparison.OrdinalIgnoreCase))
                rows = rows.Where(x => string.Equals((x.Category ?? string.Empty).Trim(), category.Trim(), StringComparison.OrdinalIgnoreCase));
            return rows.OrderBy(x => x.DisplayText, StringComparer.OrdinalIgnoreCase);
        }

        private void UpdateOldItemMode()
        {
            var useUnlisted = _oldUnlistedRadio.IsChecked == true;
            foreach (var child in ((Panel)_replacementStep).Children.OfType<Grid>().Where(x => Equals(x.Tag, "Listed")))
                child.Visibility = useUnlisted ? Visibility.Collapsed : Visibility.Visible;
            foreach (var child in ((Panel)_replacementStep).Children.OfType<Grid>().Where(x => Equals(x.Tag, "Unlisted")))
                child.Visibility = useUnlisted ? Visibility.Visible : Visibility.Collapsed;
            RefreshValidationState();
        }

        private void AutoSelectOldActionFromCondition()
        {
            if (_oldActionTouched)
                return;

            var condition = _oldConditionCombo.SelectedItem as ConditionDto;
            var conditionName = (condition?.ConditionName ?? string.Empty).Trim();
            _suppressOldActionTouched = true;
            try
            {
                if (conditionName.Equals("Damaged", StringComparison.OrdinalIgnoreCase))
                    _oldActionUnrepairedRadio.IsChecked = true;
                else
                    _oldActionRepairedRadio.IsChecked = true;
            }
            finally
            {
                _suppressOldActionTouched = false;
            }
        }

        private void TouchOldAction()
        {
            if (!_suppressOldActionTouched)
                _oldActionTouched = true;
        }

        private void UpdateNewItemStockHint()
        {
            var item = _newItemCombo.SelectedItem as ItemLookupDto;
            _newStockHint.Text = item == null ? string.Empty : "Available stock: " + item.StockOnHand;
            ClampQuantityToSelectedStock();
        }

        private void GoNext()
        {
            if (_wizardStep == 0)
            {
                _wizardStep = _replacementRadio.IsChecked == true ? 1 : 2;
                if (_wizardStep == 2)
                    CaptureServiceOnly();
                UpdateWizardStep();
                return;
            }

            if (_wizardStep == 1)
            {
                if (!ValidateAndCaptureReplacement(out var message))
                {
                    ShowValidation(message);
                    return;
                }
                _wizardStep = 2;
                UpdateWizardStep();
            }
        }

        private void GoBack()
        {
            if (_wizardStep <= 0)
                return;
            _wizardStep = _wizardStep == 2 && _serviceOnlyRadio.IsChecked == true ? 0 : _wizardStep - 1;
            UpdateWizardStep();
        }

        private void UpdateWizardStep()
        {
            var isReplacement = _replacementRadio.IsChecked == true;
            var isServiceOnly = !isReplacement;
            var isTemporaryService = isServiceOnly && _temporaryServiceCheck != null && _temporaryServiceCheck.IsChecked == true;

            var targetStatus = (isReplacement && _temporaryCheck.IsChecked == true) || isTemporaryService ? "Resolved (Temporary)" : "Solved";
            _dialogTitle.Text = isReplacement ? "Mark as Replacement Resolution" : (isTemporaryService ? "Mark as Resolved (Temporary)" : "Mark as Solved");
            _dialogSubtitle.Text = isReplacement
                ? "Review the replaced item, issued item, inventory impact, and required remarks before changing the ticket status."
                : "Record the service notes and confirm that no inventory item was issued or replaced.";
            _statusChipText.Text = targetStatus;
            _statusChip.Background = targetStatus.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                ? BrushFromRgb(99, 102, 241)
                : BrushFromRgb(22, 163, 74);

            _stepTitle.Text = _wizardStep == 0
                ? (isServiceOnly ? "Step 1 of 2 - Service notes" : "Step 1 of 3 - Replacement flow")
                : _wizardStep == 1
                    ? "Step 2 of 3 - Old item / new item selection"
                    : (isServiceOnly ? "Step 2 of 2 - Confirmation" : "Step 3 of 3 - Confirmation and stock impact preview");

            _typeHelpText.Text = isServiceOnly
                ? (_stockHardwareItems.Count == 0
                    ? "No replacement stock items are available, so this ticket can only be marked as Service Only."
                    : "Use Service Only when no inventory item is issued or replaced. This will only save the service remarks and mark the ticket as Solved, unless Temporary service is checked.")
                : "Use Replacement when an old item is exchanged for stock. The next step will require old item details, replacement stock, quantity, condition, and stock impact.";
            _remarksTitle.Text = isServiceOnly ? "Service notes" : "Replacement remarks";

            _typeStep.Visibility = _wizardStep == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_temporaryServiceCheck != null)
                _temporaryServiceCheck.Visibility = isServiceOnly && _wizardStep == 0 ? Visibility.Visible : Visibility.Collapsed;
            _replacementStep.Visibility = _wizardStep == 1 && isReplacement ? Visibility.Visible : Visibility.Collapsed;
            var showRemarks = (_wizardStep == 0 && isServiceOnly)
                || (_wizardStep == 1 && isReplacement)
                || _wizardStep == 2;
            _remarksSection.Visibility = showRemarks ? Visibility.Visible : Visibility.Collapsed;
            _previewStep.Visibility = _wizardStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            _backButton.IsEnabled = _wizardStep > 0;
            _nextButton.Visibility = _wizardStep < 2 ? Visibility.Visible : Visibility.Collapsed;
            _confirmButton.Visibility = _wizardStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            _nextButton.IsDefault = _wizardStep < 2;
            _confirmButton.IsDefault = _wizardStep == 2;

            if (_wizardStep == 2)
            {
                if (isReplacement)
                    ValidateAndCaptureReplacement(out _);
                else
                    CaptureServiceOnly();
                _previewBox.Text = BuildPreviewText();
            }

            RefreshValidationState();
        }

        private void RefreshPreviewIfNeeded()
        {
            if (_wizardStep != 2)
                return;

            try
            {
                if (_replacementRadio.IsChecked == true)
                    ValidateAndCaptureReplacement(out _);
                else
                    CaptureServiceOnly();
                _previewBox.Text = BuildPreviewText();
            }
            catch
            {
            }
        }

        private void CaptureServiceOnly()
        {
            ResolutionType = "Service Only";
            OldItemId = null;
            NewItemId = null;
            Quantity = 0;
            Remarks = (_remarksBox.Text ?? string.Empty).Trim();
            IsTemporaryReplacement = _temporaryServiceCheck != null && _temporaryServiceCheck.IsChecked == true;
            UseUnlistedOldItem = false;
            OldItemConditionId = 0;
            OldItemConditionRemarks = null;
            OldItemRepairAction = null;
        }

        private bool ValidateAndCaptureReplacement(out string message)
        {
            message = null;
            if (_stockHardwareItems.Count == 0)
                return Invalid("No replacement stock items are available.", out message);

            var newItem = _newItemCombo.SelectedItem as ItemLookupDto;
            if (newItem == null)
                return Invalid("Select a replacement item.", out message);

            var condition = _oldConditionCombo.SelectedItem as ConditionDto;
            if (condition == null)
                return Invalid("Select an old item condition.", out message);

            if (!int.TryParse((_qtyBox.Text ?? string.Empty).Trim(), out var qty) || qty <= 0)
                return Invalid("Quantity must be a positive number.", out message);

            if (newItem.StockOnHand < 1)
                return Invalid("The selected replacement item has no available stock.", out message);

            if (qty > newItem.StockOnHand)
                return Invalid("Quantity exceeds available stock. Available: " + newItem.StockOnHand + ", requested: " + qty + ".", out message);

            if (_oldUnlistedRadio.IsChecked != true)
            {
                var oldItem = _oldItemCombo.SelectedItem as ItemLookupDto;
                if (oldItem == null)
                    return Invalid("Select the replaced old item, or choose Not Listed.", out message);
                if (oldItem.ItemId == newItem.ItemId)
                    return Invalid("Replaced item and replacement item must be different.", out message);
                OldItemId = oldItem.ItemId;
                UseUnlistedOldItem = false;
            }
            else
            {
                var name = (_oldNameBox.Text ?? string.Empty).Trim();
                var model = (_oldModelBox.Text ?? string.Empty).Trim();
                var unit = _oldUnitCombo.SelectedItem as string;
                var category = _oldCategoryCombo.SelectedItem as ItemCategoryDto;

                if (string.IsNullOrWhiteSpace(name)) return Invalid("Old item name is required.", out message);
                if (string.IsNullOrWhiteSpace(model)) return Invalid("Old item model number is required.", out message);
                if (string.IsNullOrWhiteSpace(unit)) return Invalid("Old item unit of measure is required.", out message);
                if (category == null) return Invalid("Old item category is required.", out message);

                UseUnlistedOldItem = true;
                UnlistedOldItemName = name;
                UnlistedOldItemDescription = NullIfWhiteSpace(_oldDescriptionBox.Text);
                UnlistedOldItemCategoryId = category.CategoryId;
                UnlistedOldItemCategoryName = category.Name;
                UnlistedOldItemSerialNumber = NullIfWhiteSpace(_oldSerialBox.Text);
                UnlistedOldItemModelNumber = model;
                UnlistedOldItemUnitOfMeasure = unit;
                OldItemId = null;
            }

            ResolutionType = "Replacement";
            NewItemId = newItem.ItemId;
            Quantity = qty;
            Remarks = (_remarksBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(Remarks))
                return Invalid("Enter replacement remarks before confirming.", out message);

            IsTemporaryReplacement = _temporaryCheck.IsChecked == true;
            OldItemConditionId = condition.ConditionId;
            OldItemConditionRemarks = (_conditionRemarksBox.Text ?? string.Empty).Trim();
            OldItemRepairAction = GetSelectedOldItemRepairAction();
            return true;
        }

        private string GetSelectedOldItemRepairAction()
        {
            if (_oldActionSpareRadio.IsChecked == true)
                return SpareRepairAction;
            if (_oldActionUnrepairedRadio.IsChecked == true)
                return "Unrepaired";
            return "Repaired";
        }

        private string BuildPreviewText()
        {
            if (_replacementRadio.IsChecked != true)
            {
                var followUp = IsTemporaryReplacement
                    ? "\r\n\r\nFollow-up:\r\n  This ticket remains Resolved (Temporary) until the service fix is confirmed permanent."
                    : string.Empty;
                return "Resolution: " + (IsTemporaryReplacement ? "Temporary Service" : "Service Only")
                    + "\r\nStatus after save: " + (IsTemporaryReplacement ? "Resolved (Temporary)" : "Solved")
                    + "\r\n\r\nInventory impact: None" + followUp + "\r\n\r\nRemarks:\r\n"
                    + (string.IsNullOrWhiteSpace(Remarks) ? "(None)" : Remarks);
            }

            var oldItemText = UseUnlistedOldItem
                ? UnlistedOldItemName + " (" + UnlistedOldItemModelNumber + ")"
                : (_outHardwareItems.FirstOrDefault(x => x.ItemId == (OldItemId ?? 0))?.DisplayText ?? "(Unknown)");
            var newItem = _stockHardwareItems.FirstOrDefault(x => x.ItemId == (NewItemId ?? 0));
            var conditionText = _conditions.FirstOrDefault(x => x.ConditionId == OldItemConditionId)?.ConditionName ?? OldItemConditionId.ToString();
            var actionText = string.IsNullOrWhiteSpace(OldItemRepairAction) ? "Repaired" : OldItemRepairAction.Trim();
            var stockAfter = newItem == null ? (int?)null : newItem.StockOnHand - Quantity;
            var oldStockImpact = actionText.Equals("Unrepaired", StringComparison.OrdinalIgnoreCase) ? "0 (kept out of stock)" : "+" + Quantity;

            var sb = new StringBuilder();
            sb.AppendLine("Resolution: " + (IsTemporaryReplacement ? "Temporary Replacement" : "Permanent Replacement"));
            sb.AppendLine("Status after save: " + (IsTemporaryReplacement ? "Resolved (Temporary)" : "Solved"));
            sb.AppendLine();
            sb.AppendLine("Old item:");
            sb.AppendLine("  " + oldItemText);
            sb.AppendLine("  Condition: " + conditionText);
            sb.AppendLine("  Action: " + actionText);
            sb.AppendLine("  Stock impact: " + oldStockImpact);
            sb.AppendLine();
            sb.AppendLine("New item:");
            sb.AppendLine("  " + (newItem?.DisplayText ?? "(Unknown)"));
            sb.AppendLine("  Current stock: " + (newItem == null ? "-" : newItem.StockOnHand.ToString()));
            sb.AppendLine("  Stock impact: -" + Quantity);
            sb.AppendLine("  Stock after save: " + (stockAfter.HasValue ? stockAfter.Value.ToString() : "-"));
            sb.AppendLine();
            sb.AppendLine("Follow-up:");
            sb.AppendLine(IsTemporaryReplacement
                ? "  This ticket remains Resolved (Temporary) until the replacement item is returned."
                : "  No temporary return is required.");
            sb.AppendLine();
            sb.AppendLine("Remarks:");
            sb.AppendLine(string.IsNullOrWhiteSpace(Remarks) ? "(None)" : Remarks);
            return sb.ToString();
        }

        private void Confirm()
        {
            if (_replacementRadio.IsChecked == true)
            {
                if (!ValidateAndCaptureReplacement(out _))
                    return;
            }
            else
            {
                CaptureServiceOnly();
                if (string.IsNullOrWhiteSpace(Remarks))
                {
                    ShowValidation("Enter service remarks before confirming.");
                    return;
                }
            }

            DialogResult = true;
            Close();
        }

        private static bool Invalid(string text, out string message)
        {
            message = text;
            return false;
        }

        private void RefreshValidationState()
        {
            if (_confirmButton == null)
                return;

            var message = GetValidationMessage();
            if (string.IsNullOrWhiteSpace(message))
            {
                _validationText.Visibility = Visibility.Collapsed;
                _validationText.Text = string.Empty;
                _confirmButton.IsEnabled = true;
            }
            else
            {
                ShowValidation(message);
                _confirmButton.IsEnabled = false;
            }

            if (_wizardStep == 2)
            {
                if (_replacementRadio.IsChecked == true)
                    ValidateAndCaptureReplacement(out _);
                else
                    CaptureServiceOnly();
                _previewBox.Text = BuildPreviewText();
            }
        }

        private string GetValidationMessage()
        {
            if (_replacementRadio.IsChecked == true)
            {
                return ValidateAndCaptureReplacement(out var message) ? null : message;
            }

            CaptureServiceOnly();
            return string.IsNullOrWhiteSpace(Remarks)
                ? "Enter service remarks before confirming."
                : null;
        }

        private void ShowValidation(string message)
        {
            _validationText.Text = message;
            _validationText.Visibility = Visibility.Visible;
        }

        private static string NullIfWhiteSpace(string text)
        {
            var value = (text ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static StackPanel CreateCard()
        {
            var panel = new StackPanel
            {
                Background = Brushes.White,
                Margin = new Thickness(0, 0, 0, 14),
                MinHeight = 56
            };
            return panel;
        }

        private static TextBlock CreateSectionTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42),
                Margin = new Thickness(0, 0, 0, 10)
            };
        }

        private static Grid CreateTwoColumnGrid()
        {
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            return grid;
        }

        private static void AddField(Grid grid, int row, int column, string label, FrameworkElement control)
        {
            var stack = new StackPanel
            {
                Margin = new Thickness(column == 0 ? 0 : 12, row == 0 ? 0 : 10, 0, 0)
            };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105),
                Margin = new Thickness(0, 0, 0, 5)
            });
            stack.Children.Add(control);
            Grid.SetRow(stack, row);
            Grid.SetColumn(stack, column);
            grid.Children.Add(stack);
        }

        private static ComboBox CreateCombo()
        {
            return new ComboBox
            {
                MinHeight = 32,
                Padding = new Thickness(8, 4, 8, 4),
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1)
            };
        }

        private static ComboBox CreateItemCombo(bool showStock)
        {
            var combo = CreateCombo();
            combo.IsEditable = true;
            combo.IsTextSearchEnabled = true;
            TextSearch.SetTextPath(combo, "DisplayText");
            combo.ItemTemplate = CreateItemTemplate(showStock);
            return combo;
        }

        private static DataTemplate CreateItemTemplate(bool showStock)
        {
            var panel = new FrameworkElementFactory(typeof(StackPanel));
            panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            var name = new FrameworkElementFactory(typeof(TextBlock));
            name.SetBinding(TextBlock.TextProperty, new Binding("DisplayText"));
            panel.AppendChild(name);

            if (showStock)
            {
                var stock = new FrameworkElementFactory(typeof(TextBlock));
                stock.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(100, 116, 139));
                stock.SetBinding(TextBlock.TextProperty, new Binding("StockOnHand") { StringFormat = "  (Stock: {0})" });
                panel.AppendChild(stock);
            }

            return new DataTemplate { VisualTree = panel };
        }

        private static void SelectItemByIdOrFirst(ComboBox combo, IList<ItemLookupDto> rows, int? previousId)
        {
            if (combo == null)
                return;

            if (previousId.HasValue)
            {
                var match = rows.FirstOrDefault(x => x.ItemId == previousId.Value);
                if (match != null)
                {
                    combo.SelectedItem = match;
                    return;
                }
            }

            combo.SelectedIndex = rows.Count > 0 ? 0 : -1;
        }

        private void ClampQuantityToSelectedStock()
        {
            var item = _newItemCombo.SelectedItem as ItemLookupDto;
            var max = item != null && item.StockOnHand > 0 ? item.StockOnHand : 1;
            if (!int.TryParse((_qtyBox.Text ?? string.Empty).Trim(), out var qty) || qty < 1)
            {
                _qtyBox.Text = "1";
                _qtyBox.CaretIndex = _qtyBox.Text.Length;
                return;
            }

            if (qty > max)
            {
                _qtyBox.Text = max.ToString();
                _qtyBox.CaretIndex = _qtyBox.Text.Length;
            }
        }

        private static void HookNumericQuantityInput(TextBox box)
        {
            box.PreviewTextInput += (_, e) => e.Handled = !IsDigitsOnly(e.Text);
            DataObject.AddPastingHandler(box, (_, e) =>
            {
                if (!e.DataObject.GetDataPresent(DataFormats.Text) || !IsDigitsOnly(e.DataObject.GetData(DataFormats.Text) as string))
                    e.CancelCommand();
            });
        }

        private static bool IsDigitsOnly(string text)
        {
            return !string.IsNullOrEmpty(text) && text.All(char.IsDigit);
        }

        private static TextBox CreateTextBox()
        {
            return new TextBox
            {
                MinHeight = 32,
                Padding = new Thickness(8, 6, 8, 6),
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Foreground = BrushFromRgb(30, 41, 59)
            };
        }

        private static Button CreateButton(string text, Brush background)
        {
            return new Button
            {
                Content = text,
                MinWidth = 96,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(14, 8, 14, 8),
                Background = background,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
        }

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
