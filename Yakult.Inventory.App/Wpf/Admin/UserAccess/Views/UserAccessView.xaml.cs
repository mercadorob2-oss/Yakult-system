using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Yakult.Inventory.App.WPF.Admin.UserAccess.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.UserAccess.Views
{
    /// <summary>
    /// Admin Portal > User Access (WPF). The Portals / Actions grids have one column per
    /// portal / action, which only come from the database, so their DataGrid columns are
    /// built here whenever the grid's view model is (re)configured.
    /// </summary>
    public partial class UserAccessView : UserControl
    {
        private static readonly Brush BaselineCellBrush = new SolidColorBrush(Color.FromRgb(0xDB, 0xEA, 0xFE));

        private readonly UserAccessViewModel _vm;

        public UserAccessView()
        {
            InitializeComponent();
            _vm = new UserAccessViewModel();
            DataContext = _vm;
            Loaded += OnLoaded;
        }

        private bool _loadedOnce;

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_loadedOnce) return;   // the host can fire Loaded again when re-parented
            _loadedOnce = true;
            await _vm.LoadAllAsync();
        }

        // ── Portals / Actions grids ──────────────────────────────────────────

        /// <summary>
        /// The grid lives in a DataTemplate, so it is recreated every time its tab is shown.
        /// Build its columns now, and rebuild them if the view model is reconfigured while it
        /// is on screen (initial load finishing after the tab is already visible).
        /// </summary>
        private void PermissionGrid_Loaded(object sender, RoutedEventArgs e)
        {
            var grid = (DataGrid)sender;
            if (!(grid.DataContext is PermissionGridViewModel gridVm)) return;

            EventHandler rebuild = (s, args) => BuildColumns(grid, gridVm);
            gridVm.ColumnsChanged += rebuild;

            RoutedEventHandler unloaded = null;
            unloaded = (s, args) =>
            {
                gridVm.ColumnsChanged -= rebuild;
                grid.Unloaded -= unloaded;
            };
            grid.Unloaded += unloaded;

            BuildColumns(grid, gridVm);
        }

        private static void BuildColumns(DataGrid grid, PermissionGridViewModel gridVm)
        {
            grid.Columns.Clear();

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "User",
                Binding = new Binding(nameof(GridRowViewModel.Name)),
                Width = new DataGridLength(200),
                IsReadOnly = true,
                HeaderStyle = LeftHeaderStyle(grid),
                ElementStyle = UserCellStyle()
            });

            for (int i = 0; i < gridVm.Columns.Count; i++)
            {
                grid.Columns.Add(new DataGridTemplateColumn
                {
                    Header = new TextBlock
                    {
                        Text = gridVm.Columns[i].DisplayName,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        Foreground = Brushes.White
                    },
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                    MinWidth = 90,
                    CellTemplate = CheckCellTemplate(i)
                });
            }
        }

        private static Style LeftHeaderStyle(DataGrid grid)
        {
            var baseStyle = grid.TryFindResource(typeof(DataGridColumnHeader)) as Style;
            var style = new Style(typeof(DataGridColumnHeader), baseStyle);
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 8, 10, 8)));
            return style;
        }

        private static Style UserCellStyle()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(14, 0, 8, 0)));
            style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            return style;
        }

        /// <summary>
        /// Cell = CheckBox bound to Cells[i].IsChecked, enabled only while the grid is in Edit mode,
        /// on a light-blue background when the access comes from the user's role.
        /// </summary>
        private static DataTemplate CheckCellTemplate(int index)
        {
            string cellPath = $"Cells[{index}]";

            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);

            var borderStyle = new Style(typeof(Border));
            var baselineTrigger = new DataTrigger { Binding = new Binding($"{cellPath}.IsBaseline"), Value = true };
            baselineTrigger.Setters.Add(new Setter(Border.BackgroundProperty, BaselineCellBrush));
            baselineTrigger.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,
                "Access granted by this user's role assignment. Uncheck to override."));
            borderStyle.Triggers.Add(baselineTrigger);
            border.SetValue(FrameworkElement.StyleProperty, borderStyle);

            var check = new FrameworkElementFactory(typeof(CheckBox));
            check.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            check.SetBinding(CheckBox.IsCheckedProperty, new Binding($"{cellPath}.IsChecked")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            check.SetBinding(UIElement.IsEnabledProperty, new Binding("DataContext.IsEditing")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1)
            });

            border.AppendChild(check);
            return new DataTemplate { VisualTree = border };
        }

        private static PermissionGridViewModel GridVmOf(object sender)
            => (sender as FrameworkElement)?.DataContext as PermissionGridViewModel;

        private void GridEdit_Click(object sender, RoutedEventArgs e)   => GridVmOf(sender)?.BeginEdit();
        private void GridCancel_Click(object sender, RoutedEventArgs e) => GridVmOf(sender)?.CancelEdit();
        private void GridFirst_Click(object sender, RoutedEventArgs e)  => GridVmOf(sender)?.GoToFirstPage();
        private void GridPrev_Click(object sender, RoutedEventArgs e)   => GridVmOf(sender)?.GoToPrevPage();
        private void GridNext_Click(object sender, RoutedEventArgs e)   => GridVmOf(sender)?.GoToNextPage();
        private void GridLast_Click(object sender, RoutedEventArgs e)   => GridVmOf(sender)?.GoToLastPage();

        private async void GridSave_Click(object sender, RoutedEventArgs e)
        {
            var gridVm = GridVmOf(sender);
            if (gridVm == null || gridVm.IsSaving) return;
            await gridVm.SaveAsync();
        }

        // ── Pages / Role Pages ───────────────────────────────────────────────

        private static PageAccessEditorViewModel PageVmOf(object sender)
            => (sender as FrameworkElement)?.DataContext as PageAccessEditorViewModel;

        private void PortalPill_Click(object sender, RoutedEventArgs e)
        {
            // The pill's DataContext is the PortalTabViewModel; walk up to the editor's.
            var pill = (FrameworkElement)sender;
            var editor = FindEditor(pill);
            if (editor != null && pill.Tag is string key)
                editor.SelectPortal(key);
        }

        private static PageAccessEditorViewModel FindEditor(DependencyObject element)
        {
            while (element != null)
            {
                if (element is FrameworkElement fe && fe.DataContext is PageAccessEditorViewModel vm)
                    return vm;
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        private async void PageSave_Click(object sender, RoutedEventArgs e)
        {
            var editor = PageVmOf(sender);
            if (editor == null || editor.IsBusy) return;
            await editor.SaveAsync();
        }

        private async void PageRestore_Click(object sender, RoutedEventArgs e)
        {
            var editor = PageVmOf(sender);
            if (editor == null || editor.IsBusy) return;
            await editor.RestoreDefaultsAsync();
        }

        // ── Item Categories ──────────────────────────────────────────────────

        private async void CategoryAdd_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Categories.IsBusy) return;
            await _vm.Categories.AddAsync();
        }

        private async void CategoryRemove_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is string category)
                await _vm.Categories.RemoveAsync(category);
        }
    }
}
