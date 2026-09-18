using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.WPF.RequestPortal.RequestHistory.ViewModels;

namespace Yakult.Inventory.App.WPF.RequestPortal.RequestHistory.Views
{
    public partial class RequestHistoryView : UserControl
    {
        public RequestHistoryViewModel ViewModel { get; }

        public event Action<List<PortalRequestStatusDto>> ShowDetailRequested;

        public RequestHistoryView(RequestHistoryViewModel vm)
        {
            InitializeComponent();
            ViewModel   = vm;
            DataContext = vm;
            Loaded     += async (s, e) => await vm.LoadAsync();
            vm.ShowDetailRequested += items => ShowDetailRequested?.Invoke(items);
        }

        public RequestHistoryView() : this(new RequestHistoryViewModel()) { }

        private void HistoryGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel.SelectedRow != null)
                ViewModel.ShowDetailCommand.Execute(ViewModel.SelectedRow);
        }

        // ── Tour targets ──────────────────────────────────────────────────────
        public FrameworkElement TourTarget_HistoryHeaderBar    => HistoryHeaderBar;
        public FrameworkElement TourTarget_HistoryGrid         => HistoryGrid;
        public FrameworkElement TourTarget_HistoryColumnHeaders => HistoryColumnHeaderZone;
        public FrameworkElement TourTarget_HistoryDemoRows     => HistoryDemoRowsZone;
        public FrameworkElement TourTarget_HistoryStatusColumn => HistoryStatusZone;
        public FrameworkElement TourTarget_PaginationBar       => PaginationBar;
    }
}
