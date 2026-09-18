using System.Collections.ObjectModel;
using System.Windows;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Shared.Views
{
    /// <summary>
    /// Full editor for a page's Document Date filter rows, opened via the "More" link that
    /// appears once more than 3 rows exist in the Advanced Filters popup (which only has room
    /// to show the first 3 before it starts overlapping the rest of the page). Binds directly to
    /// the same ObservableCollection the inline popup uses, so edits/removals/additions here are
    /// reflected immediately back in the popup and the applied filter — no separate state to sync.
    /// </summary>
    public partial class DocumentDateOverflowWindow : Window
    {
        public ObservableCollection<DocumentDateFilterRow> Rows { get; }
        public RelayCommand AddRowCommand { get; }

        public DocumentDateOverflowWindow(ObservableCollection<DocumentDateFilterRow> rows, RelayCommand addRowCommand)
        {
            InitializeComponent();
            Rows = rows;
            AddRowCommand = addRowCommand;
            DataContext = this;
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
