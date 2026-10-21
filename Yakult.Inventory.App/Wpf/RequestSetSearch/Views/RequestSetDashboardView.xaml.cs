using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Wpf.RequestSetSearch.Views
{
    /// <summary>
    /// Analytics landing page for the Request &amp; Set Management portal — KPI tiles, charts and a
    /// records table underneath the portal's search hero (see RequestSetSearchView's Initial state).
    ///
    /// Every number here is produced from the SAME repository calls the portal's own pages use, so
    /// what you see on a tile/chart is exactly what the matching page lists:
    ///   • Requests/KPIs/charts ...... RequestRepository.GetAllRequests() filtered to the portal's
    ///                                 consumable categories + WorkflowType='RequestSetManagement'
    ///                                 (identical to Pages\Request\ViewRequestsPage's own filter)
    ///   • Unfulfilled / Partially ... RequestRepository.GetUnfulfilledRequestsFullSet() /
    ///                                 GetPartiallyFulfilledRequestsFullSet()
    ///   • Sets ...................... SetRepository.GetAllSetsAsync(includeArchived:false) filtered
    ///                                 to sets holding at least one consumable item, mirroring
    ///                                 SetPageViewModel's "Show Consumable Items" default.
    ///
    /// Clicking a tile or chart selects the records behind it into the table below ("drill-down in
    /// place"); the table's own "Open full page" button then hands off to the portal page that owns
    /// those records.
    /// </summary>
    public partial class RequestSetDashboardView : UserControl
    {
        // ── Palette (mirrors Wpf\Dashboard\Views\AnalyticsDashboardView) ──────────────
        private static readonly Brush InkBrush        = Hex("#202635");
        private static readonly Brush MutedBrush      = Hex("#677081");
        private static readonly Brush HairlineBrush   = Hex("#E5E6E8");
        private static readonly Brush HoverBrush      = Hex("#FAFAFA");
        private static readonly Brush SoftCircleBrush = Hex("#F5F6F8");
        private static readonly Brush EmptyBarBrush   = Hex("#EDEEF1");
        private static readonly Brush RedBrush        = Hex("#B92335");
        private static readonly Brush AmberBrush      = Hex("#B7791F");
        private static readonly Brush GreenBrush      = Hex("#2F855A");
        private static readonly Brush RequestBarBrush = Hex("#3B7EA1");
        private static readonly Brush SetBarBrush     = Hex("#6B5CA5");

        private static readonly Brush[] SlicePalette =
        {
            Hex("#3B7EA1"), Hex("#6B5CA5"), Hex("#C79A2E"), Hex("#2F855A"),
            Hex("#9AA1AC"), Hex("#B76E79"), Hex("#4A7C8C"), Hex("#8B6F47")
        };

        /// <summary>The consumable categories this portal owns — same list (and same
        /// case-insensitive comparison) ViewRequestsPage is constructed with.</summary>
        private static readonly string[] ConsumableCategoryNames = { "Cartridge", "Ink", "Printhead", "Toner" };

        private readonly RequestRepository _requestRepo = new RequestRepository();
        private readonly SetRepository     _setRepo     = new SetRepository();

        private Snapshot  _snapshot;
        private Selection _selection = new Selection { Mode = TableMode.Requests };
        private bool      _isLoading;

        private enum TableMode { Requests, Unfulfilled, PartiallyFulfilled, Sets }

        // ── Callbacks wired by RequestSetManagementPortalForm (same pattern as DashboardWpfHost) ──
        public Action OnOpenRequestsPage { get; set; }
        public Action OnOpenUnfulfilledPage { get; set; }
        public Action OnOpenPartiallyFulfilledPage { get; set; }
        public Action OnOpenSetsPage { get; set; }

        public RequestSetDashboardView()
        {
            InitializeComponent();

            BtnRefresh.Click  += (s, e) => Load();
            BtnOpenPage.Click += (s, e) => OpenFullPage();
        }

        // ------------------------------------------------------------------
        // Loading
        // ------------------------------------------------------------------

        /// <summary>Loads (or reloads) the analytics. Safe to call repeatedly — Refresh re-runs it.</summary>
        public void Load()
        {
            if (_isLoading) return;
            _isLoading = true;

            ShowStatus("Loading analytics…");
            LoadAsync();
        }

        private async void LoadAsync()
        {
            Snapshot snapshot;
            try
            {
                snapshot = await BuildSnapshotAsync();
            }
            catch (Exception ex)
            {
                _isLoading = false;
                RunOnUi(() => ShowStatus("Could not load analytics: " + ex.Message));
                return;
            }

            RunOnUi(() =>
            {
                _snapshot  = snapshot;
                _isLoading = false;

                StatusText.Visibility  = Visibility.Collapsed;
                KpiGrid.Visibility     = Visibility.Visible;
                ChartsGrid.Visibility  = Visibility.Visible;
                TableCard.Visibility   = Visibility.Visible;

                BuildKpis();
                BuildCharts();
                _selection = new Selection { Mode = TableMode.Requests };
                RenderTable();
            });
        }

        private async Task<Snapshot> BuildSnapshotAsync()
        {
            var snapshot = new Snapshot();

            // Requests — exactly the rows View Requests (consumables filter, RequestSetManagement
            // workflow) lists, so the tiles and the page can never disagree.
            var allRequests = await Task.Run(() => _requestRepo.GetAllRequests()) ?? new List<RequestDto>();
            snapshot.Requests = allRequests
                .Where(r => r.Category != null && ConsumableCategoryNames.Contains(r.Category, StringComparer.OrdinalIgnoreCase))
                .Where(r => r.WorkflowType == "RequestSetManagement")
                .ToList();

            snapshot.Unfulfilled = await Task.Run(() => _requestRepo.GetUnfulfilledRequestsFullSet())
                                   ?? new List<RequestDto>();
            snapshot.PartiallyFulfilled = await Task.Run(() => _requestRepo.GetPartiallyFulfilledRequestsFullSet())
                                   ?? new List<RequestDto>();

            // Sets — active sets holding at least one consumable-category item, i.e. the same
            // population ViewSetPage(restrictToConsumablesByDefault: true) shows by default.
            var sets = await _setRepo.GetAllSetsAsync(includeArchived: false, includeInvoices: true)
                       ?? new List<SetDto>();
            var setIds = sets.Select(s => s.SetId).ToList();
            var categoriesBySetId = setIds.Count == 0
                ? new Dictionary<int, List<string>>()
                : await Task.Run(() => _setRepo.GetCategoriesBySetIds(setIds)) ?? new Dictionary<int, List<string>>();

            snapshot.Sets = sets.Where(s =>
            {
                List<string> categories;
                if (!categoriesBySetId.TryGetValue(s.SetId, out categories) || categories == null)
                    return false;

                return categories.Any(c => ConsumableCategoryNames.Contains(
                    Yakult.Inventory.App.Models.ConsumableCategories.Canonicalize(c),
                    StringComparer.OrdinalIgnoreCase));
            }).ToList();

            return snapshot;
        }

        private void ShowStatus(string message)
        {
            StatusText.Text       = message;
            StatusText.Visibility = Visibility.Visible;
        }

        private void RunOnUi(Action action)
        {
            if (Dispatcher.CheckAccess()) action();
            else Dispatcher.Invoke(action);
        }

        // ------------------------------------------------------------------
        // KPI tiles — each one selects the records behind its number
        // ------------------------------------------------------------------

        private void BuildKpis()
        {
            KpiGrid.Children.Clear();

            var s = _snapshot;

            AddKpiCard("Total Requests", s.Requests.Count, "\U0001F4DD", RequestBarBrush,
                "Click to list these requests",
                () => SelectRequests(TableMode.Requests, null, null));

            AddKpiCard("Quantity Requested", s.Requests.Sum(r => r.Quantity), "\U0001F4E6", RequestBarBrush,
                "Click to list the requests behind this quantity",
                () => SelectRequests(TableMode.Requests, null, null, sortByQuantityDesc: true));

            AddKpiCard("Unfulfilled", s.Unfulfilled.Count, "\u23F3", AmberBrush,
                "Click to list these requests (same rows as the Unfulfilled page)",
                () => SelectRequests(TableMode.Unfulfilled, null, null));

            AddKpiCard("Partially Fulfilled", s.PartiallyFulfilled.Count, "\U0001F504", RequestBarBrush,
                "Click to list these requests (same rows as the Partially Fulfilled page)",
                () => SelectRequests(TableMode.PartiallyFulfilled, null, null));

            AddKpiCard("Sets", s.Sets.Count, "\U0001F5C2", SetBarBrush,
                "Click to list these sets",
                () => SelectSets(null, null));

            AddKpiCard("Items in Sets", s.Sets.Sum(x => x.ItemCount), "\U0001F9FE", SetBarBrush,
                "Click to list the sets holding these items",
                () => SelectSets(null, null, sortByItemCountDesc: true));
        }

        private void AddKpiCard(string title, int value, string icon, Brush accent, string tooltip, Action onClick)
        {
            var card = new Border
            {
                Style     = (Style)FindResource("DashCard"),
                Margin    = new Thickness(7),
                MinHeight = 98,
                Cursor    = Cursors.Hand,
                ToolTip   = tooltip
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var labelRow = new StackPanel { Orientation = Orientation.Horizontal };
            labelRow.Children.Add(new Ellipse
            {
                Width = 8, Height = 8, Fill = accent,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
            });
            labelRow.Children.Add(new TextBlock
            {
                Text = (title ?? string.Empty).ToUpperInvariant(),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = MutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            left.Children.Add(labelRow);

            left.Children.Add(new TextBlock
            {
                Text = FormatNumber(value),
                FontSize = 26,
                FontWeight = FontWeights.SemiBold,
                Foreground = InkBrush,
                Margin = new Thickness(0, 8, 0, 0)
            });
            grid.Children.Add(left);

            var iconCircle = new Border
            {
                Width = 32, Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = SoftCircleBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 0, 0, 0)
            };
            iconCircle.Child = new TextBlock
            {
                Text = icon,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconCircle, 1);
            grid.Children.Add(iconCircle);

            card.Child = grid;

            card.MouseEnter += (s, e) => { card.Background = HoverBrush; card.BorderBrush = accent; };
            card.MouseLeave += (s, e) => { card.Background = Brushes.White; card.BorderBrush = HairlineBrush; };
            card.MouseLeftButtonUp += (s, e) => onClick();

            KpiGrid.Children.Add(card);
        }

        // ------------------------------------------------------------------
        // Charts — slicing by any dimension selects the matching records below
        // ------------------------------------------------------------------

        private void BuildCharts()
        {
            ChartsGrid.Children.Clear();

            var requests = _snapshot.Requests;
            var sets     = _snapshot.Sets;

            // 1. Requests by status (donut)
            var statusMetrics = GroupCounts(requests, r => r.Status);
            ChartsGrid.Children.Add(BuildDonutCard("Requests by Status", statusMetrics, "requests", label =>
                SelectRequests(TableMode.Requests, "Status: " + label,
                    r => string.Equals((r.Status ?? string.Empty).Trim(), label, StringComparison.OrdinalIgnoreCase))));

            // 2. Requests by category (bars)
            var categoryMetrics = GroupCounts(requests,
                r => Yakult.Inventory.App.Models.ConsumableCategories.Canonicalize(r.Category));
            ChartsGrid.Children.Add(BuildBarsCard("Requests by Category", categoryMetrics, RequestBarBrush, "requests", label =>
                SelectRequests(TableMode.Requests, "Category: " + label,
                    r => string.Equals(
                        Yakult.Inventory.App.Models.ConsumableCategories.Canonicalize(r.Category),
                        label, StringComparison.OrdinalIgnoreCase))));

            // 3. Top requested items by quantity (bars)
            var itemMetrics = GroupSum(requests, r => r.ItemName, r => r.Quantity, 6);
            ChartsGrid.Children.Add(BuildBarsCard("Top Requested Items (qty)", itemMetrics, RequestBarBrush, "units", label =>
                SelectRequests(TableMode.Requests, "Item: " + label,
                    r => string.Equals((r.ItemName ?? string.Empty).Trim(), label, StringComparison.OrdinalIgnoreCase))));

            // 4. Requests by month (last 12 months)
            var requestMonths = MonthSeries(requests, r => r.DateRequested ?? r.DateCreated);
            ChartsGrid.Children.Add(BuildMonthsCard("Requests by Month", requestMonths, RequestBarBrush, "requests", point =>
                SelectRequests(TableMode.Requests, "Month: " + point.FullLabel,
                    r => SameMonth(r.DateRequested ?? r.DateCreated, point.Month))));

            // 5. Sets by status (donut)
            var setStatusMetrics = GroupCounts(sets, s => s.Status);
            ChartsGrid.Children.Add(BuildDonutCard("Sets by Status", setStatusMetrics, "sets", label =>
                SelectSets("Status: " + label,
                    s => string.Equals((s.Status ?? string.Empty).Trim(), label, StringComparison.OrdinalIgnoreCase))));

            // 6. Sets created per month (last 12 months)
            var setMonths = MonthSeries(sets, s => s.CreatedAt);
            ChartsGrid.Children.Add(BuildMonthsCard("Sets by Month", setMonths, SetBarBrush, "sets", point =>
                SelectSets("Month: " + point.FullLabel, s => SameMonth(s.CreatedAt, point.Month))));
        }

        private Border BuildChartCard(string title, UIElement content)
        {
            var card = new Border
            {
                Style     = (Style)FindResource("DashCard"),
                Margin    = new Thickness(7),
                MinHeight = 310
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = (title ?? string.Empty).ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = MutedBrush,
                TextWrapping = TextWrapping.Wrap
            });

            if (content != null) stack.Children.Add(content);

            card.Child = stack;
            return card;
        }

        private Border BuildDonutCard(string title, List<Metric> metrics, string unit, Action<string> onSliceClick)
        {
            var list  = (metrics ?? new List<Metric>()).Where(m => m != null && m.Value > 0).ToList();
            double total = list.Sum(m => (double)m.Value);

            var stack = new StackPanel();

            if (list.Count == 0 || total <= 0)
            {
                stack.Children.Add(BuildNoData());
                return BuildChartCard(title, stack);
            }

            const double canvasSize = 168;
            const double outer = 78;
            const double inner = 50;
            double center = canvasSize / 2.0;

            var canvas = new Canvas
            {
                Width = canvasSize,
                Height = canvasSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 14, 0, 0)
            };

            double start = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var metric = list[i];
                double sweep = metric.Value / total * 360.0;
                AddDonutSlice(canvas, new Point(center, center), outer, inner, start, start + sweep,
                    SlicePalette[i % SlicePalette.Length], metric, total, onSliceClick);
                start += sweep;
            }

            // Count in the middle of the ring — the number the slices add up to.
            var centerValue = new TextBlock
            {
                Text = FormatNumber((long)total),
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = InkBrush,
                Width = inner * 2,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(centerValue, center - inner);
            Canvas.SetTop(centerValue, center - 15);
            canvas.Children.Add(centerValue);

            var centerCaption = new TextBlock
            {
                Text = unit.ToUpperInvariant(),
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = MutedBrush,
                Width = inner * 2,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(centerCaption, center - inner);
            Canvas.SetTop(centerCaption, center + 8);
            canvas.Children.Add(centerCaption);

            stack.Children.Add(canvas);
            stack.Children.Add(BuildLegend(list, i => SlicePalette[i % SlicePalette.Length], onSliceClick));

            return BuildChartCard(title, stack);
        }

        private void AddDonutSlice(Canvas canvas, Point center, double outerRadius, double innerRadius,
            double startDegrees, double endDegrees, Brush fill, Metric metric, double total, Action<string> onSliceClick)
        {
            double sweep = endDegrees - startDegrees;
            var shapes = new List<Shape>();

            if (sweep >= 359.99)
            {
                shapes.Add(CreateDonutSector(center, outerRadius, innerRadius, 0, 180, fill));
                shapes.Add(CreateDonutSector(center, outerRadius, innerRadius, 180, 360, fill));
            }
            else
            {
                shapes.Add(CreateDonutSector(center, outerRadius, innerRadius, startDegrees, endDegrees, fill));
            }

            foreach (var shape in shapes)
            {
                var label = metric.Label;
                shape.Cursor  = Cursors.Hand;
                shape.ToolTip = $"{label}: {FormatNumber(metric.Value)} ({FormatPercent(metric.Value, total)}) — click to list these";
                shape.MouseLeftButtonUp += (s, e) => onSliceClick(label);
                canvas.Children.Add(shape);
            }
        }

        private static Path CreateDonutSector(Point center, double outer, double inner, double startDeg, double endDeg, Brush fill)
        {
            bool large = (endDeg - startDeg) > 180;

            var figure = new PathFigure { StartPoint = Polar(center, outer, startDeg), IsClosed = true, IsFilled = true };
            figure.Segments.Add(new ArcSegment(Polar(center, outer, endDeg), new Size(outer, outer), 0, large, SweepDirection.Clockwise, false));
            figure.Segments.Add(new LineSegment(Polar(center, inner, endDeg), false));
            figure.Segments.Add(new ArcSegment(Polar(center, inner, startDeg), new Size(inner, inner), 0, large, SweepDirection.Counterclockwise, false));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return new Path { Data = geometry, Fill = fill, Stroke = Brushes.White, StrokeThickness = 1.5 };
        }

        private static Point Polar(Point center, double radius, double degrees)
        {
            double radians = (degrees - 90.0) * Math.PI / 180.0;
            return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
        }

        private UIElement BuildLegend(List<Metric> metrics, Func<int, Brush> colorSelector, Action<string> onChipClick)
        {
            var wrap = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };

            for (int i = 0; i < metrics.Count; i++)
            {
                var metric = metrics[i];
                var chip = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 2, 14, 2),
                    MaxWidth = 190,
                    Cursor = Cursors.Hand,
                    ToolTip = "Click to list these records"
                };
                chip.Children.Add(new Ellipse
                {
                    Width = 9, Height = 9, Fill = colorSelector(i),
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0)
                });
                chip.Children.Add(new TextBlock
                {
                    Text = $"{metric.Label} ({FormatNumber(metric.Value)})",
                    FontSize = 11.5,
                    Foreground = InkBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                chip.MouseLeftButtonUp += (s, e) => onChipClick(metric.Label);
                wrap.Children.Add(chip);
            }

            return wrap;
        }

        private Border BuildBarsCard(string title, List<Metric> metrics, Brush barBrush, string unit, Action<string> onRowClick)
        {
            var list = (metrics ?? new List<Metric>()).Where(m => m != null).ToList();

            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            if (list.Count == 0)
            {
                stack.Children.Add(BuildNoData());
                return BuildChartCard(title, stack);
            }

            double max = Math.Max(1.0, list.Max(m => (double)m.Value));

            foreach (var metric in list)
            {
                var metricRef = metric;
                double value = Math.Max(0.0, metric.Value);

                var row = new Border
                {
                    Padding = new Thickness(6, 5, 6, 5),
                    CornerRadius = new CornerRadius(6),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    ToolTip = $"{metric.Label}: {FormatNumber(metric.Value)} {unit} — click to list these"
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var label = new TextBlock
                {
                    Text = metric.Label ?? string.Empty,
                    FontSize = 12.5,
                    Foreground = InkBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0),
                    ToolTip = metric.Label
                };
                Grid.SetColumn(label, 0);
                grid.Children.Add(label);

                var track = new Grid { VerticalAlignment = VerticalAlignment.Center };
                track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(value, 0.0001), GridUnitType.Star) });
                track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(max - value, 0.0001), GridUnitType.Star) });

                var bar = new Border
                {
                    Height = 10,
                    CornerRadius = new CornerRadius(3),
                    Background = barBrush,
                    MinWidth = 6,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                track.Children.Add(bar);
                Grid.SetColumn(track, 1);
                grid.Children.Add(track);

                var valueText = new TextBlock
                {
                    Text = FormatNumber(metric.Value),
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = InkBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Right,
                    MinWidth = 48
                };
                Grid.SetColumn(valueText, 2);
                grid.Children.Add(valueText);

                row.Child = grid;
                row.MouseEnter += (s, e) => row.Background = HoverBrush;
                row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
                row.MouseLeftButtonUp += (s, e) => onRowClick(metricRef.Label);

                stack.Children.Add(row);
            }

            return BuildChartCard(title, stack);
        }

        private Border BuildMonthsCard(string title, List<MonthPoint> series, Brush barBrush, string unit, Action<MonthPoint> onBarClick)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };

            var points = series ?? new List<MonthPoint>();
            if (points.Count == 0 || points.All(p => p.Value <= 0))
            {
                stack.Children.Add(BuildNoData());
                return BuildChartCard(title, stack);
            }

            double max = Math.Max(1.0, points.Max(p => (double)p.Value));

            var bars   = new Grid { Height = 148 };
            var labels = new Grid { Margin = new Thickness(0, 7, 0, 0), Height = 16 };

            for (int i = 0; i < points.Count; i++)
            {
                bars.ColumnDefinitions.Add(new ColumnDefinition());
                labels.ColumnDefinitions.Add(new ColumnDefinition());

                var point = points[i];
                bool hasValue = point.Value > 0;

                var bar = new Border
                {
                    Width = 17,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    CornerRadius = new CornerRadius(3, 3, 0, 0),
                    Background = hasValue ? barBrush : EmptyBarBrush,
                    Height = hasValue ? Math.Max(6.0, point.Value / max * 140.0) : 3.0,
                    Cursor = hasValue ? Cursors.Hand : Cursors.Arrow,
                    ToolTip = hasValue
                        ? $"{point.FullLabel}: {FormatNumber(point.Value)} {unit} — click to list these"
                        : $"{point.FullLabel}: no records"
                };
                bar.MouseEnter += (s, e) => { if (hasValue) bar.Opacity = 0.82; };
                bar.MouseLeave += (s, e) => bar.Opacity = 1.0;
                bar.MouseLeftButtonUp += (s, e) => { if (hasValue) onBarClick(point); };
                Grid.SetColumn(bar, i);
                bars.Children.Add(bar);

                var monthLabel = new TextBlock
                {
                    Text = point.Label,
                    FontSize = 9.5,
                    Foreground = hasValue ? MutedBrush : Hex("#B9BEC7"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top
                };
                Grid.SetColumn(monthLabel, i);
                labels.Children.Add(monthLabel);
            }

            stack.Children.Add(bars);
            stack.Children.Add(new Border { Height = 1, Background = HairlineBrush });
            stack.Children.Add(labels);

            return BuildChartCard(title, stack);
        }

        private UIElement BuildNoData()
        {
            return new TextBlock
            {
                Text = "No data yet",
                FontSize = 12.5,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 18, 0, 0)
            };
        }

        // ------------------------------------------------------------------
        // Drill-down table
        // ------------------------------------------------------------------

        private void SelectRequests(TableMode mode, string filterLabel, Func<RequestDto, bool> predicate, bool sortByQuantityDesc = false)
        {
            _selection = new Selection
            {
                Mode = mode,
                FilterLabel = filterLabel,
                RequestPredicate = predicate,
                SortByQuantityDesc = sortByQuantityDesc
            };
            RenderTable();
        }

        private void SelectSets(string filterLabel, Func<SetDto, bool> predicate, bool sortByItemCountDesc = false)
        {
            _selection = new Selection
            {
                Mode = TableMode.Sets,
                FilterLabel = filterLabel,
                SetPredicate = predicate,
                SortByItemCountDesc = sortByItemCountDesc
            };
            RenderTable();
        }

        private void ClearFilter()
        {
            var previous = _selection;
            if (previous == null || previous.FilterLabel == null) return;

            _selection = new Selection
            {
                Mode = previous.Mode,
                SortByQuantityDesc = previous.SortByQuantityDesc,
                SortByItemCountDesc = previous.SortByItemCountDesc
            };
            RenderTable();
        }

        private void RenderTable()
        {
            if (_snapshot == null) return;

            var selection = _selection ?? new Selection { Mode = TableMode.Requests };

            if (selection.Mode == TableMode.Sets)
            {
                var sets = _snapshot.Sets.Where(selection.SetPredicate ?? (s => true));
                sets = selection.SortByItemCountDesc
                    ? sets.OrderByDescending(s => s.ItemCount).ThenByDescending(s => s.CreatedAt)
                    : sets.OrderByDescending(s => s.CreatedAt);
                RenderSetsTable(sets.ToList(), selection.FilterLabel);
                return;
            }

            var population = selection.Mode == TableMode.Unfulfilled
                ? _snapshot.Unfulfilled
                : selection.Mode == TableMode.PartiallyFulfilled
                    ? _snapshot.PartiallyFulfilled
                    : _snapshot.Requests;

            var rows = population.Where(selection.RequestPredicate ?? (r => true));
            rows = selection.SortByQuantityDesc
                ? rows.OrderByDescending(r => r.Quantity).ThenByDescending(r => r.DateRequested ?? r.DateCreated)
                : rows.OrderByDescending(r => r.DateRequested ?? r.DateCreated);

            RenderRequestsTable(selection.Mode, rows.ToList(), selection.FilterLabel);
        }

        private void RenderRequestsTable(TableMode mode, List<RequestDto> rows, string filterLabel)
        {
            string title, buttonLabel;
            switch (mode)
            {
                case TableMode.Unfulfilled:
                    title = "Unfulfilled Requests"; buttonLabel = "Open Unfulfilled"; break;
                case TableMode.PartiallyFulfilled:
                    title = "Partially Fulfilled Requests"; buttonLabel = "Open Partially Fulfilled"; break;
                default:
                    title = "Requests"; buttonLabel = "Open Requests"; break;
            }

            var gridRows = new List<RsGridRow>();

            foreach (var r in rows)
            {
                bool isOutstandingQueue = mode == TableMode.Unfulfilled || mode == TableMode.PartiallyFulfilled;

                if (isOutstandingQueue)
                {
                    gridRows.Add(new RsGridRow
                    {
                        Cells = new object[]
                        {
                            FormatDate(r.DateCreated),
                            Text(r.EmployeeName),
                            Text(r.ItemName),
                            Text(r.DepartmentName),
                            FormatNumber(r.Quantity),
                            FormatNumber(r.IssuedQty),
                            FormatNumber(Math.Max(0, r.Quantity - r.IssuedQty))
                        },
                        Status = Text(r.Status),
                        StatusBrush = StatusBrush(r.Status)
                    });
                }
                else
                {
                    gridRows.Add(new RsGridRow
                    {
                        Cells = new object[]
                        {
                            FormatDate(r.DateRequested ?? r.DateCreated),
                            Text(r.EmployeeName),
                            Text(r.ItemName),
                            Text(Yakult.Inventory.App.Models.ConsumableCategories.Canonicalize(r.Category)),
                            Text(r.DepartmentName),
                            FormatNumber(r.Quantity)
                        },
                        Status = Text(r.Status),
                        StatusBrush = StatusBrush(r.Status)
                    });
                }
            }

            bool isQueue = mode != TableMode.Requests;

            BuildTable(
                title,
                buttonLabel,
                isQueue
                    ? new[] { "Date", "Employee", "Item", "Department", "Requested", "Issued", "Pending" }
                    : new[] { "Date Requested", "Employee", "Item", "Category", "Department", "Qty" },
                isQueue
                    ? new[] { false, false, false, false, true, true, true }
                    : new[] { false, false, false, false, false, true },
                gridRows,
                filterLabel,
                mode);
        }

        private void RenderSetsTable(List<SetDto> rows, string filterLabel)
        {
            var gridRows = rows.Select(s => new RsGridRow
            {
                Cells = new object[]
                {
                    Text(s.SetCode),
                    FormatDate(s.CreatedAt),
                    FormatNumber(s.ItemCount),
                    Text(s.CurrentDepartmentName),
                    Text(s.CurrentBranchName),
                    FormatDate(s.EffectiveExpiry),
                    Text(s.SetType)
                },
                Status = Text(s.Status),
                StatusBrush = StatusBrush(s.Status)
            }).ToList();

            BuildTable(
                "Sets",
                "Open Sets",
                new[] { "Set Code", "Created", "Items", "Department", "Branch", "Expires", "Type" },
                new[] { false, false, true, false, false, false, false },
                gridRows,
                filterLabel,
                TableMode.Sets);
        }

        private void BuildTable(string title, string buttonLabel, string[] headers, bool[] numericFlags,
            List<RsGridRow> rows, string filterLabel, TableMode mode)
        {
            TableTitle.Text  = (title ?? "Records").ToUpperInvariant();
            BtnOpenPage.Content = buttonLabel + " →";
            TableCount.Text  = rows.Count == 0
                ? "No records"
                : $"Showing {rows.Count} record{(rows.Count == 1 ? string.Empty : "s")}";

            FilterChipPanel.Children.Clear();
            if (!string.IsNullOrWhiteSpace(filterLabel))
                FilterChipPanel.Children.Add(BuildFilterChip(filterLabel));

            TableGrid.Columns.Clear();
            TableGrid.ItemsSource = null;

            if (rows.Count == 0)
            {
                TableGrid.Visibility = Visibility.Collapsed;
                return;
            }

            TableGrid.Visibility = Visibility.Visible;

            var baseCell    = (Style)FindResource("TableCellBase");
            var rightCell   = (Style)FindResource("TableCellRight");
            var baseHeader  = (Style)FindResource("TableHeaderBase");
            var rightHeader = (Style)FindResource("TableHeaderRight");

            for (int i = 0; i < headers.Length; i++)
            {
                bool numeric = numericFlags != null && i < numericFlags.Length && numericFlags[i];

                TableGrid.Columns.Add(new DataGridTextColumn
                {
                    Header      = headers[i].ToUpperInvariant(),
                    Binding     = new System.Windows.Data.Binding("Cells[" + i + "]"),
                    CellStyle   = numeric ? rightCell : baseCell,
                    HeaderStyle = numeric ? rightHeader : baseHeader,
                    Width       = i == 0
                        ? new DataGridLength(1, DataGridLengthUnitType.Star)
                        : DataGridLength.Auto,
                    MinWidth    = 90
                });
            }

            if (rows.Any(r => !string.IsNullOrWhiteSpace(r.Status)))
            {
                TableGrid.Columns.Add(new DataGridTemplateColumn
                {
                    Header      = "STATUS",
                    CellTemplate = (DataTemplate)FindResource("StatusCellTemplate"),
                    CellStyle   = baseCell,
                    HeaderStyle = baseHeader,
                    Width       = DataGridLength.Auto,
                    MinWidth    = 92
                });
            }

            TableGrid.ItemsSource = rows;
        }

        private UIElement BuildFilterChip(string text)
        {
            var accent       = (Brush)FindResource("YakultPrimaryBrush");
            var accentTint   = (Brush)FindResource("YakultAccentTintBrush");
            var accentBorder = (Brush)FindResource("YakultAccentBorderBrush");

            var chip = new Border
            {
                Background      = accentTint,
                BorderBrush     = accentBorder,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(12),
                Padding         = new Thickness(10, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = accent,
                VerticalAlignment = VerticalAlignment.Center
            });

            var clear = new TextBlock
            {
                Text = "✕",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = accent,
                Margin = new Thickness(7, 0, 0, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Clear this filter"
            };
            clear.MouseLeftButtonUp += (s, e) => ClearFilter();
            panel.Children.Add(clear);

            chip.Child = panel;
            return chip;
        }

        private void OpenFullPage()
        {
            switch ((_selection ?? new Selection { Mode = TableMode.Requests }).Mode)
            {
                case TableMode.Unfulfilled:
                    OnOpenUnfulfilledPage?.Invoke();
                    break;
                case TableMode.PartiallyFulfilled:
                    OnOpenPartiallyFulfilledPage?.Invoke();
                    break;
                case TableMode.Sets:
                    OnOpenSetsPage?.Invoke();
                    break;
                default:
                    OnOpenRequestsPage?.Invoke();
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Aggregation helpers
        // ------------------------------------------------------------------

        private sealed class Snapshot
        {
            public List<RequestDto> Requests = new List<RequestDto>();
            public List<RequestDto> Unfulfilled = new List<RequestDto>();
            public List<RequestDto> PartiallyFulfilled = new List<RequestDto>();
            public List<SetDto> Sets = new List<SetDto>();
        }

        private sealed class Selection
        {
            public TableMode Mode;
            public string FilterLabel;
            public Func<RequestDto, bool> RequestPredicate;
            public Func<SetDto, bool> SetPredicate;
            public bool SortByQuantityDesc;
            public bool SortByItemCountDesc;
        }

        private sealed class Metric
        {
            public string Label { get; }
            public double Value { get; set; }

            public Metric(string label) { Label = label ?? string.Empty; }
        }

        private sealed class MonthPoint
        {
            public DateTime Month;
            public string Label;      // "Nov" — unique across a 12-month window
            public string FullLabel;  // "Nov 2025"
            public int Value;
        }

        private static List<Metric> GroupCounts<T>(IEnumerable<T> items, Func<T, string> keySelector)
        {
            var metrics = new Dictionary<string, Metric>(StringComparer.OrdinalIgnoreCase);
            var order   = new List<string>();

            foreach (var item in items)
            {
                var raw = keySelector(item);
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var key = raw.Trim();
                Metric metric;
                if (!metrics.TryGetValue(key, out metric))
                {
                    metric = new Metric(key);
                    metrics[key] = metric;
                    order.Add(key);
                }

                metric.Value += 1;
            }

            return order.Select(k => metrics[k])
                        .OrderByDescending(m => m.Value)
                        .ThenBy(m => m.Label, StringComparer.OrdinalIgnoreCase)
                        .ToList();
        }

        private static List<Metric> GroupSum<T>(IEnumerable<T> items, Func<T, string> keySelector,
            Func<T, double> valueSelector, int topN)
        {
            var metrics = new Dictionary<string, Metric>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                var raw = keySelector(item);
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var key = raw.Trim();
                Metric metric;
                if (!metrics.TryGetValue(key, out metric))
                {
                    metric = new Metric(key);
                    metrics[key] = metric;
                }

                metric.Value += Math.Max(0, valueSelector(item));
            }

            var ordered = metrics.Values
                .Where(m => m.Value > 0)
                .OrderByDescending(m => m.Value)
                .ThenBy(m => m.Label, StringComparer.OrdinalIgnoreCase);

            return (topN > 0 ? ordered.Take(topN) : ordered).ToList();
        }

        private static List<MonthPoint> MonthSeries<T>(IEnumerable<T> items, Func<T, DateTime?> dateSelector)
        {
            var today = DateTime.Today;
            var start = new DateTime(today.Year, today.Month, 1).AddMonths(-11);

            var counts = new Dictionary<DateTime, int>();
            foreach (var item in items)
            {
                var date = dateSelector(item);
                if (!date.HasValue) continue;

                var key = new DateTime(date.Value.Year, date.Value.Month, 1);
                int existing;
                counts.TryGetValue(key, out existing);
                counts[key] = existing + 1;
            }

            var points = new List<MonthPoint>();
            for (int i = 0; i < 12; i++)
            {
                var month = start.AddMonths(i);
                int count;
                counts.TryGetValue(month, out count);

                points.Add(new MonthPoint
                {
                    Month     = month,
                    Label     = month.ToString("MMM", CultureInfo.InvariantCulture),
                    FullLabel = month.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                    Value     = count
                });
            }

            return points;
        }

        private static bool SameMonth(DateTime value, DateTime month)
            => value.Year == month.Year && value.Month == month.Month;

        // ------------------------------------------------------------------
        // Formatting helpers
        // ------------------------------------------------------------------

        private static Brush StatusBrush(string status)
        {
            var s = (status ?? string.Empty).Trim().ToLowerInvariant();

            // Match incomplete and negative states before "fulfilled", which they also contain.
            if (s.Contains("cancel") || s.Contains("reject") || s.Contains("denied") ||
                s.Contains("expired") || s.Contains("unfulfilled"))
                return RedBrush;

            if (s.Contains("partial") || s.Contains("progress") || s.Contains("processing"))
                return RequestBarBrush;

            if (s.Contains("pending") || s.Contains("under review") || s.Contains("submitted") ||
                s.Contains("awaiting") || s.Contains("hold"))
                return AmberBrush;

            if (s.Contains("fulfilled") || s.Contains("completed") || s.Contains("issued") ||
                s.Contains("dispatched") || s.Contains("approved"))
                return GreenBrush;

            return MutedBrush;
        }

        private static string Text(string value)
            => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

        private static string FormatDate(DateTime? value)
            => value.HasValue ? value.Value.ToString("MMM d, yyyy", CultureInfo.CurrentCulture) : "—";

        private static string FormatNumber(int value)
            => value.ToString("N0", CultureInfo.CurrentCulture);

        private static string FormatNumber(long value)
            => value.ToString("N0", CultureInfo.CurrentCulture);

        private static string FormatNumber(double value)
            => Math.Round(value).ToString("N0", CultureInfo.CurrentCulture);

        private static string FormatPercent(double value, double total)
            => total <= 0 ? "0%" : (value / total * 100.0).ToString("0.#") + "%";

        private static Brush Hex(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }

    /// <summary>
    /// Flat row wrapper for this dashboard's DataGrid — columns bind into the Cells array, and the
    /// status template column reads Status/StatusBrush (same shape as the main Dashboard's rows).
    /// </summary>
    public sealed class RsGridRow
    {
        public object[] Cells { get; set; }
        public string Status { get; set; }
        public Brush StatusBrush { get; set; }
    }
}
