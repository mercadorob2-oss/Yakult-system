import re
import os

file_path = r'c:\Users\russel.mercado\source\repos\Yakult-System-for-Merging - Copy\Yakult.Inventory.App\Wpf\CallMonitoring\WpfCallMonitoringDiagnosticsWorkspace.cs'

with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Add scrollbar
scrollbar_xaml = """
            var scrollbarXaml = @"
                <ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                    <Style TargetType=""ScrollBar"">
                        <Setter Property=""Background"" Value=""Transparent""/>
                        <Setter Property=""Width"" Value=""10""/>
                        <Setter Property=""Template"">
                            <Setter.Value>
                                <ControlTemplate TargetType=""ScrollBar"">
                                    <Border Background=""Transparent"">
                                        <Track x:Name=""PART_Track"" IsDirectionReversed=""true"">
                                            <Track.Thumb>
                                                <Thumb>
                                                    <Thumb.Template>
                                                        <ControlTemplate TargetType=""Thumb"">
                                                            <Border Background=""#cbd5e1"" CornerRadius=""3"" Margin=""2,0,2,0""/>
                                                        </ControlTemplate>
                                                    </Thumb.Template>
                                                </Thumb>
                                            </Track.Thumb>
                                        </Track>
                                    </Border>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>
                </ResourceDictionary>";
            var ctx = new System.Windows.Markup.ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Parse(scrollbarXaml, ctx));
"""

content = content.replace("BuildUi();", "BuildUi();\n" + scrollbar_xaml)

# 2. Add RowDefinition for metrics and insert metric grid
metric_row_def = """            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Hero
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Metrics
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Content (Summary + Email)
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3: Lower Content (Schema + Scheduler)"""

content = content.replace(
"""            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Hero
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Content (Summary + Email)
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Lower Content (Schema + Scheduler)""", 
metric_row_def)

hero_call = "rootGrid.Children.Add(BuildHero(0));"

metrics_creation = """
            var metricGrid = new Grid { Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AddMetricPill(metricGrid, 0, "Open", _lblOpenValue, Color.FromRgb(59, 130, 246));
            AddMetricPill(metricGrid, 1, "Pending", _lblPendingValue, Color.FromRgb(245, 158, 11));
            AddMetricPill(metricGrid, 2, "Critical", _lblCriticalValue, Color.FromRgb(239, 68, 68));
            AddMetricPill(metricGrid, 3, "Today", _lblTodayValue, Color.FromRgb(16, 185, 129));
            Grid.SetRow(metricGrid, 1);
            rootGrid.Children.Add(metricGrid);
"""

content = content.replace(hero_call, hero_call + "\n" + metrics_creation)

# 3. Update Grid.SetRow for midGrid and bottomGrid
content = content.replace("Grid.SetRow(midGrid, 1);", "Grid.SetRow(midGrid, 2);")
content = content.replace("Grid.SetRow(bottomGrid, 2);", "Grid.SetRow(bottomGrid, 3);")

# 4. Remove metric grid creation from BuildSystemSummaryCard
old_metric_creation_in_summary = """            var metricGrid = new Grid { Margin = new Thickness(0, 24, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metricGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AddMetricPill(metricGrid, 0, "Open", _lblOpenValue, Color.FromRgb(59, 130, 246));
            AddMetricPill(metricGrid, 1, "Pending", _lblPendingValue, Color.FromRgb(245, 158, 11));
            AddMetricPill(metricGrid, 2, "Critical", _lblCriticalValue, Color.FromRgb(239, 68, 68));
            AddMetricPill(metricGrid, 3, "Today", _lblTodayValue, Color.FromRgb(16, 185, 129));
            stack.Children.Add(metricGrid);"""

content = content.replace(old_metric_creation_in_summary, "")

# 5. Add status dots to System Summary fields. We'll change AddRow logic slightly, or we can just add dots to the stackpanel directly in ApplySnapshotToUi, 
# but it's better to modify CreateValueText to be a StackPanel or border. Actually, simpler:
# Just modify `AddRow` to handle standard formatting. Wait, text formatting was: muted keys, bold values.
# In `CreateValueText()`:
# `FontWeight = FontWeights.SemiBold`
content = content.replace("""        private TextBlock CreateValueText()
        {
            return new TextBlock
            {
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),""", """        private TextBlock CreateValueText()
        {
            return new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),""")

# In `CreateKeyLabel()`
content = content.replace("""        private TextBlock CreateKeyLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),""", """        private TextBlock CreateKeyLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),""")

# Add visual divider in System Summary. Let's just modify AddRow.
add_row_old = """        private void AddRow(Grid grid, int row, string label, UIElement value)
        {
            var lbl = CreateKeyLabel(label);
            Grid.SetRow(lbl, row);
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            Grid.SetRow(value, row);
            Grid.SetColumn(value, 1);
            grid.Children.Add(value);
        }"""
add_row_new = """        private void AddRow(Grid grid, int row, string label, UIElement value)
        {
            var lbl = CreateKeyLabel(label);
            Grid.SetRow(lbl, row);
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            Grid.SetRow(value, row);
            Grid.SetColumn(value, 1);
            grid.Children.Add(value);

            if (row < grid.RowDefinitions.Count - 1)
            {
                var divider = new Border { BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = new SolidColorBrush(Color.FromRgb(241, 245, 249)), Margin = new Thickness(0, 0, 0, -2), VerticalAlignment = VerticalAlignment.Bottom };
                Grid.SetRow(divider, row);
                Grid.SetColumnSpan(divider, 2);
                grid.Children.Add(divider);
            }
        }"""
content = content.replace(add_row_old, add_row_new)

# Modify status indicator (Dot) for Scheduler textblocks. We will wrap the textblock in a StackPanel with an Ellipse.
# Let's add an Ellipse next to the text in ApplySnapshotToUi, but they are just TextBlocks. 
# Better: We change the type of `_lblSchedulerStatusValue` etc to `ContentControl`? No, let's keep them TextBlocks and we just use unicode circle symbols: "● RUNNING"
apply_snapshot_old = """
            if (snap.SchedulerIsRunning == true)
            {
                _lblSchedulerStatusValue.Text = "RUNNING";
                _lblSchedulerStatusValue.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
            }
            else if (snap.SchedulerIsRunning == false)
            {
                _lblSchedulerStatusValue.Text = "IDLE";
                _lblSchedulerStatusValue.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            }
            else
            {
                _lblSchedulerStatusValue.Text = "UNKNOWN";
                _lblSchedulerStatusValue.Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 140));
            }"""

apply_snapshot_new = """
            if (snap.SchedulerIsRunning == true)
            {
                _lblSchedulerStatusValue.Text = "● RUNNING";
                _lblSchedulerStatusValue.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
            }
            else if (snap.SchedulerIsRunning == false)
            {
                _lblSchedulerStatusValue.Text = "● IDLE";
                _lblSchedulerStatusValue.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            }
            else
            {
                _lblSchedulerStatusValue.Text = "● UNKNOWN";
                _lblSchedulerStatusValue.Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 140));
            }"""
content = content.replace(apply_snapshot_old, apply_snapshot_new)

# Dynamic Email TextBoxes: hide them if no error.
apply_email_section_old = """        private void ApplyEmailSection(TextBlock label, TextBox details, Yakult.Inventory.App.Models.CallMonitoring.CallEmailLogItem log, string emptyMessage)
        {
            if (log == null)
            {
                label.Text = emptyMessage;
                label.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                details.Text = string.Empty;
                return;
            }

            var dateSent = ToLocalDisplay(log.DateSent);
            var ticket = log.TicketId.HasValue && log.TicketId.Value > 0 ? $"Ticket #{log.TicketId.Value}" : "No Ticket";
            label.Text = $"{dateSent} • {log.EmailType} • {ticket}";
            
            if (string.Equals(log.Status, "Failed", StringComparison.OrdinalIgnoreCase)) label.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            else if (string.Equals(log.Status, "Skipped", StringComparison.OrdinalIgnoreCase)) label.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            else label.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));

            details.Text = FormatActionableText(log.ErrorMessage);
        }"""
apply_email_section_new = """        private void ApplyEmailSection(TextBlock label, TextBox details, Yakult.Inventory.App.Models.CallMonitoring.CallEmailLogItem log, string emptyMessage)
        {
            if (log == null)
            {
                label.Text = "No recent issues (" + emptyMessage + ")";
                label.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                details.Text = string.Empty;
                details.Visibility = Visibility.Collapsed;
                return;
            }

            var dateSent = ToLocalDisplay(log.DateSent);
            var ticket = log.TicketId.HasValue && log.TicketId.Value > 0 ? $"Ticket #{log.TicketId.Value}" : "No Ticket";
            label.Text = $"{dateSent} • {log.EmailType} • {ticket}";
            
            if (string.Equals(log.Status, "Failed", StringComparison.OrdinalIgnoreCase)) label.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            else if (string.Equals(log.Status, "Skipped", StringComparison.OrdinalIgnoreCase)) label.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            else label.Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42));

            var formatted = FormatActionableText(log.ErrorMessage);
            details.Text = formatted;
            details.Visibility = string.IsNullOrWhiteSpace(formatted) ? Visibility.Collapsed : Visibility.Visible;
        }"""
content = content.replace(apply_email_section_old, apply_email_section_new)

# Dark Terminal
terminal_old = """            _txtSchedulerOutput = new RichTextBox
            {
                Height = 220,
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            };"""
terminal_new = """            _txtSchedulerOutput = new RichTextBox
            {
                Height = 220,
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128))
            };"""
content = content.replace(terminal_old, terminal_new)

# Terminal color logic
set_scheduler_color_old = """                Color lineColor = Color.FromRgb(15, 23, 42); // default
                bool isHeader = false;

                if (upper.Contains("ERROR") || upper.Contains("FAILED") || upper.Contains("FAILURE") || upper.Contains("WARNING -") || upper.Contains("NOT FOUND") || upper.Contains("MISSING") || upper.Contains("DISABLED") && upper.Contains("WARNING"))
                    lineColor = Color.FromRgb(220, 38, 38);
                else if (upper.Contains("OK") || upper.Contains("VERIFIED") || upper.Contains("COMPLETED") || upper.Contains("INSTALLED") || upper.Contains("GOOD") || (upper.Contains("ENABLED") && !upper.Contains("DISABLED")) || upper.Contains("REGISTERED") || upper.Contains("ACCEPTED"))
                    lineColor = Color.FromRgb(5, 150, 105);
                else if (upper.Contains("===") || trimmed.StartsWith("[") || trimmed.StartsWith(":: "))
                {
                    lineColor = Color.FromRgb(37, 99, 235);
                    isHeader = true;
                }
                else if (upper.Contains("NOTE -") || upper.Contains("WAITING") || upper.Contains("TRYING"))
                    lineColor = Color.FromRgb(217, 119, 6);"""
set_scheduler_color_new = """                Color lineColor = Color.FromRgb(74, 222, 128); // default green terminal text
                bool isHeader = false;

                if (upper.Contains("ERROR") || upper.Contains("FAILED") || upper.Contains("FAILURE") || upper.Contains("WARNING -") || upper.Contains("NOT FOUND") || upper.Contains("MISSING") || upper.Contains("DISABLED") && upper.Contains("WARNING"))
                    lineColor = Color.FromRgb(248, 113, 113); // light red
                else if (upper.Contains("OK") || upper.Contains("VERIFIED") || upper.Contains("COMPLETED") || upper.Contains("INSTALLED") || upper.Contains("GOOD") || (upper.Contains("ENABLED") && !upper.Contains("DISABLED")) || upper.Contains("REGISTERED") || upper.Contains("ACCEPTED"))
                    lineColor = Color.FromRgb(52, 211, 153); // bright green
                else if (upper.Contains("===") || trimmed.StartsWith("[") || trimmed.StartsWith(":: "))
                {
                    lineColor = Color.FromRgb(96, 165, 250); // light blue
                    isHeader = true;
                }
                else if (upper.Contains("NOTE -") || upper.Contains("WAITING") || upper.Contains("TRYING"))
                    lineColor = Color.FromRgb(251, 191, 36); // amber"""
content = content.replace(set_scheduler_color_old, set_scheduler_color_new)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)
print("done")
