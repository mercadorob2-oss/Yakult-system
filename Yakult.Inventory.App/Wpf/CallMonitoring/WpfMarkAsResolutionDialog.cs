using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Models.CallMonitoring;

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

        private readonly List<WpfMarkAsResolutionDialog> _additionalItems = new List<WpfMarkAsResolutionDialog>();
        private readonly StackPanel _additionalItemList;
        private readonly bool _allowBatch;
        private readonly TextBox _oldAssetBox;
        private readonly TextBox _newAssetBox;
        private readonly Grid _oldListedGrid;
        private readonly Grid _oldUnlistedGrid;
        private readonly FrameworkElement _oldDispositionCard;
        private readonly FrameworkElement _itemsOverviewCard;
        private readonly Button _addItemButton;
        private readonly TextBlock _itemsCountText;
        private readonly TextBlock _guidanceText;
        private readonly Border[] _stepIndicators;
        private readonly ScrollViewer _mainScroll;
        private readonly CheckBox _includeOldCheck;
        private readonly CheckBox _includeNewCheck;
        private readonly StackPanel _oldFields;
        private readonly StackPanel _newFields;
        private readonly FrameworkElement _quantityFields;
        private readonly WrapPanel _addItemActions;
        private readonly TextBlock _entryHeading;
        private readonly TextBlock _oldItemHeading;
        private readonly TextBlock _newItemHeading;
        private TextBlock _inlineCaption;
        private int _entryNumber = 1;
        private readonly TextBox _oldSerialSearchBox;
        private readonly TextBox _newSerialSearchBox;
        private readonly ListBox _oldSerialResults;
        private readonly ListBox _newSerialResults;
        private bool _serialLookupInProgress;
        private bool _refreshingValidation;
        private bool _isInlineEditor;
        private bool _inheritTicketNotes;
        private Window _inlineOwner;
        private bool IncludesOldItem => _includeOldCheck != null && _includeOldCheck.IsChecked == true;
        private bool IncludesNewItem => _includeNewCheck != null && _includeNewCheck.IsChecked == true;

        public IList<CallReplacementLine> GetReplacementLines()
        {
            return new[] { this }.Concat(_additionalItems).Select(x => new CallReplacementLine
            {
                OldItemId = x.OldItemId ?? 0, NewItemId = x.NewItemId ?? 0, Quantity = x.Quantity,
                OldItemConditionId = x.OldItemConditionId, OldItemConditionRemarks = x.OldItemConditionRemarks,
                OldItemRepairAction = x.OldItemRepairAction, Remarks = x.Remarks,
                UnlistedOldItem = !x.IncludesOldItem || !x.UseUnlistedOldItem ? null : new ItemDto
                {
                    Name = x.UnlistedOldItemName, Description = x.UnlistedOldItemDescription,
                    ModelNumber = x.UnlistedOldItemModelNumber, SerialNumber = x.UnlistedOldItemSerialNumber,
                    UnitOfMeasure = x.UnlistedOldItemUnitOfMeasure, Active = true, DateCreated = DateTime.Now,
                    ItemType = "Hardware", CategoryId = x.UnlistedOldItemCategoryId, Category = x.UnlistedOldItemCategoryName,
                    StockOnHand = 0, ConditionId = x.OldItemConditionId, Remarks = x.OldItemConditionRemarks,
                    AffectsInventory = true, IsTrackedAsset = false
                }
            }).Where(x => x.HasOldItem || x.HasNewItem).ToList();
        }

        private int _wizardStep;
        private bool _oldActionTouched;
        private bool _suppressOldActionTouched;

        // Stable category-scoped lists backing the searchable combos. The
        // CollectionView filter attaches to these instances, so they are only
        // replaced on category change / load, never per keystroke.
        private List<ItemLookupDto> _searchBaseOld = new List<ItemLookupDto>();
        private List<ItemLookupDto> _searchBaseNew = new List<ItemLookupDto>();

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
            IList<ConditionDto> conditions, bool allowBatch = true)
        {
            _outHardwareItems = (outHardwareItems ?? new List<ItemLookupDto>()).Where(x => x != null).OrderBy(x => x.DisplayText).ToList();
            _stockHardwareItems = (stockHardwareItems ?? new List<ItemLookupDto>()).Where(x => x != null).OrderBy(x => x.DisplayText).ToList();
            _categories = (categories ?? new List<ItemCategoryDto>()).Where(x => x != null).OrderBy(x => x.Name).ToList();
            _conditions = (conditions ?? new List<ConditionDto>()).Where(x => x != null).OrderBy(x => x.ConditionId).ToList();

            _allowBatch = allowBatch;
            var radioScope = Guid.NewGuid().ToString("N");
            Title = "Mark Ticket Resolution";
            Width = 1440;
            Height = 900;
            MinWidth = 1000;
            MinHeight = 640;
            WindowState = WindowState.Maximized;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            Background = BrushFromRgb(245, 247, 250);

            var root = new Grid { Background = Background };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, BrushFromRgb(30, 41, 59));
            Content = root;

            var headerBorder = new Border
            {
                Background = Brushes.White,
                Padding = new Thickness(28, 20, 28, 16),
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            Grid.SetRow(headerBorder, 0);
            root.Children.Add(headerBorder);

            var header = new StackPanel();
            headerBorder.Child = header;

            header.Children.Add(new TextBlock
            {
                Text = "IT CALL MONITORING  /  RESOLUTION", FontSize = 10.5,
                FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(100, 116, 139),
                Margin = new Thickness(0, 0, 0, 6)
            });
            _dialogTitle = new TextBlock
            {
                Text = "Resolution Details",
                FontSize = 26,
                FontWeight = FontWeights.SemiBold,
                Foreground = WpfThemeResources.TextDarkBrush
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
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105)
            };
            var progress = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
            _stepIndicators = new Border[3];
            var stepLabels = new[] { "1  Choose resolution", "2  Item details", "3  Review & save" };
            for (var i = 0; i < stepLabels.Length; i++)
            {
                _stepIndicators[i] = new Border
                {
                    CornerRadius = new CornerRadius(7), Padding = new Thickness(14, 8, 14, 8),
                    Margin = new Thickness(0, 0, 8, 0),
                    Child = new TextBlock { Text = stepLabels[i], FontSize = 12, FontWeight = FontWeights.SemiBold }
                };
                progress.Children.Add(_stepIndicators[i]);
            }
            header.Children.Add(progress);

            var footerBorder = new Border
            {
                Background = Brushes.White, BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(28, 14, 28, 14)
            };
            Grid.SetRow(footerBorder, 2);
            root.Children.Add(footerBorder);
            var footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footerBorder.Child = footer;
            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(buttonRow, 1);
            footer.Children.Add(buttonRow);
            _validationText = new TextBlock
            {
                Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12, Foreground = BrushFromRgb(185, 28, 28), TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            footer.Children.Add(_validationText);

            _backButton = CreateButton("Back", WpfThemeResources.TextMutedBrush);
            _backButton.Click += (_, __) => GoBack();
            var cancelButton = CreateButton("Cancel", BrushFromRgb(71, 85, 105));
            cancelButton.IsCancel = true;
            cancelButton.Click += (_, __) => DialogResult = false;
            _nextButton = CreateButton("Continue →", WpfThemeResources.PrimaryBrush);
            _nextButton.Click += (_, __) => GoNext();
            _confirmButton = CreateButton("Save resolution", BrushFromRgb(22, 163, 74));
            _confirmButton.Click += (_, __) => Confirm();
            _nextButton.MinWidth = 184;
            _confirmButton.MinWidth = 184;

            buttonRow.Children.Add(_backButton);
            buttonRow.Children.Add(cancelButton);
            buttonRow.Children.Add(_nextButton);
            buttonRow.Children.Add(_confirmButton);

            var workspace = new Grid { Margin = new Thickness(24, 20, 24, 12) };
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star), MinWidth = 500 });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 350 });
            Grid.SetRow(workspace, 1);
            root.Children.Add(workspace);
            _mainScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 6, 0)
            };
            workspace.Children.Add(_mainScroll);
            var body = new StackPanel();
            _mainScroll.Content = body;
            var detailScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(detailScroll, 2);
            workspace.Children.Add(detailScroll);
            var details = new StackPanel();
            detailScroll.Content = details;

            var guide = CreateCard();
            guide.Children.Add(CreateSectionTitle("Your resolution"));
            guide.Children.Add(_stepTitle);
            _statusChipText = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
            _statusChip = new Border
            {
                CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 5, 12, 5),
                HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 10),
                Background = BrushFromRgb(22, 163, 74), Child = _statusChipText
            };
            guide.Children.Add(_statusChip);
            _guidanceText = CreateHelpText("Choose how this ticket was resolved. You can review everything before saving.");
            guide.Children.Add(_guidanceText);
            details.Children.Add(WrapCard(guide));

            var typeCard = CreateCard();
            _typeStep = WrapCard(typeCard);
            typeCard.Children.Add(CreateSectionTitle("How was the issue resolved?"));
            var typeRow = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            _serviceOnlyRadio = new RadioButton { Content = "Service Only (No Parts)", GroupName = "ResolutionType" + radioScope, IsChecked = true, Margin = new Thickness(0, 0, 28, 0), FontWeight = FontWeights.SemiBold };
            _replacementRadio = new RadioButton { Content = "Replacement (Parts Used)", GroupName = "ResolutionType" + radioScope, FontWeight = FontWeights.SemiBold };
            _serviceOnlyRadio.Checked += (_, __) => UpdateWizardStep();
            _replacementRadio.Checked += (_, __) => UpdateWizardStep();
            typeRow.Children.Add(CreateChoiceCard(_serviceOnlyRadio, "Service only", "The issue was fixed without issuing or replacing an item."));
            typeRow.Children.Add(CreateChoiceCard(_replacementRadio, "Items used or collected", "Record old items collected, new items issued, or an old / new replacement."));
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
                Content = "Temporary fix — follow-up needed",
                Margin = new Thickness(0, 10, 0, 0),
                FontWeight = FontWeights.SemiBold
            };
            _temporaryServiceCheck.Checked += (_, __) => UpdateWizardStep();
            _temporaryServiceCheck.Unchecked += (_, __) => UpdateWizardStep();
            typeCard.Children.Add(_temporaryServiceCheck);
            body.Children.Add(_typeStep);

            var replacementCard = CreateCard();
            _replacementStep = WrapCard(replacementCard);
            _entryHeading = CreateSectionTitle("What would you like to record?");
            replacementCard.Children.Add(_entryHeading);
            replacementCard.Children.Add(CreateHelpText("Choose either side, or both for a direct replacement. Old and new items do not have to be added in equal numbers."));
            var includeRow = new WrapPanel { Margin = new Thickness(0, 14, 0, 20) };
            _includeOldCheck = new CheckBox { Content = "Include an old item", IsChecked = true, Margin = new Thickness(0, 0, 24, 8), FontWeight = FontWeights.SemiBold };
            _includeNewCheck = new CheckBox { Content = "Issue a new item", IsChecked = true, Margin = new Thickness(0, 0, 0, 8), FontWeight = FontWeights.SemiBold };
            includeRow.Children.Add(_includeOldCheck);
            includeRow.Children.Add(_includeNewCheck);
            replacementCard.Children.Add(includeRow);
            _oldFields = CreateCard();
            replacementCard.Children.Add(_oldFields);
            _oldItemHeading = CreateSectionTitle("Old item being collected");
            _oldFields.Children.Add(_oldItemHeading);

            _oldFields.Children.Add(CreateHelpText("Select the old item from inventory, or enter its details if it is not listed."));
            var oldModeRow = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 12) };
            _oldListedRadio = new RadioButton { Content = "Listed in Inventory", GroupName = "OldItemMode" + radioScope, IsChecked = true, Margin = new Thickness(0, 0, 22, 0) };
            _oldUnlistedRadio = new RadioButton { Content = "Not listed — enter details", GroupName = "OldItemMode" + radioScope };
            _oldListedRadio.Checked += (_, __) => UpdateOldItemMode();
            _oldUnlistedRadio.Checked += (_, __) => UpdateOldItemMode();
            oldModeRow.Children.Add(_oldListedRadio);
            oldModeRow.Children.Add(_oldUnlistedRadio);
            _oldFields.Children.Add(oldModeRow);

            var oldListedGrid = CreateTwoColumnGrid();
            _oldListedGrid = oldListedGrid;
            _oldCategoryFilter = CreateCombo();
            _oldItemCombo = CreateItemCombo(showStock: false);
            AddField(oldListedGrid, 0, 0, "Category", _oldCategoryFilter);
            AddField(oldListedGrid, 0, 1, "Choose the old item", _oldItemCombo);
            _oldAssetBox = CreateTextBox();
            _oldAssetBox.IsReadOnly = true;
            _oldAssetBox.ToolTip = "Fetched from the inventory item description, where imported fixed asset numbers are stored.";
            AddField(oldListedGrid, 1, 1, "Fixed asset number / description · automatic", _oldAssetBox);
            _oldItemCombo.SelectionChanged += (_, __) =>
            {
                _oldAssetBox.Text = (_oldItemCombo.SelectedItem as ItemLookupDto)?.FixedAssetNumber ?? "";
                if (!_serialLookupInProgress && _oldSerialSearchBox != null) _oldSerialSearchBox.Text = "";
            };
            _oldFields.Children.Add(oldListedGrid);
            _oldFields.Children.Insert(1, CreateSerialSearch(false, out _oldSerialSearchBox, out _oldSerialResults));

            var oldUnlistedGrid = CreateTwoColumnGrid();
            _oldCategoryCombo = CreateCombo();
            _oldNameBox = CreateTextBox();
            _oldModelBox = CreateTextBox();
            _oldSerialBox = CreateTextBox();
            _oldUnitCombo = CreateCombo();
            _oldDescriptionBox = CreateTextBox();
            AddField(oldUnlistedGrid, 0, 0, "Item name *", _oldNameBox);
            AddField(oldUnlistedGrid, 0, 1, "Category", _oldCategoryCombo);
            AddField(oldUnlistedGrid, 1, 0, "Model number *", _oldModelBox);
            AddField(oldUnlistedGrid, 1, 1, "Serial number", _oldSerialBox);
            AddField(oldUnlistedGrid, 2, 0, "Unit of measure *", _oldUnitCombo);
            var fetchAsset = CreateButton("Fetch by serial", WpfThemeResources.PrimaryBrush);
            fetchAsset.Click += (_, __) =>
            {
                var serial = (_oldSerialBox.Text ?? "").Trim();
                var matches = _outHardwareItems.Concat(_stockHardwareItems).GroupBy(x => x.ItemId).Select(x => x.First())
                    .Where(x => serial.Length > 0 && string.Equals(x.SerialNumber?.Trim(), serial, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count != 1 || string.IsNullOrWhiteSpace(matches[0].FixedAssetNumber))
                {
                    MessageBox.Show(_inlineOwner ?? this, matches.Count > 1 ? "Several items have this serial. Choose Listed in Inventory to select the correct item."
                        : "No fixed asset number / description was found for this serial.", "Fetch asset number", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                _oldDescriptionBox.Text = matches[0].FixedAssetNumber;
            };
            var assetStack = new StackPanel();
            assetStack.Children.Add(_oldDescriptionBox);
            assetStack.Children.Add(fetchAsset);
            AddField(oldUnlistedGrid, 2, 1, "Fixed asset number / description", assetStack);
            _oldUnlistedGrid = oldUnlistedGrid;
            _oldFields.Children.Add(oldUnlistedGrid);

            var conditionGrid = CreateTwoColumnGrid();
            _oldConditionCombo = CreateCombo();
            _conditionRemarksBox = CreateTextBox();
            AddField(conditionGrid, 0, 0, "Current condition *", _oldConditionCombo);
            AddField(conditionGrid, 0, 1, "Condition notes (optional)", _conditionRemarksBox);
            var dispositionCard = CreateCard();
            dispositionCard.Children.Add(CreateSectionTitle("What happens to the old item?"));
            dispositionCard.Children.Add(CreateHelpText("Record its condition and choose how IT will handle it."));
            dispositionCard.Children.Add(conditionGrid);

            var actionRow = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            actionRow.Children.Add(new TextBlock { Text = "After replacement", Margin = new Thickness(0, 0, 0, 10), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(71, 85, 105) });
            _oldActionRepairedRadio = new RadioButton { Content = "Repaired — return to available stock", GroupName = "OldItemAction" + radioScope, IsChecked = true, Margin = new Thickness(0, 0, 22, 0) };
            _oldActionUnrepairedRadio = new RadioButton { Content = "Needs repair — keep out of stock", GroupName = "OldItemAction" + radioScope, Margin = new Thickness(0, 0, 22, 0) };
            _oldActionSpareRadio = new RadioButton { Content = "Repaired — keep as a spare", GroupName = "OldItemAction" + radioScope };
            _oldActionRepairedRadio.Checked += (_, __) => { TouchOldAction(); RefreshPreviewIfNeeded(); };
            _oldActionUnrepairedRadio.Checked += (_, __) => { TouchOldAction(); RefreshPreviewIfNeeded(); };
            _oldActionSpareRadio.Checked += (_, __) => { TouchOldAction(); RefreshPreviewIfNeeded(); };
            actionRow.Children.Add(_oldActionRepairedRadio);
            actionRow.Children.Add(_oldActionUnrepairedRadio);
            actionRow.Children.Add(_oldActionSpareRadio);
            foreach (var option in actionRow.Children.OfType<RadioButton>()) option.Margin = new Thickness(0, 0, 0, 12);
            dispositionCard.Children.Add(actionRow);
            _oldDispositionCard = WrapCard(dispositionCard);
            details.Children.Add(_oldDispositionCard);

            _newFields = CreateCard();
            replacementCard.Children.Add(_newFields);
            _newFields.Children.Add(new Border { Height = 1, Background = BrushFromRgb(226, 232, 240), Margin = new Thickness(0, 18, 0, 22) });
            _newItemHeading = CreateSectionTitle("New item to issue from stock");
            _newFields.Children.Add(_newItemHeading);
            _newFields.Children.Add(CreateHelpText("Search by item name, model, serial number, or fixed asset number. Only available stock can be issued."));
            var newGrid = CreateTwoColumnGrid();
            _newCategoryFilter = CreateCombo();
            _newItemCombo = CreateItemCombo(showStock: true);
            _qtyBox = CreateTextBox();
            _qtyBox.Text = "1";
            HookNumericQuantityInput(_qtyBox);
            _qtyBox.LostFocus += (_, __) => ClampQuantityToSelectedStock();
            _newStockHint = new TextBlock { Margin = new Thickness(0, 4, 0, 0), Foreground = WpfThemeResources.TextMutedBrush };
            var newItemStack = new StackPanel();
            newItemStack.Children.Add(_newItemCombo);
            newItemStack.Children.Add(_newStockHint);
            AddField(newGrid, 0, 0, "Category", _newCategoryFilter);
            AddField(newGrid, 0, 1, "Choose a replacement *", newItemStack);

            _temporaryCheck = new CheckBox { Content = "Temporary (return later)", Margin = new Thickness(0, 8, 0, 0) };

            _newAssetBox = CreateTextBox();
            _newAssetBox.IsReadOnly = true;
            _newAssetBox.ToolTip = _oldAssetBox.ToolTip;
            AddField(newGrid, 2, 1, "Fixed asset number / description · automatic", _newAssetBox);
            _newItemCombo.SelectionChanged += (_, __) =>
            {
                _newAssetBox.Text = (_newItemCombo.SelectedItem as ItemLookupDto)?.FixedAssetNumber ?? "";
                if (!_serialLookupInProgress && _newSerialSearchBox != null) _newSerialSearchBox.Text = "";
            };
            _newFields.Children.Add(newGrid);
            _newFields.Children.Insert(2, CreateSerialSearch(true, out _newSerialSearchBox, out _newSerialResults));
            var quantityGrid = CreateTwoColumnGrid();
            AddField(quantityGrid, 0, 0, "Quantity for this entry *", _qtyBox);
            AddField(quantityGrid, 0, 1, "Return arrangement for issued items", _temporaryCheck);
            _quantityFields = quantityGrid;
            replacementCard.Children.Add(quantityGrid);
            body.Children.Add(_replacementStep);

            var itemsCard = CreateCard();
            itemsCard.Children.Add(CreateSectionTitle("Add items to this resolution"));
            _itemsCountText = CreateHelpText("Add an old item, a new item, or both. Each entry has its own quantity and notes.");
            itemsCard.Children.Add(_itemsCountText);
            _additionalItemList = new StackPanel { Margin = new Thickness(0, 10, 0, 12) };

            _addItemActions = new WrapPanel();
            var addOld = CreateButton("+ Add old item", WpfThemeResources.PrimaryBrush);
            var addNew = CreateButton("+ Add new item", WpfThemeResources.PrimaryBrush);
            _addItemButton = CreateButton("+ Add both", WpfThemeResources.TextMutedBrush);
            addOld.Click += (_, __) => AddAnotherItem(true, false);
            addNew.Click += (_, __) => AddAnotherItem(false, true);
            _addItemButton.Click += (_, __) => AddAnotherItem(true, true);
            foreach (var button in new[] { addOld, addNew, _addItemButton })
            {
                button.Margin = new Thickness(0, 0, 8, 8);
                _addItemActions.Children.Add(button);
            }
            if (allowBatch) itemsCard.Children.Add(_addItemActions);
            body.Children.Add(_additionalItemList);
            _itemsOverviewCard = WrapCard(itemsCard);

            var remarksCard = CreateCard();
            _remarksSection = WrapCard(remarksCard);
            _remarksTitle = CreateSectionTitle("Service notes");
            remarksCard.Children.Add(_remarksTitle);
            _remarksBox = CreateTextBox();
            _remarksBox.MinHeight = 130;
            _remarksBox.AcceptsReturn = true;
            _remarksBox.TextWrapping = TextWrapping.Wrap;
            _remarksBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            remarksCard.Children.Add(CreateHelpText("Briefly explain what was done and any follow-up needed. These notes are saved with the ticket."));
            _remarksBox.Margin = new Thickness(0, 12, 0, 0);
            remarksCard.Children.Add(_remarksBox);
            details.Children.Add(_remarksSection);
            details.Children.Insert(1, _itemsOverviewCard);

            var previewCard = CreateCard();
            _previewStep = WrapCard(previewCard);
            previewCard.Children.Add(CreateSectionTitle("Review your resolution"));
            previewCard.Children.Add(CreateHelpText("Check the items, quantities, and stock changes below. Use Back if anything needs changing."));
            _previewBox = CreateTextBox();
            _previewBox.MinHeight = 380;
            _previewBox.Margin = new Thickness(0, 16, 0, 0);
            _previewBox.Background = BrushFromRgb(248, 250, 252);
            _previewBox.Padding = new Thickness(18);
            _previewBox.AcceptsReturn = true;
            _previewBox.TextWrapping = TextWrapping.Wrap;
            _previewBox.IsReadOnly = true;
            previewCard.Children.Add(_previewBox);
            body.Children.Add(_previewStep);

            if (!allowBatch)
            {
                _serviceOnlyRadio.IsEnabled = false;
                _replacementRadio.IsChecked = true;
                _temporaryCheck.Visibility = Visibility.Collapsed;
            }
            _includeOldCheck.Checked += (_, __) => UpdateEntryMode();
            _includeOldCheck.Unchecked += (_, __) => UpdateEntryMode();
            _includeNewCheck.Checked += (_, __) => UpdateEntryMode();
            _includeNewCheck.Unchecked += (_, __) => UpdateEntryMode();
            PopulateLookups();
            HookPreviewRefresh();
            AttachItemSearch(_oldItemCombo, () => _searchBaseOld);
            AttachItemSearch(_newItemCombo, () => _searchBaseNew);
            ApplyAvailabilityGuards();
            UpdateEntryMode();
            UpdateOldItemMode();
            UpdateWizardStep();
            Loaded += (_, __) =>
            {
                _wizardStep = _allowBatch ? 0 : 1;
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
                _typeHelpText.Text = "No new stock is available. You can still record an old item collected.";
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
            _searchBaseOld = rows;
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
            _searchBaseNew = rows;
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
            if (_oldListedGrid != null) _oldListedGrid.Visibility = useUnlisted ? Visibility.Collapsed : Visibility.Visible;
            if (_oldUnlistedGrid != null) _oldUnlistedGrid.Visibility = useUnlisted ? Visibility.Visible : Visibility.Collapsed;
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
            if (_serialLookupInProgress) return;
            var item = _newItemCombo.SelectedItem as ItemLookupDto;
            _newStockHint.Text = item == null ? string.Empty : "Available stock: " + item.StockOnHand;
            ClampQuantityToSelectedStock();
        }

        private IEnumerable<ItemLookupDto> GetSerialSearchItems(bool forNewItem)
        {
            var source = forNewItem
                ? _stockHardwareItems.Where(x => x.StockOnHand > 0 && !IsConsumableCategory(x.Category))
                : _outHardwareItems.AsEnumerable();
            return source.Where(x => !string.IsNullOrWhiteSpace(x.SerialNumber))
                .OrderBy(x => x.SerialNumber, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.DisplayText);
        }

        private FrameworkElement CreateSerialSearch(bool forNewItem, out TextBox searchBox, out ListBox resultList)
        {
            var box = CreateTextBox();
            box.ToolTip = "Type or scan a serial number. Search covers all eligible categories; choosing a match fills the item and category.";
            var results = new ListBox
            {
                MaxHeight = 170, Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 6, 0, 0), Background = Brushes.White,
                BorderBrush = BrushFromRgb(203, 213, 225), BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            var row = new FrameworkElementFactory(typeof(TextBlock));
            row.SetBinding(TextBlock.TextProperty, new Binding("DisplayText"));
            row.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            row.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 6, 8, 6));
            results.ItemTemplate = new DataTemplate { VisualTree = row };
            var hint = CreateHelpText("Type or scan a serial number to fill the item and category automatically.");
            hint.Margin = new Thickness(0, 5, 0, 0);
            Action<ItemLookupDto> choose = item =>
            {
                SelectSerialMatch(item, forNewItem);
                results.Visibility = Visibility.Collapsed;
                hint.Text = "Selected: " + item.DisplayText + " | Category: " + (string.IsNullOrWhiteSpace(item.Category) ? "Not specified" : item.Category);
            };
            box.TextChanged += (_, __) =>
            {
                if (_serialLookupInProgress) return;
                var query = (box.Text ?? "").Trim();
                if (query.Length == 0)
                {
                    results.Visibility = Visibility.Collapsed;
                    hint.Text = "Type or scan a serial number to fill the item and category automatically.";
                    return;
                }
                var matches = GetSerialSearchItems(forNewItem)
                    .Where(x => x.SerialNumber.Trim().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                var exact = matches.Where(x => string.Equals(x.SerialNumber.Trim(), query, StringComparison.OrdinalIgnoreCase)).ToList();
                if (exact.Count == 1)
                {
                    choose(exact[0]);
                    return;
                }
                // A partial, missing, or ambiguous serial must not retain a stale item selection.
                _serialLookupInProgress = true;
                try
                {
                    results.SelectedItem = null;
                    var combo = forNewItem ? _newItemCombo : _oldItemCombo;
                    combo.SelectedItem = null;
                    combo.Text = "";
                    if (forNewItem) _newStockHint.Text = "";
                }
                finally { _serialLookupInProgress = false; }
                results.ItemsSource = matches.Take(40).ToList();
                results.Visibility = matches.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                hint.Text = matches.Count == 0
                    ? (forNewItem ? "No available replacement stock matches this serial." : "No listed old item matches this serial. Use Not listed to enter its details.")
                    : exact.Count > 1 ? "Several items share this serial. Choose the correct item below."
                    : "Choose a matching serial below" + (matches.Count > 40 ? " (showing the first 40; type more to narrow the results)." : ".");
                RefreshValidationState();
            };
            results.SelectionChanged += (_, __) =>
            {
                if (!_serialLookupInProgress && results.SelectedItem is ItemLookupDto item) choose(item);
            };
            box.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) { results.Visibility = Visibility.Collapsed; e.Handled = true; }
                if (e.Key == Key.Enter)
                {
                    e.Handled = true; // A scanner's Enter key selects an item, never advances the wizard.
                    if (results.Visibility == Visibility.Visible && results.Items.Count == 1)
                        choose((ItemLookupDto)results.Items[0]);
                }
            };
            var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 16) };
            panel.Children.Add(new TextBlock
            {
                Text = forNewItem ? "Search new item by serial number" : "Search old item by serial number",
                FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6)
            });
            panel.Children.Add(box);
            panel.Children.Add(hint);
            panel.Children.Add(results);
            searchBox = box;
            resultList = results;
            return panel;
        }

        private void SelectSerialMatch(ItemLookupDto item, bool forNewItem)
        {
            if (item == null || !GetSerialSearchItems(forNewItem).Any(x => x.ItemId == item.ItemId)) return;
            _serialLookupInProgress = true;
            try
            {
                if (!forNewItem) _oldListedRadio.IsChecked = true;
                var category = forNewItem ? _newCategoryFilter : _oldCategoryFilter;
                var categoryName = (item.Category ?? "").Trim();
                category.SelectedItem = category.Items.Cast<string>().FirstOrDefault(x => string.Equals(
                    forNewItem ? ParseCategoryDisplay(x) : x, categoryName, StringComparison.OrdinalIgnoreCase))
                    ?? category.Items.Cast<string>().FirstOrDefault(x => x.StartsWith("All Categories", StringComparison.OrdinalIgnoreCase));
                // Rebuild even when the category is unchanged, clearing any old name-search filter.
                if (forNewItem) ApplyNewFilter(); else ApplyOldFilter();
                var combo = forNewItem ? _newItemCombo : _oldItemCombo;
                combo.SelectedItem = item;
                combo.Text = item.DisplayText;
                (forNewItem ? _newSerialSearchBox : _oldSerialSearchBox).Text = item.SerialNumber.Trim();
            }
            finally { _serialLookupInProgress = false; }
            if (forNewItem) UpdateNewItemStockHint();
            RefreshValidationState();
            RefreshPreviewIfNeeded();
        }

        private static string FormatEntryHeading(int number, bool hasOld, bool hasNew)
        {
            if (number <= 0 || (!hasOld && !hasNew)) return "Choose an old or new item";
            if (hasOld && hasNew) return "Old " + number + " \u2194 New " + number;
            return (hasOld ? "Old " : "New ") + number + " (no pair)";
        }

        private void UpdateEntryLabels(int number)
        {
            _entryNumber = number;
            if (_entryHeading == null || _oldItemHeading == null || _newItemHeading == null) return;
            var heading = FormatEntryHeading(number, IncludesOldItem, IncludesNewItem);
            _entryHeading.Text = _isInlineEditor ? "Items in this entry" : heading;
            if (_inlineCaption != null) _inlineCaption.Text = heading;
            var suffix = number > 0 ? " " + number : "";
            var pairNote = IncludesOldItem && IncludesNewItem ? "" : " (no pair)";
            _oldItemHeading.Text = "Old item" + suffix + pairNote + " - being collected";
            _newItemHeading.Text = "New item" + suffix + pairNote + " - to issue from stock";
        }

        private void RefreshEntryNumbers()
        {
            if (_isInlineEditor)
            {
                UpdateEntryLabels(_entryNumber);
                return;
            }
            var number = 1;
            foreach (var entry in new[] { this }.Concat(_additionalItems))
            {
                var hasItems = entry.IncludesOldItem || entry.IncludesNewItem;
                entry.UpdateEntryLabels(hasItems ? number++ : 0);
            }
        }

        private void UpdateEntryMode()
        {
            if (_oldFields == null || _newFields == null || _quantityFields == null) return;
            _oldFields.Visibility = IncludesOldItem ? Visibility.Visible : Visibility.Collapsed;
            _newFields.Visibility = IncludesNewItem ? Visibility.Visible : Visibility.Collapsed;
            _qtyBox.IsEnabled = IncludesOldItem || IncludesNewItem;
            var hasIssuedItems = IncludesNewItem || _additionalItems.Any(x => x.IncludesNewItem);
            _quantityFields.Visibility = IncludesOldItem || hasIssuedItems ? Visibility.Visible : Visibility.Collapsed;
            _temporaryCheck.IsEnabled = hasIssuedItems;
            if (!hasIssuedItems) _temporaryCheck.IsChecked = false;
            if (IncludesNewItem) ClampQuantityToSelectedStock();
            UpdateWizardStep();
        }

        private static void DetachEditorElement(FrameworkElement element)
        {
            var panel = element.Parent as Panel;
            if (panel != null) panel.Children.Remove(element);
            var border = element.Parent as Border;
            if (border != null) border.Child = null;
        }

        private void AddAnotherItem(bool includeOld = true, bool includeNew = true)
        {
            // Reuse the same field controller, without showing another window.
            // Its form sections become children of this page's left scroll panel.
            var child = new WpfMarkAsResolutionDialog(_outHardwareItems, _stockHardwareItems, _categories, _conditions, false);
            child._isInlineEditor = true;
            child._inheritTicketNotes = true;
            child._inlineOwner = this;
            child._includeOldCheck.IsChecked = includeOld;
            child._includeNewCheck.IsChecked = includeNew;
            child._remarksBox.Text = _remarksBox.Text;
            child._wizardStep = 1;
            child.UpdateWizardStep();
            var editorBody = new StackPanel();
            foreach (var section in new[] { child._replacementStep, child._oldDispositionCard, child._remarksSection })
            {
                DetachEditorElement(section);
                var card = section as Border;
                if (card != null)
                {
                    card.Padding = new Thickness(0, 12, 0, 0);
                    card.BorderThickness = new Thickness(0);
                    card.Background = Brushes.Transparent;
                    card.Margin = new Thickness(0, 0, 0, 12);
                }
                editorBody.Children.Add(section);
            }
            var useTicketNotes = new CheckBox
            {
                Content = "Use the ticket notes for this entry", IsChecked = true,
                Margin = new Thickness(0, 12, 0, 12)
            };
            useTicketNotes.Checked += (_, __) =>
            {
                child._inheritTicketNotes = true;
                child._remarksSection.Visibility = Visibility.Collapsed;
                RefreshValidationState();
            };
            useTicketNotes.Unchecked += (_, __) =>
            {
                child._inheritTicketNotes = false;
                child._remarksSection.Visibility = Visibility.Visible;
                RefreshValidationState();
            };
            editorBody.Children.Insert(editorBody.Children.Count - 1, useTicketNotes);
            DetachEditorElement(child._validationText);
            child._validationText.Margin = new Thickness(0, 8, 0, 0);
            editorBody.Children.Add(child._validationText);
            child.Content = null;

            var editor = new Expander
            {
                IsExpanded = true, Content = editorBody, Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            var editorCard = new Border
            {
                Child = editor, Background = Brushes.White, CornerRadius = new CornerRadius(10),
                BorderBrush = BrushFromRgb(203, 213, 225), BorderThickness = new Thickness(1),
                Padding = new Thickness(22), Margin = new Thickness(0, 0, 0, 16)
            };
            var header = new DockPanel { LastChildFill = true };
            var remove = CreateButton("Remove", WpfThemeResources.TextMutedBrush);
            remove.MinWidth = 76;
            remove.Padding = new Thickness(10, 6, 10, 6);
            remove.MinHeight = 32;
            DockPanel.SetDock(remove, Dock.Right);
            header.Children.Add(remove);
            var caption = new TextBlock
            {
                FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
            };
            child._inlineCaption = caption;
            header.Children.Add(caption);
            editor.Header = header;
            remove.Click += (_, __) =>
            {
                _additionalItems.Remove(child);
                _additionalItemList.Children.Remove(editorCard);
                UpdateEntryMode();
            };
            Action refreshEntry = () =>
            {
                if (!child._serialLookupInProgress && !_refreshingValidation) UpdateEntryMode();
            };
            editorBody.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler((_, __) => refreshEntry()));
            editorBody.AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,
                new SelectionChangedEventHandler((_, __) => refreshEntry()));
            editorBody.AddHandler(System.Windows.Controls.Primitives.ToggleButton.CheckedEvent,
                new RoutedEventHandler((_, __) => refreshEntry()));
            editorBody.AddHandler(System.Windows.Controls.Primitives.ToggleButton.UncheckedEvent,
                new RoutedEventHandler((_, __) => refreshEntry()));
            _additionalItems.Add(child);
            _additionalItemList.Children.Add(editorCard);
            refreshEntry();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_additionalItemList.Children.Contains(editorCard)) editorCard.BringIntoView();
            }), DispatcherPriority.Loaded);
        }

        private void GoNext()
        {
            if (_wizardStep == 0)
            {
                _wizardStep = _replacementRadio.IsChecked == true ? 1 : 2;
                if (_wizardStep == 2)
                    CaptureServiceOnly();
                _mainScroll.ScrollToTop();
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
                _mainScroll.ScrollToTop();
                UpdateWizardStep();
            }
        }

        private void GoBack()
        {
            if (_wizardStep <= 0)
                return;
            _wizardStep = _wizardStep == 2 && _serviceOnlyRadio.IsChecked == true ? 0 : _wizardStep - 1;
            _mainScroll.ScrollToTop();
            UpdateWizardStep();
        }

        private void UpdateWizardStep()
        {
            var isReplacement = _replacementRadio.IsChecked == true;
            var isServiceOnly = !isReplacement;
            var isTemporaryService = isServiceOnly && _temporaryServiceCheck != null && _temporaryServiceCheck.IsChecked == true;

            var targetStatus = (isReplacement && _temporaryCheck.IsChecked == true) || isTemporaryService ? "Resolved (Temporary)" : "Solved";
            _dialogTitle.Text = _allowBatch ? "Mark ticket resolution"
                : IncludesOldItem && IncludesNewItem ? "Add old and new items"
                : IncludesOldItem ? "Add old item" : "Add new item";
            _dialogSubtitle.Text = isReplacement
                ? "Record old items collected, new items issued, or both. Add your notes, then review and save."
                : "Tell us how the issue was fixed, then review and save your resolution.";
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
                    ? "No new stock is available. You can still record old items collected, or choose Service only for a fix without items."
                    : "Choose this when the issue was fixed without parts. Add your service notes on the right.")
                : "Next, choose whether to collect an old item, issue a new one, or record both. Add as many entries as needed.";
            _remarksTitle.Text = isServiceOnly ? "Service notes *" : "Item notes *";
            _confirmButton.Content = _allowBatch ? "Save resolution" : "Add to resolution";
            _guidanceText.Text = !_allowBatch
                ? "This entry will be added to your resolution. Stock changes are applied only when you save the main resolution."
                : _wizardStep == 2
                ? "Everything ready? Save resolution will apply the stock changes and update the ticket status shown above."
                : isReplacement
                    ? "Add old items and new items independently. Use Add both when two items form a direct replacement. Nothing is saved until the final review."
                    : "Add a short explanation of the fix. Choose temporary service if further follow-up is needed.";
            _oldDispositionCard.Visibility = isReplacement && IncludesOldItem && _wizardStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            _itemsOverviewCard.Visibility = _allowBatch && isReplacement && _wizardStep > 0 ? Visibility.Visible : Visibility.Collapsed;
            _addItemActions.Visibility = _wizardStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            _nextButton.Content = _wizardStep == 1 || isServiceOnly ? "Review resolution →" : "Choose items →";
            for (var i = 0; i < _stepIndicators.Length; i++)
            {
                var active = i == _wizardStep;
                _stepIndicators[i].Background = active ? WpfThemeResources.PrimaryBrush : BrushFromRgb(241, 245, 249);
                ((TextBlock)_stepIndicators[i].Child).Foreground = active ? Brushes.White : BrushFromRgb(100, 116, 139);
                _stepIndicators[i].Visibility = isServiceOnly && i == 1 ? Visibility.Collapsed : Visibility.Visible;
            }
            ((TextBlock)_stepIndicators[2].Child).Text = isServiceOnly ? "2  Review & save" : "3  Review & save";

            _typeStep.Visibility = _wizardStep == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_temporaryServiceCheck != null)
                _temporaryServiceCheck.Visibility = isServiceOnly && _wizardStep == 0 ? Visibility.Visible : Visibility.Collapsed;
            _replacementStep.Visibility = _wizardStep == 1 && isReplacement ? Visibility.Visible : Visibility.Collapsed;
            var showRemarks = (_wizardStep == 0 && isServiceOnly)
                || (_wizardStep == 1 && isReplacement)
                || _wizardStep == 2;
            _remarksSection.Visibility = showRemarks && (!_isInlineEditor || !_inheritTicketNotes) ? Visibility.Visible : Visibility.Collapsed;
            _additionalItemList.Visibility = _allowBatch && isReplacement && _wizardStep > 0
                ? Visibility.Visible : Visibility.Collapsed;
            _additionalItemList.IsEnabled = _wizardStep == 1;
            if (_wizardStep == 2)
                foreach (var editor in _additionalItemList.Children.OfType<Border>().Select(x => x.Child).OfType<Expander>()) editor.IsExpanded = false;
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
                _previewBox.Text = BuildAllPreviewText();
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
                _previewBox.Text = BuildAllPreviewText();
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

        private bool ValidateAndCaptureReplacement(out string message, bool commit = false)
        {
            message = null;
            if (_allowBatch)
            {
                for (var index = 0; index < _additionalItems.Count; index++)
                {
                    var child = _additionalItems[index];
                    if (child._inheritTicketNotes) child._remarksBox.Text = _remarksBox.Text;
                    if (!child.ValidateAndCaptureReplacement(out var entryMessage, commit))
                    {
                        child.ShowValidation(entryMessage);
                        return Invalid("Additional entry " + (index + 1) + ": " + entryMessage, out message);
                    }
                    child._validationText.Text = "";
                    child._validationText.Visibility = Visibility.Collapsed;
                }
            }
            var hasOld = IncludesOldItem;
            var hasNew = IncludesNewItem;
            if (!hasOld && !hasNew && (!_allowBatch || _additionalItems.Count == 0))
                return Invalid("Include an old item, a new item, or both.", out message);

            OldItemId = null;
            NewItemId = null;
            UseUnlistedOldItem = false;
            OldItemConditionId = 0;
            OldItemConditionRemarks = null;
            OldItemRepairAction = null;
            Quantity = 0;
            if (hasOld || hasNew)
            {
                if (!int.TryParse((_qtyBox.Text ?? "").Trim(), out var qty) || qty <= 0)
                    return Invalid("Quantity must be a positive number.", out message);
                Quantity = qty;
            }
            if (hasNew)
            {
                if (_stockHardwareItems.Count == 0)
                    return Invalid("No replacement stock items are available.", out message);
                ResolveItemCombo(_newItemCombo, FilterByCategory(_stockHardwareItems, ParseCategoryDisplay(_newCategoryFilter.SelectedItem as string)), commit);
                var newItem = _newItemCombo.SelectedItem as ItemLookupDto;
                if (newItem == null) return Invalid("Select a new item to issue.", out message);
                if (newItem.StockOnHand < Quantity)
                    return Invalid("Quantity exceeds available stock. Available: " + newItem.StockOnHand + ".", out message);
                NewItemId = newItem.ItemId;
            }
            if (hasOld)
            {
                var condition = _oldConditionCombo.SelectedItem as ConditionDto;
                if (condition == null) return Invalid("Select an old item condition.", out message);
                OldItemConditionId = condition.ConditionId;
                OldItemConditionRemarks = (_conditionRemarksBox.Text ?? "").Trim();
                OldItemRepairAction = GetSelectedOldItemRepairAction();
                if (_oldUnlistedRadio.IsChecked != true)
                {
                    ResolveItemCombo(_oldItemCombo, FilterByCategory(_outHardwareItems, _oldCategoryFilter.SelectedItem as string), commit);
                    var oldItem = _oldItemCombo.SelectedItem as ItemLookupDto;
                    if (oldItem == null) return Invalid("Select the old item, or choose Not listed.", out message);
                    OldItemId = oldItem.ItemId;
                }
                else
                {
                    var name = (_oldNameBox.Text ?? "").Trim();
                    var model = (_oldModelBox.Text ?? "").Trim();
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
                }
            }
            ResolutionType = "Replacement";
            Remarks = (_remarksBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(Remarks)) return Invalid("Add notes before reviewing this resolution.", out message);
            var lines = GetReplacementLines();
            var structuralError = CallReplacementLine.GetValidationError(lines);
            if (structuralError != null) return Invalid(structuralError, out message);
            IsTemporaryReplacement = _temporaryCheck.IsChecked == true && lines.Any(x => x.HasNewItem);
            foreach (var group in lines.Where(x => x.HasNewItem).GroupBy(x => x.NewItemId))
            {
                var item = _stockHardwareItems.FirstOrDefault(x => x.ItemId == group.Key);
                if (item == null || group.Sum(x => (long)x.Quantity) > item.StockOnHand)
                    return Invalid("Combined quantities exceed stock for " + (item?.DisplayText ?? group.Key.ToString()), out message);
            }
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

        private string BuildAllPreviewText()
        {
            if (_replacementRadio.IsChecked != true) return BuildPreviewText();
            var lines = GetReplacementLines();
            var sb = new StringBuilder();
            sb.AppendLine("Status after save: " + (IsTemporaryReplacement ? "Resolved (Temporary)" : "Solved"));
            sb.AppendLine("Old items: " + lines.Count(x => x.HasOldItem) + " | New items: " + lines.Count(x => x.HasNewItem));
            var number = 1;
            foreach (var line in lines)
            {
                sb.AppendLine();
                sb.AppendLine(FormatEntryHeading(number++, line.HasOldItem, line.HasNewItem));
                sb.AppendLine(line.HasOldItem && line.HasNewItem ? "Old + new (direct replacement)" : line.HasOldItem ? "Old item only (collection)" : "New item only (issue)");
                sb.AppendLine("Quantity: " + line.Quantity);
                if (line.HasOldItem)
                {
                    var old = _outHardwareItems.FirstOrDefault(x => x.ItemId == line.OldItemId);
                    sb.AppendLine("Old: " + (line.UnlistedOldItem?.Name ?? old?.DisplayText));
                    sb.AppendLine("Fixed asset / description: " + (line.UnlistedOldItem?.Description ?? old?.FixedAssetNumber));
                    sb.AppendLine("Condition: " + (_conditions.FirstOrDefault(x => x.ConditionId == line.OldItemConditionId)?.ConditionName ?? line.OldItemConditionId.ToString()));
                    sb.AppendLine("Action: " + line.OldItemRepairAction);
                    sb.AppendLine("Old stock impact: " + (string.Equals(line.OldItemRepairAction, "Unrepaired", StringComparison.OrdinalIgnoreCase) ? "0 (kept out of stock)" : "+" + line.Quantity));
                }
                if (line.HasNewItem)
                {
                    var item = _stockHardwareItems.FirstOrDefault(x => x.ItemId == line.NewItemId);
                    sb.AppendLine("New: " + item?.DisplayText);
                    sb.AppendLine("Fixed asset / description: " + item?.FixedAssetNumber);
                    sb.AppendLine("New stock impact: -" + line.Quantity);
                }
                sb.AppendLine("Notes: " + line.Remarks);
            }
            sb.AppendLine();
            sb.AppendLine("Combined new item stock after save:");
            foreach (var group in lines.Where(x => x.HasNewItem).GroupBy(x => x.NewItemId))
            {
                var item = _stockHardwareItems.FirstOrDefault(x => x.ItemId == group.Key);
                sb.AppendLine("  " + item?.DisplayText + ": " + ((item?.StockOnHand ?? 0) - group.Sum(x => x.Quantity)));
            }
            if (!lines.Any(x => x.HasNewItem)) sb.AppendLine("No new items will be issued.");
            sb.AppendLine();
            sb.AppendLine(IsTemporaryReplacement ? "Temporary return: only issued new items will be returned. Old-only entries are not returned again."
                : "No temporary return is required.");
            sb.AppendLine("Only entries containing both an old and new item perform a direct set swap.");
            sb.AppendLine("Ticket notes: " + Remarks);
            return sb.ToString();
        }

        private string BuildPreviewText()
        {
            if (_replacementRadio.IsChecked == true) return BuildAllPreviewText();
            return "Resolution: " + (IsTemporaryReplacement ? "Temporary service" : "Service only")
                + "\r\nStatus after save: " + (IsTemporaryReplacement ? "Resolved (Temporary)" : "Solved")
                + "\r\n\r\nInventory impact: None\r\n\r\nNotes:\r\n" + Remarks;
        }

        private void Confirm()
        {
            if (_replacementRadio.IsChecked == true)
            {
                if (!ValidateAndCaptureReplacement(out var message, commit: true))
                {
                    ShowValidation(message);
                    return;
                }
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
            if (_refreshingValidation || _serialLookupInProgress) return;
            RefreshEntryNumbers();
            _refreshingValidation = true;
            try
            {
                if (_confirmButton == null)
                    return;

                if (_itemsCountText != null)
                    _itemsCountText.Text = _additionalItems.Count == 0
                        ? "Add old items, new items, or both. Each entry has its own quantity and notes."
                        : "Additional entries: " + _additionalItems.Count + ". Old and new items are independent; counts do not need to match.";
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
                    _previewBox.Text = BuildAllPreviewText();
                }
            }
            finally
            {
                _refreshingValidation = false;
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
            return new StackPanel { Background = Brushes.Transparent };
        }

        private static Border WrapCard(StackPanel content)
        {
            return new Border
            {
                Background = Brushes.White, BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                Padding = new Thickness(22), Margin = new Thickness(0, 0, 0, 16), Child = content
            };
        }

        private static TextBlock CreateHelpText(string text)
        {
            return new TextBlock
            {
                Text = text, FontSize = 12.5, Foreground = BrushFromRgb(100, 116, 139),
                TextWrapping = TextWrapping.Wrap, LineHeight = 19
            };
        }

        private static Border CreateChoiceCard(RadioButton radio, string title, string description)
        {
            radio.Margin = new Thickness(0);
            radio.Padding = new Thickness(8, 0, 0, 0);
            radio.VerticalContentAlignment = VerticalAlignment.Center;
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            content.Children.Add(CreateHelpText(description));
            radio.Content = content;
            var style = new Style(typeof(Border));
            style.Setters.Add(new Setter(Border.BackgroundProperty, Brushes.White));
            style.Setters.Add(new Setter(Border.BorderBrushProperty, BrushFromRgb(226, 232, 240)));
            var selected = new DataTrigger { Binding = new Binding("IsChecked") { Source = radio }, Value = true };
            selected.Setters.Add(new Setter(Border.BackgroundProperty, BrushFromRgb(252, 240, 241)));
            selected.Setters.Add(new Setter(Border.BorderBrushProperty, WpfThemeResources.PrimaryBrush));
            style.Triggers.Add(selected);
            return new Border
            {
                Style = style, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 12), Child = radio
            };
        }

        private static TextBlock CreateSectionTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = WpfThemeResources.TextDarkBrush,
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
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105),
                Margin = new Thickness(0, 0, 0, 5),
                TextWrapping = TextWrapping.Wrap
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
                MinHeight = 40,
                MaxDropDownHeight = 320,
                VerticalContentAlignment = VerticalAlignment.Center,
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
            combo.ToolTip = "Type a name, model, serial number, or fixed asset number to search.";
            // Borrow-style live filtering (AttachItemSearch) owns the text:
            // WPF's own text search would append-complete and fight it.
            combo.IsTextSearchEnabled = false;
            TextSearch.SetTextPath(combo, "DisplayText");
            combo.ItemTemplate = CreateItemTemplate(showStock);
            return combo;
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) return typed;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private static string ItemSearchHaystack(ItemLookupDto x)
        {
            if (x == null) return "";
            return string.Join(" ", new[] { x.Name, x.DisplayText, x.ModelNumber, x.Category, x.SerialNumber, x.FixedAssetNumber });
        }

        /// <summary>
        /// Borrow-style searchable combo (mirrors AttachEmpSearch in
        /// WpfBorrowTransactionWorkspace): typing narrows the dropdown with a
        /// multi-token AND match across name/model/category/serial instead of
        /// scrolling the full list. getBaseList is the current category scope.
        /// </summary>
        private static void AttachItemSearch(ComboBox cb, Func<List<ItemLookupDto>> getBaseList)
        {
            // Loaded may already have fired (or never fire before typing in a
            // pre-shown tree): hook now when loaded, otherwise on Loaded.
            if (cb.IsLoaded) HookItemSearchBox(cb, getBaseList);
            else cb.Loaded += (s, e) => HookItemSearchBox(cb, getBaseList);
        }

        private static void HookItemSearchBox(ComboBox cb, Func<List<ItemLookupDto>> getBaseList)
        {
            // Deterministic lookup first (no visual-tree timing involved),
            // visual-tree walk as fallback.
            cb.ApplyTemplate();
            var textBox = cb.Template?.FindName("PART_EditableTextBox", cb) as TextBox
                          ?? FindVisualChild<TextBox>(cb);
            if (textBox == null) return;

            // Debounced like the Replacement Items dialog (250ms): filtering
            // a thousand-row view on every keystroke lags while typing.
            var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            debounce.Tick += (s2, e2) =>
            {
                try
                {
                    debounce.Stop();
                    RunItemFilter(cb, textBox, getBaseList);
                }
                catch { }
            };
            cb.Unloaded += (s2, e2) =>
            {
                try { debounce.Stop(); } catch { }
            };

            textBox.TextChanged += (s2, e2) =>
            {
                // Selection echo (click picked an item and WPF painted its
                // text) is not typing: never re-filter on it, or the refresh
                // lands mid-click and eats the selection.
                var sel = cb.SelectedItem as ItemLookupDto;
                if (sel != null && string.Equals(sel.DisplayText ?? "", textBox.Text ?? "", StringComparison.OrdinalIgnoreCase))
                    return;
                debounce.Stop();
                debounce.Start();
            };
        }

        private static void RunItemFilter(ComboBox cb, TextBox textBox, Func<List<ItemLookupDto>> getBaseList)
        {
            var rawText = textBox.Text ?? string.Empty;
            var query = rawText.Trim();
            var baseList = getBaseList() ?? new List<ItemLookupDto>();

            // Filter the live view in place. ItemsSource, SelectedItem and the
            // edit text are never rewritten, so a clicked pick can never be
            // blanked by a rebind and typing is never interrupted.
            var view = CollectionViewSource.GetDefaultView(baseList);
            if (view == null) return;

            if (query.Length == 0)
            {
                view.Filter = null;
                cb.IsDropDownOpen = false;
                return;
            }

            var tokens = query.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            view.Filter = o =>
            {
                var x = o as ItemLookupDto;
                return x != null && tokens.All(token =>
                    ItemSearchHaystack(x).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
            };

            if (!view.IsEmpty) cb.IsDropDownOpen = true;
        }

        /// <summary>
        /// Typing in an editable combo does not move SelectedItem, so a stale
        /// pick could validate. Re-resolve the text to an exact display match
        /// before reading the selection. Live validation (commit:false) only
        /// ever upgrades null to a match: it must never clear a pick, because
        /// SelectionChanged fires before WPF repaints the edit text and a
        /// strict clear would blank a just-clicked row. The explicit Confirm
        /// path passes commit:true for the strict gate.
        /// </summary>
        private static void ResolveItemCombo(ComboBox combo, IEnumerable<ItemLookupDto> baseList, bool commit)
        {
            var selected = combo.SelectedItem as ItemLookupDto;
            string text = (combo.Text ?? "").Trim();
            if (selected != null)
            {
                if (string.Equals(selected.DisplayText ?? "", text, StringComparison.OrdinalIgnoreCase))
                    return;
                if (!commit) return;
            }
            combo.SelectedItem = text.Length == 0 ? null : (baseList ?? Enumerable.Empty<ItemLookupDto>())
                .FirstOrDefault(x => string.Equals(x?.DisplayText ?? "", text, StringComparison.OrdinalIgnoreCase));
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
                stock.SetValue(TextBlock.ForegroundProperty, WpfThemeResources.TextMutedBrush);
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
            if (!IncludesNewItem) return;
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
                MinHeight = 40,
                Padding = new Thickness(8, 6, 8, 6),
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Foreground = BrushFromRgb(30, 41, 59)
            };
        }

        private static Button CreateButton(string text, Brush background)
        {
            var chrome = new FrameworkElementFactory(typeof(Border));
            chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            chrome.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            chrome.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            chrome.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            chrome.AppendChild(presenter);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = chrome };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.88));
            template.Triggers.Add(hover);
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.42));
            template.Triggers.Add(disabled);
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focus.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFromRgb(96, 165, 250)));
            template.Triggers.Add(focus);
            return new Button
            {
                Content = text, Template = template, MinWidth = 100, MinHeight = 40,
                Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(18, 10, 18, 10),
                Background = background, Foreground = Brushes.White, BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(2), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand
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
