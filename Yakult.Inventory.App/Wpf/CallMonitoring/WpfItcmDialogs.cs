using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public enum WpfEmailNotificationDeepLinkTarget
    {
        SetupSmtp,
        SetupNotificationRules,
        TemplatesNewTicket,
        TemplatesAssignment,
        TemplatesReassignment,
        TemplatesStatusUpdate,
        TemplatesReminder,
        TemplatesEscalation,
        DepartmentRecipients,
        BranchRecipients,
        SmtpProfiles,
        EmailLog
    }

    internal static class WpfItcmDialogService
    {
        public static void ShowInfo(DependencyObject owner, string message, string title)
            => ShowAlert(owner, message, title, "Info", BrushFromRgb(14, 116, 144));

        public static void ShowWarning(DependencyObject owner, string message, string title)
            => ShowAlert(owner, message, title, "Warning", BrushFromRgb(217, 119, 6));

        public static void ShowError(DependencyObject owner, string message, string title)
            => ShowAlert(owner, message, title, "Error", BrushFromRgb(220, 38, 38));

        public static bool Confirm(DependencyObject owner, string message, string title, string confirmText = "Confirm", bool destructive = false)
        {
            var dialog = CreateBaseWindow(owner, title, 520, 300);
            var accent = destructive ? BrushFromRgb(220, 38, 38) : BrushFromRgb(37, 99, 235);
            dialog.Content = BuildAlertContent(
                title,
                message,
                destructive ? "Confirm" : "Review",
                accent,
                confirmText,
                () => dialog.DialogResult = true,
                "Cancel",
                () => dialog.DialogResult = false);
            return dialog.ShowDialog() == true;
        }

        private static void ShowAlert(DependencyObject owner, string message, string title, string label, Brush accent)
        {
            var dialog = CreateBaseWindow(owner, title, 500, 280);
            dialog.Content = BuildAlertContent(
                title,
                message,
                label,
                accent,
                "OK",
                () => dialog.DialogResult = true,
                null,
                null);
            dialog.ShowDialog();
        }

        private static Window CreateBaseWindow(DependencyObject owner, string title, double width, double height)
        {
            var window = new Window
            {
                Title = title ?? "IT Call Monitoring",
                Width = width,
                Height = height,
                MinWidth = 420,
                MinHeight = 240,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Background = BrushFromRgb(245, 247, 250)
            };

            var ownerWindow = owner as Window ?? Window.GetWindow(owner);
            if (ownerWindow != null)
                window.Owner = ownerWindow;

            return window;
        }

        private static FrameworkElement BuildAlertContent(
            string title,
            string message,
            string label,
            Brush accent,
            string primaryText,
            Action primaryAction,
            string secondaryText,
            Action secondaryAction)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var body = new StackPanel { Margin = new Thickness(24, 22, 24, 18) };
            body.Children.Add(CreatePill(label, accent));
            body.Children.Add(new TextBlock
            {
                Text = title ?? "IT Call Monitoring",
                Margin = new Thickness(0, 14, 0, 0),
                FontSize = 21,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42),
                TextWrapping = TextWrapping.Wrap
            });
            body.Children.Add(new TextBlock
            {
                Text = message ?? string.Empty,
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 13,
                LineHeight = 20,
                Foreground = BrushFromRgb(71, 85, 105),
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetRow(body, 0);
            root.Children.Add(body);

            var footer = CreateFooter();
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 12, 22, 12)
            };
            if (!string.IsNullOrWhiteSpace(secondaryText))
            {
                var secondary = CreateButton(secondaryText, BrushFromRgb(100, 116, 139));
                secondary.IsCancel = true;
                secondary.Click += (_, __) => secondaryAction?.Invoke();
                buttons.Children.Add(secondary);
            }
            var primary = CreateButton(primaryText ?? "OK", accent);
            primary.IsDefault = true;
            primary.Click += (_, __) => primaryAction?.Invoke();
            buttons.Children.Add(primary);
            footer.Children.Add(buttons);
            Grid.SetRow(footer, 1);
            root.Children.Add(footer);

            return root;
        }

        internal static DockPanel CreateFooter()
        {
            return new DockPanel
            {
                Height = 64,
                Background = Brushes.White,
                LastChildFill = false
            };
        }

        internal static Button CreateButton(string text, Brush background)
        {
            return new Button
            {
                Content = text,
                MinWidth = 104,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(14, 8, 14, 8),
                Background = background,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
        }

        internal static TextBox CreateTextBox(string text = null, double minHeight = 34)
        {
            return new TextBox
            {
                Text = text ?? string.Empty,
                MinHeight = minHeight,
                Padding = new Thickness(9, 7, 9, 7),
                BorderBrush = BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Foreground = BrushFromRgb(15, 23, 42),
                TextWrapping = TextWrapping.Wrap
            };
        }

        internal static TextBlock CreateSectionTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(15, 23, 42),
                Margin = new Thickness(0, 0, 0, 10)
            };
        }

        internal static Border CreateCard(double padding = 16)
        {
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(padding),
                Margin = new Thickness(0, 0, 0, 14)
            };
        }

        internal static Border CreatePill(string text, Brush background)
        {
            return new Border
            {
                Background = background,
                CornerRadius = new CornerRadius(12),
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 4, 10, 4),
                Child = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(text) ? "-" : text.Trim(),
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                }
            };
        }

        internal static TextBlock CreateMutedText(string text)
        {
            return new TextBlock
            {
                Text = text ?? string.Empty,
                FontSize = 12.5,
                Foreground = BrushFromRgb(100, 116, 139),
                TextWrapping = TextWrapping.Wrap
            };
        }

        internal static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }

    public sealed class WpfConfirmTicketCreationDialog : Window
    {
        public enum CreationAction
        {
            Cancel,
            Create,
            CreateAndOpen,
            CreateAnother
        }

        public sealed class TicketCreationModel
        {
            public string Company { get; set; }
            public string Department { get; set; }
            public string Branch { get; set; }
            public string Caller { get; set; }
            public string AssignedTo { get; set; }
            public string Priority { get; set; }
            public string EscalationSummary { get; set; }
            public string EscalationReason { get; set; }
            public string Issue { get; set; }
            public string InitialNotes { get; set; }
            public string BackdateDisplay { get; set; }
            public string RecentOpenTicketWarning { get; set; }
            public string SuggestedPriority { get; set; }
            public string PrioritySuggestionReason { get; set; }
            public string TicketContactEmail { get; set; }
            public string TicketContactEmailSource { get; set; }
            public bool RequiresTemporaryTicketContactEmail { get; set; }
            public bool RequiresCallerEmail { get; set; }
            public bool CanLinkTicketContactEmailToCaller { get; set; }
        }

        private readonly TicketCreationModel _model;
        private readonly ComboBox _priorityCombo;
        private readonly TextBox _temporaryTicketContactEmailBox;
        private readonly CheckBox _linkCallerEmailToProfileCheckBox;
        private readonly CheckBox _useOrgFallbackCheckBox;

        public CreationAction SelectedAction { get; private set; } = CreationAction.Cancel;
        public string SelectedPriority => (_priorityCombo.SelectedItem as string) ?? "Medium";
            public string SelectedTicketContactEmail { get; private set; }

            // Never link the organization fallback as a personal email: the
            // link is only honored when the operator actually typed an address.
            public bool ShouldLinkTicketContactEmailToCaller => _linkCallerEmailToProfileCheckBox?.IsChecked == true
                && !string.IsNullOrWhiteSpace(_temporaryTicketContactEmailBox?.Text)
                && _useOrgFallbackCheckBox?.IsChecked != true;

            public bool UseOrganizationalFallback => !_model.RequiresCallerEmail
                || _useOrgFallbackCheckBox?.IsChecked == true;
        public WpfConfirmTicketCreationDialog(TicketCreationModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            _model = model;

            Title = "Confirm Ticket Creation";
            Width = 900;
            Height = 760;
            MinWidth = 760;
            MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            Background = WpfItcmDialogService.BrushFromRgb(245, 247, 250);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var header = new Border
            {
                Background = Brushes.White,
                Padding = new Thickness(28, 20, 28, 18),
                BorderBrush = WpfItcmDialogService.BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = "Confirm ticket creation",
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = WpfItcmDialogService.BrushFromRgb(15, 23, 42)
            });
            headerStack.Children.Add(WpfItcmDialogService.CreateMutedText("Review the requester, contact routing, and ticket details before saving."));
            Grid.SetColumn(headerStack, 0);
            headerGrid.Children.Add(headerStack);
            var reviewPill = WpfItcmDialogService.CreatePill("REVIEW", WpfItcmDialogService.BrushFromRgb(37, 99, 235));
            reviewPill.VerticalAlignment = VerticalAlignment.Top;
            reviewPill.Margin = new Thickness(18, 2, 0, 0);
            Grid.SetColumn(reviewPill, 1);
            headerGrid.Children.Add(reviewPill);
            header.Child = headerGrid;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var body = new StackPanel { Margin = new Thickness(28, 22, 28, 24) };
            var scroll = new ScrollViewer
            {
                Content = body,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var summaryGrid = new Grid();
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var requester = new StackPanel();
            requester.Children.Add(WpfItcmDialogService.CreateSectionTitle("Requester & organization"));
            AddDetail(requester, "Caller", model.Caller);
            AddDetail(requester, "Company", model.Company);
            AddDetail(requester, "Department", model.Department);
            AddDetail(requester, "Branch", model.Branch);
            var requesterCard = WpfItcmDialogService.CreateCard();
            requesterCard.Child = requester;
            requesterCard.Margin = new Thickness(0, 0, 7, 0);
            Grid.SetColumn(requesterCard, 0);
            summaryGrid.Children.Add(requesterCard);

            var handling = new StackPanel();
            handling.Children.Add(WpfItcmDialogService.CreateSectionTitle("Ticket handling"));
            AddDetail(handling, "Assigned to", model.AssignedTo);
            handling.Children.Add(CreateLabel("Priority"));
            _priorityCombo = new ComboBox
            {
                ItemsSource = new[] { "Low", "Medium", "High", "Critical" },
                MinWidth = 190,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 2, 0, 10)
            };
            var initialPriority = string.IsNullOrWhiteSpace(model.SuggestedPriority) ? model.Priority : model.SuggestedPriority;
            _priorityCombo.SelectedItem = new[] { "Low", "Medium", "High", "Critical" }.Contains(initialPriority) ? initialPriority : "Medium";
            handling.Children.Add(_priorityCombo);
            handling.Children.Add(CreateLabel("Escalation"));
            handling.Children.Add(WpfItcmDialogService.CreatePill(
                model.EscalationSummary ?? "-",
                (model.EscalationSummary ?? string.Empty).StartsWith("Custom", StringComparison.OrdinalIgnoreCase)
                    ? WpfItcmDialogService.BrushFromRgb(217, 119, 6)
                    : WpfItcmDialogService.BrushFromRgb(100, 116, 139)));
            if (!string.IsNullOrWhiteSpace(model.EscalationReason))
                handling.Children.Add(WpfItcmDialogService.CreateMutedText("Reason: " + model.EscalationReason.Trim()));
            if (!string.IsNullOrWhiteSpace(model.BackdateDisplay))
                AddDetail(handling, "Backdate", model.BackdateDisplay + " (logbook entry)");
            if (!string.IsNullOrWhiteSpace(model.PrioritySuggestionReason))
                handling.Children.Add(WpfItcmDialogService.CreateMutedText(model.PrioritySuggestionReason.Trim()));
            var handlingCard = WpfItcmDialogService.CreateCard();
            handlingCard.Child = handling;
            handlingCard.Margin = new Thickness(7, 0, 0, 0);
            Grid.SetColumn(handlingCard, 1);
            summaryGrid.Children.Add(handlingCard);
            body.Children.Add(summaryGrid);

            var contactStack = new StackPanel();
            contactStack.Children.Add(WpfItcmDialogService.CreateSectionTitle("Contact routing"));
            var needsContactInput = model.RequiresCallerEmail || model.RequiresTemporaryTicketContactEmail;
            if (model.RequiresCallerEmail)
            {
                contactStack.Children.Add(WpfItcmDialogService.CreatePill("CALLER EMAIL (OPTIONAL)", WpfItcmDialogService.BrushFromRgb(217, 119, 6)));
                contactStack.Children.Add(WpfItcmDialogService.CreateMutedText("The selected caller has no active primary personal email. Enter the caller's email below, or leave it blank to use the organization fallback."));
                AddDetail(contactStack, "Organization fallback", string.IsNullOrWhiteSpace(model.TicketContactEmail) ? "No branch or department email found" : model.TicketContactEmail);
                if (!string.IsNullOrWhiteSpace(model.TicketContactEmailSource))
                    AddDetail(contactStack, "Fallback source", model.TicketContactEmailSource);
                var hasFallback = !string.IsNullOrWhiteSpace(model.TicketContactEmail)
                    && EmailAddressValidator.TryNormalize(model.TicketContactEmail, out _);
                _useOrgFallbackCheckBox = new CheckBox
                {
                    Content = "Use organizational fallback above",
                    IsChecked = hasFallback,
                    IsEnabled = hasFallback,
                    Margin = new Thickness(0, 8, 0, 3),
                    Foreground = WpfItcmDialogService.BrushFromRgb(15, 23, 42)
                };
                contactStack.Children.Add(_useOrgFallbackCheckBox);
                contactStack.Children.Add(CreateLabel("Caller personal email (optional)"));
                _temporaryTicketContactEmailBox = WpfItcmDialogService.CreateTextBox(string.Empty, 38);
                _temporaryTicketContactEmailBox.MaxLength = 255;
                _temporaryTicketContactEmailBox.IsEnabled = !hasFallback;
                contactStack.Children.Add(_temporaryTicketContactEmailBox);
                if (model.CanLinkTicketContactEmailToCaller)
                {
                    _linkCallerEmailToProfileCheckBox = new CheckBox
                    {
                        Content = "Link this as the caller's active primary employee email",
                        Margin = new Thickness(0, 8, 0, 3),
                        Foreground = WpfItcmDialogService.BrushFromRgb(15, 23, 42),
                        IsEnabled = !hasFallback
                    };
                    contactStack.Children.Add(_linkCallerEmailToProfileCheckBox);
                }
                else
                {
                    _linkCallerEmailToProfileCheckBox = null;
                }
                _useOrgFallbackCheckBox.Checked += (_, __) =>
                {
                    _temporaryTicketContactEmailBox.IsEnabled = false;
                    if (_linkCallerEmailToProfileCheckBox != null)
                        _linkCallerEmailToProfileCheckBox.IsEnabled = false;
                };
                _useOrgFallbackCheckBox.Unchecked += (_, __) =>
                {
                    _temporaryTicketContactEmailBox.IsEnabled = true;
                    if (_linkCallerEmailToProfileCheckBox != null)
                        _linkCallerEmailToProfileCheckBox.IsEnabled = true;
                };
                contactStack.Children.Add(WpfItcmDialogService.CreateMutedText("Leave blank to use the organization fallback above. A typed address saves only on this ticket unless linked; selecting the option also updates the caller's employee profile."));
            }
            else if (model.RequiresTemporaryTicketContactEmail)
            {
                contactStack.Children.Add(WpfItcmDialogService.CreatePill("TICKET CONTACT REQUIRED", WpfItcmDialogService.BrushFromRgb(217, 119, 6)));
                contactStack.Children.Add(WpfItcmDialogService.CreateMutedText("No linked employee, branch, or department email was found."));
                contactStack.Children.Add(CreateLabel("Temporary ticket contact email (required)"));
                _temporaryTicketContactEmailBox = WpfItcmDialogService.CreateTextBox(string.Empty, 38);
                _temporaryTicketContactEmailBox.MaxLength = 255;
                contactStack.Children.Add(_temporaryTicketContactEmailBox);
                _linkCallerEmailToProfileCheckBox = null;
                _useOrgFallbackCheckBox = null;
                contactStack.Children.Add(WpfItcmDialogService.CreateMutedText("This address is saved only on this ticket and does not change shared email settings."));
            }
            else
            {
                contactStack.Children.Add(WpfItcmDialogService.CreatePill("CONTACT RESOLVED", WpfItcmDialogService.BrushFromRgb(22, 163, 74)));
                AddDetail(contactStack, "Ticket contact", model.TicketContactEmail);
                AddDetail(contactStack, "Source", model.TicketContactEmailSource);
                _temporaryTicketContactEmailBox = null;
                _linkCallerEmailToProfileCheckBox = null;
                _useOrgFallbackCheckBox = null;
            }
            var contactCard = WpfItcmDialogService.CreateCard();
            contactCard.Background = needsContactInput
                ? WpfItcmDialogService.BrushFromRgb(255, 251, 235)
                : WpfItcmDialogService.BrushFromRgb(240, 253, 244);
            contactCard.Child = contactStack;
            contactCard.Margin = new Thickness(0, 14, 0, 0);
            body.Children.Add(contactCard);

            if (!string.IsNullOrWhiteSpace(model.RecentOpenTicketWarning))
            {
                var warning = new StackPanel();
                warning.Children.Add(WpfItcmDialogService.CreateSectionTitle("Recent open tickets"));
                warning.Children.Add(new TextBlock
                {
                    Text = model.RecentOpenTicketWarning.Trim(),
                    Foreground = WpfItcmDialogService.BrushFromRgb(146, 64, 14),
                    TextWrapping = TextWrapping.Wrap
                });
                var warningCard = WpfItcmDialogService.CreateCard();
                warningCard.Background = WpfItcmDialogService.BrushFromRgb(255, 251, 235);
                warningCard.Child = warning;
                body.Children.Add(warningCard);
            }

            var textGrid = new Grid();
            textGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            textGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var issueCard = CreateTextCard("Issue", model.Issue, 125);
            var notesCard = CreateTextCard("Initial notes", string.IsNullOrWhiteSpace(model.InitialNotes) ? "(None)" : model.InitialNotes, 125);
            issueCard.Margin = new Thickness(0, 14, 7, 0);
            notesCard.Margin = new Thickness(7, 14, 0, 0);
            Grid.SetColumn(issueCard, 0);
            Grid.SetColumn(notesCard, 1);
            textGrid.Children.Add(issueCard);
            textGrid.Children.Add(notesCard);
            body.Children.Add(textGrid);

            var footer = WpfItcmDialogService.CreateFooter();
            var footerGrid = new Grid { Margin = new Thickness(20, 10, 20, 10) };
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var footerHint = WpfItcmDialogService.CreateMutedText("Nothing is saved until you choose a create action.");
            footerHint.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(footerHint, 0);
            footerGrid.Children.Add(footerHint);
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            buttons.Children.Add(CreateActionButton("Cancel", WpfItcmDialogService.BrushFromRgb(100, 116, 139), CreationAction.Cancel, true));
            buttons.Children.Add(CreateActionButton("Create Another", WpfItcmDialogService.BrushFromRgb(15, 118, 110), CreationAction.CreateAnother, false));
            buttons.Children.Add(CreateActionButton("Create and Open", WpfItcmDialogService.BrushFromRgb(37, 99, 235), CreationAction.CreateAndOpen, false));
            buttons.Children.Add(CreateActionButton("Create", WpfItcmDialogService.BrushFromRgb(22, 163, 74), CreationAction.Create, false));
            Grid.SetColumn(buttons, 1);
            footerGrid.Children.Add(buttons);
            footer.Children.Add(footerGrid);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
        }
        public static string SuggestPriority(string issue, out string reason)
        {
            reason = null;
            var text = (issue ?? string.Empty).Trim();
            if (text.Length == 0)
                return "Medium";

            var lower = text.ToLowerInvariant();
            if (ContainsAny(lower, "system down", "server down", "network down", "cannot login", "can't login", "urgent", "critical", "production down"))
            {
                reason = "Suggested: Critical because the issue appears urgent or blocks access/service.";
                return "Critical";
            }

            if (ContainsAny(lower, "not working", "printer not working", "offline", "no internet", "cannot print", "unable to print", "failed"))
            {
                reason = "Suggested: High because the issue likely blocks normal work.";
                return "High";
            }

            if (ContainsAny(lower, "slow", "intermittent", "request", "setup", "install"))
            {
                reason = "Suggested: Medium based on the issue description.";
                return "Medium";
            }

            return "Medium";
        }

        private Button CreateActionButton(string text, Brush brush, CreationAction action, bool cancel)
        {
            var button = WpfItcmDialogService.CreateButton(text, brush);
            button.IsCancel = cancel;
            button.IsDefault = action == CreationAction.Create;
            button.Click += (_, __) =>
            {
                if (action != CreationAction.Cancel)
                {
                    // Optional caller email: checked fallback box (or blank) uses the
                    // resolved organization address. The temporary-contact path has no
                    // fallback, so blank still blocks there.
                    var useFallback = _model.RequiresCallerEmail && _useOrgFallbackCheckBox?.IsChecked == true;
                    var typed = useFallback
                        ? null
                        : (_model.RequiresCallerEmail || _model.RequiresTemporaryTicketContactEmail)
                            ? _temporaryTicketContactEmailBox?.Text
                            : null;
                    var contactEmail = string.IsNullOrWhiteSpace(typed) ? _model.TicketContactEmail : typed;
                    if (!EmailAddressValidator.TryNormalize(contactEmail, out var normalizedContactEmail))
                    {
                        WpfItcmDialogService.ShowWarning(this, "Enter a valid ticket contact email before creating the ticket.", "Ticket Contact Required");
                        return;
                    }

                    SelectedTicketContactEmail = normalizedContactEmail;
                }

                SelectedAction = action;
                DialogResult = action != CreationAction.Cancel;
                Close();
            };
            return button;
        }

        private static Border CreateTextCard(string title, string text, double height)
        {
            var stack = new StackPanel();
            stack.Children.Add(WpfItcmDialogService.CreateSectionTitle(title));
            var box = WpfItcmDialogService.CreateTextBox(text ?? string.Empty, height);
            box.IsReadOnly = true;
            box.AcceptsReturn = true;
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            stack.Children.Add(box);
            var card = WpfItcmDialogService.CreateCard();
            card.Child = stack;
            return card;
        }

        private static void AddDetail(Panel parent, string label, string value)
        {
            parent.Children.Add(CreateLabel(label));
            parent.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(value) ? "-" : value.Trim(),
                Margin = new Thickness(0, 0, 0, 10),
                FontSize = 13,
                Foreground = WpfItcmDialogService.BrushFromRgb(15, 23, 42),
                TextWrapping = TextWrapping.Wrap
            });
        }

        private static TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = WpfItcmDialogService.BrushFromRgb(100, 116, 139),
                Margin = new Thickness(0, 3, 0, 3)
            };
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            return needles.Any(needle => !string.IsNullOrWhiteSpace(needle) && text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    public sealed class WpfEscalationOverrideDialog : Window
    {
        private readonly int _defaultSupDays;
        private readonly int _defaultMgrDays;
        private readonly bool _allowClear;
        private readonly ComboBox _supCombo;
        private readonly ComboBox _mgrCombo;
        private readonly TextBox _reasonBox;
        private readonly TextBlock _previewText;
        private readonly TextBlock _validationText;
        private readonly Button _saveButton;
        private readonly Button _clearButton;

        public bool ClearRequested { get; private set; }
        public int DaysToSupervisor { get; private set; }
        public int DaysToManager { get; private set; }
        public string Reason { get; private set; }

        public WpfEscalationOverrideDialog(
            CallEscalationSettingsItem settings,
            CallTicketEscalationOverrideItem existingOverride,
            bool allowClear = true,
            string headerText = null,
            string windowTitle = null)
        {
            _defaultSupDays = Math.Max(1, settings?.DaysToSupervisor ?? 2);
            _defaultMgrDays = Math.Max(_defaultSupDays, settings?.DaysToManager ?? 3);
            _allowClear = allowClear;

            Title = string.IsNullOrWhiteSpace(windowTitle) ? "Escalation Override" : windowTitle.Trim();
            Width = 600;
            Height = 560;
            MinWidth = 520;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            Background = WpfItcmDialogService.BrushFromRgb(245, 247, 250);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var header = new Border { Background = Brushes.White, Padding = new Thickness(24, 18, 24, 16) };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(headerText) ? "Override escalation" : headerText.Trim(),
                FontSize = 23,
                FontWeight = FontWeights.SemiBold,
                Foreground = WpfItcmDialogService.BrushFromRgb(15, 23, 42)
            });
            headerStack.Children.Add(WpfItcmDialogService.CreateMutedText("Set ticket-specific escalation timing. A reason is required and will be saved in ticket history."));
            header.Child = headerStack;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var body = new StackPanel { Margin = new Thickness(24) };
            var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var timing = new StackPanel();
            timing.Children.Add(WpfItcmDialogService.CreateSectionTitle("Escalation Timing"));
            timing.Children.Add(WpfItcmDialogService.CreateMutedText($"System defaults: Supervisor {_defaultSupDays} day(s), Manager {_defaultMgrDays} day(s)"));
            _supCombo = CreateDayCombo(_defaultSupDays);
            _mgrCombo = CreateDayCombo(_defaultMgrDays);
            AddCombo(timing, "Escalate to Supervisor after", _supCombo);
            AddCombo(timing, "Escalate to Manager after", _mgrCombo);
            _supCombo.SelectedIndex = 0;
            _mgrCombo.SelectedIndex = 0;
            _supCombo.SelectionChanged += (_, __) =>
            {
                if (_supCombo.SelectedItem == null) return;
                var mgr = _mgrCombo.SelectedItem is int m ? m : _defaultMgrDays;
                var sup = (int)_supCombo.SelectedItem;
                if (mgr < sup)
                    _mgrCombo.SelectedItem = sup;
                RefreshValidation();
            };
            _mgrCombo.SelectionChanged += (_, __) => RefreshValidation();
            _previewText = WpfItcmDialogService.CreateMutedText(string.Empty);
            _previewText.Margin = new Thickness(0, 8, 0, 0);
            timing.Children.Add(_previewText);
            var timingCard = WpfItcmDialogService.CreateCard();
            timingCard.Child = timing;
            body.Children.Add(timingCard);

            var reason = new StackPanel();
            reason.Children.Add(WpfItcmDialogService.CreateSectionTitle("Justification"));
            reason.Children.Add(WpfItcmDialogService.CreateMutedText("Required. Include what changed, such as vendor delay, part ordering, or schedule."));
            _reasonBox = WpfItcmDialogService.CreateTextBox(existingOverride?.Reason ?? string.Empty, 120);
            _reasonBox.AcceptsReturn = true;
            _reasonBox.MaxLength = 400;
            _reasonBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _reasonBox.Margin = new Thickness(0, 8, 0, 0);
            _reasonBox.TextChanged += (_, __) => RefreshValidation();
            reason.Children.Add(_reasonBox);
            _validationText = new TextBlock
            {
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = WpfItcmDialogService.BrushFromRgb(185, 28, 28),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            reason.Children.Add(_validationText);
            var reasonCard = WpfItcmDialogService.CreateCard();
            reasonCard.Child = reason;
            body.Children.Add(reasonCard);

            var sup = existingOverride?.DaysToSupervisor ?? _defaultSupDays;
            var mgr = existingOverride?.DaysToManager ?? _defaultMgrDays;
            _supCombo.SelectedItem = Math.Max(_defaultSupDays, Math.Min(30, sup));
            _mgrCombo.SelectedItem = Math.Max(_defaultMgrDays, Math.Min(30, mgr));

            var footer = WpfItcmDialogService.CreateFooter();
            _clearButton = WpfItcmDialogService.CreateButton("Clear Override", WpfItcmDialogService.BrushFromRgb(220, 38, 38));
            _clearButton.Margin = new Thickness(22, 0, 8, 0);
            _clearButton.Visibility = _allowClear ? Visibility.Visible : Visibility.Collapsed;
            _clearButton.Click += (_, __) => SaveAndClose(clear: true);
            DockPanel.SetDock(_clearButton, Dock.Left);
            footer.Children.Add(_clearButton);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 12, 20, 12)
            };
            var cancel = WpfItcmDialogService.CreateButton("Cancel", WpfItcmDialogService.BrushFromRgb(100, 116, 139));
            cancel.IsCancel = true;
            cancel.Click += (_, __) => DialogResult = false;
            _saveButton = WpfItcmDialogService.CreateButton("Save", WpfItcmDialogService.BrushFromRgb(37, 99, 235));
            _saveButton.IsDefault = true;
            _saveButton.Click += (_, __) => SaveAndClose(clear: false);
            buttons.Children.Add(cancel);
            buttons.Children.Add(_saveButton);
            DockPanel.SetDock(buttons, Dock.Right);
            footer.Children.Add(buttons);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            Loaded += (_, __) =>
            {
                _reasonBox.Focus();
                _reasonBox.CaretIndex = _reasonBox.Text.Length;
                RefreshValidation();
            };
        }

        private void SaveAndClose(bool clear)
        {
            if (!IsValid(out var message))
            {
                ShowValidation(message);
                return;
            }

            if (clear && !WpfItcmDialogService.Confirm(this, "This will remove the escalation override for this ticket and revert to system defaults.", "Clear Override", "Clear Override", destructive: true))
                return;

            ClearRequested = clear;
            DaysToSupervisor = (int)_supCombo.SelectedItem;
            DaysToManager = (int)_mgrCombo.SelectedItem;
            Reason = (_reasonBox.Text ?? string.Empty).Trim();
            DialogResult = true;
            Close();
        }

        private void RefreshValidation()
        {
            if (_saveButton == null)
                return;

            var sup = _supCombo.SelectedItem is int s ? s : _defaultSupDays;
            var mgr = _mgrCombo.SelectedItem is int m ? m : _defaultMgrDays;
            _previewText.Text = $"Preview: Supervisor {_defaultSupDays} -> {sup} day(s), Manager {_defaultMgrDays} -> {mgr} day(s).";

            if (IsValid(out var message))
            {
                _validationText.Visibility = Visibility.Collapsed;
                _saveButton.IsEnabled = true;
                _clearButton.IsEnabled = true;
            }
            else
            {
                ShowValidation(message);
                _saveButton.IsEnabled = false;
                _clearButton.IsEnabled = false;
            }
        }

        private bool IsValid(out string message)
        {
            if (string.IsNullOrWhiteSpace(_reasonBox.Text))
            {
                message = "Reason is required.";
                return false;
            }

            var sup = _supCombo.SelectedItem is int s ? s : _defaultSupDays;
            var mgr = _mgrCombo.SelectedItem is int m ? m : _defaultMgrDays;
            if (mgr < sup)
            {
                message = "Manager days must be greater than or equal to Supervisor days.";
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

        private static ComboBox CreateDayCombo(int minimum)
        {
            return new ComboBox
            {
                ItemsSource = Enumerable.Range(minimum, 31 - minimum).ToList(),
                Width = 130,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 10)
            };
        }

        private static void AddCombo(Panel parent, string label, ComboBox combo)
        {
            parent.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = WpfItcmDialogService.BrushFromRgb(100, 116, 139),
                Margin = new Thickness(0, 10, 0, 2)
            });
            parent.Children.Add(combo);
        }
    }

    public sealed class WpfTicketSummaryDialog : Window
    {
        public WpfTicketSummaryDialog(
            CallTicketListItem ticket,
            string slaLabel,
            string slaTooltip,
            string escalationText,
            string lastActivityText,
            string idleText)
        {
            if (ticket == null) throw new ArgumentNullException(nameof(ticket));

            Title = "Ticket Summary";
            Width = 900;
            Height = 620;
            MinWidth = 720;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            Background = WpfItcmDialogService.BrushFromRgb(245, 247, 250);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var header = new Border { Background = Brushes.White, Padding = new Thickness(24, 18, 24, 16) };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(ticket.TicketCode) ? "Ticket #" + ticket.TicketId : ticket.TicketCode.Trim(),
                FontSize = 23,
                FontWeight = FontWeights.SemiBold,
                Foreground = WpfItcmDialogService.BrushFromRgb(15, 23, 42)
            });
            var chips = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            chips.Children.Add(WpfItcmDialogService.CreatePill(ticket.Status ?? "-", StatusBrush(ticket.Status)));
            chips.Children.Add(WpfItcmDialogService.CreatePill(ticket.Priority ?? "-", PriorityBrush(ticket.Priority)));
            chips.Children.Add(WpfItcmDialogService.CreatePill(string.IsNullOrWhiteSpace(slaLabel) ? "SLA -" : slaLabel, WpfItcmDialogService.BrushFromRgb(14, 116, 144)));
            foreach (FrameworkElement child in chips.Children)
                child.Margin = new Thickness(0, 0, 8, 0);
            headerStack.Children.Add(chips);
            header.Child = headerStack;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var body = new StackPanel { Margin = new Thickness(24) };
            var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var overview = new StackPanel();
            overview.Children.Add(WpfItcmDialogService.CreateSectionTitle("Overview"));
            AddDetail(overview, "Issue", ticket.Issue);
            AddDetail(overview, "Assigned To", string.IsNullOrWhiteSpace(ticket.ResponsiblePerson) ? "(Unassigned)" : ticket.ResponsiblePerson);
            AddDetail(overview, "Caller", ticket.CallerName);
            AddDetail(overview, "Company", ticket.Company);
            AddDetail(overview, "Department / Branch", (ticket.Department ?? "-") + " / " + (ticket.Branch ?? "-"));
            var overviewCard = WpfItcmDialogService.CreateCard();
            overviewCard.Child = overview;
            body.Children.Add(overviewCard);

            var timing = new StackPanel();
            timing.Children.Add(WpfItcmDialogService.CreateSectionTitle("Timing and SLA"));
            AddDetail(timing, "Created", AppTime.ToLocalString(ticket.CreatedAt, "g"));
            AddDetail(timing, "Updated", AppTime.ToLocalString(ticket.UpdatedAt, "g"));
            AddDetail(timing, "Last Contact", ticket.LastContactAt.HasValue ? AppTime.ToLocalString(ticket.LastContactAt.Value, "g") : "-");
            AddDetail(timing, "Age", ticket.TicketAgeDays + " day(s)");
            AddDetail(timing, "Last Activity", lastActivityText);
            AddDetail(timing, "Idle", idleText);
            AddDetail(timing, "SLA", string.IsNullOrWhiteSpace(slaTooltip) ? slaLabel : slaLabel + " - " + slaTooltip);
            AddDetail(timing, "Escalation", escalationText);
            var timingCard = WpfItcmDialogService.CreateCard();
            timingCard.Child = timing;
            body.Children.Add(timingCard);

            var footer = WpfItcmDialogService.CreateFooter();
            var close = WpfItcmDialogService.CreateButton("Close", WpfItcmDialogService.BrushFromRgb(37, 99, 235));
            close.IsDefault = true;
            close.Click += (_, __) => Close();
            close.Margin = new Thickness(8, 12, 22, 12);
            DockPanel.SetDock(close, Dock.Right);
            footer.Children.Add(close);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
        }

        private static void AddDetail(Panel parent, string label, string value)
        {
            parent.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = WpfItcmDialogService.BrushFromRgb(100, 116, 139),
                Margin = new Thickness(0, 8, 0, 2)
            });
            parent.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(value) ? "-" : value.Trim(),
                FontSize = 13,
                Foreground = WpfItcmDialogService.BrushFromRgb(15, 23, 42),
                TextWrapping = TextWrapping.Wrap
            });
        }

        private static Brush StatusBrush(string status)
        {
            var s = (status ?? string.Empty).Trim().ToLowerInvariant();
            if (s.Contains("closed") || s.Contains("solved")) return WpfItcmDialogService.BrushFromRgb(22, 163, 74);
            if (s.Contains("escalated")) return WpfItcmDialogService.BrushFromRgb(234, 88, 12);
            if (s.Contains("progress")) return WpfItcmDialogService.BrushFromRgb(37, 99, 235);
            return WpfItcmDialogService.BrushFromRgb(100, 116, 139);
        }

        private static Brush PriorityBrush(string priority)
        {
            var p = (priority ?? string.Empty).Trim().ToLowerInvariant();
            if (p == "critical") return WpfItcmDialogService.BrushFromRgb(190, 18, 60);
            if (p == "high") return WpfItcmDialogService.BrushFromRgb(234, 88, 12);
            if (p == "medium") return WpfItcmDialogService.BrushFromRgb(37, 99, 235);
            return WpfItcmDialogService.BrushFromRgb(22, 163, 74);
        }
    }

    public sealed class WpfDashboardDrillDownDialog : Window
    {
        public int? RequestedTicketId { get; private set; }

        public WpfDashboardDrillDownDialog(string title, IEnumerable<CallTicketListItem> tickets)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Ticket Drill Down" : title.Trim();
            Width = 1050;
            Height = 700;
            MinWidth = 800;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            Background = WpfItcmDialogService.BrushFromRgb(245, 247, 250);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var header = new Border { Background = Brushes.White, Padding = new Thickness(24, 18, 24, 16) };
            header.Child = new TextBlock
            {
                Text = Title,
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = WpfItcmDialogService.BrushFromRgb(15, 23, 42)
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                Margin = new Thickness(24),
                SelectionMode = DataGridSelectionMode.Single,
                ItemsSource = (tickets ?? Enumerable.Empty<CallTicketListItem>()).ToList()
            };
            grid.Columns.Add(new DataGridTextColumn { Header = "Ticket", Binding = new Binding("TicketCode"), Width = 120 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Priority", Binding = new Binding("Priority"), Width = 100 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new Binding("Status"), Width = 130 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Issue", Binding = new Binding("Issue"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Assigned To", Binding = new Binding("ResponsiblePerson"), Width = 160 });
            grid.MouseDoubleClick += (_, __) =>
            {
                if (grid.SelectedItem is CallTicketListItem item && item.TicketId > 0)
                {
                    RequestedTicketId = item.TicketId;
                    DialogResult = true;
                    Close();
                }
            };
            Grid.SetRow(grid, 1);
            root.Children.Add(grid);

            var footer = WpfItcmDialogService.CreateFooter();
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 12, 20, 12)
            };
            var open = WpfItcmDialogService.CreateButton("Open Ticket", WpfItcmDialogService.BrushFromRgb(37, 99, 235));
            open.Click += (_, __) =>
            {
                if (grid.SelectedItem is CallTicketListItem item && item.TicketId > 0)
                {
                    RequestedTicketId = item.TicketId;
                    DialogResult = true;
                    Close();
                }
            };
            var close = WpfItcmDialogService.CreateButton("Close", WpfItcmDialogService.BrushFromRgb(100, 116, 139));
            close.IsCancel = true;
            close.Click += (_, __) => DialogResult = false;
            buttons.Children.Add(close);
            buttons.Children.Add(open);
            footer.Children.Add(buttons);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
        }
    }

    public sealed class WpfEmailLogShortcutDialog : Window
    {
        private readonly ICallMonitoringRepository _repository;
        private readonly int _ticketId;
        private readonly TextBlock _statusText;
        private readonly DataGrid _grid;

        public WpfEmailLogShortcutDialog(ICallMonitoringRepository repository, int ticketId, string ticketCode)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _ticketId = ticketId;

            Title = "Ticket Email Log";
            Width = 1060;
            Height = 680;
            MinWidth = 860;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            Background = WpfItcmDialogService.BrushFromRgb(245, 247, 250);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var header = new Border { Background = Brushes.White, Padding = new Thickness(24, 18, 24, 16) };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = "Email Log - " + (string.IsNullOrWhiteSpace(ticketCode) ? "Ticket #" + ticketId : ticketCode.Trim()),
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = WpfItcmDialogService.BrushFromRgb(15, 23, 42)
            });
            _statusText = WpfItcmDialogService.CreateMutedText("Loading email activity...");
            _statusText.Margin = new Thickness(0, 6, 0, 0);
            headerStack.Children.Add(_statusText);
            header.Child = headerStack;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                Margin = new Thickness(24),
                SelectionMode = DataGridSelectionMode.Single
            };
            _grid.Columns.Add(new DataGridTextColumn { Header = "Date Sent", Binding = new Binding("DateSent") { StringFormat = "g" }, Width = 140 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Type", Binding = new Binding("EmailType"), Width = 120 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new Binding("Status"), Width = 100 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Recipient", Binding = new Binding("Recipient"), Width = 210 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Subject", Binding = new Binding("Subject"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Error", Binding = new Binding("ErrorMessage"), Width = 220 });
            Grid.SetRow(_grid, 1);
            root.Children.Add(_grid);

            var footer = WpfItcmDialogService.CreateFooter();
            var close = WpfItcmDialogService.CreateButton("Close", WpfItcmDialogService.BrushFromRgb(37, 99, 235));
            close.IsDefault = true;
            close.Click += (_, __) => Close();
            close.Margin = new Thickness(8, 12, 22, 12);
            DockPanel.SetDock(close, Dock.Right);
            footer.Children.Add(close);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            Loaded += async (_, __) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                var rows = await _repository.GetEmailLogPageAsync(_ticketId.ToString(), null, 0, 250) ?? new List<CallEmailLogItem>();
                _grid.ItemsSource = rows;
                _statusText.Text = rows.Count == 0
                    ? "No email log entries were found for this ticket."
                    : $"{rows.Count:N0} email log entr{(rows.Count == 1 ? "y" : "ies")} found.";
            }
            catch (Exception ex)
            {
                _statusText.Text = "Unable to load email log.";
                WpfItcmDialogService.ShowError(this, ex.Message, "Email Log");
            }
        }
    }
}
