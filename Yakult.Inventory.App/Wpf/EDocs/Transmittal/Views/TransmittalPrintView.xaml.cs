using System;
using System.Collections.Generic;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using Yakult.Inventory.App.WPF.EDocs.Transmittal.ViewModels;

namespace Yakult.Inventory.App.WPF.EDocs.Transmittal.Views
{
    // Host + print flow for the transmittal form, matching the reference
    // workbook transmittal.xlsx: sheet 1 is a Legal page carrying two half-page
    // copies (TRANSMITTAL on top, FILE below) so the paper can be cut in half,
    // followed by sheet 2 with the GATEPASS copy on its top half.
    //
    // The copies are drawn in point-space (612 x 1008 = Legal at 72 DPI) by
    // TransmittalCopyElement and scaled to the selected WPF page, the same way
    // GatepassPrintView works.
    public partial class TransmittalPrintView : UserControl
    {
        // Reference page in PDF points (Legal, 8.5in x 14in at 72 DPI).
        private const double PagePointWidth = 612.0;
        private const double PagePointHeight = 1008.0;
        // Top/bottom margins plus the gap between the two copies. The
        // reference workbook's own numbers (54 + 423.95 + 45 + 423.95 + 54 =
        // 1000.9pt) leave only ~22.5pt (0.31in) of white space on each side of
        // the cut line — comfortable for a paper cutter/guillotine, but too
        // tight for scissors: any manual wobble cuts into the ruled item lines
        // or the "TRANSMITTAL"/"FILE" heading of one of the two copies.
        // Shrinking each copy slightly (via CopyScale) buys extra allowance on
        // both sides of the cut line without changing the sheet size or
        // clipping content, so a hand cut can wander a bit and still land in
        // blank paper.
        private const double MarginTop = 54.0;
        private const double CopyGap = 81.0;
        // Uniform shrink applied to each copy so it still fits the space freed
        // up by the wider CopyGap. 1000.9pt of content/gap must fit the same
        // 900pt of usable height (1008 - 2*54); the two 423.95pt copies plus
        // the enlarged 81pt gap need trimming down to that budget.
        private const double CopyScale = (PagePointHeight - 2 * MarginTop - CopyGap) / (2 * TransmittalCopyElement.CopyHeight);

        public TransmittalPrintView()
        {
            InitializeComponent();
            DataContextChanged += (s, e) => BuildPreview();
        }

        // ── Preview ───────────────────────────────────────────────────────────
        // Rendered at the Legal scale (x4/3) so what is on screen is what a
        // Legal sheet produces.
        private void BuildPreview()
        {
            PagesHost.Children.Clear();
            var vm = DataContext as TransmittalPrintViewModel;
            if (vm == null || vm.Document == null) return;

            double scale = 816.0 / PagePointWidth;
            foreach (var page in BuildPages(vm))
            {
                PagesHost.Children.Add(new Border
                {
                    Background = Brushes.White,
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 16),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    LayoutTransform = new ScaleTransform(scale, scale),
                    Child = page
                });
            }
        }

        // ── Page model ─────────────────────────────────────────────────────────
        // Sheet 1: TRANSMITTAL (top half) + FILE (bottom half), per the
        // reference workbook. Sheet 2: GATEPASS on the top half, on its own.
        private static List<Canvas> BuildPages(TransmittalPrintViewModel vm)
        {
            return new List<Canvas>
            {
                BuildPageCanvas(vm, "TRANSMITTAL", "FILE"),
                BuildPageCanvas(vm, "GATEPASS", null)
            };
        }

        private static Canvas BuildPageCanvas(TransmittalPrintViewModel vm, string topTitle, string bottomTitle)
        {
            var canvas = new Canvas
            {
                Width = PagePointWidth,
                Height = PagePointHeight,
                Background = Brushes.White
            };
            double scaledCopyHeight = TransmittalCopyElement.CopyHeight * CopyScale;
            AddCopy(canvas, vm, topTitle, MarginTop);
            if (!string.IsNullOrEmpty(bottomTitle))
                AddCopy(canvas, vm, bottomTitle, MarginTop + scaledCopyHeight + CopyGap);
            return canvas;
        }

        private static void AddCopy(Canvas canvas, TransmittalPrintViewModel vm, string title, double top)
        {
            var copy = new TransmittalCopyElement { ViewModel = vm, CopyTitle = title };
            var host = new Border
            {
                LayoutTransform = new ScaleTransform(CopyScale, CopyScale),
                Child = copy
            };
            Canvas.SetLeft(host, 0);
            Canvas.SetTop(host, top);
            canvas.Children.Add(host);
        }

        // ── Print ─────────────────────────────────────────────────────────────
        // Pass null/empty as the printer name to prompt for a printer.
        // pageSize accepts "Legal" (the reference), "Letter" or "A4"; anything
        // smaller than Legal is scaled down to fit, exactly like Excel's
        // fit-to-page.
        public void Print(string printerName, string pageSize = "Legal", int copies = 1)
        {
            var vm = DataContext as TransmittalPrintViewModel;
            if (vm == null || vm.Document == null) return;

            var dlg = new PrintDialog();
            PrintServer printServer = null;
            PrintQueue namedQueue = null;
            try
            {
                if (string.IsNullOrWhiteSpace(printerName))
                {
                    if (dlg.ShowDialog() != true) return;
                }
                else
                {
                    try
                    {
                        printServer = new PrintServer();
                        namedQueue = new PrintQueue(printServer, printerName);
                        dlg.PrintQueue = namedQueue;
                    }
                    catch (PrintSystemException ex)
                    {
                        MessageBox.Show(
                            "Could not print to printer \"" + printerName + "\": " + ex.Message,
                            "Print failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }

                ApplyPageMediaSize(dlg, pageSize);
                var pageSizeWpf = PageSizeInWpfUnits(pageSize);

                var doc = new FixedDocument();
                doc.DocumentPaginator.PageSize = pageSizeWpf;
                int sets = copies < 1 ? 1 : copies;
                for (int i = 0; i < sets; i++)
                    foreach (var page in BuildPages(vm))
                        doc.Pages.Add(ToPageContent(page, pageSizeWpf));

                dlg.PrintDocument(doc.DocumentPaginator, "Transmittal");
            }
            finally
            {
                if (namedQueue != null) namedQueue.Dispose();
                if (printServer != null) printServer.Dispose();
            }
        }

        private static void ApplyPageMediaSize(PrintDialog dlg, string pageSize)
        {
            var delta = new PrintTicket
            {
                PageMediaSize = new PageMediaSize(MediaSizeName(pageSize))
            };
            var validated = dlg.PrintQueue.MergeAndValidatePrintTicket(dlg.PrintTicket, delta);
            dlg.PrintTicket = validated.ValidatedPrintTicket;
        }

        private static PageMediaSizeName MediaSizeName(string pageSize)
        {
            if (string.Equals(pageSize, "Letter", StringComparison.OrdinalIgnoreCase))
                return PageMediaSizeName.NorthAmericaLetter;
            if (string.Equals(pageSize, "A4", StringComparison.OrdinalIgnoreCase))
                return PageMediaSizeName.ISOA4;
            return PageMediaSizeName.NorthAmericaLegal;
        }

        // WPF units (96 DPI) for the supported paper sizes.
        private static Size PageSizeInWpfUnits(string pageSize)
        {
            if (string.Equals(pageSize, "Letter", StringComparison.OrdinalIgnoreCase))
                return new Size(816, 1056);
            if (string.Equals(pageSize, "A4", StringComparison.OrdinalIgnoreCase))
                return new Size(794, 1123);
            return new Size(816, 1344);
        }

        private static PageContent ToPageContent(Canvas pageCanvas, Size pageSize)
        {
            // Uniform scale from the 612x1008 point-space sheet onto the chosen
            // page, so Letter/A4 shrink the whole sheet instead of clipping it.
            double scale = Math.Min(pageSize.Width / PagePointWidth, pageSize.Height / PagePointHeight);
            var scaled = new Border
            {
                Child = pageCanvas,
                LayoutTransform = new ScaleTransform(scale, scale)
            };

            var fixedPage = new FixedPage { Width = pageSize.Width, Height = pageSize.Height };
            FixedPage.SetLeft(scaled, 0);
            FixedPage.SetTop(scaled, 0);
            fixedPage.Children.Add(scaled);
            fixedPage.Measure(pageSize);
            fixedPage.Arrange(new Rect(0, 0, pageSize.Width, pageSize.Height));
            fixedPage.UpdateLayout();

            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(fixedPage);
            return pageContent;
        }
    }
}
