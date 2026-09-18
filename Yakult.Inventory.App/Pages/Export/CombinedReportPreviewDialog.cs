using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Dapper;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.Export
{
    /// <summary>
    /// Shows a combined HTML preview of one or more selected report types.
    /// Each section matches the layout of its individual export preview dialog.
    /// Pass null for any report type that was not selected.
    /// </summary>
    internal class CombinedReportPreviewDialog : Form
    {
        private static readonly Color AccentNavy   = Color.FromArgb(30, 64, 111);
        private static readonly Color CharcoalText = Color.FromArgb(33, 37, 41);
        private static readonly Color LightBg      = Color.FromArgb(248, 249, 250);
        private static readonly Color BorderColor  = Color.FromArgb(206, 212, 218);

        // ── Data from each report type (null = not selected) ────────────
        private readonly List<DataRow>               _invoiceHeaders;
        private readonly DataTable                   _invoiceItems;
        private readonly List<SetDto>                _sets;
        private readonly SetRepository               _setRepo;
        private readonly List<RenewalDto>            _renewals;
        private readonly RenewalRepository           _renewalRepo;
        private readonly List<RenewalGroupViewModel> _renewalGroups;
        private readonly RenewalRepository           _groupedRepo;
        private readonly List<OutboundBatchDto>       _outboundBatches;
        private readonly Dictionary<int, List<VendorBatchAuditTrailDto>> _cartridgeAuditTrails;

        private WebBrowser _browser;

        // ── Caches loaded before rendering ──────────────────────────────
        private Dictionary<int, List<SetDetailRequestDto>> _setItemsCache;
        private Dictionary<int, List<SetItemRenewalDto>>   _renewalSetItemsCache;
        private Dictionary<int, ItemInfo>                   _itemDetails;

        private class ItemInfo
        {
            public int    ItemId       { get; set; }
            public string Name         { get; set; }
            public string ModelNumber  { get; set; }
            public string SerialNumber { get; set; }
        }

        internal CombinedReportPreviewDialog(
            List<DataRow> invoiceHeaders, DataTable invoiceItems,
            List<SetDto> sets, SetRepository setRepo,
            List<RenewalDto> renewals, RenewalRepository renewalRepo,
            List<RenewalGroupViewModel> renewalGroups, RenewalRepository groupedRepo,
            List<OutboundBatchDto> outboundBatches,
            Dictionary<int, List<VendorBatchAuditTrailDto>> cartridgeAuditTrails = null)
        {
            _invoiceHeaders        = invoiceHeaders;
            _invoiceItems          = invoiceItems;
            _sets                  = sets;
            _setRepo               = setRepo;
            _renewals              = renewals;
            _renewalRepo           = renewalRepo;
            _renewalGroups         = renewalGroups;
            _groupedRepo           = groupedRepo;
            _outboundBatches       = outboundBatches;
            _cartridgeAuditTrails  = cartridgeAuditTrails;

            Text            = "Combined Report Preview";
            Size            = new Size(1320, 900);
            MinimumSize     = new Size(960, 620);
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = LightBg;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = true;

            BuildUi();
            LoadAdditionalDataAndRender();
        }

        // ════════════════════════════════════════════════════════════════════
        //  UI shell
        // ════════════════════════════════════════════════════════════════════

        private void BuildUi()
        {
            int count = 0;
            if (_invoiceHeaders != null) count++;
            if (_sets != null)           count++;
            if (_renewals != null)       count++;
            if (_renewalGroups != null)  count++;
            if (_outboundBatches != null) count++;

            var header = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = AccentNavy };
            header.Controls.Add(new Label
            {
                Text      = $"Combined Report Preview  \u2014  {count} Report Section(s)",
                Font      = new Font("Arial", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(16, 10)
            });
            header.Controls.Add(new Label
            {
                Text      = "Scroll down to see each selected report type.",
                Font      = new Font("Arial", 9F),
                ForeColor = Color.FromArgb(176, 210, 240),
                AutoSize  = true,
                Location  = new Point(16, 36)
            });

            var btnBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Color.White };
            btnBar.Paint += (s, ev) =>
            {
                using (var p = new Pen(BorderColor))
                    ev.Graphics.DrawLine(p, 0, 0, btnBar.Width, 0);
            };

            var btnCancel = new Button
            {
                Text      = "Cancel",
                Font      = new Font("Arial", 9.5F),
                Size      = new Size(90, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = CharcoalText,
                Cursor    = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, ev) => Close();

            var btnExport = new Button
            {
                Text      = "Export to PDF",
                Font      = new Font("Arial", 9.5F, FontStyle.Bold),
                Size      = new Size(140, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentNavy,
                ForeColor = Color.White,
                Cursor    = Cursors.Hand
            };
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.Click += OnExportToPdf;

            btnBar.Layout += (s, ev) =>
            {
                btnExport.Location = new Point(btnBar.Width - btnExport.Width - 16, (btnBar.Height - btnExport.Height) / 2);
                btnCancel.Location = new Point(btnBar.Width - btnExport.Width - btnCancel.Width - 28, (btnBar.Height - btnCancel.Height) / 2);
            };
            btnBar.Controls.Add(btnExport);
            btnBar.Controls.Add(btnCancel);

            _browser = new WebBrowser
            {
                Dock                           = DockStyle.Fill,
                ScrollBarsEnabled              = true,
                IsWebBrowserContextMenuEnabled = false,
                WebBrowserShortcutsEnabled     = false,
                ScriptErrorsSuppressed         = true,
                AllowNavigation                = false
            };

            Controls.Add(_browser);
            Controls.Add(btnBar);
            Controls.Add(header);
        }

        // ════════════════════════════════════════════════════════════════════
        //  Load additional data then render
        // ════════════════════════════════════════════════════════════════════

        private async void LoadAdditionalDataAndRender()
        {
            _setItemsCache        = new Dictionary<int, List<SetDetailRequestDto>>();
            _renewalSetItemsCache = new Dictionary<int, List<SetItemRenewalDto>>();
            _itemDetails          = new Dictionary<int, ItemInfo>();

            // Fetch set items for sets section
            if (_sets != null && _setRepo != null)
            {
                foreach (var set in _sets)
                {
                    try
                    {
                        var items = await _setRepo.GetSetRequestsAsync(set.SetId);
                        _setItemsCache[set.SetId] = items ?? new List<SetDetailRequestDto>();
                    }
                    catch
                    {
                        _setItemsCache[set.SetId] = new List<SetDetailRequestDto>();
                    }
                }
            }

            // Fetch set items for renewal and renewals-grouped sections
            var allRenewalsForItems = new List<RenewalDto>();
            if (_renewals != null)      allRenewalsForItems.AddRange(_renewals);
            if (_renewalGroups != null)
                foreach (var g in _renewalGroups)
                    if (g.Chain != null) allRenewalsForItems.AddRange(g.Chain);

            var renewalRepo = _renewalRepo ?? _groupedRepo;
            if (renewalRepo != null && allRenewalsForItems.Count > 0)
            {
                var allItemIds = new HashSet<int>();
                foreach (var r in allRenewalsForItems)
                {
                    if (_renewalSetItemsCache.ContainsKey(r.SetId)) continue;
                    try
                    {
                        var items = renewalRepo.GetSetItemsForRenewal(r.SetId);
                        _renewalSetItemsCache[r.SetId] = items ?? new List<SetItemRenewalDto>();
                        foreach (var item in _renewalSetItemsCache[r.SetId])
                            allItemIds.Add(item.ItemId);
                    }
                    catch
                    {
                        _renewalSetItemsCache[r.SetId] = new List<SetItemRenewalDto>();
                    }
                }

                if (allItemIds.Count > 0)
                    _itemDetails = FetchItemDetails(allItemIds);
            }

            _browser.DocumentText = BuildCombinedHtml();
        }

        private static Dictionary<int, ItemInfo> FetchItemDetails(IEnumerable<int> itemIds)
        {
            var result = new Dictionary<int, ItemInfo>();
            try
            {
                DatabaseConfig.EnsureConfigured();
                const string sql = "SELECT ItemId, Name, ModelNumber, SerialNumber FROM dbo.Item WHERE ItemId IN @Ids";
                using (var conn = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    var rows = conn.Query<ItemInfo>(sql, new { Ids = itemIds.ToList() });
                    foreach (var row in rows)
                        result[row.ItemId] = row;
                }
            }
            catch { }
            return result;
        }

        private string GetRenewalItemName(SetItemRenewalDto si)
        {
            if (_itemDetails.TryGetValue(si.ItemId, out var info) && !string.IsNullOrWhiteSpace(info.Name))
                return info.Name;
            return si.Description ?? "";
        }

        private string GetRenewalModel(SetItemRenewalDto si)
        {
            if (_itemDetails.TryGetValue(si.ItemId, out var info))
                return info.ModelNumber ?? "";
            return "";
        }

        private string GetRenewalSerial(SetItemRenewalDto si)
        {
            if (_itemDetails.TryGetValue(si.ItemId, out var info))
                return info.SerialNumber ?? "";
            return "";
        }

        // ════════════════════════════════════════════════════════════════════
        //  COMBINED HTML
        // ════════════════════════════════════════════════════════════════════

        private string BuildCombinedHtml()
        {
            var sb = new StringBuilder();

            sb.Append(@"<!DOCTYPE html><html><head><meta charset='UTF-8'><title>Combined Report Preview</title>
<style>
*{box-sizing:border-box;margin:0;padding:0;}
body{background:#f0f2f5;font-family:Arial,Helvetica,sans-serif;font-size:12px;color:#212529;}
.section{background:#fff;margin:20px 24px 30px;border-radius:6px;box-shadow:0 2px 12px rgba(0,0,0,.12);overflow:hidden;}
.sec-title{background:#1e406f;color:#fff;padding:18px 28px 14px;border-bottom:3px solid #2c6fad;}
.sec-title h1{font-family:Arial,Helvetica,sans-serif;font-size:22px;font-weight:700;margin:0 0 4px;letter-spacing:.3px;}
.sec-title p{font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#b0cce4;margin:0;}
/* ── Month header (sets / renewal / renewals-grouped / cartridge) ── */
.month-header{background:#f8f9fa;border-top:2px solid #1e406f;border-bottom:1px solid #dee2e6;padding:10px 24px;margin-top:6px;}
.month-header h2{font-family:Arial,Helvetica,sans-serif;font-size:14px;font-weight:700;color:#1e406f;margin:0;letter-spacing:.3px;}
.month-header .cnt{font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#6c757d;margin-left:10px;font-weight:400;}
/* ── Standard report table (sets / renewal / renewals-grouped / cartridge) ── */
table.rpt{border-collapse:collapse;width:100%;background:#fff;}
table.rpt th{background:#34495e;color:#fff;font-family:Arial,Helvetica,sans-serif;font-weight:700;font-size:10px;text-align:left;padding:8px 10px;white-space:nowrap;letter-spacing:.3px;text-transform:uppercase;border-right:1px solid #4a6274;}
table.rpt th:last-child{border-right:none;}
table.rpt td{font-family:Arial,Helvetica,sans-serif;padding:6px 10px;border-bottom:1px solid #e9ecef;font-size:11px;vertical-align:top;border-right:1px solid #f0f0f0;}
table.rpt td:last-child{border-right:none;}
table.rpt tr:nth-child(even) td{background:#f8f9fa;}
table.rpt tr.set-border td{border-top:2px solid #dee2e6;}
/* ── Invoice table row styles ── */
table.inv{width:100%;border-collapse:collapse;}
tr.yr-row td{background:#1e406f;color:#fff;font-size:13px;font-weight:700;text-align:center;padding:7px 0;letter-spacing:2px;}
tr.mo-row td{background:#2c6fad;color:#fff;font-size:11.5px;font-weight:700;text-align:center;padding:5px 0;letter-spacing:1px;}
tr.col-hdr td{background:#34495e;color:#fff;font-size:10.5px;font-weight:700;padding:8px;white-space:nowrap;border-right:1px solid #2c3e50;}
tr.col-hdr td.r{text-align:right;}tr.col-hdr td:last-child{border-right:none;}
tr.item{border-bottom:1px solid #dee2e6;}
tr.item:nth-child(even){background:#f8f9fa;}
tr.item td{padding:7px 8px;font-size:11px;vertical-align:top;border-right:1px solid #dee2e6;color:#212529;}
tr.item td:last-child{border-right:none;}
tr.item td.r{text-align:right;}tr.item td.wrap{white-space:normal;}
tr.item td.shared{border-right:2px solid #adb5bd;}
tr.sum-row td{padding:5px 8px;font-size:11px;border-bottom:1px solid #dee2e6;}
tr.sum-row td.sum-lbl{text-align:right;font-weight:600;color:#495057;padding-right:14px;}
tr.sum-row td.sum-val{text-align:right;font-weight:700;color:#212529;}
tr.sum-row.total-row td.sum-lbl,tr.sum-row.total-row td.sum-val{background:#ebf5fb;font-size:12px;color:#1e406f;font-weight:800;border-top:1px solid #2c6fad;}
tr.sep-row td{background:#2c6fad;padding:4px 0;border:none;}
/* ── Shared td classes ── */
td.set-code{font-weight:700;color:#1e406f;font-size:11px;}
td.company{font-weight:600;color:#1e406f;}
td.bold{font-weight:600;}
td.item-name{font-weight:600;}
td.set-status{font-weight:600;}
td.serial{font-family:Arial,Helvetica,sans-serif;font-size:10px;color:#495057;}
td.empty-items{font-style:italic;color:#6c757d;padding:6px 10px;}
td.item-status{font-weight:500;}
td.qty{text-align:center;font-weight:600;}
td.amount{text-align:right;font-weight:600;}
td.date{text-align:center;white-space:nowrap;}
/* ── Cartridge-specific ── */
.batch-header{background:#e8ecf1;border-top:3px solid #1e406f;border-bottom:1px solid #dee2e6;padding:10px 24px;margin-top:6px;}
.batch-header h2{font-family:Arial,Helvetica,sans-serif;font-size:15px;font-weight:700;color:#1e406f;margin:0;letter-spacing:.3px;}
.batch-header .batch-meta{font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#6c757d;margin-top:2px;}
.batch-header .batch-count{font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#6c757d;margin-left:10px;font-weight:400;}
.purpose-dispose{color:#991b1b;font-weight:700;}
.purpose-sell{color:#15803d;font-weight:700;}
td.type-dispose{font-weight:600;color:#991b1b;background:#fee2e2;}
td.type-sell{font-weight:600;color:#15803d;background:#dcfce7;}
td.batch-id{font-weight:700;color:#1e406f;}
td.cartridge-id{font-weight:700;color:#1e406f;}
td.model{font-weight:600;}
td.recipient{font-weight:500;}
/* ── Badges ── */
.badge{display:inline-block;padding:2px 8px;border-radius:3px;font-size:10px;font-weight:700;white-space:nowrap;}
.b-ok{background:#d4edda;color:#155724;}.b-warn{background:#fff3cd;color:#856404;}
.b-bad{background:#f8d7da;color:#721c24;}.b-def{background:#e9ecef;color:#495057;}
/* ── Report footer ── */
.rpt-footer{background:#f8f9fa;border-top:2px solid #dee2e6;padding:12px 24px;font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#6c757d;}
.rpt-footer strong{color:#212529;}
/* ── Section separator ── */
.rpt-sep{display:flex;align-items:center;margin:0 24px;gap:12px;}
.rpt-sep::before,.rpt-sep::after{content:'';flex:1;height:2px;background:#1e406f;}
.rpt-sep span{font-family:Arial,Helvetica,sans-serif;font-size:10px;font-weight:700;color:#1e406f;white-space:nowrap;letter-spacing:.5px;text-transform:uppercase;padding:4px 10px;border:2px solid #1e406f;border-radius:3px;}
</style></head><body>");

            bool anySectionWritten = false;

            if (_invoiceHeaders != null && _invoiceHeaders.Count > 0)
            {
                AppendInvoiceSection(sb);
                anySectionWritten = true;
            }

            if (_sets != null && _sets.Count > 0)
            {
                if (anySectionWritten) sb.Append("<div class='rpt-sep'><span>Sets Report</span></div>");
                AppendSetsSection(sb);
                anySectionWritten = true;
            }

            if (_renewals != null && _renewals.Count > 0)
            {
                if (anySectionWritten) sb.Append("<div class='rpt-sep'><span>Renewal Report</span></div>");
                AppendRenewalSection(sb);
                anySectionWritten = true;
            }

            if (_renewalGroups != null && _renewalGroups.Count > 0)
            {
                if (anySectionWritten) sb.Append("<div class='rpt-sep'><span>Renewals Grouped</span></div>");
                AppendRenewalsGroupedSection(sb);
                anySectionWritten = true;
            }

            if (_outboundBatches != null && _outboundBatches.Count > 0)
            {
                if (anySectionWritten) sb.Append("<div class='rpt-sep'><span>Cartridge Disposed / Sold</span></div>");
                AppendCartridgeSection(sb);
            }

            sb.Append("</body></html>");
            return sb.ToString();
        }

        // ════════════════════════════════════════════════════════════════════
        //  INVOICE SECTION  —  matches ExportInvoicePreviewDialog layout
        // ════════════════════════════════════════════════════════════════════

        private void AppendInvoiceSection(StringBuilder sb)
        {
            const int COLS = 10;

            sb.Append("<div class='section'>");
            sb.Append("<div class='sec-title'><h1>Invoice Report</h1>");
            sb.Append($"<p>Generated: {DateTime.Now:MMMM dd, yyyy  h:mm tt}  &nbsp;|&nbsp;  {_invoiceHeaders.Count} Invoice(s)  &nbsp;|&nbsp;  {(_invoiceItems?.Rows.Count ?? 0)} Item(s)</p></div>");

            var sorted = _invoiceHeaders
                .Select(r =>
                {
                    object d = r["InvoiceDate"];
                    return new { Row = r, Date = d != null && d != DBNull.Value ? (DateTime?)Convert.ToDateTime(d) : null };
                })
                .OrderBy(x => x.Date ?? DateTime.MaxValue)
                .ToList();

            sb.Append("<table class='inv'>");

            int prevYear = -1, prevMonth = -1;

            foreach (var x in sorted)
            {
                var hdr = x.Row;
                DateTime? invDate = x.Date;
                int year = invDate?.Year ?? 0, month = invDate?.Month ?? 0;

                if (year != prevYear)
                {
                    sb.Append($"<tr class='yr-row'><td colspan='{COLS}'>{(year > 0 ? year.ToString() : "Unknown")}</td></tr>");
                    prevYear = year; prevMonth = -1;
                }
                if (month != prevMonth)
                {
                    string mn = invDate.HasValue ? invDate.Value.ToString("MMMM") : "Unknown";
                    sb.Append($"<tr class='mo-row'><td colspan='{COLS}'>{mn}</td></tr>");
                    sb.Append("<tr class='col-hdr'><td>DATE</td><td>COMPANY</td><td>DOCUMENT #</td><td>STATUS</td><td>REFERENCE #</td><td class='r'>QTY</td><td>ITEM NAME</td><td>DURATION</td><td class='r'>AMOUNT</td><td>SITE</td></tr>");
                    prevMonth = month;
                }

                string setCode   = hdr["SetCode"]?.ToString()        ?? "";
                string company   = hdr["CompanyName"]?.ToString()    ?? "";
                string docNum    = hdr["DocumentNumber"]?.ToString()  ?? "";
                string refNum    = hdr["ReferenceNumber"]?.ToString() ?? "";
                string site      = ColVal(hdr, "Site");
                string statusRaw = hdr["Status"]?.ToString()         ?? "";
                string dateStr   = invDate.HasValue ? invDate.Value.ToString("M/d/yyyy") : "";

                var groupItems = new List<DataRow>();
                if (_invoiceItems != null)
                    foreach (DataRow it in _invoiceItems.Rows)
                        if (string.Equals(ColVal(it, "SetCode"), setCode, StringComparison.OrdinalIgnoreCase))
                            groupItems.Add(it);

                int span = Math.Max(1, groupItems.Count);

                if (groupItems.Count == 0)
                {
                    sb.Append("<tr class='item'>");
                    sb.Append($"<td>{Esc(dateStr)}</td><td>{Esc(company)}</td><td>{Esc(docNum)}</td>");
                    sb.Append($"<td>{StatusBadge(statusRaw)}</td>");
                    sb.Append($"<td>{Esc(refNum)}</td><td colspan='4' style='color:#94a3b8;font-style:italic'>No line items</td><td>{Esc(site)}</td></tr>");
                }
                else
                {
                    bool isFirst = true;
                    foreach (var it in groupItems)
                    {
                        sb.Append("<tr class='item'>");
                        if (isFirst)
                        {
                            string rs = span > 1 ? $" rowspan='{span}'" : "";
                            sb.Append($"<td{rs} class='shared'>{Esc(dateStr)}</td>");
                            sb.Append($"<td{rs} class='shared'>{Esc(company)}</td>");
                            sb.Append($"<td{rs} class='shared'>{Esc(docNum)}</td>");
                            sb.Append($"<td{rs} class='shared'>{StatusBadge(statusRaw)}</td>");
                            sb.Append($"<td{rs} class='shared'>{Esc(refNum)}</td>");
                        }
                        decimal qty = decimal.TryParse(ColVal(it, "Quantity"), out var q) ? q : 0;
                        sb.Append($"<td class='r'>{qty:N0}</td>");
                        sb.Append($"<td class='wrap'>{Esc(ColVal(it, "ItemName"))}</td>");
                        var sd = SafeDate(it, "LineStartDate");
                        var ed = SafeDate(it, "LineEndDate");
                        string dur = (sd.HasValue && ed.HasValue)
                            ? $"{sd.Value:yyyy-MM-dd}&nbsp;&nbsp;{ed.Value:yyyy-MM-dd}"
                            : sd.HasValue ? sd.Value.ToString("yyyy-MM-dd") : "";
                        sb.Append($"<td style='white-space:nowrap'>{dur}</td><td></td>");
                        if (isFirst)
                        {
                            sb.Append($"<td{(span > 1 ? $" rowspan='{span}'" : "")} class='shared'>{Esc(site)}</td>");
                            isFirst = false;
                        }
                        sb.Append("</tr>");
                    }
                }

                AppendInvSumRow(sb, "Subtotal",     SafeDecimal(hdr, "Subtotal").ToString("N2"),       false);
                AppendInvSumRow(sb, "VAT",          SafeDecimal(hdr, "VatAmount").ToString("N2"),      false);
                AppendInvSumRow(sb, "WHT",          SafeDecimal(hdr, "WhtAmount").ToString("N2"),      false);
                AppendInvSumRow(sb, "Discount",     SafeDecimal(hdr, "DiscountAmount").ToString("N2"), false);
                AppendInvSumRow(sb, "Total Amount", SafeDecimal(hdr, "TotalAmountDue").ToString("N2"), true);
                sb.Append($"<tr class='sep-row'><td colspan='{COLS}'></td></tr>");
            }

            sb.Append("</table>");

            decimal grandTotal = _invoiceHeaders.Sum(h => SafeDecimal(h, "TotalAmountDue"));
            sb.Append($"<div class='rpt-footer'><strong>Total:</strong> {_invoiceHeaders.Count} invoice(s) &nbsp;&middot;&nbsp; <strong>Grand Total:</strong> {grandTotal:N2}</div>");
            sb.Append("</div>");
        }

        private static void AppendInvSumRow(StringBuilder sb, string label, string val, bool isTotal)
        {
            string cls = isTotal ? "sum-row total-row" : "sum-row";
            sb.Append($"<tr class='{cls}'><td colspan='7'></td><td class='sum-lbl'>{Esc(label)}</td><td class='sum-val'>{Esc(val)}</td><td></td></tr>");
        }

        // ════════════════════════════════════════════════════════════════════
        //  SETS SECTION  —  matches ExportSetsPreviewDialog layout
        // ════════════════════════════════════════════════════════════════════

        private void AppendSetsSection(StringBuilder sb)
        {
            sb.Append("<div class='section'>");
            sb.Append("<div class='sec-title'><h1>Sets Export Report</h1>");
            sb.Append($"<p>Generated: {DateTime.Now:MMMM dd, yyyy  h:mm tt}  &nbsp;|&nbsp;  {_sets.Count} Set(s)</p></div>");

            var sorted = _sets.OrderBy(s => s.DispatchDate ?? s.CreatedAt).ToList();
            var grouped = sorted
                .GroupBy(s => { var dt = s.DispatchDate ?? s.CreatedAt; return new DateTime(dt.Year, dt.Month, 1); })
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                string monthLabel = group.Key.ToString("MMMM yyyy");
                int setCount = group.Count();
                sb.Append($"<div class='month-header'><h2>{Esc(monthLabel)}<span class='cnt'>({setCount} set{(setCount != 1 ? "s" : "")})</span></h2></div>");

                sb.Append("<table class='rpt'>");
                sb.Append("<thead><tr>");
                sb.Append("<th>Set Code</th><th>Employee</th><th>Item Count</th>");
                sb.Append("<th>Item Name</th><th>Model</th><th>Serial #</th>");
                sb.Append("<th>Item Type</th><th>Category</th><th>Qty</th>");
                sb.Append("<th>Date Dispatched</th><th>Amount</th>");
                sb.Append("<th>Company</th><th>Branch</th><th>Department</th>");
                sb.Append("</tr></thead><tbody>");

                bool firstSet = true;
                foreach (var set in group)
                {
                    string dispatch = set.DispatchDate.HasValue ? set.DispatchDate.Value.ToString("MM/dd/yyyy") : "";

                    if (!_setItemsCache.TryGetValue(set.SetId, out var items) || items == null || items.Count == 0)
                    {
                        string borderCls = !firstSet ? " class='set-border'" : "";
                        sb.Append($"<tr{borderCls}>");
                        sb.Append($"<td class='set-code'>{Esc(set.SetCode)}</td>");
                        sb.Append($"<td class='bold'>{Esc(set.CurrentEmployeeName)}</td>");
                        sb.Append($"<td class='qty'>{set.ItemCount}</td>");
                        sb.Append("<td class='empty-items' colspan='6'>No items found</td>");
                        sb.Append($"<td class='date'>{dispatch}</td>");
                        sb.Append("<td class='amount'>&mdash;</td>");
                        sb.Append($"<td class='company'>{Esc(set.CurrentCompanyName ?? set.Company)}</td>");
                        sb.Append($"<td>{Esc(set.CurrentBranchName)}</td>");
                        sb.Append($"<td>{Esc(set.CurrentDepartmentName)}</td>");
                        sb.Append("</tr>");
                    }
                    else
                    {
                        bool firstItem = true;
                        foreach (var item in items)
                        {
                            string rowCls = (firstItem && !firstSet) ? " class='set-border'" : "";
                            sb.Append($"<tr{rowCls}>");

                            if (firstItem)
                            {
                                sb.Append($"<td class='set-code' rowspan='{items.Count}'>{Esc(set.SetCode)}</td>");
                                sb.Append($"<td class='bold' rowspan='{items.Count}'>{Esc(set.CurrentEmployeeName)}</td>");
                                sb.Append($"<td class='qty' rowspan='{items.Count}'>{set.ItemCount}</td>");
                            }

                            sb.Append($"<td class='item-name'>{Esc(item.ItemName)}</td>");
                            sb.Append($"<td>{Esc(item.ModelNumber)}</td>");
                            sb.Append($"<td class='serial'>{Esc(item.SerialNumber)}</td>");
                            sb.Append($"<td>{Esc(item.Status)}</td>");
                            sb.Append($"<td>{Esc(item.Category)}</td>");
                            sb.Append($"<td class='qty'>{item.Quantity}</td>");

                            if (firstItem)
                            {
                                sb.Append($"<td class='date' rowspan='{items.Count}'>{dispatch}</td>");
                                sb.Append($"<td class='amount' rowspan='{items.Count}'>&mdash;</td>");
                                sb.Append($"<td class='company' rowspan='{items.Count}'>{Esc(set.CurrentCompanyName ?? set.Company)}</td>");
                                sb.Append($"<td rowspan='{items.Count}'>{Esc(set.CurrentBranchName)}</td>");
                                sb.Append($"<td rowspan='{items.Count}'>{Esc(set.CurrentDepartmentName)}</td>");
                            }

                            sb.Append("</tr>");
                            firstItem = false;
                        }
                    }
                    firstSet = false;
                }

                sb.Append("</tbody></table>");
            }

            var dates = _sets.Where(s => s.DispatchDate.HasValue).Select(s => s.DispatchDate.Value).ToList();
            string dateRange = dates.Count > 0 ? $"{dates.Min():MM/dd/yyyy} &ndash; {dates.Max():MM/dd/yyyy}" : "N/A";
            sb.Append("<div class='rpt-footer'>");
            sb.Append($"<strong>Total:</strong> {_sets.Count} set(s) &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Total Items:</strong> {_sets.Sum(s => s.ItemCount)} &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Date Range:</strong> {dateRange}");
            sb.Append("</div></div>");
        }

        // ════════════════════════════════════════════════════════════════════
        //  RENEWAL SECTION  —  matches ExportRenewalPreviewDialog layout
        // ════════════════════════════════════════════════════════════════════

        private void AppendRenewalSection(StringBuilder sb)
        {
            sb.Append("<div class='section'>");
            sb.Append("<div class='sec-title'><h1>Renewal Export Report</h1>");
            sb.Append($"<p>Generated: {DateTime.Now:MMMM dd, yyyy  h:mm tt}  &nbsp;|&nbsp;  {_renewals.Count} Renewal(s)</p></div>");

            var sorted = _renewals
                .OrderBy(r => r.StartDate ?? r.CreatedDate ?? DateTime.MinValue)
                .ToList();
            var grouped = sorted
                .GroupBy(r => { var dt = r.StartDate ?? r.CreatedDate ?? DateTime.MinValue; return new DateTime(dt.Year, dt.Month, 1); })
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                string monthLabel = group.Key == DateTime.MinValue ? "No Date" : group.Key.ToString("MMMM yyyy");
                int renewalCount = group.Count();

                sb.Append($"<div class='month-header'><h2>{Esc(monthLabel)}<span class='cnt'>({renewalCount} renewal{(renewalCount != 1 ? "s" : "")})</span></h2></div>");

                sb.Append("<table class='rpt'><thead><tr>");
                sb.Append("<th>Set Code</th><th>Type</th><th>Document #</th>");
                sb.Append("<th>Company</th><th>Site</th><th>Set Status</th>");
                sb.Append("<th>Item Name</th><th>Model</th><th>Serial #</th><th>Qty</th>");
                sb.Append("<th>Start Date</th><th>Expiry Date</th><th>Item Status</th><th>Amount</th>");
                sb.Append("</tr></thead><tbody>");

                bool firstSet = true;
                foreach (var renewal in group)
                {
                    _renewalSetItemsCache.TryGetValue(renewal.SetId, out var items);
                    bool hasItems = items != null && items.Count > 0;

                    if (!hasItems)
                    {
                        string borderCls = !firstSet ? " class='set-border'" : "";
                        sb.Append($"<tr{borderCls}>");
                        sb.Append($"<td class='set-code'>{Esc(renewal.SetCode)}</td>");
                        sb.Append($"<td>{Esc(renewal.SetType)}</td>");
                        sb.Append($"<td>{Esc(renewal.DocumentNumber)}</td>");
                        sb.Append($"<td class='company'>{Esc(renewal.CompanyName)}</td>");
                        sb.Append($"<td>{Esc(renewal.SiteDisplay)}</td>");
                        sb.Append($"<td class='set-status'>{Esc(renewal.SetLevelStatus)}</td>");
                        sb.Append("<td class='empty-items' colspan='6'>No items found</td>");
                        sb.Append("<td class='item-status'>&mdash;</td>");
                        sb.Append($"<td class='amount'>{renewal.TotalAmountDue:N2}</td>");
                        sb.Append("</tr>");
                    }
                    else
                    {
                        bool firstItem = true;
                        foreach (var item in items)
                        {
                            string rowCls = (firstItem && !firstSet) ? " class='set-border'" : "";
                            sb.Append($"<tr{rowCls}>");

                            if (firstItem)
                            {
                                sb.Append($"<td class='set-code' rowspan='{items.Count}'>{Esc(renewal.SetCode)}</td>");
                                sb.Append($"<td rowspan='{items.Count}'>{Esc(renewal.SetType)}</td>");
                                sb.Append($"<td rowspan='{items.Count}'>{Esc(renewal.DocumentNumber)}</td>");
                                sb.Append($"<td class='company' rowspan='{items.Count}'>{Esc(renewal.CompanyName)}</td>");
                                sb.Append($"<td rowspan='{items.Count}'>{Esc(renewal.SiteDisplay)}</td>");
                                sb.Append($"<td class='set-status' rowspan='{items.Count}'>{Esc(renewal.SetLevelStatus)}</td>");
                            }

                            string startDt = item.LineStartDate?.ToString("MM/dd/yyyy") ?? "";
                            string endDt   = item.LineEndDate?.ToString("MM/dd/yyyy")   ?? "";

                            sb.Append($"<td class='item-name'>{Esc(GetRenewalItemName(item))}</td>");
                            sb.Append($"<td>{Esc(GetRenewalModel(item))}</td>");
                            sb.Append($"<td class='serial'>{Esc(GetRenewalSerial(item))}</td>");
                            sb.Append($"<td class='qty'>{item.Quantity}</td>");
                            sb.Append($"<td class='date'>{startDt}</td>");
                            sb.Append($"<td class='date'>{endDt}</td>");
                            sb.Append($"<td class='item-status'>{Esc(item.DisplayStatus)}</td>");
                            sb.Append($"<td class='amount'>{item.Amount:N2}</td>");
                            sb.Append("</tr>");
                            firstItem = false;
                        }
                    }
                    firstSet = false;
                }

                sb.Append("</tbody></table>");
            }

            sb.Append("<div class='rpt-footer'>");
            sb.Append($"<strong>Total:</strong> {_renewals.Count} renewal(s) &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Total Items:</strong> {_renewals.Sum(r => r.ItemCount)} &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Total Amount:</strong> {_renewals.Sum(r => r.TotalAmountDue):N2}");
            sb.Append("</div></div>");
        }

        // ════════════════════════════════════════════════════════════════════
        //  RENEWALS GROUPED SECTION  —  matches ExportRenewalsGroupedPreviewDialog layout
        // ════════════════════════════════════════════════════════════════════

        private void AppendRenewalsGroupedSection(StringBuilder sb)
        {
            sb.Append("<div class='section'>");
            sb.Append("<div class='sec-title'><h1>Renewal Group Export Report</h1>");
            sb.Append($"<p>Generated: {DateTime.Now:MMMM dd, yyyy  h:mm tt}  &nbsp;|&nbsp;  {_renewalGroups.Count} Group(s)</p></div>");

            var sortedGroups = _renewalGroups
                .OrderBy(g => g.RootEndDate ?? DateTime.MinValue)
                .ToList();

            var monthlyBuckets = sortedGroups
                .GroupBy(g => { var dt = g.RootEndDate ?? DateTime.MinValue; return new DateTime(dt.Year, dt.Month, 1); })
                .OrderBy(mb => mb.Key);

            foreach (var monthBucket in monthlyBuckets)
            {
                var allRenewals = monthBucket.SelectMany(g => g.Chain ?? new List<RenewalDto>()).ToList();
                string monthLabel = monthBucket.Key == DateTime.MinValue ? "No Date" : monthBucket.Key.ToString("MMMM yyyy");
                int renewalCount = allRenewals.Count;

                sb.Append($"<div class='month-header'><h2>{Esc(monthLabel)}<span class='cnt'>({renewalCount} renewal{(renewalCount != 1 ? "s" : "")})</span></h2></div>");

                sb.Append("<table class='rpt'><thead><tr>");
                sb.Append("<th>Set Code</th><th>Type</th><th>Document #</th>");
                sb.Append("<th>Company</th><th>Site</th><th>Set Status</th>");
                sb.Append("<th>Item Name</th><th>Model</th><th>Serial #</th><th>Qty</th>");
                sb.Append("<th>Start Date</th><th>Expiry Date</th><th>Item Status</th><th>Amount</th>");
                sb.Append("</tr></thead><tbody>");

                bool firstSet = true;
                foreach (var renewal in allRenewals)
                {
                    _renewalSetItemsCache.TryGetValue(renewal.SetId, out var items);
                    bool hasItems = items != null && items.Count > 0;

                    if (!hasItems)
                    {
                        string borderCls = !firstSet ? " class='set-border'" : "";
                        sb.Append($"<tr{borderCls}>");
                        sb.Append($"<td class='set-code'>{Esc(renewal.SetCode)}</td>");
                        sb.Append($"<td>{Esc(renewal.SetType)}</td>");
                        sb.Append($"<td>{Esc(renewal.DocumentNumber)}</td>");
                        sb.Append($"<td class='company'>{Esc(renewal.CompanyName)}</td>");
                        sb.Append($"<td>{Esc(renewal.SiteDisplay)}</td>");
                        sb.Append($"<td class='set-status'>{Esc(renewal.SetLevelStatus)}</td>");
                        sb.Append("<td class='empty-items' colspan='6'>No items found</td>");
                        sb.Append("<td class='item-status'>&mdash;</td>");
                        sb.Append($"<td class='amount'>{renewal.TotalAmountDue:N2}</td>");
                        sb.Append("</tr>");
                    }
                    else
                    {
                        bool firstItem = true;
                        foreach (var item in items)
                        {
                            string rowCls = (firstItem && !firstSet) ? " class='set-border'" : "";
                            sb.Append($"<tr{rowCls}>");

                            if (firstItem)
                            {
                                sb.Append($"<td class='set-code' rowspan='{items.Count}'>{Esc(renewal.SetCode)}</td>");
                                sb.Append($"<td rowspan='{items.Count}'>{Esc(renewal.SetType)}</td>");
                                sb.Append($"<td rowspan='{items.Count}'>{Esc(renewal.DocumentNumber)}</td>");
                                sb.Append($"<td class='company' rowspan='{items.Count}'>{Esc(renewal.CompanyName)}</td>");
                                sb.Append($"<td rowspan='{items.Count}'>{Esc(renewal.SiteDisplay)}</td>");
                                sb.Append($"<td class='set-status' rowspan='{items.Count}'>{Esc(renewal.SetLevelStatus)}</td>");
                            }

                            string startDt = item.LineStartDate?.ToString("MM/dd/yyyy") ?? "";
                            string endDt   = item.LineEndDate?.ToString("MM/dd/yyyy")   ?? "";

                            sb.Append($"<td class='item-name'>{Esc(GetRenewalItemName(item))}</td>");
                            sb.Append($"<td>{Esc(GetRenewalModel(item))}</td>");
                            sb.Append($"<td class='serial'>{Esc(GetRenewalSerial(item))}</td>");
                            sb.Append($"<td class='qty'>{item.Quantity}</td>");
                            sb.Append($"<td class='date'>{startDt}</td>");
                            sb.Append($"<td class='date'>{endDt}</td>");
                            sb.Append($"<td class='item-status'>{Esc(item.DisplayStatus)}</td>");
                            sb.Append($"<td class='amount'>{item.Amount:N2}</td>");
                            sb.Append("</tr>");
                            firstItem = false;
                        }
                    }
                    firstSet = false;
                }

                sb.Append("</tbody></table>");
            }

            sb.Append("<div class='rpt-footer'>");
            sb.Append($"<strong>Total:</strong> {_renewalGroups.Count} group(s) &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Total Renewals:</strong> {_renewalGroups.Sum(g => g.Chain?.Count ?? 0)} &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Total Items:</strong> {_renewalGroups.Sum(g => g.Chain?.Sum(r => r.ItemCount) ?? 0)} &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Total Amount:</strong> {_renewalGroups.Sum(g => g.Chain?.Sum(r => r.TotalAmountDue) ?? 0m):N2}");
            sb.Append("</div></div>");
        }

        // ════════════════════════════════════════════════════════════════════
        //  CARTRIDGE SECTION  —  matches ExportCartridgeDisposedSoldPreviewDialog layout
        // ════════════════════════════════════════════════════════════════════

        private void AppendCartridgeSection(StringBuilder sb)
        {
            int totalCartridges = _cartridgeAuditTrails?.Values.Sum(t => t.Count) ?? 0;
            int totalQty        = _outboundBatches.Sum(b => b.TotalQty);

            sb.Append("<div class='section'>");
            sb.Append("<div class='sec-title'><h1>Cartridge Disposed/Sold Export Report</h1>");
            sb.Append($"<p>Generated: {DateTime.Now:MMMM dd, yyyy  h:mm tt}  &nbsp;|&nbsp;  {_outboundBatches.Count} Batch(es)  &nbsp;|&nbsp;  {totalCartridges} Cartridge(s)  &nbsp;|&nbsp;  {totalQty} Total Qty</p></div>");

            foreach (var batch in _outboundBatches)
            {
                var trail = (_cartridgeAuditTrails != null && _cartridgeAuditTrails.ContainsKey(batch.BatchId))
                    ? _cartridgeAuditTrails[batch.BatchId]
                    : new List<VendorBatchAuditTrailDto>();

                bool isDispose = batch.BatchPurpose == "DISPOSE";
                string purposeCls = isDispose ? "purpose-dispose" : "purpose-sell";

                sb.Append($"<div class='batch-header'><h2>Batch #{batch.BatchId} &mdash; <span class='{purposeCls}'>{Esc(batch.BatchPurpose)}</span>");
                sb.Append($"<span class='batch-count'>({trail.Count} cartridge{(trail.Count != 1 ? "s" : "")})</span></h2>");
                sb.Append($"<div class='batch-meta'>Vendor: {Esc(batch.VendorName)} &nbsp;&middot;&nbsp; Models: {Esc(batch.ModelSummary ?? "\u2014")} &nbsp;&middot;&nbsp; Total Qty: {batch.TotalQty} &nbsp;&middot;&nbsp; Created: {batch.CreatedDate:MM/dd/yyyy HH:mm}</div>");
                sb.Append("</div>");

                sb.Append("<table class='rpt'><thead><tr>");
                sb.Append("<th>Cartridge ID</th>");
                sb.Append("<th>Request ID</th>");
                sb.Append("<th>Returned Date</th>");
                sb.Append("<th>Qty</th>");
                sb.Append("<th>Cartridge Model</th>");
                sb.Append("<th>Vendor</th>");
                sb.Append("<th>Returned By</th>");
                sb.Append("<th>Remarks</th>");
                sb.Append("</tr></thead><tbody>");

                if (trail.Count == 0)
                {
                    sb.Append("<tr><td colspan='8' style='text-align:center;color:#6c757d;font-style:italic;padding:12px;'>No cartridges assigned to this batch.</td></tr>");
                }
                else
                {
                    foreach (var item in trail)
                    {
                        sb.Append("<tr>");
                        sb.Append($"<td class='cartridge-id'>{item.EmptyCartridgeId}</td>");
                        sb.Append($"<td>{(item.RequestId.HasValue ? item.RequestId.Value.ToString() : "\u2014")}</td>");
                        sb.Append($"<td>{item.RequestDate:MM/dd/yyyy HH:mm}</td>");
                        sb.Append($"<td class='qty'>{item.ReturnedQty}</td>");
                        sb.Append($"<td class='model'>{Esc(item.CartridgeModel)}</td>");
                        sb.Append($"<td>{Esc(item.Vendor)}</td>");
                        sb.Append($"<td>{Esc(item.ReturnedByName)}</td>");
                        sb.Append($"<td>{Esc(item.Remarks)}</td>");
                        sb.Append("</tr>");
                    }
                }

                sb.Append("</tbody></table>");
            }

            sb.Append("<div class='rpt-footer'>");
            sb.Append($"<strong>Total:</strong> {_outboundBatches.Count} batch(es) &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Cartridges:</strong> {totalCartridges} &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>DISPOSE:</strong> {_outboundBatches.Count(b => b.BatchPurpose == "DISPOSE")} batch(es) &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>SELL:</strong> {_outboundBatches.Count(b => b.BatchPurpose == "SELL")} batch(es) &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Total Qty:</strong> {totalQty}");
            sb.Append("</div></div>");
        }

        // ════════════════════════════════════════════════════════════════════
        //  PDF EXPORT — PdfSharp, renders each section matching the preview
        // ════════════════════════════════════════════════════════════════════

        private void OnExportToPdf(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog
            {
                Title      = "Save Combined Report",
                Filter     = "PDF files (*.pdf)|*.pdf",
                FileName   = $"CombinedReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                DefaultExt = "pdf"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    GenerateCombinedPdf(dlg.FileName);
                    Close();
                    System.Diagnostics.Process.Start(dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void GenerateCombinedPdf(string outputPath)
        {
            // ── Shared layout constants ─────────────────────────────────────
            const double margin  = 24;
            const double pageW   = 841.89;   // A4 landscape
            const double pageH   = 595.28;
            const double tableW  = pageW - margin * 2;
            const double padX    = 4;
            const double padY    = 2;
            const double hTitle  = 34;
            const double hMonth  = 22;
            const double hHdr    = 18;
            const double hRow    = 14;
            const double hFooter = 20;
            const double hBatch  = 28;

            // Fonts
            var fTitle    = new XFont("Arial", 14, XFontStyle.Bold);
            var fSubtitle = new XFont("Arial",  7.5, XFontStyle.Regular);
            var fMonth    = new XFont("Arial", 10, XFontStyle.Bold);
            var fMonthCnt = new XFont("Arial",  7.5, XFontStyle.Regular);
            var fHdr      = new XFont("Arial",  6.5, XFontStyle.Bold);
            var fCell     = new XFont("Arial",  7, XFontStyle.Regular);
            var fCellBold = new XFont("Arial",  7, XFontStyle.Bold);
            var fFooter   = new XFont("Arial",  8, XFontStyle.Regular);
            var fFooterB  = new XFont("Arial",  8, XFontStyle.Bold);
            var fYear     = new XFont("Arial", 12, XFontStyle.Bold);
            var fMonthBnr = new XFont("Arial", 10, XFontStyle.Bold);
            var fColHdr   = new XFont("Arial",  8, XFontStyle.Bold);
            var fSum      = new XFont("Arial",  7.5, XFontStyle.Bold);
            var fTot      = new XFont("Arial",  8.5, XFontStyle.Bold);
            var fBatchSub = new XFont("Arial",  7.5, XFontStyle.Regular);

            // Brushes & pens
            var bNavy     = new XSolidBrush(XColor.FromArgb( 30,  64, 111));
            var bMidBlue  = new XSolidBrush(XColor.FromArgb( 44, 111, 173));
            var bSlate    = new XSolidBrush(XColor.FromArgb( 52,  73,  94));
            var bMonthBg  = new XSolidBrush(XColor.FromArgb(248, 249, 250));
            var bAlt      = new XSolidBrush(XColor.FromArgb(248, 249, 250));
            var bBlack    = new XSolidBrush(XColor.FromArgb( 33,  37,  41));
            var bGray     = new XSolidBrush(XColor.FromArgb(108, 117, 125));
            var bSubFg    = new XSolidBrush(XColor.FromArgb(176, 204, 228));
            var bFooterBg = new XSolidBrush(XColor.FromArgb(248, 249, 250));
            var bBatchBg  = new XSolidBrush(XColor.FromArgb(232, 236, 241));
            var bTotalBg  = new XSolidBrush(XColor.FromArgb(235, 245, 251));
            var bSumLbl   = new XSolidBrush(XColor.FromArgb( 73,  80,  87));
            var bDispose  = new XSolidBrush(XColor.FromArgb(153,  27,  27));
            var bSell     = new XSolidBrush(XColor.FromArgb( 21, 128,  61));
            var pBorder   = new XPen(XColor.FromArgb(206, 212, 218), 0.4);
            var pStrong   = new XPen(XColor.FromArgb(108, 117, 125), 0.6);
            var pSetBorder= new XPen(XColor.FromArgb(206, 212, 218), 1.0);
            var pMonthTop = new XPen(XColor.FromArgb( 30,  64, 111), 1.5);

            // ── Document & page state ─────────────────────────────────────
            var doc = new PdfDocument { Info = { Title = "Combined Report", Author = "Yakult.Inventory.App" } };
            PdfPage   page = null;
            XGraphics gfx  = null;
            double    y    = 0;

            double Bottom() => pageH - margin;

            void NewPage()
            {
                page = doc.AddPage();
                page.Orientation = PdfSharp.PageOrientation.Landscape;
                page.Size        = PdfSharp.PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                y   = margin;
            }

            string TrimText(string text, XFont f, double maxW)
            {
                if (string.IsNullOrEmpty(text) || maxW <= 0) return "";
                if (gfx.MeasureString(text, f).Width <= maxW) return text;
                const string ell = "...";
                double ew = gfx.MeasureString(ell, f).Width;
                for (int i = text.Length - 1; i > 0; i--)
                { var t = text.Substring(0, i); if (gfx.MeasureString(t, f).Width + ew <= maxW) return t + ell; }
                return ell;
            }

            void OuterV(double ry, double rh, double m, double tw)
            {
                gfx.DrawLine(pStrong, m, ry, m, ry + rh);
                gfx.DrawLine(pStrong, m + tw, ry, m + tw, ry + rh);
            }

            void DrawSectionTitle(string title, string subtitle)
            {
                NewPage();
                gfx.DrawRectangle(bNavy,    margin, y, tableW, hTitle);
                gfx.DrawRectangle(bMidBlue, margin, y + hTitle, tableW, 3);
                gfx.DrawString(title, fTitle, XBrushes.White,
                    new XRect(margin + 10, y, tableW - 10, hTitle * 0.62), XStringFormats.BottomLeft);
                gfx.DrawString(subtitle, fSubtitle, bSubFg,
                    new XRect(margin + 10, y + hTitle * 0.62, tableW - 10, hTitle * 0.38), XStringFormats.TopLeft);
                y += hTitle + 3 + 6;
            }

            // ═══════════════════════════════════════════════════════════════
            //  INVOICE SECTION PDF
            // ═══════════════════════════════════════════════════════════════
            if (_invoiceHeaders != null && _invoiceHeaders.Count > 0)
            {
                double[] icw = { 60, 100, 76, 48, 68, 32, 148, 108, 72, 94 };
                { double s = icw.Sum(); for (int i = 0; i < icw.Length; i++) icw[i] = icw[i] / s * tableW; }
                double IColX(int c) { double x = margin; for (int i = 0; i < c; i++) x += icw[i]; return x; }

                const double hYearRow  = 22;
                const double hMonthRow = 18;
                const double hColHdr   = 20;
                const double hItemRow  = 16;
                const double hSumRow   = 15;
                const double hTotalRow = 18;
                const double hSep      = 7;

                DrawSectionTitle("Invoice Report",
                    $"Generated: {DateTime.Now:MMMM dd, yyyy  h:mm tt}   |   {_invoiceHeaders.Count} Invoice(s)   |   {(_invoiceItems?.Rows.Count ?? 0)} Item(s)");

                void InvBanner(XBrush bg, XBrush fg, XFont f, string text, double h)
                {
                    if (y + h > Bottom()) NewPage();
                    gfx.DrawRectangle(bg, margin, y, tableW, h);
                    gfx.DrawString(text, f, fg, new XRect(margin, y, tableW, h), XStringFormats.Center);
                    gfx.DrawLine(pStrong, margin, y + h, margin + tableW, y + h);
                    y += h;
                }

                void InvColHeaders()
                {
                    if (y + hColHdr > Bottom()) NewPage();
                    string[] names = {"DATE","COMPANY","DOCUMENT #","STATUS","REFERENCE #","QTY","ITEM NAME","DURATION","AMOUNT","SITE"};
                    bool[]   right = {false,false,false,false,false,true,false,false,true,false};
                    gfx.DrawRectangle(bMidBlue, margin, y, tableW, hColHdr);
                    for (int c = 0; c < names.Length; c++)
                    {
                        var r = new XRect(IColX(c) + padX, y + padY, icw[c] - padX * 2, hColHdr - padY * 2);
                        gfx.DrawString(names[c], fColHdr, XBrushes.White, r, right[c] ? XStringFormats.TopRight : XStringFormats.TopLeft);
                        if (c < names.Length - 1) gfx.DrawLine(pBorder, IColX(c + 1), y, IColX(c + 1), y + hColHdr);
                    }
                    OuterV(y, hColHdr, margin, tableW);
                    gfx.DrawLine(pStrong, margin, y + hColHdr, margin + tableW, y + hColHdr);
                    y += hColHdr;
                }

                void InvSumRow(string label, string val, bool isTotal)
                {
                    double h = isTotal ? hTotalRow : hSumRow;
                    if (isTotal) gfx.DrawRectangle(bTotalBg, margin, y, tableW, h);
                    var lf = isTotal ? fTot : fSum;
                    var lb = isTotal ? bNavy : bSumLbl;
                    gfx.DrawString(label, lf, lb, new XRect(IColX(7) + padX, y + padY, icw[7] - padX * 2, h - padY * 2), XStringFormats.TopRight);
                    gfx.DrawString(val, lf, isTotal ? bNavy : bBlack, new XRect(IColX(8) + padX, y + padY, icw[8] - padX * 2, h - padY * 2), XStringFormats.TopRight);
                    gfx.DrawLine(pBorder, margin, y + h, margin + tableW, y + h);
                    OuterV(y, h, margin, tableW);
                    y += h;
                }

                var sorted = _invoiceHeaders
                    .Select(r => { object d = r["InvoiceDate"]; return new { Row = r, Date = d != null && d != DBNull.Value ? (DateTime?)Convert.ToDateTime(d) : null }; })
                    .OrderBy(x => x.Date ?? DateTime.MaxValue).ToList();

                int prevYear = -1, prevMonth = -1;

                foreach (var x in sorted)
                {
                    var hdr = x.Row;
                    DateTime? invDate = x.Date;
                    int yr = invDate?.Year ?? 0, mo = invDate?.Month ?? 0;

                    if (yr != prevYear)
                    {
                        InvBanner(bBlack, XBrushes.White, fYear, yr > 0 ? yr.ToString() : "Unknown", hYearRow);
                        prevYear = yr; prevMonth = -1;
                    }
                    if (mo != prevMonth)
                    {
                        string mn = invDate.HasValue ? invDate.Value.ToString("MMMM") : "Unknown";
                        InvBanner(bMidBlue, XBrushes.White, fMonthBnr, mn, hMonthRow);
                        InvColHeaders();
                        prevMonth = mo;
                    }

                    string setCode = hdr["SetCode"]?.ToString() ?? "";
                    string company = hdr["CompanyName"]?.ToString() ?? "";
                    string docNum  = hdr["DocumentNumber"]?.ToString() ?? "";
                    string refNum  = hdr["ReferenceNumber"]?.ToString() ?? "";
                    string site    = ColVal(hdr, "Site");
                    string statusR = hdr["Status"]?.ToString() ?? "";
                    string dateStr = invDate.HasValue ? invDate.Value.ToString("M/d/yyyy") : "";

                    var groupItems = new List<DataRow>();
                    if (_invoiceItems != null)
                        foreach (DataRow it in _invoiceItems.Rows)
                            if (string.Equals(ColVal(it, "SetCode"), setCode, StringComparison.OrdinalIgnoreCase))
                                groupItems.Add(it);

                    var items = groupItems.Count > 0 ? groupItems : new List<DataRow> { null };
                    foreach (var it in items)
                    {
                        if (y + hItemRow > Bottom()) { NewPage(); InvColHeaders(); }

                        string[] vals = {
                            dateStr, company, docNum, statusR, refNum,
                            it != null ? (decimal.TryParse(ColVal(it, "Quantity"), out var q) ? q.ToString("N0") : "0") : "",
                            it != null ? ColVal(it, "ItemName") : "(no items)",
                            "", "", site
                        };
                        if (it != null) {
                            var sd2 = SafeDate(it, "LineStartDate"); var ed2 = SafeDate(it, "LineEndDate");
                            vals[7] = (sd2.HasValue && ed2.HasValue) ? $"{sd2.Value:yyyy-MM-dd}  {ed2.Value:yyyy-MM-dd}" : sd2.HasValue ? sd2.Value.ToString("yyyy-MM-dd") : "";
                        }
                        bool[] rAlign = {false,false,false,false,false,true,false,false,true,false};
                        for (int c = 0; c < vals.Length; c++)
                        {
                            var rect = new XRect(IColX(c) + padX, y + padY, icw[c] - padX * 2, hItemRow - padY * 2);
                            gfx.DrawString(TrimText(vals[c], fCell, rect.Width), fCell, bBlack, rect, rAlign[c] ? XStringFormats.TopRight : XStringFormats.TopLeft);
                        }
                        gfx.DrawLine(pBorder, margin, y + hItemRow, margin + tableW, y + hItemRow);
                        for (int c = 1; c < icw.Length; c++) gfx.DrawLine(pBorder, IColX(c), y, IColX(c), y + hItemRow);
                        OuterV(y, hItemRow, margin, tableW);
                        y += hItemRow;
                    }

                    InvSumRow("Subtotal",     SafeDecimal(hdr, "Subtotal").ToString("N2"),       false);
                    InvSumRow("VAT",          SafeDecimal(hdr, "VatAmount").ToString("N2"),      false);
                    InvSumRow("WHT",          SafeDecimal(hdr, "WhtAmount").ToString("N2"),      false);
                    InvSumRow("Discount",     SafeDecimal(hdr, "DiscountAmount").ToString("N2"), false);
                    InvSumRow("Total Amount", SafeDecimal(hdr, "TotalAmountDue").ToString("N2"), true);

                    gfx.DrawRectangle(bSlate, margin, y, tableW, hSep);
                    y += hSep + 2;
                }
            }

            // ═══════════════════════════════════════════════════════════════
            //  SETS SECTION PDF
            // ═══════════════════════════════════════════════════════════════
            if (_sets != null && _sets.Count > 0)
            {
                double[] scw = { 58, 82, 38, 110, 72, 68, 52, 62, 28, 62, 44, 72, 62, 62 };
                { double s = scw.Sum(); for (int i = 0; i < scw.Length; i++) scw[i] = scw[i] / s * tableW; }
                double SColX(int c) { double x = margin; for (int i = 0; i < c; i++) x += scw[i]; return x; }

                DrawSectionTitle("Sets Export Report",
                    $"Generated: {DateTime.Now:MMMM dd, yyyy  h:mm tt}   |   {_sets.Count} Set(s)");

                void SetsColHeaders()
                {
                    if (y + hHdr > Bottom()) NewPage();
                    string[] names = { "SET CODE", "EMPLOYEE", "ITEMS", "ITEM NAME", "MODEL", "SERIAL #", "ITEM TYPE", "CATEGORY", "QTY", "DISPATCHED", "AMOUNT", "COMPANY", "BRANCH", "DEPARTMENT" };
                    gfx.DrawRectangle(bSlate, margin, y, tableW, hHdr);
                    for (int c = 0; c < names.Length; c++)
                    {
                        var r = new XRect(SColX(c) + padX, y + padY, scw[c] - padX * 2, hHdr - padY * 2);
                        gfx.DrawString(names[c], fHdr, XBrushes.White, r, XStringFormats.CenterLeft);
                        if (c < names.Length - 1) gfx.DrawLine(new XPen(XColor.FromArgb(74, 98, 116), 0.3), SColX(c + 1), y, SColX(c + 1), y + hHdr);
                    }
                    OuterV(y, hHdr, margin, tableW);
                    gfx.DrawLine(pStrong, margin, y + hHdr, margin + tableW, y + hHdr);
                    y += hHdr;
                }

                var sortedSets = _sets.OrderBy(s2 => s2.DispatchDate ?? s2.CreatedAt).ToList();
                var groupedSets = sortedSets.GroupBy(s2 => { var dt = s2.DispatchDate ?? s2.CreatedAt; return new DateTime(dt.Year, dt.Month, 1); }).OrderBy(g => g.Key);
                int sRowIdx = 0;

                foreach (var group in groupedSets)
                {
                    if (y + hMonth + hHdr + hRow > Bottom()) NewPage();
                    gfx.DrawLine(pMonthTop, margin, y, margin + tableW, y);
                    gfx.DrawRectangle(bMonthBg, margin, y, tableW, hMonth);
                    string ml = group.Key.ToString("MMMM yyyy");
                    gfx.DrawString(ml, fMonth, bNavy, new XRect(margin + 10, y, tableW * 0.5, hMonth), XStringFormats.CenterLeft);
                    gfx.DrawString($"({group.Count()} set{(group.Count() != 1 ? "s" : "")})", fMonthCnt, bGray,
                        new XRect(margin + 10 + gfx.MeasureString(ml, fMonth).Width + 8, y, 100, hMonth), XStringFormats.CenterLeft);
                    gfx.DrawLine(pBorder, margin, y + hMonth, margin + tableW, y + hMonth);
                    OuterV(y, hMonth, margin, tableW);
                    y += hMonth;
                    SetsColHeaders();

                    bool firstSetInGroup = true;
                    foreach (var set in group)
                    {
                        string dispatch = set.DispatchDate.HasValue ? set.DispatchDate.Value.ToString("MM/dd/yyyy") : "";
                        _setItemsCache.TryGetValue(set.SetId, out var sitems);
                        bool hasItems = sitems != null && sitems.Count > 0;

                        if (!hasItems)
                        {
                            if (y + hRow > Bottom()) { NewPage(); SetsColHeaders(); }
                            if (!firstSetInGroup) gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);
                            if (sRowIdx % 2 == 1) gfx.DrawRectangle(bAlt, margin, y, tableW, hRow);
                            string[] vals = { set.SetCode ?? "", set.CurrentEmployeeName ?? "", set.ItemCount.ToString(), "No items found", "", "", "", "", "", dispatch, "\u2014", set.CurrentCompanyName ?? set.Company ?? "", set.CurrentBranchName ?? "", set.CurrentDepartmentName ?? "" };
                            for (int c = 0; c < vals.Length; c++)
                            {
                                var rect = new XRect(SColX(c) + padX, y + padY, scw[c] - padX * 2, hRow - padY * 2);
                                var font = c == 0 ? fCellBold : fCell;
                                var brush = c == 0 ? bNavy : (c == 3 ? bGray : bBlack);
                                gfx.DrawString(TrimText(vals[c], font, rect.Width), font, brush, rect, XStringFormats.TopLeft);
                            }
                            gfx.DrawLine(pBorder, margin, y + hRow, margin + tableW, y + hRow);
                            for (int c = 1; c < scw.Length; c++) gfx.DrawLine(pBorder, SColX(c), y, SColX(c), y + hRow);
                            OuterV(y, hRow, margin, tableW);
                            y += hRow; sRowIdx++;
                        }
                        else
                        {
                            bool firstItem = true;
                            foreach (var item in sitems)
                            {
                                if (y + hRow > Bottom()) { NewPage(); SetsColHeaders(); }
                                if (firstItem && !firstSetInGroup) gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);
                                if (sRowIdx % 2 == 1) gfx.DrawRectangle(bAlt, margin, y, tableW, hRow);
                                string[] vals = { firstItem ? (set.SetCode ?? "") : "", firstItem ? (set.CurrentEmployeeName ?? "") : "", firstItem ? set.ItemCount.ToString() : "", item.ItemName ?? "", item.ModelNumber ?? "", item.SerialNumber ?? "", item.Status ?? "", item.Category ?? "", item.Quantity.ToString(), firstItem ? dispatch : "", "\u2014", firstItem ? (set.CurrentCompanyName ?? set.Company ?? "") : "", firstItem ? (set.CurrentBranchName ?? "") : "", firstItem ? (set.CurrentDepartmentName ?? "") : "" };
                                for (int c = 0; c < vals.Length; c++)
                                {
                                    var rect = new XRect(SColX(c) + padX, y + padY, scw[c] - padX * 2, hRow - padY * 2);
                                    var font = (c == 0 && firstItem) ? fCellBold : ((c == 11 && firstItem) ? fCellBold : fCell);
                                    var brush = (c == 0 && firstItem) ? bNavy : ((c == 11 && firstItem) ? bNavy : bBlack);
                                    gfx.DrawString(TrimText(vals[c], font, rect.Width), font, brush, rect, XStringFormats.TopLeft);
                                }
                                gfx.DrawLine(pBorder, margin, y + hRow, margin + tableW, y + hRow);
                                for (int c = 1; c < scw.Length; c++) gfx.DrawLine(pBorder, SColX(c), y, SColX(c), y + hRow);
                                OuterV(y, hRow, margin, tableW);
                                y += hRow; sRowIdx++; firstItem = false;
                            }
                        }
                        firstSetInGroup = false;
                    }
                    gfx.DrawLine(pStrong, margin, y, margin + tableW, y);
                }

                // Sets footer
                if (y + hFooter > Bottom()) NewPage();
                gfx.DrawRectangle(bFooterBg, margin, y, tableW, hFooter);
                gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);
                var sdates = _sets.Where(s2 => s2.DispatchDate.HasValue).Select(s2 => s2.DispatchDate.Value).ToList();
                string sDateRange = sdates.Count > 0 ? $"{sdates.Min():MM/dd/yyyy} \u2013 {sdates.Max():MM/dd/yyyy}" : "N/A";
                double sfx = margin + 10;
                gfx.DrawString("Total: ", fFooterB, bBlack, new XRect(sfx, y, 40, hFooter), XStringFormats.CenterLeft);
                sfx += gfx.MeasureString("Total: ", fFooterB).Width;
                gfx.DrawString($"{_sets.Count} set(s)   \u00B7   ", fFooter, bGray, new XRect(sfx, y, 120, hFooter), XStringFormats.CenterLeft);
                sfx += gfx.MeasureString($"{_sets.Count} set(s)   \u00B7   ", fFooter).Width;
                gfx.DrawString("Date Range: ", fFooterB, bBlack, new XRect(sfx, y, 80, hFooter), XStringFormats.CenterLeft);
                sfx += gfx.MeasureString("Date Range: ", fFooterB).Width;
                gfx.DrawString(sDateRange, fFooter, bGray, new XRect(sfx, y, 150, hFooter), XStringFormats.CenterLeft);
            }

            // ═══════════════════════════════════════════════════════════════
            //  RENEWAL SECTION PDF
            // ═══════════════════════════════════════════════════════════════
            if (_renewals != null && _renewals.Count > 0)
            {
                double[] rcw = { 58, 48, 58, 72, 72, 58, 100, 68, 68, 28, 52, 52, 52, 52 };
                { double s = rcw.Sum(); for (int i = 0; i < rcw.Length; i++) rcw[i] = rcw[i] / s * tableW; }
                double RColX(int c) { double x = margin; for (int i = 0; i < c; i++) x += rcw[i]; return x; }

                DrawSectionTitle("Renewal Export Report",
                    $"Generated: {DateTime.Now:MMMM dd, yyyy  h:mm tt}   |   {_renewals.Count} Renewal(s)");

                void RenColHeaders()
                {
                    if (y + hHdr > Bottom()) NewPage();
                    string[] names = { "SET CODE", "TYPE", "DOC #", "COMPANY", "SITE", "SET STATUS", "ITEM NAME", "MODEL", "SERIAL #", "QTY", "START DATE", "EXPIRY DATE", "ITEM STATUS", "AMOUNT" };
                    bool[] right = { false, false, false, false, false, false, false, false, false, true, false, false, false, true };
                    gfx.DrawRectangle(bSlate, margin, y, tableW, hHdr);
                    for (int c = 0; c < names.Length; c++)
                    {
                        var r = new XRect(RColX(c) + padX, y + padY, rcw[c] - padX * 2, hHdr - padY * 2);
                        gfx.DrawString(names[c], fHdr, XBrushes.White, r, right[c] ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
                        if (c < names.Length - 1) gfx.DrawLine(new XPen(XColor.FromArgb(74, 98, 116), 0.3), RColX(c + 1), y, RColX(c + 1), y + hHdr);
                    }
                    OuterV(y, hHdr, margin, tableW);
                    gfx.DrawLine(pStrong, margin, y + hHdr, margin + tableW, y + hHdr);
                    y += hHdr;
                }

                var sortedRen = _renewals.OrderBy(r2 => r2.StartDate ?? r2.CreatedDate ?? DateTime.MinValue).ToList();
                var groupedRen = sortedRen.GroupBy(r2 => { var dt = r2.StartDate ?? r2.CreatedDate ?? DateTime.MinValue; return new DateTime(dt.Year, dt.Month, 1); }).OrderBy(g => g.Key);
                int rRowIdx = 0;

                foreach (var group in groupedRen)
                {
                    if (y + hMonth + hHdr + hRow > Bottom()) NewPage();
                    gfx.DrawLine(pMonthTop, margin, y, margin + tableW, y);
                    gfx.DrawRectangle(bMonthBg, margin, y, tableW, hMonth);
                    string rl = group.Key == DateTime.MinValue ? "No Date" : group.Key.ToString("MMMM yyyy");
                    gfx.DrawString(rl, fMonth, bNavy, new XRect(margin + 10, y, tableW * 0.5, hMonth), XStringFormats.CenterLeft);
                    gfx.DrawString($"({group.Count()} renewal{(group.Count() != 1 ? "s" : "")})", fMonthCnt, bGray,
                        new XRect(margin + 10 + gfx.MeasureString(rl, fMonth).Width + 8, y, 100, hMonth), XStringFormats.CenterLeft);
                    gfx.DrawLine(pBorder, margin, y + hMonth, margin + tableW, y + hMonth);
                    OuterV(y, hMonth, margin, tableW);
                    y += hMonth;
                    RenColHeaders();

                    bool firstSet = true;
                    foreach (var renewal in group)
                    {
                        _renewalSetItemsCache.TryGetValue(renewal.SetId, out var ritems);
                        bool hasItems = ritems != null && ritems.Count > 0;
                        if (!hasItems)
                        {
                            if (y + hRow > Bottom()) { NewPage(); RenColHeaders(); }
                            if (!firstSet) gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);
                            if (rRowIdx % 2 == 1) gfx.DrawRectangle(bAlt, margin, y, tableW, hRow);
                            string[] vals = { renewal.SetCode ?? "", renewal.SetType ?? "", renewal.DocumentNumber ?? "", renewal.CompanyName ?? "", renewal.SiteDisplay ?? "", renewal.SetLevelStatus ?? "", "No items found", "", "", "", "", "", "\u2014", renewal.TotalAmountDue.ToString("N2") };
                            bool[] ra = { false, false, false, false, false, false, false, false, false, false, false, false, false, true };
                            for (int c = 0; c < vals.Length; c++)
                            {
                                var rect = new XRect(RColX(c) + padX, y + padY, rcw[c] - padX * 2, hRow - padY * 2);
                                var font = (c == 0 || c == 3) ? fCellBold : fCell;
                                var brush = (c == 0 || c == 3) ? bNavy : (c == 6 ? bGray : bBlack);
                                gfx.DrawString(TrimText(vals[c], font, rect.Width), font, brush, rect, ra[c] ? XStringFormats.TopRight : XStringFormats.TopLeft);
                            }
                            gfx.DrawLine(pBorder, margin, y + hRow, margin + tableW, y + hRow);
                            for (int c = 1; c < rcw.Length; c++) gfx.DrawLine(pBorder, RColX(c), y, RColX(c), y + hRow);
                            OuterV(y, hRow, margin, tableW);
                            y += hRow; rRowIdx++;
                        }
                        else
                        {
                            bool firstItem = true;
                            foreach (var item in ritems)
                            {
                                if (y + hRow > Bottom()) { NewPage(); RenColHeaders(); }
                                if (firstItem && !firstSet) gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);
                                if (rRowIdx % 2 == 1) gfx.DrawRectangle(bAlt, margin, y, tableW, hRow);
                                string startDt = item.LineStartDate?.ToString("MM/dd/yyyy") ?? "";
                                string endDt   = item.LineEndDate?.ToString("MM/dd/yyyy") ?? "";
                                string[] vals = { firstItem ? (renewal.SetCode ?? "") : "", firstItem ? (renewal.SetType ?? "") : "", firstItem ? (renewal.DocumentNumber ?? "") : "", firstItem ? (renewal.CompanyName ?? "") : "", firstItem ? (renewal.SiteDisplay ?? "") : "", firstItem ? (renewal.SetLevelStatus ?? "") : "", GetRenewalItemName(item), GetRenewalModel(item), GetRenewalSerial(item), item.Quantity.ToString(), startDt, endDt, item.DisplayStatus ?? "", item.Amount.ToString("N2") };
                                bool[] ra = { false, false, false, false, false, false, false, false, false, true, false, false, false, true };
                                for (int c = 0; c < vals.Length; c++)
                                {
                                    var rect = new XRect(RColX(c) + padX, y + padY, rcw[c] - padX * 2, hRow - padY * 2);
                                    var font = (c == 0 && firstItem) ? fCellBold : ((c == 3 && firstItem) ? fCellBold : (c == 6 ? fCellBold : fCell));
                                    var brush = ((c == 0 || c == 3) && firstItem) ? bNavy : bBlack;
                                    gfx.DrawString(TrimText(vals[c], font, rect.Width), font, brush, rect, ra[c] ? XStringFormats.TopRight : XStringFormats.TopLeft);
                                }
                                gfx.DrawLine(pBorder, margin, y + hRow, margin + tableW, y + hRow);
                                for (int c = 1; c < rcw.Length; c++) gfx.DrawLine(pBorder, RColX(c), y, RColX(c), y + hRow);
                                OuterV(y, hRow, margin, tableW);
                                y += hRow; rRowIdx++; firstItem = false;
                            }
                        }
                        firstSet = false;
                    }
                    gfx.DrawLine(pStrong, margin, y, margin + tableW, y);
                }

                // Renewal footer
                if (y + hFooter > Bottom()) NewPage();
                gfx.DrawRectangle(bFooterBg, margin, y, tableW, hFooter);
                gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);
                double rfx = margin + 10;
                gfx.DrawString("Total: ", fFooterB, bBlack, new XRect(rfx, y, 40, hFooter), XStringFormats.CenterLeft);
                rfx += gfx.MeasureString("Total: ", fFooterB).Width;
                gfx.DrawString($"{_renewals.Count} renewal(s)   \u00B7   ", fFooter, bGray, new XRect(rfx, y, 120, hFooter), XStringFormats.CenterLeft);
                rfx += gfx.MeasureString($"{_renewals.Count} renewal(s)   \u00B7   ", fFooter).Width;
                gfx.DrawString("Total Amount: ", fFooterB, bBlack, new XRect(rfx, y, 80, hFooter), XStringFormats.CenterLeft);
                rfx += gfx.MeasureString("Total Amount: ", fFooterB).Width;
                gfx.DrawString($"{_renewals.Sum(r2 => r2.TotalAmountDue):N2}", fFooter, bGray, new XRect(rfx, y, 100, hFooter), XStringFormats.CenterLeft);
            }

            // ═══════════════════════════════════════════════════════════════
            //  RENEWALS GROUPED SECTION PDF
            // ═══════════════════════════════════════════════════════════════
            if (_renewalGroups != null && _renewalGroups.Count > 0)
            {
                double[] gcw = { 58, 48, 58, 72, 72, 58, 100, 68, 68, 28, 52, 52, 52, 52 };
                { double s = gcw.Sum(); for (int i = 0; i < gcw.Length; i++) gcw[i] = gcw[i] / s * tableW; }
                double GColX(int c) { double x = margin; for (int i = 0; i < c; i++) x += gcw[i]; return x; }

                DrawSectionTitle("Renewal Group Export Report",
                    $"Generated: {DateTime.Now:MMMM dd, yyyy  h:mm tt}   |   {_renewalGroups.Count} Group(s)");

                void GrpColHeaders()
                {
                    if (y + hHdr > Bottom()) NewPage();
                    string[] names = { "SET CODE", "TYPE", "DOC #", "COMPANY", "SITE", "SET STATUS", "ITEM NAME", "MODEL", "SERIAL #", "QTY", "START DATE", "EXPIRY DATE", "ITEM STATUS", "AMOUNT" };
                    bool[] right = { false, false, false, false, false, false, false, false, false, true, false, false, false, true };
                    gfx.DrawRectangle(bSlate, margin, y, tableW, hHdr);
                    for (int c = 0; c < names.Length; c++)
                    {
                        var r = new XRect(GColX(c) + padX, y + padY, gcw[c] - padX * 2, hHdr - padY * 2);
                        gfx.DrawString(names[c], fHdr, XBrushes.White, r, right[c] ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
                        if (c < names.Length - 1) gfx.DrawLine(new XPen(XColor.FromArgb(74, 98, 116), 0.3), GColX(c + 1), y, GColX(c + 1), y + hHdr);
                    }
                    OuterV(y, hHdr, margin, tableW);
                    gfx.DrawLine(pStrong, margin, y + hHdr, margin + tableW, y + hHdr);
                    y += hHdr;
                }

                var sortedGrp = _renewalGroups.OrderBy(g => g.RootEndDate ?? DateTime.MinValue).ToList();
                var monthlyBkt = sortedGrp.GroupBy(g => { var dt = g.RootEndDate ?? DateTime.MinValue; return new DateTime(dt.Year, dt.Month, 1); }).OrderBy(mb => mb.Key);
                int gRowIdx = 0;

                foreach (var monthBucket in monthlyBkt)
                {
                    var allRen = monthBucket.SelectMany(g => g.Chain ?? new List<RenewalDto>()).ToList();
                    if (y + hMonth + hHdr + hRow > Bottom()) NewPage();
                    string gl = monthBucket.Key == DateTime.MinValue ? "No Date" : monthBucket.Key.ToString("MMMM yyyy");
                    gfx.DrawLine(pMonthTop, margin, y, margin + tableW, y);
                    gfx.DrawRectangle(bBatchBg, margin, y, tableW, hMonth);
                    gfx.DrawString(gl, fMonth, bNavy, new XRect(margin + 10, y, tableW * 0.5, hMonth), XStringFormats.CenterLeft);
                    gfx.DrawString($"({allRen.Count} renewal{(allRen.Count != 1 ? "s" : "")})", fMonthCnt, bGray,
                        new XRect(margin + 10 + gfx.MeasureString(gl, fMonth).Width + 8, y, 100, hMonth), XStringFormats.CenterLeft);
                    gfx.DrawLine(pBorder, margin, y + hMonth, margin + tableW, y + hMonth);
                    OuterV(y, hMonth, margin, tableW);
                    y += hMonth;
                    GrpColHeaders();

                    bool firstSet = true;
                    foreach (var renewal in allRen)
                    {
                        _renewalSetItemsCache.TryGetValue(renewal.SetId, out var gitems);
                        bool hasItems = gitems != null && gitems.Count > 0;
                        if (!hasItems)
                        {
                            if (y + hRow > Bottom()) { NewPage(); GrpColHeaders(); }
                            if (!firstSet) gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);
                            if (gRowIdx % 2 == 1) gfx.DrawRectangle(bAlt, margin, y, tableW, hRow);
                            string[] vals = { renewal.SetCode ?? "", renewal.SetType ?? "", renewal.DocumentNumber ?? "", renewal.CompanyName ?? "", renewal.SiteDisplay ?? "", renewal.SetLevelStatus ?? "", "No items found", "", "", "", "", "", "\u2014", renewal.TotalAmountDue.ToString("N2") };
                            bool[] ra = { false, false, false, false, false, false, false, false, false, false, false, false, false, true };
                            for (int c = 0; c < vals.Length; c++)
                            {
                                var rect = new XRect(GColX(c) + padX, y + padY, gcw[c] - padX * 2, hRow - padY * 2);
                                var font = (c == 0 || c == 3) ? fCellBold : fCell;
                                var brush = (c == 0 || c == 3) ? bNavy : (c == 6 ? bGray : bBlack);
                                gfx.DrawString(TrimText(vals[c], font, rect.Width), font, brush, rect, ra[c] ? XStringFormats.TopRight : XStringFormats.TopLeft);
                            }
                            gfx.DrawLine(pBorder, margin, y + hRow, margin + tableW, y + hRow);
                            for (int c = 1; c < gcw.Length; c++) gfx.DrawLine(pBorder, GColX(c), y, GColX(c), y + hRow);
                            OuterV(y, hRow, margin, tableW);
                            y += hRow; gRowIdx++;
                        }
                        else
                        {
                            bool firstItem = true;
                            foreach (var item in gitems)
                            {
                                if (y + hRow > Bottom()) { NewPage(); GrpColHeaders(); }
                                if (firstItem && !firstSet) gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);
                                if (gRowIdx % 2 == 1) gfx.DrawRectangle(bAlt, margin, y, tableW, hRow);
                                string startDt = item.LineStartDate?.ToString("MM/dd/yyyy") ?? "";
                                string endDt   = item.LineEndDate?.ToString("MM/dd/yyyy") ?? "";
                                string[] vals = { firstItem ? (renewal.SetCode ?? "") : "", firstItem ? (renewal.SetType ?? "") : "", firstItem ? (renewal.DocumentNumber ?? "") : "", firstItem ? (renewal.CompanyName ?? "") : "", firstItem ? (renewal.SiteDisplay ?? "") : "", firstItem ? (renewal.SetLevelStatus ?? "") : "", GetRenewalItemName(item), GetRenewalModel(item), GetRenewalSerial(item), item.Quantity.ToString(), startDt, endDt, item.DisplayStatus ?? "", item.Amount.ToString("N2") };
                                bool[] ra = { false, false, false, false, false, false, false, false, false, true, false, false, false, true };
                                for (int c = 0; c < vals.Length; c++)
                                {
                                    var rect = new XRect(GColX(c) + padX, y + padY, gcw[c] - padX * 2, hRow - padY * 2);
                                    var font = (c == 0 && firstItem) ? fCellBold : ((c == 3 && firstItem) ? fCellBold : (c == 6 ? fCellBold : fCell));
                                    var brush = ((c == 0 || c == 3) && firstItem) ? bNavy : bBlack;
                                    gfx.DrawString(TrimText(vals[c], font, rect.Width), font, brush, rect, ra[c] ? XStringFormats.TopRight : XStringFormats.TopLeft);
                                }
                                gfx.DrawLine(pBorder, margin, y + hRow, margin + tableW, y + hRow);
                                for (int c = 1; c < gcw.Length; c++) gfx.DrawLine(pBorder, GColX(c), y, GColX(c), y + hRow);
                                OuterV(y, hRow, margin, tableW);
                                y += hRow; gRowIdx++; firstItem = false;
                            }
                        }
                        firstSet = false;
                    }
                    gfx.DrawLine(pStrong, margin, y, margin + tableW, y);
                }

                // Grouped footer
                if (y + hFooter > Bottom()) NewPage();
                gfx.DrawRectangle(bFooterBg, margin, y, tableW, hFooter);
                gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);
                double gfx2 = margin + 10;
                gfx.DrawString("Total: ", fFooterB, bBlack, new XRect(gfx2, y, 40, hFooter), XStringFormats.CenterLeft);
                gfx2 += gfx.MeasureString("Total: ", fFooterB).Width;
                gfx.DrawString($"{_renewalGroups.Count} group(s)   \u00B7   ", fFooter, bGray, new XRect(gfx2, y, 120, hFooter), XStringFormats.CenterLeft);
                gfx2 += gfx.MeasureString($"{_renewalGroups.Count} group(s)   \u00B7   ", fFooter).Width;
                gfx.DrawString("Total Amount: ", fFooterB, bBlack, new XRect(gfx2, y, 80, hFooter), XStringFormats.CenterLeft);
                gfx2 += gfx.MeasureString("Total Amount: ", fFooterB).Width;
                gfx.DrawString($"{_renewalGroups.Sum(g => g.Chain?.Sum(r2 => r2.TotalAmountDue) ?? 0m):N2}", fFooter, bGray, new XRect(gfx2, y, 100, hFooter), XStringFormats.CenterLeft);
            }

            // ═══════════════════════════════════════════════════════════════
            //  CARTRIDGE SECTION PDF
            // ═══════════════════════════════════════════════════════════════
            if (_outboundBatches != null && _outboundBatches.Count > 0)
            {
                double[] ccw = { 80, 70, 110, 50, 140, 120, 110, 90 };
                { double s = ccw.Sum(); for (int i = 0; i < ccw.Length; i++) ccw[i] = ccw[i] / s * tableW; }
                double CColX(int c) { double x = margin; for (int i = 0; i < c; i++) x += ccw[i]; return x; }

                int totalCartridges = _cartridgeAuditTrails?.Values.Sum(t => t.Count) ?? 0;
                int totalQty = _outboundBatches.Sum(b => b.TotalQty);

                DrawSectionTitle("Cartridge Disposed/Sold Export Report",
                    $"Generated: {DateTime.Now:MMMM dd, yyyy  h:mm tt}   |   {_outboundBatches.Count} Batch(es)   |   {totalCartridges} Cartridge(s)   |   {totalQty} Total Qty");

                void CartColHeaders()
                {
                    if (y + hHdr > Bottom()) NewPage();
                    string[] names = { "CARTRIDGE ID", "REQUEST ID", "RETURNED DATE", "QTY", "CARTRIDGE MODEL", "VENDOR", "RETURNED BY", "REMARKS" };
                    gfx.DrawRectangle(bSlate, margin, y, tableW, hHdr);
                    for (int c = 0; c < names.Length; c++)
                    {
                        var r = new XRect(CColX(c) + padX, y + padY, ccw[c] - padX * 2, hHdr - padY * 2);
                        gfx.DrawString(names[c], fHdr, XBrushes.White, r, c == 3 ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
                        if (c < names.Length - 1) gfx.DrawLine(new XPen(XColor.FromArgb(74, 98, 116), 0.3), CColX(c + 1), y, CColX(c + 1), y + hHdr);
                    }
                    OuterV(y, hHdr, margin, tableW);
                    gfx.DrawLine(pStrong, margin, y + hHdr, margin + tableW, y + hHdr);
                    y += hHdr;
                }

                int cRowIdx = 0;
                foreach (var batch in _outboundBatches)
                {
                    var trail = (_cartridgeAuditTrails != null && _cartridgeAuditTrails.ContainsKey(batch.BatchId))
                        ? _cartridgeAuditTrails[batch.BatchId] : new List<VendorBatchAuditTrailDto>();

                    // Batch header
                    if (y + hBatch + hHdr + hRow > Bottom()) NewPage();
                    bool isDispose = batch.BatchPurpose == "DISPOSE";
                    gfx.DrawLine(pMonthTop, margin, y, margin + tableW, y);
                    gfx.DrawRectangle(bBatchBg, margin, y, tableW, hBatch);
                    string batchTitle = $"Batch #{batch.BatchId} \u2014 {batch.BatchPurpose}";
                    gfx.DrawString(batchTitle, fMonth, isDispose ? bDispose : bSell,
                        new XRect(margin + 10, y, tableW * 0.35, hBatch * 0.55), XStringFormats.BottomLeft);
                    string cntText = $"({trail.Count} cartridge{(trail.Count != 1 ? "s" : "")})";
                    gfx.DrawString(cntText, fBatchSub, bGray,
                        new XRect(margin + 10 + gfx.MeasureString(batchTitle, fMonth).Width + 8, y, 100, hBatch * 0.55), XStringFormats.BottomLeft);
                    string metaText = $"Vendor: {batch.VendorName ?? "\u2014"}   \u00B7   Models: {batch.ModelSummary ?? "\u2014"}   \u00B7   Total Qty: {batch.TotalQty}   \u00B7   Created: {batch.CreatedDate:MM/dd/yyyy HH:mm}";
                    gfx.DrawString(metaText, fBatchSub, bGray,
                        new XRect(margin + 10, y + hBatch * 0.55, tableW - 20, hBatch * 0.45), XStringFormats.TopLeft);
                    gfx.DrawLine(pBorder, margin, y + hBatch, margin + tableW, y + hBatch);
                    OuterV(y, hBatch, margin, tableW);
                    y += hBatch;
                    CartColHeaders();

                    if (trail.Count == 0)
                    {
                        if (y + hRow > Bottom()) { NewPage(); CartColHeaders(); }
                        gfx.DrawRectangle(bAlt, margin, y, tableW, hRow);
                        gfx.DrawString("No cartridges assigned to this batch.", fCell, bGray,
                            new XRect(margin + padX, y + padY, tableW - padX * 2, hRow - padY * 2), XStringFormats.CenterLeft);
                        gfx.DrawLine(pBorder, margin, y + hRow, margin + tableW, y + hRow);
                        OuterV(y, hRow, margin, tableW);
                        y += hRow;
                    }
                    else
                    {
                        foreach (var item in trail)
                        {
                            if (y + hRow > Bottom()) { NewPage(); CartColHeaders(); }
                            if (cRowIdx % 2 == 1) gfx.DrawRectangle(bAlt, margin, y, tableW, hRow);
                            string[] vals = { item.EmptyCartridgeId.ToString(), item.RequestId.HasValue ? item.RequestId.Value.ToString() : "\u2014", item.RequestDate.ToString("MM/dd/yyyy HH:mm"), item.ReturnedQty.ToString(), item.CartridgeModel ?? "", item.Vendor ?? "", item.ReturnedByName ?? "", item.Remarks ?? "" };
                            bool[] rAlign = { false, false, false, true, false, false, false, false };
                            for (int c = 0; c < vals.Length; c++)
                            {
                                var rect = new XRect(CColX(c) + padX, y + padY, ccw[c] - padX * 2, hRow - padY * 2);
                                var font = (c == 0 || c == 4) ? fCellBold : fCell;
                                var brush = c == 0 ? bNavy : bBlack;
                                gfx.DrawString(TrimText(vals[c], font, rect.Width), font, brush, rect, rAlign[c] ? XStringFormats.TopRight : XStringFormats.TopLeft);
                            }
                            gfx.DrawLine(pBorder, margin, y + hRow, margin + tableW, y + hRow);
                            for (int c = 1; c < ccw.Length; c++) gfx.DrawLine(pBorder, CColX(c), y, CColX(c), y + hRow);
                            OuterV(y, hRow, margin, tableW);
                            y += hRow; cRowIdx++;
                        }
                    }
                    gfx.DrawLine(pStrong, margin, y, margin + tableW, y);
                }

                // Cartridge footer
                if (y + hFooter > Bottom()) NewPage();
                gfx.DrawRectangle(bFooterBg, margin, y, tableW, hFooter);
                gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);
                double cfx = margin + 10;
                gfx.DrawString("Total: ", fFooterB, bBlack, new XRect(cfx, y, 40, hFooter), XStringFormats.CenterLeft);
                cfx += gfx.MeasureString("Total: ", fFooterB).Width;
                gfx.DrawString($"{_outboundBatches.Count} batch(es)   \u00B7   ", fFooter, bGray, new XRect(cfx, y, 120, hFooter), XStringFormats.CenterLeft);
                cfx += gfx.MeasureString($"{_outboundBatches.Count} batch(es)   \u00B7   ", fFooter).Width;
                gfx.DrawString("Cartridges: ", fFooterB, bBlack, new XRect(cfx, y, 80, hFooter), XStringFormats.CenterLeft);
                cfx += gfx.MeasureString("Cartridges: ", fFooterB).Width;
                gfx.DrawString($"{totalCartridges}   \u00B7   ", fFooter, bGray, new XRect(cfx, y, 60, hFooter), XStringFormats.CenterLeft);
                cfx += gfx.MeasureString($"{totalCartridges}   \u00B7   ", fFooter).Width;
                gfx.DrawString("Total Qty: ", fFooterB, bBlack, new XRect(cfx, y, 70, hFooter), XStringFormats.CenterLeft);
                cfx += gfx.MeasureString("Total Qty: ", fFooterB).Width;
                gfx.DrawString($"{totalQty}", fFooter, bGray, new XRect(cfx, y, 60, hFooter), XStringFormats.CenterLeft);
            }

            doc.Save(outputPath);
        }

        // ════════════════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════════════════

        private static string Esc(string text) =>
            string.IsNullOrEmpty(text) ? "" : System.Net.WebUtility.HtmlEncode(text);

        private static string ColVal(DataRow row, string col) =>
            row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? row[col].ToString() : "";

        private static decimal SafeDecimal(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value) return 0m;
            try { return Convert.ToDecimal(row[col]); } catch { return 0m; }
        }

        private static DateTime? SafeDate(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value) return null;
            try { return Convert.ToDateTime(row[col]); } catch { return null; }
        }

        private static string StatusBadge(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return "";
            string cls;
            switch (val.ToLowerInvariant())
            {
                case "active": case "completed": case "yes": case "dispatched": case "fully renewed":
                    cls = "b-ok"; break;
                case "draft": case "pending": case "for approval": case "processing": case "submitted":
                case "expiring soon": case "warning": case "partially renewed": case "partial":
                    cls = "b-warn"; break;
                case "cancelled": case "rejected": case "expired": case "inactive":
                    cls = "b-bad"; break;
                default:
                    cls = "b-def"; break;
            }
            return $"<span class='badge {cls}'>{Esc(val)}</span>";
        }
    }
}
