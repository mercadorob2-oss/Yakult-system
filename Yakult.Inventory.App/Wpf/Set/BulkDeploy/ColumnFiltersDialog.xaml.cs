using System;
using System.Windows;

namespace Yakult.Inventory.App.Wpf.Set.BulkDeploy
{
    /// <summary>
    /// Non-modal dialog housing the "Step 1 Columns" checkboxes and presets
    /// that used to live inline in BulkDeployWindow. Kept as a separate
    /// window (not a modal ShowDialog) so the user can still see/interact
    /// with the grid behind it while toggling columns, and so every change
    /// still applies live via the existing OptionChanged/DropdownChanged
    /// events - identical behavior to the old inline checkboxes, just
    /// relocated into a popup opened by a single "Column Filters..." button.
    /// </summary>
    public partial class ColumnFiltersDialog : Window
    {
        public event EventHandler OptionChanged;
        public event EventHandler DropdownChanged;

        public bool IsDateOn => ChkDate.IsChecked == true;
        public bool IsPcOn => ChkPc.IsChecked == true;
        public bool IsIpOn => ChkIp.IsChecked == true;
        public bool IsDeptOn => ChkDept.IsChecked == true;
        public bool IsFaOn => ChkFa.IsChecked == true;
        public bool IsEmpOn => ChkEmp.IsChecked == true;
        public bool IsComOn => ChkCom.IsChecked == true;
        public bool IsBranchOn => ChkBranch.IsChecked == true;
        public bool IsCatOn => ChkCat.IsChecked == true;
        public bool IsDropdownOn => ChkDropdown.IsChecked == true;

        public ColumnFiltersDialog()
        {
            InitializeComponent();
            // "Done" hides the window instead of closing it (see BtnDone_Click)
            // so the same instance can be reopened via Show() indefinitely -
            // WPF Windows cannot be Show()n again after Close() destroys their
            // HWND ("Cannot set Visibility or call Show... after a Window has
            // closed"). This mirrors the WPF-overlay lifecycle pattern used
            // elsewhere in this app (WpfPortalTourWindow): never destroy the
            // HWND if the window needs to be shown again later. Only allow a
            // real Close() when the owner (BulkDeployWindow) itself closes.
            Closing += ColumnFiltersDialog_Closing;
        }

        private bool _allowClose;

        private void ColumnFiltersDialog_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_allowClose) return;
            e.Cancel = true;
            Hide();
        }

        /// <summary>
        /// Called by BulkDeployWindow when it is actually closing, so this
        /// dialog's HWND is allowed to be destroyed along with its owner
        /// instead of being hidden forever.
        /// </summary>
        public void AllowRealClose()
        {
            _allowClose = true;
        }

        public void ApplyState(bool date, bool pc, bool ip, bool fa, bool emp, bool com, bool branch, bool cat, bool dropdown)
        {
            ChkDate.IsChecked = date;
            ChkPc.IsChecked = pc;
            ChkIp.IsChecked = ip;
            ChkFa.IsChecked = fa;
            ChkEmp.IsChecked = emp;
            ChkCom.IsChecked = com;
            ChkBranch.IsChecked = branch;
            ChkCat.IsChecked = cat;
            ChkDropdown.IsChecked = dropdown;
        }

        private void OptionCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            OptionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void DropdownCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            DropdownChanged?.Invoke(this, EventArgs.Empty);
        }

        private void PresetYpi_Click(object sender, RoutedEventArgs e)
        {
            ChkDate.IsChecked = true;
            ChkPc.IsChecked = true;
            ChkIp.IsChecked = true;
            ChkFa.IsChecked = true;
        }

        private void PresetMinimal_Click(object sender, RoutedEventArgs e)
        {
            ChkDate.IsChecked = false;
            ChkPc.IsChecked = true;
            ChkIp.IsChecked = false;
            ChkFa.IsChecked = false;
        }

        private void BtnDone_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}
