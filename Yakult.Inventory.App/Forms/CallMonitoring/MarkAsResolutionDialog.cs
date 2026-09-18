using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class MarkAsResolutionDialog : Form
    {
        private const string SpareRepairAction = "Repaired - Spare inventory";

        private readonly List<ItemLookupDto> _outHardwareItems;
        private readonly List<ItemLookupDto> _stockHardwareItems;
        private List<ItemLookupDto> _filteredStockHardwareItems;
        private readonly List<ItemCategoryDto> _categories;
        private readonly List<ConditionDto> _conditions;

        private Label lblNewItemStockHint;

        private RadioButton rdoServiceOnly;
        private RadioButton rdoReplacement;

        private RadioButton rdoOldListed;
        private RadioButton rdoOldUnlisted;

        private ComboBox cboOldItemListed;
        private ComboBox cboOldListedCategory;
        private FlowLayoutPanel pnlOldListedStack;
        private List<ItemLookupDto> _filteredOutHardwareItems;
        private Panel pnlOldItemUnlisted;
        private TextBox txtOldName;
        private ComboBox cboOldCategory;
        private TextBox txtOldModel;
        private TextBox txtOldSerial;
        private ComboBox cboOldUnit;
        private TextBox txtOldDescription;

        private ComboBox cboNewCategory;
        private ComboBox cboNewItem;
        private CheckBox chkTemporaryReplacement;
        private CheckBox chkTemporaryService;
        private ComboBox cboOldCondition;
        private TextBox txtConditionRemarks;
        private RadioButton rdoOldActionRepaired;
        private RadioButton rdoOldActionUnrepaired;
        private RadioButton rdoOldActionSpare;
        private bool _oldActionTouched;
        private bool _suppressOldActionTouched;
        private NumericUpDown numQty;
        private TextBox txtRemarks;

        private Button btnConfirm;
        private Button btnCancel;
        private Button btnBack;
        private Button btnNext;
        private Label lblStepTitle;
        private Panel pnlTypeStep;
        private Panel pnlRemarksSection;
        private Panel pnlPreviewSection;
        private TextBox txtPreview;
        private int _wizardStep;

        // Container Panels for visibility toggling
        private Panel pnlReplacementSection;
        private Panel pnlOldItemSelectionContainer;

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

        public MarkAsResolutionDialog(
            List<ItemLookupDto> outHardwareItems,
            List<ItemLookupDto> stockHardwareItems,
            List<ItemCategoryDto> categories,
            IList<ConditionDto> conditions)
        {
            _outHardwareItems = outHardwareItems ?? new List<ItemLookupDto>();
            _stockHardwareItems = stockHardwareItems ?? new List<ItemLookupDto>();
            _categories = categories ?? new List<ItemCategoryDto>();
            _conditions = (conditions ?? new List<ConditionDto>()).ToList();

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Text = "Mark Ticket Resolution";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.White;
            this.Font = ModernUiHelper.FontNormal;
            this.ClientSize = new Size(950, 750);

            var mainScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var contentPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(24)
            };
            mainScroll.Controls.Add(contentPanel);

            // Header
            contentPanel.Controls.Add(ModernUiHelper.CreateHeaderLabel("Resolution Details"));
            lblStepTitle = new Label
            {
                AutoSize = false,
                Width = 860,
                Height = 28,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Text = "Step 1 of 3 - Resolution type"
            };
            contentPanel.Controls.Add(lblStepTitle);
            contentPanel.Controls.Add(new Label { Height = 10 });

            // 1. Resolution Type
            var typeCard = ModernUiHelper.CreateStyledPanel();
            typeCard.AutoSize = true;
            typeCard.Width = 860;
            pnlTypeStep = typeCard;
            var typeFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true };
            typeCard.Controls.Add(typeFlow);

            typeFlow.Controls.Add(ModernUiHelper.CreateLabel("Resolution Type"));
            
            var rdoPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            rdoServiceOnly = new RadioButton { Text = "Service Only (No Parts)", AutoSize = true, Checked = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            rdoReplacement = new RadioButton { Text = "Replacement (Parts Used)", AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            rdoServiceOnly.CheckedChanged += (_, __) => UpdateMode();
            rdoReplacement.CheckedChanged += (_, __) => UpdateMode();
            
            rdoPanel.Controls.Add(rdoServiceOnly);
            rdoPanel.Controls.Add(new Label { Width = 30 });
            rdoPanel.Controls.Add(rdoReplacement);
            typeFlow.Controls.Add(rdoPanel);

            chkTemporaryService = new CheckBox { Text = "Temporary service (mark as Resolved (Temporary))", AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            chkTemporaryService.CheckedChanged += (_, __) => RefreshPreviewIfNeeded();
            typeFlow.Controls.Add(chkTemporaryService);

            contentPanel.Controls.Add(typeCard);
            contentPanel.Controls.Add(new Label { Height = 10 });

            // 2. Replacement Section (Hidden by default)
            pnlReplacementSection = new Panel { AutoSize = true, Width = 860, Visible = false };
            var repLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true };
            pnlReplacementSection.Controls.Add(repLayout);

            // 2a. Replaced Item (The "Old" Item)
            repLayout.Controls.Add(ModernUiHelper.CreateSectionHeader("Item Being Replaced (Old Unit)"));
            
            var oldModePanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            rdoOldListed = new RadioButton { Text = "Listed in Inventory", AutoSize = true, Checked = true };
            rdoOldUnlisted = new RadioButton { Text = "Not Listed (External/New)", AutoSize = true };
            rdoOldListed.CheckedChanged += (_, __) => UpdateOldItemMode();
            rdoOldUnlisted.CheckedChanged += (_, __) => UpdateOldItemMode();
            oldModePanel.Controls.AddRange(new Control[] { rdoOldListed, new Label { Width = 20 }, rdoOldUnlisted });
            repLayout.Controls.Add(oldModePanel);

            pnlOldItemSelectionContainer = new Panel { AutoSize = true, Width = 840, Padding = new Padding(0, 5, 0, 15) };
            
            cboOldItemListed = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                DisplayMember = "DisplayText",
                ValueMember = "ItemId",
                Width = 600,
                Height = 30,
                Font = ModernUiHelper.FontNormal
            };

            cboOldListedCategory = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 320,
                Height = 30,
                Font = ModernUiHelper.FontNormal
            };
            var oldCategoryChoices = _outHardwareItems
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Category))
                .Select(x => x.Category.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            oldCategoryChoices.Insert(0, "All Categories");
            cboOldListedCategory.DataSource = oldCategoryChoices;
            cboOldListedCategory.SelectedIndexChanged += (_, __) => ApplyOldItemCategoryFilter();

            _filteredOutHardwareItems = _outHardwareItems.ToList();
            cboOldItemListed.DataSource = _filteredOutHardwareItems;
            
            pnlOldItemUnlisted = BuildUnlistedOldItemPanel();

            pnlOldListedStack = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            var catRow = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Width = 840, Margin = new Padding(0, 0, 0, 6) };
            catRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            catRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            catRow.Controls.Add(ModernUiHelper.CreateLabel("Category:"), 0, 0);
            catRow.Controls.Add(cboOldListedCategory, 1, 0);
            pnlOldListedStack.Controls.Add(catRow);

            // Align the item dropdown with the category dropdown (same left gutter).
            var itemRow = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Width = 840, Margin = new Padding(0) };
            itemRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            itemRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            cboOldItemListed.Dock = DockStyle.Fill;
            itemRow.Controls.Add(ModernUiHelper.CreateLabel("Item:"), 0, 0);
            itemRow.Controls.Add(cboOldItemListed, 1, 0);
            pnlOldListedStack.Controls.Add(itemRow);

            pnlOldItemSelectionContainer.Controls.Add(pnlOldListedStack);
            pnlOldItemSelectionContainer.Controls.Add(pnlOldItemUnlisted);
            repLayout.Controls.Add(pnlOldItemSelectionContainer);

            // 2b. Old Item Condition
            var condGrid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Width = 840 };
            condGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            condGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            
            cboOldCondition = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "ConditionName", ValueMember = "ConditionId", Width = 250, Height = 28 };
            cboOldCondition.DataSource = _conditions.ToList();
            if (cboOldCondition.Items.Count > 0) cboOldCondition.SelectedIndex = 0;
            cboOldCondition.SelectedIndexChanged += (_, __) => AutoSelectOldUnitActionFromCondition();

            txtConditionRemarks = ModernUiHelper.CreateTextBox();
            txtConditionRemarks.TextChanged += (_, __) => RefreshPreviewIfNeeded();
            
            condGrid.Controls.Add(ModernUiHelper.CreateLabel("Condition:"), 0, 0);
            condGrid.Controls.Add(cboOldCondition, 1, 0);
            condGrid.Controls.Add(ModernUiHelper.CreateLabel("Remarks:"), 0, 1);
            condGrid.Controls.Add(txtConditionRemarks, 1, 1);
            
            repLayout.Controls.Add(condGrid);
            repLayout.Controls.Add(new Label { Height = 20 });

            // 2c. Old Unit Action (what happens to the pulled-out old unit)
            repLayout.Controls.Add(ModernUiHelper.CreateSectionHeader("Old Unit Action (After Pullout)"));

            var oldActionGrid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Width = 840 };
            oldActionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            oldActionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var pnlOldAction = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0)
            };

            rdoOldActionRepaired = new RadioButton { AutoSize = true, Text = "Repaired (return to stock)", Checked = true };
            rdoOldActionUnrepaired = new RadioButton { AutoSize = true, Text = "Damaged / For repair (keep OUT of stock)" };
            rdoOldActionSpare = new RadioButton { AutoSize = true, Text = "Spare (return to stock as spare)" };

            void TouchOldAction()
            {
                if (!_suppressOldActionTouched)
                    _oldActionTouched = true;
            }

            rdoOldActionRepaired.CheckedChanged += (_, __) => { if (rdoOldActionRepaired.Checked) TouchOldAction(); };
            rdoOldActionUnrepaired.CheckedChanged += (_, __) => { if (rdoOldActionUnrepaired.Checked) TouchOldAction(); };
            rdoOldActionSpare.CheckedChanged += (_, __) => { if (rdoOldActionSpare.Checked) TouchOldAction(); };
            rdoOldActionRepaired.CheckedChanged += (_, __) => RefreshPreviewIfNeeded();
            rdoOldActionUnrepaired.CheckedChanged += (_, __) => RefreshPreviewIfNeeded();
            rdoOldActionSpare.CheckedChanged += (_, __) => RefreshPreviewIfNeeded();

            pnlOldAction.Controls.Add(rdoOldActionRepaired);
            pnlOldAction.Controls.Add(rdoOldActionUnrepaired);
            pnlOldAction.Controls.Add(rdoOldActionSpare);

            oldActionGrid.Controls.Add(ModernUiHelper.CreateLabel("Action:"), 0, 0);
            oldActionGrid.Controls.Add(pnlOldAction, 1, 0);

            repLayout.Controls.Add(oldActionGrid);
            repLayout.Controls.Add(new Label { Height = 20 });

            // 2c. Replacement Item (The "New" Item)
            repLayout.Controls.Add(ModernUiHelper.CreateSectionHeader("Replacement Item (New Unit)"));
            
            cboNewItem = new ComboBox
            {
                // Use DropDown to allow built-in autocomplete (DropDownList + AutoComplete can throw on some runtimes).
                DropDownStyle = ComboBoxStyle.DropDown,
                DisplayMember = "DisplayText",
                ValueMember = "ItemId",
                Width = 600,
                Height = 30,
                Font = ModernUiHelper.FontNormal
            };
            cboNewItem.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cboNewItem.AutoCompleteSource = AutoCompleteSource.ListItems;

            _filteredStockHardwareItems = _stockHardwareItems.ToList();
            cboNewItem.DataSource = _filteredStockHardwareItems;
            cboNewItem.FormattingEnabled = true;
            cboNewItem.Format += (_, e) =>
            {
                if (e.ListItem is ItemLookupDto i)
                    e.Value = i.DisplayText + (i.StockOnHand > 0 ? $"  (Stock: {i.StockOnHand})" : "  (Stock: 0)");
            };
            cboNewItem.SelectedIndexChanged += (_, __) => UpdateNewItemStockHint();
            
            lblNewItemStockHint = new Label { AutoSize = true, ForeColor = ModernUiHelper.ColorSuccess, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            
            var newGrid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Width = 840 };
            newGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            newGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            cboNewCategory = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 320,
                Height = 30,
                Font = ModernUiHelper.FontNormal
            };
            var categoryChoices = _stockHardwareItems
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Category))
                .Select(x => x.Category.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            categoryChoices.Insert(0, "All Categories");
            cboNewCategory.DataSource = categoryChoices;
            cboNewCategory.SelectedIndexChanged += (_, __) =>
            {
                ApplyNewItemCategoryFilter();
                UpdateNewItemStockHint();
            };

            newGrid.Controls.Add(ModernUiHelper.CreateLabel("Category:"), 0, 0);
            newGrid.Controls.Add(cboNewCategory, 1, 0);

            newGrid.Controls.Add(ModernUiHelper.CreateLabel("Item:"), 0, 1);
            var itemStack = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };
            itemStack.Controls.Add(cboNewItem);
            itemStack.Controls.Add(lblNewItemStockHint);
            newGrid.Controls.Add(itemStack, 1, 1);

            numQty = new NumericUpDown { Minimum = 1, Maximum = 999, Value = 1, Width = 100, Height = 28 };
            numQty.ValueChanged += (_, __) => RefreshPreviewIfNeeded();
            newGrid.Controls.Add(ModernUiHelper.CreateLabel("Quantity:"), 0, 2);
            newGrid.Controls.Add(numQty, 1, 2);

            chkTemporaryReplacement = new CheckBox { Text = "Temporary replacement (mark as Resolved (Temporary))", AutoSize = true };
            chkTemporaryReplacement.CheckedChanged += (_, __) => RefreshPreviewIfNeeded();
            newGrid.Controls.Add(new Label(), 0, 3);
            newGrid.Controls.Add(chkTemporaryReplacement, 1, 3);

            repLayout.Controls.Add(newGrid);
            
            contentPanel.Controls.Add(pnlReplacementSection);
            contentPanel.Controls.Add(new Label { Height = 10 });

            // 3. General Remarks
            pnlRemarksSection = new Panel { AutoSize = true, Width = 860 };
            var remarksFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            pnlRemarksSection.Controls.Add(remarksFlow);
            remarksFlow.Controls.Add(ModernUiHelper.CreateSectionHeader("General Remarks"));
            txtRemarks = ModernUiHelper.CreateTextBox();
            txtRemarks.Multiline = true;
            txtRemarks.Height = 80;
            txtRemarks.ScrollBars = ScrollBars.Vertical;
            txtRemarks.Width = 860;
            txtRemarks.TextChanged += (_, __) => RefreshPreviewIfNeeded();
            remarksFlow.Controls.Add(txtRemarks);
            contentPanel.Controls.Add(pnlRemarksSection);

            pnlPreviewSection = new Panel { AutoSize = true, Width = 860 };
            var previewFlow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            pnlPreviewSection.Controls.Add(previewFlow);
            previewFlow.Controls.Add(ModernUiHelper.CreateSectionHeader("Confirmation and Stock Impact Preview"));
            txtPreview = new TextBox
            {
                Width = 860,
                Height = 220,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(248, 250, 252),
                Font = new Font("Consolas", 9.5F)
            };
            previewFlow.Controls.Add(txtPreview);
            contentPanel.Controls.Add(pnlPreviewSection);


            // Footer
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = ModernUiHelper.ColorBackground,
                Padding = new Padding(24, 15, 24, 15)
            };
            footer.Paint += (_, e) => { using (var pen = new Pen(Color.FromArgb(220, 220, 220))) e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0); };

            btnConfirm = ModernUiHelper.CreatePrimaryButton("Confirm Resolution", 160);
            btnConfirm.Click += (_, __) => Confirm();

            btnNext = ModernUiHelper.CreatePrimaryButton("Next", 100);
            btnNext.Click += (_, __) => GoNext();

            btnBack = ModernUiHelper.CreateSecondaryButton("Back", 90);
            btnBack.Click += (_, __) => GoBack();

            btnCancel = ModernUiHelper.CreateSecondaryButton("Cancel");
            btnCancel.Click += (_, __) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            var btnFlow = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, WrapContents = false };
            btnFlow.Controls.Add(btnConfirm);
            btnFlow.Controls.Add(btnNext);
            btnFlow.Controls.Add(btnBack);
            btnFlow.Controls.Add(btnCancel);
            footer.Controls.Add(btnFlow);

            this.Controls.Add(mainScroll);
            this.Controls.Add(footer);

            this.AcceptButton = btnConfirm;
            this.CancelButton = btnCancel;

            if (_stockHardwareItems.Count == 0)
            {
                rdoReplacement.Enabled = false;
                rdoServiceOnly.Checked = true;
            }

            if (_categories.Count == 0)
            {
                rdoOldUnlisted.Enabled = false;
                rdoOldListed.Checked = true;
            }

            UpdateMode();
            UpdateOldItemMode();
            ApplyOldItemCategoryFilter();
            ApplyNewItemCategoryFilter();
            UpdateNewItemStockHint();
            AutoSelectOldUnitActionFromCondition();
            UpdateWizardStep();

            this.ResumeLayout(false);
        }

        private void AutoSelectOldUnitActionFromCondition()
        {
            if (_oldActionTouched)
                return;

            try
            {
                var cond = cboOldCondition?.SelectedItem as ConditionDto;
                var condName = (cond?.ConditionName ?? string.Empty).Trim();
                _suppressOldActionTouched = true;
                try
                {
                    if (condName.Equals("Damaged", StringComparison.OrdinalIgnoreCase))
                    {
                        if (rdoOldActionUnrepaired != null) rdoOldActionUnrepaired.Checked = true;
                    }
                    else
                    {
                        if (rdoOldActionRepaired != null) rdoOldActionRepaired.Checked = true;
                    }
                }
                finally
                {
                    _suppressOldActionTouched = false;
                }
            }
            catch
            {
                // Ignore auto-select failures; user can still choose manually.
            }
        }

        private string GetSelectedOldItemRepairAction()
        {
            if (rdoOldActionSpare != null && rdoOldActionSpare.Checked)
                return SpareRepairAction;
            if (rdoOldActionUnrepaired != null && rdoOldActionUnrepaired.Checked)
                return "Unrepaired";
            return "Repaired";
        }

        private void ApplyOldItemCategoryFilter()
        {
            if (cboOldItemListed == null)
                return;

            var prevSelected = cboOldItemListed.SelectedValue;
            var selectedCategory = (cboOldListedCategory?.SelectedItem as string) ?? "All Categories";

            IEnumerable<ItemLookupDto> source = _outHardwareItems;
            if (!string.IsNullOrWhiteSpace(selectedCategory)
                && !selectedCategory.Equals("All Categories", StringComparison.OrdinalIgnoreCase))
            {
                source = source.Where(x =>
                    x != null
                    && !string.IsNullOrWhiteSpace(x.Category)
                    && string.Equals(x.Category.Trim(), selectedCategory.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            _filteredOutHardwareItems = source
                .Where(x => x != null)
                .OrderBy(x => x.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToList();

            cboOldItemListed.DataSource = null;
            cboOldItemListed.DataSource = _filteredOutHardwareItems;
            cboOldItemListed.DisplayMember = "DisplayText";
            cboOldItemListed.ValueMember = "ItemId";

            if (prevSelected != null)
            {
                try { cboOldItemListed.SelectedValue = prevSelected; }
                catch { }
            }

            if (cboOldItemListed.SelectedIndex < 0 && cboOldItemListed.Items.Count > 0)
                cboOldItemListed.SelectedIndex = 0;
        }

        private void ApplyNewItemCategoryFilter()
        {
            if (cboNewItem == null)
                return;

            var prevSelected = cboNewItem.SelectedValue;
            var selectedCategory = (cboNewCategory?.SelectedItem as string) ?? "All Categories";

            IEnumerable<ItemLookupDto> source = _stockHardwareItems;
            if (!string.IsNullOrWhiteSpace(selectedCategory)
                && !selectedCategory.Equals("All Categories", StringComparison.OrdinalIgnoreCase))
            {
                source = source.Where(x =>
                    x != null
                    && !string.IsNullOrWhiteSpace(x.Category)
                    && string.Equals(x.Category.Trim(), selectedCategory.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            _filteredStockHardwareItems = source
                .Where(x => x != null)
                .OrderBy(x => x.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ToList();

            cboNewItem.DataSource = null;
            cboNewItem.DataSource = _filteredStockHardwareItems;
            cboNewItem.DisplayMember = "DisplayText";
            cboNewItem.ValueMember = "ItemId";

            if (prevSelected != null)
            {
                try { cboNewItem.SelectedValue = prevSelected; }
                catch { }
            }

            if (cboNewItem.SelectedIndex < 0 && cboNewItem.Items.Count > 0)
                cboNewItem.SelectedIndex = 0;
        }

        private void UpdateNewItemStockHint()
        {
            var item = GetSelectedNewItem();
            if (item == null)
            {
                lblNewItemStockHint.Text = "";
                return;
            }

            lblNewItemStockHint.Text = $"Available Stock: {item.StockOnHand}";
            var max = item.StockOnHand > 0 ? item.StockOnHand : 1;
            numQty.Maximum = max;
            if (numQty.Value > max) numQty.Value = max;
        }

        private ItemLookupDto GetSelectedNewItem()
        {
            var selectedId = cboNewItem.SelectedValue;
            if (selectedId == null) return null;

            var id = Convert.ToInt32(selectedId);
            return _stockHardwareItems.FirstOrDefault(x => x != null && x.ItemId == id);
        }

        private Panel BuildUnlistedOldItemPanel()
        {
            var pnl = new Panel { Visible = false, AutoSize = true, Width = 840 };
            var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 4, Dock = DockStyle.Top };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            txtOldName = ModernUiHelper.CreateTextBox();
            table.Controls.Add(ModernUiHelper.CreateLabel("Name*"), 0, 0);
            table.Controls.Add(txtOldName, 1, 0);

            cboOldCategory = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Height = 28 };
            cboOldCategory.DisplayMember = "Name";
            cboOldCategory.ValueMember = "CategoryId";
            cboOldCategory.DataSource = _categories.ToList();
            table.Controls.Add(ModernUiHelper.CreateLabel("Category*"), 2, 0);
            table.Controls.Add(cboOldCategory, 3, 0);

            txtOldModel = ModernUiHelper.CreateTextBox();
            table.Controls.Add(ModernUiHelper.CreateLabel("Model*"), 0, 1);
            table.Controls.Add(txtOldModel, 1, 1);

            txtOldSerial = ModernUiHelper.CreateTextBox();
            table.Controls.Add(ModernUiHelper.CreateLabel("Serial"), 2, 1);
            table.Controls.Add(txtOldSerial, 3, 1);
            
            cboOldUnit = new ComboBox { Dock = DockStyle.Left, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList, Height = 28 };
            cboOldUnit.Items.AddRange(new object[] { "Unit", "Piece", "Set", "Box" });
            if (cboOldUnit.Items.Count > 0) cboOldUnit.SelectedIndex = 0;
            table.Controls.Add(ModernUiHelper.CreateLabel("Unit*"), 0, 2);
            table.Controls.Add(cboOldUnit, 1, 2);

            txtOldDescription = ModernUiHelper.CreateTextBox();
            table.Controls.Add(ModernUiHelper.CreateLabel("Desc"), 2, 2);
            table.Controls.Add(txtOldDescription, 3, 2);

            pnl.Controls.Add(table);
            return pnl;
        }

        private void UpdateMode()
        {
            var isReplacement = rdoReplacement.Checked;
            if (pnlReplacementSection != null)
                pnlReplacementSection.Visible = isReplacement && _wizardStep == 1;
            // Temporary service only applies to Service Only; the replacement
            // path has its own temporary checkbox inside its section.
            if (chkTemporaryService != null)
                chkTemporaryService.Visible = !isReplacement;
        }

        private void UpdateOldItemMode()
        {
            var useUnlisted = rdoOldUnlisted.Checked;
            if (pnlOldListedStack != null) pnlOldListedStack.Visible = !useUnlisted;
            else cboOldItemListed.Visible = !useUnlisted;
            if(pnlOldItemUnlisted != null) pnlOldItemUnlisted.Visible = useUnlisted;
        }

        private void GoNext()
        {
            if (_wizardStep == 0)
            {
                _wizardStep = rdoReplacement.Checked ? 1 : 2;
                if (_wizardStep == 2)
                    CaptureServiceOnly();
                UpdateWizardStep();
                return;
            }

            if (_wizardStep == 1)
            {
                if (!ValidateAndCaptureReplacement())
                    return;

                _wizardStep = 2;
                UpdateWizardStep();
            }
        }

        private void GoBack()
        {
            if (_wizardStep <= 0)
                return;

            _wizardStep = _wizardStep == 2 && rdoServiceOnly.Checked ? 0 : _wizardStep - 1;
            UpdateWizardStep();
        }

        private void UpdateWizardStep()
        {
            var isReplacement = rdoReplacement.Checked;

            if (lblStepTitle != null)
            {
                lblStepTitle.Text = _wizardStep == 0
                    ? "Step 1 of 3 - Resolution type"
                    : _wizardStep == 1
                        ? "Step 2 of 3 - Old item / new item selection"
                        : "Step 3 of 3 - Confirmation and stock impact preview";
            }

            if (pnlTypeStep != null) pnlTypeStep.Visible = _wizardStep == 0;
            if (pnlReplacementSection != null) pnlReplacementSection.Visible = _wizardStep == 1 && isReplacement;
            if (pnlRemarksSection != null) pnlRemarksSection.Visible = _wizardStep == 0 || _wizardStep == 2;
            if (pnlPreviewSection != null) pnlPreviewSection.Visible = _wizardStep == 2;

            if (btnBack != null) btnBack.Enabled = _wizardStep > 0;
            if (btnNext != null) btnNext.Visible = _wizardStep < 2;
            if (btnConfirm != null) btnConfirm.Visible = _wizardStep == 2;
            AcceptButton = _wizardStep == 2 ? btnConfirm : btnNext;

            if (_wizardStep == 2)
            {
                if (isReplacement)
                    ValidateAndCaptureReplacement();
                else
                    CaptureServiceOnly();
                if (txtPreview != null)
                    txtPreview.Text = BuildPreviewText();
            }
        }

        private void RefreshPreviewIfNeeded()
        {
            if (_wizardStep != 2 || txtPreview == null)
                return;

            try
            {
                if (rdoReplacement.Checked)
                    ValidateAndCaptureReplacement();
                else
                    CaptureServiceOnly();
                txtPreview.Text = BuildPreviewText();
            }
            catch
            {
                // Preview refresh is best-effort while the operator is still editing.
            }
        }

        private void CaptureServiceOnly()
        {
            ResolutionType = "Service Only";
            OldItemId = null;
            NewItemId = null;
            Quantity = 0;
            Remarks = (txtRemarks.Text ?? string.Empty).Trim();
            IsTemporaryReplacement = chkTemporaryService != null && chkTemporaryService.Checked;
            UseUnlistedOldItem = false;
            OldItemConditionId = 0;
            OldItemConditionRemarks = null;
            OldItemRepairAction = null;
        }

        private bool ValidateAndCaptureReplacement()
        {
            if (_stockHardwareItems.Count == 0)
            {
                MessageBox.Show("No replacement stock items are available (Hardware with StockOnHand > 0).", "Mark As",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (cboNewItem.SelectedValue == null)
            {
                MessageBox.Show("Select a replacement item.", "Mark As", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (cboOldCondition.SelectedValue == null)
            {
                MessageBox.Show("Select an old item condition.", "Mark As", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            var newId = Convert.ToInt32(cboNewItem.SelectedValue);
            var conditionId = Convert.ToInt32(cboOldCondition.SelectedValue);
            var qty = (int)numQty.Value;

            var selectedNewItem = GetSelectedNewItem();
            if (selectedNewItem != null && selectedNewItem.StockOnHand < 1)
            {
                MessageBox.Show(
                    "The selected replacement item has no available stock.",
                    "Stock Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            if (selectedNewItem != null && qty > selectedNewItem.StockOnHand)
            {
                MessageBox.Show(
                    $"Quantity exceeds available stock.\r\nMake sure Expected: {selectedNewItem.StockOnHand} >= Requested: {qty}",
                    "Stock Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            var useUnlisted = rdoOldUnlisted.Checked;
            if (!useUnlisted)
            {
                if (cboOldItemListed.SelectedValue == null)
                {
                    MessageBox.Show("Select the replaced (old) item, or choose Not Listed.", "Mark As",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                var oldId = Convert.ToInt32(cboOldItemListed.SelectedValue);
                if (oldId == newId)
                {
                    MessageBox.Show("Replaced item and replacement item must be different.", "Mark As",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                OldItemId = oldId;
                UseUnlistedOldItem = false;
            }
            else
            {
                var name = (txtOldName.Text ?? string.Empty).Trim();
                var model = (txtOldModel.Text ?? string.Empty).Trim();
                var serial = (txtOldSerial.Text ?? string.Empty).Trim();
                var unit = cboOldUnit.SelectedItem?.ToString();
                var desc = (txtOldDescription.Text ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show("Old item name is required.", "Mark As", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(model))
                {
                    MessageBox.Show("Old item model number is required.", "Mark As", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(unit))
                {
                    MessageBox.Show("Old item unit of measure is required.", "Mark As", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                if (cboOldCategory.SelectedItem == null)
                {
                    MessageBox.Show("Old item category is required.", "Mark As", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                var cat = (ItemCategoryDto)cboOldCategory.SelectedItem;

                UseUnlistedOldItem = true;
                UnlistedOldItemName = name;
                UnlistedOldItemDescription = string.IsNullOrWhiteSpace(desc) ? null : desc;
                UnlistedOldItemCategoryId = cat.CategoryId;
                UnlistedOldItemCategoryName = cat.Name;
                UnlistedOldItemSerialNumber = string.IsNullOrWhiteSpace(serial) ? null : serial;
                UnlistedOldItemModelNumber = model;
                UnlistedOldItemUnitOfMeasure = unit;

                OldItemId = null;
            }

            ResolutionType = "Replacement";
            NewItemId = newId;
            Quantity = qty;
            Remarks = (txtRemarks.Text ?? string.Empty).Trim();
            IsTemporaryReplacement = chkTemporaryReplacement.Checked;
            OldItemConditionId = conditionId;
            OldItemConditionRemarks = (txtConditionRemarks.Text ?? string.Empty).Trim();
            OldItemRepairAction = GetSelectedOldItemRepairAction();
            return true;
        }

        private string BuildPreviewText()
        {
            if (!rdoReplacement.Checked)
            {
                return
                    "Resolution: " + (IsTemporaryReplacement ? "Temporary Service" : "Service Only") + "\r\n" +
                    "Status after save: " + (IsTemporaryReplacement ? "Resolved (Temporary)" : "Solved") + "\r\n\r\n" +
                    "Inventory impact: None\r\n\r\n" +
                    (IsTemporaryReplacement
                        ? "Follow-up:\r\n  This ticket remains Resolved (Temporary) until the service fix is confirmed permanent.\r\n\r\n"
                        : string.Empty) +
                    "Remarks:\r\n" + ((txtRemarks.Text ?? string.Empty).Trim().Length == 0 ? "(None)" : (txtRemarks.Text ?? string.Empty).Trim());
            }

            var oldItemText = UseUnlistedOldItem
                ? $"{UnlistedOldItemName} ({UnlistedOldItemModelNumber})"
                : (_outHardwareItems.FirstOrDefault(x => x != null && x.ItemId == (OldItemId ?? 0))?.DisplayText ?? "(Unknown)");

            var newItem = _stockHardwareItems.FirstOrDefault(x => x != null && x.ItemId == (NewItemId ?? 0));
            var newItemText = newItem?.DisplayText ?? "(Unknown)";
            var conditionText = _conditions.FirstOrDefault(c => c != null && c.ConditionId == OldItemConditionId)?.ConditionName ?? OldItemConditionId.ToString();
            var actionText = string.IsNullOrWhiteSpace(OldItemRepairAction) ? "Repaired" : OldItemRepairAction.Trim();
            var stockAfter = newItem == null ? (int?)null : newItem.StockOnHand - Quantity;
            var oldStockImpact = actionText.Equals("Unrepaired", StringComparison.OrdinalIgnoreCase) ? "0 (kept out of stock)" : "+" + Quantity;

            return
                "Resolution: " + (IsTemporaryReplacement ? "Temporary Replacement" : "Permanent Replacement") + "\r\n" +
                "Status after save: " + (IsTemporaryReplacement ? "Resolved (Temporary)" : "Solved") + "\r\n\r\n" +
                "Old item:\r\n" +
                "  " + oldItemText + "\r\n" +
                "  Condition: " + conditionText + "\r\n" +
                "  Action: " + actionText + "\r\n" +
                "  Stock impact: " + oldStockImpact + "\r\n\r\n" +
                "New item:\r\n" +
                "  " + newItemText + "\r\n" +
                "  Current stock: " + (newItem == null ? "-" : newItem.StockOnHand.ToString()) + "\r\n" +
                "  Stock impact: -" + Quantity + "\r\n" +
                "  Stock after save: " + (stockAfter.HasValue ? stockAfter.Value.ToString() : "-") + "\r\n\r\n" +
                "Follow-up:\r\n" +
                (IsTemporaryReplacement
                    ? "  This ticket remains Resolved (Temporary) until the replacement item is returned."
                    : "  No temporary return is required.") + "\r\n\r\n" +
                "Remarks:\r\n" + ((txtRemarks.Text ?? string.Empty).Trim().Length == 0 ? "(None)" : (txtRemarks.Text ?? string.Empty).Trim());
        }

        private void Confirm()
        {
            var isReplacement = rdoReplacement.Checked;
            var remarks = (txtRemarks.Text ?? string.Empty).Trim();

            if (!isReplacement)
            {
                CaptureServiceOnly();

                this.DialogResult = DialogResult.OK;
                this.Close();
                return;
            }

            if (_stockHardwareItems.Count == 0)
            {
                MessageBox.Show("No replacement stock items are available (Hardware with StockOnHand > 0).", "Mark As",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (cboNewItem.SelectedValue == null)
            {
                MessageBox.Show("Select a replacement item.", "Mark As", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cboOldCondition.SelectedValue == null)
            {
                MessageBox.Show("Select an old item condition.", "Mark As", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var newId = Convert.ToInt32(cboNewItem.SelectedValue);
            var conditionId = Convert.ToInt32(cboOldCondition.SelectedValue);
            var qty = (int)numQty.Value;

            var selectedNewItem = GetSelectedNewItem();
            if (selectedNewItem != null && selectedNewItem.StockOnHand < 1)
            {
                MessageBox.Show(
                    "The selected replacement item has no available stock.",
                    "Stock Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (selectedNewItem != null && qty > selectedNewItem.StockOnHand)
            {
                MessageBox.Show(
                    $"Quantity exceeds available stock.\r\nMake sure Expected: {selectedNewItem.StockOnHand} >= Requested: {qty}",
                    "Stock Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var useUnlisted = rdoOldUnlisted.Checked;
            if (!useUnlisted)
            {
                if (cboOldItemListed.SelectedValue == null)
                {
                    MessageBox.Show("Select the replaced (old) item, or choose Not Listed.", "Mark As",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var oldId = Convert.ToInt32(cboOldItemListed.SelectedValue);
                if (oldId == newId)
                {
                    MessageBox.Show("Replaced item and replacement item must be different.", "Mark As",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                OldItemId = oldId;
                UseUnlistedOldItem = false;
            }
            else
            {
                var name = (txtOldName.Text ?? string.Empty).Trim();
                var model = (txtOldModel.Text ?? string.Empty).Trim();
                var serial = (txtOldSerial.Text ?? string.Empty).Trim();
                var unit = cboOldUnit.SelectedItem?.ToString();
                var desc = (txtOldDescription.Text ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show("Old item name is required.", "Mark As", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(model))
                {
                    MessageBox.Show("Old item model number is required.", "Mark As", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(unit))
                {
                    MessageBox.Show("Old item unit of measure is required.", "Mark As", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (cboOldCategory.SelectedItem == null)
                {
                    MessageBox.Show("Old item category is required.", "Mark As", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var cat = (ItemCategoryDto)cboOldCategory.SelectedItem;

                UseUnlistedOldItem = true;
                UnlistedOldItemName = name;
                UnlistedOldItemDescription = string.IsNullOrWhiteSpace(desc) ? null : desc;
                UnlistedOldItemCategoryId = cat.CategoryId;
                UnlistedOldItemCategoryName = cat.Name;
                UnlistedOldItemSerialNumber = string.IsNullOrWhiteSpace(serial) ? null : serial;
                UnlistedOldItemModelNumber = model;
                UnlistedOldItemUnitOfMeasure = unit;

                OldItemId = null;
            }

            ResolutionType = "Replacement";
            NewItemId = newId;
            Quantity = qty;
            Remarks = remarks;
            IsTemporaryReplacement = chkTemporaryReplacement.Checked;
            OldItemConditionId = conditionId;
            OldItemConditionRemarks = (txtConditionRemarks.Text ?? string.Empty).Trim();
            OldItemRepairAction = GetSelectedOldItemRepairAction();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private bool ConfirmSummary()
        {
            var isReplacement = rdoReplacement.Checked;

            if (!isReplacement)
            {
                var tempService = chkTemporaryService != null && chkTemporaryService.Checked;
                var msg =
                    "Confirm Service Only resolution?\r\n\r\n" +
                    (tempService
                        ? "This will mark the ticket as Resolved (Temporary) without any inventory movement."
                        : "This will mark the ticket as Solved (or Closed) without any inventory movement.");
                return MessageBox.Show(msg, "Confirm Resolution", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            }

            var oldItemText = UseUnlistedOldItem
                ? $"{UnlistedOldItemName} ({UnlistedOldItemModelNumber})"
                : (_outHardwareItems.FirstOrDefault(x => x != null && x.ItemId == (OldItemId ?? 0))?.DisplayText ?? "(Unknown)");

            var newItemText = _stockHardwareItems.FirstOrDefault(x => x != null && x.ItemId == (NewItemId ?? 0))?.DisplayText ?? "(Unknown)";
            var conditionText = _conditions.FirstOrDefault(c => c != null && c.ConditionId == OldItemConditionId)?.ConditionName ?? OldItemConditionId.ToString();
            var actionText = string.IsNullOrWhiteSpace(OldItemRepairAction) ? "Repaired" : OldItemRepairAction.Trim();
            
            var message =
                "Confirm Replacement?\r\n\r\n" +
                $"IN:  {oldItemText} (Condition: {conditionText})\r\n" +
                $"Old Unit Action: {actionText}\r\n" +
                $"OUT: {newItemText} (Qty: {Quantity})\r\n\r\n" +
                $"Temporary: {(IsTemporaryReplacement ? "Yes" : "No")}";

            return MessageBox.Show(message, "Confirm Inventory Movement", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }
    }
}
