using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Yakult.Inventory.App.WPF.EDocs.Transmittal.ViewModels;

namespace Yakult.Inventory.App.WPF.EDocs.Transmittal.Views
{
    // Renders ONE transmittal copy in point-space, reproducing the reference
    // workbook (transmittal.xlsx) cell for cell.
    //
    // Reference geometry, taken straight out of the workbook XML:
    //   * 14 columns A..N totalling 591.75pt, squeezed into the 576pt printable
    //     width (8.5in paper minus the 0.25in side margins) — the same job
    //     Excel's scale="99" print setting does.
    //   * rows 1..27 = one copy = 423.95pt tall. Two copies plus the 45pt
    //     spacer rows and 0.75in top/bottom margins add up to 14in, which is
    //     why the reference prints on LEGAL, not Letter.
    //   * the underline under a value is the bottom border of its row; the rule
    //     above the first item line is the top border of row 10.
    public class TransmittalCopyElement : FrameworkElement
    {
        // Legal width in PDF points; one copy = rows 1..27.
        public const double CopyWidth = 612.0;
        public const double CopyHeight = 423.95;

        private const double MarginX = 18.0;      // 0.25in side margin
        private const double PrintableWidth = CopyWidth - 2 * MarginX;

        // Column widths A..N in points (workbook column widths / 96 * 72).
        private static readonly double[] Cols =
        {
            69.00, 21.00, 50.25, 50.25, 40.50, 35.25, 21.00,
            23.25, 36.75, 18.00, 56.25, 69.75, 50.25, 50.25
        };

        // Row heights 1..27 in points.
        private static readonly double[] RowHeights =
        {
            20.25, 15.00, 15.00, 20.25, 18.00, 15.00, 15.00, 15.00, 15.75,
            15.00, 15.00, 15.00, 15.00, 15.00, 15.00, 15.00, 15.00, 15.00,
            17.45, 15.00, 17.25, 15.00, 15.00, 15.00, 15.00, 15.00, 15.00
        };

        // Column indices used below (0 = A).
        private const int ColA = 0;
        private const int ColB = 1;
        private const int ColC = 2;
        private const int ColE = 4;
        private const int ColG = 6;
        private const int ColK = 10;
        private const int ColL = 11;
        private const int ColM = 12;
        private const int ColN = 13;

        private static readonly double Squeeze = PrintableWidth / 591.75;

        // Times New Roman throughout, matching the paper requisition reference:
        // headers, labels and item lines are all the same serif family, with
        // Bold for headings, labels, item text and signatory names.
        private static readonly FontFamily FormFamily = new FontFamily("Arial");
        private static readonly FontFamily ItemFamily = new FontFamily("Arial");

        private static readonly Typeface FormBold =
            new Typeface(FormFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface FormRegular =
            new Typeface(FormFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private static readonly Typeface ItemBold =
            new Typeface(ItemFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        public TransmittalPrintViewModel ViewModel { get; set; }

        // Copy heading: "TRANSMITTAL" on the top half, "FILE" on the bottom.
        public string CopyTitle { get; set; } = "TRANSMITTAL";

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(CopyWidth, CopyHeight);
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, CopyWidth, CopyHeight));

            var vm = ViewModel;
            if (vm == null || vm.Document == null) return;
            var doc = vm.Document;
            var pen = new Pen(Brushes.Black, 0.7);

            double sheetCenter = (ColLeft(ColA) + ColRight(ColN)) / 2.0;
            double itemLeft = ColLeft(ColC);
            double itemRight = ColRight(ColM);

            // ── Rows 1-4 : company header + copy heading ──────────────────────
            TextCenter(dc, "YAKULT PHILIPPINES INC.", sheetCenter, RowBottom(1) - 4, FormBold, 16);
            TextCenter(dc, "1461 Agoncillo cor. Escoda St. Ermita Manila", sheetCenter, RowBottom(2) - 3, FormBold, 11);
            TextCenter(dc, CopyTitle, sheetCenter, RowBottom(4) - 4, FormBold, 16);

            // ── Rows 6-7 : TO / DATE / FROM ───────────────────────────────────
            Text(dc, "TO : ", ColLeft(ColA), RowBottom(6) - 3, FormRegular, 11);
            RuledValue(dc, doc.To, ColLeft(ColB), ColRight(ColG), RowBottom(6), pen);
            Text(dc, "DATE : ", ColLeft(ColL), RowBottom(6) - 3, FormRegular, 11);
            RuledValue(dc, doc.Date, ColLeft(ColM), ColRight(ColN), RowBottom(6), pen);
            Text(dc, "FROM : ", ColLeft(ColA), RowBottom(7) - 3, FormRegular, 11);
            RuledValue(dc, FromText(doc), ColLeft(ColB), ColRight(ColG), RowBottom(7), pen);

            // ── Row 9 : items banner. Its rule is row 10's top border, so it
            //    starts at column C and stops at column M, like the reference. ─
            TextCenter(dc, "RECEIVED THE FOLLOWING ITEMS:",
                (ColLeft(ColA) + ColRight(ColE)) / 2.0, RowBottom(9) - 3, FormBold, 12);
            dc.DrawLine(pen, new Point(itemLeft, RowBottom(9)), new Point(itemRight, RowBottom(9)));

            // ── Rows 10-21 : the 12 numbered, ruled item lines ────────────────
            var lines = vm.DisplayLines;
            for (int i = 0; i < TransmittalPrintViewModel.TemplateItemCount && i < lines.Count; i++)
            {
                double bottom = RowBottom(10 + i);
                Text(dc, lines[i].No.ToString(CultureInfo.InvariantCulture),
                    ColLeft(ColB), bottom - 3, FormRegular, 11);
                dc.DrawLine(pen, new Point(itemLeft, bottom), new Point(itemRight, bottom));
                Fitted(dc, lines[i].Text, itemLeft + 2, bottom - 2.5, itemRight - itemLeft - 4, ItemBold, 11);
            }

            // ── Rows 22-26 : copy marker + sign-offs ──────────────────────────
            // The marker reads TRANSMITTAL on every copy in the reference,
            // including the FILE half.
            TextCenter(dc, "TRANSMITTAL", (ColLeft(ColA) + ColRight(ColB)) / 2.0,
                RowBottom(22) - 3, FormBold, 10);

            SignLabel(dc, "PREPARED BY:", 23, 10);
            RuledValue(dc, doc.PreparedBy, ColLeft(ColC), ColRight(ColE), RowBottom(23), pen);

            SignLabel(dc, "TRANSMIT BY :", 24, 10);
            RuledValue(dc, doc.TransmitBy, ColLeft(ColC), ColRight(ColE), RowBottom(24), pen);
            Text(dc, "RECEIVED BY/DATE : ", ColLeft(ColK), RowBottom(24) - 3, FormBold, 11);
            RuledValue(dc, doc.ReceivedByDate, ColLeft(ColM), ColRight(ColN), RowBottom(24), pen);

            SignLabel(dc, "NOTED BY:", 26, 11);
            RuledValue(dc, doc.NotedBy, ColLeft(ColC), ColRight(ColE), RowBottom(26), pen);
            Text(dc, "APPROVED BY/DATE: ", ColLeft(ColK), RowBottom(26) - 3, FormBold, 11);
            RuledValue(dc, doc.ApprovedByDate, ColLeft(ColM), ColRight(ColN), RowBottom(26), pen);
        }

        // ── Geometry ──────────────────────────────────────────────────────────
        // FROM is permanent (IT department); a blank/legacy document still
        // prints the fixed text.
        private static string FromText(Models.TransmittalDocument doc)
        {
            var from = doc == null ? null : doc.From;
            return string.IsNullOrWhiteSpace(from) ? Models.TransmittalDocument.PermanentFrom : from;
        }

        private static double ColLeft(int col)
        {
            double x = 0;
            for (int i = 0; i < col && i < Cols.Length; i++) x += Cols[i];
            return MarginX + x * Squeeze;
        }

        private static double ColRight(int col)
        {
            return ColLeft(col) + Cols[col] * Squeeze;
        }

        private static double RowBottom(int row)
        {
            double y = 0;
            for (int i = 0; i < row && i < RowHeights.Length; i++) y += RowHeights[i];
            return y;
        }

        // ── Drawing helpers ───────────────────────────────────────────────────
        // Sign-off labels are centred over columns A..B in the reference.
        private void SignLabel(DrawingContext dc, string label, int row, double size)
        {
            TextCenter(dc, label, (ColLeft(ColA) + ColRight(ColB)) / 2.0, RowBottom(row) - 3, FormBold, size);
        }

        // A value centred over its underline, the rule sitting on the row's
        // bottom edge.
        private static void RuledValue(DrawingContext dc, string value, double x1, double x2, double ruleY, Pen pen)
        {
            dc.DrawLine(pen, new Point(x1, ruleY), new Point(x2, ruleY));
            if (string.IsNullOrWhiteSpace(value)) return;
            double avail = x2 - x1 - 4;
            string text = Ellipsize(value, avail, FormBold, 10);
            var ft = FT(text, FormBold, 10);
            dc.DrawText(ft, new Point((x1 + x2) / 2.0 - ft.Width / 2.0, ruleY - 2.5 - ft.Baseline));
        }

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
            dc.DrawText(ft, new Point(centerX - ft.Width / 2.0, baseline - ft.Baseline));
        }

        private static void Fitted(DrawingContext dc, string value, double x, double baseline, double avail, Typeface tf, double size)
        {
            if (string.IsNullOrWhiteSpace(value) || avail <= 0) return;
            Text(dc, Ellipsize(value, avail, tf, size), x, baseline, tf, size);
        }

        private static string Ellipsize(string value, double avail, Typeface tf, double size)
        {
            if (string.IsNullOrEmpty(value) || avail <= 0) return value ?? "";
            if (MeasureW(value, tf, size) <= avail) return value;
            const string ellipsis = "\u2026";
            int end = 0;
            while (end < value.Length && MeasureW(value.Substring(0, end + 1) + ellipsis, tf, size) <= avail)
                end++;
            return value.Substring(0, end) + ellipsis;
        }

        private static double MeasureW(string s, Typeface tf, double size)
        {
            return FT(s ?? "", tf, size).WidthIncludingTrailingWhitespace;
        }
    }
}
