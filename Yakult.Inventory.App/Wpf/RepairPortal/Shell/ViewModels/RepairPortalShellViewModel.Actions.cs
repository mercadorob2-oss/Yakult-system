using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Shell.ViewModels
{
    /// <summary>Load/Refresh, attendance actions, and reconciliation of the shared ticket
    /// collection after a New Ticket dialog or Detail window makes a change.</summary>
    public sealed partial class RepairPortalShellViewModel
    {
        private async Task InitializeAsync()
        {
            await LoadLookupsAsync();
            await RefreshAsync();

            if (AppSession.CurrentEmployeeId.HasValue && AppSession.CurrentEmployeeId.Value > 0)
            {
                try
                {
                    Attendance = await _repository.GetTodayStatusAsync(AppSession.CurrentEmployeeId.Value);
                }
                catch
                {
                    Attendance = new RepairTechnicianAttendanceStatus { EmployeeId = AppSession.CurrentEmployeeId.Value };
                }

                try
                {
                    LastTimeOut = await _repository.GetLastTimeOutAsync(AppSession.CurrentEmployeeId.Value);
                }
                catch
                {
                    LastTimeOut = null;
                }
            }

            IsAttendanceLoaded = true;
            RaiseAttendanceDerivedProps();
            UpdateElapsedDisplay();
            if (IsTimedIn) _elapsedTimer.Start();
        }

        public async Task RefreshAsync()
        {
            await LoadTicketsAsync();
            await LoadSummaryAsync();
        }

        /// <summary>Guards against out-of-order results: Company/Branch/Department/Priority/Quick
        /// Filter changes each call LoadTicketsAsync immediately (no debounce), and the search box
        /// calls it 300ms after typing stops — so two calls can easily be in flight at once. Without
        /// this token, whichever SQL round-trip happens to finish LAST wins, even if it was started
        /// FIRST with now-stale criteria — visibly "losing" Company/Branch/Department/Employee data
        /// (or reverting a filter) whenever the earlier, stale request happened to resolve later.</summary>
        private int _ticketLoadToken;

        private async Task LoadTicketsAsync()
        {
            if (IsDisposed) return;

            var token = ++_ticketLoadToken;
            SetBusy(true);
            try
            {
                var criteria = BuildCriteria();
                var rows = await _repository.GetTicketsAsync(criteria);

                if (token != _ticketLoadToken) return;

                Tickets.Clear();
                foreach (var row in rows)
                    Tickets.Add(row);

                // A collection that drops to exactly 0 items and is then repopulated can leave
                // the DataGrid's virtualized panel (VirtualizingPanel.VirtualizationMode=Recycling
                // in ListDataGridChrome) stuck showing nothing, even though Tickets genuinely has
                // rows again — a known WPF virtualization quirk after a 0-item transition. Force a
                // view refresh so Gallery/Table/Kanban always repaint correctly.
                CollectionViewSource.GetDefaultView(Tickets)?.Refresh();
            }
            catch (Exception ex)
            {
                if (token == _ticketLoadToken)
                    RequestError?.Invoke("Load Failed", "Failed to load repair tickets: " + ex.Message);
            }
            finally
            {
                if (token == _ticketLoadToken) SetBusy(false);
            }
        }

        private async Task LoadSummaryAsync()
        {
            try
            {
                Summary = await _repository.GetSummaryCountsAsync();
            }
            catch
            {
                Summary = new RepairPortalSummaryCounts();
            }
        }

        private async Task TimeInAsync()
        {
            if (!AppSession.CurrentEmployeeId.HasValue || AppSession.CurrentEmployeeId.Value <= 0)
            {
                RequestInfo?.Invoke("Time In", "Your account is not linked to an employee record, so attendance cannot be tracked.");
                return;
            }

            // Wait for the Shell's initial load (including its own first attendance query) to
            // finish first — otherwise, if that still-in-flight query resolves after this call,
            // it would overwrite the Attendance we're about to set with its own stale result.
            if (_initializeTask != null) await _initializeTask;

            try
            {
                Attendance = await _repository.TimeInAsync(AppSession.CurrentEmployeeId.Value);
                RaiseAttendanceDerivedProps();
                UpdateElapsedDisplay();
                if (IsTimedIn) _elapsedTimer.Start();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Time In Failed", ex.Message);
            }
        }

        private async Task TimeOutAsync()
        {
            if (!AppSession.CurrentEmployeeId.HasValue || AppSession.CurrentEmployeeId.Value <= 0)
                return;

            if (_initializeTask != null) await _initializeTask;

            var confirmed = RequestConfirm?.Invoke(
                "Time Out",
                $"Time out now, at {DateTime.Now:MMM d, yyyy h:mm tt}?") ?? false;
            if (!confirmed) return;

            try
            {
                Attendance = await _repository.TimeOutAsync(AppSession.CurrentEmployeeId.Value);
                LastTimeOut = Attendance?.TimeOut ?? LastTimeOut;
                RaiseAttendanceDerivedProps();
                _elapsedTimer.Stop();
                UpdateElapsedDisplay();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Time Out Failed", ex.Message);
            }
        }

        /// <summary>Permanently deletes a ticket directly from its Gallery/Table/Kanban card quick
        /// action — for correcting an accidental intake mistake without needing to open Detail
        /// first. Confirms since this cannot be undone.</summary>
        private async Task DeleteTicketAsync(RepairTicketListItem ticket)
        {
            if (ticket == null) return;

            var confirmed = RequestConfirm?.Invoke(
                "Delete Ticket",
                $"Permanently delete this repair ticket for \"{ticket.ItemName}\"? This removes all observations, parts, notes, evidence, and history logged on it. This cannot be undone.") ?? false;

            if (!confirmed) return;

            try
            {
                await _repository.DeleteTicketAsync(ticket.RepairTicketId);
                Tickets.Remove(ticket);
                _ = LoadSummaryAsync();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Delete Failed", ex.Message);
            }
        }

        /// <summary>Bulk delete for Table View's multi-select checkboxes — reuses the exact same
        /// per-ticket delete path as DeleteTicketAsync (single confirmation dialog for the whole
        /// batch instead of one per ticket, since looping DeleteTicketAsync directly would prompt
        /// N times for an N-ticket delete).</summary>
        public async Task DeleteTicketsAsync(System.Collections.Generic.IEnumerable<RepairTicketListItem> tickets)
        {
            var list = tickets?.Where(t => t != null).ToList() ?? new System.Collections.Generic.List<RepairTicketListItem>();
            if (list.Count == 0) return;

            if (list.Count == 1)
            {
                await DeleteTicketAsync(list[0]);
                return;
            }

            var confirmed = RequestConfirm?.Invoke(
                "Delete Tickets",
                $"Permanently delete {list.Count} repair tickets? This removes all observations, parts, notes, evidence, and history logged on each. This cannot be undone.") ?? false;

            if (!confirmed) return;

            var errors = new System.Collections.Generic.List<string>();
            foreach (var ticket in list)
            {
                try
                {
                    await _repository.DeleteTicketAsync(ticket.RepairTicketId);
                    Tickets.Remove(ticket);
                }
                catch (Exception ex)
                {
                    errors.Add($"{ticket.ItemName}: {ex.Message}");
                }
            }

            _ = LoadSummaryAsync();

            if (errors.Count > 0)
                RequestError?.Invoke("Some Deletes Failed", string.Join("\n", errors));
        }

        /// <summary>Called by the View after NewRepairTicketDialog returns a created ticket —
        /// prepends it to the shared collection without a full reload.</summary>
        public void PrependNewTicket(RepairTicketListItem ticket)
        {
            if (ticket == null) return;
            Tickets.Insert(0, ticket);
            _ = LoadSummaryAsync();
        }

        /// <summary>Opens a ticket's Detail window given only its id — used by the header
        /// notification bell, which carries a RepairTicketId (Notification.ReferenceId) rather than
        /// a RepairTicketListItem. Reuses the existing RequestOpenDetail path: prefers the row
        /// already in the shared collection, otherwise builds a minimal stub (the Detail window
        /// only needs RepairTicketId) after confirming the ticket still exists.</summary>
        public async Task OpenTicketByIdAsync(int repairTicketId)
        {
            if (repairTicketId <= 0) return;

            var existing = Tickets.FirstOrDefault(t => t.RepairTicketId == repairTicketId);
            if (existing != null)
            {
                RequestOpenDetail?.Invoke(existing);
                return;
            }

            try
            {
                var detail = await _repository.GetTicketDetailAsync(repairTicketId);
                if (detail == null)
                {
                    RequestInfo?.Invoke("Repair Ticket", "That repair ticket no longer exists.");
                    return;
                }

                RequestOpenDetail?.Invoke(new RepairTicketListItem
                {
                    RepairTicketId = detail.RepairTicketId,
                    TicketCode     = detail.TicketCode,
                    ItemId         = detail.ItemId,
                    ItemName       = detail.ItemNameSnapshot,
                    Status         = detail.Status,
                });
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Open Failed", "Failed to open repair ticket: " + ex.Message);
            }
        }

        /// <summary>Called by the View after the Detail window closes — refreshes just that one
        /// row in the shared collection instead of a full reload.</summary>
        public async Task RefreshSingleTicketAsync(int repairTicketId)
        {
            try
            {
                var criteria = new RepairTicketFilterCriteria();
                var detail = await _repository.GetTicketDetailAsync(repairTicketId);
                var existing = Tickets.FirstOrDefault(t => t.RepairTicketId == repairTicketId);

                if (detail == null)
                {
                    // Ticket no longer exists (deleted from the Detail window) — drop it from the
                    // shared collection so Gallery/Table/Kanban stop showing it.
                    if (existing != null) Tickets.Remove(existing);
                    _ = LoadSummaryAsync();
                    return;
                }
                var updated = new RepairTicketListItem
                {
                    RepairTicketId = detail.RepairTicketId,
                    TicketCode = detail.TicketCode,
                    ItemId = detail.ItemId,
                    ItemName = detail.ItemNameSnapshot,
                    ItemSerial = detail.ItemSerialSnapshot,
                    SetCode = detail.SetCode,
                    // Same Requested-By-first priority as GetTicketsAsync/MapListItem — otherwise
                    // this single-row refresh (fired every time the Detail window closes) would
                    // overwrite the list's Company/Branch/Department/Employee with the item's own
                    // (often unset) org, wiping out what the list query originally showed.
                    CompanyName = detail.RequestedByComName ?? detail.CompanyName,
                    BranchName = detail.RequestedByBranchName ?? detail.BranchName,
                    DeptName = detail.RequestedByDeptName ?? detail.DeptName,
                    Problem = detail.Problem,
                    Priority = detail.Priority,
                    Status = detail.Status,
                    SubmittedByName = detail.SubmittedByName,
                    AssignedTechName = detail.AssignedTechName,
                    DateReceived = detail.DateReceived,
                    UpdatedAt = detail.UpdatedAt,
                    ThumbnailImageBytes = existing?.ThumbnailImageBytes,
                    ThumbnailMimeType = existing?.ThumbnailMimeType,
                    Category = detail.Category,
                    DaysInCurrentStatus = detail.DaysInCurrentStatus,
                    RequestedByType = detail.RequestedByType,
                    RequestedByEmpName = detail.RequestedByEmpName,
                    RequestedByDeptName = detail.RequestedByDeptName
                };

                var firstImage = detail.Attachments.FirstOrDefault(a => string.Equals(a.AttachmentType, "Image", StringComparison.OrdinalIgnoreCase));
                if (firstImage != null)
                {
                    var full = await _repository.GetAttachmentAsync(firstImage.AttachmentId);
                    if (full != null)
                    {
                        updated.ThumbnailImageBytes = full.FileBytes;
                        updated.ThumbnailMimeType = full.MimeType;
                    }
                }

                if (existing != null)
                {
                    var index = Tickets.IndexOf(existing);
                    Tickets[index] = updated;
                }
                else
                {
                    Tickets.Insert(0, updated);
                }

                await LoadSummaryAsync();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Refresh Failed", "Failed to refresh ticket: " + ex.Message);
            }
        }
    }
}
