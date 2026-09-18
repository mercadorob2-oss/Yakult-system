using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed class WpfEmployeeProfileDetailsDialog : Window
    {
        private readonly CallEmployeeProfileLookup _employee;
        private readonly ICallMonitoringRepository _repo;
        private readonly TextBlock _statusText;
        private readonly DatePicker _fromPicker;
        private readonly DatePicker _toPicker;
        private readonly TextBlock _periodText;
        private readonly TextBlock _refreshedText;
        private TextBlock _positionText;
        private TextBlock _emailValueText;
        private TextBox _emailEditor;
        private Button _saveEmailButton;
        private TextBlock _emailEditStatus;
        private readonly TextBlock _openValue;
        private readonly TextBlock _pendingValue;
        private readonly TextBlock _inProgressValue;
        private readonly TextBlock _escalatedValue;
        private readonly TextBlock _handledAssigneeValue;
        private readonly TextBlock _handledSolverValue;
        private readonly TextBlock _avgResolutionValue;
        private readonly TextBlock _slaValue;
        private readonly StackPanel _recentActivityPanel;
        private readonly DataGrid _openGrid;
        private readonly DataGrid _handledGrid;
        private readonly DataGrid _replacementGrid;
        private readonly Button _refreshButton;
        private bool _isRefreshing;

        public WpfEmployeeProfileDetailsDialog(CallEmployeeProfileLookup employee, ICallMonitoringRepository repo)
        {
            _employee = employee ?? throw new ArgumentNullException(nameof(employee));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));

            Title = "Employee Profile";
            Width = 1280;
            Height = 820;
            MinWidth = 1100;
            MinHeight = 720;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = Brushes.White;

            var root = new Grid { Background = Brushes.White };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Content = root;

            var left = BuildProfileRail();
            Grid.SetColumn(left, 0);
            root.Children.Add(left);

            var divider = new Border { Background = BrushFromRgb(226, 232, 240) };
            Grid.SetColumn(divider, 1);
            root.Children.Add(divider);

            var rightScroll = new ScrollViewer
            {
                Background = BrushFromRgb(248, 250, 252),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetColumn(rightScroll, 2);
            root.Children.Add(rightScroll);

            var right = new StackPanel { Margin = new Thickness(28, 24, 28, 28) };
            rightScroll.Content = right;

            var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            right.Children.Add(header);

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = "Performance Stats",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(15, 23, 42)
            });
            _periodText = new TextBlock
            {
                Text = string.Empty,
                Margin = new Thickness(0, 5, 0, 0),
                FontSize = 12,
                Foreground = BrushFromRgb(100, 116, 139)
            };
            titleStack.Children.Add(_periodText);
            header.Children.Add(titleStack);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            actions.Children.Add(CreateLabel("From"));
            _fromPicker = new DatePicker { SelectedDate = DateTime.Today.AddDays(-30), Width = 142, Margin = new Thickness(8, 0, 14, 0) };
            actions.Children.Add(_fromPicker);
            actions.Children.Add(CreateLabel("To"));
            _toPicker = new DatePicker { SelectedDate = DateTime.Today, Width = 142, Margin = new Thickness(8, 0, 14, 0) };
            actions.Children.Add(_toPicker);
            _refreshButton = CreateButton("Refresh", BrushFromRgb(37, 99, 235));
            _refreshButton.Click += async (_, __) => await LoadAsync();
            actions.Children.Add(_refreshButton);
            var closeButton = CreateButton("Close", BrushFromRgb(71, 85, 105));
            closeButton.Margin = new Thickness(8, 0, 0, 0);
            closeButton.Click += (_, __) => Close();
            actions.Children.Add(closeButton);
            Grid.SetColumn(actions, 1);
            header.Children.Add(actions);

            _refreshedText = new TextBlock
            {
                Text = string.Empty,
                FontSize = 12,
                Foreground = BrushFromRgb(100, 116, 139),
                Margin = new Thickness(0, 0, 0, 16)
            };
            right.Children.Add(_refreshedText);

            var performanceGrid = CreateThreeColumnGrid(new Thickness(0, 0, 0, 18));
            _handledSolverValue = AddMetric(performanceGrid, 0, "Tickets Solved", "Solved by this user", BrushFromRgb(37, 99, 235));
            _avgResolutionValue = AddMetric(performanceGrid, 1, "Avg Resolution", "Completed ticket average", BrushFromRgb(14, 165, 233));
            _slaValue = AddMetric(performanceGrid, 2, "SLA Compliance", "Completed within target", BrushFromRgb(16, 185, 129));
            right.Children.Add(performanceGrid);

            right.Children.Add(CreateSectionTitle("Current Workload"));
            var workloadGrid = CreateThreeColumnGrid(new Thickness(0, 0, 0, 18));
            _pendingValue = AddMetric(workloadGrid, 0, "Pending", "Awaiting action", BrushFromRgb(245, 158, 11));
            _inProgressValue = AddMetric(workloadGrid, 1, "In Progress", "Actively being worked", BrushFromRgb(124, 58, 237));
            _escalatedValue = AddMetric(workloadGrid, 2, "Escalated", "Needs attention", BrushFromRgb(239, 68, 68));
            right.Children.Add(workloadGrid);

            right.Children.Add(CreateSectionTitle("Resolution Activity"));
            var resolutionGrid = CreateTwoColumnGrid(new Thickness(0, 0, 0, 18));
            _openValue = AddMetric(resolutionGrid, 0, "Open Tickets", "Currently assigned", BrushFromRgb(15, 118, 110));
            _handledAssigneeValue = AddMetric(resolutionGrid, 1, "Solved (As Assignee)", "Assigned work completed", BrushFromRgb(16, 185, 129));
            right.Children.Add(resolutionGrid);

            right.Children.Add(CreateSectionTitle("Recent Activity"));
            _recentActivityPanel = new StackPanel();
            right.Children.Add(CreatePanelCard(_recentActivityPanel, new Thickness(0, 0, 0, 18), 220));

            right.Children.Add(CreateSectionTitle("Ticket Lists"));
            var tabs = new TabControl
            {
                Background = Brushes.Transparent,
                BorderBrush = BrushFromRgb(226, 232, 240),
                MinHeight = 330
            };
            _openGrid = CreateGrid();
            AddTextColumn(_openGrid, "Ticket", "TicketCode", 120);
            AddTextColumn(_openGrid, "Status", "Status", 115);
            AddTextColumn(_openGrid, "Priority", "Priority", 90);
            AddTextColumn(_openGrid, "Location", "Location", 210);
            AddTextColumn(_openGrid, "Issue", "Issue", 420);
            AddTextColumn(_openGrid, "Updated", "UpdatedAtText", 150);
            _openGrid.MouseDoubleClick += (_, __) => OpenSelectedTicket(_openGrid.SelectedItem);

            _handledGrid = CreateGrid();
            AddTextColumn(_handledGrid, "Ticket", "TicketCode", 120);
            AddTextColumn(_handledGrid, "Status", "Status", 115);
            AddTextColumn(_handledGrid, "Priority", "Priority", 90);
            AddTextColumn(_handledGrid, "Location", "Location", 210);
            AddTextColumn(_handledGrid, "Completed", "CompletedAtText", 150);
            AddTextColumn(_handledGrid, "Issue", "Issue", 420);
            _handledGrid.MouseDoubleClick += (_, __) => OpenSelectedTicket(_handledGrid.SelectedItem);

            _replacementGrid = CreateGrid();
            AddTextColumn(_replacementGrid, "Ticket", "TicketCode", 120);
            AddTextColumn(_replacementGrid, "Resolved", "ResolvedAtText", 150);
            AddTextColumn(_replacementGrid, "Old Item", "ReplacementOldItem", 260);
            AddTextColumn(_replacementGrid, "New Item", "ReplacementNewItem", 260);
            AddTextColumn(_replacementGrid, "Qty", "ReplacementQty", 70);
            AddTextColumn(_replacementGrid, "Remarks", "Remarks", 320);
            _replacementGrid.MouseDoubleClick += (_, __) => OpenSelectedTicket(_replacementGrid.SelectedItem);

            tabs.Items.Add(new TabItem { Header = "Open Tickets", Content = _openGrid });
            tabs.Items.Add(new TabItem { Header = "Handled Tickets", Content = _handledGrid });
            tabs.Items.Add(new TabItem { Header = "Replacement Items", Content = _replacementGrid });
            right.Children.Add(tabs);

            _statusText = new TextBlock
            {
                Text = "Loading...",
                FontSize = 12,
                Foreground = BrushFromRgb(100, 116, 139),
                Margin = new Thickness(0, 12, 0, 0)
            };
            right.Children.Add(_statusText);

            Loaded += async (_, __) =>
            {
                await LoadEmployeeEmailAsync();
                await LoadAsync();
            };
        }

        private FrameworkElement BuildProfileRail()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.White
            };

            var stack = new StackPanel { Margin = new Thickness(24, 30, 24, 24), HorizontalAlignment = HorizontalAlignment.Stretch };
            scroll.Content = stack;

            var avatar = new Border
            {
                Width = 120,
                Height = 120,
                CornerRadius = new CornerRadius(60),
                Background = new LinearGradientBrush(Color.FromRgb(37, 99, 235), Color.FromRgb(14, 165, 233), new Point(0, 0), new Point(1, 1)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = GetInitials(_employee.EmployeeName),
                    FontSize = 34,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            stack.Children.Add(avatar);

            stack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_employee.EmployeeName) ? "Employee" : _employee.EmployeeName.Trim(),
                Margin = new Thickness(0, 18, 0, 0),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(15, 23, 42),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });

            _positionText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_employee.Position) ? "IT Support Specialist" : _employee.Position.Trim(),
                Margin = new Thickness(0, 6, 0, 18),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(220, 38, 38),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(_positionText);

            stack.Children.Add(CreateQuote());
            stack.Children.Add(CreateMetaBlock());
            return scroll;
        }

        private FrameworkElement CreateQuote()
        {
            return new TextBlock
            {
                Text = "\"Providing excellent IT support to keep Yakult running smoothly.\"",
                FontSize = 12,
                FontStyle = FontStyles.Italic,
                Foreground = BrushFromRgb(100, 116, 139),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 24)
            };
        }

        private FrameworkElement CreateMetaBlock()
        {
            var stack = new StackPanel();
            stack.Children.Add(CreateMetaRow("Dept", "IT Department", null));
            stack.Children.Add(CreateMetaRow("Location", "Head Office", null));
            _emailValueText = new TextBlock
            {
                Text = "Loading...",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42),
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(CreateMetaRow("Email", _emailValueText, null));
            if (AppSession.IsAdmin || AppSession.IsDeveloper)
            {
                _emailEditor = new TextBox
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(7, 5, 7, 5),
                    FontSize = 12,
                    ToolTip = "Leave blank to clear the employee's primary email."
                };
                _saveEmailButton = CreateButton("Save Email", BrushFromRgb(16, 185, 129));
                _saveEmailButton.MinWidth = 112;
                _saveEmailButton.HorizontalAlignment = HorizontalAlignment.Left;
                _saveEmailButton.Margin = new Thickness(0, 6, 0, 0);
                _saveEmailButton.Click += async (_, __) => await SaveEmployeeEmailAsync();
                _emailEditStatus = new TextBlock
                {
                    FontSize = 11,
                    Foreground = BrushFromRgb(100, 116, 139),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 5, 0, 0)
                };
                var emailEditorPanel = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                emailEditorPanel.Children.Add(_emailEditor);
                emailEditorPanel.Children.Add(_saveEmailButton);
                emailEditorPanel.Children.Add(_emailEditStatus);
                stack.Children.Add(CreateMetaRow("Edit email", emailEditorPanel, null));
            }
            stack.Children.Add(CreateMetaRow("Status", _employee.UserId.HasValue ? "Active" : "No account", _employee.UserId.HasValue ? BrushFromRgb(22, 163, 74) : BrushFromRgb(245, 158, 11)));
            return stack;
        }

        private static FrameworkElement CreateMetaRow(string label, string value, Brush valueBrush)
        {
            var valueText = new TextBlock
            {
                Text = value,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = valueBrush ?? BrushFromRgb(15, 23, 42),
                TextWrapping = TextWrapping.Wrap
            };
            return CreateMetaRow(label, valueText, valueBrush);
        }

        private static FrameworkElement CreateMetaRow(string label, FrameworkElement valueControl, Brush valueBrush)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(100, 116, 139)
            });
            if (valueControl != null)
            {
                if (valueBrush != null && valueControl is Control control)
                    control.Foreground = valueBrush;
                Grid.SetColumn(valueControl, 1);
                grid.Children.Add(valueControl);
            }
            return grid;
        }

        private async Task LoadEmployeeEmailAsync()
        {
            try
            {
                var email = await _repo.GetEmployeeEmailBindingAsync(_employee.EmpId);
                var display = string.IsNullOrWhiteSpace(email) ? "(No email configured)" : email.Trim();
                if (_emailValueText != null) _emailValueText.Text = display;
                if (_emailEditor != null) _emailEditor.Text = email ?? string.Empty;
                if (_emailEditStatus != null) _emailEditStatus.Text = string.Empty;
            }
            catch (Exception ex)
            {
                if (_emailValueText != null) _emailValueText.Text = "(Unable to load email)";
                if (_emailEditStatus != null) _emailEditStatus.Text = ex.Message;
            }
        }

        private async Task SaveEmployeeEmailAsync()
        {
            if (!(AppSession.IsAdmin || AppSession.IsDeveloper) || _emailEditor == null)
                return;

            var value = (_emailEditor.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                try { _ = new System.Net.Mail.MailAddress(value); }
                catch
                {
                    _emailEditStatus.Text = "Enter a valid email address.";
                    return;
                }
            }
            else if (MessageBox.Show(this, "Clear this employee's primary email address?", "Confirm email removal", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _saveEmailButton.IsEnabled = false;
                await _repo.SaveEmployeeEmailBindingAsync(_employee.EmpId, value, AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null);
                await LoadEmployeeEmailAsync();
                _emailEditStatus.Text = string.IsNullOrWhiteSpace(value) ? "Primary email cleared." : "Email saved.";
            }
            catch (Exception ex)
            {
                _emailEditStatus.Text = ex.Message;
            }
            finally
            {
                _saveEmailButton.IsEnabled = true;
            }
        }

        private async Task LoadAsync()
        {
            if (_isRefreshing)
                return;

            if (!_fromPicker.SelectedDate.HasValue || !_toPicker.SelectedDate.HasValue || _fromPicker.SelectedDate.Value.Date > _toPicker.SelectedDate.Value.Date)
            {
                MessageBox.Show(this, "'From' date must be earlier than or equal to 'To' date.", "Employee Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isRefreshing = true;
            try
            {
                _refreshButton.IsEnabled = false;
                _fromPicker.IsEnabled = false;
                _toPicker.IsEnabled = false;
                _statusText.Text = "Loading...";
                _refreshedText.Text = "Refreshing...";

                var fromUtc = DateTime.SpecifyKind(_fromPicker.SelectedDate.Value.Date, DateTimeKind.Local).ToUniversalTime();
                var toUtc = DateTime.SpecifyKind(_toPicker.SelectedDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime();
                _periodText.Text = BuildPeriodHint(fromUtc, toUtc);

                var summaryTask = _repo.GetTechProfileSummaryAsync(_employee.EmpId, fromUtc, toUtc);
                var samplesTask = _repo.GetTechResolutionSamplesAsync(_employee.EmpId, fromUtc, toUtc);
                var openTask = _repo.GetOpenTicketsAssignedToEmployeeAsync(_employee.EmpId, 500);
                var handledTask = _repo.GetTechHandledTicketsAsync(_employee.EmpId, fromUtc, toUtc, 500);
                var replacementsTask = _repo.GetEmployeeReplacementItemsAsync(_employee.EmpId, fromUtc, toUtc, 500);
                await Task.WhenAll(summaryTask, samplesTask, openTask, handledTask, replacementsTask);

                var summary = summaryTask.Result;
                _openValue.Text = (summary?.AssignedOpenTotal ?? 0).ToString();
                _pendingValue.Text = (summary?.AssignedPending ?? 0).ToString();
                _inProgressValue.Text = (summary?.AssignedInProgress ?? 0).ToString();
                _escalatedValue.Text = (summary?.AssignedEscalated ?? 0).ToString();
                _handledAssigneeValue.Text = (summary?.HandledAsAssigneeSolvedClosed ?? 0).ToString();
                _handledSolverValue.Text = (summary?.HandledAsSolverSolvedClosed ?? 0).ToString();
                UpdateResolutionAndSla(samplesTask.Result);

                var openRows = openTask.Result ?? new List<CallTechOpenTicketRow>();
                var handledRows = handledTask.Result ?? new List<CallTechHandledTicketRow>();

                _openGrid.ItemsSource = openRows.Select(x => new TicketRowVm
                {
                    TicketId = x.TicketId,
                    TicketCode = x.TicketCode,
                    Status = x.Status,
                    Priority = x.Priority,
                    Location = x.Location,
                    Issue = x.Issue,
                    UpdatedAtText = ToLocalString(x.UpdatedAt)
                }).ToList();

                _handledGrid.ItemsSource = handledRows.Select(x => new TicketRowVm
                {
                    TicketId = x.TicketId,
                    TicketCode = x.TicketCode,
                    Status = x.Status,
                    Priority = x.Priority,
                    Location = x.Location,
                    Issue = x.Issue,
                    CompletedAtText = x.CompletedAtUtc.HasValue ? ToLocalString(x.CompletedAtUtc.Value) : "-"
                }).ToList();

                _replacementGrid.ItemsSource = (replacementsTask.Result ?? new List<CallEmployeeReplacementItemRow>()).Select(x => new ReplacementRowVm
                {
                    TicketId = x.TicketId,
                    TicketCode = x.TicketCode,
                    ResolvedAtText = ToLocalString(x.ResolutionMarkedAtUtc),
                    ReplacementOldItem = x.ReplacementOldItem,
                    ReplacementNewItem = x.ReplacementNewItem,
                    ReplacementQty = x.ReplacementQty,
                    Remarks = x.Remarks
                }).ToList();

                PopulateRecentActivity(openRows.OrderByDescending(t => t.UpdatedAt).Take(5).ToList());
                _positionText.Text = string.IsNullOrWhiteSpace(_employee.Position) ? "IT Support Specialist" : _employee.Position.Trim();
                _refreshedText.Text = $"Last refreshed: {AppTime.ToLocalString(AppTime.UtcNow, "g")}";
                _statusText.Text = $"{openRows.Count} open | {handledRows.Count} handled | {(_replacementGrid.Items.Count)} replacement rows";
            }
            catch (Exception ex)
            {
                _statusText.Text = "Error.";
                _refreshedText.Text = "Refresh failed.";
                MessageBox.Show(this, ex.Message, "Employee Profile", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _fromPicker.IsEnabled = true;
                _toPicker.IsEnabled = true;
                _refreshButton.IsEnabled = true;
                _isRefreshing = false;
            }
        }

        private void UpdateResolutionAndSla(List<CallMonitoringRepository.CallTechResolutionSampleRow> samples)
        {
            if (samples == null || samples.Count == 0)
            {
                _avgResolutionValue.Text = "-";
                _slaValue.Text = "-";
                return;
            }

            var durations = new List<double>(samples.Count);
            var compliant = 0;
            var total = 0;

            foreach (var sample in samples)
            {
                var createdUtc = AppTime.AssumeUtc(sample.CreatedAt);
                var completedUtc = AppTime.AssumeUtc(sample.CompletedAtUtc);
                var delta = completedUtc - createdUtc;
                if (delta < TimeSpan.Zero)
                    continue;

                total++;
                durations.Add(delta.TotalHours);
                var target = CallMonitoringSla.GetResolutionSlaTarget(sample.Priority, sample.IssueType);
                if (delta.TotalHours <= Math.Max(1, target.TargetHours))
                    compliant++;
            }

            if (total == 0)
            {
                _avgResolutionValue.Text = "-";
                _slaValue.Text = "-";
                return;
            }

            var avgHours = durations.Count == 0 ? 0 : durations.Average();
            _avgResolutionValue.Text = avgHours < 0.01 ? "0h" : $"{avgHours:0.0}h";
            _slaValue.Text = $"{(int)Math.Round((double)compliant * 100.0 / total)}%";
        }

        private void PopulateRecentActivity(List<CallTechOpenTicketRow> tickets)
        {
            _recentActivityPanel.Children.Clear();
            if (tickets == null || tickets.Count == 0)
            {
                _recentActivityPanel.Children.Add(new TextBlock
                {
                    Text = "No active tickets assigned.",
                    FontSize = 13,
                    FontStyle = FontStyles.Italic,
                    Foreground = BrushFromRgb(100, 116, 139),
                    Margin = new Thickness(2, 2, 2, 0)
                });
                return;
            }

            _recentActivityPanel.Children.Add(CreateRecentHeaderRow());
            foreach (var ticket in tickets)
                _recentActivityPanel.Children.Add(CreateRecentTicketRow(ticket));
        }

        private FrameworkElement CreateRecentHeaderRow()
        {
            var grid = CreateRecentGrid();
            grid.Margin = new Thickness(0, 0, 0, 6);
            AddRecentCell(grid, "Ticket", 0, true, null);
            AddRecentCell(grid, "Issue", 1, true, null);
            AddRecentCell(grid, "Updated", 2, true, null);
            AddRecentCell(grid, "Status", 3, true, null);
            return grid;
        }

        private FrameworkElement CreateRecentTicketRow(CallTechOpenTicketRow ticket)
        {
            var grid = CreateRecentGrid();
            grid.Cursor = Cursors.Hand;
            grid.Margin = new Thickness(0, 0, 0, 6);
            grid.Background = Brushes.White;
            grid.MouseLeftButtonUp += (_, __) => OpenSelectedTicket(new TicketRowVm { TicketId = ticket.TicketId });

            AddRecentCell(grid, string.IsNullOrWhiteSpace(ticket.TicketCode) ? $"#{ticket.TicketId}" : ticket.TicketCode, 0, false, BrushFromRgb(2, 132, 199));
            AddRecentCell(grid, string.IsNullOrWhiteSpace(ticket.Issue) ? "-" : ticket.Issue, 1, false, BrushFromRgb(15, 23, 42));
            AddRecentCell(grid, ToLocalString(ticket.UpdatedAt), 2, false, BrushFromRgb(71, 85, 105));
            AddRecentCell(grid, string.IsNullOrWhiteSpace(ticket.Status) ? "-" : ticket.Status, 3, false, GetStatusBrush(ticket.Status));
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 9, 10, 9),
                Child = grid
            };
        }

        private static Grid CreateRecentGrid()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
            return grid;
        }

        private static void AddRecentCell(Grid grid, string text, int column, bool header, Brush foreground)
        {
            var cell = new TextBlock
            {
                Text = text,
                FontSize = header ? 11 : 12,
                FontWeight = header ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = foreground ?? BrushFromRgb(100, 116, 139),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(column == 0 ? 0 : 8, 0, 0, 0)
            };
            Grid.SetColumn(cell, column);
            grid.Children.Add(cell);
        }

        private void OpenSelectedTicket(object selected)
        {
            var ticketId = selected is TicketRowVm ticket ? ticket.TicketId : selected is ReplacementRowVm replacement ? replacement.TicketId : 0;
            if (ticketId <= 0)
                return;

            var dlg = new WpfTicketDetailsDialog(_repo, ticketId) { Owner = this };
            dlg.ShowDialog();
        }

        private static Grid CreateThreeColumnGrid(Thickness margin)
        {
            var grid = new Grid { Margin = margin };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            return grid;
        }

        private static Grid CreateTwoColumnGrid(Thickness margin)
        {
            var grid = new Grid { Margin = margin };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            return grid;
        }

        private static TextBlock AddMetric(Grid host, int column, string label, string subtitle, Brush accent)
        {
            var value = new TextBlock
            {
                Text = "0",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(15, 23, 42),
                Margin = new Thickness(0, 8, 0, 0)
            };

            var stack = new StackPanel();
            stack.Children.Add(new Border
            {
                Width = 36,
                Height = 4,
                CornerRadius = new CornerRadius(99),
                Background = accent,
                HorizontalAlignment = HorizontalAlignment.Left
            });
            stack.Children.Add(new TextBlock
            {
                Text = label,
                Margin = new Thickness(0, 12, 0, 0),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(71, 85, 105)
            });
            stack.Children.Add(value);
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                Margin = new Thickness(0, 5, 0, 0),
                FontSize = 11.5,
                Foreground = BrushFromRgb(100, 116, 139),
                TextWrapping = TextWrapping.Wrap
            });

            var card = CreatePanelCard(stack, new Thickness(column == 0 ? 0 : 12, 0, 0, 0), 0);
            Grid.SetColumn(card, column);
            host.Children.Add(card);
            return value;
        }

        private static Border CreatePanelCard(UIElement child, Thickness margin, double minHeight)
        {
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16),
                Margin = margin,
                MinHeight = minHeight,
                Effect = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 0, Color = Color.FromArgb(20, 15, 23, 42), Opacity = 0.18 },
                Child = child
            };
        }

        private static TextBlock CreateSectionTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(15, 23, 42),
                Margin = new Thickness(0, 4, 0, 10)
            };
        }

        private static TextBlock CreateLabel(string text)
        {
            return new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(71, 85, 105) };
        }

        private static DataGrid CreateGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                RowHeaderWidth = 0,
                RowHeight = 42,
                Background = Brushes.White,
                AlternatingRowBackground = BrushFromRgb(248, 250, 252),
                BorderThickness = new Thickness(0),
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow
            };

            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(100, 116, 139)));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 10, 8, 10)));
            grid.ColumnHeaderStyle = headerStyle;
            return grid;
        }

        private static void AddTextColumn(DataGrid grid, string header, string binding, double width)
        {
            grid.Columns.Add(new DataGridTextColumn { Header = header, Binding = new Binding(binding), Width = new DataGridLength(width) });
        }

        private static Button CreateButton(string text, Brush background)
        {
            return new Button
            {
                Content = text,
                MinWidth = 96,
                Padding = new Thickness(14, 8, 14, 8),
                Background = background,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
        }

        private static string BuildPeriodHint(DateTime fromUtc, DateTime toUtc)
        {
            var from = AppTime.ToLocalString(fromUtc, "MMM d, yyyy");
            var to = AppTime.ToLocalString(toUtc, "MMM d, yyyy");
            return $"Period: {from} to {to}";
        }

        private static string GetInitials(string name)
        {
            var parts = (name ?? string.Empty)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(p => p.Substring(0, 1).ToUpperInvariant())
                .ToArray();
            return parts.Length == 0 ? "IT" : string.Join(string.Empty, parts);
        }

        private static Brush GetStatusBrush(string status)
        {
            var text = (status ?? string.Empty).Trim();
            if (text.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(239, 68, 68);
            if (text.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(124, 58, 237);
            if (text.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(37, 99, 235);
            if (text.Equals("Solved", StringComparison.OrdinalIgnoreCase) || text.Equals("Closed", StringComparison.OrdinalIgnoreCase)) return BrushFromRgb(16, 185, 129);
            return BrushFromRgb(71, 85, 105);
        }

        private static string ToLocalString(DateTime dt)
        {
            return AppTime.ToLocalString(dt, "g");
        }

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private sealed class TicketRowVm
        {
            public int TicketId { get; set; }
            public string TicketCode { get; set; }
            public string Status { get; set; }
            public string Priority { get; set; }
            public string Location { get; set; }
            public string Issue { get; set; }
            public string UpdatedAtText { get; set; }
            public string CompletedAtText { get; set; }
        }

        private sealed class ReplacementRowVm
        {
            public int TicketId { get; set; }
            public string TicketCode { get; set; }
            public string ResolvedAtText { get; set; }
            public string ReplacementOldItem { get; set; }
            public string ReplacementNewItem { get; set; }
            public int? ReplacementQty { get; set; }
            public string Remarks { get; set; }
        }
    }
}
