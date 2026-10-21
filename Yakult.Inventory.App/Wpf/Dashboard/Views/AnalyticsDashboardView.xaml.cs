using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Yakult.Inventory.App.Forms.Dashboard;
using DrawingColor = System.Drawing.Color;

namespace Yakult.Inventory.App.WPF.Dashboard.Views
{
    /// <summary>
    /// WPF rebuild of the legacy WinForms dashboard. Same data and drill-down behavior
    /// (both come from DashboardService), restyled in the workspace visual language.
    /// Hosted inside the WinForms shell via DashboardWpfHost.
    /// </summary>
    public partial class AnalyticsDashboardView : UserControl
    {
        private enum ChartKind
        {
            Primary,
            Secondary,
            Tertiary
        }

        private static readonly Brush InkBrush      = Hex("#202635");
        private static readonly Brush MutedBrush    = Hex("#677081");
        private static readonly Brush HairlineBrush = Hex("#E5E6E8");
        private static readonly Brush HoverBrush    = Hex("#FAFAFA");
        private static readonly Brush SoftCircleBrush = Hex("#F5F6F8");
        private static readonly Brush GreenBrush    = Hex("#2F855A");
        private static readonly Brush RedBrush      = Hex("#B92335");
        private static readonly Brush AmberBrush    = Hex("#B7791F");
        private static readonly Brush BarBrush      = Hex("#B92335");

        private static readonly Brush[] ChartPalette =
        {
            Hex("#B92335"), Hex("#3B7EA1"), Hex("#C79A2E"), Hex("#35855C"),
            Hex("#6B5CA5"), Hex("#A05C7B"), Hex("#5C7A99"), Hex("#B08F3D")
        };

        private readonly DashboardService _service = new DashboardService();
        private readonly List<DashboardScope> _scopeOrder = new List<DashboardScope>();
        private DashboardScope _currentScope = DashboardScope.Categories;
        private DashboardFilter _filter;
        private int _pendingMobileUpdatesCount;
        private bool _suppressScopeChange;

        public Action OnViewInventoryClicked { get; set; }
        public Action OnViewUpdatesClicked { get; set; }
        public Action OnRefreshClicked { get; set; }

        public AnalyticsDashboardView()
        {
            InitializeComponent();

            BuildScopeCombo();

            BtnViewInventory.Click += (s, e) => OnViewInventoryClicked?.Invoke();
            BtnViewUpdates.Click   += (s, e) => OnViewUpdatesClicked?.Invoke();
            BtnRefresh.Click       += (s, e) => OnRefreshClicked?.Invoke();
        }

        // ------------------------------------------------------------------
        // Public surface (kept identical to the legacy DashboardView)
        // ------------------------------------------------------------------

        public async void LoadDashboard(DashboardScope scope, DashboardFilter filter = null)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => LoadDashboard(scope, filter)));
                return;
            }

            _currentScope = scope;
            _filter = filter;
            SelectScopeInCombo(scope);

            ShowStatus("Loading dashboard…", false);
            HideContent();

            try
            {
                var data = await Task.Run(() => _service.GetDashboardData(scope, filter));
                RenderDashboard(data);
            }
            catch (Exception ex)
            {
                HideContent();
                ShowStatus($"Could not load this dashboard: {ex.Message}", true);
            }
        }

        public void SetMobileUpdatesPendingCount(int pendingCount)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => SetMobileUpdatesPendingCount(pendingCount)));
                return;
            }

            _pendingMobileUpdatesCount = Math.Max(0, pendingCount);

            if (_pendingMobileUpdatesCount <= 0)
            {
                UpdatesBadge.Visibility = Visibility.Collapsed;
                return;
            }

            UpdatesBadgeText.Text = _pendingMobileUpdatesCount > 99
                ? "99+"
                : _pendingMobileUpdatesCount.ToString(CultureInfo.CurrentCulture);
            UpdatesBadge.Visibility = Visibility.Visible;
        }

        // ------------------------------------------------------------------
        // Scope selector
        // ------------------------------------------------------------------

        private void BuildScopeCombo()
        {
            _suppressScopeChange = true;

            _scopeOrder.Clear();
            var displayNames = new List<string>();
            foreach (DashboardScope scope in Enum.GetValues(typeof(DashboardScope)))
            {
                _scopeOrder.Add(scope);
                displayNames.Add(GetScopeDisplayName(scope));
            }

            ScopeCombo.ItemsSource = displayNames;
            ScopeCombo.SelectedIndex = Math.Max(0, _scopeOrder.IndexOf(_currentScope));
            _suppressScopeChange = false;
        }

        private void ScopeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressScopeChange) return;

            int index = ScopeCombo.SelectedIndex;
            if (index < 0 || index >= _scopeOrder.Count) return;

            var scope = _scopeOrder[index];
            if (scope != _currentScope)
                LoadDashboard(scope, _filter);
        }

        private void SelectScopeInCombo(DashboardScope scope)
        {
            int index = _scopeOrder.IndexOf(scope);
            if (index < 0) return;

            _suppressScopeChange = true;
            ScopeCombo.SelectedIndex = index;
            _suppressScopeChange = false;
        }

        private static string GetScopeDisplayName(DashboardScope scope)
        {
            switch (scope)
            {
                case DashboardScope.Categories:         return "Categories";
                case DashboardScope.Items:              return "Items";
                case DashboardScope.InventoryMovements: return "Inventory Movements";
                case DashboardScope.Requests:           return "Requests";
                case DashboardScope.Renewals:           return "Renewals";
                case DashboardScope.InvoicesServices:   return "Invoices & Services";
                case DashboardScope.Employees:          return "Employees";
                case DashboardScope.Vendors:            return "Vendors";
                case DashboardScope.ArchivedEntities:   return "Archived Entities";
                default:                                return scope.ToString();
            }
        }

        // ------------------------------------------------------------------
        // Rendering
        // ------------------------------------------------------------------

        private void RenderDashboard(DashboardData data)
        {
            StatusText.Visibility = Visibility.Collapsed;

            TitleText.Text = CleanTitle(data?.Title) ?? "Dashboard";

            BuildKpis(data?.Kpis);
            BuildQuickStats(data?.Stat1, data?.Stat2, data?.Stat3);
            BuildCharts(data);
            BuildTable(data);

            KpiGrid.Visibility    = KpiGrid.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            StatsCard.Visibility  = StatsGrid.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ChartsGrid.Visibility = ChartsGrid.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            TableCard.Visibility  = Visibility.Visible;
        }

        private void ShowStatus(string message, bool isError)
        {
            StatusText.Text = message;
            StatusText.Foreground = isError ? RedBrush : MutedBrush;
            StatusText.Visibility = Visibility.Visible;
        }

        private void HideContent()
        {
            KpiGrid.Children.Clear();
            KpiGrid.Visibility = Visibility.Collapsed;

            StatsGrid.Children.Clear();
            StatsGrid.ColumnDefinitions.Clear();
            StatsCard.Visibility = Visibility.Collapsed;

            ChartsGrid.Children.Clear();
            ChartsGrid.Visibility = Visibility.Collapsed;

            TableGrid.Columns.Clear();
            TableGrid.ItemsSource = null;
            TableCard.Visibility = Visibility.Collapsed;
        }

        // ------------------------------------------------------------------
        // KPI cards
        // ------------------------------------------------------------------

        private void BuildKpis(List<DashboardKpi> kpis)
        {
            KpiGrid.Children.Clear();
            if (kpis == null || kpis.Count == 0) return;

            KpiGrid.Columns = kpis.Count;
            foreach (var kpi in kpis)
                KpiGrid.Children.Add(BuildKpiCard(kpi));
        }

        private Border BuildKpiCard(DashboardKpi kpi)
        {
            var accentBrush = new SolidColorBrush(ToMediaColor(kpi.AccentColor));

            var card = new Border
            {
                Style = (Style)FindResource("DashCard"),
                Margin = new Thickness(7),
                MinHeight = 98,
                Cursor = Cursors.Hand,
                ToolTip = $"Click to view {kpi.Title} details"
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var labelRow = new StackPanel { Orientation = Orientation.Horizontal };
            labelRow.Children.Add(new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = accentBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            labelRow.Children.Add(new TextBlock
            {
                Text = (kpi.Title ?? string.Empty).ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = MutedBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            left.Children.Add(labelRow);

            left.Children.Add(new TextBlock
            {
                Text = FormatNumber(kpi.Value),
                FontSize = 26,
                FontWeight = FontWeights.SemiBold,
                Foreground = InkBrush,
                Margin = new Thickness(0, 8, 0, 0)
            });
            grid.Children.Add(left);

            if (!string.IsNullOrWhiteSpace(kpi.Icon))
            {
                var iconCircle = new Border
                {
                    Width = 32,
                    Height = 32,
                    CornerRadius = new CornerRadius(16),
                    Background = SoftCircleBrush,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(10, 0, 0, 0)
                };
                iconCircle.Child = new TextBlock
                {
                    Text = kpi.Icon,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(iconCircle, 1);
                grid.Children.Add(iconCircle);
            }

            card.Child = grid;

            card.MouseEnter += (s, e) =>
            {
                card.Background = HoverBrush;
                card.BorderBrush = accentBrush;
            };
            card.MouseLeave += (s, e) =>
            {
                card.Background = Brushes.White;
                card.BorderBrush = HairlineBrush;
            };
            card.MouseLeftButtonUp += (s, e) => OnSummaryCardClick(kpi.Title);

            return card;
        }

        // ------------------------------------------------------------------
        // Quick stats
        // ------------------------------------------------------------------

        private void BuildQuickStats(DashboardQuickStat stat1, DashboardQuickStat stat2, DashboardQuickStat stat3)
        {
            StatsGrid.Children.Clear();
            StatsGrid.ColumnDefinitions.Clear();

            var stats = new List<DashboardQuickStat>();
            if (stat1 != null) stats.Add(stat1);
            if (stat2 != null) stats.Add(stat2);
            if (stat3 != null) stats.Add(stat3);
            if (stats.Count == 0) return;

            for (int i = 0; i < stats.Count; i++)
            {
                if (i > 0)
                {
                    StatsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var separator = new Border
                    {
                        Width = 1,
                        Background = HairlineBrush,
                        Margin = new Thickness(0, 10, 0, 10)
                    };
                    Grid.SetColumn(separator, StatsGrid.ColumnDefinitions.Count - 1);
                    StatsGrid.Children.Add(separator);
                }

                StatsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var stat = stats[i];
                var colorBrush = new SolidColorBrush(ToMediaColor(stat.Color));

                var cell = new Border
                {
                    Padding = new Thickness(10),
                    CornerRadius = new CornerRadius(8),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    ToolTip = $"Click to view {stat.Label} details"
                };

                var stack = new StackPanel();

                var labelRow = new StackPanel { Orientation = Orientation.Horizontal };
                labelRow.Children.Add(new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = colorBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                });
                labelRow.Children.Add(new TextBlock
                {
                    Text = (stat.Label ?? string.Empty).ToUpperInvariant(),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = MutedBrush,
                    VerticalAlignment = VerticalAlignment.Center
                });
                stack.Children.Add(labelRow);

                stack.Children.Add(new TextBlock
                {
                    Text = FormatNumberString(stat.Value),
                    FontSize = 22,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = InkBrush,
                    Margin = new Thickness(0, 6, 0, 0)
                });
                cell.Child = stack;

                cell.MouseEnter += (s, e) => cell.Background = HoverBrush;
                cell.MouseLeave += (s, e) => cell.Background = Brushes.Transparent;
                cell.MouseLeftButtonUp += (s, e) => OnSummaryCardClick(stat.Label);

                Grid.SetColumn(cell, StatsGrid.ColumnDefinitions.Count - 1);
                StatsGrid.Children.Add(cell);
            }
        }

        // ------------------------------------------------------------------
        // Charts
        // ------------------------------------------------------------------

        private void BuildCharts(DashboardData data)
        {
            ChartsGrid.Children.Clear();
            if (data == null) return;

            ChartsGrid.Children.Add(BuildChartCard(data.PrimaryChartTitle, BuildPieChart(data.PrimaryChartMetrics, data.PrimaryChartTitle)));
            ChartsGrid.Children.Add(BuildChartCard(data.SecondaryChartTitle, BuildBars(data.SecondaryChartMetrics, data.SecondaryChartTitle)));
            ChartsGrid.Children.Add(BuildChartCard(data.TertiaryChartTitle, BuildDonutChart(data.TertiaryChartMetrics, data.TertiaryChartTitle)));
        }

        private Border BuildChartCard(string title, UIElement content)
        {
            var card = new Border
            {
                Style = (Style)FindResource("DashCard"),
                Margin = new Thickness(7),
                MinHeight = 372
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = (CleanTitle(title) ?? string.Empty).ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = MutedBrush,
                TextWrapping = TextWrapping.Wrap
            });

            if (content != null)
                stack.Children.Add(content);

            card.Child = stack;
            return card;
        }

        private UIElement BuildPieChart(List<DashboardMetric> metrics, string chartTitle)
        {
            var list = (metrics ?? new List<DashboardMetric>())
                .Where(m => m != null && m.Value > 0)
                .ToList();
            double total = list.Sum(m => (double)m.Value);

            var stack = new StackPanel();
            if (list.Count == 0 || total <= 0)
            {
                stack.Children.Add(BuildNoData());
                return stack;
            }

            var canvas = new Canvas
            {
                Width = 240,
                Height = 240,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 4)
            };
            var center = new Point(120, 120);
            const double radius = 106;

            double start = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var metric = list[i];
                double sweep = 360.0 * metric.Value / total;
                var slice = CreatePieSlice(center, radius, start, start + sweep, PaletteBrush(i));
                slice.Tag = metric;
                slice.Cursor = Cursors.Hand;
                slice.ToolTip = $"{metric.Label}: {FormatNumber(metric.Value)} ({FormatPercent(metric.Value, total)})";
                slice.MouseLeftButtonUp += (s, e) => HandleChartClick(metric, chartTitle, ChartKind.Primary);
                canvas.Children.Add(slice);
                start += sweep;
            }

            stack.Children.Add(canvas);
            stack.Children.Add(BuildLegend(list, i => PaletteBrush(i)));
            return stack;
        }

        private UIElement BuildDonutChart(List<DashboardMetric> metrics, string chartTitle)
        {
            var list = (metrics ?? new List<DashboardMetric>())
                .Where(m => m != null && m.Value > 0)
                .ToList();
            double total = list.Sum(m => (double)m.Value);

            var stack = new StackPanel();
            if (list.Count == 0 || total <= 0)
            {
                stack.Children.Add(BuildNoData());
                return stack;
            }

            var host = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 4)
            };

            var canvas = new Canvas { Width = 240, Height = 240 };
            var center = new Point(120, 120);
            const double outer = 106;
            const double inner = 64;

            double start = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var metric = list[i];
                double sweep = 360.0 * metric.Value / total;
                AddDonutSlice(canvas, center, outer, inner, start, start + sweep,
                    SemanticBrush(metric.Label, i), metric, chartTitle, total);
                start += sweep;
            }

            host.Children.Add(canvas);

            var centerText = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            centerText.Children.Add(new TextBlock
            {
                Text = FormatNumber((long)total),
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = InkBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            centerText.Children.Add(new TextBlock
            {
                Text = "total",
                FontSize = 10,
                Foreground = MutedBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            });
            host.Children.Add(centerText);

            stack.Children.Add(host);
            stack.Children.Add(BuildLegend(list, i => SemanticBrush(list[i].Label, i)));
            return stack;
        }

        private UIElement BuildBars(List<DashboardMetric> metrics, string chartTitle)
        {
            var list = (metrics ?? new List<DashboardMetric>()).Where(m => m != null).ToList();

            var stack = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            if (list.Count == 0)
            {
                stack.Children.Add(BuildNoData());
                return stack;
            }

            double max = Math.Max(1.0, list.Max(m => (double)m.Value));

            foreach (var metric in list)
            {
                var metricRef = metric;
                double value = Math.Max(0.0, metric.Value);

                var row = new Border
                {
                    Padding = new Thickness(8, 5, 8, 5),
                    CornerRadius = new CornerRadius(6),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    ToolTip = $"{metric.Label}: {FormatNumber(metric.Value)}"
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var label = new TextBlock
                {
                    Text = metric.Label ?? string.Empty,
                    FontSize = 12.5,
                    Foreground = InkBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
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
                    Background = BarBrush,
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
                    MinWidth = 56
                };
                Grid.SetColumn(valueText, 2);
                grid.Children.Add(valueText);

                row.Child = grid;
                row.MouseEnter += (s, e) => row.Background = HoverBrush;
                row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
                row.MouseLeftButtonUp += (s, e) => HandleChartClick(metricRef, chartTitle, ChartKind.Secondary);

                stack.Children.Add(row);
            }

            return stack;
        }

        private void AddDonutSlice(Canvas canvas, Point center, double outer, double inner,
            double startDeg, double endDeg, Brush fill, DashboardMetric metric, string chartTitle, double total)
        {
            double sweep = endDeg - startDeg;
            var shapes = new List<Shape>();

            if (sweep >= 359.99)
            {
                shapes.Add(CreateDonutSector(center, outer, inner, 0, 180, fill));
                shapes.Add(CreateDonutSector(center, outer, inner, 180, 360, fill));
            }
            else
            {
                shapes.Add(CreateDonutSector(center, outer, inner, startDeg, endDeg, fill));
            }

            foreach (var shape in shapes)
            {
                var metricRef = metric;
                shape.Tag = metricRef;
                shape.Cursor = Cursors.Hand;
                shape.ToolTip = $"{metricRef.Label}: {FormatNumber(metricRef.Value)} ({FormatPercent(metricRef.Value, total)})";
                shape.MouseLeftButtonUp += (s, e) => HandleChartClick(metricRef, chartTitle, ChartKind.Tertiary);
                canvas.Children.Add(shape);
            }
        }

        private static Shape CreatePieSlice(Point center, double radius, double startDeg, double endDeg, Brush fill)
        {
            double sweep = endDeg - startDeg;
            if (sweep >= 359.99)
            {
                return new Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Fill = fill,
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5,
                    Margin = new Thickness(center.X - radius, center.Y - radius, 0, 0)
                };
            }

            var figure = new PathFigure { StartPoint = center, IsClosed = true, IsFilled = true };
            figure.Segments.Add(new LineSegment(Polar(center, radius, startDeg), false));
            figure.Segments.Add(new ArcSegment(Polar(center, radius, endDeg), new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise, false));

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return new Path { Data = geometry, Fill = fill, Stroke = Brushes.White, StrokeThickness = 1.5 };
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

        private static UIElement BuildLegend(List<DashboardMetric> metrics, Func<int, Brush> colorSelector)
        {
            var wrap = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };

            for (int i = 0; i < metrics.Count; i++)
            {
                var metric = metrics[i];
                var chip = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 2, 16, 2),
                    MaxWidth = 210
                };
                chip.Children.Add(new Ellipse
                {
                    Width = 9,
                    Height = 9,
                    Fill = colorSelector(i),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 7, 0)
                });
                chip.Children.Add(new TextBlock
                {
                    Text = $"{metric.Label} ({FormatNumber(metric.Value)})",
                    FontSize = 11.5,
                    Foreground = MutedBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = metric.Label
                });
                wrap.Children.Add(chip);
            }

            return wrap;
        }

        private static UIElement BuildNoData()
        {
            return new TextBlock
            {
                Text = "No data for this scope.",
                FontSize = 12.5,
                Foreground = MutedBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 36, 0, 0)
            };
        }

        // ------------------------------------------------------------------
        // Data table
        // ------------------------------------------------------------------

        private void BuildTable(DashboardData data)
        {
            TableGrid.Columns.Clear();
            TableGrid.ItemsSource = null;
            TableGrid.Visibility = Visibility.Visible;

            var rows = data?.TableData ?? new List<DashboardTableRow>();
            TableTitle.Text = (CleanTitle(data?.TableTitle) ?? "Data").ToUpperInvariant();
            TableCount.Text = rows.Count == 0
                ? "No records"
                : $"Showing {rows.Count} record{(rows.Count == 1 ? string.Empty : "s")}";

            if (rows.Count == 0)
            {
                TableGrid.Visibility = Visibility.Collapsed;
                return;
            }

            bool skipArchivedColumns = _currentScope == DashboardScope.ArchivedEntities;

            var includedColumns = new List<int>();
            for (int col = 1; col <= 7; col++)
            {
                if (skipArchivedColumns && (col == 3 || col == 6)) continue;
                if (rows.Any(r => !string.IsNullOrWhiteSpace(GetCell(r, col))))
                    includedColumns.Add(col);
            }

            if (includedColumns.Count == 0)
            {
                TableGrid.Visibility = Visibility.Collapsed;
                return;
            }

            bool hasStatus = rows.Any(r => !string.IsNullOrWhiteSpace(r.StatusBadge));

            var baseCell    = (Style)FindResource("TableCellBase");
            var rightCell   = (Style)FindResource("TableCellRight");
            var baseHeader  = (Style)FindResource("TableHeaderBase");
            var rightHeader = (Style)FindResource("TableHeaderRight");

            for (int position = 0; position < includedColumns.Count; position++)
            {
                int columnNumber = includedColumns[position];
                bool numeric = IsNumericColumn(rows, columnNumber);

                TableGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = GetColumnHeader(columnNumber).ToUpperInvariant(),
                    Binding = new System.Windows.Data.Binding("Cells[" + position + "]"),
                    CellStyle = numeric ? rightCell : baseCell,
                    HeaderStyle = numeric ? rightHeader : baseHeader,
                    Width = position == 0
                        ? new DataGridLength(1, DataGridLengthUnitType.Star)
                        : DataGridLength.Auto,
                    MinWidth = 90
                });
            }

            if (hasStatus)
            {
                TableGrid.Columns.Add(new DataGridTemplateColumn
                {
                    Header = "STATUS",
                    CellTemplate = (DataTemplate)FindResource("StatusCellTemplate"),
                    CellStyle = baseCell,
                    HeaderStyle = baseHeader,
                    Width = DataGridLength.Auto,
                    MinWidth = 80
                });
            }

            var gridRows = new List<DashboardGridRow>();
            foreach (var row in rows)
            {
                var cells = new object[includedColumns.Count];
                for (int i = 0; i < includedColumns.Count; i++)
                    cells[i] = GetCell(row, includedColumns[i]) ?? string.Empty;

                var statusBrush = row.StatusColor.IsEmpty
                    ? InkBrush
                    : (Brush)new SolidColorBrush(ToMediaColor(row.StatusColor));

                gridRows.Add(new DashboardGridRow
                {
                    Cells = cells,
                    Status = row.StatusBadge ?? string.Empty,
                    StatusBrush = statusBrush
                });
            }

            TableGrid.ItemsSource = gridRows;
        }

        private string GetColumnHeader(int columnNumber)
        {
            switch (_currentScope)
            {
                case DashboardScope.Categories:
                    switch (columnNumber)
                    {
                        case 1: return "Category Name";
                        case 2: return "Active Stock";
                        case 3: return "Good";
                        case 4: return "Damaged";
                        case 5: return "Item Records";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.Items:
                    switch (columnNumber)
                    {
                        case 1: return "Item Name";
                        case 2: return "Category";
                        case 3: return "Item Type";
                        case 4: return "Stock";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.InventoryMovements:
                    switch (columnNumber)
                    {
                        case 1: return "Date";
                        case 2: return "Item Name";
                        case 3: return "Category";
                        case 4: return "Entry Type";
                        case 5: return "Stock +/-";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.Requests:
                    switch (columnNumber)
                    {
                        case 1: return "Date Requested";
                        case 2: return "Employee";
                        case 3: return "Item Name";
                        case 4: return "Category";
                        case 5: return "Quantity";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.Renewals:
                    switch (columnNumber)
                    {
                        case 1: return "Item Name";
                        case 2: return "Invoice Type";
                        case 3: return "Renewal Start Date";
                        case 4: return "Renewal End Date";
                        case 5: return "Days Before Expiry";
                        case 6: return "Renewal Status";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.InvoicesServices:
                    switch (columnNumber)
                    {
                        case 1: return "Set Code";
                        case 2: return "Document Number";
                        case 3: return "Invoice Type";
                        case 4: return "Date Created";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.Employees:
                    switch (columnNumber)
                    {
                        case 1: return "Employee Name";
                        case 2: return "Position";
                        case 3: return "Department";
                        case 4: return "Branch";
                        case 5: return "Company";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.Vendors:
                    switch (columnNumber)
                    {
                        case 1: return "Vendor Name";
                        case 2: return "Item Quantity";
                        case 3: return "Invoices";
                        default: return $"Column {columnNumber}";
                    }

                case DashboardScope.ArchivedEntities:
                    switch (columnNumber)
                    {
                        case 1: return "Table";
                        case 2: return "Data Name";
                        case 4: return "Archive Date";
                        case 5: return "Archived By";
                        default: return $"Column {columnNumber}";
                    }

                default:
                    return $"Column {columnNumber}";
            }
        }

        private static string GetCell(DashboardTableRow row, int column)
        {
            switch (column)
            {
                case 1: return row.Col1;
                case 2: return row.Col2;
                case 3: return row.Col3;
                case 4: return row.Col4;
                case 5: return row.Col5;
                case 6: return row.Col6;
                case 7: return row.Col7;
                default: return null;
            }
        }

        private static bool IsNumericColumn(List<DashboardTableRow> rows, int column)
        {
            bool any = false;
            foreach (var row in rows)
            {
                var value = GetCell(row, column);
                if (string.IsNullOrWhiteSpace(value)) continue;

                any = true;
                var probe = value.Trim().TrimEnd('%');
                if (!double.TryParse(probe, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                    return false;
            }
            return any;
        }

        // ------------------------------------------------------------------
        // Drill-downs (same routing as the legacy DashboardView)
        // ------------------------------------------------------------------

        private void OnSummaryCardClick(string cardTitle)
        {
            var context = MapScopeToContext(_currentScope);
            if (context == null) return;

            string filterKey;
            switch ((cardTitle ?? string.Empty).ToLowerInvariant())
            {
                case "total movements":
                case "out of stock":
                case "low stock":
                case "in stock":
                case "good condition":
                case "damaged items":
                    filterKey = cardTitle;
                    break;
                case "total quantity":
                case "total items":
                case "total categories":
                case "total employees":
                case "total requests":
                case "total renewals":
                case "total invoices":
                case "total vendors":
                case "total archived":
                    filterKey = null;
                    break;
                default:
                    filterKey = cardTitle;
                    break;
            }

            RunDrillDown(new DrillDownRequest
            {
                Context = context.Value,
                FilterKey = filterKey
            });
        }

        private void HandleChartClick(DashboardMetric metric, string chartTitle, ChartKind kind)
        {
            if (metric == null) return;

            var context = MapScopeToContext(_currentScope);
            if (context == null) return;

            var categoryName = (metric.Label ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(categoryName)) return;

            var normalizedTitle = (chartTitle ?? string.Empty).Trim();
            bool isDonut = kind == ChartKind.Tertiary;

            var effectiveContext = context.Value;
            string filterKey = null;

            // Scope-specific mappings so the drill-down shows the underlying records
            // a user expects. Mirrors the legacy DashboardView chart click routing.
            if (_currentScope == DashboardScope.Categories)
            {
                // Categories charts drill into Items, not the category summary rows.
                effectiveContext = DrillDownContext.Items;

                if (isDonut)
                {
                    if (string.Equals(categoryName, "Good", StringComparison.OrdinalIgnoreCase))
                        filterKey = "Good Condition";
                    else if (string.Equals(categoryName, "Damaged", StringComparison.OrdinalIgnoreCase))
                        filterKey = "Damaged Items";
                    else
                        filterKey = categoryName;
                }
                else
                {
                    filterKey = categoryName;
                }
            }
            else if (_currentScope == DashboardScope.InvoicesServices && isDonut)
            {
                // Invoices donut uses Yes/No labels for Approved/Not Approved.
                if (string.Equals(categoryName, "Yes", StringComparison.OrdinalIgnoreCase))
                    filterKey = "Approved";
                else if (string.Equals(categoryName, "No", StringComparison.OrdinalIgnoreCase))
                    filterKey = "Not Approved";
                else
                    filterKey = categoryName;
            }
            else if (_currentScope == DashboardScope.ArchivedEntities && isDonut)
            {
                filterKey = categoryName;
            }
            else if (_currentScope == DashboardScope.Renewals)
            {
                // Renewals bar chart labels are item names (Expiring Soon).
                if (normalizedTitle.IndexOf("EXPIRING", StringComparison.OrdinalIgnoreCase) >= 0)
                    filterKey = "Item:" + categoryName;
            }
            else if (_currentScope == DashboardScope.Requests)
            {
                // Requests bar chart labels are item names (Top Requested Items).
                if (normalizedTitle.IndexOf("TOP REQUEST", StringComparison.OrdinalIgnoreCase) >= 0)
                    filterKey = "Item:" + categoryName;
            }

            // Label pass-through exists only for the scopes the drill-down understands;
            // other scopes intentionally open the unfiltered list, same as before.
            if (filterKey == null &&
                (effectiveContext == DrillDownContext.Categories ||
                 effectiveContext == DrillDownContext.Items ||
                 effectiveContext == DrillDownContext.Movements ||
                 effectiveContext == DrillDownContext.Vendors ||
                 effectiveContext == DrillDownContext.Employees ||
                 effectiveContext == DrillDownContext.Requests))
            {
                if (effectiveContext == DrillDownContext.Items &&
                    normalizedTitle.IndexOf("TOP ITEMS", StringComparison.OrdinalIgnoreCase) >= 0)
                    filterKey = "Item:" + categoryName;
                else
                    filterKey = categoryName;
            }

            RunDrillDown(new DrillDownRequest
            {
                Context = effectiveContext,
                FilterKey = filterKey
            });
        }

        private async void RunDrillDown(DrillDownRequest request)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var data = await Task.Run(() => _service.GetDrillDownItems(request));
                Mouse.OverrideCursor = null;

                string title = request.Context.ToString();
                if (!string.IsNullOrWhiteSpace(request.FilterKey))
                    title += " – " + request.FilterKey;

                var dialog = new DashboardDrillDownDialog();
                dialog.SetDataFromDto(data, title);
                ShowDialogWithOwner(dialog);
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                System.Windows.MessageBox.Show(
                    $"Error loading drill-down data: {ex.Message}",
                    "Drill-Down Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static void ShowDialogWithOwner(System.Windows.Forms.Form dialog)
        {
            var owner = System.Windows.Forms.Form.ActiveForm;
            if (owner != null && !owner.IsDisposed)
                dialog.ShowDialog(owner);
            else
                dialog.ShowDialog();
        }

        private static DrillDownContext? MapScopeToContext(DashboardScope scope)
        {
            switch (scope)
            {
                case DashboardScope.Categories:         return DrillDownContext.Categories;
                case DashboardScope.Items:              return DrillDownContext.Items;
                case DashboardScope.InventoryMovements: return DrillDownContext.Movements;
                case DashboardScope.Requests:           return DrillDownContext.Requests;
                case DashboardScope.Renewals:           return DrillDownContext.Renewals;
                case DashboardScope.InvoicesServices:   return DrillDownContext.Invoices;
                case DashboardScope.Employees:          return DrillDownContext.Employees;
                case DashboardScope.Vendors:            return DrillDownContext.Vendors;
                case DashboardScope.ArchivedEntities:   return DrillDownContext.Archived;
                default:                                return null;
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static Brush Hex(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private static Brush PaletteBrush(int index)
        {
            int safeIndex = ((index % ChartPalette.Length) + ChartPalette.Length) % ChartPalette.Length;
            return ChartPalette[safeIndex];
        }

        private static Brush SemanticBrush(string label, int index)
        {
            var l = (label ?? string.Empty).Trim().ToLowerInvariant();

            if (l.Contains("damaged") || l.Contains("negative") || l.Contains("expired") ||
                l.Contains("inactive") || l.Contains("out of stock") || l == "no")
                return RedBrush;

            if (l.Contains("hold") || l.Contains("low") || l.Contains("pending") || l.Contains("soon"))
                return AmberBrush;

            if (l.Contains("good") || l.Contains("positive") || l.Contains("in stock") ||
                l.Contains("active") || l == "yes" || l.Contains("approved") || l.Contains("completed"))
                return GreenBrush;

            return PaletteBrush(index);
        }

        private static string CleanTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            var cleaned = Regex.Replace(title.Trim(), @"^[^\p{L}\p{N}]+", string.Empty).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? title.Trim() : cleaned;
        }

        private static string FormatNumber(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

        private static string FormatNumber(long value) => value.ToString("N0", CultureInfo.CurrentCulture);

        private static string FormatNumberString(string value)
        {
            if (long.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var parsed))
                return parsed.ToString("N0", CultureInfo.CurrentCulture);
            return value ?? string.Empty;
        }

        private static string FormatPercent(double value, double total)
            => total <= 0 ? "0%" : (value / total * 100.0).ToString("0.#") + "%";

        private static Color ToMediaColor(DrawingColor color)
            => Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    /// <summary>
    /// Flat row wrapper for the dashboard DataGrid (columns bind into the Cells array).
    /// </summary>
    public sealed class DashboardGridRow
    {
        public object[] Cells { get; set; }
        public string Status { get; set; }
        public Brush StatusBrush { get; set; }
    }
}
