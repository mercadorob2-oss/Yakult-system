using System;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class SmtpTestDialog : Form
    {
        private readonly ICallMonitoringRepository _repo;
        private readonly NumericUpDown _numTicketId;
        private readonly ComboBox _cboType;
        private readonly TextBox _txtTo;
        private readonly Button _btnRun;
        private readonly TextBox _txtOut;

        public SmtpTestDialog(ICallMonitoringRepository repo, int? defaultTicketId)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            this.Text = "Run SMTP Test";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.Width = 820;
            this.Height = 520;
            this.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _numTicketId = new NumericUpDown { Minimum = 0, Maximum = 999999999, Dock = DockStyle.Left, Width = 160 };
            if (defaultTicketId.HasValue && defaultTicketId.Value > 0) _numTicketId.Value = defaultTicketId.Value;

            _cboType = new ComboBox { Dock = DockStyle.Left, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboType.Items.AddRange(new object[] { "Reminder", "Escalation", "StatusUpdate", "NewTicket" });
            _cboType.SelectedIndex = 0;

            _txtTo = new TextBox { Dock = DockStyle.Fill };

            _btnRun = new Button { Text = "Send Test Email", Dock = DockStyle.Left, Width = 140 };
            _btnRun.Click += async (_, __) => await RunAsync();

            var lblHint = new Label
            {
                Text = "Sends a single email to the override address only (still resolves the exact SMTP profile + recipients path).",
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 130, 140),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _txtOut = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F)
            };

            root.Controls.Add(new Label { Text = "TicketId", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            root.Controls.Add(_numTicketId, 1, 0);
            root.Controls.Add(new Label { Text = "Email Type", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            root.Controls.Add(_cboType, 1, 1);
            root.Controls.Add(new Label { Text = "Test To (override)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
            root.Controls.Add(_txtTo, 1, 2);

            var pnlRun = new Panel { Dock = DockStyle.Fill };
            pnlRun.Controls.Add(_btnRun);
            pnlRun.Controls.Add(lblHint);
            _btnRun.Location = new Point(0, 6);
            lblHint.Location = new Point(_btnRun.Right + 12, 10);
            lblHint.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            root.Controls.Add(pnlRun, 1, 3);
            root.SetColumnSpan(pnlRun, 1);

            root.Controls.Add(_txtOut, 0, 4);
            root.SetColumnSpan(_txtOut, 2);

            this.Controls.Add(root);
        }

        private async Task RunAsync()
        {
            _btnRun.Enabled = false;
            try
            {
                var ticketId = (int)_numTicketId.Value;
                var type = (_cboType.SelectedItem ?? "Reminder").ToString();
                var to = (_txtTo.Text ?? string.Empty).Trim();

                var svc = new CallEmailNotificationService(_repo);
                var res = await svc.RunSmtpTestAsync(ticketId, type, to);

                var sb = new StringBuilder();
                sb.AppendLine("SMTP Test Result");
                sb.AppendLine("Time (local): " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine();
                sb.AppendLine($"TicketId: {res.TicketId}");
                sb.AppendLine($"EmailType: {res.EmailType}");
                sb.AppendLine($"TestTo: {res.TestToEmail}");
                sb.AppendLine();

                if (res.SenderResolution != null)
                {
                    var s = res.SenderResolution.Sender;
                    var auth = s != null && !string.IsNullOrWhiteSpace(s.SmtpUsername) ? "Username+Password" : "DefaultCredentials";
                    string GetFirstValidEmail(string a, string b)
                    {
                        foreach (var candidate in new[] { a, b })
                        {
                            var v = (candidate ?? string.Empty).Trim();
                            if (string.IsNullOrWhiteSpace(v)) continue;
                            try { _ = new System.Net.Mail.MailAddress(v); return v; } catch { }
                        }
                        return null;
                    }

                    sb.AppendLine($"SMTP source: {res.SenderResolution.Source}");
                    sb.AppendLine($"Profile: {(res.SenderResolution.ProfileId.HasValue ? res.SenderResolution.ProfileId.Value.ToString() : "")} {res.SenderResolution.ProfileName}".Trim());
                    if (s != null)
                    {
                        sb.AppendLine($"Host: {s.SmtpServer}");
                        sb.AppendLine($"Port: {s.SmtpPort}");
                        sb.AppendLine($"SSL: {s.UseSsl}");
                        sb.AppendLine($"Auth: {auth}");
                        sb.AppendLine($"User: {(string.IsNullOrWhiteSpace(s.SmtpUsername) ? "(none)" : "(set)")}");
                        var effectiveFrom = GetFirstValidEmail(s.FromEmail, s.SmtpUsername);
                        sb.AppendLine($"From: {(effectiveFrom ?? "(invalid)")}");
                    }
                    sb.AppendLine();
                }

                if (res.RecipientResolution != null)
                {
                    sb.AppendLine("Resolved recipients (normal pipeline):");
                    sb.AppendLine("Final: " + string.Join(", ", res.RecipientResolution.FinalRecipients ?? new System.Collections.Generic.List<string>()));
                    sb.AppendLine("GroupEmail: " + (res.RecipientResolution.RulesGroupEmail ?? ""));
                    sb.AppendLine("Dept recipients: " + (res.RecipientResolution.DepartmentRecipients ?? ""));
                    sb.AppendLine("Branch recipients: " + (res.RecipientResolution.BranchRecipients ?? ""));
                    sb.AppendLine();
                }

                if (res.Sent)
                    sb.AppendLine("Result: SENT OK");
                else
                    sb.AppendLine("Result: FAILED\n" + (res.Error ?? ""));

                _txtOut.Text = sb.ToString().TrimEnd();

                if (!res.Sent && !string.IsNullOrWhiteSpace(res.Error))
                    MessageBox.Show(res.Error, "SMTP Test Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                _txtOut.Text = ex.ToString();
                MessageBox.Show(ex.Message, "SMTP Test Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnRun.Enabled = true;
            }
        }
    }
}
