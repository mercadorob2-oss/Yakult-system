using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models.BorrowItems;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Category;
using Yakult.Inventory.App.Pages.Employee;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using WinForms = System.Windows.Forms;
using CompanyDtoModel = Yakult.Inventory.App.Models.CompanyDto;

namespace Yakult.Inventory.App.Wpf.BorrowItems
{
    /// <summary>
    /// WPF replacement for the WinForms AddExternalAndBorrowDialog. Lets a user either add a
    /// brand-new item to inventory and immediately borrow it, or borrow an item already listed
    /// in inventory by serial number. Kept WinForms-compatible (IDisposable + ShowDialog overloads
    /// returning System.Windows.Forms.DialogResult) so it's a drop-in replacement at call sites.
    /// </summary>
    public sealed class WpfAddExternalAndBorrowDialog : Window, IDisposable
    {
        private static readonly Color PageBack = Color.FromRgb(240, 242, 246);
        private static readonly Color CardBack = Colors.White;
        private static readonly Color BorderSoft = Color.FromRgb(200, 210, 220);
        private static readonly Color TextHeader = Color.FromRgb(26, 35, 51);
        private static readonly Color TextMuted = Color.FromRgb(74, 90, 106);
        private static readonly Color TextFaint = Color.FromRgb(138, 154, 170);
        private static readonly Color AccentBlue = Color.FromRgb(58, 142, 246);

        private readonly BorrowItemsRepository _borrowRepo;
        private readonly ItemConditionRepository _condRepo = new ItemConditionRepository();
        private readonly string _prefillSerial;
        private readonly bool _requireBorrower;
        private readonly int? _defaultBorrowEmpId;

        private readonly List<BorrowEmployeeLookup> _employees = new List<BorrowEmployeeLookup>();
        private readonly List<CompanyDtoModel> _companies = new List<CompanyDtoModel>();

        // ── Item tab ─────────────────────────────────────────────────────────
        private RadioButton _rbListed;
        private RadioButton _rbExternal;
        private Border _externalHost;
        private Border _listedHost;

        private TextBox _txtName, _txtDescription, _txtSerialNumber, _txtModelNumber, _txtRemarks, _txtCreatedByReadonly;
        private ComboBox _cboCategory, _cboVendor, _cboUnitOfMeasure, _cboCondition;
        private DatePicker _dpCreated;
        private CheckBox _chkActive;
        private bool _vendorsLoaded;

        private TextBox _txtListedSerial, _txtListedName, _txtListedModel;
        private ComboBox _cboListedCategory;
        private TextBlock _lblListedItem, _lblListedModel, _lblListedCategory, _lblListedDesc;
        private Button _btnListedResolve;
        private DataGrid _gridListedResults;
        private TextBlock _lblListedStatus;
        private BorrowItemLookup _resolvedListedItem;
        private List<CategoryItem> _categoryItems = new List<CategoryItem>();

        // ── Borrowed By tab ──────────────────────────────────────────────────
        private TabItem _tabBorrower;
        private RadioButton _rbEmpListed, _rbEmpAddNew;
        private StackPanel _pnlEmpListed, _pnlEmpAddNew;
        private ComboBox _cboEmpCompany, _cboEmpDept, _cboEmp;
        private CheckBox _chkEmpDeptOnly;
        private TabControl _tabs;

        private Button _btnPrimary;
        private TextBlock _statusText;

        public ItemDto NewItem { get; private set; }
        public string ListedSerialToBorrow { get; private set; }
        public int? BorrowEmployeeId { get; private set; }
        public int? BorrowedByDeptId { get; private set; }
        public string BorrowedByDeptName { get; private set; }

        public WpfAddExternalAndBorrowDialog(BorrowItemsRepository borrowRepo, string serialPrefill, bool requireBorrower = true, int? defaultBorrowEmpId = null)
        {
            _borrowRepo = borrowRepo ?? throw new ArgumentNullException(nameof(borrowRepo));
            _prefillSerial = (serialPrefill ?? string.Empty).Trim();
            _requireBorrower = requireBorrower;
            _defaultBorrowEmpId = defaultBorrowEmpId;

            Title = "Add Item / Borrow";
            Width = 1040;
            Height = 640;
            MinWidth = 880;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Background = new SolidColorBrush(PageBack);
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 12.5;
            ShowInTaskbar = false;

            Content = BuildRoot();

            Loaded += async (s, e) =>
            {
                LoadCategories();
                await EnsureVendorsLoadedAsync();
                await LoadConditionsAsync();
                await LoadBorrowerLookupsAsync();
            };
        }

        // ── IDisposable / WinForms-compatible ShowDialog ────────────────────
        public void Dispose() { }

        public new WinForms.DialogResult ShowDialog()
        {
            bool? result = base.ShowDialog();
            return (result == true) ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }

        public WinForms.DialogResult ShowDialog(WinForms.IWin32Window owner)
        {
            if (owner != null)
                new System.Windows.Interop.WindowInteropHelper(this).Owner = owner.Handle;
            return ShowDialog();
        }

        private sealed class Win32Owner : WinForms.IWin32Window
        {
            public Win32Owner(IntPtr handle) { Handle = handle; }
            public IntPtr Handle { get; }
        }

        // ── Layout ───────────────────────────────────────────────────────────
        private UIElement BuildRoot()
        {
            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // card
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // footer

            var header = new TextBlock
            {
                Text = "Item Being Borrowed",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(TextHeader),
                Margin = new Thickness(0, 0, 0, 14)
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var card = new Border
            {
                Background = new SolidColorBrush(CardBack),
                BorderBrush = new SolidColorBrush(BorderSoft),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16),
                Effect = new DropShadowEffect { BlurRadius = 14, Color = Color.FromArgb(20, 15, 23, 42), ShadowDepth = 0 }
            };
            Grid.SetRow(card, 1);
            root.Children.Add(card);

            var cardGrid = new Grid();
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _tabs = BuildTabs();
            Grid.SetRow(_tabs, 0);
            cardGrid.Children.Add(_tabs);

            _statusText = new TextBlock
            {
                Foreground = new SolidColorBrush(TextFaint),
                FontSize = 11,
                Margin = new Thickness(2, 8, 0, 0)
            };
            Grid.SetRow(_statusText, 1);
            cardGrid.Children.Add(_statusText);

            card.Child = cardGrid;

            var footer = BuildFooter();
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            UpdateMode();

            if (!_requireBorrower)
            {
                _rbListed.IsEnabled = false;
                _rbExternal.IsChecked = true;
                if (_tabBorrower != null && _tabs.Items.Contains(_tabBorrower))
                    _tabs.Items.Remove(_tabBorrower);
            }

            return root;
        }

        private TabControl BuildTabs()
        {
            var tabs = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };

            var tabItem = new TabItem { Header = "Item", Content = BuildItemTab() };
            tabs.Items.Add(tabItem);

            _tabBorrower = new TabItem { Header = "Borrowed By", Content = BuildBorrowerTab() };
            tabs.Items.Add(_tabBorrower);

            return tabs;
        }

        private UIElement BuildItemTab()
        {
            var root = new Grid { Margin = new Thickness(4, 10, 4, 4) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            _rbListed = new RadioButton { Content = "Listed in Inventory", VerticalAlignment = VerticalAlignment.Center };
            _rbExternal = new RadioButton { Content = "Not Listed (External/New)", IsChecked = true, Margin = new Thickness(18, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            _rbListed.Checked += (s, e) => UpdateMode();
            _rbExternal.Checked += (s, e) => UpdateMode();
            modeRow.Children.Add(_rbListed);
            modeRow.Children.Add(_rbExternal);
            Grid.SetRow(modeRow, 0);
            root.Children.Add(modeRow);

            var host = new Grid();
            Grid.SetRow(host, 1);
            root.Children.Add(host);

            _externalHost = new Border { Child = BuildExternalUi() };
            _listedHost = new Border { Child = BuildListedUi() };
            host.Children.Add(_externalHost);
            host.Children.Add(_listedHost);

            return root;
        }

        private UIElement BuildExternalUi()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35, GridUnitType.Star) });

            // ── Left column: core item fields ──
            var left = new StackPanel();

            left.Children.Add(FieldLabel("Item Name *"));
            _txtName = FieldBox();
            left.Children.Add(_txtName);

            left.Children.Add(FieldLabel("Description"));
            _txtDescription = FieldBox(multiline: true, height: 56);
            left.Children.Add(_txtDescription);

            var catRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            catRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            catRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _cboCategory = FieldCombo();
            Grid.SetColumn(_cboCategory, 0);
            catRow.Children.Add(_cboCategory);
            var btnAddCategory = new Button
            {
                Content = "+ Add",
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(10, 0, 10, 0),
                Height = 32,
                FontSize = 11.5,
                Background = Brushes.White,
                Foreground = new SolidColorBrush(AccentBlue),
                BorderBrush = new SolidColorBrush(BorderSoft),
                Cursor = Cursors.Hand
            };
            btnAddCategory.Click += (s, e) => AddCategory();
            Grid.SetColumn(btnAddCategory, 1);
            catRow.Children.Add(btnAddCategory);
            left.Children.Add(FieldLabel("Category *"));
            left.Children.Add(catRow);

            left.Children.Add(FieldLabel("Serial Number"));
            _txtSerialNumber = FieldBox();
            _txtSerialNumber.Text = _prefillSerial;
            left.Children.Add(_txtSerialNumber);

            left.Children.Add(FieldLabel("Model Number *"));
            _txtModelNumber = FieldBox();
            left.Children.Add(_txtModelNumber);

            left.Children.Add(FieldLabel("Unit of Measure *"));
            _cboUnitOfMeasure = FieldCombo(editable: true);
            foreach (var uom in new[] { "Unit", "Piece", "Cartridge", "Box", "Set" })
                _cboUnitOfMeasure.Items.Add(uom);
            left.Children.Add(_cboUnitOfMeasure);

            _chkActive = new CheckBox
            {
                Content = "Item is Active",
                IsChecked = true,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = new SolidColorBrush(TextMuted)
            };
            left.Children.Add(_chkActive);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            // ── Right column: metadata + condition/remarks ──
            var right = new StackPanel();

            right.Children.Add(FieldLabel("Date Created"));
            _dpCreated = new DatePicker { SelectedDate = DateTime.Now, Height = 32, Margin = new Thickness(0, 0, 0, 10), FontSize = 12 };
            right.Children.Add(_dpCreated);

            right.Children.Add(FieldLabel("Created By"));
            _txtCreatedByReadonly = FieldBox();
            _txtCreatedByReadonly.Text = AppSession.CurrentUserName ?? string.Empty;
            _txtCreatedByReadonly.IsReadOnly = true;
            _txtCreatedByReadonly.Background = new SolidColorBrush(Color.FromRgb(245, 247, 250));
            right.Children.Add(_txtCreatedByReadonly);

            right.Children.Add(FieldLabel("Vendor"));
            _cboVendor = FieldCombo();
            right.Children.Add(_cboVendor);

            right.Children.Add(FieldLabel("Condition"));
            _cboCondition = FieldCombo();
            right.Children.Add(_cboCondition);

            right.Children.Add(FieldLabel("Remarks"));
            _txtRemarks = FieldBox();
            right.Children.Add(_txtRemarks);

            var hint = new TextBlock
            {
                Text = "This uses the same Add Item form as View Items.\nOn Save it creates the item in inventory, then borrows it.",
                Foreground = new SolidColorBrush(TextFaint),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };
            right.Children.Add(hint);

            Grid.SetColumn(right, 2);
            grid.Children.Add(right);

            var scroll = new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            return scroll;
        }

        private UIElement BuildListedUi()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // filters
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // status
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // results grid
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // selected item detail
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // note

            // ── Filter row: Name / Model / Category / Serial, all optional and ANDed ──
            var filters = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            for (int i = 0; i < 4; i++)
                filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 140 });
            filters.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameCol = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            nameCol.Children.Add(FieldLabel("Item Name"));
            _txtListedName = FieldBox();
            _txtListedName.Margin = new Thickness(0);
            _txtListedName.KeyDown += async (s, e) => { if (e.Key == Key.Enter) await SearchListedAsync(); };
            nameCol.Children.Add(_txtListedName);
            Grid.SetColumn(nameCol, 0);
            filters.Children.Add(nameCol);

            var modelCol = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            modelCol.Children.Add(FieldLabel("Model Number"));
            _txtListedModel = FieldBox();
            _txtListedModel.Margin = new Thickness(0);
            _txtListedModel.KeyDown += async (s, e) => { if (e.Key == Key.Enter) await SearchListedAsync(); };
            modelCol.Children.Add(_txtListedModel);
            Grid.SetColumn(modelCol, 1);
            filters.Children.Add(modelCol);

            var categoryCol = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            categoryCol.Children.Add(FieldLabel("Category"));
            _cboListedCategory = FieldCombo();
            _cboListedCategory.Margin = new Thickness(0);
            _cboListedCategory.SelectionChanged += async (s, e) => await SearchListedAsync();
            categoryCol.Children.Add(_cboListedCategory);
            Grid.SetColumn(categoryCol, 2);
            filters.Children.Add(categoryCol);

            var serialCol = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            serialCol.Children.Add(FieldLabel("Serial Number"));
            _txtListedSerial = FieldBox();
            _txtListedSerial.Margin = new Thickness(0);
            _txtListedSerial.Text = _prefillSerial;
            _txtListedSerial.KeyDown += async (s, e) => { if (e.Key == Key.Enter) await SearchListedAsync(); };
            serialCol.Children.Add(_txtListedSerial);
            Grid.SetColumn(serialCol, 3);
            filters.Children.Add(serialCol);

            _btnListedResolve = new Button
            {
                Content = "Search",
                Width = 90,
                Height = 32,
                VerticalAlignment = VerticalAlignment.Bottom,
                FontSize = 11.5,
                Background = new SolidColorBrush(AccentBlue),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            _btnListedResolve.Click += async (s, e) => await SearchListedAsync();
            Grid.SetColumn(_btnListedResolve, 4);
            filters.Children.Add(_btnListedResolve);

            Grid.SetRow(filters, 0);
            root.Children.Add(filters);

            _lblListedStatus = new TextBlock
            {
                Text = "Enter any combination of filters above and click Search.",
                Foreground = new SolidColorBrush(TextFaint),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(_lblListedStatus, 1);
            root.Children.Add(_lblListedStatus);

            _gridListedResults = BuildListedResultsGrid();
            var gridBorder = new Border
            {
                BorderBrush = new SolidColorBrush(BorderSoft),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                MinHeight = 140,
                MaxHeight = 220,
                Child = _gridListedResults
            };
            Grid.SetRow(gridBorder, 2);
            root.Children.Add(gridBorder);

            var detail = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            _lblListedItem = ValueRow(detail, "Name");
            _lblListedModel = ValueRow(detail, "Model");
            _lblListedCategory = ValueRow(detail, "Category");
            _lblListedDesc = ValueRow(detail, "Description");
            Grid.SetRow(detail, 3);
            root.Children.Add(detail);

            var note = new TextBlock
            {
                Text = "Listed mode will NOT create a new item. It will only borrow an existing inventory item.",
                Foreground = new SolidColorBrush(TextFaint),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(note, 4);
            root.Children.Add(note);

            return root;
        }

        private DataGrid BuildListedResultsGrid()
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
                FontSize = 12,
                RowHeight = 30,
                ColumnHeaderHeight = 28,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new System.Windows.Data.Binding("ItemName"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Serial", Binding = new System.Windows.Data.Binding("SerialNumber"), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Model", Binding = new System.Windows.Data.Binding("ModelNumber"), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Category", Binding = new System.Windows.Data.Binding("Category"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

            grid.SelectionChanged += (s, e) =>
            {
                _resolvedListedItem = grid.SelectedItem as BorrowItemLookup;
                ApplyListedSelection(_resolvedListedItem);
            };

            return grid;
        }

        private void ApplyListedSelection(BorrowItemLookup item)
        {
            if (item == null)
            {
                _lblListedItem.Text = "--";
                _lblListedModel.Text = "--";
                _lblListedCategory.Text = "--";
                _lblListedDesc.Text = "--";
                return;
            }

            _lblListedItem.Text = string.IsNullOrWhiteSpace(item.ItemName) ? "--" : item.ItemName.Trim();
            _lblListedModel.Text = string.IsNullOrWhiteSpace(item.ModelNumber) ? "--" : item.ModelNumber.Trim();
            _lblListedCategory.Text = string.IsNullOrWhiteSpace(item.Category) ? "--" : item.Category.Trim();
            _lblListedDesc.Text = string.IsNullOrWhiteSpace(item.ItemDescription) ? "--" : item.ItemDescription.Trim();
        }

        private TextBlock ValueRow(StackPanel parent, string label)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(TextMuted), FontSize = 11.5 });
            var value = new TextBlock { Text = "--", Foreground = new SolidColorBrush(TextHeader), FontSize = 12.5, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(value, 1);
            row.Children.Add(value);
            parent.Children.Add(row);
            return value;
        }

        private UIElement BuildBorrowerTab()
        {
            var stack = new StackPanel { Margin = new Thickness(4, 14, 4, 4), MaxWidth = 480 };

            var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            _rbEmpListed = new RadioButton { Content = "Listed employee", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
            _rbEmpAddNew = new RadioButton { Content = "Not listed (Add employee)", Margin = new Thickness(18, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            _rbEmpListed.Checked += (s, e) => UpdateEmployeeMode();
            _rbEmpAddNew.Checked += (s, e) => UpdateEmployeeMode();
            modeRow.Children.Add(_rbEmpListed);
            modeRow.Children.Add(_rbEmpAddNew);
            stack.Children.Add(modeRow);

            _pnlEmpListed = new StackPanel();
            _pnlEmpListed.Children.Add(FieldLabel("Company"));
            _cboEmpCompany = FieldCombo();
            _cboEmpCompany.SelectionChanged += (s, e) => { BindDepts(); BindEmps(); };
            _pnlEmpListed.Children.Add(_cboEmpCompany);

            _pnlEmpListed.Children.Add(FieldLabel("Department"));
            _cboEmpDept = FieldCombo();
            _cboEmpDept.SelectionChanged += (s, e) => BindEmps();
            _pnlEmpListed.Children.Add(_cboEmpDept);

            _pnlEmpListed.Children.Add(FieldLabel("Employee"));
            _cboEmp = FieldCombo();
            _pnlEmpListed.Children.Add(_cboEmp);

            _chkEmpDeptOnly = new CheckBox
            {
                Content = "Borrow for whole department (no specific employee)",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(TextMuted),
                Margin = new Thickness(0, 2, 0, 10)
            };
            _chkEmpDeptOnly.Checked += (s, e) => { _cboEmp.IsEnabled = false; _cboEmp.SelectedIndex = -1; };
            _chkEmpDeptOnly.Unchecked += (s, e) => _cboEmp.IsEnabled = true;
            _pnlEmpListed.Children.Add(_chkEmpDeptOnly);
            stack.Children.Add(_pnlEmpListed);

            _pnlEmpAddNew = new StackPanel { Visibility = Visibility.Collapsed };
            var addRow = new StackPanel { Orientation = Orientation.Horizontal };
            var btnAddEmp = new Button
            {
                Content = "Add Employee...",
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 11.5,
                Background = Brushes.White,
                Foreground = new SolidColorBrush(AccentBlue),
                BorderBrush = new SolidColorBrush(BorderSoft),
                Cursor = Cursors.Hand
            };
            btnAddEmp.Click += async (s, e) => await AddEmployeeAndSelectAsync();
            addRow.Children.Add(btnAddEmp);
            addRow.Children.Add(new TextBlock
            {
                Text = "Creates a new employee then returns here.",
                Foreground = new SolidColorBrush(TextFaint),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            });
            _pnlEmpAddNew.Children.Add(addRow);
            stack.Children.Add(_pnlEmpAddNew);

            return stack;
        }

        private UIElement BuildFooter()
        {
            var grid = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnCancel = new Button
            {
                Content = "Cancel", Height = 38, Width = 110,
                FontSize = 12.5, FontWeight = FontWeights.SemiBold,
                Background = Brushes.White, Foreground = new SolidColorBrush(TextMuted),
                BorderBrush = new SolidColorBrush(BorderSoft), BorderThickness = new Thickness(1.5),
                Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0)
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };

            _btnPrimary = new Button
            {
                Content = "Save", Height = 38, Width = 130,
                FontSize = 12.5, FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White, BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(AccentBlue),
                Cursor = Cursors.Hand
            };
            _btnPrimary.Click += (s, e) => OnPrimaryClick();

            var flow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            flow.Children.Add(btnCancel);
            flow.Children.Add(_btnPrimary);
            Grid.SetColumn(flow, 1);
            grid.Children.Add(flow);

            return grid;
        }

        // ── Field helpers ────────────────────────────────────────────────────
        private static TextBlock FieldLabel(string text) => new TextBlock
        {
            Text = text,
            FontSize = 11.5,
            Foreground = new SolidColorBrush(TextMuted),
            Margin = new Thickness(0, 0, 0, 4)
        };

        private TextBox FieldBox(bool multiline = false, double height = 32)
        {
            var box = new TextBox
            {
                Height = multiline ? height : 32,
                FontSize = 12.5,
                Padding = new Thickness(8, multiline ? 6 : 0, 8, 0),
                Background = Brushes.White,
                Foreground = new SolidColorBrush(TextHeader),
                BorderBrush = new SolidColorBrush(BorderSoft),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            if (multiline)
            {
                box.AcceptsReturn = true;
                box.TextWrapping = TextWrapping.Wrap;
                box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            return box;
        }

        private ComboBox FieldCombo(bool editable = false)
        {
            return new ComboBox
            {
                Height = 32,
                FontSize = 12.5,
                Background = Brushes.White,
                Foreground = new SolidColorBrush(TextHeader),
                BorderBrush = new SolidColorBrush(BorderSoft),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10),
                IsEditable = editable,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        // ── Mode switching ───────────────────────────────────────────────────
        private void UpdateMode()
        {
            if (_externalHost == null || _listedHost == null || _rbListed == null) return;

            var listed = _rbListed.IsChecked == true;
            _listedHost.Visibility = listed ? Visibility.Visible : Visibility.Collapsed;
            _externalHost.Visibility = listed ? Visibility.Collapsed : Visibility.Visible;

            if (_btnPrimary != null)
                _btnPrimary.Content = listed ? "Borrow" : "Save";

            UpdateEmployeeMode();
        }

        private void UpdateEmployeeMode()
        {
            var listed = _rbEmpListed != null && _rbEmpListed.IsChecked == true;
            if (_pnlEmpListed != null) _pnlEmpListed.Visibility = listed ? Visibility.Visible : Visibility.Collapsed;
            if (_pnlEmpAddNew != null) _pnlEmpAddNew.Visibility = listed ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SwitchToBorrowerTab()
        {
            if (!_requireBorrower || _tabs == null || _tabBorrower == null) return;
            if (!_tabs.Items.Contains(_tabBorrower)) return;
            _tabs.SelectedItem = _tabBorrower;
        }

        // ── Primary action ───────────────────────────────────────────────────
        private void OnPrimaryClick()
        {
            if (_rbListed.IsChecked == true)
                OkListed();
            else
                OkExternal();
        }

        private void OkExternal()
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show("Please enter an Item Name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                _txtName.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(_txtModelNumber.Text))
            {
                MessageBox.Show("Please enter a Model Number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                _txtModelNumber.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(_cboUnitOfMeasure.Text))
            {
                MessageBox.Show("Please enter a Unit of Measure (e.g., 'piece', 'box', 'cartridge').", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                _cboUnitOfMeasure.Focus();
                return;
            }
            var selectedCategory = _cboCategory.SelectedItem as CategoryItem;
            if (selectedCategory == null)
            {
                MessageBox.Show("Please select a Category.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                _cboCategory.Focus();
                return;
            }

            BorrowEmployeeLookup selectedEmp = null;
            DeptOpt selectedDept = null;
            if (_requireBorrower && !TryGetSelectedBorrower(out selectedEmp, out selectedDept))
                return;

            var selectedVendor = _cboVendor.SelectedItem as VendorItem;
            var selectedCondition = _cboCondition.SelectedItem as ConditionDto;

            var item = new ItemDto
            {
                Name = _txtName.Text.Trim(),
                Description = string.IsNullOrWhiteSpace(_txtDescription.Text) ? null : _txtDescription.Text.Trim(),
                CategoryId = selectedCategory.CategoryId,
                Category = selectedCategory.Name,
                SerialNumber = string.IsNullOrWhiteSpace(_txtSerialNumber.Text) ? null : _txtSerialNumber.Text.Trim(),
                ModelNumber = _txtModelNumber.Text.Trim(),
                Active = _chkActive.IsChecked == true,
                UnitOfMeasure = _cboUnitOfMeasure.Text.Trim(),
                StockOnHand = 1,
                DateCreated = _dpCreated.SelectedDate ?? DateTime.Now,
                CreatedByUserId = AppSession.CurrentUserId,
                CreatedByName = AppSession.CurrentUserName,
                VendorId = (selectedVendor != null && selectedVendor.VendorId > 0) ? (int?)selectedVendor.VendorId : null,
                VendorName = (selectedVendor != null && selectedVendor.VendorId > 0) ? selectedVendor.VendorName : null,
                ConditionId = selectedCondition?.ConditionId ?? 1,
                ConditionName = selectedCondition?.ConditionName,
                Remarks = string.IsNullOrWhiteSpace(_txtRemarks.Text) ? null : _txtRemarks.Text.Trim(),
                // More sensible defaults for borrowing flows.
                IsTrackedAsset = true,
                AffectsInventory = false,
                AcquisitionType = "Both"
            };

            NewItem = item;
            ListedSerialToBorrow = null;
            if (_requireBorrower && selectedDept != null)
            {
                BorrowEmployeeId = null;
                BorrowedByDeptId = selectedDept.Id;
                BorrowedByDeptName = selectedDept.Name;
            }
            else
            {
                BorrowEmployeeId = _requireBorrower ? (int?)selectedEmp.EmpId : null;
                BorrowedByDeptId = null;
                BorrowedByDeptName = null;
            }
            DialogResult = true;
            Close();
        }

        private void OkListed()
        {
            var serial = (_resolvedListedItem?.SerialNumber ?? string.Empty).Trim();
            if (serial.Length == 0)
            {
                MessageBox.Show("Search and select an item from the results list first.", "Borrow", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            BorrowEmployeeLookup selectedEmp = null;
            DeptOpt selectedDept = null;
            if (_requireBorrower && !TryGetSelectedBorrower(out selectedEmp, out selectedDept))
                return;

            ListedSerialToBorrow = serial;
            NewItem = null;
            if (_requireBorrower && selectedDept != null)
            {
                BorrowEmployeeId = null;
                BorrowedByDeptId = selectedDept.Id;
                BorrowedByDeptName = selectedDept.Name;
            }
            else
            {
                BorrowEmployeeId = _requireBorrower ? (int?)selectedEmp.EmpId : null;
                BorrowedByDeptId = null;
                BorrowedByDeptName = null;
            }
            DialogResult = true;
            Close();
        }

        private bool TryGetSelectedBorrower(out BorrowEmployeeLookup selectedEmp, out DeptOpt selectedDept)
        {
            selectedEmp = null;
            selectedDept = null;

            if (_rbEmpListed != null && _rbEmpListed.IsChecked != true)
            {
                SwitchToBorrowerTab();
                MessageBox.Show("Employee is not selected. Choose 'Listed employee' or add the employee first.", "Borrow", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            if (_chkEmpDeptOnly != null && _chkEmpDeptOnly.IsChecked == true)
            {
                selectedDept = _cboEmpDept?.SelectedItem as DeptOpt;
                if (selectedDept == null || selectedDept.Id <= 0)
                {
                    SwitchToBorrowerTab();
                    MessageBox.Show("Select a department first.", "Borrow", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }
                return true;
            }

            selectedEmp = _cboEmp?.SelectedItem as BorrowEmployeeLookup;
            if (selectedEmp == null || selectedEmp.EmpId <= 0)
            {
                SwitchToBorrowerTab();
                MessageBox.Show("Select Borrowed By first.", "Borrow", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            return true;
        }

        private async Task SearchListedAsync()
        {
            var name = _txtListedName?.Text ?? string.Empty;
            var serial = _txtListedSerial?.Text ?? string.Empty;
            var model = _txtListedModel?.Text ?? string.Empty;
            var selectedCategoryFilter = _cboListedCategory?.SelectedItem as CategoryItem;
            var category = (selectedCategoryFilter != null && selectedCategoryFilter.CategoryId > 0) ? selectedCategoryFilter.Name : string.Empty;

            if (name.Trim().Length == 0 && serial.Trim().Length == 0 && model.Trim().Length == 0 && category.Trim().Length == 0)
            {
                _lblListedStatus.Text = "Enter any combination of filters above and click Search.";
                _gridListedResults.ItemsSource = null;
                _resolvedListedItem = null;
                ApplyListedSelection(null);
                return;
            }

            List<BorrowItemLookup> results;
            try
            {
                _btnListedResolve.IsEnabled = false;
                Cursor = Cursors.Wait;
                _lblListedStatus.Text = "Searching...";
                results = await _borrowRepo.SearchListedItemsAsync(name, serial, model, category);
            }
            catch (Exception ex)
            {
                Logger.LogError("[WpfAddExternalAndBorrowDialog] SearchListedAsync failed.", ex);
                MessageBox.Show("We couldn't search inventory right now. Please try again.", "Search", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                Cursor = Cursors.Arrow;
                if (_btnListedResolve != null) _btnListedResolve.IsEnabled = true;
            }

            _resolvedListedItem = null;
            ApplyListedSelection(null);
            _gridListedResults.ItemsSource = results;

            if (results.Count == 0)
            {
                _lblListedStatus.Text = "No matching items found in inventory.";
            }
            else if (results.Count == 1)
            {
                _lblListedStatus.Text = "1 item found — selected automatically.";
                _gridListedResults.SelectedIndex = 0;
            }
            else
            {
                _lblListedStatus.Text = $"{results.Count} items found — select one below.";
            }
        }

        // ── Data loading ─────────────────────────────────────────────────────
        private void LoadCategories()
        {
            try
            {
                _categoryItems.Clear();
                var cs = DatabaseConfig.ConnectionString;
                if (string.IsNullOrWhiteSpace(cs))
                {
                    MessageBox.Show("Connection string not configured.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (var con = new System.Data.SqlClient.SqlConnection(cs))
                {
                    con.Open();
                    const string query = @"SELECT CategoryId, Name FROM dbo.ItemCategory WHERE Active = 1 ORDER BY Name";
                    using (var cmd = new System.Data.SqlClient.SqlCommand(query, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            _categoryItems.Add(new CategoryItem { CategoryId = reader.GetInt32(0), Name = reader.GetString(1) });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load categories: {ex.Message}\n\nPlease run the migration scripts.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            _cboCategory.Items.Clear();
            foreach (var c in _categoryItems)
                _cboCategory.Items.Add(c);

            if (_cboListedCategory != null)
            {
                var previouslySelected = (_cboListedCategory.SelectedItem as CategoryItem)?.Name;
                _cboListedCategory.Items.Clear();
                _cboListedCategory.Items.Add(new CategoryItem { CategoryId = 0, Name = "(Any Category)" });
                foreach (var c in _categoryItems)
                    _cboListedCategory.Items.Add(c);

                var restoreIndex = 0;
                if (!string.IsNullOrEmpty(previouslySelected))
                {
                    for (var i = 0; i < _cboListedCategory.Items.Count; i++)
                    {
                        if ((_cboListedCategory.Items[i] as CategoryItem)?.Name == previouslySelected) { restoreIndex = i; break; }
                    }
                }
                _cboListedCategory.SelectedIndex = restoreIndex;
            }
        }

        private void AddCategory()
        {
            var dialog = new QuickAddCategory();
            var ownerHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (dialog.ShowDialog(new Win32Owner(ownerHandle)) == WinForms.DialogResult.OK)
            {
                LoadCategories();
                for (var i = 0; i < _cboCategory.Items.Count; i++)
                {
                    if (_cboCategory.Items[i] is CategoryItem item && item.Name == dialog.NewCategoryName)
                    {
                        _cboCategory.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private async Task EnsureVendorsLoadedAsync()
        {
            if (_vendorsLoaded || _cboVendor == null) return;

            try
            {
                _vendorsLoaded = true;
                _cboVendor.Items.Clear();
                _cboVendor.Items.Add(new VendorItem { VendorId = 0, VendorName = "(None)" });

                var repo = new VendorRepository();
                var vendors = await repo.GetAllVendorsAsync();
                if (vendors != null)
                {
                    foreach (var vendor in vendors)
                    {
                        if (vendor == null || !vendor.IsActive || vendor.IsArchived || string.IsNullOrWhiteSpace(vendor.VendorName))
                            continue;
                        _cboVendor.Items.Add(new VendorItem { VendorId = vendor.VendorId, VendorName = vendor.VendorName.Trim() });
                    }
                }

                _cboVendor.SelectedIndex = _cboVendor.Items.Count > 0 ? 0 : -1;
            }
            catch (Exception ex)
            {
                _vendorsLoaded = false;
                _cboVendor.Items.Clear();
                _cboVendor.Items.Add(new VendorItem { VendorId = 0, VendorName = "(None)" });
                _cboVendor.SelectedIndex = 0;
                MessageBox.Show($"Failed to load vendors: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task LoadConditionsAsync()
        {
            try
            {
                var conds = _condRepo.GetAll();
                _cboCondition.Items.Clear();
                _cboCondition.DisplayMemberPath = "ConditionName";
                foreach (var c in conds ?? new List<ConditionDto>())
                    _cboCondition.Items.Add(c);

                for (var i = 0; i < _cboCondition.Items.Count; i++)
                {
                    if ((_cboCondition.Items[i] as ConditionDto)?.ConditionName?.Equals("Good", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        _cboCondition.SelectedIndex = i;
                        break;
                    }
                }
            }
            catch
            {
                // Non-fatal: ItemRepository will default ConditionId to 1 anyway.
            }

            await Task.CompletedTask;
        }

        private async Task LoadBorrowerLookupsAsync()
        {
            if (!_requireBorrower) return;

            try
            {
                var all = await _borrowRepo.GetActiveEmployeesAsync(12000);
                _employees.Clear();
                _employees.AddRange((all ?? new List<BorrowEmployeeLookup>()).Where(e => e != null && e.EmpId > 0));
            }
            catch (Exception ex)
            {
                _employees.Clear();
                Logger.LogError("[WpfAddExternalAndBorrowDialog] LoadBorrowerLookupsAsync employees failed.", ex);
                MessageBox.Show("We couldn't load the employee list right now. Please try again.", "Borrow", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            try
            {
                var companies = await _borrowRepo.GetActiveCompaniesAsync(5000);
                _companies.Clear();
                _companies.AddRange((companies ?? new List<CompanyDtoModel>()).Where(c => c != null && c.ComId > 0));
            }
            catch
            {
                _companies.Clear();
            }

            BindCompanies();
            if (_defaultBorrowEmpId.HasValue && _defaultBorrowEmpId.Value > 0)
                SelectEmployeeById(_defaultBorrowEmpId.Value);
        }

        // ── Company/Department/Employee cascade ─────────────────────────────
        private sealed class CompanyOpt
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private sealed class DeptOpt
        {
            public int Id { get; set; }
            public int CompanyId { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private void BindCompanies()
        {
            if (_cboEmpCompany == null) return;

            var companies = new List<CompanyOpt> { new CompanyOpt { Id = -1, Name = "All Companies" } };

            companies.AddRange(_companies.Select(c => new CompanyOpt
            {
                Id = c.ComId,
                Name = (c.CompanyName ?? string.Empty).Trim().Length == 0 ? $"ComId {c.ComId}" : c.CompanyName.Trim()
            }));

            foreach (var missing in _employees
                         .Where(e => e != null && e.ComId > 0)
                         .Select(e => e.ComId)
                         .Distinct()
                         .Where(comId => companies.All(c => c.Id != comId))
                         .OrderBy(x => x))
            {
                companies.Add(new CompanyOpt { Id = missing, Name = $"ComId {missing}" });
            }

            if (_employees.Any(e => e != null && e.ComId <= 0))
                companies.Add(new CompanyOpt { Id = 0, Name = "Unknown" });

            _cboEmpCompany.ItemsSource = companies.OrderBy(c => c.Name).ToList();
            _cboEmpCompany.SelectedIndex = 0;

            BindDepts();
            BindEmps();
            UpdateEmployeeMode();
        }

        private int GetSelectedCompanyId() => (_cboEmpCompany?.SelectedItem as CompanyOpt)?.Id ?? -1;

        private void BindDepts()
        {
            if (_cboEmpDept == null) return;

            var comId = GetSelectedCompanyId();
            var filtered = _employees.Where(e => e != null && (comId < 0 || e.ComId == comId)).ToList();

            var depts = filtered
                .Where(e => e.DeptId > 0)
                .GroupBy(e => e.DeptId)
                .Select(g => new DeptOpt { Id = g.Key, CompanyId = g.First().ComId, Name = g.First().DepartmentName })
                .OrderBy(d => d.Name)
                .ToList();

            if (filtered.Any(e => e.DeptId <= 0))
                depts.Add(new DeptOpt { Id = 0, CompanyId = comId, Name = "N/A" });

            _cboEmpDept.ItemsSource = depts;
            _cboEmpDept.SelectedIndex = depts.Count > 0 ? 0 : -1;
        }

        private void BindEmps()
        {
            if (_cboEmp == null) return;

            var dept = _cboEmpDept.SelectedItem as DeptOpt;
            var comId = GetSelectedCompanyId();
            var list = _employees
                .Where(e => dept != null && ((dept.Id > 0 && e.DeptId == dept.Id) || (dept.Id == 0 && e.DeptId <= 0)) && (comId < 0 || e.ComId == comId))
                .OrderBy(e => e.EmployeeName)
                .ToList();

            _cboEmp.ItemsSource = list;
            _cboEmp.DisplayMemberPath = "DisplayText";
            _cboEmp.SelectedIndex = list.Count > 0 ? 0 : -1;
        }

        private void SelectEmployeeById(int empId)
        {
            if (empId <= 0) return;

            var e = _employees.FirstOrDefault(x => x != null && x.EmpId == empId);
            if (e == null) return;

            var companies = _cboEmpCompany.ItemsSource as List<CompanyOpt>;
            if (companies != null)
            {
                for (var i = 0; i < companies.Count; i++)
                {
                    if (companies[i].Id == e.ComId) { _cboEmpCompany.SelectedIndex = i; break; }
                }
            }

            BindDepts();
            var depts = _cboEmpDept.ItemsSource as List<DeptOpt>;
            if (depts != null)
            {
                for (var i = 0; i < depts.Count; i++)
                {
                    if (depts[i].Id == e.DeptId) { _cboEmpDept.SelectedIndex = i; break; }
                }
            }

            BindEmps();
            var emps = _cboEmp.ItemsSource as List<BorrowEmployeeLookup>;
            if (emps != null)
            {
                for (var i = 0; i < emps.Count; i++)
                {
                    if (emps[i].EmpId == empId) { _cboEmp.SelectedIndex = i; break; }
                }
            }
        }

        private async Task AddEmployeeAndSelectAsync()
        {
            var dlg = new QuickAddEmployeeDialog();
            var ownerHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (dlg.ShowDialog(new Win32Owner(ownerHandle)) != WinForms.DialogResult.OK || !dlg.NewEmployeeId.HasValue || dlg.NewEmployeeId.Value <= 0)
                return;

            var newEmpId = dlg.NewEmployeeId.Value;
            if (_chkEmpDeptOnly != null) _chkEmpDeptOnly.IsChecked = false;
            await LoadBorrowerLookupsAsync();
            SelectEmployeeById(newEmpId);

            if (_employees.All(e => e == null || e.EmpId != newEmpId))
            {
                MessageBox.Show("Employee saved, but it is missing a Department.\n\nSet the employee's Department then try again.", "Borrow", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_rbEmpListed != null) _rbEmpListed.IsChecked = true;
        }

        // Helper classes shared with the WinForms AddItemPage's combobox item shapes.
        private sealed class CategoryItem
        {
            public int CategoryId { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private sealed class VendorItem
        {
            public int VendorId { get; set; }
            public string VendorName { get; set; }
            public override string ToString() => VendorName;
        }
    }
}
