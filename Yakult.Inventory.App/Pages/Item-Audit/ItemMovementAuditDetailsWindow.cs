using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Newtonsoft.Json;
using Yakult.Inventory.App.Forms.BorrowItems;
using Yakult.Inventory.App.Pages.Inventory;
using Yakult.Inventory.App.Pages.Request;
using Yakult.Inventory.App.Pages.Set;
using Yakult.Inventory.App.Repositories;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.ItemAudit
{
    public sealed class ItemMovementAuditDetailsWindow : Window
    {
        private readonly ItemMovementAuditDto _dto;
        private readonly Func<string, Task> _openHistory;
        private readonly ItemMovementAuditRepository _repo = new ItemMovementAuditRepository();

        private readonly List<ItemMovementAuditDto> _fullHistory = new List<ItemMovementAuditDto>();
        private DataGrid _timelineGrid;
        private TextBlock _timelineStatus;
        private TextBox _timelineSearch;
        private ComboBox _categoryFilter;
        private ComboBox _sourceFilter;
        private CheckBox _changesOnly;

        private static readonly Brush Primary = BrushFromRgb(30, 111, 184);
        private static readonly Brush PrimarySoft = BrushFromRgb(232, 244, 255);
        private static readonly Brush TextMain = BrushFromRgb(31, 41, 55);
        private static readonly Brush TextMuted = BrushFromRgb(100, 116, 139);
        private static readonly Brush Border = BrushFromRgb(226, 232, 240);
        private static readonly Brush Surface = BrushFromRgb(248, 250, 252);
        private static readonly Brush Success = BrushFromRgb(34, 197, 94);
        private static readonly Brush Danger = BrushFromRgb(220, 38, 38);

        public ItemMovementAuditDetailsWindow(ItemMovementAuditDto dto, Func<string, Task> openHistory = null)
        {
            _dto = dto ?? new ItemMovementAuditDto();
            _openHistory = openHistory;

            Title = "Movement Details";
            Width = 1100;
            Height = 760;
            MinWidth = 900;
            MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White;
            FontFamily = new FontFamily("Segoe UI");

            Content = BuildUi();
            Loaded += async (_, __) => await LoadTimelineAsync();
        }

        private UIElement BuildUi()
        {
            var root = new DockPanel();

            root.Children.Add(BuildHeader());

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(22)
            };
            DockPanel.SetDock(scroll, Dock.Top);

            var body = new StackPanel { Orientation = Orientation.Vertical };
            scroll.Content = body;

            body.Children.Add(BuildSummaryCards());
            body.Children.Add(BuildOverviewGrid());
            body.Children.Add(BuildTabs());

            root.Children.Add(scroll);
            return root;
        }

        private UIElement BuildHeader()
        {
            var header = new Grid
            {
                Background = Brushes.White,
                MinHeight = 82,
                Margin = new Thickness(22, 16, 22, 14)
            };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            DockPanel.SetDock(header, Dock.Top);

            var titleStack = new StackPanel { Orientation = Orientation.Vertical };
            titleStack.Children.Add(new TextBlock
            {
                Text = "Movement Details",
                Foreground = TextMain,
                FontSize = 21,
                FontWeight = FontWeights.SemiBold
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = BuildSubtitleLine1(_dto),
                Foreground = TextMuted,
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = BuildSubtitleLine2(_dto),
                Foreground = TextMuted,
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            Grid.SetColumn(titleStack, 0);
            header.Children.Add(titleStack);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top
            };

            var openButton = MakeButton("Open", Brushes.White, TextMain);
            openButton.ContextMenu = BuildOpenMenu();
            openButton.Click += (_, __) => openButton.ContextMenu.IsOpen = true;
            actions.Children.Add(openButton);

            var close = MakeButton("Close", Primary, Brushes.White);
            close.Margin = new Thickness(8, 0, 0, 0);
            close.Click += (_, __) => Close();
            actions.Children.Add(close);

            Grid.SetColumn(actions, 1);
            header.Children.Add(actions);

            header.Children.Add(new Border
            {
                BorderBrush = Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                VerticalAlignment = VerticalAlignment.Bottom
            });

            return header;
        }

        private ContextMenu BuildOpenMenu()
        {
            var menu = new ContextMenu();

            var insights = new MenuItem { Header = "Insights", IsEnabled = !string.IsNullOrWhiteSpace(_dto.SerialNumber) };
            insights.Click += (_, __) => OpenInsights();
            menu.Items.Add(insights);

            var history = new MenuItem { Header = "View Full History", IsEnabled = _openHistory != null && !string.IsNullOrWhiteSpace(_dto.SerialNumber) };
            history.Click += async (_, __) => await OpenFullHistoryAsync();
            menu.Items.Add(history);

            menu.Items.Add(new Separator());

            var set = new MenuItem { Header = "Open Set Details", IsEnabled = _dto.SetId.HasValue && _dto.SetId.Value > 0 };
            set.Click += (_, __) => OpenSetDetails(_dto.SetId);
            menu.Items.Add(set);

            var reference = new MenuItem { Header = "Open Reference", IsEnabled = !string.IsNullOrWhiteSpace(_dto.ReferenceType) && _dto.ReferenceId.HasValue };
            reference.Click += async (_, __) => await OpenReferenceAsync(_dto);
            menu.Items.Add(reference);

            menu.Items.Add(new Separator());

            var copyIds = new MenuItem { Header = "Copy IDs" };
            copyIds.Click += (_, __) => Clipboard.SetText(BuildIdsClipboardText(_dto));
            menu.Items.Add(copyIds);

            var copyJson = new MenuItem { Header = "Copy Details JSON" };
            copyJson.Click += (_, __) => Clipboard.SetText(JsonConvert.SerializeObject(_dto, Formatting.Indented));
            menu.Items.Add(copyJson);

            return menu;
        }

        private UIElement BuildSummaryCards()
        {
            var grid = new UniformGrid
            {
                Columns = 4,
                Margin = new Thickness(0, 0, 0, 16)
            };

            grid.Children.Add(BuildMetric("Direction", EmptyDash(_dto.Direction), IsOut(_dto.Direction) ? Danger : Success));
            grid.Children.Add(BuildMetric("Type", EmptyDash(_dto.MovementType), Primary));
            grid.Children.Add(BuildMetric("Source", EmptyDash(_dto.Source), BrushFromRgb(99, 102, 241)));
            grid.Children.Add(BuildMetric("Quantity", _dto.Quantity.ToString(), BrushFromRgb(15, 118, 110)));

            return grid;
        }

        private UIElement BuildMetric(string label, string value, Brush accent)
        {
            var card = Card();
            card.Margin = new Thickness(0, 0, 10, 0);
            card.Padding = new Thickness(14);

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = TextMuted,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            });
            stack.Children.Add(new TextBlock
            {
                Text = value,
                Foreground = accent,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 7, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            card.Child = stack;
            return card;
        }

        private UIElement BuildOverviewGrid()
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var left = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            left.Children.Add(BuildSection("Item Information",
                Pair("Serial Number", _dto.SerialNumber),
                Pair("Item Name", _dto.ItemName),
                Pair("Model Number", _dto.ModelNumber),
                Pair("Set Code", _dto.SetCode)));

            left.Children.Add(BuildSection("Movement Classification",
                Pair("Category", _dto.MovementCategory),
                Pair("Type", _dto.MovementType),
                Pair("Priority", _dto.MovementPriority),
                Pair("Status Before", _dto.StatusBefore),
                Pair("Status After", _dto.StatusAfter)));

            var right = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            right.Children.Add(BuildSection("Item Location",
                Pair("Company", _dto.CompanyName),
                Pair("Branch", _dto.BranchName),
                Pair("Department", _dto.DepartmentName),
                Pair("Employee", _dto.EmployeeName),
                Pair("User", _dto.UserName)));

            right.Children.Add(BuildSection("Reference",
                Pair("Reference Type", _dto.ReferenceType),
                Pair("Reference ID", _dto.ReferenceId?.ToString()),
                Pair("Item ID", _dto.ItemId?.ToString()),
                Pair("Set ID", _dto.SetId?.ToString()),
                Pair("Audit Location", _dto.AuditLocation)));

            Grid.SetColumn(left, 0);
            Grid.SetColumn(right, 1);
            grid.Children.Add(left);
            grid.Children.Add(right);
            return grid;
        }

        private UIElement BuildTabs()
        {
            var tabs = new TabControl { Margin = new Thickness(0, 0, 0, 8) };

            var timelineTab = new TabItem { Header = "Timeline" };
            timelineTab.Content = BuildTimelineTab();
            tabs.Items.Add(timelineTab);

            var notesTab = new TabItem { Header = "Notes" };
            notesTab.Content = BuildNotesTab();
            tabs.Items.Add(notesTab);

            var rawTab = new TabItem { Header = "Raw" };
            rawTab.Content = BuildRawTab();
            tabs.Items.Add(rawTab);

            return tabs;
        }

        private UIElement BuildTimelineTab()
        {
            var root = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };

            var filter = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(filter, Dock.Top);

            _timelineSearch = new TextBox { Width = 240, Height = 30, Padding = new Thickness(8, 4, 8, 4), VerticalContentAlignment = VerticalAlignment.Center };
            _timelineSearch.TextChanged += (_, __) => ApplyTimelineFilters();
            filter.Children.Add(LabeledControl("Search", _timelineSearch));

            _categoryFilter = new ComboBox { Width = 160, Height = 30 };
            _categoryFilter.SelectionChanged += (_, __) => ApplyTimelineFilters();
            filter.Children.Add(LabeledControl("Category", _categoryFilter));

            _sourceFilter = new ComboBox { Width = 150, Height = 30 };
            _sourceFilter.SelectionChanged += (_, __) => ApplyTimelineFilters();
            filter.Children.Add(LabeledControl("Source", _sourceFilter));

            _changesOnly = new CheckBox
            {
                Content = "Changes only",
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(10, 0, 0, 6)
            };
            _changesOnly.Checked += (_, __) => ApplyTimelineFilters();
            _changesOnly.Unchecked += (_, __) => ApplyTimelineFilters();
            filter.Children.Add(_changesOnly);

            root.Children.Add(filter);

            _timelineStatus = new TextBlock
            {
                Text = "Loading timeline...",
                Foreground = TextMuted,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(_timelineStatus, Dock.Top);
            root.Children.Add(_timelineStatus);

            _timelineGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                RowHeaderWidth = 0,
                MinHeight = 320,
                Background = Brushes.White,
                BorderBrush = Border,
                BorderThickness = new Thickness(1)
            };

            AddTextColumn(_timelineGrid, "When", "EventTime", 145, "yyyy-MM-dd HH:mm");
            AddTextColumn(_timelineGrid, "Type", "MovementType", 130);
            AddTextColumn(_timelineGrid, "Category", "MovementCategory", 145);
            AddTextColumn(_timelineGrid, "Dir", "Direction", 70);
            AddTextColumn(_timelineGrid, "Company", "CompanyName", 120);
            AddTextColumn(_timelineGrid, "Branch", "BranchName", 130);
            AddTextColumn(_timelineGrid, "Department", "DepartmentName", 150);
            AddTextColumn(_timelineGrid, "Employee", "EmployeeName", 150);
            AddTextColumn(_timelineGrid, "Notes", "Notes", 360);

            _timelineGrid.MouseDoubleClick += async (_, __) =>
            {
                if (_timelineGrid.SelectedItem is ItemMovementAuditDto row)
                    await OpenReferenceAsync(row);
            };

            root.Children.Add(_timelineGrid);
            return root;
        }

        private UIElement BuildNotesTab()
        {
            var card = Card();
            card.Margin = new Thickness(0, 12, 0, 0);
            card.Padding = new Thickness(16);

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "Notes",
                Foreground = Primary,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            });
            stack.Children.Add(new TextBox
            {
                Text = EmptyDash(_dto.Notes),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = TextMain,
                FontSize = 13,
                Margin = new Thickness(0, 10, 0, 0),
                MinHeight = 120
            });

            card.Child = stack;
            return card;
        }

        private UIElement BuildRawTab()
        {
            var card = Card();
            card.Margin = new Thickness(0, 12, 0, 0);
            card.Padding = new Thickness(16);

            card.Child = BuildKeyValueGrid(new[]
            {
                Pair("ItemId", _dto.ItemId?.ToString()),
                Pair("Serial", _dto.SerialNumber),
                Pair("SetId", _dto.SetId?.ToString()),
                Pair("Set Code", _dto.SetCode),
                Pair("Reference Type", _dto.ReferenceType),
                Pair("Reference ID", _dto.ReferenceId?.ToString()),
                Pair("Inventory Entry Type", _dto.InventoryEntryType),
                Pair("Request Status", _dto.RequestStatus),
                Pair("Request Entry Type", _dto.RequestEntryType),
                Pair("Audit Action", _dto.AuditAction),
                Pair("Audit Status", _dto.AuditStatus),
                Pair("Event Time", _dto.EventTime.ToString("MMM dd, yyyy HH:mm:ss"))
            });

            return card;
        }

        private UIElement BuildSection(string title, params KeyValuePair<string, string>[] rows)
        {
            var card = Card();
            card.Margin = new Thickness(0, 0, 0, 14);
            card.Padding = new Thickness(16);

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Primary,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 12)
            });
            stack.Children.Add(BuildKeyValueGrid(rows));
            card.Child = stack;

            return card;
        }

        private UIElement BuildKeyValueGrid(IEnumerable<KeyValuePair<string, string>> rows)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rowIndex = 0;
            foreach (var row in rows)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var label = new TextBlock
                {
                    Text = row.Key,
                    Foreground = TextMuted,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 12, 10)
                };
                Grid.SetRow(label, rowIndex);
                Grid.SetColumn(label, 0);
                grid.Children.Add(label);

                var value = new TextBlock
                {
                    Text = EmptyDash(row.Value),
                    Foreground = TextMain,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                Grid.SetRow(value, rowIndex);
                Grid.SetColumn(value, 1);
                grid.Children.Add(value);

                rowIndex++;
            }

            return grid;
        }

        private async Task LoadTimelineAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_dto.SerialNumber) && !_dto.ItemId.HasValue)
                {
                    _timelineStatus.Text = "No serial or item id is available for timeline lookup.";
                    return;
                }

                var movements = await _repo.GetTimelineAsync(
                    fromDate: null,
                    toDate: null,
                    direction: "All",
                    serial: _dto.SerialNumber,
                    itemId: _dto.ItemId,
                    setCode: null,
                    source: "All",
                    userName: null,
                    top: 5000);

                _fullHistory.Clear();
                _fullHistory.AddRange(EnrichMovements(movements)
                    .OrderBy(m => m.EventTime)
                    .ThenBy(m => GetStableSortRank(m))
                    .ThenBy(m => m.ReferenceId ?? int.MaxValue));

                PopulateTimelineFilters();
                ApplyTimelineFilters();
            }
            catch (Exception ex)
            {
                _timelineStatus.Text = "Timeline failed to load: " + ex.Message;
            }
        }

        private void PopulateTimelineFilters()
        {
            FillCombo(_categoryFilter, _fullHistory.Select(m => Normalize(m.MovementCategory) ?? "Other"));
            FillCombo(_sourceFilter, _fullHistory.Select(m => Normalize(m.Source) ?? "(unknown)"));
        }

        private void ApplyTimelineFilters()
        {
            if (_timelineGrid == null)
                return;

            IEnumerable<ItemMovementAuditDto> query = _fullHistory;

            var search = Normalize(_timelineSearch?.Text);
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(m =>
                    ContainsAny(Normalize(m.MovementCategory) ?? string.Empty, search) ||
                    ContainsAny(Normalize(m.MovementType) ?? string.Empty, search) ||
                    ContainsAny(Normalize(m.Source) ?? string.Empty, search) ||
                    ContainsAny(Normalize(m.Notes) ?? string.Empty, search) ||
                    ContainsAny(Normalize(m.EmployeeName) ?? string.Empty, search) ||
                    ContainsAny(Normalize(m.CompanyName) ?? string.Empty, search) ||
                    ContainsAny(Normalize(m.DepartmentName) ?? string.Empty, search) ||
                    ContainsAny(Normalize(m.BranchName) ?? string.Empty, search) ||
                    ContainsAny(Normalize(m.SetCode) ?? string.Empty, search) ||
                    ContainsAny(Normalize(m.ReferenceType) ?? string.Empty, search) ||
                    (m.ReferenceId.HasValue && m.ReferenceId.Value.ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            var category = _categoryFilter?.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
                query = query.Where(m => string.Equals(Normalize(m.MovementCategory) ?? "Other", category, StringComparison.OrdinalIgnoreCase));

            var source = _sourceFilter?.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(source) && !string.Equals(source, "All", StringComparison.OrdinalIgnoreCase))
                query = query.Where(m => string.Equals(Normalize(m.Source) ?? "(unknown)", source, StringComparison.OrdinalIgnoreCase));

            if (_changesOnly?.IsChecked == true)
                query = query.Where(HasAnyFieldChange);

            var list = query
                .OrderBy(m => m.EventTime)
                .ThenBy(m => GetStableSortRank(m))
                .ThenBy(m => m.ReferenceId ?? int.MaxValue)
                .ToList();

            _timelineGrid.ItemsSource = list;
            _timelineStatus.Text = list.Count == _fullHistory.Count
                ? $"{list.Count} timeline event(s)"
                : $"{list.Count} of {_fullHistory.Count} timeline event(s)";
        }

        private async Task OpenFullHistoryAsync()
        {
            if (_openHistory == null || string.IsNullOrWhiteSpace(_dto.SerialNumber))
                return;

            try
            {
                await _openHistory(_dto.SerialNumber);
            }
            catch (Exception ex)
            {
                WinForms.MessageBox.Show(ex.Message, "Open history failed", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        private void OpenInsights()
        {
            if (string.IsNullOrWhiteSpace(_dto.SerialNumber))
                return;

            using (var dlg = new ItemMovementAuditInsightsDialog(_dto.SerialNumber))
                dlg.ShowDialog(new Win32Owner(new WindowInteropHelper(this).Handle));
        }

        private void OpenSetDetails(int? setId)
        {
            if (!setId.HasValue || setId.Value <= 0)
                return;

            try
            {
                using (var dlg = new ViewSetDetailPage(setId.Value))
                    dlg.ShowDialog(new Win32Owner(new WindowInteropHelper(this).Handle));
            }
            catch (Exception ex)
            {
                WinForms.MessageBox.Show(ex.Message, "Open Set failed", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        private async Task OpenReferenceAsync(ItemMovementAuditDto row)
        {
            if (row == null)
                return;

            var referenceType = row.ReferenceType;
            int? referenceId = row.ReferenceId;

            // Audit-trail rows are re-tagged ReferenceType='ItemAuditTrail' at read time with
            // ReferenceId = audit row id. Navigate to the stored underlying reference instead.
            if (string.Equals(referenceType, "ItemAuditTrail", StringComparison.OrdinalIgnoreCase))
            {
                referenceType = row.AuditReferenceType;
                referenceId = row.AuditReferenceId;
                if (string.IsNullOrWhiteSpace(referenceType) || !referenceId.HasValue)
                {
                    WinForms.MessageBox.Show(
                        $"Audit event \"{row.AuditAction ?? "Unknown"}\" has no linked reference to open.",
                        "Open Reference", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(referenceType) || !referenceId.HasValue)
                return;

            try
            {
                if (string.Equals(referenceType, "Request", StringComparison.OrdinalIgnoreCase))
                {
                    var repo = new RequestRepository();
                    var req = repo.GetRequestById(referenceId.Value);
                    if (req == null)
                    {
                        WinForms.MessageBox.Show($"Request #{referenceId.Value} not found.", "Open Reference", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                        return;
                    }

                    using (var dlg = new EditRequestDialog(req))
                        dlg.ShowDialog(new Win32Owner(new WindowInteropHelper(this).Handle));
                    return;
                }

                if (string.Equals(referenceType, "Inventory", StringComparison.OrdinalIgnoreCase))
                {
                    var entry = await TryLoadInventoryViewDtoAsync(referenceId.Value);
                    if (entry == null)
                    {
                        WinForms.MessageBox.Show($"Inventory entry #{referenceId.Value} not found.", "Open Reference", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                        return;
                    }

                    using (var dlg = new EditInventoryDialog(entry))
                        dlg.ShowDialog(new Win32Owner(new WindowInteropHelper(this).Handle));
                    return;
                }

                if (string.Equals(referenceType, "Set", StringComparison.OrdinalIgnoreCase))
                {
                    using (var dlg = new ViewSetDetailPage(referenceId.Value))
                        dlg.ShowDialog(new Win32Owner(new WindowInteropHelper(this).Handle));
                    return;
                }

                if (string.Equals(referenceType, "BorrowLog", StringComparison.OrdinalIgnoreCase))
                {
                    var repo = new BorrowItemsRepository();
                    var row2 = await repo.GetBorrowByIdAsync(referenceId.Value);
                    if (row2 == null)
                    {
                        WinForms.MessageBox.Show($"Borrow transaction #{referenceId.Value} not found.", "Open Reference", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                        return;
                    }

                    var dlg = new Yakult.Inventory.App.Wpf.BorrowItems.WpfBorrowDetailsDialog(row2);
                    new WindowInteropHelper(dlg).Owner = new WindowInteropHelper(this).Handle;
                    dlg.ShowDialog();
                    return;
                }

                WinForms.MessageBox.Show($"No viewer is registered for reference type '{referenceType}'.\n\nReference ID: {referenceId.Value}", "Open Reference", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                WinForms.MessageBox.Show(ex.Message, "Open Reference failed", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        private async Task<InventoryViewDto> TryLoadInventoryViewDtoAsync(int invId)
        {
            try
            {
                var repo = new InventoryRepository();
                var inv = await repo.GetInventoryByIdAsync(invId);
                if (inv == null)
                    return null;

                var itemName = _dto.ItemName;
                var postedByName = _dto.UserName ?? _dto.Source;
                var category = string.Empty;

                try
                {
                    using (var con = new System.Data.SqlClient.SqlConnection(Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString))
                    using (var cmd = new System.Data.SqlClient.SqlCommand(@"
                        SELECT TOP (1)
                            it.Name AS ItemName,
                            c.Name AS CategoryName,
                            u.Name AS PostedByName
                        FROM dbo.Inventory i
                        LEFT JOIN dbo.Item it ON it.ItemId = i.ItemId
                        LEFT JOIN dbo.ItemCategory c ON c.CategoryId = it.CategoryId
                        LEFT JOIN dbo.[User] u ON u.UserId = i.PostedBy
                        WHERE i.InvId = @InvId;", con))
                    {
                        cmd.Parameters.AddWithValue("@InvId", invId);
                        con.Open();
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                itemName = reader.IsDBNull(reader.GetOrdinal("ItemName")) ? itemName : reader.GetString(reader.GetOrdinal("ItemName"));
                                category = reader.IsDBNull(reader.GetOrdinal("CategoryName")) ? category : reader.GetString(reader.GetOrdinal("CategoryName"));
                                postedByName = reader.IsDBNull(reader.GetOrdinal("PostedByName")) ? postedByName : reader.GetString(reader.GetOrdinal("PostedByName"));
                            }
                        }
                    }
                }
                catch
                {
                }

                return new InventoryViewDto
                {
                    InvId = inv.InvId,
                    Description = inv.Description,
                    EntryType = inv.EntryType,
                    Quantity = inv.Quantity,
                    DatePosted = inv.DatePosted,
                    PostedByName = postedByName,
                    RequestId = inv.ReqId,
                    ItemName = itemName,
                    Category = category,
                    CategoryTotalStock = 0,
                    IsArchived = false
                };
            }
            catch
            {
                return null;
            }
        }

        private static List<ItemMovementAuditDto> EnrichMovements(List<ItemMovementAuditDto> movements)
        {
            if (movements == null || movements.Count == 0)
                return movements ?? new List<ItemMovementAuditDto>();

            var enriched = movements.ToList();
            var groups = enriched
                .GroupBy(m => string.IsNullOrWhiteSpace(m.SerialNumber)
                    ? (m.ItemId.HasValue ? $"ItemId:{m.ItemId.Value}" : "(unknown)")
                    : $"Serial:{m.SerialNumber.Trim()}");

            foreach (var group in groups)
            {
                var ordered = group
                    .OrderBy(x => x.EventTime)
                    .ThenBy(GetStableSortRank)
                    .ThenBy(x => x.ReferenceId ?? int.MaxValue)
                    .ToList();

                string prevStatus = null;
                string prevSetCode = null;
                string prevBranch = null;
                string prevDept = null;
                string prevEmp = null;

                foreach (var m in ordered)
                {
                    m.MovementCategory = ClassifyCategory(m);
                    m.MovementType = ClassifyType(m);
                    m.MovementPriority = ClassifyPriority(m);
                    m.PrevSetCode = prevSetCode;
                    m.PrevBranchName = prevBranch;
                    m.PrevDepartmentName = prevDept;
                    m.PrevEmployeeName = prevEmp;
                    m.NewSetCode = string.IsNullOrWhiteSpace(m.SetCode) ? prevSetCode : m.SetCode;
                    m.NewBranchName = string.IsNullOrWhiteSpace(m.BranchName) ? prevBranch : m.BranchName;
                    m.NewDepartmentName = string.IsNullOrWhiteSpace(m.DepartmentName) ? prevDept : m.DepartmentName;
                    m.NewEmployeeName = string.IsNullOrWhiteSpace(m.EmployeeName) ? prevEmp : m.EmployeeName;
                    m.StatusBefore = prevStatus;
                    m.StatusAfter = DetermineStatusAfter(m, prevStatus);

                    prevSetCode = m.NewSetCode;
                    prevBranch = m.NewBranchName;
                    prevDept = m.NewDepartmentName;
                    prevEmp = m.NewEmployeeName;
                    prevStatus = m.StatusAfter;
                }
            }

            return enriched;
        }

        private static string ClassifyCategory(ItemMovementAuditDto m)
        {
            if (m == null) return null;
            if (string.Equals(m.ReferenceType, "ItemAuditTrail", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Source, "AuditTrail", StringComparison.OrdinalIgnoreCase)) return "Audit Trail";

            var notes = m.Notes ?? string.Empty;
            if (ContainsAny(notes, "repair", "repaired", "service", "serviced", "calibrat")) return "Maintenance";
            if (ContainsAny(notes, "item sold", "item disposed", " sold ", " disposed", "dispose")) return "Lifecycle";
            if (ContainsAny(notes, "archive", "archived", "inactive", "active", "lost", "missing", "damage", "damaged", "broken")) return "Status Changes";
            if (string.Equals(m.ReferenceType, "Request", StringComparison.OrdinalIgnoreCase) || string.Equals(m.Source, "Request", StringComparison.OrdinalIgnoreCase)) return "Request Lifecycle";
            if (string.Equals(m.ReferenceType, "Inventory", StringComparison.OrdinalIgnoreCase) || string.Equals(m.Source, "Inventory", StringComparison.OrdinalIgnoreCase)) return "Inventory Operations";
            if (string.Equals(m.ReferenceType, "BorrowLog", StringComparison.OrdinalIgnoreCase) || string.Equals(m.Source, "Borrow", StringComparison.OrdinalIgnoreCase)) return "Borrow Operations";
            if (ContainsAny(m.Source ?? string.Empty, "Mobile")) return "Inventory Operations";
            return "Other";
        }

        private static string ClassifyType(ItemMovementAuditDto m)
        {
            if (m == null) return null;
            if (string.Equals(m.ReferenceType, "ItemAuditTrail", StringComparison.OrdinalIgnoreCase) || string.Equals(m.Source, "AuditTrail", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(m.AuditAction) ? "Audit" : m.AuditAction;

            var notes = m.Notes ?? string.Empty;
            if (ContainsAny(notes, "item sold", " sold ")) return "Sold";
            if (ContainsAny(notes, "item disposed", "dispose", " disposed")) return "Disposed";
            if (ContainsAny(notes, "lost", "missing")) return "Lost";
            if (ContainsAny(notes, "broken")) return "Broken";
            if (ContainsAny(notes, "damage", "damaged")) return "Damaged";
            if (ContainsAny(notes, "archive", "archived")) return "Archived";
            if (ContainsAny(notes, "inactive")) return "Inactive";
            if (ContainsAny(notes, " active ", "activate", "activated")) return "Active";
            if (ContainsAny(notes, "repair", "repaired")) return "Repair";
            if (ContainsAny(notes, "service", "serviced")) return "Service";
            if (ContainsAny(notes, "calibrat")) return "Calibration";

            if (string.Equals(m.ReferenceType, "Request", StringComparison.OrdinalIgnoreCase) || string.Equals(m.Source, "Request", StringComparison.OrdinalIgnoreCase))
            {
                var status = m.RequestStatus ?? string.Empty;
                if (ContainsAny(status, "approve", "approved")) return "Approved";
                if (ContainsAny(status, "return", "returned")) return "Returned";
                if (ContainsAny(status, "issue", "issued", "release", "released")) return "Issued";
                return "Requested";
            }

            if (string.Equals(m.ReferenceType, "BorrowLog", StringComparison.OrdinalIgnoreCase) || string.Equals(m.Source, "Borrow", StringComparison.OrdinalIgnoreCase))
                return string.Equals(m.Direction, "IN", StringComparison.OrdinalIgnoreCase) ? "Returned" : "Borrowed";

            if (string.Equals(m.InventoryEntryType, "Positive", StringComparison.OrdinalIgnoreCase)) return "Stock In";
            if (string.Equals(m.InventoryEntryType, "Negative", StringComparison.OrdinalIgnoreCase)) return "Stock Out";
            if (string.Equals(m.Direction, "IN", StringComparison.OrdinalIgnoreCase)) return "Stock In";
            if (string.Equals(m.Direction, "OUT", StringComparison.OrdinalIgnoreCase)) return "Stock Out";
            return "Update";
        }

        private static string ClassifyPriority(ItemMovementAuditDto m)
        {
            var notes = m?.Notes ?? string.Empty;
            if (ContainsAny(notes, "lost", "missing", "broken", "damage", "damaged")) return "High";
            if (ContainsAny(notes, "unprocessed")) return "Medium";
            return "Low";
        }

        private static string DetermineStatusAfter(ItemMovementAuditDto m, string prevStatus)
        {
            if (m == null) return prevStatus;
            if (!string.IsNullOrWhiteSpace(m.AuditStatus) &&
                (string.Equals(m.ReferenceType, "ItemAuditTrail", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(m.Source, "AuditTrail", StringComparison.OrdinalIgnoreCase)))
                return m.AuditStatus;

            var type = m.MovementType ?? string.Empty;
            if (type == "Lost" || type == "Broken" || type == "Damaged" || type == "Archived" || type == "Inactive" || type == "Active" || type == "Sold" || type == "Disposed")
                return type;
            if (type == "Repair" || type == "Service" || type == "Calibration") return "Maintenance";
            if (type == "Approved") return "Approved";
            if (type == "Requested") return "Requested";
            if (type == "Returned") return "Returned";
            if (string.Equals(m.Direction, "OUT", StringComparison.OrdinalIgnoreCase)) return "Issued";
            if (string.Equals(m.Direction, "IN", StringComparison.OrdinalIgnoreCase)) return "In Stock";
            return prevStatus;
        }

        private static bool HasAnyFieldChange(ItemMovementAuditDto m)
        {
            if (m == null) return false;

            // Audit-trail rows carry no location diff data; count them as changes when the
            // action itself represents a state transition (updated/transfer/archive/...).
            if (string.Equals(m.ReferenceType, "ItemAuditTrail", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Source, "AuditTrail", StringComparison.OrdinalIgnoreCase))
            {
                return ContainsAny(m.AuditAction,
                    "Updated", "Transfer", "Changed", "Archived", "Restored", "Unarchived",
                    "Deleted", "Deactivated", "Sold", "Disposed", "Classified", "Fulfilled",
                    "Upgraded", "Created", "Added", "Removed", "Dispatched", "Allocated",
                    "Pullout", "Returned", "Issued", "Relocation");
            }

            return !string.Equals(Normalize(m.PrevSetCode), Normalize(m.NewSetCode), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Normalize(m.PrevBranchName), Normalize(m.NewBranchName), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Normalize(m.PrevDepartmentName), Normalize(m.NewDepartmentName), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Normalize(m.PrevEmployeeName), Normalize(m.NewEmployeeName), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Normalize(m.StatusBefore), Normalize(m.StatusAfter), StringComparison.OrdinalIgnoreCase);
        }

        private static int GetStableSortRank(ItemMovementAuditDto movement)
        {
            var referenceType = movement?.ReferenceType ?? string.Empty;
            if (referenceType.Equals("Inventory", StringComparison.OrdinalIgnoreCase)) return 1;
            if (referenceType.Equals("Request", StringComparison.OrdinalIgnoreCase)) return 2;
            if (referenceType.Equals("ItemAuditTrail", StringComparison.OrdinalIgnoreCase)) return 3;
            if (referenceType.Equals("SetItemUpdate", StringComparison.OrdinalIgnoreCase)) return 4;
            if (referenceType.Equals("BorrowLog", StringComparison.OrdinalIgnoreCase)) return 5;
            return 9;
        }

        private static string BuildSubtitleLine1(ItemMovementAuditDto dto)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(dto.SerialNumber)) parts.Add($"Serial: {dto.SerialNumber}");
            if (!string.IsNullOrWhiteSpace(dto.ModelNumber)) parts.Add($"Model: {dto.ModelNumber}");
            if (!string.IsNullOrWhiteSpace(dto.SetCode)) parts.Add($"Set: {dto.SetCode}");
            if (!string.IsNullOrWhiteSpace(dto.ItemName)) parts.Add(dto.ItemName);
            if (parts.Count == 0 && dto.ItemId.HasValue) parts.Add($"ItemId: {dto.ItemId.Value}");
            return string.Join("  |  ", parts);
        }

        private static string BuildSubtitleLine2(ItemMovementAuditDto dto)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(dto.StatusAfter)) parts.Add($"Status: {dto.StatusAfter}");
            if (!string.IsNullOrWhiteSpace(dto.CompanyName)) parts.Add(dto.CompanyName);
            if (!string.IsNullOrWhiteSpace(dto.BranchName)) parts.Add(dto.BranchName);
            if (!string.IsNullOrWhiteSpace(dto.DepartmentName)) parts.Add(dto.DepartmentName);

            var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
            var offsetText = $"{(offset >= TimeSpan.Zero ? "+" : "-")}{offset:hh\\:mm}";
            parts.Add($"{dto.EventTime:MMM dd, yyyy HH:mm} (UTC{offsetText})");
            return string.Join("  |  ", parts);
        }

        private static string BuildIdsClipboardText(ItemMovementAuditDto dto)
        {
            return
                $"ItemId: {dto.ItemId}\r\n" +
                $"Serial: {dto.SerialNumber}\r\n" +
                $"SetId: {dto.SetId}\r\n" +
                $"SetCode: {dto.SetCode}\r\n" +
                $"ReferenceType: {dto.ReferenceType}\r\n" +
                $"ReferenceId: {dto.ReferenceId}";
        }

        private static Border Card()
        {
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Effect = null
            };
        }

        private static Button MakeButton(string text, Brush background, Brush foreground)
        {
            return new Button
            {
                Content = text,
                MinWidth = 86,
                Height = 34,
                Padding = new Thickness(14, 0, 14, 0),
                Background = background,
                Foreground = foreground,
                BorderBrush = Border,
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
        }

        private static UIElement LabeledControl(string label, Control control)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 10, 0) };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = TextMuted,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 3)
            });
            stack.Children.Add(control);
            return stack;
        }

        private static void AddTextColumn(DataGrid grid, string header, string property, double width, string format = null)
        {
            var binding = new Binding(property);
            if (!string.IsNullOrWhiteSpace(format))
                binding.StringFormat = "{0:" + format + "}";

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = binding,
                Width = width,
                ElementStyle = new Style(typeof(TextBlock))
                {
                    Setters =
                    {
                        new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap),
                        new Setter(TextBlock.MarginProperty, new Thickness(4, 3, 4, 3))
                    }
                }
            });
        }

        private static void FillCombo(ComboBox combo, IEnumerable<string> values)
        {
            if (combo == null)
                return;

            var selected = combo.SelectedItem as string;
            combo.Items.Clear();
            combo.Items.Add("All");
            foreach (var value in values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase))
            {
                combo.Items.Add(value);
            }

            combo.SelectedItem = combo.Items.Cast<object>()
                .Select(i => i?.ToString())
                .FirstOrDefault(v => string.Equals(v, selected, StringComparison.OrdinalIgnoreCase)) ?? "All";
        }

        private static KeyValuePair<string, string> Pair(string key, string value)
        {
            return new KeyValuePair<string, string>(key, value);
        }

        private static string EmptyDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            if (string.IsNullOrEmpty(value) || needles == null || needles.Length == 0)
                return false;
            return needles.Any(n => !string.IsNullOrEmpty(n) && value.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsOut(string direction)
        {
            return string.Equals(direction, "OUT", StringComparison.OrdinalIgnoreCase);
        }

        private static Brush BrushFromRgb(byte r, byte g, byte b)
        {
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private sealed class Win32Owner : WinForms.IWin32Window
        {
            public Win32Owner(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; }
        }
    }
}
