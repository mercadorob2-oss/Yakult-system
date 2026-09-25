using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Yakult.Inventory.App.WPF.Set.RequisitionForm.Dialogs;
using Yakult.Inventory.App.WPF.Set.RequisitionForm.ViewModels;
using Yakult.Inventory.App.WPF.Set.RequisitionForm.Views;

namespace Yakult.Inventory.App.WPF.Set.RequisitionForm.Services
{
    public enum RequisitionPageSize
    {
        Letter,
        Legal,
        A4
    }

    public static class RequisitionFormPrintService
    {
        // Side margins match the reference workbook (0.25in), giving the form the
        // full 8in content width instead of a narrower 7.5in.
        private const double PageMarginX = 24.0;

        // Minimum breathing room between a copy and the edge of its own half of the
        // sheet — a safety allowance against printer hardware margins.
        private const double PageMarginY = 36.0;

        // Never shrink text past this factor — beyond this point it stops being legible.
        private const double MinFontScale = 0.55;

        // Page dimensions in WPF device-independent units (96 DPI: 1 unit = 1/96 inch).
        // Letter is the default so the two stacked copies line up with the standard
        // company requisition pad; Legal and A4 are offered as print-preview options.
        public static (double Width, double Height) GetPageDimensions(RequisitionPageSize size)
        {
            switch (size)
            {
                case RequisitionPageSize.Legal: return (816.0, 1344.0);   // 8.5" x 14"
                case RequisitionPageSize.A4:     return (793.92, 1122.24); // 210mm x 297mm
                default:                         return (816.0, 1056.0);  // Letter: 8.5" x 11"
            }
        }

        /// <summary>
        /// Full workflow:
        ///   1. Show editable Prepare Requisition dialog.
        ///   2. If cancelled/skipped, abort.
        ///   3. Apply edits to VM, then show read-only print preview.
        /// </summary>
        /// <param name="ownerWindow">
        /// The WPF window invoking this (e.g. ViewSetDetailPage). Pass it explicitly
        /// whenever the caller is itself a top-level WPF Window — see SetDialogOwner.
        /// </param>
        public static void ShowPrintDialog(RequisitionFormViewModel vm, Window ownerWindow = null)
            => ShowPrintDialog(vm, ownerWindow, IntPtr.Zero);

        /// <summary>
        /// Same workflow, owned by a WinForms window given by its handle. Use this from a WPF
        /// UserControl hosted in an ElementHost: pass the hosting form's handle so the dialogs
        /// never depend on Form.ActiveForm (see SetDialogOwner).
        /// </summary>
        public static void ShowPrintDialog(RequisitionFormViewModel vm, IntPtr ownerHandle)
            => ShowPrintDialog(vm, null, ownerHandle);

        private static void ShowPrintDialog(RequisitionFormViewModel vm, Window ownerWindow, IntPtr ownerHandle)
        {
            var prepareDialog = new PrepareRequisitionDialog(vm);
            SetDialogOwner(prepareDialog, ownerWindow, ownerHandle);
            if (prepareDialog.ShowDialog() != true) return;

            prepareDialog.ViewModel.ApplyTo(vm);

            var printDialog = new PrintRequisitionDialog(vm);
            SetDialogOwner(printDialog, ownerWindow, ownerHandle);
            printDialog.ShowDialog();
        }

        // When the caller is a top-level WPF Window (e.g. ViewSetDetailPage), set its
        // native Owner directly — that's the only correct way to establish the
        // Z-order/taskbar relationship between two WPF windows.
        //
        // Falling back to Form.ActiveForm (as TransmittalPrintService does) is ONLY
        // correct when the caller is a WPF UserControl hosted inside a WinForms
        // ElementHost, with no WPF ancestor window of its own. Using that fallback
        // here when ownerWindow is available would attach the dialog to whatever
        // WinForms Form happens to sit BEHIND the caller's WPF window instead of to
        // the caller itself. Alt-tabbing away and back then reactivates the wrong
        // owner, desyncing the Z-order/taskbar chain — the caller's WPF window (and
        // any dialog stacked on it) ends up stranded off-screen while the process
        // keeps running, un-closable except via Task Manager.
        //
        // An explicit ownerHandle (the hosting WinForms form) beats Form.ActiveForm: ActiveForm is
        // null whenever the app is not the foreground window at that instant (e.g. right after a
        // MessageBox closes). A modal dialog with no owner then opens BEHIND the form it disables,
        // leaving the app unclickable, including its minimize / close buttons.
        private static void SetDialogOwner(Window dialog, Window ownerWindow, IntPtr ownerHandle)
        {
            if (ownerWindow != null)
            {
                dialog.Owner = ownerWindow;
                return;
            }

            var helper = new System.Windows.Interop.WindowInteropHelper(dialog);
            if (ownerHandle != IntPtr.Zero)
            {
                helper.Owner = ownerHandle;
                return;
            }

            var wfForm = System.Windows.Forms.Form.ActiveForm;
            if (wfForm != null)
                helper.Owner = wfForm.Handle;
        }

        // Measures one copy of the form at the given font scale, without arranging it
        // into the page yet. Used both to probe the natural height and to build the
        // final copies once the right scale has been found.
        private static RequisitionFormPrintView BuildCopy(RequisitionFormViewModel vm, double fontScale, double contentWidth)
        {
            var view = new RequisitionFormPrintView
            {
                DataContext         = vm,
                FontScale           = fontScale,
                Width               = contentWidth,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Top
            };
            view.Measure(new Size(contentWidth, double.PositiveInfinity));
            return view;
        }

        // Finds the largest font scale (down to MinFontScale) at which one copy of the
        // form fits within its half of the page. Requisition forms with few line items
        // print at full size (scale 1.0); forms with many items shrink just enough to
        // still fit a single page instead of spilling the 2nd copy onto a new sheet.
        private static double ComputeFontScale(RequisitionFormViewModel vm, double contentWidth, double copyHeight)
        {
            double scale = 1.0;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                double desired = BuildCopy(vm, scale, contentWidth).DesiredSize.Height;
                if (desired <= copyHeight || scale <= MinFontScale) break;

                scale = Math.Max(MinFontScale, scale * (copyHeight / desired));
            }
            return scale;
        }

        /// <summary>
        /// Builds one page-sized visual containing either one or two copies of the
        /// requisition form, per <paramref name="copies"/>. Both cases use the exact
        /// same top-half sizing/position — selecting 1 copy just omits the bottom copy
        /// and the cut line rather than re-scaling the remaining copy to fill the page.
        /// Used by both the print preview dialog and Print(), so what the user reviews
        /// is exactly what comes out of the printer.
        /// </summary>
        public static FrameworkElement BuildPageVisual(RequisitionFormViewModel vm, RequisitionPageSize pageSize = RequisitionPageSize.Letter, int copies = 2)
        {
            var (pageWidth, pageHeight) = GetPageDimensions(pageSize);
            double contentWidth = pageWidth - (2 * PageMarginX);

            // Each copy owns exactly half the sheet, so the cut line is the page's
            // true midpoint.
            double halfHeight    = pageHeight / 2.0;
            double maxCopyHeight = halfHeight - (2 * PageMarginY);

            var page = new Grid { Width = pageWidth, Height = pageHeight, Background = Brushes.White };

            double fontScale = ComputeFontScale(vm, contentWidth, maxCopyHeight);

            // Centre the content inside its own half rather than offsetting from the
            // page margin. Anchoring to the margin made the two copies asymmetric:
            // the top copy sat ~0.19in below its half's centre while the bottom copy
            // sat ~0.19in above its own, so cutting the sheet down the middle produced
            // two halves whose forms were ~0.375in out of alignment with each other.
            // Deriving both offsets from the same half-height keeps them identical.
            double contentHeight = Math.Min(maxCopyHeight, BuildCopy(vm, fontScale, contentWidth).DesiredSize.Height);
            double copyOffset    = Math.Max(PageMarginY, (halfHeight - contentHeight) / 2.0);

            // No manual Arrange() call here — copyTop/copyBottom are Top/Left-aligned,
            // so the Grid sizes them to their own DesiredSize once added below. A
            // pre-emptive Arrange() with a different (copyHeight-sized) rect risked
            // leaving a stale ActualHeight if it didn't get fully invalidated when the
            // element was reparented, which showed up as the second copy overflowing
            // past the page edge for short forms.
            var copyTop = BuildCopy(vm, fontScale, contentWidth);
            copyTop.Margin = new Thickness(PageMarginX, copyOffset, 0, 0);
            page.Children.Add(copyTop);

            // The bottom copy uses the same offset measured from the midpoint, so both
            // copies land in the same spot on their respective halves. No dashed
            // fold/cut marker is drawn.
            if (copies >= 2)
            {
                var copyBottom = BuildCopy(vm, fontScale, contentWidth);
                copyBottom.Margin = new Thickness(PageMarginX, halfHeight + copyOffset, 0, 0);
                page.Children.Add(copyBottom);
            }

            page.UpdateLayout();
            return page;
        }

        public static void Print(RequisitionFormViewModel vm, RequisitionPageSize pageSize = RequisitionPageSize.Letter, int copies = 2)
        {
            Print(vm, pageSize, copies, null);
        }

        // printerName lets callers target a specific queue (e.g. "Microsoft Print to
        // PDF" for Save-as-PDF flows) instead of always prompting the user to pick one.
        public static void Print(RequisitionFormViewModel vm, RequisitionPageSize pageSize, int copies, string printerName)
        {
            var dlg = new System.Windows.Controls.PrintDialog();
            System.Printing.PrintServer printServer = null;
            System.Printing.PrintQueue namedQueue = null;
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
                        printServer = new System.Printing.PrintServer();
                        namedQueue = new System.Printing.PrintQueue(printServer, printerName);
                        dlg.PrintQueue = namedQueue;
                    }
                    catch (System.Printing.PrintSystemException ex)
                    {
                        MessageBox.Show(
                            "Could not print to printer \"" + printerName + "\": " + ex.Message,
                            "Print failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }

                var (pageWidth, pageHeight) = GetPageDimensions(pageSize);

                // Tell the printer which paper to use. Without this the queue stays on
                // its default media (usually Letter, 11in tall), so a 14in Legal page
                // overflows and the driver pushes the bottom-half copy onto a second
                // sheet instead of keeping both copies on one page.
                ApplyPageMediaSize(dlg, pageSize);

                var doc = new FixedDocument();
                doc.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);
                doc.Pages.Add(ToPageContent(BuildPageVisual(vm, pageSize, copies), pageWidth, pageHeight));

                dlg.PrintDocument(doc.DocumentPaginator, "Requisition Form");
            }
            finally
            {
                if (namedQueue != null) namedQueue.Dispose();
                if (printServer != null) printServer.Dispose();
            }
        }

        // Forces the print queue onto the paper the form was laid out for, so the
        // sheet is never split or shrunk by the driver.
        private static void ApplyPageMediaSize(System.Windows.Controls.PrintDialog dlg, RequisitionPageSize pageSize)
        {
            try
            {
                var delta = new System.Printing.PrintTicket
                {
                    PageMediaSize = new System.Printing.PageMediaSize(MediaSizeName(pageSize)),
                    PageOrientation = System.Printing.PageOrientation.Portrait
                };
                var validated = dlg.PrintQueue.MergeAndValidatePrintTicket(dlg.PrintTicket, delta);
                dlg.PrintTicket = validated.ValidatedPrintTicket;
            }
            catch (System.Printing.PrintSystemException)
            {
                // Queue rejected the media change (e.g. virtual printer with a fixed
                // size) — fall back to its current ticket rather than aborting.
            }
        }

        private static System.Printing.PageMediaSizeName MediaSizeName(RequisitionPageSize pageSize)
        {
            switch (pageSize)
            {
                case RequisitionPageSize.Legal: return System.Printing.PageMediaSizeName.NorthAmericaLegal;
                case RequisitionPageSize.A4:    return System.Printing.PageMediaSizeName.ISOA4;
                default:                        return System.Printing.PageMediaSizeName.NorthAmericaLetter;
            }
        }

        private static PageContent ToPageContent(FrameworkElement pageVisual, double pageWidth, double pageHeight)
        {
            var fixedPage = new FixedPage { Width = pageWidth, Height = pageHeight };
            FixedPage.SetLeft(pageVisual, 0);
            FixedPage.SetTop(pageVisual, 0);
            fixedPage.Children.Add(pageVisual);
            fixedPage.Measure(new Size(pageWidth, pageHeight));
            fixedPage.Arrange(new Rect(0, 0, pageWidth, pageHeight));
            fixedPage.UpdateLayout();

            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(fixedPage);
            return pageContent;
        }
    }
}
