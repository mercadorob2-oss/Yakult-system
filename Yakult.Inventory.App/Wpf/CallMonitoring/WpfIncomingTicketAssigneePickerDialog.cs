using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    /// <summary>
    /// Searchable staff-card picker used by Incoming Tickets for both single and bulk assignment.
    /// It deliberately receives the already-filtered eligible candidates so assignment rules stay
    /// centralized in the repository rather than duplicated in the UI.
    /// </summary>
    public sealed class WpfIncomingTicketAssigneePickerDialog : Window
    {
        private readonly List<LookupItem> _allCandidates;
        private readonly IReadOnlyDictionary<int, int> _openTicketCounts;
        private readonly int _ticketCount;
        private readonly TextBox _searchBox;
        private readonly WrapPanel _cardsPanel;
        private readonly TextBlock _resultText;
        private readonly TextBlock _selectionText;
        private readonly Button _assignButton;
        private LookupItem _selected;

        public LookupItem SelectedAssignee => _selected;

        public WpfIncomingTicketAssigneePickerDialog(
            IEnumerable<LookupItem> candidates,
            IReadOnlyDictionary<int, int> openTicketCounts,
            int ticketCount)
        {
            _allCandidates = (candidates ?? Enumerable.Empty<LookupItem>())
                .Where(x => x != null && x.Id > 0)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .OrderBy(x => x.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _openTicketCounts = openTicketCounts ?? new Dictionary<int, int>();
            _ticketCount = Math.Max(1, ticketCount);

            Title = _ticketCount == 1 ? "Assign portal request" : "Assign portal requests";
            Width = 820;
            Height = 650;
            MinWidth = 660;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            Background = Brush(241, 245, 249);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var header = new Border
            {
                Background = new LinearGradientBrush(Brush(13, 148, 136).Color, Brush(15, 118, 110).Color, new Point(0, 0), new Point(1, 1)),
                Padding = new Thickness(26, 22, 26, 20)
            };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = "ASSIGN OWNER",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255))
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = _ticketCount == 1 ? "Choose the right IT owner" : "Choose one IT owner for the selected requests",
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 23,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Workload is shown to help balance portal intake. Only eligible IT staff are listed.",
                Margin = new Thickness(0, 7, 0, 0),
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                TextWrapping = TextWrapping.Wrap
            });
            header.Child = headerStack;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var tools = new Grid { Margin = new Thickness(22, 18, 22, 12) };
            tools.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tools.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var searchFrame = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brush(203, 213, 225),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 0, 12, 0),
                Height = 40
            };
            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.Children.Add(new TextBlock
            {
                Text = "⌕",
                FontSize = 18,
                Foreground = Brush(100, 116, 139),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 1)
            });
            _searchBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Foreground = Brush(15, 23, 42),
                ToolTip = "Search IT staff by name, username, or role"
            };
            _searchBox.TextChanged += (_, __) => RenderCards();
            Grid.SetColumn(_searchBox, 1);
            searchGrid.Children.Add(_searchBox);
            searchFrame.Child = searchGrid;
            tools.Children.Add(searchFrame);

            _resultText = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(71, 85, 105),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };
            Grid.SetColumn(_resultText, 1);
            tools.Children.Add(_resultText);
            Grid.SetRow(tools, 1);
            root.Children.Add(tools);

            var cardsScroll = new ScrollViewer
            {
                Margin = new Thickness(22, 0, 22, 16),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            cardsScroll.Resources.MergedDictionaries.Add(WpfThemeResources.GetScrollBarStyle());
            _cardsPanel = new WrapPanel { ItemWidth = 368, Margin = new Thickness(0, 0, 0, 4) };
            cardsScroll.Content = _cardsPanel;
            Grid.SetRow(cardsScroll, 2);
            root.Children.Add(cardsScroll);

            var footer = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brush(226, 232, 240),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(22, 14, 22, 14)
            };
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _selectionText = new TextBlock
            {
                Text = "Select an IT staff member to continue.",
                FontSize = 12.5,
                Foreground = Brush(100, 116, 139),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            footerGrid.Children.Add(_selectionText);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = CreateButton("Cancel", Brush(100, 116, 139));
            cancel.IsCancel = true;
            cancel.Click += (_, __) => DialogResult = false;
            _assignButton = CreateButton(_ticketCount == 1 ? "Assign owner" : "Assign selected", Brush(13, 148, 136));
            _assignButton.IsEnabled = false;
            _assignButton.IsDefault = true;
            _assignButton.Click += (_, __) => DialogResult = true;
            buttons.Children.Add(cancel);
            buttons.Children.Add(_assignButton);
            Grid.SetColumn(buttons, 1);
            footerGrid.Children.Add(buttons);
            footer.Child = footerGrid;
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            Loaded += (_, __) =>
            {
                RenderCards();
                _searchBox.Focus();
            };
        }

        private void RenderCards()
        {
            var query = (_searchBox?.Text ?? string.Empty).Trim();
            var filtered = _allCandidates.Where(candidate => Matches(candidate, query)).ToList();
            _cardsPanel.Children.Clear();

            foreach (var candidate in filtered)
                _cardsPanel.Children.Add(BuildStaffCard(candidate));

            _resultText.Text = filtered.Count == 1 ? "1 eligible staff member" : filtered.Count + " eligible staff members";
            if (filtered.Count == 0)
            {
                _cardsPanel.Children.Add(new Border
                {
                    Width = 730,
                    Margin = new Thickness(0, 8, 0, 0),
                    Padding = new Thickness(24),
                    Background = Brushes.White,
                    BorderBrush = Brush(226, 232, 240),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Child = new TextBlock
                    {
                        Text = "No eligible IT staff match this search. Try a name, username, or role.",
                        FontSize = 13,
                        Foreground = Brush(71, 85, 105),
                        TextWrapping = TextWrapping.Wrap
                    }
                });
            }
        }

        private Border BuildStaffCard(LookupItem candidate)
        {
            var selected = _selected != null && _selected.Id == candidate.Id;
            var workload = _openTicketCounts.TryGetValue(candidate.Id, out var count) ? Math.Max(0, count) : 0;
            var accent = WorkloadAccent(workload);
            var card = new Border
            {
                Width = 356,
                Margin = new Thickness(0, 0, 12, 12),
                Padding = new Thickness(16),
                Background = selected ? Brush(240, 253, 250) : Brushes.White,
                BorderBrush = selected ? Brush(13, 148, 136) : Brush(226, 232, 240),
                BorderThickness = new Thickness(selected ? 2 : 1),
                CornerRadius = new CornerRadius(14),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 1, Opacity = 0.06, Color = Color.FromRgb(15, 23, 42) }
            };
            card.MouseLeftButtonUp += (_, __) => SelectCandidate(candidate);
            card.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Space)
                {
                    SelectCandidate(candidate);
                    e.Handled = true;
                }
            };
            card.Focusable = true;
            card.ToolTip = "Select " + SafeName(candidate);

            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatar = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(22),
                Background = Brush(13, 148, 136),
                Child = new TextBlock
                {
                    Text = Initials(candidate.Name),
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            root.Children.Add(avatar);

            var details = new StackPanel { Margin = new Thickness(12, 0, 8, 0) };
            details.Children.Add(new TextBlock
            {
                Text = SafeName(candidate),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(15, 23, 42),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            details.Children.Add(new TextBlock
            {
                Text = RoleLabel(candidate),
                Margin = new Thickness(0, 3, 0, 0),
                FontSize = 11.5,
                Foreground = Brush(71, 85, 105),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            details.Children.Add(new TextBlock
            {
                Text = UserLabel(candidate),
                Margin = new Thickness(0, 3, 0, 0),
                FontSize = 11,
                Foreground = Brush(100, 116, 139),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Grid.SetColumn(details, 1);
            root.Children.Add(details);

            var workloadBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, accent.Color.R, accent.Color.G, accent.Color.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(95, accent.Color.R, accent.Color.G, accent.Color.B)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5),
                CornerRadius = new CornerRadius(9),
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = WorkloadLabel(workload),
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = accent
                }
            };
            Grid.SetColumn(workloadBadge, 2);
            root.Children.Add(workloadBadge);

            card.Child = root;
            return card;
        }

        private void SelectCandidate(LookupItem candidate)
        {
            _selected = candidate;
            _selectionText.Text = SafeName(candidate) + " will receive " + (_ticketCount == 1 ? "this portal request." : _ticketCount + " selected portal requests.");
            _selectionText.Foreground = Brush(15, 118, 110);
            _assignButton.IsEnabled = true;
            RenderCards();
        }

        private static bool Matches(LookupItem candidate, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            return (candidate.Name ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (candidate.DisplayName ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string SafeName(LookupItem candidate) => string.IsNullOrWhiteSpace(candidate?.Name) ? "IT staff member" : candidate.Name.Trim();

        private static string UserLabel(LookupItem candidate)
        {
            var display = candidate?.DisplayName ?? string.Empty;
            var start = display.IndexOf('(');
            var end = start >= 0 ? display.IndexOf(')', start + 1) : -1;
            return start >= 0 && end > start + 1 ? "@" + display.Substring(start + 1, end - start - 1).Trim() : "Eligible IT account";
        }

        private static string RoleLabel(LookupItem candidate)
        {
            var display = candidate?.DisplayName ?? string.Empty;
            var marker = display.LastIndexOf(" - ", StringComparison.Ordinal);
            if (marker >= 0 && marker + 3 < display.Length)
                return display.Substring(marker + 3).Trim();
            return "Eligible IT staff";
        }

        private static string Initials(string name)
        {
            var pieces = (name ?? string.Empty).Split(new[] { ' ', '.', '-', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length == 0) return "IT";
            if (pieces.Length == 1) return pieces[0].Substring(0, Math.Min(2, pieces[0].Length)).ToUpperInvariant();
            return (pieces[0][0].ToString() + pieces[pieces.Length - 1][0]).ToUpperInvariant();
        }

        private static string WorkloadLabel(int count)
        {
            if (count <= 0) return "Available";
            return count == 1 ? "1 open" : count + " open";
        }

        private static SolidColorBrush WorkloadAccent(int count)
        {
            if (count <= 1) return Brush(22, 163, 74);
            if (count <= 4) return Brush(217, 119, 6);
            return Brush(220, 38, 38);
        }

        private static Button CreateButton(string text, Brush background)
        {
            return new Button
            {
                Content = text,
                Height = 36,
                MinWidth = 112,
                Padding = new Thickness(14, 0, 14, 0),
                Margin = new Thickness(8, 0, 0, 0),
                Background = background,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
        }

        private static SolidColorBrush Brush(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}
