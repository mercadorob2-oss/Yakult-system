namespace Yakult.Inventory.App.Pages.User
{
    partial class LoginPage
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
            this.MainPanel = new ReaLTaiizor.Controls.Panel();
            this.BrandPanel = new ReaLTaiizor.Controls.Panel();
            this.lblBrandSub = new System.Windows.Forms.Label();
            this.lblBrandTitle = new System.Windows.Forms.Label();
            this.logoCard = new ReaLTaiizor.Controls.Panel();
            this.picLogo = new System.Windows.Forms.PictureBox();
            this.lblContactAdmin = new System.Windows.Forms.Label();
            this.LoginBtn = new ReaLTaiizor.Controls.HopeButton();
            this.lblTogglePassword = new System.Windows.Forms.Label();
            this.PassLoginField = new ReaLTaiizor.Controls.HopeTextBox();
            this.lblPassCaption = new System.Windows.Forms.Label();
            this.NameLoginField = new ReaLTaiizor.Controls.HopeTextBox();
            this.lblUserCaption = new System.Windows.Forms.Label();
            this.lblSubtitle = new System.Windows.Forms.Label();
            this.TitleLabel = new ReaLTaiizor.Controls.BigLabel();
            this.MainPanel.SuspendLayout();
            this.BrandPanel.SuspendLayout();
            this.logoCard.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).BeginInit();
            this.SuspendLayout();
            //
            // MainPanel
            //
            this.MainPanel.BackColor = System.Drawing.Color.White;
            this.MainPanel.Controls.Add(this.BrandPanel);
            this.MainPanel.Controls.Add(this.lblContactAdmin);
            this.MainPanel.Controls.Add(this.LoginBtn);
            this.MainPanel.Controls.Add(this.lblTogglePassword);
            this.MainPanel.Controls.Add(this.PassLoginField);
            this.MainPanel.Controls.Add(this.lblPassCaption);
            this.MainPanel.Controls.Add(this.NameLoginField);
            this.MainPanel.Controls.Add(this.lblUserCaption);
            this.MainPanel.Controls.Add(this.lblSubtitle);
            this.MainPanel.Controls.Add(this.TitleLabel);
            this.MainPanel.EdgeColor = System.Drawing.Color.FromArgb(((int)(((byte)(235)))), ((int)(((byte)(235)))), ((int)(((byte)(240)))));
            this.MainPanel.Location = new System.Drawing.Point(80, 45);
            this.MainPanel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MainPanel.Name = "MainPanel";
            this.MainPanel.Padding = new System.Windows.Forms.Padding(0);
            this.MainPanel.Size = new System.Drawing.Size(680, 410);
            this.MainPanel.SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            this.MainPanel.TabIndex = 0;
            this.MainPanel.Text = "panel1";
            //
            // BrandPanel
            //
            this.BrandPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(6)))), ((int)(((byte)(19)))));
            this.BrandPanel.Controls.Add(this.lblBrandSub);
            this.BrandPanel.Controls.Add(this.lblBrandTitle);
            this.BrandPanel.Controls.Add(this.logoCard);
            this.BrandPanel.Dock = System.Windows.Forms.DockStyle.Left;
            this.BrandPanel.EdgeColor = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(6)))), ((int)(((byte)(19)))));
            this.BrandPanel.Location = new System.Drawing.Point(0, 0);
            this.BrandPanel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.BrandPanel.Name = "BrandPanel";
            this.BrandPanel.Padding = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.BrandPanel.Size = new System.Drawing.Size(250, 410);
            this.BrandPanel.SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            this.BrandPanel.TabIndex = 10;
            this.BrandPanel.Text = "brandPanel";
            //
            // lblBrandSub
            //
            this.lblBrandSub.BackColor = System.Drawing.Color.Transparent;
            this.lblBrandSub.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblBrandSub.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(225)))), ((int)(((byte)(225)))));
            this.lblBrandSub.Location = new System.Drawing.Point(20, 268);
            this.lblBrandSub.Name = "lblBrandSub";
            this.lblBrandSub.Size = new System.Drawing.Size(210, 60);
            this.lblBrandSub.TabIndex = 13;
            this.lblBrandSub.Text = "Stock, sets and requests — in one place.";
            this.lblBrandSub.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            //
            // lblBrandTitle
            //
            this.lblBrandTitle.BackColor = System.Drawing.Color.Transparent;
            this.lblBrandTitle.Font = new System.Drawing.Font("Segoe UI", 15F, System.Drawing.FontStyle.Bold);
            this.lblBrandTitle.ForeColor = System.Drawing.Color.White;
            this.lblBrandTitle.Location = new System.Drawing.Point(20, 228);
            this.lblBrandTitle.Name = "lblBrandTitle";
            this.lblBrandTitle.Size = new System.Drawing.Size(210, 32);
            this.lblBrandTitle.TabIndex = 12;
            this.lblBrandTitle.Text = "Inventory System";
            this.lblBrandTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // logoCard
            //
            this.logoCard.BackColor = System.Drawing.Color.White;
            this.logoCard.Controls.Add(this.picLogo);
            this.logoCard.EdgeColor = System.Drawing.Color.White;
            this.logoCard.Location = new System.Drawing.Point(35, 100);
            this.logoCard.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.logoCard.Name = "logoCard";
            this.logoCard.Padding = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.logoCard.Size = new System.Drawing.Size(180, 110);
            this.logoCard.SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            this.logoCard.TabIndex = 11;
            this.logoCard.Text = "logoCard";
            //
            // picLogo
            //
            this.picLogo.BackColor = System.Drawing.Color.White;
            this.picLogo.Location = new System.Drawing.Point(12, 10);
            this.picLogo.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.picLogo.Name = "picLogo";
            this.picLogo.Size = new System.Drawing.Size(156, 90);
            this.picLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picLogo.TabIndex = 0;
            this.picLogo.TabStop = false;
            //
            // lblContactAdmin
            //
            this.lblContactAdmin.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblContactAdmin.BackColor = System.Drawing.Color.Transparent;
            this.lblContactAdmin.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblContactAdmin.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(150)))));
            this.lblContactAdmin.Location = new System.Drawing.Point(280, 330);
            this.lblContactAdmin.Name = "lblContactAdmin";
            this.lblContactAdmin.Size = new System.Drawing.Size(360, 48);
            this.lblContactAdmin.TabIndex = 6;
            this.lblContactAdmin.Text = "Need an account? Please contact the Information Technology Department.";
            this.lblContactAdmin.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // LoginBtn
            //
            this.LoginBtn.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.LoginBtn.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(6)))), ((int)(((byte)(19)))));
            this.LoginBtn.ButtonType = ReaLTaiizor.Util.HopeButtonType.Primary;
            this.LoginBtn.Cursor = System.Windows.Forms.Cursors.Hand;
            this.LoginBtn.DangerColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(108)))), ((int)(((byte)(108)))));
            this.LoginBtn.DefaultColor = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(6)))), ((int)(((byte)(19)))));
            this.LoginBtn.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.LoginBtn.HoverTextColor = System.Drawing.Color.White;
            this.LoginBtn.InfoColor = System.Drawing.Color.FromArgb(((int)(((byte)(144)))), ((int)(((byte)(147)))), ((int)(((byte)(153)))));
            this.LoginBtn.Location = new System.Drawing.Point(280, 272);
            this.LoginBtn.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.LoginBtn.Name = "LoginBtn";
            this.LoginBtn.PrimaryColor = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(6)))), ((int)(((byte)(19)))));
            this.LoginBtn.Size = new System.Drawing.Size(360, 44);
            this.LoginBtn.SuccessColor = System.Drawing.Color.FromArgb(((int)(((byte)(103)))), ((int)(((byte)(194)))), ((int)(((byte)(58)))));
            this.LoginBtn.TabIndex = 5;
            this.LoginBtn.Text = "Sign In";
            this.LoginBtn.TextColor = System.Drawing.Color.White;
            this.LoginBtn.WarningColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(162)))), ((int)(((byte)(60)))));
            this.LoginBtn.Click += new System.EventHandler(this.LoginBtn_Click);
            //
            // lblTogglePassword
            //
            this.lblTogglePassword.BackColor = System.Drawing.Color.Transparent;
            this.lblTogglePassword.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblTogglePassword.Location = new System.Drawing.Point(593, 216);
            this.lblTogglePassword.Name = "lblTogglePassword";
            this.lblTogglePassword.Size = new System.Drawing.Size(45, 41);
            this.lblTogglePassword.TabIndex = 8;
            this.lblTogglePassword.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // PassLoginField
            //
            this.PassLoginField.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.PassLoginField.BackColor = System.Drawing.Color.White;
            this.PassLoginField.BaseColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this.PassLoginField.BorderColorA = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.PassLoginField.BorderColorB = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(6)))), ((int)(((byte)(19)))));
            this.PassLoginField.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.PassLoginField.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.PassLoginField.Hint = "Password";
            this.PassLoginField.Location = new System.Drawing.Point(280, 216);
            this.PassLoginField.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.PassLoginField.MaxLength = 32767;
            this.PassLoginField.Multiline = false;
            this.PassLoginField.Name = "PassLoginField";
            this.PassLoginField.PasswordChar = '*';
            this.PassLoginField.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.PassLoginField.SelectedText = "";
            this.PassLoginField.SelectionLength = 0;
            this.PassLoginField.SelectionStart = 0;
            this.PassLoginField.Size = new System.Drawing.Size(310, 41);
            this.PassLoginField.TabIndex = 3;
            this.PassLoginField.TabStop = false;
            this.PassLoginField.UseSystemPasswordChar = false;
            //
            // lblPassCaption
            //
            this.lblPassCaption.BackColor = System.Drawing.Color.Transparent;
            this.lblPassCaption.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblPassCaption.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(100)))), ((int)(((byte)(100)))));
            this.lblPassCaption.Location = new System.Drawing.Point(282, 193);
            this.lblPassCaption.Name = "lblPassCaption";
            this.lblPassCaption.Size = new System.Drawing.Size(356, 20);
            this.lblPassCaption.TabIndex = 9;
            this.lblPassCaption.Text = "Password";
            this.lblPassCaption.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // NameLoginField
            //
            this.NameLoginField.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.NameLoginField.BackColor = System.Drawing.Color.White;
            this.NameLoginField.BaseColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this.NameLoginField.BorderColorA = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
            this.NameLoginField.BorderColorB = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(6)))), ((int)(((byte)(19)))));
            this.NameLoginField.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.NameLoginField.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.NameLoginField.Hint = "Username";
            this.NameLoginField.Location = new System.Drawing.Point(280, 144);
            this.NameLoginField.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.NameLoginField.MaxLength = 32767;
            this.NameLoginField.Multiline = false;
            this.NameLoginField.Name = "NameLoginField";
            this.NameLoginField.PasswordChar = '\0';
            this.NameLoginField.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.NameLoginField.SelectedText = "";
            this.NameLoginField.SelectionLength = 0;
            this.NameLoginField.SelectionStart = 0;
            this.NameLoginField.Size = new System.Drawing.Size(360, 41);
            this.NameLoginField.TabIndex = 2;
            this.NameLoginField.TabStop = false;
            this.NameLoginField.UseSystemPasswordChar = false;
            //
            // lblUserCaption
            //
            this.lblUserCaption.BackColor = System.Drawing.Color.Transparent;
            this.lblUserCaption.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblUserCaption.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(100)))), ((int)(((byte)(100)))));
            this.lblUserCaption.Location = new System.Drawing.Point(282, 121);
            this.lblUserCaption.Name = "lblUserCaption";
            this.lblUserCaption.Size = new System.Drawing.Size(356, 20);
            this.lblUserCaption.TabIndex = 7;
            this.lblUserCaption.Text = "Username";
            this.lblUserCaption.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // lblSubtitle
            //
            this.lblSubtitle.BackColor = System.Drawing.Color.Transparent;
            this.lblSubtitle.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.lblSubtitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(110)))), ((int)(((byte)(115)))));
            this.lblSubtitle.Location = new System.Drawing.Point(282, 73);
            this.lblSubtitle.Name = "lblSubtitle";
            this.lblSubtitle.Size = new System.Drawing.Size(356, 24);
            this.lblSubtitle.TabIndex = 4;
            this.lblSubtitle.Text = "Welcome to Yakult Inventory System";
            this.lblSubtitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // TitleLabel
            //
            this.TitleLabel.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.TitleLabel.BackColor = System.Drawing.Color.Transparent;
            this.TitleLabel.Font = new System.Drawing.Font("Segoe UI", 22F, System.Drawing.FontStyle.Bold);
            this.TitleLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(26)))), ((int)(((byte)(26)))), ((int)(((byte)(26)))));
            this.TitleLabel.Location = new System.Drawing.Point(280, 28);
            this.TitleLabel.Name = "TitleLabel";
            this.TitleLabel.Size = new System.Drawing.Size(360, 45);
            this.TitleLabel.TabIndex = 0;
            this.TitleLabel.Text = "Sign In";
            this.TitleLabel.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            this.TitleLabel.Click += new System.EventHandler(this.TitleLabel_Click);
            //
            // LoginPage
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(246)))), ((int)(((byte)(250)))));
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.ClientSize = new System.Drawing.Size(840, 500);
            this.Controls.Add(this.MainPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LoginPage";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Login - Yakult Inventory System";
            this.MainPanel.ResumeLayout(false);
            this.BrandPanel.ResumeLayout(false);
            this.logoCard.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private ReaLTaiizor.Controls.Panel MainPanel;
        private ReaLTaiizor.Controls.Panel BrandPanel;
        private ReaLTaiizor.Controls.Panel logoCard;
        private System.Windows.Forms.PictureBox picLogo;
        private System.Windows.Forms.Label lblBrandTitle;
        private System.Windows.Forms.Label lblBrandSub;
        private ReaLTaiizor.Controls.BigLabel TitleLabel;
        private System.Windows.Forms.Label lblSubtitle;
        private System.Windows.Forms.Label lblUserCaption;
        private ReaLTaiizor.Controls.HopeTextBox NameLoginField;
        private System.Windows.Forms.Label lblPassCaption;
        private ReaLTaiizor.Controls.HopeTextBox PassLoginField;
        private ReaLTaiizor.Controls.HopeButton LoginBtn;
        private System.Windows.Forms.Label lblContactAdmin;
        private System.Windows.Forms.Label lblTogglePassword;
    }
}
