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
            Title = "Yakult system updates"; Width = 540; Height = 420; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Brushes.White;
            FontFamily = new FontFamily("Segoe UI"); Foreground = new SolidColorBrush(Color.FromRgb(32, 38, 53));
            var panel = new StackPanel { Margin = new Thickness(28) }; Content = panel;
            panel.Children.Add(new TextBlock { Text = completion == null ? "System updates" : "Update complete!", FontSize = 25, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(185, 35, 53)) });
            panel.Children.Add(new TextBlock { Text = "Installed version " + _updates.InstalledVersion, FontSize = 12, Margin = new Thickness(0, 7, 0, 16), Foreground = Brushes.DimGray });
            _status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 14 };
            panel.Children.Add(_status);
            _notes = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 12, 0, 12) };
            panel.Children.Add(new ScrollViewer { Content = _notes, Height = 105, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            _progress = new ProgressBar { Height = 8, Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 12) }; panel.Children.Add(_progress);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            _check = MakeButton("Check again"); _check.Click += async (_, __) => await _updates.CheckAsync(); buttons.Children.Add(_check);
            _install = MakeButton("Update now"); _install.Click += async (_, __) => await InstallAsync(); buttons.Children.Add(_install);
            _close = MakeButton("Close"); _close.Click += (_, __) => { _cancel?.Cancel(); Close(); }; buttons.Children.Add(_close); panel.Children.Add(buttons);
            _updates.Changed += Refresh;
            Closed += (_, __) => { _cancel?.Cancel(); if (!RestartRequested) _updates.CancelPendingInstall(); _updates.Changed -= Refresh; };
            Refresh();
            if (completion != null) { _status.Text = completion; _status.Foreground = Brushes.DarkGreen; _notes.Text = "You're ready to continue your work. Future releases will appear under Updates."; }
        }

        private static Button MakeButton(string text) => new Button { Content = text, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(5, 0, 0, 0), MinWidth = 80 };

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
