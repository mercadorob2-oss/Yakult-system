using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Yakult.Inventory.App.Models.BorrowItems;
using Yakult.Inventory.App.Wpf.CallMonitoring;

namespace Yakult.Inventory.App.Wpf.BorrowItems
{
    public enum TransactionMode
    {
        None,
        Borrow,
        Return
    }

    public sealed class IdNamePair
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public override string ToString() => Name ?? string.Empty;
    }

    public sealed class BorrowFormEventArgs : EventArgs
    {
        public string SerialNumber { get; set; }
        public int? CompanyId { get; set; }
        public int? DepartmentId { get; set; }
        public int? BranchId { get; set; }
        public int? EmployeeId { get; set; }
        public bool BorrowForDepartmentOnly { get; set; }
        public bool IsBackdate { get; set; }
        public DateTime? BackdateLocal { get; set; }
    }

    public sealed class WpfBorrowTransactionWorkspace : UserControl
    {
        // ── Events ─────────────────────────────────────────────────────────────
        public event EventHandler<string> ScanSubmitted;
        public event EventHandler ResolveClicked;
        public event EventHandler AddNewItemClicked;
        public event EventHandler BrowseItemsClicked;
        public event EventHandler<string> ModelSearchRequested;
        public event EventHandler<BorrowFormEventArgs> BorrowClicked;
        public event EventHandler<BorrowFormEventArgs> ReturnClicked;
        public event EventHandler<bool> AddEmployeeClicked;
        public event EventHandler<int?> BorrowCompanyChanged;
        public event EventHandler<int?> BorrowDepartmentChanged;
        public event EventHandler<int?> BorrowBranchChanged;
        public event EventHandler<int?> ReturnCompanyChanged;
        public event EventHandler<int?> ReturnDepartmentChanged;
        public event EventHandler<int?> ReturnBranchChanged;
        public event EventHandler BackdateToggled;

        // ── Scan ─────────────────────────────────────────────────────────────────
        private TextBox _scanInput;
        private TextBox _modelHelperInput;
        private Border _modelInputBorder;
        private Popup _modelSuggestionsPopup;
        private ListBox _modelSuggestionsList;
        private TextBlock _modelSuggestionsHeaderText;
        private System.Windows.Threading.DispatcherTimer _modelSearchDebounce;

        // ── Info banner ────────────────────────────────────────────────────────
        private Border _infoBanner;
        private TextBlock _infoTitle;
        private TextBlock _infoSubtitle;

        // ── Borrow card ────────────────────────────────────────────────────────
        private ComboBox _borrowCompanyCombo;
        private ComboBox _borrowDeptCombo;
        private ComboBox _borrowBranchCombo;
        private ComboBox _borrowEmpCombo;
        private CheckBox _borrowDeptOnlyCheck;
        private CheckBox _returnDeptOnlyCheck;
        private CheckBox _backdateCheck;
        private DatePicker _backdateDatePicker;
        private TextBox _backdateTimeText;
        private StackPanel _backdateDetails;
        private Button _borrowButton;

        // ── Return card ────────────────────────────────────────────────────────
        private ComboBox _returnCompanyCombo;
        private ComboBox _returnDeptCombo;
        private ComboBox _returnBranchCombo;
        private ComboBox _returnEmpCombo;
        private Button _returnButton;

        // ── State ────────────────────────────────────────────────────────────────
        private List<BorrowEmployeeLookup> _allEmployees = new List<BorrowEmployeeLookup>();
        private bool _suppressEvents;

        public WpfBorrowTransactionWorkspace()
        {
            Background = BrushFromRgb(242, 245, 249);
            Resources.MergedDictionaries.Add(WpfThemeResources.GetScrollBarStyle());

            var outer = new Grid { Margin = new Thickness(14, 12, 14, 12) };
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Content = outer;

            var scanEl = BuildScanSection() as FrameworkElement;
            Grid.SetRow(scanEl, 0);
            outer.Children.Add(scanEl);

            _infoBanner = BuildInfoBanner();
            _infoBanner.Visibility = Visibility.Collapsed;
            Grid.SetRow(_infoBanner, 1);
            outer.Children.Add(_infoBanner);

            var cardsGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            cardsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            cardsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10, GridUnitType.Pixel) });
            cardsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(cardsGrid, 2);
            outer.Children.Add(cardsGrid);

            var borrowCard = BuildBorrowCard();
            borrowCard.VerticalAlignment = VerticalAlignment.Stretch;
            Grid.SetRow(borrowCard, 0);
            cardsGrid.Children.Add(borrowCard);

            var retCard = BuildReturnCard();
            retCard.VerticalAlignment = VerticalAlignment.Stretch;
            Grid.SetRow(retCard, 2);
            cardsGrid.Children.Add(retCard);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ═══════════════════════════════════════════════════════════════════════

        public string ScanText
        {
            get => (_scanInput.Text ?? string.Empty).Trim();
            set => _scanInput.Text = value ?? string.Empty;
        }

        public void SetMode(TransactionMode mode) { /* Both cards are always visible */ }

        public void SetResolvedItem(BorrowItemLookup item)
        {
            if (item == null) { _infoBanner.Visibility = Visibility.Collapsed; return; }
            _infoTitle.Text = item.DisplayText;
            _infoSubtitle.Text = string.IsNullOrWhiteSpace(item.ItemDescription)
                ? "Ready to borrow" : item.ItemDescription.Trim();
            _infoBanner.Background = BrushFromRgb(239, 246, 255);
            _infoBanner.BorderBrush = BrushFromRgb(191, 219, 254);
            _infoBanner.Visibility = Visibility.Visible;
        }

        public void SetReturnInfo(BorrowLogRow row)
        {
            if (row == null) { _infoBanner.Visibility = Visibility.Collapsed; return; }
            _infoTitle.Text = row.ItemDisplay;
            _infoSubtitle.Text = $"Borrowed by {row.BorrowedByEmpName} ({row.BorrowedByDeptName}) @ {row.BorrowedAtLocal}";
            _infoBanner.Background = BrushFromRgb(240, 253, 244);
            _infoBanner.BorderBrush = BrushFromRgb(134, 239, 172);
            _infoBanner.Visibility = Visibility.Visible;
        }

        public void SetCompanies(List<IdNamePair> companies)
        {
            _suppressEvents = true;
            var list = companies ?? new List<IdNamePair>();
            _borrowCompanyCombo.ItemsSource = list;
            _returnCompanyCombo.ItemsSource = list;
            if (list.Count > 0)
            {
                _borrowCompanyCombo.SelectedIndex = 0;
                _returnCompanyCombo.SelectedIndex = 0;
            }
            _suppressEvents = false;
            RefreshBorrowDept();
            RefreshReturnDept();
        }

        public void SetDepartments(List<IdNamePair> departments) { /* filtering now handled internally */ }

        public void SetEmployees(List<BorrowEmployeeLookup> employees)
        {
            _allEmployees = employees ?? new List<BorrowEmployeeLookup>();
            RefreshBorrowDept();
            RefreshReturnDept();
        }

        public void SelectCompany(int comId)
        {
            SelectByIdName(_returnCompanyCombo, comId);
            RefreshReturnDept();
        }

        public void SelectDepartment(int deptId)
        {
            SelectByIdName(_returnDeptCombo, deptId);
            RefreshReturnEmp();
        }

        public void SelectBranch(int branchId)
        {
            SelectByIdName(_returnBranchCombo, branchId);
            RefreshReturnEmp();
        }

        public void SelectBorrowCompany(int comId)
        {
            SelectByIdName(_borrowCompanyCombo, comId);
            RefreshBorrowDept();
        }

        public void SelectBorrowDepartment(int deptId)
        {
            SelectByIdName(_borrowDeptCombo, deptId);
            RefreshBorrowEmp();
        }

        public void SelectBorrowBranch(int branchId)
        {
            SelectByIdName(_borrowBranchCombo, branchId);
            RefreshBorrowEmp();
        }

        public void SelectEmployee(int empId)
        {
            var emp = _allEmployees.FirstOrDefault(e => e.EmpId == empId);
            if (emp == null) return;

            _suppressEvents = true;
            var branches = GetBranches(emp.ComId);
            _returnBranchCombo.ItemsSource = branches;
            var branchIndex = branches.FindIndex(b => b.Id == emp.BranchId);
            _returnBranchCombo.SelectedIndex = branchIndex;

            var list = GetEmps(emp.ComId, emp.DeptId, emp.BranchId);
            _returnEmpCombo.ItemsSource = list;
            var idx = list.FindIndex(e => e.EmpId == empId);
            if (idx >= 0) _returnEmpCombo.SelectedIndex = idx;
            _suppressEvents = false;
        }

        public void Clear()
        {
            _scanInput.Clear();
            _modelHelperInput?.Clear();
            HideModelSuggestions();
            _infoBanner.Visibility = Visibility.Collapsed;
            _borrowDeptOnlyCheck.IsChecked = false;
            _returnDeptOnlyCheck.IsChecked = false;
            _backdateCheck.IsChecked = false;
            if (_backdateDetails != null) _backdateDetails.Visibility = Visibility.Collapsed;
            if (_backdateDatePicker != null) _backdateDatePicker.SelectedDate = DateTime.Today;
            if (_backdateTimeText != null) _backdateTimeText.Text = DateTime.Now.ToString("HH:mm");
        }

        /// <summary>
        /// Populates the suggestions dropdown under the "Model number" helper field. Called by
        /// the host after it resolves a <see cref="ModelSearchRequested"/> search. Purely a
        /// convenience for locating a serial number — selecting a suggestion fills the serial
        /// scan box but does not submit/borrow anything by itself.
        /// </summary>
        public void ShowModelSuggestions(List<BorrowItemLookup> matches)
        {
            if (matches == null || matches.Count == 0)
            {
                HideModelSuggestions();
                return;
            }

            _modelSuggestionsList.ItemsSource = matches;
            if (_modelSuggestionsHeaderText != null)
            {
                _modelSuggestionsHeaderText.Text = matches.Count == 1
                    ? "1 matching item"
                    : $"{matches.Count} matching items";
            }

            if (_modelInputBorder != null)
            {
                _modelInputBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(139, 92, 246));
                _modelInputBorder.Effect = new DropShadowEffect
                {
                    BlurRadius = 14,
                    Color = Color.FromArgb(40, 139, 92, 246),
                    ShadowDepth = 0
                };
            }

            _modelSuggestionsPopup.IsOpen = true;
        }

        private void HideModelSuggestions()
        {
            if (_modelSuggestionsPopup != null)
                _modelSuggestionsPopup.IsOpen = false;

            if (_modelInputBorder != null && !_modelHelperInput.IsKeyboardFocused)
            {
                _modelInputBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                _modelInputBorder.Effect = new DropShadowEffect
                {
                    BlurRadius = 8,
                    Color = Color.FromArgb(15, 100, 116, 139),
                    ShadowDepth = 0
                };
            }
        }

        private void ApplySelectedModelSuggestion()
        {
            var selected = _modelSuggestionsList.SelectedItem as BorrowItemLookup;
            if (selected == null || string.IsNullOrWhiteSpace(selected.SerialNumber))
                return;

            ScanText = selected.SerialNumber;
            HideModelSuggestions();
            _scanInput.Focus();
            _scanInput.CaretIndex = _scanInput.Text.Length;
        }

        public void SetBusy(bool busy)
        {
            _scanInput.IsEnabled = !busy;
            _borrowButton.IsEnabled = !busy;
            _returnButton.IsEnabled = !busy;
            _borrowCompanyCombo.IsEnabled = !busy;
            _borrowDeptCombo.IsEnabled = !busy;
            _borrowBranchCombo.IsEnabled = !busy;
            _borrowEmpCombo.IsEnabled = !busy && _borrowDeptOnlyCheck.IsChecked != true;
            _borrowDeptOnlyCheck.IsEnabled = !busy;
            _returnCompanyCombo.IsEnabled = !busy;
            _returnDeptCombo.IsEnabled = !busy;
            _returnBranchCombo.IsEnabled = !busy;
            _returnEmpCombo.IsEnabled = !busy && _returnDeptOnlyCheck.IsChecked != true;
            _returnDeptOnlyCheck.IsEnabled = !busy;
        }

        public bool IsBackdateChecked => _backdateCheck.IsChecked == true;

        public DateTime? GetBackdateLocal()
        {
            if (!IsBackdateChecked) return null;
            var date = _backdateDatePicker.SelectedDate ?? DateTime.Today;
            if (TimeSpan.TryParse(_backdateTimeText.Text, out var time))
                return date.Date + time;
            return date;
        }

        public int? SelectedCompanyId => (_borrowCompanyCombo.SelectedItem as IdNamePair)?.Id;
        public int? SelectedDepartmentId => (_borrowDeptCombo.SelectedItem as IdNamePair)?.Id;
        public int? SelectedBranchId => (_borrowBranchCombo.SelectedItem as IdNamePair)?.Id;
        public int? SelectedEmployeeId => (_borrowEmpCombo.SelectedItem as BorrowEmployeeLookup)?.EmpId;

        // ── Internal cascade filtering ───────────────────────────────────────────

        private void RefreshBorrowDept()
        {
            _suppressEvents = true;
            var comId = (_borrowCompanyCombo.SelectedItem as IdNamePair)?.Id ?? 0;
            _borrowDeptCombo.ItemsSource = GetDepts(comId);
            _borrowDeptCombo.SelectedIndex = -1;
            _suppressEvents = false;
            RefreshBorrowBranch();
        }

        private void RefreshBorrowBranch()
        {
            _suppressEvents = true;
            var comId = (_borrowCompanyCombo.SelectedItem as IdNamePair)?.Id ?? 0;
            _borrowBranchCombo.ItemsSource = GetBranches(comId);
            _borrowBranchCombo.SelectedIndex = -1;
            _suppressEvents = false;
            RefreshBorrowEmp();
        }

        private void RefreshBorrowEmp()
        {
            _suppressEvents = true;
            var comId = (_borrowCompanyCombo.SelectedItem as IdNamePair)?.Id ?? 0;
            var deptId = (_borrowDeptCombo.SelectedItem as IdNamePair)?.Id ?? 0;
            var branchId = (_borrowBranchCombo.SelectedItem as IdNamePair)?.Id ?? 0;
            _borrowEmpCombo.ItemsSource = GetEmps(comId, deptId, branchId);
            _borrowEmpCombo.SelectedIndex = -1;
            _suppressEvents = false;
        }

        private void RefreshReturnDept()
        {
            _suppressEvents = true;
            var comId = (_returnCompanyCombo.SelectedItem as IdNamePair)?.Id ?? 0;
            _returnDeptCombo.ItemsSource = GetDepts(comId);
            _returnDeptCombo.SelectedIndex = -1;
            _suppressEvents = false;
            RefreshReturnBranch();
        }

        private void RefreshReturnBranch()
        {
            _suppressEvents = true;
            var comId = (_returnCompanyCombo.SelectedItem as IdNamePair)?.Id ?? 0;
            _returnBranchCombo.ItemsSource = GetBranches(comId);
            _returnBranchCombo.SelectedIndex = -1;
            _suppressEvents = false;
            RefreshReturnEmp();
        }

        private void RefreshReturnEmp()
        {
            _suppressEvents = true;
            var comId = (_returnCompanyCombo.SelectedItem as IdNamePair)?.Id ?? 0;
            var deptId = (_returnDeptCombo.SelectedItem as IdNamePair)?.Id ?? 0;
            var branchId = (_returnBranchCombo.SelectedItem as IdNamePair)?.Id ?? 0;
            _returnEmpCombo.ItemsSource = GetEmps(comId, deptId, branchId);
            _returnEmpCombo.SelectedIndex = -1;
            _suppressEvents = false;
        }

        private List<IdNamePair> GetDepts(int comId)
        {
            return _allEmployees
                .Where(e => e.DeptId > 0 && (comId <= 0 || e.ComId == comId))
                .GroupBy(e => e.DeptId)
                .Select(g => new IdNamePair { Id = g.Key, Name = g.First().DepartmentName ?? $"Dept {g.Key}" })
                .OrderBy(d => d.Name)
                .ToList();
        }

        private List<IdNamePair> GetBranches(int comId)
        {
            return _allEmployees
                .Where(e => e.BranchId > 0 && (comId <= 0 || e.ComId == comId))
                .GroupBy(e => e.BranchId)
                .Select(g => new IdNamePair { Id = g.Key, Name = g.First().BranchName ?? $"Branch {g.Key}" })
                .OrderBy(b => b.Name)
                .ToList();
        }

        private List<BorrowEmployeeLookup> GetEmps(int comId, int deptId, int branchId)
        {
            return _allEmployees
                .Where(e =>
                    (comId <= 0 || e.ComId == comId) &&
                    (deptId <= 0 || e.DeptId == deptId) &&
                    (branchId <= 0 || e.BranchId == branchId))
                .OrderBy(e => e.EmployeeName)
                .ThenBy(e => e.DepartmentName)
                .ThenBy(e => e.BranchName)
                .ToList();
        }

        private static void SelectByIdName(ComboBox cb, int id)
        {
            var list = cb.ItemsSource as List<IdNamePair>;
            if (list == null) return;
            var idx = list.FindIndex(x => x.Id == id);
            if (idx >= 0) cb.SelectedIndex = idx;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  UI BUILDERS
        // ═══════════════════════════════════════════════════════════════════════

        private UIElement BuildScanSection()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            // ── Optional "Model number" helper ──────────────────────────────────
            // Not a required input and not submitted anywhere — it only helps the
            // user narrow down which serial number to type/scan below when they
            // only know (or partially know) the item's model number.
            var modelTitleRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            modelTitleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            modelTitleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            modelTitleRow.Children.Add(new TextBlock
            {
                Text = "Model number",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105),
                VerticalAlignment = VerticalAlignment.Center
            });
            var modelHint = new TextBlock
            {
                Text = "Optional — helps you find the serial",
                FontSize = 10.5, Foreground = BrushFromRgb(148, 163, 184),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(modelHint, 1);
            modelTitleRow.Children.Add(modelHint);
            stack.Children.Add(modelTitleRow);

            var modelBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 9, 14, 9),
                Margin = new Thickness(0, 0, 0, 12),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 8,
                    Color = Color.FromArgb(15, 100, 116, 139),
                    ShadowDepth = 0
                }
            };
            modelBorder.GotKeyboardFocus += (s, e) =>
            {
                modelBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(139, 92, 246));
                modelBorder.Effect = new DropShadowEffect
                {
                    BlurRadius = 14,
                    Color = Color.FromArgb(40, 139, 92, 246),
                    ShadowDepth = 0
                };
            };
            modelBorder.LostKeyboardFocus += (s, e) =>
            {
                if (_modelSuggestionsPopup != null && _modelSuggestionsPopup.IsOpen)
                    return;
                modelBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                modelBorder.Effect = new DropShadowEffect
                {
                    BlurRadius = 8,
                    Color = Color.FromArgb(15, 100, 116, 139),
                    ShadowDepth = 0
                };
            };

            var modelInputGrid = new Grid();
            modelInputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            modelInputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var modelIcon = new System.Windows.Shapes.Path
            {
                Data = System.Windows.Media.Geometry.Parse(
                    "M4,4 L16,4 L16,7 L13,7 L13,16 L10,16 L10,9 L7,9 L7,16 L4,16 Z"),
                Fill = BrushFromRgb(139, 92, 246),
                Stretch = Stretch.Uniform,
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.85
            };
            Grid.SetColumn(modelIcon, 0);
            modelInputGrid.Children.Add(modelIcon);

            var modelInputHost = new Grid();
            Grid.SetColumn(modelInputHost, 1);
            modelInputGrid.Children.Add(modelInputHost);

            var modelPlaceholder = new TextBlock
            {
                Text = "e.g. Latitude 5420, LaserJet Pro...",
                FontSize = 13, Foreground = BrushFromRgb(180, 190, 204),
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center
            };
            modelInputHost.Children.Add(modelPlaceholder);

            _modelHelperInput = new TextBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = BrushFromRgb(51, 65, 85),
                CaretBrush = BrushFromRgb(139, 92, 246),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            modelInputHost.Children.Add(_modelHelperInput);
            TextOptions.SetTextFormattingMode(_modelHelperInput, TextFormattingMode.Display);
            _modelHelperInput.TextChanged += (s, e) =>
            {
                var text = _modelHelperInput.Text?.Trim() ?? string.Empty;
                modelPlaceholder.Visibility = text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (text.Length == 0)
                {
                    HideModelSuggestions();
                    _modelSearchDebounce?.Stop();
                    return;
                }

                if (_modelSearchDebounce == null)
                {
                    _modelSearchDebounce = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(300)
                    };
                    _modelSearchDebounce.Tick += (ds, de) =>
                    {
                        _modelSearchDebounce.Stop();
                        var current = _modelHelperInput.Text?.Trim() ?? string.Empty;
                        if (current.Length > 0)
                            ModelSearchRequested?.Invoke(this, current);
                    };
                }

                _modelSearchDebounce.Stop();
                _modelSearchDebounce.Start();
            };
            _modelHelperInput.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    HideModelSuggestions();
                }
                else if (e.Key == Key.Down && _modelSuggestionsPopup != null && _modelSuggestionsPopup.IsOpen)
                {
                    e.Handled = true;
                    _modelSuggestionsList.Focus();
                    if (_modelSuggestionsList.Items.Count > 0)
                        _modelSuggestionsList.SelectedIndex = 0;
                }
            };
            var modelClearBtn = new Button
            {
                Content = "✕",
                FontSize = 10, Width = 20, Height = 20,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = BrushFromRgb(148, 163, 184),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            modelClearBtn.Click += (s, e) =>
            {
                _modelHelperInput.Clear();
                HideModelSuggestions();
                _modelHelperInput.Focus();
            };
            _modelHelperInput.TextChanged += (s, e) =>
            {
                modelClearBtn.Visibility = string.IsNullOrEmpty(_modelHelperInput.Text) ? Visibility.Collapsed : Visibility.Visible;
            };
            Grid.SetColumn(modelClearBtn, 1);
            modelInputGrid.Children.Add(modelClearBtn);

            modelBorder.Child = modelInputGrid;
            _modelInputBorder = modelBorder;
            stack.Children.Add(modelBorder);

            _modelSuggestionsList = new ListBox
            {
                MaxHeight = 260,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Padding = new Thickness(6)
            };
            // Item template: model number (prominent) + serial + name/description, with hover/selection highlight.
            var itemTemplate = new DataTemplate();
            var itemBorderFactory = new FrameworkElementFactory(typeof(Border));
            itemBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            itemBorderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 8, 10, 8));
            itemBorderFactory.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 2));
            itemBorderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            itemBorderFactory.SetValue(FrameworkElement.NameProperty, "ItemBg");

            var itemStackFactory = new FrameworkElementFactory(typeof(StackPanel));
            var modelRowFactory = new FrameworkElementFactory(typeof(StackPanel));
            modelRowFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            var modelBadgeFactory = new FrameworkElementFactory(typeof(Border));
            modelBadgeFactory.SetValue(Border.BackgroundProperty, BrushFromRgb(238, 233, 254));
            modelBadgeFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            modelBadgeFactory.SetValue(Border.PaddingProperty, new Thickness(6, 1, 6, 1));
            modelBadgeFactory.SetValue(Border.MarginProperty, new Thickness(0, 0, 8, 0));
            var modelBadgeText = new FrameworkElementFactory(typeof(TextBlock));
            modelBadgeText.SetBinding(TextBlock.TextProperty, new Binding("ModelNumber"));
            modelBadgeText.SetValue(TextBlock.FontSizeProperty, 11.0);
            modelBadgeText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            modelBadgeText.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(109, 40, 217));
            modelBadgeFactory.AppendChild(modelBadgeText);
            modelRowFactory.AppendChild(modelBadgeFactory);

            var serialTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            serialTextFactory.SetBinding(TextBlock.TextProperty, new Binding("SerialNumber"));
            serialTextFactory.SetValue(TextBlock.FontSizeProperty, 13.0);
            serialTextFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            serialTextFactory.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(15, 23, 42));
            serialTextFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            modelRowFactory.AppendChild(serialTextFactory);
            itemStackFactory.AppendChild(modelRowFactory);

            var nameTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            nameTextFactory.SetBinding(TextBlock.TextProperty, new Binding("ItemName"));
            nameTextFactory.SetValue(TextBlock.FontSizeProperty, 11.5);
            nameTextFactory.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(100, 116, 139));
            nameTextFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 3, 0, 0));
            nameTextFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            itemStackFactory.AppendChild(nameTextFactory);

            itemBorderFactory.AppendChild(itemStackFactory);
            itemTemplate.VisualTree = itemBorderFactory;
            _modelSuggestionsList.ItemTemplate = itemTemplate;

            var listItemStyle = new Style(typeof(ListBoxItem));
            listItemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
            listItemStyle.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0)));
            listItemStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            var hoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Panel.BackgroundProperty, BrushFromRgb(245, 243, 255)));
            var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Panel.BackgroundProperty, BrushFromRgb(237, 233, 254)));
            listItemStyle.Triggers.Add(hoverTrigger);
            listItemStyle.Triggers.Add(selectedTrigger);
            _modelSuggestionsList.ItemContainerStyle = listItemStyle;

            _modelSuggestionsList.MouseLeftButtonUp += (s, e) => ApplySelectedModelSuggestion();
            _modelSuggestionsList.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    ApplySelectedModelSuggestion();
                }
                else if (e.Key == Key.Escape)
                {
                    HideModelSuggestions();
                    _modelHelperInput.Focus();
                }
            };

            var popupHeader = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(139, 92, 246), Color.FromRgb(109, 40, 217), 0),
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                Padding = new Thickness(12, 8, 12, 8)
            };
            _modelSuggestionsHeaderText = new TextBlock
            {
                Text = "Matching items",
                FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            popupHeader.Child = _modelSuggestionsHeaderText;

            var popupContentStack = new DockPanel();
            DockPanel.SetDock(popupHeader, Dock.Top);
            popupContentStack.Children.Add(popupHeader);
            var listScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _modelSuggestionsList
            };
            popupContentStack.Children.Add(listScroll);

            var popupBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(139, 92, 246),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(10),
                Effect = new DropShadowEffect { BlurRadius = 22, Opacity = 0.28, ShadowDepth = 4, Color = Color.FromRgb(76, 29, 149) },
                Child = popupContentStack
            };
            _modelSuggestionsPopup = new Popup
            {
                PlacementTarget = modelBorder,
                Placement = PlacementMode.Bottom,
                VerticalOffset = 4,
                StaysOpen = false,
                Width = 380,
                Child = popupBorder
            };
            stack.Children.Add(_modelSuggestionsPopup);

            var titleRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleRow.Children.Add(new TextBlock
            {
                Text = "Serial number",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105),
                VerticalAlignment = VerticalAlignment.Center
            });
            var hint = new TextBlock
            {
                Text = "Scan or type — press Enter to resolve",
                FontSize = 10.5, Foreground = BrushFromRgb(148, 163, 184),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(hint, 1);
            titleRow.Children.Add(hint);
            stack.Children.Add(titleRow);

            // Scan input with glow border
            var inputBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 12),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 12,
                    Color = Color.FromArgb(25, 59, 130, 246),
                    ShadowDepth = 0
                }
            };
            inputBorder.GotKeyboardFocus += (s, e) =>
            {
                inputBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                inputBorder.Effect = new DropShadowEffect
                {
                    BlurRadius = 16,
                    Color = Color.FromArgb(45, 59, 130, 246),
                    ShadowDepth = 0
                };
            };
            inputBorder.LostKeyboardFocus += (s, e) =>
            {
                inputBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254));
                inputBorder.Effect = new DropShadowEffect
                {
                    BlurRadius = 12,
                    Color = Color.FromArgb(25, 59, 130, 246),
                    ShadowDepth = 0
                };
            };

            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var scanIcon = new System.Windows.Shapes.Path
            {
                Data = System.Windows.Media.Geometry.Parse(
                    "M3,3 L3,8 M3,3 L8,3 M17,3 L17,8 M17,3 L12,3 M3,17 L3,12 M3,17 L8,17 M17,17 L17,12 M17,17 L12,17 M2,10 L18,10"),
                Stroke = BrushFromRgb(59, 130, 246),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                Stretch = Stretch.Uniform,
                Width = 22,
                Height = 22,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(scanIcon, 0);
            inputGrid.Children.Add(scanIcon);

            _scanInput = new TextBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42),
                CaretBrush = BrushFromRgb(59, 130, 246),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _scanInput.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    ScanSubmitted?.Invoke(this, ScanText);
                }
            };
            Grid.SetColumn(_scanInput, 1);
            inputGrid.Children.Add(_scanInput);

            inputBorder.Child = inputGrid;
            stack.Children.Add(inputBorder);

            // Buttons row — equal-width side-by-side
            var btnGrid = new Grid();
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var resolveBtn = new Button
            {
                Content = "Resolve", Height = 36,
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White, BorderThickness = new Thickness(0),
                Background = new LinearGradientBrush(
                    Color.FromRgb(59, 130, 246), Color.FromRgb(37, 99, 235), 90),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 0, Opacity = 0.18 }
            };
            resolveBtn.Click += (s, e) => ResolveClicked?.Invoke(this, EventArgs.Empty);
            Grid.SetColumn(resolveBtn, 0);
            btnGrid.Children.Add(resolveBtn);

            var browseBtn = new Button
            {
                Content = "Select from List", Height = 36,
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(59, 130, 246), BorderThickness = new Thickness(1.5),
                BorderBrush = BrushFromRgb(191, 219, 254), Background = Brushes.White,
                Cursor = Cursors.Hand
            };
            browseBtn.Click += (s, e) => BrowseItemsClicked?.Invoke(this, EventArgs.Empty);
            Grid.SetColumn(browseBtn, 2);
            btnGrid.Children.Add(browseBtn);

            var addNewBtn = new Button
            {
                Content = "Add New Item", Height = 36,
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(59, 130, 246), BorderThickness = new Thickness(1.5),
                BorderBrush = BrushFromRgb(191, 219, 254), Background = Brushes.White,
                Cursor = Cursors.Hand
            };
            addNewBtn.Click += (s, e) => AddNewItemClicked?.Invoke(this, EventArgs.Empty);
            Grid.SetColumn(addNewBtn, 4);
            btnGrid.Children.Add(addNewBtn);

            stack.Children.Add(btnGrid);
            return stack;
        }

        private Border BuildInfoBanner()
        {
            var border = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var sp = new StackPanel();
            _infoTitle = new TextBlock
            {
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42), TextWrapping = TextWrapping.Wrap
            };
            _infoSubtitle = new TextBlock
            {
                FontSize = 11, Foreground = BrushFromRgb(100, 116, 139),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0)
            };
            sp.Children.Add(_infoTitle);
            sp.Children.Add(_infoSubtitle);
            border.Child = sp;
            return border;
        }

        private Border BuildBorrowCard()
        {
            var card = WpfThemeResources.CreateGlassCard(padding: 0);
            card.Margin = new Thickness(0);
            card.ClipToBounds = true;

            var dock = new DockPanel { LastChildFill = true };
            card.Child = dock;

            // ── Header band ────────────────────────────────────────────────────
            var header = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(37, 99, 235), Color.FromRgb(29, 78, 216), 90),
                Padding = new Thickness(18, 14, 18, 14)
            };
            var headerContent = new StackPanel { Orientation = Orientation.Horizontal };
            var borrowIcon = new System.Windows.Shapes.Path
            {
                Data = System.Windows.Media.Geometry.Parse("M12,2 L12,14 M8,10 L12,14 L16,10 M4,17 L4,19 Q4,20 5,20 L19,20 Q20,20 20,19 L20,17"),
                Stroke = Brushes.White, StrokeThickness = 1.8,
                StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Stretch = Stretch.Uniform, Width = 16, Height = 16,
                Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center
            };
            headerContent.Children.Add(borrowIcon);
            headerContent.Children.Add(new TextBlock
            {
                Text = "Borrow Item", FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center
            });
            header.Child = headerContent;
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            // ── Action button (pinned to bottom) ───────────────────────────────
            _borrowButton = MakeActionButton("Borrow Item", BrushFromRgb(37, 99, 235), BrushFromRgb(29, 78, 216));
            _borrowButton.Click += (s, e) =>
            {
                var deptOnly = _borrowDeptOnlyCheck.IsChecked == true;
                BorrowClicked?.Invoke(this, new BorrowFormEventArgs
                {
                    SerialNumber = ScanText, CompanyId = SelectedCompanyId,
                    DepartmentId = SelectedDepartmentId, BranchId = SelectedBranchId,
                    EmployeeId = deptOnly ? null : SelectedEmployeeId,
                    BorrowForDepartmentOnly = deptOnly,
                    IsBackdate = IsBackdateChecked, BackdateLocal = GetBackdateLocal()
                });
            };
            var btnBorder = new Border { Padding = new Thickness(16, 8, 16, 16) };
            btnBorder.Child = _borrowButton;
            DockPanel.SetDock(btnBorder, Dock.Bottom);
            dock.Children.Add(btnBorder);

            // ── Scrollable fields (fills remaining space) ──────────────────────
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var fields = new StackPanel { Margin = new Thickness(16, 14, 16, 0) };

            _borrowCompanyCombo = MakeCombo();
            _borrowCompanyCombo.SelectionChanged += (s, e) =>
            {
                if (_suppressEvents) return;
                RefreshBorrowDept();
                BorrowCompanyChanged?.Invoke(this, SelectedCompanyId);
            };
            fields.Children.Add(FieldRow("Company", _borrowCompanyCombo));

            _borrowDeptCombo = MakeCombo();
            _borrowDeptCombo.SelectionChanged += (s, e) =>
            {
                if (_suppressEvents) return;
                RefreshBorrowEmp();
                BorrowDepartmentChanged?.Invoke(this, SelectedDepartmentId);
            };
            fields.Children.Add(FieldRow("Department", _borrowDeptCombo));

            _borrowBranchCombo = MakeCombo();
            _borrowBranchCombo.SelectionChanged += (s, e) =>
            {
                if (_suppressEvents) return;
                RefreshBorrowEmp();
                BorrowBranchChanged?.Invoke(this, (_borrowBranchCombo.SelectedItem as IdNamePair)?.Id);
            };
            fields.Children.Add(FieldRow("Branch", _borrowBranchCombo));

            _borrowEmpCombo = MakeEditableCombo();
            _borrowEmpCombo.SelectionChanged += (s, e) =>
                OnEmpSelected(_borrowEmpCombo, _borrowCompanyCombo, _borrowDeptCombo, _borrowBranchCombo,
                              BorrowCompanyChanged, BorrowDepartmentChanged, BorrowBranchChanged);
            fields.Children.Add(FieldRow("Employee", _borrowEmpCombo));
            AttachEmpSearch(_borrowEmpCombo, () => GetEmps(
                (_borrowCompanyCombo.SelectedItem as IdNamePair)?.Id ?? 0,
                (_borrowDeptCombo.SelectedItem as IdNamePair)?.Id ?? 0,
                (_borrowBranchCombo.SelectedItem as IdNamePair)?.Id ?? 0));

            _borrowDeptOnlyCheck = new CheckBox
            {
                Content = "Borrow for whole department (no specific employee)", FontSize = 12,
                Foreground = BrushFromRgb(71, 85, 105), Margin = new Thickness(0, 4, 0, 12)
            };
            _borrowDeptOnlyCheck.Checked += (s, e) =>
            {
                _borrowEmpCombo.IsEnabled = false;
                _borrowEmpCombo.SelectedIndex = -1;
                _borrowEmpCombo.Text = string.Empty;
            };
            _borrowDeptOnlyCheck.Unchecked += (s, e) => _borrowEmpCombo.IsEnabled = true;
            fields.Children.Add(_borrowDeptOnlyCheck);

            var addBorrowLink = new TextBlock
            {
                Text = "Not listed? Add employee", FontSize = 11,
                Foreground = BrushFromRgb(59, 130, 246), Cursor = Cursors.Hand,
                TextDecorations = TextDecorations.Underline, Margin = new Thickness(0, 2, 0, 12)
            };
            addBorrowLink.MouseLeftButtonDown += (s, e) => { e.Handled = true; AddEmployeeClicked?.Invoke(this, true); };
            fields.Children.Add(addBorrowLink);

            var divider = new Border
            {
                Height = 1, Background = BrushFromRgb(226, 232, 240),
                Margin = new Thickness(0, 0, 0, 12)
            };
            fields.Children.Add(divider);

            _backdateCheck = new CheckBox
            {
                Content = "Backdate from logbook", FontSize = 12,
                Foreground = BrushFromRgb(71, 85, 105), Margin = new Thickness(0, 0, 0, 4)
            };
            _backdateCheck.Checked += (s, e) => { _backdateDetails.Visibility = Visibility.Visible; BackdateToggled?.Invoke(this, EventArgs.Empty); };
            _backdateCheck.Unchecked += (s, e) => { _backdateDetails.Visibility = Visibility.Collapsed; BackdateToggled?.Invoke(this, EventArgs.Empty); };
            fields.Children.Add(_backdateCheck);

            _backdateDetails = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 0),
                Visibility = Visibility.Collapsed
            };
            _backdateDatePicker = new DatePicker { FontSize = 12, Width = 120, SelectedDate = DateTime.Today };
            _backdateTimeText = new TextBox
            {
                Text = DateTime.Now.ToString("HH:mm"), FontSize = 12, Width = 60,
                Margin = new Thickness(6, 0, 0, 0), VerticalContentAlignment = VerticalAlignment.Center
            };
            _backdateDetails.Children.Add(_backdateDatePicker);
            _backdateDetails.Children.Add(_backdateTimeText);
            fields.Children.Add(_backdateDetails);

            scroll.Content = fields;
            dock.Children.Add(scroll);
            return card;
        }

        private Border BuildReturnCard()
        {
            var card = WpfThemeResources.CreateGlassCard(padding: 0);
            card.Margin = new Thickness(0);
            card.ClipToBounds = true;

            var dock = new DockPanel { LastChildFill = true };
            card.Child = dock;

            // ── Header band ────────────────────────────────────────────────────
            var header = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(16, 185, 129), Color.FromRgb(5, 150, 105), 90),
                Padding = new Thickness(18, 14, 18, 14)
            };
            var headerContent = new StackPanel { Orientation = Orientation.Horizontal };
            var returnIcon = new System.Windows.Shapes.Path
            {
                Data = System.Windows.Media.Geometry.Parse("M12,14 L12,2 M8,6 L12,2 L16,6 M4,17 L4,19 Q4,20 5,20 L19,20 Q20,20 20,19 L20,17"),
                Stroke = Brushes.White, StrokeThickness = 1.8,
                StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Stretch = Stretch.Uniform, Width = 16, Height = 16,
                Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center
            };
            headerContent.Children.Add(returnIcon);
            headerContent.Children.Add(new TextBlock
            {
                Text = "Return Item", FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center
            });
            header.Child = headerContent;
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            // ── Action button (pinned to bottom) ───────────────────────────────
            _returnButton = MakeActionButton("Return Item", BrushFromRgb(16, 185, 129), BrushFromRgb(5, 150, 105));
            _returnButton.Click += (s, e) =>
            {
                var deptOnly = _returnDeptOnlyCheck.IsChecked == true;
                var emp = _returnEmpCombo.SelectedItem as BorrowEmployeeLookup;
                ReturnClicked?.Invoke(this, new BorrowFormEventArgs
                {
                    SerialNumber = ScanText,
                    CompanyId = (_returnCompanyCombo.SelectedItem as IdNamePair)?.Id,
                    DepartmentId = (_returnDeptCombo.SelectedItem as IdNamePair)?.Id,
                    BranchId = (_returnBranchCombo.SelectedItem as IdNamePair)?.Id,
                    EmployeeId = deptOnly ? null : emp?.EmpId,
                    BorrowForDepartmentOnly = deptOnly
                });
            };
            var btnBorder = new Border { Padding = new Thickness(16, 8, 16, 16) };
            btnBorder.Child = _returnButton;
            DockPanel.SetDock(btnBorder, Dock.Bottom);
            dock.Children.Add(btnBorder);

            // ── Scrollable fields (fills remaining space) ──────────────────────
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var fields = new StackPanel { Margin = new Thickness(16, 14, 16, 0) };

            _returnCompanyCombo = MakeCombo();
            _returnCompanyCombo.SelectionChanged += (s, e) =>
            {
                if (_suppressEvents) return;
                RefreshReturnDept();
                ReturnCompanyChanged?.Invoke(this, (_returnCompanyCombo.SelectedItem as IdNamePair)?.Id);
            };
            fields.Children.Add(FieldRow("Company", _returnCompanyCombo));

            _returnDeptCombo = MakeCombo();
            _returnDeptCombo.SelectionChanged += (s, e) =>
            {
                if (_suppressEvents) return;
                RefreshReturnEmp();
                ReturnDepartmentChanged?.Invoke(this, (_returnDeptCombo.SelectedItem as IdNamePair)?.Id);
            };
            fields.Children.Add(FieldRow("Department", _returnDeptCombo));

            _returnBranchCombo = MakeCombo();
            _returnBranchCombo.SelectionChanged += (s, e) =>
            {
                if (_suppressEvents) return;
                RefreshReturnEmp();
                ReturnBranchChanged?.Invoke(this, (_returnBranchCombo.SelectedItem as IdNamePair)?.Id);
            };
            fields.Children.Add(FieldRow("Branch", _returnBranchCombo));

            _returnEmpCombo = MakeEditableCombo();
            _returnEmpCombo.SelectionChanged += (s, e) =>
                OnEmpSelected(_returnEmpCombo, _returnCompanyCombo, _returnDeptCombo, _returnBranchCombo,
                              ReturnCompanyChanged, ReturnDepartmentChanged, ReturnBranchChanged);
            fields.Children.Add(FieldRow("Employee", _returnEmpCombo));
            AttachEmpSearch(_returnEmpCombo, () => GetEmps(
                (_returnCompanyCombo.SelectedItem as IdNamePair)?.Id ?? 0,
                (_returnDeptCombo.SelectedItem as IdNamePair)?.Id ?? 0,
                (_returnBranchCombo.SelectedItem as IdNamePair)?.Id ?? 0));

            _returnDeptOnlyCheck = new CheckBox
            {
                Content = "Return for department (no specific employee)", FontSize = 12,
                Foreground = BrushFromRgb(71, 85, 105), Margin = new Thickness(0, 4, 0, 12)
            };
            _returnDeptOnlyCheck.Checked += (s, e) =>
            {
                _returnEmpCombo.IsEnabled = false;
                _returnEmpCombo.SelectedIndex = -1;
                _returnEmpCombo.Text = string.Empty;
            };
            _returnDeptOnlyCheck.Unchecked += (s, e) => _returnEmpCombo.IsEnabled = true;
            fields.Children.Add(_returnDeptOnlyCheck);

            var addReturnLink = new TextBlock
            {
                Text = "Not listed? Add employee", FontSize = 11,
                Foreground = BrushFromRgb(16, 185, 129), Cursor = Cursors.Hand,
                TextDecorations = TextDecorations.Underline, Margin = new Thickness(0, 2, 0, 10)
            };
            addReturnLink.MouseLeftButtonDown += (s, e) => { e.Handled = true; AddEmployeeClicked?.Invoke(this, false); };
            fields.Children.Add(addReturnLink);

            scroll.Content = fields;
            dock.Children.Add(scroll);
            return card;
        }

        private static ComboBox MakeCombo() => new ComboBox
        {
            FontSize = 12, Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0)
        };

        private static ComboBox MakeEditableCombo() => new ComboBox
        {
            IsEditable = true, IsTextSearchEnabled = false, DisplayMemberPath = "DisplayText",
            FontSize = 12, Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0)
        };

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) return typed;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void AttachEmpSearch(ComboBox cb, Func<List<BorrowEmployeeLookup>> getBaseList)
        {
            bool skipNext = false;

            cb.Loaded += (s, e) =>
            {
                var textBox = FindVisualChild<TextBox>(cb);
                if (textBox == null) return;

                // When user picks from dropdown, ComboBox updates the TextBox text automatically.
                // Skip the TextChanged that follows so we don't re-filter on DisplayText.
                cb.SelectionChanged += (s2, e2) => skipNext = true;

                textBox.TextChanged += (s2, e2) =>
                {
                    if (skipNext) { skipNext = false; return; }
                    if (_suppressEvents) return;

                    // Keep the raw text exactly as typed. Trimming the TextBox value here
                    // removes a trailing space, making "Mary Ann" become "MaryAnn" while typing.
                    var rawText = textBox.Text ?? string.Empty;
                    var query = rawText.Trim();
                    var baseList = getBaseList() ?? new List<BorrowEmployeeLookup>();

                    if (query.Length < 2)
                    {
                        _suppressEvents = true;
                        cb.ItemsSource = baseList;
                        textBox.Text = rawText;
                        textBox.CaretIndex = textBox.Text.Length;
                        _suppressEvents = false;
                        return;
                    }

                    var tokens = query.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                    var filtered = baseList
                        .Where(x =>
                        {
                            var searchable = string.Join(" ",
                                x.EmployeeName ?? string.Empty,
                                x.DisplayText ?? string.Empty,
                                x.DepartmentName ?? string.Empty,
                                x.BranchName ?? string.Empty);
                            return tokens.All(token => searchable.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0);
                        })
                        .OrderBy(x => x.EmployeeName)
                        .ThenBy(x => x.DepartmentName)
                        .ThenBy(x => x.BranchName)
                        .ToList();

                    _suppressEvents = true;
                    cb.ItemsSource = filtered;
                    if (filtered.Count > 0) cb.IsDropDownOpen = true;
                    textBox.Text = rawText;
                    textBox.CaretIndex = textBox.Text.Length;
                    _suppressEvents = false;
                };
            };
        }

        private void OnEmpSelected(ComboBox empCombo, ComboBox comCombo, ComboBox deptCombo, ComboBox branchCombo,
                                   EventHandler<int?> comEvent, EventHandler<int?> deptEvent, EventHandler<int?> branchEvent)
        {
            if (_suppressEvents) return;
            var emp = empCombo.SelectedItem as BorrowEmployeeLookup;
            if (emp == null || emp.EmpId <= 0) return;

            _suppressEvents = true;

            var companies = comCombo.ItemsSource as List<IdNamePair>;
            var cIdx = companies?.FindIndex(c => c.Id == emp.ComId) ?? -1;
            if (cIdx >= 0) comCombo.SelectedIndex = cIdx;

            var depts = GetDepts(emp.ComId);
            deptCombo.ItemsSource = depts;
            var dIdx = depts.FindIndex(d => d.Id == emp.DeptId);
            deptCombo.SelectedIndex = dIdx;

            var branches = GetBranches(emp.ComId);
            branchCombo.ItemsSource = branches;
            var bIdx = branches.FindIndex(b => b.Id == emp.BranchId);
            branchCombo.SelectedIndex = bIdx;

            var emps = GetEmps(emp.ComId, emp.DeptId, emp.BranchId);
            empCombo.ItemsSource = emps;
            var eIdx = emps.FindIndex(x => x.EmpId == emp.EmpId);
            if (eIdx >= 0) empCombo.SelectedIndex = eIdx;

            _suppressEvents = false;

            comEvent?.Invoke(this, emp.ComId);
            deptEvent?.Invoke(this, emp.DeptId > 0 ? emp.DeptId : (int?)null);
            branchEvent?.Invoke(this, emp.BranchId > 0 ? emp.BranchId : (int?)null);
        }

        private static Button MakeActionButton(string text, SolidColorBrush from, SolidColorBrush to) => new Button
        {
            Content = text, Height = 42, FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = Brushes.White, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, Margin = new Thickness(0, 6, 0, 0),
            Background = new LinearGradientBrush(from.Color, to.Color, 90),
            Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 0, Opacity = 0.2 }
        };

        private static UIElement FieldRow(string label, Control input)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            sp.Children.Add(new TextBlock
            {
                Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105), Margin = new Thickness(0, 0, 0, 3)
            });
            sp.Children.Add(input);
            return sp;
        }

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
            => new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}
