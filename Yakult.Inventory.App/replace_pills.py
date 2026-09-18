import re
import os

file_path = r'c:\Users\russel.mercado\source\repos\Yakult-System-for-Merging - Copy\Yakult.Inventory.App\Wpf\CallMonitoring\WpfCallMonitoringDiagnosticsWorkspace.cs'

with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Remove the 4 row definitions and replace with 3
row_defs_old = """            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Hero
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Metrics
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Content (Summary + Email)
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3: Lower Content (Schema + Scheduler)"""
row_defs_new = """            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Hero
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Content (Summary + Email)
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Lower Content (Schema + Scheduler)"""
content = content.replace(row_defs_old, row_defs_new)

# 2. Fix Grid.SetRow for midGrid and bottomGrid
content = content.replace("Grid.SetRow(midGrid, 2);", "Grid.SetRow(midGrid, 1);")
content = content.replace("Grid.SetRow(bottomGrid, 3);", "Grid.SetRow(bottomGrid, 2);")

# 3. Remove metricGrid creation from BuildUi
metric_grid_build_ui = """            var metricGrid = new Grid { Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AddMetricPill(metricGrid, 0, "Open", _lblOpenValue, Color.FromRgb(59, 130, 246));
            AddMetricPill(metricGrid, 1, "Pending", _lblPendingValue, Color.FromRgb(245, 158, 11));
            AddMetricPill(metricGrid, 2, "Critical", _lblCriticalValue, Color.FromRgb(239, 68, 68));
            AddMetricPill(metricGrid, 3, "Today", _lblTodayValue, Color.FromRgb(16, 185, 129));
            Grid.SetRow(metricGrid, 1);
            rootGrid.Children.Add(metricGrid);"""
content = content.replace(metric_grid_build_ui, "")

# 4. Add metricGrid creation to BuildSystemSummaryCard
stack_children_add_grid = "            stack.Children.Add(grid);"

metric_grid_in_summary = """            stack.Children.Add(grid);
            
            var metricGrid = new Grid { Margin = new Thickness(0, 24, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AddMetricPill(metricGrid, 0, "Open", _lblOpenValue, Color.FromRgb(59, 130, 246));
            AddMetricPill(metricGrid, 1, "Pending", _lblPendingValue, Color.FromRgb(245, 158, 11));
            AddMetricPill(metricGrid, 2, "Critical", _lblCriticalValue, Color.FromRgb(239, 68, 68));
            AddMetricPill(metricGrid, 3, "Today", _lblTodayValue, Color.FromRgb(16, 185, 129));
            stack.Children.Add(metricGrid);"""
content = content.replace(stack_children_add_grid, metric_grid_in_summary)

# 5. Add HorizontalScrollBarVisibility to DataGrid
schema_grid_old = """            _schemaGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Background = Brushes.White,
                RowBackground = Brushes.White,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 16, 0, 0),
                Height = 350
            };"""
schema_grid_new = """            _schemaGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.White,
                RowBackground = Brushes.White,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 16, 0, 0),
                Height = 350
            };"""
content = content.replace(schema_grid_old, schema_grid_new)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)
print("done")
