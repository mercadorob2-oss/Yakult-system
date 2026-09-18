namespace Yakult.Inventory.App.Pages.Renewal
{
    partial class ViewRenewalDetailPage
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panelTop = new System.Windows.Forms.Panel();
            this.lblExpiryStatus = new System.Windows.Forms.Label();
            this.lblItemName = new System.Windows.Forms.Label();
            this.lblItemType = new System.Windows.Forms.Label();
            this.panelMain = new System.Windows.Forms.Panel();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPageDetails = new System.Windows.Forms.TabPage();
            this.panelDetails = new System.Windows.Forms.Panel();
            this.groupBoxItemInfo = new System.Windows.Forms.GroupBox();
            this.txtItemName = new System.Windows.Forms.TextBox();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.txtItemType = new System.Windows.Forms.TextBox();
            this.txtCategory = new System.Windows.Forms.TextBox();
            this.txtSerialNumber = new System.Windows.Forms.TextBox();
            this.txtModelNumber = new System.Windows.Forms.TextBox();
            this.txtLicenseNumber = new System.Windows.Forms.TextBox();
            this.txtAmount = new System.Windows.Forms.TextBox();
            this.txtPartNumber = new System.Windows.Forms.TextBox();
            this.lblItemNameCaption = new System.Windows.Forms.Label();
            this.lblDescriptionCaption = new System.Windows.Forms.Label();
            this.lblItemTypeCaption = new System.Windows.Forms.Label();
            this.lblCategoryCaption = new System.Windows.Forms.Label();
            this.lblSerialNumberCaption = new System.Windows.Forms.Label();
            this.lblModelNumberCaption = new System.Windows.Forms.Label();
            this.lblLicenseNumberCaption = new System.Windows.Forms.Label();
            this.lblAmountCaption = new System.Windows.Forms.Label();
            this.lblPartNumberCaption = new System.Windows.Forms.Label();
            this.groupBoxRenewalInfo = new System.Windows.Forms.GroupBox();
            this.txtRenewalStatus = new System.Windows.Forms.TextBox();
            this.dtpStartDate = new System.Windows.Forms.DateTimePicker();
            this.dtpEndDate = new System.Windows.Forms.DateTimePicker();
            this.txtDaysLeft = new System.Windows.Forms.TextBox();
            this.lblExpiryWarning = new System.Windows.Forms.Label();
            this.lblRenewalStatusCaption = new System.Windows.Forms.Label();
            this.lblStartDateCaption = new System.Windows.Forms.Label();
            this.lblEndDateCaption = new System.Windows.Forms.Label();
            this.lblDaysLeftCaption = new System.Windows.Forms.Label();
            this.groupBoxVendorInfo = new System.Windows.Forms.GroupBox();
            this.txtVendorName = new System.Windows.Forms.TextBox();
            this.txtVendorAddress = new System.Windows.Forms.TextBox();
            this.txtVendorTIN = new System.Windows.Forms.TextBox();
            this.btnSaveVendor = new System.Windows.Forms.Button();
            this.lblVendorNameCaption = new System.Windows.Forms.Label();
            this.lblVendorAddressCaption = new System.Windows.Forms.Label();
            this.lblVendorTINCaption = new System.Windows.Forms.Label();
            this.groupBoxSiteInfo = new System.Windows.Forms.GroupBox();
            this.txtSiteDisplay = new System.Windows.Forms.TextBox();
            this.lblSiteDisplayCaption = new System.Windows.Forms.Label();
            this.groupBoxFinancialInfo = new System.Windows.Forms.GroupBox();
            this.txtSubtotal = new System.Windows.Forms.TextBox();
            this.txtVatAmount = new System.Windows.Forms.TextBox();
            this.txtWhtAmount = new System.Windows.Forms.TextBox();
            this.txtDiscountAmount = new System.Windows.Forms.TextBox();
            this.txtTotalAmountDue = new System.Windows.Forms.TextBox();
            this.lblSubtotalCaption = new System.Windows.Forms.Label();
            this.lblVatAmountCaption = new System.Windows.Forms.Label();
            this.lblWhtAmountCaption = new System.Windows.Forms.Label();
            this.lblDiscountAmountCaption = new System.Windows.Forms.Label();
            this.lblTotalAmountDueCaption = new System.Windows.Forms.Label();
            this.groupBoxArchive = new System.Windows.Forms.GroupBox();
            this.chkArchived = new System.Windows.Forms.CheckBox();
            this.txtArchiveReason = new System.Windows.Forms.TextBox();
            this.lblArchiveReasonCaption = new System.Windows.Forms.Label();
            this.tabPageHistory = new System.Windows.Forms.TabPage();
            this.dgvRenewalHistory = new System.Windows.Forms.DataGridView();
            this.panelBottom = new System.Windows.Forms.Panel();
            this.btnCreateRenewal = new System.Windows.Forms.Button();
            this.btnArchive = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.lblCreatedBy = new System.Windows.Forms.Label();
            this.lblCreatedAt = new System.Windows.Forms.Label();
            this.panelTop.SuspendLayout();
            this.panelMain.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.tabPageDetails.SuspendLayout();
            this.panelDetails.SuspendLayout();
            this.groupBoxItemInfo.SuspendLayout();
            this.groupBoxRenewalInfo.SuspendLayout();
            this.groupBoxVendorInfo.SuspendLayout();
            this.groupBoxSiteInfo.SuspendLayout();
            this.groupBoxFinancialInfo.SuspendLayout();
            this.groupBoxArchive.SuspendLayout();
            this.tabPageHistory.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRenewalHistory)).BeginInit();
            this.panelBottom.SuspendLayout();
            this.SuspendLayout();
            //
            // panelTop
            //
            this.panelTop.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(41)))), ((int)(((byte)(128)))), ((int)(((byte)(185)))));
            this.panelTop.Controls.Add(this.lblExpiryStatus);
            this.panelTop.Controls.Add(this.lblItemName);
            this.panelTop.Controls.Add(this.lblItemType);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Padding = new System.Windows.Forms.Padding(20, 15, 20, 10);
            this.panelTop.Size = new System.Drawing.Size(1000, 130);
            this.panelTop.TabIndex = 0;
            //
            // lblExpiryStatus
            //
            this.lblExpiryStatus.AutoSize = true;
            this.lblExpiryStatus.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblExpiryStatus.ForeColor = System.Drawing.Color.White;
            this.lblExpiryStatus.Location = new System.Drawing.Point(20, 88);
            this.lblExpiryStatus.Margin = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.lblExpiryStatus.MaximumSize = new System.Drawing.Size(960, 0);
            this.lblExpiryStatus.Name = "lblExpiryStatus";
            this.lblExpiryStatus.Size = new System.Drawing.Size(200, 25);
            this.lblExpiryStatus.TabIndex = 2;
            this.lblExpiryStatus.Text = "Loading...";
            //
            // lblItemName
            //
            this.lblItemName.AutoSize = true;
            this.lblItemName.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this.lblItemName.ForeColor = System.Drawing.Color.White;
            this.lblItemName.Location = new System.Drawing.Point(20, 15);
            this.lblItemName.Margin = new System.Windows.Forms.Padding(0);
            this.lblItemName.MaximumSize = new System.Drawing.Size(960, 0);
            this.lblItemName.Name = "lblItemName";
            this.lblItemName.Size = new System.Drawing.Size(200, 30);
            this.lblItemName.TabIndex = 0;
            this.lblItemName.Text = "Renewal Details";
            //
            // lblItemType
            //
            this.lblItemType.AutoSize = true;
            this.lblItemType.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.lblItemType.ForeColor = System.Drawing.Color.White;
            this.lblItemType.Location = new System.Drawing.Point(20, 50);
            this.lblItemType.Margin = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.lblItemType.MaximumSize = new System.Drawing.Size(960, 0);
            this.lblItemType.Name = "lblItemType";
            this.lblItemType.Size = new System.Drawing.Size(100, 19);
            this.lblItemType.TabIndex = 1;
            this.lblItemType.Text = "Software/License";
            //
            // panelMain
            //
            this.panelMain.Controls.Add(this.tabControl);
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMain.Location = new System.Drawing.Point(0, 120);
            this.panelMain.Name = "panelMain";
            this.panelMain.Padding = new System.Windows.Forms.Padding(10);
            this.panelMain.Size = new System.Drawing.Size(1000, 480);
            this.panelMain.TabIndex = 1;
            //
            // tabControl
            //
            this.tabControl.Controls.Add(this.tabPageDetails);
            this.tabControl.Controls.Add(this.tabPageHistory);
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl.Location = new System.Drawing.Point(10, 10);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(980, 480);
            this.tabControl.TabIndex = 0;
            //
            // tabPageDetails
            //
            this.tabPageDetails.Controls.Add(this.panelDetails);
            this.tabPageDetails.Location = new System.Drawing.Point(4, 24);
            this.tabPageDetails.Name = "tabPageDetails";
            this.tabPageDetails.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageDetails.Size = new System.Drawing.Size(972, 452);
            this.tabPageDetails.TabIndex = 0;
            this.tabPageDetails.Text = "Renewal Details";
            this.tabPageDetails.UseVisualStyleBackColor = true;
            //
            // panelDetails
            //
            this.panelDetails.AutoScroll = true;
            this.panelDetails.Controls.Add(this.groupBoxItemInfo);
            this.panelDetails.Controls.Add(this.groupBoxRenewalInfo);
            this.panelDetails.Controls.Add(this.groupBoxVendorInfo);
            this.panelDetails.Controls.Add(this.groupBoxSiteInfo);
            this.panelDetails.Controls.Add(this.groupBoxFinancialInfo);
            this.panelDetails.Controls.Add(this.groupBoxArchive);
            this.panelDetails.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelDetails.Location = new System.Drawing.Point(3, 3);
            this.panelDetails.Name = "panelDetails";
            this.panelDetails.Size = new System.Drawing.Size(966, 446);
            this.panelDetails.TabIndex = 0;
            //
            // groupBoxItemInfo
            //
            this.groupBoxItemInfo.Controls.Add(this.txtItemName);
            this.groupBoxItemInfo.Controls.Add(this.txtDescription);
            this.groupBoxItemInfo.Controls.Add(this.txtItemType);
            this.groupBoxItemInfo.Controls.Add(this.txtCategory);
            this.groupBoxItemInfo.Controls.Add(this.txtSerialNumber);
            this.groupBoxItemInfo.Controls.Add(this.txtModelNumber);
            this.groupBoxItemInfo.Controls.Add(this.txtLicenseNumber);
            this.groupBoxItemInfo.Controls.Add(this.txtAmount);
            this.groupBoxItemInfo.Controls.Add(this.txtPartNumber);
            this.groupBoxItemInfo.Controls.Add(this.lblItemNameCaption);
            this.groupBoxItemInfo.Controls.Add(this.lblDescriptionCaption);
            this.groupBoxItemInfo.Controls.Add(this.lblItemTypeCaption);
            this.groupBoxItemInfo.Controls.Add(this.lblCategoryCaption);
            this.groupBoxItemInfo.Controls.Add(this.lblSerialNumberCaption);
            this.groupBoxItemInfo.Controls.Add(this.lblModelNumberCaption);
            this.groupBoxItemInfo.Controls.Add(this.lblLicenseNumberCaption);
            this.groupBoxItemInfo.Controls.Add(this.lblAmountCaption);
            this.groupBoxItemInfo.Controls.Add(this.lblPartNumberCaption);
            this.groupBoxItemInfo.Location = new System.Drawing.Point(10, 10);
            this.groupBoxItemInfo.Name = "groupBoxItemInfo";
            this.groupBoxItemInfo.Size = new System.Drawing.Size(450, 385);
            this.groupBoxItemInfo.TabIndex = 0;
            this.groupBoxItemInfo.TabStop = false;
            this.groupBoxItemInfo.Text = "Item Information";
            //
            // txtItemName
            //
            this.txtItemName.Location = new System.Drawing.Point(150, 30);
            this.txtItemName.Name = "txtItemName";
            this.txtItemName.ReadOnly = true;
            this.txtItemName.Size = new System.Drawing.Size(280, 23);
            this.txtItemName.TabIndex = 1;
            //
            // txtDescription
            //
            this.txtDescription.Location = new System.Drawing.Point(150, 65);
            this.txtDescription.Multiline = true;
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.ReadOnly = true;
            this.txtDescription.Size = new System.Drawing.Size(280, 60);
            this.txtDescription.TabIndex = 3;
            //
            // txtItemType
            //
            this.txtItemType.Location = new System.Drawing.Point(150, 135);
            this.txtItemType.Name = "txtItemType";
            this.txtItemType.ReadOnly = true;
            this.txtItemType.Size = new System.Drawing.Size(280, 23);
            this.txtItemType.TabIndex = 5;
            //
            // txtCategory
            //
            this.txtCategory.Location = new System.Drawing.Point(150, 170);
            this.txtCategory.Name = "txtCategory";
            this.txtCategory.ReadOnly = true;
            this.txtCategory.Size = new System.Drawing.Size(280, 23);
            this.txtCategory.TabIndex = 7;
            //
            // txtSerialNumber
            //
            this.txtSerialNumber.Location = new System.Drawing.Point(150, 205);
            this.txtSerialNumber.Name = "txtSerialNumber";
            this.txtSerialNumber.ReadOnly = true;
            this.txtSerialNumber.Size = new System.Drawing.Size(280, 23);
            this.txtSerialNumber.TabIndex = 9;
            //
            // txtModelNumber
            //
            this.txtModelNumber.Location = new System.Drawing.Point(150, 240);
            this.txtModelNumber.Name = "txtModelNumber";
            this.txtModelNumber.ReadOnly = true;
            this.txtModelNumber.Size = new System.Drawing.Size(280, 23);
            this.txtModelNumber.TabIndex = 11;
            //
            // txtLicenseNumber
            //
            this.txtLicenseNumber.Location = new System.Drawing.Point(150, 275);
            this.txtLicenseNumber.Name = "txtLicenseNumber";
            this.txtLicenseNumber.ReadOnly = true;
            this.txtLicenseNumber.Size = new System.Drawing.Size(280, 23);
            this.txtLicenseNumber.TabIndex = 13;
            //
            // txtAmount
            //
            this.txtAmount.Location = new System.Drawing.Point(150, 310);
            this.txtAmount.Name = "txtAmount";
            this.txtAmount.ReadOnly = true;
            this.txtAmount.Size = new System.Drawing.Size(280, 23);
            this.txtAmount.TabIndex = 15;
            //
            // lblItemNameCaption
            //
            this.lblItemNameCaption.AutoSize = true;
            this.lblItemNameCaption.Location = new System.Drawing.Point(20, 33);
            this.lblItemNameCaption.Name = "lblItemNameCaption";
            this.lblItemNameCaption.Size = new System.Drawing.Size(70, 15);
            this.lblItemNameCaption.TabIndex = 0;
            this.lblItemNameCaption.Text = "Item Name:";
            //
            // lblDescriptionCaption
            //
            this.lblDescriptionCaption.AutoSize = true;
            this.lblDescriptionCaption.Location = new System.Drawing.Point(20, 68);
            this.lblDescriptionCaption.Name = "lblDescriptionCaption";
            this.lblDescriptionCaption.Size = new System.Drawing.Size(73, 15);
            this.lblDescriptionCaption.TabIndex = 2;
            this.lblDescriptionCaption.Text = "Description:";
            //
            // lblItemTypeCaption
            //
            this.lblItemTypeCaption.AutoSize = true;
            this.lblItemTypeCaption.Location = new System.Drawing.Point(20, 138);
            this.lblItemTypeCaption.Name = "lblItemTypeCaption";
            this.lblItemTypeCaption.Size = new System.Drawing.Size(64, 15);
            this.lblItemTypeCaption.TabIndex = 4;
            this.lblItemTypeCaption.Text = "Item Type:";
            //
            // lblCategoryCaption
            //
            this.lblCategoryCaption.AutoSize = true;
            this.lblCategoryCaption.Location = new System.Drawing.Point(20, 173);
            this.lblCategoryCaption.Name = "lblCategoryCaption";
            this.lblCategoryCaption.Size = new System.Drawing.Size(58, 15);
            this.lblCategoryCaption.TabIndex = 6;
            this.lblCategoryCaption.Text = "Category:";
            //
            // lblSerialNumberCaption
            //
            this.lblSerialNumberCaption.AutoSize = true;
            this.lblSerialNumberCaption.Location = new System.Drawing.Point(20, 208);
            this.lblSerialNumberCaption.Name = "lblSerialNumberCaption";
            this.lblSerialNumberCaption.Size = new System.Drawing.Size(88, 15);
            this.lblSerialNumberCaption.TabIndex = 8;
            this.lblSerialNumberCaption.Text = "Serial Number:";
            //
            // lblModelNumberCaption
            //
            this.lblModelNumberCaption.AutoSize = true;
            this.lblModelNumberCaption.Location = new System.Drawing.Point(20, 243);
            this.lblModelNumberCaption.Name = "lblModelNumberCaption";
            this.lblModelNumberCaption.Size = new System.Drawing.Size(93, 15);
            this.lblModelNumberCaption.TabIndex = 10;
            this.lblModelNumberCaption.Text = "Model Number:";
            //
            // lblLicenseNumberCaption
            //
            this.lblLicenseNumberCaption.AutoSize = true;
            this.lblLicenseNumberCaption.Location = new System.Drawing.Point(20, 278);
            this.lblLicenseNumberCaption.Name = "lblLicenseNumberCaption";
            this.lblLicenseNumberCaption.Size = new System.Drawing.Size(96, 15);
            this.lblLicenseNumberCaption.TabIndex = 12;
            this.lblLicenseNumberCaption.Text = "License Number:";
            //
            // lblAmountCaption
            //
            this.lblAmountCaption.AutoSize = true;
            this.lblAmountCaption.Location = new System.Drawing.Point(20, 313);
            this.lblAmountCaption.Name = "lblAmountCaption";
            this.lblAmountCaption.Size = new System.Drawing.Size(54, 15);
            this.lblAmountCaption.TabIndex = 14;
            this.lblAmountCaption.Text = "Amount:";
            //
            // txtPartNumber
            //
            this.txtPartNumber.Location = new System.Drawing.Point(150, 345);
            this.txtPartNumber.Name = "txtPartNumber";
            this.txtPartNumber.ReadOnly = true;
            this.txtPartNumber.Size = new System.Drawing.Size(280, 23);
            this.txtPartNumber.TabIndex = 16;
            //
            // lblPartNumberCaption
            //
            this.lblPartNumberCaption.AutoSize = true;
            this.lblPartNumberCaption.Location = new System.Drawing.Point(20, 348);
            this.lblPartNumberCaption.Name = "lblPartNumberCaption";
            this.lblPartNumberCaption.Size = new System.Drawing.Size(45, 15);
            this.lblPartNumberCaption.TabIndex = 17;
            this.lblPartNumberCaption.Text = "Part #:";
            //
            // groupBoxRenewalInfo
            //
            this.groupBoxRenewalInfo.Controls.Add(this.txtRenewalStatus);
            this.groupBoxRenewalInfo.Controls.Add(this.dtpStartDate);
            this.groupBoxRenewalInfo.Controls.Add(this.dtpEndDate);
            this.groupBoxRenewalInfo.Controls.Add(this.txtDaysLeft);
            this.groupBoxRenewalInfo.Controls.Add(this.lblExpiryWarning);
            this.groupBoxRenewalInfo.Controls.Add(this.lblRenewalStatusCaption);
            this.groupBoxRenewalInfo.Controls.Add(this.lblStartDateCaption);
            this.groupBoxRenewalInfo.Controls.Add(this.lblEndDateCaption);
            this.groupBoxRenewalInfo.Controls.Add(this.lblDaysLeftCaption);
            this.groupBoxRenewalInfo.Location = new System.Drawing.Point(480, 10);
            this.groupBoxRenewalInfo.Name = "groupBoxRenewalInfo";
            this.groupBoxRenewalInfo.Size = new System.Drawing.Size(450, 220);
            this.groupBoxRenewalInfo.TabIndex = 1;
            this.groupBoxRenewalInfo.TabStop = false;
            this.groupBoxRenewalInfo.Text = "Renewal Information";
            //
            // txtRenewalStatus
            //
            this.txtRenewalStatus.Location = new System.Drawing.Point(150, 30);
            this.txtRenewalStatus.Name = "txtRenewalStatus";
            this.txtRenewalStatus.ReadOnly = true;
            this.txtRenewalStatus.Size = new System.Drawing.Size(280, 23);
            this.txtRenewalStatus.TabIndex = 1;
            //
            // dtpStartDate
            //
            this.dtpStartDate.Enabled = false;
            this.dtpStartDate.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtpStartDate.CustomFormat = "MM/dd/yyyy";
            this.dtpStartDate.Location = new System.Drawing.Point(150, 65);
            this.dtpStartDate.Name = "dtpStartDate";
            this.dtpStartDate.Size = new System.Drawing.Size(280, 23);
            this.dtpStartDate.TabIndex = 3;
            //
            // dtpEndDate
            //
            this.dtpEndDate.Enabled = false;
            this.dtpEndDate.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.dtpEndDate.CustomFormat = "MM/dd/yyyy";
            this.dtpEndDate.Location = new System.Drawing.Point(150, 100);
            this.dtpEndDate.Name = "dtpEndDate";
            this.dtpEndDate.Size = new System.Drawing.Size(280, 23);
            this.dtpEndDate.TabIndex = 5;
            //
            // txtDaysLeft
            //
            this.txtDaysLeft.Location = new System.Drawing.Point(150, 135);
            this.txtDaysLeft.Name = "txtDaysLeft";
            this.txtDaysLeft.ReadOnly = true;
            this.txtDaysLeft.Size = new System.Drawing.Size(280, 23);
            this.txtDaysLeft.TabIndex = 7;
            //
            // lblExpiryWarning
            //
            this.lblExpiryWarning.AutoSize = true;
            this.lblExpiryWarning.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblExpiryWarning.Location = new System.Drawing.Point(20, 175);
            this.lblExpiryWarning.Name = "lblExpiryWarning";
            this.lblExpiryWarning.Size = new System.Drawing.Size(150, 19);
            this.lblExpiryWarning.TabIndex = 8;
            this.lblExpiryWarning.Text = "Expiry Status: Active";
            //
            // lblRenewalStatusCaption
            //
            this.lblRenewalStatusCaption.AutoSize = true;
            this.lblRenewalStatusCaption.Location = new System.Drawing.Point(20, 33);
            this.lblRenewalStatusCaption.Name = "lblRenewalStatusCaption";
            this.lblRenewalStatusCaption.Size = new System.Drawing.Size(91, 15);
            this.lblRenewalStatusCaption.TabIndex = 0;
            this.lblRenewalStatusCaption.Text = "Renewal Status:";
            //
            // lblStartDateCaption
            //
            this.lblStartDateCaption.AutoSize = true;
            this.lblStartDateCaption.Location = new System.Drawing.Point(20, 68);
            this.lblStartDateCaption.Name = "lblStartDateCaption";
            this.lblStartDateCaption.Size = new System.Drawing.Size(64, 15);
            this.lblStartDateCaption.TabIndex = 2;
            this.lblStartDateCaption.Text = "Start Date:";
            //
            // lblEndDateCaption
            //
            this.lblEndDateCaption.AutoSize = true;
            this.lblEndDateCaption.Location = new System.Drawing.Point(20, 103);
            this.lblEndDateCaption.Name = "lblEndDateCaption";
            this.lblEndDateCaption.Size = new System.Drawing.Size(57, 15);
            this.lblEndDateCaption.TabIndex = 4;
            this.lblEndDateCaption.Text = "End Date:";
            //
            // lblDaysLeftCaption
            //
            this.lblDaysLeftCaption.AutoSize = true;
            this.lblDaysLeftCaption.Location = new System.Drawing.Point(20, 138);
            this.lblDaysLeftCaption.Name = "lblDaysLeftCaption";
            this.lblDaysLeftCaption.Size = new System.Drawing.Size(58, 15);
            this.lblDaysLeftCaption.TabIndex = 6;
            this.lblDaysLeftCaption.Text = "Days Left:";
            //
            // groupBoxVendorInfo
            //
            this.groupBoxVendorInfo.Controls.Add(this.txtVendorName);
            this.groupBoxVendorInfo.Controls.Add(this.txtVendorAddress);
            this.groupBoxVendorInfo.Controls.Add(this.txtVendorTIN);
            this.groupBoxVendorInfo.Controls.Add(this.btnSaveVendor);
            this.groupBoxVendorInfo.Controls.Add(this.lblVendorNameCaption);
            this.groupBoxVendorInfo.Controls.Add(this.lblVendorAddressCaption);
            this.groupBoxVendorInfo.Controls.Add(this.lblVendorTINCaption);
            this.groupBoxVendorInfo.Location = new System.Drawing.Point(480, 240);
            this.groupBoxVendorInfo.Name = "groupBoxVendorInfo";
            this.groupBoxVendorInfo.Size = new System.Drawing.Size(450, 220);
            this.groupBoxVendorInfo.TabIndex = 2;
            this.groupBoxVendorInfo.TabStop = false;
            this.groupBoxVendorInfo.Text = "Vendor Information";
            //
            // txtVendorName
            //
            this.txtVendorName.Location = new System.Drawing.Point(150, 30);
            this.txtVendorName.Name = "txtVendorName";
            this.txtVendorName.Size = new System.Drawing.Size(280, 23);
            this.txtVendorName.TabIndex = 1;
            //
            // txtVendorAddress
            //
            this.txtVendorAddress.Location = new System.Drawing.Point(150, 65);
            this.txtVendorAddress.Multiline = true;
            this.txtVendorAddress.Name = "txtVendorAddress";
            this.txtVendorAddress.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtVendorAddress.Size = new System.Drawing.Size(280, 60);
            this.txtVendorAddress.TabIndex = 3;
            //
            // txtVendorTIN
            //
            this.txtVendorTIN.Location = new System.Drawing.Point(150, 135);
            this.txtVendorTIN.Name = "txtVendorTIN";
            this.txtVendorTIN.Size = new System.Drawing.Size(280, 23);
            this.txtVendorTIN.TabIndex = 5;
            //
            // btnSaveVendor
            //
            this.btnSaveVendor.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(41)))), ((int)(((byte)(128)))), ((int)(((byte)(185)))));
            this.btnSaveVendor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSaveVendor.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnSaveVendor.ForeColor = System.Drawing.Color.White;
            this.btnSaveVendor.Location = new System.Drawing.Point(150, 170);
            this.btnSaveVendor.Name = "btnSaveVendor";
            this.btnSaveVendor.Size = new System.Drawing.Size(120, 32);
            this.btnSaveVendor.TabIndex = 7;
            this.btnSaveVendor.Text = "Save Vendor";
            this.btnSaveVendor.UseVisualStyleBackColor = false;
            this.btnSaveVendor.Click += new System.EventHandler(this.btnSaveVendor_Click);
            //
            // lblVendorNameCaption
            //
            this.lblVendorNameCaption.AutoSize = true;
            this.lblVendorNameCaption.Location = new System.Drawing.Point(20, 33);
            this.lblVendorNameCaption.Name = "lblVendorNameCaption";
            this.lblVendorNameCaption.Size = new System.Drawing.Size(84, 15);
            this.lblVendorNameCaption.TabIndex = 0;
            this.lblVendorNameCaption.Text = "Vendor Name:";
            //
            // lblVendorAddressCaption
            //
            this.lblVendorAddressCaption.AutoSize = true;
            this.lblVendorAddressCaption.Location = new System.Drawing.Point(20, 68);
            this.lblVendorAddressCaption.Name = "lblVendorAddressCaption";
            this.lblVendorAddressCaption.Size = new System.Drawing.Size(52, 15);
            this.lblVendorAddressCaption.TabIndex = 2;
            this.lblVendorAddressCaption.Text = "Address:";
            //
            // lblVendorTINCaption
            //
            this.lblVendorTINCaption.AutoSize = true;
            this.lblVendorTINCaption.Location = new System.Drawing.Point(20, 138);
            this.lblVendorTINCaption.Name = "lblVendorTINCaption";
            this.lblVendorTINCaption.Size = new System.Drawing.Size(30, 15);
            this.lblVendorTINCaption.TabIndex = 4;
            this.lblVendorTINCaption.Text = "TIN:";
            //
            // groupBoxArchive
            //
            this.groupBoxArchive.Controls.Add(this.chkArchived);
            this.groupBoxArchive.Controls.Add(this.txtArchiveReason);
            this.groupBoxArchive.Controls.Add(this.lblArchiveReasonCaption);
            this.groupBoxArchive.Location = new System.Drawing.Point(10, 370);
            this.groupBoxArchive.Name = "groupBoxArchive";
            this.groupBoxArchive.Size = new System.Drawing.Size(450, 135);
            this.groupBoxArchive.TabIndex = 3;
            this.groupBoxArchive.TabStop = false;
            this.groupBoxArchive.Text = "Archive";
            //
            // chkArchived
            //
            this.chkArchived.AutoSize = true;
            this.chkArchived.Location = new System.Drawing.Point(20, 30);
            this.chkArchived.Name = "chkArchived";
            this.chkArchived.Size = new System.Drawing.Size(74, 19);
            this.chkArchived.TabIndex = 0;
            this.chkArchived.Text = "Archived";
            this.chkArchived.UseVisualStyleBackColor = true;
            //
            // txtArchiveReason
            //
            this.txtArchiveReason.Location = new System.Drawing.Point(20, 82);
            this.txtArchiveReason.Multiline = true;
            this.txtArchiveReason.Name = "txtArchiveReason";
            this.txtArchiveReason.Size = new System.Drawing.Size(410, 38);
            this.txtArchiveReason.TabIndex = 2;
            //
            // lblArchiveReasonCaption
            //
            this.lblArchiveReasonCaption.AutoSize = true;
            this.lblArchiveReasonCaption.Location = new System.Drawing.Point(20, 63);
            this.lblArchiveReasonCaption.Name = "lblArchiveReasonCaption";
            this.lblArchiveReasonCaption.Size = new System.Drawing.Size(93, 15);
            this.lblArchiveReasonCaption.TabIndex = 1;
            this.lblArchiveReasonCaption.Text = "Archive Reason:";
            //
            // groupBoxSiteInfo
            //
            this.groupBoxSiteInfo.Controls.Add(this.txtSiteDisplay);
            this.groupBoxSiteInfo.Controls.Add(this.lblSiteDisplayCaption);
            this.groupBoxSiteInfo.Location = new System.Drawing.Point(480, 470);
            this.groupBoxSiteInfo.Name = "groupBoxSiteInfo";
            this.groupBoxSiteInfo.Size = new System.Drawing.Size(450, 80);
            this.groupBoxSiteInfo.TabIndex = 4;
            this.groupBoxSiteInfo.TabStop = false;
            this.groupBoxSiteInfo.Text = "Site Information";
            //
            // txtSiteDisplay
            //
            this.txtSiteDisplay.Location = new System.Drawing.Point(150, 30);
            this.txtSiteDisplay.Name = "txtSiteDisplay";
            this.txtSiteDisplay.ReadOnly = true;
            this.txtSiteDisplay.Size = new System.Drawing.Size(280, 23);
            this.txtSiteDisplay.TabIndex = 1;
            //
            // lblSiteDisplayCaption
            //
            this.lblSiteDisplayCaption.AutoSize = true;
            this.lblSiteDisplayCaption.Location = new System.Drawing.Point(20, 33);
            this.lblSiteDisplayCaption.Name = "lblSiteDisplayCaption";
            this.lblSiteDisplayCaption.Size = new System.Drawing.Size(30, 15);
            this.lblSiteDisplayCaption.TabIndex = 0;
            this.lblSiteDisplayCaption.Text = "Site:";
            //
            // groupBoxFinancialInfo
            //
            this.groupBoxFinancialInfo.Controls.Add(this.txtSubtotal);
            this.groupBoxFinancialInfo.Controls.Add(this.txtVatAmount);
            this.groupBoxFinancialInfo.Controls.Add(this.txtWhtAmount);
            this.groupBoxFinancialInfo.Controls.Add(this.txtDiscountAmount);
            this.groupBoxFinancialInfo.Controls.Add(this.txtTotalAmountDue);
            this.groupBoxFinancialInfo.Controls.Add(this.lblSubtotalCaption);
            this.groupBoxFinancialInfo.Controls.Add(this.lblVatAmountCaption);
            this.groupBoxFinancialInfo.Controls.Add(this.lblWhtAmountCaption);
            this.groupBoxFinancialInfo.Controls.Add(this.lblDiscountAmountCaption);
            this.groupBoxFinancialInfo.Controls.Add(this.lblTotalAmountDueCaption);
            this.groupBoxFinancialInfo.Location = new System.Drawing.Point(10, 500);
            this.groupBoxFinancialInfo.Name = "groupBoxFinancialInfo";
            this.groupBoxFinancialInfo.Size = new System.Drawing.Size(450, 220);
            this.groupBoxFinancialInfo.TabIndex = 5;
            this.groupBoxFinancialInfo.TabStop = false;
            this.groupBoxFinancialInfo.Text = "Financial Information";
            //
            // txtSubtotal
            //
            this.txtSubtotal.Location = new System.Drawing.Point(150, 30);
            this.txtSubtotal.Name = "txtSubtotal";
            this.txtSubtotal.ReadOnly = true;
            this.txtSubtotal.Size = new System.Drawing.Size(280, 23);
            this.txtSubtotal.TabIndex = 1;
            //
            // txtVatAmount
            //
            this.txtVatAmount.Location = new System.Drawing.Point(150, 65);
            this.txtVatAmount.Name = "txtVatAmount";
            this.txtVatAmount.ReadOnly = true;
            this.txtVatAmount.Size = new System.Drawing.Size(280, 23);
            this.txtVatAmount.TabIndex = 3;
            //
            // txtWhtAmount
            //
            this.txtWhtAmount.Location = new System.Drawing.Point(150, 100);
            this.txtWhtAmount.Name = "txtWhtAmount";
            this.txtWhtAmount.ReadOnly = true;
            this.txtWhtAmount.Size = new System.Drawing.Size(280, 23);
            this.txtWhtAmount.TabIndex = 5;
            //
            // txtDiscountAmount
            //
            this.txtDiscountAmount.Location = new System.Drawing.Point(150, 135);
            this.txtDiscountAmount.Name = "txtDiscountAmount";
            this.txtDiscountAmount.ReadOnly = true;
            this.txtDiscountAmount.Size = new System.Drawing.Size(280, 23);
            this.txtDiscountAmount.TabIndex = 7;
            //
            // txtTotalAmountDue
            //
            this.txtTotalAmountDue.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.txtTotalAmountDue.Location = new System.Drawing.Point(150, 175);
            this.txtTotalAmountDue.Name = "txtTotalAmountDue";
            this.txtTotalAmountDue.ReadOnly = true;
            this.txtTotalAmountDue.Size = new System.Drawing.Size(280, 23);
            this.txtTotalAmountDue.TabIndex = 9;
            //
            // lblSubtotalCaption
            //
            this.lblSubtotalCaption.AutoSize = true;
            this.lblSubtotalCaption.Location = new System.Drawing.Point(20, 33);
            this.lblSubtotalCaption.Name = "lblSubtotalCaption";
            this.lblSubtotalCaption.Size = new System.Drawing.Size(54, 15);
            this.lblSubtotalCaption.TabIndex = 0;
            this.lblSubtotalCaption.Text = "Subtotal:";
            //
            // lblVatAmountCaption
            //
            this.lblVatAmountCaption.AutoSize = true;
            this.lblVatAmountCaption.Location = new System.Drawing.Point(20, 68);
            this.lblVatAmountCaption.Name = "lblVatAmountCaption";
            this.lblVatAmountCaption.Size = new System.Drawing.Size(75, 15);
            this.lblVatAmountCaption.TabIndex = 2;
            this.lblVatAmountCaption.Text = "VAT (12%):";
            //
            // lblWhtAmountCaption
            //
            this.lblWhtAmountCaption.AutoSize = true;
            this.lblWhtAmountCaption.Location = new System.Drawing.Point(20, 103);
            this.lblWhtAmountCaption.Name = "lblWhtAmountCaption";
            this.lblWhtAmountCaption.Size = new System.Drawing.Size(73, 15);
            this.lblWhtAmountCaption.TabIndex = 4;
            this.lblWhtAmountCaption.Text = "WHT (2%):";
            //
            // lblDiscountAmountCaption
            //
            this.lblDiscountAmountCaption.AutoSize = true;
            this.lblDiscountAmountCaption.Location = new System.Drawing.Point(20, 138);
            this.lblDiscountAmountCaption.Name = "lblDiscountAmountCaption";
            this.lblDiscountAmountCaption.Size = new System.Drawing.Size(60, 15);
            this.lblDiscountAmountCaption.TabIndex = 6;
            this.lblDiscountAmountCaption.Text = "Discount:";
            //
            // lblTotalAmountDueCaption
            //
            this.lblTotalAmountDueCaption.AutoSize = true;
            this.lblTotalAmountDueCaption.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblTotalAmountDueCaption.Location = new System.Drawing.Point(20, 178);
            this.lblTotalAmountDueCaption.Name = "lblTotalAmountDueCaption";
            this.lblTotalAmountDueCaption.Size = new System.Drawing.Size(88, 15);
            this.lblTotalAmountDueCaption.TabIndex = 8;
            this.lblTotalAmountDueCaption.Text = "Total Amount:";
            //
            // tabPageHistory
            //
            this.tabPageHistory.Controls.Add(this.dgvRenewalHistory);
            this.tabPageHistory.Location = new System.Drawing.Point(4, 24);
            this.tabPageHistory.Name = "tabPageHistory";
            this.tabPageHistory.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageHistory.Size = new System.Drawing.Size(972, 452);
            this.tabPageHistory.TabIndex = 1;
            this.tabPageHistory.Text = "Renewal History";
            this.tabPageHistory.UseVisualStyleBackColor = true;
            //
            // dgvRenewalHistory
            //
            this.dgvRenewalHistory.AllowUserToAddRows = false;
            this.dgvRenewalHistory.AllowUserToDeleteRows = false;
            this.dgvRenewalHistory.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvRenewalHistory.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvRenewalHistory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvRenewalHistory.Location = new System.Drawing.Point(3, 3);
            this.dgvRenewalHistory.Name = "dgvRenewalHistory";
            this.dgvRenewalHistory.ReadOnly = true;
            this.dgvRenewalHistory.RowTemplate.Height = 25;
            this.dgvRenewalHistory.Size = new System.Drawing.Size(966, 446);
            this.dgvRenewalHistory.TabIndex = 0;
            //
            // panelBottom
            //
            this.panelBottom.Controls.Add(this.btnCreateRenewal);
            this.panelBottom.Controls.Add(this.btnArchive);
            this.panelBottom.Controls.Add(this.btnClose);
            this.panelBottom.Controls.Add(this.lblCreatedBy);
            this.panelBottom.Controls.Add(this.lblCreatedAt);
            this.panelBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBottom.Location = new System.Drawing.Point(0, 600);
            this.panelBottom.Name = "panelBottom";
            this.panelBottom.Size = new System.Drawing.Size(1000, 60);
            this.panelBottom.TabIndex = 2;
            //
            // btnCreateRenewal
            //
            this.btnCreateRenewal.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(46)))), ((int)(((byte)(204)))), ((int)(((byte)(113)))));
            this.btnCreateRenewal.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCreateRenewal.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnCreateRenewal.ForeColor = System.Drawing.Color.White;
            this.btnCreateRenewal.Location = new System.Drawing.Point(20, 15);
            this.btnCreateRenewal.Name = "btnCreateRenewal";
            this.btnCreateRenewal.Size = new System.Drawing.Size(130, 35);
            this.btnCreateRenewal.TabIndex = 0;
            this.btnCreateRenewal.Text = "Create Renewal";
            this.btnCreateRenewal.UseVisualStyleBackColor = false;
            this.btnCreateRenewal.Click += new System.EventHandler(this.btnCreateRenewal_Click);
            //
            // btnArchive
            //
            this.btnArchive.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(231)))), ((int)(((byte)(76)))), ((int)(((byte)(60)))));
            this.btnArchive.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnArchive.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnArchive.ForeColor = System.Drawing.Color.White;
            this.btnArchive.Location = new System.Drawing.Point(160, 15);
            this.btnArchive.Name = "btnArchive";
            this.btnArchive.Size = new System.Drawing.Size(110, 35);
            this.btnArchive.TabIndex = 1;
            this.btnArchive.Text = "Archive";
            this.btnArchive.UseVisualStyleBackColor = false;
            this.btnArchive.Click += new System.EventHandler(this.btnArchive_Click);
            //
            // btnClose
            //
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(127)))), ((int)(((byte)(140)))), ((int)(((byte)(141)))));
            this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClose.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnClose.ForeColor = System.Drawing.Color.White;
            this.btnClose.Location = new System.Drawing.Point(870, 15);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(110, 35);
            this.btnClose.TabIndex = 2;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = false;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // lblCreatedBy
            //
            this.lblCreatedBy.AutoSize = true;
            this.lblCreatedBy.Location = new System.Drawing.Point(400, 15);
            this.lblCreatedBy.Name = "lblCreatedBy";
            this.lblCreatedBy.Size = new System.Drawing.Size(100, 15);
            this.lblCreatedBy.TabIndex = 3;
            this.lblCreatedBy.Text = "Created By: ...";
            //
            // lblCreatedAt
            //
            this.lblCreatedAt.AutoSize = true;
            this.lblCreatedAt.Location = new System.Drawing.Point(400, 35);
            this.lblCreatedAt.Name = "lblCreatedAt";
            this.lblCreatedAt.Size = new System.Drawing.Size(100, 15);
            this.lblCreatedAt.TabIndex = 4;
            this.lblCreatedAt.Text = "Created: ...";
            //
            // ViewRenewalDetailPage
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1000, 660);
            this.Controls.Add(this.panelMain);
            this.Controls.Add(this.panelTop);
            this.Controls.Add(this.panelBottom);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.Name = "ViewRenewalDetailPage";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Renewal Details";
            this.Load += new System.EventHandler(this.ViewRenewalDetailPage_Load);
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.panelMain.ResumeLayout(false);
            this.tabControl.ResumeLayout(false);
            this.tabPageDetails.ResumeLayout(false);
            this.panelDetails.ResumeLayout(false);
            this.groupBoxItemInfo.ResumeLayout(false);
            this.groupBoxItemInfo.PerformLayout();
            this.groupBoxRenewalInfo.ResumeLayout(false);
            this.groupBoxRenewalInfo.PerformLayout();
            this.groupBoxVendorInfo.ResumeLayout(false);
            this.groupBoxVendorInfo.PerformLayout();
            this.groupBoxSiteInfo.ResumeLayout(false);
            this.groupBoxSiteInfo.PerformLayout();
            this.groupBoxFinancialInfo.ResumeLayout(false);
            this.groupBoxFinancialInfo.PerformLayout();
            this.groupBoxArchive.ResumeLayout(false);
            this.groupBoxArchive.PerformLayout();
            this.tabPageHistory.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvRenewalHistory)).EndInit();
            this.panelBottom.ResumeLayout(false);
            this.panelBottom.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label lblExpiryStatus;
        private System.Windows.Forms.Label lblItemName;
        private System.Windows.Forms.Label lblItemType;
        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabPageDetails;
        private System.Windows.Forms.Panel panelDetails;
        private System.Windows.Forms.GroupBox groupBoxItemInfo;
        private System.Windows.Forms.TextBox txtItemName;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.TextBox txtItemType;
        private System.Windows.Forms.TextBox txtCategory;
        private System.Windows.Forms.TextBox txtSerialNumber;
        private System.Windows.Forms.TextBox txtModelNumber;
        private System.Windows.Forms.TextBox txtLicenseNumber;
        private System.Windows.Forms.TextBox txtAmount;
        private System.Windows.Forms.TextBox txtPartNumber;
        private System.Windows.Forms.Label lblItemNameCaption;
        private System.Windows.Forms.Label lblDescriptionCaption;
        private System.Windows.Forms.Label lblItemTypeCaption;
        private System.Windows.Forms.Label lblCategoryCaption;
        private System.Windows.Forms.Label lblSerialNumberCaption;
        private System.Windows.Forms.Label lblModelNumberCaption;
        private System.Windows.Forms.Label lblLicenseNumberCaption;
        private System.Windows.Forms.Label lblAmountCaption;
        private System.Windows.Forms.Label lblPartNumberCaption;
        private System.Windows.Forms.GroupBox groupBoxRenewalInfo;
        private System.Windows.Forms.TextBox txtRenewalStatus;
        private System.Windows.Forms.DateTimePicker dtpStartDate;
        private System.Windows.Forms.DateTimePicker dtpEndDate;
        private System.Windows.Forms.TextBox txtDaysLeft;
        private System.Windows.Forms.Label lblExpiryWarning;
        private System.Windows.Forms.Label lblRenewalStatusCaption;
        private System.Windows.Forms.Label lblStartDateCaption;
        private System.Windows.Forms.Label lblEndDateCaption;
        private System.Windows.Forms.Label lblDaysLeftCaption;
        private System.Windows.Forms.GroupBox groupBoxVendorInfo;
        private System.Windows.Forms.TextBox txtVendorName;
        private System.Windows.Forms.TextBox txtVendorAddress;
        private System.Windows.Forms.TextBox txtVendorTIN;
        private System.Windows.Forms.Button btnSaveVendor;
        private System.Windows.Forms.Label lblVendorNameCaption;
        private System.Windows.Forms.Label lblVendorAddressCaption;
        private System.Windows.Forms.Label lblVendorTINCaption;
        private System.Windows.Forms.GroupBox groupBoxArchive;
        private System.Windows.Forms.CheckBox chkArchived;
        private System.Windows.Forms.TextBox txtArchiveReason;
        private System.Windows.Forms.Label lblArchiveReasonCaption;
        private System.Windows.Forms.TabPage tabPageHistory;
        private System.Windows.Forms.DataGridView dgvRenewalHistory;
        private System.Windows.Forms.Panel panelBottom;
        private System.Windows.Forms.Button btnCreateRenewal;
        private System.Windows.Forms.Button btnArchive;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Label lblCreatedBy;
        private System.Windows.Forms.Label lblCreatedAt;
        private System.Windows.Forms.GroupBox groupBoxSiteInfo;
        private System.Windows.Forms.TextBox txtSiteDisplay;
        private System.Windows.Forms.Label lblSiteDisplayCaption;
        private System.Windows.Forms.GroupBox groupBoxFinancialInfo;
        private System.Windows.Forms.TextBox txtSubtotal;
        private System.Windows.Forms.TextBox txtVatAmount;
        private System.Windows.Forms.TextBox txtWhtAmount;
        private System.Windows.Forms.TextBox txtDiscountAmount;
        private System.Windows.Forms.TextBox txtTotalAmountDue;
        private System.Windows.Forms.Label lblSubtotalCaption;
        private System.Windows.Forms.Label lblVatAmountCaption;
        private System.Windows.Forms.Label lblWhtAmountCaption;
        private System.Windows.Forms.Label lblDiscountAmountCaption;
        private System.Windows.Forms.Label lblTotalAmountDueCaption;
    }
}
