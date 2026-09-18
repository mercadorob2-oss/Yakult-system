using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public partial class TicketManagementControl
    {
        private void UpdateTicketActionStates()
        {
            var hasTicket = this._selectedTicket != null;
            var isFinal = hasTicket && IsFinalStatus(this._selectedTicket.Status);
            var isResolvedTemp = hasTicket && string.Equals(this._selectedTicket.Status, "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase);

            if (this.cboStatus != null) this.cboStatus.Enabled = hasTicket && !isFinal;
            if (this.cboPriority != null) this.cboPriority.Enabled = hasTicket && !isFinal;
            if (this.btnUpdateNotes != null) this.btnUpdateNotes.Enabled = hasTicket && !isFinal;
            if (this.btnMarkAs != null) this.btnMarkAs.Enabled = hasTicket && !isFinal;
            if (this.btnEscalationOverride != null) this.btnEscalationOverride.Enabled = hasTicket && !isFinal && _escalationOverridesInstalled;
            if (this.btnReturnTempItem != null)
            {
                this.btnReturnTempItem.Visible = isResolvedTemp;
                this.btnReturnTempItem.Enabled = isResolvedTemp;
            }

            if (this.btnSyncSet != null) this.btnSyncSet.Enabled = hasTicket;
            if (this.cboReassignTo != null) this.cboReassignTo.Enabled = hasTicket && !isFinal;
            if (this.btnReassignTicket != null) this.btnReassignTicket.Enabled = hasTicket && !isFinal;
            if (this.btnAssignToMe != null) this.btnAssignToMe.Enabled = hasTicket && !isFinal;
            if (this.btnReopen != null)
            {
                this.btnReopen.Visible = hasTicket && isFinal;
                this.btnReopen.Enabled = hasTicket && isFinal;
            }
        }

        private async void btnUpdateNotes_Click(object sender, EventArgs e)
        {
            try
            {
                var selected = GetSelectedTicketsForUpdate();
                if (selected.Count == 0)
                    return;

                var changedByUserId = GetCurrentChangedByUserIdOrWarn("Update");
                if (changedByUserId == null)
                    return;

                var status = this.cboStatus?.SelectedItem?.ToString();
                var noteText = (this.txtAttemptedSolutions?.Text ?? string.Empty).Trim();
                var priority = this.cboPriority?.SelectedItem?.ToString();
                var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
                var normalizedPriority = string.IsNullOrWhiteSpace(priority) ? null : priority.Trim();
                var hasNote = !string.IsNullOrWhiteSpace(noteText);
                var isBulk = selected.Count > 1;

                if (!ValidateDirectStatusSelection(selected, normalizedStatus, noteText))
                    return;

                if (isBulk)
                {
                    if (!ConfirmBulkTicketUpdate(selected, normalizedStatus, normalizedPriority, hasNote, noteText))
                        return;
                }
                else
                {
                    if (!ConfirmSingleTicketUpdate(selected[0], normalizedStatus, normalizedPriority, hasNote, noteText))
                        return;
                }

                await ApplyTicketUpdatesAsync(selected, normalizedStatus, normalizedPriority, noteText, changedByUserId);
                await SafeRefreshTicketGridsAsync();
            }
            catch (Exception ex)
            {
                ShowFriendlyError("Update Failed", "We couldn't update the ticket right now. Please try again.", ex, "UpdateTicketNotes");
            }
        }

        private async void btnReopen_Click(object sender, EventArgs e)
        {
            if (this._selectedTicket == null) return;

            try
            {
                var changedByUserId = GetCurrentChangedByUserIdOrWarn("Reopen");
                if (changedByUserId == null)
                    return;

                var noteText = (this.txtAttemptedSolutions?.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(noteText))
                {
                    MessageBox.Show(
                        "Please enter a short note (in the Solutions tab) explaining why you are reopening this ticket.",
                        "Reopen Note Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                var ticketId = this._selectedTicket.TicketId;
                var oldStatus = this._selectedTicket.Status;
                await _callRepo.SetTicketStatusAsync(ticketId, "Reopened", changedByUserId, noteText);
                RunNotification(async () => await _callEmail.NotifyStatusChangeAsync(ticketId, oldStatus, "Reopened", noteText, changedByUserId));
                await SafeRefreshTicketGridsAsync();
                TrySelectTicketInPendingGrid(ticketId);
            }
            catch (Exception ex)
            {
                ShowFriendlyError("Reopen Failed", "We couldn't reopen the ticket right now. Please try again.", ex, "ReopenTicket");
            }
        }

        private List<CallTicketListItem> GetSelectedTicketsForUpdate()
        {
            var selected = GetSelectedPendingTickets();
            if (selected.Count == 0 && this._selectedTicket != null)
                selected.Add(this._selectedTicket);

            return selected;
        }

        private static string BuildTicketNotePreview(string noteText)
        {
            if (string.IsNullOrWhiteSpace(noteText))
                return "(None)";

            return noteText.Length > 220 ? noteText.Substring(0, 220) + "..." : noteText;
        }

        private int? GetCurrentChangedByUserIdOrWarn(string actionTitle)
        {
            var changedByUserId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
            if (changedByUserId != null)
                return changedByUserId;

            MessageBox.Show("Must be logged in.", actionTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        private bool ValidateDirectStatusSelection(List<CallTicketListItem> selected, string normalizedStatus, string noteText)
        {
            if (string.IsNullOrWhiteSpace(normalizedStatus))
                return true;

            var willChangeStatus = selected.Any(t =>
                t != null && !string.Equals((t.Status ?? string.Empty).Trim(), normalizedStatus, StringComparison.OrdinalIgnoreCase));

            if (normalizedStatus.Equals("Solved", StringComparison.OrdinalIgnoreCase) ||
                normalizedStatus.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "To mark a ticket as Solved/Resolved (Temporary), use the 'Mark As...' button.\n\n" +
                    "This ensures resolution details are captured properly.",
                    "Status Change",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            if (normalizedStatus.Equals("Reopened", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "To reopen a ticket, use the 'Reopen' button.\n\n" +
                    "This system blocks setting 'Reopened' from the status dropdown.",
                    "Status Change",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            if (willChangeStatus
                && normalizedStatus.Equals("Escalated", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(noteText))
            {
                MessageBox.Show(
                    "Please enter a short note (in the Solutions tab) before setting status to Escalated.\n\n" +
                    "Example: reason for escalation / what's needed / who to contact.",
                    "Escalation Note Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private bool ConfirmBulkTicketUpdate(
            List<CallTicketListItem> selected,
            string normalizedStatus,
            string normalizedPriority,
            bool hasNote,
            string noteText)
        {
            var willChangeStatusCount = !string.IsNullOrWhiteSpace(normalizedStatus)
                ? selected.Count(t => t != null && !string.Equals((t.Status ?? string.Empty).Trim(), normalizedStatus, StringComparison.OrdinalIgnoreCase))
                : 0;
            var willChangePriorityCount = !string.IsNullOrWhiteSpace(normalizedPriority)
                ? selected.Count(t => t != null && !string.Equals((t.Priority ?? string.Empty).Trim(), normalizedPriority, StringComparison.OrdinalIgnoreCase))
                : 0;

            var msg =
                $"Apply updates to {selected.Count} ticket(s)?\n\n" +
                $"Status:   {(string.IsNullOrWhiteSpace(normalizedStatus) ? "(no change)" : $"{normalizedStatus}  (changes {willChangeStatusCount})")}\n" +
                $"Priority: {(string.IsNullOrWhiteSpace(normalizedPriority) ? "(no change)" : $"{normalizedPriority}  (changes {willChangePriorityCount})")}\n" +
                $"Add note: {(hasNote ? "Yes (same note for all)" : "No")}\n\n" +
                $"Note preview:\n{BuildTicketNotePreview(noteText)}\n\n" +
                "Continue?";

            return MessageBox.Show(
                       msg,
                       "Confirm Update",
                       MessageBoxButtons.YesNo,
                       MessageBoxIcon.Question,
                       MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private bool ConfirmSingleTicketUpdate(
            CallTicketListItem ticket,
            string normalizedStatus,
            string normalizedPriority,
            bool hasNote,
            string noteText)
        {
            var currentStatus = (ticket?.Status ?? string.Empty).Trim();
            var currentPriority = (ticket?.Priority ?? string.Empty).Trim();

            var willChangeStatus = !string.IsNullOrWhiteSpace(normalizedStatus)
                && !string.Equals(currentStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase);
            var willChangePriority = !string.IsNullOrWhiteSpace(normalizedPriority)
                && !string.Equals(currentPriority, normalizedPriority, StringComparison.OrdinalIgnoreCase);

            if (!willChangeStatus && !willChangePriority && !hasNote)
            {
                MessageBox.Show("Nothing to update.", "Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            var statusLine = willChangeStatus ? $"Status:   {currentStatus} -> {normalizedStatus}\n" : string.Empty;
            var priorityLine = willChangePriority ? $"Priority: {currentPriority} -> {normalizedPriority}\n" : string.Empty;

            var msg =
                $"Update {GetTicketLabel(ticket)}?\n\n" +
                statusLine +
                priorityLine +
                $"Add note: {(hasNote ? "Yes" : "No")}\n\n" +
                $"Note preview:\n{BuildTicketNotePreview(noteText)}\n\n" +
                "Continue?";

            return MessageBox.Show(
                       msg,
                       "Confirm Update",
                       MessageBoxButtons.YesNo,
                       MessageBoxIcon.Question,
                       MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private async Task ApplyTicketUpdatesAsync(
            List<CallTicketListItem> selected,
            string normalizedStatus,
            string normalizedPriority,
            string noteText,
            int? changedByUserId)
        {
            foreach (var ticket in selected.Where(t => t != null))
            {
                if (IsFinalStatus(ticket.Status))
                    continue;

                var ticketId = ticket.TicketId;

                if (!string.IsNullOrWhiteSpace(normalizedStatus)
                    && !string.Equals(normalizedStatus, ticket.Status, StringComparison.OrdinalIgnoreCase))
                {
                    if (!TicketWorkflow.IsStatusTransitionAllowed(ticket.Status, normalizedStatus))
                    {
                        MessageBox.Show(
                            $"Status change blocked.\n\n" +
                            $"Current: {ticket.Status}\n" +
                            $"Requested: {normalizedStatus}\n\n" +
                            "This system prevents moving a ticket backward in the workflow (e.g., In Progress → Pending).",
                            "Invalid Status Change",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        continue;
                    }

                    var oldStatus = ticket.Status;
                    await _callRepo.SetTicketStatusAsync(ticketId, normalizedStatus, changedByUserId, noteText);
                    RunNotification(async () => await _callEmail.NotifyStatusChangeAsync(ticketId, oldStatus, normalizedStatus, noteText, changedByUserId));
                }

                if (!string.IsNullOrWhiteSpace(normalizedPriority)
                    && !string.Equals(normalizedPriority, ticket.Priority, StringComparison.OrdinalIgnoreCase))
                {
                    await _callRepo.SetTicketPriorityAsync(ticketId, normalizedPriority, changedByUserId);
                }

                if (!string.IsNullOrWhiteSpace(noteText))
                    await _callRepo.AddTicketNoteAsync(ticketId, "Solution", noteText, changedByUserId);
            }
        }
    }
}
