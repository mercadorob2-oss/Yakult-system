using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    /// <summary>
    /// Records the parent IT Call outcome after its linked Repair Ticket has been explicitly
    /// created. Repair forwarding remains a separate, permanent relationship regardless of the
    /// parent call outcome selected here.
    /// </summary>
    internal sealed class WpfRepairForwardResolutionDialog : Window
    {
        public string SelectedStatus { get; private set; }

        public WpfRepairForwardResolutionDialog(string repairTicketCode)
        {
            Title = "Resolve IT Call After Repair Forwarding";
            Width = 620;
            Height = 340;
            MinWidth = 560;
            MinHeight = 310;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = Brush(245, 247, 250);

            var root = new Grid { Margin = new Thickness(24) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var title = new TextBlock
            {
                Text = "Repair Ticket created. What is the IT Call outcome?",
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(15, 23, 42),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(title, 0);
            root.Children.Add(title);

            var repairCode = string.IsNullOrWhiteSpace(repairTicketCode) ? "the linked Repair Ticket" : repairTicketCode.Trim();
            var description = new TextBlock
            {
                Text = "The physical item is permanently linked to Repair Ticket " + repairCode + ". Choose whether the parent IT Call remains active, is solved, or is temporarily resolved. The Forwarded to Repair badge will remain visible for every option.",
                Margin = new Thickness(0, 8, 0, 16),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5,
                Foreground = Brush(71, 85, 105)
            };
            Grid.SetRow(description, 1);
            root.Children.Add(description);

            var choices = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            choices.Children.Add(CreateChoice(
                "Not solved — keep IT Call active",
                "Status: Forwarded to Repair. Use this while Repair is still assessing the item.",
                Brush(14, 116, 144),
                "Forwarded to Repair"));
            choices.Children.Add(CreateChoice(
                "Solved",
                "Status: Solved. The Repair link stays on the IT Call for audit and follow-up.",
                Brush(22, 163, 74),
                "Solved"));
            choices.Children.Add(CreateChoice(
                "Temporarily resolved",
                "Status: Resolved (Temporary). Use when the IT Call is covered for now but needs later review.",
                Brush(79, 70, 229),
                "Resolved (Temporary)"));
            Grid.SetRow(choices, 2);
            root.Children.Add(choices);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var cancel = new Button
            {
                Content = "Cancel",
                MinHeight = 34,
                Padding = new Thickness(14, 7, 14, 7),
                Background = Brush(71, 85, 105),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            cancel.Click += (_, __) => { DialogResult = false; Close(); };
            actions.Children.Add(cancel);
            Grid.SetRow(actions, 3);
            root.Children.Add(actions);
        }

        private Border CreateChoice(string heading, string description, Brush accent, string status)
        {
            var button = new Button
            {
                Background = Brushes.White,
                BorderBrush = Brush(203, 213, 225),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 8),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = heading, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = accent });
            content.Children.Add(new TextBlock { Text = description, Margin = new Thickness(0, 3, 0, 0), FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush(71, 85, 105) });
            button.Content = content;
            button.Click += (_, __) =>
            {
                SelectedStatus = status;
                DialogResult = true;
                Close();
            };

            return new Border { Child = button };
        }

        private static SolidColorBrush Brush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
