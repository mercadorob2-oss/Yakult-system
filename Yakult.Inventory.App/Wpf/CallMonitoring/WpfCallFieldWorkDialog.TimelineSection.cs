using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed partial class WpfCallFieldWorkDialog
    {
        private Border BuildTimelineSection(List<CallTicketHistoryItem> history)
        {
            var card = NewCard("Field Visit Timeline", "\uE81C", "Status changes for this field visit (Scheduled → Completed / Cancelled, Cancelled → Scheduled reschedule tracked)");
            var host = new StackPanel { Margin = new Thickness(0,8,0,0) };
            ((StackPanel)card.Child).Children.Add(host);

            var fieldHistory = history.FindAll(h => string.Equals(h.FieldName, "FieldVisitStatus", System.StringComparison.OrdinalIgnoreCase));
            fieldHistory.Sort((a,b) => a.ChangedAt.GetValueOrDefault().CompareTo(b.ChangedAt.GetValueOrDefault()));
            if (fieldHistory.Count == 0)
            {
                host.Children.Add(new Border { Background = BrushFromRgb(248,250,252), CornerRadius = new CornerRadius(10), Padding = new Thickness(14), BorderBrush = BrushFromRgb(226,232,240), BorderThickness = new Thickness(1), Child = new TextBlock { Text = "No status history yet — this visit was just scheduled.", Foreground = BrushFromRgb(100,116,139), FontStyle = FontStyles.Italic } });
                return card;
            }
            for (int i=0;i<fieldHistory.Count;i++)
            {
                var h = fieldHistory[i];
                bool isReschedule = string.Equals(h.OldValue, "Cancelled", System.StringComparison.OrdinalIgnoreCase) && string.Equals(h.NewValue, "Scheduled", System.StringComparison.OrdinalIgnoreCase);
                bool isLast = i == fieldHistory.Count-1;
                var row = new Grid { Margin = new Thickness(0,0,0, isLast?0:14) };
                row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(28)});
                row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1, GridUnitType.Star)});
                var dotCol = new StackPanel{ HorizontalAlignment = HorizontalAlignment.Center };
                Grid.SetColumn(dotCol,0); row.Children.Add(dotCol);
                var dot = new Border{ Width=12, Height=12, CornerRadius=new CornerRadius(6), Background=GetHistoryDotBrush(h.NewValue), BorderBrush=Brushes.White, BorderThickness=new Thickness(2), HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Top, Margin=new Thickness(0,4,0,0) };
                dotCol.Children.Add(dot);
                if (!isLast) dotCol.Children.Add(new Border{ Width=2, Background=BrushFromRgb(226,232,240), Margin=new Thickness(0,2,0,0), HorizontalAlignment=HorizontalAlignment.Center, Height=44 });
                var cardInner = new Border{ CornerRadius=new CornerRadius(12), Padding=new Thickness(14,12,14,12), Background=isReschedule?BrushFromRgb(254,249,195):Brushes.White, BorderBrush=isReschedule?BrushFromRgb(253,224,71):BrushFromRgb(226,232,240), BorderThickness=new Thickness(1) };
                Grid.SetColumn(cardInner,1); row.Children.Add(cardInner);
                var stack = new StackPanel(); cardInner.Child=stack;
                var header = new StackPanel{ Orientation=Orientation.Horizontal, VerticalAlignment=VerticalAlignment.Center }; stack.Children.Add(header);
                var pill = new Border{ CornerRadius=new CornerRadius(8), Background=GetFieldWorkStatusBrush(h.NewValue), Padding=new Thickness(8,3,8,3) }; pill.Child=new TextBlock{ Text=(h.NewValue??"-").ToUpperInvariant(), Foreground=Brushes.White, FontWeight=FontWeights.Bold, FontSize=11 }; header.Children.Add(pill);
                if (!string.IsNullOrWhiteSpace(h.OldValue)) header.Children.Add(new TextBlock{ Text=$"  {h.OldValue} →", Foreground=BrushFromRgb(100,116,139), FontSize=11, VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(8,0,0,0)});
                if (isReschedule) { var rs=new Border{ CornerRadius=new CornerRadius(6), Background=BrushFromRgb(250,204,21), Padding=new Thickness(6,2,6,2), Margin=new Thickness(8,0,0,0)}; rs.Child=new TextBlock{ Text="RESCHEDULE", FontSize=10, FontWeight=FontWeights.Bold, Foreground=BrushFromRgb(113,63,18)}; header.Children.Add(rs); }
                stack.Children.Add(new TextBlock{ Text=$"{h.ChangedAt:MMM d, yyyy h:mm tt} • {h.ChangedByName ?? "System"}", Foreground=BrushFromRgb(100,116,139), FontSize=11, Margin=new Thickness(0,6,0,0)});
                if (!string.IsNullOrWhiteSpace(h.Note)) stack.Children.Add(new TextBlock{ Text=h.Note, Foreground=BrushFromRgb(30,41,59), FontSize=12, TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,6,0,0)});
                host.Children.Add(row);
            }
            return card;
        }

        private Brush GetHistoryDotBrush(string status)
        {
            switch((status??"").Trim().ToLowerInvariant()){
                case "scheduled": return BrushFromRgb(37,99,235);
                case "completed": return BrushFromRgb(22,163,74);
                case "cancelled": return BrushFromRgb(220,38,38);
                default: return BrushFromRgb(148,163,184);
            }
        }
    }
}

