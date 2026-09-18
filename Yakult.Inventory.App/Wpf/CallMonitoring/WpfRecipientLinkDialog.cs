using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    /// <summary>
    /// Native WPF recipient editor for a department or branch (replaces the
    /// WinForms SmtpProfileLinkDialog family). Recipients-only: branches and
    /// departments are notification targets — the single sender lives in
    /// Setup (dbo.CallEmailSettings). Unlike its predecessor, this dialog
    /// prefills the existing addresses so editing never wipes them.
    /// </summary>
    public sealed class WpfRecipientLinkDialog : Window
    {
        private readonly string _targetNoun;
        private readonly ComboBox _cboTarget;
        private readonly TextBox _txtNotify;
        private readonly TextBox _txtEscalate;

        public int SelectedTargetId { get; private set; }
        public string RecipientEmails { get; private set; } = string.Empty;
        public string EscalationEmails { get; private set; } = string.Empty;

        public WpfRecipientLinkDialog(
            IList<LookupItem> targets,
            string targetNoun,
            int? preselectedTargetId,
            string notifyPrefill,
            string escalationPrefill)
        {
            _targetNoun = string.IsNullOrWhiteSpace(targetNoun) ? "Target" : targetNoun.Trim();

            Title = _targetNoun + " Recipients";
            Width = 560;
            Height = 430;
            MinWidth = 480;
            MinHeight = 380;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
            FontFamily = new FontFamily("Segoe UI");

            var root = new DockPanel { LastChildFill = true };
            Content = root;

            // Header
            var header = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(24, 16, 24, 16)
            };
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            var headerStack = new StackPanel();
            header.Child = headerStack;
            headerStack.Children.Add(new TextBlock
            {
                Text = _targetNoun + " Recipients",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Notify and escalation addresses for this " + _targetNoun.ToLowerInvariant() + ". Delivery uses the single Setup sender.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });

            // Footer
            var footer = new DockPanel
            {
                Background = Brushes.White,
                LastChildFill = false,
                Height = 60
            };
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            var saveBtn = new Button
            {
                Content = "Save",
                MinWidth = 110,
                Height = 34,
                Margin = new Thickness(0, 0, 12, 0),
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            saveBtn.Click += (_, __) => SaveAndClose();
            var cancelBtn = new Button
            {
                Content = "Cancel",
                MinWidth = 100,
                Height = 34,
                Margin = new Thickness(0, 0, 16, 0),
                Background = Brushes.White,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                IsCancel = true
            };
            var footerActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            footerActions.Children.Add(saveBtn);
            footerActions.Children.Add(cancelBtn);
            DockPanel.SetDock(footerActions, Dock.Right);
            footer.Children.Add(footerActions);

            // Body
            var body = new StackPanel { Margin = new Thickness(24, 16, 24, 16) };
            root.Children.Add(body);

            body.Children.Add(FieldLabel("Select " + _targetNoun));
            _cboTarget = new ComboBox
            {
                DisplayMemberPath = "Name",
                SelectedValuePath = "Id",
                MinHeight = 32,
                Padding = new Thickness(8, 4, 8, 4),
                Background = Brushes.White,
                Margin = new Thickness(0, 0, 0, 12)
            };
            _cboTarget.ItemsSource = (targets ?? new List<LookupItem>()).OrderBy(t => t.Name).ToList();
            if (preselectedTargetId.HasValue)
            {
                try { _cboTarget.SelectedValue = preselectedTargetId.Value; } catch { }
            }
            body.Children.Add(_cboTarget);

            body.Children.Add(FieldLabel("Notify To (emails separated by ;)"));
            _txtNotify = new TextBox
            {
                MinHeight = 32,
                Padding = new Thickness(8, 6, 8, 6),
                Background = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 70,
                Margin = new Thickness(0, 0, 0, 12),
                Text = notifyPrefill ?? string.Empty
            };
            body.Children.Add(_txtNotify);

            body.Children.Add(FieldLabel("Escalate To (emails separated by ;)"));
            _txtEscalate = new TextBox
            {
                MinHeight = 32,
                Padding = new Thickness(8, 6, 8, 6),
                Background = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 70,
                Margin = new Thickness(0, 0, 0, 4),
                Text = escalationPrefill ?? string.Empty
            };
            body.Children.Add(_txtEscalate);
            body.Children.Add(new TextBlock
            {
                Text = "Leave Escalate To blank to use the Notify To addresses on escalation.",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                TextWrapping = TextWrapping.Wrap
            });
        }

        private static TextBlock FieldLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                Margin = new Thickness(0, 0, 0, 4)
            };
        }

        private void SaveAndClose()
        {
            if (!(_cboTarget.SelectedValue is int targetId) || targetId <= 0)
            {
                MessageBox.Show(this, _targetNoun + " is required.", _targetNoun + " Recipients", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedTargetId = targetId;
            RecipientEmails = (_txtNotify.Text ?? string.Empty).Trim();
            EscalationEmails = (_txtEscalate.Text ?? string.Empty).Trim();
            DialogResult = true;
        }
    }
}
