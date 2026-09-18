namespace Yakult.Inventory.App.Pages.User
{
    partial class RegisterPage
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
            this.LoginBtnRedirect = new ReaLTaiizor.Controls.MaterialButton();
            this.OrLabel = new ReaLTaiizor.Controls.SmallLabel();
            this.RegBtn = new ReaLTaiizor.Controls.HopeButton();
            this.lblPasswordMatch = new ReaLTaiizor.Controls.SmallLabel();
            this.ConfPassRegField = new ReaLTaiizor.Controls.HopeTextBox();
            this.PassRegField = new ReaLTaiizor.Controls.HopeTextBox();
            this.EmailAddRegField = new ReaLTaiizor.Controls.HopeTextBox();
            this.NameRegField = new ReaLTaiizor.Controls.HopeTextBox();
            this.TitleLabel = new ReaLTaiizor.Controls.BigLabel();
            this.lblTogglePassword = new System.Windows.Forms.Label();
            this.lblToggleConfirmPassword = new System.Windows.Forms.Label();
            this.MainPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // MainPanel
            // 
            this.MainPanel.BackColor = System.Drawing.Color.White;
            this.MainPanel.Controls.Add(this.lblToggleConfirmPassword);
            this.MainPanel.Controls.Add(this.lblTogglePassword);
            this.MainPanel.Controls.Add(this.LoginBtnRedirect);
            this.MainPanel.Controls.Add(this.OrLabel);
            this.MainPanel.Controls.Add(this.RegBtn);
            this.MainPanel.Controls.Add(this.lblPasswordMatch);
            this.MainPanel.Controls.Add(this.ConfPassRegField);
            this.MainPanel.Controls.Add(this.PassRegField);
            this.MainPanel.Controls.Add(this.EmailAddRegField);
            this.MainPanel.Controls.Add(this.NameRegField);
            this.MainPanel.Controls.Add(this.TitleLabel);
            this.MainPanel.EdgeColor = System.Drawing.Color.FromArgb(((int)(((byte)(210)))), ((int)(((byte)(228)))), ((int)(((byte)(255)))));
            this.MainPanel.Location = new System.Drawing.Point(220, 50);
            this.MainPanel.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MainPanel.Name = "MainPanel";
            this.MainPanel.Padding = new System.Windows.Forms.Padding(5, 5, 5, 5);
            this.MainPanel.Size = new System.Drawing.Size(400, 450);
            this.MainPanel.SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            this.MainPanel.TabIndex = 0;
            this.MainPanel.Text = "panel1";
            // 
            // LoginBtnRedirect
            // 
            this.LoginBtnRedirect.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.LoginBtnRedirect.AutoSize = false;
            this.LoginBtnRedirect.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.LoginBtnRedirect.BackColor = System.Drawing.Color.Transparent;
            this.LoginBtnRedirect.Cursor = System.Windows.Forms.Cursors.Hand;
            this.LoginBtnRedirect.Density = ReaLTaiizor.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.LoginBtnRedirect.Depth = 0;
            this.LoginBtnRedirect.HighEmphasis = false;
            this.LoginBtnRedirect.Icon = null;
            this.LoginBtnRedirect.IconType = ReaLTaiizor.Controls.MaterialButton.MaterialIconType.Rebase;
            this.LoginBtnRedirect.Location = new System.Drawing.Point(99, 411);
            this.LoginBtnRedirect.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.LoginBtnRedirect.MouseState = ReaLTaiizor.Helper.MaterialDrawHelper.MaterialMouseState.HOVER;
            this.LoginBtnRedirect.Name = "LoginBtnRedirect";
            this.LoginBtnRedirect.NoAccentTextColor = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(154)))), ((int)(((byte)(252)))));
            this.LoginBtnRedirect.Size = new System.Drawing.Size(180, 28);
            this.LoginBtnRedirect.TabIndex = 11;
            this.LoginBtnRedirect.Text = "Back to Login";
            this.LoginBtnRedirect.Type = ReaLTaiizor.Controls.MaterialButton.MaterialButtonType.Text;
            this.LoginBtnRedirect.UseAccentColor = false;
            this.LoginBtnRedirect.UseVisualStyleBackColor = false;
            this.LoginBtnRedirect.Click += new System.EventHandler(this.LoginBtnRedirect_Click);
            // 
            // OrLabel
            // 
            this.OrLabel.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.OrLabel.BackColor = System.Drawing.Color.Transparent;
            this.OrLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.OrLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(150)))));
            this.OrLabel.Location = new System.Drawing.Point(168, 385);
            this.OrLabel.Name = "OrLabel";
            this.OrLabel.Size = new System.Drawing.Size(43, 20);
            this.OrLabel.TabIndex = 10;
            this.OrLabel.Text = "or";
            this.OrLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // RegBtn
            // 
            this.RegBtn.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.RegBtn.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(154)))), ((int)(((byte)(252)))));
            this.RegBtn.ButtonType = ReaLTaiizor.Util.HopeButtonType.Primary;
            this.RegBtn.Cursor = System.Windows.Forms.Cursors.Hand;
            this.RegBtn.DangerColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(108)))), ((int)(((byte)(108)))));
            this.RegBtn.DefaultColor = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(154)))), ((int)(((byte)(252)))));
            this.RegBtn.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.RegBtn.HoverTextColor = System.Drawing.Color.White;
            this.RegBtn.InfoColor = System.Drawing.Color.FromArgb(((int)(((byte)(144)))), ((int)(((byte)(147)))), ((int)(((byte)(153)))));
            this.RegBtn.Location = new System.Drawing.Point(40, 330);
            this.RegBtn.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.RegBtn.Name = "RegBtn";
            this.RegBtn.PrimaryColor = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(154)))), ((int)(((byte)(252)))));
            this.RegBtn.Size = new System.Drawing.Size(320, 42);
            this.RegBtn.SuccessColor = System.Drawing.Color.FromArgb(((int)(((byte)(103)))), ((int)(((byte)(194)))), ((int)(((byte)(58)))));
            this.RegBtn.TabIndex = 9;
            this.RegBtn.Text = "Create Account";
            this.RegBtn.TextColor = System.Drawing.Color.White;
            this.RegBtn.WarningColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(162)))), ((int)(((byte)(60)))));
            this.RegBtn.Click += new System.EventHandler(this.RegBtn_Click);
            // 
            // lblPasswordMatch
            // 
            this.lblPasswordMatch.AutoSize = true;
            this.lblPasswordMatch.BackColor = System.Drawing.Color.Transparent;
            this.lblPasswordMatch.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblPasswordMatch.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(130)))), ((int)(((byte)(130)))), ((int)(((byte)(130)))));
            this.lblPasswordMatch.Location = new System.Drawing.Point(40, 305);
            this.lblPasswordMatch.Name = "lblPasswordMatch";
            this.lblPasswordMatch.Size = new System.Drawing.Size(0, 20);
            this.lblPasswordMatch.TabIndex = 8;
            // 
            // ConfPassRegField
            // 
            this.ConfPassRegField.BackColor = System.Drawing.Color.White;
            this.ConfPassRegField.BaseColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(255)))));
            this.ConfPassRegField.BorderColorA = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(220)))), ((int)(((byte)(250)))));
            this.ConfPassRegField.BorderColorB = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(154)))), ((int)(((byte)(252)))));
            this.ConfPassRegField.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.ConfPassRegField.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.ConfPassRegField.Hint = "Confirm Password";
            this.ConfPassRegField.Location = new System.Drawing.Point(40, 260);
            this.ConfPassRegField.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.ConfPassRegField.MaxLength = 32767;
            this.ConfPassRegField.Multiline = false;
            this.ConfPassRegField.Name = "ConfPassRegField";
            this.ConfPassRegField.PasswordChar = '*';
            this.ConfPassRegField.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.ConfPassRegField.SelectedText = "";
            this.ConfPassRegField.SelectionLength = 0;
            this.ConfPassRegField.SelectionStart = 0;
            this.ConfPassRegField.Size = new System.Drawing.Size(269, 41);
            this.ConfPassRegField.TabIndex = 6;
            this.ConfPassRegField.TabStop = false;
            this.ConfPassRegField.UseSystemPasswordChar = false;
            // 
            // PassRegField
            // 
            this.PassRegField.BackColor = System.Drawing.Color.White;
            this.PassRegField.BaseColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(255)))));
            this.PassRegField.BorderColorA = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(220)))), ((int)(((byte)(250)))));
            this.PassRegField.BorderColorB = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(154)))), ((int)(((byte)(252)))));
            this.PassRegField.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.PassRegField.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.PassRegField.Hint = "Password";
            this.PassRegField.Location = new System.Drawing.Point(40, 210);
            this.PassRegField.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.PassRegField.MaxLength = 32767;
            this.PassRegField.Multiline = false;
            this.PassRegField.Name = "PassRegField";
            this.PassRegField.PasswordChar = '*';
            this.PassRegField.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.PassRegField.SelectedText = "";
            this.PassRegField.SelectionLength = 0;
            this.PassRegField.SelectionStart = 0;
            this.PassRegField.Size = new System.Drawing.Size(269, 41);
            this.PassRegField.TabIndex = 4;
            this.PassRegField.TabStop = false;
            this.PassRegField.UseSystemPasswordChar = false;
            // 
            // EmailAddRegField
            // 
            this.EmailAddRegField.BackColor = System.Drawing.Color.White;
            this.EmailAddRegField.BaseColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(255)))));
            this.EmailAddRegField.BorderColorA = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(220)))), ((int)(((byte)(250)))));
            this.EmailAddRegField.BorderColorB = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(154)))), ((int)(((byte)(252)))));
            this.EmailAddRegField.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.EmailAddRegField.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.EmailAddRegField.Hint = "Email Address";
            this.EmailAddRegField.Location = new System.Drawing.Point(40, 160);
            this.EmailAddRegField.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.EmailAddRegField.MaxLength = 32767;
            this.EmailAddRegField.Multiline = false;
            this.EmailAddRegField.Name = "EmailAddRegField";
            this.EmailAddRegField.PasswordChar = '\0';
            this.EmailAddRegField.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.EmailAddRegField.SelectedText = "";
            this.EmailAddRegField.SelectionLength = 0;
            this.EmailAddRegField.SelectionStart = 0;
            this.EmailAddRegField.Size = new System.Drawing.Size(320, 41);
            this.EmailAddRegField.TabIndex = 3;
            this.EmailAddRegField.TabStop = false;
            this.EmailAddRegField.UseSystemPasswordChar = false;
            // 
            // NameRegField
            // 
            this.NameRegField.BackColor = System.Drawing.Color.White;
            this.NameRegField.BaseColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(248)))), ((int)(((byte)(255)))));
            this.NameRegField.BorderColorA = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(220)))), ((int)(((byte)(250)))));
            this.NameRegField.BorderColorB = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(154)))), ((int)(((byte)(252)))));
            this.NameRegField.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.NameRegField.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.NameRegField.Hint = "Username";
            this.NameRegField.Location = new System.Drawing.Point(40, 110);
            this.NameRegField.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.NameRegField.MaxLength = 32767;
            this.NameRegField.Multiline = false;
            this.NameRegField.Name = "NameRegField";
            this.NameRegField.PasswordChar = '\0';
            this.NameRegField.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.NameRegField.SelectedText = "";
            this.NameRegField.SelectionLength = 0;
            this.NameRegField.SelectionStart = 0;
            this.NameRegField.Size = new System.Drawing.Size(320, 41);
            this.NameRegField.TabIndex = 2;
            this.NameRegField.TabStop = false;
            this.NameRegField.UseSystemPasswordChar = false;
            // 
            // TitleLabel
            // 
            this.TitleLabel.BackColor = System.Drawing.Color.Transparent;
            this.TitleLabel.Font = new System.Drawing.Font("Segoe UI", 28F, System.Drawing.FontStyle.Bold);
            this.TitleLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(154)))), ((int)(((byte)(252)))));
            this.TitleLabel.Location = new System.Drawing.Point(87, 31);
            this.TitleLabel.Name = "TitleLabel";
            this.TitleLabel.Size = new System.Drawing.Size(235, 62);
            this.TitleLabel.TabIndex = 0;
            this.TitleLabel.Text = "Register";
            this.TitleLabel.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // lblTogglePassword
            // 
            this.lblTogglePassword.BackColor = System.Drawing.Color.Transparent;
            this.lblTogglePassword.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblTogglePassword.Font = new System.Drawing.Font("Segoe MDL2 Assets", 13.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTogglePassword.Location = new System.Drawing.Point(315, 210);
            this.lblTogglePassword.Name = "lblTogglePassword";
            this.lblTogglePassword.Size = new System.Drawing.Size(38, 36);
            this.lblTogglePassword.TabIndex = 12;
            this.lblTogglePassword.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblToggleConfirmPassword
            // 
            this.lblToggleConfirmPassword.BackColor = System.Drawing.Color.Transparent;
            this.lblToggleConfirmPassword.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblToggleConfirmPassword.Font = new System.Drawing.Font("Segoe MDL2 Assets", 13.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblToggleConfirmPassword.Location = new System.Drawing.Point(315, 260);
            this.lblToggleConfirmPassword.Name = "lblToggleConfirmPassword";
            this.lblToggleConfirmPassword.Size = new System.Drawing.Size(38, 36);
            this.lblToggleConfirmPassword.TabIndex = 13;
            this.lblToggleConfirmPassword.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // RegisterPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(246)))), ((int)(((byte)(250)))));
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.ClientSize = new System.Drawing.Size(840, 570);
            this.Controls.Add(this.MainPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RegisterPage";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Register - Yakult Inventory System";
            this.MainPanel.ResumeLayout(false);
            this.MainPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private ReaLTaiizor.Controls.Panel MainPanel;
        private ReaLTaiizor.Controls.BigLabel TitleLabel;
        private ReaLTaiizor.Controls.HopeTextBox NameRegField;
        private ReaLTaiizor.Controls.HopeTextBox EmailAddRegField;
        private ReaLTaiizor.Controls.HopeTextBox PassRegField;
        private ReaLTaiizor.Controls.HopeTextBox ConfPassRegField;
        private ReaLTaiizor.Controls.SmallLabel lblPasswordMatch;
        private ReaLTaiizor.Controls.HopeButton RegBtn;
        private ReaLTaiizor.Controls.SmallLabel OrLabel;
        private ReaLTaiizor.Controls.MaterialButton LoginBtnRedirect;
        private System.Windows.Forms.Label lblToggleConfirmPassword;
        private System.Windows.Forms.Label lblTogglePassword;
    }
}