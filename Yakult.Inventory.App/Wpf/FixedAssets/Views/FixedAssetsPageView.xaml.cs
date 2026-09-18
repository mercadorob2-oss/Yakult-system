using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.WPF.FixedAssets.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.FixedAssets.Views
{
    public partial class FixedAssetsPageView : UserControl
    {
        private readonly FixedAssetsPageViewModel _vm;

        public FixedAssetsPageView()
        {
            InitializeComponent();

            _vm = new FixedAssetsPageViewModel();
            DataContext = _vm;

            _vm.PropertyChanged += Vm_PropertyChanged;
            _vm.ErrorOccurred += OnErrorOccurred;

            ConfigureColumnsForFilter(_vm.SelectedFilter);
            RebuildSortByOptions();
            _vm.LoadData();
        }

        public void ApplyInitialSearch(string query) => _vm.ApplyInitialSearch(query);

        private void Vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FixedAssetsPageViewModel.SelectedFilter))
            {
                ConfigureColumnsForFilter(_vm.SelectedFilter);
                RebuildSortByOptions();
            }
        }

        private void OnErrorOccurred(string message)
        {
            var owner = WinForms.Form.ActiveForm;
            WinForms.MessageBox.Show(owner, $"Error loading data: {message}", "Error",
                WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
        }

        // ── Column layout per Filter selection (Items/Requests/Sets/Invoices) ──
        // Presentation-only: mirrors ConfigureColumnsForFilter/ConfigureXxxColumns from the
        // original WinForms page. Column *shape* differs per filter because each SQL query
        // projects a different anonymous type — this is UI structure, not business logic.

        private void ConfigureColumnsForFilter(string filter)
        {
            FixedAssetsGrid.Columns.Clear();

            switch (filter)
            {
                case "Items":
                    AddColumn("ItemId", "ID", 55, centered: true);
                    AddColumn("Name", "Name", 190);
                    AddColumn("Category", "Category", 130);
                    AddColumn("ItemType", "Type", 100);
                    AddColumn("ModelNumber", "Model #", 130);
                    AddColumn("SerialNumber", "Serial #", 130);
                    AddDateColumn("DateCreated", "Date Created", 110, centered: true);
                    break;
                case "Requests":
                    AddColumn("ReqId", "Req ID", 70, centered: true);
                    AddColumn("ItemName", "Item", 190);
                    AddColumn("Category", "Category", 130);
                    AddColumn("EmployeeName", "Employee", 150);
                    AddColumn("Status", "Status", 110);
                    AddDateColumn("DateRequested", "Date Requested", 120, centered: true);
                    break;
                case "Sets":
                    AddColumn("SetId", "Set ID", 70, centered: true);
                    AddColumn("SetCode", "Set Code", 130);
                    AddColumn("SetType", "Type", 110);
                    AddColumn("CreatedByName", "Created By", 150);
                    AddDateColumn("CreatedAt", "Created At", 120, centered: true);
                    break;
                case "Invoices":
                    AddColumn("SetId", "Invoice ID", 80, centered: true);
                    AddColumn("SetCode", "Invoice Code", 130);
                    AddColumn("DocumentNumber", "Document #", 130);
                    AddNumericColumn("TotalAmountDue", "Amount", 110, "N2");
                    AddDateColumn("CreatedAt", "Date", 110, centered: true);
                    break;
            }
        }

        private void AddColumn(string binding, string header, double minWidth, bool centered = false)
        {
            var col = new DataGridTextColumn
            {
                Header = header,
                Binding = new System.Windows.Data.Binding(binding),
                SortMemberPath = binding,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = minWidth
            };
            col.ElementStyle = centered ? (Style)Resources["CenteredCellText"] : WrapStyle();
            FixedAssetsGrid.Columns.Add(col);
        }

        private void AddDateColumn(string binding, string header, double minWidth, bool centered = false)
        {
            var col = new DataGridTextColumn
            {
                Header = header,
                Binding = new System.Windows.Data.Binding(binding) { StringFormat = "MM/dd/yyyy" },
                SortMemberPath = binding,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = minWidth
            };
            if (centered)
                col.ElementStyle = (Style)Resources["CenteredCellText"];
            FixedAssetsGrid.Columns.Add(col);
        }

        private void AddNumericColumn(string binding, string header, double minWidth, string format)
        {
            var col = new DataGridTextColumn
            {
                Header = header,
                Binding = new System.Windows.Data.Binding(binding) { StringFormat = format },
                SortMemberPath = binding,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = minWidth,
                ElementStyle = (Style)Resources["CenteredCellText"]
            };
            FixedAssetsGrid.Columns.Add(col);
        }

        private static Style WrapStyle()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            return style;
        }

        private void RebuildSortByOptions()
        {
            var options = ListSortHelper.BuildOptions(FixedAssetsGrid);
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(FixedAssetsGrid);
            _vm.SetSortByOptions(options, defaultKey);
        }

        private void FixedAssetsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            _vm.SortByColumn(key);

            foreach (var col in FixedAssetsGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = _vm.CurrentSortDirection == ListSortDirection.Descending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }
    }
}
