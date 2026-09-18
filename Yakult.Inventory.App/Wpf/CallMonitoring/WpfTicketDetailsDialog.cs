using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WinForms = System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Forms.CallMonitoring;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed class WpfTicketDetailsDialog : Window
    {
        private readonly ICallMonitoringRepository _repo;
        private readonly int _ticketId;
        private CallTicketListItem _loadedTicket;

        private readonly TextBlock _titleText;
        private readonly TextBlock _subtitleText;
        private readonly Border _statusBadge;
        private readonly TextBlock _statusText;
        private readonly TextBlock _callerText;
        private readonly TextBlock _departmentText;
        private readonly TextBlock _assigneeText;
        private readonly TextBlock _priorityText;
        private readonly TextBlock _lastContactText;
        private readonly TextBlock _createdText;
        private readonly TextBox _issueBox;
        private readonly DataGrid _notesGrid;
        private readonly DataGrid _historyGrid;
        private readonly TextBlock _statusLine;
        private readonly Button _deleteButton;
        private readonly Button _copyIdButton;
        private readonly Button _copySummaryButton;
        private readonly Button _emailLogButton;
        private readonly Button _reassignButton;
        private readonly Button _markAsButton;
        private readonly Button _reopenButton;

        public TicketDetailsDialog.TicketDetailAction RequestedAction { get; private set; } = TicketDetailsDialog.TicketDetailAction.None;
        public bool IsChanged { get; private set; }

        public WpfTicketDetailsDialog(ICallMonitoringRepository repo, int ticketId)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _ticketId = ticketId > 0 ? ticketId : throw new ArgumentOutOfRangeException(nameof(ticketId));

            Title = "Ticket Details";
            Width = 1040;
            Height = 760;
            MinWidth = 860;
            MinHeight = 560;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = BrushFromRgb(245, 247, 250);

            var root = new DockPanel();
            Content = root;

            var header = new DockPanel
            {
                Background = Brushes.White,
                LastChildFill = true,
                Margin = new Thickness(0),
                Height = 86
            };
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var headerButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 18, 0)
            };
            DockPanel.SetDock(headerButtons, Dock.Right);
            header.Children.Add(headerButtons);

            _copyIdButton = CreateButton("Copy ID", BrushFromRgb(71, 85, 105));
            _copyIdButton.Click += (_, __) => CopyTicketId();
            _copySummaryButton = CreateButton("Copy Summary", BrushFromRgb(71, 85, 105));
            _copySummaryButton.Click += (_, __) => CopyTicketSummary();
            _emailLogButton = CreateButton("Email Log", BrushFromRgb(14, 116, 144));
            _emailLogButton.Click += (_, __) => OpenEmailLogShortcut();
            _deleteButton = CreateButton("Delete", BrushFromRgb(220, 38, 38));
            _deleteButton.Click += async (_, __) => await DeleteTicketAsync();
            var closeButton = CreateButton("Close", BrushFromRgb(52, 73, 94));
            closeButton.Click += (_, __) => Close();

            headerButtons.Children.Add(_copyIdButton);
            headerButtons.Children.Add(_copySummaryButton);
            headerButtons.Children.Add(_emailLogButton);
            headerButtons.Children.Add(_deleteButton);
            headerButtons.Children.Add(closeButton);

            var titleHost = new StackPanel
            {
                Margin = new Thickness(24, 14, 10, 12),
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(titleHost);

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            _titleText = new TextBlock
            {
                Text = "Ticket Details",
                FontSize = 21,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42),
                VerticalAlignment = VerticalAlignment.Center
            };
            _statusText = new TextBlock
            {
                Text = "LOADING",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Margin = new Thickness(12, 0, 12, 0)
            };
            _statusBadge = new Border
            {
                Background = BrushFromRgb(100, 116, 139),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(0, 5, 0, 5),
                Margin = new Thickness(14, 1, 0, 0),
                Child = _statusText
            };
            titleRow.Children.Add(_titleText);
            titleRow.Children.Add(_statusBadge);
            titleHost.Children.Add(titleRow);

            _subtitleText = new TextBlock
            {
                Text = "Ticket ID: " + _ticketId,
                Margin = new Thickness(1, 5, 0, 0),
                FontSize = 12.5,
                Foreground = BrushFromRgb(100, 116, 139)
            };
            titleHost.Children.Add(_subtitleText);

            var footer = new DockPanel
            {
                Height = 58,
                Background = Brushes.White,
                LastChildFill = false,
                Margin = new Thickness(0)
            };
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 10, 16, 10)
            };
            DockPanel.SetDock(actions, Dock.Right);
            footer.Children.Add(actions);

            _reopenButton = CreateButton("Reopen", BrushFromRgb(22, 163, 74));
            _reopenButton.Click += (_, __) => RequestAction(TicketDetailsDialog.TicketDetailAction.Reopen);
            _reassignButton = CreateButton("Reassign", BrushFromRgb(124, 58, 237));
            _reassignButton.Click += (_, __) => RequestAction(TicketDetailsDialog.TicketDetailAction.Reassign);
            _markAsButton = CreateButton("Mark As", BrushFromRgb(52, 73, 94));
            _markAsButton.Click += (_, __) => RequestAction(TicketDetailsDialog.TicketDetailAction.MarkAs);
            var fieldWorkButton = CreateButton("Field Work", BrushFromRgb(14, 116, 144));
            fieldWorkButton.Click += async (_, __) => await OpenFieldWorkAsync();

            actions.Children.Add(_reopenButton);
            actions.Children.Add(_reassignButton);
            actions.Children.Add(_markAsButton);
            actions.Children.Add(fieldWorkButton);

            _statusLine = new TextBlock
            {
                Text = "Loading details...",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 0, 0, 0),
                Foreground = BrushFromRgb(100, 116, 139)
            };
            footer.Children.Add(_statusLine);

            var body = new Grid { Margin = new Thickness(20) };
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(180) });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(body);

            var infoGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition());
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition());
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetRow(infoGrid, 0);
            body.Children.Add(infoGrid);

            var callerCard = CreateInfoCard();
            _callerText = AddInfoLine(callerCard, "Caller", "-");
            _departmentText = AddInfoLine(callerCard, "Department", "-");
            infoGrid.Children.Add(callerCard);

            var assignmentCard = CreateInfoCard();
            _assigneeText = AddInfoLine(assignmentCard, "Assigned To", "-");
            _priorityText = AddInfoLine(assignmentCard, "Priority", "-");
            Grid.SetColumn(assignmentCard, 1);
            infoGrid.Children.Add(assignmentCard);

            var timingCard = CreateInfoCard();
            _lastContactText = AddInfoLine(timingCard, "Last Update", "-");
            _createdText = AddInfoLine(timingCard, "Created", "-");
            Grid.SetColumn(timingCard, 2);
            infoGrid.Children.Add(timingCard);

            var issuePanel = CreateSection("Issue Description");
            _issueBox = new TextBox
            {
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderThickness = new Thickness(0),
                Background = BrushFromRgb(250, 252, 254),
                Foreground = BrushFromRgb(30, 41, 59),
                FontSize = 13,
                Padding = new Thickness(10)
            };
            issuePanel.Children.Add(_issueBox);
            Grid.SetRow(issuePanel, 1);
            body.Children.Add(issuePanel);

            var tabs = new TabControl { Margin = new Thickness(0, 16, 0, 0) };
            _notesGrid = CreateGrid();
            AddTextColumn(_notesGrid, "Created", "CreatedAt", 150);
            AddTextColumn(_notesGrid, "Type", "NoteType", 110);
            AddTextColumn(_notesGrid, "By", "CreatedByName", 150);
            AddTextColumn(_notesGrid, "Note", "NoteText", 460);

            _historyGrid = CreateGrid();
            AddTextColumn(_historyGrid, "When", "ChangedAt", 150);
            AddTextColumn(_historyGrid, "Field", "FieldName", 130);
            AddTextColumn(_historyGrid, "Old", "OldValue", 150);
            AddTextColumn(_historyGrid, "New", "NewValue", 150);
            AddTextColumn(_historyGrid, "By", "ChangedByName", 130);
            AddTextColumn(_historyGrid, "Note", "Note", 260);

            tabs.Items.Add(new TabItem { Header = "Notes", Content = _notesGrid });
            tabs.Items.Add(new TabItem { Header = "History", Content = _historyGrid });
            Grid.SetRow(tabs, 2);
            body.Children.Add(tabs);

            Loaded += async (_, __) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            _statusLine.Text = "Loading details...";

            try
            {
                var ticket = await _repo.GetTicketByIdAsync(_ticketId);
                if (ticket == null)
                {
                    _titleText.Text = "Ticket Not Found";
                    _statusLine.Text = "Ticket ID invalid or removed.";
                    SetActionButtons(false, false);
                    return;
                }

                _loadedTicket = ticket;
                _titleText.Text = ticket.TicketCode ?? "Ticket #" + ticket.TicketId;
                _subtitleText.Text = "ID: " + ticket.TicketId + " | Branch: " + (ticket.Branch ?? "Head Office");
                _statusText.Text = (ticket.Status ?? "Unknown").ToUpperInvariant();
                _statusBadge.Background = GetStatusBrush(ticket.Status);

                _callerText.Text = ticket.CallerName ?? "-";
                _departmentText.Text = ticket.Department ?? "-";
                _assigneeText.Text = string.IsNullOrWhiteSpace(ticket.ResponsiblePerson) ? "(Unassigned)" : ticket.ResponsiblePerson.Trim();
                _priorityText.Text = (ticket.Priority ?? "-").ToUpperInvariant();
                _priorityText.Foreground = GetPriorityBrush(ticket.Priority);
                _lastContactText.Text = ticket.LastContactAt.HasValue ? GetTimeAgo(ticket.LastContactAt.Value) : "Never";
                _createdText.Text = ToLocalString(ticket.CreatedAt);
                _issueBox.Text = ticket.Issue ?? string.Empty;

                var isFinal = IsFinalStatus(ticket.Status);
                SetActionButtons(true, isFinal);

                var notes = await _repo.GetTicketNotesAsync(ticket.TicketId, 200) ?? new List<CallTicketNoteItem>();
                _notesGrid.ItemsSource = notes.Select(x => new
                {
                    CreatedAt = x.CreatedAt.HasValue ? ToLocalString(x.CreatedAt.Value) : "-",
                    x.NoteType,
                    x.CreatedByName,
                    x.NoteText
                }).ToList();

                var history = await _repo.GetTicketHistoryAsync(ticket.TicketId, 200) ?? new List<CallTicketHistoryItem>();
                _historyGrid.ItemsSource = history.Select(x => new
                {
                    ChangedAt = x.ChangedAt.HasValue ? ToLocalString(x.ChangedAt.Value) : "-",
                    x.FieldName,
                    x.OldValue,
                    x.NewValue,
                    x.ChangedByName,
                    x.Note
                }).ToList();

                _statusLine.Text = "Ready. " + notes.Count + " notes loaded.";
            }
            catch (Exception ex)
            {
                _statusLine.Text = "Error loading ticket details.";
                MessageBox.Show(this, ex.Message, "Ticket Details", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetActionButtons(bool hasTicket, bool isFinal)
        {
            _copyIdButton.IsEnabled = hasTicket;
            _copySummaryButton.IsEnabled = hasTicket;
            _emailLogButton.IsEnabled = hasTicket;
            _deleteButton.IsEnabled = hasTicket && AppSession.IsLoggedIn && (AppSession.IsAdmin || AppSession.IsDeveloper);
            _reassignButton.IsEnabled = hasTicket && !isFinal;
            _markAsButton.IsEnabled = hasTicket && !isFinal;
            _reopenButton.Visibility = hasTicket && isFinal ? Visibility.Visible : Visibility.Collapsed;
            _reopenButton.IsEnabled = hasTicket && isFinal;
        }

        private void RequestAction(TicketDetailsDialog.TicketDetailAction action)
        {
            if (_loadedTicket == null || _loadedTicket.TicketId <= 0)
                return;

            RequestedAction = action;
            DialogResult = true;
            Close();
        }

        private void OpenEmailLogShortcut()
        {
            var ticket = _loadedTicket;
            if (ticket == null || ticket.TicketId <= 0)
                return;

            var dlg = new WpfEmailLogShortcutDialog(_repo, ticket.TicketId, ticket.TicketCode)
            {
                Owner = this
            };
            dlg.ShowDialog();
        }

        private void CopyTicketId()
        {
            var ticket = _loadedTicket;
            if (ticket == null)
                return;

            Clipboard.SetText(string.IsNullOrWhiteSpace(ticket.TicketCode) ? ticket.TicketId.ToString() : ticket.TicketCode.Trim());
            _statusLine.Text = "Copied ticket ID/code to clipboard.";
        }

        private void CopyTicketSummary()
        {
            var ticket = _loadedTicket;
            if (ticket == null)
                return;

            Clipboard.SetText(BuildTicketSummaryText(ticket));
            _statusLine.Text = "Copied ticket summary to clipboard.";
        }

        private string BuildTicketSummaryText(CallTicketListItem ticket)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ticket: " + (ticket.TicketCode ?? ticket.TicketId.ToString()));
            sb.AppendLine("ID: " + ticket.TicketId);
            sb.AppendLine("Status: " + (ticket.Status ?? "-"));
            sb.AppendLine("Priority: " + (ticket.Priority ?? "-"));
            sb.AppendLine("Caller: " + (ticket.CallerName ?? "-"));
            sb.AppendLine("Department: " + (ticket.Department ?? "-"));
            sb.AppendLine("Branch: " + (ticket.Branch ?? "-"));
            sb.AppendLine("Assigned To: " + (ticket.ResponsiblePerson ?? "(Unassigned)"));
            sb.AppendLine("Created: " + ToLocalString(ticket.CreatedAt));
            if (ticket.LastContactAt.HasValue)
                sb.AppendLine("Last Contact: " + ToLocalString(ticket.LastContactAt.Value));
            sb.AppendLine();
            sb.AppendLine("Issue:");
            sb.AppendLine(ticket.Issue ?? string.Empty);
            return sb.ToString();
        }

        private async Task OpenFieldWorkAsync()
        {
            if (_loadedTicket == null) return;
            try
            {
                var dlg = new WpfCallFieldWorkDialog(_repo, _loadedTicket.TicketId, _loadedTicket.TicketCode) { Owner = this };
                dlg.ShowDialog();
                // Refresh history/notes after field work (it logs FieldVisitStatus to CallTicketHistory)
                try
                {
                    var history = await _repo.GetTicketHistoryAsync(_loadedTicket.TicketId, 200) ?? new List<CallTicketHistoryItem>();
                    _historyGrid.ItemsSource = history.Select(x => new
                    {
                        ChangedAt = x.ChangedAt.HasValue ? ToLocalString(x.ChangedAt.Value) : "-",
                        x.FieldName,
                        x.OldValue,
                        x.NewValue,
                        x.ChangedByName,
                        x.Note
                    }).ToList();
                }
                catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Field Work", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteTicketAsync()
        {
            if (!AppSession.IsLoggedIn || (!AppSession.IsAdmin && !AppSession.IsDeveloper))
            {
                MessageBox.Show(this, "You don't have permission to delete tickets.", "Delete Ticket", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                this,
                "This will permanently delete this ticket and its related records.\n\nThis cannot be undone.\n\nDelete this ticket?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
                return;

            var userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
            if (userId == null)
            {
                MessageBox.Show(this, "No current user ID found.", "Delete Ticket", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _deleteButton.IsEnabled = false;
                Cursor = Cursors.Wait;
                var deleted = await _repo.DeleteTicketAsync(_ticketId, userId);
                if (!deleted)
                {
                    MessageBox.Show(this, "Ticket not found or already deleted.", "Delete Ticket", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                IsChanged = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Delete Ticket Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Cursor = Cursors.Arrow;
                _deleteButton.IsEnabled = AppSession.IsLoggedIn && (AppSession.IsAdmin || AppSession.IsDeveloper);
            }
        }

        private static StackPanel CreateInfoCard()
        {
            return new StackPanel
            {
                Background = Brushes.White,
                Margin = new Thickness(0, 0, 12, 0),
                MinHeight = 96
            };
        }

        private static TextBlock AddInfoLine(Panel panel, string label, string value)
        {
            panel.Children.Add(new TextBlock
            {
                Text = label.ToUpperInvariant(),
                Margin = new Thickness(12, 10, 12, 2),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(100, 116, 139)
            });
            var valueBlock = new TextBlock
            {
                Text = value,
                Margin = new Thickness(12, 0, 12, 8),
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(30, 41, 59),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            panel.Children.Add(valueBlock);
            return valueBlock;
        }

        private static DockPanel CreateSection(string title)
        {
            var panel = new DockPanel
            {
                Background = Brushes.White,
                LastChildFill = true
            };
            var header = new TextBlock
            {
                Text = title,
                Margin = new Thickness(12, 10, 12, 8),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(30, 41, 59)
            };
            DockPanel.SetDock(header, Dock.Top);
            panel.Children.Add(header);
            return panel;
        }

        private static DataGrid CreateGrid()
        {
            return new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                BorderThickness = new Thickness(0),
                RowHeaderWidth = 0,
                Background = Brushes.White,
                AlternatingRowBackground = BrushFromRgb(248, 250, 252),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };
        }

        private static void AddTextColumn(DataGrid grid, string header, string binding, double width)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(binding),
                Width = new DataGridLength(width)
            });
        }

        private static Button CreateButton(string text, Brush background)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 94,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(12, 8, 12, 8),
                Background = background,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            return button;
        }

        private static bool IsFinalStatus(string status)
        {
            var value = (status ?? string.Empty).Trim();
            return value.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                || value.Equals("Closed", StringComparison.OrdinalIgnoreCase)
                || value.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase);
        }

        private static Brush GetStatusBrush(string status)
        {
            switch ((status ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "pending": return BrushFromRgb(52, 152, 219);
                case "in progress": return BrushFromRgb(124, 58, 237);
                case "solved": return BrushFromRgb(22, 163, 74);
                case "closed": return BrushFromRgb(21, 128, 61);
                case "overdue": return BrushFromRgb(220, 38, 38);
                case "escalated": return BrushFromRgb(230, 126, 34);
                case "waiting on department": return BrushFromRgb(14, 116, 144);
                case "waiting on vendor": return BrushFromRgb(100, 116, 139);
                default: return BrushFromRgb(100, 116, 139);
            }
        }

        private static Brush GetPriorityBrush(string priority)
        {
            switch ((priority ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "critical": return BrushFromRgb(153, 27, 27);
                case "high": return BrushFromRgb(220, 38, 38);
                case "medium": return BrushFromRgb(230, 126, 34);
                case "low": return BrushFromRgb(22, 163, 74);
                default: return BrushFromRgb(100, 116, 139);
            }
        }

        private static string ToLocalString(DateTime dt)
        {
            return AppTime.ToLocalString(dt, "g");
        }

        private static string GetTimeAgo(DateTime dt)
        {
            var local = AppTime.AssumeUtc(dt).ToLocalTime();
            var diff = DateTime.Now - local;

            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return (int)diff.TotalMinutes + "m ago";
            if (diff.TotalHours < 24) return (int)diff.TotalHours + "h ago";
            if (diff.TotalDays < 7) return (int)diff.TotalDays + "d ago";
            return local.ToString("MMM dd");
        }

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
