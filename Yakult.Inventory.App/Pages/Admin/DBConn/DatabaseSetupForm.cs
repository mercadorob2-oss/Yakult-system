using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Pages.Admin.DBConn
{
    public class DatabaseSetupForm : Form
    {
        private TextBox txtServer;
        private TextBox txtDatabase;
        private TextBox txtUsername;
        private TextBox txtPassword;
        private RadioButton rbWindowsAuth;
        private RadioButton rbSqlAuth;
        private Label lblUsername;
        private Label lblPassword;
        private Button btnTest;
        private Button btnSave;
        private Button btnCancel;
        private Label _lblEnvCircle;

        private bool _lastTestSuccessful;

        public DatabaseSetupForm()
        {
            InitializeUi();
            Load += DatabaseSetupForm_Load;
        }

        private void InitializeUi()
        {
            Text = "Database Setup";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 300);

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 2,
                RowCount = 6
            };

            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // 0: Server Address
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // 1: Database Name
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // 2: Authentication
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // 3: Username
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // 4: Password
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 5: Buttons

            // Row 0: Server Address — always masked (no reveal-on-focus).
            table.Controls.Add(CreateLabel("Server Address"), 0, 0);
            txtServer = CreateTextBox();
            txtServer.UseSystemPasswordChar = true;
            table.Controls.Add(txtServer, 1, 0);

            // Row 1: Database Name — always masked (same as Password, no reveal-on-focus).
            //        Environment circle sits inline on the right edge of the cell.
            table.Controls.Add(CreateLabel("Database Name"), 0, 1);
            var dbCell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            dbCell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            dbCell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22));
            dbCell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            txtDatabase = CreateTextBox();
            txtDatabase.UseSystemPasswordChar = true;
            txtDatabase.TextChanged += (s, e) => UpdateEnvironmentIndicator();
            dbCell.Controls.Add(txtDatabase, 0, 0);

            _lblEnvCircle = new Label
            {
                Text = "●",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.Silver,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            dbCell.Controls.Add(_lblEnvCircle, 1, 0);
            table.Controls.Add(dbCell, 1, 1);

            // Row 2: Authentication
            table.Controls.Add(CreateLabel("Authentication"), 0, 2);
            var authPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            rbWindowsAuth = new RadioButton { Text = "Windows", AutoSize = true, Checked = true };
            rbSqlAuth = new RadioButton { Text = "SQL Server", AutoSize = true };
            rbWindowsAuth.CheckedChanged += AuthMode_Changed;
            authPanel.Controls.Add(rbWindowsAuth);
            authPanel.Controls.Add(rbSqlAuth);
            table.Controls.Add(authPanel, 1, 2);

            // Row 3: Username — always masked (no reveal-on-focus).
            lblUsername = CreateLabel("Username");
            table.Controls.Add(lblUsername, 0, 3);
            txtUsername = CreateTextBox();
            txtUsername.UseSystemPasswordChar = true;
            table.Controls.Add(txtUsername, 1, 3);

            // Row 4: Password — always masked (existing behaviour)
            lblPassword = CreateLabel("Password");
            table.Controls.Add(lblPassword, 0, 4);
            txtPassword = CreateTextBox();
            txtPassword.UseSystemPasswordChar = true;
            table.Controls.Add(txtPassword, 1, 4);

            // Row 5: Buttons
            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                WrapContents = false,
                Padding = new Padding(0, 12, 0, 0)
            };

            btnSave = new Button { Text = "Save && Continue", Width = 130, Height = 32 };
            btnSave.Click += BtnSave_Click;

            btnTest = new Button { Text = "Test Connection", Width = 130, Height = 32 };
            btnTest.Click += BtnTest_Click;

            btnCancel = new Button { Text = "Cancel", Width = 90, Height = 32 };
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            buttonPanel.Controls.Add(btnSave);
            buttonPanel.Controls.Add(btnTest);
            buttonPanel.Controls.Add(btnCancel);

            table.Controls.Add(buttonPanel, 0, 5);
            table.SetColumnSpan(buttonPanel, 2);

            Controls.Add(table);

            txtServer.TextChanged   += (s, e) => _lastTestSuccessful = false;
            txtDatabase.TextChanged += (s, e) => _lastTestSuccessful = false;
            txtUsername.TextChanged += (s, e) => _lastTestSuccessful = false;
            txtPassword.TextChanged += (s, e) => _lastTestSuccessful = false;
            rbWindowsAuth.CheckedChanged += (s, e) => _lastTestSuccessful = false;
            rbSqlAuth.CheckedChanged     += (s, e) => _lastTestSuccessful = false;

            UpdateCredentialFieldsVisibility();
        }

        private void UpdateEnvironmentIndicator()
        {
            var dbName = (txtDatabase.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(dbName))
            {
                _lblEnvCircle.ForeColor = Color.Silver;
                return;
            }

            // Dummy database is YIMS_PROD. Any Yakult_* name is production.
            // The production DB has a "_DEV" suffix by historical convention — do not use
            // keyword matching on DEV/PROD; match on the known dummy name instead.
            if (dbName.Equals("YIMS_PROD", StringComparison.OrdinalIgnoreCase))
                _lblEnvCircle.ForeColor = Color.FromArgb(0x00, 0x78, 0xD4); // blue = dummy
            else if (dbName.StartsWith("Yakult", StringComparison.OrdinalIgnoreCase))
                _lblEnvCircle.ForeColor = Color.FromArgb(0x10, 0x7C, 0x10); // green = production
            else
                _lblEnvCircle.ForeColor = Color.FromArgb(0xCC, 0x6A, 0x00); // amber = unknown
        }

        private void AuthMode_Changed(object sender, EventArgs e)
        {
            UpdateCredentialFieldsVisibility();
        }

        private void UpdateCredentialFieldsVisibility()
        {
            bool sqlAuth = rbSqlAuth.Checked;
            txtUsername.Enabled = sqlAuth;
            txtPassword.Enabled = sqlAuth;
            lblUsername.Enabled = sqlAuth;
            lblPassword.Enabled = sqlAuth;

            // Windows auth doesn't use these — clear them so SQL Server credentials
            // don't linger (just disabled) and look like they carried over.
            if (!sqlAuth)
            {
                txtUsername.Clear();
                txtPassword.Clear();
            }
        }

        private static Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
        }

        private static TextBox CreateTextBox()
        {
            return new TextBox { Dock = DockStyle.Fill };
        }

        private void DatabaseSetupForm_Load(object sender, EventArgs e)
        {
            try
            {
                var cs = DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs))
                {
                    txtServer.Text   = "192.168.100.186,50301";
                    txtDatabase.Text = "Yakult_Inventory_System_DEV";
                    rbSqlAuth.Checked = true;
                    txtUsername.Text = "remote_user";
                    UpdateCredentialFieldsVisibility();
                    UpdateEnvironmentIndicator();
                    return;
                }

                var builder = new SqlConnectionStringBuilder(cs);
                txtServer.Text   = builder.DataSource;
                txtDatabase.Text = builder.InitialCatalog;

                if (builder.IntegratedSecurity)
                {
                    rbWindowsAuth.Checked = true;
                }
                else
                {
                    rbSqlAuth.Checked    = true;
                    txtUsername.Text     = builder.UserID;
                    txtPassword.Text     = builder.Password;
                }

                UpdateCredentialFieldsVisibility();
                UpdateEnvironmentIndicator();
            }
            catch
            {
            }
        }

        private void BtnTest_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtServer.Text))
                {
                    MessageBox.Show("Please enter the server address.", "Test Connection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtServer.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtDatabase.Text))
                {
                    MessageBox.Show("Please enter the database name.", "Test Connection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtDatabase.Focus();
                    return;
                }

                if (rbSqlAuth.Checked)
                {
                    if (string.IsNullOrWhiteSpace(txtUsername.Text))
                    {
                        MessageBox.Show("Please enter the username.", "Test Connection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        txtUsername.Focus();
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(txtPassword.Text))
                    {
                        MessageBox.Show("Please enter the password.", "Test Connection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        txtPassword.Focus();
                        return;
                    }
                }

                var testConnString = BuildConnectionString(
                    txtServer.Text,
                    txtDatabase.Text,
                    rbWindowsAuth.Checked,
                    txtUsername.Text,
                    txtPassword.Text);

                using (var con = new SqlConnection(testConnString))
                {
                    con.Open();
                }

                _lastTestSuccessful = true;
                MessageBox.Show("Connection successful.", "Test Connection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _lastTestSuccessful = false;
                MessageBox.Show($"Unable to connect.\n\n{ex.Message}", "Test Connection", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (!_lastTestSuccessful)
            {
                MessageBox.Show("Please test the connection first.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var newConnString = BuildConnectionString(
                    txtServer.Text,
                    txtDatabase.Text,
                    rbWindowsAuth.Checked,
                    txtUsername.Text,
                    txtPassword.Text);

                Properties.Settings.Default.DbConnectionString = newConnString;
                Properties.Settings.Default.Save();

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to save settings.\n\n{ex.Message}", "Save", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string BuildConnectionString(string server, string database, bool windowsAuth, string username, string password)
        {
            var serverTrimmed   = (server   ?? string.Empty).Trim();
            var databaseTrimmed = (database ?? string.Empty).Trim();

            if (windowsAuth)
            {
                return $"Data Source={serverTrimmed};" +
                       $"Initial Catalog={databaseTrimmed};" +
                       "Integrated Security=True;" +
                       "Encrypt=False;" +
                       "TrustServerCertificate=True;";
            }

            var usernameTrimmed = (username ?? string.Empty).Trim();
            var passwordValue   = password ?? string.Empty;

            return $"Data Source={serverTrimmed};" +
                   $"Initial Catalog={databaseTrimmed};" +
                   $"User ID={usernameTrimmed};" +
                   $"Password={passwordValue};" +
                   "Encrypt=False;" +
                   "TrustServerCertificate=True;";
        }
    }
}
