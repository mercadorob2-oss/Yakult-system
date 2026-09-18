using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.WPF.Archive.ViewModels;

namespace Yakult.Inventory.App.WPF.Archive.Views
{
    public partial class ArchiveDetailsWindow : Window
    {
        private readonly ArchiveDetailsViewModel _vm;

        public ArchiveDetailsWindow(int archiveId, string entityType, int entityId)
        {
            InitializeComponent();

            _vm = new ArchiveDetailsViewModel(archiveId, entityType, entityId,
                DatabaseConfig.ConnectionString);

            DataContext = _vm;

            _vm.CloseRequested += OnCloseRequested;
            CloseBtn.Click      += (s, e) => Close();

            Loaded += async (s, e) => await _vm.LoadAsync();
        }

        private void OnCloseRequested(bool restored)
        {
            DialogResult = restored ? true : (bool?)null;
            Close();
        }
    }
}
