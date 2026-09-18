using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class EmailLogDetailsDialog : Form
    {
        private readonly string _dateSent;
        private readonly string _ticketId;
        private readonly string _branch;
        private readonly string _type;
        private readonly string _recipient;
        private readonly string _status;
        private readonly string _error;

        public EmailLogDetailsDialog(string dateSent, string ticketId, string branch, string type, string recipient, string status, string error)
        {
            _dateSent = dateSent ?? string.Empty;
            _ticketId = ticketId ?? string.Empty;
            _branch = branch ?? string.Empty;
            _type = type ?? string.Empty;
            _recipient = recipient ?? string.Empty;
            _status = status ?? string.Empty;
            _error = error ?? string.Empty;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            
            this.Text = "Email Log Details";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(245, 247, 250); // Light gray
            this.Font = new Font("Segoe UI", 9.5F);
            this.ClientSize = new Size(800, 500);

            // 1. HEADER
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.White,
                Padding = new Padding(24, 16, 24, 16)
            };

            var lblTitle = new Label
            {
                Text = "Email Delivery Log",
                AutoSize = true,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(44, 62, 80),
                Location = new Point(20, 16)
            };

            var lblSubtitle = new Label
            {
                Text = string.IsNullOrEmpty(_recipient) ? "No Recipient" : $"To: {_recipient}",
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(149, 165, 166),
                Location = new Point(22, 48)
            };

            var lblStatusBadge = new Label
            {
                Text = (_status ?? "Unknown").ToUpper(),
                AutoSize = false,
                Size = new Size(100, 26),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = GetStatusColor(_status),
                Location = new Point(320, 22) // Approx offset from title
            };
            
            // Adjust badge calculation
            using (var g = lblTitle.CreateGraphics())
            {
                var size = g.MeasureString(lblTitle.Text, lblTitle.Font);
                lblStatusBadge.Location = new Point(lblTitle.Location.X + (int)size.Width + 15, lblTitle.Location.Y + 4);
            }

            pnlHeader.Controls.Add(lblStatusBadge);
            pnlHeader.Controls.Add(lblSubtitle);
            pnlHeader.Controls.Add(lblTitle);

            // 2. BODY
            var pnlBody = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24)
            };

            // 2a. Info Grid (Ticket, Date, Type, Branch)
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 1,
                Height = 110,
                BackColor = Color.White,
                Padding = new Padding(15)
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            
            // Col 1
            var pnlCol1 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true };
            pnlCol1.Controls.Add(CreateMetaLabel("Date Sent"));
            pnlCol1.Controls.Add(CreateValueLabel(_dateSent));
            pnlCol1.Controls.Add(CreateMetaLabel("Ticket Reference"));
            pnlCol1.Controls.Add(CreateValueLabel(string.IsNullOrEmpty(_ticketId) ? "-" : $"#{_ticketId}"));
            
            // Col 2
            var pnlCol2 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true };
            pnlCol2.Controls.Add(CreateMetaLabel("Type"));
            pnlCol2.Controls.Add(CreateValueLabel(_type));
            pnlCol2.Controls.Add(CreateMetaLabel("Branch"));
            pnlCol2.Controls.Add(CreateValueLabel(_branch));

            tlp.Controls.Add(pnlCol1, 0, 0);
            tlp.Controls.Add(pnlCol2, 1, 0);

            // 2b. Error Box (Conditional)
            Control errorControl = null;
            var parsed = ParseActionable(_error);
            if (!string.IsNullOrEmpty(parsed.Problem) || !string.IsNullOrEmpty(parsed.Fix) || !string.IsNullOrEmpty(parsed.Info))
            {
                var tlpError = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 3,
                    BackColor = Color.Transparent,
                    Padding = new Padding(0),
                    Margin = new Padding(0)
                };
                tlpError.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
                tlpError.RowStyles.Add(new RowStyle(SizeType.Absolute, 12F));
                tlpError.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));

                var grpProblem = new GroupBox
                {
                    Text = "Problem / Error",
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(192, 57, 43),
                    Padding = new Padding(10),
                    BackColor = Color.FromArgb(255, 245, 245)
                };

                var txtProblem = new TextBox
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    BorderStyle = BorderStyle.None,
                    BackColor = Color.FromArgb(255, 245, 245),
                    ForeColor = Color.FromArgb(192, 57, 43),
                    Text = string.IsNullOrWhiteSpace(parsed.Problem) ? (_error ?? string.Empty) : parsed.Problem,
                    Font = new Font("Consolas", 9F, FontStyle.Regular)
                };
                grpProblem.Controls.Add(txtProblem);

                Control grpFix = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
                if (!string.IsNullOrWhiteSpace(parsed.Fix) || !string.IsNullOrWhiteSpace(parsed.Info))
                {
                    var grpRecommended = new GroupBox
                    {
                        Text = "Recommended Fix",
                        Dock = DockStyle.Fill,
                        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                        ForeColor = Color.FromArgb(41, 128, 185),
                        Padding = new Padding(10),
                        BackColor = Color.FromArgb(245, 251, 255)
                    };

                    var txtFix = new TextBox
                    {
                        Dock = DockStyle.Fill,
                        ReadOnly = true,
                        Multiline = true,
                        ScrollBars = ScrollBars.Vertical,
                        BorderStyle = BorderStyle.None,
                        BackColor = Color.FromArgb(245, 251, 255),
                        ForeColor = Color.FromArgb(44, 62, 80),
                        Text = BuildFixText(parsed),
                        Font = new Font("Consolas", 9F, FontStyle.Regular)
                    };
                    grpRecommended.Controls.Add(txtFix);
                    grpFix = grpRecommended;
                }

                tlpError.Controls.Add(grpProblem, 0, 0);
                tlpError.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 1);
                tlpError.Controls.Add(grpFix, 0, 2);
                errorControl = tlpError;
            }
            else
            {
                // Spacer if no error
                 errorControl = new Panel { Dock = DockStyle.Fill };
            }

            pnlBody.Controls.Add(errorControl);
            pnlBody.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 20 }); // spacer
            pnlBody.Controls.Add(tlp);

            // 3. FOOTER BUTTONS
            var pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.WhiteSmoke,
                Padding = new Padding(12)
            };

            var btnClose = new Button 
            { 
                Text = "Close", 
                Width = 100, 
                Height = 36,
                DialogResult = DialogResult.Cancel,
                Anchor = AnchorStyles.Right, // Manual anchors since we'll dock Right
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 73, 94),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            
            var btnCopyAll = new Button { Text = "Copy All", Width = 100, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand };
            var btnCopyError = new Button { Text = "Copy Error", Width = 100, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand };
            var btnCopyFix = new Button { Text = "Copy Fix", Width = 100, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand };
            
            btnCopyError.Visible = !string.IsNullOrEmpty(parsed.Problem) || !string.IsNullOrEmpty(_error);
            btnCopyFix.Visible = !string.IsNullOrWhiteSpace(parsed.Fix) || !string.IsNullOrWhiteSpace(parsed.Info);

            btnCopyError.Click += (_, __) => TryCopyToClipboard(string.IsNullOrWhiteSpace(parsed.Problem) ? (_error ?? string.Empty) : parsed.Problem);
            btnCopyFix.Click += (_, __) => TryCopyToClipboard(BuildFixText(parsed));
            btnCopyAll.Click += (_, __) => TryCopyToClipboard(BuildAllText());
            
            // Flow layout for buttons
            var flowBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false
            };
            flowBtns.Controls.Add(btnClose);
            flowBtns.Controls.Add(btnCopyAll);
            if (btnCopyFix.Visible) flowBtns.Controls.Add(btnCopyFix);
            if (btnCopyError.Visible) flowBtns.Controls.Add(btnCopyError);

            pnlFooter.Controls.Add(flowBtns);

            this.Controls.Add(pnlBody);
            this.Controls.Add(pnlFooter);
            this.Controls.Add(pnlHeader);

            this.CancelButton = btnClose;
            this.ResumeLayout(false);
        }

        // --- HELPERS ---

        private Label CreateMetaLabel(string text)
        {
            return new Label
            {
                Text = text.ToUpper(),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(149, 165, 166),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
        }

        private Label CreateValueLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(44, 62, 80),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 15)
            };
        }

        private Color GetStatusColor(string status)
        {
            if (string.IsNullOrEmpty(status)) return Color.Gray;
            if (status.IndexOf("Sent", StringComparison.OrdinalIgnoreCase) >= 0 || status.IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0)
                return Color.FromArgb(46, 204, 113); // Green
            if (status.IndexOf("Resent", StringComparison.OrdinalIgnoreCase) >= 0)
                return Color.FromArgb(41, 128, 185); // Blue
            if (status.IndexOf("Skip", StringComparison.OrdinalIgnoreCase) >= 0)
                return Color.FromArgb(230, 126, 34); // Orange
            if (status.IndexOf("Fail", StringComparison.OrdinalIgnoreCase) >= 0 || status.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0)
                return Color.FromArgb(231, 76, 60); // Red
            return Color.Gray;
        }

        private string BuildAllText()
        {
            var parsed = ParseActionable(_error);
            var sb = new StringBuilder();
            sb.AppendLine($"Date Sent: {_dateSent}");
            sb.AppendLine($"Ticket: {_ticketId}");
            sb.AppendLine($"Branch: {_branch}");
            sb.AppendLine($"Type: {_type}");
            sb.AppendLine($"Recipient: {_recipient}");
            sb.AppendLine($"Status: {_status}");
            sb.AppendLine("Problem/Error:");
            sb.AppendLine(string.IsNullOrWhiteSpace(parsed.Problem) ? (_error ?? string.Empty) : parsed.Problem);
            if (!string.IsNullOrWhiteSpace(parsed.Fix) || !string.IsNullOrWhiteSpace(parsed.Info))
            {
                sb.AppendLine();
                sb.AppendLine("Recommended Fix:");
                sb.AppendLine(BuildFixText(parsed));
            }
            return sb.ToString();
        }

        private sealed class ParsedActionable
        {
            public string Problem { get; set; }
            public string Fix { get; set; }
            public string Info { get; set; }
        }

        private static ParsedActionable ParseActionable(string error)
        {
            var parsed = new ParsedActionable();
            if (string.IsNullOrWhiteSpace(error))
                return parsed;

            try
            {
                var lines = error
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    .Select(l => (l ?? string.Empty).Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                foreach (var line in lines)
                {
                    if (line.StartsWith("Problem:", StringComparison.OrdinalIgnoreCase))
                        parsed.Problem = line.Substring(8).Trim();
                    else if (line.StartsWith("Fix:", StringComparison.OrdinalIgnoreCase))
                        parsed.Fix = line.Substring(4).Trim();
                    else if (line.StartsWith("Info:", StringComparison.OrdinalIgnoreCase))
                        parsed.Info = line.Substring(5).Trim();
                }

                if (string.IsNullOrWhiteSpace(parsed.Problem))
                    parsed.Problem = error.Trim();
            }
            catch
            {
                parsed.Problem = error ?? string.Empty;
            }

            return parsed;
        }

        private static string BuildFixText(ParsedActionable parsed)
        {
            if (parsed == null)
                return string.Empty;

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(parsed.Fix))
                sb.AppendLine(parsed.Fix.Trim());
            if (!string.IsNullOrWhiteSpace(parsed.Info))
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append("Info: ").Append(parsed.Info.Trim());
            }
            return sb.ToString().Trim();
        }

        private static void TryCopyToClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text ?? string.Empty);
            }
            catch
            {
                // Ignore clipboard errors
            }
        }
    }
}
