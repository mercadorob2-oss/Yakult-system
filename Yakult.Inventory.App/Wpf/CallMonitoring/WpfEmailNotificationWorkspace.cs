using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Yakult.Inventory.App.Forms.CallMonitoring;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using System.Windows.Forms.Integration;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed class WpfEmailNotificationWorkspace : UserControl
    {
        private readonly ICallMonitoringRepository _repository;
        private readonly CallEmailNotificationService _emailService;

        private readonly TextBlock _smtpStatusText;
        private readonly TextBlock _rulesStatusText;
        private readonly TextBlock _recipientsCountText;
        private readonly TextBlock _logCountText;
        private readonly TextBlock _statusText;
        private readonly TextBlock _buildStampText;
        private readonly TabControl _tabControl;

        private readonly WinForms.TextBox _smtpServerBox;
        private readonly WinForms.TextBox _smtpPortBox;
        private readonly CheckBox _useSslCheck;
        private readonly WinForms.TextBox _smtpUserBox;
        private readonly WinForms.TextBox _smtpPasswordBox;
        private readonly WinForms.TextBox _fromNameBox;
        private readonly WinForms.TextBox _fromEmailBox;
        private readonly Button _saveSmtpButton;
        private readonly Button _smtpTestButton;

        private readonly CheckBox _notifyNewCheck;
        private readonly CheckBox _notifyStatusCheck;
        private readonly CheckBox _notifyEscalationCheck;
        private readonly CheckBox _notifyReminderCheck;
        private readonly TextBox _groupEmailBox;
        private readonly TextBox _escalationEmailBox;
        private readonly TextBox _reminderDaysBox;
        private readonly Button _saveRulesButton;

        private readonly TabControl _templateTabControl;
        private readonly Dictionary<string, TextBox> _templateSubjectBoxes = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TextBox> _templateBodyBoxes = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);

        private readonly TextBox _recipientSearchBox;
        private readonly DataGrid _departmentGrid;
        private readonly DataGrid _branchGrid;
        private readonly DataGrid _profileGrid;

        private readonly TextBox _logSearchBox;
        private readonly ComboBox _logTypeCombo;
        private readonly Button _logPrevButton;
        private readonly Button _logNextButton;
        private readonly TextBlock _logPagerText;
        private readonly DataGrid _logGrid;

        private CallEmailSettingsItem _loadedSettings;
        private List<CallDepartmentSmtpProfileRow> _departmentRows = new List<CallDepartmentSmtpProfileRow>();
        private List<CallBranchSmtpProfileRow> _branchRows = new List<CallBranchSmtpProfileRow>();
        private List<CallSmtpProfileItem> _profileRows = new List<CallSmtpProfileItem>();
        private List<CallEmailLogItem> _logRows = new List<CallEmailLogItem>();
        private int _logPageIndex = 1;
        private bool _logHasNext;
        private int _logTotalCount;
        private const int EmailLogPageSize = 12;

        public WpfEmailNotificationWorkspace(ICallMonitoringRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _emailService = new CallEmailNotificationService(_repository);

            Resources.MergedDictionaries.Add(WpfThemeResources.GetScrollBarStyle());
            Background = new SolidColorBrush(Color.FromRgb(241, 244, 247));

            var root = new Grid { Margin = new Thickness(28, 24, 28, 28) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            root.Children.Add(BuildHero());

            var shortcutGrid = new Grid { Margin = new Thickness(0, 18, 0, 18) };
            for (var i = 0; i < 4; i++)
                shortcutGrid.ColumnDefinitions.Add(new ColumnDefinition());

            _smtpStatusText = CreateMetricText("Checking");
            _rulesStatusText = CreateMetricText("Checking");
            _recipientsCountText = CreateMetricText("-");
            _logCountText = CreateMetricText("-");

            AddShortcutCard(shortcutGrid, 0, "Setup", "SMTP sender and notification rules", _smtpStatusText, () => NavigateTo(WpfEmailNotificationDeepLinkTarget.SetupSmtp));
            AddShortcutCard(shortcutGrid, 1, "Templates", "New ticket, updates, reminders", _rulesStatusText, () => NavigateTo(WpfEmailNotificationDeepLinkTarget.TemplatesNewTicket));
            AddShortcutCard(shortcutGrid, 2, "Recipients", "Department and branch routing", _recipientsCountText, () => NavigateTo(WpfEmailNotificationDeepLinkTarget.DepartmentRecipients));
            AddShortcutCard(shortcutGrid, 3, "Email Log", "Delivery history and resend review", _logCountText, () => NavigateTo(WpfEmailNotificationDeepLinkTarget.EmailLog));
            Grid.SetRow(shortcutGrid, 1);
            root.Children.Add(shortcutGrid);

            var workspaceCard = WpfThemeResources.CreateGlassCard(18);
            workspaceCard.CornerRadius = new CornerRadius(22);
            Grid.SetRow(workspaceCard, 2);
            root.Children.Add(workspaceCard);

            var workspaceGrid = new Grid();
            workspaceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            workspaceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            workspaceCard.Child = workspaceGrid;

            var topStrip = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            _statusText = new TextBlock
            {
                Text = "Loading email workspace...",
                FontSize = 12,
                Foreground = BrushFromRgb(100, 116, 139),
                VerticalAlignment = VerticalAlignment.Center
            };
            topStrip.Children.Add(_statusText);
            _buildStampText = new TextBlock
            {
                Text = BuildStamp(),
                FontSize = 11,
                Foreground = BrushFromRgb(148, 163, 184),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };
            DockPanel.SetDock(_buildStampText, Dock.Right);
            topStrip.Children.Add(_buildStampText);
            Grid.SetRow(topStrip, 0);
            workspaceGrid.Children.Add(topStrip);

            _tabControl = new TabControl
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            _tabControl.ItemContainerStyle = BuildTabItemStyle();
            Grid.SetRow(_tabControl, 1);
            workspaceGrid.Children.Add(_tabControl);

            var setupGrid = new Grid { Margin = new Thickness(6, 10, 6, 6) };
            setupGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            setupGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            setupGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            setupGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var smtpCard = CreateSectionCard("SMTP Settings", "Server, authentication, and sender identity.");
            var smtpPanel = new Grid();
            smtpPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            smtpPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            smtpPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            smtpPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            smtpPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            smtpPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            smtpPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            smtpPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            smtpPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Native WinForms boxes via WindowsFormsHost (last-known-good input
            // path for these fields): WinForms editing does not depend on the
            // WPF text-composition (WM_CHAR -> TextInput) pipeline.
            _smtpServerBox = CreateNativeTextBox();
            _smtpPortBox = CreateNativeTextBox("587");
            _useSslCheck = new CheckBox { Content = "Use SSL", Foreground = BrushFromRgb(31, 41, 55), FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
            _smtpUserBox = CreateNativeTextBox();
            _smtpPasswordBox = CreateNativeTextBox(isPassword: true);
            _fromNameBox = CreateNativeTextBox();
            _fromEmailBox = CreateNativeTextBox();
            var smtpServerHost = CreateNativeTextBoxHost(_smtpServerBox);
            var smtpPortHost = CreateNativeTextBoxHost(_smtpPortBox);
            var smtpUserHost = CreateNativeTextBoxHost(_smtpUserBox);
            var smtpPasswordHost = CreateNativeTextBoxHost(_smtpPasswordBox);
            var fromNameHost = CreateNativeTextBoxHost(_fromNameBox);
            var fromEmailHost = CreateNativeTextBoxHost(_fromEmailBox);
            AddField(smtpPanel, 0, "SMTP Server", smtpServerHost);
            AddField(smtpPanel, 1, "Port", smtpPortHost);
            AddField(smtpPanel, 2, "Security", _useSslCheck);
            AddField(smtpPanel, 3, "Username", smtpUserHost);
            AddField(smtpPanel, 4, "Password", smtpPasswordHost);
            AddField(smtpPanel, 5, "From Name", fromNameHost);
            AddField(smtpPanel, 6, "From Email", fromEmailHost);
            AttachSetupInputDiagnostics();

            var smtpActions = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
            _saveSmtpButton = CreateRoundedButton("Save SMTP", BrushFromRgb(37, 99, 235));
            _saveSmtpButton.Click += async (_, __) => await SaveSmtpSettingsAsync();
            _smtpTestButton = CreateRoundedButton("Run SMTP Test", BrushFromRgb(16, 185, 129));
            _smtpTestButton.Margin = new Thickness(8, 0, 0, 8);
            _smtpTestButton.Click += (_, __) => OpenSmtpTestDialog();
            smtpActions.Children.Add(_saveSmtpButton);
            smtpActions.Children.Add(_smtpTestButton);

            var smtpStack = new StackPanel();
            smtpStack.Children.Add(smtpPanel);
            smtpStack.Children.Add(smtpActions);
            smtpCard.Child = smtpStack;
            Grid.SetRow(smtpCard, 0);
            Grid.SetColumn(smtpCard, 0);
            setupGrid.Children.Add(smtpCard);

            var rulesCard = CreateSectionCard("Notification Rules", "Decide when outbound emails are sent.");
            var rulesPanel = new StackPanel();
            _notifyNewCheck = CreateRuleCheck("Notify on new ticket");
            _notifyStatusCheck = CreateRuleCheck("Notify on status change");
            _notifyEscalationCheck = CreateRuleCheck("Notify on escalation");
            _notifyReminderCheck = CreateRuleCheck("Notify on reminder");
            rulesPanel.Children.Add(_notifyNewCheck);
            rulesPanel.Children.Add(_notifyStatusCheck);
            rulesPanel.Children.Add(_notifyEscalationCheck);
            rulesPanel.Children.Add(_notifyReminderCheck);

            var rulesGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            rulesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rulesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rulesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rulesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            rulesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _groupEmailBox = CreateSetupTextBox();
            _escalationEmailBox = CreateSetupTextBox();
            _reminderDaysBox = CreateSetupTextBox("3");
            AddField(rulesGrid, 0, "Group Email", WrapInFieldBorder(_groupEmailBox));
            AddField(rulesGrid, 1, "Escalation Email", WrapInFieldBorder(_escalationEmailBox));
            AddField(rulesGrid, 2, "Reminder Days", WrapInFieldBorder(_reminderDaysBox));
            rulesPanel.Children.Add(rulesGrid);

            _saveRulesButton = CreateRoundedButton("Save Rules", BrushFromRgb(59, 130, 246), fullWidth: true);
            _saveRulesButton.Margin = new Thickness(0, 16, 0, 0);
            _saveRulesButton.Click += async (_, __) => await SaveNotificationRulesAsync();
            rulesPanel.Children.Add(_saveRulesButton);
            rulesCard.Child = rulesPanel;
            Grid.SetRow(rulesCard, 0);
            Grid.SetColumn(rulesCard, 1);
            rulesCard.Margin = new Thickness(14, 0, 0, 0);
            setupGrid.Children.Add(rulesCard);

            _tabControl.Items.Add(new TabItem { Header = "Setup", Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = setupGrid } });

            _templateTabControl = new TabControl { Margin = new Thickness(6, 10, 6, 6) };
            _templateTabControl.ItemContainerStyle = BuildTabItemStyle();
            AddTemplateTab("NewTicket", "New Ticket");
            AddTemplateTab("Assignment", "Assignment");
            AddTemplateTab("Reassignment", "Reassignment");
            AddTemplateTab("StatusUpdate", "Status Update");
            AddTemplateTab("Escalation", "Escalation");
            AddTemplateTab("Reminder", "Reminder");
            _tabControl.Items.Add(new TabItem { Header = "Templates", Content = _templateTabControl });

            var recipientsRoot = new Grid { Margin = new Thickness(6, 10, 6, 6) };
            recipientsRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            recipientsRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var recipientsTop = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            _recipientSearchBox = CreateTextBox();
            _recipientSearchBox.Width = 280;
            _recipientSearchBox.TextChanged += (_, __) => ApplyRecipientFilters();
            recipientsTop.Children.Add(_recipientSearchBox);
            var recipientActions = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var manageDeptButton = CreatePrimaryButton("Manage Departments", BrushFromRgb(37, 99, 235));
            manageDeptButton.Click += async (_, __) => await ManageDepartmentRecipientsAsync();
            var manageBranchButton = CreatePrimaryButton("Manage Branches", BrushFromRgb(14, 116, 144));
            manageBranchButton.Margin = new Thickness(8, 0, 0, 8);
            manageBranchButton.Click += async (_, __) => await ManageBranchRecipientsAsync();
            var manageProfilesButton = CreatePrimaryButton("Manage Profiles", BrushFromRgb(71, 85, 105));
            manageProfilesButton.Margin = new Thickness(8, 0, 0, 8);
            manageProfilesButton.Click += async (_, __) => await ManageSmtpProfilesAsync();
            recipientActions.Children.Add(manageDeptButton);
            recipientActions.Children.Add(manageBranchButton);
            recipientActions.Children.Add(manageProfilesButton);
            DockPanel.SetDock(recipientActions, Dock.Right);
            recipientsTop.Children.Add(recipientActions);
            Grid.SetRow(recipientsTop, 0);
            recipientsRoot.Children.Add(recipientsTop);

            var recipientTabs = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            recipientTabs.ItemContainerStyle = BuildTabItemStyle();
            _departmentGrid = CreateGrid();
            BuildDepartmentColumns(_departmentGrid);
            _branchGrid = CreateGrid();
            BuildBranchColumns(_branchGrid);
            _profileGrid = CreateGrid();
            BuildProfileColumns(_profileGrid);
            _profileGrid.MouseDoubleClick += async (_, __) => await EditSelectedSmtpProfileAsync();
            recipientTabs.Items.Add(new TabItem { Header = "Departments", Content = BuildTablePanel("Department Routing", "Notify and escalation recipients by department. Delivery uses the single Setup sender.", _departmentGrid) });
            recipientTabs.Items.Add(new TabItem { Header = "Branches", Content = BuildTablePanel("Branch Routing", "Notify and escalation recipients by branch. Delivery uses the single Setup sender.", _branchGrid) });
            recipientTabs.Items.Add(new TabItem { Header = "Profiles", Content = BuildTablePanel("SMTP Profiles", "Reusable sender identities used by department and branch routing.", _profileGrid) });
            Grid.SetRow(recipientTabs, 1);
            recipientsRoot.Children.Add(recipientTabs);
            _tabControl.Items.Add(new TabItem { Header = "Recipients", Content = recipientsRoot });

            var logRoot = new Grid { Margin = new Thickness(6, 10, 6, 6) };
            logRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            logRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var logTop = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var logLeft = new WrapPanel { Orientation = Orientation.Horizontal };
            _logSearchBox = CreateTextBox();
            _logSearchBox.Width = 260;
            _logTypeCombo = CreateComboBox();
            _logTypeCombo.Width = 160;
            _logTypeCombo.Margin = new Thickness(8, 0, 0, 0);
            _logTypeCombo.Items.Add("All");
            _logTypeCombo.Items.Add("New Ticket");
            _logTypeCombo.Items.Add("Assignment");
            _logTypeCombo.Items.Add("Reassignment");
            _logTypeCombo.Items.Add("Status Update");
            _logTypeCombo.Items.Add("Escalation");
            _logTypeCombo.Items.Add("Reminder");
            _logTypeCombo.SelectedIndex = 0;
            var refreshLogButton = CreatePrimaryButton("Refresh", BrushFromRgb(37, 99, 235));
            refreshLogButton.Margin = new Thickness(8, 0, 0, 0);
            refreshLogButton.Click += async (_, __) => await ReloadEmailLogAsync(true);
            var resendButton = CreatePrimaryButton("Resend Selected", BrushFromRgb(16, 185, 129));
            resendButton.Margin = new Thickness(8, 0, 0, 0);
            resendButton.Click += async (_, __) => await ResendSelectedEmailAsync();
            logLeft.Children.Add(_logSearchBox);
            logLeft.Children.Add(_logTypeCombo);
            logLeft.Children.Add(refreshLogButton);
            logLeft.Children.Add(resendButton);

            var pager = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            _logPrevButton = CreatePagerButton("Prev");
            _logPrevButton.Click += async (_, __) => { if (_logPageIndex > 1) { _logPageIndex--; await ReloadEmailLogAsync(false); } };
            _logPagerText = new TextBlock { Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = BrushFromRgb(71, 85, 105), FontWeight = FontWeights.SemiBold };
            _logNextButton = CreatePagerButton("Next");
            _logNextButton.Click += async (_, __) => { if (_logHasNext) { _logPageIndex++; await ReloadEmailLogAsync(false); } };
            pager.Children.Add(_logPrevButton);
            pager.Children.Add(_logPagerText);
            pager.Children.Add(_logNextButton);
            DockPanel.SetDock(pager, Dock.Right);
            logTop.Children.Add(pager);
            logTop.Children.Add(logLeft);

            Grid.SetRow(logTop, 0);
            logRoot.Children.Add(logTop);

            _logGrid = CreateGrid();
            BuildLogColumns(_logGrid);
            _logGrid.MouseDoubleClick += (_, __) => ShowSelectedLogDetails();
            var logTablePanel = BuildTablePanel("Delivery Ledger", "Searchable outbound email history with resend support.", _logGrid);
            Grid.SetRow(logTablePanel, 1);
            logRoot.Children.Add(logTablePanel);
            _tabControl.Items.Add(new TabItem { Header = "Email Log", Content = logRoot });

            Content = root;

            _logSearchBox.TextChanged += (_, __) => DebounceReloadLog();
            _logTypeCombo.SelectionChanged += async (_, __) => await ReloadEmailLogAsync(true);

            Loaded += async (_, __) => await LoadWorkspaceAsync();
        }

        public void NavigateTo(WpfEmailNotificationDeepLinkTarget target, int? deptId = null, int? branchId = null, string emailLogSearch = null)
        {
            switch (target)
            {
                case WpfEmailNotificationDeepLinkTarget.SetupNotificationRules:
                    _tabControl.SelectedIndex = 0;
                    break;
                case WpfEmailNotificationDeepLinkTarget.TemplatesNewTicket:
                    _tabControl.SelectedIndex = 1;
                    _templateTabControl.SelectedIndex = 0;
                    break;
                case WpfEmailNotificationDeepLinkTarget.TemplatesAssignment:
                    _tabControl.SelectedIndex = 1;
                    _templateTabControl.SelectedIndex = 1;
                    break;
                case WpfEmailNotificationDeepLinkTarget.TemplatesReassignment:
                    _tabControl.SelectedIndex = 1;
                    _templateTabControl.SelectedIndex = 2;
                    break;
                case WpfEmailNotificationDeepLinkTarget.TemplatesStatusUpdate:
                    _tabControl.SelectedIndex = 1;
                    _templateTabControl.SelectedIndex = 3;
                    break;
                case WpfEmailNotificationDeepLinkTarget.TemplatesEscalation:
                    _tabControl.SelectedIndex = 1;
                    _templateTabControl.SelectedIndex = 4;
                    break;
                case WpfEmailNotificationDeepLinkTarget.TemplatesReminder:
                    _tabControl.SelectedIndex = 1;
                    _templateTabControl.SelectedIndex = 5;
                    break;
                case WpfEmailNotificationDeepLinkTarget.DepartmentRecipients:
                case WpfEmailNotificationDeepLinkTarget.BranchRecipients:
                case WpfEmailNotificationDeepLinkTarget.SmtpProfiles:
                    _tabControl.SelectedIndex = 2;
                    _recipientSearchBox.Text = deptId.HasValue && deptId.Value > 0
                        ? deptId.Value.ToString()
                        : branchId.HasValue && branchId.Value > 0
                            ? branchId.Value.ToString()
                            : string.Empty;
                    ApplyRecipientFilters();
                    break;
                case WpfEmailNotificationDeepLinkTarget.EmailLog:
                    _tabControl.SelectedIndex = 3;
                    _logSearchBox.Text = (emailLogSearch ?? string.Empty).Trim();
                    _ = ReloadEmailLogAsync(true);
                    break;
                case WpfEmailNotificationDeepLinkTarget.SetupSmtp:
                default:
                    _tabControl.SelectedIndex = 0;
                    break;
            }
        }

        private async Task LoadWorkspaceAsync()
        {
            _statusText.Text = "Loading email workspace...";
            try
            {
                await Task.WhenAll(
                    LoadSetupAsync(),
                    LoadTemplatesAsync(),
                    LoadRecipientsAsync(),
                    ReloadEmailLogAsync(true),
                    RefreshSummaryAsync());
                _statusText.Text = "Email workspace ready.";
            }
            catch (Exception ex)
            {
                _statusText.Text = "Failed to load email workspace.";
                MessageBox.Show(ex.Message, "Email Notifications", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadSetupAsync()
        {
            var settingsTask = _repository.GetEmailSettingsAsync();
            var rulesTask = _repository.GetNotificationRulesAsync();
            await Task.WhenAll(settingsTask, rulesTask);

            _loadedSettings = settingsTask.Result;
            var settings = _loadedSettings ?? new CallEmailSettingsItem { SmtpPort = 587, UseSsl = true };
            _smtpServerBox.Text = settings.SmtpServer ?? string.Empty;
            _smtpPortBox.Text = settings.SmtpPort > 0 ? settings.SmtpPort.ToString() : "587";
            _useSslCheck.IsChecked = settings.UseSsl;
            _smtpUserBox.Text = settings.SmtpUsername ?? string.Empty;
            _smtpPasswordBox.Text = string.Empty;
            _fromNameBox.Text = settings.FromName ?? string.Empty;
            _fromEmailBox.Text = settings.FromEmail ?? string.Empty;

            var rules = rulesTask.Result ?? new CallNotificationRulesItem { ReminderDays = 3 };
            _notifyNewCheck.IsChecked = rules.NotifyOnNewTicket;
            _notifyStatusCheck.IsChecked = rules.NotifyOnStatusChange;
            _notifyEscalationCheck.IsChecked = rules.NotifyOnEscalation;
            _notifyReminderCheck.IsChecked = rules.NotifyOnReminder;
            _groupEmailBox.Text = rules.GroupEmail ?? string.Empty;
            _escalationEmailBox.Text = rules.EscalationEmail ?? string.Empty;
            _reminderDaysBox.Text = Math.Max(0, rules.ReminderDays).ToString();
        }

        private async Task LoadTemplatesAsync()
        {
            var types = new[] { "NewTicket", "Assignment", "Reassignment", "StatusUpdate", "Escalation", "Reminder" };
            foreach (var type in types)
            {
                var template = await _repository.GetEmailTemplateByTypeAsync(type);
                if (_templateSubjectBoxes.TryGetValue(type, out var subject))
                    subject.Text = template?.Subject ?? string.Empty;
                if (_templateBodyBoxes.TryGetValue(type, out var body))
                    body.Text = template?.Body ?? string.Empty;
            }
        }

        private async Task LoadRecipientsAsync()
        {
            var deptTask = _repository.GetDepartmentSmtpProfilesOptionBAsync();
            var branchTask = _repository.GetBranchSmtpProfilesOptionBAsync();
            var profileTask = _repository.GetSmtpProfilesAsync(activeOnly: false);
            await Task.WhenAll(deptTask, branchTask, profileTask);

            _departmentRows = deptTask.Result ?? new List<CallDepartmentSmtpProfileRow>();
            _branchRows = branchTask.Result ?? new List<CallBranchSmtpProfileRow>();
            _profileRows = profileTask.Result ?? new List<CallSmtpProfileItem>();
            ApplyRecipientFilters();
        }

        private async Task SaveSmtpSettingsAsync()
        {
            var host = (_smtpServerBox.Text ?? string.Empty).Trim();
            var username = (_smtpUserBox.Text ?? string.Empty).Trim();
            var fromName = (_fromNameBox.Text ?? string.Empty).Trim();
            var fromEmail = (_fromEmailBox.Text ?? string.Empty).Trim();
            if (!int.TryParse((_smtpPortBox.Text ?? string.Empty).Trim(), out var port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("Port must be a valid number (1-65535).", "Email Notifications", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(host))
            {
                MessageBox.Show("SMTP server is required.", "Email Notifications", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(fromEmail))
                fromEmail = username;

            if (!EmailAddressValidator.TryNormalize(fromEmail, out fromEmail))
            {
                MessageBox.Show("From Email must be a valid email address (for example, robpogi54@gmail.com).", "Email Notifications", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var passwordText = _smtpPasswordBox.Text ?? string.Empty;
            byte[] passwordEnc = null;
            if (!string.IsNullOrWhiteSpace(passwordText))
                passwordEnc = SecretProtector.ProtectString(passwordText);
            else if (_loadedSettings?.SmtpPasswordEnc != null && _loadedSettings.SmtpPasswordEnc.Length > 0)
                passwordEnc = _loadedSettings.SmtpPasswordEnc;

            _saveSmtpButton.IsEnabled = false;
            try
            {
                await _repository.SaveEmailSettingsAsync(new CallEmailSettingsItem
                {
                    SmtpServer = host,
                    SmtpPort = port,
                    UseSsl = _useSslCheck.IsChecked == true,
                    SmtpUsername = username,
                    SmtpPasswordEnc = passwordEnc,
                    FromName = fromName,
                    FromEmail = fromEmail,
                    UpdatedByUserId = AppSession.IsLoggedIn ? (int?)AppSession.CurrentUserId : null
                });
                await LoadSetupAsync();
                await RefreshSummaryAsync();
                _statusText.Text = "SMTP settings saved.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _saveSmtpButton.IsEnabled = true;
            }
        }

        private async Task SaveNotificationRulesAsync()
        {
            if (!int.TryParse((_reminderDaysBox.Text ?? string.Empty).Trim(), out var reminderDays) || reminderDays < 0)
            {
                MessageBox.Show("Reminder days must be a non-negative whole number.", "Email Notifications", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _saveRulesButton.IsEnabled = false;
            try
            {
                await _repository.SaveNotificationRulesAsync(new CallNotificationRulesItem
                {
                    NotifyOnNewTicket = _notifyNewCheck.IsChecked == true,
                    NotifyOnStatusChange = _notifyStatusCheck.IsChecked == true,
                    NotifyOnEscalation = _notifyEscalationCheck.IsChecked == true,
                    NotifyOnReminder = _notifyReminderCheck.IsChecked == true,
                    GroupEmail = (_groupEmailBox.Text ?? string.Empty).Trim(),
                    EscalationEmail = (_escalationEmailBox.Text ?? string.Empty).Trim(),
                    ReminderDays = reminderDays,
                    UpdatedByUserId = AppSession.IsLoggedIn ? (int?)AppSession.CurrentUserId : null
                });
                await RefreshSummaryAsync();
                _statusText.Text = "Notification rules saved.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _saveRulesButton.IsEnabled = true;
            }
        }

        private async Task SaveTemplateAsync(string type)
        {
            if (!_templateSubjectBoxes.TryGetValue(type, out var subject) || !_templateBodyBoxes.TryGetValue(type, out var body))
                return;

            try
            {
                await _repository.SaveEmailTemplateAsync(new CallEmailTemplateItem
                {
                    TemplateType = type,
                    Subject = subject.Text ?? string.Empty,
                    Body = body.Text ?? string.Empty,
                    IsActive = true,
                    UpdatedByUserId = AppSession.IsLoggedIn ? (int?)AppSession.CurrentUserId : null
                });
                await RefreshSummaryAsync();
                _statusText.Text = $"{type} template saved.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Save Template Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreviewTemplate(string type)
        {
            if (!_templateSubjectBoxes.TryGetValue(type, out var subject) || !_templateBodyBoxes.TryGetValue(type, out var body))
                return;

            var win = new Window
            {
                Title = $"{type} Template Preview",
                Width = 920,
                Height = 640,
                MinWidth = 760,
                MinHeight = 520,
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = BrushFromRgb(245, 247, 250),
                Content = BuildTemplatePreviewContent(subject.Text ?? string.Empty, body.Text ?? string.Empty)
            };
            win.ShowDialog();
        }

        private UIElement BuildTemplatePreviewContent(string subject, string body)
        {
            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new StackPanel();
            header.Children.Add(new TextBlock { Text = "Rendered Template Preview", FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(15, 23, 42) });
            header.Children.Add(new TextBlock { Text = "This preview shows the raw subject and body currently saved in the WPF editor.", Margin = new Thickness(0, 5, 0, 0), Foreground = BrushFromRgb(100, 116, 139) });
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var split = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            split.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            split.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var subj = CreateTextBox(subject);
            subj.IsReadOnly = true;
            subj.MinHeight = 36;
            Grid.SetRow(subj, 0);
            split.Children.Add(subj);
            var bodyBox = CreateTextBox(body);
            bodyBox.IsReadOnly = true;
            bodyBox.AcceptsReturn = true;
            bodyBox.TextWrapping = TextWrapping.Wrap;
            bodyBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            bodyBox.Margin = new Thickness(0, 12, 0, 0);
            Grid.SetRow(bodyBox, 1);
            split.Children.Add(bodyBox);
            Grid.SetRow(split, 1);
            root.Children.Add(split);
            return root;
        }

        private void ApplyRecipientFilters()
        {
            var q = (_recipientSearchBox.Text ?? string.Empty).Trim();
            var hasQuery = !string.IsNullOrWhiteSpace(q);

            _departmentGrid.ItemsSource = (_departmentRows ?? new List<CallDepartmentSmtpProfileRow>())
                .Where(x => !hasQuery || ContainsAny(q, x.DepartmentName, x.RecipientEmails, x.EscalationEmails, x.DeptId.ToString()))
                .OrderBy(x => x.DepartmentName)
                .ToList();

            _branchGrid.ItemsSource = (_branchRows ?? new List<CallBranchSmtpProfileRow>())
                .Where(x => !hasQuery || ContainsAny(q, x.BranchName, x.CompanyName, x.DepartmentName, x.RecipientEmails, x.EscalationEmails, x.BranchId.ToString()))
                .OrderBy(x => x.BranchName)
                .ToList();

            _profileGrid.ItemsSource = (_profileRows ?? new List<CallSmtpProfileItem>())
                .Where(x => !hasQuery || ContainsAny(q, x.ProfileName, x.SmtpServer, x.SmtpUsername, x.FromName, x.FromEmail, x.ProfileId.ToString()))
                .OrderBy(x => x.ProfileName)
                .ToList();
        }

        private async Task ReloadEmailLogAsync(bool resetPage)
        {
            try
            {
                if (resetPage)
                    _logPageIndex = 1;

                var searchText = (_logSearchBox.Text ?? string.Empty).Trim();
                var type = _logTypeCombo.SelectedItem == null || string.Equals((_logTypeCombo.SelectedItem ?? "All").ToString(), "All", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : (_logTypeCombo.SelectedItem ?? string.Empty).ToString();

                var rowsTask = _repository.GetEmailLogPageAsync(searchText, type, _logPageIndex, EmailLogPageSize + 1);
                var countTask = _repository.GetEmailLogCountAsync(searchText, type);
                await Task.WhenAll(rowsTask, countTask);

                var rows = rowsTask.Result ?? new List<CallEmailLogItem>();
                _logHasNext = rows.Count > EmailLogPageSize;
                if (_logHasNext)
                    rows = rows.Take(EmailLogPageSize).ToList();
                _logRows = rows;
                _logTotalCount = countTask.Result;
                _logGrid.ItemsSource = _logRows;
                UpdateLogPager();
                await RefreshSummaryAsync();
            }
            catch (Exception ex)
            {
                _statusText.Text = "Failed to load email log.";
                MessageBox.Show(ex.Message, "Email Log", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ResendSelectedEmailAsync()
        {
            if (!(_logGrid.SelectedItem is CallEmailLogItem item) || !item.TicketId.HasValue || item.TicketId.Value <= 0)
            {
                MessageBox.Show("Select a log row with a valid ticket first.", "Email Log", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Resend '{item.EmailType}' for ticket {item.TicketId.Value}?", "Resend Email", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                var result = await _emailService.ResendEmailAsync(item.TicketId.Value, item.EmailType, AppSession.IsLoggedIn ? (int?)AppSession.CurrentUserId : null);
                _statusText.Text = result.SentSuccessfully ? "Email resent successfully." : "Email resend completed with warning.";
                await ReloadEmailLogAsync(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Resend Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowSelectedLogDetails()
        {
            if (!(_logGrid.SelectedItem is CallEmailLogItem item))
                return;

            var win = new Window
            {
                Title = "Email Log Details",
                Width = 760,
                Height = 520,
                MinWidth = 620,
                MinHeight = 440,
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = BrushFromRgb(245, 247, 250)
            };

            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(new TextBlock
            {
                Text = $"{(item.EmailType ?? "-")} • {(item.Status ?? "-")}",
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42)
            });

            var details = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            for (var i = 0; i < 6; i++) details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AddField(details, 0, "Ticket", CreateReadOnlyValue(item.TicketId.HasValue ? item.TicketId.Value.ToString() : "-"));
            AddField(details, 1, "Recipient", CreateReadOnlyValue(item.Recipient ?? "-"));
            AddField(details, 2, "Subject", CreateReadOnlyValue(item.Subject ?? "-"));
            AddField(details, 3, "Branch", CreateReadOnlyValue(item.Branch ?? "-"));
            AddField(details, 4, "Sent", CreateReadOnlyValue(item.DateSent.ToString("yyyy-MM-dd HH:mm")));
            var errorBox = CreateTextBox(item.ErrorMessage ?? string.Empty);
            errorBox.AcceptsReturn = true;
            errorBox.TextWrapping = TextWrapping.Wrap;
            errorBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            errorBox.IsReadOnly = true;
            errorBox.MinHeight = 180;
            AddField(details, 5, "Error", errorBox);
            Grid.SetRow(details, 1);
            root.Children.Add(details);
            win.Content = root;
            win.ShowDialog();
        }

        private async Task RefreshSummaryAsync()
        {
            try
            {
                var settingsTask = _repository.GetEmailSettingsAsync();
                var rulesTask = _repository.GetNotificationRulesAsync();
                var deptRowsTask = _repository.GetDepartmentSmtpProfilesOptionBAsync();
                var branchRowsTask = _repository.GetBranchSmtpProfilesOptionBAsync();
                var logCountTask = _repository.GetEmailLogCountAsync(null, null);

                await Task.WhenAll(settingsTask, rulesTask, deptRowsTask, branchRowsTask, logCountTask);

                var settings = settingsTask.Result;
                var rules = rulesTask.Result;
                var configuredRecipients = (deptRowsTask.Result ?? new List<CallDepartmentSmtpProfileRow>()).Count(x => !string.IsNullOrWhiteSpace(x.RecipientEmails))
                    + (branchRowsTask.Result ?? new List<CallBranchSmtpProfileRow>()).Count(x => !string.IsNullOrWhiteSpace(x.RecipientEmails));

                _smtpStatusText.Text = settings == null || string.IsNullOrWhiteSpace(settings.SmtpServer)
                    ? "SMTP not configured"
                    : $"{settings.SmtpServer}:{settings.SmtpPort}";

                _rulesStatusText.Text = rules == null
                    ? "Rules not configured"
                    : $"{CountEnabledRules(rules)} rule(s) enabled";

                _recipientsCountText.Text = $"{configuredRecipients} recipient override(s)";
                _logCountText.Text = $"{logCountTask.Result:N0} logged email(s)";
            }
            catch
            {
                _smtpStatusText.Text = "Summary unavailable";
                _rulesStatusText.Text = "Open section to review";
                _recipientsCountText.Text = "-";
                _logCountText.Text = "-";
            }
        }

        private void DebounceReloadLog()
        {
            _ = ReloadEmailLogAsync(true);
        }

        private void UpdateLogPager()
        {
            var totalPages = _logTotalCount <= 0 ? 1 : (int)Math.Ceiling(_logTotalCount / (double)EmailLogPageSize);
            _logPagerText.Text = $"Page {_logPageIndex} of {Math.Max(1, totalPages)} ({Math.Max(0, _logTotalCount)})";
            _logPrevButton.IsEnabled = _logPageIndex > 1;
            _logNextButton.IsEnabled = _logHasNext;
        }

        private void OpenSmtpTestDialog()
        {
            var dlg = new WpfSmtpTestDialog(_repository, null) { Owner = Window.GetWindow(this) };
            dlg.ShowDialog();
        }

        private async Task ManageDepartmentRecipientsAsync()
        {
            List<LookupItem> departments;
            try
            {
                departments = await _repository.GetDepartmentsAsync() ?? new List<LookupItem>();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Department Recipients", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var selected = _departmentGrid?.SelectedItem as CallDepartmentSmtpProfileRow;
            int? preselected = selected != null && selected.DeptId > 0 ? (int?)selected.DeptId : null;
            string notify = null;
            string escalate = null;
            if (preselected.HasValue)
            {
                try
                {
                    var existing = await _repository.GetDepartmentNotificationRecipientAsync(preselected.Value);
                    notify = existing?.RecipientEmails;
                    escalate = existing?.EscalationEmails;
                }
                catch
                {
                }
            }

            var dlg = new WpfRecipientLinkDialog(departments, "Department", preselected, notify, escalate)
            {
                Owner = Window.GetWindow(this)
            };
            if (dlg.ShowDialog() != true)
                return;

            try
            {
                await _repository.UpsertDepartmentNotificationRecipientAsync(
                    dlg.SelectedTargetId,
                    dlg.RecipientEmails,
                    dlg.EscalationEmails,
                    AppSession.IsLoggedIn ? (int?)AppSession.CurrentUserId : null);
                await LoadRecipientsAsync();
                _statusText.Text = "Department recipients saved.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Department Recipients", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ManageBranchRecipientsAsync()
        {
            List<LookupItem> branches;
            try
            {
                branches = await _repository.GetBranchesAsync() ?? new List<LookupItem>();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Branch Recipients", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var selected = _branchGrid?.SelectedItem as CallBranchSmtpProfileRow;
            int? preselected = selected != null && selected.BranchId > 0 ? (int?)selected.BranchId : null;
            string notify = null;
            string escalate = null;
            if (preselected.HasValue)
            {
                try
                {
                    var existing = await _repository.GetBranchNotificationRecipientAsync(preselected.Value);
                    notify = existing?.RecipientEmails;
                    escalate = existing?.EscalationEmails;
                }
                catch
                {
                }
            }

            var dlg = new WpfRecipientLinkDialog(branches, "Branch", preselected, notify, escalate)
            {
                Owner = Window.GetWindow(this)
            };
            if (dlg.ShowDialog() != true)
                return;

            try
            {
                await _repository.UpsertBranchNotificationRecipientAsync(
                    dlg.SelectedTargetId,
                    dlg.RecipientEmails,
                    dlg.EscalationEmails,
                    AppSession.IsLoggedIn ? (int?)AppSession.CurrentUserId : null);
                await LoadRecipientsAsync();
                _statusText.Text = "Branch recipients saved.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Branch Recipients", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ManageSmtpProfilesAsync()
        {
            await EditSmtpProfileAsync(null);
        }

        private async Task EditSelectedSmtpProfileAsync()
        {
            var selected = _profileGrid?.SelectedItem as CallSmtpProfileItem;
            if (selected == null || selected.ProfileId <= 0)
            {
                MessageBox.Show("Select a profile in the Profiles tab first.", "SMTP Profiles", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            CallSmtpProfileItem fresh = null;
            try
            {
                fresh = await _repository.GetCallSmtpProfileByIdAsync(selected.ProfileId);
            }
            catch
            {
            }
            await EditSmtpProfileAsync(fresh ?? selected);
        }

        private async Task EditSmtpProfileAsync(CallSmtpProfileItem existing)
        {
            List<LookupItem> departments;
            List<LookupItem> branches;
            try
            {
                var deptTask = _repository.GetDepartmentsAsync();
                var branchTask = _repository.GetBranchesAsync();
                await Task.WhenAll(deptTask, branchTask);
                departments = deptTask.Result ?? new List<LookupItem>();
                branches = branchTask.Result ?? new List<LookupItem>();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SMTP Profiles", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dlg = new WpfSmtpProfileDialog(
                _repository,
                departments,
                branches,
                existing,
                AppSession.IsLoggedIn ? (int?)AppSession.CurrentUserId : null)
            {
                Owner = Window.GetWindow(this)
            };
            if (dlg.ShowDialog() != true || !dlg.Saved)
                return;

            await LoadRecipientsAsync();
            _statusText.Text = "SMTP profile saved.";
        }

        private static string BuildStamp()
        {
            // Proves which binary is on screen (feature tag + exe write time).
            string when = "?";
            try
            {
                var path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                    when = System.IO.File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm");
            }
            catch
            {
            }
            return $"Build {when} • profiles+ping";
        }

        // TEMP-DIAG: input-path instrumentation for the Setup-fields-typing
        // investigation. Logs only event names + text lengths (never content).
        // KeyPress on a WinForms box proves WM_CHAR reached the control, which
        // isolates WPF text-composition vs upstream starvation. Remove once
        // root cause is confirmed.
        private void AttachSetupInputDiagnostics()
        {
            AttachNativeBox("SmtpServer", _smtpServerBox);
            AttachNativeBox("Port", _smtpPortBox);
            AttachNativeBox("Username", _smtpUserBox);
            AttachNativeBox("Password", _smtpPasswordBox);
            AttachNativeBox("FromName", _fromNameBox);
            AttachNativeBox("FromEmail", _fromEmailBox);
        }

        private static void AttachNativeBox(string name, WinForms.TextBox box)
        {
            if (box == null) return;
            box.GotFocus += (_, __) => Logger.LogInfo($"[SetupDiag] {name} GotFocus len={box.Text?.Length ?? 0}");
            box.LostFocus += (_, __) => Logger.LogInfo($"[SetupDiag] {name} LostFocus len={box.Text?.Length ?? 0}");
            box.KeyDown += (_, e) => Logger.LogInfo($"[SetupDiag] {name} KeyDown key={e.KeyCode} len={box.Text?.Length ?? 0}");
            box.KeyPress += (_, e) => Logger.LogInfo($"[SetupDiag] {name} KeyPress len={box.Text?.Length ?? 0}");
            box.TextChanged += (_, __) => Logger.LogInfo($"[SetupDiag] {name} TextChanged len={box.Text?.Length ?? 0}");
        }

        private void AddTemplateTab(string type, string header)
        {
            var layout = new Grid { Margin = new Thickness(6, 10, 6, 6) };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            var saveButton = CreatePrimaryButton("Save Template", BrushFromRgb(37, 99, 235));
            saveButton.Click += async (_, __) => await SaveTemplateAsync(type);
            var previewButton = CreatePrimaryButton("Preview", BrushFromRgb(71, 85, 105));
            previewButton.Margin = new Thickness(8, 0, 0, 0);
            previewButton.Click += (_, __) => PreviewTemplate(type);
            actions.Children.Add(saveButton);
            actions.Children.Add(previewButton);
            Grid.SetRow(actions, 0);
            layout.Children.Add(actions);

            var subjectBox = CreateTextBox();
            subjectBox.MinHeight = 36;
            Grid.SetRow(subjectBox, 1);
            layout.Children.Add(subjectBox);

            var bodyBox = CreateTextBox();
            bodyBox.Margin = new Thickness(0, 12, 0, 0);
            bodyBox.AcceptsReturn = true;
            bodyBox.TextWrapping = TextWrapping.Wrap;
            bodyBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            bodyBox.MinHeight = 360;
            Grid.SetRow(bodyBox, 2);
            layout.Children.Add(bodyBox);

            _templateSubjectBoxes[type] = subjectBox;
            _templateBodyBoxes[type] = bodyBox;
            _templateTabControl.Items.Add(new TabItem { Header = header, Content = layout });
        }

        private static Border BuildHero()
        {
            var card = new Border
            {
                Background = new LinearGradientBrush(Color.FromRgb(15, 23, 42), Color.FromRgb(31, 90, 96), new Point(0, 0), new Point(1, 1)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(28),
                Padding = new Thickness(26, 24, 26, 24),
                Effect = new DropShadowEffect { BlurRadius = 24, Color = Color.FromArgb(34, 15, 23, 42), ShadowDepth = 0, Opacity = 0.25 }
            };

            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            card.Child = layout;

            var textStack = new StackPanel();
            textStack.Children.Add(new TextBlock { Text = "Email Notifications", FontSize = 30, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            textStack.Children.Add(new TextBlock
            {
                Text = "Native WPF workspace for SMTP setup, templates, recipients, and delivery logs. Legacy forms remain only for advanced maintenance fallback.",
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(204, 251, 241)),
                TextWrapping = TextWrapping.Wrap
            });
            layout.Children.Add(textStack);

            var chips = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            chips.Children.Add(CreateHeroChip("Native WPF", BrushFromRgb(16, 185, 129)));
            chips.Children.Add(CreateHeroChip("SMTP + Templates", BrushFromRgb(59, 130, 246)));
            chips.Children.Add(CreateHeroChip("Recipients + Logs", BrushFromRgb(245, 158, 11)));
            Grid.SetColumn(chips, 1);
            layout.Children.Add(chips);
            return card;
        }

        private void AddShortcutCard(Grid grid, int column, string title, string subtitle, TextBlock metric, Action click)
        {
            var card = WpfThemeResources.CreateGlassCard(18);
            card.Margin = new Thickness(column == 0 ? 0 : 12, 0, 0, 0);
            card.Cursor = Cursors.Hand;
            card.MouseLeftButtonUp += (_, __) => click();
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(31, 41, 55) });
            stack.Children.Add(new TextBlock { Text = subtitle, Margin = new Thickness(0, 5, 0, 12), FontSize = 12, Foreground = BrushFromRgb(100, 116, 139), TextWrapping = TextWrapping.Wrap });
            stack.Children.Add(metric);
            card.Child = stack;
            Grid.SetColumn(card, column);
            grid.Children.Add(card);
        }

        private static Border CreateSectionCard(string title, string subtitle)
        {
            var card = WpfThemeResources.CreateGlassCard(18);
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(15, 23, 42) });
            stack.Children.Add(new TextBlock { Text = subtitle, Margin = new Thickness(0, 5, 0, 16), Foreground = BrushFromRgb(100, 116, 139), TextWrapping = TextWrapping.Wrap });
            card.Child = stack;
            return card;
        }

        private static void AddField(Grid grid, int row, string label, FrameworkElement control)
        {
            var labelBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(71, 85, 105),
                Margin = new Thickness(0, row == 0 ? 0 : 10, 12, 0)
            };
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            control.Margin = new Thickness(0, row == 0 ? 0 : 10, 0, 0);
            Grid.SetRow(control, row);
            Grid.SetColumn(control, 1);
            grid.Children.Add(control);
        }

        private static TextBox CreateTextBox(string text = "")
        {
            return new TextBox
            {
                Text = text,
                MinHeight = 32,
                Padding = new Thickness(8, 6, 8, 6),
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private static WinForms.TextBox CreateNativeTextBox(string text = "", bool isPassword = false)
        {
            return new WinForms.TextBox
            {
                Text = text,
                Dock = WinForms.DockStyle.Fill,
                BorderStyle = WinForms.BorderStyle.FixedSingle,
                BackColor = System.Drawing.Color.White,
                ForeColor = System.Drawing.Color.FromArgb(31, 41, 55),
                Font = new System.Drawing.Font("Segoe UI", 10F),
                Margin = new WinForms.Padding(0),
                UseSystemPasswordChar = isPassword
            };
        }

        private static WindowsFormsHost CreateNativeTextBoxHost(WinForms.TextBox textBox)
        {
            return new WindowsFormsHost
            {
                Child = textBox,
                MinHeight = 32,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
        }

        private static TextBox CreateSetupTextBox(string text = "")
        {
            return new TextBox
            {
                Text = text,
                MinHeight = 36,
                Padding = new Thickness(10, 8, 10, 8),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = BrushFromRgb(15, 23, 42),
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private static PasswordBox CreateSetupPasswordBox()
        {
            return new PasswordBox
            {
                MinHeight = 36,
                Padding = new Thickness(10, 8, 10, 8),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = BrushFromRgb(15, 23, 42),
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private static Border WrapInFieldBorder(FrameworkElement inner)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(9),
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                SnapsToDevicePixels = true,
                Child = inner
            };
        }

        private static Button CreateRoundedButton(string text, Brush background, bool fullWidth = false)
        {
            var btn = new Button
            {
                Content = text,
                Padding = new Thickness(16, 10, 16, 10),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = background,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                MinWidth = 120
            };
            if (fullWidth)
            {
                btn.HorizontalAlignment = HorizontalAlignment.Stretch;
                btn.MinWidth = 0;
            }
            btn.SetValue(Control.TemplateProperty, CreateRoundedButtonTemplate());
            try { btn.Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 1, Opacity = 0.12, Direction = 270, Color = Colors.Black }; } catch { }
            return btn;
        }

        private static ControlTemplate CreateRoundedButtonTemplate()
        {
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            borderFactory.SetValue(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            borderFactory.SetValue(Border.PaddingProperty, new Binding("Padding") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            borderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(cp);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = borderFactory };
            template.Triggers.Add(new Trigger { Property = UIElement.IsMouseOverProperty, Value = true, Setters = { new Setter(UIElement.OpacityProperty, 0.92) } });
            template.Triggers.Add(new Trigger { Property = Button.IsPressedProperty, Value = true, Setters = { new Setter(UIElement.OpacityProperty, 0.85) } });
            template.Triggers.Add(new Trigger { Property = UIElement.IsEnabledProperty, Value = false, Setters = { new Setter(UIElement.OpacityProperty, 0.55) } });
            return template;
        }

        private static ComboBox CreateComboBox()
        {
            return new ComboBox
            {
                MinHeight = 32,
                Padding = new Thickness(8, 4, 8, 4),
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1)
            };
        }

        private static CheckBox CreateRuleCheck(string text)
        {
            return new CheckBox
            {
                Content = text,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = BrushFromRgb(15, 23, 42),
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = Cursors.Hand
            };
        }

        private static TextBlock CreateMetricText(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(14, 116, 144)
            };
        }

        private static Button CreatePrimaryButton(string text, Brush background)
        {
            return new Button
            {
                Content = text,
                Padding = new Thickness(14, 9, 14, 9),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = background,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
        }

        private static Button CreatePagerButton(string text)
        {
            return new Button
            {
                Content = text,
                Padding = new Thickness(12, 7, 12, 7),
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1),
                Foreground = BrushFromRgb(71, 85, 105),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
        }

        private static TextBlock CreateReadOnlyValue(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = BrushFromRgb(15, 23, 42),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static Border CreateHeroChip(string text, Brush background)
        {
            return new Border
            {
                Margin = new Thickness(8, 0, 0, 8),
                Padding = new Thickness(12, 7, 12, 7),
                CornerRadius = new CornerRadius(999),
                Background = background,
                Child = new TextBlock { Text = text, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White }
            };
        }

        private static Border BuildTablePanel(string title, string subtitle, DataGrid grid)
        {
            var shell = new Border
            {
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                ClipToBounds = true,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            shell.Child = layout;

            var header = new Border
            {
                Background = BrushFromRgb(248, 250, 252),
                Padding = new Thickness(16, 14, 16, 14)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.Child = headerGrid;

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(15, 23, 42) });
            titleStack.Children.Add(new TextBlock { Text = subtitle, Margin = new Thickness(0, 3, 0, 0), FontSize = 11.5, Foreground = BrushFromRgb(100, 116, 139), TextWrapping = TextWrapping.Wrap });
            headerGrid.Children.Add(titleStack);

            Grid.SetRow(header, 0);
            layout.Children.Add(header);

            var gridHost = new Border
            {
                Background = Brushes.White,
                Padding = new Thickness(0),
                Child = grid
            };
            Grid.SetRow(gridHost, 1);
            layout.Children.Add(gridHost);

            return shell;
        }

        private static DataGrid CreateGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = false,
                CanUserReorderColumns = false,
                CanUserResizeColumns = true,
                CanUserSortColumns = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.None,
                RowHeaderWidth = 0,
                Background = Brushes.White,
                AlternatingRowBackground = BrushFromRgb(252, 253, 254),
                BorderThickness = new Thickness(0),
                RowHeight = 56,
                ColumnHeaderHeight = 44,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                EnableRowVirtualization = true,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };

            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, BrushFromRgb(248, 250, 252)));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(71, 85, 105)));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 0, 14, 0)));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFromRgb(226, 232, 240)));
            headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            headerStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            headerStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            headerStyle.Setters.Add(new Setter(Control.IsTabStopProperty, false));
            grid.ColumnHeaderStyle = headerStyle;

            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 0, 14, 0)));
            cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            cellStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            cellStyle.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            cellStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
            cellStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(15, 23, 42)));
            cellStyle.Triggers.Add(new Trigger
            {
                Property = DataGridCell.IsSelectedProperty,
                Value = true,
                Setters =
                {
                    new Setter(Control.BackgroundProperty, Brushes.Transparent),
                    new Setter(Control.BorderThicknessProperty, new Thickness(0))
                }
            });
            grid.CellStyle = cellStyle;

            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(15, 23, 42)));
            rowStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
            rowStyle.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFromRgb(241, 245, 249)));
            rowStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            rowStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            rowStyle.Setters.Add(new Setter(Control.SnapsToDevicePixelsProperty, true));
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, BrushFromRgb(241, 245, 249)));
            rowStyle.Triggers.Add(hoverTrigger);
            var selectedTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty, BrushFromRgb(238, 242, 255)));
            selectedTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty, BrushFromRgb(15, 23, 42)));
            selectedTrigger.Setters.Add(new Setter(DataGridRow.BorderBrushProperty, BrushFromRgb(199, 210, 254)));
            selectedTrigger.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            rowStyle.Triggers.Add(selectedTrigger);
            grid.RowStyle = rowStyle;
            return grid;
        }

        private static void BuildDepartmentColumns(DataGrid grid)
        {
            grid.Columns.Add(CreateIconColumn("#0e7490", "#ccfbf1", "D"));
            grid.Columns.Add(CreateTemplateColumn("Department", 240,
                "<DataTemplate><StackPanel><TextBlock Text='{Binding DepartmentName}' TextWrapping='Wrap' FontSize='13' FontWeight='SemiBold' Foreground='#0f172a'/><TextBlock Text='{Binding DeptId, StringFormat=Department #{0}}' Margin='0,3,0,0' FontSize='11' Foreground='#94a3b8'/></StackPanel></DataTemplate>"));
            grid.Columns.Add(CreateTemplateColumn("Routing", 320,
                "<DataTemplate><StackPanel><TextBlock Text='{Binding RecipientDisplay}' TextWrapping='Wrap' FontSize='12.5' FontWeight='SemiBold' Foreground='#1e293b'/><TextBlock Text='{Binding EscalationDisplay}' Margin='0,4,0,0' TextWrapping='Wrap' FontSize='11' Foreground='#b45309'/></StackPanel></DataTemplate>"));
            // "SMTP Profile" and SSL badge columns removed: departments no longer link to a
            // per-department SMTP profile (single global sender design), so these columns
            // would always render blank. See CallDepartmentSmtpProfileLink (kept, unused).
            grid.Columns.Add(CreateDateColumn("UpdatedAt", "Updated", 130));
        }

        private static void BuildBranchColumns(DataGrid grid)
        {
            grid.Columns.Add(CreateIconColumn("#f59e0b", "#fef3c7", "B"));
            grid.Columns.Add(CreateTemplateColumn("Branch", 250,
                "<DataTemplate><StackPanel><TextBlock Text='{Binding BranchName}' TextWrapping='Wrap' FontSize='13' FontWeight='SemiBold' Foreground='#0f172a'/><TextBlock Text='{Binding CompanyName}' Margin='0,4,0,0' TextWrapping='Wrap' FontSize='11' Foreground='#475569'/><TextBlock Text='{Binding DepartmentName}' Margin='0,2,0,0' TextWrapping='Wrap' FontSize='11' Foreground='#94a3b8'/></StackPanel></DataTemplate>"));
            grid.Columns.Add(CreateTemplateColumn("Recipients", 300,
                "<DataTemplate><StackPanel><TextBlock Text='{Binding RecipientDisplay}' TextWrapping='Wrap' FontSize='12.5' FontWeight='SemiBold' Foreground='#1e293b'/><TextBlock Text='{Binding EscalationDisplay}' Margin='0,4,0,0' TextWrapping='Wrap' FontSize='11' Foreground='#b45309'/></StackPanel></DataTemplate>"));
            // "SMTP Profile" and SSL badge columns removed: branches no longer link to a
            // per-branch SMTP profile (single global sender design), so these columns
            // would always render blank. See CallBranchSmtpProfileLink (kept, unused).
            grid.Columns.Add(CreateDateColumn("UpdatedAt", "Updated", 130));
        }

        private static void BuildProfileColumns(DataGrid grid)
        {
            grid.Columns.Add(CreateIconColumn("#2563eb", "#dbeafe", "P"));
            grid.Columns.Add(CreateTemplateColumn("Profile", 230,
                "<DataTemplate><StackPanel><TextBlock Text='{Binding ProfileName}' FontSize='13' FontWeight='SemiBold' Foreground='#0f172a'/><TextBlock Text='{Binding ProfileId, StringFormat=Profile #{0}}' Margin='0,3,0,0' FontSize='11' Foreground='#94a3b8'/></StackPanel></DataTemplate>"));
            grid.Columns.Add(CreateTemplateColumn("Server", 230,
                "<DataTemplate><StackPanel><TextBlock Text='{Binding SmtpServer}' FontSize='12.5' FontWeight='SemiBold' Foreground='#1e293b'/><TextBlock Text='{Binding SmtpPort, StringFormat=Port {0}}' Margin='0,3,0,0' FontSize='11' Foreground='#94a3b8'/></StackPanel></DataTemplate>"));
            grid.Columns.Add(CreateColumn("SmtpUsername", "Username", 160));
            grid.Columns.Add(CreateTemplateColumn("From Identity", 200,
                "<DataTemplate><StackPanel><TextBlock Text='{Binding FromEmail}' TextWrapping='Wrap' FontSize='12.5' FontWeight='SemiBold' Foreground='#1e293b'/><TextBlock Text='{Binding FromName}' Margin='0,3,0,0' TextWrapping='Wrap' FontSize='11' Foreground='#94a3b8'/></StackPanel></DataTemplate>"));
            grid.Columns.Add(CreateActiveBadgeColumn());
            grid.Columns.Add(CreateDateColumn("UpdatedAt", "Updated", 130));
        }

        private static void BuildLogColumns(DataGrid grid)
        {
            grid.Columns.Add(CreateIconColumn("#10b981", "#d1fae5", "E"));
            grid.Columns.Add(CreateTemplateColumn("Timestamp", 170,
                "<DataTemplate><StackPanel><TextBlock FontSize='12.5' FontWeight='SemiBold' Foreground='#0f172a'><TextBlock.Text><Binding Path='DateSent' StringFormat='{}{0:MMM dd, yyyy}'/></TextBlock.Text></TextBlock><TextBlock Margin='0,3,0,0' FontSize='11' Foreground='#94a3b8' FontFamily='Consolas'><TextBlock.Text><Binding Path='DateSent' StringFormat='{}{0:HH:mm:ss}'/></TextBlock.Text></TextBlock></StackPanel></DataTemplate>"));
            grid.Columns.Add(CreateTemplateColumn("Ticket", 110,
                "<DataTemplate><Border Padding='8,4,8,4' HorizontalAlignment='Left' CornerRadius='6' Background='#f1f5f9'><TextBlock Text='{Binding TicketId, StringFormat=#{0}}' FontSize='11.5' FontWeight='SemiBold' Foreground='#475569' FontFamily='Consolas'/></Border></DataTemplate>"));
            grid.Columns.Add(CreateTypeBadgeColumn());
            grid.Columns.Add(CreateTemplateColumn("Recipient", 240,
                "<DataTemplate><StackPanel><TextBlock Text='{Binding Recipient}' TextWrapping='Wrap' FontSize='12.5' FontWeight='SemiBold' Foreground='#1e293b'/><TextBlock Text='{Binding Subject}' Margin='0,3,0,0' TextWrapping='Wrap' FontSize='11' Foreground='#94a3b8'/></StackPanel></DataTemplate>"));
            grid.Columns.Add(CreateTemplateColumn("Branch", 160,
                "<DataTemplate><TextBlock Text='{Binding Branch}' FontSize='12' Foreground='#475569'/></DataTemplate>"));
            grid.Columns.Add(CreateDeliveryStatusColumn());
            grid.Columns.Add(CreateTemplateColumn("Notes", 280,
                "<DataTemplate><TextBlock Text='{Binding ErrorMessage}' Foreground='#64748b' TextWrapping='Wrap' FontSize='11'/></DataTemplate>"));
        }

        private static DataGridTextColumn CreateColumn(string path, string header, double width)
        {
            return new DataGridTextColumn { Header = header, Binding = new Binding(path), Width = new DataGridLength(width) };
        }

        private static DataGridTemplateColumn CreateIconColumn(string accent, string soft, string glyph)
        {
            return CreateTemplateColumn(string.Empty, 46,
                "<DataTemplate><Border VerticalAlignment='Center' HorizontalAlignment='Center' Width='28' Height='28' CornerRadius='8' Background='" + soft + "'><TextBlock Text='" + glyph + "' HorizontalAlignment='Center' VerticalAlignment='Center' FontSize='13' FontWeight='Bold' Foreground='" + accent + "'/></Border></DataTemplate>");
        }

        private static DataGridTemplateColumn CreateSslBadgeColumn()
        {
            return CreateTemplateColumn("Security", 110,
                "<DataTemplate><Border Padding='10,4,10,4' CornerRadius='999' HorizontalAlignment='Left' VerticalAlignment='Center'><Border.Style><Style TargetType='Border'><Setter Property='Background' Value='#fef2f2'/><Style.Triggers><DataTrigger Binding='{Binding UseSsl}' Value='True'><Setter Property='Background' Value='#ecfdf5'/></DataTrigger></Style.Triggers></Style></Border.Style><StackPanel Orientation='Horizontal'><Ellipse Width='6' Height='6' VerticalAlignment='Center'><Ellipse.Style><Style TargetType='Ellipse'><Setter Property='Fill' Value='#dc2626'/><Style.Triggers><DataTrigger Binding='{Binding UseSsl}' Value='True'><Setter Property='Fill' Value='#10b981'/></DataTrigger></Style.Triggers></Style></Ellipse.Style></Ellipse><TextBlock Margin='6,0,0,0' VerticalAlignment='Center' FontSize='11' FontWeight='SemiBold'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Text' Value='Off'/><Setter Property='Foreground' Value='#991b1b'/><Style.Triggers><DataTrigger Binding='{Binding UseSsl}' Value='True'><Setter Property='Text' Value='SSL'/><Setter Property='Foreground' Value='#065f46'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></StackPanel></Border></DataTemplate>");
        }

        private static DataGridTemplateColumn CreateActiveBadgeColumn()
        {
            return CreateTemplateColumn("Status", 110,
                "<DataTemplate><Border Padding='10,4,10,4' CornerRadius='999' HorizontalAlignment='Left' VerticalAlignment='Center'><Border.Style><Style TargetType='Border'><Setter Property='Background' Value='#fef2f2'/><Style.Triggers><DataTrigger Binding='{Binding IsActive}' Value='True'><Setter Property='Background' Value='#ecfdf5'/></DataTrigger></Style.Triggers></Style></Border.Style><StackPanel Orientation='Horizontal'><Ellipse Width='6' Height='6' VerticalAlignment='Center'><Ellipse.Style><Style TargetType='Ellipse'><Setter Property='Fill' Value='#dc2626'/><Style.Triggers><DataTrigger Binding='{Binding IsActive}' Value='True'><Setter Property='Fill' Value='#10b981'/></DataTrigger></Style.Triggers></Style></Ellipse.Style></Ellipse><TextBlock Margin='6,0,0,0' VerticalAlignment='Center' FontSize='11' FontWeight='SemiBold'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Text' Value='Inactive'/><Setter Property='Foreground' Value='#991b1b'/><Style.Triggers><DataTrigger Binding='{Binding IsActive}' Value='True'><Setter Property='Text' Value='Active'/><Setter Property='Foreground' Value='#065f46'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></StackPanel></Border></DataTemplate>");
        }

        private static DataGridTemplateColumn CreateTypeBadgeColumn()
        {
            return CreateTemplateColumn("Type", 140,
                "<DataTemplate><Border Padding='10,4,10,4' CornerRadius='6' HorizontalAlignment='Left' VerticalAlignment='Center' Background='#eff6ff'><TextBlock Text='{Binding EmailType}' FontSize='11' FontWeight='SemiBold' Foreground='#1e40af'/></Border></DataTemplate>");
        }

        private static DataGridTemplateColumn CreateDeliveryStatusColumn()
        {
            return CreateTemplateColumn("Status", 130,
                "<DataTemplate><Border Padding='10,4,10,4' CornerRadius='999' HorizontalAlignment='Left' VerticalAlignment='Center'><Border.Style><Style TargetType='Border'><Setter Property='Background' Value='#f1f5f9'/><Style.Triggers><DataTrigger Binding='{Binding Status}' Value='Sent'><Setter Property='Background' Value='#ecfdf5'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Failed'><Setter Property='Background' Value='#fef2f2'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Skipped'><Setter Property='Background' Value='#fffbeb'/></DataTrigger></Style.Triggers></Style></Border.Style><StackPanel Orientation='Horizontal'><Ellipse Width='6' Height='6' VerticalAlignment='Center'><Ellipse.Style><Style TargetType='Ellipse'><Setter Property='Fill' Value='#64748b'/><Style.Triggers><DataTrigger Binding='{Binding Status}' Value='Sent'><Setter Property='Fill' Value='#10b981'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Failed'><Setter Property='Fill' Value='#dc2626'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Skipped'><Setter Property='Fill' Value='#d97706'/></DataTrigger></Style.Triggers></Style></Ellipse.Style></Ellipse><TextBlock Margin='6,0,0,0' Text='{Binding Status}' VerticalAlignment='Center' FontSize='11' FontWeight='SemiBold'><TextBlock.Style><Style TargetType='TextBlock'><Setter Property='Foreground' Value='#475569'/><Style.Triggers><DataTrigger Binding='{Binding Status}' Value='Sent'><Setter Property='Foreground' Value='#065f46'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Failed'><Setter Property='Foreground' Value='#991b1b'/></DataTrigger><DataTrigger Binding='{Binding Status}' Value='Skipped'><Setter Property='Foreground' Value='#92400e'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></StackPanel></Border></DataTemplate>");
        }

        private static DataGridTemplateColumn CreateDateColumn(string path, string header, double width)
        {
            return CreateTemplateColumn(header, width,
                "<DataTemplate><StackPanel><TextBlock Text='{Binding " + path + "}' FontSize='12' FontWeight='SemiBold' Foreground='#1e293b'/><TextBlock Text='{Binding " + path + "}' Margin='0,3,0,0' FontSize='11' Foreground='#94a3b8' FontFamily='Consolas'/></StackPanel></DataTemplate>");
        }

        private static DataGridTemplateColumn CreateTemplateColumn(string header, double width, string xaml)
        {
            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            ctx.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
            return new DataGridTemplateColumn
            {
                Header = header,
                CellTemplate = (DataTemplate)XamlReader.Parse(xaml, ctx),
                Width = new DataGridLength(width)
            };
        }

        private static Style BuildTabItemStyle()
        {
            const string xaml = @"<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" TargetType=""TabItem""><Setter Property=""Foreground"" Value=""#64748b""/><Setter Property=""FontSize"" Value=""13""/><Setter Property=""Template""><Setter.Value><ControlTemplate TargetType=""TabItem""><Border Name=""Bd"" Padding=""16,8"" Margin=""0,0,4,0"" CornerRadius=""8,8,0,0"" Background=""#e2e8f0""><ContentPresenter VerticalAlignment=""Center"" HorizontalAlignment=""Center"" ContentSource=""Header""/></Border><ControlTemplate.Triggers><Trigger Property=""IsSelected"" Value=""True""><Setter TargetName=""Bd"" Property=""Background"" Value=""White""/><Setter Property=""Foreground"" Value=""#0f172a""/><Setter Property=""FontWeight"" Value=""SemiBold""/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>";
            var ctx = new ParserContext();
            ctx.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            ctx.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
            return (Style)XamlReader.Parse(xaml, ctx);
        }

        private static int CountEnabledRules(CallNotificationRulesItem rules)
        {
            var count = 0;
            if (rules.NotifyOnNewTicket) count++;
            if (rules.NotifyOnStatusChange) count++;
            if (rules.NotifyOnEscalation) count++;
            if (rules.NotifyOnReminder) count++;
            return count;
        }

        private static bool ContainsAny(string query, params string[] values)
        {
            return values.Any(v => !string.IsNullOrWhiteSpace(v) && v.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
