using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public partial class TicketManagementControl
    {
        private static Color GetStatusColor(string status, bool isOverdue)
        {
            if (isOverdue) return Color.FromArgb(231, 76, 60);

            var s = NormalizeLegacyTicketStatus(status);
            if (s.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(230, 126, 34);
            if (s.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(155, 89, 182);
            if (s.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(52, 152, 219);
            if (s.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(99, 102, 241);
            if (s.Equals("Solved", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(46, 204, 113);
            if (s.Equals("Closed", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(46, 204, 113);
            if (s.Equals("Reopened", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(231, 76, 60);

            return Color.FromArgb(44, 62, 80);
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

        private static Color GetPriorityColor(string priority)
        {
            var p = (priority ?? string.Empty).Trim();
            if (p.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(231, 76, 60);
            if (p.Equals("High", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(230, 126, 34);
            if (p.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(52, 152, 219);
            if (p.Equals("Low", StringComparison.OrdinalIgnoreCase)) return Color.FromArgb(127, 140, 141);
            return Color.FromArgb(44, 62, 80);
        }

        private ToolTip EnsureToolTip()
        {
            if (_toolTip != null) return _toolTip;
            _toolTip = new ToolTip
            {
                AutoPopDelay = 20000,
                InitialDelay = 500,
                ReshowDelay = 200,
                ShowAlways = true
            };
            return _toolTip;
        }

        private static string BuildEscalationShortLabel(string full)
        {
            var s = (full ?? string.Empty).Trim();
            if (s.Length == 0) return "-";

            var hasOverride = s.IndexOf("override", StringComparison.OrdinalIgnoreCase) >= 0;

            if (s.StartsWith("Escalated:", StringComparison.OrdinalIgnoreCase))
            {
                if (s.IndexOf("Manager", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var idx = s.IndexOf("•", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        var after = s.Substring(idx + 1).Trim();
                        var dIdx = after.IndexOf("d", StringComparison.OrdinalIgnoreCase);
                        if (dIdx > 0 && int.TryParse(after.Substring(0, dIdx).Trim(), out var days))
                            return $"Mgr +{days}d" + (hasOverride ? " (ovr)" : string.Empty);
                    }
                    return "Mgr" + (hasOverride ? " (ovr)" : string.Empty);
                }

                if (s.IndexOf("Supervisor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var inIdx = s.IndexOf("manager in", StringComparison.OrdinalIgnoreCase);
                    if (inIdx >= 0)
                    {
                        var tail = s.Substring(inIdx + "manager in".Length).Trim();
                        var dIdx = tail.IndexOf("d", StringComparison.OrdinalIgnoreCase);
                        if (dIdx > 0 && int.TryParse(tail.Substring(0, dIdx).Trim(), out var days))
                            return $"Sup, mgr {days}d" + (hasOverride ? " (ovr)" : string.Empty);
                    }
                    return "Sup" + (hasOverride ? " (ovr)" : string.Empty);
                }
            }

            var supIdx = s.IndexOf("sup in", StringComparison.OrdinalIgnoreCase);
            var mgrIdx = s.IndexOf("mgr in", StringComparison.OrdinalIgnoreCase);
            if (supIdx >= 0 && mgrIdx >= 0)
            {
                int? supDays = null;
                int? mgrDays = null;

                var supTail = s.Substring(supIdx + "sup in".Length).Trim();
                var supD = supTail.IndexOf("d", StringComparison.OrdinalIgnoreCase);
                if (supD > 0 && int.TryParse(supTail.Substring(0, supD).Trim(), out var sd)) supDays = sd;

                var mgrTail = s.Substring(mgrIdx + "mgr in".Length).Trim();
                var mgrD = mgrTail.IndexOf("d", StringComparison.OrdinalIgnoreCase);
                if (mgrD > 0 && int.TryParse(mgrTail.Substring(0, mgrD).Trim(), out var md)) mgrDays = md;

                if (supDays.HasValue || mgrDays.HasValue)
                {
                    var left = supDays.HasValue ? $"Sup {supDays.Value}d" : "Sup -";
                    var right = mgrDays.HasValue ? $"Mgr {mgrDays.Value}d" : "Mgr -";
                    return $"{left}, {right}" + (hasOverride ? " (ovr)" : string.Empty);
                }
            }

            if (s.Length > 24) s = s.Substring(0, 24).TrimEnd() + "…";
            return s;
        }

        private void ConfigureStatusDropdownForTicket(string currentStatus)
        {
            if (this.cboStatus == null || _suppressStatusDropdownUpdate)
                return;

            try
            {
                _suppressStatusDropdownUpdate = true;

                var cur = NormalizeLegacyTicketStatus(currentStatus);
                var curRank = TicketWorkflow.GetStatusRank(cur);

                this.cboStatus.BeginUpdate();
                this.cboStatus.Items.Clear();

                if (curRank < 0)
                {
                    this.cboStatus.Items.AddRange(WorkflowStatuses.Cast<object>().ToArray());
                }
                else
                {
                    foreach (var s in WorkflowStatuses)
                    {
                        if (s.Equals("Reopened", StringComparison.OrdinalIgnoreCase))
                        {
                            if (cur.Equals("Reopened", StringComparison.OrdinalIgnoreCase))
                                this.cboStatus.Items.Add(s);
                            continue;
                        }

                        if (TicketWorkflow.GetStatusRank(s) >= curRank)
                            this.cboStatus.Items.Add(s);
                    }
                }

                if (!string.IsNullOrWhiteSpace(cur) && !this.cboStatus.Items.Contains(cur))
                    this.cboStatus.Items.Insert(0, cur);

                if (!string.IsNullOrWhiteSpace(cur))
                    this.cboStatus.SelectedItem = cur;
            }
            catch
            {
            }
            finally
            {
                try { this.cboStatus.EndUpdate(); } catch { }
                _suppressStatusDropdownUpdate = false;
            }
        }

        private async Task LoadSelectedTicketDetailsAsync(int ticketId, int version)
        {
            if (version != _selectionVersion) return;

            try
            {
                var notes = await _callRepo.GetTicketNotesAsync(ticketId);
                if (version != _selectionVersion) return;
                var history = await _callRepo.GetTicketHistoryAsync(ticketId);
                if (version != _selectionVersion) return;
                var escalation = await _callRepo.GetTicketEscalationOverrideAsync(ticketId);
                if (version != _selectionVersion) return;
                var lastEmail = await _callRepo.GetLastEmailLogForTicketAsync(ticketId);
                if (version != _selectionVersion) return;

                this._notesCacheSelected = (notes ?? new List<CallTicketNoteItem>()).ToList();
                this._historyCacheSelected = (history ?? new List<CallTicketHistoryItem>()).ToList();
                RenderSolutionNotes();
                ApplyHistoryFilterAndRender();

                if (this._selectedTicket != null && this._selectedTicket.TicketId == ticketId)
                {
                    var full = BuildEscalationLabel(this._selectedTicket, _escalationSettings, escalation);
                    this.lblSummaryEscalation.Text = BuildEscalationShortLabel(full);
                    this.lblSummaryEscalation.AutoEllipsis = true;
                    EnsureToolTip().SetToolTip(this.lblSummaryEscalation, full);

                    this.lblNextEscalation.Text = full;
                    EnsureToolTip().SetToolTip(this.lblNextEscalation, full);

                    UpdateProfileLastEmail(lastEmail);
                }
            }
            catch (Exception ex)
            {
                ShowTicketDetailLoadWarning(ex, ticketId);
            }
        }

        private void UpdateProfileLastEmail(CallEmailLogItem lastEmail)
        {
            if (this.lblLastEmail == null)
                return;

            try
            {
                this.lblLastEmail.ForeColor = Color.FromArgb(44, 62, 80);
                var tt = EnsureToolTip();
                if (lastEmail == null)
                {
                    this.lblLastEmail.Text = "None";
                    tt.SetToolTip(this.lblLastEmail, "No email log entries for this ticket.");
                    return;
                }

                var when = ToLocalString(lastEmail.DateSent, "g");
                var type = string.IsNullOrWhiteSpace(lastEmail.EmailType) ? "-" : lastEmail.EmailType.Trim();
                var status = string.IsNullOrWhiteSpace(lastEmail.Status) ? "-" : lastEmail.Status.Trim();
                var recipient = string.IsNullOrWhiteSpace(lastEmail.Recipient) ? "-" : lastEmail.Recipient.Trim();

                this.lblLastEmail.Text = $"{type} • {when}";

                var tip =
                    $"Type: {type}\n" +
                    $"To: {recipient}\n" +
                    $"Status: {status}\n" +
                    $"Sent: {when}";

                if (!string.IsNullOrWhiteSpace(lastEmail.ErrorMessage))
                    tip += $"\nError: {lastEmail.ErrorMessage.Trim()}";

                tt.SetToolTip(this.lblLastEmail, tip);
            }
            catch
            {
            }
        }

        private static string BuildEscalationLabel(CallTicketListItem t, CallEscalationSettingsItem s, CallTicketEscalationOverrideItem o)
        {
            if (t == null) return "N/A";
            var ageDays = Math.Max(0, t.TicketAgeDays);

            var supDays = o != null ? o.DaysToSupervisor : (s?.DaysToSupervisor ?? 2);
            var mgrDays = o != null ? o.DaysToManager : (s?.DaysToManager ?? 3);

            if (supDays < 1) supDays = 1;
            if (mgrDays < supDays) mgrDays = supDays;

            if (ageDays >= mgrDays)
            {
                var overBy = ageDays - mgrDays;
                return overBy > 0
                    ? $"Escalated: Manager • {overBy}d past threshold"
                    : "Escalated: Manager";
            }

            if (ageDays >= supDays)
            {
                var mgrIn = mgrDays - ageDays;
                return mgrIn > 0
                    ? $"Escalated: Supervisor • manager in {mgrIn}d"
                    : "Escalated: Supervisor";
            }

            var supInDays = supDays - ageDays;
            var mgrInDays = mgrDays - ageDays;
            var isNear = supInDays <= 1;
            var prefix = isNear ? "Warning" : "Next";

            var suffix = o != null ? " (override)" : string.Empty;
            return $"{prefix}: sup in {supInDays}d, mgr in {mgrInDays}d{suffix}";
        }

        private static string BuildSlaLabel(CallTicketListItem t)
        {
            if (t == null)
                return "SLA: -";

            var target = GetResolutionSlaTarget(t.Priority, t.IssueType);
            var targetHours = target.TargetHours;
            if (targetHours <= 0)
                return "SLA: -";

            var createdUtc = DateTime.SpecifyKind(t.CreatedAt, DateTimeKind.Utc);
            var dueUtc = createdUtc.AddHours(targetHours);
            var nowUtc = DateTime.UtcNow;
            var remaining = dueUtc - nowUtc;

            var isFinal = string.Equals((t.Status ?? string.Empty).Trim(), "Solved", StringComparison.OrdinalIgnoreCase)
                          || string.Equals((t.Status ?? string.Empty).Trim(), "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                          || string.Equals((t.Status ?? string.Empty).Trim(), "Closed", StringComparison.OrdinalIgnoreCase);

            var dueLocal = dueUtc.ToLocalTime();
            if (isFinal)
                return $"SLA: (closed) • due {dueLocal:yyyy-MM-dd HH:mm}";

            if (remaining <= TimeSpan.Zero)
            {
                var overdue = remaining.Duration();
                return $"SLA: Breached by {FormatShortDuration(overdue)} • due {dueLocal:yyyy-MM-dd HH:mm} ({target.Name})";
            }

            var warn = TimeSpan.FromHours(Math.Max(1, target.WarningHours));
            var isAtRisk = remaining <= warn;
            var label = isAtRisk ? "At risk in" : "Due in";
            return $"SLA: {label} {FormatShortDuration(remaining)} • due {dueLocal:yyyy-MM-dd HH:mm} ({target.Name})";
        }

        private static (int TargetHours, int WarningHours, string Name) GetResolutionSlaTarget(string priority, string issueType)
        {
            var p = (priority ?? string.Empty).Trim();
            if (p.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return (24, 6, "Critical 24h");
            if (p.Equals("High", StringComparison.OrdinalIgnoreCase)) return (48, 12, "High 48h");
            if (p.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return (72, 24, "Medium 72h");
            if (p.Equals("Low", StringComparison.OrdinalIgnoreCase)) return (120, 24, "Low 120h");

            return (72, 24, "Default 72h");
        }

        private static string FormatShortDuration(TimeSpan value)
        {
            if (value < TimeSpan.Zero)
                value = value.Duration();

            var days = value.Days;
            var hours = value.Hours;
            var minutes = value.Minutes;

            if (days > 0)
                return hours > 0 ? $"{days}d {hours}h" : $"{days}d";
            if (hours > 0)
                return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
            return $"{Math.Max(1, (int)Math.Round(value.TotalMinutes))}m";
        }
    }
}
