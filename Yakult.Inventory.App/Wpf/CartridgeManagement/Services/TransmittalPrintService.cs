using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Yakult.Inventory.App.WPF.CartridgeManagement.Dialogs;
using Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels;
using Yakult.Inventory.App.WPF.CartridgeManagement.Views;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Services
{
    public static class TransmittalPrintService
    {
        public enum TransmittalPageSize { Letter, Legal, A4 }

        // Page dimensions in WPF device-independent units (96 DPI: 1 unit = 1/96 inch).
        // A4 remains the default to preserve the form's existing printed size; Letter
        // and Legal are offered as print-preview options, same as the requisition form.
        public static (double Width, double Height) GetPageDimensions(TransmittalPageSize size)
        {
            switch (size)
            {
                case TransmittalPageSize.Letter: return (816.0, 1056.0);  // 8.5" x 11"
                case TransmittalPageSize.Legal:  return (816.0, 1344.0);  // 8.5" x 14"
                default:                         return (793.7, 1122.5); // A4: 210mm x 297mm
            }
        }

        // 0.5-inch page margin on all sides (48 units = 0.5 inch at 96 DPI)
        private const double PageMargin = 48;

        // Empirically calibrated to the HP Color LaserJet Pro M478f-9f PCL-6 driver
        // (network queue), measured 2026-07-08: confirmed via PrintDiagnosticsService
        // logs that Selected Page Size / PrintTicket.PageMediaSize / PageImageableArea
        // were all correct (Legal, 816x1344, symmetric 16-unit margin) for this exact
        // print, and Microsoft Print to PDF renders the identical FixedDocument content
        // with zero offset — yet this printer still places everything 0.5in (48 units)
        // lower than the true physical page center on two independent content types
        // (the calibration ruler page and the real transmittal form). PrintCapabilities
        // does not expose this — it is a driver/print-engine-level behavior invisible
        // to any PrintTicket/PrintCapabilities property .NET can query, so there is no
        // way to derive it generically. This constant is a fixed, printer-specific
        // correction, applied only to actual physical prints (never to Print Preview,
        // which already renders correctly) — re-measure and adjust if the print
        // queue/driver for this printer ever changes.
        public const double PhysicalPrintVerticalCompensation = 48;

        public enum CopyType { Gatepass, Transmittal, File }

        /// <summary>
        /// Full workflow:
        ///   1. Show editable Prepare Transmittal dialog.
        ///   2. If cancelled/skipped/closed, abort and report false — callers should
        ///      NOT treat this as "fulfillment flow completed" (e.g. must not navigate
        ///      away to a follow-on step like Send Notifications).
        ///   3. Apply edits to VM, then show read-only print preview.
        /// Returns true only once the user has actually reached and dismissed the
        /// print preview (whether they printed or skipped printing there) — i.e. they
        /// got past the Prepare Transmittal form via "Generate Preview", not via
        /// Cancel/Skip/closing that first dialog.
        /// </summary>
        public static bool ShowPrintDialog(CartridgeTransmittalViewModel vm)
        {
            // Loops so "Go Back" on the print preview can return to the Prepare
            // Transmittal form to re-edit fields, then come back to preview again.
            while (true)
            {
                var prepareDialog = new PrepareTransmittalDialog(vm);
                SetDialogOwner(prepareDialog);
                if (prepareDialog.ShowDialog() != true) return false;

                prepareDialog.ViewModel.ApplyTo(vm);

                // Checkbox state is now decided per-copy by ApplyCopyTypeFlags when
                // each page is actually rendered — nothing to set here.
                var printDialog = new PrintTransmittalDialog(vm);
                SetDialogOwner(printDialog);
                printDialog.ShowDialog();

                if (!printDialog.WentBack) return true;
            }
        }

        // These dialogs are opened from a UserControl hosted inside a WinForms
        // ElementHost, so Window.GetWindow() can't find a WPF ancestor — and with
        // no Owner set, WPF's modal ShowDialog() disables the currently-active
        // top-level WinForms Form via Win32 EnableWindow() but has no owner
        // relationship to bring it back to front. If the user switches to another
        // app and back while the dialog is still open, the disabled WinForms Form
        // resurfaces (it has the taskbar entry) while the real modal dialog stays
        // hidden behind it — its ElementHost content still responds (separate
        // child HWND, not disabled) but the native title-bar Close/Minimize/
        // Maximize buttons don't, because the top-level frame itself is disabled.
        // Setting Owner via WindowInteropHelper fixes the Z-order/taskbar
        // relationship so the dialog always resurfaces above its owner.
        private static void SetDialogOwner(Window dialog)
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(dialog);
            var wfForm = System.Windows.Forms.Form.ActiveForm;
            if (wfForm != null)
                helper.Owner = wfForm.Handle;
        }

        /// <summary>
        /// Builds one page-sized visual containing the given copy in the top half,
        /// and optionally a second copy in the bottom half separated by a dashed cut
        /// line. Used by both the print preview dialog and TransmittalPrintService.
        /// Print(), so what the user reviews is exactly what comes out of the printer.
        /// </summary>
        public static FrameworkElement BuildPageVisual(
            CartridgeTransmittalViewModel vm, CopyType top, CopyType? bottom,
            TransmittalPageSize pageSize = TransmittalPageSize.A4, double topExtraMargin = 0)
        {
            var (pageWidth, pageHeight) = GetPageDimensions(pageSize);
            double contentWidth = pageWidth - (2 * PageMargin);

            // The dashed guide line always sits at the exact paper midpoint,
            // independent of any copy's content — it is a fixed cut mark, not a
            // layout boundary. Copies must never be positioned relative to it (that
            // was tried and rejected: centering a copy's content against "the space
            // between the page edge and the line" makes the copy's position drift
            // depending on where the line happens to land, and reads as the top
            // copy being pushed down away from the page's top edge). Instead each
            // copy is pinned a fixed PageMargin below its own nearest edge — the
            // physical page top for an upper copy, the guide line itself for the
            // lower copy — completely independent of the other copy or the line.
            double midpointY = pageHeight / 2.0;

            var page = new Grid { Width = pageWidth, Height = pageHeight, Background = Brushes.White };

            // topExtraMargin borrows from the slack space between the top copy's
            // own content and the cut line (pushing the top copy further down,
            // never moving the line or the bottom copy) — used only by the
            // physical Print() path to counteract PhysicalPrintVerticalCompensation
            // eating into the top copy's margin. Left at 0 for print preview, which
            // isn't shifted and already renders the correct symmetric margin.
            page.Children.Add(CreateHalfView(vm, top, PageMargin + topExtraMargin, contentWidth));
            page.Children.Add(CreateCutLine(pageWidth, midpointY));

            if (bottom.HasValue)
                page.Children.Add(CreateHalfView(vm, bottom.Value, midpointY + PageMargin, contentWidth));

            page.Measure(new Size(pageWidth, pageHeight));
            page.Arrange(new Rect(0, 0, pageWidth, pageHeight));
            page.UpdateLayout();
            return page;
        }

        /// <summary>
        /// Builds one page visual per two selected copies (caller supplies the
        /// order — GetSelectedCopies in PrintTransmittalDialog passes Gatepass,
        /// Transmittal, File), pairing them top/bottom the same way BuildPageVisual
        /// always has. Used by both the print preview (to show exactly what will
        /// print) and Print() itself, so a partial selection (e.g. File only)
        /// still reuses the same layout math as the full 3-copy case.
        /// </summary>
        public static System.Collections.Generic.List<FrameworkElement> BuildPages(
            CartridgeTransmittalViewModel vm, System.Collections.Generic.IReadOnlyList<CopyType> copies,
            TransmittalPageSize pageSize = TransmittalPageSize.A4, double topExtraMargin = 0)
        {
            var pages = new System.Collections.Generic.List<FrameworkElement>();
            for (int i = 0; i < copies.Count; i += 2)
            {
                CopyType? bottom = (i + 1 < copies.Count) ? copies[i + 1] : (CopyType?)null;
                pages.Add(BuildPageVisual(vm, copies[i], bottom, pageSize, topExtraMargin));
            }
            return pages;
        }

        // Shared dashed cut/guide line so Page 1 and Page 2 use identical styling
        // and width — X-span uses the same PageMargin as the form content, so the
        // line's left/right edges always line up with the printed form below it.
        private static UIElement CreateCutLine(double pageWidth, double y) => new Line
        {
            X1 = PageMargin, X2 = pageWidth - PageMargin, Y1 = 0, Y2 = 0,
            Stroke = Brushes.Gray, StrokeThickness = 0.75,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, y, 0, 0)
        };

        // Each copy checks exactly one of GATEPASS/TRANSMITTAL/FILE — the one
        // matching its own CopyType — never more than one box per copy.
        private static void ApplyCopyTypeFlags(CartridgeTransmittalViewModel vm, CopyType type)
        {
            vm.IsGatepassCopy    = type == CopyType.Gatepass;
            vm.IsTransmittalCopy = type == CopyType.Transmittal;
            vm.IsFileCopy        = type == CopyType.File;
        }

        // Clones the VM per half and sets that clone's copy-type flags before
        // binding — the VM has no INPC, so if both halves of a page shared one
        // instance, mutating the flags for the second half (e.g. Transmittal)
        // would silently flip the already-bound first half's (e.g. Gatepass)
        // checkboxes too, since bindings only reflect whatever the shared object
        // currently holds. Cloning keeps each half's flag state independent of
        // the other. Top/Left-aligned and un-Heighted so the Grid sizes it to its
        // own natural content — the caller's Margin.Top (including any centering
        // offset) is what positions it.
        private static FrameworkElement CreateHalfView(
            CartridgeTransmittalViewModel vm, CopyType type, double top, double contentWidth)
        {
            var copyVm = vm.Clone();
            ApplyCopyTypeFlags(copyVm, type);

            var view = new CartridgeTransmittalPrintView
            {
                DataContext         = copyVm,
                Width                = contentWidth,
                HorizontalAlignment  = HorizontalAlignment.Left,
                VerticalAlignment    = VerticalAlignment.Top,
                Margin               = new Thickness(PageMargin, top, 0, 0)
            };

            return view;
        }

        /// <summary>
        /// Prints one page per two selected copies (see BuildPages), e.g. with all
        /// three selected:
        ///   Page 1 — GATEPASS copy (top half) + TRANSMITTAL copy (bottom half)
        ///   Page 2 — FILE copy
        /// </summary>
        public static void Print(
            CartridgeTransmittalViewModel vm,
            System.Collections.Generic.IReadOnlyList<CopyType> copies,
            TransmittalPageSize pageSize = TransmittalPageSize.A4)
        {
            if (copies == null || copies.Count == 0) return;

            var dlg = new System.Windows.Controls.PrintDialog();
            if (dlg.ShowDialog() != true) return;

            var (pageWidth, pageHeight) = GetPageDimensions(pageSize);

            // ShowDialog() alone leaves the printer's own default paper size in
            // effect (commonly Letter) — it does NOT pick up the Letter/Legal/A4
            // radio button from PrintTransmittalDialog. Without forcing the actual
            // PrintTicket to match, our FixedDocument content gets laid out for the
            // selected pageSize (e.g. A4, 1122.5 units tall) while the printer feeds
            // a physically shorter sheet (Letter, 1056 units) and silently clips
            // everything past its real paper edge — the dotted cut line and the
            // bottom copy's signature rows disappearing off the printed sheet.
            ApplyPageMediaSize(dlg, pageSize);

            // Diagnostic-only — logs to Debug console, does not affect printing.
            PrintDiagnosticsService.LogPageMetrics("TRANSMITTAL", dlg, pageSize, pageWidth, pageHeight,
                topY: PageMargin, lineY: pageHeight / 2.0, bottomY: (pageHeight / 2.0) + PageMargin);

            var doc = new FixedDocument();
            doc.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);

            // Every page's top copy needs the extra top margin: PhysicalPrintVerticalCompensation
            // shifts the ENTIRE composed page up by 48 units to correct the printer's
            // own downward offset — which also shifts the top copy's margin right off
            // the physical page edge. Restoring it here (borrowed from the top copy's
            // own slack space, not from the line/bottom copy) cancels that side effect
            // for the one copy on each page that sits close enough to the edge to be
            // clipped by it.
            foreach (var page in BuildPages(vm, copies, pageSize, PhysicalPrintVerticalCompensation))
                doc.Pages.Add(ToPageContent(page, pageWidth, pageHeight, PhysicalPrintVerticalCompensation));

            dlg.PrintDocument(doc.DocumentPaginator, "Cartridge Transmittal Form");
        }

        private static System.Printing.PageMediaSizeName ToPageMediaSizeName(TransmittalPageSize size)
        {
            switch (size)
            {
                case TransmittalPageSize.Letter: return System.Printing.PageMediaSizeName.NorthAmericaLetter;
                case TransmittalPageSize.Legal:  return System.Printing.PageMediaSizeName.NorthAmericaLegal;
                default:                         return System.Printing.PageMediaSizeName.ISOA4;
            }
        }

        // Directly assigning dlg.PrintTicket.PageMediaSize produces a ticket that
        // was never validated against the selected printer's actual capabilities —
        // some drivers (confirmed: Microsoft Print to PDF) reject that ticket
        // outright at spool time with a RuntimeWrappedException out of
        // StartDocPrinterW, crashing the print instead of printing anything.
        // PrintQueue.MergeAndValidatedPrintTicket() is the documented, correct way
        // to change ticket properties — it merges the delta into a full ticket the
        // driver has actually validated. Shared by both Print() and
        // CalibrationPrintService.Print() so both go through the same safe path.
        public static void ApplyPageMediaSize(System.Windows.Controls.PrintDialog dlg, TransmittalPageSize pageSize)
        {
            var delta = new System.Printing.PrintTicket
            {
                PageMediaSize = new System.Printing.PageMediaSize(ToPageMediaSizeName(pageSize))
            };
            var result = dlg.PrintQueue.MergeAndValidatePrintTicket(dlg.PrintTicket, delta);
            dlg.PrintTicket = result.ValidatedPrintTicket;
        }

        // verticalOffset shifts the WHOLE composed page visual (every copy, the cut
        // line, everything) up as one rigid unit within the FixedPage, without
        // touching any of BuildPageVisual's internal Y-coordinate math — it exists
        // purely to counteract PhysicalPrintVerticalCompensation's printer-specific
        // downward shift. Defaults to 0 (no shift) for anything not going through
        // Print()'s physical print path.
        internal static PageContent ToPageContent(
            FrameworkElement pageVisual, double pageWidth, double pageHeight, double verticalOffset = 0)
        {
            var fixedPage = new FixedPage { Width = pageWidth, Height = pageHeight };
            FixedPage.SetLeft(pageVisual, 0);
            FixedPage.SetTop(pageVisual, -verticalOffset);
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
