using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.WPF.Admin.DepartmentRequestHistory.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.DepartmentRequestHistory.Views
{
    public partial class DepartmentRequestHistoryView : UserControl
    {
        public DepartmentRequestHistoryViewModel ViewModel { get; }

        /// <summary>Admin Portal page: all departments, every filter open.</summary>
        public DepartmentRequestHistoryView() : this(new DepartmentRequestHistoryViewModel()) { }

        /// <summary>Opened from a Department Accounts row: fixed to that department.</summary>
        public DepartmentRequestHistoryView(DepartmentRequestHistoryViewModel vm)
        {
            InitializeComponent();
            ViewModel   = vm;
            DataContext = vm;
            Loaded     += OnLoaded;
        }

        private bool _loadedOnce;

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_loadedOnce) return;   // ElementHost can raise Loaded again on re-parent
            _loadedOnce = true;
            await ViewModel.LoadAsync();
        }
    }
}
