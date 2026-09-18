using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Yakult.Inventory.App.Models.BorrowItems;
using Yakult.Inventory.App.Pages.Item;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Wpf.BorrowItems
{
    /// <summary>
    /// Third way to pick an item for a borrow transaction, alongside scanning its QR code or
    /// typing its serial number: browse a searchable list of items flagged IsBorrowable and pick
    /// one directly. Styled to match the WPF Borrow Items scan panel (WpfBorrowTransactionWorkspace).
    /// </summary>
    public sealed class WpfBorrowableItemsPickerDialog : Window
    {
        private static readonly Color PageBack = Color.FromRgb(242, 245, 249);
        private static readonly Color CardBack = Colors.White;
        private static readonly Color BorderSoft = Color.FromRgb(226, 232, 240);
        private static readonly Color TextHeader = Color.FromRgb(15, 23, 42);
        private static readonly Color TextMuted = Color.FromRgb(100, 116, 139);
        private static readonly Color AccentBlue = Color.FromRgb(59, 130, 246);
        private static readonly Color AccentBlueDark = Color.FromRgb(37, 99, 235);
        private static readonly Color RowHover = Color.FromRgb(241, 245, 249);
        private static readonly Color RowSelected = Color.FromRgb(219, 234, 254);

        private readonly BorrowItemsRepository _repo;
        private TextBox _searchInput;
        private DataGrid _grid;
        private TextBlock _statusText;
        private Button _btnSelect;
        private DispatcherTimer _searchDebounce;

        public BorrowItemLookup SelectedItem { get; private set; }

        public WpfBorrowableItemsPickerDialog(BorrowItemsRepository repo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            Title = "Select a Borrowable Item";
            Width = 700;
            Height = 560;
            MinWidth = 560;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Background = new SolidColorBrush(PageBack);
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            ShowInTaskbar = false;

            Content = BuildRoot();
            Loaded += async (s, e) => await LoadAsync(string.Empty);
        }

        private UIElement BuildRoot()
        {
            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // search
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid card
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

            var searchSection = BuildSearchSection();
            Grid.SetRow(searchSection, 0);
            root.Children.Add(searchSection);

            var gridCard = BuildGridCard();
            Grid.SetRow(gridCard, 1);
            root.Children.Add(gridCard);

            var footer = BuildFooter();
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            return root;
        }

        private UIElement BuildSearchSection()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

            var titleRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleRow.Children.Add(new TextBlock
            {
                Text = "Search",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105),
                VerticalAlignment = VerticalAlignment.Center
            });
            var hint = new TextBlock
            {
                Text = "By name, serial number, or model",
                FontSize = 10.5, Foreground = new SolidColorBrush(TextMuted),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(hint, 1);
            titleRow.Children.Add(hint);
            stack.Children.Add(titleRow);

            var inputBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 9, 8, 9),
                Effect = new DropShadowEffect { BlurRadius = 12, Color = Color.FromArgb(25, 59, 130, 246), ShadowDepth = 0 }
            };
            inputBorder.GotKeyboardFocus += (s, e) =>
            {
                inputBorder.BorderBrush = new SolidColorBrush(AccentBlue);
                inputBorder.Effect = new DropShadowEffect { BlurRadius = 16, Color = Color.FromArgb(45, 59, 130, 246), ShadowDepth = 0 };
            };
            inputBorder.LostKeyboardFocus += (s, e) =>
            {
                inputBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254));
                inputBorder.Effect = new DropShadowEffect { BlurRadius = 12, Color = Color.FromArgb(25, 59, 130, 246), ShadowDepth = 0 };
            };

            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var searchIcon = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M8,2 A6,6 0 1 0 8,14 A6,6 0 1 0 8,2 Z M12.5,12.5 L17,17"),
                Stroke = new SolidColorBrush(AccentBlue),
                StrokeThickness = 1.6,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Width = 18, Height = 18,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(searchIcon, 0);
            inputGrid.Children.Add(searchIcon);

            _searchInput = new TextBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                FontSize = 14,
                Foreground = new SolidColorBrush(TextHeader),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _searchInput.TextChanged += (s, e) => DebounceSearch();
            _searchInput.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    _searchDebounce?.Stop();
                    await LoadAsync(_searchInput.Text);
                }
            };
            Grid.SetColumn(_searchInput, 1);
            inputGrid.Children.Add(_searchInput);

            var searchBtn = new Button
            {
                Content = "Search", Height = 30, Width = 84,
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White, BorderThickness = new Thickness(0),
                Background = new LinearGradientBrush(AccentBlue, AccentBlueDark, 90),
                Cursor = Cursors.Hand
            };
            searchBtn.Click += async (s, e) => await LoadAsync(_searchInput.Text);
            Grid.SetColumn(searchBtn, 2);
            inputGrid.Children.Add(searchBtn);

            inputBorder.Child = inputGrid;
            stack.Children.Add(inputBorder);

            return stack;
        }

        private UIElement BuildGridCard()
        {
            var card = new Border
            {
                Background = new SolidColorBrush(CardBack),
                BorderBrush = new SolidColorBrush(BorderSoft),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(0),
                Effect = new DropShadowEffect { BlurRadius = 10, Color = Color.FromArgb(15, 15, 23, 42), ShadowDepth = 0 }
            };

            var cardGrid = new Grid();
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            _statusText = new TextBlock
            {
                Text = "Loading...",
                FontSize = 12,
                Foreground = new SolidColorBrush(TextMuted),
                Margin = new Thickness(16, 12, 16, 8)
            };
            Grid.SetRow(_statusText, 0);
            cardGrid.Children.Add(_statusText);

            _grid = BuildGrid();
            Grid.SetRow(_grid, 1);
            cardGrid.Children.Add(_grid);

            card.Child = cardGrid;
            return card;
        }

        private DataGrid BuildGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserReorderColumns = false,
                CanUserResizeRows = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush(BorderSoft),
                RowBackground = Brushes.White,
                AlternatingRowBackground = Brushes.White,
                BorderThickness = new Thickness(0),
                Background = Brushes.White,
                Margin = new Thickness(0, 0, 0, 0),
                FontSize = 13,
                RowHeight = 40,
                ColumnHeaderHeight = 34,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, BrushFromRgb(248, 250, 252)));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(TextMuted)));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 0, 0, 0)));
            headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(BorderSoft)));
            headerStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty, 34.0));
            grid.ColumnHeaderStyle = headerStyle;

            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 0, 0, 0)));
            cellStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            var focusTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
            focusTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            focusTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(TextHeader)));
            cellStyle.Triggers.Add(focusTrigger);
            grid.CellStyle = cellStyle;

            var rowStyle = new Style(typeof(DataGridRow));
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(RowHover)));
            var selectedTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(RowSelected)));
            rowStyle.Triggers.Add(hoverTrigger);
            rowStyle.Triggers.Add(selectedTrigger);
            grid.RowStyle = rowStyle;

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new System.Windows.Data.Binding("ItemName"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Serial No.",
                Binding = new System.Windows.Data.Binding("SerialNumber"),
                Width = new DataGridLength(1.2, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Model",
                Binding = new System.Windows.Data.Binding("ModelNumber"),
                Width = new DataGridLength(1.2, DataGridLengthUnitType.Star)
            });

            grid.SelectionChanged += (s, e) => UpdateButtons();
            grid.MouseDoubleClick += (s, e) => { if (grid.SelectedItem != null) AcceptSelection(); };

            return grid;
        }

        private UIElement BuildFooter()
        {
            var grid = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var addNewBtn = new Button
            {
                Content = "Add New Item", Height = 36, Padding = new Thickness(16, 0, 16, 0),
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(AccentBlue), BorderThickness = new Thickness(1.5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)), Background = Brushes.White,
                Cursor = Cursors.Hand
            };
            addNewBtn.Click += async (s, e) => await AddNewItemAsync();
            Grid.SetColumn(addNewBtn, 0);
            grid.Children.Add(addNewBtn);

            var rightFlow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var cancelBtn = new Button
            {
                Content = "Cancel", Height = 36, Padding = new Thickness(16, 0, 16, 0),
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(TextMuted), BorderThickness = new Thickness(1.5),
                BorderBrush = new SolidColorBrush(BorderSoft), Background = Brushes.White,
                Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0)
            };
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };

            _btnSelect = new Button
            {
                Content = "Select", Height = 36, Padding = new Thickness(20, 0, 20, 0),
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White, BorderThickness = new Thickness(0),
                Background = new LinearGradientBrush(AccentBlue, AccentBlueDark, 90),
                Cursor = Cursors.Hand,
                IsEnabled = false
            };
            _btnSelect.Click += (s, e) => AcceptSelection();

            rightFlow.Children.Add(cancelBtn);
            rightFlow.Children.Add(_btnSelect);
            Grid.SetColumn(rightFlow, 2);
            grid.Children.Add(rightFlow);

            return grid;
        }

        private void DebounceSearch()
        {
            if (_searchDebounce == null)
            {
                _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                _searchDebounce.Tick += async (s, e) =>
                {
                    _searchDebounce.Stop();
                    await LoadAsync(_searchInput.Text);
                };
            }

            _searchDebounce.Stop();
            _searchDebounce.Start();
        }

        private async Task LoadAsync(string searchText)
        {
            _statusText.Text = "Loading...";
            _grid.ItemsSource = null;

            try
            {
                var items = await _repo.GetBorrowableItemsAsync(searchText);
                _grid.ItemsSource = items;
                _statusText.Text = items.Count == 0
                    ? "No borrowable items found. Add one below, or mark existing items as Borrowable on the Items page."
                    : $"{items.Count} item(s) available.";
            }
            catch (Exception ex)
            {
                _statusText.Text = "Failed to load borrowable items.";
                System.Diagnostics.Debug.WriteLine($"[WpfBorrowableItemsPickerDialog] Load failed: {ex}");
            }

            UpdateButtons();
        }

        private async Task AddNewItemAsync()
        {
            var dialog = new BatchAddItemDialog(presetBorrowable: true) { Owner = this };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                await LoadAsync(_searchInput.Text);
        }

        private void UpdateButtons()
        {
            _btnSelect.IsEnabled = _grid.SelectedItem != null;
        }

        private void AcceptSelection()
        {
            SelectedItem = _grid.SelectedItem as BorrowItemLookup;
            if (SelectedItem == null)
                return;

            DialogResult = true;
            Close();
        }

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}
