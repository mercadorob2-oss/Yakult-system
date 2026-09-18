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
            this.lblContactAdmin = new System.Windows.Forms.Label();
            this.LoginBtn = new ReaLTaiizor.Controls.HopeButton();
            this.PassLoginField = new ReaLTaiizor.Controls.HopeTextBox();
            this.NameLoginField = new ReaLTaiizor.Controls.HopeTextBox();
            this.TitleLabel = new ReaLTaiizor.Controls.BigLabel();
            this.lblTogglePassword = new System.Windows.Forms.Label();
            this.MainPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // MainPanel
            // 
            this.MainPanel.BackColor = System.Drawing.Color.White;
            this.MainPanel.Controls.Add(this.lblTogglePassword);
            this.MainPanel.Controls.Add(this.lblContactAdmin);
            this.MainPanel.Controls.Add(this.LoginBtn);
            this.MainPanel.Controls.Add(this.PassLoginField);
            this.MainPanel.Controls.Add(this.NameLoginField);
            this.MainPanel.Controls.Add(this.TitleLabel);
            this.MainPanel.EdgeColor = System.Drawing.Color.FromArgb(((int)(((byte)(210)))), ((int)(((byte)(228)))), ((int)(((byte)(255)))));
            this.MainPanel.Location = new System.Drawing.Point(220, 80);
            this.MainPanel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MainPanel.Name = "MainPanel";
            this.MainPanel.Padding = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.MainPanel.Size = new System.Drawing.Size(400, 348);
            this.MainPanel.SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            this.MainPanel.TabIndex = 0;
            this.MainPanel.Text = "panel1";
            //
            // lblContactAdmin
            //
            this.lblContactAdmin.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblContactAdmin.BackColor = System.Drawing.Color.Transparent;
            this.lblContactAdmin.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblContactAdmin.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(150)))));
            this.lblContactAdmin.Location = new System.Drawing.Point(40, 270);
            this.lblContactAdmin.Name = "lblContactAdmin";
            this.lblContactAdmin.Size = new System.Drawing.Size(320, 48);
            this.lblContactAdmin.TabIndex = 6;
            this.lblContactAdmin.Text = "Need an account? Please contact the Information Technology Department.";
            this.lblContactAdmin.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // LoginBtn
            // 
            this.LoginBtn.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.LoginBtn.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(154)))), ((int)(((byte)(252)))));
            this.LoginBtn.ButtonType = ReaLTaiizor.Util.HopeButtonType.Primary;
            this.LoginBtn.Cursor = System.Windows.Forms.Cursors.Hand;
            this.LoginBtn.DangerColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(108)))), ((int)(((byte)(108)))));
            this.LoginBtn.DefaultColor = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(154)))), ((int)(((byte)(252)))));
            this.LoginBtn.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.LoginBtn.HoverTextColor = System.Drawing.Color.White;
            this.LoginBtn.InfoColor = System.Drawing.Color.FromArgb(((int)(((byte)(144)))), ((int)(((byte)(147)))), ((int)(((byte)(153)))));
            this.LoginBtn.Location = new System.Drawing.Point(40, 215);
            this.LoginBtn.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.LoginBtn.Name = "LoginBtn";
            this.LoginBtn.PrimaryColor = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(154)))), ((int)(((byte)(252)))));
            this.LoginBtn.Size = new System.Drawing.Size(320, 42);
            this.LoginBtn.SuccessColor = System.Drawing.Color.FromArgb(((int)(((byte)(103)))), ((int)(((byte)(194)))), ((int)(((byte)(58)))));
            this.LoginBtn.TabIndex = 5;
            this.LoginBtn.Text = "Sign In";
            this.LoginBtn.TextColor = System.Drawing.Color.White;
            this.LoginBtn.WarningColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(162)))), ((int)(((byte)(60)))));
            this.LoginBtn.Click += new System.EventHandler(this.LoginBtn_Click);
            // 
            // PassLoginField
            // 
            this.PassLoginField.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.PassLoginField.BackColor = System.Drawing.Color.White;
            this.PassLoginField.BaseColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(255)))));
            this.PassLoginField.BorderColorA = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(220)))), ((int)(((byte)(250)))));
            this.PassLoginField.BorderColorB = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(154)))), ((int)(((byte)(252)))));
            this.PassLoginField.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.PassLoginField.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.PassLoginField.Hint = "Password";
            this.PassLoginField.Location = new System.Drawing.Point(40, 160);
            this.PassLoginField.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.PassLoginField.MaxLength = 32767;
            this.PassLoginField.Multiline = false;
            this.PassLoginField.Name = "PassLoginField";
            this.PassLoginField.PasswordChar = '*';
            this.PassLoginField.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.PassLoginField.SelectedText = "";
            this.PassLoginField.SelectionLength = 0;
            this.PassLoginField.SelectionStart = 0;
            this.PassLoginField.Size = new System.Drawing.Size(269, 41);
            this.PassLoginField.TabIndex = 3;
            this.PassLoginField.TabStop = false;
            this.PassLoginField.UseSystemPasswordChar = false;
            // 
            // NameLoginField
            // 
            this.NameLoginField.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.NameLoginField.BackColor = System.Drawing.Color.White;
            this.NameLoginField.BaseColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(255)))));
            this.NameLoginField.BorderColorA = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(220)))), ((int)(((byte)(250)))));
            this.NameLoginField.BorderColorB = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(154)))), ((int)(((byte)(252)))));
            this.NameLoginField.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.NameLoginField.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.NameLoginField.Hint = "Username";
            this.NameLoginField.Location = new System.Drawing.Point(40, 110);
            this.NameLoginField.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.NameLoginField.MaxLength = 32767;
            this.NameLoginField.Multiline = false;
            this.NameLoginField.Name = "NameLoginField";
            this.NameLoginField.PasswordChar = '\0';
            this.NameLoginField.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.NameLoginField.SelectedText = "";
            this.NameLoginField.SelectionLength = 0;
            this.NameLoginField.SelectionStart = 0;
            this.NameLoginField.Size = new System.Drawing.Size(320, 41);
            this.NameLoginField.TabIndex = 2;
            this.NameLoginField.TabStop = false;
            this.NameLoginField.UseSystemPasswordChar = false;
            // 
            // TitleLabel
            // 
            this.TitleLabel.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.TitleLabel.BackColor = System.Drawing.Color.Transparent;
            this.TitleLabel.Font = new System.Drawing.Font("Segoe UI", 28F, System.Drawing.FontStyle.Bold);
            this.TitleLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(154)))), ((int)(((byte)(252)))));
            this.TitleLabel.Location = new System.Drawing.Point(52, 32);
            this.TitleLabel.Name = "TitleLabel";
            this.TitleLabel.Size = new System.Drawing.Size(281, 62);
            this.TitleLabel.TabIndex = 0;
            this.TitleLabel.Text = "Welcome";
            this.TitleLabel.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.TitleLabel.Click += new System.EventHandler(this.TitleLabel_Click);
            // 
            // lblTogglePassword
            // 
            this.lblTogglePassword.BackColor = System.Drawing.Color.Transparent;
            this.lblTogglePassword.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblTogglePassword.Location = new System.Drawing.Point(315, 160);
            this.lblTogglePassword.Name = "lblTogglePassword";
            this.lblTogglePassword.Size = new System.Drawing.Size(45, 41);
            this.lblTogglePassword.TabIndex = 8;
            this.lblTogglePassword.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
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
            this.ResumeLayout(false);

        }

        #endregion

        private ReaLTaiizor.Controls.Panel MainPanel;
        private ReaLTaiizor.Controls.BigLabel TitleLabel;
        private ReaLTaiizor.Controls.HopeTextBox NameLoginField;
        private ReaLTaiizor.Controls.HopeTextBox PassLoginField;
        private ReaLTaiizor.Controls.HopeButton LoginBtn;
        private System.Windows.Forms.Label lblContactAdmin;
        private System.Windows.Forms.Label lblTogglePassword;
    }
}