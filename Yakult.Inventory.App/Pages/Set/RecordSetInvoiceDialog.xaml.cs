using System;
using System.Globalization;
using System.Windows;
using Yakult.Inventory.App.Repositories;

using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Set
{
    /// <summary>
    /// Flags an existing Request-based dispatch Set as also being an invoice, in place — no new
    /// Set or SetItem rows are created. Captures only the invoice identity (Document / Reference)
    /// and header amounts; MarkRequestSetAsInvoiceAsync also seeds the Set's site columns
    /// (Company / Department / Branch) from the requester, since an invoice is addressed to a
    /// site rather than a person. Everything else current about invoices — the site builder,
    /// Sub-Type groups, Parent Tags — is edited on ViewInvoiceDetailPage, which the caller
    /// (ViewSetDetailPage) opens right after this returns OK.
    /// </summary>
    public partial class RecordSetInvoiceDialog : Window
    {
        private readonly int _setId;
        private readonly SetRepository _setRepository = new SetRepository();
        private readonly ServiceSetRepository _serviceSetRepository = new ServiceSetRepository();

        public RecordSetInvoiceDialog(int setId)
        {
            _setId = setId;
            InitializeComponent();
            Loaded += async (s, e) => await LoadAsync();
        }

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

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                var setDto = await _setRepository.GetSetByIdAsync(_setId);
                if (setDto == null)
                {
                    MessageBox.Show("Set not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    DialogResult = false;
                    return;
                }

                TxtSetCodeLabel.Text = $"{setDto.SetCode} — {setDto.ItemCount} item(s)";
                TxtDocumentNumber.Text = setDto.DocumentNumber ?? string.Empty;
                TxtReferenceNumber.Text = setDto.ReferenceNumber ?? string.Empty;

                var requests = await _setRepository.GetSetRequestsAsync(_setId);
                decimal subtotal = 0m;
                // GetSetRequestsAsync doesn't carry UnitPrice, so read it directly for the default.
                if (requests != null && requests.Count > 0)
                {
                    subtotal = await _setRepository.GetSetRequestsSubtotalAsync(_setId);
                }

                TxtSubtotal.Text = subtotal.ToString("N2", CultureInfo.InvariantCulture);
                CalculateTotalAmountDue();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load set: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        /// <summary>Recomputes Total Amount Due from Subtotal/VAT/WHT/Discount on every keystroke —
        /// wired from each amount TextBox's TextChanged in the XAML. Matches the calculation chain
        /// used by ViewInvoiceDetailPage.TryCalculateFinancialsFromPercentages (paper sales invoice
        /// layout: Subtotal -&gt; less Discount -&gt; net -&gt; plus VAT -&gt; less WHT -&gt; Total Due), since
        /// VAT/WHT/Discount here are entered as percentages, not flat dollar amounts.</summary>
        private void Amounts_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => CalculateTotalAmountDue();

        private void CalculateTotalAmountDue()
        {
            if (TxtTotalAmountDue == null) return; // fires before InitializeComponent finishes building the tree
            if (TryCalculateFinancials(out _, out _, out _, out decimal totalAmountDue))
                TxtTotalAmountDue.Text = totalAmountDue.ToString("N2", CultureInfo.InvariantCulture);
        }

        /// <summary>VAT/WHT/Discount are percentages of the net-after-discount amount (VAT and WHT
        /// are NOT applied to the raw Subtotal) — see the class-level remarks. Soft-fails (defaults
        /// to 0) on WHT/Discount so a blank/invalid optional field doesn't block calculation; only
        /// an unparseable Subtotal fails outright.</summary>
        private bool TryCalculateFinancials(out decimal vatAmount, out decimal discountAmount, out decimal whtAmount, out decimal totalAmountDue)
        {
            vatAmount = 0m; discountAmount = 0m; whtAmount = 0m; totalAmountDue = 0m;

            if (!decimal.TryParse(TxtSubtotal.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal subtotal))
                return false;

            decimal.TryParse(TxtVatAmount.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal vatPercent);
            decimal.TryParse(TxtDiscountAmount.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal discountPercent);
            decimal.TryParse(TxtWhtAmount.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal whtPercent);

            discountAmount = subtotal * (discountPercent / 100m);
            decimal netAfterDiscount = subtotal - discountAmount;

            vatAmount = netAfterDiscount * (vatPercent / 100m);
            whtAmount = netAfterDiscount * (whtPercent / 100m);

            totalAmountDue = netAfterDiscount + vatAmount - whtAmount;
            return true;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var documentNumber = TxtDocumentNumber.Text.Trim();
            if (string.IsNullOrWhiteSpace(documentNumber))
            {
                MessageBox.Show("Document Number is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtDocumentNumber.Focus();
                return;
            }

            if (!decimal.TryParse(TxtSubtotal.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal subtotal))
            {
                MessageBox.Show("Invalid Subtotal value.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtSubtotal.Focus();
                return;
            }

            // Persisted as dollar amounts (dbo.Set.VatAmount/WhtAmount/DiscountAmount), computed
            // fresh here from the on-screen percentages rather than re-parsing TxtTotalAmountDue's
            // display text — it's read-only and always reflects this same calculation anyway.
            TryCalculateFinancials(out decimal vatAmount, out decimal discountAmount, out decimal whtAmount, out decimal totalAmountDue);

            try
            {
                _serviceSetRepository.MarkRequestSetAsInvoiceAsync(
                    _setId,
                    documentNumber,
                    string.IsNullOrWhiteSpace(TxtReferenceNumber.Text) ? null : TxtReferenceNumber.Text.Trim(),
                    subtotal,
                    vatAmount,
                    whtAmount,
                    discountAmount,
                    totalAmountDue,
                    comId: null);

                // No confirmation dialog here — the caller opens the full invoice screen next,
                // which is the real confirmation and where the rest of the invoice is filled in.
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to record invoice: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
