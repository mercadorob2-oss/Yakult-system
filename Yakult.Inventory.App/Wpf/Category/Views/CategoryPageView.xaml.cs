using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Category;
using Yakult.Inventory.App.WPF.Category.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Category.Views
{
    public partial class CategoryPageView : UserControl
    {
        private readonly CategoryPageViewModel _vm;

        public CategoryPageView()
        {
            InitializeComponent();

            _vm = new CategoryPageViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.ConfirmYesNo = (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Question) == WinForms.DialogResult.Yes;
            _vm.RequestAddCategory += OnRequestAddCategory;
            _vm.RequestEditCategory += OnRequestEditCategory;
            _vm.RebuildSortByOptionsRequested += RebuildSortByOptions;

            RebuildSortByOptions();
            _ = _vm.LoadCategoriesAsync();
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestAddCategory()
        {
            using (var dlg = new AddCategoryDialog())
            {
                if (dlg.ShowDialog(GetOwner()) == WinForms.DialogResult.OK)
                {
                    if (string.IsNullOrWhiteSpace(dlg.CategoryName))
                    {
                        WinForms.MessageBox.Show(GetOwner(), "Category name is required.", "Validation", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                        return;
                    }
                    _ = _vm.CreateCategoryAsync(dlg.CategoryName.Trim());
                }
            }
        }

        private async void OnRequestEditCategory(ItemCategoryDto category)
        {
            var (activeItems, inactiveItems) = await _vm.LoadCategoryItemsAsync(category.CategoryId);

            using (var dlg = new EditCategoryDialog(category, activeItems, inactiveItems))
            {
                if (dlg.ShowDialog() == WinForms.DialogResult.OK)
                {
                    if (string.IsNullOrWhiteSpace(dlg.CategoryName))
                    {
                        WinForms.MessageBox.Show(GetOwner(), "Category name is required.", "Validation", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                        return;
                    }
                    category.Name = dlg.CategoryName.Trim();
                    _ = _vm.UpdateCategoryAsync(category);
                }
            }
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool newState = ((CheckBox)sender).IsChecked == true;
            _vm.SetPageSelected(newState);
            CategoriesGrid.Items.Refresh();
        }

        private void RowSelectCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (((CheckBox)sender).DataContext is ItemCategoryDto category)
                _vm.SetCategorySelected(category.CategoryId, ((CheckBox)sender).IsChecked == true);
        }

        private void SelectedBadge_Click(object sender, RoutedEventArgs e)
        {
            var summaries = _vm.GetSelectedCategories().Select(c => new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowSummary
            {
                Id = c.CategoryId,
                Values = new[] { c.CategoryId.ToString(), c.Name, c.Active ? "Active" : "Inactive", c.ItemCount.ToString() }
            });

            var dialog = new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowsReviewDialog(
                "Selected Categories", "categories",
                new[] { "ID", "Name", "Status", "Items" },
                summaries);
            SetWpfOwner(dialog);
            dialog.ShowDialog();

            foreach (var categoryId in dialog.RemovedIds)
                _vm.SetCategorySelected(categoryId, false);

            CategoriesGrid.Items.Refresh();
        }

        private static void SetWpfOwner(Window window)
        {
            var owner = GetOwner();
            if (owner != null) new System.Windows.Interop.WindowInteropHelper(window).Owner = owner.Handle;
        }

        private void CategoriesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is CheckBox) return; // double-click on the checkbox column is not an "edit" gesture
                source = VisualTreeHelper.GetParent(source);
            }

            if (_vm.EditCommand.CanExecute(null))
                _vm.EditCommand.Execute(null);
        }

        private void RebuildSortByOptions()
        {
            var options = ListSortHelper.BuildOptions(CategoriesGrid);
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(CategoriesGrid);
            _vm.SetSortByOptions(options, defaultKey);
        }

        private void CategoriesGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            var direction = e.Column.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            _vm.SortByColumn(key, direction);

            foreach (var col in CategoriesGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = direction;
        }
    }

    /// <summary>Page-local: "Status" column display text has no backing DTO property in the
    /// original (it was CellFormatting-only), so it stays a converter here rather than a real
    /// bindable property — see the SortMemberPath note on the Status column in the XAML.</summary>
    public sealed class ActiveToStatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? "Active" : "Inactive";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
