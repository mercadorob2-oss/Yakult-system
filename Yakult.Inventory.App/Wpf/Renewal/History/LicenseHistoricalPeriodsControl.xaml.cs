using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.WPF.Renewal.History
{
    public partial class LicenseHistoricalPeriodsControl : UserControl
    {
        private readonly LicenseHistoricalPeriodRepository _repository = new LicenseHistoricalPeriodRepository();
        private LicenseHistoricalPeriodContext _context;
        private int _setId;
        private int _generation;
        private bool _opening;

        public ObservableCollection<LicenseHistoricalPeriodDto> Periods { get; } = new ObservableCollection<LicenseHistoricalPeriodDto>();

        public LicenseHistoricalPeriodsControl() { InitializeComponent(); }

        public async Task LoadAsync(int setId)
        {
            int generation = ++_generation;
            _setId = setId;
            _context = null;
            Periods.Clear();
            Visibility = setId > 0 ? Visibility.Visible : Visibility.Collapsed;
            AddPeriodButton.IsEnabled = false;
            if (setId <= 0) return;
            RefreshButton.IsEnabled = false;
            HistoryStatus.Text = "Loading previous renewals…";
            try
            {
                var context = await Task.Run(() => _repository.GetContext(setId));
                if (generation != _generation) return;
                _context = context;
                foreach (var period in context.Periods) Periods.Add(period);
                HistoryStatus.Text = context.FirstRecordedStartDate.HasValue
                    ? $"Past periods must end before {context.FirstRecordedStartDate:MMM d, yyyy}." +
                      (Periods.Count == 0 ? " No past periods have been entered yet." : "")
                    : "Set a coverage start date on the recorded invoice before adding previous renewals.";
                AddPeriodButton.IsEnabled = context.FirstRecordedStartDate.HasValue;
            }
            catch (Exception ex)
            {
                if (generation == _generation) HistoryStatus.Text = "Unable to load previous renewals: " + ex.Message;
                System.Diagnostics.Debug.WriteLine("[LicenseHistoricalPeriods] " + ex);
            }
            finally
            {
                if (generation == _generation) RefreshButton.IsEnabled = true;
            }
        }

        public async Task OpenAddPeriodAsync()
        {
            if (_opening || _setId <= 0) return;
            _opening = true;
            try
            {
                await LoadAsync(_setId);
                if (_context == null || !_context.FirstRecordedStartDate.HasValue) return;
                var dialog = new AddLicenseHistoricalPeriodWindow(_setId, _context) { Owner = Window.GetWindow(this) };
                if (dialog.ShowDialog() == true) await LoadAsync(_setId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this), ex.Message, "Previous Renewals", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { _opening = false; }
        }

        private async void OnAddPeriod(object sender, RoutedEventArgs e) => await OpenAddPeriodAsync();
        private async void OnRefresh(object sender, RoutedEventArgs e) => await LoadAsync(_setId);
    }
}
