using System;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class ConfirmTicketCreationDialog : Form
    {
        public enum CreationAction
        {
            Cancel,
            Create,
            CreateAndOpen,
            CreateAnother
        }

        public sealed class TicketCreationModel
        {
            public string Company { get; set; }
            public string Department { get; set; }
            public string Branch { get; set; }
            public string Caller { get; set; }
            public string AssignedTo { get; set; }
            public string Priority { get; set; }
            public string EscalationSummary { get; set; }
            public string EscalationReason { get; set; }
            public string Issue { get; set; }
            public string InitialNotes { get; set; }
            public string BackdateDisplay { get; set; }
            public string RecentOpenTicketWarning { get; set; }
            public string SuggestedPriority { get; set; }
            public string PrioritySuggestionReason { get; set; }
            public string TicketContactEmail { get; set; }
            public string TicketContactEmailSource { get; set; }
            public bool RequiresTemporaryTicketContactEmail { get; set; }
            public bool RequiresCallerEmail { get; set; }
            public bool CanLinkTicketContactEmailToCaller { get; set; }
        }

        private readonly TicketCreationModel _model;
        private ComboBox _priorityCombo;
        private TextBox _temporaryTicketContactEmailBox;
        private CheckBox _linkCallerEmailToProfileCheckBox;
        private CheckBox _useOrgFallbackCheckBox;

        public ConfirmTicketCreationDialog(TicketCreationModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            InitializeComponent();
        }

        public CreationAction SelectedAction { get; private set; } = CreationAction.Cancel;
        public string SelectedPriority => (_priorityCombo?.SelectedItem as string) ?? _model.Priority ?? "Medium";
        public string SelectedTicketContactEmail { get; private set; }

        // Never link the organization fallback as a personal email: the
        // link is only honored when the operator actually typed an address.
        public bool ShouldLinkTicketContactEmailToCaller => _linkCallerEmailToProfileCheckBox?.Checked == true
            && !string.IsNullOrWhiteSpace(_temporaryTicketContactEmailBox?.Text)
            && _useOrgFallbackCheckBox?.Checked != true;

        public bool UseOrganizationalFallback => !_model.RequiresCallerEmail
            || _useOrgFallbackCheckBox?.Checked == true;
        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Font;
            Font = ModernUiHelper.FontNormal;
            BackColor = Color.FromArgb(245, 247, 250);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Text = "Confirm Ticket Creation";
            ClientSize = new Size(900, 760);
            MinimumSize = new Size(760, 620);

            var contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(28, 22, 28, 22),
                AutoScroll = true,
                BackColor = Color.FromArgb(245, 247, 250)
            };
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                BackColor = Color.FromArgb(245, 247, 250),
                Margin = new Padding(0)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 74,
                BackColor = Color.White,
                Padding = new Padding(18, 14, 18, 10),
                Margin = new Padding(0, 0, 0, 14)
            };
            header.Paint += (_, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240)))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            var headerTitle = new Label
            {
                AutoSize = true,
                Text = "Confirm ticket creation",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = ModernUiHelper.ColorTextPrimary,
                Location = new Point(18, 12)
            };
            var headerSubtitle = new Label
            {
                AutoSize = true,
                Text = "Review the requester, contact routing, and ticket details before saving.",
                ForeColor = ModernUiHelper.ColorTextSecondary,
                Location = new Point(19, 42)
            };
            var reviewBadge = ModernUiHelper.CreateBadge("REVIEW", ModernUiHelper.ColorPrimary);
            reviewBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            reviewBadge.Location = new Point(760, 18);
            header.Controls.Add(headerTitle);
            header.Controls.Add(headerSubtitle);
            header.Controls.Add(reviewBadge);
            content.Controls.Add(header);

            var createCard = new Func<string, Control, Color, Panel>((title, child, background) =>
            {
                var card = new Panel
                {
                    AutoSize = true,
                    Dock = DockStyle.Top,
                    BackColor = background,
                    BorderStyle = BorderStyle.FixedSingle,
                    Padding = new Padding(16),
                    Margin = new Padding(0)
                };
                var inner = new TableLayoutPanel
                {
                    AutoSize = true,
                    Dock = DockStyle.Top,
                    ColumnCount = 1,
                    Margin = new Padding(0)
                };
                inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                var titleLabel = new Label
                {
                    AutoSize = true,
                    Text = title,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = ModernUiHelper.ColorTextPrimary,
                    Margin = new Padding(0, 0, 0, 10)
                };
                child.Dock = DockStyle.Top;
                inner.Controls.Add(titleLabel, 0, 0);
                inner.Controls.Add(child, 0, 1);
                card.Controls.Add(inner);
                return card;
            });

            var summaryColumns = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0, 0, 0, 14)
            };
            summaryColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            summaryColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var requesterGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0)
            };
            requesterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105F));
            requesterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            AddRow(requesterGrid, "Caller", _model.Caller);
            AddRow(requesterGrid, "Company", _model.Company);
            AddRow(requesterGrid, "Department", _model.Department);
            AddRow(requesterGrid, "Branch", _model.Branch);
            var requesterCard = createCard("Requester & organization", requesterGrid, Color.White);
            requesterCard.Margin = new Padding(0, 0, 7, 0);
            summaryColumns.Controls.Add(requesterCard, 0, 0);

            var handlingGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0)
            };
            handlingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105F));
            handlingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            AddRow(handlingGrid, "Assigned to", _model.AssignedTo);
            _priorityCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 170,
                Margin = new Padding(0, 0, 0, 4)
            };
            _priorityCombo.Items.AddRange(new object[] { "Low", "Medium", "High", "Critical" });
            var initialPriority = string.IsNullOrWhiteSpace(_model.SuggestedPriority) ? _model.Priority : _model.SuggestedPriority;
            if (!_priorityCombo.Items.Contains(initialPriority)) initialPriority = "Medium";
            _priorityCombo.SelectedItem = initialPriority;
            AddRow(handlingGrid, "Priority", _priorityCombo);
            var escalationPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0)
            };
            escalationPanel.Controls.Add(ModernUiHelper.CreateBadge(
                _model.EscalationSummary ?? "-",
                (_model.EscalationSummary ?? string.Empty).StartsWith("Custom", StringComparison.OrdinalIgnoreCase)
                    ? Color.FromArgb(217, 119, 6)
                    : Color.FromArgb(100, 116, 139)));
            AddRow(handlingGrid, "Escalation", escalationPanel);
            if (!string.IsNullOrWhiteSpace(_model.EscalationReason))
                AddRow(handlingGrid, "Reason", _model.EscalationReason.Trim());
            if (!string.IsNullOrWhiteSpace(_model.BackdateDisplay))
                AddRow(handlingGrid, "Backdate", _model.BackdateDisplay + " (logbook entry)");
            if (!string.IsNullOrWhiteSpace(_model.PrioritySuggestionReason))
                AddRow(handlingGrid, "Suggestion", _model.PrioritySuggestionReason.Trim());
            var handlingCard = createCard("Ticket handling", handlingGrid, Color.White);
            handlingCard.Margin = new Padding(7, 0, 0, 0);
            summaryColumns.Controls.Add(handlingCard, 1, 0);
            content.Controls.Add(summaryColumns);

            var contactGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0)
            };
            contactGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155F));
            contactGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            var needsContactInput = _model.RequiresCallerEmail || _model.RequiresTemporaryTicketContactEmail;
            var contactBadge = ModernUiHelper.CreateBadge(
                _model.RequiresCallerEmail
                    ? "CALLER EMAIL (OPTIONAL)"
                    : (_model.RequiresTemporaryTicketContactEmail ? "TICKET CONTACT REQUIRED" : "CONTACT RESOLVED"),
                needsContactInput ? Color.FromArgb(217, 119, 6) : Color.FromArgb(22, 163, 74));
            AddRow(contactGrid, "Status", contactBadge);
            if (_model.RequiresCallerEmail)
            {
                AddRow(contactGrid, "Information", "Selected caller has no active primary personal email. Leave blank to use the fallback below.");
                AddRow(contactGrid, "Org fallback", string.IsNullOrWhiteSpace(_model.TicketContactEmail) ? "No branch or department email found" : _model.TicketContactEmail);
                if (!string.IsNullOrWhiteSpace(_model.TicketContactEmailSource))
                    AddRow(contactGrid, "Fallback source", _model.TicketContactEmailSource);
                var hasFallback = !string.IsNullOrWhiteSpace(_model.TicketContactEmail)
                    && EmailAddressValidator.TryNormalize(_model.TicketContactEmail, out _);
                _useOrgFallbackCheckBox = new CheckBox
                {
                    AutoSize = true,
                    Text = "Use organizational fallback above",
                    Checked = hasFallback,
                    Enabled = hasFallback,
                    ForeColor = ModernUiHelper.ColorTextPrimary,
                    Margin = new Padding(0, 0, 0, 4)
                };
                AddRow(contactGrid, "Fallback", _useOrgFallbackCheckBox);
                _temporaryTicketContactEmailBox = new TextBox
                {
                    Width = 360,
                    MaxLength = 255,
                    Margin = new Padding(0, 0, 0, 4),
                    Enabled = !hasFallback
                };
                AddRow(contactGrid, "Caller email (optional)", _temporaryTicketContactEmailBox);
                if (_model.CanLinkTicketContactEmailToCaller)
                {
                    _linkCallerEmailToProfileCheckBox = new CheckBox
                    {
                        AutoSize = true,
                        Text = "Link this as the caller's active primary employee email",
                        ForeColor = ModernUiHelper.ColorTextPrimary,
                        Margin = new Padding(0, 0, 0, 4),
                        Enabled = !hasFallback
                    };
                    AddRow(contactGrid, "Caller profile", _linkCallerEmailToProfileCheckBox);
                }
                else
                {
                    _linkCallerEmailToProfileCheckBox = null;
                }
                _useOrgFallbackCheckBox.CheckedChanged += (_, __) =>
                {
                    var useFallback = _useOrgFallbackCheckBox.Checked;
                    if (_temporaryTicketContactEmailBox != null)
                        _temporaryTicketContactEmailBox.Enabled = !useFallback;
                    if (_linkCallerEmailToProfileCheckBox != null)
                        _linkCallerEmailToProfileCheckBox.Enabled = !useFallback;
                };
                AddRow(contactGrid, string.Empty, "Leave blank to use the fallback above. A typed address saves only on this ticket; selecting the option also updates the caller's employee profile.");
            }
            else if (_model.RequiresTemporaryTicketContactEmail)
            {
                AddRow(contactGrid, "Information", "No linked employee, branch, or department email was found.");
                _temporaryTicketContactEmailBox = new TextBox
                {
                    Width = 360,
                    MaxLength = 255,
                    Margin = new Padding(0, 0, 0, 4)
                };
                AddRow(contactGrid, "Temporary email (required)", _temporaryTicketContactEmailBox);
                _linkCallerEmailToProfileCheckBox = null;
                _useOrgFallbackCheckBox = null;
                AddRow(contactGrid, string.Empty, "Saved only on this ticket; it does not change shared email settings.");
            }
            else
            {
                AddRow(contactGrid, "Ticket contact", _model.TicketContactEmail);
                AddRow(contactGrid, "Source", _model.TicketContactEmailSource);
                _temporaryTicketContactEmailBox = null;
                _linkCallerEmailToProfileCheckBox = null;
                _useOrgFallbackCheckBox = null;
            }
            var contactCard = createCard("Contact routing", contactGrid, needsContactInput ? Color.FromArgb(255, 251, 235) : Color.FromArgb(240, 253, 244));
            contactCard.Margin = new Padding(0, 0, 0, 14);
            content.Controls.Add(contactCard);

            if (!string.IsNullOrWhiteSpace(_model.RecentOpenTicketWarning))
            {
                var recentBox = CreateReadOnlyBox(_model.RecentOpenTicketWarning.Trim());
                recentBox.Height = 74;
                recentBox.BackColor = Color.FromArgb(255, 251, 235);
                var recentCard = createCard("Recent open tickets", recentBox, Color.FromArgb(255, 251, 235));
                recentCard.Margin = new Padding(0, 0, 0, 14);
                content.Controls.Add(recentCard);
            }

            var textColumns = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0)
            };
            textColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            textColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            var issueBox = CreateReadOnlyBox(_model.Issue);
            issueBox.Height = 125;
            var notesBox = CreateReadOnlyBox(string.IsNullOrWhiteSpace(_model.InitialNotes) ? "(None)" : _model.InitialNotes);
            notesBox.Height = 125;
            var issueCard = createCard("Issue", issueBox, Color.White);
            var notesCard = createCard("Initial notes", notesBox, Color.White);
            issueCard.Margin = new Padding(0, 0, 7, 0);
            notesCard.Margin = new Padding(7, 0, 0, 0);
            textColumns.Controls.Add(issueCard, 0, 0);
            textColumns.Controls.Add(notesCard, 1, 0);
            content.Controls.Add(textColumns);

            contentHost.Controls.Add(content);

            var buttonsPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 72,
                BackColor = Color.White,
                Padding = new Padding(28, 14, 28, 14)
            };
            buttonsPanel.Paint += (_, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240)))
                    e.Graphics.DrawLine(pen, 0, 0, buttonsPanel.Width, 0);
            };
            var footerHint = new Label
            {
                AutoSize = true,
                Text = "Nothing is saved until you choose a create action.",
                ForeColor = ModernUiHelper.ColorTextSecondary,
                Location = new Point(28, 27)
            };
            buttonsPanel.Controls.Add(footerHint);
            var rightFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 2, 0, 0)
            };
            var btnCreate = ModernUiHelper.CreatePrimaryButton("Create", width: 96);
            btnCreate.Click += (_, __) => Complete(CreationAction.Create);
            var btnCreateOpen = ModernUiHelper.CreateSecondaryButton("Create and Open", width: 132);
            btnCreateOpen.Click += (_, __) => Complete(CreationAction.CreateAndOpen);
            var btnCreateAnother = ModernUiHelper.CreateSecondaryButton("Create Another", width: 130);
            btnCreateAnother.Click += (_, __) => Complete(CreationAction.CreateAnother);
            var btnCancel = ModernUiHelper.CreateSecondaryButton("Cancel", width: 100);
            btnCancel.Click += (_, __) => Complete(CreationAction.Cancel);
            rightFlow.Controls.Add(btnCreate);
            rightFlow.Controls.Add(btnCreateOpen);
            rightFlow.Controls.Add(btnCreateAnother);
            rightFlow.Controls.Add(btnCancel);
            buttonsPanel.Controls.Add(rightFlow);

            AcceptButton = btnCreate;
            CancelButton = btnCancel;
            Controls.Add(contentHost);
            Controls.Add(buttonsPanel);
            ResumeLayout(false);
            PerformLayout();
        }
        private void Complete(CreationAction action)
        {
            if (action != CreationAction.Cancel)
            {
                // Optional caller email: checked fallback box (or blank) uses the
                // resolved organization address. The temporary-contact path has no
                // fallback, so blank still blocks there.
                var useFallback = _model.RequiresCallerEmail && _useOrgFallbackCheckBox?.Checked == true;
                var typed = useFallback
                    ? null
                    : (_model.RequiresCallerEmail || _model.RequiresTemporaryTicketContactEmail)
                        ? _temporaryTicketContactEmailBox?.Text
                        : null;
                var contactEmail = string.IsNullOrWhiteSpace(typed) ? _model.TicketContactEmail : typed;
                if (!EmailAddressValidator.TryNormalize(contactEmail, out var normalizedContactEmail))
                {
                    MessageBox.Show(
                        this,
                        "Enter a valid ticket contact email before creating the ticket.",
                        "Ticket Contact Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    _temporaryTicketContactEmailBox?.Focus();
                    return;
                }

                SelectedTicketContactEmail = normalizedContactEmail;
            }

            SelectedAction = action;
            DialogResult = action == CreationAction.Cancel ? DialogResult.Cancel : DialogResult.OK;
            Close();
        }

        public static string SuggestPriority(string issue, out string reason)
        {
            reason = null;
            var text = (issue ?? string.Empty).Trim();
            if (text.Length == 0)
                return "Medium";

            var lower = text.ToLowerInvariant();
            if (ContainsAny(lower, "system down", "server down", "network down", "cannot login", "can't login", "urgent", "critical", "production down"))
            {
                reason = "Suggested: Critical because the issue appears urgent or blocks access/service.";
                return "Critical";
            }

            if (ContainsAny(lower, "not working", "printer not working", "offline", "no internet", "cannot print", "unable to print", "failed"))
            {
                reason = "Suggested: High because the issue likely blocks normal work.";
                return "High";
            }

            if (ContainsAny(lower, "slow", "intermittent", "request", "setup", "install"))
            {
                reason = "Suggested: Medium based on the issue description.";
                return "Medium";
            }

            return "Medium";
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            foreach (var needle in needles)
            {
                if (!string.IsNullOrWhiteSpace(needle) && text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static TextBox CreateReadOnlyBox(string text)
        {
            return new TextBox
            {
                Text = text ?? string.Empty,
                Dock = DockStyle.Top,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White,
                Height = 110
            };
        }

        private static void AddRow(TableLayoutPanel grid, string label, string value)
        {
            AddRow(grid, label, new Label
            {
                AutoSize = true,
                ForeColor = ModernUiHelper.ColorTextPrimary,
                Text = value ?? "-"
            });
        }

        private static void AddRow(TableLayoutPanel grid, string label, Control valueControl)
        {
            if (grid == null)
                return;

            var rowIndex = grid.RowCount;
            grid.RowCount = rowIndex + 1;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lbl = ModernUiHelper.CreateLabel(label);
            lbl.Margin = new Padding(0, 8, 10, 0);
            lbl.Dock = DockStyle.Fill;

            valueControl.Margin = new Padding(0, 8, 0, 0);
            valueControl.Dock = DockStyle.Fill;

            grid.Controls.Add(lbl, 0, rowIndex);
            grid.Controls.Add(valueControl, 1, rowIndex);
        }
    }
}
