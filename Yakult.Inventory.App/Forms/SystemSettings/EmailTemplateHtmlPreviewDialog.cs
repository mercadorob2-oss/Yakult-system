using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Forms.SystemSettings
{
    /// <summary>
    /// Read-only preview of an email template's rendered HTML, so an admin can see
    /// roughly what the message will look like in a mail client before saving/sending.
    /// Placeholders are optionally swapped for sample values; unknown placeholders are
    /// left untouched so it is obvious which tokens have no sample.
    /// </summary>
    public sealed class EmailTemplateHtmlPreviewDialog : Form
    {
        private static readonly Regex PlaceholderRegex = new Regex(
            @"\{\{\s*(?<key>[A-Za-z0-9_]+)\s*\}\}|\{\s*(?<key2>[A-Za-z0-9_]+)\s*\}",
            RegexOptions.Compiled);

        private readonly string _templateKey;
        private readonly string _subjectTemplate;
        private readonly string _bodyTemplate;
        private readonly bool _isHtml;

        // When true the subject/body passed in are already fully rendered for a specific
        // record (no placeholders left to fill), so the sample-data toggle is hidden.
        private readonly bool _preRendered;

        private WebBrowser _browser;
        private Label _lblSubject;
        private CheckBox _chkSampleData;
        private CheckBox _chkShowSource;
        private TextBox _txtSource;

        /// <summary>Preview a stored template, with an optional sample-data fill for placeholders.</summary>
        public EmailTemplateHtmlPreviewDialog(EmailTemplateDto template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));

            _templateKey = template.TemplateKey ?? string.Empty;
            _subjectTemplate = template.SubjectTemplate ?? string.Empty;
            _bodyTemplate = template.BodyTemplate ?? string.Empty;
            _isHtml = template.IsHtml;
            _preRendered = false;

            BuildUi();
        }

        /// <summary>
        /// Preview an email whose subject/body are already rendered for a specific record
        /// (e.g. the exact message the "Send Notification" button would dispatch).
        /// </summary>
        public EmailTemplateHtmlPreviewDialog(string label, string renderedSubject, string renderedBodyHtml, bool isHtml)
        {
            _templateKey = label ?? string.Empty;
            _subjectTemplate = renderedSubject ?? string.Empty;
            _bodyTemplate = renderedBodyHtml ?? string.Empty;
            _isHtml = isHtml;
            _preRendered = true;

            BuildUi();
        }

        private void BuildUi()
        {
            SuspendLayout();
            var caption = _preRendered ? "Email Preview" : "Email Template Preview";
            Text = string.IsNullOrWhiteSpace(_templateKey) ? caption : $"{caption} — {_templateKey}";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(900, 720);
            MinimumSize = new Size(560, 420);
            Font = new Font("Segoe UI", 9.5F);
            BackColor = Color.White;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));  // subject header
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));  // options
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // preview / source
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));  // buttons

            // --- Subject header (caption row + value row, no overlap) ---
            var subjectPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.FromArgb(240, 246, 255),
                Margin = new Padding(0, 0, 0, 6),
                Padding = new Padding(14, 8, 14, 10)
            };
            subjectPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
            subjectPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            subjectPanel.Controls.Add(new Label
            {
                Text = "SUBJECT",
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                ForeColor = Color.FromArgb(110, 130, 155),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            _lblSubject = new Label
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                ForeColor = Color.FromArgb(26, 35, 51),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            subjectPanel.Controls.Add(_lblSubject, 0, 1);

            // --- Options row ---
            var optionsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 4)
            };
            optionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            optionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            optionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            _chkSampleData = new CheckBox
            {
                Text = "Fill placeholders with sample values",
                Checked = !_preRendered,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 28, 8),
                // Already-rendered content has no placeholders to fill.
                Visible = !_preRendered
            };
            _chkSampleData.CheckedChanged += (_, __) => Refresh_Preview();

            _chkShowSource = new CheckBox
            {
                Text = "Show HTML source",
                Checked = false,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 0, 8)
            };
            _chkShowSource.CheckedChanged += (_, __) => ToggleSourceView();

            optionsPanel.Controls.Add(_chkSampleData, 0, 0);
            optionsPanel.Controls.Add(_chkShowSource, 1, 0);

            // --- Preview / source (stacked, visibility toggled) ---
            var previewHost = new Panel { Dock = DockStyle.Fill };

            _browser = new WebBrowser
            {
                Dock = DockStyle.Fill,
                IsWebBrowserContextMenuEnabled = false,
                AllowWebBrowserDrop = false,
                ScriptErrorsSuppressed = true
            };
            _txtSource = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 9F),
                BackColor = Color.White,
                Visible = false
            };
            previewHost.Controls.Add(_txtSource);
            previewHost.Controls.Add(_browser);

            // --- Buttons ---
            var buttonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 0)
            };
            var btnClose = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                Width = 96,
                Height = 30
            };
            buttonsPanel.Controls.Add(btnClose);

            root.Controls.Add(subjectPanel, 0, 0);
            root.Controls.Add(optionsPanel, 0, 1);
            root.Controls.Add(previewHost, 0, 2);
            root.Controls.Add(buttonsPanel, 0, 3);

            Controls.Add(root);
            AcceptButton = btnClose;
            CancelButton = btnClose;

            ResumeLayout(false);

            Refresh_Preview();
        }

        private void ToggleSourceView()
        {
            var showSource = _chkShowSource.Checked;
            _txtSource.Visible = showSource;
            _browser.Visible = !showSource;
            if (showSource) _txtSource.BringToFront();
            else _browser.BringToFront();
        }

        private void Refresh_Preview()
        {
            var values = (!_preRendered && _chkSampleData.Checked) ? BuildSampleValues() : null;

            var subject = Render(_subjectTemplate, values);
            _lblSubject.Text = string.IsNullOrWhiteSpace(subject) ? "(no subject)" : subject;

            var body = Render(_bodyTemplate, values);
            var html = _isHtml ? WrapHtml(body) : WrapPlainText(body);

            _txtSource.Text = html;
            try { _browser.DocumentText = html; }
            catch { /* WebBrowser can throw during teardown; preview is best-effort */ }
        }

        /// <summary>Replaces {Key} / {{Key}} tokens with sample values. Unknown tokens are left as-is.</summary>
        private static string Render(string template, IReadOnlyDictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            if (values == null) return template;

            return PlaceholderRegex.Replace(template, m =>
            {
                var key = m.Groups["key"].Success ? m.Groups["key"].Value : m.Groups["key2"].Value;
                return values.TryGetValue(key.Trim(), out var v) ? v : m.Value;
            });
        }

        private static string WrapHtml(string bodyHtml)
        {
            // If the template already carries a full document, render it untouched.
            if (bodyHtml.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0)
                return bodyHtml;

            return
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\">" +
                "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">" +
                "<style>body{font-family:'Segoe UI',Arial,sans-serif;font-size:14px;color:#1a2333;" +
                "margin:0;padding:16px;background:#f4f6f9;}" +
                ".yk-mail{background:#ffffff;border:1px solid #dde3ec;border-radius:6px;" +
                "padding:24px;max-width:680px;margin:0 auto;}</style></head>" +
                "<body><div class=\"yk-mail\">" + bodyHtml + "</div></body></html>";
        }

        private static string WrapPlainText(string bodyText)
        {
            var encoded = WebUtility.HtmlEncode(bodyText ?? string.Empty);
            return
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head>" +
                "<body style=\"font-family:'Segoe UI',Arial,sans-serif;font-size:14px;color:#1a2333;" +
                "background:#f4f6f9;margin:0;padding:16px;\">" +
                "<div style=\"background:#fff;border:1px solid #dde3ec;border-radius:6px;padding:24px;" +
                "max-width:680px;margin:0 auto;white-space:pre-wrap;\">" + encoded + "</div></body></html>";
        }

        /// <summary>
        /// Sample values for the placeholders used across the system's email templates.
        /// Keys are matched case-insensitively.
        /// </summary>
        private static IReadOnlyDictionary<string, string> BuildSampleValues()
        {
            var now = DateTime.Now;

            // 5-column rows, matching CartridgeManagementForm's modelRowsHtml
            // (Model | Returned Empty | Issued Brand New | Issued Refilled | Pending).
            string cell(string v, bool center) =>
                "<td style=\"border:1px solid #ddd; padding:10px;" + (center ? " text-align:center;" : "") + "\">" + v + "</td>";
            var modelRows =
                "<tr>" + cell("MOON123", false) + cell("1", true) + cell("1", true) + cell("0", true) + cell("0", true) + "</tr>" +
                "<tr>" + cell("SOLAR123", false) + cell("1", true) + cell("1", true) + cell("0", true) + cell("0", true) + "</tr>";

            var itemListTable =
                "<table style=\"border-collapse:collapse;width:100%;\">" +
                "<tr><th style=\"border:1px solid #ddd;padding:8px;text-align:left;\">Item</th>" +
                "<th style=\"border:1px solid #ddd;padding:8px;text-align:center;\">Qty</th></tr>" +
                "<tr><td style=\"border:1px solid #ddd;padding:8px;\">Wireless Mouse</td>" +
                "<td style=\"border:1px solid #ddd;padding:8px;text-align:center;\">2</td></tr>" +
                "<tr><td style=\"border:1px solid #ddd;padding:8px;\">USB-C Dock</td>" +
                "<td style=\"border:1px solid #ddd;padding:8px;text-align:center;\">1</td></tr></table>";

            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // ── People ──────────────────────────────────────────────
                ["EmployeeName"]        = "Juan Dela Cruz",
                ["EMPLOYEE_NAME"]       = "Juan Dela Cruz",
                ["EMPLOYEE_POSITION"]   = "IT Officer",
                ["RequesterName"]       = "Juan Dela Cruz",
                ["RequesterDisplayName"] = "Mr. Juan Dela Cruz",
                ["RequesterTitle"]      = "Mr.",
                ["SUPERVISOR_NAME"]     = "Maria Santos",
                ["ApproverName"]        = "Maria Santos",
                ["ReceivedBy"]          = "Juan Dela Cruz",
                ["CreatedBy"]           = "IT Department",

                // ── Org ─────────────────────────────────────────────────
                ["BranchName"]          = "Manila Office",
                ["BRANCH_NAME"]         = "Manila Office",
                ["RequesterBranch"]     = "Manila Office",
                ["DepartmentName"]      = "Accounting Department",
                ["DEPARTMENT_NAME"]     = "Accounting Department",
                ["RequesterDepartment"] = "Accounting Department",
                ["Department"]          = "Accounting Department",
                ["CompanyName"]         = "YPI",
                ["COMPANY_NAME"]        = "YPI",
                ["RequesterCompany"]    = "YPI",
                ["Company"]             = "Yakult Philippines, Inc.",
                ["DeptCompany"]         = "Accounting Department, Yakult Philippines, Inc.",
                ["RequesterEmail"]      = "juan.delacruz@example.com",

                // ── Dates ───────────────────────────────────────────────
                ["Date"]            = now.ToString("yyyy-MM-dd HH:mm"),
                ["DateTime"]        = now.ToString("MMMM d, yyyy h:mm tt"),
                ["Year"]            = now.Year.ToString(),
                ["CreatedAt"]       = now.ToString("MM/dd/yyyy HH:mm"),
                ["APPROVED_DATE"]   = now.ToString("MMMM dd, yyyy"),
                ["REQUESTED_DATE"]  = now.ToString("MMMM dd, yyyy  h:mm tt"),
                ["EXPIRES_DATE"]    = now.AddMonths(6).ToString("MMMM dd, yyyy"),

                // ── Request / Set ───────────────────────────────────────
                ["RequestId"]       = "10482",
                ["RequestCode"]     = "REQ-10482",
                ["SetCode"]         = "SET-0248",
                ["SetName"]         = "Multi model cartridge exchange",
                ["SetStatus"]       = "Dispatched",
                ["SetRemarks"]      = "Multi model cartridge exchange",
                ["ItemCount"]       = "2",
                ["DistributionMethod"] = "PICKUP",
                ["DistributionStatus"] = "Distributed and Dispatched",
                ["Status"]          = "Approved",
                ["Reason"]          = "Stock replenishment for Q3",
                ["REJECTION_NOTES"] = "No reason provided.",
                ["Notes"]           = "If you have any questions, please contact IT support.",
                ["Note"]            = "If you have any questions, please contact IT support.",

                // ── Totals (cartridge exchange) ─────────────────────────
                ["TotalReturnedEmpty"] = "2",
                ["TotalBrandNew"]      = "2",
                ["TotalRefilled"]      = "0",
                ["TotalPending"]       = "0",

                // ── HTML fragments ─────────────────────────────────────
                ["ModelRows"]     = modelRows,
                ["ItemListTable"] = itemListTable,
                ["QrCodeUrl"]     = "",

                // ── Links ───────────────────────────────────────────────
                ["Link"] = "https://portal.example.local/requests/10482",
                ["Url"]  = "https://portal.example.local/requests/10482",
            };
            return d;
        }
    }
}
