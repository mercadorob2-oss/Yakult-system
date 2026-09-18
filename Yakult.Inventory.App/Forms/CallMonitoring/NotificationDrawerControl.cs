using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class NotificationDrawerControl : UserControl
    {
        private enum FeedFilter
        {
            Attention,
            Overdue,
            Unassigned,
            AssignedToMe,
            Urgent,
            Today,
            All
        }

        private enum FeedSort
        {
            Risk,
            Newest,
            Oldest,
            SlaNearest
        }

        private enum FeedGroup
        {
            NeedsActionNow,
            AssignedToMe,
            RecentlyUpdated
        }

        private sealed class TicketNotification
        {
            public CallTicketListItem Ticket { get; set; }
            public int Score { get; set; }
            public bool IsOverdue { get; set; }
            public bool IsUnassigned { get; set; }
            public bool IsUrgent { get; set; }
            public bool IsToday { get; set; }
            public bool IsAssignedToMe { get; set; }
            public bool IsEscalated { get; set; }
            public bool IsStale { get; set; }
            public bool IsUnread { get; set; }
            public bool IsPinned { get; set; }
            public bool IsMuted { get; set; }
            public bool IsSnoozed { get; set; }
            public DateTime? SnoozedUntil { get; set; }
            public string Reason { get; set; }
        }

        private sealed class TicketRowControl : UserControl
        {
            private readonly Panel _pnlStripe;
            private readonly Label _lblTitle;
            private readonly Label _lblIssue;
            private readonly Label _lblMeta;
            private readonly Label _lblSub;
            private readonly Label _lblTagPriority;
            private readonly Label _lblTagStatus;
            private readonly Label _lblInlineStatus;
            private readonly Label _lblInlinePriority;
            private readonly Label _lblInlineSla;
            private readonly Label _lblInlineEta;
            private readonly Label _lblInlineUnread;
            private readonly Button _btnOpen;
            private readonly Button _btnMore;
            private readonly Button _btnPin;
            private readonly Button _btnMute;
            private readonly ContextMenuStrip _menu;
            private readonly ToolStripMenuItem _miResolve;
            private readonly ToolStripMenuItem _miToggleRead;
            private readonly ToolStripMenuItem _miTogglePin;
            private readonly ToolStripMenuItem _miToggleMute;
            private readonly ToolStripMenuItem _miSnooze;
            private bool _isUnread;

            public event Action OpenRequested;
            public event Action AssignToMeRequested;
            public event Action InProgressRequested;
            public event Action AddNoteRequested;
            public event Action EscalateRequested;
            public event Action EscalationOverrideRequested;
            public event Action ResolveRequested;
            public event Action<bool> ToggleReadRequested;
            public event Action TogglePinRequested;
            public event Action ToggleMuteRequested;
            public event Action SnoozeRequested;

            public string ToolTipText { get; private set; }

            public TicketRowControl()
            {
                this.Height = 166;
                this.Dock = DockStyle.Top;
                this.Margin = new Padding(0, 0, 0, 8);
                this.Padding = new Padding(0);
                this.BackColor = Color.White;
                this.Cursor = Cursors.Hand;
                this.DoubleBuffered = true;

                _pnlStripe = new Panel
                {
                    Dock = DockStyle.Left,
                    Width = 6,
                    BackColor = ModernUiHelper.ColorPrimary
                };

                var pnlMain = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 10, 8) };

                var pnlText = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };

                _lblTitle = new Label
                {
                    AutoSize = false,
                    Dock = DockStyle.Top,
                    Height = 22,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = ModernUiHelper.ColorTextPrimary,
                    AutoEllipsis = true,
                    UseCompatibleTextRendering = true
                };

                _lblIssue = new Label
                {
                    AutoSize = false,
                    Dock = DockStyle.Top,
                    Height = 30,
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                    ForeColor = ModernUiHelper.ColorTextPrimary,
                    AutoEllipsis = true,
                    UseCompatibleTextRendering = true
                };

                _lblMeta = new Label
                {
                    AutoSize = false,
                    Dock = DockStyle.Top,
                    Height = 18,
                    Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                    ForeColor = ModernUiHelper.ColorTextSecondary,
                    AutoEllipsis = true
                };

                _lblSub = new Label
                {
                    AutoSize = false,
                    Dock = DockStyle.Top,
                    Height = 18,
                    Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                    ForeColor = Color.FromArgb(120, 130, 140),
                    AutoEllipsis = true
                };

                var pnlInlineTags = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    Height = 26,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    AutoScroll = false,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };

                _lblInlineUnread = ModernUiHelper.CreateBadge("Unread", Color.FromArgb(46, 134, 222));
                _lblInlineUnread.AutoSize = false;
                _lblInlineUnread.Width = 66;
                _lblInlineUnread.Height = 20;
                _lblInlineUnread.Margin = new Padding(0, 2, 6, 0);
                _lblInlineUnread.TextAlign = ContentAlignment.MiddleCenter;

                _lblInlineStatus = ModernUiHelper.CreateBadge("Status", Color.FromArgb(120, 130, 140));
                _lblInlineStatus.AutoSize = false;
                _lblInlineStatus.Width = 86;
                _lblInlineStatus.Height = 20;
                _lblInlineStatus.Margin = new Padding(0, 2, 6, 0);
                _lblInlineStatus.TextAlign = ContentAlignment.MiddleCenter;
                _lblInlineStatus.AutoEllipsis = true;

                _lblInlinePriority = ModernUiHelper.CreateBadge("Priority", ModernUiHelper.ColorPrimary);
                _lblInlinePriority.AutoSize = false;
                _lblInlinePriority.Width = 78;
                _lblInlinePriority.Height = 20;
                _lblInlinePriority.Margin = new Padding(0, 2, 6, 0);
                _lblInlinePriority.TextAlign = ContentAlignment.MiddleCenter;
                _lblInlinePriority.AutoEllipsis = true;

                _lblInlineSla = ModernUiHelper.CreateBadge("SLA", Color.FromArgb(120, 130, 140));
                _lblInlineSla.AutoSize = false;
                _lblInlineSla.Width = 96;
                _lblInlineSla.Height = 20;
                _lblInlineSla.Margin = new Padding(0, 2, 6, 0);
                _lblInlineSla.TextAlign = ContentAlignment.MiddleCenter;
                _lblInlineSla.AutoEllipsis = true;

                _lblInlineEta = ModernUiHelper.CreateBadge("ETA", Color.FromArgb(120, 130, 140));
                _lblInlineEta.AutoSize = false;
                _lblInlineEta.Width = 120;
                _lblInlineEta.Height = 20;
                _lblInlineEta.Margin = new Padding(0, 2, 0, 0);
                _lblInlineEta.TextAlign = ContentAlignment.MiddleCenter;
                _lblInlineEta.AutoEllipsis = true;

                pnlInlineTags.Controls.Add(_lblInlineUnread);
                pnlInlineTags.Controls.Add(_lblInlineStatus);
                pnlInlineTags.Controls.Add(_lblInlinePriority);
                pnlInlineTags.Controls.Add(_lblInlineSla);
                pnlInlineTags.Controls.Add(_lblInlineEta);

                var pnlInlineActions = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    Height = 28,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    AutoScroll = false,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0),
                    Padding = new Padding(0, 2, 0, 0)
                };

                _btnOpen = CreateActionButton("Open", 64);
                _btnMore = CreateActionButton("Modify", 72);
                _btnPin = CreateActionToggleButton("Pin");
                _btnMute = CreateActionToggleButton("Mute");
                _btnOpen.Margin = new Padding(0, 0, 6, 0);
                _btnMore.Margin = new Padding(0, 0, 6, 0);
                _btnPin.Margin = new Padding(0, 0, 6, 0);

                pnlInlineActions.Controls.Add(_btnOpen);
                pnlInlineActions.Controls.Add(_btnMore);
                pnlInlineActions.Controls.Add(_btnPin);
                pnlInlineActions.Controls.Add(_btnMute);

                const int rightPanelWidth = 0;

                var pnlRight = new TableLayoutPanel
                {
                    Width = rightPanelWidth,
                    MinimumSize = new Size(rightPanelWidth, 0),
                    BackColor = Color.Transparent,
                    Margin = new Padding(0),
                    Padding = new Padding(0),
                    ColumnCount = 1,
                    RowCount = 2
                };
                pnlRight.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                pnlRight.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
                pnlRight.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));

                var pnlRightBadges = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };

                _lblTagStatus = ModernUiHelper.CreateBadge("STATUS", Color.FromArgb(120, 130, 140));
                _lblTagStatus.Margin = new Padding(0);
                _lblTagStatus.AutoSize = false;
                _lblTagStatus.Width = 86;
                _lblTagStatus.Height = 24;
                _lblTagStatus.TextAlign = ContentAlignment.MiddleCenter;
                _lblTagStatus.AutoEllipsis = true;

                _lblTagPriority = ModernUiHelper.CreateBadge("PRIORITY", ModernUiHelper.ColorPrimary);
                _lblTagPriority.Margin = new Padding(0);
                _lblTagPriority.AutoSize = false;
                _lblTagPriority.Width = 78;
                _lblTagPriority.Height = 24;
                _lblTagPriority.TextAlign = ContentAlignment.MiddleCenter;
                _lblTagPriority.AutoEllipsis = true;

                pnlRightBadges.Controls.Add(_lblTagPriority);
                pnlRightBadges.Controls.Add(_lblTagStatus);

                pnlRight.Controls.Add(pnlRightBadges, 0, 0);

                pnlText.Controls.Add(_lblSub);
                pnlText.Controls.Add(_lblMeta);
                pnlText.Controls.Add(pnlInlineActions);
                pnlText.Controls.Add(pnlInlineTags);
                pnlText.Controls.Add(_lblIssue);
                pnlText.Controls.Add(_lblTitle);

                // Use a simple 2-column layout so the status/priority badges never "disappear"
                // due to Dock order/z-order quirks (Fill overlapping Right).
                var tlpMain = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 1,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                tlpMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                tlpMain.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, rightPanelWidth));
                tlpMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

                pnlText.Dock = DockStyle.Fill;
                pnlRight.Dock = DockStyle.Fill;
                tlpMain.Controls.Add(pnlText, 0, 0);
                tlpMain.Controls.Add(pnlRight, 1, 0);

                pnlMain.Controls.Add(tlpMain);

                this.Controls.Add(pnlMain);
                this.Controls.Add(_pnlStripe);

                _menu = new ContextMenuStrip();
                _menu.Items.Add("Open").Click += (_, __) => OpenRequested?.Invoke();
                _miToggleRead = new ToolStripMenuItem("Mark as read");
                _miToggleRead.Click += (_, __) => ToggleReadRequested?.Invoke(_isUnread);
                _menu.Items.Add(_miToggleRead);
                _miTogglePin = new ToolStripMenuItem("Pin");
                _miTogglePin.Click += (_, __) => TogglePinRequested?.Invoke();
                _menu.Items.Add(_miTogglePin);
                _miToggleMute = new ToolStripMenuItem("Mute");
                _miToggleMute.Click += (_, __) => ToggleMuteRequested?.Invoke();
                _menu.Items.Add(_miToggleMute);
                _miSnooze = new ToolStripMenuItem("Snooze (2h)");
                _miSnooze.Click += (_, __) => SnoozeRequested?.Invoke();
                _menu.Items.Add(_miSnooze);
                _menu.Items.Add(new ToolStripSeparator());
                _menu.Items.Add("Assign to me").Click += (_, __) => AssignToMeRequested?.Invoke();
                _menu.Items.Add("Set In Progress").Click += (_, __) => InProgressRequested?.Invoke();
                _miResolve = new ToolStripMenuItem("Resolve");
                _miResolve.Click += (_, __) => ResolveRequested?.Invoke();
                _menu.Items.Add(_miResolve);
                _menu.Items.Add("Add note…").Click += (_, __) => AddNoteRequested?.Invoke();
                _menu.Items.Add(new ToolStripSeparator());
                _menu.Items.Add("Escalate…").Click += (_, __) => EscalateRequested?.Invoke();
                _menu.Items.Add("Escalation override…").Click += (_, __) => EscalationOverrideRequested?.Invoke();

                _btnMore.Click += (_, __) => _menu.Show(_btnMore, new Point(0, _btnMore.Height));
                _btnOpen.Click += (_, __) => OpenRequested?.Invoke();
                _btnPin.Click += (_, __) => TogglePinRequested?.Invoke();
                _btnMute.Click += (_, __) => ToggleMuteRequested?.Invoke();

                this.Click += (_, __) => OpenRequested?.Invoke();
                tlpMain.Click += (_, __) => OpenRequested?.Invoke();
                pnlText.Click += (_, __) => OpenRequested?.Invoke();
                pnlRight.Click += (_, __) => OpenRequested?.Invoke();
                _lblTitle.Click += (_, __) => OpenRequested?.Invoke();
                _lblIssue.Click += (_, __) => OpenRequested?.Invoke();
                _lblMeta.Click += (_, __) => OpenRequested?.Invoke();
                _lblSub.Click += (_, __) => OpenRequested?.Invoke();
                _lblTagStatus.Click += (_, __) => OpenRequested?.Invoke();
                _lblTagPriority.Click += (_, __) => OpenRequested?.Invoke();
                _lblInlineUnread.Click += (_, __) => OpenRequested?.Invoke();
                _lblInlineStatus.Click += (_, __) => OpenRequested?.Invoke();
                _lblInlinePriority.Click += (_, __) => OpenRequested?.Invoke();
                _lblInlineSla.Click += (_, __) => OpenRequested?.Invoke();
                _lblInlineEta.Click += (_, __) => OpenRequested?.Invoke();
                pnlInlineTags.Click += (_, __) => OpenRequested?.Invoke();

                Action<Control> wireContextMenu = control =>
                {
                    if (control == null) return;
                    control.MouseUp += (_, e) =>
                    {
                        if (e.Button == MouseButtons.Right)
                            _menu.Show(control, e.Location);
                    };
                };

                wireContextMenu(this);
                wireContextMenu(tlpMain);
                wireContextMenu(pnlText);
                wireContextMenu(pnlRight);
                wireContextMenu(_lblTitle);
                wireContextMenu(_lblIssue);
                wireContextMenu(_lblMeta);
                wireContextMenu(_lblSub);
                wireContextMenu(_lblTagStatus);
                wireContextMenu(_lblTagPriority);
                wireContextMenu(_lblInlineUnread);
                wireContextMenu(_lblInlineStatus);
                wireContextMenu(_lblInlinePriority);
                wireContextMenu(_lblInlineSla);
                wireContextMenu(_lblInlineEta);
                wireContextMenu(pnlInlineTags);

                this.Paint += (_, e) =>
                {
                    using (var pen = new Pen(Color.FromArgb(235, 235, 235)))
                        e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
                };

                this.MouseEnter += (_, __) => this.BackColor = Color.FromArgb(248, 250, 252);
                this.MouseLeave += (_, __) => this.BackColor = Color.White;
            }

            public void Bind(TicketNotification n, int overdueDays)
            {
                var t = n?.Ticket;
                if (t == null) return;

                _lblTitle.Text = string.IsNullOrWhiteSpace(t.TicketCode) ? $"Ticket #{t.TicketId}" : t.TicketCode.Trim();
                _isUnread = n.IsUnread;
                _lblIssue.Text = NormalizeOneLine(t.Issue);

                var metaParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(t.Company)) metaParts.Add(t.Company.Trim());
                if (!string.IsNullOrWhiteSpace(t.Branch)) metaParts.Add(t.Branch.Trim());
                if (!string.IsNullOrWhiteSpace(t.Department)) metaParts.Add(t.Department.Trim());
                _lblMeta.Text = metaParts.Count > 0 ? string.Join("  •  ", metaParts) : "—";

                var updatedAt = t.UpdatedAt != default ? t.UpdatedAt : t.CreatedAt;
                var idleDays = NotificationDrawerControl.GetIdleDays(t);
                var lastActivityUtc = NotificationDrawerControl.GetLastActivityUtc(t);
                var assignedLabel = !string.IsNullOrWhiteSpace(t.ResponsiblePerson) ? t.ResponsiblePerson.Trim() : "Unassigned";
                _lblSub.Text = $"{assignedLabel}  •  Updated {ToRelativeShort(updatedAt)}";

                _lblTagPriority.Text = string.IsNullOrWhiteSpace(t.Priority) ? "—" : t.Priority.Trim();
                _lblTagPriority.BackColor = PriorityColor(t.Priority);
                _lblTagStatus.Text = string.IsNullOrWhiteSpace(t.Status) ? "—" : t.Status.Trim();
                _lblTagStatus.BackColor = StatusColor(t.Status);

                _lblInlineUnread.Visible = _isUnread;
                _lblInlineStatus.Text = _lblTagStatus.Text;
                _lblInlineStatus.BackColor = _lblTagStatus.BackColor;
                _lblInlinePriority.Text = _lblTagPriority.Text;
                _lblInlinePriority.BackColor = _lblTagPriority.BackColor;
                _lblInlineSla.Text = BuildSlaLabel(idleDays, overdueDays, out var slaColor);
                _lblInlineSla.BackColor = slaColor;
                _lblInlineEta.Text = BuildBreachEtaLabel(lastActivityUtc, overdueDays, out var etaColor);
                _lblInlineEta.BackColor = etaColor;

                _miToggleRead.Text = _isUnread ? "Mark as read" : "Mark as unread";
                _miTogglePin.Text = n.IsPinned ? "Unpin" : "Pin";
                _miToggleMute.Text = n.IsMuted ? "Unmute" : "Mute";

                _pnlStripe.BackColor =
                    n.IsPinned ? Color.FromArgb(241, 196, 15) :
                    n.IsMuted ? Color.FromArgb(189, 195, 199) :
                    n.IsEscalated ? Color.FromArgb(243, 156, 18) :
                    n.IsOverdue ? Color.FromArgb(231, 76, 60) :
                    n.IsUnassigned ? Color.FromArgb(52, 152, 219) :
                    n.IsUrgent ? Color.FromArgb(230, 126, 34) :
                    n.IsStale ? Color.FromArgb(155, 89, 182) :
                    ModernUiHelper.ColorPrimary;

                _btnPin.Text = n.IsPinned ? "Pinned" : "Pin";
                _btnPin.BackColor = n.IsPinned ? Color.FromArgb(255, 243, 205) : Color.White;
                _btnPin.ForeColor = n.IsPinned ? Color.FromArgb(166, 124, 0) : ModernUiHelper.ColorTextSecondary;
                _btnPin.FlatAppearance.BorderColor = n.IsPinned ? Color.FromArgb(241, 196, 15) : Color.FromArgb(230, 230, 230);

                _btnMute.Text = n.IsMuted ? "Muted" : "Mute";
                _btnMute.BackColor = n.IsMuted ? Color.FromArgb(237, 242, 247) : Color.White;
                _btnMute.ForeColor = n.IsMuted ? Color.FromArgb(90, 100, 110) : ModernUiHelper.ColorTextSecondary;
                _btnMute.FlatAppearance.BorderColor = n.IsMuted ? Color.FromArgb(189, 195, 199) : Color.FromArgb(230, 230, 230);

                var status = (t.Status ?? string.Empty).Trim();
                _miResolve.Enabled =
                    !status.Equals("Solved", StringComparison.OrdinalIgnoreCase) &&
                    !status.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase) &&
                    !status.Equals("Closed", StringComparison.OrdinalIgnoreCase);

                if (status.Equals("Escalated", StringComparison.OrdinalIgnoreCase))
                {
                    ToolTipText = "Escalated";
                }
                else if (n.SnoozedUntil.HasValue && n.SnoozedUntil.Value > DateTime.Now)
                {
                    ToolTipText = $"Snoozed until {n.SnoozedUntil.Value:MMM dd hh:mm tt}";
                }
                else if (overdueDays > 0 && idleDays >= overdueDays)
                {
                    ToolTipText = $"Overdue (idle {idleDays}d)";
                }
                else
                {
                    ToolTipText = null;
                }
            }

            private static string BuildSlaLabel(int idleDays, int overdueDays, out Color color)
            {
                if (overdueDays <= 0)
                {
                    color = Color.FromArgb(120, 130, 140);
                    return idleDays > 0 ? $"Idle {idleDays}d" : "SLA n/a";
                }

                var remainingDays = overdueDays - idleDays;
                if (remainingDays < 0)
                {
                    color = Color.FromArgb(231, 76, 60);
                    return $"Overdue {Math.Abs(remainingDays)}d";
                }

                if (remainingDays == 0)
                {
                    color = Color.FromArgb(230, 126, 34);
                    return "Due today";
                }

                color = Color.FromArgb(52, 152, 219);
                return $"Due in {remainingDays}d";
            }

            private static string BuildBreachEtaLabel(DateTime lastActivityUtc, int overdueDays, out Color color)
            {
                if (overdueDays <= 0 || lastActivityUtc == default)
                {
                    color = Color.FromArgb(120, 130, 140);
                    return "ETA n/a";
                }

                var lastUtc = lastActivityUtc.Kind == DateTimeKind.Utc
                    ? lastActivityUtc
                    : DateTime.SpecifyKind(lastActivityUtc, DateTimeKind.Utc);
                var etaUtc = lastUtc.AddDays(overdueDays);
                var span = etaUtc - DateTime.UtcNow;
                if (span.TotalSeconds >= 0)
                {
                    color = span.TotalHours <= 8 ? Color.FromArgb(230, 126, 34) : Color.FromArgb(52, 152, 219);
                    return "ETA " + ToCompactSpan(span);
                }

                color = Color.FromArgb(192, 57, 43);
                return "Breached " + ToCompactSpan(span.Negate());
            }

            private static string ToCompactSpan(TimeSpan span)
            {
                if (span.TotalMinutes < 1) return "now";
                if (span.TotalHours < 1) return $"{Math.Max(1, (int)Math.Round(span.TotalMinutes))}m";
                if (span.TotalDays < 1) return $"{Math.Max(1, (int)Math.Round(span.TotalHours))}h";
                return $"{Math.Max(1, (int)Math.Round(span.TotalDays))}d";
            }

            private static Button CreateActionToggleButton(string text)
            {
                var button = new Button
                {
                    Text = text,
                    Width = 54,
                    Height = 24,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.White,
                    ForeColor = ModernUiHelper.ColorTextSecondary,
                    Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                    Cursor = Cursors.Hand,
                    Margin = new Padding(0)
                };
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = Color.FromArgb(230, 230, 230);
                return button;
            }

            private static Button CreateActionButton(string text, int width)
            {
                var button = CreateActionToggleButton(text);
                button.Width = Math.Max(54, width);
                button.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
                return button;
            }

            private static string NormalizeOneLine(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return "(No issue)";
                s = s.Replace("\r", " ").Replace("\n", " ").Trim();
                while (s.Contains("  ")) s = s.Replace("  ", " ");
                return s;
            }

            private static Color PriorityColor(string p)
            {
                if (string.IsNullOrWhiteSpace(p)) return Color.FromArgb(120, 130, 140);
                p = p.Trim();
                if (p.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(192, 57, 43);
                if (p.Equals("High", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(230, 126, 34);
                if (p.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(241, 196, 15);
                if (p.Equals("Low", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(46, 204, 113);
                return Color.FromArgb(120, 130, 140);
            }

            private static Color StatusColor(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return Color.FromArgb(120, 130, 140);
                s = NormalizeLegacyTicketStatus(s);
                if (s.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(243, 156, 18);
                if (s.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(52, 152, 219);
                if (s.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(39, 174, 96);
                return Color.FromArgb(120, 130, 140);
            }

            private static string NormalizeLegacyTicketStatus(string status)
            {
                var s = (status ?? string.Empty).Trim();
                if (s.Equals("Waiting on Vendor", StringComparison.OrdinalIgnoreCase)
                    || s.Equals("Waiting on Department", StringComparison.OrdinalIgnoreCase))
                {
                    return "In Progress";
                }

                return string.IsNullOrWhiteSpace(s) ? "-" : s;
            }

            private static string ToRelativeShort(DateTime dt)
            {
                if (dt == default) return "—";
                var now = DateTime.Now;
                var span = now - dt;
                if (span.TotalSeconds < 0) span = TimeSpan.Zero;

                if (span.TotalMinutes < 2) return "just now";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 36) return $"{(int)Math.Round(span.TotalHours)}h ago";
                return $"{(int)Math.Floor(span.TotalDays)}d ago";
            }
        }

        private ICallMonitoringRepository _repo;
        private CallEscalationSettingsItem _escSettings;
        private CancellationTokenSource _refreshCts;
        private int _refreshGate;

        private readonly ToolTip _toolTip;
        private readonly TableLayoutPanel _root;
        private readonly Panel _pnlHeader;
        private readonly Panel _pnlControls;
        private readonly Button _btnClose;
        private readonly Button _btnRefresh;
        private readonly TextBox _txtSearch;
        private readonly ComboBox _cboSort;
        private readonly FlowLayoutPanel _pnlChips;
        private readonly FlowLayoutPanel _pnlBulkActions;
        private readonly Button _btnBulkActions;
        private readonly ContextMenuStrip _bulkActionsMenu;
        private readonly Panel _pnlList;
        private readonly Label _lblEmpty;
        private readonly Label _lblFooter;
        private readonly LinkLabel _lnkViewAll;
        private readonly LinkLabel _lnkMarkAllRead;
        private readonly System.Windows.Forms.Timer _renderDebounce;

        private FeedFilter _filter = FeedFilter.Attention;
        private FeedSort _sort = FeedSort.Risk;
        private List<TicketNotification> _items = new List<TicketNotification>();
        private List<TicketNotification> _currentView = new List<TicketNotification>();
        private readonly HashSet<int> _readTicketIds = new HashSet<int>();
        private readonly HashSet<int> _pinnedTicketIds = new HashSet<int>();
        private readonly HashSet<int> _mutedTicketIds = new HashSet<int>();
        private readonly Dictionary<int, DateTime> _snoozedUntilByTicketId = new Dictionary<int, DateTime>();
        private int _overdueDays = 3;

        public Func<int, Task> OpenTicketAsync { get; set; }
        public Func<Task> OpenTicketListAsync { get; set; }
        public Func<Task> AfterMutationAsync { get; set; }

        public event Action CloseRequested;
        public event Action<int> AttentionCountChanged;

        public NotificationDrawerControl()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;
            this.Padding = new Padding(0);
            this.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

            _toolTip = new ToolTip { AutoPopDelay = 15000, InitialDelay = 250, ReshowDelay = 100, ShowAlways = true };
            _renderDebounce = new System.Windows.Forms.Timer { Interval = 200 };
            _renderDebounce.Tick += (_, __) =>
            {
                try
                {
                    _renderDebounce.Stop();
                    Render();
                }
                catch
                {
                }
            };

            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.White
            };
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 196));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            _pnlHeader = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(250, 252, 254), Padding = new Padding(14, 10, 10, 10) };
            var pnlHeader = _pnlHeader;
            var lblHeader = new Label
            {
                Text = "Notifications",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = ModernUiHelper.ColorTextPrimary
            };
            var lblHeaderSub = new Label
            {
                Text = "IT Call Monitoring",
                Dock = DockStyle.Top,
                Height = 18,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = ModernUiHelper.ColorTextSecondary
            };

            var pnlHeaderRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 120,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            _btnClose = new Button
            {
                Text = "\uE711",
                Width = 34,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = ModernUiHelper.ColorTextSecondary,
                Font = new Font("Segoe MDL2 Assets", 10F),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0)
            };
            _btnClose.FlatAppearance.BorderSize = 1;
            _btnClose.FlatAppearance.BorderColor = Color.FromArgb(230, 230, 230);
            _btnClose.Click += (_, __) => CloseRequested?.Invoke();

            _btnRefresh = new Button
            {
                Text = "\uE72C",
                Width = 34,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = ModernUiHelper.ColorTextSecondary,
                Font = new Font("Segoe MDL2 Assets", 11F),
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0)
            };
            _btnRefresh.FlatAppearance.BorderSize = 1;
            _btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(230, 230, 230);
            _btnRefresh.Click += async (_, __) => await RefreshAsync();

            _toolTip.SetToolTip(_btnRefresh, "Refresh feed");
            _toolTip.SetToolTip(_btnClose, "Close");

            pnlHeaderRight.Controls.Add(_btnClose);
            pnlHeaderRight.Controls.Add(_btnRefresh);
            pnlHeader.Controls.Add(pnlHeaderRight);
            pnlHeader.Controls.Add(lblHeaderSub);
            pnlHeader.Controls.Add(lblHeader);

            _pnlControls = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14, 10, 14, 8) };
            _txtSearch = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 130, 140),
                Text = "Search tickets…"
            };
            _txtSearch.GotFocus += (_, __) =>
            {
                if (_txtSearch.Text == "Search tickets…")
                {
                    _txtSearch.Text = string.Empty;
                    _txtSearch.ForeColor = ModernUiHelper.ColorTextPrimary;
                }
            };
            _txtSearch.LostFocus += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                {
                    _txtSearch.Text = "Search tickets…";
                    _txtSearch.ForeColor = Color.FromArgb(120, 130, 140);
                }
            };
            _txtSearch.TextChanged += (_, __) => RequestRenderDebounced();

            _cboSort = new ComboBox
            {
                Dock = DockStyle.Top,
                Height = 28,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(0, 6, 0, 0)
            };
            _cboSort.Items.Add("Sort: Risk (default)");
            _cboSort.Items.Add("Sort: Newest");
            _cboSort.Items.Add("Sort: Oldest");
            _cboSort.Items.Add("Sort: SLA nearest");
            _cboSort.SelectedIndex = 0;
            _cboSort.SelectedIndexChanged += (_, __) =>
            {
                switch (_cboSort.SelectedIndex)
                {
                    case 1:
                        _sort = FeedSort.Newest;
                        break;
                    case 2:
                        _sort = FeedSort.Oldest;
                        break;
                    case 3:
                        _sort = FeedSort.SlaNearest;
                        break;
                    default:
                        _sort = FeedSort.Risk;
                        break;
                }
                Render();
            };

            _pnlChips = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 58,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false,
                Padding = new Padding(0, 6, 0, 0),
                Margin = new Padding(0, 6, 0, 0)
            };

            _pnlBulkActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 30,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(0, 1, 0, 0),
                Margin = new Padding(0, 6, 0, 0),
                BackColor = Color.White
            };

            _btnBulkActions = ModernUiHelper.CreateSecondaryButton("Bulk actions (0) \u25BE", 176);
            _btnBulkActions.Height = 24;
            _btnBulkActions.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            _btnBulkActions.Margin = new Padding(0, 0, 0, 0);

            _bulkActionsMenu = new ContextMenuStrip();
            _bulkActionsMenu.Items.Add("Assign to me").Click += async (_, __) => await BulkAssignVisibleAsync();
            _bulkActionsMenu.Items.Add("Set In Progress").Click += async (_, __) => await BulkSetInProgressVisibleAsync();
            _bulkActionsMenu.Items.Add("Mark Read").Click += (_, __) => BulkMarkVisibleAsRead();
            _bulkActionsMenu.Items.Add("Snooze 2h").Click += (_, __) => BulkSnoozeVisible(TimeSpan.FromHours(2));

            _btnBulkActions.Click += (_, __) =>
            {
                if (_btnBulkActions.Enabled)
                    _bulkActionsMenu.Show(_btnBulkActions, new Point(0, _btnBulkActions.Height));
            };

            _pnlBulkActions.Controls.Add(_btnBulkActions);

            _pnlControls.Controls.Add(_pnlBulkActions);
            _pnlControls.Controls.Add(_pnlChips);
            _pnlControls.Controls.Add(_cboSort);
            _pnlControls.Controls.Add(_txtSearch);
            _pnlControls.SizeChanged += (_, __) => UpdateControlsRowHeight();

            var pnlListHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14, 8, 14, 8) };
            _pnlList = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White
            };
            _pnlList.SizeChanged += (_, __) => FitRowsToWidth();

            _lblEmpty = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = ModernUiHelper.ColorTextSecondary,
                Visible = false
            };

            pnlListHost.Controls.Add(_pnlList);
            pnlListHost.Controls.Add(_lblEmpty);

            var pnlFooter = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(250, 252, 254), Padding = new Padding(14, 6, 14, 6) };
            _lblFooter = new Label
            {
                Dock = DockStyle.Left,
                AutoSize = false,
                Width = 260,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 130, 140),
                Text = "Stateless feed • refresh for latest"
            };

            _lnkViewAll = new LinkLabel
            {
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Width = 140,
                Text = "View ticket list →",
                LinkColor = ModernUiHelper.ColorPrimary,
                ActiveLinkColor = ModernUiHelper.ColorPrimary,
                VisitedLinkColor = ModernUiHelper.ColorPrimary
            };
            _lnkViewAll.LinkClicked += async (_, __) =>
            {
                if (OpenTicketListAsync != null)
                    await OpenTicketListAsync();
            };

            _lnkMarkAllRead = new LinkLabel
            {
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false,
                Width = 120,
                Text = "Mark all read",
                LinkColor = ModernUiHelper.ColorPrimary,
                ActiveLinkColor = ModernUiHelper.ColorPrimary,
                VisitedLinkColor = ModernUiHelper.ColorPrimary
            };
            _lnkMarkAllRead.LinkClicked += (_, __) => MarkAllAsRead();

            pnlFooter.Controls.Add(_lnkViewAll);
            pnlFooter.Controls.Add(_lnkMarkAllRead);
            pnlFooter.Controls.Add(_lblFooter);

            _root.Controls.Add(_pnlHeader, 0, 0);
            _root.Controls.Add(_pnlControls, 0, 1);
            _root.Controls.Add(pnlListHost, 0, 2);
            _root.Controls.Add(pnlFooter, 0, 3);

            this.Controls.Add(_root);
            BuildChips();
            UpdateControlsRowHeight();
            this.SizeChanged += (_, __) => UpdateControlsRowHeight();
        }

        private void RequestRenderDebounced()
        {
            if (this.IsDisposed)
                return;

            try
            {
                _renderDebounce.Stop();
                _renderDebounce.Start();
            }
            catch
            {
                Render();
            }
        }

        private static bool ContainsIgnoreCase(string haystack, string needle)
        {
            if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(needle))
                return false;
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void Initialize(ICallMonitoringRepository repo)
        {
            _repo = repo;
        }

        public void HideInternalHeader()
        {
            if (_pnlHeader != null)
                _pnlHeader.Visible = false;
            if (_root != null && _root.RowStyles.Count > 0)
                _root.RowStyles[0] = new RowStyle(SizeType.Absolute, 0);
        }

        public async Task RefreshAsync()
        {
            if (_repo == null)
                return;

            if (Interlocked.Exchange(ref _refreshGate, 1) == 1)
                return;

            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = new CancellationTokenSource();
            var token = _refreshCts.Token;

            SetLoading(true);

            try
            {
                if (!await _repo.CallSchemaExistsAsync())
                {
                    _items = new List<TicketNotification>();
                    AttentionCountChanged?.Invoke(0);
                    _lblEmpty.Text = "Call Monitoring tables are missing.\r\nRun the Call Monitoring DB scripts, then refresh.";
                    _lblEmpty.Visible = true;
                    _pnlList.Controls.Clear();
                    return;
                }

                var overdueDaysTask = _repo.GetOverdueDaysAsync(defaultDays: 3);
                var ticketsTask = _repo.GetOpenTicketsListAsync(maxRows: 500);
                await Task.WhenAll(overdueDaysTask, ticketsTask);

                _overdueDays = overdueDaysTask.Result;
                var tickets = ticketsTask.Result;
                if (token.IsCancellationRequested) return;

                _items = BuildFeed(tickets ?? new List<CallTicketListItem>(), _overdueDays);
                var activeTicketIds = new HashSet<int>(_items.Select(x => x.Ticket?.TicketId ?? 0).Where(id => id > 0));
                _readTicketIds.RemoveWhere(id => !activeTicketIds.Contains(id));
                _pinnedTicketIds.RemoveWhere(id => !activeTicketIds.Contains(id));
                _mutedTicketIds.RemoveWhere(id => !activeTicketIds.Contains(id));

                var staleSnoozes = _snoozedUntilByTicketId
                    .Where(x => !activeTicketIds.Contains(x.Key) || x.Value <= DateTime.Now)
                    .Select(x => x.Key)
                    .ToList();
                foreach (var key in staleSnoozes)
                    _snoozedUntilByTicketId.Remove(key);

                foreach (var item in _items)
                {
                    var ticketId = item.Ticket?.TicketId ?? 0;
                    item.IsUnread = ticketId > 0 && !_readTicketIds.Contains(ticketId);
                    item.IsAssignedToMe = IsAssignedToCurrentUser(item.Ticket);
                    item.IsPinned = ticketId > 0 && _pinnedTicketIds.Contains(ticketId);
                    item.IsMuted = ticketId > 0 && _mutedTicketIds.Contains(ticketId);
                    if (ticketId > 0 && _snoozedUntilByTicketId.TryGetValue(ticketId, out var snoozeUntil))
                    {
                        item.SnoozedUntil = snoozeUntil;
                        item.IsSnoozed = snoozeUntil > DateTime.Now;
                    }
                    else
                    {
                        item.SnoozedUntil = null;
                        item.IsSnoozed = false;
                    }
                }

                AttentionCountChanged?.Invoke(_items.Count(IsAttention));
                RebuildChips();

                Render();
            }
            catch (Exception ex)
            {
                _items = new List<TicketNotification>();
                AttentionCountChanged?.Invoke(0);
                _lblEmpty.Text = "Failed to load notifications.\r\n" + ex.Message;
                _lblEmpty.Visible = true;
                _pnlList.Controls.Clear();
            }
            finally
            {
                SetLoading(false);
                Interlocked.Exchange(ref _refreshGate, 0);
            }
        }

        private void SetLoading(bool loading)
        {
            _btnRefresh.Enabled = !loading;
            _txtSearch.Enabled = !loading;
            _cboSort.Enabled = !loading;
            _pnlChips.Enabled = !loading;
            _pnlBulkActions.Enabled = !loading;
            _lnkViewAll.Enabled = !loading;
            _lnkMarkAllRead.Enabled = !loading;
            _lblFooter.Text = loading ? "Loading…" : "Stateless feed • refresh for latest";
            UpdateBulkActionsButton(_currentView?.Count ?? 0);
        }

        private static bool IsAttention(TicketNotification n)
        {
            if (n == null) return false;
            if (n.IsMuted) return false;
            if (n.IsSnoozed && n.SnoozedUntil.HasValue && n.SnoozedUntil.Value > DateTime.Now) return false;
            return n.IsEscalated || n.IsOverdue || n.IsUnassigned || n.IsUrgent;
        }

        private static bool IsAssignedToCurrentUser(CallTicketListItem ticket)
        {
            if (ticket == null) return false;

            var currentEmployeeId = AppSession.CurrentEmployeeId;
            if (currentEmployeeId.HasValue && currentEmployeeId.Value > 0 &&
                ticket.AssignedToEmpId.HasValue && ticket.AssignedToEmpId.Value > 0)
            {
                return ticket.AssignedToEmpId.Value == currentEmployeeId.Value;
            }

            var currentEmployeeName = (AppSession.CurrentEmployeeName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(currentEmployeeName) && !string.IsNullOrWhiteSpace(ticket.ResponsiblePerson))
            {
                return string.Equals(ticket.ResponsiblePerson.Trim(), currentEmployeeName, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static DateTime GetLastActivityUtc(CallTicketListItem ticket)
        {
            if (ticket == null)
                return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

            if (ticket.LastContactAt.HasValue)
            {
                var dt = ticket.LastContactAt.Value;
                return dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }

            var updated = ticket.UpdatedAt != default ? ticket.UpdatedAt : ticket.CreatedAt;
            return updated.Kind == DateTimeKind.Utc ? updated : DateTime.SpecifyKind(updated, DateTimeKind.Utc);
        }

        private static int GetIdleDays(CallTicketListItem ticket)
        {
            var lastActivityUtc = GetLastActivityUtc(ticket);
            var idle = DateTime.UtcNow - lastActivityUtc;
            if (idle < TimeSpan.Zero) idle = TimeSpan.Zero;
            return Math.Max(0, (int)Math.Floor(idle.TotalDays));
        }

        private static List<TicketNotification> BuildFeed(List<CallTicketListItem> tickets, int overdueDays)
        {
            var now = DateTime.Now;
            var list = new List<TicketNotification>();

            foreach (var t in tickets.Where(x => x != null))
            {
                var status = (t.Status ?? string.Empty).Trim();
                var priority = (t.Priority ?? string.Empty).Trim();

                var isEsc = status.Equals("Escalated", StringComparison.OrdinalIgnoreCase);
                var idleDays = GetIdleDays(t);
                var isOverdue = overdueDays > 0 && idleDays >= overdueDays;

                var assignedEmpty = string.IsNullOrWhiteSpace(t.ResponsiblePerson);
                var isUnassigned = assignedEmpty || (t.AssignedToEmpId == null || t.AssignedToEmpId <= 0);

                var isUrgent = priority.Equals("Critical", StringComparison.OrdinalIgnoreCase)
                               || priority.Equals("High", StringComparison.OrdinalIgnoreCase);

                var isToday = t.CreatedAt != default && t.CreatedAt.Date == DateTime.Today;

                var updatedAt = t.UpdatedAt != default ? t.UpdatedAt : t.CreatedAt;
                var isStale = updatedAt != default && (now - updatedAt).TotalDays >= 2.0;

                var score = 0;
                if (isEsc) score += 1000;
                if (isOverdue) score += 800;
                if (priority.Equals("Critical", StringComparison.OrdinalIgnoreCase)) score += 450;
                if (priority.Equals("High", StringComparison.OrdinalIgnoreCase)) score += 300;
                if (isUnassigned) score += 260;
                if (isStale) score += 160;
                if (isToday) score += 80;

                if (score <= 0)
                    score = 20;

                list.Add(new TicketNotification
                {
                    Ticket = t,
                    Score = score,
                    IsEscalated = isEsc,
                    IsOverdue = isOverdue,
                    IsUnassigned = isUnassigned,
                    IsUrgent = isUrgent,
                    IsToday = isToday,
                    IsStale = isStale,
                    Reason =
                        isEsc ? "Escalated" :
                        isOverdue ? "Overdue" :
                        isUnassigned ? "Unassigned" :
                        isUrgent ? "Urgent" :
                        isStale ? "Stale" :
                        isToday ? "New today" :
                        "Recently updated"
                });
            }

            return list
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Ticket?.UpdatedAt ?? x.Ticket?.CreatedAt ?? DateTime.MinValue)
                .Take(120)
                .ToList();
        }

        private void BuildChips()
        {
            RebuildChips();
        }

        private void RebuildChips()
        {
            _pnlChips.Controls.Clear();
            _pnlChips.Controls.Add(Chip($"Attention ({CountForFilter(FeedFilter.Attention)})", FeedFilter.Attention));
            _pnlChips.Controls.Add(Chip($"Overdue ({CountForFilter(FeedFilter.Overdue)})", FeedFilter.Overdue));
            _pnlChips.Controls.Add(Chip($"Unassigned ({CountForFilter(FeedFilter.Unassigned)})", FeedFilter.Unassigned));
            var assignedToMeChip = Chip($"Assigned to me ({CountForFilter(FeedFilter.AssignedToMe)})", FeedFilter.AssignedToMe);
            assignedToMeChip.Enabled = (AppSession.CurrentEmployeeId ?? 0) > 0 || !string.IsNullOrWhiteSpace(AppSession.CurrentEmployeeName);
            _pnlChips.Controls.Add(assignedToMeChip);
            _pnlChips.Controls.Add(Chip($"Critical/High ({CountForFilter(FeedFilter.Urgent)})", FeedFilter.Urgent));
            _pnlChips.Controls.Add(Chip($"Today ({CountForFilter(FeedFilter.Today)})", FeedFilter.Today));
            _pnlChips.Controls.Add(Chip($"All ({CountForFilter(FeedFilter.All)})", FeedFilter.All));
            UpdateControlsRowHeight();
        }

        private int CountForFilter(FeedFilter filter)
        {
            var source = _items ?? new List<TicketNotification>();
            switch (filter)
            {
                case FeedFilter.Attention:
                    return source.Count(IsAttention);
                case FeedFilter.Overdue:
                    return source.Count(x => x.IsOverdue);
                case FeedFilter.Unassigned:
                    return source.Count(x => x.IsUnassigned);
                case FeedFilter.AssignedToMe:
                    return source.Count(x => x.IsAssignedToMe);
                case FeedFilter.Urgent:
                    return source.Count(x => x.IsUrgent);
                case FeedFilter.Today:
                    return source.Count(x => x.IsToday);
                case FeedFilter.All:
                default:
                    return source.Count;
            }
        }

        private Button Chip(string label, FeedFilter f)
        {
            var btn = ModernUiHelper.CreateFilterChip(label, _filter == f, () =>
            {
                _filter = f;
                RebuildChips();
                Render();
            });

            // Drawer-specific: slightly more compact so all filters fit.
            btn.Height = 26;
            btn.Margin = new Padding(3, 0, 3, 0);
            btn.Padding = new Padding(10, 0, 10, 0);
            btn.Font = new Font("Segoe UI", 8.75F, _filter == f ? FontStyle.Bold : FontStyle.Regular);
            return btn;
        }

        private void UpdateBulkActionsButton(int visibleCount)
        {
            if (_btnBulkActions == null || _btnBulkActions.IsDisposed)
                return;

            var safeCount = Math.Max(0, visibleCount);
            _btnBulkActions.Text = $"Bulk actions ({safeCount}) \u25BE";
            _btnBulkActions.Enabled = safeCount > 0 && _btnRefresh.Enabled;
        }

        private void UpdateControlsRowHeight()
        {
            if (_pnlControls == null || _pnlControls.IsDisposed || _root == null || _root.RowStyles.Count < 2)
                return;

            var availableChipWidth = _pnlControls.ClientSize.Width - _pnlControls.Padding.Left - _pnlControls.Padding.Right;
            if (availableChipWidth > 0)
            {
                var preferredChip = _pnlChips.GetPreferredSize(new Size(availableChipWidth, int.MaxValue));
                _pnlChips.Height = Math.Max(32, preferredChip.Height);
            }

            var contentHeight =
                _pnlControls.Padding.Top +
                _txtSearch.Height +
                6 +
                _cboSort.Height +
                6 +
                _pnlChips.Height +
                6 +
                _pnlBulkActions.Height +
                _pnlControls.Padding.Bottom;

            var targetHeight = Math.Max(166, contentHeight);
            if (Math.Abs(_root.RowStyles[1].Height - targetHeight) > 0.5f)
            {
                _root.RowStyles[1].Height = targetHeight;
                _root.PerformLayout();
            }
        }

        private void Render()
        {
            if (this.IsDisposed)
                return;

            var q = (_txtSearch.Text ?? string.Empty).Trim();
            var hasQuery = !string.IsNullOrWhiteSpace(q) && q != "Search tickets…";
            ApplyTicketStateFlags();

            IEnumerable<TicketNotification> view = _items ?? Enumerable.Empty<TicketNotification>();
            if (_filter != FeedFilter.All)
                view = view.Where(x => !x.IsSnoozed);

            switch (_filter)
            {
                case FeedFilter.Attention:
                    view = view.Where(IsAttention);
                    break;
                case FeedFilter.Overdue:
                    view = view.Where(x => x.IsOverdue);
                    break;
                case FeedFilter.Unassigned:
                    view = view.Where(x => x.IsUnassigned);
                    break;
                case FeedFilter.AssignedToMe:
                    view = view.Where(x => x.IsAssignedToMe);
                    break;
                case FeedFilter.Urgent:
                    view = view.Where(x => x.IsUrgent);
                    break;
                case FeedFilter.Today:
                    view = view.Where(x => x.IsToday);
                    break;
                case FeedFilter.All:
                    break;
            }

            if (hasQuery)
            {
                view = view.Where(x =>
                {
                    var t = x.Ticket;
                    if (t == null) return false;
                    return ContainsIgnoreCase(t.TicketCode, q)
                           || ContainsIgnoreCase(t.Issue, q)
                           || ContainsIgnoreCase(t.Company, q)
                           || ContainsIgnoreCase(t.Branch, q)
                           || ContainsIgnoreCase(t.Department, q)
                           || ContainsIgnoreCase(t.CallerName, q)
                           || ContainsIgnoreCase(t.ResponsiblePerson, q);
                });
            }

            view = ApplySort(view);
            var rows = view.Take(120).ToList();
            _currentView = rows;
            UpdateBulkActionsButton(rows.Count);

            _pnlList.SuspendLayout();
            _pnlList.Controls.Clear();

            if (rows.Count == 0)
            {
                if (_filter == FeedFilter.AssignedToMe &&
                    (AppSession.CurrentEmployeeId ?? 0) <= 0 &&
                    string.IsNullOrWhiteSpace(AppSession.CurrentEmployeeName))
                {
                    _lblEmpty.Text = "Assigned-to-me needs your account linked to an employee.";
                }
                else
                {
                    _lblEmpty.Text = _items.Count == 0 ? "No notifications." : "No matches.";
                }
                _lblEmpty.Visible = true;
                _pnlList.Visible = false;
            }
            else
            {
                _lblEmpty.Visible = false;
                _pnlList.Visible = true;

                var controls = new List<Control>();
                AddGroupControls(controls, "Needs action now", rows.Where(x => ResolveGroup(x) == FeedGroup.NeedsActionNow).ToList());
                AddGroupControls(controls, "Assigned to me", rows.Where(x => ResolveGroup(x) == FeedGroup.AssignedToMe).ToList());
                AddGroupControls(controls, "Recently updated", rows.Where(x => ResolveGroup(x) == FeedGroup.RecentlyUpdated).ToList());

                for (var i = controls.Count - 1; i >= 0; i--)
                    _pnlList.Controls.Add(controls[i]);
            }

            _pnlList.ResumeLayout();
            FitRowsToWidth();
            UpdateFooter(rows);

            // Re-apply once after layout/scrollbar state settles. In AutoScroll containers,
            // initial row widths can briefly stay at default and hide the right badge strip.
            if (!this.IsDisposed && _pnlList != null && !_pnlList.IsDisposed && _pnlList.IsHandleCreated)
            {
                _pnlList.BeginInvoke((Action)(() =>
                {
                    if (!this.IsDisposed && _pnlList != null && !_pnlList.IsDisposed)
                        FitRowsToWidth();
                }));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    _renderDebounce?.Stop();
                    _renderDebounce?.Dispose();
                }
                catch { }

                try
                {
                    _refreshCts?.Cancel();
                }
                catch { }

                try
                {
                    _refreshCts?.Dispose();
                }
                catch { }
            }

            base.Dispose(disposing);
        }

        private FeedGroup ResolveGroup(TicketNotification notification)
        {
            if (notification == null) return FeedGroup.RecentlyUpdated;
            if (IsAttention(notification)) return FeedGroup.NeedsActionNow;
            if (notification.IsAssignedToMe) return FeedGroup.AssignedToMe;
            return FeedGroup.RecentlyUpdated;
        }

        private void AddGroupControls(List<Control> controls, string sectionTitle, List<TicketNotification> rows)
        {
            if (controls == null || rows == null || rows.Count == 0)
                return;

            controls.Add(CreateGroupHeader(sectionTitle, rows.Count));
            foreach (var notification in rows)
                controls.Add(CreateRow(notification));
        }

        private Control CreateGroupHeader(string title, int count)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = Color.White,
                Padding = new Padding(2, 10, 2, 2),
                Margin = new Padding(0)
            };

            var label = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(110, 120, 130),
                Text = $"{title.ToUpperInvariant()} ({count})"
            };

            panel.Controls.Add(label);
            return panel;
        }

        private Control CreateRow(TicketNotification notification)
        {
            var row = new TicketRowControl();
            row.Dock = DockStyle.Top;
            row.Bind(notification, _overdueDays);
            var ticket = notification?.Ticket;
            if (ticket != null)
            {
                _toolTip.SetToolTip(row, BuildTicketTooltip(ticket, notification, _overdueDays));
            }
            else if (!string.IsNullOrWhiteSpace(row.ToolTipText))
            {
                _toolTip.SetToolTip(row, row.ToolTipText);
            }

            var ticketId = ticket?.TicketId ?? 0;
            row.OpenRequested += async () => await SafeOpenTicketAsync(ticketId);
            row.AssignToMeRequested += async () => await AssignToMeAsync(ticketId);
            row.InProgressRequested += async () => await SetStatusAsync(ticketId, "In Progress", note: null);
            row.ResolveRequested += async () => await SetStatusAsync(ticketId, "Solved", note: null);
            row.AddNoteRequested += async () => await AddNoteAsync(ticketId);
            row.EscalateRequested += async () => await EscalateAsync(ticketId);
            row.EscalationOverrideRequested += async () => await EscalationOverrideAsync(ticketId);
            row.ToggleReadRequested += markAsRead => MarkTicketReadState(ticketId, markAsRead);
            row.TogglePinRequested += () => TogglePin(ticketId);
            row.ToggleMuteRequested += () => ToggleMute(ticketId);
            row.SnoozeRequested += () => SnoozeTicket(ticketId, TimeSpan.FromHours(2));
            return row;
        }

        private void ApplyTicketStateFlags()
        {
            if (_items == null || _items.Count == 0)
                return;

            var now = DateTime.Now;
            var expired = _snoozedUntilByTicketId.Where(x => x.Value <= now).Select(x => x.Key).ToList();
            foreach (var ticketId in expired)
                _snoozedUntilByTicketId.Remove(ticketId);

            foreach (var item in _items)
            {
                var ticketId = item.Ticket?.TicketId ?? 0;
                item.IsAssignedToMe = IsAssignedToCurrentUser(item.Ticket);
                item.IsPinned = ticketId > 0 && _pinnedTicketIds.Contains(ticketId);
                item.IsMuted = ticketId > 0 && _mutedTicketIds.Contains(ticketId);
                if (ticketId > 0 && _snoozedUntilByTicketId.TryGetValue(ticketId, out var snoozeUntil))
                {
                    item.IsSnoozed = snoozeUntil > now;
                    item.SnoozedUntil = snoozeUntil;
                }
                else
                {
                    item.IsSnoozed = false;
                    item.SnoozedUntil = null;
                }
            }
        }

        private void TogglePin(int ticketId)
        {
            if (ticketId <= 0) return;
            if (_pinnedTicketIds.Contains(ticketId)) _pinnedTicketIds.Remove(ticketId);
            else _pinnedTicketIds.Add(ticketId);
            RebuildChips();
            Render();
        }

        private void ToggleMute(int ticketId)
        {
            if (ticketId <= 0) return;
            if (_mutedTicketIds.Contains(ticketId)) _mutedTicketIds.Remove(ticketId);
            else _mutedTicketIds.Add(ticketId);
            RebuildChips();
            Render();
        }

        private void SnoozeTicket(int ticketId, TimeSpan duration)
        {
            if (ticketId <= 0) return;
            _snoozedUntilByTicketId[ticketId] = DateTime.Now.Add(duration);
            RebuildChips();
            Render();
        }

        private void UpdateFooter(List<TicketNotification> visibleRows)
        {
            var total = _items?.Count ?? 0;
            var muted = _items?.Count(x => x.IsMuted) ?? 0;
            var snoozed = _items?.Count(x => x.IsSnoozed) ?? 0;
            var visible = visibleRows?.Count ?? 0;
            _lblFooter.Text = total <= 0
                ? "Stateless feed • refresh for latest"
                : $"Showing {visible} of {total} • muted {muted} • snoozed {snoozed}";
        }

        private List<int> GetBulkTargetTicketIds(bool actionableOnly = true, int maxTickets = 25)
        {
            var ids = new List<int>();
            foreach (var notification in _currentView ?? new List<TicketNotification>())
            {
                var ticket = notification?.Ticket;
                if (ticket == null) continue;
                var ticketId = ticket.TicketId;
                if (ticketId <= 0) continue;
                if (notification.IsSnoozed) continue;
                if (actionableOnly && IsFinalStatus(ticket.Status)) continue;
                ids.Add(ticketId);
            }

            return ids.Distinct().Take(Math.Max(1, maxTickets)).ToList();
        }

        private async Task BulkAssignVisibleAsync()
        {
            if (_repo == null) return;
            if (!AppSession.IsLoggedIn)
            {
                MessageBox.Show("Please login first.", "Bulk Assign", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (AppSession.CurrentEmployeeId == null || AppSession.CurrentEmployeeId <= 0)
            {
                MessageBox.Show(
                    "Your user is not linked to an employee record.\n\nAsk the admin to map your account to an employee.",
                    "Bulk Assign",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var targets = GetBulkTargetTicketIds(actionableOnly: true);
            if (targets.Count == 0)
            {
                MessageBox.Show("No actionable tickets in the current view.", "Bulk Assign", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(
                    $"Assign {targets.Count} visible ticket(s) to you?",
                    "Bulk Assign",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            var success = 0;
            foreach (var ticketId in targets)
            {
                try
                {
                    await new CallEmailNotificationService(_repo).AssignTicketAndNotifyAsync(ticketId, AppSession.CurrentEmployeeId, AppSession.CurrentUserId);
                    success++;
                }
                catch
                {
                }
            }

            await RefreshAsync();
            if (AfterMutationAsync != null) await AfterMutationAsync();
            MessageBox.Show($"Assigned {success}/{targets.Count} ticket(s).", "Bulk Assign", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task BulkSetInProgressVisibleAsync()
        {
            if (_repo == null) return;
            if (!AppSession.IsLoggedIn)
            {
                MessageBox.Show("Please login first.", "Bulk Status Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var targets = GetBulkTargetTicketIds(actionableOnly: true);
            if (targets.Count == 0)
            {
                MessageBox.Show("No actionable tickets in the current view.", "Bulk Status Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(
                    $"Set {targets.Count} visible ticket(s) to In Progress?",
                    "Bulk Status Update",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            var success = 0;
            var skipped = new List<string>();
            var statusById = (_currentView ?? new List<TicketNotification>())
                .Where(x => x != null && x.Ticket != null)
                .GroupBy(x => x.Ticket.TicketId)
                .ToDictionary(x => x.Key, x => x.First().Ticket);
            foreach (var ticketId in targets)
            {
                try
                {
                    CallTicketListItem current = null;
                    statusById.TryGetValue(ticketId, out current);
                    var currentStatus = current?.Status ?? string.Empty;
                    // Same workflow guard as the main dropdown: no backward
                    // moves, no writes on final tickets.
                    if (!TicketWorkflow.IsStatusTransitionAllowed(currentStatus, "In Progress")
                        || IsFinalStatus(currentStatus))
                    {
                        skipped.Add((current?.TicketCode ?? ("#" + ticketId)) + " (" + currentStatus + ")");
                        continue;
                    }
                    await _repo.SetTicketStatusAsync(ticketId, "In Progress", AppSession.CurrentUserId, "Bulk update from notification drawer");
                    success++;
                }
                catch (Exception ex)
                {
                    CallTicketListItem failed = null;
                    statusById.TryGetValue(ticketId, out failed);
                    skipped.Add((failed?.TicketCode ?? ("#" + ticketId)) + ": " + ex.Message);
                }
            }

            await RefreshAsync();
            if (AfterMutationAsync != null) await AfterMutationAsync();
            var summary = $"Updated {success}/{targets.Count} ticket(s).";
            if (skipped.Count > 0)
                summary += "\n\nSkipped:\n- " + string.Join("\n- ", skipped.Take(10).ToArray())
                    + (skipped.Count > 10 ? $"\n...and {skipped.Count - 10} more." : string.Empty);
            MessageBox.Show(summary, "Bulk Status Update", MessageBoxButtons.OK,
                skipped.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private void BulkMarkVisibleAsRead()
        {
            var targets = GetBulkTargetTicketIds(actionableOnly: false, maxTickets: int.MaxValue);
            if (targets.Count == 0)
                return;

            foreach (var ticketId in targets)
                _readTicketIds.Add(ticketId);

            foreach (var item in _items)
                item.IsUnread = (item.Ticket?.TicketId ?? 0) > 0 && !_readTicketIds.Contains(item.Ticket.TicketId);

            RebuildChips();
            Render();
        }

        private void BulkSnoozeVisible(TimeSpan duration)
        {
            var targets = GetBulkTargetTicketIds(actionableOnly: true, maxTickets: int.MaxValue);
            if (targets.Count == 0)
            {
                MessageBox.Show("No actionable tickets in the current view.", "Bulk Snooze", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var snoozeUntil = DateTime.Now.Add(duration);
            foreach (var ticketId in targets)
                _snoozedUntilByTicketId[ticketId] = snoozeUntil;

            RebuildChips();
            Render();
        }

        private static bool IsFinalStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            return status.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                   || status.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                   || status.Equals("Closed", StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<TicketNotification> ApplySort(IEnumerable<TicketNotification> source)
        {
            source = source ?? Enumerable.Empty<TicketNotification>();
            switch (_sort)
            {
                case FeedSort.Newest:
                    return source
                        .OrderByDescending(x => x.IsPinned)
                        .ThenBy(x => x.IsMuted)
                        .ThenBy(x => x.IsSnoozed)
                        .ThenByDescending(x => x.Ticket?.UpdatedAt ?? x.Ticket?.CreatedAt ?? DateTime.MinValue)
                        .ThenByDescending(x => x.Score);
                case FeedSort.Oldest:
                    return source
                        .OrderByDescending(x => x.IsPinned)
                        .ThenBy(x => x.IsMuted)
                        .ThenBy(x => x.IsSnoozed)
                        .ThenBy(x => x.Ticket?.CreatedAt ?? DateTime.MaxValue)
                        .ThenByDescending(x => x.Score);
                case FeedSort.SlaNearest:
                    return source
                        .OrderByDescending(x => x.IsPinned)
                        .ThenBy(x => x.IsMuted)
                        .ThenBy(x => x.IsSnoozed)
                        .ThenBy(x => SlaSortKey(x))
                        .ThenByDescending(x => x.Score);
                case FeedSort.Risk:
                default:
                    return source
                        .OrderByDescending(x => x.IsPinned)
                        .ThenBy(x => x.IsMuted)
                        .ThenBy(x => x.IsSnoozed)
                        .ThenByDescending(x => x.Score)
                        .ThenByDescending(x => x.Ticket?.UpdatedAt ?? x.Ticket?.CreatedAt ?? DateTime.MinValue);
            }
        }

        private int SlaSortKey(TicketNotification notification)
        {
            var idleDays = notification?.Ticket != null ? GetIdleDays(notification.Ticket) : int.MaxValue / 4;
            if (_overdueDays <= 0)
                return idleDays;
            return _overdueDays - idleDays;
        }

        private void FitRowsToWidth()
        {
            if (_pnlList == null || _pnlList.IsDisposed)
                return;

            // Use viewport width, not DisplayRectangle width (virtual scroll extent),
            // otherwise rows can get stuck narrow and hide right-side badges/actions.
            var width = _pnlList.ClientSize.Width;
            if (width <= 0)
                return;
            if (_pnlList.VerticalScroll.Visible)
                width -= SystemInformation.VerticalScrollBarWidth;
            if (width < 460)
                width = 460;

            foreach (Control c in _pnlList.Controls)
            {
                // Helps if the list host ever changes layout container (e.g., FlowLayoutPanel),
                // and avoids the right action-strip being clipped off-screen.
                c.Width = width;
            }
        }

        private void MarkTicketReadState(int ticketId, bool markAsRead)
        {
            if (ticketId <= 0) return;

            if (markAsRead) _readTicketIds.Add(ticketId);
            else _readTicketIds.Remove(ticketId);

            foreach (var item in _items.Where(x => (x.Ticket?.TicketId ?? 0) == ticketId))
                item.IsUnread = !markAsRead;

            RebuildChips();
            Render();
        }

        private void MarkAllAsRead()
        {
            if (_items == null || _items.Count == 0)
                return;

            foreach (var item in _items)
            {
                var ticketId = item.Ticket?.TicketId ?? 0;
                if (ticketId > 0)
                    _readTicketIds.Add(ticketId);
                item.IsUnread = false;
            }

            RebuildChips();
            Render();
        }

        private async Task SafeOpenTicketAsync(int ticketId)
        {
            if (ticketId <= 0) return;
            if (OpenTicketAsync == null) return;

            MarkTicketReadState(ticketId, markAsRead: true);
            try { await OpenTicketAsync(ticketId); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Open Ticket Failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private async Task AssignToMeAsync(int ticketId)
        {
            if (ticketId <= 0) return;
            if (!AppSession.IsLoggedIn)
            {
                MessageBox.Show("Please login first.", "Assign to Me", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (AppSession.CurrentEmployeeId == null || AppSession.CurrentEmployeeId <= 0)
            {
                MessageBox.Show(
                    "Your user is not linked to an employee record.\n\nAsk the admin to map your account to an employee.",
                    "Assign to Me",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                await new CallEmailNotificationService(_repo).AssignTicketAndNotifyAsync(ticketId, AppSession.CurrentEmployeeId, AppSession.CurrentUserId);
                await RefreshAsync();
                if (AfterMutationAsync != null) await AfterMutationAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Assign Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task SetStatusAsync(int ticketId, string status, string note)
        {
            if (ticketId <= 0) return;
            if (!AppSession.IsLoggedIn)
            {
                MessageBox.Show("Please login first.", "Update Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Final states require resolution details: route through Mark As
            // instead of setting them directly from the drawer.
            if (string.Equals(status, "Solved", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "To mark a ticket as Solved/Resolved (Temporary)/Closed, open the ticket and use the 'Mark As...' button.\n\n" +
                    "This ensures resolution details are captured properly.",
                    "Update Status",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                await _repo.SetTicketStatusAsync(ticketId, status, AppSession.CurrentUserId, note);
                await RefreshAsync();
                if (AfterMutationAsync != null) await AfterMutationAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Update Status Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task AddNoteAsync(int ticketId)
        {
            if (ticketId <= 0) return;
            if (!AppSession.IsLoggedIn)
            {
                MessageBox.Show("Please login first.", "Add Note", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var note = PromptMultiline("Add Note", "Write a short note to add to this ticket:");
            if (string.IsNullOrWhiteSpace(note)) return;

            try
            {
                await _repo.AddTicketNoteAsync(ticketId, "Note", note.Trim(), AppSession.CurrentUserId);
                await RefreshAsync();
                if (AfterMutationAsync != null) await AfterMutationAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Add Note Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task EscalateAsync(int ticketId)
        {
            if (ticketId <= 0) return;
            if (!AppSession.IsLoggedIn)
            {
                MessageBox.Show("Please login first.", "Escalate", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var note = PromptMultiline("Escalate Ticket", "Escalation note (required):");
            if (string.IsNullOrWhiteSpace(note))
                return;

            try
            {
                await _repo.SetTicketStatusAsync(ticketId, "Escalated", AppSession.CurrentUserId, note.Trim());
                await RefreshAsync();
                if (AfterMutationAsync != null) await AfterMutationAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Escalate Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task EscalationOverrideAsync(int ticketId)
        {
            if (ticketId <= 0) return;
            try
            {
                // Overrides are meaningless on final tickets (the proc throws
                // 50023) - stop before opening the form.
                var currentStatus = string.Empty;
                try
                {
                    var match = (_items ?? new List<TicketNotification>())
                        .FirstOrDefault(x => x != null && x.Ticket != null && x.Ticket.TicketId == ticketId);
                    currentStatus = match?.Ticket?.Status ?? string.Empty;
                }
                catch
                {
                }
                if (currentStatus.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                    || currentStatus.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                    || currentStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        "Escalation overrides cannot be changed after a ticket is solved.",
                        "Escalation Override",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                if (!await _repo.EscalationOverridesEnabledAsync())
                {
                    MessageBox.Show(
                        "Escalation overrides are not installed in this database yet.\n\nRun the CallTicketEscalationOverride DB script to enable per-ticket escalation timing.",
                        "Escalation Override",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                var existing = await _repo.GetTicketEscalationOverrideAsync(ticketId);
                _escSettings = _escSettings ?? await _repo.GetEscalationSettingsAsync();

                using (var dlg = new EscalationOverrideForm(_escSettings, existing))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    var changedByUserId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                    await _repo.SetTicketEscalationOverrideAsync(
                        ticketId,
                        dlg.ClearRequested ? null : (int?)dlg.DaysToSupervisor,
                        dlg.ClearRequested ? null : (int?)dlg.DaysToManager,
                        dlg.Reason,
                        changedByUserId,
                        dlg.ClearRequested);
                }

                await RefreshAsync();
                if (AfterMutationAsync != null) await AfterMutationAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Escalation Override Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string PromptMultiline(string title, string prompt)
        {
            using (var f = new Form())
            {
                f.Text = title;
                f.StartPosition = FormStartPosition.CenterParent;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.ShowInTaskbar = false;
                f.ClientSize = new Size(520, 260);
                f.Font = new Font("Segoe UI", 9.5F);

                var lbl = new Label
                {
                    Text = prompt,
                    Dock = DockStyle.Top,
                    Height = 40,
                    Padding = new Padding(12, 10, 12, 0),
                    ForeColor = ModernUiHelper.ColorTextPrimary
                };

                var txt = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 9.5F)
                };

                var pnlButtons = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(12, 8, 12, 8) };

                var btnOk = ModernUiHelper.CreatePrimaryButton("OK", 90);
                btnOk.Dock = DockStyle.Right;
                btnOk.DialogResult = DialogResult.OK;

                var btnCancel = ModernUiHelper.CreateSecondaryButton("Cancel", 90);
                btnCancel.Dock = DockStyle.Right;
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Margin = new Padding(0, 0, 10, 0);

                pnlButtons.Controls.Add(btnOk);
                pnlButtons.Controls.Add(btnCancel);

                f.Controls.Add(txt);
                f.Controls.Add(pnlButtons);
                f.Controls.Add(lbl);

                f.AcceptButton = btnOk;
                f.CancelButton = btnCancel;

                return f.ShowDialog() == DialogResult.OK ? (txt.Text ?? string.Empty) : null;
            }
        }

        private static string BuildTicketTooltip(CallTicketListItem t, TicketNotification notification, int overdueDays)
        {
            if (t == null) return string.Empty;

            var updatedAt = t.UpdatedAt != default ? t.UpdatedAt : t.CreatedAt;
            var idleDays = GetIdleDays(t);
            var lastActivityUtc = GetLastActivityUtc(t);
            var lines = new List<string>();
            var code = string.IsNullOrWhiteSpace(t.TicketCode) ? $"Ticket #{t.TicketId}" : t.TicketCode.Trim();

            lines.Add(code);
            if (!string.IsNullOrWhiteSpace(t.Issue)) lines.Add(Norm(t.Issue));
            if (!string.IsNullOrWhiteSpace(t.Department)) lines.Add("Dept: " + t.Department.Trim());
            if (!string.IsNullOrWhiteSpace(t.Branch)) lines.Add("Branch: " + t.Branch.Trim());
            if (!string.IsNullOrWhiteSpace(t.Company)) lines.Add("Company: " + t.Company.Trim());
            lines.Add("Assigned: " + (!string.IsNullOrWhiteSpace(t.ResponsiblePerson) ? t.ResponsiblePerson.Trim() : "Unassigned"));

            if (!string.IsNullOrWhiteSpace(t.Status)) lines.Add("Status: " + t.Status.Trim());
            if (!string.IsNullOrWhiteSpace(t.Priority)) lines.Add("Priority: " + t.Priority.Trim());
            if (!string.IsNullOrWhiteSpace(notification?.Reason)) lines.Add("Reason: " + notification.Reason.Trim());
            if (notification != null && notification.IsPinned) lines.Add("Pinned: Yes");
            if (notification != null && notification.IsMuted) lines.Add("Muted: Yes");
            if (notification?.SnoozedUntil != null && notification.SnoozedUntil.Value > DateTime.Now)
                lines.Add("Snoozed until: " + notification.SnoozedUntil.Value.ToString("yyyy-MM-dd hh:mm tt"));

            lines.Add("SLA: " + BuildSlaTooltip(idleDays, overdueDays));
            lines.Add("Breach ETA: " + BuildBreachEtaTooltip(lastActivityUtc, overdueDays));
            if (idleDays > 0)
                lines.Add($"Idle: {idleDays} day(s)");

            if (updatedAt != default)
                lines.Add("Updated: " + updatedAt.ToString("yyyy-MM-dd hh:mm tt"));

            return string.Join("\r\n", lines.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        private static string Norm(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s;
        }

        private static string BuildSlaTooltip(int idleDays, int overdueDays)
        {
            if (overdueDays <= 0) return "Not configured";
            var remaining = overdueDays - idleDays;
            if (remaining < 0) return $"Overdue by {Math.Abs(remaining)} day(s)";
            if (remaining == 0) return "Due today";
            return $"Due in {remaining} day(s)";
        }

        private static string BuildBreachEtaTooltip(DateTime lastActivityUtc, int overdueDays)
        {
            if (overdueDays <= 0 || lastActivityUtc == default)
                return "Not available";

            var lastUtc = lastActivityUtc.Kind == DateTimeKind.Utc
                ? lastActivityUtc
                : DateTime.SpecifyKind(lastActivityUtc, DateTimeKind.Utc);
            var dueAtUtc = lastUtc.AddDays(overdueDays);
            if (dueAtUtc >= DateTime.UtcNow)
                return dueAtUtc.ToLocalTime().ToString("yyyy-MM-dd hh:mm tt");

            var lateBy = DateTime.UtcNow - dueAtUtc;
            if (lateBy.TotalHours < 1)
                return "Breached < 1h ago";
            if (lateBy.TotalDays < 1)
                return $"Breached {(int)Math.Round(lateBy.TotalHours)}h ago";
            return $"Breached {(int)Math.Round(lateBy.TotalDays)}d ago";
        }
    }
}
