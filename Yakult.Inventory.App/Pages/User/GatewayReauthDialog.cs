using System;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Services.Gateway;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.User
{
    /// <summary>
    /// Shown when the gateway rejects the session token (e.g. the PC slept past the
    /// 12-hour token). Signs the same user in again so work continues without a
    /// full logout. Opened by GatewaySessionGuard.
    /// </summary>
    public class GatewayReauthDialog : Form
    {
        private readonly TextBox _txtPassword;
        private readonly Label _lblError;
        private readonly Button _btnSignIn;

        public GatewayReauthDialog()
        {
            Text = "Session Expired";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            TopMost = true;
            ClientSize = new Size(420, 190);

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 4 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));  // message
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));  // password
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // error
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));  // buttons

            var message = new Label
            {
                Text = $"Your sign-in has expired. Enter the password for {GatewayClient.LoginName} to continue.",
                Dock = DockStyle.Fill
            };
            table.Controls.Add(message, 0, 0);
            table.SetColumnSpan(message, 2);

            table.Controls.Add(new Label { Text = "Password", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 1);
            _txtPassword = new TextBox { Dock = DockStyle.Fill, PasswordChar = '*' };
            table.Controls.Add(_txtPassword, 1, 1);

            _lblError = new Label { Dock = DockStyle.Fill, ForeColor = Color.Firebrick };
            table.Controls.Add(_lblError, 0, 2);
            table.SetColumnSpan(_lblError, 2);

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, WrapContents = false };
            _btnSignIn = new Button { Text = "Sign In", Width = 100, Height = 30 };
            _btnSignIn.Click += BtnSignIn_Click;
            var btnCancel = new Button { Text = "Later", Width = 90, Height = 30, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(_btnSignIn);
            buttons.Controls.Add(btnCancel);
            table.Controls.Add(buttons, 0, 3);
            table.SetColumnSpan(buttons, 2);

            Controls.Add(table);
            AcceptButton = _btnSignIn;
            CancelButton = btnCancel;
        }

        private async void BtnSignIn_Click(object sender, EventArgs e)
        {
            var password = _txtPassword.Text.Trim();
            if (password.Length == 0) { _lblError.Text = "Please enter your password."; return; }

            _btnSignIn.Enabled = false;
            _lblError.Text = string.Empty;
            try
            {
                var session = await GatewayClient.LoginAsync(GatewayClient.LoginName, password);

                // Only the same user may resume this session.
                if (session == null || session.UserId != AppSession.CurrentUserId)
                {
                    GatewayClient.SignOut();
                    _lblError.Text = "That is a different account. Log out to switch users.";
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (GatewayException ex)
            {
                _lblError.Text = ex.StatusCode == 401 ? "Invalid password." : ex.Message;
            }
            finally
            {
                if (!IsDisposed) _btnSignIn.Enabled = true;
            }
        }
    }
}
