using System;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages.Category;
using Yakult.Inventory.App.Pages.Vendor;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Pages.Item
{
    // ... rest of the code remains the same ...
    public partial class BatchAddItemDialog : Form
    {
        private string _connectionString;
        private Label lblItemName, lblDescription, lblCategory, lblSerialNumbers, lblQuantity;
        private Label lblUnitOfMeasure, lblAmount, lblItemType, lblStartDate, lblEndDate;
        private Label lblCondition, lblVendor, lblRemarks; // NEW (added lblVendor)
        private Label lblWarrantyYears, lblDatePurchased; // WARRANTY + DATE PURCHASED
        private Label lblWarrantyInfo; // Display calculated warranty for Software/License
        private Label lblLicenseNumber; // LICENSE NUMBER
        private Label lblPartNumber; // PART NUMBER
        private Label lblCartridgeModel; // CARTRIDGE MODEL (shown only when Category = Cartridge)
        private Label lblCartridgeOrigin; // CARTRIDGE ORIGIN: Brand New / Refilled (shown only when Category = Cartridge)
        private ComboBox cmbCartridgeOrigin;
        private ReaLTaiizor.Controls.Panel _cardPanel;
        private TextBox txtItemName, txtDescription, txtSerialNumbers, txtModelNumber, txtAmount;
        private TextBox txtRemarks; // NEW
        private TextBox txtLicenseNumber; // LICENSE NUMBER
        private TextBox txtPartNumber; // PART NUMBER
        private NumericUpDown numWarrantyYears; // WARRANTY
        private NumericUpDown numQuantity; // QUANTITY for non-serialized items
        private DateTimePicker dtpDatePurchased; // DATE PURCHASED

        private ComboBox cmbCategory, cmbUnitOfMeasure, cmbItemType, cmbCondition, cmbVendor, cmbCartridgeModel; // NEW (added cmbVendor, cmbCartridgeModel)
        private DateTimePicker dtpStartDate, dtpEndDate; // NEW
        private CheckBox chkServiceContractDates; // NEW: allow Start/End dates for Services
        private Button btnAdd, btnDone, btnPreview, btnAddCategory, btnAddVendor, btnAddCartridgeModel; // NEW (added btnAddVendor, btnAddCartridgeModel)
        private CheckBox chkIsTrackedAsset; // FIXED ASSET checkbox
        private ListBox lstPreview;
        private Label lblPreviewTitle, lblInfo, lblSerialHelp;
        private int totalItemsAdded = 0;

        // ==================================================================================
        // CRITICAL: RUNTIME RE-ENTRY PROTECTION
        // This flag prevents the insert logic from executing more than once simultaneously.
        // It guards against: double-clicks, accidental multiple event registrations,
        // merge conflicts that duplicate code paths, and any other runtime re-entry.
        // ==================================================================================
        private volatile bool _isProcessing = false;

        // Track if BuildUI has been called to prevent accidental multiple initialization
        private bool _uiBuilt = false;

        private bool _suppressModelTextChanged = false;
        private bool _suppressComboBoxEvents = false;

        public BatchAddItemDialog()
        {
            System.Diagnostics.Debug.WriteLine($"[BatchAdd][CONSTRUCTOR] Initializing BatchAddItemDialog at {DateTime.Now:HH:mm:ss.fff}");

            _connectionString = DatabaseConfig.ConnectionString;

            //InitializeComponent();
            BuildUI();
            LoadCategories();
            LoadConditions(); // NEW: Load condition options
            LoadVendors(); // NEW: Load vendor options

            System.Diagnostics.Debug.WriteLine($"[BatchAdd][CONSTRUCTOR] Initialization complete");
        }

        private void BuildUI()
        {
            // ==================================================================================
            // DEFENSIVE: Prevent BuildUI from being called multiple times
            // This ensures event handlers are only registered once, preventing double-execution
            // ==================================================================================
            if (_uiBuilt)
            {
                System.Diagnostics.Debug.WriteLine($"[BatchAdd][BuildUI] WARNING: BuildUI already called - SKIPPING to prevent duplicate event handlers");
                return;
            }
            _uiBuilt = true;
            System.Diagnostics.Debug.WriteLine($"[BatchAdd][BuildUI] Building UI at {DateTime.Now:HH:mm:ss.fff}");

            Text = "Batch Add Items";
            Width = 1100;
            Height = 750;

            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = true;
            MinimizeBox = true;
            AutoScroll = true;
            BackColor = Color.FromArgb(245, 246, 250);

            // Info Label
            lblInfo = new Label
            {
                Text = "Add items with serial numbers (one per line) or use Quantity for bulk items. Serial numbers take priority if provided.",
                AutoSize = false,
                Width = 1000,
                Height = 40,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(60, 60, 60),
                Padding = new Padding(12, 8, 12, 8),
                BackColor = Color.FromArgb(230, 240, 255),
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 20)
            };

            // Core fields (instantiate BEFORE any sizing/layout)
            lblItemName = new Label { Text = "Item Name *", AutoSize = true };
            txtItemName = new TextBox();

            lblDescription = new Label { Text = "Description", AutoSize = true };
            txtDescription = new TextBox();

            lblItemType = new Label { Text = "Type *", AutoSize = true };
            cmbItemType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                MaxDropDownItems = 10
            };
            cmbItemType.Items.AddRange(new object[] { "Hardware", "Software/License", "Services" });
            cmbItemType.Tag = cmbItemType.Items.Cast<object>().ToList();
            if (cmbItemType.Items.Count > 0)
            {
                cmbItemType.SelectedIndex = 0;
            }

            lblStartDate = new Label { Text = "Start Date", AutoSize = true };
            dtpStartDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "MM/dd/yyyy" };

            lblEndDate = new Label { Text = "End Date", AutoSize = true };
            dtpEndDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "MM/dd/yyyy" };

            chkServiceContractDates = new CheckBox
            {
                Text = "Enable Start/End Date for Services",
                AutoSize = true,
                Checked = false,
                Visible = false
            };

            // Model Number
            var lblModelNumber = new Label { Text = "Model Number *", AutoSize = true };
            txtModelNumber = new TextBox();

            // Cartridge Model (+Add) (shown only when Category = Cartridge)
            lblCartridgeModel = new Label { Text = "Cartridge Model *", AutoSize = true };
            cmbCartridgeModel = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            btnAddCartridgeModel = new Button { Text = "Add", Width = 60, Height = 28 };

            // Cartridge Origin: Brand New / Refilled (shown only when Category = Cartridge)
            // Brand New = NULL RefillStatus (default, preserves existing behavior)
            // Refilled  = 'Available' RefillStatus (consistent with refill reception workflow)
            lblCartridgeOrigin = new Label { Text = "Origin *", AutoSize = true };
            cmbCartridgeOrigin = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCartridgeOrigin.Items.Add("Brand New");
            cmbCartridgeOrigin.Items.Add("Refilled");
            cmbCartridgeOrigin.SelectedIndex = 0; // Default to Brand New

            // Category (+Add)
            lblCategory = new Label { Text = "Category *", AutoSize = true };
            cmbCategory = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                MaxDropDownItems = 15,
                Sorted = false
            };
            btnAddCategory = new Button { Text = "Add", Width = 60, Height = 28 };

            // Quantity (for non-serialized items)
            lblQuantity = new Label { Text = "Quantity *", AutoSize = true };
            numQuantity = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 999999,
                Value = 1,
                Width = 120
            };

            // Serial Numbers
            lblSerialNumbers = new Label { Text = "Serial Numbers (Optional)", AutoSize = true };
            txtSerialNumbers = new TextBox
            {
                Height = 80,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                MinimumSize = new Size(600, 80)  // Ensure wider field for long serial numbers
            };
            lblSerialHelp = new Label
            {
                Text = "Enter one serial number per line. Leave empty to use Quantity instead.",
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 120, 120)
            };

            // Unit of Measure + Amount
            lblUnitOfMeasure = new Label { Text = "Unit of Measure *", AutoSize = true };
            cmbUnitOfMeasure = new ComboBox { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
            lblAmount = new Label { Text = "Amount", AutoSize = true, Width = 60 };
            txtAmount = new TextBox { Width = 80, Text = "0" };

            // Condition
            lblCondition = new Label { Text = "Condition *", AutoSize = true };
            cmbCondition = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

            // Vendor (+Add)
            lblVendor = new Label { Text = "Vendor", AutoSize = true };
            cmbVendor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            btnAddVendor = new Button { Text = "Add", Width = 60, Height = 28 };

            // Remarks
            lblRemarks = new Label { Text = "Remarks", AutoSize = true };
            txtRemarks = new TextBox();
            txtRemarks.Multiline = true;

            // Field row: License Number (toggled visible by item type)
            lblLicenseNumber = new Label { Text = "License Number", AutoSize = true };
            txtLicenseNumber = new TextBox();

            // Field row: Part Number (stored in dbo.Renewals)
            lblPartNumber = new Label { Text = "Part #", AutoSize = true };
            txtPartNumber = new TextBox { MaxLength = 100 };

            // Field row: Warranty (years input OR warranty info label)
            lblWarrantyYears = new Label { Text = "Warranty (Years)", AutoSize = true };
            numWarrantyYears = new NumericUpDown();
            lblWarrantyInfo = new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0)
            };

            // Field row: Date Purchased
            lblDatePurchased = new Label { Text = "Date Purchased", AutoSize = true };
            dtpDatePurchased = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MM/dd/yyyy",
                ShowCheckBox = true,
                Checked = false
            };

            // Field row: Is Tracked Asset (Fixed Asset) checkbox
            chkIsTrackedAsset = new CheckBox
            {
                Text = "Treat this item as a Fixed Asset",
                AutoSize = true,
                Checked = false,  // Default: will be set based on ItemType in CmbItemType_SelectedIndexChanged
                Font = new Font("Segoe UI", 9.5F),
                Padding = new Padding(0, 8, 0, 0)
            };
            var tooltipTrackedAsset = new ToolTip();
            tooltipTrackedAsset.SetToolTip(chkIsTrackedAsset, "Tracked assets are lifecycle-managed, not stock-managed.");

            // Preview section
            btnPreview = new Button
            {
                Text = "Preview Serial Numbers",
                Width = 200,
                Height = 32,
                Font = new Font("Segoe UI", 9.5F),
                Dock = DockStyle.Top,
                Margin = new Padding(0, 12, 0, 12)
            };
            lblPreviewTitle = new Label
            {
                Text = "Serial Numbers Preview:",
                AutoSize = false,
                Height = 24,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 4),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            lstPreview = new ListBox
            {
                Height = 180,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 0),
                Font = new Font("Consolas", 9.5F),
                BorderStyle = BorderStyle.FixedSingle
            };
            var previewStack = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0)
            };
            previewStack.Controls.Add(btnPreview);
            previewStack.Controls.Add(lblPreviewTitle);
            previewStack.Controls.Add(lstPreview);

            // Row composites (created AFTER controls)
            var categoryRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Margin = new Padding(0)
            };
            cmbCategory.Margin = new Padding(0, 0, 8, 0);
            btnAddCategory.Margin = new Padding(0, 0, 0, 0);
            categoryRow.Controls.Add(cmbCategory);
            categoryRow.Controls.Add(btnAddCategory);

            var serialStack = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = false,
                Margin = new Padding(0)
            };
            txtSerialNumbers.Margin = new Padding(0, 0, 0, 4);
            lblSerialHelp.Margin = new Padding(0, 0, 0, 0);
            serialStack.Controls.Add(txtSerialNumbers);
            serialStack.Controls.Add(lblSerialHelp);

            var uomAmountRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Margin = new Padding(0)
            };
            lblAmount.Margin = new Padding(12, 8, 8, 0);
            cmbUnitOfMeasure.Margin = new Padding(0);
            txtAmount.Margin = new Padding(0);
            uomAmountRow.Controls.Add(cmbUnitOfMeasure);
            uomAmountRow.Controls.Add(lblAmount);
            uomAmountRow.Controls.Add(txtAmount);

            var vendorRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Margin = new Padding(0)
            };
            cmbVendor.Margin = new Padding(0, 0, 8, 0);
            btnAddVendor.Margin = new Padding(0);
            vendorRow.Controls.Add(cmbVendor);
            vendorRow.Controls.Add(btnAddVendor);

            var cartridgeModelRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Margin = new Padding(0)
            };
            cmbCartridgeModel.Margin = new Padding(0, 0, 8, 0);
            btnAddCartridgeModel.Margin = new Padding(0);
            cartridgeModelRow.Controls.Add(cmbCartridgeModel);
            cartridgeModelRow.Controls.Add(btnAddCartridgeModel);

            var warrantyRow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Margin = new Padding(0)
            };
            warrantyRow.Controls.Add(numWarrantyYears);
            warrantyRow.Controls.Add(lblWarrantyInfo);

            // Bottom action buttons (Cancel | Add Batch | Done) aligned bottom-right
            var buttonBar = new FlowLayoutPanel
            {
                Dock = DockStyle.None,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 10, 0, 10),
                Margin = new Padding(0),
                Anchor = AnchorStyles.Right
            };
            btnAdd = new Button
            {
                Text = "Add Batch",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 42),
                Padding = new Padding(18, 0, 18, 0),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                Margin = new Padding(8, 0, 0, 0)
            };
            btnDone = new Button
            {
                Text = "Done",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 42),
                Padding = new Padding(18, 0, 18, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                Margin = new Padding(8, 0, 0, 0)
            };
            var btnCancel = new Button
            {
                Text = "Cancel",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 42),
                Padding = new Padding(18, 0, 18, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                Margin = new Padding(0, 0, 0, 0)
            };
            buttonBar.Controls.Add(btnDone);
            buttonBar.Controls.Add(btnAdd);
            buttonBar.Controls.Add(btnCancel);

            // --- Layout-only modernization: wrap all existing controls in a card + layout panels ---
            Controls.Clear();

            // Root layout: scrollable content + fixed bottom button row.
            // This keeps the action buttons aligned bottom-right even when the form is resized/maximized.
            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor,
                Padding = new Padding(24)
            };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(rootLayout);

            var scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BackColor,
                Padding = new Padding(0)
            };
            rootLayout.Controls.Add(scrollHost, 0, 0);

            _cardPanel = new ReaLTaiizor.Controls.Panel
            {
                BackColor = Color.White,
                EdgeColor = Color.FromArgb(220, 220, 220),
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(32, 24, 32, 32),
                SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality
            };
            scrollHost.Controls.Add(_cardPanel);

            // Top info/description line inside the card
            _cardPanel.Controls.Add(lblInfo);

            // Ensure consistent typography
            Font = new Font("Segoe UI", 9F);

            var formGrid = new TableLayoutPanel
            {
                ColumnCount = 4,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0),
                Margin = new Padding(0, 20, 0, 0)
            };
            // 2 sets of label-field columns for side-by-side layout
            formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F)); // Left label
            formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));    // Left field
            formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F)); // Right label
            formGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));    // Right field
            _cardPanel.Controls.Add(formGrid);

            void StyleLabel(Control c)
            {
                if (c is Label l)
                {
                    l.AutoSize = false;
                    l.Height = 32;
                    l.TextAlign = ContentAlignment.MiddleLeft;
                    l.ForeColor = Color.FromArgb(60, 60, 60);
                    l.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
                    l.Margin = new Padding(0, 6, 12, 6);
                }
            }

            void StyleField(Control c)
            {
                c.Dock = DockStyle.Fill;
                c.Margin = new Padding(0, 6, 20, 6);
                c.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

                if (c is FlowLayoutPanel flp)
                {
                    flp.Dock = DockStyle.Fill;
                    flp.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                }

                if (c is TextBox tb)
                {
                    tb.Font = new Font("Segoe UI", 9.5F);
                    if (tb.Multiline)
                    {
                        tb.MinimumSize = new Size(0, Math.Max(tb.Height, 72));
                        tb.ScrollBars = ScrollBars.Vertical;
                    }
                    else
                    {
                        tb.Height = 28;
                    }
                }

                if (c is ComboBox cmb)
                {
                    cmb.Font = new Font("Segoe UI", 9.5F);
                    cmb.Height = 28;
                }

                if (c is DateTimePicker dtp)
                {
                    dtp.Font = new Font("Segoe UI", 9.5F);
                    dtp.Height = 28;
                }

                if (c is NumericUpDown nud)
                {
                    nud.Font = new Font("Segoe UI", 9.5F);
                    nud.Height = 28;
                }

                if (c is ListBox lb)
                {
                    lb.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                    lb.MinimumSize = new Size(0, Math.Max(lb.Height, 110));
                }
            }

            void AddRow(Control label, Control field, int columnSpan = 1)
            {
                int row = formGrid.RowCount;
                formGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                formGrid.Controls.Add(label, 0, row);
                formGrid.Controls.Add(field, 1, row);
                if (columnSpan > 1)
                {
                    formGrid.SetColumnSpan(field, columnSpan);
                }
                formGrid.RowCount++;
                StyleLabel(label);
                StyleField(field);
            }

            void AddRowPair(Control label1, Control field1, Control label2, Control field2)
            {
                int row = formGrid.RowCount;
                formGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                formGrid.Controls.Add(label1, 0, row);
                formGrid.Controls.Add(field1, 1, row);
                formGrid.Controls.Add(label2, 2, row);
                formGrid.Controls.Add(field2, 3, row);
                formGrid.RowCount++;
                StyleLabel(label1);
                StyleField(field1);
                StyleLabel(label2);
                StyleField(field2);
            }

            // === SECTION 1: ITEM INFORMATION ===
            var sectionHeader1 = new Label
            {
                Text = "ITEM INFORMATION",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 215),
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 8)
            };
            formGrid.Controls.Add(sectionHeader1, 0, formGrid.RowCount);
            formGrid.SetColumnSpan(sectionHeader1, 4);
            formGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            formGrid.RowCount++;

            // Row: Item Name (spans all columns)
            AddRow(lblItemName, txtItemName, 3);

            // Row: Description (spans all columns, multiline)
            txtDescription.Height = 60;
            txtDescription.Multiline = true;
            AddRow(lblDescription, txtDescription, 3);

            // Row: Model Number (spans all columns)
            AddRow(lblModelNumber, txtModelNumber, 3);

            // Row: Cartridge Model (+Add) (shown only when Category = Cartridge, spans all columns)
            AddRow(lblCartridgeModel, cartridgeModelRow, 3);

            // Row: Cartridge Origin - Brand New / Refilled (shown only when Category = Cartridge)
            AddRow(lblCartridgeOrigin, cmbCartridgeOrigin, 3);

            // === SECTION 2: SERIAL NUMBERS ===
            var sectionHeader2 = new Label
            {
                Text = "SERIAL NUMBERS",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 215),
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 20, 0, 8)
            };
            formGrid.Controls.Add(sectionHeader2, 0, formGrid.RowCount);
            formGrid.SetColumnSpan(sectionHeader2, 4);
            formGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            formGrid.RowCount++;

            // Row: Quantity (for non-serialized items like Cartridges)
            AddRow(lblQuantity, numQuantity, 3);

            // Row: Serial Numbers (spans all columns)
            AddRow(lblSerialNumbers, serialStack, 3);

            // Row: Preview section (spans all columns)
            AddRow(new Label { Text = "" }, previewStack, 3);

            // === SECTION 3: CLASSIFICATION & STOCK ===
            var sectionHeader3 = new Label
            {
                Text = "CLASSIFICATION & STOCK",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 215),
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 20, 0, 8)
            };
            formGrid.Controls.Add(sectionHeader3, 0, formGrid.RowCount);
            formGrid.SetColumnSpan(sectionHeader3, 4);
            formGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            formGrid.RowCount++;

            // Row: Type | Category
            AddRowPair(lblItemType, cmbItemType, lblCategory, categoryRow);

            // Row: Services date toggle (shown only when ItemType = Services)
            AddRow(new Label { Text = "" }, chkServiceContractDates, 3);

            // Row: Start/End Date (toggled visible by item type)
            AddRowPair(lblStartDate, dtpStartDate, lblEndDate, dtpEndDate);

            // Row: License Number (toggled visible, full width)
            AddRow(lblLicenseNumber, txtLicenseNumber, 3);

            // Row: Condition | Unit of Measure + Amount
            AddRowPair(lblCondition, cmbCondition, lblUnitOfMeasure, uomAmountRow);

            // === SECTION 4: PURCHASE DETAILS ===
            var sectionHeader4 = new Label
            {
                Text = "PURCHASE DETAILS",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 215),
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 20, 0, 8)
            };
            formGrid.Controls.Add(sectionHeader4, 0, formGrid.RowCount);
            formGrid.SetColumnSpan(sectionHeader4, 4);
            formGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            formGrid.RowCount++;

            // Row: Vendor | Warranty
            AddRowPair(lblVendor, vendorRow, lblWarrantyYears, warrantyRow);

            // Row: Date Purchased | empty
            AddRowPair(lblDatePurchased, dtpDatePurchased, new Label { Text = "" }, new Panel());

            // Row: Part # (spans all columns)
            AddRow(lblPartNumber, txtPartNumber, 3);

            // Row: Is Tracked Asset checkbox (full width)
            AddRow(new Label { Text = "" }, chkIsTrackedAsset, 3);

            // === SECTION 5: REMARKS ===
            var sectionHeader5 = new Label
            {
                Text = "REMARKS (OPTIONAL)",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 100, 100),
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 20, 0, 8)
            };
            formGrid.Controls.Add(sectionHeader5, 0, formGrid.RowCount);
            formGrid.SetColumnSpan(sectionHeader5, 4);
            formGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            formGrid.RowCount++;

            // Row: Remarks (spans all columns)
            txtRemarks.Height = 60;
            AddRow(lblRemarks, txtRemarks, 3);

            // Ensure stacked preview controls stretch to the available width
            previewStack.Dock = DockStyle.Fill;
            previewStack.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            btnPreview.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            lblPreviewTitle.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            lstPreview.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            // Ensure serial stack stretches too
            serialStack.Dock = DockStyle.Fill;
            serialStack.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            txtSerialNumbers.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            // Ensure remarks stretches
            txtRemarks.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            // Bottom action buttons (Cancel | Add Batch | Done) aligned bottom-right
            buttonBar.Anchor = AnchorStyles.Right;
            buttonBar.Margin = new Padding(0, 14, 0, 0);
            rootLayout.Controls.Add(buttonBar, 0, 1);

            // ==================================================================================
            // CRITICAL: EVENT HANDLER REGISTRATION
            // This is the ONLY place where BtnAdd_Click should be registered.
            // We use -= before += to ensure it's only registered ONCE, even if this code
            // is executed multiple times due to merge conflicts or refactoring errors.
            // ==================================================================================
            System.Diagnostics.Debug.WriteLine($"[BatchAdd][BuildUI] Registering event handlers at {DateTime.Now:HH:mm:ss.fff}");

            cmbItemType.SelectedIndexChanged += CmbItemType_SelectedIndexChanged;
            cmbCategory.SelectedIndexChanged += CmbCategory_SelectedIndexChanged;
            cmbCartridgeModel.SelectedIndexChanged += CmbCartridgeModel_SelectedIndexChanged;
            txtModelNumber.TextChanged += TxtModelNumber_TextChanged;
            chkServiceContractDates.CheckedChanged += (s, e) => CmbItemType_SelectedIndexChanged(s, e);
            btnPreview.Click += BtnPreview_Click;
            btnAddCategory.Click += BtnAddCategory_Click;
            btnAddVendor.Click += BtnAddVendor_Click;
            btnAddCartridgeModel.Click += BtnAddCartridgeModel_Click;

            // CRITICAL: Defensive registration - remove before adding to guarantee single registration
            btnAdd.Click -= BtnAdd_Click;  // Safe even if not previously registered
            btnAdd.Click += BtnAdd_Click;
            System.Diagnostics.Debug.WriteLine($"[BatchAdd][BuildUI] *** btnAdd.Click handler registered - BtnAdd_Click is the ONLY save handler ***");

            btnDone.Click += (s, e) =>
            {
                if (totalItemsAdded > 0)
                    DialogResult = DialogResult.OK;
                else
                    DialogResult = DialogResult.Cancel;
            };
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

            System.Diagnostics.Debug.WriteLine($"[BatchAdd][BuildUI] Event handlers registered successfully");

            // Initialize visibility states
            // Cartridge Model and Origin should be hidden initially (no category selected)
            lblCartridgeModel.Visible = false;
            cmbCartridgeModel.Visible = false;
            btnAddCartridgeModel.Visible = false;
            lblCartridgeOrigin.Visible = false;
            cmbCartridgeOrigin.Visible = false;

            // Initialize state based on default item type
            CmbItemType_SelectedIndexChanged(cmbItemType, EventArgs.Empty);

            // ==========================================================================
            // UX FIX: Configure searchable ComboBox behavior for Type and Category
            // Fixes: Enter key commit, Escape close, mouse-wheel cursor bug,
            //        auto-open dropdown on typing, arrow key navigation
            // ==========================================================================
            ConfigureSearchableComboBox(cmbItemType, cmbCategory);
            ConfigureSearchableComboBox(cmbCategory, cmbCondition);
        }

        // ==========================================================================
        // P/Invoke: Fix WinForms mouse-wheel cursor-hide bug.
        // ShowCursor() is Win32 reference-counted: each ShowCursor(false) call
        // decrements an internal counter; cursor is visible only when counter >= 0.
        // A single ShowCursor(true) may not suffice if the counter was decremented
        // multiple times by rapid scroll events. We loop until counter >= 0.
        // ==========================================================================
        [DllImport("user32.dll")]
        private static extern int ShowCursor(bool bShow);

        // ==========================================================================
        // REUSABLE: Configure a ComboBox as a keyboard-searchable dropdown.
        //
        // ROOT CAUSE of previous failures:
        //   AutoCompleteMode.SuggestAppend is implemented via Windows Shell's
        //   IAutoComplete2 COM interface. This component installs its own message
        //   hook on the Edit child control and processes WM_KEYDOWN(VK_RETURN) at
        //   the NATIVE level — independently of, and potentially AFTER, the WinForms
        //   managed KeyDown event. On VK_RETURN the COM component fires a text-change
        //   notification (CBN_EDITCHANGE) that resets SelectedIndex to -1, undoing
        //   any commit made in managed KeyDown. This is why Enter was unreliable.
        //   Additionally, forcing DroppedDown = true while IAutoComplete2's own
        //   suggestion popup is active creates two competing dropdowns, causing
        //   further undefined behavior.
        //
        // FIX:
        //   Disable AutoCompleteMode entirely (set to None at construction time).
        //   Implement manual item filtering in TextUpdate using a backing list stored
        //   in cmb.Tag. This keeps everything in managed code, making behavior
        //   fully deterministic.
        // ==========================================================================
        private void ConfigureSearchableComboBox(ComboBox cmb, Control nextFocusControl)
        {
            // --- PreviewKeyDown: Ensure arrow/Enter/Escape treated as input keys ---
            cmb.PreviewKeyDown += (s, ev) =>
            {
                if (ev.KeyCode == Keys.Up || ev.KeyCode == Keys.Down ||
                    ev.KeyCode == Keys.Enter || ev.KeyCode == Keys.Escape)
                    ev.IsInputKey = true;
            };

            // --- KeyDown: Enter commits item, Escape closes dropdown ---
            // Without IAutoComplete2 interference, cmb.Text is exactly what the
            // user typed, and SelectedIndex is stable once set.
            cmb.KeyDown += (s, ev) =>
            {
                if (ev.KeyCode == Keys.Enter)
                {
                    string currentText = cmb.Text?.Trim();

                    // Close dropdown; DropDownClosed will restore filtered Items
                    if (cmb.DroppedDown)
                        cmb.DroppedDown = false;

                    if (!string.IsNullOrEmpty(currentText))
                    {
                        // Search the FULL backing list — not the potentially-filtered Items
                        var allItems = cmb.Tag as List<object>;
                        var searchList = allItems ?? cmb.Items.Cast<object>().ToList();

                        int matchIndex = -1;

                        // Pass 1: exact match (case-insensitive)
                        for (int i = 0; i < searchList.Count; i++)
                        {
                            if (string.Equals(cmb.GetItemText(searchList[i]), currentText,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                matchIndex = i;
                                break;
                            }
                        }

                        // Pass 2: StartsWith for partial input
                        if (matchIndex < 0)
                        {
                            for (int i = 0; i < searchList.Count; i++)
                            {
                                if (cmb.GetItemText(searchList[i]).StartsWith(currentText,
                                        StringComparison.OrdinalIgnoreCase))
                                {
                                    matchIndex = i;
                                    break;
                                }
                            }
                        }

                        if (matchIndex >= 0)
                        {
                            // Ensure the full list is in place before committing
                            RestoreComboItems(cmb);
                            if (cmb.SelectedIndex != matchIndex)
                                cmb.SelectedIndex = matchIndex;
                        }
                    }

                    nextFocusControl?.Focus();
                    ev.Handled = true;
                    ev.SuppressKeyPress = true;
                }
                else if (ev.KeyCode == Keys.Escape)
                {
                    if (cmb.DroppedDown)
                    {
                        cmb.DroppedDown = false;
                        ev.Handled = true;
                        ev.SuppressKeyPress = true;
                    }
                }
            };

            // --- MouseWheel: block scroll when not focused; fix cursor-disappearing bug ---
            cmb.MouseWheel += (s, ev) =>
            {
                if (!cmb.Focused) { ((HandledMouseEventArgs)ev).Handled = true; return; }
                while (ShowCursor(true) < 0) { }
                Cursor.Current = Cursors.Default;
            };

            // --- TextUpdate: Manual item filtering (replaces AutoCompleteMode) ---
            // TextUpdate fires only on user keystrokes, not programmatic text changes.
            // Filter cmb.Items from the full backing list (cmb.Tag), then show the
            // dropdown. _suppressComboBoxEvents prevents cascading SelectedIndexChanged
            // events (e.g., hiding/showing Cartridge Model row) while Items is being
            // rebuilt mid-keystroke.
            cmb.TextUpdate += (s, ev) =>
            {
                cmb.BeginInvoke((Action)(() =>
                {
                    if (!cmb.Focused) return;

                    var allItems = cmb.Tag as List<object>;
                    if (allItems == null) return;

                    string savedText = cmb.Text;

                    var filtered = string.IsNullOrEmpty(savedText)
                        ? allItems
                        : allItems
                            .Where(item => cmb.GetItemText(item).StartsWith(savedText,
                                StringComparison.OrdinalIgnoreCase))
                            .ToList();

                    _suppressComboBoxEvents = true;
                    try
                    {
                        cmb.Items.Clear();
                        foreach (var item in filtered)
                            cmb.Items.Add(item);
                    }
                    finally
                    {
                        _suppressComboBoxEvents = false;
                    }

                    cmb.Text = savedText;
                    cmb.SelectionStart = savedText.Length;
                    cmb.SelectionLength = 0;

                    if (!cmb.DroppedDown && filtered.Count > 0 && !string.IsNullOrEmpty(savedText))
                        cmb.DroppedDown = true;

                    // Re-assert after opening the dropdown: the native Win32 combo
                    // auto-selects the first matching item when DroppedDown=true fires,
                    // which overwrites the edit box text with a highlighted suggestion.
                    cmb.Text = savedText;
                    cmb.SelectionStart = savedText.Length;
                    cmb.SelectionLength = 0;
                }));
            };

            // --- DropDownClosed: Restore full Items list and commit selection ---
            // If Items were filtered when the dropdown closes, SelectedIndex refers
            // to a position in the filtered list — not the backing list. We must
            // restore Items first, then re-commit by object reference (for click) or
            // by exact text match (for keyboard entry without explicit selection).
            cmb.DropDownClosed += (s, ev) =>
            {
                var allItems = cmb.Tag as List<object>;
                if (allItems == null || cmb.Items.Count == allItems.Count) return;

                var selectedItem = cmb.SelectedItem;
                string currentText = cmb.Text?.Trim();

                _suppressComboBoxEvents = true;
                try
                {
                    cmb.Items.Clear();
                    foreach (var item in allItems)
                        cmb.Items.Add(item);
                }
                finally
                {
                    _suppressComboBoxEvents = false;
                }

                // Re-commit the exact object the user clicked (by reference)
                if (selectedItem != null)
                {
                    for (int i = 0; i < cmb.Items.Count; i++)
                    {
                        if (ReferenceEquals(cmb.Items[i], selectedItem))
                        {
                            if (cmb.SelectedIndex != i)
                                cmb.SelectedIndex = i;
                            return;
                        }
                    }
                }

                // No click selection — try to resolve typed text to exact match
                if (!string.IsNullOrWhiteSpace(currentText))
                {
                    for (int i = 0; i < cmb.Items.Count; i++)
                    {
                        if (string.Equals(cmb.GetItemText(cmb.Items[i]), currentText,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            cmb.SelectedIndex = i;
                            return;
                        }
                    }
                }
            };
        }

        // Restores a ComboBox's Items to its full backing list (stored in cmb.Tag).
        // Uses _suppressComboBoxEvents to prevent SelectedIndexChanged side effects
        // during the clear-and-rebuild. After this returns the flag is false again,
        // so the caller's subsequent SelectedIndex assignment fires events normally.
        private void RestoreComboItems(ComboBox cmb)
        {
            var allItems = cmb.Tag as List<object>;
            if (allItems == null || cmb.Items.Count == allItems.Count) return;

            _suppressComboBoxEvents = true;
            try
            {
                cmb.Items.Clear();
                foreach (var item in allItems)
                    cmb.Items.Add(item);
            }
            finally
            {
                _suppressComboBoxEvents = false;
            }
        }

        private void CmbCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressComboBoxEvents) return;

            // Show/hide Cartridge Model dropdown based on selected category
            var selectedCategory = cmbCategory.SelectedItem as CategoryItem;
            bool isCartridge = selectedCategory != null &&
                             string.Equals(selectedCategory.Name, "Cartridge", StringComparison.OrdinalIgnoreCase);

            lblCartridgeModel.Visible = isCartridge;
            cmbCartridgeModel.Visible = isCartridge;
            btnAddCartridgeModel.Visible = isCartridge;
            lblCartridgeOrigin.Visible = isCartridge;
            cmbCartridgeOrigin.Visible = isCartridge;

            // Default Origin to Brand New when Cartridge category is first selected
            if (isCartridge && cmbCartridgeOrigin.SelectedIndex < 0)
                cmbCartridgeOrigin.SelectedIndex = 0;

            // Load cartridge models when Cartridge category is selected
            if (isCartridge && cmbCartridgeModel.Items.Count == 0)
            {
                LoadCartridgeModels();
            }
        }

        private void CmbCartridgeModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedCategory = cmbCategory.SelectedItem as CategoryItem;
            bool isCartridge = selectedCategory != null &&
                             string.Equals(selectedCategory.Name, "Cartridge", StringComparison.OrdinalIgnoreCase);
            if (!isCartridge)
                return;

            var modelItem = cmbCartridgeModel.SelectedItem as CartridgeModelItem;
            if (modelItem == null)
                return;

            if (modelItem.CartridgeModelId <= 0)
                return;

            var modelNumber = modelItem.ModelNumber?.Trim();
            if (!string.IsNullOrWhiteSpace(modelNumber))
            {
                var currentText = txtModelNumber.Text?.Trim() ?? string.Empty;
                if (!string.Equals(currentText, modelNumber, StringComparison.OrdinalIgnoreCase))
                {
                    _suppressModelTextChanged = true;
                    try
                    {
                        txtModelNumber.Text = modelNumber;
                    }
                    finally
                    {
                        _suppressModelTextChanged = false;
                    }
                }
            }

            if (modelItem.VendorId.HasValue && modelItem.VendorId.Value > 0)
            {
                var currentVendor = cmbVendor.SelectedItem as VendorItem;
                bool vendorIsNone = currentVendor == null || currentVendor.VendorId <= 0;
                if (vendorIsNone)
                {
                    SelectVendorInCombo(modelItem.VendorId.Value);
                }
            }
        }

        private void TxtModelNumber_TextChanged(object sender, EventArgs e)
        {
            if (_suppressModelTextChanged)
                return;

            var selectedCategory = cmbCategory.SelectedItem as CategoryItem;
            bool isCartridge = selectedCategory != null &&
                             string.Equals(selectedCategory.Name, "Cartridge", StringComparison.OrdinalIgnoreCase);
            if (!isCartridge)
                return;

            var typed = txtModelNumber.Text?.Trim();
            if (string.IsNullOrWhiteSpace(typed))
                return;

            for (int i = 0; i < cmbCartridgeModel.Items.Count; i++)
            {
                if (cmbCartridgeModel.Items[i] is CartridgeModelItem item
                    && !string.IsNullOrWhiteSpace(item.ModelNumber)
                    && string.Equals(item.ModelNumber.Trim(), typed, StringComparison.OrdinalIgnoreCase))
                {
                    if (cmbCartridgeModel.SelectedIndex != i)
                        cmbCartridgeModel.SelectedIndex = i;
                    return;
                }
            }

            if (cmbCartridgeModel.Items.Count > 0 && cmbCartridgeModel.SelectedIndex != 0)
                cmbCartridgeModel.SelectedIndex = 0;
        }

        private void SelectVendorInCombo(int vendorId)
        {
            for (int i = 0; i < cmbVendor.Items.Count; i++)
            {
                if (cmbVendor.Items[i] is VendorItem v && v.VendorId == vendorId)
                {
                    cmbVendor.SelectedIndex = i;
                    return;
                }
            }
        }

        private void CmbItemType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressComboBoxEvents) return;

            var selectedType = cmbItemType.SelectedItem?.ToString();

            bool isSoftware = string.Equals(selectedType, "Software/License", StringComparison.OrdinalIgnoreCase);
            bool isServices = string.Equals(selectedType, "Services", StringComparison.OrdinalIgnoreCase);

            chkServiceContractDates.Visible = isServices;
            if (!isServices)
                chkServiceContractDates.Checked = false;

            // UI Polish: Disable Fixed Asset checkbox for Service items
            // Service items must NEVER generate inventory records
            if (isServices)
            {
                chkIsTrackedAsset.Checked = true;  // Force checked for Services
                chkIsTrackedAsset.Enabled = false;  // Disable to prevent user from unchecking
            }
            else
            {
                chkIsTrackedAsset.Enabled = true;  // Re-enable for Hardware/Software
                // Set default for IsTrackedAsset checkbox based on ItemType (user can override)
                // Software/License is tracked asset by default, Hardware is not
                chkIsTrackedAsset.Checked = isSoftware;
            }

            // Show/hide StartDate and EndDate for Software/License, or for Services when enabled by checkbox
            bool showContractDates = isSoftware || (isServices && chkServiceContractDates.Checked);
            lblStartDate.Visible = showContractDates;
            dtpStartDate.Visible = showContractDates;
            lblEndDate.Visible = showContractDates;
            dtpEndDate.Visible = showContractDates;

            // Show/hide License Number for Software/License and Services
            bool showLicenseNumber = isSoftware || isServices;
            lblLicenseNumber.Visible = showLicenseNumber;
            txtLicenseNumber.Visible = showLicenseNumber;

            // Show/hide warranty fields based on ItemType
            if (isSoftware)
            {
                // For Software/License: Hide warranty years input, show warranty info label
                lblWarrantyYears.Visible = false;
                numWarrantyYears.Visible = false;
                lblWarrantyInfo.Visible = true;

                // Set up date pickers
                // IMPORTANT: Allow back-encoding legacy inventories.
                // Do NOT force Start/End dates to today/future.
                dtpEndDate.MinDate = DateTimePicker.MinimumDateTime;

                // Keep a basic logical constraint: EndDate should be after StartDate.
                // (Still allows both dates in the past.)
                if (dtpEndDate.Value <= dtpStartDate.Value)
                {
                    dtpEndDate.Value = dtpStartDate.Value.AddDays(1);
                }

                // Wire up event handlers to update warranty display
                dtpStartDate.ValueChanged -= DtpLicenseDate_ValueChanged;
                dtpEndDate.ValueChanged -= DtpLicenseDate_ValueChanged;
                dtpStartDate.ValueChanged += DtpLicenseDate_ValueChanged;
                dtpEndDate.ValueChanged += DtpLicenseDate_ValueChanged;

                // Update warranty display immediately
                UpdateWarrantyDisplay();
            }
            else
            {
                // For Hardware/Services: Show warranty years input, hide warranty info label
                lblWarrantyYears.Visible = true;
                numWarrantyYears.Visible = true;
                lblWarrantyInfo.Visible = false;

                // Remove event handlers
                dtpStartDate.ValueChanged -= DtpLicenseDate_ValueChanged;
                dtpEndDate.ValueChanged -= DtpLicenseDate_ValueChanged;
            }

            // Basic logical constraint when contract dates are enabled (Software/License or Services with checkbox)
            if (dtpStartDate.Visible && dtpEndDate.Visible)
            {
                dtpEndDate.MinDate = DateTimePicker.MinimumDateTime;
                if (dtpEndDate.Value <= dtpStartDate.Value)
                    dtpEndDate.Value = dtpStartDate.Value.AddDays(1);
            }

            cmbUnitOfMeasure.Items.Clear();

            if (isSoftware)
            {
                cmbUnitOfMeasure.Items.AddRange(new object[] { "Monthly", "Annually", "One Time" });
            }
            else if (isServices)
            {
                cmbUnitOfMeasure.Items.AddRange(new object[] { "Contract" });
            }
            else
            {
                cmbUnitOfMeasure.Items.AddRange(new object[] { "Unit", "Piece", "Box", "Pack", "Set" });
            }

            if (cmbUnitOfMeasure.Items.Count > 0)
            {
                cmbUnitOfMeasure.SelectedIndex = 0;
            }
        }

        private void LoadCategories()
        {
            try
            {
                cmbCategory.Items.Clear();

                string connectionString = DatabaseConfig.ConnectionString;

                using (var con = new System.Data.SqlClient.SqlConnection(connectionString))
                {
                    con.Open();
                    string query = "SELECT CategoryId, Name FROM dbo.ItemCategory WHERE Active = 1 ORDER BY Name";

                    using (var cmd = new System.Data.SqlClient.SqlCommand(query, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbCategory.Items.Add(new CategoryItem
                            {
                                CategoryId = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }
                // Update backing list used by manual filtering in ConfigureSearchableComboBox
                cmbCategory.Tag = cmbCategory.Items.Cast<object>().ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load categories: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadConditions()
        {
            try
            {
                cmbCondition.Items.Clear();

                string connectionString = DatabaseConfig.ConnectionString;

                using (var con = new System.Data.SqlClient.SqlConnection(connectionString))
                {
                    con.Open();
                    // Simplified query - just get all conditions
                    string query = "SELECT ConditionId, ConditionName FROM dbo.Condition ORDER BY ConditionId";

                    using (var cmd = new System.Data.SqlClient.SqlCommand(query, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbCondition.Items.Add(new ConditionItem
                            {
                                ConditionId = reader.GetInt32(0),
                                ConditionName = reader.GetString(1)
                            });
                        }
                    }
                }

                // Default to "Good" if available
                for (int i = 0; i < cmbCondition.Items.Count; i++)
                {
                    if (cmbCondition.Items[i] is ConditionItem item &&
                        item.ConditionName.Equals("Good", StringComparison.OrdinalIgnoreCase))
                    {
                        cmbCondition.SelectedIndex = i;
                        break;
                    }
                }

                // If "Good" not found, select first item
                if (cmbCondition.SelectedIndex < 0 && cmbCondition.Items.Count > 0)
                {
                    cmbCondition.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load conditions: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadVendors()
        {
            try
            {
                cmbVendor.Items.Clear();
                cmbVendor.Items.Add(new VendorItem { VendorId = 0, VendorName = "(None)" });

                string connectionString = DatabaseConfig.ConnectionString;

                using (var con = new System.Data.SqlClient.SqlConnection(connectionString))
                {
                    con.Open();
                    // Get active vendors that are not archived
                    string query = @"
                        SELECT v.VendorID, v.VendorName
                        FROM dbo.Vendor v
                        LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Vendor' AND arc.EntityId = v.VendorID
                        WHERE v.IsActive = 1 AND arc.ArchiveId IS NULL
                        ORDER BY v.VendorName";

                    using (var cmd = new System.Data.SqlClient.SqlCommand(query, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbVendor.Items.Add(new VendorItem
                            {
                                VendorId = reader.GetInt32(0),
                                VendorName = reader.GetString(1)
                            });
                        }
                    }
                }

                // Default to "(None)"
                if (cmbVendor.Items.Count > 0)
                {
                    cmbVendor.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load vendors: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void LoadCartridgeModels(string selectModelNumber = null, int? selectVendorId = null)
        {
            try
            {
                cmbCartridgeModel.Items.Clear();
                cmbCartridgeModel.Enabled = false;  // Disable while loading

                cmbCartridgeModel.Items.Add(new CartridgeModelItem
                {
                    CartridgeModelId = 0,
                    ModelNumber = "(Select cartridge model)",
                    VendorName = null,
                    VendorId = null
                });

                var cartridgeModelRepo = new CartridgeModelRepository();
                var models = await cartridgeModelRepo.GetAllActiveModelsAsync();

                foreach (var model in models)
                {
                    cmbCartridgeModel.Items.Add(new CartridgeModelItem
                    {
                        CartridgeModelId = model.CartridgeModelId,
                        ModelNumber = model.ModelNumber,
                        VendorName = model.VendorName,
                        VendorId = model.VendorId
                    });
                }

                if (!string.IsNullOrWhiteSpace(selectModelNumber))
                {
                    for (int i = 0; i < cmbCartridgeModel.Items.Count; i++)
                    {
                        if (cmbCartridgeModel.Items[i] is CartridgeModelItem item
                            && !string.IsNullOrWhiteSpace(item.ModelNumber)
                            && string.Equals(item.ModelNumber.Trim(), selectModelNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            cmbCartridgeModel.SelectedIndex = i;
                            break;
                        }
                    }
                }

                if (cmbCartridgeModel.SelectedIndex < 0 && cmbCartridgeModel.Items.Count > 0)
                    cmbCartridgeModel.SelectedIndex = 0;

                if (selectVendorId.HasValue && selectVendorId.Value > 0)
                {
                    SelectVendorInCombo(selectVendorId.Value);
                }

                cmbCartridgeModel.Enabled = true;  // Re-enable after loading
            }
            catch (Exception ex)
            {
                cmbCartridgeModel.Enabled = true;  // Re-enable on error
                MessageBox.Show($"Failed to load cartridge models: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAddCategory_Click(object sender, EventArgs e)
        {
            var dialog = new QuickAddCategory();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                LoadCategories();

                // Select the newly added category
                for (int i = 0; i < cmbCategory.Items.Count; i++)
                {
                    if (cmbCategory.Items[i] is CategoryItem item && item.Name == dialog.NewCategoryName)
                    {
                        cmbCategory.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void BtnAddVendor_Click(object sender, EventArgs e)
        {
            var dialog = new QuickAddVendorDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                LoadVendors();

                // Select the newly added vendor
                if (dialog.NewVendorId.HasValue)
                {
                    for (int i = 0; i < cmbVendor.Items.Count; i++)
                    {
                        if (cmbVendor.Items[i] is VendorItem item && item.VendorId == dialog.NewVendorId.Value)
                        {
                            cmbVendor.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }

        private async void BtnAddCartridgeModel_Click(object sender, EventArgs e)
        {
            // Inline dialog creation to avoid separate class file issues
            using (var dialog = CreateQuickAddCartridgeModelDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (dialog.Tag is QuickAddCartridgeModelResult result)
                        LoadCartridgeModels(result.ModelNumber);
                    else
                        LoadCartridgeModels();
                }
            }
        }

        private Form CreateQuickAddCartridgeModelDialog()
        {
            var dialog = new Form
            {
                Text = "Add Cartridge Model",
                Width = 450,
                Height = 320,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(20)
            };
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            dialog.Controls.Add(mainPanel);

            // Model Number
            var lblModelNumber = new Label { Text = "Model Number *", Dock = DockStyle.Fill };
            var txtModelNumber = new TextBox { Dock = DockStyle.Fill };
            mainPanel.Controls.Add(lblModelNumber, 0, 0);
            mainPanel.Controls.Add(txtModelNumber, 1, 0);

            if (this.txtModelNumber != null)
            {
                var initial = this.txtModelNumber.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(initial))
                    txtModelNumber.Text = initial;
            }

            // Is Requestable
            var chkRequestable = new CheckBox { Text = "Is Requestable", Checked = true, Dock = DockStyle.Fill };
            mainPanel.Controls.Add(new Label(), 0, 1);
            mainPanel.Controls.Add(chkRequestable, 1, 1);

            // Is Refillable
            var chkRefillable = new CheckBox { Text = "Is Refillable", Checked = true, Dock = DockStyle.Fill };
            mainPanel.Controls.Add(new Label(), 0, 2);
            mainPanel.Controls.Add(chkRefillable, 1, 2);

            // Buttons
            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true
            };

            var btnSave = new Button
            {
                Text = "Save",
                Width = 80,
                DialogResult = DialogResult.None
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Width = 80,
                DialogResult = DialogResult.Cancel
            };

            btnSave.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtModelNumber.Text))
                {
                    MessageBox.Show("Please enter a model number.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    btnSave.Enabled = false;
                    btnSave.Text = "Saving...";

                    var repository = new CartridgeModelRepository();
                    var existing = await repository.FindByModelNumberAsync(txtModelNumber.Text.Trim());
                    if (existing != null)
                    {
                        MessageBox.Show($"Model number '{txtModelNumber.Text.Trim()}' already exists.",
                            "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        btnSave.Enabled = true;
                        btnSave.Text = "Save";
                        return;
                    }

                    var model = new CartridgeModelDto
                    {
                        ModelNumber = txtModelNumber.Text.Trim(),
                        IsRequestable = chkRequestable.Checked,
                        IsRefillable = chkRefillable.Checked,
                        IsActive = true,
                        CreatedBy = AppSession.CurrentUserId,
                        CreatedAt = DateTime.Now
                    };

                    var newId = await repository.CreateAsync(model);
                    MessageBox.Show("Cartridge model added successfully!", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    dialog.Tag = new QuickAddCartridgeModelResult
                    {
                        CartridgeModelId = newId,
                        ModelNumber = model.ModelNumber
                    };

                    dialog.DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnSave.Enabled = true;
                    btnSave.Text = "Save";
                }
            };

            buttonPanel.Controls.Add(btnSave);
            buttonPanel.Controls.Add(btnCancel);
            mainPanel.Controls.Add(buttonPanel, 0, 3);
            mainPanel.SetColumnSpan(buttonPanel, 2);

            dialog.AcceptButton = btnSave;
            dialog.CancelButton = btnCancel;

            return dialog;
        }

        private void BtnPreview_Click(object sender, EventArgs e)
        {
            var validationResult = ValidateInput();
            if (!validationResult.IsValid)
            {
                MessageBox.Show(validationResult.ErrorMessage, "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            lstPreview.Items.Clear();

            if (validationResult.IsNonSerialized)
            {
                // Preview for non-serialized items
                lstPreview.Items.Add($"Items to be created: 1");
                lstPreview.Items.Add($"Item Name: {txtItemName.Text}");
                lstPreview.Items.Add($"Category: {cmbCategory.Text}");
                lstPreview.Items.Add($"Quantity: {validationResult.Quantity}");
                lstPreview.Items.Add($"StockOnHand: {validationResult.Quantity}");
                lstPreview.Items.Add("");
                lstPreview.Items.Add("One item record will be created with the specified quantity.");
            }
            else
            {
                // Preview for serialized items
                var serialNumbers = validationResult.SerialNumbers;

                lstPreview.Items.Add($"Items to be created: {serialNumbers.Length}");
                lstPreview.Items.Add($"Item Name: {txtItemName.Text}");
                lstPreview.Items.Add($"Category: {cmbCategory.Text}");
                lstPreview.Items.Add("");
                lstPreview.Items.Add("Serial Numbers:");
                lstPreview.Items.Add("────────────────────────────────────");

                for (int i = 0; i < Math.Min(serialNumbers.Length, 20); i++)
                {
                    lstPreview.Items.Add($"  {i + 1,3}. {serialNumbers[i]}");
                }

                if (serialNumbers.Length > 20)
                {
                    lstPreview.Items.Add($"  ... and {serialNumbers.Length - 20} more");
                }
            }

            lblPreviewTitle.Visible = true;
            lstPreview.Visible = true;
        }

        /// <summary>
        /// SINGLE ENTRY POINT for batch add operation.
        /// This method is called ONLY from the btnAdd.Click event.
        /// It contains bulletproof re-entry protection to ensure the insert logic
        /// executes EXACTLY ONCE per user click, preventing SQL duplicate errors.
        /// </summary>
        private void BtnAdd_Click(object sender, EventArgs e)
        {
            // ==================================================================================
            // ENTRY POINT - Log every time this method is entered
            // If you see this log twice in a row, there's a runtime double-execution bug
            // ==================================================================================
            var entryTime = DateTime.Now;
            System.Diagnostics.Debug.WriteLine($"");
            System.Diagnostics.Debug.WriteLine($"╔════════════════════════════════════════════════════════════════════════════╗");
            System.Diagnostics.Debug.WriteLine($"║ [BatchAdd] BtnAdd_Click ENTER {entryTime:HH:mm:ss.fff}");
            System.Diagnostics.Debug.WriteLine($"║ Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            System.Diagnostics.Debug.WriteLine($"╚════════════════════════════════════════════════════════════════════════════╝");

            // ==================================================================================
            // LAYER 1: ATOMIC RE-ENTRY CHECK
            // This MUST be the first check to prevent ANY duplicate execution
            // Uses lock() to ensure thread-safe check-and-set operation
            // ==================================================================================
            lock (this)
            {
                if (_isProcessing)
                {
                    System.Diagnostics.Debug.WriteLine($"[BatchAdd] ⛔ RE-ENTRY BLOCKED - _isProcessing flag is TRUE");
                    System.Diagnostics.Debug.WriteLine($"[BatchAdd] ⛔ This prevents double-execution from double-clicks or duplicate event wiring");
                    System.Diagnostics.Debug.WriteLine($"[BatchAdd] BtnAdd_Click EXIT (blocked)");
                    return;
                }
                // Atomic set - from this point, any other call will be blocked
                _isProcessing = true;
                System.Diagnostics.Debug.WriteLine($"[BatchAdd] ✓ Re-entry guard SET - _isProcessing = true");
            }

            // ==================================================================================
            // LAYER 2: BUTTON STATE CHECK (Defensive)
            // Additional safety check - button should be enabled when clicked
            // ==================================================================================
            if (!btnAdd.Enabled)
            {
                System.Diagnostics.Debug.WriteLine($"[BatchAdd] ⚠ WARNING: Button already disabled - clearing flag and exiting");
                _isProcessing = false;
                return;
            }

            // ==================================================================================
            // LAYER 3: IMMEDIATE BUTTON DISABLE
            // Prevent UI-level double-clicks from reaching this code again
            // ==================================================================================
            btnAdd.Enabled = false;
            btnAdd.Text = "Processing...";
            System.Diagnostics.Debug.WriteLine($"[BatchAdd] ✓ Button DISABLED - UI-level double-clicks now blocked");

            try
            {
                // ==================================================================================
                // VALIDATION PHASE
                // ==================================================================================
                System.Diagnostics.Debug.WriteLine($"[BatchAdd] Validating input...");

                var validationResult = ValidateInput();
                if (!validationResult.IsValid)
                {
                    System.Diagnostics.Debug.WriteLine($"[BatchAdd] ❌ Validation FAILED: {validationResult.ErrorMessage}");
                    MessageBox.Show(validationResult.ErrorMessage, "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var serialNumbers = validationResult.SerialNumbers;
                bool isNonSerialized = validationResult.IsNonSerialized;
                int quantity = isNonSerialized ? validationResult.Quantity : serialNumbers.Length;

                System.Diagnostics.Debug.WriteLine($"[BatchAdd] ✓ Validation SUCCESS - IsNonSerialized={isNonSerialized}, Quantity={quantity}");
                if (!isNonSerialized)
                {
                    System.Diagnostics.Debug.WriteLine($"[BatchAdd] Serials to insert: {string.Join(", ", serialNumbers)}");
                }

                // ==================================================================================
                // USER CONFIRMATION
                // Temporarily re-enable button for confirmation dialog (user might cancel)
                // ==================================================================================
                btnAdd.Enabled = true;
                btnAdd.Text = "Add Batch";

                var confirmCategorySelected = cmbCategory.SelectedItem as CategoryItem;
                bool confirmIsCartridge = confirmCategorySelected != null &&
                    string.Equals(confirmCategorySelected.Name, "Cartridge", StringComparison.OrdinalIgnoreCase);
                string cartridgeOriginLine = confirmIsCartridge
                    ? $"Origin: {cmbCartridgeOrigin.SelectedItem}\n"
                    : string.Empty;

                string confirmMessage;
                if (isNonSerialized)
                {
                    confirmMessage = $"Add 1 item with quantity {quantity}?\n\n" +
                        $"Item: {txtItemName.Text}\n" +
                        $"Category: {cmbCategory.Text}\n" +
                        cartridgeOriginLine +
                        $"Quantity: {quantity}\n" +
                        $"StockOnHand will be set to {quantity}\n\n" +
                        "Continue?";
                }
                else
                {
                    confirmMessage = $"Add {quantity} items with unique serial numbers?\n\n" +
                        $"Item: {txtItemName.Text}\n" +
                        $"Category: {cmbCategory.Text}\n" +
                        cartridgeOriginLine +
                        $"Serial Numbers: {quantity} unique items\n" +
                        $"Each item will have StockOnHand = 1\n\n" +
                        "Continue?";
                }

                var result = MessageBox.Show(
                    confirmMessage,
                    "Confirm Batch Add",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                {
                    System.Diagnostics.Debug.WriteLine($"[BatchAdd] User CANCELLED - operation aborted");
                    System.Diagnostics.Debug.WriteLine($"[BatchAdd] BtnAdd_Click EXIT (user cancelled)");
                    return;
                }

                // ==================================================================================
                // INSERT PHASE - THIS IS THE SINGLE POINT WHERE DATABASE INSERTS HAPPEN
                // ==================================================================================
                btnAdd.Enabled = false;
                btnAdd.Text = "Adding...";
                Cursor = Cursors.WaitCursor;

                System.Diagnostics.Debug.WriteLine($"");
                System.Diagnostics.Debug.WriteLine($"[BatchAdd] ▶▶▶ Insert START at {DateTime.Now:HH:mm:ss.fff}");
                System.Diagnostics.Debug.WriteLine($"[BatchAdd] Calling BatchAddItems() - THE ONLY insert method");

                var addResult = BatchAddItems(serialNumbers, isNonSerialized, quantity);

                System.Diagnostics.Debug.WriteLine($"[BatchAdd] ◀◀◀ Insert COMPLETE at {DateTime.Now:HH:mm:ss.fff}");
                System.Diagnostics.Debug.WriteLine($"[BatchAdd] Results: {addResult.SuccessCount} success, {addResult.FailedSerials.Count} failed");

                if (addResult.FailedSerials.Any())
                {
                    // Build summary message
                    var summaryLines = new List<string>();
                    summaryLines.Add($"Successfully added {addResult.SuccessCount} items.");
                    summaryLines.Add("");
                    summaryLines.Add($"Failed to add {addResult.FailedSerials.Count} items:");

                    // Show up to 15 failed serials
                    int displayCount = Math.Min(15, addResult.FailedSerials.Count);
                    for (int i = 0; i < displayCount; i++)
                    {
                        summaryLines.Add($"  • {addResult.FailedSerials[i]}");
                    }

                    if (addResult.FailedSerials.Count > displayCount)
                    {
                        summaryLines.Add($"  ... and {addResult.FailedSerials.Count - displayCount} more");
                    }

                    MessageBox.Show(
                        string.Join("\n", summaryLines),
                        "Partial Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    // Update total count only with successful items
                    totalItemsAdded += addResult.SuccessCount;
                }
                else
                {
                    totalItemsAdded += addResult.SuccessCount;

                    var continueResult = MessageBox.Show(
                        $"Successfully added {addResult.SuccessCount} items!\n\n" +
                        $"Total items added this session: {totalItemsAdded}\n\n" +
                        $"Add another batch?",
                        "Success",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (continueResult == DialogResult.Yes)
                    {
                        ClearForm();
                    }
                    else
                    {
                        DialogResult = DialogResult.OK;
                    }
                }
            }
            catch (Exception ex)
            {
                // ==================================================================================
                // EXCEPTION HANDLING
                // Any unexpected exception during the operation is logged here
                // ==================================================================================
                System.Diagnostics.Debug.WriteLine($"");
                System.Diagnostics.Debug.WriteLine($"[BatchAdd] ❌❌❌ EXCEPTION CAUGHT ❌❌❌");
                System.Diagnostics.Debug.WriteLine($"[BatchAdd] Exception Type: {ex.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"[BatchAdd] Exception Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[BatchAdd] Stack Trace:");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[BatchAdd] Inner Exception: {ex.InnerException.Message}");
                }

                MessageBox.Show(
                    $"Error adding items: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                // ==================================================================================
                // CLEANUP - ALWAYS EXECUTED
                // This ensures the button and flag are reset even if an exception occurs
                // ==================================================================================
                Cursor = Cursors.Default;
                btnAdd.Enabled = true;
                btnAdd.Text = "Add Batch";
                _isProcessing = false;

                System.Diagnostics.Debug.WriteLine($"");
                System.Diagnostics.Debug.WriteLine($"[BatchAdd] ✓ CLEANUP: Button re-enabled, cursor restored");
                System.Diagnostics.Debug.WriteLine($"[BatchAdd] ✓ CLEANUP: _isProcessing flag cleared (set to false)");
                System.Diagnostics.Debug.WriteLine($"[BatchAdd] BtnAdd_Click EXIT at {DateTime.Now:HH:mm:ss.fff}");
                System.Diagnostics.Debug.WriteLine($"╔════════════════════════════════════════════════════════════════════════════╗");
                System.Diagnostics.Debug.WriteLine($"║ [BatchAdd] END OF OPERATION");
                System.Diagnostics.Debug.WriteLine($"╚════════════════════════════════════════════════════════════════════════════╝");
                System.Diagnostics.Debug.WriteLine($"");
            }
        }

        private ValidationResult ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(txtItemName.Text))
            {
                txtItemName.Focus();
                return new ValidationResult { IsValid = false, ErrorMessage = "Please enter an item name." };
            }

            if (string.IsNullOrWhiteSpace(txtModelNumber.Text))
            {
                txtModelNumber.Focus();
                return new ValidationResult { IsValid = false, ErrorMessage = "Please enter a model number." };
            }

            // For DropDown-style ComboBox, typed text may match a valid item
            // even when SelectedIndex == -1. Try to resolve before rejecting.
            if (cmbCategory.SelectedIndex < 0)
            {
                bool resolved = false;
                if (!string.IsNullOrWhiteSpace(cmbCategory.Text))
                {
                    string typed = cmbCategory.Text.Trim();
                    for (int i = 0; i < cmbCategory.Items.Count; i++)
                    {
                        string itemText = cmbCategory.GetItemText(cmbCategory.Items[i]);
                        if (string.Equals(itemText, typed, StringComparison.OrdinalIgnoreCase))
                        {
                            cmbCategory.SelectedIndex = i;
                            resolved = true;
                            break;
                        }
                    }
                }
                if (!resolved)
                {
                    cmbCategory.Focus();
                    return new ValidationResult { IsValid = false, ErrorMessage = "Please select a category." };
                }
            }

            // For DropDown-style ComboBox, typed text may match a valid item
            // even when SelectedIndex == -1. Try to resolve before rejecting.
            if (cmbItemType.SelectedIndex < 0)
            {
                bool resolved = false;
                if (!string.IsNullOrWhiteSpace(cmbItemType.Text))
                {
                    string typed = cmbItemType.Text.Trim();
                    for (int i = 0; i < cmbItemType.Items.Count; i++)
                    {
                        string itemText = cmbItemType.GetItemText(cmbItemType.Items[i]);
                        if (string.Equals(itemText, typed, StringComparison.OrdinalIgnoreCase))
                        {
                            cmbItemType.SelectedIndex = i;
                            resolved = true;
                            break;
                        }
                    }
                }
                if (!resolved)
                {
                    cmbItemType.Focus();
                    return new ValidationResult { IsValid = false, ErrorMessage = "Please select an item type." };
                }
            }

            // Validate Cartridge Model and Origin if Category is Cartridge
            var selectedCategory = cmbCategory.SelectedItem as CategoryItem;
            bool isCartridge = selectedCategory != null &&
                             string.Equals(selectedCategory.Name, "Cartridge", StringComparison.OrdinalIgnoreCase);
            if (isCartridge && cmbCartridgeModel.SelectedIndex <= 0)
            {
                cmbCartridgeModel.Focus();
                return new ValidationResult { IsValid = false, ErrorMessage = "Please select a cartridge model." };
            }
            if (isCartridge && cmbCartridgeOrigin.SelectedIndex < 0)
            {
                cmbCartridgeOrigin.Focus();
                return new ValidationResult { IsValid = false, ErrorMessage = "Please select whether the cartridge is Brand New or Refilled." };
            }

            if (cmbCondition.SelectedIndex < 0)
            {
                cmbCondition.Focus();
                return new ValidationResult { IsValid = false, ErrorMessage = "Please select a condition." };
            }

            // PRIORITY LOGIC: Serial Numbers take priority if provided, Quantity is fallback
            System.Diagnostics.Debug.WriteLine($"[BatchAdd][ValidateInput] Starting validation");

            // STEP 1: Parse serial numbers
            var splitLines = txtSerialNumbers.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            System.Diagnostics.Debug.WriteLine($"[BatchAdd][ValidateInput] After split: {splitLines.Length} lines");

            // STEP 2: NORMALIZE - Trim whitespace and convert to uppercase for consistency
            var normalizedSerials = splitLines.Select(s => s.Trim().ToUpperInvariant()).ToList();
            System.Diagnostics.Debug.WriteLine($"[BatchAdd][ValidateInput] After normalize (trim + uppercase): {normalizedSerials.Count} items");

            // STEP 3: Filter out empty strings
            var nonEmptySerials = normalizedSerials.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            System.Diagnostics.Debug.WriteLine($"[BatchAdd][ValidateInput] After removing empty: {nonEmptySerials.Count} items");

            // STEP 4: DEDUPLICATE - Remove in-memory duplicates (case-insensitive)
            var uniqueSerials = nonEmptySerials.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            System.Diagnostics.Debug.WriteLine($"[BatchAdd][ValidateInput] After deduplication: {uniqueSerials.Length} UNIQUE items");

            // Log if duplicates were found
            int duplicatesRemoved = nonEmptySerials.Count - uniqueSerials.Length;
            if (duplicatesRemoved > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[BatchAdd][ValidateInput] WARNING: Removed {duplicatesRemoved} duplicate serial number(s)");
            }

            // STEP 5: Determine which input to use
            if (uniqueSerials.Length > 0)
            {
                // SERIAL NUMBERS PROVIDED - Use them (ignore Quantity)
                System.Diagnostics.Debug.WriteLine($"[BatchAdd][ValidateInput] SERIALIZED MODE: {uniqueSerials.Length} serial numbers provided");

                // Log each unique serial number
                for (int i = 0; i < uniqueSerials.Length; i++)
                {
                    System.Diagnostics.Debug.WriteLine($"[BatchAdd][ValidateInput] Unique serial #{i + 1}: [{uniqueSerials[i]}]");
                }

                return new ValidationResult
                {
                    IsValid = true,
                    SerialNumbers = uniqueSerials,
                    Quantity = uniqueSerials.Length,  // Derived from serial count
                    IsNonSerialized = false
                };
            }
            else
            {
                // NO SERIAL NUMBERS - Use Quantity (if valid)
                System.Diagnostics.Debug.WriteLine($"[BatchAdd][ValidateInput] QUANTITY MODE: No serial numbers, checking Quantity");

                if (numQuantity.Value <= 0)
                {
                    numQuantity.Focus();
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Please provide either serial numbers OR a quantity greater than 0."
                    };
                }

                System.Diagnostics.Debug.WriteLine($"[BatchAdd][ValidateInput] QUANTITY MODE SUCCESS: Quantity = {numQuantity.Value}");

                return new ValidationResult
                {
                    IsValid = true,
                    SerialNumbers = new string[] { null },  // Single item with no serial
                    Quantity = (int)numQuantity.Value,
                    IsNonSerialized = true
                };
            }
        }

        /// <summary>
        /// SINGLE INSERT METHOD - This is the ONLY place where items are inserted into the database.
        /// Called from: BtnAdd_Click (and nowhere else)
        ///
        /// Protection mechanisms:
        /// - Uses HashSet to detect in-memory duplicates within THIS batch operation
        /// - Relies on SQL Server UNIQUE constraint on SerialNumber as final safeguard
        /// - Catches SQL error 2627/2601 (duplicate key violation) gracefully
        /// - Continues processing remaining items even if some fail
        ///
        /// For non-serialized items (Cartridge category OR IsTrackedAsset=false):
        /// - Creates ONE item record with StockOnHand = quantity, SerialNumber = NULL
        ///
        /// For serialized items:
        /// - Creates one item record per serial number with StockOnHand = 1
        ///
        /// Returns: BatchAddResult with success count and list of failed serial numbers
        /// </summary>
        private BatchAddResult BatchAddItems(string[] serialNumbers, bool isNonSerialized, int quantity)
        {
            System.Diagnostics.Debug.WriteLine($"[BatchAdd] BatchAddItems() method entered");
            System.Diagnostics.Debug.WriteLine($"[BatchAdd] IsNonSerialized: {isNonSerialized}");
            System.Diagnostics.Debug.WriteLine($"[BatchAdd] Quantity: {quantity}");
            System.Diagnostics.Debug.WriteLine($"[BatchAdd] Total serials to process: {serialNumbers.Length}");

            var repository = new ItemRepository();
            var result = new BatchAddResult();

            // CRITICAL: Track serials in THIS operation to catch in-memory duplicates
            // If a serial appears twice here, it means the validation layer failed
            var processedSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Capture form values once before the loop to avoid any potential issues with UI state changes
            var itemName = txtItemName.Text.Trim();
            var description = string.IsNullOrWhiteSpace(txtDescription.Text) ? null : txtDescription.Text.Trim();
            var modelNumber = txtModelNumber.Text.Trim();
            var selectedCategory = cmbCategory.SelectedItem as CategoryItem;
            var categoryId = selectedCategory?.CategoryId ?? 0;
            var categoryName = selectedCategory?.Name ?? cmbCategory.Text;
            bool isCartridge = string.Equals(categoryName, "Cartridge", StringComparison.OrdinalIgnoreCase);
            int? cartridgeModelId = null;
            if (isCartridge)
            {
                cartridgeModelId = (cmbCartridgeModel.SelectedItem as CartridgeModelItem)?.CartridgeModelId;
            }
            // Cartridge origin: Brand New = NULL RefillStatus (default, backward compatible)
            //                   Refilled  = 'Available' (matches refill reception pathway in CompleteRefillAndRestockAsync)
            string cartridgeRefillStatus = null;
            if (isCartridge)
            {
                var selectedOrigin = cmbCartridgeOrigin.SelectedItem?.ToString() ?? "Brand New";
                if (string.Equals(selectedOrigin, "Refilled", StringComparison.OrdinalIgnoreCase))
                    cartridgeRefillStatus = "Available";
                // else: Brand New → null
            }

            var unitOfMeasure = cmbUnitOfMeasure.Text;
            var selectedType = cmbItemType.SelectedItem?.ToString() ?? "Hardware";
            bool isSoftware = string.Equals(selectedType, "Software/License", StringComparison.OrdinalIgnoreCase);
            bool isServices = string.Equals(selectedType, "Services", StringComparison.OrdinalIgnoreCase);
            bool isHardware = string.Equals(selectedType, "Hardware", StringComparison.OrdinalIgnoreCase);

            // Determine new column values based on ItemType and user selections
            bool affectsInventory = isHardware;  // Only hardware affects inventory by default
            string acquisitionType = isHardware ? "Both" : "Invoice";  // Hardware via both, SW/Services via invoice
            bool isTrackedAsset = chkIsTrackedAsset.Checked;  // Use checkbox value (user can override default)

            decimal amount = 0;
            if (!decimal.TryParse(txtAmount.Text, out amount))
            {
                amount = 0;
            }

            var selectedVendor = cmbVendor.SelectedItem as VendorItem;
            int? vendorId = (selectedVendor != null && selectedVendor.VendorId > 0) ? (int?)selectedVendor.VendorId : null;
            var vendorName = selectedVendor?.VendorName;

            var conditionId = (cmbCondition.SelectedItem as ConditionItem)?.ConditionId ?? 1;
            var conditionName = (cmbCondition.SelectedItem as ConditionItem)?.ConditionName ?? "Good";
            var remarks = string.IsNullOrWhiteSpace(txtRemarks.Text) ? null : txtRemarks.Text.Trim();
            var partNumber = string.IsNullOrWhiteSpace(txtPartNumber.Text) ? null : txtPartNumber.Text.Trim();
            var warrantyYears = (int)numWarrantyYears.Value;
            // Do not strip time — database requires full timestamp
            var datePurchased = dtpDatePurchased.Checked ? dtpDatePurchased.Value : (DateTime?)null;
            var licenseNumber = string.IsNullOrWhiteSpace(txtLicenseNumber.Text) ? null : txtLicenseNumber.Text.Trim();
            bool hasContractDates = isSoftware || (isServices && chkServiceContractDates.Checked);
            var startDate = hasContractDates ? dtpStartDate.Value : (DateTime?)null;
            var endDate = hasContractDates ? dtpEndDate.Value : (DateTime?)null;

            foreach (string serialNumber in serialNumbers)
            {
                // ==================================================================================
                // PROCESSING EACH SERIAL NUMBER
                // ==================================================================================
                System.Diagnostics.Debug.WriteLine($"[BatchAdd] Processing serial: {serialNumber}");

                var normalizedSerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim();

                // CRITICAL: In-memory duplicate check (skip for NULL serials - they're allowed)
                if (!string.IsNullOrEmpty(normalizedSerialNumber) && processedSerials.Contains(normalizedSerialNumber))
                {
                    System.Diagnostics.Debug.WriteLine($"[BatchAdd] ⚠⚠⚠ IN-MEMORY DUPLICATE: {serialNumber}");
                    System.Diagnostics.Debug.WriteLine($"[BatchAdd] This serial was already processed in THIS operation!");
                    System.Diagnostics.Debug.WriteLine($"[BatchAdd] This indicates a validation layer failure!");
                    result.FailedSerials.Add($"{serialNumber} (duplicate in batch - skipped)");
                    continue;
                }

                // Add to processed set BEFORE attempting insert (skip for NULL serials)
                if (!string.IsNullOrEmpty(normalizedSerialNumber))
                {
                    processedSerials.Add(normalizedSerialNumber);
                }

                try
                {
                    var dateCreated = DateTime.Now;

                    var item = new ItemDto
                    {
                        Name = itemName,
                        Description = description,
                        ModelNumber = modelNumber,
                        CartridgeModelId = cartridgeModelId,  // REFACTOR: Set CartridgeModelId FK for cartridges
                        CategoryId = categoryId,
                        Category = categoryName,
                        SerialNumber = normalizedSerialNumber,
                        // For non-serialized items, use quantity; for serialized items, always 1
                        StockOnHand = isNonSerialized ? quantity : 1,
                        UnitOfMeasure = unitOfMeasure,
                        Amount = amount,
                        ItemType = selectedType,
                        // StartDate/EndDate are used for Software/License and optionally for Services (when enabled).
                        // When StartDate is null, ItemRepository will default it to DateCreated.
                        StartDate = startDate,
                        EndDate = endDate,
                        // NEW: Condition tracking fields
                        ConditionId = conditionId,
                        ConditionName = conditionName,
                        // Vendor field
                        VendorId = vendorId,
                        VendorName = vendorName,
                        Remarks = remarks,
                        // Warranty field
                        WarrantyYears = warrantyYears,
                        // Warranty Start Date - automatically set to DateCreated when WarrantyYears > 0
                        WarrantyStartDate = warrantyYears > 0 ? dateCreated : (DateTime?)null,
                        // Date Purchased field
                        DatePurchased = datePurchased,
                        // License Number field (for Software/License and Services)
                        LicenseNumber = licenseNumber,
                        // Part Number (stored in dbo.Renewals via ItemRepository.AddItem)
                        PartNumber = partNumber,
                        // NEW: Inventory control columns
                        AffectsInventory = affectsInventory,
                        AcquisitionType = acquisitionType,
                        IsTrackedAsset = isTrackedAsset,
                        // END NEW FIELDS
                        // Cartridge origin: null = Brand New (default), 'Available' = Refilled
                        RefillStatus = cartridgeRefillStatus,
                        DateCreated = dateCreated,
                        CreatedByUserId = AppSession.CurrentUserId,
                        CreatedByName = AppSession.CurrentUserName,
                        Active = true
                    };

                    // Attempt database insert
                    int newItemId = repository.AddItem(item);

                    // SUCCESS
                    System.Diagnostics.Debug.WriteLine($"[BatchAdd] Insert SUCCESS: {serialNumber}");
                    result.SuccessCount++;
                }
                catch (System.Data.SqlClient.SqlException ex)
                {
                    // SQL Server error - check if it's a duplicate key violation
                    if (ex.Number == 2627 || ex.Number == 2601)
                    {
                        // ==================================================================================
                        // DUPLICATE KEY VIOLATION (SQL Error 2627/2601)
                        // Could be EITHER:
                        //   1. Serial number UNIQUE constraint violation
                        //   2. ItemId PRIMARY KEY constraint violation (IDENTITY seed issue)
                        // ==================================================================================
                        System.Diagnostics.Debug.WriteLine($"[BatchAdd] ❌ SQL Error {ex.Number}: Duplicate key violation");
                        System.Diagnostics.Debug.WriteLine($"[BatchAdd] Full error message: {ex.Message}");

                        // Check if it's a PRIMARY KEY violation (ItemId) vs UNIQUE constraint (SerialNumber)
                        if (ex.Message.Contains("PRIMARY KEY") || ex.Message.Contains("PK__Item"))
                        {
                            System.Diagnostics.Debug.WriteLine($"[BatchAdd] ⚠⚠⚠ PRIMARY KEY VIOLATION ON ItemId ⚠⚠⚠");
                            System.Diagnostics.Debug.WriteLine($"[BatchAdd] This is NOT a serial number duplicate!");
                            System.Diagnostics.Debug.WriteLine($"[BatchAdd] The IDENTITY column (ItemId) is out of sync.");
                            System.Diagnostics.Debug.WriteLine($"[BatchAdd] FIX: Run DBCC CHECKIDENT('dbo.Item', RESEED) in SQL Server");
                            result.FailedSerials.Add($"{serialNumber} (database error - ItemId conflict, see debug log)");
                        }
                        else if (ex.Message.Contains("SerialNumber") || ex.Message.Contains("UQ_"))
                        {
                            System.Diagnostics.Debug.WriteLine($"[BatchAdd] Serial number UNIQUE constraint violation");
                            System.Diagnostics.Debug.WriteLine($"[BatchAdd] Serial [{serialNumber}] already exists in database");
                            result.FailedSerials.Add($"{serialNumber} (already exists)");
                        }
                        else
                        {
                            // Generic duplicate - can't determine which constraint
                            System.Diagnostics.Debug.WriteLine($"[BatchAdd] Duplicate key violation (constraint unknown)");
                            result.FailedSerials.Add($"{serialNumber} (duplicate key - see debug log)");
                        }
                    }
                    else
                    {
                        // Unexpected SQL error - log and rethrow
                        System.Diagnostics.Debug.WriteLine($"[BatchAdd] ❌ UNEXPECTED SQL ERROR {ex.Number}: {ex.Message}");
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    // Unexpected non-SQL exception
                    System.Diagnostics.Debug.WriteLine($"[BatchAdd] ❌ EXCEPTION for {serialNumber}: {ex.GetType().Name} - {ex.Message}");
                    result.FailedSerials.Add($"{serialNumber} (Error: {ex.Message})");
                }
            }

            // ==================================================================================
            // BATCH OPERATION COMPLETE
            // ==================================================================================
            System.Diagnostics.Debug.WriteLine($"");
            System.Diagnostics.Debug.WriteLine($"[BatchAdd] BatchAddItems() completed");
            System.Diagnostics.Debug.WriteLine($"[BatchAdd] Successful inserts: {result.SuccessCount}");
            System.Diagnostics.Debug.WriteLine($"[BatchAdd] Failed: {result.FailedSerials.Count}");
            if (result.FailedSerials.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[BatchAdd] Failed serials: {string.Join(", ", result.FailedSerials)}");
            }

            return result;
        }

        private void ClearForm()
        {
            txtItemName.Clear();
            txtDescription.Clear();
            txtModelNumber.Clear();
            txtSerialNumbers.Clear();
            numQuantity.Value = 1;  // Reset quantity to default
            // Restore full item lists in case filtering was active when form is cleared
            RestoreComboItems(cmbCategory);
            RestoreComboItems(cmbItemType);
            cmbCategory.SelectedIndex = -1;
            cmbItemType.SelectedIndex = 0;

            // Reset date pickers - CRITICAL: Reset MinDate first to avoid validation errors
            dtpEndDate.MinDate = DateTimePicker.MinimumDateTime;
            dtpStartDate.Value = DateTime.Today;
            dtpEndDate.Value = DateTime.Today.AddDays(1);
            CmbItemType_SelectedIndexChanged(cmbItemType, EventArgs.Empty);
            cmbUnitOfMeasure.SelectedIndex = 0;
            txtAmount.Text = "0";

            // NEW: Reset condition tracking fields
            // Reset to "Good" if available
            for (int i = 0; i < cmbCondition.Items.Count; i++)
            {
                if (cmbCondition.Items[i] is ConditionItem item &&
                    item.ConditionName.Equals("Good", StringComparison.OrdinalIgnoreCase))
                {
                    cmbCondition.SelectedIndex = i;
                    break;
                }
            }
            // Reset vendor to "(None)"
            if (cmbVendor.Items.Count > 0)
            {
                cmbVendor.SelectedIndex = 0;
            }
            // Reset cartridge origin to Brand New (default)
            if (cmbCartridgeOrigin.Items.Count > 0)
                cmbCartridgeOrigin.SelectedIndex = 0;
            txtRemarks.Clear();
            txtLicenseNumber.Clear();  // Clear license number
            txtPartNumber.Clear();     // Clear part number
            numWarrantyYears.Value = 0;
            dtpDatePurchased.Checked = false;  // Uncheck date purchased
            // END NEW RESET

            lstPreview.Items.Clear();
            lstPreview.Visible = false;
            lblPreviewTitle.Visible = false;
            txtItemName.Focus();
        }

        // Event handler for StartDate/EndDate changes in Software/License mode
        private void DtpLicenseDate_ValueChanged(object sender, EventArgs e)
        {
            UpdateWarrantyDisplay();
        }

        // Update the warranty display label for Software/License items
        private void UpdateWarrantyDisplay()
        {
            if (cmbItemType.SelectedItem?.ToString() == "Software/License")
            {
                var startDate = dtpStartDate.Value;
                var endDate = dtpEndDate.Value;

                if (endDate <= startDate)
                {
                    lblWarrantyInfo.Text = $"End Date must be after Start Date. ({startDate:MMM dd, yyyy} → {endDate:MMM dd, yyyy})";
                    lblWarrantyInfo.ForeColor = Color.Firebrick;
                    return;
                }

                // Calculate the warranty period
                var warrantyPeriod = endDate - startDate;
                var totalDays = (int)warrantyPeriod.TotalDays;
                var years = totalDays / 365;
                var remainingDays = totalDays % 365;
                var months = remainingDays / 30;

                // Build the display text
                string periodText;
                if (years > 0 && months > 0)
                    periodText = $"{years} year(s) and {months} month(s)";
                else if (years > 0)
                    periodText = $"{years} year(s)";
                else if (months > 0)
                    periodText = $"{months} month(s)";
                else
                    periodText = $"{totalDays} day(s)";

                lblWarrantyInfo.Text = $"Warranty/License Period: {startDate:MMM dd, yyyy} to {endDate:MMM dd, yyyy} ({periodText})";
                lblWarrantyInfo.ForeColor = Color.DarkGreen;
            }
        }

        public void AddSerialFromMobile(string serialNumber)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
                return;

            var trimmed = serialNumber.Trim();

            var existingSerials = txtSerialNumbers.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();

            if (!existingSerials.Any(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                if (txtSerialNumbers.Text.Length == 0)
                {
                    txtSerialNumbers.Text = trimmed;
                }
                else
                {
                    txtSerialNumbers.AppendText(Environment.NewLine + trimmed);
                }
            }

            txtSerialNumbers.BackColor = Color.LightYellow;
            txtSerialNumbers.Focus();
            txtSerialNumbers.SelectionStart = txtSerialNumbers.Text.Length;
        }


        // Helper classes
        private class CategoryItem
        {
            public int CategoryId { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private class ConditionItem
        {
            public int ConditionId { get; set; }
            public string ConditionName { get; set; }
            public override string ToString() => ConditionName;
        }

        private class VendorItem
        {
            public int VendorId { get; set; }
            public string VendorName { get; set; }
            public override string ToString() => VendorName;
        }

        private class CartridgeModelItem
        {
            public int CartridgeModelId { get; set; }
            public string ModelNumber { get; set; }
            public string VendorName { get; set; }
            public int? VendorId { get; set; }

            public override string ToString() => string.IsNullOrWhiteSpace(VendorName)
                ? ModelNumber
                : $"{ModelNumber} - {VendorName}";
        }

        private sealed class QuickAddCartridgeModelResult
        {
            public int CartridgeModelId { get; set; }
            public string ModelNumber { get; set; }
        }

        // Helper classes
        private class ValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
            public string[] SerialNumbers { get; set; }
            public int Quantity { get; set; }
            public bool IsNonSerialized { get; set; }
        }

        private class BatchAddResult
        {
            public int SuccessCount { get; set; }
            public List<string> FailedSerials { get; set; } = new List<string>();
        }
    }
}
