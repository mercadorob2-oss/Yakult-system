using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Reporting.WinForms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Dialogs;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Renewal;

namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// Launches RDLC reports in a dedicated viewer form.
    ///
    /// USAGE PATTERN (WinForms LocalReport / RDLC):
    ///   Each public method:
    ///     1. Loads data from the matching SQL view into a DataTable.
    ///     2. Creates a Form with a ReportViewer control.
    ///     3. Binds the DataTable as a ReportDataSource.
    ///     4. Shows the viewer (or exports to PDF).
    ///
    /// The RDLC files live in  Yakult.Inventory.App/Reports/
    /// and must be set as  Build Action = Content  (or Embedded Resource).
    /// </summary>
    public static class ReportLauncher
    {
        // ── Resolve the folder that contains *.rdlc files ────────────────────
        private static string ReportsFolder =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");

        private static string ReportPath(string fileName) =>
            Path.Combine(ReportsFolder, fileName);

        // ════════════════════════════════════════════════════════════════════
        // 1. SETS REPORT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Loads data for the Sets report (one row per item in each set).</summary>
        public static DataTable LoadSetsData()
        {
            const string sql = @"
SELECT
  [Year], MonthNumber, MonthName,
  SetId, SetCode, DateDispatched, EmployeeName, ItemNo,
  SetItemId, ItemName, ModelNumber, SerialNumber, ItemType, Category,
  Quantity, Amount, CompanyName, BranchName, DepartmentName
FROM dbo.vw_Report_SetsWithItems
ORDER BY [Year] ASC, MonthNumber ASC, SetId ASC, ItemNo ASC";

            return FillDataTable(sql, "SetsData");
        }

        /// <summary>Opens the Sets report viewer form.</summary>
        public static void ShowSetsReport()
        {
            var sigRow = ShowSetsSignatoryPickers();
            if (sigRow == null) return;
            var colParams = ShowColumnSelection("Sets Report", ReportColumnSelectionDialog.SetsReportColumns());
            if (colParams == null) return;
            var form = CreateSetsReportForm(sigRow, colParams);
            form?.ShowDialog();
        }

        public static ReportViewerForm CreateSetsReportForm(DataTable sigRowData = null, ReportParameter[] columnParams = null)
        {
            if (sigRowData == null) sigRowData = BuildEmptySigRowTable();

            var rdlcBytes = BuildSetsRdlcBytes(columnParams);
            var sources = new List<(string, DataTable)>
            {
                ("SetsData",   LoadSetsData()),
                ("SigRowData", sigRowData)
            };

            var form = new ReportViewerForm(rdlcBytes, "Sets Report", sources);
            if (columnParams != null && columnParams.Length > 0)
                form.SetReportParameters(columnParams);
            return form;
        }

        /// <summary>
        /// Shows ONE dialog covering all four Sets roles (Prepared By, Noted By, Approved By,
        /// Received By) and returns a SigRowData DataTable, or null if the user cancelled.
        /// Received By stays optional — leaving it blank simply produces a blank column.
        /// </summary>
        public static DataTable ShowSetsSignatoryPickers()
        {
            var roles = new[] { "Prepared By", "Noted By", "Approved By", "Received By" };
            using (var picker = new SignatoryPickerDialog(roles, multiRole: true))
            {
                if (picker.ShowDialog() != DialogResult.OK) return null;

                var byRole = picker.RoleSignatoryData;
                Func<string, DataTable> get = role =>
                {
                    DataTable dt;
                    return byRole != null && byRole.TryGetValue(role, out dt) ? dt : BuildEmptySignatoryTable();
                };

                return BuildSetsSignatoryRow(get(roles[0]), get(roles[1]), get(roles[2]), get(roles[3]));
            }
        }

        /// <summary>
        /// Shows a signatory picker where cancelling returns an empty table (blank entry)
        /// rather than null, so the report section still renders.
        /// </summary>
        public static DataTable ShowOptionalSignatoryPicker(string label)
        {
            using (var picker = new Dialogs.SignatoryPickerDialog(new[] { label }))
            {
                if (picker.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return BuildEmptySignatoryTable();
                return picker.SignatoryData;
            }
        }

        /// <summary>
        /// Builds a single-row SigRowData DataTable from four signatory picker results.
        /// Each picker result is expected to have at least one row; the first LeftName/LeftTitle is used.
        /// </summary>
        public static DataTable BuildSetsSignatoryRow(DataTable preparedBy, DataTable notedBy, DataTable approvedBy, DataTable receivedBy)
        {
            var dt = BuildEmptySigRowTable();
            dt.Rows.Clear();

            var (prepName,  prepPrefix,  prepTitle)  = ExtractFirstSignatory(preparedBy);
            var (notedName, notedPrefix, notedTitle)  = ExtractFirstSignatory(notedBy);
            var (appName,   appPrefix,   appTitle)    = ExtractFirstSignatory(approvedBy);
            var (recName,   recPrefix,   recTitle)    = ExtractFirstSignatory(receivedBy);

            dt.Rows.Add(prepName, prepPrefix, prepTitle,
                        notedName, notedPrefix, notedTitle,
                        appName,  appPrefix,  appTitle,
                        recName,  recPrefix,  recTitle);
            return dt;
        }

        private static (string name, string prefix, string title) ExtractFirstSignatory(DataTable dt)
        {
            if (dt == null || dt.Rows.Count == 0) return ("", "", "");
            var row = dt.Rows[0];
            string name   = row["LeftName"]?.ToString()  ?? "";
            string prefix = dt.Columns.Contains("LeftPrefix") ? row["LeftPrefix"]?.ToString() ?? "" : "";
            string title  = row["LeftTitle"]?.ToString() ?? "";
            return (name, prefix, title);
        }

        /// <summary>Builds an empty SigRowData DataTable (one blank row) for the 4-column signatory section.</summary>
        public static DataTable BuildEmptySigRowTable()
        {
            var dt = new DataTable("SigRowData");
            dt.Columns.Add("PreparedByName",   typeof(string));
            dt.Columns.Add("PreparedByPrefix", typeof(string));
            dt.Columns.Add("PreparedByTitle",  typeof(string));
            dt.Columns.Add("NotedByName",      typeof(string));
            dt.Columns.Add("NotedByPrefix",    typeof(string));
            dt.Columns.Add("NotedByTitle",     typeof(string));
            dt.Columns.Add("ApprovedByName",   typeof(string));
            dt.Columns.Add("ApprovedByPrefix", typeof(string));
            dt.Columns.Add("ApprovedByTitle",  typeof(string));
            dt.Columns.Add("ReceivedByName",   typeof(string));
            dt.Columns.Add("ReceivedByPrefix", typeof(string));
            dt.Columns.Add("ReceivedByTitle",  typeof(string));
            dt.Rows.Add("", "", "", "", "", "", "", "", "", "", "", "");
            return dt;
        }

        /// <summary>
        /// Loads Sets report data filtered to the specified SetIds (the "Generate Report" on
        /// selected rows path).
        ///
        /// This deliberately does NOT go through dbo.vw_Report_SetsWithItems: that view is built
        /// for the bulk report, so it INNER JOINs dbo.SetItem and hard-excludes invoice sets —
        /// which made a directly-selected Set (a not-yet-materialised Request Set, or one that's
        /// also been recorded as an invoice) come back blank. When the user explicitly picks the
        /// rows, honour the selection: report every chosen Set's items regardless of IsInvoice,
        /// pulling from dbo.SetItem where rows exist and falling back to dbo.Request for Sets
        /// that have none. Column shape and org resolution match the view exactly. Cartridge
        /// items stay excluded (placeholder ItemIds — they have their own report). The
        /// parameterless overload / bulk report is unchanged.
        /// </summary>
        public static DataTable LoadSetsData(IEnumerable<int> setIds)
        {
            var idList = string.Join(",", setIds ?? Enumerable.Empty<int>());
            if (string.IsNullOrEmpty(idList))
                return LoadSetsData();

            string sql = @"
-- ── SetItem-sourced (mirrors vw_Report_SetsWithItems, minus its IsInvoice filter) ──
SELECT
  YEAR  (ISNULL(s.DispatchDate, s.CreatedAt))                       AS [Year],
  MONTH (ISNULL(s.DispatchDate, s.CreatedAt))                       AS MonthNumber,
  DATENAME(MONTH, ISNULL(s.DispatchDate, s.CreatedAt))              AS MonthName,
  s.SetId,
  s.SetCode,
  CONVERT(date, ISNULL(s.DispatchDate, s.CreatedAt))               AS DateDispatched,
  emp.Name                                                          AS EmployeeName,
  ROW_NUMBER() OVER (PARTITION BY s.SetCode ORDER BY si.SetItemId)  AS ItemNo,
  si.SetItemId,
  i.[Name]                                                          AS ItemName,
  ISNULL(i.ModelNumber,  '')                                        AS ModelNumber,
  ISNULL(i.SerialNumber, '')                                        AS SerialNumber,
  i.ItemType,
  ISNULL(i.Category,     '')                                        AS Category,
  ISNULL(si.Quantity,    0)                                         AS Quantity,
  ISNULL(si.UnitPrice,   0)                                         AS Amount,
  CASE WHEN dist.Name IS NOT NULL
       THEN COALESCE(emp_co.Name, req_co.Name) + ' / ' + dist.Name
       ELSE COALESCE(emp_co.Name, req_co.Name) END                 AS CompanyName,
  COALESCE(emp_br.Name, req_br.Name)                                AS BranchName,
  COALESCE(emp_dp.Name, req_dp.Name)                                AS DepartmentName
FROM dbo.[Set]      s
INNER JOIN dbo.SetItem si ON si.SetId  = s.SetId
INNER JOIN dbo.Item    i  ON i.ItemId  = si.ItemId
OUTER APPLY (
    SELECT TOP 1 r.EmpId, r.ComId, r.DeptId, r.BranchId
    FROM   dbo.Request r WHERE r.SetId = s.SetId ORDER BY r.ReqId
) top_req
LEFT  JOIN dbo.Employee   emp    ON emp.EmpId       = COALESCE(s.ReceivedById, top_req.EmpId)
LEFT  JOIN dbo.Branch     emp_br ON emp_br.BranchId = emp.BranchId
LEFT  JOIN dbo.Department emp_dp ON emp_dp.DeptId   = emp.DeptId
LEFT  JOIN dbo.Company    emp_co ON emp_co.ComId    = emp.ComId
LEFT  JOIN dbo.Company    req_co ON req_co.ComId    = top_req.ComId
LEFT  JOIN dbo.Department req_dp ON req_dp.DeptId   = top_req.DeptId
LEFT  JOIN dbo.Branch     req_br ON req_br.BranchId = top_req.BranchId
LEFT  JOIN dbo.Distributor dist  ON dist.DistributorId = s.DistributorId
WHERE s.SetId IN (" + idList + @")
  AND ISNULL(i.Category, '') <> 'Cartridge'

UNION ALL

-- ── Request-sourced fallback: only for selected Sets that have NO SetItem rows ──
SELECT
  YEAR  (ISNULL(s.DispatchDate, s.CreatedAt))                       AS [Year],
  MONTH (ISNULL(s.DispatchDate, s.CreatedAt))                       AS MonthNumber,
  DATENAME(MONTH, ISNULL(s.DispatchDate, s.CreatedAt))              AS MonthName,
  s.SetId,
  s.SetCode,
  CONVERT(date, ISNULL(s.DispatchDate, s.CreatedAt))               AS DateDispatched,
  emp.Name                                                          AS EmployeeName,
  ROW_NUMBER() OVER (PARTITION BY s.SetCode ORDER BY r.ReqId)       AS ItemNo,
  CAST(-r.ReqId AS INT)                                             AS SetItemId,
  i.[Name]                                                          AS ItemName,
  ISNULL(i.ModelNumber,  '')                                        AS ModelNumber,
  ISNULL(i.SerialNumber, '')                                        AS SerialNumber,
  i.ItemType,
  ISNULL(i.Category,     '')                                        AS Category,
  ISNULL(r.Quantity,     0)                                         AS Quantity,
  ISNULL(r.UnitPrice,    0)                                         AS Amount,
  CASE WHEN dist.Name IS NOT NULL
       THEN COALESCE(emp_co.Name, req_co.Name) + ' / ' + dist.Name
       ELSE COALESCE(emp_co.Name, req_co.Name) END                 AS CompanyName,
  COALESCE(emp_br.Name, req_br.Name)                                AS BranchName,
  COALESCE(emp_dp.Name, req_dp.Name)                                AS DepartmentName
FROM dbo.[Set]      s
INNER JOIN dbo.Request r ON r.SetId  = s.SetId
INNER JOIN dbo.Item    i ON i.ItemId = r.ItemId
LEFT  JOIN dbo.Employee   emp    ON emp.EmpId       = COALESCE(s.ReceivedById, r.EmpId)
LEFT  JOIN dbo.Branch     emp_br ON emp_br.BranchId = emp.BranchId
LEFT  JOIN dbo.Department emp_dp ON emp_dp.DeptId   = emp.DeptId
LEFT  JOIN dbo.Company    emp_co ON emp_co.ComId    = emp.ComId
LEFT  JOIN dbo.Company    req_co ON req_co.ComId    = r.ComId
LEFT  JOIN dbo.Department req_dp ON req_dp.DeptId   = r.DeptId
LEFT  JOIN dbo.Branch     req_br ON req_br.BranchId = r.BranchId
LEFT  JOIN dbo.Distributor dist  ON dist.DistributorId = s.DistributorId
WHERE s.SetId IN (" + idList + @")
  AND ISNULL(i.Category, '') <> 'Cartridge'
  AND NOT EXISTS (SELECT 1 FROM dbo.SetItem si WHERE si.SetId = s.SetId)

ORDER BY [Year] ASC, MonthNumber ASC, SetId ASC, ItemNo ASC";

            return FillDataTable(sql, "SetsData");
        }

        /// <summary>Opens the Sets report viewer filtered to the specified SetIds.</summary>
        public static void ShowSetsReport(IEnumerable<int> setIds)
        {
            var sigRow = ShowSetsSignatoryPickers();
            if (sigRow == null) return;
            var colParams = ShowColumnSelection("Sets Report", ReportColumnSelectionDialog.SetsReportColumns());
            if (colParams == null) return;

            var setsData = LoadSetsData(setIds);
            if (setsData.Rows.Count == 0)
            {
                MessageBox.Show(
                    "The selected set(s) have no reportable items.\n\n" +
                    "Cartridge items are excluded (they have their own report), and a set with no " +
                    "items at all has nothing to list.",
                    "Sets Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var rdlcBytes = BuildSetsRdlcBytes(colParams);
            var sources = new List<(string, DataTable)>
            {
                ("SetsData",   setsData),
                ("SigRowData", sigRow)
            };

            var form = new ReportViewerForm(rdlcBytes, "Sets Report (Selected)", sources);
            if (colParams != null && colParams.Length > 0)
                form.SetReportParameters(colParams);
            form?.ShowDialog();
        }

        // Column definitions for SetsReport. Weights proportional to original landscape widths,
        // scaled so visible columns always fill exactly 190mm in portrait.
        // Original landscape widths (352mm total): 22|30|8|50|28|28|20|25|10|25|22|28|28|28
        private static readonly (string ShowParam, int Weight)[] _setsColDefs =
        {
            ("ShowSetCode",        12),   // 0  — was 22mm
            ("ShowEmployee",       16),   // 1  — was 30mm
            ("ShowRowNum",          4),   // 2  — was 8mm
            ("ShowItemName",       27),   // 3  — was 50mm
            ("ShowModel",          15),   // 4  — was 28mm
            ("ShowSerial",         15),   // 5  — was 28mm
            ("ShowItemType",       11),   // 6  — was 20mm
            ("ShowCategory",       14),   // 7  — was 25mm
            ("ShowQty",             5),   // 8  — was 10mm
            ("ShowDateDispatched", 14),   // 9  — was 25mm
            ("ShowAmount",         12),   // 10 — was 22mm
            ("ShowCompany",        15),   // 11 — was 28mm
            ("ShowBranch",         15),   // 12 — was 28mm
            ("ShowDepartment",     15),   // 13 — was 28mm
        };

        public static byte[] BuildSetsRdlcBytes(ReportParameter[] columnParams)
        {
            int totalWeight = 0;
            foreach (var cd in _setsColDefs)
                if (IsGroupedColVisible(cd.ShowParam, columnParams))
                    totalWeight += cd.Weight;
            if (totalWeight == 0) totalWeight = 190;

            double scale = 190.0 / totalWeight;

            string Scaled(bool vis, int weight) =>
                string.Format("{0:F2}mm", vis ? weight * scale : (double)weight);

            string xml = System.IO.File.ReadAllText(ReportPath("SetsReport.rdlc"), System.Text.Encoding.UTF8);
            for (int i = 0; i < _setsColDefs.Length; i++)
            {
                bool vis = IsGroupedColVisible(_setsColDefs[i].ShowParam, columnParams);
                xml = xml.Replace($"##COLW_{i}##", Scaled(vis, _setsColDefs[i].Weight));
            }

            return System.Text.Encoding.UTF8.GetBytes(xml);
        }

        // ════════════════════════════════════════════════════════════════════
        // SUB-TYPE GROUP COLUMNS (shared by Invoice / Renewals / Renewals Grouped)
        // ════════════════════════════════════════════════════════════════════

        // Sub-Type Groups (Contract / Subscription / License / Services, each with its own
        // ReferenceCode + Begin/End date) are the printed equivalent of the on-screen group
        // cards in ViewInvoiceDetailPage and the Renewals workspace. Every report dataset gets
        // the same six columns appended so the RDLC can render a per-group banner and subtotal.
        //
        // GroupSubtotal deliberately prefers g.SubtotalOverride over the aggregated member
        // amounts — that is exactly the precedence the group cards use, so a printed subtotal
        // always matches what the user edited on screen rather than silently recomputing it.
        // Rows with no group come back as SubTypeGroupId = 0 / SubType = '' rather than NULL,
        // so the RDLC visibility expressions stay simple and ungrouped items still print.
        private const string SubTypeGroupSelectSql = @"
  ISNULL(g.GroupId, 0)                              AS SubTypeGroupId,
  ISNULL(g.SubType, '')                             AS SubType,
  ISNULL(g.ReferenceCode, '')                       AS GroupReferenceCode,
  g.BeginDate                                       AS GroupBeginDate,
  g.EndDate                                         AS GroupEndDate,
  ISNULL(g.SubtotalOverride, ISNULL(g.Subtotal, 0)) AS GroupSubtotal";

        // For datasets whose base view already exposes SetItemId (aliased 'v'): hop through
        // dbo.SetItem to reach GroupId, then to the aggregated group view.
        private const string SubTypeGroupJoinBySetItemSql = @"
LEFT JOIN dbo.SetItem                 sitm ON sitm.SetItemId = v.SetItemId
LEFT JOIN dbo.vw_SetItemSubTypeGroups g    ON g.GroupId      = sitm.GroupId";

        // vw_InvoiceItems has no SetItemId to join on (its two UNION ALL branches are keyed
        // differently), so the group is resolved by SetId + ItemId instead. The inner GROUP BY
        // collapses that to at most one row per pair, which is what keeps this a lookup and not
        // a fan-out: without it, an item listed twice in the same set under two different groups
        // would duplicate every invoice line it appears on.
        private const string SubTypeGroupJoinBySetItemKeySql = @"
LEFT JOIN (
    SELECT sx.SetId, sx.ItemId, MIN(sx.GroupId) AS GroupId
    FROM dbo.SetItem sx
    WHERE sx.GroupId IS NOT NULL
    GROUP BY sx.SetId, sx.ItemId
) sig                                 ON sig.SetId = v.SetId AND sig.ItemId = v.ItemId
LEFT JOIN dbo.vw_SetItemSubTypeGroups g ON g.GroupId = sig.GroupId";

        // Parent Tag Groups (free-text label, e.g. "Cisco") are an independent, orthogonal
        // grouping from Sub-Type — an item can belong to both at once, and Parent Tag has no
        // date range / financial override columns of its own (see
        // dbo.vw_SetItemParentTagGroups). The printed banner shows only the label.
        private const string ParentTagGroupSelectSql = @"
  ISNULL(pg.ParentTagGroupId, 0) AS ParentTagGroupId,
  ISNULL(pg.Label, '')           AS ParentTagLabel";

        // For datasets whose base view already exposes SetItemId (aliased 'v'): hop through
        // dbo.SetItem to reach ParentTagGroupId, then to the aggregated group view. Reuses the
        // `sitm` alias already joined by SubTypeGroupJoinBySetItemSql.
        private const string ParentTagGroupJoinBySetItemSql = @"
LEFT JOIN dbo.vw_SetItemParentTagGroups pg ON pg.ParentTagGroupId = sitm.ParentTagGroupId";

        // vw_InvoiceItems has no SetItemId (see SubTypeGroupJoinBySetItemKeySql above for why) —
        // resolved by SetId + ItemId instead, same MIN()-collapsing rationale.
        private const string ParentTagGroupJoinBySetItemKeySql = @"
LEFT JOIN (
    SELECT sx.SetId, sx.ItemId, MIN(sx.ParentTagGroupId) AS ParentTagGroupId
    FROM dbo.SetItem sx
    WHERE sx.ParentTagGroupId IS NOT NULL
    GROUP BY sx.SetId, sx.ItemId
) pig                                    ON pig.SetId = v.SetId AND pig.ItemId = v.ItemId
LEFT JOIN dbo.vw_SetItemParentTagGroups pg ON pg.ParentTagGroupId = pig.ParentTagGroupId";

        // "Recipient" column for the Invoice Report — one string matching how ViewInvoiceDetailPage
        // reads the recipient: the employee (only when the invoice has an employee-level requester,
        // dbo.[Set].InvoiceRequesterEmpId) followed by Company - Department - Branch. STUFF() drops
        // the leading separator; falls back to the Set's own composed Site text when no org row
        // resolves. vw_InvoiceItems already exposes v.SetId.
        private const string RecipientSelectSql = @"
  LTRIM(RTRIM(
    ISNULL(NULLIF(recEmp.Name, '') + ' - ', '')
    + COALESCE(
        NULLIF(STUFF(
              ISNULL(' - ' + NULLIF(recCo.Name, ''), '')
            + ISNULL(' - ' + NULLIF(recDp.Name, ''), '')
            + ISNULL(' - ' + NULLIF(recBr.Name, ''), ''),
          1, 3, ''), ''),
        v.Site)
  ))                                       AS Recipient,";

        private const string RecipientJoinSql = @"
LEFT JOIN dbo.[Set]      recSet ON recSet.SetId  = v.SetId
LEFT JOIN dbo.Employee   recEmp ON recEmp.EmpId  = recSet.InvoiceRequesterEmpId
LEFT JOIN dbo.Company    recCo  ON recCo.ComId   = recSet.ComId
LEFT JOIN dbo.Branch     recBr  ON recBr.BranchId = recSet.CurrentBranchId
LEFT JOIN dbo.Department recDp  ON recDp.DeptId  = recSet.CurrentDepartmentId";

        // ════════════════════════════════════════════════════════════════════
        // 2. RENEWALS REPORT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads data for the Renewals report.
        /// <param name="expiryFilter">"Active" — excludes Expired rows;
        /// "Expired" — only Expired rows; anything else (or null) — all rows.</param>
        /// </summary>
        public static DataTable LoadRenewalsData(string expiryFilter = null, bool chainOrderDescending = false, int previewTop = 0)
        {
            string whereClause;
            if (expiryFilter == "Active")       whereClause = "AND v.ExpiryStatus <> 'Expired'";
            else if (expiryFilter == "Expired") whereClause = "AND v.ExpiryStatus = 'Expired'";
            else                                whereClause = "";

            // ChainSortKey is a pre-computed, monotonically increasing sequence that already
            // encodes the desired display order (including chain direction) — the RDLC's
            // Year/Month/Set groups all sort by this one plain static field instead of trying
            // to flip direction dynamically at render time, which the LocalReport engine
            // doesn't support reliably. Year/Month/SetId are flipped TOGETHER so "Renewal N ->
            // Original" actually reorders the whole document (most-recent-first) instead of
            // only breaking ties between sets that happen to share the same Year/Month.
            string dir = chainOrderDescending ? "DESC" : "ASC";
            string sql = @"
SELECT" + TopClause(previewTop) + @" *, ROW_NUMBER() OVER (ORDER BY [Year] " + dir + ", MonthNumber " + dir + ", SetId " + dir + @", ItemNo ASC) AS ChainSortKey
FROM (
SELECT
  v.[Year], v.MonthNumber, v.MonthName,
  v.SetId, v.SetCode, v.SetType, v.RenewalOfSetId, v.ChainPositionLabel, v.DocumentDate,
  v.DocumentNumber, v.CompanyName, v.SiteName,
  v.ExpiryStatus, v.SetLevelStatus,
  v.StartDate, v.EndDate, v.DaysUntilExpiry,
  v.TotalAmountDue, v.Subtotal, v.VatAmount, v.WhtAmount, v.DiscountAmount, v.ItemNo,
  v.SetItemId, v.ItemName, v.ItemDescription, v.ModelNumber, v.SerialNumber, v.ItemType, v.Category,
  v.Quantity, v.UnitPrice, v.ItemAmount,
  v.LineStartDate, v.LineEndDate, v.ItemRenewalStatus," + SubTypeGroupSelectSql + "," + ParentTagGroupSelectSql + @"
FROM dbo.vw_Report_RenewalsWithItems v" + SubTypeGroupJoinBySetItemSql + ParentTagGroupJoinBySetItemSql + @"
WHERE 1=1 " + whereClause + @"
) t
ORDER BY " + PreviewGroupedFirst(previewTop, "SubTypeGroupId = 0") + @"ChainSortKey";

            return FillDataTable(sql, "RenewalsData");
        }

        public static void ShowRenewalsReport(string filter, bool showChainLabels = true, bool chainOrderDescending = false)
        {
            var sigData = ShowSignatoryPicker();
            if (sigData == null) return;
            var colParams = ShowColumnSelection("Renewals Report", ReportColumnSelectionDialog.RenewalsReportColumns());
            if (colParams == null) return;
            colParams = AppendChainParams(colParams, showChainLabels, chainOrderDescending);
            var form = CreateRenewalsReportForm(filter, sigData, colParams, chainOrderDescending);
            form?.ShowDialog();
        }

        /// <summary>
        /// Appends the ShowChainLabels/ChainOrderDescending report parameters (Original/Renewal #N
        /// banner visibility + sort order) onto an existing column-selection ReportParameter array.
        /// </summary>
        private static ReportParameter[] AppendChainParams(ReportParameter[] columnParams, bool showChainLabels, bool chainOrderDescending)
        {
            var list = new List<ReportParameter>(columnParams ?? Array.Empty<ReportParameter>())
            {
                new ReportParameter("ShowChainLabels", showChainLabels.ToString()),
                new ReportParameter("ChainOrderDescending", chainOrderDescending.ToString())
            };
            return list.ToArray();
        }

        public static ReportViewerForm CreateRenewalsReportForm(string filter, DataTable signatoryData = null, ReportParameter[] columnParams = null, bool chainOrderDescending = false)
        {
            string title;
            if (filter == "Active")       title = "Renewals Report \u2014 Active Only";
            else if (filter == "Expired") title = "Renewals Report \u2014 Expired Only";
            else                          title = "Renewals Report";

            var rdlcBytes = BuildRenewalsRdlcBytes(columnParams);
            return CreateReportFormFromBytes(rdlcBytes, "RenewalsData", LoadRenewalsData(filter, chainOrderDescending), title, signatoryData, columnParams);
        }

        /// <summary>
        /// Shows the filter dialog (expiry + chain label/order options), then the signatory
        /// picker, then opens the Renewals report viewer.
        /// </summary>
        public static void ShowRenewalsReport()
        {
            using (var dialog = new RenewalReportFilterDialog(showExpiryFilter: true, showChainOptions: true))
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                ShowRenewalsReport(dialog.SelectedFilter, dialog.ShowChainLabels, dialog.ChainOrderDescending);
            }
        }

        /// <summary>
        /// Loads Renewals report data filtered to the specified SetIds.
        /// </summary>
        /// <param name="expandChain">
        /// True (default) walks each selected set out to its whole renewal chain. False reports
        /// ONLY the sets that were ticked — what the dialog's "Print selected only" option asks for.
        /// </param>
        public static DataTable LoadRenewalsData(IEnumerable<int> setIds, string expiryFilter = null,
                                                 bool chainOrderDescending = false, bool expandChain = true)
        {
            var idSet = new HashSet<int>(setIds ?? Enumerable.Empty<int>());
            if (idSet.Count == 0)
                return LoadRenewalsData(expiryFilter);

            if (!expandChain)
                return LoadRenewalsForExactSets(idSet, expiryFilter, chainOrderDescending);

            // A selected set can itself be a renewal, part of a longer chain. Walk up to each
            // selected set's chain root, then back down through every descendant, so the report
            // shows every "Renewal #N" table in the chain — not just the one the user picked.
            var selectedIdList = string.Join(",", idSet);
            var chainRows = FillDataTable($@"
;WITH UpChain AS (
    SELECT SetId, RenewalOfSetId FROM dbo.[Set] WHERE SetId IN ({selectedIdList})
    UNION ALL
    SELECT p.SetId, p.RenewalOfSetId FROM dbo.[Set] p INNER JOIN UpChain u ON p.SetId = u.RenewalOfSetId
),
Roots AS (
    SELECT DISTINCT SetId FROM UpChain WHERE RenewalOfSetId IS NULL
),
FullChain AS (
    SELECT SetId FROM Roots
    UNION ALL
    SELECT s.SetId FROM dbo.[Set] s INNER JOIN FullChain fc ON s.RenewalOfSetId = fc.SetId
)
SELECT SetId FROM FullChain", "ChainIds");

            idSet.Clear();
            foreach (DataRow row in chainRows.Rows)
                idSet.Add(Convert.ToInt32(row["SetId"]));

            return LoadRenewalsForExactSets(idSet, expiryFilter, chainOrderDescending);
        }

        /// <summary>
        /// Loads Renewals rows for EXACTLY the given SetIds — no chain walking. Shared by the
        /// chain-expanded path (which passes the resolved chain) and by "Print selected only"
        /// (which passes just the ticked rows), so the projection lives in one place.
        /// </summary>
        private static DataTable LoadRenewalsForExactSets(HashSet<int> idSet, string expiryFilter,
                                                          bool chainOrderDescending)
        {
            if (idSet == null || idSet.Count == 0)
                return LoadRenewalsData(expiryFilter);

            var idList = string.Join(",", idSet);

            string whereClause = $"AND v.SetId IN ({idList})";
            if (expiryFilter == "Active")       whereClause += " AND v.ExpiryStatus <> 'Expired'";
            else if (expiryFilter == "Expired") whereClause += " AND v.ExpiryStatus = 'Expired'";

            string dir = chainOrderDescending ? "DESC" : "ASC";
            string sql = @"
SELECT *, ROW_NUMBER() OVER (ORDER BY [Year] " + dir + ", MonthNumber " + dir + ", SetId " + dir + @", ItemNo ASC) AS ChainSortKey
FROM (
SELECT
  v.[Year], v.MonthNumber, v.MonthName,
  v.SetId, v.SetCode, v.SetType, v.RenewalOfSetId, v.ChainPositionLabel, v.DocumentDate,
  v.DocumentNumber, v.CompanyName, v.SiteName,
  v.ExpiryStatus, v.SetLevelStatus,
  v.StartDate, v.EndDate, v.DaysUntilExpiry,
  v.TotalAmountDue, v.Subtotal, v.VatAmount, v.WhtAmount, v.DiscountAmount, v.ItemNo,
  v.SetItemId, v.ItemName, v.ItemDescription, v.ModelNumber, v.SerialNumber, v.ItemType, v.Category,
  v.Quantity, v.UnitPrice, v.ItemAmount,
  v.LineStartDate, v.LineEndDate, v.ItemRenewalStatus," + SubTypeGroupSelectSql + "," + ParentTagGroupSelectSql + @"
FROM dbo.vw_Report_RenewalsWithItems v" + SubTypeGroupJoinBySetItemSql + ParentTagGroupJoinBySetItemSql + @"
WHERE 1=1 " + whereClause + @"
) t
ORDER BY ChainSortKey";

            return FillDataTable(sql, "RenewalsData");
        }

        /// <summary>
        /// Opens the Renewals report viewer filtered to the specified SetIds.
        /// </summary>
        public static void ShowRenewalsReport(IEnumerable<int> setIds, string filter)
        {
            var ids = (setIds ?? Enumerable.Empty<int>()).ToList();

            // The Active/Expired filter is already fixed by the caller for this flow, so only
            // ask about chain label visibility/order (and selection scope) here.
            bool showChainLabels = true;
            bool chainOrderDescending = false;
            bool selectedOnly = false;
            using (var chainDialog = new RenewalReportFilterDialog(
                       showExpiryFilter: false, showChainOptions: true, selectedCount: ids.Count))
            {
                if (chainDialog.ShowDialog() != DialogResult.OK)
                    return;
                showChainLabels = chainDialog.ShowChainLabels;
                chainOrderDescending = chainDialog.ChainOrderDescending;
                selectedOnly = chainDialog.PrintSelectedOnly;
            }

            var sigData = ShowSignatoryPicker();
            if (sigData == null) return;
            var colParams = ShowColumnSelection("Renewals Report", ReportColumnSelectionDialog.RenewalsReportColumns());
            if (colParams == null) return;
            colParams = AppendChainParams(colParams, showChainLabels, chainOrderDescending);

            string scope = selectedOnly ? " \u2014 Selected Rows Only" : "";
            string title;
            if (filter == "Active")       title = "Renewals Report (Selected)" + scope + " \u2014 Active Only";
            else if (filter == "Expired") title = "Renewals Report (Selected)" + scope + " \u2014 Expired Only";
            else                          title = "Renewals Report (Selected)" + scope;

            var data = LoadRenewalsData(ids, filter, chainOrderDescending, expandChain: !selectedOnly);

            var rdlcBytes = BuildRenewalsRdlcBytes(colParams);
            var form = CreateReportFormFromBytes(rdlcBytes, "RenewalsData", data, title, sigData, colParams);
            form?.ShowDialog();
        }

        // ════════════════════════════════════════════════════════════════════
        // 3. RENEWALS GROUPED REPORT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Loads data for the Renewals (Grouped) report — latest set per chain only.</summary>
        public static DataTable LoadRenewalGroupsData(string expiryFilter = null, bool chainOrderDescending = false, int previewTop = 0)
        {
            string whereClause;
            if (expiryFilter == "Active")       whereClause = "AND v.ExpiryStatus <> 'Expired'";
            else if (expiryFilter == "Expired") whereClause = "AND v.ExpiryStatus = 'Expired'";
            else                                whereClause = "";

            string dir = chainOrderDescending ? "DESC" : "ASC";
            string sql = @"
SELECT" + TopClause(previewTop) + @" *, ROW_NUMBER() OVER (ORDER BY [Year] " + dir + ", MonthNumber " + dir + ", SetId " + dir + @", ItemNo ASC) AS ChainSortKey
FROM (
SELECT
  v.[Year], v.MonthNumber, v.MonthName,
  v.RootSetId, v.RootSetCode, v.RenewalCount,
  v.SetId, v.SetCode, CASE WHEN v.RenewalCount = 0 THEN 'Original' ELSE 'Renewal #' + CAST(v.RenewalCount AS VARCHAR(10)) END AS ChainPositionLabel, v.SetType, v.DocumentDate,
  v.DocumentNumber, v.RenewedFromSetCode,
  v.CompanyName, v.SiteName,
  v.ExpiryStatus, v.SetLevelStatus,
  v.StartDate, v.EndDate, v.DaysUntilExpiry,
  v.TotalAmountDue, v.Subtotal, v.VatAmount, v.WhtAmount, v.DiscountAmount, v.ItemNo,
  v.SetItemId, v.ItemName, v.ItemDescription, v.ModelNumber, v.SerialNumber, v.ItemType, v.Category,
  v.Quantity, v.ItemAmount, v.LineStartDate, v.LineEndDate, v.ItemRenewalStatus," + SubTypeGroupSelectSql + "," + ParentTagGroupSelectSql + @"
FROM dbo.vw_Report_RenewalGroupsLatest v" + SubTypeGroupJoinBySetItemSql + ParentTagGroupJoinBySetItemSql + @"
WHERE 1=1 " + whereClause + @"
) t
ORDER BY " + PreviewGroupedFirst(previewTop, "SubTypeGroupId = 0") + @"ChainSortKey";

            return FillDataTable(sql, "RenewalGroupsData");
        }

        public static void ShowRenewalGroupsReport(string filter)
        {
            var sigData = ShowSignatoryPicker();
            if (sigData == null) return;
            var colParams = ShowColumnSelection("Renewals Report (Grouped)", ReportColumnSelectionDialog.RenewalsGroupedReportColumns());
            if (colParams == null) return;
            var form = CreateRenewalGroupsReportForm(filter, sigData, colParams);
            form?.ShowDialog();
        }

        public static ReportViewerForm CreateRenewalGroupsReportForm(string filter, DataTable signatoryData = null, ReportParameter[] columnParams = null)
        {
            string title;
            if (filter == "Active")       title = "Renewals Report (Grouped) \u2014 Active Only";
            else if (filter == "Expired") title = "Renewals Report (Grouped) \u2014 Expired Only";
            else                          title = "Renewals Report (Grouped)";

            var rdlcBytes = BuildGroupedRdlcBytes(columnParams);
            return CreateReportFormFromBytes(rdlcBytes, "RenewalGroupsData", LoadRenewalGroupsData(filter), title, signatoryData, columnParams);
        }

        /// <summary>Shows filter dialog, then signatory picker, then opens the Renewals (Grouped) report viewer.</summary>
        public static void ShowRenewalGroupsReport()
        {
            using (var dialog = new RenewalReportFilterDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                string filter = dialog.SelectedFilter;
                ShowRenewalGroupsReport(filter);
            }
        }

        /// <summary>Loads data for the Renewals (Grouped) report filtered to the specified RootSetIds.</summary>
        public static DataTable LoadRenewalGroupsData(IEnumerable<int> rootSetIds, string expiryFilter = null, bool chainOrderDescending = false)
        {
            var idList = string.Join(",", rootSetIds);
            if (string.IsNullOrEmpty(idList))
                return LoadRenewalGroupsData(expiryFilter, chainOrderDescending);

            string whereClause = $"AND v.RootSetId IN ({idList})";
            if (expiryFilter == "Active")       whereClause += " AND v.ExpiryStatus <> 'Expired'";
            else if (expiryFilter == "Expired") whereClause += " AND v.ExpiryStatus = 'Expired'";

            string dir = chainOrderDescending ? "DESC" : "ASC";
            string sql = @"
SELECT *, ROW_NUMBER() OVER (ORDER BY [Year] " + dir + ", MonthNumber " + dir + ", SetId " + dir + @", ItemNo ASC) AS ChainSortKey
FROM (
SELECT
  v.[Year], v.MonthNumber, v.MonthName,
  v.RootSetId, v.RootSetCode, v.RenewalCount,
  v.SetId, v.SetCode, CASE WHEN v.RenewalCount = 0 THEN 'Original' ELSE 'Renewal #' + CAST(v.RenewalCount AS VARCHAR(10)) END AS ChainPositionLabel, v.SetType, v.DocumentDate,
  v.DocumentNumber, v.RenewedFromSetCode,
  v.CompanyName, v.SiteName,
  v.ExpiryStatus, v.SetLevelStatus,
  v.StartDate, v.EndDate, v.DaysUntilExpiry,
  v.TotalAmountDue, v.Subtotal, v.VatAmount, v.WhtAmount, v.DiscountAmount, v.ItemNo,
  v.SetItemId, v.ItemName, v.ItemDescription, v.ModelNumber, v.SerialNumber, v.ItemType, v.Category,
  v.Quantity, v.ItemAmount, v.LineStartDate, v.LineEndDate, v.ItemRenewalStatus," + SubTypeGroupSelectSql + "," + ParentTagGroupSelectSql + @"
FROM dbo.vw_Report_RenewalGroupsLatest v" + SubTypeGroupJoinBySetItemSql + ParentTagGroupJoinBySetItemSql + @"
WHERE 1=1 " + whereClause + @"
) t
ORDER BY ChainSortKey";

            return FillDataTable(sql, "RenewalGroupsData");
        }

        /// <summary>Opens the Renewals (Grouped) report viewer filtered to the specified RootSetIds.</summary>
        public static void ShowRenewalGroupsReport(IEnumerable<int> rootSetIds, string filter)
        {
            var sigData = ShowSignatoryPicker();
            if (sigData == null) return;
            var colParams = ShowColumnSelection("Renewals Report (Grouped)", ReportColumnSelectionDialog.RenewalsGroupedReportColumns());
            if (colParams == null) return;

            string title;
            if (filter == "Active")       title = "Renewals Report (Grouped, Selected) \u2014 Active Only";
            else if (filter == "Expired") title = "Renewals Report (Grouped, Selected) \u2014 Expired Only";
            else                          title = "Renewals Report (Grouped, Selected)";

            var rdlcBytes = BuildGroupedRdlcBytes(colParams);
            var form = CreateReportFormFromBytes(rdlcBytes, "RenewalGroupsData", LoadRenewalGroupsData(rootSetIds, filter), title, sigData, colParams);
            form?.ShowDialog();
        }

        // ── Full-chain overloads (includeOriginalSets = true) ────────────────

        /// <summary>
        /// Loads ALL sets in every renewal chain (not just the latest).
        /// Each set gets its own ChainPositionLabel ("Renewal #1", "Renewal #2", ...) so the
        /// RDLC report can render every set in the chain as its own visually separated
        /// table instead of one merged block.
        /// Year/Month grouping (and banner display) is each set's OWN document date —
        /// NOT anchored to the chain's latest set — so a set always sorts and prints
        /// under the Year/Month it actually happened in, even when other sets in the
        /// same chain fall under a different Year/Month.
        /// The expiryFilter is still applied to the LATEST set of each chain so that
        /// superseded/historical sets are never filtered out individually.
        /// </summary>
        public static DataTable LoadRenewalGroupsDataWithChain(string expiryFilter = null, bool chainOrderDescending = false, IEnumerable<int> excludeSetIds = null)
        {
            string chainFilter = BuildChainExpiryFilter(expiryFilter, useAnd: true);
            string sql = BuildFullChainSql(rootIdsClause: "", expiryClause: chainFilter, chainOrderDescending: chainOrderDescending, excludeSetIds: excludeSetIds);
            return FillDataTable(sql, "RenewalGroupsData");
        }

        /// <summary>Filtered to specific RootSetIds — full chain version.</summary>
        public static DataTable LoadRenewalGroupsDataWithChain(IEnumerable<int> rootSetIds, string expiryFilter = null, bool chainOrderDescending = false, IEnumerable<int> excludeSetIds = null)
        {
            var idList = string.Join(",", rootSetIds ?? Enumerable.Empty<int>());
            if (string.IsNullOrEmpty(idList))
                return LoadRenewalGroupsDataWithChain(expiryFilter, chainOrderDescending, excludeSetIds);

            string rootClause  = $"AND rc.RootSetId IN ({idList})";
            string chainFilter = BuildChainExpiryFilter(expiryFilter, useAnd: true);
            string sql         = BuildFullChainSql(rootIdsClause: rootClause, expiryClause: chainFilter, chainOrderDescending: chainOrderDescending, excludeSetIds: excludeSetIds);
            return FillDataTable(sql, "RenewalGroupsData");
        }

        /// <summary>
        /// Returns the correct DataTable based on includeOriginalSets and whether specific chains
        /// were picked. Per-row checkboxes always win: once the user has selected specific root
        /// chains (rootSetIds non-empty), the full chain is always loaded so excludeSetIds can
        /// filter it down to exactly the rows that were checked — the separate "Include Original
        /// Sets" toggle only governs the no-selection/global report, where there's no per-row
        /// checkbox state to honor.
        /// </summary>
        public static DataTable BuildRenewalChain(IEnumerable<int> rootSetIds, bool includeOriginalSets, string expiryFilter, bool chainOrderDescending = false, IEnumerable<int> excludeSetIds = null)
        {
            var ids = rootSetIds?.ToList();
            bool hasExplicitSelection = ids != null && ids.Count > 0;

            if (includeOriginalSets || hasExplicitSelection)
            {
                return hasExplicitSelection
                    ? LoadRenewalGroupsDataWithChain(ids, expiryFilter, chainOrderDescending, excludeSetIds)
                    : LoadRenewalGroupsDataWithChain(expiryFilter, chainOrderDescending, excludeSetIds);
            }
            else
            {
                return LoadRenewalGroupsData(expiryFilter, chainOrderDescending);
            }
        }

        /// <summary>
        /// Entry point for Generate Report with no pre-selected groups.
        /// Shows the expiry filter dialog, then signatory picker, then column selection,
        /// then opens the report viewer.
        /// </summary>
        public static void GenerateRenewalReport(bool includeOriginalSets)
        {
            using (var filterDlg = new RenewalReportFilterDialog())
            {
                if (filterDlg.ShowDialog() != DialogResult.OK) return;
                string filter = filterDlg.SelectedFilter;
                GenerateRenewalReport(null, filter, includeOriginalSets);
            }
        }

        /// <summary>
        /// Entry point for Generate Report with pre-selected RootSetIds.
        /// Shows a chain-options dialog (Original/Renewal #N label visibility + display order),
        /// then signatory picker and column selection, then opens the report viewer.
        /// Pass null/empty rootSetIds to include all chains. Pass excludeSetIds to leave out
        /// specific sets the user unchecked within an otherwise-included chain.
        /// </summary>
        public static void GenerateRenewalReport(IEnumerable<int> rootSetIds, string filter, bool includeOriginalSets, IEnumerable<int> excludeSetIds = null)
        {
            var ids       = rootSetIds?.ToList();
            bool hasIds   = ids != null && ids.Count > 0;

            bool showChainLabels = true;
            bool chainOrderDescending = false;
            bool selectedOnly = false;
            using (var chainDialog = new RenewalReportFilterDialog(
                       showExpiryFilter: false, showChainOptions: true,
                       selectedCount: hasIds ? ids.Count : 0))
            {
                if (chainDialog.ShowDialog() != DialogResult.OK)
                    return;
                showChainLabels = chainDialog.ShowChainLabels;
                chainOrderDescending = chainDialog.ChainOrderDescending;
                selectedOnly = chainDialog.PrintSelectedOnly;
            }

            var sigData   = ShowSignatoryPicker();
            if (sigData == null) return;

            var colParams = ShowColumnSelection("Renewals Report (Grouped)", ReportColumnSelectionDialog.RenewalsGroupedReportColumns());
            if (colParams == null) return;
            colParams = AppendChainParams(colParams, showChainLabels, chainOrderDescending);

            // "Print selected only" bypasses BuildRenewalChain entirely — that method always
            // expands to the full chain once an explicit selection exists, which is exactly what
            // this option is asking us not to do.
            var data      = selectedOnly && hasIds
                ? LoadRenewalGroupsData(ids, filter == "Both" ? null : filter, chainOrderDescending)
                : BuildRenewalChain(hasIds ? ids : null, includeOriginalSets, filter == "Both" ? null : filter, chainOrderDescending, excludeSetIds);

            // With the chain excluded the title must not claim "Full Chain".
            string title  = BuildRenewalGroupsTitle(filter, hasIds, !selectedOnly && (includeOriginalSets || hasIds))
                          + (selectedOnly ? " — Selected Rows Only" : "");

            var rdlcBytes = BuildGroupedRdlcBytes(colParams);
            var form = CreateReportFormFromBytes(rdlcBytes, "RenewalGroupsData", data, title, sigData, colParams);
            form?.ShowDialog();
        }

        private static string BuildRenewalGroupsTitle(string filter, bool isFiltered, bool fullChain)
        {
            string base_ = isFiltered ? "Renewals Report (Grouped, Selected)" : "Renewals Report (Grouped)";
            string chain = fullChain ? " — Full Chain" : "";
            if (filter == "Active")       return base_ + chain + " — Active Only";
            if (filter == "Expired")      return base_ + chain + " — Expired Only";
            return base_ + chain;
        }

        private static string BuildChainExpiryFilter(string expiryFilter, bool useAnd)
        {
            string prefix = useAnd ? "AND " : "";
            if (expiryFilter == "Active")  return prefix + "lsi.LatestExpiryStatus <> 'Expired'";
            if (expiryFilter == "Expired") return prefix + "lsi.LatestExpiryStatus = 'Expired'";
            return "";
        }

        private static string BuildFullChainSql(string rootIdsClause, string expiryClause, bool chainOrderDescending = false, IEnumerable<int> excludeSetIds = null)
        {
            string dir = chainOrderDescending ? "DESC" : "ASC";
            var excludeList = excludeSetIds?.ToList();
            string excludeClause = (excludeList != null && excludeList.Count > 0)
                ? $"AND s.SetId NOT IN ({string.Join(",", excludeList)})"
                : "";
            return @"
WITH RenewalChain AS
(
    SELECT s.SetId, s.SetId AS RootSetId, 0 AS ChainLevel
    FROM dbo.[Set] s
    WHERE s.RenewalOfSetId IS NULL
      AND s.SetType IN ('Software/License', 'Service', 'Services')

    UNION ALL

    SELECT s.SetId, rc.RootSetId, rc.ChainLevel + 1
    FROM dbo.[Set] s
    INNER JOIN RenewalChain rc ON s.RenewalOfSetId = rc.SetId
),
LatestPerChain AS
(
    SELECT RootSetId, MAX(ChainLevel) AS MaxLevel
    FROM RenewalChain
    GROUP BY RootSetId
),
LatestSetInfo AS
(
    SELECT
        rc.RootSetId,
        YEAR  (ISNULL(ls.DispatchDate, ls.CreatedAt))            AS [Year],
        MONTH (ISNULL(ls.DispatchDate, ls.CreatedAt))            AS MonthNumber,
        DATENAME(MONTH, ISNULL(ls.DispatchDate, ls.CreatedAt))   AS MonthName,
        CASE
            WHEN ls.EndDate IS NULL                              THEN 'No Expiry Date'
            WHEN ls.EndDate < GETDATE()                         THEN 'Expired'
            WHEN DATEDIFF(DAY, GETDATE(), ls.EndDate) <= 30     THEN 'Expiring Soon'
            WHEN DATEDIFF(DAY, GETDATE(), ls.EndDate) <= 90     THEN 'Warning'
            ELSE 'Active'
        END AS LatestExpiryStatus
    FROM LatestPerChain lpc
    INNER JOIN RenewalChain rc ON rc.RootSetId = lpc.RootSetId AND rc.ChainLevel = lpc.MaxLevel
    INNER JOIN dbo.[Set] ls ON ls.SetId = rc.SetId
)
SELECT
    YEAR  (ISNULL(s.StartDate, ISNULL(s.DispatchDate, s.CreatedAt)))                   AS [Year],
    MONTH (ISNULL(s.StartDate, ISNULL(s.DispatchDate, s.CreatedAt)))                   AS MonthNumber,
    DATENAME(MONTH, ISNULL(s.StartDate, ISNULL(s.DispatchDate, s.CreatedAt)))          AS MonthName,
    ROW_NUMBER() OVER (ORDER BY
        YEAR(ISNULL(s.StartDate, ISNULL(s.DispatchDate, s.CreatedAt))) " + dir + @",
        MONTH(ISNULL(s.StartDate, ISNULL(s.DispatchDate, s.CreatedAt))) " + dir + @",
        s.SetId " + dir + @",
        si.SetItemId ASC)                                         AS ChainSortKey,
    rc.RootSetId,
    root.SetCode                                                  AS RootSetCode,
    lpc.MaxLevel                                                  AS RenewalCount,
    s.SetId,
    s.SetCode                                                     AS SetCode,
    CASE WHEN rc.ChainLevel = 0 THEN 'Original'
         ELSE 'Renewal #' + CAST(rc.ChainLevel AS VARCHAR(10))
    END                                                            AS ChainPositionLabel,
    s.SetType,
    ISNULL(s.DispatchDate, s.CreatedAt)                           AS DocumentDate,
    ISNULL(s.DocumentNumber, '')                                  AS DocumentNumber,
    ISNULL(prev.SetCode, '—')                                     AS RenewedFromSetCode,
    ISNULL(com.Name, 'N/A')                                       AS CompanyName,
    ISNULL(s.Site, '')                                            AS SiteName,
    CASE
        WHEN s.EndDate IS NULL                              THEN 'No Expiry Date'
        WHEN s.EndDate < GETDATE()                         THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 30     THEN 'Expiring Soon'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 90     THEN 'Warning'
        ELSE 'Active'
    END                                                           AS ExpiryStatus,
    CASE
        WHEN (SELECT COUNT(*) FROM dbo.SetItem sx WHERE sx.SetId = s.SetId) = 0
            THEN 'No Items'
        WHEN (SELECT COUNT(*) FROM dbo.SetItem sx WHERE sx.SetId = s.SetId AND ISNULL(sx.RenewalStatus,'Active') = 'Active') = 0
         AND (SELECT COUNT(*) FROM dbo.SetItem sx WHERE sx.SetId = s.SetId AND sx.RenewalStatus = 'Renewed') > 0
            THEN 'Fully Renewed'
        WHEN (SELECT COUNT(*) FROM dbo.SetItem sx WHERE sx.SetId = s.SetId AND sx.RenewalStatus = 'Renewed') > 0
            THEN 'Partially Renewed'
        WHEN s.EndDate IS NULL                              THEN 'No Expiry Date'
        WHEN s.EndDate < GETDATE()                         THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 30     THEN 'Expiring Soon'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 90     THEN 'Warning'
        ELSE 'Active'
    END                                                           AS SetLevelStatus,
    s.StartDate,
    s.EndDate,
    CASE
        WHEN s.EndDate IS NULL THEN NULL
        ELSE DATEDIFF(DAY, GETDATE(), s.EndDate)
    END                                                           AS DaysUntilExpiry,
    ISNULL(s.TotalAmountDue,  0)                                  AS TotalAmountDue,
    ISNULL(s.Subtotal,        0)                                  AS Subtotal,
    ISNULL(s.VatAmount,       0)                                  AS VatAmount,
    ISNULL(s.WhtAmount,       0)                                  AS WhtAmount,
    ISNULL(s.DiscountAmount,  0)                                  AS DiscountAmount,
    ROW_NUMBER() OVER (
        PARTITION BY s.SetId
        ORDER BY si.SetItemId
    )                                                             AS ItemNo,
    si.SetItemId,
    i.[Name]                                                      AS ItemName,
    ISNULL(i.Description,    '')                                  AS ItemDescription,
    ISNULL(i.ModelNumber,  '')                                    AS ModelNumber,
    ISNULL(i.SerialNumber, '')                                    AS SerialNumber,
    i.ItemType,
    ISNULL(i.Category,     '')                                    AS Category,
    ISNULL(si.Quantity,    0)                                     AS Quantity,
    ISNULL(si.Amount,      0)                                     AS ItemAmount,
    si.LineStartDate,
    si.LineEndDate,
    ISNULL(si.RenewalStatus, 'Active')                            AS ItemRenewalStatus,
    ISNULL(g.GroupId, 0)                                          AS SubTypeGroupId,
    ISNULL(g.SubType, '')                                         AS SubType,
    ISNULL(g.ReferenceCode, '')                                   AS GroupReferenceCode,
    g.BeginDate                                                   AS GroupBeginDate,
    g.EndDate                                                     AS GroupEndDate,
    ISNULL(g.SubtotalOverride, ISNULL(g.Subtotal, 0))             AS GroupSubtotal,
    ISNULL(pg.ParentTagGroupId, 0)                                AS ParentTagGroupId,
    ISNULL(pg.Label, '')                                          AS ParentTagLabel
FROM RenewalChain rc
INNER JOIN LatestPerChain lpc ON lpc.RootSetId = rc.RootSetId
INNER JOIN LatestSetInfo  lsi ON lsi.RootSetId = rc.RootSetId
INNER JOIN dbo.[Set]      s    ON s.SetId    = rc.SetId
INNER JOIN dbo.[Set]      root ON root.SetId = rc.RootSetId
LEFT  JOIN dbo.[Set]      prev ON prev.SetId = s.RenewalOfSetId
LEFT  JOIN dbo.Company    com  ON com.ComId  = s.ComId
INNER JOIN dbo.SetItem    si   ON si.SetId   = s.SetId
INNER JOIN dbo.Item       i    ON i.ItemId   = si.ItemId
LEFT  JOIN dbo.vw_SetItemSubTypeGroups g ON g.GroupId = si.GroupId
LEFT  JOIN dbo.vw_SetItemParentTagGroups pg ON pg.ParentTagGroupId = si.ParentTagGroupId
WHERE 1=1
  " + rootIdsClause + @"
  " + expiryClause + @"
  " + excludeClause + @"
ORDER BY ChainSortKey";
        }

        // ════════════════════════════════════════════════════════════════════
        // 4. INVOICE REPORT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Loads data for the Invoice report from vw_InvoiceItems.</summary>
        public static DataTable LoadInvoiceData(int previewTop = 0)
        {
            string sql = @"
SELECT" + TopClause(previewTop) + @"
  v.DocumentDate,
  v.SetCode,
  v.ItemId,
  v.Status,
  v.Subtotal,
  v.VatAmount,
  v.WhtAmount,
  v.DiscountAmount,
  v.TotalAmountDue,
  v.DocumentNumber,
  v.ReferenceNumber,
  v.StartDate,
  v.EndDate,
  v.Site,
  v.CompanyName," + RecipientSelectSql + @"
  v.Quantity,
  v.ItemDescription,
  v.ItemName,
  v.ModelNumber,
  v.LineTotal,
  v.Year,
  v.Month," + SubTypeGroupSelectSql + "," + ParentTagGroupSelectSql + @"
FROM vw_InvoiceItems v" + RecipientJoinSql + SubTypeGroupJoinBySetItemKeySql + ParentTagGroupJoinBySetItemKeySql + @"
ORDER BY " + PreviewGroupedFirst(previewTop, "g.GroupId IS NULL") + @"v.[Year] ASC, v.[Month] ASC, v.DocumentDate ASC, v.SetCode ASC, v.ItemName ASC";

            return FillDataTable(sql, "InvoiceData");
        }

        /// <summary>Opens the signatory picker, column selection, then the Invoice report viewer.</summary>
        public static void ShowInvoiceReport()
        {
            var sigData = ShowSignatoryPicker();
            if (sigData == null) return;
            var colParams = ShowColumnSelection("Invoice Report", ReportColumnSelectionDialog.InvoiceReportColumns());
            if (colParams == null) return;
            var form = CreateInvoiceReportForm(sigData, colParams);
            form?.ShowDialog();
        }

        public static ReportViewerForm CreateInvoiceReportForm(DataTable signatoryData = null, ReportParameter[] columnParams = null)
        {
            var rdlcBytes = BuildInvoiceRdlcBytes(columnParams);
            return CreateReportFormFromBytes(rdlcBytes, "InvoiceData", LoadInvoiceData(), "Invoice Report", signatoryData, columnParams);
        }

        /// <summary>Loads Invoice report data filtered to the specified SetCodes. An empty
        /// (but non-null) collection means the caller made an explicit selection that
        /// resolved to no matching codes — this must return zero rows, not every invoice.</summary>
        public static DataTable LoadInvoiceData(IEnumerable<string> setCodes)
        {
            var codeList = string.Join(",", setCodes.Select(c => $"'{c.Replace("'", "''")}'"));
            string whereClause = string.IsNullOrEmpty(codeList) ? "1 = 0" : "v.SetCode IN (" + codeList + ")";

            string sql = @"
SELECT
  v.DocumentDate,
  v.SetCode,
  v.ItemId,
  v.Status,
  v.Subtotal,
  v.VatAmount,
  v.WhtAmount,
  v.DiscountAmount,
  v.TotalAmountDue,
  v.DocumentNumber,
  v.ReferenceNumber,
  v.StartDate,
  v.EndDate,
  v.Site,
  v.CompanyName," + RecipientSelectSql + @"
  v.Quantity,
  v.ItemDescription,
  v.ItemName,
  v.ModelNumber,
  v.LineTotal,
  v.Year,
  v.Month," + SubTypeGroupSelectSql + "," + ParentTagGroupSelectSql + @"
FROM vw_InvoiceItems v" + RecipientJoinSql + SubTypeGroupJoinBySetItemKeySql + ParentTagGroupJoinBySetItemKeySql + @"
WHERE " + whereClause + @"
ORDER BY v.[Year] ASC, v.[Month] ASC, v.DocumentDate ASC, v.SetCode ASC, v.ItemName ASC";

            return FillDataTable(sql, "InvoiceData");
        }

        /// <summary>Opens the Invoice report viewer filtered to the specified SetCodes.</summary>
        public static void ShowInvoiceReport(IEnumerable<string> setCodes)
        {
            var sigData = ShowSignatoryPicker();
            if (sigData == null) return;
            var colParams = ShowColumnSelection("Invoice Report", ReportColumnSelectionDialog.InvoiceReportColumns());
            if (colParams == null) return;
            var rdlcBytes = BuildInvoiceRdlcBytes(colParams);
            var form = CreateReportFormFromBytes(
                rdlcBytes, "InvoiceData", LoadInvoiceData(setCodes), "Invoice Report (Selected)", sigData, colParams);
            form?.ShowDialog();
        }

        // ════════════════════════════════════════════════════════════════════
        // 5. CARTRIDGE DISPOSE/SOLD REPORT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads data for the Cartridge Dispose/Sold report.
        /// Reads directly from EmptyCartridge + VendorCartridgeBatch so that every
        /// finalized cartridge is included — even those without an ItemLifecycleDecision
        /// record (which vw_DisposedSoldItems would miss).
        /// </summary>
        public static DataTable LoadCartridgeDisposeSoldData()
        {
            const string sql = @"
SELECT
    -- DecisionId: use the linked decision if it exists, otherwise NULL
    ldc.DecisionId,

    -- Batch
    vcb.BatchId,

    -- Date: use batch close date (finalization timestamp)
    vcb.ClosedDate                          AS DecidedAt,

    -- Type: derive from batch purpose
    vcb.BatchPurpose                        AS DecisionTypeName,

    -- Cartridge quantity
    ec.Quantity,

    -- RecipientName / SaleAmount come from decision if present, else NULL
    d.RecipientName,
    d.SaleAmount,

    -- Remarks: prefer cartridge-level, fall back to batch remarks
    ISNULL(ec.Remarks, ISNULL(vcb.Remarks, ''))  AS Remarks,

    -- Item info (NULL when no linked Item)
    i.Name          AS ItemName,
    i.ModelNumber   AS ItemModelNumber,

    -- Category (NULL when no linked Item)
    cat.Name        AS CategoryName,

    -- Condition
    c.ConditionName,

    -- Who finalized (closed) the batch
    u.Name          AS DecidedByName,

    -- Cartridge model
    cm.ModelNumber  AS CartridgeModelNumber,
    cm.Brand        AS CartridgeBrand,

    -- Disposal company stored on the cartridge row
    ec.DisposalCompanyName,

    -- Vendor who received the batch
    v.VendorName

FROM dbo.EmptyCartridge ec
INNER JOIN dbo.VendorCartridgeBatch          vcb ON ec.VendorBatchId    = vcb.BatchId
INNER JOIN dbo.Vendor                        v   ON vcb.VendorId        = v.VendorID
LEFT  JOIN dbo.CartridgeModel                cm  ON ec.CartridgeModelId = cm.CartridgeModelId
LEFT  JOIN dbo.Condition                     c   ON ec.ConditionId      = c.ConditionID
LEFT  JOIN dbo.[User]                        u   ON vcb.ClosedBy        = u.UserId
LEFT  JOIN dbo.Item                          i   ON ec.SourceItemId     = i.ItemId
LEFT  JOIN dbo.ItemCategory                  cat ON i.CategoryId        = cat.CategoryId
LEFT  JOIN dbo.ItemLifecycleDecisionCartridge ldc ON ec.EmptyCartridgeId = ldc.EmptyCartridgeId
LEFT  JOIN dbo.ItemLifecycleDecision          d   ON ldc.DecisionId      = d.DecisionId

WHERE ec.Status IN ('Disposed', 'Sold')
  AND vcb.Status IN ('Disposed', 'Sold')
  AND vcb.BatchPurpose IN ('DISPOSE', 'SELL')

ORDER BY vcb.ClosedDate ASC, vcb.BatchId ASC, ec.EmptyCartridgeId ASC";

            return FillDataTable(sql, "DisposedSoldDataSet");
        }

        // Column definitions for CartridgeDisposeSold. Weights sum to 190 when all 7 are visible.
        private static readonly (string ShowParam, int Weight)[] _cartridgeDisposeSoldColDefs =
        {
            ("ShowBatchId",        17),   // 0
            ("ShowDate",           23),   // 1
            ("ShowType",           19),   // 2
            ("ShowCartridgeModel", 42),   // 3
            ("ShowQty",            11),   // 4
            ("ShowCondition",      25),   // 5
            ("ShowRecipient",      53),   // 6
        };

        public static byte[] BuildCartridgeDisposeSoldRdlcBytes(ReportParameter[] columnParams)
        {
            int totalWeight = 0;
            foreach (var cd in _cartridgeDisposeSoldColDefs)
                if (IsGroupedColVisible(cd.ShowParam, columnParams))
                    totalWeight += cd.Weight;
            if (totalWeight == 0) totalWeight = 190;

            double scale = 190.0 / totalWeight;

            string Scaled(bool vis, int weight) =>
                string.Format("{0:F2}mm", vis ? weight * scale : (double)weight);

            string xml = System.IO.File.ReadAllText(ReportPath("CartridgeDisposeSold.rdlc"), System.Text.Encoding.UTF8);
            for (int i = 0; i < _cartridgeDisposeSoldColDefs.Length; i++)
            {
                bool vis = IsGroupedColVisible(_cartridgeDisposeSoldColDefs[i].ShowParam, columnParams);
                xml = xml.Replace($"##COLW_{i}##", Scaled(vis, _cartridgeDisposeSoldColDefs[i].Weight));
            }

            return System.Text.Encoding.UTF8.GetBytes(xml);
        }

        /// <summary>Opens the signatory picker, column selection, then the Cartridge Dispose/Sold report viewer.</summary>
        public static void ShowCartridgeDisposeSoldReport()
        {
            var sigData = ShowSignatoryPicker();
            if (sigData == null) return;
            var colParams = ShowColumnSelection("Cartridge Dispose/Sold Report", ReportColumnSelectionDialog.CartridgeDisposeSoldReportColumns());
            if (colParams == null) return;
            var form = CreateCartridgeDisposeSoldReportForm(sigData, colParams);
            form?.ShowDialog();
        }

        public static ReportViewerForm CreateCartridgeDisposeSoldReportForm(DataTable signatoryData = null, ReportParameter[] columnParams = null)
        {
            var rdlcBytes = BuildCartridgeDisposeSoldRdlcBytes(columnParams);
            return CreateReportFormFromBytes(rdlcBytes, "DisposedSoldDataSet", LoadCartridgeDisposeSoldData(), "Cartridge Dispose/Sold Report", signatoryData, columnParams);
        }

        public static ReportViewerForm CreateCartridgeDisposeSoldReportForm(DataTable mainData, DataTable signatoryData, ReportParameter[] columnParams = null)
        {
            var rdlcBytes = BuildCartridgeDisposeSoldRdlcBytes(columnParams);
            return CreateReportFormFromBytes(rdlcBytes, "DisposedSoldDataSet", mainData, "Cartridge Dispose/Sold Report", signatoryData, columnParams);
        }

        // ════════════════════════════════════════════════════════════════════
        // 6. WARRANTY REPORT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Loads data for the Warranty report.</summary>
        public static DataTable LoadWarrantyData()
        {
            const string sql = @"
SELECT
  [Year], MonthNumber, MonthName,
  ItemId, ItemName, ItemType,
  SerialNumber, ModelNumber, VendorName,
  SetCode,
  WarrantyStartDate, WarrantyEndDate, WarrantyYears,
  DaysRemaining, WarrantyStatus
FROM dbo.vw_Report_Warranty
ORDER BY [Year] ASC, MonthNumber ASC, WarrantyEndDate ASC";

            return FillDataTable(sql, "WarrantyData");
        }

        /// <summary>Opens the Warranty report viewer form.</summary>
        public static void ShowWarrantyReport()
        {
            var sigData = ShowSignatoryPicker();
            if (sigData == null) return;
            var form = CreateReportFormWithSignatories("WarrantyReport.rdlc", "WarrantyData", LoadWarrantyData(), "Warranty Report", sigData);
            form?.ShowDialog();
        }

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Picks the best-fit Renewals RDLC template based on how many columns are visible.
        ///   1–7  visible → RenewalsReport_Wide.rdlc    (wider cols — extra space for Item Name &amp; Model)
        ///   8–11 visible → RenewalsReport.rdlc         (standard portrait widths)
        ///   12–14 visible → RenewalsReport_Normal.rdlc (moderate cols, tighter font)
        ///   15+  visible → RenewalsReport_Compact.rdlc (compact cols, all 15 fit portrait)
        /// </summary>
        // Params that toggle whole SUMMARY ROWS rather than a physical column. They must never
        // count toward the visible-column tally — that tally picks the RDLC variant and scales
        // the ##COLW_N## widths, and a row toggle occupies no width.
        private static readonly HashSet<string> _summaryRowParamNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ShowSubtotal", "ShowVat", "ShowWht", "ShowDiscount", "ShowTotalAmount", "ShowSubTypeGroups", "ShowParentTagGroups"
        };

        // Invoice Report column definitions. Indices match the physical TablixColumn order.
        // ShowAmount spans 2 physical columns (indices 11 and 12), both controlled by one param.
        // Weights are the Wide-layout base widths (mm) used for proportional scaling.
        private static readonly (string ShowParam, int Weight)[] _invoiceColDefs =
        {
            ("ShowDate",        21),   // 0
            ("ShowCompany",     24),   // 1
            ("ShowDocNum",      17),   // 2
            ("ShowRefNum",      17),   // 3
            ("ShowStatus",      13),   // 4
            ("ShowQty",         10),   // 5
            ("ShowItemName",    32),   // 6
            ("ShowDescription", 35),   // 7
            ("ShowModel",       17),   // 8
            ("ShowStartDate",   17),   // 9
            ("ShowEndDate",     17),   // 10
            // Amount spans these two on every row except the Sub-Type group line, which splits
            // them into a "Total:" label and the figure.
            // DO NOT narrow column 11: it carries the LABELS of the existing Subtotal / VAT / WHT
            // / Discount / Total Amount rows. Shrinking it to buy width for the group figure wraps
            // every one of those labels ("Subto tal", "Disco unt"). 17/17 is the original split and
            // holds real amounts (620,000.00) comfortably.
            ("ShowAmount",      17),   // 11 — summary-row labels; "Total:" on the group line
            ("ShowAmount",      17),   // 12 — summary-row values; the group figure
            ("ShowSite",        25),   // 13
        };

        // Patches the Invoice RDL at runtime: substitutes ##COLW_N## placeholders with mm values
        // scaled so visible columns always fill exactly 190mm. Picks Wide (≤9 cols) or Compact (10+).
        private static byte[] BuildInvoiceRdlcBytes(ReportParameter[] columnParams)
        {
            int visible = 0;
            if (columnParams != null)
            {
                foreach (var p in columnParams)
                {
                    if (p.Name.StartsWith("Show", StringComparison.OrdinalIgnoreCase) &&
                        !_summaryRowParamNames.Contains(p.Name) &&
                        p.Values.Count > 0 &&
                        string.Equals(p.Values[0], "true", StringComparison.OrdinalIgnoreCase))
                        visible++;
                }
            }

            string candidate = visible <= 9 ? "InvoiceReport_Wide.rdl" : "InvoiceReport_Compact.rdl";
            string fileName = File.Exists(ReportPath(candidate)) ? candidate : "InvoiceReport.rdl";

            int totalWeight = 0;
            foreach (var cd in _invoiceColDefs)
                if (IsGroupedColVisible(cd.ShowParam, columnParams))
                    totalWeight += cd.Weight;
            if (totalWeight == 0) totalWeight = 190;

            double scale = 190.0 / totalWeight;

            string Scaled(bool vis, int weight) =>
                string.Format("{0:F2}mm", vis ? weight * scale : (double)weight);

            var widths = new string[14];
            for (int i = 0; i < _invoiceColDefs.Length; i++)
                widths[i] = Scaled(IsGroupedColVisible(_invoiceColDefs[i].ShowParam, columnParams), _invoiceColDefs[i].Weight);

            string xml = System.IO.File.ReadAllText(ReportPath(fileName), System.Text.Encoding.UTF8);
            for (int i = 0; i < 14; i++)
                xml = xml.Replace($"##COLW_{i}##", widths[i]);

            return System.Text.Encoding.UTF8.GetBytes(xml);
        }

        private static string SelectRenewalsRdlcFileName(ReportParameter[] columnParams)
        {
            int visible = 0;
            if (columnParams != null)
            {
                foreach (var p in columnParams)
                {
                    if (p.Name.StartsWith("Show", StringComparison.OrdinalIgnoreCase) &&
                        !_summaryRowParamNames.Contains(p.Name) &&
                        p.Values.Count > 0 &&
                        string.Equals(p.Values[0], "true", StringComparison.OrdinalIgnoreCase))
                        visible++;
                }
            }

            if (visible < 8)   return "RenewalsReport_Wide.rdlc";
            if (visible <= 11) return "RenewalsReport.rdlc";
            if (visible <= 14) return "RenewalsReport_Normal.rdlc";
            return "RenewalsReport_Compact.rdlc";
        }

        private static bool IsGroupedColVisible(string paramName, ReportParameter[] ps)
        {
            if (ps == null) return false;
            foreach (var p in ps)
                if (string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase) &&
                    p.Values.Count > 0 &&
                    string.Equals(p.Values[0], "true", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        // Flat Renewals Report column definitions. Amount spans 2 columns (10 + 4 = 14).
        private static readonly (string ShowParam, int Weight)[] _renewalsColDefs =
        {
            ("ShowRowNum",        4),
            ("ShowSetCode",      10),
            ("ShowType",          9),
            ("ShowDocNum",       10),
            ("ShowCompany",      17),
            ("ShowSite",         18),
            ("ShowSetStatus",    11),
            ("ShowItemName",     18),
            ("ShowDescription",  20),
            ("ShowModel",        12),
            ("ShowSerial",       12),
            ("ShowQty",           6),
            ("ShowStartDate",    11),
            ("ShowExpiryDate",   11),
            ("ShowItemStatus",   11),
        };

        private static byte[] BuildRenewalsRdlcBytes(ReportParameter[] columnParams)
        {
            // Picks Wide (<8 cols) / default / Normal (12-14) / Compact (15+) — each file uses a
            // progressively smaller body font so more columns keep fitting in the same 190mm page
            // width. Falls back to the default file if the picked variant is missing on disk.
            string candidate = SelectRenewalsRdlcFileName(columnParams);
            string fileName = File.Exists(ReportPath(candidate)) ? candidate : "RenewalsReport.rdlc";

            int totalWeight = 0;
            foreach (var cd in _renewalsColDefs)
                if (IsGroupedColVisible(cd.ShowParam, columnParams))
                    totalWeight += cd.Weight;
            bool amtOn = IsGroupedColVisible("ShowAmount", columnParams);
            // The two Amount columns are merged (ColSpan 2) on EVERY row here, including the
            // Sub-Type group line — that report shows the group total with no "Total:" label, so
            // the pair never needs splitting and keeps its original 10 + 4 weighting.
            if (amtOn) totalWeight += 14; // 10 + 4
            if (totalWeight == 0) totalWeight = 190;

            double scale = 190.0 / totalWeight;

            string Scaled(bool visible, int weight) =>
                string.Format("{0:F2}mm", visible ? weight * scale : (double)weight);

            var widths = new string[17];
            for (int i = 0; i < _renewalsColDefs.Length; i++)
                widths[i] = Scaled(IsGroupedColVisible(_renewalsColDefs[i].ShowParam, columnParams), _renewalsColDefs[i].Weight);
            widths[15] = Scaled(amtOn, 10);
            widths[16] = Scaled(amtOn,  4);

            string xml = System.IO.File.ReadAllText(ReportPath(fileName), System.Text.Encoding.UTF8);
            for (int i = 0; i < 17; i++)
                xml = xml.Replace($"##COLW_{i}##", widths[i]);

            return System.Text.Encoding.UTF8.GetBytes(xml);
        }

        // Column definitions: (ShowParam, base weight mm). Amount spans 2 columns (10 + 4 = 14).
        private static readonly (string ShowParam, int Weight)[] _groupedColDefs =
        {
            ("ShowRowNum",        4),
            ("ShowSetCode",      10),
            ("ShowRenewedFrom",  10),
            ("ShowType",          9),
            ("ShowDocNum",       10),
            ("ShowCompany",      17),
            ("ShowSite",         18),
            ("ShowSetStatus",    11),
            ("ShowItemName",     18),
            ("ShowDescription",  20),
            ("ShowModel",        12),
            ("ShowSerial",       12),
            ("ShowQty",           6),
            ("ShowStartDate",    11),
            ("ShowExpiryDate",   11),
            ("ShowItemStatus",   11),
        };

        // Patches RenewalsGroupedReport.rdlc at runtime: substitutes ##COLW_N## placeholders
        // with mm values scaled so visible columns always fill exactly 190mm.
        private static byte[] BuildGroupedRdlcBytes(ReportParameter[] columnParams)
        {
            int totalWeight = 0;
            foreach (var cd in _groupedColDefs)
                if (IsGroupedColVisible(cd.ShowParam, columnParams))
                    totalWeight += cd.Weight;
            bool amtOn = IsGroupedColVisible("ShowAmount", columnParams);
            // See BuildRenewalsRdlcBytes — the Amount pair stays merged on every row, so the
            // original 10 + 4 weighting is kept.
            if (amtOn) totalWeight += 14; // 10 + 4
            if (totalWeight == 0) totalWeight = 190;

            double scale = 190.0 / totalWeight;

            string Scaled(bool visible, int weight) =>
                string.Format("{0:F2}mm", visible ? weight * scale : (double)weight);

            var widths = new string[18];
            for (int i = 0; i < _groupedColDefs.Length; i++)
                widths[i] = Scaled(IsGroupedColVisible(_groupedColDefs[i].ShowParam, columnParams), _groupedColDefs[i].Weight);
            widths[16] = Scaled(amtOn, 10);
            widths[17] = Scaled(amtOn,  4);

            string xml = System.IO.File.ReadAllText(ReportPath("RenewalsGroupedReport.rdlc"), System.Text.Encoding.UTF8);
            for (int i = 0; i < 18; i++)
                xml = xml.Replace($"##COLW_{i}##", widths[i]);

            return System.Text.Encoding.UTF8.GetBytes(xml);
        }

        /// <summary>
        /// Shows the signatory picker dialog.
        /// Returns the resulting DataTable on OK, or null if the user cancelled.
        /// </summary>
        public static DataTable ShowSignatoryPicker()
        {
            using (var picker = new SignatoryPickerDialog())
            {
                if (picker.ShowDialog() != DialogResult.OK)
                    return null;

                return picker.SignatoryData;
            }
        }

        /// <summary>
        /// Shows the signatory picker dialog with a subtitle label (e.g. "Issued By" or "Noted By").
        /// Returns the resulting DataTable on OK, or null if the user cancelled.
        /// </summary>
        public static DataTable ShowSignatoryPicker(string label)
        {
            using (var picker = new SignatoryPickerDialog(new[] { label }))
            {
                if (picker.ShowDialog() != DialogResult.OK)
                    return null;

                return picker.SignatoryData;
            }
        }

        /// <summary>
        /// Builds a DataTable with the required columns for signatory data.
        /// Each entry in <paramref name="signatories"/> is (name, title).
        /// Pairs are formed from consecutive entries: [0,1], [2,3], …
        /// </summary>
        public static DataTable BuildEmptySignatoryTable()
        {
            var dt = new DataTable("SignatoryData");
            dt.Columns.Add("PairIndex",   typeof(int));
            dt.Columns.Add("LeftName",    typeof(string));
            dt.Columns.Add("LeftTitle",   typeof(string));
            dt.Columns.Add("RightName",   typeof(string));
            dt.Columns.Add("RightTitle",  typeof(string));
            dt.Columns.Add("LeftPrefix",  typeof(string));
            dt.Columns.Add("RightPrefix", typeof(string));
            return dt;
        }

        public static DataTable BuildSignatoryData(IList<(string name, string title)> signatories)
        {
            var dt = new DataTable("SignatoryData");
            dt.Columns.Add("PairIndex",  typeof(int));
            dt.Columns.Add("LeftName",   typeof(string));
            dt.Columns.Add("LeftTitle",  typeof(string));
            dt.Columns.Add("RightName",  typeof(string));
            dt.Columns.Add("RightTitle", typeof(string));

            for (int i = 0; i < signatories.Count; i += 2)
            {
                string leftName  = signatories[i].name;
                string leftTitle = signatories[i].title;
                string rightName  = i + 1 < signatories.Count ? signatories[i + 1].name  : "";
                string rightTitle = i + 1 < signatories.Count ? signatories[i + 1].title : "";
                dt.Rows.Add(i / 2, leftName, leftTitle, rightName, rightTitle);
            }

            return dt;
        }

        /// <summary>
        /// Creates a ReportViewerForm with two DataSources: the main report data
        /// and the signatory DataTable. If <paramref name="signatoryData"/> is null
        /// an empty signatory table is used so the signatory section renders blank.
        /// Pass <paramref name="columnParams"/> (from ReportColumnSelectionDialog) to
        /// control which optional columns are visible.
        /// </summary>
        private static ReportViewerForm CreateReportFormFromBytes(
            byte[] rdlcBytes,
            string mainDataSetName,
            DataTable mainData,
            string title,
            DataTable signatoryData,
            ReportParameter[] columnParams)
        {
            if (signatoryData == null)
                signatoryData = BuildEmptySignatoryTable();

            var sources = new List<(string, DataTable)>
            {
                (mainDataSetName, mainData),
                ("SignatoryData",  signatoryData)
            };

            var form = new ReportViewerForm(rdlcBytes, title, sources);
            if (columnParams != null && columnParams.Length > 0)
                form.SetReportParameters(columnParams);

            return form;
        }

        public static ReportViewerForm CreateReportFormWithSignatories(
            string rdlcFileName,
            string mainDataSetName,
            DataTable mainData,
            string title,
            DataTable signatoryData = null,
            ReportParameter[] columnParams = null)
        {
            var reportPath = rdlcFileName.Contains(Path.DirectorySeparatorChar) || rdlcFileName.Contains('/')
                ? rdlcFileName
                : ReportPath(rdlcFileName);

            if (!File.Exists(reportPath))
            {
                MessageBox.Show(
                    $"Report file not found:\n{reportPath}",
                    "Report Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            if (signatoryData == null)
                signatoryData = BuildEmptySignatoryTable();

            var sources = new List<(string, DataTable)>
            {
                (mainDataSetName, mainData),
                ("SignatoryData",  signatoryData)
            };

            var form = new ReportViewerForm(reportPath, title, sources);
            if (columnParams != null && columnParams.Length > 0)
                form.SetReportParameters(columnParams);

            return form;
        }

        /// <summary>
        /// Shows the column selection dialog. Returns chosen parameters, or null if cancelled.
        /// </summary>
        public static ReportParameter[] ShowColumnSelection(
            string reportName,
            IEnumerable<ReportColumnDef> columns,
            IWin32Window owner = null)
        {
            return ReportColumnSelectionDialog.Show(owner, reportName, columns,
                n => DescribeLayout(reportName, n), CreatePreviewRenderer(reportName));
        }

        // ════════════════════════════════════════════════════════════════════
        // LIVE COLUMN PREVIEW
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Rows pulled for the preview. Enough to show a group banner, items and a subtotal.</summary>
        private const int PreviewRowCount = 12;

        private static string TopClause(int top) => top > 0 ? " TOP (" + top + ")" : "";

        /// <summary>
        /// Preview-only ORDER BY prefix that floats rows belonging to a Sub-Type Group to the top.
        /// Without it the TOP-N sample is the OLDEST rows, which predate the feature and therefore
        /// have no groups — so the preview showed no banners even though the real report does.
        /// Returns "" for normal (non-preview) loads so real report ordering is untouched.
        /// </summary>
        private static string PreviewGroupedFirst(int top, string ungroupedPredicate) =>
            top > 0 ? "CASE WHEN " + ungroupedPredicate + " THEN 1 ELSE 0 END, " : "";

        // Sample rows are fetched once per report per app session — the preview re-renders on every
        // checkbox toggle and must not hit the database each time.
        private static readonly Dictionary<string, DataTable> _previewSampleCache =
            new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _previewCacheLock = new object();

        private enum PreviewKind { None, Invoice, Renewals, RenewalsGrouped }

        private static PreviewKind KindOf(string reportName)
        {
            if (string.IsNullOrEmpty(reportName)) return PreviewKind.None;
            if (reportName.IndexOf("Invoice", StringComparison.OrdinalIgnoreCase) >= 0) return PreviewKind.Invoice;
            if (reportName.IndexOf("Grouped", StringComparison.OrdinalIgnoreCase) >= 0) return PreviewKind.RenewalsGrouped;
            if (reportName.IndexOf("Renewals", StringComparison.OrdinalIgnoreCase) >= 0) return PreviewKind.Renewals;
            return PreviewKind.None;
        }

        /// <summary>
        /// Builds the live-preview renderer handed to the column selector, or null for reports that
        /// have no preview. The returned delegate renders page 1 of the real RDLC — same
        /// ##COLW_N## substitution and template selection the finished report uses — so what the
        /// user sees is the actual layout, not an approximation.
        ///
        /// Called on a background thread by the dialog; it must not touch UI.
        /// </summary>
        public static Func<ReportParameter[], int, System.Drawing.Image> CreatePreviewRenderer(string reportName)
        {
            var kind = KindOf(reportName);
            if (kind == PreviewKind.None) return null;

            return (columnParams, dpi) =>
            {
                string dataSetName;
                byte[] rdlc;
                switch (kind)
                {
                    case PreviewKind.Invoice:
                        dataSetName = "InvoiceData";
                        rdlc = BuildInvoiceRdlcBytes(columnParams);
                        break;
                    case PreviewKind.RenewalsGrouped:
                        dataSetName = "RenewalGroupsData";
                        rdlc = BuildGroupedRdlcBytes(columnParams);
                        break;
                    default:
                        dataSetName = "RenewalsData";
                        rdlc = BuildRenewalsRdlcBytes(columnParams);
                        break;
                }

                var data = GetPreviewSample(reportName, kind, rdlc, dataSetName);

                using (var report = new LocalReport())
                {
                    report.LoadReportDefinition(new MemoryStream(rdlc));
                    if (columnParams != null && columnParams.Length > 0)
                        report.SetParameters(columnParams);

                    report.DataSources.Add(new ReportDataSource(dataSetName, data));
                    // Signatories are irrelevant to column layout; an empty table keeps that
                    // section blank instead of failing the render.
                    report.DataSources.Add(new ReportDataSource("SignatoryData", BuildEmptySignatoryTable()));
                    report.DataSources.Add(new ReportDataSource("SigRowData", BuildEmptySigRowTable()));

                    if (dpi <= 0) dpi = 96;
                    string deviceInfo =
                        "<DeviceInfo><OutputFormat>PNG</OutputFormat>" +
                        "<StartPage>1</StartPage><EndPage>1</EndPage>" +
                        "<DpiX>" + dpi + "</DpiX><DpiY>" + dpi + "</DpiY></DeviceInfo>";

                    byte[] png = report.Render("IMAGE", deviceInfo);
                    return System.Drawing.Image.FromStream(new MemoryStream(png));
                }
            };
        }

        /// <summary>
        /// Sample rows for the preview: a small TOP-N slice of the same view the report reads.
        /// Falls back to synthetic rows shaped from the RDLC's own field list when the database is
        /// unreachable, so the preview still shows true column geometry with the DB down.
        /// </summary>
        private static DataTable GetPreviewSample(string reportName, PreviewKind kind, byte[] rdlc, string dataSetName)
        {
            lock (_previewCacheLock)
            {
                DataTable cached;
                if (_previewSampleCache.TryGetValue(reportName, out cached) && cached != null)
                    return cached;
            }

            DataTable sample = null;
            try
            {
                switch (kind)
                {
                    case PreviewKind.Invoice:         sample = LoadInvoiceData(PreviewRowCount); break;
                    case PreviewKind.RenewalsGrouped: sample = LoadRenewalGroupsData(null, false, PreviewRowCount); break;
                    default:                          sample = LoadRenewalsData(null, false, PreviewRowCount); break;
                }
                if (sample != null && sample.Rows.Count == 0) sample = null;
            }
            catch (Exception ex)
            {
                Logger.LogError("Column preview: sample query failed, using synthetic rows", ex);
                sample = null;
            }

            if (sample == null)
                sample = BuildSyntheticSample(rdlc, dataSetName);

            lock (_previewCacheLock) { _previewSampleCache[reportName] = sample; }
            return sample;
        }

        /// <summary>
        /// Builds a placeholder table straight from the RDLC's declared &lt;Fields&gt;, so it can
        /// never drift out of sync with the report definition.
        /// </summary>
        private static DataTable BuildSyntheticSample(byte[] rdlc, string dataSetName)
        {
            var dt = new DataTable(dataSetName);
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(System.Text.Encoding.UTF8.GetString(rdlc));
            var ns = new System.Xml.XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("d", doc.DocumentElement.NamespaceURI);
            ns.AddNamespace("rd", "http://schemas.microsoft.com/SQLServer/reporting/reportdesigner");

            var dsNode = doc.SelectSingleNode("//d:DataSet[@Name='" + dataSetName + "']", ns);
            if (dsNode == null) return dt;

            var names = new List<string>();
            foreach (System.Xml.XmlNode f in dsNode.SelectNodes(".//d:Field", ns))
            {
                string fn = ((System.Xml.XmlElement)f).GetAttribute("Name");
                var tn = f.SelectSingleNode("rd:TypeName", ns);
                Type t = Type.GetType(tn != null ? tn.InnerText : "System.String") ?? typeof(string);
                dt.Columns.Add(fn, t);
                names.Add(fn);
            }

            for (int i = 0; i < 6; i++)
            {
                var r = dt.NewRow();
                foreach (var fn in names)
                {
                    Type t = dt.Columns[fn].DataType;
                    if (t == typeof(int))            r[fn] = i + 1;
                    else if (t == typeof(long))      r[fn] = (long)(i + 1);
                    else if (t == typeof(decimal))   r[fn] = 1000m * (i + 1);
                    else if (t == typeof(DateTime))  r[fn] = DateTime.Today.AddDays(i);
                    else                             r[fn] = "Sample " + fn;
                }
                SetIfPresent(dt, r, "Year", 2025);
                SetIfPresent(dt, r, "MonthNumber", 7);
                SetIfPresent(dt, r, "MonthName", "July");
                SetIfPresent(dt, r, "Month", "July");
                SetIfPresent(dt, r, "SetId", 1);
                SetIfPresent(dt, r, "DocumentNumber", "DOC-0001");
                SetIfPresent(dt, r, "ChainPositionLabel", "Original");
                SetIfPresent(dt, r, "ItemName", "Sample Item Name");
                SetIfPresent(dt, r, "ItemNo", (long)(i + 1));
                // Two groups plus an ungrouped tail, mirroring the real Sub-Type Group layout.
                int g = i % 3;
                SetIfPresent(dt, r, "SubTypeGroupId", g);
                SetIfPresent(dt, r, "SubType", new[] { "", "Contract", "Subscription" }[g]);
                SetIfPresent(dt, r, "GroupReferenceCode", new[] { "", "100316593", "SUB-42" }[g]);
                // Realistic magnitudes — this placeholder is what the preview shows when the
                // sample query fails, so an inflated figure would misrepresent the column widths.
                SetIfPresent(dt, r, "GroupSubtotal", new[] { 0m, 620000m, 148500m }[g]);
                dt.Rows.Add(r);
            }
            return dt;
        }

        private static void SetIfPresent(DataTable dt, DataRow r, string col, object val)
        {
            if (!dt.Columns.Contains(col)) return;
            try { r[col] = Convert.ChangeType(val, dt.Columns[col].DataType); } catch { }
        }

        /// <summary>
        /// One-line description of the page layout a given visible-column count produces, for the
        /// column selector's live readout. Defined here so the template thresholds live in exactly
        /// one place — the same ones SelectRenewalsRdlcFileName and BuildInvoiceRdlcBytes use.
        /// </summary>
        private static string DescribeLayout(string reportName, int visibleColumns)
        {
            if (visibleColumns == 0) return "nothing selected — the report will have no columns";

            string template;
            if (reportName != null && reportName.IndexOf("Invoice", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                template = visibleColumns <= 9 ? "Wide template" : "Compact template";
            }
            else if (reportName != null && reportName.IndexOf("Renewals", StringComparison.OrdinalIgnoreCase) >= 0
                     && reportName.IndexOf("Grouped", StringComparison.OrdinalIgnoreCase) < 0)
            {
                template = visibleColumns < 8  ? "Wide template"
                         : visibleColumns <= 11 ? "Standard template"
                         : visibleColumns <= 14 ? "Normal template"
                                                : "Compact template";
            }
            else
            {
                template = "single template";
            }

            // Widths are always rescaled to exactly 190mm, so the only real risk is per-column
            // space getting too tight to read rather than the page overflowing.
            string fit = visibleColumns >= 14 ? "cramped at 190mm"
                       : visibleColumns >= 11 ? "tight but fits 190mm"
                                              : "comfortable at 190mm";

            return template + ", " + fit;
        }

        /// <summary>
        /// Executes <paramref name="sql"/> against the configured connection
        /// and returns the result as a DataTable named <paramref name="tableName"/>.
        /// </summary>
        private static DataTable FillDataTable(string sql, string tableName)
        {
            DatabaseConfig.EnsureConfigured();
            var dt = new DataTable(tableName);

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            using (var cmd = new SqlCommand(sql, con))
            using (var adapter = new SqlDataAdapter(cmd))
            {
                con.Open();
                adapter.Fill(dt);
            }

            return dt;
        }

        // ════════════════════════════════════════════════════════════════════
        // OPTIONAL: Export to PDF without ReportViewer UI
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Renders the specified RDLC to a PDF byte array using LocalReport.Render().
        /// Requires Microsoft.Reporting.WinForms (or Microsoft.Reporting.NETCore).
        ///
        /// Example usage:
        /// <code>
        ///   byte[] pdf = ReportLauncher.ExportToPdf(
        ///       "SetsReport.rdlc", "SetsData", ReportLauncher.LoadSetsData());
        ///   File.WriteAllBytes("SetsReport.pdf", pdf);
        ///   Process.Start(new ProcessStartInfo("SetsReport.pdf") { UseShellExecute = true });
        /// </code>
        /// </summary>
        public static byte[] ExportToPdf(
            string rdlcFileName,
            string dataSetName,
            DataTable data)
        {
            /* ── UNCOMMENT AFTER ADDING ReportViewer REFERENCE ─────────────
            var reportPath = ReportPath(rdlcFileName);

            using (var localReport = new LocalReport())
            {
                localReport.ReportPath = reportPath;
                localReport.DataSources.Add(new ReportDataSource(dataSetName, data));

                string mimeType, encoding, fileNameExtension;
                Warning[] warnings;
                string[] streamIds;

                byte[] renderedBytes = localReport.Render(
                    format:             "PDF",
                    deviceInfo:         null,
                    mimeType:           out mimeType,
                    encoding:           out encoding,
                    fileNameExtension:  out fileNameExtension,
                    streamIds:          out streamIds,
                    warnings:           out warnings);

                return renderedBytes;
            }
            ── END UNCOMMENT ─────────────────────────────────────────────── */

            throw new NotImplementedException(
                "Add Microsoft.Reporting.WinForms NuGet package and uncomment ExportToPdf body.");
        }
    }
}
