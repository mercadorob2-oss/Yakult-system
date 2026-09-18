import re

with open('Wpf/CallMonitoring/WpfCallMonitoringDiagnosticsWorkspace.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Replace AddMetricPill with larger sizing
old1 = r'private void AddMetricPill\(StackPanel parent, string title, TextBlock valueBlock, Color accentColor\).*?parent\.Children\.Add\(pill\);\s*\}'
new1 = r'''private void AddMetricPill(StackPanel parent, string title, TextBlock valueBlock, Color accentColor)
        {
            var pill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(248, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(223, 229, 236)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 14, 0),
                MinWidth = 80,
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
            parent.Children.Add(pill);
        }'''

content = re.sub(old1, new1, content, flags=re.DOTALL)

with open('Wpf/CallMonitoring/WpfCallMonitoringDiagnosticsWorkspace.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print('Done!')
