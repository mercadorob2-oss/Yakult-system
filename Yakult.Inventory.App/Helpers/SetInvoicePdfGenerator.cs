using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Helpers
{
    public static class SetInvoicePdfGenerator
    {
        private const double Margin = 40;
        private const double PadX = 4;
        private const double PadY = 3;

        // Yakult teal brand colour
        private static readonly XColor BrandColor = XColor.FromArgb(0, 150, 136);
        private static readonly XColor LightGray = XColor.FromArgb(245, 247, 250);
        private static readonly XColor MidGray = XColor.FromArgb(220, 220, 220);
        private static readonly XColor ZebraGray = XColor.FromArgb(250, 251, 252);
        private static readonly XColor FinanceBack = XColor.FromArgb(232, 245, 243);

        public static void Generate(SetDto set, IReadOnlyList<SetDetailRequestDto> requests, string outputPath)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            var items = (requests ?? Array.Empty<SetDetailRequestDto>())
                .Where(r => r != null).ToList();

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var doc = new PdfDocument();
            doc.Info.Title = $"Set Invoice – {set.SetCode}";
            doc.Info.Author = "Yakult Inventory App";

            // --- fonts ---
            var fTitle    = new XFont("Segoe UI", 18, XFontStyle.Bold);
            var fHeading  = new XFont("Segoe UI", 10, XFontStyle.Bold);
            var fNormal   = new XFont("Segoe UI", 9,  XFontStyle.Regular);
            var fSmall    = new XFont("Segoe UI", 8,  XFontStyle.Regular);
            var fSmallB   = new XFont("Segoe UI", 8,  XFontStyle.Bold);
            var fColHdr   = new XFont("Segoe UI", 8,  XFontStyle.Bold);
            var fCell     = new XFont("Segoe UI", 8,  XFontStyle.Regular);
            var fFooter   = new XFont("Segoe UI", 7,  XFontStyle.Regular);

            // --- pens / brushes ---
            var penBrand    = new XPen(BrandColor, 1.0);
            var penThin     = new XPen(MidGray, 0.6);
            var brushBrand  = new XSolidBrush(BrandColor);
            var brushWhite  = XBrushes.White;
            var brushBlack  = XBrushes.Black;
            var brushGray   = new XSolidBrush(XColor.FromArgb(100, 100, 100));
            var brushLight  = new XSolidBrush(LightGray);
            var brushZebra  = new XSolidBrush(ZebraGray);
            var brushFinance = new XSolidBrush(FinanceBack);

            PdfPage page = null;
            XGraphics gfx = null;
            double pageW = 0, pageH = 0, y = 0;
            int pageNumber = 0;

            void AddPage()
            {
                page = doc.AddPage();
                page.Size = PdfSharp.PageSize.A4;
                page.Orientation = PdfSharp.PageOrientation.Portrait;
                pageW = page.Width;
                pageH = page.Height;
                gfx = XGraphics.FromPdfPage(page);
                y = Margin;
                pageNumber++;
            }

            void DrawFooter()
            {
                var footerY = pageH - 22;
                gfx.DrawLine(penThin, Margin, footerY, pageW - Margin, footerY);
                var generatedText = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}";
                var pageText = $"Page {pageNumber}";
                gfx.DrawString(generatedText, fFooter, brushGray,
                    new XRect(Margin, footerY + 4, (pageW - Margin * 2) / 2, 12), XStringFormats.TopLeft);
                gfx.DrawString(pageText, fFooter, brushGray,
                    new XRect(Margin, footerY + 4, pageW - Margin * 2, 12), XStringFormats.TopRight);
            }

            double ContentW() => pageW - Margin * 2;

            // ── PAGE 1 ─────────────────────────────────────────────────────────────
            AddPage();

            // Brand header bar
            gfx.DrawRectangle(brushBrand, Margin, y, ContentW(), 48);
            gfx.DrawString("SET INVOICE", fTitle, brushWhite,
                new XRect(Margin + 12, y + 8, ContentW() - 12, 32), XStringFormats.CenterLeft);
            var setCodeLabel = set.SetCode ?? $"Set #{set.SetId}";
            gfx.DrawString(setCodeLabel, fHeading, brushWhite,
                new XRect(Margin, y + 8, ContentW() - 12, 32), XStringFormats.CenterRight);
            y += 56;

            // ── Info boxes ──────────────────────────────────────────────────────────
            var boxW = (ContentW() - 8) / 2;

            // Left box – Set details
            DrawInfoBox(gfx, Margin, y, boxW, 100, "SET DETAILS", fSmallB, fSmall, brushLight, penThin, brushBlack,
                new[]
                {
                    ("Set Code",        set.SetCode ?? "—"),
                    ("Document #",      string.IsNullOrWhiteSpace(set.DocumentNumber) ? "—" : set.DocumentNumber),
                    ("Reference #",     string.IsNullOrWhiteSpace(set.ReferenceNumber) ? "—" : set.ReferenceNumber),
                    ("Status",          set.SetStatus ?? "—"),
                    ("Dispatch Date",   set.DispatchDate.HasValue ? set.DispatchDate.Value.ToString("yyyy-MM-dd") : "Not dispatched"),
                    ("Set Type",        string.IsNullOrWhiteSpace(set.SetType) ? "—" : set.SetType),
                });

            // Right box – Recipient details
            DrawInfoBox(gfx, Margin + boxW + 8, y, boxW, 100, "RECIPIENT", fSmallB, fSmall, brushLight, penThin, brushBlack,
                new[]
                {
                    ("Employee",    string.IsNullOrWhiteSpace(set.CurrentEmployeeName) ? "—" : set.CurrentEmployeeName),
                    ("Emp #",       string.IsNullOrWhiteSpace(set.CurrentEmployeeNumber) ? "—" : set.CurrentEmployeeNumber),
                    ("Department",  string.IsNullOrWhiteSpace(set.CurrentDepartmentName) ? "—" : set.CurrentDepartmentName),
                    ("Branch",      string.IsNullOrWhiteSpace(set.CurrentBranchName) ? "—" : set.CurrentBranchName),
                    ("Company",     string.IsNullOrWhiteSpace(set.CurrentCompanyName) ? (string.IsNullOrWhiteSpace(set.Company) ? "—" : set.Company) : set.CurrentCompanyName),
                    ("Created By",  string.IsNullOrWhiteSpace(set.CreatedByName) ? "—" : set.CreatedByName),
                });

            y += 108;

            // Remarks
            if (!string.IsNullOrWhiteSpace(set.Remarks))
            {
                gfx.DrawString("Remarks:", fSmallB, brushBlack,
                    new XRect(Margin, y, 60, 14), XStringFormats.TopLeft);
                gfx.DrawString(set.Remarks, fSmall, brushBlack,
                    new XRect(Margin + 62, y, ContentW() - 62, 14), XStringFormats.TopLeft);
                y += 18;
            }

            y += 6;

            // ── Items table ─────────────────────────────────────────────────────────
            var columns = new[]
            {
                new Col("#",          20, rightAlign: true),
                new Col("Item Name",  160, wrap: true, maxLines: 2),
                new Col("Model",      80),
                new Col("Serial #",   80),
                new Col("Category",   65),
                new Col("Qty",        30, rightAlign: true),
                new Col("Status",     65),
            };

            FitColumns(columns, ContentW());

            double bottomLimit = pageH - Margin - 28;
            const double colHdrH = 18;
            const double minRowH = 16;

            void DrawColumnHeaders()
            {
                gfx.DrawRectangle(brushBrand, Margin, y, ContentW(), colHdrH);
                var cx = Margin;
                foreach (var col in columns)
                {
                    gfx.DrawString(col.Title, fColHdr, brushWhite,
                        new XRect(cx + PadX, y + 3, col.Width - PadX * 2, colHdrH - 6),
                        col.RightAlign ? XStringFormats.TopRight : XStringFormats.TopLeft);
                    cx += col.Width;
                }
                y += colHdrH;
            }

            // Section heading
            gfx.DrawString("ITEMS", fHeading, brushBlack,
                new XRect(Margin, y, ContentW(), 16), XStringFormats.TopLeft);
            y += 18;

            DrawColumnHeaders();

            bool zebra = false;
            int rowNum = 0;

            foreach (var req in items)
            {
                var lineH = gfx.MeasureString("Ag", fCell).Height;
                var nameLines = WrapLines(gfx, req.ItemName ?? string.Empty, fCell, columns[1].Width - PadX * 2, columns[1].MaxLines);
                var rowH = Math.Max(minRowH, PadY * 2 + nameLines.Count * lineH);

                if (y + rowH > bottomLimit)
                {
                    DrawFooter();
                    AddPage();
                    bottomLimit = pageH - Margin - 28;
                    DrawColumnHeaders();
                    zebra = false;
                }

                if (zebra) gfx.DrawRectangle(brushZebra, Margin, y, ContentW(), rowH);
                gfx.DrawRectangle(penThin, Margin, y, ContentW(), rowH);
                zebra = !zebra;
                rowNum++;

                var cx = Margin;
                void Cell(string text, int ci)
                {
                    var col = columns[ci];
                    text = (text ?? string.Empty).Trim();
                    var rect = new XRect(cx + PadX, y + PadY, col.Width - PadX * 2, rowH - PadY * 2);

                    if (col.Wrap)
                    {
                        var wLines = WrapLines(gfx, text, fCell, col.Width - PadX * 2, col.MaxLines);
                        var ty = y + PadY;
                        foreach (var ln in wLines)
                        {
                            gfx.DrawString(ln, fCell, brushBlack,
                                new XRect(cx + PadX, ty, col.Width - PadX * 2, lineH), XStringFormats.TopLeft);
                            ty += lineH;
                        }
                    }
                    else
                    {
                        text = TrimFit(gfx, text, fCell, col.Width - PadX * 2);
                        gfx.DrawString(text, fCell, brushBlack, rect,
                            col.RightAlign ? XStringFormats.TopRight : XStringFormats.TopLeft);
                    }

                    cx += col.Width;
                    gfx.DrawLine(penThin, cx, y, cx, y + rowH);
                }

                gfx.DrawLine(penThin, Margin, y, Margin, y + rowH);
                Cell(rowNum.ToString(), 0);
                Cell(req.ItemName, 1);
                Cell(req.ModelNumber, 2);
                Cell(req.SerialNumber, 3);
                Cell(req.Category, 4);
                Cell(req.Quantity.ToString(), 5);
                Cell(req.Status, 6);

                y += rowH;
            }

            if (items.Count == 0)
            {
                gfx.DrawRectangle(brushLight, Margin, y, ContentW(), 20);
                gfx.DrawString("No items in this set.", fNormal, brushGray,
                    new XRect(Margin + PadX, y + 3, ContentW(), 14), XStringFormats.TopLeft);
                y += 20;
            }

            y += 14;

            // ── Financials ──────────────────────────────────────────────────────────
            bool hasFinancials = set.Subtotal != 0 || set.VatAmount != 0
                || set.DiscountAmount != 0 || set.WhtAmount != 0 || set.TotalAmountDue != 0;

            if (hasFinancials)
            {
                const double finW = 220;
                var finX = Margin + ContentW() - finW;

                if (y + 90 > bottomLimit)
                {
                    DrawFooter();
                    AddPage();
                    bottomLimit = pageH - Margin - 28;
                }

                void FinRow(string label, decimal amount, bool bold = false, bool isTotal = false)
                {
                    const double rowH2 = 16;
                    if (isTotal)
                        gfx.DrawRectangle(brushBrand, finX, y, finW, rowH2);
                    else
                        gfx.DrawRectangle(brushFinance, finX, y, finW, rowH2);

                    gfx.DrawRectangle(penThin, finX, y, finW, rowH2);
                    var labelFont = bold ? fSmallB : fSmall;
                    var valBrush = isTotal ? brushWhite : brushBlack;
                    var lblBrush = isTotal ? brushWhite : brushBlack;
                    gfx.DrawString(label, labelFont, lblBrush,
                        new XRect(finX + PadX, y + 2, finW * 0.6, rowH2 - 4), XStringFormats.TopLeft);
                    gfx.DrawString(amount.ToString("N2"), labelFont, valBrush,
                        new XRect(finX, y + 2, finW - PadX, rowH2 - 4), XStringFormats.TopRight);
                    y += rowH2;
                }

                // Section label
                gfx.DrawString("FINANCIALS", fHeading, brushBlack,
                    new XRect(Margin, y, ContentW(), 16), XStringFormats.TopLeft);
                y += 18;

                FinRow("Subtotal",     set.Subtotal);
                if (set.VatAmount != 0)      FinRow("VAT",          set.VatAmount);
                if (set.DiscountAmount != 0) FinRow("Discount",     -set.DiscountAmount);
                if (set.WhtAmount != 0)      FinRow("WHT",          -set.WhtAmount);
                FinRow("TOTAL DUE",    set.TotalAmountDue, bold: true, isTotal: true);

                y += 10;
            }

            // ── Summary line ────────────────────────────────────────────────────────
            gfx.DrawRectangle(brushLight, Margin, y, ContentW(), 18);
            gfx.DrawRectangle(penThin, Margin, y, ContentW(), 18);
            gfx.DrawString(
                $"Total items: {items.Count}    Set created: {set.CreatedAt:yyyy-MM-dd}    Created by: {set.CreatedByName ?? "—"}",
                fSmall, brushGray,
                new XRect(Margin + PadX, y + 3, ContentW() - PadX * 2, 14), XStringFormats.TopLeft);
            y += 18;

            DrawFooter();
            doc.Save(outputPath);
        }

        public static void GenerateList(
            IReadOnlyList<SetDto> sets,
            string outputPath,
            IReadOnlyDictionary<int, List<SetItemPdfRow>> itemsBySetId = null)
        {
            if (sets == null) throw new ArgumentNullException(nameof(sets));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var doc = new PdfDocument();
            doc.Info.Title = "Set Report";
            doc.Info.Author = "Yakult Inventory App";

            // ── Fonts ────────────────────────────────────────────────────────────
            var fOrg     = new XFont("Segoe UI", 13, XFontStyle.Bold);
            var fTitle   = new XFont("Segoe UI", 10, XFontStyle.Regular);
            var fMeta    = new XFont("Segoe UI", 8,  XFontStyle.Regular);
            var fSetHdr  = new XFont("Segoe UI", 8,  XFontStyle.Bold);    // set header row
            var fItemHdr = new XFont("Segoe UI", 7.5, XFontStyle.Bold);   // item column headers
            var fItem    = new XFont("Segoe UI", 7.5, XFontStyle.Regular);// item data
            var fFooter  = new XFont("Segoe UI", 7,  XFontStyle.Regular);

            // ── Pens / Brushes ───────────────────────────────────────────────────
            var penBorder = new XPen(XColors.Black, 0.5);
            var penHeavy  = new XPen(XColors.Black, 0.8);
            var penThin   = new XPen(XColor.FromArgb(160, 160, 160), 0.4);

            // Row fills — all monochrome
            var brushSetHdr   = new XSolidBrush(XColor.FromArgb(50,  50,  50));  // dark  — set header
            var brushColHdr   = new XSolidBrush(XColor.FromArgb(200, 200, 200)); // mid   — items col header
            var brushZebra    = new XSolidBrush(XColor.FromArgb(245, 245, 245)); // light — alternate items row
            var brushGrayText = new XSolidBrush(XColor.FromArgb(90,  90,  90));

            PdfPage   page = null;
            XGraphics gfx  = null;
            double pageW = 0, pageH = 0, y = 0;
            int pageNumber = 0;
            var generatedAt = DateTime.Now;

            void AddPage()
            {
                page = doc.AddPage();
                page.Size = PdfSharp.PageSize.A4;
                page.Orientation = PdfSharp.PageOrientation.Landscape;
                pageW = page.Width;
                pageH = page.Height;
                gfx   = XGraphics.FromPdfPage(page);
                y     = Margin;
                pageNumber++;
            }

            double CW() => pageW - Margin * 2;

            // ── Document header ──────────────────────────────────────────────────
            void DrawPageHeader()
            {
                gfx.DrawString("Yakult Inventory System", fOrg, XBrushes.Black,
                    new XRect(Margin, y, CW(), 18), XStringFormats.TopLeft);
                y += 19;

                gfx.DrawString("Set Report", fTitle, XBrushes.Black,
                    new XRect(Margin, y, CW(), 13), XStringFormats.TopLeft);
                gfx.DrawString($"Generated: {generatedAt:yyyy-MM-dd HH:mm}", fMeta, brushGrayText,
                    new XRect(Margin, y, CW(), 13), XStringFormats.TopRight);
                y += 14;

                gfx.DrawLine(penHeavy, Margin, y, Margin + CW(), y);
                y += 5;

                gfx.DrawString($"Total sets: {sets.Count}", fMeta, brushGrayText,
                    new XRect(Margin, y, CW(), 12), XStringFormats.TopLeft);
                y += 14;
            }

            void DrawFooter()
            {
                var fy = pageH - 20;
                gfx.DrawLine(penThin, Margin, fy, Margin + CW(), fy);
                gfx.DrawString("Yakult Inventory System — Set Report", fFooter, brushGrayText,
                    new XRect(Margin, fy + 3, CW() * 0.6, 10), XStringFormats.TopLeft);
                gfx.DrawString($"Page {pageNumber}", fFooter, brushGrayText,
                    new XRect(Margin, fy + 3, CW(), 10), XStringFormats.TopRight);
            }

            // ── Generic bordered-cell helper ─────────────────────────────────────
            // Draws fill + border + text in a single cell rect.
            void DrawBorderedCell(
                string text, double cx, double ry, double w, double h,
                XFont font, XBrush textBrush, XBrush fillBrush,
                bool rightAlign = false)
            {
                gfx.DrawRectangle(fillBrush, cx, ry, w, h);
                gfx.DrawRectangle(penBorder, cx, ry, w, h);
                text = TrimFit(gfx, text ?? string.Empty, font, w - PadX * 2);
                gfx.DrawString(text, font, textBrush,
                    new XRect(cx + PadX, ry + PadY, w - PadX * 2, h - PadY * 2),
                    rightAlign ? XStringFormats.TopRight : XStringFormats.TopLeft);
            }

            // ── Column definitions ───────────────────────────────────────────────
            // Compute after AddPage so CW() is valid.
            AddPage();

            // Set-summary row: # | Set Code | Type | Status | Recipient | Branch | Dispatch Date | Items | Total Due
            double wNum = 22, wCode = 72, wType = 60, wStatus = 65, wDate = 78, wItems = 35, wTotal = 75;
            double varW    = CW() - (wNum + wCode + wType + wStatus + wDate + wItems + wTotal);
            double wRecip  = varW * 0.55;
            double wBranch = varW * 0.45;

            double[] sCols  = { wNum, wCode, wType, wStatus, wRecip, wBranch, wDate, wItems, wTotal };
            string[] sHdrs  = { "#", "Set Code", "Type", "Status", "Recipient", "Branch", "Dispatch Date", "Items", "Total Due" };
            bool[]   sRight = { true, false, false, false, false, false, false, true, true };

            // Items sub-table: Item Name | Model | Serial # | Category | Qty | Unit Price | Amount
            double wModel = 100, wSerial = 100, wCat = 90, wQty = 35, wUP = 82, wAmt = 82;
            double wName  = CW() - (wModel + wSerial + wCat + wQty + wUP + wAmt);

            double[] iCols  = { wName, wModel, wSerial, wCat, wQty, wUP, wAmt };
            string[] iHdrs  = { "Item Name", "Model", "Serial #", "Category", "Qty", "Unit Price", "Amount" };
            bool[]   iRight = { false, false, false, false, true, true, true };

            const double setRowH     = 20; // set summary row height
            const double itemHdrH    = 16; // items table column header height
            const double itemRowMinH = 14; // minimum items data row height

            // ── Set column-header row (repeated at top of every page) ────────────
            void DrawSetColumnHeaders()
            {
                var cx = Margin;
                for (int i = 0; i < sHdrs.Length; i++)
                {
                    DrawBorderedCell(sHdrs[i], cx, y, sCols[i], setRowH,
                        fSetHdr, XBrushes.White, brushSetHdr, sRight[i]);
                    cx += sCols[i];
                }
                y += setRowH;
            }

            // ── Items column-header row ──────────────────────────────────────────
            void DrawItemColumnHeaders()
            {
                var cx = Margin;
                for (int i = 0; i < iHdrs.Length; i++)
                {
                    DrawBorderedCell(iHdrs[i], cx, y, iCols[i], itemHdrH,
                        fItemHdr, XBrushes.Black, brushColHdr, iRight[i]);
                    cx += iCols[i];
                }
                y += itemHdrH;
            }

            // ── Start first page ─────────────────────────────────────────────────
            DrawPageHeader();
            DrawSetColumnHeaders();

            double bottomLimit = pageH - Margin - 24;
            int rowNum = 0;

            foreach (var s in sets)
            {
                rowNum++;

                // ── Page break check for set row ─────────────────────────────────
                if (y + setRowH > bottomLimit)
                {
                    DrawFooter();
                    AddPage();
                    bottomLimit = pageH - Margin - 24;
                    DrawPageHeader();
                    DrawSetColumnHeaders();
                }

                // ── Set summary row (dark header) ────────────────────────────────
                var dispDate = s.DispatchDate.HasValue
                    ? s.DispatchDate.Value.ToString("yyyy-MM-dd") : "—";
                var totalDue = s.TotalAmountDue != 0
                    ? s.TotalAmountDue.ToString("N2") : "—";

                string[] sVals =
                {
                    rowNum.ToString(),
                    s.SetCode ?? $"#{s.SetId}",
                    s.SetType ?? "—",
                    s.SetStatus ?? "—",
                    s.CurrentEmployeeName ?? "—",
                    s.CurrentBranchName   ?? "—",
                    dispDate,
                    s.ItemCount.ToString(),
                    totalDue
                };

                var scx = Margin;
                for (int i = 0; i < sVals.Length; i++)
                {
                    DrawBorderedCell(sVals[i], scx, y, sCols[i], setRowH,
                        fSetHdr, XBrushes.White, brushSetHdr, sRight[i]);
                    scx += sCols[i];
                }
                y += setRowH;

                // ── Items sub-table ──────────────────────────────────────────────
                List<SetItemPdfRow> setItems = null;
                itemsBySetId?.TryGetValue(s.SetId, out setItems);

                if (setItems != null && setItems.Count > 0)
                {
                    // Items column header
                    if (y + itemHdrH > bottomLimit)
                    {
                        DrawFooter(); AddPage(); bottomLimit = pageH - Margin - 24;
                        DrawPageHeader(); DrawSetColumnHeaders();
                    }
                    DrawItemColumnHeaders();

                    var subLineH = gfx.MeasureString("Ag", fItem).Height;
                    bool itemZebra = false;

                    foreach (var item in setItems)
                    {
                        var nameLines = WrapLines(gfx, item.ItemName ?? string.Empty,
                            fItem, iCols[0] - PadX * 2, 3);
                        var rowH = Math.Max(itemRowMinH, PadY * 2 + nameLines.Count * subLineH);

                        if (y + rowH > bottomLimit)
                        {
                            DrawFooter(); AddPage(); bottomLimit = pageH - Margin - 24;
                            DrawPageHeader(); DrawSetColumnHeaders();
                            DrawItemColumnHeaders();
                            itemZebra = false;
                        }

                        var rowFill = itemZebra ? brushZebra : XBrushes.White;
                        itemZebra = !itemZebra;

                        var ix = Margin;

                        // Item Name — needs wrap support, draw manually
                        gfx.DrawRectangle(rowFill, ix, y, iCols[0], rowH);
                        gfx.DrawRectangle(penBorder, ix, y, iCols[0], rowH);
                        var ty = y + PadY;
                        foreach (var ln in nameLines)
                        {
                            gfx.DrawString(ln, fItem, XBrushes.Black,
                                new XRect(ix + PadX, ty, iCols[0] - PadX * 2, subLineH),
                                XStringFormats.TopLeft);
                            ty += subLineH;
                        }
                        ix += iCols[0];

                        // Remaining item cells
                        string[] iVals =
                        {
                            item.ModelNumber ?? "—",
                            item.SerialNumber ?? "—",
                            item.Category     ?? "—",
                            item.Quantity.ToString(),
                            item.UnitPrice.HasValue ? item.UnitPrice.Value.ToString("N2") : "—",
                            item.Amount.HasValue    ? item.Amount.Value.ToString("N2")    : "—"
                        };

                        for (int i = 0; i < iVals.Length; i++)
                        {
                            DrawBorderedCell(iVals[i], ix, y, iCols[i + 1], rowH,
                                fItem, XBrushes.Black, rowFill, iRight[i + 1]);
                            ix += iCols[i + 1];
                        }

                        y += rowH;
                    }
                }
                else
                {
                    // No-items placeholder row (full width, single bordered cell)
                    if (y + itemRowMinH > bottomLimit)
                    {
                        DrawFooter(); AddPage(); bottomLimit = pageH - Margin - 24;
                        DrawPageHeader(); DrawSetColumnHeaders();
                    }
                    gfx.DrawRectangle(XBrushes.White, Margin, y, CW(), itemRowMinH);
                    gfx.DrawRectangle(penBorder, Margin, y, CW(), itemRowMinH);
                    gfx.DrawString("No items.", fItem, brushGrayText,
                        new XRect(Margin + PadX, y + PadY, CW() - PadX * 2, itemRowMinH - PadY * 2),
                        XStringFormats.TopLeft);
                    y += itemRowMinH;
                }

                y += 8; // gap between set blocks
            }

            if (sets.Count == 0)
            {
                gfx.DrawString("No sets to display.", fMeta, XBrushes.Black,
                    new XRect(Margin, y + 4, CW(), 14), XStringFormats.TopLeft);
            }

            DrawFooter();
            doc.Save(outputPath);
        }

        public static void TryOpen(string filePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch { }
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private static void DrawInfoBox(
            XGraphics gfx, double x, double y, double w, double h,
            string heading,
            XFont fontHeading, XFont fontNormal,
            XBrush backBrush, XPen borderPen, XBrush textBrush,
            (string Label, string Value)[] rows)
        {
            gfx.DrawRectangle(backBrush, x, y, w, h);
            gfx.DrawRectangle(borderPen, x, y, w, h);

            double iy = y + 5;
            gfx.DrawString(heading, fontHeading, textBrush,
                new XRect(x + PadX, iy, w - PadX * 2, 12), XStringFormats.TopLeft);
            iy += 14;

            foreach (var (label, value) in rows)
            {
                gfx.DrawString(label + ":", fontHeading, textBrush,
                    new XRect(x + PadX, iy, 72, 11), XStringFormats.TopLeft);
                var valText = TrimFit(gfx, value, fontNormal, w - 76 - PadX * 2);
                gfx.DrawString(valText, fontNormal, textBrush,
                    new XRect(x + 76, iy, w - 76 - PadX, 11), XStringFormats.TopLeft);
                iy += 12;
                if (iy > y + h - 6) break;
            }
        }

        private static void FitColumns(Col[] columns, double availableWidth)
        {
            var total = columns.Sum(c => c.Width);
            if (total <= availableWidth) return;

            var shrinkable = columns.Where(c => c.Wrap).ToArray();
            if (shrinkable.Length == 0) return;

            var excess = total - availableWidth;
            foreach (var col in shrinkable)
                col.Width = Math.Max(60, col.Width - excess / shrinkable.Length);
        }

        private static string TrimFit(XGraphics gfx, string text, XFont font, double maxW)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (maxW <= 0) return string.Empty;
            if (gfx.MeasureString(text, font).Width <= maxW) return text;

            const string ell = "…";
            var ew = gfx.MeasureString(ell, font).Width;
            if (ew > maxW) return string.Empty;

            var s = text;
            while (s.Length > 1)
            {
                s = s.Substring(0, s.Length - 1);
                if (gfx.MeasureString(s, font).Width + ew <= maxW)
                    return s + ell;
            }
            return ell;
        }

        private static List<string> WrapLines(XGraphics gfx, string text, XFont font, double maxW, int maxLines)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return lines;
            if (maxW <= 0) { lines.Add(string.Empty); return lines; }

            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var current = string.Empty;

            foreach (var w in words)
            {
                var candidate = string.IsNullOrEmpty(current) ? w : current + " " + w;
                if (gfx.MeasureString(candidate, font).Width <= maxW)
                {
                    current = candidate;
                    continue;
                }
                if (!string.IsNullOrEmpty(current))
                {
                    lines.Add(current);
                    current = w;
                    if (lines.Count >= maxLines) break;
                }
                else
                {
                    lines.Add(TrimFit(gfx, w, font, maxW));
                    current = string.Empty;
                    if (lines.Count >= maxLines) break;
                }
            }

            if (lines.Count < maxLines && !string.IsNullOrEmpty(current))
                lines.Add(current);

            return lines.Take(maxLines).ToList();
        }

        private sealed class Col
        {
            public Col(string title, double width, bool rightAlign = false, bool wrap = false, int maxLines = 1)
            {
                Title = title;
                Width = width;
                RightAlign = rightAlign;
                Wrap = wrap;
                MaxLines = Math.Max(1, maxLines);
            }

            public string Title { get; }
            public double Width { get; set; }
            public bool RightAlign { get; }
            public bool Wrap { get; }
            public int MaxLines { get; }
        }
    }
}
