using System;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Invoice
{
    /// <summary>
    /// WPF replacement for the legacy WinForms DeleteInvoiceLineDialog — adapts to two
    /// mutually exclusive contexts: deleting a single selected line (itemName given), or
    /// deleting an entire Sub-Type Group selected via its header row in the Invoice Items
    /// grid (groupDisplayName given). The old "Delete Data Item" option (permanently
    /// deleting the dbo.Item record) has been removed — this dialog only ever removes
    /// things from the invoice, never the underlying Item catalog.
    /// </summary>
    public partial class DeleteInvoiceLineDialog : Window, IDisposable
    {
        public enum DeleteChoice
        {
            None = 0,
            DeleteRowOnly = 1,
            DeleteGroup = 2
        }

        public DeleteChoice Choice { get; private set; } = DeleteChoice.None;

        public DeleteInvoiceLineDialog(string itemName, string groupDisplayName = null)
        {
            InitializeComponent();

            if (!string.IsNullOrWhiteSpace(groupDisplayName))
            {
                TxtTitle.Text = $"Delete the entire \"{groupDisplayName}\" Sub-Type Group?";
                TxtNote.Text = "This removes ALL of this group's items from the invoice and sends them back to " +
                    "the Invoice Sub Groups page's Not Completed tab, fully intact, so they can be re-invoiced later.";
                BtnDeleteGroup.Visibility = Visibility.Visible;
            }
            else
            {
                TxtTitle.Text = $"Delete this line from the invoice?\n{(string.IsNullOrWhiteSpace(itemName) ? "(selected item)" : itemName)}";
                TxtNote.Text = "This removes it from this invoice only.";
                BtnDeleteRow.Visibility = Visibility.Visible;
            }
        }

        // WinForms-compatible ShowDialog overloads (same pattern as SoftwareServiceSetDialog).
        public new WinForms.DialogResult ShowDialog()
        {
            bool? result = base.ShowDialog();
            return result == true ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }

        public WinForms.DialogResult ShowDialog(WinForms.IWin32Window owner)
        {
            if (owner != null)
                new System.Windows.Interop.WindowInteropHelper(this).Owner = owner.Handle;
            return ShowDialog();
        }

        public void Dispose() { }

        private void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
        {
            Choice = DeleteChoice.DeleteRowOnly;
            DialogResult = true;
        }

        private void BtnDeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            Choice = DeleteChoice.DeleteGroup;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
