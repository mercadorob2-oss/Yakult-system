using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Receipt;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.Item
{
    /// <summary>
    /// The "Edit Invoice Details" modal — every field that used to sit in a large block at the top
    /// of Build Invoice, moved here so the primary workspace can be spent on the Sub-Type groups
    /// and their line items instead. Purely a UI relocation: validation of these values (Document #
    /// required, Company/Distributor mutual exclusivity) still happens once, in
    /// InvoiceBuilderDialog.BuildRows, exactly as before.
    /// </summary>
    public partial class InvoiceHeaderDialog : Window
    {
        /// <summary>The edited values, populated only when the dialog closes via Save.</summary>
        public InvoiceBuilderDialog.InvoiceHeaderValues Result { get; private set; }

        private int? _receiptSetId;
        private string _receiptSetLabel;
        private ReceiptSetDto _receiptSet;

        public InvoiceHeaderDialog(
            InvoiceBuilderDialog.InvoiceHeaderValues current,
            List<string> companyNames, List<string> distributorNames,
            List<string> branchNames, List<string> departmentNames)
        {
            InitializeComponent();

            foreach (var name in companyNames) { CmbCompany.Items.Add(name); CmbSiteCompany.Items.Add(name); }
            foreach (var name in distributorNames) CmbDistributor.Items.Add(name);
            foreach (var name in branchNames) CmbSiteBranch.Items.Add(name);
            foreach (var name in departmentNames) CmbSiteDepartment.Items.Add(name);

            TxtDocumentNumber.Text = current.DocumentNumber;
            TxtReferenceNumber.Text = current.ReferenceNumber;
            DpDocumentDate.SelectedDate = current.DocumentDate;
            CmbCompany.Text = current.Company;
            CmbDistributor.Text = current.Distributor;
            CmbSiteCompany.Text = current.SiteCompany;
            CmbSiteBranch.Text = current.SiteBranch;
            CmbSiteDepartment.Text = current.SiteDepartment;
            DpStartDate.SelectedDate = current.StartDate;
            DpEndDate.SelectedDate = current.EndDate;

            _receiptSetId = current.ReceiptSetId;
            _receiptSetLabel = current.ReceiptSetLabel;
            RefreshReceiptSetLabel();

            OnCompanyOrDistributorChanged(null, null);
        }

        /// <summary>Keeps the receipt-set row in sync with the current selection, since Build
        /// Invoice's Set doesn't exist yet — this only remembers a choice for BtnCreate_Click to
        /// attach after the invoice is created.</summary>
        private void RefreshReceiptSetLabel()
        {
            bool hasSelection = _receiptSetId.HasValue;
            LblReceiptSet.Text = hasSelection
                ? _receiptSetLabel
                : "No receipt set linked. It will be attached right after the invoice is created.";
            BtnRemoveReceiptSet.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
            BtnViewReceiptSet.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
            BtnLinkReceiptSet.Content = hasSelection ? "Change" : "Link Receipt Set";
        }

        private void BtnLinkReceiptSet_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var owner = new Yakult.Inventory.App.WPF.Shared.Win32WindowWrapper(hwnd);

            using (var dialog = new AttachReceiptSetDialog())
            {
                if (dialog.ShowDialog(owner) != System.Windows.Forms.DialogResult.OK || !dialog.SelectedReceiptSetId.HasValue)
                    return;

                var set = dialog.SelectedSet;
                _receiptSet = set;
                _receiptSetId = set.ReceiptSetId;
                _receiptSetLabel = BuildReceiptSetLabel(set);
            }

            RefreshReceiptSetLabel();
        }

        private void BtnRemoveReceiptSet_Click(object sender, RoutedEventArgs e)
        {
            _receiptSet = null;
            _receiptSetId = null;
            _receiptSetLabel = null;
            RefreshReceiptSetLabel();
        }

        /// <summary>Mirrors ViewInvoiceDetailPage.OpenLinkedReceiptSetViewer — re-fetches the full
        /// dto by id (the picker's row is a lightweight projection) and opens it read-only, since
        /// this invoice's Set doesn't exist yet to attach edits to.</summary>
        private void BtnViewReceiptSet_Click(object sender, RoutedEventArgs e)
        {
            if (!_receiptSetId.HasValue) return;

            try
            {
                var dto = new ReceiptSetRepository().GetByReceiptSetId(_receiptSetId.Value)
                    ?? _receiptSet ?? new ReceiptSetDto { ReceiptSetId = _receiptSetId.Value };

                Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForReceipt(this, dto, readOnly: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to open receipt set viewer: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string BuildReceiptSetLabel(ReceiptSetDto set)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(set.Supplier)) parts.Add(set.Supplier);
            if (!string.IsNullOrWhiteSpace(set.SiNumber)) parts.Add("SI " + set.SiNumber);
            if (!string.IsNullOrWhiteSpace(set.DrNumber)) parts.Add("DR " + set.DrNumber);
            if (!string.IsNullOrWhiteSpace(set.PoNumber)) parts.Add("PO " + set.PoNumber);
            return parts.Count > 0 ? string.Join(" · ", parts) : "Receipt Set #" + set.ReceiptSetId;
        }

        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e) => Close();

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>
        /// Company and Distributor both name the same "subject" of the invoice — never both.
        /// Reddens the hint the moment they conflict, rather than waiting for the hard stop that
        /// still happens at Create Invoice time (InvoiceBuilderDialog.BuildRows) — same behaviour
        /// as before this dialog existed, just relocated with the fields themselves.
        /// </summary>
        private void OnCompanyOrDistributorChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (LblCompanyDistributorHint == null) return;

            bool bothSet = !string.IsNullOrWhiteSpace(CmbCompany.Text) && !string.IsNullOrWhiteSpace(CmbDistributor.Text);
            LblCompanyDistributorHint.Foreground = bothSet
                ? new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28))
                : new SolidColorBrush(Color.FromRgb(0x8A, 0x94, 0xA6));
            LblCompanyDistributorHint.FontWeight = bothSet ? FontWeights.SemiBold : FontWeights.Normal;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Result = new InvoiceBuilderDialog.InvoiceHeaderValues
            {
                DocumentNumber = TxtDocumentNumber.Text,
                ReferenceNumber = TxtReferenceNumber.Text,
                DocumentDate = DpDocumentDate.SelectedDate,
                Company = CmbCompany.Text,
                Distributor = CmbDistributor.Text,
                SiteCompany = CmbSiteCompany.Text,
                SiteBranch = CmbSiteBranch.Text,
                SiteDepartment = CmbSiteDepartment.Text,
                StartDate = DpStartDate.SelectedDate,
                EndDate = DpEndDate.SelectedDate,
                ReceiptSetId = _receiptSetId,
                ReceiptSetLabel = _receiptSetLabel
            };
            DialogResult = true;
            Close();
        }
    }
}
