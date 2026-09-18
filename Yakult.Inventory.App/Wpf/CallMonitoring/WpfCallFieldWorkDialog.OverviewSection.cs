using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed partial class WpfCallFieldWorkDialog
    {
        private StackPanel BuildOverviewPillRow(CallFieldVisitItem v)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,4,0,0) };
            var pill = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(12,5,12,5), HorizontalAlignment = HorizontalAlignment.Left, Background = GetFieldWorkStatusBrush(v.Status) };
            pill.Child = new TextBlock { Text = (v.Status ?? "-").ToUpperInvariant(), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 11.5 };
            row.Children.Add(pill);
            row.Children.Add(new TextBlock { Text = $"Visit #{v.FieldVisitId}", Foreground = BrushFromRgb(100,116,139), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0) });
            if (v.HasSignature)
            {
                var sig = new Border { CornerRadius = new CornerRadius(8), Background = BrushFromRgb(220,252,231), Padding = new Thickness(8,3,8,3), Margin = new Thickness(10,0,0,0), VerticalAlignment = VerticalAlignment.Center, BorderBrush = BrushFromRgb(134,239,172), BorderThickness = new Thickness(1) };
                sig.Child = new TextBlock { Text = "✓ Signature", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(22,101,52) };
                row.Children.Add(sig);
            }
            return row;
        }

        private Grid BuildOverviewGrid(CallFieldVisitItem v)
        {
            var grid = new Grid { Margin = new Thickness(0,14,0,0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Children.Add(MakeDetailCell("Technician", v.TechnicianName ?? "—", 0, 0));
            grid.Children.Add(MakeDetailCell("Scheduled", v.ScheduledAt.HasValue ? v.ScheduledAt.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt") : "—", 1, 0));
            grid.Children.Add(MakeDetailCell("Completed", v.CompletedAt.HasValue ? v.CompletedAt.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt") : "Not yet", 2, 0));
            grid.Children.Add(MakeDetailCell("Created", v.CreatedAt.ToLocalTime().ToString("MMM d, yyyy h:mm tt"), 0, 1));
            grid.Children.Add(MakeDetailCell("Ticket", $"#{v.TicketId}", 1, 1));
            var hasLoc = !string.IsNullOrWhiteSpace(v.Location);
            grid.Children.Add(MakeDetailCell("Location", hasLoc ? v.Location : "—", 2, 1, "\uE81D", hasLoc ? BrushFromRgb(37,99,235) : BrushFromRgb(148,163,184)));
            return grid;
        }

        private Border BuildNotesCard(string notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return null;
            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(14), Margin = new Thickness(0,12,0,0), BorderBrush = BrushFromRgb(226,232,240), BorderThickness = new Thickness(1) };
            var stack = new StackPanel();
            card.Child = stack;
            stack.Children.Add(new TextBlock { Text = "Notes", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(71,85,105), Margin = new Thickness(0,0,0,6) });
            stack.Children.Add(new TextBlock { Text = notes, Foreground = BrushFromRgb(30,41,59), TextWrapping = TextWrapping.Wrap, FontSize = 13 });
            return card;
        }

        private Border BuildCancelledHint()
        {
            var panel = new StackPanel { Margin = new Thickness(0,12,0,0) };
            var warnBorder = new Border { Background = BrushFromRgb(254,242,242), BorderBrush = BrushFromRgb(252,165,165), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12,10,12,10) };
            var warnRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            warnRow.Children.Add(new TextBlock { Text = "\uE7BA", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15, Foreground = BrushFromRgb(220,38,38), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) });
            warnRow.Children.Add(new TextBlock { Text = "Cancelled — pick a new technician/date above, then Reschedule to reopen (Cancelled → Scheduled).", Foreground = BrushFromRgb(127,29,29), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
            warnBorder.Child = warnRow;
            panel.Children.Add(warnBorder);
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0,8,0,0) };
            var btn = CreatePillButton("Reschedule this visit", "\uE895", BrushFromRgb(245,158,11), isPrimary: true);
            btn.Margin = new Thickness(0,0,0,0);
            btn.Click += async (_, __) => await SetStatusAsync("Scheduled");
            btnRow.Children.Add(btn);
            panel.Children.Add(btnRow);
            var wrap = new Border { Child = panel, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            return wrap;
        }

        private StackPanel BuildSignatureRow(bool hasSig)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,12,0,0), VerticalAlignment = VerticalAlignment.Center };
            var icon = new TextBlock { Text = hasSig ? "\uE73E" : "\uE70B", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, Foreground = hasSig ? BrushFromRgb(22,163,74) : BrushFromRgb(148,163,184), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) };
            row.Children.Add(icon);
            row.Children.Add(new TextBlock { Text = hasSig ? "✓ Customer signature on file — tap Signature to replace" : "No signature yet — capture before completing visit", Foreground = hasSig ? BrushFromRgb(22,163,74) : BrushFromRgb(100,116,139), FontStyle = hasSig ? FontStyles.Normal : FontStyles.Italic, FontSize = 12.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            return row;
        }

        private Border MakeDetailCell(string label, string value, int col, int row)
        {
            return MakeDetailCell(label, value, col, row, null, null);
        }

        private Border MakeDetailCell(string label, string value, int col, int row, string glyph, Brush glyphBrush)
        {
            var panel = new StackPanel { Margin = new Thickness(0,0,12,8) };
            panel.Children.Add(new TextBlock { Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(100,116,139), Margin = new Thickness(0,0,0,4) });
            var display = string.IsNullOrWhiteSpace(value) ? "—" : value;
            var isPlaceholder = string.IsNullOrWhiteSpace(value) || display == "—" || display == "Not yet";
            if (string.IsNullOrEmpty(glyph))
            {
                panel.Children.Add(new TextBlock { Text = display, Foreground = isPlaceholder ? BrushFromRgb(100,116,139) : BrushFromRgb(15,23,42), FontStyle = isPlaceholder ? FontStyles.Italic : FontStyles.Normal, FontWeight = FontWeights.SemiBold, FontSize = 13, TextWrapping = TextWrapping.Wrap });
            }
            else
            {
                var valueRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                valueRow.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = glyphBrush ?? BrushFromRgb(100,116,139), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) });
                valueRow.Children.Add(new TextBlock { Text = display, Foreground = isPlaceholder ? BrushFromRgb(100,116,139) : BrushFromRgb(15,23,42), FontStyle = isPlaceholder ? FontStyles.Italic : FontStyles.Normal, FontWeight = FontWeights.SemiBold, FontSize = 13, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
                panel.Children.Add(valueRow);
            }
            var border = new Border { Child = panel, Background = BrushFromRgb(248,250,252), CornerRadius = new CornerRadius(10), Padding = new Thickness(12,10,12,10), Margin = new Thickness(0,0,8,0), BorderBrush = BrushFromRgb(226,232,240), BorderThickness = new Thickness(1) };
            Grid.SetColumn(border, col); Grid.SetRow(border, row);
            return border;
        }
    }
}


