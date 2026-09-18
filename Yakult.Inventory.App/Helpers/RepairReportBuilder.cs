using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Interop;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Wpf.RepairPortal.Reports.ViewModels;
using Yakult.Inventory.App.Wpf.RepairPortal.Reports.Views;

namespace Yakult.Inventory.App.Helpers
{
    /// <summary>Builds and launches the Repair Report (RepairReport.rdlc) for one ticket. Owns all
    /// presentation decisions (dedup, "is this worth printing", section visibility) — the repository
    /// only gathers raw data. Design principle: a section prints only when it has something
    /// meaningful to say. Never render "N/A", "No parts logged.", zero-value stats, etc. — omit the
    /// whole section instead, so a simple repair renders as a short, dense report rather than a
    /// database dump padded with empty sections.</summary>
    public static class RepairReportBuilder
    {
        private static string ReportPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", "RepairReport.rdlc");

        private static string BatchReportPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", "RepairReportBatch.rdlc");

        private const string ReviewedByRole = "Reviewed By";
        private const string ReceivedByRole = "Received By";
        private const string ReviewedByRolePrefix = "Reviewed By — ";
        private const string ReceivedByRolePrefix = "Received By — ";

        public static async Task ShowRepairReportAsync(int repairTicketId)
        {
            var generatedByName = string.IsNullOrWhiteSpace(AppSession.CurrentEmployeeName)
                ? AppSession.CurrentUserName
                : AppSession.CurrentEmployeeName;

            var repo = new RepairTicketRepository();
            var data = await repo.GetReportDataAsync(repairTicketId, generatedByName);

            if (data == null)
            {
                MessageBox.Show("This repair ticket could not be found.", "Repair Report",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Repair Portal's own WPF signatory picker (RepairSignatoryPickerWindow) — shows a real
            // item table above each role's Employee/Title picker so it's unmistakable which item
            // that signature is for. Scoped so the picker actually helps rather than just searching
            // the whole company: Reviewed By only offers IT Dept. employees (repairs are handled by
            // IT), Received By only offers employees from the ticket's own requesting department.
            // Cancelling doesn't abort report generation — it just leaves those two signature blocks
            // blank for physical fill-in.
            var itDeptId = await FindDepartmentIdByNameAsync("Information Technology");

            // Prefills Reviewed By/Received By from whoever was recorded on this ticket's last
            // printed report, if it's ever been printed before — empty for a first-ever print.
            var lastSignatures = await repo.GetLatestReportSignaturesAsync(repairTicketId);
            var reviewedPrefill = lastSignatures.TryGetValue(ReviewedByRole, out var rvw) ? rvw : (name: null, title: null, date: (DateTime?)null);
            var receivedPrefill = lastSignatures.TryGetValue(ReceivedByRole, out var rcv) ? rcv : (name: null, title: null, date: (DateTime?)null);

            string reviewedByName = "", reviewedByTitle = "", receivedByName = "", receivedByTitle = "";
            DateTime? reviewedByDate = null, receivedByDate = null;
            var itemRow = new SignatoryItemRow
            {
                Item = $"{data.ItemName} ({data.TicketCode})",
                Category = IsMeaningful(data.Category) ? data.Category : "N/A",
                Problem = IsMeaningful(data.ProblemFreeText) || data.ObservationTexts.Count > 0
                    ? string.Join(" / ", data.ObservationTexts.Concat(new[] { data.ProblemFreeText }).Where(IsMeaningful).Distinct())
                    : "N/A",
                Status = data.Status ?? ""
            };
            var groups = new[]
            {
                new RepairSignatoryPickerWindow.GroupSpec
                {
                    GroupLabel = $"{data.ItemName} ({data.TicketCode})",
                    Items = new List<SignatoryItemRow> { itemRow },
                    Roles = new List<RepairSignatoryPickerWindow.RoleSpec>
                    {
                        new RepairSignatoryPickerWindow.RoleSpec
                        {
                            RoleName = ReviewedByRole, Hint = "Information Technology Dept.", DeptId = itDeptId,
                            PrefilledName = reviewedPrefill.name, PrefilledTitle = reviewedPrefill.title, PrefilledDate = reviewedPrefill.date
                        },
                        new RepairSignatoryPickerWindow.RoleSpec
                        {
                            RoleName = ReceivedByRole, Hint = IsMeaningful(data.DeptName) ? data.DeptName : "", DeptId = data.DeptId,
                            PrefilledName = receivedPrefill.name, PrefilledTitle = receivedPrefill.title, PrefilledDate = receivedPrefill.date
                        }
                    }
                }
            };

            var ownerHandle = ResolveActiveOwnerHandle();
            var ownerWrapper = ownerHandle != IntPtr.Zero
                ? new Yakult.Inventory.App.WPF.Shared.Win32WindowWrapper(ownerHandle) : null;

            var picker = new RepairSignatoryPickerWindow(groups);
            AssignOwner(picker, ownerHandle);
            // Cancel means abort the whole report — Skip means proceed with blank signatures. Both
            // close the dialog, but only Cancel returns DialogResult=false; Skip returns true with
            // every field left blank, same as GetResults() would naturally return anyway.
            if (picker.ShowDialog() != true)
                return;

            var results = picker.GetResults();
            (reviewedByName, reviewedByTitle, reviewedByDate) = results[ReviewedByRole];
            (receivedByName, receivedByTitle, receivedByDate) = results[ReceivedByRole];

            // Append to history only for non-blank results — Skip (which explicitly blanks every
            // field before closing) naturally records nothing here.
            var recordedByUserId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : (int?)null;
            if (IsMeaningful(reviewedByName))
                await repo.RecordReportSignatureAsync(repairTicketId, ReviewedByRole, reviewedByName, reviewedByTitle, reviewedByDate, recordedByUserId);
            if (IsMeaningful(receivedByName))
                await repo.RecordReportSignatureAsync(repairTicketId, ReceivedByRole, receivedByName, receivedByTitle, receivedByDate, recordedByUserId);

            // Only ask if there's actually an image to decide about — no point prompting for a
            // ticket with no photo evidence at all.
            var selectedAttachments = new HashSet<RepairReportAttachmentRow>(
                data.Attachments.Where(a => string.Equals(a.AttachmentType, "Image", StringComparison.OrdinalIgnoreCase) && a.ThumbnailBytes != null));
            if (selectedAttachments.Count > 0)
            {
                var pickerRows = selectedAttachments.Select(a => new AttachmentPickerRow(a)).ToList();
                var attachmentPicker = new RepairReportAttachmentPickerDialog(pickerRows);
                AssignOwner(attachmentPicker, ownerHandle);
                if (attachmentPicker.ShowDialog() == true)
                    selectedAttachments = attachmentPicker.SelectedAttachments;

                // Only the images that made the final cut get the print-quality re-fetch — never
                // the whole attachment list, so this stays cheap regardless of how many photos a
                // ticket has.
                foreach (var a in selectedAttachments)
                    await repo.UpgradeAttachmentForPrintAsync(a);
            }

            var rdlcBytes = File.ReadAllBytes(ReportPath);
            var dataSources = new[]
            {
                ("RepairReportHeader", BuildHeaderTable(data,
                    reviewedByName, reviewedByTitle, reviewedByDate,
                    receivedByName, receivedByTitle, receivedByDate,
                    selectedAttachments))
            };

            var form = new ReportViewerForm(rdlcBytes, $"Repair Report — {data.TicketCode}", dataSources);
            if (ownerWrapper != null) form.ShowDialog(ownerWrapper); else form.ShowDialog();
        }

        /// <summary>Opens one combined report for several tickets at once, GROUPED BY REQUESTER —
        /// tickets sharing the same requesting department merge into one copy with a shared
        /// header/item list/signature block; tickets from different requesters each get their own
        /// copy, page-broken apart. A single selected ticket is just delegated straight to
        /// ShowRepairReportAsync (one ticket = one requester = the exact same full report as
        /// clicking that row's own "View Report").</summary>
        public static async Task ShowRepairReportBatchAsync(IReadOnlyList<int> repairTicketIds)
        {
            if (repairTicketIds == null || repairTicketIds.Count == 0) return;
            if (repairTicketIds.Count == 1)
            {
                await ShowRepairReportAsync(repairTicketIds[0]);
                return;
            }

            var generatedByName = string.IsNullOrWhiteSpace(AppSession.CurrentEmployeeName)
                ? AppSession.CurrentUserName
                : AppSession.CurrentEmployeeName;

            var repo = new RepairTicketRepository();
            var items = new List<RepairReportData>();
            foreach (var id in repairTicketIds)
            {
                var data = await repo.GetReportDataAsync(id, generatedByName);
                if (data != null) items.Add(data);
            }
            if (items.Count == 0)
            {
                MessageBox.Show("None of the selected repair tickets could be found.", "Repair Reports",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Group by requesting department — the "requester" concept already established
            // throughout the Repair Portal (RequestedByDeptId-first, falling back to the item's own
            // department). Tickets with no department at all land in one "Unspecified" bucket rather
            // than being silently dropped.
            var groups = items
                .GroupBy(d => d.DeptId ?? -1)
                .Select(g => new
                {
                    DeptId = g.Key == -1 ? (int?)null : g.Key,
                    Label = IsMeaningful(g.First().DeptName) ? g.First().DeptName : "Unspecified Department",
                    Tickets = g.OrderBy(t => t.TicketCode).ToList()
                })
                .OrderBy(g => g.Label)
                .ToList();

            // One signatory picker for the whole batch — ONE panel per requester group, each
            // showing that group's item table ONCE, followed by both "Reviewed By" and "Received
            // By" pickers scoped to that same group (so a different reviewer/receiver can be picked
            // per group instead of one shared reviewer for everything, without repeating the table).
            var itDeptId = await FindDepartmentIdByNameAsync("Information Technology");
            var groupSpecs = new List<RepairSignatoryPickerWindow.GroupSpec>();

            SignatoryItemRow ItemRow(RepairReportData t) => new SignatoryItemRow
            {
                Item = $"{t.ItemName} ({t.TicketCode})",
                Category = IsMeaningful(t.Category) ? t.Category : "N/A",
                Problem = GetProblemSummary(t),
                Status = t.Status ?? ""
            };

            foreach (var g in groups)
            {
                // A group's Reviewed By/Received By is one shared signature across every ticket in
                // it — prefill from whichever ticket in the group has been printed most recently
                // (the first one, since Tickets is already ordered), rather than per-ticket.
                var groupSignatures = await repo.GetLatestReportSignaturesAsync(g.Tickets[0].RepairTicketId);
                var reviewedPrefill = groupSignatures.TryGetValue(ReviewedByRole, out var rvw) ? rvw : (name: null, title: null, date: (DateTime?)null);
                var receivedPrefill = groupSignatures.TryGetValue(ReceivedByRole, out var rcv) ? rcv : (name: null, title: null, date: (DateTime?)null);

                groupSpecs.Add(new RepairSignatoryPickerWindow.GroupSpec
                {
                    GroupLabel = g.Label,
                    Items = g.Tickets.Select(ItemRow).ToList(),
                    ShowDivider = true,
                    Roles = new List<RepairSignatoryPickerWindow.RoleSpec>
                    {
                        new RepairSignatoryPickerWindow.RoleSpec
                        {
                            RoleName = ReviewedByRolePrefix + g.Label, Hint = "Information Technology Dept.", DeptId = itDeptId,
                            PrefilledName = reviewedPrefill.name, PrefilledTitle = reviewedPrefill.title, PrefilledDate = reviewedPrefill.date
                        },
                        new RepairSignatoryPickerWindow.RoleSpec
                        {
                            RoleName = ReceivedByRolePrefix + g.Label, Hint = g.Label, DeptId = g.DeptId,
                            PrefilledName = receivedPrefill.name, PrefilledTitle = receivedPrefill.title, PrefilledDate = receivedPrefill.date
                        }
                    }
                });
            }

            var ownerHandle = ResolveActiveOwnerHandle();
            var ownerWrapper = ownerHandle != IntPtr.Zero
                ? new Yakult.Inventory.App.WPF.Shared.Win32WindowWrapper(ownerHandle) : null;

            var reviewedByPerGroup = new Dictionary<string, (string name, string title, DateTime? date)>();
            var receivedByPerGroup = new Dictionary<string, (string name, string title, DateTime? date)>();
            var picker = new RepairSignatoryPickerWindow(groupSpecs);
            AssignOwner(picker, ownerHandle);
            // Cancel aborts the whole batch report; Skip proceeds with blank signatures (see the
            // single-ticket flow's identical comment above for why DialogResult alone is enough).
            if (picker.ShowDialog() != true)
                return;

            var results = picker.GetResults();
            var recordedByUserId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : (int?)null;
            foreach (var g in groups)
            {
                var reviewed = results[ReviewedByRolePrefix + g.Label];
                var received = results[ReceivedByRolePrefix + g.Label];
                reviewedByPerGroup[g.Label] = reviewed;
                receivedByPerGroup[g.Label] = received;

                // Record against EVERY ticket in the group (not just one) — a group's signature
                // applies to all of them, so each ticket's own prefill history stays accurate even
                // if it's later reprinted solo.
                foreach (var t in g.Tickets)
                {
                    if (IsMeaningful(reviewed.name))
                        await repo.RecordReportSignatureAsync(t.RepairTicketId, ReviewedByRole, reviewed.name, reviewed.title, reviewed.date, recordedByUserId);
                    if (IsMeaningful(received.name))
                        await repo.RecordReportSignatureAsync(t.RepairTicketId, ReceivedByRole, received.name, received.title, received.date, recordedByUserId);
                }
            }

            var selectedAttachments = new HashSet<RepairReportAttachmentRow>(
                items.SelectMany(d => d.Attachments.Where(a =>
                    string.Equals(a.AttachmentType, "Image", StringComparison.OrdinalIgnoreCase) && a.ThumbnailBytes != null)));
            if (selectedAttachments.Count > 0)
            {
                var pickerRows = items
                    .SelectMany(d => d.Attachments
                        .Where(a => selectedAttachments.Contains(a))
                        .Select(a => new AttachmentPickerRow(a, d.TicketCode)))
                    .ToList();
                var attachmentPicker = new RepairReportAttachmentPickerDialog(pickerRows);
                AssignOwner(attachmentPicker, ownerHandle);
                if (attachmentPicker.ShowDialog() == true)
                    selectedAttachments = attachmentPicker.SelectedAttachments;

                foreach (var a in selectedAttachments)
                    await repo.UpgradeAttachmentForPrintAsync(a);
            }

            var table = BuildBatchTable(groups.Select(g => (g.Label, g.Tickets)).ToList(),
                generatedByName, reviewedByPerGroup, receivedByPerGroup, selectedAttachments);

            var rdlcBytes = File.ReadAllBytes(BatchReportPath);
            var form = new ReportViewerForm(rdlcBytes, $"Repair Reports — {items.Count} tickets, {groups.Count} requester(s)",
                new[] { ("RepairReportBatchItems", table) });
            if (ownerWrapper != null) form.ShowDialog(ownerWrapper); else form.ShowDialog();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        /// <summary>Finds the Win32 HWND to use as the owner for the report's dialogs
        /// (RepairSignatoryPickerWindow, ReportViewerForm). Without an explicit owner, these
        /// top-level windows have no place in the Win32 owner chain — closing them can then leave
        /// the wrong window activated, which in this hybrid WPF-in-WinForms app has been observed
        /// to minimize the whole application.
        ///
        /// This MUST be the window that is actually active right now (e.g. the WPF Repair Reports
        /// list window), not a distant ancestor like MainForm — pointing the owner straight at
        /// MainForm and skipping the WPF windows in between breaks the Z-order chain Windows expects
        /// for activation, which was tried first and caused the exact same minimize bug on close.
        /// GetActiveWindow() returns the active window of the calling thread's message queue — since
        /// this whole hybrid app (WinForms host + every WPF window) runs on one UI thread, it
        /// correctly reflects whichever window — WPF or WinForms — is actually on top right now.</summary>
        private static IntPtr ResolveActiveOwnerHandle() => GetActiveWindow();

        /// <summary>Sets the Win32 owner on a WPF window BEFORE it's shown (WindowInteropHelper.Owner
        /// only takes effect if assigned before Show()/ShowDialog() creates the HWND).</summary>
        private static void AssignOwner(System.Windows.Window window, IntPtr ownerHandle)
        {
            if (ownerHandle == IntPtr.Zero) return;
            var interop = new WindowInteropHelper(window);
            interop.Owner = ownerHandle;
        }

        /// <summary>Looks up a Department's Id by a case-insensitive partial name match (e.g.
        /// "Information Technology" matches "Information Technology Department"). Returns null if
        /// no match is found — callers treat that as "don't filter", not an error.</summary>
        private static async Task<int?> FindDepartmentIdByNameAsync(string namePattern)
        {
            try
            {
                Core.DatabaseConfig.EnsureConfigured();
                using (var con = new System.Data.SqlClient.SqlConnection(Core.DatabaseConfig.ConnectionString))
                using (var cmd = new System.Data.SqlClient.SqlCommand(
                    "SELECT TOP (1) DeptId FROM dbo.Department WHERE Active = 1 AND Name LIKE @Pattern ORDER BY DeptId", con))
                {
                    cmd.Parameters.AddWithValue("@Pattern", "%" + namePattern + "%");
                    await con.OpenAsync();
                    var result = await cmd.ExecuteScalarAsync();
                    return result == null || result == DBNull.Value ? (int?)null : Convert.ToInt32(result);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Deduped Observations + free-text Problem for one ticket, joined into one line —
        /// shared by BuildBatchTable's item narrative and the signatory picker's item table so both
        /// show the exact same summary.</summary>
        private static string GetProblemSummary(RepairReportData t)
        {
            var problemLines = new List<string>();
            foreach (var o in t.ObservationTexts)
                if (IsMeaningful(o) && !problemLines.Any(p => string.Equals(p.Trim(), o.Trim(), StringComparison.OrdinalIgnoreCase)))
                    problemLines.Add(o.Trim());
            if (IsMeaningful(t.ProblemFreeText) &&
                !problemLines.Any(p => string.Equals(p, t.ProblemFreeText.Trim(), StringComparison.OrdinalIgnoreCase)))
                problemLines.Add(t.ProblemFreeText.Trim());
            return problemLines.Count > 0 ? string.Join(" / ", problemLines) : "N/A";
        }

        private static bool IsMeaningful(string text) =>
            !string.IsNullOrWhiteSpace(text) && !string.Equals(text.Trim(), "N/A", StringComparison.OrdinalIgnoreCase);

        /// <summary>Every per-item field the single-ticket report renders (Asset Info, Reported
        /// Problem, Repair Details, Work Performed, Parts, Diagnosis &amp; Resolution,
        /// Recommendations, Attachments) — computed once here and reused by BOTH BuildHeaderTable
        /// (single ticket) and BuildBatchTable (one block per item within a requester group), so the
        /// batch report can never drift from or drop content the single-ticket report shows.</summary>
        private sealed class ItemFields
        {
            public string AssetL1, AssetR1, AssetL2, AssetR2, AssetL3, AssetR3, AssetL4, AssetR4;
            public bool ShowReportedProblem; public string ProblemText;
            public string DetailsText;
            public bool ShowWorkPerformed; public string WorkPerformedText;
            public bool ShowParts; public string PartsText;
            public bool ShowDiagnosis; public string DiagnosisText;
            public bool ShowItemDisposition; public string ItemDispositionText;
            public bool ShowRecommendations; public string RecommendationsText;
            public bool ShowAttachments; public byte[] AttachmentsPreviewImage;
        }

        private static ItemFields ComputeItemFields(RepairReportData d, ISet<RepairReportAttachmentRow> selectedAttachments)
        {
            var f = new ItemFields();

            // Asset Info — 7 fields, paired two-per-row into a compact 2-column block. Asset Tag
            // and Brand are deliberately excluded (no backing schema anywhere — always N/A, so per
            // the data-driven principle they're never printed rather than shown blank).
            var assetFields = new List<string>();
            if (IsMeaningful(d.ItemName)) assetFields.Add("Item Name: " + d.ItemName);
            if (IsMeaningful(d.Category)) assetFields.Add("Category: " + d.Category);
            if (IsMeaningful(d.ModelNumber)) assetFields.Add("Model Number: " + d.ModelNumber);
            if (IsMeaningful(d.SerialNumber)) assetFields.Add("Serial Number: " + d.SerialNumber);
            if (IsMeaningful(d.DeptName)) assetFields.Add("Department: " + d.DeptName);
            if (IsMeaningful(d.BranchName)) assetFields.Add("Location: " + d.BranchName);
            if (IsMeaningful(d.AssignedUserName)) assetFields.Add("Assigned User: " + d.AssignedUserName);
            var slots = new string[8];
            for (int i = 0; i < 8; i++) slots[i] = i < assetFields.Count ? assetFields[i] : "";
            f.AssetL1 = slots[0]; f.AssetR1 = slots[1]; f.AssetL2 = slots[2]; f.AssetR2 = slots[3];
            f.AssetL3 = slots[4]; f.AssetR3 = slots[5]; f.AssetL4 = slots[6]; f.AssetR4 = slots[7];

            // Reported Problem — dedupe: if the only observation says the same thing as the
            // free-text Problem field, print it once, not twice.
            var problemLines = new List<string>();
            foreach (var o in d.ObservationTexts)
                if (IsMeaningful(o) && !problemLines.Any(p => string.Equals(p.Trim(), o.Trim(), StringComparison.OrdinalIgnoreCase)))
                    problemLines.Add(o.Trim());
            if (IsMeaningful(d.ProblemFreeText) &&
                !problemLines.Any(p => string.Equals(p, d.ProblemFreeText.Trim(), StringComparison.OrdinalIgnoreCase)))
                problemLines.Add(d.ProblemFreeText.Trim());
            f.ShowReportedProblem = problemLines.Count > 0;
            f.ProblemText = string.Join("\n", problemLines);

            // Repair Details — compact, only the fields that are always meaningful once a ticket
            // exists. No raw counts (Total Repair Actions/Parts Logged) per the "don't pad with
            // redundant stats" principle.
            var detailLines = new List<string> { "Status: " + (d.Status ?? "") };
            if (IsMeaningful(d.TechnicianName)) detailLines.Add("Technician: " + d.TechnicianName);

            // Handed to a 3rd-party vendor: the vendor is who actually performed the repair, so
            // "Repaired By" shows the vendor's name directly instead of a separate "3rd Party
            // Handover" line duplicating the same fact — matches how it should read on the report.
            if (IsMeaningful(d.HandedOverToVendorName))
                detailLines.Add("Repaired By: " + d.HandedOverToVendorName);
            else if (d.RepairedByNames != null && d.RepairedByNames.Count > 0)
                detailLines.Add("Repaired By: " + string.Join(", ", d.RepairedByNames));

            detailLines.Add("Reported: " + FormatDate(d.CreatedAt));
            if (d.CompletedAt.HasValue)
            {
                detailLines.Add("Completed: " + FormatDate(d.CompletedAt.Value));
                detailLines.Add("Duration: " + FormatDuration(d.CompletedAt.Value - d.CreatedAt));
            }
            f.DetailsText = string.Join("\n", detailLines);

            f.ShowWorkPerformed = IsMeaningful(d.WorkPerformed);
            f.WorkPerformedText = d.WorkPerformed?.Trim() ?? "";

            f.ShowParts = d.Parts.Count > 0;
            f.PartsText = string.Join("\n", d.Parts.Select(p => $"{p.PartDisplayName}: {FormatRepairResult(p.Status)}"));

            var diagnosisLines = new List<string>();
            if (IsMeaningful(d.RootCause)) diagnosisLines.Add("Root Cause: " + d.RootCause.Trim());
            if (IsMeaningful(d.ResolutionSummary)) diagnosisLines.Add("Resolution: " + d.ResolutionSummary.Trim());
            f.ShowDiagnosis = diagnosisLines.Count > 0;
            f.DiagnosisText = string.Join("\n", diagnosisLines);

            f.ShowItemDisposition = d.ShowItemDisposition;
            f.ItemDispositionText = d.ItemDispositionText ?? "";

            f.ShowRecommendations = IsMeaningful(d.Recommendations);
            f.RecommendationsText = d.Recommendations?.Trim() ?? "";

            // Attachments — no filenames anywhere (per explicit request); every image attachment
            // is composited into one montage image, so the section is just "here's what it looked
            // like," nothing more. Only the attachments the technician left checked in
            // RepairReportAttachmentPickerDialog go into the montage.
            var relevantAttachments = d.Attachments.Where(a => selectedAttachments.Contains(a)).ToList();
            var montage = relevantAttachments.Count > 0 ? BuildAttachmentsMontage(relevantAttachments) : null;
            f.ShowAttachments = montage != null;
            f.AttachmentsPreviewImage = montage;

            return f;
        }

        private static DataTable BuildHeaderTable(RepairReportData d,
            string reviewedByName, string reviewedByTitle, DateTime? reviewedByDate,
            string receivedByName, string receivedByTitle, DateTime? receivedByDate,
            ISet<RepairReportAttachmentRow> selectedAttachments)
        {
            var table = new DataTable("RepairReportHeader");
            table.Columns.Add("TicketCode", typeof(string));
            table.Columns.Add("RepairTicketId", typeof(int));
            table.Columns.Add("Status", typeof(string));
            table.Columns.Add("CreatedAtText", typeof(string));
            table.Columns.Add("CompletedAtText", typeof(string));
            table.Columns.Add("GeneratedAtText", typeof(string));
            table.Columns.Add("GeneratedByName", typeof(string));
            table.Columns.Add("AssetL1", typeof(string));
            table.Columns.Add("AssetR1", typeof(string));
            table.Columns.Add("AssetL2", typeof(string));
            table.Columns.Add("AssetR2", typeof(string));
            table.Columns.Add("AssetL3", typeof(string));
            table.Columns.Add("AssetR3", typeof(string));
            table.Columns.Add("AssetL4", typeof(string));
            table.Columns.Add("AssetR4", typeof(string));
            table.Columns.Add("ShowReportedProblem", typeof(bool));
            table.Columns.Add("ProblemText", typeof(string));
            table.Columns.Add("DetailsText", typeof(string));
            table.Columns.Add("ShowWorkPerformed", typeof(bool));
            table.Columns.Add("WorkPerformedText", typeof(string));
            table.Columns.Add("ShowParts", typeof(bool));
            table.Columns.Add("PartsText", typeof(string));
            table.Columns.Add("ShowDiagnosis", typeof(bool));
            table.Columns.Add("DiagnosisText", typeof(string));
            table.Columns.Add("ShowItemDisposition", typeof(bool));
            table.Columns.Add("ItemDispositionText", typeof(string));
            table.Columns.Add("ShowRecommendations", typeof(bool));
            table.Columns.Add("RecommendationsText", typeof(string));
            table.Columns.Add("ShowAttachments", typeof(bool));
            table.Columns.Add("AttachmentsPreviewImage", typeof(byte[]));
            table.Columns.Add("PreparedByPosition", typeof(string));
            table.Columns.Add("PreparedByDateText", typeof(string));
            table.Columns.Add("ReviewedByName", typeof(string));
            table.Columns.Add("ReviewedByPosition", typeof(string));
            table.Columns.Add("ReviewedByDateText", typeof(string));
            table.Columns.Add("ReceivedByName", typeof(string));
            table.Columns.Add("ReceivedByPosition", typeof(string));
            table.Columns.Add("ReceivedByDateText", typeof(string));

            var f = ComputeItemFields(d, selectedAttachments);

            var row = table.NewRow();
            row["TicketCode"] = d.TicketCode ?? "";
            row["RepairTicketId"] = d.RepairTicketId;
            row["Status"] = d.Status ?? "";
            row["CreatedAtText"] = FormatDate(d.CreatedAt);
            row["CompletedAtText"] = d.CompletedAt.HasValue ? FormatDate(d.CompletedAt.Value) : "";
            row["GeneratedAtText"] = FormatDate(d.GeneratedAt);
            row["GeneratedByName"] = FormatSignatoryName(d.GeneratedByName);

            row["AssetL1"] = f.AssetL1; row["AssetR1"] = f.AssetR1;
            row["AssetL2"] = f.AssetL2; row["AssetR2"] = f.AssetR2;
            row["AssetL3"] = f.AssetL3; row["AssetR3"] = f.AssetR3;
            row["AssetL4"] = f.AssetL4; row["AssetR4"] = f.AssetR4;
            row["ShowReportedProblem"] = f.ShowReportedProblem;
            row["ProblemText"] = f.ProblemText;
            row["DetailsText"] = f.DetailsText;
            row["ShowWorkPerformed"] = f.ShowWorkPerformed;
            row["WorkPerformedText"] = f.WorkPerformedText;
            row["ShowParts"] = f.ShowParts;
            row["PartsText"] = f.PartsText;
            row["ShowDiagnosis"] = f.ShowDiagnosis;
            row["DiagnosisText"] = f.DiagnosisText;
            row["ShowItemDisposition"] = f.ShowItemDisposition;
            row["ItemDispositionText"] = f.ItemDispositionText;
            row["ShowRecommendations"] = f.ShowRecommendations;
            row["RecommendationsText"] = f.RecommendationsText;
            row["ShowAttachments"] = f.ShowAttachments;
            row["AttachmentsPreviewImage"] = (object)f.AttachmentsPreviewImage ?? DBNull.Value;

            // Signatures — Position is the picked Title/Designation (blank if nobody was picked, so
            // the line just doesn't print rather than showing a redundant generic label). The
            // Prepared By signatory is always the current session user, signing today.
            row["PreparedByPosition"] = FormatSignatoryPosition(AppSession.CurrentEmployeePosition);
            row["PreparedByDateText"] = FormatSignDate(DateTime.Today);
            row["ReviewedByName"] = FormatSignatoryName(reviewedByName);
            row["ReviewedByPosition"] = FormatSignatoryPosition(reviewedByTitle);
            row["ReviewedByDateText"] = FormatSignDate(reviewedByDate);
            row["ReceivedByName"] = FormatSignatoryName(receivedByName);
            row["ReceivedByPosition"] = FormatSignatoryPosition(receivedByTitle);
            row["ReceivedByDateText"] = FormatSignDate(receivedByDate);

            table.Rows.Add(row);
            return table;
        }

        /// <summary>Builds the flat, one-row-per-item DataTable for RepairReportBatch.rdlc. Every
        /// group-level value (banner text, signatures) is repeated identically across all rows that
        /// belong to that group — RepairReportBatch.rdlc groups by GroupIndex and reads those fields
        /// directly off the group's row context for its banner/column-header/signature rows, exactly
        /// like this codebase's existing WarrantyReport.rdlc reads Fields!Year.Value directly for
        /// its Year-banner row without an aggregate function.</summary>
        private static DataTable BuildBatchTable(
            List<(string Label, List<RepairReportData> Tickets)> groups,
            string generatedByName,
            Dictionary<string, (string name, string title, DateTime? date)> reviewedByPerGroup,
            Dictionary<string, (string name, string title, DateTime? date)> receivedByPerGroup,
            ISet<RepairReportAttachmentRow> selectedAttachments)
        {
            var table = new DataTable("RepairReportBatchItems");
            table.Columns.Add("GroupIndex", typeof(int));
            table.Columns.Add("GroupCount", typeof(int));
            table.Columns.Add("GroupMetaBoldLine", typeof(string));
            table.Columns.Add("GroupMetaRestLines", typeof(string));
            table.Columns.Add("RequesterInfoText", typeof(string));
            table.Columns.Add("PreparedByName", typeof(string));
            table.Columns.Add("PreparedByPosition", typeof(string));
            table.Columns.Add("PreparedByDateText", typeof(string));
            table.Columns.Add("ReviewedByName", typeof(string));
            table.Columns.Add("ReviewedByPosition", typeof(string));
            table.Columns.Add("ReviewedByDateText", typeof(string));
            table.Columns.Add("ReceivedByName", typeof(string));
            table.Columns.Add("ReceivedByPosition", typeof(string));
            table.Columns.Add("ReceivedByDateText", typeof(string));
            // Per-item fields — the exact same set RepairReport.rdlc renders for a single ticket,
            // computed once by ComputeItemFields and reused here so nothing present in the
            // single-ticket report is ever dropped from the batch/requester-grouped report.
            table.Columns.Add("ItemTitleText", typeof(string));
            table.Columns.Add("AssetL1", typeof(string));
            table.Columns.Add("AssetR1", typeof(string));
            table.Columns.Add("AssetL2", typeof(string));
            table.Columns.Add("AssetR2", typeof(string));
            table.Columns.Add("AssetL3", typeof(string));
            table.Columns.Add("AssetR3", typeof(string));
            table.Columns.Add("AssetL4", typeof(string));
            table.Columns.Add("AssetR4", typeof(string));
            table.Columns.Add("ShowReportedProblem", typeof(bool));
            table.Columns.Add("ProblemText", typeof(string));
            table.Columns.Add("DetailsText", typeof(string));
            table.Columns.Add("ShowWorkPerformed", typeof(bool));
            table.Columns.Add("WorkPerformedText", typeof(string));
            table.Columns.Add("ShowParts", typeof(bool));
            table.Columns.Add("PartsText", typeof(string));
            table.Columns.Add("ShowDiagnosis", typeof(bool));
            table.Columns.Add("DiagnosisText", typeof(string));
            table.Columns.Add("ShowItemDisposition", typeof(bool));
            table.Columns.Add("ItemDispositionText", typeof(string));
            table.Columns.Add("ShowRecommendations", typeof(bool));
            table.Columns.Add("RecommendationsText", typeof(string));
            table.Columns.Add("ShowAttachments", typeof(bool));
            table.Columns.Add("AttachmentsPreviewImage", typeof(byte[]));

            for (int gi = 0; gi < groups.Count; gi++)
            {
                var (label, tickets) = groups[gi];
                var groupIndex = gi + 1;

                // Exact reproduction of RepairReport.rdlc's Page Header metadata block (bold Report
                // No. line, then regular Repair ID/Status/Date lines) — that Page Header can't itself
                // vary per requester group, so this is that same design placed once per item set,
                // not a new banner design.
                string metaBoldLine;
                var metaRestLines = new List<string>();
                if (tickets.Count == 1)
                {
                    var t = tickets[0];
                    metaBoldLine = "Report No.: " + (t.TicketCode ?? "");
                    metaRestLines.Add("Repair ID: " + t.RepairTicketId);
                    metaRestLines.Add("Status: " + (t.Status ?? ""));
                    metaRestLines.Add("Date Created: " + FormatDate(t.CreatedAt));
                    if (t.CompletedAt.HasValue)
                        metaRestLines.Add("Date Completed: " + FormatDate(t.CompletedAt.Value));
                }
                else
                {
                    metaBoldLine = "Report Nos.: " + string.Join(", ", tickets.Select(t => t.TicketCode));
                }

                var requesterLines = new List<string> { "Requester: " + label };
                var branch = tickets.Select(t => t.BranchName).FirstOrDefault(IsMeaningful);
                if (IsMeaningful(branch)) requesterLines.Add("Location: " + branch);

                var (reviewedName, reviewedTitle, reviewedDate) = reviewedByPerGroup.TryGetValue(label, out var rvw) ? rvw : ("", "", (DateTime?)null);
                var (receivedName, receivedTitle, receivedDate) = receivedByPerGroup.TryGetValue(label, out var rv) ? rv : ("", "", (DateTime?)null);

                for (int ti = 0; ti < tickets.Count; ti++)
                {
                    var t = tickets[ti];
                    var f = ComputeItemFields(t, selectedAttachments);

                    var row = table.NewRow();
                    row["GroupIndex"] = groupIndex;
                    row["GroupCount"] = groups.Count;
                    row["GroupMetaBoldLine"] = metaRestLines.Count > 0 ? metaBoldLine + "\n" : metaBoldLine;
                    row["GroupMetaRestLines"] = string.Join("\n", metaRestLines);
                    row["RequesterInfoText"] = string.Join("\n", requesterLines);
                    row["PreparedByName"] = FormatSignatoryName(generatedByName);
                    row["PreparedByPosition"] = FormatSignatoryPosition(AppSession.CurrentEmployeePosition);
                    row["PreparedByDateText"] = FormatSignDate(DateTime.Today);
                    row["ReviewedByName"] = FormatSignatoryName(reviewedName);
                    row["ReviewedByPosition"] = FormatSignatoryPosition(reviewedTitle);
                    row["ReviewedByDateText"] = FormatSignDate(reviewedDate);
                    row["ReceivedByName"] = FormatSignatoryName(receivedName);
                    row["ReceivedByPosition"] = FormatSignatoryPosition(receivedTitle);
                    row["ReceivedByDateText"] = FormatSignDate(receivedDate);

                    row["ItemTitleText"] = tickets.Count > 1
                        ? $"Item {ti + 1} — {t.ItemName} ({t.TicketCode})"
                        : $"{t.ItemName} ({t.TicketCode})";
                    row["AssetL1"] = f.AssetL1; row["AssetR1"] = f.AssetR1;
                    row["AssetL2"] = f.AssetL2; row["AssetR2"] = f.AssetR2;
                    row["AssetL3"] = f.AssetL3; row["AssetR3"] = f.AssetR3;
                    row["AssetL4"] = f.AssetL4; row["AssetR4"] = f.AssetR4;
                    row["ShowReportedProblem"] = f.ShowReportedProblem;
                    row["ProblemText"] = f.ProblemText;
                    row["DetailsText"] = f.DetailsText;
                    row["ShowWorkPerformed"] = f.ShowWorkPerformed;
                    row["WorkPerformedText"] = f.WorkPerformedText;
                    row["ShowParts"] = f.ShowParts;
                    row["PartsText"] = f.PartsText;
                    row["ShowDiagnosis"] = f.ShowDiagnosis;
                    row["DiagnosisText"] = f.DiagnosisText;
                    row["ShowItemDisposition"] = f.ShowItemDisposition;
                    row["ItemDispositionText"] = f.ItemDispositionText;
                    row["ShowRecommendations"] = f.ShowRecommendations;
                    row["RecommendationsText"] = f.RecommendationsText;
                    row["ShowAttachments"] = f.ShowAttachments;
                    row["AttachmentsPreviewImage"] = (object)f.AttachmentsPreviewImage ?? DBNull.Value;
                    table.Rows.Add(row);
                }
            }

            return table;
        }

        /// <summary>Draws every image attachment (up to 6) into one grid image, so the report can
        /// show "what it looked like" without needing per-attachment filenames or a data-table row
        /// per image. Returns null if there are no image attachments to show.</summary>
        private static byte[] BuildAttachmentsMontage(List<RepairReportAttachmentRow> attachments)
        {
            var images = attachments
                .Where(a => string.Equals(a.AttachmentType, "Image", StringComparison.OrdinalIgnoreCase) && a.ThumbnailBytes != null)
                .Take(6)
                .ToList();
            if (images.Count == 0) return null;

            // RepairReport.rdlc's ImgAttachmentsPreview displays this at 170mm x 60mm (~6.7in x
            // 2.4in) — at print-quality ~300 DPI that box needs roughly 2000x700px of real detail.
            // The old 140px cells (a ~150px canvas for a single image) were being stretched across
            // that whole box regardless of how good the upstream source fetch was, which is what
            // actually caused the visible pixelation — not the source resolution.
            const int cellSize = 500;
            const int gap = 12;
            int columns = Math.Min(images.Count, 4);
            int rows = (int)Math.Ceiling(images.Count / (double)columns);
            int width = columns * cellSize + (columns + 1) * gap;
            int height = rows * cellSize + (rows + 1) * gap;

            using (var canvas = new Bitmap(width, height))
            using (var g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.White);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                for (int i = 0; i < images.Count; i++)
                {
                    int col = i % columns;
                    int rowIdx = i / columns;
                    int x = gap + col * (cellSize + gap);
                    int y = gap + rowIdx * (cellSize + gap);

                    using (var ms = new MemoryStream(images[i].ThumbnailBytes))
                    using (var img = Image.FromStream(ms))
                    {
                        var scale = Math.Min((double)cellSize / img.Width, (double)cellSize / img.Height);
                        var w = (int)(img.Width * scale);
                        var h = (int)(img.Height * scale);
                        g.DrawImage(img, x + (cellSize - w) / 2, y + (cellSize - h) / 2, w, h);
                    }
                }

                var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                    .FirstOrDefault(e => e.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                using (var encoderParams = new System.Drawing.Imaging.EncoderParameters(1))
                using (var outStream = new MemoryStream())
                {
                    encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 92L);
                    if (jpegEncoder != null) canvas.Save(outStream, jpegEncoder, encoderParams);
                    else canvas.Save(outStream, System.Drawing.Imaging.ImageFormat.Jpeg);
                    return outStream.ToArray();
                }
            }
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            var days = (int)span.TotalDays;
            var hours = span.Hours;
            if (days > 0) return $"{days}d {hours}h";
            return $"{hours}h {span.Minutes}m";
        }

        private static string FormatRepairResult(string partStatus)
        {
            switch (partStatus)
            {
                case "Repaired": return "Repaired";
                case "CannotRepair": return "Not Repairable";
                default: return "In Progress";
            }
        }

        private static string FormatDate(DateTime value) => value.ToString("MMM d, yyyy h:mm tt");

        private static string FormatSignDate(DateTime? value) => value.HasValue ? value.Value.ToString("MMM d, yyyy") : "";

        /// <summary>Signature block name styling: title case regardless of how it was typed or
        /// stored, e.g. "SHAWN QUIN A. BAGNOL" → "Shawn Quin A. Bagnol". Lowercasing first is
        /// required — ToTitleCase leaves already-uppercase words untouched otherwise.</summary>
        private static string FormatSignatoryName(string name) =>
            string.IsNullOrWhiteSpace(name)
                ? ""
                : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.Trim().ToLowerInvariant());

        /// <summary>Signature block position/title styling: always uppercase, e.g. "Supervisor" →
        /// "SUPERVISOR".</summary>
        private static string FormatSignatoryPosition(string position) =>
            string.IsNullOrWhiteSpace(position) ? "" : position.Trim().ToUpperInvariant();
    }
}
