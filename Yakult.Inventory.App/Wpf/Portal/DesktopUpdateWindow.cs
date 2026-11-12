using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Wpf.Portal
{
    public sealed class DesktopUpdateWindow : Window
    {
        private readonly DesktopUpdateService _updates = DesktopUpdateService.Instance;
        private readonly TextBlock _status, _notes;
        private readonly Button _install, _check, _close;
        private readonly ProgressBar _progress;
        private CancellationTokenSource _cancel;
        private bool _busy;
        public bool RestartRequested { get; private set; }

        public DesktopUpdateWindow(string completion = null)
        {
            Title = "Yakult system updates"; Width = 600; Height = 580; MinWidth = 560; MinHeight = 520; ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Brush("#F5F6F8");
            FontFamily = new FontFamily("Segoe UI"); Foreground = Brush("#202635");

            var root = new Grid { Margin = new Thickness(24) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var heading = new StackPanel { Margin = new Thickness(2, 0, 0, 18) };
            heading.Children.Add(new TextBlock { Text = "SOFTWARE UPDATES", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brush("#737B8C") });
            heading.Children.Add(new TextBlock { Text = completion == null ? "Keep Yakult up to date" : "Update complete", FontSize = 25, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 5, 0, 0) });
            heading.Children.Add(new TextBlock { Text = "Review the installed version and release details before continuing.", FontSize = 12, Foreground = Brush("#677081"), Margin = new Thickness(0, 4, 0, 0) });
            root.Children.Add(heading);

            var body = new StackPanel(); Grid.SetRow(body, 1); root.Children.Add(body);
            var statusCard = new Border { Background = Brushes.White, BorderBrush = Brush("#E5E6E8"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 14) };
            var statusLayout = new Grid(); statusLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); statusLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var statusStack = new StackPanel();
            statusStack.Children.Add(new TextBlock { Text = "UPDATE STATUS", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brush("#737B8C") });
            _status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 14, FontWeight = FontWeights.Medium, Margin = new Thickness(0, 7, 12, 0) };
            statusStack.Children.Add(_status); statusLayout.Children.Add(statusStack);
            var versionBadge = new Border { Background = Brush("#F7E9EB"), CornerRadius = new CornerRadius(7), Padding = new Thickness(11, 8, 11, 8), VerticalAlignment = VerticalAlignment.Center };
            versionBadge.Child = new TextBlock { Text = "Installed  " + _updates.InstalledVersion, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brush("#9F2434") };
            Grid.SetColumn(versionBadge, 1); statusLayout.Children.Add(versionBadge); statusCard.Child = statusLayout; body.Children.Add(statusCard);

            var notesCard = new Border { Background = Brushes.White, BorderBrush = Brush("#E5E6E8"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(18), MinHeight = 150 };
            var notesLayout = new DockPanel();
            var notesHeading = new TextBlock { Text = "RELEASE NOTES", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brush("#737B8C"), Margin = new Thickness(0, 0, 0, 10) };
            DockPanel.SetDock(notesHeading, Dock.Top); notesLayout.Children.Add(notesHeading);
            _notes = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 19, Foreground = Brush("#434B5A") };
            notesLayout.Children.Add(new ScrollViewer { Content = _notes, Height = 195, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            notesCard.Child = notesLayout; body.Children.Add(notesCard);

            _progress = new ProgressBar { Height = 7, Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed, Margin = new Thickness(2, 14, 2, 0) }; body.Children.Add(_progress);
            var footer = new DockPanel { Margin = new Thickness(0, 18, 0, 0), LastChildFill = false }; Grid.SetRow(footer, 2); root.Children.Add(footer);
            var hint = new TextBlock { Text = "Save your work before installing.", FontSize = 11, Foreground = Brush("#737B8C"), VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(hint, Dock.Left); footer.Children.Add(hint);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            _check = MakeButton("Check again"); _check.Click += async (_, __) => await _updates.CheckAsync(); buttons.Children.Add(_check);
            _install = MakeButton("Update now"); _install.Background = Brush("#B92335"); _install.Foreground = Brushes.White; _install.BorderBrush = Brush("#B92335"); _install.Click += async (_, __) => await InstallAsync(); buttons.Children.Add(_install);
            _close = MakeButton("Close"); _close.Click += (_, __) => { _cancel?.Cancel(); Close(); }; buttons.Children.Add(_close);
            DockPanel.SetDock(buttons, Dock.Right); footer.Children.Add(buttons);
            Content = root;
            _updates.Changed += Refresh;
            Closed += (_, __) => { _cancel?.Cancel(); if (!RestartRequested) _updates.CancelPendingInstall(); _updates.Changed -= Refresh; };
            Refresh();
            if (completion != null) { _status.Text = completion; _status.Foreground = Brushes.DarkGreen; _notes.Text = "You're ready to continue your work. Future releases will appear under Updates."; }
        }

        private static Button MakeButton(string text) => new Button { Content = text, Padding = new Thickness(13, 8, 13, 8), Margin = new Thickness(6, 0, 0, 0), MinWidth = 88, FontSize = 12, FontWeight = FontWeights.SemiBold, Background = Brushes.White, Foreground = Brush("#434B5A"), BorderBrush = Brush("#D9DCE2"), BorderThickness = new Thickness(1), Cursor = System.Windows.Input.Cursors.Hand };

        private static Brush Brush(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

        private void Refresh()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(Refresh)); return; }
            if (_busy) return;
            var state = _updates.State;
            _status.Text = state.Message;
            _notes.Text = state.Release?.ReleaseNotes ?? "Updates install after Yakult closes. Save your work before updating.";
            _install.Visibility = state.Release == null ? Visibility.Collapsed : Visibility.Visible;
            _check.IsEnabled = !state.Checking;
        }

        private async System.Threading.Tasks.Task InstallAsync()
        {
            var release = _updates.State.Release;
            if (release == null || _busy) return;
            if (MessageBox.Show(this, "Save your work before continuing. Yakult will close, install version " + release.Version + ", and reopen. Continue?", "Update Yakult", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;
            _busy = true; _install.IsEnabled = false; _check.IsEnabled = false; _close.Content = "Cancel";
            _cancel = new CancellationTokenSource(TimeSpan.FromMinutes(15)); _progress.Visibility = Visibility.Visible;
            string folder = null;
            try
            {
                _status.Text = "Downloading update…";
                folder = await _updates.DownloadAsync(release, new Progress<int>(percent => { if (IsLoaded) { _progress.Value = percent; _status.Text = "Downloading update… " + percent + "%"; } }), _cancel.Token);
                _cancel.Token.ThrowIfCancellationRequested();
                _status.Text = "Download verified. Preparing the installer…"; _progress.IsIndeterminate = true;
                await _updates.StartInstallerAsync(folder, release, _cancel.Token);
                RestartRequested = true; DialogResult = true;
            }
            catch (OperationCanceledException) { _updates.CancelPendingInstall(); if (IsLoaded) _status.Text = "Update cancelled. Your current version is unchanged."; }
            catch (Exception ex) { _updates.CancelPendingInstall(); if (IsLoaded) _status.Text = "Update could not start: " + ex.Message; }
            finally
            {
                _busy = false; _cancel.Dispose(); _cancel = null;
                if (!RestartRequested && folder != null) Yakult.DesktopUpdates.UpdateCache.Complete(folder);
                if (IsLoaded) { _progress.IsIndeterminate = false; _progress.Visibility = Visibility.Collapsed; _install.IsEnabled = true; _check.IsEnabled = true; _close.Content = "Close"; }
            }
        }
    }
}
