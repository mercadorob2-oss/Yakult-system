using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Models.BorrowItems;
using Yakult.Inventory.App.Wpf.CallMonitoring;

namespace Yakult.Inventory.App.Wpf.BorrowItems
{
    public enum BoardGroupBy
    {
        Age,
        Department,
        Company,
        Employee
    }

    public sealed class WpfBorrowBoardWorkspace : UserControl
    {
        public event EventHandler<BorrowLogRow> ReturnBorrowClicked;
        public event EventHandler<BorrowLogRow> BorrowRowDoubleClicked;

        private readonly TextBox _searchBox;
        private readonly Button _btnAge, _btnDept, _btnCompany, _btnEmp;
        private readonly ScrollViewer _columnScroll;
        private List<BorrowLogRow> _items = new List<BorrowLogRow>();
        private BoardGroupBy _currentGroupBy = BoardGroupBy.Age;
        private string _searchText = string.Empty;

        public WpfBorrowBoardWorkspace()
        {
            Background = BrushFromRgb(241, 244, 247);
            Resources.MergedDictionaries.Add(WpfThemeResources.GetScrollBarStyle());

            var root = new Grid { Margin = new Thickness(20, 12, 20, 16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var topBar = BuildTopBar(out _btnAge, out _btnDept, out _btnCompany, out _btnEmp, out _searchBox);
            Grid.SetRow(topBar, 0);
            root.Children.Add(topBar);

            _columnScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(_columnScroll, 1);
            root.Children.Add(_columnScroll);

            Content = root;

            SetGroupBy(BoardGroupBy.Age);
        }

        public void BindData(List<BorrowLogRow> items)
        {
            _items = items ?? new List<BorrowLogRow>();
            RebuildColumns();
        }

        public void RefreshElapsed()
        {
            if (!(_columnScroll.Content is DependencyObject root)) return;
            foreach (var el in FindVisualChildren<TextBlock>(root))
            {
                if (el.Tag is BorrowLogRow row && string.Equals(el.Name, "BrdElapsed", StringComparison.Ordinal))
                    el.Text = GetElapsedText(row);
            }
        }

        private UIElement BuildTopBar(out Button btnAge, out Button btnDept, out Button btnCompany, out Button btnEmp, out TextBox searchBox)
        {
            var bar = new Border
            {
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 8, 12, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var groupStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var label = new TextBlock
            {
                Text = "Group by:",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(100, 116, 139),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            groupStack.Children.Add(label);

            btnAge = CreateGroupButton("Age");
            btnDept = CreateGroupButton("Department");
            btnCompany = CreateGroupButton("Company");
            btnEmp = CreateGroupButton("Employee");

            btnAge.Click += (_, __) => SetGroupBy(BoardGroupBy.Age);
            btnDept.Click += (_, __) => SetGroupBy(BoardGroupBy.Department);
            btnCompany.Click += (_, __) => SetGroupBy(BoardGroupBy.Company);
            btnEmp.Click += (_, __) => SetGroupBy(BoardGroupBy.Employee);

            groupStack.Children.Add(btnAge);
            groupStack.Children.Add(btnDept);
            groupStack.Children.Add(btnCompany);
            groupStack.Children.Add(btnEmp);
            Grid.SetColumn(groupStack, 0);
            grid.Children.Add(groupStack);

            var searchBorder = new Border
            {
                Background = BrushFromRgb(248, 250, 252),
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Height = 32,
                Width = 240,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var searchBoxLocal = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            searchBox = searchBoxLocal;
            searchBoxLocal.TextChanged += (_, __) =>
            {
                _searchText = (searchBoxLocal.Text ?? string.Empty).Trim().ToLowerInvariant();
                RebuildColumns();
            };
            searchBorder.Child = searchBoxLocal;
            Grid.SetColumn(searchBorder, 1);
            grid.Children.Add(searchBorder);

            var refreshBtn = new Button
            {
                Content = "↻",
                Width = 32,
                Height = 32,
                FontSize = 18,
                Background = Brushes.Transparent,
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                Foreground = BrushFromRgb(71, 85, 105),
                Cursor = Cursors.Hand,
                ToolTip = "Refresh board"
            };
            refreshBtn.Click += (_, __) => RebuildColumns();
            Grid.SetColumn(refreshBtn, 2);
            grid.Children.Add(refreshBtn);

            bar.Child = grid;
            return bar;
        }

        private void SetGroupBy(BoardGroupBy mode)
        {
            _currentGroupBy = mode;
            RebuildColumns();
            UpdateGroupButtonStates();
        }

        private void UpdateGroupButtonStates()
        {
            void SetActive(Button btn, bool active)
            {
                btn.Background = active ? BrushFromRgb(14, 165, 233) : Brushes.Transparent;
                btn.Foreground = active ? Brushes.White : BrushFromRgb(71, 85, 105);
                btn.BorderBrush = active ? BrushFromRgb(14, 165, 233) : BrushFromRgb(210, 219, 230);
                btn.FontWeight = active ? FontWeights.Bold : FontWeights.SemiBold;
            }
            SetActive(_btnAge, _currentGroupBy == BoardGroupBy.Age);
            SetActive(_btnDept, _currentGroupBy == BoardGroupBy.Department);
            SetActive(_btnCompany, _currentGroupBy == BoardGroupBy.Company);
            SetActive(_btnEmp, _currentGroupBy == BoardGroupBy.Employee);
        }

        private UIElement BuildEmptyMessage()
        {
            return new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_searchText) ? "No open borrows to display." : "No borrows match your search.",
                FontSize = 15,
                Foreground = BrushFromRgb(148, 163, 184),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(60, 80, 60, 80),
                TextWrapping = TextWrapping.Wrap
            };
        }

        private void RebuildColumns()
        {
            var filtered = string.IsNullOrWhiteSpace(_searchText)
                ? _items
                : _items.Where(i =>
                    (i.SerialNumber ?? "").ToLowerInvariant().Contains(_searchText) ||
                    (i.ItemName ?? "").ToLowerInvariant().Contains(_searchText) ||
                    (i.BorrowedByEmpName ?? "").ToLowerInvariant().Contains(_searchText) ||
                    (i.BorrowedByDeptName ?? "").ToLowerInvariant().Contains(_searchText) ||
                    (i.ModelNumber ?? "").ToLowerInvariant().Contains(_searchText)
                ).ToList();

            var groups = GroupItems(filtered, _currentGroupBy);

            if (_currentGroupBy == BoardGroupBy.Age)
            {
                _columnScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                _columnScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                var ageGrid = new UniformGrid { Rows = 1, Columns = 4, Margin = new Thickness(0, 8, 0, 0) };
                var groupDict = groups.ToDictionary(kv => kv.Key, kv => kv.Value);
                foreach (var key in new[] { "Today", "1-3 Days", "4-7 Days", "8+ Days" })
                {
                    var rowList = groupDict.ContainsKey(key) ? groupDict[key] : new List<BorrowLogRow>();
                    ageGrid.Children.Add(BuildColumn(key, rowList));
                }
                _columnScroll.Content = ageGrid;
            }
            else if (_currentGroupBy == BoardGroupBy.Employee)
            {
                _columnScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                _columnScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                _columnScroll.Content = groups.Count == 0 ? BuildEmptyMessage() : BuildEmployeeLayout(groups);
            }
            else
            {
                _columnScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                _columnScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                _columnScroll.Content = groups.Count == 0 ? BuildEmptyMessage() : BuildDeptCompanyLayout(groups);
            }
        }

        // ── Department / Company layout ────────────────────────────────────────
        private UIElement BuildDeptCompanyLayout(List<KeyValuePair<string, List<BorrowLogRow>>> groups)
        {
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
            foreach (var g in groups)
                wrap.Children.Add(BuildDeptCard(g.Key, g.Value));
            return wrap;
        }

        private UIElement BuildDeptCard(string groupName, List<BorrowLogRow> rows)
        {
            var card = new Border
            {
                Width = 420,
                Margin = new Thickness(0, 0, 16, 16),
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10)
            };

            var inner = new Grid();
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(220, GridUnitType.Pixel) });

            // Header
            var headerBorder = new Border
            {
                Background = BrushFromRgb(30, 41, 59),
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                Padding = new Thickness(16, 12, 16, 12)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var iconBorder = new Border
            {
                Width = 32, Height = 32,
                CornerRadius = new CornerRadius(8),
                Background = BrushFromRgb(51, 65, 85),
                Margin = new Thickness(0, 0, 10, 0),
                Child = new TextBlock
                {
                    Text = groupName.Length > 0 ? groupName[0].ToString().ToUpper() : "?",
                    FontSize = 14, FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            nameStack.Children.Add(iconBorder);
            nameStack.Children.Add(new TextBlock
            {
                Text = groupName, FontSize = 14, FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(nameStack, 0);
            headerGrid.Children.Add(nameStack);

            var countBadge = new Border
            {
                CornerRadius = new CornerRadius(999),
                Background = BrushFromRgb(14, 165, 233),
                Padding = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = $"{rows.Count} item{(rows.Count != 1 ? "s" : "")}",
                    FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White
                }
            };
            Grid.SetColumn(countBadge, 1);
            headerGrid.Children.Add(countBadge);
            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            inner.Children.Add(headerBorder);

            // Item list
            var itemStack = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };
            foreach (var row in rows)
                itemStack.Children.Add(BuildDeptCardRow(row));

            var itemScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = itemStack
            };
            Grid.SetRow(itemScroll, 1);
            inner.Children.Add(itemScroll);

            card.Child = inner;
            return card;
        }

        private UIElement BuildDeptCardRow(BorrowLogRow row)
        {
            var accentColor = GetAccentColor(row);
            var rowBorder = new Border
            {
                Tag = row,
                Background = BrushFromRgb(248, 250, 252),
                BorderBrush = BrushFromRgb(241, 245, 249),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(12, 7, 12, 7),
                Cursor = Cursors.Hand
            };
            rowBorder.MouseEnter += (_, __) => rowBorder.Background = BrushFromRgb(241, 245, 249);
            rowBorder.MouseLeave += (_, __) => rowBorder.Background = BrushFromRgb(248, 250, 252);
            rowBorder.MouseDown += (s, e) =>
            {
                if (e.ClickCount == 2 && s is Border b && b.Tag is BorrowLogRow r)
                    BorrowRowDoubleClicked?.Invoke(this, r);
            };

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoStack = new StackPanel();
            infoStack.Children.Add(new TextBlock
            {
                Text = row.SerialNumber ?? "--",
                FontSize = 11.5, FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(15, 23, 42),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = row.ItemDisplay ?? "--",
                FontSize = 10.5, Foreground = BrushFromRgb(100, 116, 139),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Grid.SetColumn(infoStack, 0);
            g.Children.Add(infoStack);

            var badge = new Border
            {
                Name = "BrdElapsed", Tag = row,
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(8, 2, 8, 2),
                Background = new SolidColorBrush(Color.FromArgb(30, accentColor.R, accentColor.G, accentColor.B)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Child = new TextBlock
                {
                    Name = "BrdElapsed", Tag = row,
                    Text = GetElapsedText(row),
                    FontSize = 10, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(accentColor)
                }
            };
            Grid.SetColumn(badge, 1);
            g.Children.Add(badge);

            rowBorder.Child = g;
            return rowBorder;
        }

        // ── Employee mosaic layout ─────────────────────────────────────────────
        private UIElement BuildEmployeeLayout(List<KeyValuePair<string, List<BorrowLogRow>>> groups)
        {
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
            foreach (var g in groups)
                wrap.Children.Add(BuildEmployeeTile(g.Key, g.Value));
            return wrap;
        }

        private UIElement BuildEmployeeTile(string empName, List<BorrowLogRow> rows)
        {
            var oldestAge = rows.Count > 0 ? rows.Max(r => GetAgeDays(r)) : 0;
            var accentColor = oldestAge < 1 ? Color.FromRgb(52, 152, 219)
                            : oldestAge < 4 ? Color.FromRgb(46, 204, 113)
                            : oldestAge < 8 ? Color.FromRgb(243, 156, 18)
                            : Color.FromRgb(231, 76, 60);
            var deptName = rows.Count > 0 ? (rows[0].BorrowedByDeptName ?? "") : "";

            var tile = new Border
            {
                Width = 210,
                Margin = new Thickness(0, 0, 16, 16),
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Cursor = Cursors.Hand
            };
            tile.MouseEnter += (_, __) => tile.Background = BrushFromRgb(248, 250, 252);
            tile.MouseLeave += (_, __) => tile.Background = Brushes.White;
            tile.MouseDown += (s, e) =>
            {
                if (e.ClickCount == 2 && rows.Count > 0)
                    BorrowRowDoubleClicked?.Invoke(this, rows[0]);
            };

            var inner = new Grid();
            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6, GridUnitType.Pixel) });
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Accent strip
            var strip = new Border
            {
                Background = new SolidColorBrush(accentColor),
                CornerRadius = new CornerRadius(10, 10, 0, 0)
            };
            Grid.SetRow(strip, 0);
            inner.Children.Add(strip);

            // Avatar + Name
            var avatarSection = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(12, 16, 12, 8)
            };
            var initials = GetInitials(empName);
            var avatarCircle = new Border
            {
                Width = 58, Height = 58,
                CornerRadius = new CornerRadius(29),
                Background = new SolidColorBrush(accentColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
                Child = new TextBlock
                {
                    Text = initials, FontSize = 22, FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            avatarSection.Children.Add(avatarCircle);
            avatarSection.Children.Add(new TextBlock
            {
                Text = empName, FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(15, 23, 42),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
            if (!string.IsNullOrWhiteSpace(deptName))
            {
                avatarSection.Children.Add(new TextBlock
                {
                    Text = deptName, FontSize = 11,
                    Foreground = BrushFromRgb(100, 116, 139),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            Grid.SetRow(avatarSection, 1);
            inner.Children.Add(avatarSection);

            // Item count badge
            var countBadge = new Border
            {
                CornerRadius = new CornerRadius(999),
                Background = new SolidColorBrush(Color.FromArgb(30, accentColor.R, accentColor.G, accentColor.B)),
                Padding = new Thickness(12, 4, 12, 4),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
                Child = new TextBlock
                {
                    Text = $"{rows.Count} item{(rows.Count != 1 ? "s" : "")} borrowed",
                    FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(accentColor)
                }
            };
            Grid.SetRow(countBadge, 2);
            inner.Children.Add(countBadge);

            // Mini item list
            var itemStack = new StackPanel { Margin = new Thickness(10, 0, 10, 12) };
            itemStack.Children.Add(new Border
            {
                Height = 1,
                Background = BrushFromRgb(241, 245, 249),
                Margin = new Thickness(0, 0, 0, 8)
            });
            foreach (var row in rows.Take(3))
            {
                var rowAccentColor = GetAccentColor(row);
                var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                rowGrid.Children.Add(new TextBlock
                {
                    Text = row.ItemName ?? "--",
                    FontSize = 10.5, Foreground = BrushFromRgb(71, 85, 105),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                });
                var miniElapsed = new TextBlock
                {
                    Name = "BrdElapsed", Tag = row,
                    Text = GetElapsedText(row),
                    FontSize = 9.5, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(rowAccentColor),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(miniElapsed, 1);
                rowGrid.Children.Add(miniElapsed);
                itemStack.Children.Add(rowGrid);
            }
            if (rows.Count > 3)
            {
                itemStack.Children.Add(new TextBlock
                {
                    Text = $"+{rows.Count - 3} more",
                    FontSize = 10, Foreground = BrushFromRgb(148, 163, 184),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            Grid.SetRow(itemStack, 3);
            inner.Children.Add(itemStack);

            tile.Child = inner;
            return tile;
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0][0].ToString().ToUpper();
            return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
        }

        private List<KeyValuePair<string, List<BorrowLogRow>>> GroupItems(List<BorrowLogRow> items, BoardGroupBy groupBy)
        {
            var groups = new Dictionary<string, List<BorrowLogRow>>();
            Action<string, BorrowLogRow> add = (key, item) =>
            {
                if (!groups.ContainsKey(key)) groups[key] = new List<BorrowLogRow>();
                groups[key].Add(item);
            };

            switch (groupBy)
            {
                case BoardGroupBy.Age:
                    foreach (var item in items)
                    {
                        var age = GetAgeDays(item);
                        var key = age < 1 ? "Today" : age < 4 ? "1-3 Days" : age < 8 ? "4-7 Days" : "8+ Days";
                        add(key, item);
                    }
                    return new[] { "Today", "1-3 Days", "4-7 Days", "8+ Days" }
                        .Where(k => groups.ContainsKey(k))
                        .Select(k => new KeyValuePair<string, List<BorrowLogRow>>(k, groups[k]))
                        .ToList();

                case BoardGroupBy.Department:
                    foreach (var item in items)
                    {
                        var key = (item.BorrowedByDeptName ?? "Unknown").Trim();
                        if (key.Length == 0) key = "Unknown";
                        add(key, item);
                    }
                    break;

                case BoardGroupBy.Company:
                    foreach (var item in items)
                    {
                        var key = (item.BorrowedByDeptName ?? "Unknown").Trim();
                        if (key.Length == 0) key = "Unknown";
                        add(key, item);
                    }
                    break;

                case BoardGroupBy.Employee:
                    foreach (var item in items)
                    {
                        var key = (item.BorrowedByEmpName ?? "Unknown").Trim();
                        if (key.Length == 0) key = "Unknown";
                        add(key, item);
                    }
                    break;
            }

            return groups.OrderBy(g => g.Key)
                .Select(g => new KeyValuePair<string, List<BorrowLogRow>>(g.Key, g.Value))
                .ToList();
        }

        private static double GetAgeDays(BorrowLogRow row)
        {
            if (row == null || row.BorrowedAtUtc == default) return 0;
            return (DateTime.UtcNow - row.BorrowedAtUtc).TotalDays;
        }

        private string GetElapsedText(BorrowLogRow row)
        {
            if (row == null || row.BorrowedAtUtc == default) return "0m";
            var span = DateTime.UtcNow - row.BorrowedAtUtc;
            if (span.TotalSeconds < 0) span = TimeSpan.Zero;
            if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d {span.Hours:D2}h {span.Minutes:D2}m";
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes:D2}m";
            return $"{Math.Max(0, (int)span.TotalMinutes)}m";
        }

        private Color GetAccentColor(BorrowLogRow row)
        {
            var age = GetAgeDays(row);
            if (age < 1) return Color.FromRgb(52, 152, 219);
            if (age < 4) return Color.FromRgb(46, 204, 113);
            if (age < 8) return Color.FromRgb(243, 156, 18);
            return Color.FromRgb(231, 76, 60);
        }

        private UIElement BuildColumn(string headerText, List<BorrowLogRow> rows)
        {
            var column = new Border
            {
                Margin = new Thickness(0, 0, 10, 0),
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10)
            };

            var inner = new Grid { Margin = new Thickness(0) };
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var headerBrush = new SolidColorBrush(GetColumnAccent(_currentGroupBy, headerText));
            var headerBar = new Border
            {
                Background = headerBrush,
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                Padding = new Thickness(14, 10, 14, 10)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerLabel = new TextBlock
            {
                Text = $"{headerText}  ({rows.Count})",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(headerLabel, 0);
            headerGrid.Children.Add(headerLabel);

            headerBar.Child = headerGrid;
            Grid.SetRow(headerBar, 0);
            inner.Children.Add(headerBar);

            var cardScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(8)
            };
            var cardStack = new StackPanel { Orientation = Orientation.Vertical };

            foreach (var row in rows)
            {
                var card = BuildCard(row);
                cardStack.Children.Add(card);
            }

            if (rows.Count == 0)
            {
                cardStack.Children.Add(new TextBlock
                {
                    Text = "No items",
                    FontSize = 12,
                    Foreground = BrushFromRgb(148, 163, 184),
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 20)
                });
            }

            cardScroll.Content = cardStack;
            Grid.SetRow(cardScroll, 1);
            inner.Children.Add(cardScroll);

            column.Child = inner;
            return column;
        }

        private Color GetColumnAccent(BoardGroupBy groupBy, string header)
        {
            if (groupBy == BoardGroupBy.Age)
            {
                if (header == "Today") return Color.FromRgb(52, 152, 219);
                if (header == "1-3 Days") return Color.FromRgb(46, 204, 113);
                if (header == "4-7 Days") return Color.FromRgb(243, 156, 18);
                if (header == "8+ Days") return Color.FromRgb(231, 76, 60);
            }
            return Color.FromRgb(100, 116, 139);
        }

        private Border BuildCard(BorrowLogRow row)
        {
            var accentColor = GetAccentColor(row);
            var card = new Border
            {
                Tag = row,
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(233, 236, 241),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };
            card.MouseEnter += (_, __) => card.Background = BrushFromRgb(248, 250, 252);
            card.MouseLeave += (_, __) => card.Background = Brushes.White;

            var grid = new Grid { Margin = new Thickness(0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var accent = new Border
            {
                Background = new SolidColorBrush(accentColor),
                CornerRadius = new CornerRadius(8, 0, 0, 8),
                Width = 4,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(accent, 0);
            grid.Children.Add(accent);

            var body = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(10, 8, 10, 8)
            };

            var topRow = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var serialText = new TextBlock
            {
                Text = row.SerialNumber ?? "--",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(15, 23, 42),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(serialText, 0);
            topRow.Children.Add(serialText);

            var elapsedBadge = new Border
            {
                Name = "BrdElapsed",
                Tag = row,
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(8, 2, 8, 2),
                Background = new SolidColorBrush(Color.FromArgb(30, accentColor.R, accentColor.G, accentColor.B)),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var elapsedText = new TextBlock
            {
                Name = "BrdElapsed",
                Tag = row,
                Text = GetElapsedText(row),
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(accentColor)
            };
            elapsedBadge.Child = elapsedText;
            Grid.SetColumn(elapsedBadge, 1);
            topRow.Children.Add(elapsedBadge);

            body.Children.Add(topRow);

            var itemText = new TextBlock
            {
                Text = row.ItemDisplay ?? "Item not available",
                FontSize = 11.5,
                Foreground = BrushFromRgb(71, 85, 105),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 2)
            };
            body.Children.Add(itemText);

            var deptText = new TextBlock
            {
                Text = $"{row.BorrowedByEmpName ?? "Unknown"}  ·  {row.BorrowedByDeptName ?? "Unassigned"}",
                FontSize = 10.5,
                Foreground = BrushFromRgb(148, 163, 184),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 6)
            };
            body.Children.Add(deptText);

            var returnBorder = new Border
            {
                Tag = row,
                Height = 26,
                Background = new SolidColorBrush(Color.FromRgb(20, 150, 125)),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                Padding = new Thickness(8, 0, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            returnBorder.Child = new TextBlock
            {
                Text = "↩ Return",
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            returnBorder.MouseLeftButtonDown += (s, e) =>
            {
                if (((Border)s).Tag is BorrowLogRow r)
                    ReturnBorrowClicked?.Invoke(this, r);
            };

            body.Children.Add(returnBorder);

            card.MouseDown += (s, e) =>
            {
                if (e.ClickCount == 2 && s is Border b && b.Tag is BorrowLogRow r)
                    BorrowRowDoubleClicked?.Invoke(this, r);
            };

            Grid.SetColumn(body, 1);
            grid.Children.Add(body);
            card.Child = grid;

            return card;
        }

        private static Button CreateGroupButton(string text)
        {
            return new Button
            {
                Content = text,
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(1),
                BorderBrush = BrushFromRgb(210, 219, 230),
                Foreground = BrushFromRgb(71, 85, 105),
                Background = Brushes.Transparent
            };
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) yield return t;
                foreach (var nested in FindVisualChildren<T>(child))
                    yield return nested;
            }
        }

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
            => new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}
