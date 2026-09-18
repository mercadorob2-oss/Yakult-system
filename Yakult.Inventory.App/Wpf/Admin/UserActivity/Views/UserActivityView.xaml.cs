using System;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Dialogs;
using Yakult.Inventory.App.Forms.Admin;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.WPF.Admin.UserActivity.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.UserActivity.Views
{
    public partial class UserActivityView : UserControl
    {
        private readonly UserActivityViewModel _vm;

        public UserActivityView()
        {
            InitializeComponent();
            _vm         = new UserActivityViewModel();
            DataContext = _vm;
            Loaded     += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await _vm.LoadFilterOptionsAsync();
            await LoadDataSafeAsync();
        }

        private async System.Threading.Tasks.Task LoadDataSafeAsync()
        {
            try { await _vm.LoadDataAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data:\n{ex.Message}", "Load Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await LoadDataSafeAsync();
        private async void BtnApply_Click(object sender, RoutedEventArgs e)   => await LoadDataSafeAsync();

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _vm.ClearFilters();
        }

        private void BtnInsights_Click(object sender, RoutedEventArgs e)
        {
            var form = new UserActivityInsightsForm(_vm.BuildFilter());
            form.Show(System.Windows.Forms.Form.ActiveForm);
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.CurrentData == null || _vm.CurrentData.Count == 0)
            {
                MessageBox.Show("There are no records to export. Apply a filter and load data first.",
                    "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var owner = System.Windows.Forms.Form.ActiveForm;

            var visibleColumns = ReportColumnSelectionDialog.ShowAndGetSelected(
                owner,
                "User Activity Report",
                ReportColumnSelectionDialog.UserActivityReportColumns());

            if (visibleColumns == null) return;

            using (var dlg = new System.Windows.Forms.SaveFileDialog
            {
                Title      = "Save User Activity Report",
                Filter     = "PDF files (*.pdf)|*.pdf",
                FileName   = $"UserActivity_{DateTime.Today:yyyyMMdd}.pdf",
                DefaultExt = "pdf"
            })
            {
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                try
                {
                    UserActivityPdfGenerator.Generate(
                        _vm.CurrentData,
                        _vm.BuildFilter(),
                        dlg.FileName,
                        visibleColumns);

                    ActivityLogger.LogAsync(
                        ActivityLogger.Actions.Export,
                        "UserActivityLog",
                        null,
                        $"Exported user activity report ({_vm.CurrentData.Count} records) to PDF");

                    var result = MessageBox.Show(
                        $"PDF saved to:\n{dlg.FileName}\n\nWould you like to open it now?",
                        "Export Successful",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName        = dlg.FileName,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"PDF generation failed:\n\n{ex.Message}",
                        "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CboItemsPerPage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;

            if (CboItemsPerPage.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Content?.ToString(), out int perPage))
            {
                _vm.ItemsPerPage = perPage;
            }
        }

        private void BtnFirst_Click(object sender, RoutedEventArgs e) { _vm.GoToFirstPage(); }
        private void BtnPrev_Click(object sender, RoutedEventArgs e)  { _vm.GoToPrevPage();  }
        private void BtnNext_Click(object sender, RoutedEventArgs e)  { _vm.GoToNextPage();  }
        private void BtnLast_Click(object sender, RoutedEventArgs e)  { _vm.GoToLastPage();  }
    }
}
