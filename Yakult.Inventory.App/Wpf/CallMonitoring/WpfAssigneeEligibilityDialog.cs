using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed class WpfAssigneeEligibilityDialog : Window
    {
        private readonly ObservableCollection<CallAssignmentEligibilityItem> _items;
        private readonly ICollectionView _view;
        private readonly bool _supportsAssignmentEligibility;
        private readonly bool _supportsAutoEscalationDefault;
        private readonly bool _supportsEscalationSettings;
        private readonly CallEscalationSettingsItem _escalationSettings;
        private readonly TextBox _searchBox;
        private readonly TextBlock _summaryText;
        private readonly TextBlock _helperText;
        private readonly ComboBox _daysToSupervisorBox;
        private readonly ComboBox _daysToManagerBox;
        private readonly ComboBox _supervisorPositionBox;
        private readonly ComboBox _managerPositionBox;
        private readonly ComboBox _escalationAssigneeBox;
        private readonly TextBlock _currentEscalationAssigneeText;
        private readonly TextBlock _escalationAssigneeHelperText;
        private bool _normalizing;
        private readonly bool _isDeveloper;
        private readonly Func<CallAssignmentEligibilityItem, Task<bool>> _onLinkAccount;
        private Button _linkAccountButton;
        private DataGrid _grid;

        public IReadOnlyList<CallAssignmentEligibilityItem> Items => _items.ToList();

        public int? SelectedAutoEscalationEmpId => _items.FirstOrDefault(x => x.IsDefaultAutoEscalation)?.EmpId;

        public CallEscalationSettingsItem EscalationSettings => new CallEscalationSettingsItem
        {
            DaysToSupervisor = GetSelectedDay(_daysToSupervisorBox, _escalationSettings.DaysToSupervisor),
            DaysToManager = GetSelectedDay(_daysToManagerBox, _escalationSettings.DaysToManager),
            SupervisorPosition = (_supervisorPositionBox.SelectedItem as string ?? string.Empty).Trim(),
            ManagerPosition = (_managerPositionBox.SelectedItem as string ?? string.Empty).Trim()
        };

        public WpfAssigneeEligibilityDialog(
            IEnumerable<CallAssignmentEligibilityItem> items,
            CallEscalationSettingsItem escalationSettings,
            int? currentAutoEscalationEmpId,
            bool supportsAssignmentEligibility,
            bool supportsAutoEscalationDefault,
            bool supportsEscalationSettings,
            Func<CallAssignmentEligibilityItem, Task<bool>> onLinkAccount = null,
            bool isDeveloper = false)
        {
            _isDeveloper = isDeveloper;
            _onLinkAccount = onLinkAccount;
            _supportsAssignmentEligibility = supportsAssignmentEligibility;
            _supportsAutoEscalationDefault = supportsAutoEscalationDefault;
            _supportsEscalationSettings = supportsEscalationSettings;
            _escalationSettings = new CallEscalationSettingsItem
            {
                DaysToSupervisor = Math.Max(1, escalationSettings?.DaysToSupervisor ?? 2),
                DaysToManager = Math.Max(Math.Max(1, escalationSettings?.DaysToSupervisor ?? 2), escalationSettings?.DaysToManager ?? 3),
                SupervisorPosition = string.IsNullOrWhiteSpace(escalationSettings?.SupervisorPosition) ? "IT Supervisor" : escalationSettings.SupervisorPosition.Trim(),
                ManagerPosition = string.IsNullOrWhiteSpace(escalationSettings?.ManagerPosition) ? "IT Manager" : escalationSettings.ManagerPosition.Trim()
            };
            _items = new ObservableCollection<CallAssignmentEligibilityItem>(
                (items ?? Enumerable.Empty<CallAssignmentEligibilityItem>())
                .Where(x => x != null)
                .OrderBy(x => x.EmployeeName ?? string.Empty));

            foreach (var item in _items)
            {
                if (currentAutoEscalationEmpId.HasValue && item.EmpId == currentAutoEscalationEmpId.Value)
                    item.IsDefaultAutoEscalation = true;

                item.PropertyChanged += ItemOnPropertyChanged;
            }

            _searchBox = new TextBox
            {
                Padding = new Thickness(14, 10, 14, 10),
                FontSize = 13,
                MinWidth = 280,
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Foreground = BrushFromRgb(15, 23, 42)
            };

            _summaryText = new TextBlock
            {
                FontSize = 12,
                Foreground = BrushFromRgb(71, 85, 105),
                VerticalAlignment = VerticalAlignment.Center
            };

            _helperText = new TextBlock
            {
                FontSize = 12,
                Foreground = BrushFromRgb(100, 116, 139),
                TextWrapping = TextWrapping.Wrap
            };

            _daysToSupervisorBox = CreateDayComboBox(_escalationSettings.DaysToSupervisor);
            _daysToManagerBox = CreateDayComboBox(_escalationSettings.DaysToManager);

            var distinctPositions = _items
                .Select(x => (x.Position ?? string.Empty).Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _supervisorPositionBox = CreatePositionComboBox(distinctPositions, _escalationSettings.SupervisorPosition);
            _managerPositionBox = CreatePositionComboBox(distinctPositions, _escalationSettings.ManagerPosition);
            _escalationAssigneeBox = new ComboBox
            {
                MinWidth = 360,
                Height = 38,
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 12,
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1),
                DisplayMemberPath = "DisplayText"
            };
            _currentEscalationAssigneeText = new TextBlock
            {
                FontSize = 12,
                Foreground = BrushFromRgb(15, 23, 42),
                TextWrapping = TextWrapping.Wrap,
                Text = "-"
            };
            _escalationAssigneeHelperText = new TextBlock
            {
                FontSize = 12,
                Foreground = BrushFromRgb(100, 116, 139),
                TextWrapping = TextWrapping.Wrap
            };

            if (!_supportsEscalationSettings)
            {
                _daysToSupervisorBox.IsEnabled = false;
                _daysToManagerBox.IsEnabled = false;
                _supervisorPositionBox.IsEnabled = false;
                _managerPositionBox.IsEnabled = false;
            }

            if (!_supportsAutoEscalationDefault)
                _escalationAssigneeBox.IsEnabled = false;

            _escalationAssigneeBox.SelectionChanged += EscalationAssigneeBoxOnSelectionChanged;
            _supervisorPositionBox.SelectionChanged += (_, __) => RefreshEscalationAssigneeOptions();

            _view = CollectionViewSource.GetDefaultView(_items);
            _view.Filter = FilterItem;
            _searchBox.TextChanged += (_, __) => _view.Refresh();

            Title = "Manage IT Assignees";
            Width = 1120;
            Height = 760;
            MinWidth = 980;
            MinHeight = 680;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            Background = BrushFromRgb(241, 244, 247);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var header = BuildHeader();
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var body = BuildBody();
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            var footer = BuildFooter();
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            AttachDynamicControls(header);

            NormalizeAllItems();
            RefreshSummary();
            RefreshEscalationAssigneeOptions();
        }

        private Border BuildHeader()
        {
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(28, 24, 28, 20)
            };
        }

        private Grid BuildBody()
        {
            var bodyGrid = new Grid { Margin = new Thickness(24, 20, 24, 20) };
            var tabs = new TabControl
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };

            tabs.Items.Add(new TabItem
            {
                Header = "Assignee Eligibility",
                Content = BuildAssignmentTab()
            });

            tabs.Items.Add(new TabItem
            {
                Header = "Escalation Assignee",
                Content = BuildEscalationAssigneeTab()
            });

            tabs.Items.Add(new TabItem
            {
                Header = "Escalation Settings",
                Content = BuildEscalationSettingsTab()
            });

            bodyGrid.Children.Add(tabs);
            return bodyGrid;
        }

        private Grid BuildAssignmentTab()
        {
            var contentGrid = new Grid();
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var topCard = CreateCard();
            topCard.Padding = new Thickness(20, 18, 20, 18);
            Grid.SetRow(topCard, 0);
            contentGrid.Children.Add(topCard);

            var cardGrid = new Grid();
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topCard.Child = cardGrid;

            var searchWrap = new StackPanel { Orientation = Orientation.Vertical };
            searchWrap.Children.Add(new TextBlock
            {
                Text = "Search IT users",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105),
                Margin = new Thickness(0, 0, 0, 8)
            });
            searchWrap.Children.Add(_searchBox);
            Grid.SetColumn(searchWrap, 0);
            cardGrid.Children.Add(searchWrap);

            var notesWrap = new StackPanel { Margin = new Thickness(24, 2, 24, 0) };
            notesWrap.Children.Add(_summaryText);
            notesWrap.Children.Add(new Border { Height = 8, Background = Brushes.Transparent });
            notesWrap.Children.Add(_helperText);
            Grid.SetColumn(notesWrap, 1);
            cardGrid.Children.Add(notesWrap);

            var legend = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            legend.Children.Add(CreateBadge("Ready", Color.FromRgb(220, 252, 231), Color.FromRgb(22, 101, 52)));
            legend.Children.Add(CreateBadge("No active account", Color.FromRgb(254, 242, 242), Color.FromRgb(153, 27, 27), new Thickness(10, 0, 0, 0)));
            legend.Children.Add(CreateBadge("Missing allowed role", Color.FromRgb(255, 247, 237), Color.FromRgb(154, 52, 18), new Thickness(10, 0, 0, 0)));
            Grid.SetColumn(legend, 2);
            cardGrid.Children.Add(legend);

            var tableCard = CreateCard();
            tableCard.Padding = new Thickness(0);
            tableCard.Margin = new Thickness(0, 16, 0, 0);
            Grid.SetRow(tableCard, 1);
            contentGrid.Children.Add(tableCard);

            _grid = BuildGrid();
            tableCard.Child = _grid;

            if (_isDeveloper)
            {
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 12, 0, 0)
                };
                _linkAccountButton = CreateActionButton("Link Account...", false);
                _linkAccountButton.IsEnabled = false;
                _linkAccountButton.Click += async (_, __) => await OnLinkAccountClickedAsync();
                buttonPanel.Children.Add(_linkAccountButton);
                Grid.SetRow(buttonPanel, 2);
                contentGrid.Children.Add(buttonPanel);
            }

            _grid.SelectionChanged += GridOnSelectionChanged;
            GridOnSelectionChanged(null, null);

            return contentGrid;
        }

        private Grid BuildEscalationAssigneeTab()
        {
            var contentGrid = new Grid();
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var infoCard = CreateCard();
            infoCard.Padding = new Thickness(24, 20, 24, 20);
            Grid.SetRow(infoCard, 0);
            contentGrid.Children.Add(infoCard);

            var infoStack = new StackPanel();
            infoCard.Child = infoStack;
            infoStack.Children.Add(new TextBlock
            {
                Text = "Escalation assignee",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(15, 23, 42)
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = "Choose the actual escalation person when there are multiple supervisors or escalation-ready users. This mirrors the original single-person escalation assignee behavior.",
                FontSize = 12,
                Foreground = BrushFromRgb(100, 116, 139),
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            var formCard = CreateCard();
            formCard.Padding = new Thickness(24, 22, 24, 22);
            formCard.Margin = new Thickness(0, 16, 0, 0);
            Grid.SetRow(formCard, 1);
            contentGrid.Children.Add(formCard);

            var formStack = new StackPanel();
            formCard.Child = formStack;
            formStack.Children.Add(CreateFieldHost("Current escalation assignee", _currentEscalationAssigneeText));
            formStack.Children.Add(CreateFieldHost("Select escalation person", _escalationAssigneeBox, new Thickness(0, 18, 0, 0)));
            formStack.Children.Add(new Border { Height = 14, Background = Brushes.Transparent });
            formStack.Children.Add(_escalationAssigneeHelperText);

            return contentGrid;
        }

        private Grid BuildEscalationSettingsTab()
        {
            var contentGrid = new Grid();
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var infoCard = CreateCard();
            infoCard.Padding = new Thickness(24, 20, 24, 20);
            Grid.SetRow(infoCard, 0);
            contentGrid.Children.Add(infoCard);

            var infoStack = new StackPanel();
            infoCard.Child = infoStack;
            infoStack.Children.Add(new TextBlock
            {
                Text = "Escalation supervisor and manager",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(15, 23, 42)
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = _supportsEscalationSettings
                    ? "Manage the default escalation timing and the employee positions used by the escalation engine. These settings apply system-wide."
                    : "Escalation settings schema is not installed. The values below are shown for reference and cannot be edited until the escalation settings script is installed.",
                FontSize = 12,
                Foreground = BrushFromRgb(100, 116, 139),
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            var formCard = CreateCard();
            formCard.Padding = new Thickness(24, 22, 24, 22);
            formCard.Margin = new Thickness(0, 16, 0, 0);
            Grid.SetRow(formCard, 1);
            contentGrid.Children.Add(formCard);

            var formGrid = new Grid();
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formCard.Child = formGrid;

            var timingPanel = new StackPanel();
            timingPanel.Children.Add(CreateSectionTitle("Escalation timing"));
            timingPanel.Children.Add(CreateFieldHost("Supervisor escalation after (days)", _daysToSupervisorBox));
            timingPanel.Children.Add(CreateFieldHost("Manager escalation after (days)", _daysToManagerBox, new Thickness(0, 16, 0, 0)));
            Grid.SetColumn(timingPanel, 0);
            Grid.SetRow(timingPanel, 0);
            formGrid.Children.Add(timingPanel);

            var rolePanel = new StackPanel();
            rolePanel.Children.Add(CreateSectionTitle("Escalation role matching"));
            rolePanel.Children.Add(CreateFieldHost("Supervisor position", _supervisorPositionBox));
            rolePanel.Children.Add(CreateFieldHost("Manager position", _managerPositionBox, new Thickness(0, 16, 0, 0)));
            Grid.SetColumn(rolePanel, 2);
            Grid.SetRow(rolePanel, 0);
            formGrid.Children.Add(rolePanel);

            var summaryBorder = new Border
            {
                Background = BrushFromRgb(248, 250, 252),
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 20, 0, 0)
            };
            summaryBorder.Child = new TextBlock
            {
                Text = "Tickets escalate to the configured supervisor position first, then to the configured manager position. Manager days must be greater than or equal to supervisor days.",
                FontSize = 12,
                Foreground = BrushFromRgb(71, 85, 105),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(summaryBorder, 0);
            Grid.SetColumnSpan(summaryBorder, 3);
            Grid.SetRow(summaryBorder, 1);
            formGrid.Children.Add(summaryBorder);

            return contentGrid;
        }

        private Border BuildFooter()
        {
            var footer = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(24, 16, 24, 16)
            };

            var actions = new DockPanel();
            footer.Child = actions;

            var info = new TextBlock
            {
                Text = "Manage assignees and escalation rules from one dialog.",
                FontSize = 12,
                Foreground = BrushFromRgb(100, 116, 139),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(info, Dock.Left);
            actions.Children.Add(info);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(buttons, Dock.Right);
            actions.Children.Add(buttons);

            var cancel = CreateActionButton("Cancel", false);
            cancel.Click += (_, __) =>
            {
                DialogResult = false;
                Close();
            };
            buttons.Children.Add(cancel);

            var save = CreateActionButton("Save Changes", true);
            save.Margin = new Thickness(10, 0, 0, 0);
            save.Click += (_, __) => SaveAndClose();
            buttons.Children.Add(save);

            return footer;
        }

        private void AttachDynamicControls(Border header)
        {
            var headerPanel = new DockPanel();
            header.Child = headerPanel;

            var textStack = new StackPanel { Orientation = Orientation.Vertical };
            DockPanel.SetDock(textStack, Dock.Left);
            headerPanel.Children.Add(textStack);

            textStack.Children.Add(new TextBlock
            {
                Text = "Manage IT assignees and escalation",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(15, 23, 42)
            });

            textStack.Children.Add(new TextBlock
            {
                Text = "Use separate tabs to manage assignee eligibility and the escalation supervisor or manager rules.",
                FontSize = 13,
                Foreground = BrushFromRgb(100, 116, 139),
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
        }

        private DataGrid BuildGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(0),
                Background = Brushes.White,
                RowBackground = Brushes.White,
                AlternatingRowBackground = BrushFromRgb(248, 250, 252),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0),
                ItemsSource = _view,
                IsReadOnly = false,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                RowHeaderWidth = 0
            };

            // Column header style
            grid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader));
            grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BackgroundProperty, BrushFromRgb(241, 245, 249)));
            grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(51, 65, 85)));
            grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
            grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(16, 12, 16, 12)));
            grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFromRgb(226, 232, 240)));
            grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));

            // Cell style
            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(16, 10, 16, 10)));
            cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(Control.VerticalAlignmentProperty, VerticalAlignment.Center));
            grid.CellStyle = cellStyle;

            // Row style with hover + softer selection
            grid.RowStyle = new Style(typeof(DataGridRow));
            grid.RowStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(15, 23, 42)));
            grid.RowStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
            grid.RowStyle.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFromRgb(241, 245, 249)));
            grid.RowStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            grid.RowStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4)));

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, BrushFromRgb(248, 250, 252)));
            grid.RowStyle.Triggers.Add(hoverTrigger);

            var selectedTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, BrushFromRgb(239, 246, 255)));
            selectedTrigger.Setters.Add(new Setter(DataGridRow.BorderBrushProperty, BrushFromRgb(59, 130, 246)));
            selectedTrigger.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0, 0, 0, 2)));
            grid.RowStyle.Triggers.Add(selectedTrigger);

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Employee",
                Binding = new Binding(nameof(CallAssignmentEligibilityItem.EmployeeName)),
                Width = new DataGridLength(1.35, DataGridLengthUnitType.Star),
                IsReadOnly = true
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Username",
                Binding = new Binding(nameof(CallAssignmentEligibilityItem.DisplayUserName)),
                Width = new DataGridLength(1.1, DataGridLengthUnitType.Star),
                IsReadOnly = true
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Position",
                Binding = new Binding(nameof(CallAssignmentEligibilityItem.Position)),
                Width = new DataGridLength(1.05, DataGridLengthUnitType.Star),
                IsReadOnly = true
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Allowed Access",
                Binding = new Binding(nameof(CallAssignmentEligibilityItem.AllowedAccessDisplay)),
                Width = new DataGridLength(1.4, DataGridLengthUnitType.Star),
                IsReadOnly = true
            });

            grid.Columns.Add(CreateStatusBadgeColumn());

            var checkHeaderStyle = new Style(typeof(DataGridColumnHeader));
            checkHeaderStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));

            grid.Columns.Add(CreateCheckColumn("Assignment", nameof(CallAssignmentEligibilityItem.IsAssignmentEligible), !_supportsAssignmentEligibility, checkHeaderStyle));
            grid.Columns.Add(CreateCheckColumn("Escalation Pool", nameof(CallAssignmentEligibilityItem.IsEscalationEligible), !_supportsAssignmentEligibility, checkHeaderStyle));

            return grid;
        }

        private DataGridTemplateColumn CreateStatusBadgeColumn()
        {
            var column = new DataGridTemplateColumn
            {
                Header = "Status",
                Width = new DataGridLength(1.0, DataGridLengthUnitType.Star),
                IsReadOnly = true
            };

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "badgeBorder";
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(8, 4, 8, 4));
            borderFactory.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            borderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);
            borderFactory.SetValue(Border.BackgroundProperty, BrushFromRgb(241, 245, 249));

            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.Name = "badgeText";
            textFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(CallAssignmentEligibilityItem.EligibilityStatus)));
            textFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            textFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            textFactory.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(71, 85, 105));
            borderFactory.AppendChild(textFactory);

            var readyTrigger = new DataTrigger
            {
                Binding = new Binding(nameof(CallAssignmentEligibilityItem.EligibilityStatus)),
                Value = "Ready"
            };
            readyTrigger.Setters.Add(new Setter(Border.BackgroundProperty, BrushFromRgb(220, 252, 231), "badgeBorder"));
            readyTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, BrushFromRgb(22, 101, 52), "badgeText"));

            var noAccountTrigger = new DataTrigger
            {
                Binding = new Binding(nameof(CallAssignmentEligibilityItem.EligibilityStatus)),
                Value = "No active account"
            };
            noAccountTrigger.Setters.Add(new Setter(Border.BackgroundProperty, BrushFromRgb(254, 242, 242), "badgeBorder"));
            noAccountTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, BrushFromRgb(153, 27, 27), "badgeText"));

            var missingRoleTrigger = new DataTrigger
            {
                Binding = new Binding(nameof(CallAssignmentEligibilityItem.EligibilityStatus)),
                Value = "Missing allowed role"
            };
            missingRoleTrigger.Setters.Add(new Setter(Border.BackgroundProperty, BrushFromRgb(255, 247, 237), "badgeBorder"));
            missingRoleTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, BrushFromRgb(154, 52, 18), "badgeText"));

            var template = new DataTemplate { VisualTree = borderFactory };
            template.Triggers.Add(readyTrigger);
            template.Triggers.Add(noAccountTrigger);
            template.Triggers.Add(missingRoleTrigger);

            column.CellTemplate = template;
            return column;
        }

        private DataGridCheckBoxColumn CreateCheckColumn(string header, string propertyName, bool readOnly = false, Style headerStyle = null)
        {
            var checkStyle = new Style(typeof(CheckBox));
            checkStyle.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            checkStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));

            return new DataGridCheckBoxColumn
            {
                Header = header,
                HeaderStyle = headerStyle,
                Binding = new Binding(propertyName)
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                },
                ElementStyle = checkStyle,
                Width = new DataGridLength(138),
                IsReadOnly = readOnly
            };
        }

        private bool FilterItem(object obj)
        {
            if (!(obj is CallAssignmentEligibilityItem item))
                return false;

            var query = (_searchBox?.Text ?? string.Empty).Trim();
            if (query.Length == 0)
                return true;

            return Contains(item.EmployeeName, query)
                   || Contains(item.UserName, query)
                   || Contains(item.Position, query)
                   || Contains(item.AllowedAccessDisplay, query)
                   || Contains(item.EligibilityStatus, query);
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value)
                   && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ItemOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_normalizing)
                return;

            if (!(sender is CallAssignmentEligibilityItem item))
                return;

            NormalizeItem(item, e.PropertyName);
            RefreshSummary();
            RefreshEscalationAssigneeOptions();
        }

        private void NormalizeAllItems()
        {
            foreach (var item in _items)
                NormalizeItem(item, null);
        }

        private void NormalizeItem(CallAssignmentEligibilityItem item, string propertyName)
        {
            _normalizing = true;
            try
            {
                if (!item.CanBeAssigned)
                {
                    item.IsAssignmentEligible = false;
                    item.IsEscalationEligible = false;
                    item.IsDefaultAutoEscalation = false;
                    return;
                }

                if (string.Equals(propertyName, nameof(CallAssignmentEligibilityItem.IsAssignmentEligible), StringComparison.Ordinal)
                    && !item.IsAssignmentEligible)
                {
                    item.IsEscalationEligible = false;
                    item.IsDefaultAutoEscalation = false;
                }

                if (item.IsEscalationEligible && !item.IsAssignmentEligible)
                    item.IsAssignmentEligible = true;

                if (!item.IsEscalationEligible)
                    item.IsDefaultAutoEscalation = false;

                if (item.IsDefaultAutoEscalation)
                {
                    if (!_supportsAutoEscalationDefault)
                    {
                        item.IsDefaultAutoEscalation = false;
                    }
                    else
                    {
                        item.IsAssignmentEligible = true;
                        item.IsEscalationEligible = true;
                        foreach (var other in _items.Where(x => !ReferenceEquals(x, item) && x.IsDefaultAutoEscalation))
                            other.IsDefaultAutoEscalation = false;
                    }
                }
            }
            finally
            {
                _normalizing = false;
            }
        }

        private void RefreshSummary()
        {
            var ready = _items.Count(x => x.CanBeAssigned);
            var assignment = _items.Count(x => x.IsAssignmentEligible);
            var escalation = _items.Count(x => x.IsEscalationEligible);
            var defaultAssignee = _items.FirstOrDefault(x => x.IsDefaultAutoEscalation)?.EmployeeName ?? "None";
            _summaryText.Text = $"{assignment} assignable • {escalation} escalation-ready • {ready} technically eligible • Escalation assignee: {defaultAssignee}";

            if (_supportsAssignmentEligibility && _supportsAutoEscalationDefault)
            {
                _helperText.Text = "Use Assignment to control assignee dropdown visibility. Escalation Pool narrows who can become the auto-escalation default. Only one default can be active at a time.";
            }
            else if (_supportsAssignmentEligibility)
            {
                _helperText.Text = "Assignment eligibility is available now. Auto-escalation default is disabled until the CallAutoEscalationAssigneeSettings database script is installed.";
            }
            else if (_supportsAutoEscalationDefault)
            {
                _helperText.Text = "Assignment eligibility overrides are not installed, so the list is based on current IT-role rules. You can still choose the escalation assignee in the separate tab.";
            }
            else
            {
                _helperText.Text = "This tab is view-only until the assignment eligibility or escalation assignee schema is installed.";
            }

            _currentEscalationAssigneeText.Text = defaultAssignee;
        }

        private void SaveAndClose()
        {
            var invalidAssignment = _items.Where(x => x.IsAssignmentEligible && !x.CanBeAssigned).Select(x => x.EmployeeName).ToList();
            if (invalidAssignment.Count > 0)
            {
                MessageBox.Show(
                    "Some users cannot be enabled because they do not have an active account or an allowed role:\r\n\r\n" + string.Join("\r\n", invalidAssignment),
                    "Manage IT Assignees",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var invalidEscalation = _items.Where(x => x.IsEscalationEligible && !x.IsAssignmentEligible).Select(x => x.EmployeeName).ToList();
            if (invalidEscalation.Count > 0)
            {
                MessageBox.Show(
                    "Escalation-eligible users must also be assignment-eligible:\r\n\r\n" + string.Join("\r\n", invalidEscalation),
                    "Manage IT Assignees",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_items.Count(x => x.IsDefaultAutoEscalation) > 1)
            {
                MessageBox.Show(
                    "Select only one auto-escalation default.",
                    "Manage IT Assignees",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var escalationSettings = EscalationSettings;
            if (_supportsEscalationSettings)
            {
                if (string.IsNullOrWhiteSpace(escalationSettings.SupervisorPosition) || string.IsNullOrWhiteSpace(escalationSettings.ManagerPosition))
                {
                    MessageBox.Show(
                        "Supervisor and manager positions are required.",
                        "Manage IT Assignees",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (escalationSettings.DaysToManager < escalationSettings.DaysToSupervisor)
                {
                    MessageBox.Show(
                        "Manager escalation days must be greater than or equal to supervisor escalation days.",
                        "Manage IT Assignees",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            DialogResult = true;
            Close();
        }

        private void RefreshEscalationAssigneeOptions()
        {
            if (_escalationAssigneeBox == null || _currentEscalationAssigneeText == null || _escalationAssigneeHelperText == null)
                return;

            var selectedEmpId = _items.FirstOrDefault(x => x.IsDefaultAutoEscalation)?.EmpId;
            var candidates = GetEscalationAssigneeCandidates();

            var options = new List<EscalationOption>
            {
                new EscalationOption { DisplayText = "— None —", Item = null }
            };
            foreach (var c in candidates)
            {
                var posLabel = !string.IsNullOrWhiteSpace(c.Position) ? $"  •  {c.Position}" : string.Empty;
                options.Add(new EscalationOption
                {
                    DisplayText = c.EmployeeName + posLabel,
                    Item = c
                });
            }

            _escalationAssigneeBox.SelectionChanged -= EscalationAssigneeBoxOnSelectionChanged;
            _escalationAssigneeBox.ItemsSource = options;

            if (selectedEmpId.HasValue)
                _escalationAssigneeBox.SelectedItem = options.FirstOrDefault(x => x.Item?.EmpId == selectedEmpId.Value);
            else
                _escalationAssigneeBox.SelectedItem = options.First(); // "None"

            _escalationAssigneeBox.SelectionChanged += EscalationAssigneeBoxOnSelectionChanged;

            if (!_supportsAutoEscalationDefault)
            {
                _escalationAssigneeHelperText.Text = "Escalation assignee selection is unavailable until the CallAutoEscalationAssigneeSettings schema is installed.";
            }
            else if (candidates.Count == 0)
            {
                _escalationAssigneeHelperText.Text = "No escalation candidates are currently available. Mark a user as escalation-ready in the Assignment tab.";
            }
            else
            {
                _escalationAssigneeHelperText.Text = $"Showing {candidates.Count} eligible employee(s). Supervisor-matched candidates are listed first.";
            }
        }

        private List<CallAssignmentEligibilityItem> GetEscalationAssigneeCandidates()
        {
            var supervisorPosition = (_supervisorPositionBox?.SelectedItem as string ?? _escalationSettings.SupervisorPosition ?? string.Empty).Trim();
            IEnumerable<CallAssignmentEligibilityItem> candidates = _items.Where(x => x != null && x.EmpId > 0 && x.CanBeAssigned);

            if (_supportsAssignmentEligibility)
                candidates = candidates.Where(x => x.IsEscalationEligible || x.IsDefaultAutoEscalation);

            var list = candidates.ToList();

            // Sort supervisor-matched candidates first, then others alphabetically
            return list
                .OrderByDescending(x =>
                {
                    var pos = (x.Position ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(supervisorPosition) &&
                        string.Equals(pos, supervisorPosition, StringComparison.OrdinalIgnoreCase))
                        return 2;
                    if (pos.IndexOf("supervisor", StringComparison.OrdinalIgnoreCase) >= 0)
                        return 1;
                    return 0;
                })
                .ThenBy(x => x.EmployeeName ?? string.Empty)
                .ToList();
        }

        private void EscalationAssigneeBoxOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_normalizing || !_supportsAutoEscalationDefault)
                return;

            var option = _escalationAssigneeBox.SelectedItem as EscalationOption;
            SelectEscalationAssignee(option?.Item);
        }

        private void SelectEscalationAssignee(CallAssignmentEligibilityItem selectedItem)
        {
            _normalizing = true;
            try
            {
                foreach (var item in _items)
                    item.IsDefaultAutoEscalation = false;

                if (selectedItem != null)
                {
                    if (selectedItem.CanBeAssigned)
                    {
                        if (_supportsAssignmentEligibility)
                        {
                            selectedItem.IsAssignmentEligible = true;
                            selectedItem.IsEscalationEligible = true;
                        }

                        selectedItem.IsDefaultAutoEscalation = true;
                    }
                }
            }
            finally
            {
                _normalizing = false;
            }

            RefreshSummary();
            RefreshEscalationAssigneeOptions();
        }

        private void GridOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = _grid?.SelectedItem as CallAssignmentEligibilityItem;
            var canLink = selected != null
                && (!selected.HasActiveAccount || !selected.HasAllowedAccess)
                && _onLinkAccount != null;
            if (_linkAccountButton != null)
            {
                _linkAccountButton.IsEnabled = canLink;
                _linkAccountButton.Content = selected != null && selected.HasActiveAccount && !selected.HasAllowedAccess
                    ? "Update Account..."
                    : "Link Account...";
            }
        }

        private async Task OnLinkAccountClickedAsync()
        {
            var selected = _grid?.SelectedItem as CallAssignmentEligibilityItem;
            if (selected == null || selected.CanBeAssigned || _onLinkAccount == null)
                return;

            var result = await _onLinkAccount(selected);
            if (result)
            {
                DialogResult = false;
                Close();
            }
        }

        private static ComboBox CreateDayComboBox(int selectedDay)
        {
            var combo = new ComboBox
            {
                MinWidth = 220,
                Height = 36,
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 12,
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1),
                ItemsSource = Enumerable.Range(1, 30).ToList()
            };
            combo.SelectedItem = Math.Max(1, selectedDay);
            return combo;
        }

        private class EscalationOption
        {
            public string DisplayText { get; set; }
            public CallAssignmentEligibilityItem Item { get; set; }
        }

        private static ComboBox CreatePositionComboBox(List<string> positions, string selectedValue)
        {
            var combo = new ComboBox
            {
                MinWidth = 220,
                Height = 36,
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 12,
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1),
                Foreground = BrushFromRgb(15, 23, 42)
            };
            foreach (var pos in positions)
                combo.Items.Add(pos);
            var sel = (selectedValue ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(sel))
            {
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    if (string.Equals(combo.Items[i] as string, sel, StringComparison.OrdinalIgnoreCase))
                    {
                        combo.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (combo.SelectedIndex < 0 && combo.Items.Count > 0)
                combo.SelectedIndex = 0;
            return combo;
        }

        private static TextBox CreateSettingsTextBox(string text)
        {
            return new TextBox
            {
                Text = text ?? string.Empty,
                MinWidth = 220,
                Height = 36,
                Padding = new Thickness(12, 8, 12, 8),
                FontSize = 12,
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1),
                Foreground = BrushFromRgb(15, 23, 42)
            };
        }

        private static TextBlock CreateSectionTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(30, 41, 59),
                Margin = new Thickness(0, 0, 0, 14)
            };
        }

        private static StackPanel CreateFieldHost(string label, UIElement element, Thickness? margin = null)
        {
            var stack = new StackPanel { Margin = margin ?? new Thickness(0) };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105),
                Margin = new Thickness(0, 0, 0, 8)
            });
            stack.Children.Add(element);
            return stack;
        }

        private static int GetSelectedDay(ComboBox combo, int fallback)
        {
            return combo?.SelectedItem is int day ? day : Math.Max(1, fallback);
        }

        private static Border CreateCard()
        {
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                SnapsToDevicePixels = true
            };
        }

        private static Border CreateBadge(string text, Color background, Color foreground, Thickness? margin = null)
        {
            return new Border
            {
                Background = new SolidColorBrush(background),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = margin ?? new Thickness(0),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(foreground)
                }
            };
        }

        private static Button CreateActionButton(string content, bool primary)
        {
            return new Button
            {
                Content = content,
                MinWidth = 126,
                Height = 38,
                Padding = new Thickness(16, 0, 16, 0),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                Background = primary ? BrushFromRgb(37, 99, 235) : Brushes.White,
                Foreground = primary ? Brushes.White : BrushFromRgb(71, 85, 105),
                BorderBrush = primary ? BrushFromRgb(37, 99, 235) : new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1)
            };
        }

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
        {
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
    }
}
