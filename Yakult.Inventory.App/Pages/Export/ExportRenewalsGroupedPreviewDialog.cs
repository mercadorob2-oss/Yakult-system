using System;
using System.Collections.Generic;
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
    internal class ExportRenewalsGroupedPreviewDialog : Form
    {
        // ── Palette ──────────────────────────────────────────────────────────
        private static readonly Color AccentNavy   = Color.FromArgb( 30,  64, 111);
        private static readonly Color CharcoalText = Color.FromArgb( 33,  37,  41);
        private static readonly Color LightBg      = Color.FromArgb(248, 249, 250);
        private static readonly Color BorderColor  = Color.FromArgb(206, 212, 218);

        private readonly List<RenewalGroupViewModel> _renewalGroups;
        private readonly RenewalRepository _repo;
        private Dictionary<int, List<SetItemRenewalDto>> _setItems;
        private Dictionary<int, ItemInfo> _itemDetails;
        private WebBrowser _browser;

        internal ExportRenewalsGroupedPreviewDialog(List<RenewalGroupViewModel> renewalGroups, RenewalRepository repo)
        {
            _renewalGroups = renewalGroups;
            _repo = repo;
            _setItems = new Dictionary<int, List<SetItemRenewalDto>>();
            _itemDetails = new Dictionary<int, ItemInfo>();
            InitializeComponentAsync();
        }

        private void InitializeComponentAsync()
        {
            Text = "Renewal Group Export Preview";
            Size = new Size(1280, 860);
            MinimumSize = new Size(960, 620);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = LightBg;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            BuildUi();

            // Fetch items for each renewal set in all groups
            var allItemIds = new HashSet<int>();
            foreach (var group in _renewalGroups)
            {
                foreach (var renewal in group.Chain ?? new List<RenewalDto>())
                {
                    try
                    {
                        var items = _repo.GetSetItemsForRenewal(renewal.SetId);
                        _setItems[renewal.SetId] = items ?? new List<SetItemRenewalDto>();
                        foreach (var item in _setItems[renewal.SetId])
                            allItemIds.Add(item.ItemId);
                    }
                    catch
                    {
                        _setItems[renewal.SetId] = new List<SetItemRenewalDto>();
                    }
                }
            }

            // Batch fetch Item details (Name, ModelNumber, SerialNumber)
            if (allItemIds.Count > 0)
                _itemDetails = FetchItemDetails(allItemIds);

            _browser.DocumentText = BuildPreviewHtml();
        }

        // ── Data fetching helpers ────────────────────────────────────────────
        private class ItemInfo
        {
            public int ItemId { get; set; }
            public string Name { get; set; }
            public string ModelNumber { get; set; }
            public string SerialNumber { get; set; }
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

        // ── UI ───────────────────────────────────────────────────────────────

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = AccentNavy };
            header.Controls.Add(new Label
            {
                Text      = $"Renewal Group Export Preview  —  {_renewalGroups.Count} Group(s) selected",
                Font      = new Font("Arial", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(16, 10)
            });
            header.Controls.Add(new Label
            {
                Text      = "Review the selected renewal groups below, then click Export to PDF.",
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
                Size      = new Size(100, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(30, 41, 59),
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
                btnCancel.Location = new Point(btnExport.Left - btnCancel.Width - 12, (btnBar.Height - btnCancel.Height) / 2);
            };
            btnBar.Controls.Add(btnExport);
            btnBar.Controls.Add(btnCancel);

            _browser = new WebBrowser { Dock = DockStyle.Fill };
            Controls.Add(_browser);
            Controls.Add(btnBar);
            Controls.Add(header);
        }

        // ════════════════════════════════════════════════════════════════════
        //  HTML PREVIEW
        // ════════════════════════════════════════════════════════════════════

        private string GetItemName(SetItemRenewalDto si)
        {
            if (_itemDetails.TryGetValue(si.ItemId, out var info) && !string.IsNullOrWhiteSpace(info.Name))
                return info.Name;
            return si.Description ?? "";
        }

        private string GetModel(SetItemRenewalDto si)
        {
            if (_itemDetails.TryGetValue(si.ItemId, out var info))
                return info.ModelNumber ?? "";
            return "";
        }

        private string GetSerial(SetItemRenewalDto si)
        {
            if (_itemDetails.TryGetValue(si.ItemId, out var info))
                return info.SerialNumber ?? "";
            return "";
        }

        private string BuildPreviewHtml()
        {
            var sb = new StringBuilder();

            sb.Append(@"<!DOCTYPE html><html><head><meta charset='UTF-8'><title>Renewal Group Export Preview</title>
<style>
*{box-sizing:border-box;margin:0;padding:0;}
body{background:#f0f2f5;font-family:Arial,Helvetica,sans-serif;font-size:12px;color:#212529;}
.page{background:#fff;margin:20px 24px 30px;border-radius:6px;box-shadow:0 2px 12px rgba(0,0,0,.12);overflow:hidden;}
.rpt-title{background:#1e406f;color:#fff;padding:18px 28px 14px;border-bottom:3px solid #2c6fad;}
.rpt-title h1{font-family:Arial,Helvetica,sans-serif;font-size:22px;font-weight:700;margin:0 0 4px;letter-spacing:.3px;}
.rpt-title p{font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#b0cce4;margin:0;}

/* ── Month section ──────────────────────────────────── */
.month-header{background:#e8ecf1;border-top:3px solid #1e406f;border-bottom:1px solid #dee2e6;padding:10px 24px;margin-top:6px;}
.month-header h2{font-family:Arial,Helvetica,sans-serif;font-size:15px;font-weight:700;color:#1e406f;margin:0;letter-spacing:.3px;}
.month-header .month-count{font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#6c757d;margin-left:10px;font-weight:400;}

/* ── Table styles ───────────────────────────────────── */
.rtbl{border-collapse:collapse;width:100%;background:#fff;}
.rtbl th{background:#34495e;color:#fff;font-family:Arial,Helvetica,sans-serif;font-weight:700;font-size:10px;text-align:left;padding:8px 10px;white-space:nowrap;letter-spacing:.3px;text-transform:uppercase;border-right:1px solid #4a6274;}
.rtbl th:last-child{border-right:none;}
.rtbl td{font-family:Arial,Helvetica,sans-serif;padding:6px 10px;border-bottom:1px solid #e9ecef;font-size:11px;vertical-align:top;border-right:1px solid #f0f0f0;}
.rtbl td:last-child{border-right:none;}
.rtbl tr:nth-child(even) td{background:#f8f9fa;}
.rtbl tr.set-border td{border-top:2px solid #dee2e6;}
td.set-code{font-weight:700;color:#1e406f;font-size:11px;}
td.company{font-weight:600;color:#1e406f;}
td.set-status{font-weight:600;}
td.item-name{font-weight:600;}
td.serial{font-family:Arial,Helvetica,sans-serif;font-size:10px;color:#495057;}
td.qty{text-align:center;font-weight:600;}
td.amount{text-align:right;font-weight:600;}
td.date{text-align:center;white-space:nowrap;}
td.empty-items{font-style:italic;color:#6c757d;padding:6px 10px;}
td.item-status{font-weight:500;}

/* ── Summary footer ─────────────────────────────────── */
.rpt-footer{background:#f8f9fa;border-top:2px solid #dee2e6;padding:12px 24px;font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#6c757d;}
.rpt-footer strong{color:#212529;}
</style></head><body><div class='page'>
<div class='rpt-title'>
  <h1>Renewal Group Export Report</h1>
  <p>Generated: ");
            sb.Append($"{DateTime.Now:MMMM dd, yyyy  h:mm tt}  &nbsp;|&nbsp;  {_renewalGroups.Count} Group(s)");
            sb.Append("</p></div>");

            // Sort groups, then bucket by month of RootEndDate
            var sortedGroups = _renewalGroups
                .OrderBy(g => g.RootEndDate ?? DateTime.MinValue)
                .ToList();

            var monthlyBuckets = sortedGroups
                .GroupBy(g =>
                {
                    var dt = g.RootEndDate ?? DateTime.MinValue;
                    return new DateTime(dt.Year, dt.Month, 1);
                })
                .OrderBy(mb => mb.Key);

            foreach (var monthBucket in monthlyBuckets)
            {
                // Flatten all renewals in this month across all groups
                var allRenewals = monthBucket.SelectMany(g => g.Chain ?? new List<RenewalDto>()).ToList();
                string monthLabel = monthBucket.Key == DateTime.MinValue ? "No Date" : monthBucket.Key.ToString("MMMM yyyy");
                int renewalCount = allRenewals.Count;

                sb.Append($"<div class='month-header'><h2>{Esc(monthLabel)}<span class='month-count'>({renewalCount} renewal{(renewalCount != 1 ? "s" : "")})</span></h2></div>");

                sb.Append("<table class='rtbl'>");
                sb.Append("<thead><tr>");
                sb.Append("<th>Set Code</th>");
                sb.Append("<th>Type</th>");
                sb.Append("<th>Document #</th>");
                sb.Append("<th>Company</th>");
                sb.Append("<th>Site</th>");
                sb.Append("<th>Set Status</th>");
                sb.Append("<th>Item Name</th>");
                sb.Append("<th>Model</th>");
                sb.Append("<th>Serial #</th>");
                sb.Append("<th>Qty</th>");
                sb.Append("<th>Start Date</th>");
                sb.Append("<th>Expiry Date</th>");
                sb.Append("<th>Item Status</th>");
                sb.Append("<th>Amount</th>");
                sb.Append("</tr></thead><tbody>");

                bool firstSet = true;
                foreach (var renewal in allRenewals)
                {
                    _setItems.TryGetValue(renewal.SetId, out var items);
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
                            string endDt = item.LineEndDate?.ToString("MM/dd/yyyy") ?? "";

                            sb.Append($"<td class='item-name'>{Esc(GetItemName(item))}</td>");
                            sb.Append($"<td>{Esc(GetModel(item))}</td>");
                            sb.Append($"<td class='serial'>{Esc(GetSerial(item))}</td>");
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

            // Report footer
            sb.Append("<div class='rpt-footer'>");
            sb.Append($"<strong>Total:</strong> {_renewalGroups.Count} group(s) &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Total Renewals:</strong> {_renewalGroups.Sum(g => g.Chain?.Count ?? 0)} &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Total Items:</strong> {_renewalGroups.Sum(g => g.Chain?.Sum(r => r.ItemCount) ?? 0)} &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Total Amount:</strong> {_renewalGroups.Sum(g => g.Chain?.Sum(r => r.TotalAmountDue) ?? 0m):N2}");
            sb.Append("</div>");

            sb.Append("</div></body></html>");
            return sb.ToString();
        }

        // ════════════════════════════════════════════════════════════════════
        //  PDF EXPORT
        // ════════════════════════════════════════════════════════════════════

        private void GenerateRenewalGroupPdf(string outputPath)
        {
            // ── Layout constants ─────────────────────────────────────────────
            const double margin   = 24;
            const double pageW    = 841.89;   // A4 landscape
            const double pageH    = 595.28;
            const double tableW   = pageW - margin * 2;
            const double padX     = 4;
            const double padY     = 2;
            const double hTitle   = 34;
            const double hGroup   = 22;
            const double hHdr     = 18;
            const double hRow     = 14;
            const double hFooter  = 20;

            // Column widths for the 14 columns
            // Set Code | Type | Doc # | Company | Site | Set Status | Item Name | Model | Serial # | Qty | Start | Expiry | Item Status | Amount
            double[] cw = { 58, 48, 58, 72, 72, 58, 100, 68, 68, 28, 52, 52, 52, 52 };
            { double s = cw.Sum(); for (int i = 0; i < cw.Length; i++) cw[i] = cw[i] / s * tableW; }

            // Fonts — all Arial
            var fTitle     = new XFont("Arial", 14, XFontStyle.Bold);
            var fSubtitle  = new XFont("Arial",  7.5, XFontStyle.Regular);
            var fGroup     = new XFont("Arial", 10, XFontStyle.Bold);
            var fGroupCnt  = new XFont("Arial",  7.5, XFontStyle.Regular);
            var fHdr       = new XFont("Arial",  6.5, XFontStyle.Bold);
            var fCell      = new XFont("Arial",  7, XFontStyle.Regular);
            var fCellBold  = new XFont("Arial",  7, XFontStyle.Bold);
            var fFooter    = new XFont("Arial",  8, XFontStyle.Regular);
            var fFooterB   = new XFont("Arial",  8, XFontStyle.Bold);

            // Brushes & pens
            var bNavy      = new XSolidBrush(XColor.FromArgb( 30,  64, 111));
            var bMidBlue   = new XSolidBrush(XColor.FromArgb( 44, 111, 173));
            var bSlate     = new XSolidBrush(XColor.FromArgb( 52,  73,  94));
            var bGroupBg   = new XSolidBrush(XColor.FromArgb(248, 249, 250));
            var bAlt       = new XSolidBrush(XColor.FromArgb(248, 249, 250));
            var bBlack     = new XSolidBrush(XColor.FromArgb( 33,  37,  41));
            var bGray      = new XSolidBrush(XColor.FromArgb(108, 117, 125));
            var bSubFg     = new XSolidBrush(XColor.FromArgb(176, 204, 228));
            var bFooterBg  = new XSolidBrush(XColor.FromArgb(248, 249, 250));
            var pBorder    = new XPen(XColor.FromArgb(206, 212, 218), 0.4);
            var pStrong    = new XPen(XColor.FromArgb(108, 117, 125), 0.6);
            var pSetBorder = new XPen(XColor.FromArgb(206, 212, 218), 1.0);
            var pGroupTop  = new XPen(XColor.FromArgb( 30,  64, 111), 1.5);

            // ── Document & page state ────────────────────────────────────────
            var doc = new PdfDocument { Info = { Title = "Renewal Group Export Report", Author = "Yakult.Inventory.App" } };
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

            double ColX(int c) { double x = margin; for (int i = 0; i < c; i++) x += cw[i]; return x; }

            void OuterV(double ry, double rh)
            {
                gfx.DrawLine(pStrong, margin,          ry, margin,          ry + rh);
                gfx.DrawLine(pStrong, margin + tableW, ry, margin + tableW, ry + rh);
            }

            void EmitColHeaders()
            {
                if (y + hHdr > Bottom()) NewPage();
                string[] names = { "SET CODE", "TYPE", "DOC #", "COMPANY", "SITE", "SET STATUS",
                                   "ITEM NAME", "MODEL", "SERIAL #", "QTY", "START DATE", "EXPIRY DATE",
                                   "ITEM STATUS", "AMOUNT" };
                bool[]   right = { false, false, false, false, false, false,
                                   false, false, false, true, false, false,
                                   false, true };
                gfx.DrawRectangle(bSlate, margin, y, tableW, hHdr);
                for (int c = 0; c < names.Length; c++)
                {
                    var r = new XRect(ColX(c) + padX, y + padY, cw[c] - padX * 2, hHdr - padY * 2);
                    gfx.DrawString(names[c], fHdr, XBrushes.White, r,
                        right[c] ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
                    if (c < names.Length - 1)
                        gfx.DrawLine(new XPen(XColor.FromArgb(74, 98, 116), 0.3), ColX(c + 1), y, ColX(c + 1), y + hHdr);
                }
                OuterV(y, hHdr);
                gfx.DrawLine(pStrong, margin, y + hHdr, margin + tableW, y + hHdr);
                y += hHdr;
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

            // ── First page — report title ────────────────────────────────────
            NewPage();
            gfx.DrawRectangle(bNavy,    margin, y, tableW, hTitle);
            gfx.DrawRectangle(bMidBlue, margin, y + hTitle, tableW, 3);
            gfx.DrawString("Renewal Group Export Report", fTitle, XBrushes.White,
                new XRect(margin + 10, y, tableW - 10, hTitle * 0.62), XStringFormats.BottomLeft);
            string sub = $"Generated: {DateTime.Now:MMMM dd, yyyy  h:mm tt}   |   {_renewalGroups.Count} Group(s)";
            gfx.DrawString(sub, fSubtitle, bSubFg,
                new XRect(margin + 10, y + hTitle * 0.62, tableW - 10, hTitle * 0.38), XStringFormats.TopLeft);
            y += hTitle + 3 + 6;

            // ── Sort & bucket by month ────────────────────────────────────────
            var sortedGroups = _renewalGroups
                .OrderBy(g => g.RootEndDate ?? DateTime.MinValue)
                .ToList();

            var monthlyBuckets = sortedGroups
                .GroupBy(g =>
                {
                    var dt = g.RootEndDate ?? DateTime.MinValue;
                    return new DateTime(dt.Year, dt.Month, 1);
                })
                .OrderBy(mb => mb.Key);

            int rowIdx = 0;
            const double hMonthHdr = 24;

            foreach (var monthBucket in monthlyBuckets)
            {
                // Flatten all renewals in this month across all groups
                var allRenewals = monthBucket.SelectMany(g => g.Chain ?? new List<RenewalDto>()).ToList();

                // ── Month header ──
                if (y + hMonthHdr + hHdr + hRow > Bottom()) NewPage();

                string monthLabel = monthBucket.Key == DateTime.MinValue ? "No Date" : monthBucket.Key.ToString("MMMM yyyy");
                int renewalCount = allRenewals.Count;

                gfx.DrawLine(pGroupTop, margin, y, margin + tableW, y);
                gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(232, 236, 241)), margin, y, tableW, hMonthHdr);
                gfx.DrawString(monthLabel, fGroup, bNavy,
                    new XRect(margin + 10, y, tableW * 0.5, hMonthHdr), XStringFormats.CenterLeft);
                gfx.DrawString($"({renewalCount} renewal{(renewalCount != 1 ? "s" : "")})", fGroupCnt, bGray,
                    new XRect(margin + 10 + gfx.MeasureString(monthLabel, fGroup).Width + 8, y, 100, hMonthHdr), XStringFormats.CenterLeft);
                gfx.DrawLine(pBorder, margin, y + hMonthHdr, margin + tableW, y + hMonthHdr);
                OuterV(y, hMonthHdr);
                y += hMonthHdr;

                EmitColHeaders();

                bool firstSet = true;
                foreach (var renewal in allRenewals)
                {
                    _setItems.TryGetValue(renewal.SetId, out var items);
                    bool hasItems = items != null && items.Count > 0;

                    if (!hasItems)
                    {
                        if (y + hRow > Bottom()) { NewPage(); EmitColHeaders(); }

                        if (!firstSet)
                            gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);

                        if (rowIdx % 2 == 1)
                            gfx.DrawRectangle(bAlt, margin, y, tableW, hRow);

                        string[] vals = {
                            renewal.SetCode ?? "",
                            renewal.SetType ?? "",
                            renewal.DocumentNumber ?? "",
                            renewal.CompanyName ?? "",
                            renewal.SiteDisplay ?? "",
                            renewal.SetLevelStatus ?? "",
                            "No items found",
                            "", "", "", "", "",
                            "\u2014",
                            renewal.TotalAmountDue.ToString("N2")
                        };
                        bool[] rAlign = { false, false, false, false, false, false,
                                          false, false, false, false, false, false,
                                          false, true };

                        for (int c = 0; c < vals.Length; c++)
                        {
                            var rect = new XRect(ColX(c) + padX, y + padY, cw[c] - padX * 2, hRow - padY * 2);
                            var fmt  = rAlign[c] ? XStringFormats.TopRight : XStringFormats.TopLeft;
                            var font = (c == 0) ? fCellBold : ((c == 3) ? fCellBold : fCell);
                            var brush = (c == 0 || c == 3) ? bNavy : ((c == 6) ? bGray : bBlack);
                            gfx.DrawString(TrimText(vals[c], font, rect.Width), font, brush, rect, fmt);
                        }

                        gfx.DrawLine(pBorder, margin, y + hRow, margin + tableW, y + hRow);
                        for (int c = 1; c < cw.Length; c++)
                            gfx.DrawLine(pBorder, ColX(c), y, ColX(c), y + hRow);
                        OuterV(y, hRow);

                        y += hRow;
                        rowIdx++;
                    }
                    else
                    {
                        bool firstItem = true;
                        foreach (var item in items)
                        {
                            if (y + hRow > Bottom()) { NewPage(); EmitColHeaders(); }

                            if (firstItem && !firstSet)
                                gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);

                            if (rowIdx % 2 == 1)
                                gfx.DrawRectangle(bAlt, margin, y, tableW, hRow);

                            string startDt = item.LineStartDate?.ToString("MM/dd/yyyy") ?? "";
                            string endDt = item.LineEndDate?.ToString("MM/dd/yyyy") ?? "";

                            string[] vals = {
                                firstItem ? (renewal.SetCode ?? "") : "",
                                firstItem ? (renewal.SetType ?? "") : "",
                                firstItem ? (renewal.DocumentNumber ?? "") : "",
                                firstItem ? (renewal.CompanyName ?? "") : "",
                                firstItem ? (renewal.SiteDisplay ?? "") : "",
                                firstItem ? (renewal.SetLevelStatus ?? "") : "",
                                GetItemName(item),
                                GetModel(item),
                                GetSerial(item),
                                item.Quantity.ToString(),
                                startDt,
                                endDt,
                                item.DisplayStatus ?? "",
                                item.Amount.ToString("N2")
                            };
                            bool[] rAlign = { false, false, false, false, false, false,
                                              false, false, false, true, false, false,
                                              false, true };

                            for (int c = 0; c < vals.Length; c++)
                            {
                                var rect = new XRect(ColX(c) + padX, y + padY, cw[c] - padX * 2, hRow - padY * 2);
                                var fmt  = rAlign[c] ? XStringFormats.TopRight : XStringFormats.TopLeft;
                                var font = (c == 0 && firstItem) ? fCellBold : ((c == 3 && firstItem) ? fCellBold : ((c == 6) ? fCellBold : fCell));
                                var brush = (c == 0 && firstItem || c == 3 && firstItem) ? bNavy : bBlack;
                                gfx.DrawString(TrimText(vals[c], font, rect.Width), font, brush, rect, fmt);
                            }

                            gfx.DrawLine(pBorder, margin, y + hRow, margin + tableW, y + hRow);
                            for (int c = 1; c < cw.Length; c++)
                                gfx.DrawLine(pBorder, ColX(c), y, ColX(c), y + hRow);
                            OuterV(y, hRow);

                            y += hRow;
                            rowIdx++;
                            firstItem = false;
                        }
                    }
                    firstSet = false;
                }

                gfx.DrawLine(pStrong, margin, y, margin + tableW, y);
            }

            // ── Report footer ────────────────────────────────────────────────
            if (y + hFooter > Bottom()) NewPage();
            gfx.DrawRectangle(bFooterBg, margin, y, tableW, hFooter);
            gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);

            double fx = margin + 10;
            gfx.DrawString("Total: ", fFooterB, bBlack,
                new XRect(fx, y, 40, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString("Total: ", fFooterB).Width;
            string totalText = $"{_renewalGroups.Count} group(s)   \u00B7   ";
            gfx.DrawString(totalText, fFooter, bGray,
                new XRect(fx, y, 120, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString(totalText, fFooter).Width;
            gfx.DrawString("Total Renewals: ", fFooterB, bBlack,
                new XRect(fx, y, 100, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString("Total Renewals: ", fFooterB).Width;
            string renewalsText = $"{_renewalGroups.Sum(g => g.Chain?.Count ?? 0)}   \u00B7   ";
            gfx.DrawString(renewalsText, fFooter, bGray,
                new XRect(fx, y, 80, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString(renewalsText, fFooter).Width;
            gfx.DrawString("Total Items: ", fFooterB, bBlack,
                new XRect(fx, y, 80, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString("Total Items: ", fFooterB).Width;
            string itemsText = $"{_renewalGroups.Sum(g => g.Chain?.Sum(r => r.ItemCount) ?? 0)}   \u00B7   ";
            gfx.DrawString(itemsText, fFooter, bGray,
                new XRect(fx, y, 80, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString(itemsText, fFooter).Width;
            gfx.DrawString("Total Amount: ", fFooterB, bBlack,
                new XRect(fx, y, 80, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString("Total Amount: ", fFooterB).Width;
            gfx.DrawString($"{_renewalGroups.Sum(g => g.Chain?.Sum(r => r.TotalAmountDue) ?? 0m):N2}", fFooter, bGray,
                new XRect(fx, y, 100, hFooter), XStringFormats.CenterLeft);

            doc.Save(outputPath);
        }

        // ── Event handlers ───────────────────────────────────────────────────

        private void OnExportToPdf(object sender, EventArgs e)
        {
            try
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*";
                    sfd.FileName = $"RenewalGroupExport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    sfd.Title = "Save Renewal Group Export";

                    if (sfd.ShowDialog() != DialogResult.OK) return;

                    GenerateRenewalGroupPdf(sfd.FileName);
                    MessageBox.Show("Renewal group export saved successfully!", "Export Complete",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting renewal groups:\n{ex.Message}", "Export Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string Esc(string s)
            => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
