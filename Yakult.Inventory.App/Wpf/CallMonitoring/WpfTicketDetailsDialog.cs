using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
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
        private static readonly Brush InkBrush = BrushFromRgb(32, 38, 53);
        private static readonly Brush BodyBrush = BrushFromRgb(74, 82, 97);
        private static readonly Brush MutedBrush = BrushFromRgb(103, 112, 129);
        private static readonly Brush HairlineBrush = BrushFromRgb(229, 230, 232);
        private static readonly Brush SoftFillBrush = BrushFromRgb(245, 246, 248);

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
        private readonly ContentControl _activityContent;
        private readonly Button _notesTabButton;
        private readonly Button _historyTabButton;
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
            Width = 1120;
            Height = 740;
            MinWidth = 980;
            MinHeight = 620;
            MaxWidth = SystemParameters.WorkArea.Width - 24;
            MaxHeight = SystemParameters.WorkArea.Height - 24;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = WpfThemeResources.SurfaceMutedBrush;
            UseLayoutRounding = true;

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            // ── Header: identity, status, and document actions ─────────────────
            var header = new Border
            {
                Background = Brushes.White,
                BorderBrush = HairlineBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(22, 18, 22, 16)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Child = headerGrid;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var titleHost = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleHost.Children.Add(new TextBlock
            {
                Text = "TICKET DETAILS",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = MutedBrush
            });

            var titleRow = new Grid
            {
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            titleRow.ColumnDefinitions.Add(new ColumnDefinition());
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _titleText = new TextBlock
            {
                Text = "Ticket Details",
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = InkBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleRow.Children.Add(_titleText);

            _statusText = new TextBlock
            {
                Text = "LOADING",
                Foreground = BodyBrush,
                FontWeight = FontWeights.SemiBold,
                FontSize = 9.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            _statusBadge = new Border
            {
                Background = SoftFillBrush,
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = _statusText
            };
            Grid.SetColumn(_statusBadge, 1);
            titleRow.Children.Add(_statusBadge);
            titleHost.Children.Add(titleRow);

            _subtitleText = new TextBlock
            {
                Text = "Ticket ID: " + _ticketId,
                Margin = new Thickness(0, 5, 0, 0),
                FontSize = 11.5,
                Foreground = MutedBrush,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            titleHost.Children.Add(_subtitleText);
            headerGrid.Children.Add(titleHost);

            var headerButtons = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };
            _copyIdButton = CreateActionButton("Copy ID", "ItcmQuietButton");
            _copyIdButton.Click += (_, __) => CopyTicketId();
            _copySummaryButton = CreateActionButton("Copy Summary", "ItcmQuietButton");
            _copySummaryButton.Click += (_, __) => CopyTicketSummary();
            _emailLogButton = CreateActionButton("Email Log", "ItcmQuietButton");
            _emailLogButton.Click += (_, __) => OpenEmailLogShortcut();
            _deleteButton = CreateActionButton("Delete", "ItcmDangerButton");
            _deleteButton.Click += async (_, __) => await DeleteTicketAsync();
            var closeButton = CreateActionButton("Close", "ItcmQuietButton");
            closeButton.Click += (_, __) => Close();

            headerButtons.Children.Add(_copyIdButton);
            headerButtons.Children.Add(_copySummaryButton);
            headerButtons.Children.Add(_emailLogButton);
            headerButtons.Children.Add(_deleteButton);
            headerButtons.Children.Add(closeButton);
            Grid.SetColumn(headerButtons, 1);
            headerGrid.Children.Add(headerButtons);

            _copyIdButton.IsEnabled = false;
            _copySummaryButton.IsEnabled = false;
            _emailLogButton.IsEnabled = false;
            _deleteButton.IsEnabled = false;

            // ── Body: summary band on top, full-width activity below ───────────
            var body = new Grid { Margin = new Thickness(18) };
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            var summary = new Grid();
            summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            summary.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(summary, 0);
            body.Children.Add(summary);

            var issueCard = CreateCard();
            var issueStack = new StackPanel();
            issueStack.Children.Add(new TextBlock
            {
                Text = "ISSUE",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 0, 0, 8)
            });
            _issueBox = new TextBox
            {
                IsReadOnly = true,
                IsReadOnlyCaretVisible = false,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = BodyBrush,
                FontSize = 12.5,
                Padding = new Thickness(0),
                MinHeight = 54,
                MaxHeight = 160
            };
            issueStack.Children.Add(_issueBox);
            issueCard.Child = issueStack;
            Grid.SetColumn(issueCard, 0);
            summary.Children.Add(issueCard);

            var detailsCard = CreateCard();
            var detailsStack = new StackPanel();
            detailsStack.Children.Add(new TextBlock
            {
                Text = "CASE DETAILS",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 0, 0, 12)
            });

            var detailsGrid = new Grid();
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            detailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            detailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            detailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _callerText = AddDetailCell(detailsGrid, 0, 0, "CALLER", "-");
            _departmentText = AddDetailCell(detailsGrid, 0, 1, "DEPARTMENT", "-");
            _assigneeText = AddDetailCell(detailsGrid, 1, 0, "ASSIGNED TO", "-");
            _priorityText = AddDetailChipCell(detailsGrid, 1, 1, "PRIORITY");
            _createdText = AddDetailCell(detailsGrid, 2, 0, "CREATED", "-");
            _lastContactText = AddDetailCell(detailsGrid, 2, 1, "LAST UPDATE", "-");
            detailsStack.Children.Add(detailsGrid);
            detailsCard.Child = detailsStack;
            Grid.SetColumn(detailsCard, 2);
            summary.Children.Add(detailsCard);

            var activityCard = CreateCard(0);
            activityCard.ClipToBounds = true;
            var activityLayout = new Grid();
            activityLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            activityLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            activityCard.Child = activityLayout;
            Grid.SetRow(activityCard, 2);
            body.Children.Add(activityCard);

            var activityHeader = new Border
            {
                Background = Brushes.White,
                BorderBrush = HairlineBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 12, 16, 10)
            };
            var activityHeaderGrid = new Grid();
            activityHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition());
            activityHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var activityTitle = new StackPanel();
            activityTitle.Children.Add(new TextBlock
            {
                Text = "ACTIVITY",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = MutedBrush
            });
            activityTitle.Children.Add(new TextBlock
            {
                Text = "Notes and field changes",
                FontSize = 11,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 3, 0, 0)
            });
            activityHeaderGrid.Children.Add(activityTitle);

            var tabs = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            _notesTabButton = CreateTabButton("Notes", true);
            _historyTabButton = CreateTabButton("History", false);
            _notesTabButton.Click += (_, __) => ShowActivityTab(true);
            _historyTabButton.Click += (_, __) => ShowActivityTab(false);
            tabs.Children.Add(_notesTabButton);
            tabs.Children.Add(_historyTabButton);
            Grid.SetColumn(tabs, 1);
            activityHeaderGrid.Children.Add(tabs);
            activityHeader.Child = activityHeaderGrid;
            activityLayout.Children.Add(activityHeader);

            _notesGrid = CreateGrid();
            AddTextColumn(_notesGrid, "Created", "CreatedAt", 150);
            AddTextColumn(_notesGrid, "Type", "NoteType", 90);
            AddTextColumn(_notesGrid, "By", "CreatedByName", 120);
            AddTextColumn(_notesGrid, "Note", "NoteText", 0, wrap: true, minWidth: 320);

            _historyGrid = CreateGrid();
            AddTextColumn(_historyGrid, "When", "ChangedAt", 150);
            AddTextColumn(_historyGrid, "Field", "FieldName", 100);
            AddTextColumn(_historyGrid, "Old", "OldValue", 115);
            AddTextColumn(_historyGrid, "New", "NewValue", 125);
            AddTextColumn(_historyGrid, "By", "ChangedByName", 100);
            AddTextColumn(_historyGrid, "Note", "Note", 0, wrap: true, minWidth: 280);

            _activityContent = new ContentControl
            {
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Background = Brushes.White
            };
            Grid.SetRow(_activityContent, 1);
            activityLayout.Children.Add(_activityContent);

            // ── Footer: status line plus the ticket workflow actions ──────────
            var footer = new Border
            {
                Background = Brushes.White,
                BorderBrush = HairlineBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(18, 8, 18, 8)
            };
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.Child = footerGrid;

            _statusLine = new TextBlock
            {
                Text = "Loading details...",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = MutedBrush,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            footerGrid.Children.Add(_statusLine);

            var footerActions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };
            _reopenButton = CreateActionButton("Reopen", "ItcmQuietButton");
            _reopenButton.Click += (_, __) => RequestAction(TicketDetailsDialog.TicketDetailAction.Reopen);
            _reassignButton = CreateActionButton("Reassign", "ItcmQuietButton");
            _reassignButton.Click += (_, __) => RequestAction(TicketDetailsDialog.TicketDetailAction.Reassign);
            _markAsButton = CreateActionButton("Mark As", "ItcmQuietButton");
            _markAsButton.Click += (_, __) => RequestAction(TicketDetailsDialog.TicketDetailAction.MarkAs);
            var fieldWorkButton = CreateActionButton("Field Work", "ItcmAccentButton");
            fieldWorkButton.Click += async (_, __) => await OpenFieldWorkAsync();
            footerActions.Children.Add(_reopenButton);
            footerActions.Children.Add(_reassignButton);
            footerActions.Children.Add(_markAsButton);
            footerActions.Children.Add(fieldWorkButton);
            Grid.SetColumn(footerActions, 1);
            footerGrid.Children.Add(footerActions);

            _reopenButton.Visibility = Visibility.Collapsed;
            _reassignButton.IsEnabled = false;
            _markAsButton.IsEnabled = false;

            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            ShowActivityTab(true);
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
                _statusBadge.Background = GetStatusBackground(ticket.Status);
                _statusText.Foreground = GetStatusForeground(ticket.Status);

                _callerText.Text = ticket.CallerName ?? "-";
                _departmentText.Text = ticket.Department ?? "-";
                _assigneeText.Text = string.IsNullOrWhiteSpace(ticket.ResponsiblePerson) ? "(Unassigned)" : ticket.ResponsiblePerson.Trim();
                _priorityText.Text = (ticket.Priority ?? "-").ToUpperInvariant();
                _priorityText.Foreground = GetPriorityBrush(ticket.Priority);
                if (_priorityText.Parent is Border priorityChip)
                    priorityChip.Background = GetPriorityBackground(ticket.Priority);
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

        private static Border CreateCard(double padding = 16)
        {
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = HairlineBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(padding),
                SnapsToDevicePixels = true
            };
        }

        private static TextBlock AddDetailCell(Grid grid, int row, int column, string label, string value)
        {
            var host = new StackPanel
            {
                Margin = new Thickness(column == 1 ? 14 : 0, 0, 0, 10)
            };
            host.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = MutedBrush
            });
            var valueBlock = new TextBlock
            {
                Text = value,
                FontSize = 12,
                Foreground = InkBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0)
            };
            host.Children.Add(valueBlock);
            Grid.SetRow(host, row);
            Grid.SetColumn(host, column);
            grid.Children.Add(host);
            return valueBlock;
        }

        private static TextBlock AddDetailChipCell(Grid grid, int row, int column, string label)
        {
            var host = new StackPanel
            {
                Margin = new Thickness(column == 1 ? 14 : 0, 0, 0, 10)
            };
            host.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = MutedBrush
            });
            var valueBlock = new TextBlock
            {
                Text = "-",
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BodyBrush
            };
            var chip = new Border
            {
                Background = SoftFillBrush,
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(9, 3, 9, 3),
                Margin = new Thickness(0, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = valueBlock
            };
            host.Children.Add(chip);
            Grid.SetRow(host, row);
            Grid.SetColumn(host, column);
            grid.Children.Add(host);
            return valueBlock;
        }

        private static Button CreateActionButton(string text, string styleKey)
        {
            var styles = WpfThemeResources.GetWorkspaceCommandStyles();
            return new Button
            {
                Content = text,
                Style = (Style)styles[styleKey],
                FontSize = 11.5,
                Padding = new Thickness(10, 6, 10, 6),
                MinWidth = 0,
                Margin = new Thickness(8, 0, 0, 0),
                Cursor = Cursors.Hand
            };
        }

        private static Button CreateTabButton(string text, bool active)
        {
            var styles = WpfThemeResources.GetWorkspaceCommandStyles();
            return new Button
            {
                Content = text,
                Style = (Style)styles[active ? "ItcmRangeButtonActive" : "ItcmRangeButton"],
                FontSize = 10.5,
                Padding = new Thickness(10, 5, 10, 5),
                MinWidth = 0,
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand
            };
        }

        private void ShowActivityTab(bool showNotes)
        {
            if (_activityContent == null)
                return;

            _activityContent.Content = showNotes ? (object)_notesGrid : _historyGrid;
            var styles = WpfThemeResources.GetWorkspaceCommandStyles();
            _notesTabButton.Style = (Style)styles[showNotes ? "ItcmRangeButtonActive" : "ItcmRangeButton"];
            _historyTabButton.Style = (Style)styles[showNotes ? "ItcmRangeButton" : "ItcmRangeButtonActive"];
        }

        private static DataGrid CreateGrid()
        {
            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, SoftFillBrush));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, MutedBrush));
            headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 10.0));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 0, 10, 0)));
            headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, HairlineBrush));
            headerStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            headerStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
            rowStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            var alternateRow = new Trigger { Property = ItemsControl.AlternationIndexProperty, Value = 1 };
            alternateRow.Setters.Add(new Setter(Control.BackgroundProperty, BrushFromRgb(250, 251, 252)));
            rowStyle.Triggers.Add(alternateRow);

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = HairlineBrush,
                BorderThickness = new Thickness(0),
                RowHeaderWidth = 0,
                Background = Brushes.White,
                AlternationCount = 2,
                RowStyle = rowStyle,
                ColumnHeaderHeight = 36,
                MinRowHeight = 38,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                ColumnHeaderStyle = headerStyle,
                CellStyle = CreateCellStyle()
            };

            // DataGrid star columns are not reliable outside a live window, so size the
            // final column explicitly to absorb whatever width the fixed columns leave.
            grid.SizeChanged += (_, __) => FillLastColumn(grid);
            return grid;
        }

        private static void FillLastColumn(DataGrid grid)
        {
            if (grid == null || grid.Columns.Count == 0)
                return;

            var last = grid.Columns[grid.Columns.Count - 1];
            var available = grid.ActualWidth - SystemParameters.VerticalScrollBarWidth;
            if (available <= last.MinWidth)
                return;

            double fixedWidth = 0;
            for (var i = 0; i < grid.Columns.Count - 1; i++)
            {
                var width = grid.Columns[i].Width;
                fixedWidth += width.UnitType == DataGridLengthUnitType.Pixel
                    ? width.DisplayValue
                    : grid.Columns[i].ActualWidth;
            }

            var target = Math.Max(last.MinWidth, available - fixedWidth);
            if (Math.Abs(last.ActualWidth - target) > 1)
                last.Width = new DataGridLength(target);
        }

        private static Style CreateCellStyle()
        {
            const string xaml = @"
                <Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                       TargetType=""DataGridCell"">
                    <Setter Property=""BorderThickness"" Value=""0""/>
                    <Setter Property=""Padding"" Value=""10,0""/>
                    <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
                    <Setter Property=""Foreground"" Value=""#4A5261""/>
                    <Setter Property=""FontSize"" Value=""11""/>
                    <Setter Property=""Template"">
                        <Setter.Value>
                            <ControlTemplate TargetType=""DataGridCell"">
                                <Border Background=""{TemplateBinding Background}""
                                        BorderBrush=""{TemplateBinding BorderBrush}""
                                        BorderThickness=""{TemplateBinding BorderThickness}"">
                                    <ContentPresenter Margin=""{TemplateBinding Padding}""
                                                      VerticalAlignment=""Center""
                                                      HorizontalAlignment=""Stretch""/>
                                </Border>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                    <Style.Triggers>
                        <Trigger Property=""IsSelected"" Value=""True"">
                            <Setter Property=""Background"" Value=""#FCF0F1""/>
                            <Setter Property=""Foreground"" Value=""#202635""/>
                        </Trigger>
                    </Style.Triggers>
                </Style>";

            return (Style)XamlReader.Parse(xaml);
        }

        private static void AddTextColumn(DataGrid grid, string header, string binding, double width, bool wrap = false, double minWidth = 0)
        {
            var textStyle = new Style(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11.0));
            textStyle.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(0, 6, 0, 6)));
            textStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));

            if (wrap)
            {
                textStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            }
            else
            {
                textStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
                textStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            }

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(binding),
                Width = width <= 0
                    ? new DataGridLength(1, DataGridLengthUnitType.Star)
                    : new DataGridLength(width),
                MinWidth = minWidth,
                ElementStyle = textStyle
            });
        }

        private static bool IsFinalStatus(string status)
        {
            var value = (status ?? string.Empty).Trim();
            return value.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                || value.Equals("Closed", StringComparison.OrdinalIgnoreCase)
                || value.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase);
        }

        private static Brush GetStatusBackground(string status)
        {
            switch ((status ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "pending": return BrushFromRgb(254, 249, 195);
                case "in progress": return BrushFromRgb(219, 234, 254);
                case "solved": return BrushFromRgb(220, 252, 231);
                case "closed": return BrushFromRgb(220, 252, 231);
                case "overdue": return BrushFromRgb(255, 228, 230);
                case "escalated": return BrushFromRgb(255, 237, 213);
                case "waiting on department": return BrushFromRgb(207, 250, 254);
                case "waiting on vendor": return BrushFromRgb(241, 245, 249);
                default: return BrushFromRgb(241, 245, 249);
            }
        }

        private static Brush GetStatusForeground(string status)
        {
            switch ((status ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "pending": return BrushFromRgb(133, 77, 14);
                case "in progress": return BrushFromRgb(30, 64, 175);
                case "solved": return BrushFromRgb(22, 101, 52);
                case "closed": return BrushFromRgb(21, 128, 61);
                case "overdue": return BrushFromRgb(159, 18, 57);
                case "escalated": return BrushFromRgb(154, 52, 18);
                case "waiting on department": return BrushFromRgb(21, 94, 117);
                case "waiting on vendor": return BrushFromRgb(71, 85, 105);
                default: return BrushFromRgb(71, 85, 105);
            }
        }

        private static Brush GetPriorityBrush(string priority)
        {
            switch ((priority ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "critical": return BrushFromRgb(153, 27, 27);
                case "high": return BrushFromRgb(194, 65, 12);
                case "medium": return BrushFromRgb(30, 64, 175);
                case "low": return BrushFromRgb(22, 101, 52);
                default: return BrushFromRgb(71, 85, 105);
            }
        }

        private static Brush GetPriorityBackground(string priority)
        {
            switch ((priority ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "critical": return BrushFromRgb(254, 226, 226);
                case "high": return BrushFromRgb(255, 237, 213);
                case "medium": return BrushFromRgb(219, 234, 254);
                case "low": return BrushFromRgb(220, 252, 231);
                default: return BrushFromRgb(241, 245, 249);
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
