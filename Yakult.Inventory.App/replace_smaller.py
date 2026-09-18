import re

with open('Wpf/CallMonitoring/WpfCallMonitoringDiagnosticsWorkspace.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Replace 1: AddMetricCard
old1 = r'private void AddMetricCard\(Grid grid, int column, int row, string title, string subtitle, TextBlock valueBlock, Color accentColor\).*?Grid\.SetColumn\(card, column\);\s*Grid\.SetRow\(card, row\);\s*grid\.Children\.Add\(card\);\s*\}'
new1 = r'''private void AddMetricCard(Grid grid, int column, int row, string title, string subtitle, TextBlock valueBlock, Color accentColor)
        {
            var card = CreateGlassCard();
            card.Padding = new Thickness(14, 12, 14, 12);
            
            double leftMargin = column > 0 ? 12 : 0;
            double topMargin = row > 0 ? 12 : 0;
            card.Margin = new Thickness(leftMargin, topMargin, 0, 0);

            var stack = new StackPanel();
            stack.Children.Add(new Border
            {
                Width = 24,
                Height = 4,
                CornerRadius = new CornerRadius(999),
                Background = new SolidColorBrush(accentColor),
                HorizontalAlignment = HorizontalAlignment.Left
            });
            stack.Children.Add(new TextBlock
            {
                Text = title,
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105))
            });
            stack.Children.Add(valueBlock);
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                Margin = new Thickness(0, 2, 0, 0),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                TextWrapping = TextWrapping.Wrap
            });

            card.Child = stack;
            Grid.SetColumn(card, column);
            Grid.SetRow(card, row);
            grid.Children.Add(card);
        }'''

content = re.sub(old1, new1, content, flags=re.DOTALL)

# Replace 2: CreateMetricValue
old2 = r'private TextBlock CreateMetricValue\(\)\s*\{\s*return new TextBlock\s*\{\s*Margin = new Thickness\(0, 10, 0, 0\),\s*FontSize = 30,\s*FontWeight = FontWeights\.Bold,\s*Foreground = new SolidColorBrush\(Color\.FromRgb\(15, 23, 42\)\)\s*\};\s*\}'
new2 = r'''private TextBlock CreateMetricValue()
        {
            return new TextBlock
            {
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            };
        }'''

content = re.sub(old2, new2, content, flags=re.DOTALL)

with open('Wpf/CallMonitoring/WpfCallMonitoringDiagnosticsWorkspace.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print("Replaced!")
