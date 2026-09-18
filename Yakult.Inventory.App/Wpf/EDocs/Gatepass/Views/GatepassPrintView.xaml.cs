using System.Collections.Generic;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using Yakult.Inventory.App.WPF.EDocs.Gatepass.Models;
using Yakult.Inventory.App.WPF.EDocs.Gatepass.ViewModels;

namespace Yakult.Inventory.App.WPF.EDocs.Gatepass.Views
{
    // Host + print flow for the gatepass form, matching the mobile reference
    // (GatepassPrintUtils.kt). Copies are drawn in point-space (612 x 1008 per
    // sheet) by GatepassCopyElement and scaled x4/3 to a WPF Legal page.
    public partial class GatepassPrintView : UserControl
    {
        // WPF Legal page (96 DPI: 8.5in x 14in).
        private const double LegalWidth = 816.0;
        private const double LegalHeight = 1344.0;
        // Reference PDF point-space page (72 DPI: 8.5in x 14in) and copy height.
        private const double PagePointWidth = 612.0;
        private const double PagePointHeight = 1008.0;
        private const double CopyHeight = 504.0;
        private const double Scale = LegalWidth / PagePointWidth; // 4/3

        public GatepassPrintView()
        {
            InitializeComponent();
            DataContextChanged += (s, e) => BuildPreview();
        }

        // ── Preview ───────────────────────────────────────────────────────────
        private void BuildPreview()
        {
            PagesHost.Children.Clear();
            var vm = DataContext as GatepassPrintViewModel;
            if (vm == null || vm.Document == null) return;

            foreach (var page in BuildPages(vm))
            {
                var container = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 16),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    LayoutTransform = new ScaleTransform(Scale, Scale),
                    Child = page
                };
                PagesHost.Children.Add(container);
            }
        }

        // ── Page model (mirrors createGatepassPrintPdf) ───────────────────────
        private static List<Canvas> BuildPages(GatepassPrintViewModel vm)
        {
            var pages = new List<Canvas>();
            if (vm.Document.PrintAllThree)
            {
                // Sheet 1: Gatepass (top) + Transmittal (bottom).
                pages.Add(BuildPageCanvas(
                    FormCopy(vm, GatepassFormType.Gatepass),
                    FormCopy(vm, GatepassFormType.Transmittal)));
                // Sheet 2: File (top) + data-only summary (bottom).
                pages.Add(BuildPageCanvas(
                    FormCopy(vm, GatepassFormType.File),
                    DataOnlyCopy(vm)));
            }
            else
            {
                // Default: two identical copies of the selected form type.
                var formType = vm.Document.FormType;
                pages.Add(BuildPageCanvas(FormCopy(vm, formType), FormCopy(vm, formType)));
            }
            return pages;
        }

        private static GatepassCopyElement FormCopy(GatepassPrintViewModel vm, GatepassFormType formType)
        {
            return new GatepassCopyElement { ViewModel = vm, FormType = formType, IsDataOnly = false };
        }

        private static GatepassCopyElement DataOnlyCopy(GatepassPrintViewModel vm)
        {
            return new GatepassCopyElement { ViewModel = vm, IsDataOnly = true };
        }

        private static Canvas BuildPageCanvas(FrameworkElement top, FrameworkElement bottom)
        {
            var canvas = new Canvas
            {
                Width = PagePointWidth,
                Height = PagePointHeight,
                Background = Brushes.White
            };
            Canvas.SetLeft(top, 0);
            Canvas.SetTop(top, 0);
            canvas.Children.Add(top);
            if (bottom != null)
            {
                Canvas.SetLeft(bottom, 0);
                Canvas.SetTop(bottom, CopyHeight);
                canvas.Children.Add(bottom);
            }
            return canvas;
        }

        // ── Print ─────────────────────────────────────────────────────────────
        public void Print(string printerName)
        {
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

                var vm = DataContext as GatepassPrintViewModel;
                if (vm == null || vm.Document == null) return;

                ApplyPageMediaSize(dlg);

                var doc = new FixedDocument();
                doc.DocumentPaginator.PageSize = new Size(LegalWidth, LegalHeight);
                foreach (var page in BuildPages(vm))
                    doc.Pages.Add(ToPageContent(page));

                dlg.PrintDocument(doc.DocumentPaginator, "Gatepass");
            }
            finally
            {
                if (namedQueue != null) namedQueue.Dispose();
                if (printServer != null) printServer.Dispose();
            }
        }

        private static void ApplyPageMediaSize(PrintDialog dlg)
        {
            var delta = new PrintTicket
            {
                PageMediaSize = new PageMediaSize(PageMediaSizeName.NorthAmericaLegal)
            };
            var validated = dlg.PrintQueue.MergeAndValidatePrintTicket(dlg.PrintTicket, delta);
            dlg.PrintTicket = validated.ValidatedPrintTicket;
        }

        private static PageContent ToPageContent(Canvas pageCanvas)
        {
            // Scale the 612x1008 point-space page up to the 816x1344 Legal page.
            var scaled = new Border
            {
                Child = pageCanvas,
                LayoutTransform = new ScaleTransform(Scale, Scale)
            };

            var fixedPage = new FixedPage { Width = LegalWidth, Height = LegalHeight };
            FixedPage.SetLeft(scaled, 0);
            FixedPage.SetTop(scaled, 0);
            fixedPage.Children.Add(scaled);
            fixedPage.Measure(new Size(LegalWidth, LegalHeight));
            fixedPage.Arrange(new Rect(0, 0, LegalWidth, LegalHeight));
            fixedPage.UpdateLayout();

            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(fixedPage);
            return pageContent;
        }
    }
}
