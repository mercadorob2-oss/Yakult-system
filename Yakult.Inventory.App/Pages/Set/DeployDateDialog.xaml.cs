using System;
using System.Windows;
using Yakult.Inventory.App.Repositories;

using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Set
{
    /// <summary>
    /// Date picker for the Deploy button on ViewSetDetailPage. Today deploys
    /// normally; a past date backdates DispatchDate (and the dispatch audit
    /// entries) to that day. Future dates are rejected. Returns the chosen
    /// date via SelectedDate when the dialog closes with OK.
    /// </summary>
    public partial class DeployDateDialog : Window
    {
        private readonly int _setId;

        public DateTime SelectedDate { get; private set; } = DateTime.Today;

        public DeployDateDialog(int setId)
        {
            _setId = setId;
            InitializeComponent();
            DpDeployDate.SelectedDate = DateTime.Today;
            DpDeployDate.DisplayDateEnd = DateTime.Today;
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
                var setDto = await new SetRepository().GetSetByIdAsync(_setId);
                if (setDto == null)
                {
                    MessageBox.Show("Set not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    DialogResult = false;
                    return;
                }

                TxtSetCodeLabel.Text = $"{setDto.SetCode} — {setDto.ItemCount} item(s)";
                if (setDto.DispatchDate.HasValue)
                    DpDeployDate.SelectedDate = setDto.DispatchDate.Value.Date;
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

        private void BtnDeploy_Click(object sender, RoutedEventArgs e)
        {
            if (!DpDeployDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Please pick a deploy date.", "Deploy Date Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime picked = DpDeployDate.SelectedDate.Value.Date;
            if (picked > DateTime.Today)
            {
                MessageBox.Show("Deploy date cannot be in the future.", "Invalid Date",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedDate = picked;
            DialogResult = true;
        }
    }
}
