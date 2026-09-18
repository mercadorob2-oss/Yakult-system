using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Yakult.Inventory.App.WPF.EDocs.Gatepass.Models;
using Yakult.Inventory.App.WPF.EDocs.Gatepass.ViewModels;

namespace Yakult.Inventory.App.WPF.EDocs.Gatepass.Views
{
    // Direct WPF port of the mobile reference GatepassPrintUtils.kt drawing code.
    // Renders ONE copy in point-space (612 x 504 = one half of a Legal page in
    // PDF points). The host scales this x4/3 to fill a WPF Legal page
    // (816 x 1344). Coordinates/sizes are kept identical to the Kotlin source so
    // the desktop output matches the mobile PDF as closely as font metrics allow.
    public class GatepassCopyElement : FrameworkElement
    {
        public const double CopyWidth = 612.0;
        public const double CopyHeight = 504.0;

        private const double MarginX = 58.0;
        private const double MarginY = 12.0;

        private static readonly FontFamily Family = new FontFamily("Arial");
        private static readonly Typeface Bold =
            new Typeface(Family, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface Regular =
            new Typeface(Family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        public GatepassPrintViewModel ViewModel { get; set; }
        public GatepassFormType FormType { get; set; } = GatepassFormType.Gatepass;
        public bool IsDataOnly { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(CopyWidth, CopyHeight);
        }

        protected override void OnRender(DrawingContext dc)
        {
            // Opaque white copy background.
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, CopyWidth, CopyHeight));
            var vm = ViewModel;
            if (vm == null || vm.Document == null) return;

            if (IsDataOnly)
                DrawDataOnly(dc, vm);
            else
                DrawForm(dc, vm);
        }

        // ── Form copy (port of drawGatepassForm) ──────────────────────────────
        private void DrawForm(DrawingContext dc, GatepassPrintViewModel vm)
        {
            var doc = vm.Document;
            var linePen = new Pen(Brushes.Black, 0.7);
            var boxPen = new Pen(Brushes.Black, 0.8);
            var checkPen = new Pen(Brushes.Black, 1.2);

            double left = MarginX;
            double right = CopyWidth - MarginX;
            double top = MarginY;
            double width = right - left;

            double companyX = left + width * 0.34;
            DrawCompanyOption(dc, companyX, top + 10f, doc.IsYakultPhilippines, "YAKULT PHILIPPINES INC.", Bold, 7.4);
            DrawCompanyOption(dc, companyX, top + 23f, doc.IsYakultMarketing, "YAKULT MARKETING CORP.", Bold, 7.4);

            DrawHeaderOption(dc, left + 70f, top + 48f, FormType == GatepassFormType.Gatepass, "GATEPASS", Bold, 8.0);
            DrawHeaderOption(dc, left + 215f, top + 48f, FormType == GatepassFormType.Transmittal, "TRANSMITTAL", Bold, 8.0);
            DrawHeaderOption(dc, left + 385f, top + 48f, FormType == GatepassFormType.File, "FILE", Bold, 8.0);

            double detailsTop = top + 70f;
            LabeledLine(dc, "TO:", doc.To, left + 10f, detailsTop + 13f, width * 0.55, Bold, 8.0, Bold, 8.6, linePen);
            LabeledLine(dc, "DATE:", doc.Date, left + width * 0.68, detailsTop + 13f, width * 0.26, Bold, 8.0, Regular, 8.6, linePen);
            LabeledLine(dc, "FROM:", FromText(doc), left + 10f, detailsTop + 32f, width * 0.55, Bold, 8.0, Bold, 8.6, linePen);

            double contentTop = top + 116f;
            double modelColumn = left + width * 0.37;
            double quantityColumn = left + width * 0.77;
            Text(dc, "ITEMS:", left, contentTop + 12f, Bold, 10.2);

            var categories = new[]
            {
                GatepassItemCategory.CartridgeRibbon,
                GatepassItemCategory.Monitor,
                GatepassItemCategory.Cpu,
                GatepassItemCategory.Mouse,
                GatepassItemCategory.Keyboard,
                GatepassItemCategory.Printer,
                GatepassItemCategory.BackupUps,
                GatepassItemCategory.Avr,
                GatepassItemCategory.ComputerTableFixedAsset
            };
            for (int index = 0; index < categories.Length; index++)
            {
                var category = categories[index];
                double rowY = contentTop + 24f + index * 20.5;
                bool selected = doc.ItemCategory == category;
                string categoryLabel = CategoryUpper(category);
                DrawCheckbox(dc, left + 28f, rowY, selected, categoryLabel, Bold, 8.1, boxPen, checkPen);
                if (category == GatepassItemCategory.ComputerTableFixedAsset)
                {
                    double lineStart = left + 6f + 15f + MeasureW(categoryLabel, Bold, 8.1) + 5f;
                    double lineEnd = quantityColumn - 115f;
                    dc.DrawLine(linePen, new Point(lineStart, rowY + 9f), new Point(lineEnd, rowY + 9f));
                }
            }

            // Printer/cartridge models — three chips spaced to stay inside the
            // model column (modelColumn .. quantityColumn).
            DrawCheckbox(dc, modelColumn, contentTop + 24f, doc.Model == GatepassModel.Lx300, "LX300", Regular, 7.4, boxPen, checkPen);
            DrawCheckbox(dc, modelColumn + 70f, contentTop + 24f, doc.Model == GatepassModel.Lx310, "LX310", Regular, 7.4, boxPen, checkPen);
            DrawCheckbox(dc, modelColumn + 140f, contentTop + 24f, doc.Model == GatepassModel.Lq2190, "LQ2190", Regular, 7.4, boxPen, checkPen);
            LabeledLine(dc, "FIXED ASSET NO.:", doc.FixedAssetNumber, modelColumn, contentTop + 67f, quantityColumn - modelColumn - 55f, Regular, 7.4, Regular, 8.6, linePen);

            double quantityCenter = quantityColumn + (right - quantityColumn) / 2f;
            TextCenter(dc, "QUANTITY", quantityCenter, contentTop + 12f, Bold, 10.2);
            double quantityLine = contentTop + 48f;
            dc.DrawLine(linePen, new Point(quantityColumn, quantityLine), new Point(right, quantityLine));
            Fitted(dc, doc.Quantity, quantityColumn + 2f, quantityLine - 2f, right - quantityColumn - 4f, Regular, 8.6);

            var lines = vm.DisplayLines;
            for (int index = 0; index < GatepassPrintViewModel.TemplateItemCount; index++)
            {
                double itemLine = quantityLine + 14f + index * 14f;
                dc.DrawLine(linePen, new Point(quantityColumn, itemLine), new Point(right, itemLine));
                string value = index < lines.Count ? lines[index] : "";
                Fitted(dc, value, quantityColumn + 2f, itemLine - 2f, right - quantityColumn - 4f, Regular, 7.4);
            }

            double notesTop = contentTop + 226f;
            LabeledLine(dc, "OTHERS:", doc.Others, left + 10f, notesTop + 12f, width - 20f, Bold, 8.0, Regular, 8.6, linePen);
            LabeledLine(dc, "REMARKS:", doc.Remarks, left + 10f, notesTop + 32f, width - 20f, Bold, 8.0, Regular, 8.6, linePen);

            double signatureTop = contentTop + 275f;
            double signatureWidth = width * 0.42;
            LabeledLine(dc, "ISSUED BY:", doc.IssuedBy, left + 10f, signatureTop + 15f, signatureWidth, Bold, 8.0, Regular, 8.6, linePen);
            LabeledLine(dc, "NOTED BY:", doc.NotedBy, left + 10f, signatureTop + 38f, signatureWidth, Bold, 8.0, Regular, 8.6, linePen);
            LabeledLine(dc, "RECEIVED BY / DATE:", doc.ReceivedByDate, left + width / 2f + 10f, signatureTop + 15f, signatureWidth, Bold, 8.0, Regular, 8.6, linePen);
            LabeledLine(dc, "APPROVED BY / DATE:", doc.ApprovedByDate, left + width / 2f + 10f, signatureTop + 38f, signatureWidth, Bold, 8.0, Regular, 8.6, linePen);
        }

        // ── Data-only summary copy (port of drawGatepassDataOnly) ─────────────
        private void DrawDataOnly(DrawingContext dc, GatepassPrintViewModel vm)
        {
            var doc = vm.Document;
            double left = MarginX;
            double right = CopyWidth - MarginX;
            double width = right - left;
            double top = MarginY;
            double center = (left + right) / 2f;
            double columnGap = 14f;
            double columnWidth = (width - columnGap) / 2f;
            double rightColumn = left + columnWidth + columnGap;

            TextCenter(dc, "ITEMS & DATA SUMMARY", center, top + 16f, Bold, 10.6);
            TextCenter(dc, "Entered information and listed items", center, top + 28f, Regular, 6.8);
            SummaryField(dc, "COMPANY:", CompanyLabel(doc), left, top + 43f, width, Bold, 7.2, Regular, 7.8);
            SummaryField(dc, "FORM TYPES:", "Gatepass / Transmittal / File", left, top + 56f, width, Bold, 7.2, Regular, 7.8);

            Text(dc, "DOCUMENT DATA", left, top + 73f, Bold, 8.0);
            SummaryField(dc, "TO:", doc.To, left, top + 87f, columnWidth, Bold, 7.2, Regular, 7.8);
            SummaryField(dc, "DATE:", doc.Date, rightColumn, top + 87f, columnWidth, Bold, 7.2, Regular, 7.8);
            SummaryField(dc, "FROM:", FromText(doc), left, top + 100f, width, Bold, 7.2, Regular, 7.8);

            string categoryText = CategoryDisplay(doc.ItemCategory);
            if (doc.ItemCategory == GatepassItemCategory.Others && !string.IsNullOrWhiteSpace(doc.ItemCategoryOther))
                categoryText = categoryText + " - " + doc.ItemCategoryOther;
            SummaryField(dc, "ITEM:", categoryText, left, top + 113f, columnWidth, Bold, 7.2, Regular, 7.8);
            SummaryField(dc, "MODEL:", ModelLabel(doc.Model), rightColumn, top + 113f, columnWidth, Bold, 7.2, Regular, 7.8);
            SummaryField(dc, "ASSET NO.:", doc.FixedAssetNumber, left, top + 126f, columnWidth, Bold, 7.2, Regular, 7.8);
            SummaryField(dc, "QTY:", doc.Quantity, rightColumn, top + 126f, columnWidth, Bold, 7.2, Regular, 7.8);

            var itemSummaryLines = DataSummaryItemLines(vm.DisplayLines);
            var displayedItems = itemSummaryLines.Count > 0
                ? itemSummaryLines
                : new List<string> { "No additional listed item lines" };
            Text(dc, "LISTED ITEMS", left, top + 145f, Bold, 8.0);
            int splitIndex = (displayedItems.Count + 1) / 2;
            int itemRows = Math.Max(1, splitIndex);
            for (int row = 0; row < itemRows; row++)
            {
                if (row < displayedItems.Count)
                    Fitted(dc, displayedItems[row], left, top + 158f + row * 12f, columnWidth, Regular, 7.3);
                int rightIdx = row + splitIndex;
                if (rightIdx < displayedItems.Count)
                    Fitted(dc, displayedItems[rightIdx], rightColumn, top + 158f + row * 12f, columnWidth, Regular, 7.3);
            }

            double nextBaseline = top + 158f + itemRows * 12f + 7f;
            bool hasAdditional = !string.IsNullOrWhiteSpace(doc.Others) || !string.IsNullOrWhiteSpace(doc.Remarks);
            if (hasAdditional)
            {
                Text(dc, "ADDITIONAL DATA", left, nextBaseline, Bold, 8.0);
                nextBaseline += 13f;
                SummaryField(dc, "OTHERS:", doc.Others, left, nextBaseline, width, Bold, 7.2, Regular, 7.8);
                nextBaseline += 13f;
                SummaryField(dc, "REMARKS:", doc.Remarks, left, nextBaseline, width, Bold, 7.2, Regular, 7.8);
                nextBaseline += 5f;
            }

            bool hasSignoff =
                !string.IsNullOrWhiteSpace(doc.IssuedBy) ||
                !string.IsNullOrWhiteSpace(doc.NotedBy) ||
                !string.IsNullOrWhiteSpace(doc.ReceivedByDate) ||
                !string.IsNullOrWhiteSpace(doc.ApprovedByDate);
            if (hasSignoff)
            {
                Text(dc, "SIGN-OFF DATA", left, nextBaseline, Bold, 8.0);
                nextBaseline += 13f;
                SummaryField(dc, "ISSUED BY:", doc.IssuedBy, left, nextBaseline, columnWidth, Bold, 7.2, Regular, 7.8);
                SummaryField(dc, "RECEIVED BY / DATE:", doc.ReceivedByDate, rightColumn, nextBaseline, columnWidth, Bold, 7.2, Regular, 7.8);
                nextBaseline += 13f;
                SummaryField(dc, "NOTED BY:", doc.NotedBy, left, nextBaseline, columnWidth, Bold, 7.2, Regular, 7.8);
                SummaryField(dc, "APPROVED BY / DATE:", doc.ApprovedByDate, rightColumn, nextBaseline, columnWidth, Bold, 7.2, Regular, 7.8);
            }
        }

        // ── Drawing helpers (baseline-aware, mirroring Android Canvas) ────────
        private static FormattedText FT(string text, Typeface tf, double size)
        {
#pragma warning disable CS0618
            return new FormattedText(
                text ?? "",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                tf,
                size,
                Brushes.Black);
#pragma warning restore CS0618
        }

        private static void Text(DrawingContext dc, string s, double x, double baseline, Typeface tf, double size)
        {
            if (string.IsNullOrEmpty(s)) return;
            var ft = FT(s, tf, size);
            dc.DrawText(ft, new Point(x, baseline - ft.Baseline));
        }

        private static void TextCenter(DrawingContext dc, string s, double centerX, double baseline, Typeface tf, double size)
        {
            if (string.IsNullOrEmpty(s)) return;
            var ft = FT(s, tf, size);
            dc.DrawText(ft, new Point(centerX - ft.Width / 2f, baseline - ft.Baseline));
        }

        private static double MeasureW(string s, Typeface tf, double size)
        {
            return FT(s ?? "", tf, size).WidthIncludingTrailingWhitespace;
        }

        private static void Fitted(DrawingContext dc, string value, double x, double baseline, double avail, Typeface tf, double size)
        {
            if (string.IsNullOrWhiteSpace(value) || avail <= 0f) return;
            const string ellipsis = "\u2026";
            string text = value;
            if (MeasureW(value, tf, size) > avail)
            {
                int end = 0;
                while (end < value.Length && MeasureW(value.Substring(0, end + 1) + ellipsis, tf, size) <= avail)
                    end++;
                text = value.Substring(0, end) + ellipsis;
            }
            Text(dc, text, x, baseline, tf, size);
        }

        private static void LabeledText(DrawingContext dc, string label, string value, double x, double baseline, double width, Typeface tfLabel, double sizeLabel, Typeface tfVal, double sizeVal)
        {
            Text(dc, label, x, baseline, tfLabel, sizeLabel);
            double valueX = x + MeasureW(label, tfLabel, sizeLabel) + 4f;
            Fitted(dc, value, valueX, baseline, width - (valueX - x), tfVal, sizeVal);
        }

        private static void LabeledLine(DrawingContext dc, string label, string value, double x, double baseline, double width, Typeface tfLabel, double sizeLabel, Typeface tfVal, double sizeVal, Pen linePen)
        {
            Text(dc, label, x, baseline, tfLabel, sizeLabel);
            double valueX = x + MeasureW(label, tfLabel, sizeLabel) + 4f;
            double lineRight = x + width;
            dc.DrawLine(linePen, new Point(valueX, baseline + 2f), new Point(lineRight, baseline + 2f));
            Fitted(dc, value, valueX + 2f, baseline - 1f, lineRight - valueX - 4f, tfVal, sizeVal);
        }

        private static void SummaryField(DrawingContext dc, string label, string value, double x, double baseline, double width, Typeface tfLabel, double sizeLabel, Typeface tfVal, double sizeVal)
        {
            if (string.IsNullOrWhiteSpace(value) || width <= 0f) return;
            Text(dc, label, x, baseline, tfLabel, sizeLabel);
            double valueX = x + MeasureW(label, tfLabel, sizeLabel) + 4f;
            Fitted(dc, value, valueX, baseline, width - (valueX - x), tfVal, sizeVal);
        }

        private static void DrawHeaderOption(DrawingContext dc, double x, double baseline, bool checked_, string text, Typeface tf, double size)
        {
            string mark = checked_ ? "(\u2713)" : "( )";
            Text(dc, text, x, baseline, tf, size);
            Text(dc, mark, x + MeasureW(text, tf, size) + 5f, baseline, tf, size);
        }

        private static void DrawCompanyOption(DrawingContext dc, double x, double baseline, bool checked_, string text, Typeface tf, double size)
        {
            string mark = checked_ ? "(\u2713)" : "( )";
            Text(dc, mark, x, baseline, tf, size);
            Text(dc, text, x + MeasureW(mark, tf, size) + 5f, baseline, tf, size);
        }

        private static void DrawCheckbox(DrawingContext dc, double x, double y, bool checked_, string text, Typeface tf, double size, Pen boxPen, Pen checkPen)
        {
            const double boxSize = 10f;
            dc.DrawRectangle(null, boxPen, new Rect(x, y, boxSize, boxSize));
            if (checked_)
            {
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    g.BeginFigure(new Point(x + 2f, y + 5f), false, false);
                    g.LineTo(new Point(x + 4.5f, y + 8f), true, false);
                    g.LineTo(new Point(x + 8.5f, y + 2f), true, false);
                }
                geo.Freeze();
                dc.DrawGeometry(null, checkPen, geo);
            }
            Text(dc, text, x + 15f, y + 9f, tf, size);
        }

        // ── Label mappings ────────────────────────────────────────────────────
        // FROM is permanent (IT department); a blank/legacy document still
        // prints the fixed text.
        private static string FromText(GatepassDocument doc)
        {
            var from = doc == null ? null : doc.From;
            return string.IsNullOrWhiteSpace(from) ? GatepassDocument.PermanentFrom : from;
        }

        private static string CategoryUpper(GatepassItemCategory category)
        {
            switch (category)
            {
                case GatepassItemCategory.CartridgeRibbon: return "CARTRIDGE RIBBON";
                case GatepassItemCategory.Monitor: return "MONITOR";
                case GatepassItemCategory.Cpu: return "CPU";
                case GatepassItemCategory.Mouse: return "MOUSE";
                case GatepassItemCategory.Keyboard: return "KEYBOARD";
                case GatepassItemCategory.Printer: return "PRINTER";
                case GatepassItemCategory.BackupUps: return "BACKUP UPS";
                case GatepassItemCategory.Avr: return "AVR";
                case GatepassItemCategory.ComputerTableFixedAsset: return "COMPUTER TABLE FIXED ASSET NO.";
                case GatepassItemCategory.Others: return "OTHERS";
                default: return "";
            }
        }

        private static string CategoryDisplay(GatepassItemCategory? category)
        {
            if (category == null) return "";
            switch (category.Value)
            {
                case GatepassItemCategory.CartridgeRibbon: return "Cartridge Ribbon";
                case GatepassItemCategory.Monitor: return "Monitor";
                case GatepassItemCategory.Cpu: return "CPU";
                case GatepassItemCategory.Mouse: return "Mouse";
                case GatepassItemCategory.Keyboard: return "Keyboard";
                case GatepassItemCategory.Printer: return "Printer";
                case GatepassItemCategory.BackupUps: return "Backup UPS";
                case GatepassItemCategory.Avr: return "AVR";
                case GatepassItemCategory.ComputerTableFixedAsset: return "Computer Table / Fixed Asset";
                case GatepassItemCategory.Others: return "Others";
                default: return "";
            }
        }

        private static string ModelLabel(GatepassModel model)
        {
            switch (model)
            {
                case GatepassModel.Lx300: return "LX300";
                case GatepassModel.Lx310: return "LX310";
                case GatepassModel.Lq2190: return "LQ2190";
                default: return "";
            }
        }

        private static string CompanyLabel(GatepassDocument doc)
        {
            if (doc.IsYakultPhilippines) return "YAKULT PHILIPPINES INC.";
            if (doc.IsYakultMarketing) return "YAKULT MARKETING CORP.";
            return "";
        }

        private static List<string> DataSummaryItemLines(IList<string> lines)
        {
            var result = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                var trimmed = (lines[i] ?? "").Trim();
                if (trimmed.Length > 0)
                    result.Add((i + 1) + ". " + trimmed);
            }
            return result;
        }
    }
}
