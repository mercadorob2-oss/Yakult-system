using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed class WpfTicketStatusActionDialog : Window
    {
        private readonly string _currentStatus;
        private readonly string _targetStatus;
        private readonly string _currentPriority;
        private readonly string _targetPriority;
        private readonly bool _priorityChanged;
        private readonly bool _noteRequired;
        private readonly TextBox _noteBox;
        private readonly Button _confirmButton;
        private readonly TextBlock _validationText;

        public string NoteText { get; private set; }

        public WpfTicketStatusActionDialog(
            CallTicketListItem ticket,
            string targetStatus,
            string targetPriority,
            bool priorityChanged,
            string initialNote)
        {
            _currentStatus = Normalize(ticket?.Status);
            _targetStatus = Normalize(targetStatus);
            _currentPriority = string.IsNullOrWhiteSpace(ticket?.Priority) ? "Medium" : ticket.Priority.Trim();
            _targetPriority = string.IsNullOrWhiteSpace(targetPriority) ? _currentPriority : targetPriority.Trim();
            _priorityChanged = priorityChanged;
            _noteRequired = IsRequiredNoteStatus(_targetStatus);

            Title = GetWindowTitle();
            Width = 640;
            Height = 560;
            MinWidth = 560;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            Background = BrushFromRgb(245, 247, 250);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var header = new Border
            {
                Background = Brushes.White,
                Padding = new Thickness(24, 18, 24, 16)
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var headerStack = new StackPanel();
            header.Child = headerStack;
            headerStack.Children.Add(new TextBlock
            {
                Text = GetHeadline(),
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42)
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = GetSubtitle(),
                Margin = new Thickness(0, 6, 0, 0),
                FontSize = 12.5,
                Foreground = BrushFromRgb(71, 85, 105),
                TextWrapping = TextWrapping.Wrap
            });

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var body = new StackPanel { Margin = new Thickness(24) };
            scroll.Content = body;

            var ticketCard = CreateCard();
            ticketCard.Children.Add(CreateSectionTitle("Ticket"));
            ticketCard.Children.Add(CreateDetail("Code", string.IsNullOrWhiteSpace(ticket?.TicketCode) ? ticket?.TicketId.ToString() ?? "-" : ticket.TicketCode.Trim()));
            ticketCard.Children.Add(CreateDetail("Issue", string.IsNullOrWhiteSpace(ticket?.Issue) ? "-" : ticket.Issue.Trim()));
            body.Children.Add(ticketCard);

            var changeCard = CreateCard();
            changeCard.Children.Add(CreateSectionTitle("Changes to apply"));
            if (HasStatusChange())
                changeCard.Children.Add(CreateStatusChangeRow());
            if (_priorityChanged)
                changeCard.Children.Add(CreateDetail("Priority", _currentPriority + " -> " + _targetPriority));
            if (!HasStatusChange() && !_priorityChanged)
                changeCard.Children.Add(CreateDetail("Update", "Add note only"));
            changeCard.Children.Add(CreateConsequenceBlock());
            body.Children.Add(changeCard);

            var noteCard = CreateCard();
            noteCard.Children.Add(CreateSectionTitle(_noteRequired ? "Action note required" : "Action note"));
            _noteBox = CreateTextBox();
            _noteBox.MinHeight = 140;
            _noteBox.AcceptsReturn = true;
            _noteBox.TextWrapping = TextWrapping.Wrap;
            _noteBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _noteBox.Text = initialNote ?? string.Empty;
            _noteBox.TextChanged += (_, __) => RefreshValidation();
            noteCard.Children.Add(_noteBox);
            body.Children.Add(noteCard);

            var footer = new DockPanel
            {
                Height = 68,
                Background = Brushes.White,
                LastChildFill = false
            };
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            _validationText = new TextBlock
            {
                Margin = new Thickness(24, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(185, 28, 28),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            DockPanel.SetDock(_validationText, Dock.Left);
            footer.Children.Add(_validationText);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 12, 20, 12)
            };
            DockPanel.SetDock(buttons, Dock.Right);
            footer.Children.Add(buttons);

            var cancel = CreateButton("Cancel", BrushFromRgb(100, 116, 139));
            cancel.IsCancel = true;
            cancel.Click += (_, __) => DialogResult = false;
            _confirmButton = CreateButton(GetConfirmText(), GetActionBrush());
            _confirmButton.IsDefault = true;
            _confirmButton.Click += (_, __) => Confirm();
            buttons.Children.Add(cancel);
            buttons.Children.Add(_confirmButton);

            Loaded += (_, __) =>
            {
                _noteBox.Focus();
                _noteBox.CaretIndex = _noteBox.Text.Length;
                RefreshValidation();
            };
        }

        private void Confirm()
        {
            if (!IsValid(out var message))
            {
                ShowValidation(message);
                return;
            }

            NoteText = (_noteBox.Text ?? string.Empty).Trim();
            DialogResult = true;
            Close();
        }

        private void RefreshValidation()
        {
            if (_confirmButton == null)
                return;

            if (IsValid(out var message))
            {
                _validationText.Text = string.Empty;
                _validationText.Visibility = Visibility.Collapsed;
                _confirmButton.IsEnabled = true;
            }
            else
            {
                ShowValidation(message);
                _confirmButton.IsEnabled = false;
            }
        }

        private bool IsValid(out string message)
        {
            if (_noteRequired && string.IsNullOrWhiteSpace(_noteBox.Text))
            {
                message = "Enter an action note before confirming.";
                return false;
            }

            message = null;
            return true;
        }

        private void ShowValidation(string message)
        {
            _validationText.Text = message;
            _validationText.Visibility = Visibility.Visible;
        }

        private bool HasStatusChange()
        {
            return !string.IsNullOrWhiteSpace(_targetStatus)
                   && !_targetStatus.Equals("-")
                   && !_targetStatus.Equals(_currentStatus, StringComparison.OrdinalIgnoreCase);
        }

        private string GetWindowTitle()
        {
            if (_targetStatus.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return "Escalate Ticket";
            if (_targetStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase)) return "Close Ticket";
            if (_targetStatus.Equals("Reopened", StringComparison.OrdinalIgnoreCase)) return "Reopen Ticket";
            return "Confirm Ticket Update";
        }

        private string GetHeadline()
        {
            if (_targetStatus.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return "Escalate ticket";
            if (_targetStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase)) return "Close ticket";
            if (_targetStatus.Equals("Reopened", StringComparison.OrdinalIgnoreCase)) return "Reopen ticket";
            return "Confirm ticket update";
        }

        private string GetSubtitle()
        {
            if (_targetStatus.Equals("Escalated", StringComparison.OrdinalIgnoreCase))
                return "Escalation keeps the ticket active and records why higher support attention is needed.";
            if (_targetStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase))
                return "Closing is final for the current workflow. Use this for cancelled or administratively closed tickets.";
            if (_targetStatus.Equals("Reopened", StringComparison.OrdinalIgnoreCase))
                return "Reopening moves the ticket back into active work and clears the solved timestamp.";
            return "Review the changes before saving them to the ticket history.";
        }

        private string GetConfirmText()
        {
            if (_targetStatus.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return "Escalate";
            if (_targetStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase)) return "Close Ticket";
            if (_targetStatus.Equals("Reopened", StringComparison.OrdinalIgnoreCase)) return "Reopen";
            return "Apply Update";
        }

        private string GetConsequenceText()
        {
            if (_targetStatus.Equals("Escalated", StringComparison.OrdinalIgnoreCase))
                return "The ticket stays in the pending queue with escalation visibility.";
            if (_targetStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase))
                return "The ticket moves to the resolved/closed list and requires a reopen action for more work.";
            if (_targetStatus.Equals("Reopened", StringComparison.OrdinalIgnoreCase))
                return "The ticket returns to active work and can be updated again.";
            if (HasStatusChange())
                return "The status change will be written to ticket history.";
            return "The update will be written to ticket notes/history.";
        }

        private Brush GetActionBrush()
        {
            if (_targetStatus.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(234, 88, 12);
            if (_targetStatus.Equals("Closed", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(220, 38, 38);
            if (_targetStatus.Equals("Reopened", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(37, 99, 235);
            return BrushFromRgb(37, 99, 235);
        }

        private FrameworkElement CreateStatusChangeRow()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            row.Children.Add(CreatePill(_currentStatus, BrushFromRgb(100, 116, 139)));
            row.Children.Add(new TextBlock
            {
                Text = "->",
                Margin = new Thickness(10, 4, 10, 0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105)
            });
            row.Children.Add(CreatePill(_targetStatus, GetActionBrush()));
            return row;
        }

        private FrameworkElement CreateConsequenceBlock()
        {
            return new Border
            {
                Margin = new Thickness(0, 14, 0, 0),
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(6),
                Background = BrushFromRgb(248, 250, 252),
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = GetConsequenceText(),
                    FontSize = 12.5,
                    Foreground = BrushFromRgb(71, 85, 105),
                    TextWrapping = TextWrapping.Wrap
                }
            };
        }

        private static Border CreatePill(string text, Brush background)
        {
            return new Border
            {
                Background = background,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4),
                Child = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(text) ? "-" : text,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                }
            };
        }

        private static FrameworkElement CreateDetail(string label, string value)
        {
            var stack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(100, 116, 139)
            });
            stack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(value) ? "-" : value,
                Margin = new Thickness(0, 3, 0, 0),
                FontSize = 13,
                Foreground = BrushFromRgb(15, 23, 42),
                TextWrapping = TextWrapping.Wrap
            });
            return stack;
        }

        private static StackPanel CreateCard()
        {
            return new StackPanel
            {
                Background = Brushes.White,
                Margin = new Thickness(0, 0, 0, 14),
                MinHeight = 56
            };
        }

        private static TextBlock CreateSectionTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42),
                Margin = new Thickness(0, 0, 0, 4)
            };
        }

        private static TextBox CreateTextBox()
        {
            return new TextBox
            {
                MinHeight = 32,
                Padding = new Thickness(8, 6, 8, 6),
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Foreground = BrushFromRgb(30, 41, 59)
            };
        }

        private static Button CreateButton(string text, Brush background)
        {
            return new Button
            {
                Content = text,
                MinWidth = 110,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(14, 8, 14, 8),
                Background = background,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
        }

        private static bool IsRequiredNoteStatus(string status)
        {
            var s = Normalize(status);
            return s.Equals("Escalated", StringComparison.OrdinalIgnoreCase)
                   || s.Equals("Closed", StringComparison.OrdinalIgnoreCase)
                   || s.Equals("Reopened", StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
