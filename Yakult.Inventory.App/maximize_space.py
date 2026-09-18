import re

with open('Wpf/CallMonitoring/WpfCallMonitoringDiagnosticsWorkspace.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Update AddMetricPill to center content and be even larger
old1 = r'private void AddMetricPill\(Grid parent, int column, string title, TextBlock valueBlock, Color accentColor\).*?parent\.Children\.Add\(pill\);\s*\}'
new1 = r'''private void AddMetricPill(Grid parent, int column, string title, TextBlock valueBlock, Color accentColor)
        {
            double leftMargin = column > 0 ? 16 : 0;
            var pill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(248, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(223, 229, 236)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(leftMargin, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 12,
                    Color = Color.FromArgb(25, 15, 23, 42),
                    ShadowDepth = 0,
                    Opacity = 0.15
                }
            };

            var accentBar = new Border
            {
                Width = 5,
                Height = 44,
                CornerRadius = new CornerRadius(999),
                Background = new SolidColorBrush(accentColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            });
            valueBlock.FontSize = 32;
            valueBlock.FontWeight = FontWeights.Bold;
            valueBlock.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            valueBlock.Margin = new Thickness(0, 2, 0, 0);
            textStack.Children.Add(valueBlock);

            var innerGrid = new Grid();
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            Grid.SetColumn(accentBar, 0);
            Grid.SetColumn(textStack, 1);
            innerGrid.Children.Add(accentBar);
            innerGrid.Children.Add(textStack);
            
            pill.Child = innerGrid;
            Grid.SetColumn(pill, column);
            parent.Children.Add(pill);
        }'''

content = re.sub(old1, new1, content, flags=re.DOTALL)

# 2. Adjust metricGrid margin to use more space
old2 = r'var metricGrid = new Grid { Margin = new Thickness(0, 14, 0, 0) };'
new2 = r'var metricGrid = new Grid { Margin = new Thickness(0, 24, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };'

content = content.replace(old2, new2)

with open('Wpf/CallMonitoring/WpfCallMonitoringDiagnosticsWorkspace.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print('Done!')
