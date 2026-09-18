using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Yakult.Inventory.App.WPF.Shared.Controls
{
    /// <summary>One row's worth of display data for SelectedRowsReviewDialog. <see cref="Values"/>
    /// must have exactly as many entries, in the same order, as the column headers passed to the
    /// dialog's constructor.</summary>
    public class SelectedRowSummary
    {
        public int Id { get; set; }
        public string[] Values { get; set; }
    }

    /// <summary>
    /// Generic "review your selection" dialog — any list page passes its own column headers and
    /// row values, so one dialog implementation serves every page (Employee, Vendor, Category,
    /// Company, Branch, Department, Fixed Assets, Consumable Models, Assets, ...) instead of a
    /// bespoke dialog per page (see the now-superseded SelectedEmployeesReviewDialog).
    /// </summary>
    public partial class SelectedRowsReviewDialog : Window
    {
        public List<int> RemovedIds { get; } = new List<int>();

        private readonly ObservableCollection<SelectedRowSummary> _rows;
        private readonly string _noun;

        /// <param name="title">Header title and window title, e.g. "Selected Vendors".</param>
        /// <param name="noun">Plural noun for the subtitle, e.g. "vendors".</param>
        /// <param name="columnHeaders">Column headers, in the same order as each row's Values.</param>
        /// <param name="rows">The currently-selected rows to display.</param>
        public SelectedRowsReviewDialog(string title, string noun, string[] columnHeaders, IEnumerable<SelectedRowSummary> rows)
        {
            InitializeComponent();

            Title = title;
            TxtTitle.Text = title;
            _noun = string.IsNullOrWhiteSpace(noun) ? "rows" : noun;

            BuildColumns(columnHeaders ?? new string[0]);

            _rows = new ObservableCollection<SelectedRowSummary>(rows ?? Enumerable.Empty<SelectedRowSummary>());
            GridSelected.ItemsSource = _rows;
            UpdateSubtitle();
        }

        private void BuildColumns(string[] columnHeaders)
        {
            for (int i = 0; i < columnHeaders.Length; i++)
            {
                GridSelected.Columns.Add(new DataGridTextColumn
                {
                    Header = columnHeaders[i],
                    Binding = new Binding($"Values[{i}]"),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                });
            }

            var removeColumn = new DataGridTemplateColumn { Header = "", Width = 44, CanUserSort = false };
            var factory = new System.Windows.FrameworkElementFactory(typeof(Button));
            factory.SetValue(Button.StyleProperty, (Style)FindResource("RemoveRowBtn"));
            factory.SetValue(Button.ContentProperty, "✕");
            factory.SetValue(ToolTipService.ToolTipProperty, "Remove from selection");
            factory.SetBinding(Button.TagProperty, new Binding("Id"));
            factory.AddHandler(Button.ClickEvent, (RoutedEventHandler)BtnRemoveRow_Click);
            removeColumn.CellTemplate = new DataTemplate { VisualTree = factory };
            GridSelected.Columns.Add(removeColumn);
        }

        private void UpdateSubtitle()
        {
            TxtSubtitle.Text = _rows.Count > 0
                ? $"{_rows.Count} {_noun} currently selected"
                : $"No {_noun} selected";
        }

        private void BtnRemoveRow_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is int id)) return;

            var row = _rows.FirstOrDefault(x => x.Id == id);
            if (row == null) return;

            _rows.Remove(row);
            RemovedIds.Add(id);
            UpdateSubtitle();
        }

        private void BtnRemoveAll_Click(object sender, RoutedEventArgs e)
        {
            RemovedIds.AddRange(_rows.Select(x => x.Id));
            _rows.Clear();
            UpdateSubtitle();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
