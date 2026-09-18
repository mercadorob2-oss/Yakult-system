using System;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Items.Views
{
    /// <summary>Step 2 popup for the Items Page's "Add to Contract/Subscription/License/
    /// Service Group" bulk actions — collects the group's Reference Code (label changes
    /// per Sub-Type) and Begin/End Date, which override the member items' own dates.</summary>
    public partial class AssignSubTypeGroupDialog : Window, IDisposable
    {
        public string SubType { get; }
        public string ReferenceCode { get; private set; }
        public DateTime? BeginDate { get; private set; }
        public DateTime? EndDate { get; private set; }

        public AssignSubTypeGroupDialog(string subType, int selectedCount)
        {
            InitializeComponent();

            SubType = subType;
            Title = $"Add to {subType} Group";
            TxtHeading.Text = Title;
            TxtSummary.Text = $"{selectedCount} item(s) selected.";
            LblReferenceCode.Text = Yakult.Inventory.App.Repositories.ItemSubTypeCatalog.GetReferenceCodeLabel(subType);

            DtpStart.SelectedDate = DateTime.Today;
            DtpEnd.SelectedDate = DateTime.Today.AddYears(1);
        }

        // WinForms-compatible ShowDialog overloads (same shape as SoftwareServiceSetDialog).
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

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            ReferenceCode = string.IsNullOrWhiteSpace(TxtReferenceCode.Text) ? null : TxtReferenceCode.Text.Trim();
            BeginDate = DtpStart.SelectedDate;
            EndDate = DtpEnd.SelectedDate;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
