using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Services
{
    // Temporary diagnostic tool — NOT part of the transmittal form and does not
    // reuse or modify any of its layout logic. Prints a ruler/grid page through
    // the exact same PrintDialog -> FixedDocument -> FixedPage -> DocumentPaginator
    // pipeline as TransmittalPrintService.Print(), so a physical ruler held
    // against the printed sheet tells us whether WPF/the driver places content at
    // the correct physical location — independent of anything transmittal-specific.
    // Safe to delete once the print-pipeline discrepancy is understood and fixed.
    public static class CalibrationPrintService
    {
        // Mirrors TransmittalPrintService.PageMargin (0.5in) only so the
        // "printable content area" box drawn here is comparable to where the
        // transmittal form's own content sits. Not shared code on purpose — this
        // page must not depend on (or be able to accidentally change) the real
        // form's layout.
        private const double ContentMargin = 48;

        public static void Print(TransmittalPrintService.TransmittalPageSize pageSize)
        {
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() != true) return;

            TransmittalPrintService.ApplyPageMediaSize(dlg, pageSize);

            var (pageWidth, pageHeight) = TransmittalPrintService.GetPageDimensions(pageSize);

            PrintDiagnosticsService.LogPageMetrics("CALIBRATION", dlg, pageSize, pageWidth, pageHeight,
                topY: ContentMargin, lineY: pageHeight / 2.0, bottomY: pageHeight - ContentMargin);

            var doc = new FixedDocument();
            doc.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);
            doc.Pages.Add(TransmittalPrintService.ToPageContent(
                BuildCalibrationVisual(dlg, pageWidth, pageHeight), pageWidth, pageHeight,
                TransmittalPrintService.PhysicalPrintVerticalCompensation));

            dlg.PrintDocument(doc.DocumentPaginator, "Calibration Page");

            MessageBox.Show(
                $"Calibration page sent to printer.\n\nDiagnostics logged to:\n{PrintDiagnosticsService.LogFilePath}",
                "Calibration Print", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static FrameworkElement BuildCalibrationVisual(PrintDialog dlg, double pageWidth, double pageHeight)
        {
            var page = new Canvas { Width = pageWidth, Height = pageHeight, Background = Brushes.White };

            // Outer rectangle = full physical paper size.
            AddRect(page, 0, 0, pageWidth, pageHeight, Brushes.Black, 1.5, dashed: false);

            // Explicit top/bottom edge lines (redundant with the outer rect, but
            // called out per spec so they're unmistakable at the very edge, where
            // a printer's hardware margin is most likely to clip a plain border).
            AddLine(page, 0, pageWidth, 0.5, Brushes.Black, 1, dashed: false);
            AddLine(page, 0, pageWidth, pageHeight - 0.5, Brushes.Black, 1, dashed: false);

            // Horizontal line through the vertical center point.
            double midY = pageHeight / 2.0;
            AddLine(page, 0, pageWidth, midY, Brushes.Red, 2, dashed: false);

            // Vertical line through the horizontal center point.
            AddVLine(page, pageWidth / 2.0, 0, pageHeight, Brushes.Red, 2);

            // 1-inch ruler marks + labels, top to bottom. The label's vertical
            // CENTER is placed exactly on its own gridline's y (with a small white
            // backing box so the text stays legible over the dashed line) — not
            // offset above it. An offset label reads as belonging to whichever line
            // it's visually closest to, which is not necessarily its own line, and
            // caused a previous false "the center line is off" reading that was
            // actually just this label sitting 14 units above the line it names.
            int inches = (int)Math.Ceiling(pageHeight / 96.0);
            for (int i = 0; i <= inches; i++)
            {
                double y = i * 96.0;
                if (y > pageHeight) break;
                AddLine(page, 0, pageWidth, y, Brushes.Gray, 1, dashed: true);
                AddCenteredLabelOnLine(page, 4, y, $"{i}\"", 11, Brushes.Black);
            }

            AddCenteredLabelOnLine(page, pageWidth / 2.0 - 95, midY, "===== PAGE CENTER =====", 13, Brushes.Red, bold: true);

            // "Printable content area" — this diagnostic page's own 0.5in design
            // margin (mirrors TransmittalPrintService.PageMargin for comparison),
            // NOT the printer's reported imageable area.
            AddRect(page, ContentMargin, ContentMargin, pageWidth - 2 * ContentMargin, pageHeight - 2 * ContentMargin,
                Brushes.Blue, 1, dashed: true);
            AddLabel(page, ContentMargin + 4, ContentMargin + 4, "Printable content area (0.5in design margin)", 10, Brushes.Blue);

            // Printer's reported imageable area, if available.
            try
            {
                var caps = dlg.PrintQueue?.GetPrintCapabilities(dlg.PrintTicket);
                var area = caps?.PageImageableArea;
                if (area != null)
                {
                    AddRect(page, area.OriginWidth, area.OriginHeight, area.ExtentWidth, area.ExtentHeight,
                        Brushes.Green, 1.5, dashed: false);
                    AddLabel(page, area.OriginWidth + 4, area.OriginHeight + 20,
                        $"Printer imageable area  origin=({area.OriginWidth:F1},{area.OriginHeight:F1})  extent={area.ExtentWidth:F1}x{area.ExtentHeight:F1}",
                        10, Brushes.Green);
                }
                else
                {
                    AddLabel(page, ContentMargin, pageHeight - ContentMargin - 20,
                        "Printer imageable area: not reported by driver", 10, Brushes.Green);
                }
            }
            catch (Exception ex)
            {
                AddLabel(page, ContentMargin, pageHeight - ContentMargin - 20,
                    $"Printer imageable area: lookup failed ({ex.Message})", 10, Brushes.Green);
            }

            page.Measure(new Size(pageWidth, pageHeight));
            page.Arrange(new Rect(0, 0, pageWidth, pageHeight));
            page.UpdateLayout();
            return page;
        }

        private static void AddLine(Canvas page, double x1, double x2, double y, Brush stroke, double thickness, bool dashed)
        {
            var line = new Line
            {
                X1 = x1, X2 = x2, Y1 = y, Y2 = y,
                Stroke = stroke, StrokeThickness = thickness
            };
            if (dashed) line.StrokeDashArray = new DoubleCollection { 4, 3 };
            page.Children.Add(line);
        }

        private static void AddVLine(Canvas page, double x, double y1, double y2, Brush stroke, double thickness)
        {
            page.Children.Add(new Line { X1 = x, X2 = x, Y1 = y1, Y2 = y2, Stroke = stroke, StrokeThickness = thickness });
        }

        private static void AddRect(Canvas page, double x, double y, double width, double height, Brush stroke, double thickness, bool dashed)
        {
            var rect = new Rectangle
            {
                Width = Math.Max(0, width), Height = Math.Max(0, height),
                Stroke = stroke, StrokeThickness = thickness
            };
            if (dashed) rect.StrokeDashArray = new DoubleCollection { 4, 3 };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            page.Children.Add(rect);
        }

        private static void AddLabel(Canvas page, double x, double y, string text, double fontSize, Brush brush, bool bold = false)
        {
            var tb = new TextBlock
            {
                Text = text, FontSize = fontSize, Foreground = brush,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
            };
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
            page.Children.Add(tb);
        }

        // Places a label so its vertical CENTER lands exactly on lineY — i.e. the
        // line the label names — with a white backing box so it stays legible
        // over the dashed gridline it sits on top of. This replaces the earlier
        // "label drawn 14 units above its line" convention, which read as
        // belonging to whichever line it was visually nearest, not necessarily its
        // own — that's what caused a previous false "the center line is off" read.
        private static void AddCenteredLabelOnLine(Canvas page, double x, double lineY, string text, double fontSize, Brush brush, bool bold = false)
        {
            var tb = new TextBlock
            {
                Text = text, FontSize = fontSize, Foreground = brush,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double halfHeight = tb.DesiredSize.Height / 2.0;

            var backing = new Rectangle
            {
                Width = tb.DesiredSize.Width + 4,
                Height = tb.DesiredSize.Height,
                Fill = Brushes.White
            };
            Canvas.SetLeft(backing, x - 2);
            Canvas.SetTop(backing, lineY - halfHeight);
            page.Children.Add(backing);

            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, lineY - halfHeight);
            page.Children.Add(tb);
        }

    }
}
