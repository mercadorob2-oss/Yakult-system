using System;
using System.Windows.Forms;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Receipt;

namespace Yakult.Inventory.App.Wpf.Receipt
{
    /// <summary>
    /// Launches the WPF ReceiptSetViewerWindow from WinForms call sites, owned by the given
    /// WinForms Form. This is the replacement entry point for the legacy WinForms
    /// ReceiptSetViewerDialog (Pages\Receipt\ReceiptSetViewerDialog.cs).
    ///
    /// Note: the new viewer does not distinguish read-only mode or a specific initial slot the
    /// same way the legacy dialog did (multi-image galleries replace single "slots"); those
    /// parameters are accepted for call-site compatibility but only affect which document tab
    /// opens by default.
    /// </summary>
    public static class ReceiptSetViewerLauncher
    {
        public static void ShowForNewReceipt(Form owner)
        {
            var window = new ReceiptSetViewerWindow((ReceiptSetDto)null);
            ShowOwned(window, owner);
        }

        public static void ShowForSet(Form owner, int setId, string initialSupplier = null)
        {
            var window = new ReceiptSetViewerWindow(setId, initialSupplier);
            ShowOwned(window, owner);
        }

        public static void ShowForReceipt(Form owner, ReceiptSetDto dto, bool readOnly = false, ReceiptSetViewerDialog.ReceiptSlot? initialSlot = null)
        {
            var window = new ReceiptSetViewerWindow(dto);
            if (readOnly)
                window.Title = window.Title + " (Read-Only)";
            ShowOwned(window, owner);
        }

        private static void ShowOwned(System.Windows.Window window, Form owner)
        {
            if (owner != null)
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = owner.Handle;
            }

            window.ShowDialog();
        }

        // WPF-owner overloads — for call sites that are themselves a WPF Window (e.g.
        // ViewInvoiceDetailPage) rather than a WinForms Form.
        public static void ShowForSet(System.Windows.Window owner, int setId, string initialSupplier = null)
        {
            var window = new ReceiptSetViewerWindow(setId, initialSupplier);
            ShowOwnedByWindow(window, owner);
        }

        public static void ShowForReceipt(System.Windows.Window owner, ReceiptSetDto dto, bool readOnly = false, ReceiptSetViewerDialog.ReceiptSlot? initialSlot = null)
        {
            var window = new ReceiptSetViewerWindow(dto);
            if (readOnly)
                window.Title = window.Title + " (Read-Only)";
            ShowOwnedByWindow(window, owner);
        }

        private static void ShowOwnedByWindow(System.Windows.Window window, System.Windows.Window owner)
        {
            if (owner != null)
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = new System.Windows.Interop.WindowInteropHelper(owner).Handle;
            }

            window.ShowDialog();
        }
    }
}
