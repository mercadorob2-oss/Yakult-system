using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.WPF.RequestPortal.NewRequest.ViewModels;

namespace Yakult.Inventory.App.WPF.RequestPortal.NewRequest.Views
{
    public partial class NewRequestView : UserControl
    {
        public NewRequestViewModel ViewModel { get; }

        public NewRequestView(NewRequestViewModel viewModel)
        {
            InitializeComponent();
            ViewModel   = viewModel;
            DataContext = viewModel;
            Loaded += async (s, e) => await viewModel.LoadDataAsync();
        }

        public NewRequestView() : this(new NewRequestViewModel()) { }

        // ── Tour targets (x:Name elements exposed for WpfPortalTourService) ──
        public FrameworkElement TourTarget_RequestDetailsCard   => RequestDetailsCard;
        public FrameworkElement TourTarget_RequesterIdentityPanel =>
            ViewModel.IsDeptAccountMode ? (FrameworkElement)DeptEmployeeSelectorPanel : RequesterIdentityPanel;
        public FrameworkElement TourTarget_CartridgeSelectionCard   => CartridgeSelectionCard;
        public FrameworkElement TourTarget_CartridgeSelectionHeader => CartridgeSelectionHeader;
        public FrameworkElement TourTarget_CartridgeModelCombo      => CartridgeModelCombo;
        public FrameworkElement TourTarget_QtySpinner               => QtySpinnerBorder;
        public FrameworkElement TourTarget_ReturnedEmptyLabel       => ReturnedEmptyLabel;
        public FrameworkElement TourTarget_ReturnedEmptyRow         => ReturnedEmptyRow;
        public FrameworkElement TourTarget_AddToRequest             => AddToRequestButton;
        public FrameworkElement TourTarget_RequestItemsCard         => RequestItemsCard;
        public FrameworkElement TourTarget_RequestItemsHeader       => RequestItemsHeader;
        public FrameworkElement TourTarget_FulfillmentCard          => FulfillmentCard;
        public FrameworkElement TourTarget_FulfillmentHeader        => FulfillmentHeader;
        public FrameworkElement TourTarget_ActionBar                => ActionBar;
    }
}
