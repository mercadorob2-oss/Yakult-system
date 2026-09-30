using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Services.Gateway;

namespace Yakult.Inventory.App.Pages.Admin.DBConn
{
    /// <summary>
    /// The database switcher (Ctrl+Shift+D) when App.config has a GatewayUrl.
    /// Lists the databases the gateway offers (Production, Test / YIMS_PROD, ...)
    /// and remembers only the chosen name; the gateway holds the connections.
    /// OK means "restart and sign in to the chosen database" (callers restart the app).
    /// </summary>
    public class GatewayEnvironmentForm : Form
    {
        private readonly ComboBox _cboEnvironment;
        private readonly Label _lblEnvCircle;
        private readonly Label _lblStatus;
        private readonly Button _btnSave;

        public GatewayEnvironmentForm()
        {
            Text = "Database";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(460, 190);

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 2,
                RowCount = 3
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));  // 0: Database
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // 1: Status
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));  // 2: Buttons

            table.Controls.Add(new Label { Text = "Database", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);

            // Same inline environment circle as DatabaseSetupForm.
            var cell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22));
            _cboEnvironment = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            _cboEnvironment.SelectedIndexChanged += (s, e) => UpdateEnvironmentIndicator();
            cell.Controls.Add(_cboEnvironment, 0, 0);
            _lblEnvCircle = new Label
            {
                Text = "●",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.Silver,
                Margin = new Padding(0)
            };
            cell.Controls.Add(_lblEnvCircle, 1, 0);
            table.Controls.Add(cell, 1, 0);

            _lblStatus = new Label
            {
                Text = "Loading databases from the sign-in server...",
                Dock = DockStyle.Fill,
                ForeColor = Color.DimGray,
                Padding = new Padding(0, 8, 0, 0)
            };
            table.Controls.Add(_lblStatus, 0, 1);
            table.SetColumnSpan(_lblStatus, 2);

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, WrapContents = false };
            _btnSave = new Button { Text = "Switch && Restart", Width = 130, Height = 32, Enabled = false };
            _btnSave.Click += BtnSave_Click;
            var btnCancel = new Button { Text = "Cancel", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(_btnSave);
            buttons.Controls.Add(btnCancel);
            table.Controls.Add(buttons, 0, 2);
            table.SetColumnSpan(buttons, 2);

            Controls.Add(table);
            CancelButton = btnCancel;
            Load += GatewayEnvironmentForm_Load;
        }

        private async void GatewayEnvironmentForm_Load(object sender, EventArgs e)
        {
            try
            {
                var environments = await GatewayClient.GetEnvironmentsAsync();
                if (IsDisposed) return;

                if (environments == null || environments.Count == 0)
                {
                    _lblStatus.Text = "The sign-in server has no databases configured. Please contact IT.";
                    return;
                }

                _cboEnvironment.Items.AddRange(environments.Cast<object>().ToArray());

                // Pre-select the current session's database, else the saved choice, else the default.
                var current = GatewayClient.CurrentEnvironment?.Name ?? GatewayClient.SelectedEnvironment;
                var selected = environments.FirstOrDefault(x => string.Equals(x.Name, current, StringComparison.OrdinalIgnoreCase))
                            ?? environments.FirstOrDefault(x => x.IsDefault)
                            ?? environments[0];
                _cboEnvironment.SelectedItem = selected;

                _cboEnvironment.Enabled = true;
                _btnSave.Enabled = true;
                _lblStatus.Text = "The app restarts and you sign in again to the chosen database. " +
                                  "Each database has its own user accounts.";
            }
            catch (GatewayException ex)
            {
                if (!IsDisposed)
                    _lblStatus.Text = ex.Message;
            }
        }

        private void UpdateEnvironmentIndicator()
        {
            var kind = (_cboEnvironment.SelectedItem as GatewayEnvironmentInfo)?.Kind;
            if (string.Equals(kind, "Test", StringComparison.OrdinalIgnoreCase))
                _lblEnvCircle.ForeColor = Color.FromArgb(0x00, 0x78, 0xD4); // blue = dummy
            else if (string.Equals(kind, "Production", StringComparison.OrdinalIgnoreCase))
                _lblEnvCircle.ForeColor = Color.FromArgb(0x10, 0x7C, 0x10); // green = production
            else
                _lblEnvCircle.ForeColor = Color.FromArgb(0xCC, 0x6A, 0x00); // amber = unknown
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (!(_cboEnvironment.SelectedItem is GatewayEnvironmentInfo env)) return;

            try
            {
                // The default is stored as "no choice", so a renamed default still works.
                GatewayClient.SelectedEnvironment = env.IsDefault ? null : env.Name;
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show("Could not save the database choice: " + ex.Message, "Database",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
