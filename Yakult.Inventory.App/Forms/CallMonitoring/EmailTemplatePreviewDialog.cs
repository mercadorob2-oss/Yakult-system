using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class EmailTemplatePreviewDialog : Form
    {
        private readonly ICallMonitoringRepository _repo;
        private readonly string _templateType;

        private NumericUpDown _numTicketId;
        private Button _btnPickTicket;
        private Button _btnLoadTicket;
        private Label _lblTicketSummary;

        private TextBox _txtSubjectTemplate;
        private TextBox _txtSubjectRendered;
        private RichTextBox _txtBodyTemplate;
        private RichTextBox _txtBodyRendered;

        private TextBox _txtExtraPlaceholders;
        private Label _lblUnresolved;
        private Button _btnRender;
        private Button _btnClose;

        private CallTicketNotificationData _ticket;

        private static readonly Regex PlaceholderRegex = new Regex(
            @"\{\{\s*(?<key>[A-Za-z0-9_]+)\s*\}\}|\{\s*(?<key2>[A-Za-z0-9_]+)\s*\}",
            RegexOptions.Compiled);

        public EmailTemplatePreviewDialog(ICallMonitoringRepository repo, string templateType, string subjectTemplate, string bodyTemplate)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _templateType = string.IsNullOrWhiteSpace(templateType) ? "Template" : templateType.Trim();

            InitializeComponent();
            _txtSubjectTemplate.Text = subjectTemplate ?? string.Empty;
            _txtBodyTemplate.Text = bodyTemplate ?? string.Empty;
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            Text = $"Template Preview - {_templateType}";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Width = 1200;
            Height = 760;
            Font = new Font("Segoe UI", 9.5F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));

            // --- Ticket pick row ---
            var ticketRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 2
            };
            ticketRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
            ticketRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            ticketRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            ticketRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            ticketRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            ticketRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            ticketRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            ticketRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            ticketRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

            ticketRow.Controls.Add(new Label { Text = "Ticket ID", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _numTicketId = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = int.MaxValue,
                DecimalPlaces = 0
            };
            ticketRow.Controls.Add(_numTicketId, 1, 0);

            _btnPickTicket = new Button { Text = "Pick...", Dock = DockStyle.Fill };
            _btnPickTicket.Click += (_, __) => PickTicket();
            ticketRow.Controls.Add(_btnPickTicket, 2, 0);

            _btnLoadTicket = new Button { Text = "Load", Dock = DockStyle.Fill };
            _btnLoadTicket.Click += async (_, __) => await LoadTicketAsync();
            ticketRow.Controls.Add(_btnLoadTicket, 3, 0);

            _lblTicketSummary = new Label
            {
                Text = "No ticket loaded.",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(80, 80, 80),
                TextAlign = ContentAlignment.MiddleLeft
            };
            ticketRow.SetColumnSpan(_lblTicketSummary, 3);
            ticketRow.Controls.Add(_lblTicketSummary, 4, 0);

            _btnRender = new Button { Text = "Render", Dock = DockStyle.Fill };
            _btnRender.Click += async (_, __) => await RenderAsync();
            ticketRow.Controls.Add(_btnRender, 5, 0);

            _btnClose = new Button { Text = "Close", Dock = DockStyle.Fill, DialogResult = DialogResult.Cancel };
            ticketRow.Controls.Add(_btnClose, 6, 0);

            // Row 2 hint
            var hint = new Label
            {
                Text = "Tip: Use a real TicketId to validate placeholders. Use Extra Placeholders for OldStatus/NewStatus/Note, etc.",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(110, 110, 110),
                TextAlign = ContentAlignment.MiddleLeft
            };
            ticketRow.SetColumnSpan(hint, 7);
            ticketRow.Controls.Add(hint, 0, 1);

            // --- Main editor/preview area ---
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 10
            };
            SplitContainerUtil.BindSafeSplitterDistance(split, () => 580);

            split.Panel1.Controls.Add(BuildTemplatePanel());
            split.Panel2.Controls.Add(BuildRenderedPanel());

            // --- Extra placeholders + unresolved ---
            var bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2
            };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            bottom.Controls.Add(new Label { Text = "Extra Placeholders (one per line: Key=Value)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            bottom.Controls.Add(new Label { Text = "Unresolved Placeholders", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 1, 0);

            _txtExtraPlaceholders = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F)
            };
            _txtExtraPlaceholders.Text = "OldStatus=Pending\r\nNewStatus=Escalated\r\nNote=Sample note";

            _lblUnresolved = new Label
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Padding = new Padding(8),
                Text = "(render to see unresolved placeholders)"
            };

            bottom.Controls.Add(_txtExtraPlaceholders, 0, 1);
            bottom.Controls.Add(_lblUnresolved, 1, 1);

            // --- Root ---
            root.Controls.Add(ticketRow, 0, 0);
            root.Controls.Add(split, 0, 1);
            root.Controls.Add(bottom, 0, 2);

            // bottom buttons row is handled via ticket row (close button) to keep UX tight.
            root.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 3);

            Controls.Add(root);
            CancelButton = _btnClose;
            ResumeLayout(false);
        }

        private Control BuildTemplatePanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            panel.Controls.Add(new Label { Text = "Template Subject", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _txtSubjectTemplate = new TextBox { Dock = DockStyle.Fill };
            panel.Controls.Add(_txtSubjectTemplate, 0, 1);

            panel.Controls.Add(new Label { Text = "Template Body", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
            _txtBodyTemplate = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F)
            };
            panel.Controls.Add(_txtBodyTemplate, 0, 3);

            return panel;
        }

        private Control BuildRenderedPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            panel.Controls.Add(new Label { Text = "Rendered Subject", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _txtSubjectRendered = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White };
            panel.Controls.Add(_txtSubjectRendered, 0, 1);

            panel.Controls.Add(new Label { Text = "Rendered Body", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
            _txtBodyRendered = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 9F)
            };
            panel.Controls.Add(_txtBodyRendered, 0, 3);

            return panel;
        }

        private async void PickTicket()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                // Fetch a reasonable set of recent tickets to pick from
                var tickets = await _repo.GetPendingTicketsPageAsync("All", "", null, null, null, false, 3, 1, 100); // 100 tickets max
                Cursor = Cursors.Default;

                using (var dlg = new TicketPickerDialog(tickets, initialQuery: string.Empty))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    if (dlg.SelectedTicketId > 0)
                        _numTicketId.Value = dlg.SelectedTicketId;
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Failed to load tickets list: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadTicketAsync()
        {
            var ticketId = (int)_numTicketId.Value;
            if (ticketId <= 0)
            {
                MessageBox.Show("Enter a valid TicketId.", "Template Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _btnLoadTicket.Enabled = false;
                _btnPickTicket.Enabled = false;
                _btnRender.Enabled = false;
                Cursor = Cursors.WaitCursor;

                _ticket = await _repo.GetTicketNotificationDataAsync(ticketId);
                if (_ticket == null)
                {
                    _lblTicketSummary.Text = "Ticket not found.";
                    MessageBox.Show("Ticket not found.", "Template Preview", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _lblTicketSummary.Text =
                    $"{_ticket.TicketCode} - {_ticket.Status} - {_ticket.Priority} - {_ticket.Department} - {_ticket.CallerName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Load Ticket Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                _btnLoadTicket.Enabled = true;
                _btnPickTicket.Enabled = true;
                _btnRender.Enabled = true;
            }
        }

        private async Task RenderAsync()
        {
            if (_ticket == null || _ticket.TicketId <= 0)
            {
                await LoadTicketAsync();
                if (_ticket == null || _ticket.TicketId <= 0)
                    return;
            }

            try
            {
                _btnRender.Enabled = false;
                Cursor = Cursors.WaitCursor;

                var values = CallEmailNotificationService.BuildBasePlaceholders(_ticket);
                MergeExtraPlaceholders(values, _txtExtraPlaceholders.Text);

                var subjectTemplate = _txtSubjectTemplate.Text ?? string.Empty;
                var bodyTemplate = _txtBodyTemplate.Text ?? string.Empty;

                var renderedSubject = CallEmailNotificationService.Render(subjectTemplate, values);
                var renderedBody = CallEmailNotificationService.Render(bodyTemplate, values);

                _txtSubjectRendered.Text = renderedSubject;
                _txtBodyRendered.Text = renderedBody;

                var unresolved = FindUnresolvedPlaceholders(subjectTemplate, bodyTemplate, values);
                _lblUnresolved.Text = unresolved.Count == 0
                    ? "(none)"
                    : string.Join(Environment.NewLine, unresolved.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Render Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                _btnRender.Enabled = true;
            }
        }

        private static void MergeExtraPlaceholders(Dictionary<string, string> values, string text)
        {
            if (values == null || string.IsNullOrWhiteSpace(text))
                return;

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                    continue;

                var key = (parts[0] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var value = (parts[1] ?? string.Empty).Trim();
                values[key] = value;
            }
        }

        private static List<string> FindUnresolvedPlaceholders(string subject, string body, IReadOnlyDictionary<string, string> values)
        {
            var unresolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var known = values != null
                ? new HashSet<string>(values.Keys, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in ExtractPlaceholderKeys(subject))
            {
                if (!known.Contains(key))
                    unresolved.Add(key);
            }

            foreach (var key in ExtractPlaceholderKeys(body))
            {
                if (!known.Contains(key))
                    unresolved.Add(key);
            }

            return unresolved.ToList();
        }

        private static IEnumerable<string> ExtractPlaceholderKeys(string text)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            foreach (Match match in PlaceholderRegex.Matches(text))
            {
                var key = match.Groups["key"].Success ? match.Groups["key"].Value : match.Groups["key2"].Value;
                if (!string.IsNullOrWhiteSpace(key))
                    yield return key.Trim();
            }
        }
    }
}
