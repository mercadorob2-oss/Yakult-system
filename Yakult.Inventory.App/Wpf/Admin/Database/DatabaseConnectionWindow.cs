using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Security;

namespace Yakult.Inventory.App.Wpf.Admin.Database
{
    public sealed class DatabaseConnectionWindow : Window
    {
        private readonly string _defaultConnection;
        private readonly IDeveloperConnectionAuthorizer _authorizer;
        private readonly SecretField _server, _database, _username, _password;
        private readonly ComboBox _authentication;
        private readonly CheckBox _encrypt, _trustCertificate;
        private readonly Button _viewAll, _useDefault, _test, _save;
        private readonly TextBlock _status, _environment, _errorDetails, _visibilityHint;
        private readonly StackPanel _details;
        private readonly DataGrid _properties;
        private readonly TextBox _connectionString;
        private DatabaseConnectionProfile _profile;
        private CancellationTokenSource _testCancellation;
        private string _testedConnection, _lastError;
        private bool _loading, _revealed, _busy, _closed;

        public DatabaseConnectionWindow(bool initialSetup = false)
            : this(DatabaseConfig.ConnectionString, DatabaseConfig.DefaultConnectionString, DatabaseConfig.ConnectionSource,
                new DeveloperConnectionAuthorizer(string.IsNullOrWhiteSpace(DatabaseConfig.DefaultConnectionString) ? DatabaseConfig.ConnectionString : DatabaseConfig.DefaultConnectionString), initialSetup) { }

        public DatabaseConnectionWindow(string activeConnection, string defaultConnection, string source, IDeveloperConnectionAuthorizer authorizer, bool initialSetup = false)
        {
            _defaultConnection = defaultConnection; _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
            try { _profile = new DatabaseConnectionProfile(activeConnection); }
            catch (ArgumentException)
            {
                try { _profile = new DatabaseConnectionProfile(defaultConnection); }
                catch (ArgumentException) { _profile = new DatabaseConnectionProfile(null); }
            }
            Title = "Database connection"; Width = 780; Height = 720; MinWidth = 680; MinHeight = 570;
            MaxHeight = Math.Max(570, SystemParameters.WorkArea.Height - 24);
            WindowStartupLocation = WindowStartupLocation.CenterScreen; ShowInTaskbar = false;
            FontFamily = new FontFamily("Segoe UI"); FontSize = 13; Foreground = Ink; Background = ColorBrush("#F4F6FA");
            var root = new Grid { Background = Background }; Content = root;
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new StackPanel { Margin = new Thickness(28, 22, 28, 20) };
            header.Children.Add(new TextBlock { Text = "CONNECTION SETTINGS", Foreground = Accent, FontSize = 10, FontWeight = FontWeights.Bold });
            header.Children.Add(new TextBlock { Text = "Database connection", FontSize = 27, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 5, 0, 5) });
            header.Children.Add(new TextBlock { Text = "Review, test and save the database connection for this Windows user.", Foreground = Muted, TextWrapping = TextWrapping.Wrap });
            var badges = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            _environment = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            badges.Children.Add(new Border { Background = ColorBrush("#E9F4ED"), CornerRadius = new CornerRadius(5), Padding = new Thickness(9, 4, 9, 4), Child = _environment });
            badges.Children.Add(new TextBlock { Text = "Source: " + source, Foreground = Muted, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 11 });
            header.Children.Add(badges); root.Children.Add(header);
            var body = new StackPanel { Margin = new Thickness(28, 0, 28, 20) };
            var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetRow(scroll, 1); root.Children.Add(scroll);
            var card = new StackPanel(); body.Children.Add(Card(card));
            var heading = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 17) };
            _useDefault = MakeButton("Use default"); _useDefault.Click += (_, __) => LoadDefault(); DockPanel.SetDock(_useDefault, Dock.Right); heading.Children.Add(_useDefault);
            heading.Children.Add(new TextBlock { Text = "Connection properties", FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }); card.Children.Add(heading);
            _server = new SecretField(); _database = new SecretField(); _username = new SecretField(); _password = new SecretField();
            card.Children.Add(Pair(Field("Server address", _server), Field("Database name", _database)));
            _authentication = new ComboBox { ItemsSource = new[] { "SQL Server authentication", "Windows authentication" }, Height = 38, Padding = new Thickness(9, 7, 9, 7), VerticalContentAlignment = VerticalAlignment.Center };
            card.Children.Add(Field("Authentication", _authentication));
            card.Children.Add(Pair(Field("SQL username", _username), Field("SQL password", _password)));
            _encrypt = new CheckBox { Content = "Encrypt connection", Margin = new Thickness(0, 5, 20, 12) };
            _trustCertificate = new CheckBox { Content = "Trust server certificate", Margin = new Thickness(0, 5, 0, 12) };
            var options = new WrapPanel(); options.Children.Add(_encrypt); options.Children.Add(_trustCertificate); card.Children.Add(options);
            var revealBar = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 5, 0, 0) };
            _viewAll = MakeButton("View all"); _viewAll.Click += (_, __) => ToggleViewAll(); DockPanel.SetDock(_viewAll, Dock.Right); revealBar.Children.Add(_viewAll);
            _visibilityHint = new TextBlock { Text = "Values are masked. View all requires a developer's password.", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            revealBar.Children.Add(_visibilityHint); card.Children.Add(revealBar);
            _details = new StackPanel { Visibility = Visibility.Collapsed };
            var detailCard = Card(_details); detailCard.Visibility = Visibility.Collapsed; detailCard.Margin = new Thickness(0, 18, 0, 0); body.Children.Add(detailCard);
            _details.Children.Add(new TextBlock { Text = "All connection properties", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
            _properties = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserDeleteRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, BorderThickness = new Thickness(0), Height = 190, RowHeight = 30, AlternatingRowBackground = ColorBrush("#F7F9FC"), SelectionMode = DataGridSelectionMode.Single };
            _properties.Columns.Add(new DataGridTextColumn { Header = "Property", Binding = new Binding("Name"), Width = new DataGridLength(205) });
            _properties.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new Binding("Value"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 280 }); _details.Children.Add(_properties);
            _connectionString = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), FontSize = 11, Height = 78, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(9), Margin = new Thickness(0, 12, 0, 8) }; _details.Children.Add(_connectionString);
            var copy = MakeButton("Copy connection string"); copy.HorizontalAlignment = HorizontalAlignment.Left;
            copy.Click += (_, __) => { if (_revealed && !string.IsNullOrEmpty(_connectionString.Text)) { try { Clipboard.SetText(_connectionString.Text); } catch { SetStatus("The clipboard is busy. Please try again.", true); } } }; _details.Children.Add(copy);
            _errorDetails = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Accent, Margin = new Thickness(0, 10, 0, 0), FontSize = 11 }; _details.Children.Add(_errorDetails);
            var footer = new Grid { Margin = new Thickness(28, 12, 28, 20) }; Grid.SetRow(footer, 2); root.Children.Add(footer);
            footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _status = new TextBlock { Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) }; footer.Children.Add(_status);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; Grid.SetRow(buttons, 1); footer.Children.Add(buttons);
            var cancel = MakeButton("Cancel"); cancel.IsCancel = true; cancel.Click += (_, __) => Close(); buttons.Children.Add(cancel);
            _test = MakeButton("Test connection"); _test.Click += async (_, __) => await TestConnectionAsync(); buttons.Children.Add(_test);
            _save = MakeButton(initialSetup ? "Save & continue" : "Save & restart", true); _save.IsEnabled = false; _save.Click += (_, __) => Save(); buttons.Children.Add(_save);
            foreach (var field in new[] { _server, _database, _username, _password }) field.ValueChanged += Changed;
            _authentication.SelectionChanged += (_, __) => Changed();
            _encrypt.Checked += (_, __) => Changed(); _encrypt.Unchecked += (_, __) => Changed();
            _trustCertificate.Checked += (_, __) => Changed(); _trustCertificate.Unchecked += (_, __) => Changed();
            LoadProfile(_profile);
            Closed += (_, __) => { _closed = true; _testCancellation?.Cancel(); SetRevealed(false); foreach (var field in new[] { _server, _database, _username, _password }) field.Value = ""; };
        }

        private static Brush Ink => ColorBrush("#202A3C");
        private static Brush Muted => ColorBrush("#657187");
        private static Brush Accent => ColorBrush("#B7223C");
        private static Brush ColorBrush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        private static Border Card(UIElement child) => new Border { Background = Brushes.White, BorderBrush = ColorBrush("#E0E5ED"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(21), Child = child };
        private static StackPanel Field(string label, UIElement input)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 14) }; panel.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 0, 0, 6) }); panel.Children.Add(input); return panel;
        }
        private static Grid Pair(UIElement left, UIElement right)
        {
            var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.Children.Add(left); Grid.SetColumn(right, 2); grid.Children.Add(right); return grid;
        }
        internal static Button MakeButton(string text, bool primary = false) => new Button { Content = text, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(6, 0, 0, 0), MinHeight = 35, Background = primary ? Accent : Brushes.White, Foreground = primary ? Brushes.White : Ink, BorderBrush = primary ? Accent : ColorBrush("#CED6E2"), BorderThickness = new Thickness(1), Cursor = System.Windows.Input.Cursors.Hand };
        private void LoadProfile(DatabaseConnectionProfile profile)
        {
            _loading = true; _profile = profile; _server.Value = profile.Server; _database.Value = profile.Database; _username.Value = profile.Username; _password.Value = profile.Password;
            _authentication.SelectedIndex = profile.WindowsAuthentication ? 1 : 0; _encrypt.IsChecked = profile.Encrypt; _trustCertificate.IsChecked = profile.TrustServerCertificate; _loading = false; Changed();
        }
        private void LoadDefault()
        {
            if (_busy) return;
            try { LoadProfile(new DatabaseConnectionProfile(_defaultConnection)); SetStatus("Default loaded. Test this connection, then save to use it."); }
            catch (ArgumentException) { SetStatus("The default connection could not be read. Contact your developer.", true); }
        }
        private void Changed()
        {
            if (_loading || _closed) return;
            _testedConnection = null; _save.IsEnabled = false;
            _profile.Server = _server.Value; _profile.Database = _database.Value; _profile.Username = _username.Value; _profile.Password = _password.Value;
            _profile.WindowsAuthentication = _authentication.SelectedIndex == 1; _profile.Encrypt = _encrypt.IsChecked == true; _profile.TrustServerCertificate = _trustCertificate.IsChecked == true;
            _username.IsEnabled = _password.IsEnabled = !_profile.WindowsAuthentication && !_busy; _environment.Text = _profile.EnvironmentLabel;
            _environment.Foreground = ColorBrush(_profile.Database.Equals(DatabaseConnectionProfile.DummyDatabase, StringComparison.OrdinalIgnoreCase) ? "#2563B7" : _profile.Database.Equals(DatabaseConnectionProfile.DefaultDatabase, StringComparison.OrdinalIgnoreCase) ? "#237244" : "#996014");
            SetStatus("Test the connection before saving. Changes take effect after restart."); RefreshDetails();
        }
        private void ToggleViewAll()
        {
            if (_revealed) { SetRevealed(false); return; }
            var dialog = new DeveloperPasswordWindow(_authorizer) { Owner = this };
            if (dialog.ShowDialog() == true && !_closed) SetRevealed(true);
        }
        private void SetRevealed(bool revealed)
        {
            _revealed = revealed; foreach (var field in new[] { _server, _database, _username, _password }) field.SetRevealed(revealed);
            _visibilityHint.Text = revealed ? "Developer verified. Hide all or close this window to mask the values again." : "Values are masked. View all requires a developer's password.";
            _viewAll.Content = revealed ? "Hide all" : "View all"; _details.Visibility = revealed ? Visibility.Visible : Visibility.Collapsed; ((Border)_details.Parent).Visibility = _details.Visibility; RefreshDetails();
            if (revealed && IsLoaded) Dispatcher.BeginInvoke(new Action(() => { if (_revealed && !_closed) _details.BringIntoView(); }));
        }
        private void RefreshDetails()
        {
            if (!_revealed) { _properties.ItemsSource = null; _connectionString.Clear(); _errorDetails.Text = ""; return; }
            try { _properties.ItemsSource = _profile.GetAllProperties(); _connectionString.Text = _profile.BuildConnectionString(); }
            catch (ArgumentException) { _properties.ItemsSource = null; _connectionString.Text = "Complete the connection fields to view the full connection string."; }
            _errorDetails.Text = _lastError ?? "";
        }
        private void SetStatus(string message, bool error = false) { _status.Text = message; _status.Foreground = error ? Accent : Muted; }
        private void SetBusy(bool busy)
        {
            _busy = busy; _server.IsEnabled = _database.IsEnabled = _authentication.IsEnabled = _encrypt.IsEnabled = _trustCertificate.IsEnabled = _useDefault.IsEnabled = _test.IsEnabled = !busy;
            _username.IsEnabled = _password.IsEnabled = !busy && !_profile.WindowsAuthentication; _save.IsEnabled = !busy && _testedConnection != null;
        }
        private async Task TestConnectionAsync()
        {
            if (_busy) return;
            string selected; try { selected = _profile.BuildConnectionString(); } catch (ArgumentException ex) { SetStatus(ex.Message, true); return; }
            _testedConnection = null; _lastError = null; SetBusy(true); SetStatus("Testing connection…"); _testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            try
            {
                var builder = new SqlConnectionStringBuilder(selected) { ConnectTimeout = 8 };
                using (var connection = new SqlConnection(builder.ConnectionString))
                {
                    await connection.OpenAsync(_testCancellation.Token);
                    if (!connection.Database.Equals(_profile.Database, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The connected database does not match your selection.");
                }
                if (_closed) return; _testedConnection = selected; SetStatus("Connection verified. You can now save these settings."); _status.Foreground = ColorBrush("#237244");
            }
            catch (Exception ex) { if (_closed) return; _lastError = ex.Message; SetStatus("Connection failed. Check the connection details or ask a developer to View all.", true); }
            finally { _testCancellation.Dispose(); _testCancellation = null; if (!_closed) { SetBusy(false); RefreshDetails(); } }
        }
        private void Save()
        {
            if (_busy || _testedConnection == null || _testedConnection != _profile.BuildConnectionString()) return;
            var settings = Properties.Settings.Default; var previous = settings.DbConnectionString;
            try { settings.DbConnectionString = _testedConnection; settings.Save(); DialogResult = true; }
            catch { settings.DbConnectionString = previous; SetStatus("The settings could not be saved. Please try again.", true); }
        }
        private sealed class SecretField : Grid
        {
            private readonly PasswordBox _masked = new PasswordBox { BorderThickness = new Thickness(0), Padding = new Thickness(10, 8, 10, 8), FontSize = 13 };
            private readonly TextBox _plain = new TextBox { BorderThickness = new Thickness(0), Padding = new Thickness(10, 8, 10, 8), FontSize = 13, Visibility = Visibility.Collapsed };
            private bool _revealed, _syncing;
            public event Action ValueChanged;
            public SecretField()
            {
                Height = 38; var inner = new Grid(); inner.Children.Add(_masked); inner.Children.Add(_plain);
                Children.Add(new Border { BorderBrush = ColorBrush("#D7DFEA"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Background = Brushes.White, Child = inner });
                _masked.PasswordChanged += (_, __) => { if (!_syncing) ValueChanged?.Invoke(); }; _plain.TextChanged += (_, __) => { if (!_syncing) ValueChanged?.Invoke(); };
            }
            public string Value { get => _revealed ? _plain.Text : _masked.Password; set { _syncing = true; if (_revealed) _plain.Text = value ?? ""; else _masked.Password = value ?? ""; _syncing = false; } }
            public void SetRevealed(bool revealed)
            {
                var value = Value; _syncing = true; _revealed = revealed; _plain.Text = revealed ? value : ""; _masked.Password = revealed ? "" : value;
                _plain.Visibility = revealed ? Visibility.Visible : Visibility.Collapsed; _masked.Visibility = revealed ? Visibility.Collapsed : Visibility.Visible; _syncing = false;
            }
        }
    }
}
