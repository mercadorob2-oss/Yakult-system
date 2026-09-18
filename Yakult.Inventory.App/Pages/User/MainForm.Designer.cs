namespace Yakult.Inventory.App.Pages.User
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnNotification;
        private System.Windows.Forms.Panel notificationPanel;
        private bool isNotificationPanelVisible = false;
        private System.Windows.Forms.Label lblConnectionStatus;
        private System.Windows.Forms.Timer connectionCheckTimer;

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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.lblConnectionStatus = new System.Windows.Forms.Label();
            this.connectionCheckTimer = new System.Windows.Forms.Timer(this.components);
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.homeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.LogOutOnMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.masterDataToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.companyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.branchToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.departmentToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.employeeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.itemToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.requestToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.softwareServiceSetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewDataToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewInventoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewItemsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewEmployeesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewCompaniesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewBranchesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewDepartmentsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewVendorsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewSetsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewRequestsToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.viewReceiptsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewInvoicesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewRenewalsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewWarrantyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewArchiveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewUpdatesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewItemMovementAuditToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.UserStatusLbl = new System.Windows.Forms.ToolStripStatusLabel();
            this.ContentPanel = new System.Windows.Forms.Panel();
            this.btnNotification = new System.Windows.Forms.Button();
            this.notificationPanel = new System.Windows.Forms.Panel();
            this.menuStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblConnectionStatus
            // 
            this.lblConnectionStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblConnectionStatus.AutoSize = true;
            this.lblConnectionStatus.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblConnectionStatus.ForeColor = System.Drawing.Color.Gray;
            this.lblConnectionStatus.Location = new System.Drawing.Point(580, 6);
            this.lblConnectionStatus.Name = "lblConnectionStatus";
            this.lblConnectionStatus.Size = new System.Drawing.Size(155, 20);
            this.lblConnectionStatus.TabIndex = 5;
            this.lblConnectionStatus.Text = "Checking connection...";
            // 
            // connectionCheckTimer
            // 
            this.connectionCheckTimer.Interval = 60000;
            this.connectionCheckTimer.Tick += new System.EventHandler(this.connectionCheckTimer_Tick);
            // 
            // menuStrip1
            // 
            this.menuStrip1.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.homeToolStripMenuItem,
            this.LogOutOnMenu,
            this.masterDataToolStripMenuItem,
            this.viewDataToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(800, 28);
            this.menuStrip1.TabIndex = 0;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // homeToolStripMenuItem
            // 
            this.homeToolStripMenuItem.BackColor = System.Drawing.Color.LightGreen;
            this.homeToolStripMenuItem.Name = "homeToolStripMenuItem";
            this.homeToolStripMenuItem.Size = new System.Drawing.Size(64, 24);
            this.homeToolStripMenuItem.Text = "Home";
            this.homeToolStripMenuItem.Click += new System.EventHandler(this.homeToolStripMenuItem_Click);
            // 
            // LogOutOnMenu
            // 
            this.LogOutOnMenu.BackColor = System.Drawing.Color.Red;
            this.LogOutOnMenu.ForeColor = System.Drawing.Color.Snow;
            this.LogOutOnMenu.Name = "LogOutOnMenu";
            this.LogOutOnMenu.Size = new System.Drawing.Size(70, 24);
            this.LogOutOnMenu.Text = "Logout";
            this.LogOutOnMenu.Click += new System.EventHandler(this.LogOutOnMenu_Click);
            // 
            // masterDataToolStripMenuItem
            // 
            this.masterDataToolStripMenuItem.BackColor = System.Drawing.Color.MistyRose;
            this.masterDataToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.companyToolStripMenuItem,
            this.branchToolStripMenuItem,
            this.departmentToolStripMenuItem,
            this.employeeToolStripMenuItem,
            this.itemToolStripMenuItem,
            this.requestToolStripMenuItem,
            this.softwareServiceSetToolStripMenuItem});
            this.masterDataToolStripMenuItem.Name = "masterDataToolStripMenuItem";
            this.masterDataToolStripMenuItem.Size = new System.Drawing.Size(136, 24);
            this.masterDataToolStripMenuItem.Text = "Add Master Data";
            this.masterDataToolStripMenuItem.Click += new System.EventHandler(this.masterDataToolStripMenuItem_Click);
            // 
            // companyToolStripMenuItem
            // 
            this.companyToolStripMenuItem.Name = "companyToolStripMenuItem";
            this.companyToolStripMenuItem.Size = new System.Drawing.Size(229, 26);
            this.companyToolStripMenuItem.Text = "Company";
            this.companyToolStripMenuItem.Click += new System.EventHandler(this.companyToolStripMenuItem_Click);
            // 
            // branchToolStripMenuItem
            // 
            this.branchToolStripMenuItem.Name = "branchToolStripMenuItem";
            this.branchToolStripMenuItem.Size = new System.Drawing.Size(229, 26);
            this.branchToolStripMenuItem.Text = "Branch";
            this.branchToolStripMenuItem.Click += new System.EventHandler(this.branchToolStripMenuItem_Click);
            // 
            // departmentToolStripMenuItem
            // 
            this.departmentToolStripMenuItem.Name = "departmentToolStripMenuItem";
            this.departmentToolStripMenuItem.Size = new System.Drawing.Size(229, 26);
            this.departmentToolStripMenuItem.Text = "Department";
            this.departmentToolStripMenuItem.Click += new System.EventHandler(this.departmentToolStripMenuItem_Click);
            // 
            // employeeToolStripMenuItem
            // 
            this.employeeToolStripMenuItem.Name = "employeeToolStripMenuItem";
            this.employeeToolStripMenuItem.Size = new System.Drawing.Size(229, 26);
            this.employeeToolStripMenuItem.Text = "Employee";
            this.employeeToolStripMenuItem.Click += new System.EventHandler(this.employeeToolStripMenuItem_Click);
            // 
            // itemToolStripMenuItem
            // 
            this.itemToolStripMenuItem.Name = "itemToolStripMenuItem";
            this.itemToolStripMenuItem.Size = new System.Drawing.Size(229, 26);
            this.itemToolStripMenuItem.Text = "Item";
            this.itemToolStripMenuItem.Click += new System.EventHandler(this.itemToolStripMenuItem_Click);
            // 
            // requestToolStripMenuItem
            // 
            this.requestToolStripMenuItem.Name = "requestToolStripMenuItem";
            this.requestToolStripMenuItem.Size = new System.Drawing.Size(229, 26);
            this.requestToolStripMenuItem.Text = "Request";
            this.requestToolStripMenuItem.Click += new System.EventHandler(this.requestToolStripMenuItem_Click);
            // 
            // softwareServiceSetToolStripMenuItem
            // 
            this.softwareServiceSetToolStripMenuItem.Name = "softwareServiceSetToolStripMenuItem";
            this.softwareServiceSetToolStripMenuItem.Size = new System.Drawing.Size(229, 26);
            this.softwareServiceSetToolStripMenuItem.Text = "Software/Service Set";
            this.softwareServiceSetToolStripMenuItem.Click += new System.EventHandler(this.softwareServiceSetToolStripMenuItem_Click);
            // 
            // viewDataToolStripMenuItem
            // 
            this.viewDataToolStripMenuItem.BackColor = System.Drawing.SystemColors.MenuHighlight;
            this.viewDataToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.viewInventoryToolStripMenuItem,
            this.viewItemsToolStripMenuItem,
            this.viewEmployeesToolStripMenuItem,
            this.viewCompaniesToolStripMenuItem,
            this.viewBranchesToolStripMenuItem,
            this.viewDepartmentsToolStripMenuItem,
            this.viewVendorsToolStripMenuItem,
            this.viewSetsToolStripMenuItem,
            this.viewRequestsToolStripMenuItem1,
            this.viewReceiptsToolStripMenuItem,
            this.viewInvoicesToolStripMenuItem,
            this.viewRenewalsToolStripMenuItem,
            this.viewWarrantyToolStripMenuItem,
            this.viewArchiveToolStripMenuItem,
            this.viewUpdatesToolStripMenuItem,
            this.viewItemMovementAuditToolStripMenuItem});
            this.viewDataToolStripMenuItem.ForeColor = System.Drawing.SystemColors.ControlText;
            this.viewDataToolStripMenuItem.Name = "viewDataToolStripMenuItem";
            this.viewDataToolStripMenuItem.Size = new System.Drawing.Size(140, 24);
            this.viewDataToolStripMenuItem.Text = "View Master Data";
            // 
            // viewInventoryToolStripMenuItem
            // 
            this.viewInventoryToolStripMenuItem.Name = "viewInventoryToolStripMenuItem";
            this.viewInventoryToolStripMenuItem.Size = new System.Drawing.Size(214, 26);
            this.viewInventoryToolStripMenuItem.Text = "View Inventory";
            this.viewInventoryToolStripMenuItem.Click += new System.EventHandler(this.viewInventoryToolStripMenuItem_Click_1);
            // 
            // viewItemsToolStripMenuItem
            // 
            this.viewItemsToolStripMenuItem.Name = "viewItemsToolStripMenuItem";
            this.viewItemsToolStripMenuItem.Size = new System.Drawing.Size(214, 26);
            this.viewItemsToolStripMenuItem.Text = "View Items";
            this.viewItemsToolStripMenuItem.Click += new System.EventHandler(this.viewItemsToolStripMenuItem_Click_1);
            // 
            // viewEmployeesToolStripMenuItem
            // 
            this.viewEmployeesToolStripMenuItem.Name = "viewEmployeesToolStripMenuItem";
            this.viewEmployeesToolStripMenuItem.Size = new System.Drawing.Size(214, 26);
            this.viewEmployeesToolStripMenuItem.Text = "View Employees";
            this.viewEmployeesToolStripMenuItem.Click += new System.EventHandler(this.viewEmployeesToolStripMenuItem_Click_1);
            // 
            // viewCompaniesToolStripMenuItem
            // 
            this.viewCompaniesToolStripMenuItem.Name = "viewCompaniesToolStripMenuItem";
            this.viewCompaniesToolStripMenuItem.Size = new System.Drawing.Size(214, 26);
            this.viewCompaniesToolStripMenuItem.Text = "View Companies";
            this.viewCompaniesToolStripMenuItem.Click += new System.EventHandler(this.viewCompaniesToolStripMenuItem_Click_1);
            // 
            // viewBranchesToolStripMenuItem
            // 
            this.viewBranchesToolStripMenuItem.Name = "viewBranchesToolStripMenuItem";
            this.viewBranchesToolStripMenuItem.Size = new System.Drawing.Size(214, 26);
            this.viewBranchesToolStripMenuItem.Text = "View Branches";
            this.viewBranchesToolStripMenuItem.Click += new System.EventHandler(this.viewBranchesToolStripMenuItem_Click_1);
            // 
            // viewDepartmentsToolStripMenuItem
            // 
            this.viewDepartmentsToolStripMenuItem.Name = "viewDepartmentsToolStripMenuItem";
            this.viewDepartmentsToolStripMenuItem.Size = new System.Drawing.Size(214, 26);
            this.viewDepartmentsToolStripMenuItem.Text = "View Departments";
            this.viewDepartmentsToolStripMenuItem.Click += new System.EventHandler(this.viewDepartmentsToolStripMenuItem_Click_1);
            // 
            // viewVendorsToolStripMenuItem
            // 
            this.viewVendorsToolStripMenuItem.Name = "viewVendorsToolStripMenuItem";
            this.viewVendorsToolStripMenuItem.Size = new System.Drawing.Size(214, 26);
            this.viewVendorsToolStripMenuItem.Text = "View Vendors";
            this.viewVendorsToolStripMenuItem.Click += new System.EventHandler(this.viewVendorsToolStripMenuItem_Click);
            // 
            // viewSetsToolStripMenuItem
            // 
            this.viewSetsToolStripMenuItem.Name = "viewSetsToolStripMenuItem";
            this.viewSetsToolStripMenuItem.Size = new System.Drawing.Size(214, 26);
            this.viewSetsToolStripMenuItem.Text = "View Sets";
            this.viewSetsToolStripMenuItem.Click += new System.EventHandler(this.viewSetsToolStripMenuItem_Click_1);
            // 
            // viewRequestsToolStripMenuItem1
            // 
            this.viewRequestsToolStripMenuItem1.Name = "viewRequestsToolStripMenuItem1";
            this.viewRequestsToolStripMenuItem1.Size = new System.Drawing.Size(214, 26);
            this.viewRequestsToolStripMenuItem1.Text = "View Requests";
            this.viewRequestsToolStripMenuItem1.Click += new System.EventHandler(this.viewRequestsToolStripMenuItem1_Click);
            // 
            // viewReceiptsToolStripMenuItem
            // 
            this.viewReceiptsToolStripMenuItem.Name = "viewReceiptsToolStripMenuItem";
            this.viewReceiptsToolStripMenuItem.Size = new System.Drawing.Size(214, 26);
            this.viewReceiptsToolStripMenuItem.Text = "View Receipts";
            this.viewReceiptsToolStripMenuItem.Click += new System.EventHandler(this.viewReceiptsToolStripMenuItem_Click);
            // 
            // viewInvoicesToolStripMenuItem
            // 
            this.viewInvoicesToolStripMenuItem.Name = "viewInvoicesToolStripMenuItem";
            this.viewInvoicesToolStripMenuItem.Size = new System.Drawing.Size(214, 26);
            this.viewInvoicesToolStripMenuItem.Text = "View Invoices";
            this.viewInvoicesToolStripMenuItem.Click += new System.EventHandler(this.viewInvoicesToolStripMenuItem_Click);
            //
            // viewRenewalsToolStripMenuItem
            // 
            this.viewRenewalsToolStripMenuItem.Name = "viewRenewalsToolStripMenuItem";
            this.viewRenewalsToolStripMenuItem.Size = new System.Drawing.Size(214, 26);
            this.viewRenewalsToolStripMenuItem.Text = "View Renewals";
            this.viewRenewalsToolStripMenuItem.Click += new System.EventHandler(this.viewRenewalsToolStripMenuItem_Click);
            // 
            // viewWarrantyToolStripMenuItem
            // 
            this.viewWarrantyToolStripMenuItem.Name = "viewWarrantyToolStripMenuItem";
            this.viewWarrantyToolStripMenuItem.Size = new System.Drawing.Size(214, 26);
            this.viewWarrantyToolStripMenuItem.Text = "View Warranty";
            this.viewWarrantyToolStripMenuItem.Click += new System.EventHandler(this.viewWarrantyToolStripMenuItem_Click);
            // 
            // viewArchiveToolStripMenuItem
            // 
            this.viewArchiveToolStripMenuItem.Name = "viewArchiveToolStripMenuItem";
            this.viewArchiveToolStripMenuItem.Size = new System.Drawing.Size(214, 26);
            this.viewArchiveToolStripMenuItem.Text = "View Archive";
            this.viewArchiveToolStripMenuItem.Click += new System.EventHandler(this.viewArchiveToolStripMenuItem_Click);
            // 
            // viewUpdatesToolStripMenuItem
            // 
            this.viewUpdatesToolStripMenuItem.Name = "viewUpdatesToolStripMenuItem";
            this.viewUpdatesToolStripMenuItem.Size = new System.Drawing.Size(214, 26);
            this.viewUpdatesToolStripMenuItem.Text = "View Updates";
            this.viewUpdatesToolStripMenuItem.Click += new System.EventHandler(this.viewUpdatesToolStripMenuItem_Click);
            // 
            // viewItemMovementAuditToolStripMenuItem
            // 
            this.viewItemMovementAuditToolStripMenuItem.Name = "viewItemMovementAuditToolStripMenuItem";
            this.viewItemMovementAuditToolStripMenuItem.Size = new System.Drawing.Size(214, 26);
            this.viewItemMovementAuditToolStripMenuItem.Text = "View Item Audit Trail";
            this.viewItemMovementAuditToolStripMenuItem.Click += new System.EventHandler(this.viewItemMovementAuditToolStripMenuItem_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.UserStatusLbl});
            this.statusStrip1.Location = new System.Drawing.Point(0, 424);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(800, 26);
            this.statusStrip1.TabIndex = 1;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // UserStatusLbl
            // 
            this.UserStatusLbl.Name = "UserStatusLbl";
            this.UserStatusLbl.Size = new System.Drawing.Size(50, 20);
            this.UserStatusLbl.Text = "Ready";
            // 
            // ContentPanel
            // 
            this.ContentPanel.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.ContentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ContentPanel.Location = new System.Drawing.Point(0, 28);
            this.ContentPanel.Name = "ContentPanel";
            this.ContentPanel.Size = new System.Drawing.Size(800, 396);
            this.ContentPanel.TabIndex = 2;
            // 
            // btnNotification
            // 
            this.btnNotification.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNotification.BackColor = System.Drawing.Color.Orange;
            this.btnNotification.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnNotification.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnNotification.ForeColor = System.Drawing.Color.White;
            this.btnNotification.Location = new System.Drawing.Point(740, 0);
            this.btnNotification.Name = "btnNotification";
            this.btnNotification.Size = new System.Drawing.Size(48, 28);
            this.btnNotification.TabIndex = 3;
            this.btnNotification.Text = "🔔";
            this.btnNotification.UseVisualStyleBackColor = false;
            this.btnNotification.Click += new System.EventHandler(this.btnNotification_Click);
            // 
            // notificationPanel
            //
            this.notificationPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.notificationPanel.BackColor = System.Drawing.Color.White;
            this.notificationPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.notificationPanel.Location = new System.Drawing.Point(330, 75);
            this.notificationPanel.Name = "notificationPanel";
            this.notificationPanel.Size = new System.Drawing.Size(460, 305);
            this.notificationPanel.TabIndex = 4;
            this.notificationPanel.Visible = false;
            //
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.lblConnectionStatus);
            this.Controls.Add(this.notificationPanel);
            this.Controls.Add(this.btnNotification);
            this.Controls.Add(this.ContentPanel);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "MainForm";
            this.Text = "Yakult Inventory Monitoring System";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem homeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem LogOutOnMenu;
        private System.Windows.Forms.ToolStripMenuItem masterDataToolStripMenuItem;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel UserStatusLbl;
        private System.Windows.Forms.Panel ContentPanel;
        private System.Windows.Forms.ToolStripMenuItem branchToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem departmentToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewDataToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewInventoryToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewItemsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewEmployeesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewCompaniesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewBranchesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewDepartmentsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewVendorsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewSetsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem softwareServiceSetToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewRequestsToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem viewReceiptsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewInvoicesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewRenewalsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewWarrantyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem companyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem employeeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem itemToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem requestToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewArchiveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewUpdatesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewItemMovementAuditToolStripMenuItem;
    }
}