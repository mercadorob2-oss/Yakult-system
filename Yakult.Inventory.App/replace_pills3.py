import re

with open('Wpf/CallMonitoring/WpfCallMonitoringDiagnosticsWorkspace.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Replace the metricPanel StackPanel with a Grid that fills width
old1 = r'''            var metricPanel = new StackPanel \{ Orientation = System\.Windows\.Controls\.Orientation\.Horizontal, Margin = new Thickness\(0, 14, 0, 0\) \};
            AddMetricPill\(metricPanel, "Open", _lblOpenValue, Color\.FromRgb\(59, 130, 246\)\);
            AddMetricPill\(metricPanel, "Pending", _lblPendingValue, Color\.FromRgb\(245, 158, 11\)\);
            AddMetricPill\(metricPanel, "Critical", _lblCriticalValue, Color\.FromRgb\(239, 68, 68\)\);
            AddMetricPill\(metricPanel, "Today", _lblTodayValue, Color\.FromRgb\(16, 185, 129\)\);
            stack\.Children\.Add\(metricPanel\);'''

new1 = '''            var metricGrid = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AddMetricPill(metricGrid, 0, "Open", _lblOpenValue, Color.FromRgb(59, 130, 246));
            AddMetricPill(metricGrid, 1, "Pending", _lblPendingValue, Color.FromRgb(245, 158, 11));
            AddMetricPill(metricGrid, 2, "Critical", _lblCriticalValue, Color.FromRgb(239, 68, 68));
            AddMetricPill(metricGrid, 3, "Today", _lblTodayValue, Color.FromRgb(16, 185, 129));
            stack.Children.Add(metricGrid);'''

content = re.sub(old1, new1, content, flags=re.DOTALL)

# 2. Replace AddMetricPill signature to accept Grid + column index
old2 = r'private void AddMetricPill\(StackPanel parent, string title, TextBlock valueBlock, Color accentColor\).*?parent\.Children\.Add\(pill\);\s*\}'
new2 = r'''private void AddMetricPill(Grid parent, int column, string title, TextBlock valueBlock, Color accentColor)
        {
            double leftMargin = column > 0 ? 10 : 0;
            var pill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(248, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(223, 229, 236)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(leftMargin, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 10,
                    Color = Color.FromArgb(20, 15, 23, 42),
                    ShadowDepth = 0,
                    Opacity = 0.15
                }
            };

            var accentBar = new Border
            {
                Width = 4,
                Height = 38,
                CornerRadius = new CornerRadius(999),
                Background = new SolidColorBrush(accentColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            });
            valueBlock.FontSize = 26;
            valueBlock.FontWeight = FontWeights.Bold;
            valueBlock.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            valueBlock.Margin = new Thickness(0, 2, 0, 0);
            textStack.Children.Add(valueBlock);

            var innerRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            innerRow.Children.Add(accentBar);
            innerRow.Children.Add(textStack);
            pill.Child = innerRow;
            Grid.SetColumn(pill, column);
            parent.Children.Add(pill);
        }'''

content = re.sub(old2, new2, content, flags=re.DOTALL)

with open('Wpf/CallMonitoring/WpfCallMonitoringDiagnosticsWorkspace.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print('Done!')
