using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Wpf.Portal
{
    public sealed class WpfDeviceMessengerWindow : Window
    {
        private static WpfDeviceMessengerWindow _open;
        private readonly DeviceMessengerRepository _repository = new DeviceMessengerRepository();
        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        private readonly int _sessionUserId;
        private readonly Dictionary<string,string> _drafts = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        private List<MessengerDevice> _devices = new List<MessengerDevice>();
        private MessengerDevice _selected;
        private bool _refreshing, _refreshAgain, _renderingDevices, _sending, _closed;
        private string _renderedMessages = "";
        private TextBox _search, _draft;
        private CheckBox _activeOnly;
        private ListBox _deviceList;
        private StackPanel _messages;
        private ScrollViewer _messageScroll;
        private TextBlock _deviceCount, _peerTitle, _peerStatus, _status, _count;
        private Button _send;
        private TextBlock _draftHint, _peerInitials;
        private ComboBox _popupSize;
        private CheckBox _hidePopupPreview, _compactWindow;
        private ColumnDefinition _sidebarColumn;
        private Border _presenceDot;
        private static readonly Brush Ink = Brush("#202A3C"), Muted = Brush("#66758A"), Accent = Brush("#B92335");
        private static Brush Brush(string value) => (Brush)new BrushConverter().ConvertFromString(value);
        public static bool IsReadingMessages => _open != null && _open.IsVisible && _open.IsActive && _open.WindowState != WindowState.Minimized;
        public static void OpenMessenger(MessengerDevice peer = null)
        {
            if (!AppSession.IsLoggedIn) return;
            if (_open != null && _open._sessionUserId != AppSession.CurrentUserId) _open.Close();
            if (_open == null) _open = new WpfDeviceMessengerWindow();
            if (!_open.IsVisible) _open.Show();
            if (_open.WindowState == WindowState.Minimized) _open.WindowState = WindowState.Normal;
            _open.Activate();
            if (peer != null) _ = _open.SelectPeerAsync(peer);
        }
        public WpfDeviceMessengerWindow()
        {
            _sessionUserId = AppSession.CurrentUserId;
            Title = "Device Messenger"; Width = 1020; Height = 720; MinWidth = 760; MinHeight = 540;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Segoe UI"); Background = Brushes.White;
            BuildLayout();
            ApplyWindowSize();
            AppSession.SessionCleared += OnSessionCleared;
            Loaded += async (_,__) => { Services.ItcmPresenceReporter.Start("DeviceMessenger"); Services.DeviceMessengerNotifications.Instance.Start(); await RefreshAsync(); if (!_closed) _timer.Start(); };
            _timer.Tick += async (_,__) => await RefreshAsync();
            Closed += (_,__) => { _closed = true; _timer.Stop(); AppSession.SessionCleared -= OnSessionCleared; Services.ItcmPresenceReporter.Stop("DeviceMessenger"); if (ReferenceEquals(_open,this)) _open = null; };
        }
        private void OnSessionCleared() { if (!_closed) Dispatcher.BeginInvoke(new Action(Close)); }
        private static TextBlock Text(string value, double size = 13, Brush color = null) => new TextBlock
        { Text = value, FontSize = size, Foreground = color ?? Ink, TextWrapping = TextWrapping.Wrap };
        private static Button Button(string value, bool primary = false)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            var button = new Button { Content = value, Padding = new Thickness(16,10,16,10), Cursor = Cursors.Hand,
                Foreground = primary ? Brushes.White : Ink, Background = primary ? Accent : Brush("#EDF1F6"),
                Template = new ControlTemplate(typeof(Button)) { VisualTree = border } };
            button.MouseEnter += (_,__) => { if (button.IsEnabled) button.Opacity = .85; };
            button.MouseLeave += (_,__) => button.Opacity = button.IsEnabled ? 1 : .45;
            button.IsEnabledChanged += (_,__) => button.Opacity = button.IsEnabled ? 1 : .45;
            return button;
        }
        private static string Initials(string name)
        {
            var words = (name ?? "?").Split(new[] {' ','.','_','-'},StringSplitOptions.RemoveEmptyEntries);
            return words.Length == 0 ? "?" : (words[0].Substring(0,1) + (words.Length > 1 ? words[words.Length-1].Substring(0,1) : "")).ToUpperInvariant();
        }
        private static Border Avatar(string name, bool online, double size = 42)
        {
            var avatar = new Grid { Width = size, Height = size };
            var initials = Text(Initials(name),size/3,Accent); initials.FontWeight = FontWeights.SemiBold;
            initials.HorizontalAlignment = HorizontalAlignment.Center; initials.VerticalAlignment = VerticalAlignment.Center;
            avatar.Children.Add(new Border { CornerRadius = new CornerRadius(size/2), Background = Brush("#F8E7EB"), Child = initials });
            avatar.Children.Add(new Border { Width = 11, Height = 11, CornerRadius = new CornerRadius(6), Background = online ? Brush("#20A674") : Brush("#9CA8B8"),
                BorderBrush = Brushes.White, BorderThickness = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom });
            return new Border { Child = avatar, Margin = new Thickness(0,0,12,0) };
        }
        private static Style DeviceStyle()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty,new CornerRadius(12));
            border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.MarginProperty,new Thickness(11,12,11,12)); border.AppendChild(presenter);
            var template = new ControlTemplate(typeof(ListBoxItem)) { VisualTree = border };
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(Control.TemplateProperty,template));
            style.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.Transparent));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(Control.BackgroundProperty,Brush("#F2F4F8"))); style.Triggers.Add(hover);
            var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true }; selected.Setters.Add(new Setter(Control.BackgroundProperty,Brush("#FBECEF"))); style.Triggers.Add(selected);
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true }; focus.Setters.Add(new Setter(Control.BackgroundProperty,Brush("#FBECEF"))); style.Triggers.Add(focus);
            return style;
        }
        private void BuildLayout()
        {
            var root = new Grid { Background = Brushes.White };
            _sidebarColumn = new ColumnDefinition { Width = new GridLength(286) };
            root.ColumnDefinitions.Add(_sidebarColumn);
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) }); Content = root;
            var sidebar = new DockPanel { Margin = new Thickness(16,24,16,16) };
            root.Children.Add(new Border { Background = Brushes.White, BorderBrush = Brush("#E8ECF2"), BorderThickness = new Thickness(0,0,1,0), Child = sidebar });
            var sideHeader = new StackPanel(); DockPanel.SetDock(sideHeader,Dock.Top); sidebar.Children.Add(sideHeader);
            var brand = Text("YAKULT  /  WORKSPACE",10,Muted); brand.FontWeight = FontWeights.SemiBold; brand.Margin = new Thickness(6,0,0,8); sideHeader.Children.Add(brand);
            var title = Text("Messenger",27); title.FontWeight = FontWeights.Bold; title.Margin = new Thickness(5,0,0,0); sideHeader.Children.Add(title);
            _deviceCount = Text("Finding your people…",12,Muted); _deviceCount.Margin = new Thickness(6,6,0,20); sideHeader.Children.Add(_deviceCount);
            var searchGrid = new Grid();
            _search = new TextBox { Padding = new Thickness(12,11,12,11), BorderThickness = new Thickness(0), Background = Brushes.Transparent, FontSize = 12, ToolTip = "Search people or machines" };
            var searchHint = Text("Search people or machines",12,Muted); searchHint.Margin = new Thickness(12,0,0,0); searchHint.VerticalAlignment = VerticalAlignment.Center; searchHint.IsHitTestVisible = false;
            searchGrid.Children.Add(_search); searchGrid.Children.Add(searchHint);
            _search.TextChanged += (_,__) => { searchHint.Visibility = _search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; RenderDevices(); };
            sideHeader.Children.Add(new Border { Child = searchGrid, Background = Brush("#F2F4F8"), CornerRadius = new CornerRadius(10), Margin = new Thickness(0,0,0,14) });
            _activeOnly = new CheckBox { Content = "Active devices only", Foreground = Muted, FontSize = 12, Margin = new Thickness(6,0,0,20) };
            _activeOnly.Checked += (_,__) => RenderDevices(); _activeOnly.Unchecked += (_,__) => RenderDevices(); sideHeader.Children.Add(_activeOnly);
            var peopleLabel = Text("PEOPLE & DEVICES",10,Muted); peopleLabel.FontWeight = FontWeights.SemiBold; peopleLabel.Margin = new Thickness(6,0,0,8); sideHeader.Children.Add(peopleLabel);
            var bottom = new StackPanel(); DockPanel.SetDock(bottom,Dock.Bottom); sidebar.Children.Add(bottom);
            var refresh = Button("↻  Refresh devices"); refresh.Margin = new Thickness(0,12,0,14);
            refresh.Click += async (_,__) => await RefreshAsync(); bottom.Children.Add(refresh);
            var me = new DockPanel(); var avatar = Avatar(AppSession.CurrentUserName,true,34); DockPanel.SetDock(avatar,Dock.Left); me.Children.Add(avatar);
            var identity = new StackPanel(); identity.Children.Add(Text(AppSession.CurrentUserName ?? "Your account",12)); identity.Children.Add(Text(Environment.MachineName,10,Muted)); me.Children.Add(identity);
            bottom.Children.Add(new Border { Child = me, BorderBrush = Brush("#E8ECF2"), BorderThickness = new Thickness(0,1,0,0), Padding = new Thickness(4,14,0,0) });
            _deviceList = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch, ItemContainerStyle = DeviceStyle() };
            ScrollViewer.SetHorizontalScrollBarVisibility(_deviceList,ScrollBarVisibility.Disabled);
            _deviceList.SelectionChanged += async (_,__) =>
            {
                if (_renderingDevices) return;
                await SelectPeerAsync((_deviceList.SelectedItem as ListBoxItem)?.Tag as MessengerDevice);
            };
            sidebar.Children.Add(_deviceList);
            var conversation = new DockPanel(); Grid.SetColumn(conversation,1); root.Children.Add(conversation);
            var header = new StackPanel(); DockPanel.SetDock(header,Dock.Top); conversation.Children.Add(header);
            var person = new DockPanel { Margin = new Thickness(24,22,24,18) };
            var settingsButton = Button("\uE713");
            settingsButton.FontFamily = new FontFamily("Segoe MDL2 Assets");
            settingsButton.FontSize = 16; settingsButton.Width = 38; settingsButton.Height = 38;
            settingsButton.Padding = new Thickness(0);
            settingsButton.VerticalAlignment = VerticalAlignment.Center;
            settingsButton.Margin = new Thickness(12,0,0,0);
            settingsButton.ToolTip = "Messenger settings";
            System.Windows.Automation.AutomationProperties.SetName(settingsButton,"Messenger settings");
            DockPanel.SetDock(settingsButton,Dock.Right); person.Children.Add(settingsButton);
            var headAvatar = new Grid { Width = 44, Height = 44, Margin = new Thickness(0,0,13,0) };
            _peerInitials = Text("…",15,Accent); _peerInitials.FontWeight = FontWeights.SemiBold; _peerInitials.VerticalAlignment = VerticalAlignment.Center; _peerInitials.HorizontalAlignment = HorizontalAlignment.Center;
            headAvatar.Children.Add(new Border { Background = Brush("#F8E7EB"), CornerRadius = new CornerRadius(22), Child = _peerInitials });
            _presenceDot = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = Brush("#9CA8B8"), BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom }; headAvatar.Children.Add(_presenceDot);
            DockPanel.SetDock(headAvatar,Dock.Left); person.Children.Add(headAvatar);
            var details = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _peerTitle = Text("Your conversations",19); _peerTitle.FontWeight = FontWeights.SemiBold; details.Children.Add(_peerTitle);
            _peerTitle.TextWrapping = TextWrapping.NoWrap; _peerTitle.TextTrimming = TextTrimming.CharacterEllipsis;
            _peerStatus = Text("Choose someone from the device list",12,Muted); _peerStatus.Margin = new Thickness(0,4,0,0); details.Children.Add(_peerStatus); person.Children.Add(details);
            _peerStatus.TextWrapping = TextWrapping.NoWrap; _peerStatus.TextTrimming = TextTrimming.CharacterEllipsis;
            header.Children.Add(person);
            header.Children.Add(new Border { Background = Brush("#F7F8FB"), BorderBrush = Brush("#E8ECF2"), BorderThickness = new Thickness(0,1,0,1), Padding = new Thickness(24,10,24,10),
                ToolTip = "Double-click removes the message from your view. It is deleted after both participants dismiss it. Closing this window keeps pending messages.",
                Child = Text("◷  Temporary chat  ·  Double-click a message to dismiss it for you",11,Muted) });
            var composer = new StackPanel { Margin = new Thickness(24,12,24,18) };
            DockPanel.SetDock(composer,Dock.Bottom); conversation.Children.Add(composer);
            _status = Text("Choose an active user to start a conversation.",11,Muted); _status.Margin = new Thickness(2,0,0,10); composer.Children.Add(_status);
            var editor = new Grid();
            _draft = new TextBox { Height = 68, MaxLength = 1000, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(14,12,14,8), FontSize = 13, IsEnabled = false,
                ToolTip = "Write a message. Enter sends; Shift+Enter starts a new line." };
            _draftHint = Text("Write a message…",13,Muted); _draftHint.Margin = new Thickness(14,12,0,0); _draftHint.IsHitTestVisible = false;
            editor.Children.Add(_draft); editor.Children.Add(_draftHint);
            _draft.TextChanged += (_,__) => UpdateComposer();
            _draft.PreviewKeyDown += async (_,e) => { if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) { e.Handled = true; await SendAsync(); } };
            var editorStack = new StackPanel(); editorStack.Children.Add(editor);
            var actions = new DockPanel { Margin = new Thickness(14,0,10,10) };
            _send = Button("Send  ↗",true); _send.IsEnabled = false; _send.Padding = new Thickness(18,9,18,9);
            _send.Click += async (_,__) => await SendAsync(); DockPanel.SetDock(_send,Dock.Right); actions.Children.Add(_send);
            _count = Text("0 / 1,000  ·  Enter to send · Shift+Enter for a new line",10,Muted); _count.VerticalAlignment = VerticalAlignment.Center; actions.Children.Add(_count); editorStack.Children.Add(actions);
            var editorFrame = new Border { Child = editorStack, Background = Brushes.White, BorderBrush = Brush("#DDE3EC"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14) };
            _draft.GotKeyboardFocus += (_,__) => editorFrame.BorderBrush = Accent;
            _draft.LostKeyboardFocus += (_,__) => editorFrame.BorderBrush = Brush("#DDE3EC");
            composer.Children.Add(editorFrame);
            _messages = new StackPanel { Margin = new Thickness(26,22,26,16) };
            _messageScroll = new ScrollViewer { Content = _messages, Background = Brush("#F7F8FC"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            conversation.Children.Add(_messageScroll);
            var empty = new StackPanel { Margin = new Thickness(20,72,20,0), HorizontalAlignment = HorizontalAlignment.Center };
            var emptyIcon = Text("↗",34,Accent); emptyIcon.HorizontalAlignment = HorizontalAlignment.Center; empty.Children.Add(emptyIcon);
            var emptyTitle = Text("A quick conversation starts here",19); emptyTitle.FontWeight = FontWeights.SemiBold; emptyTitle.TextAlignment = TextAlignment.Center; emptyTitle.Margin = new Thickness(0,14,0,8); empty.Children.Add(emptyTitle);
            var emptyText = Text("Choose a person and machine to send a temporary message.",13,Muted); emptyText.TextAlignment = TextAlignment.Center; emptyText.MaxWidth = 280; empty.Children.Add(emptyText); _messages.Children.Add(empty);
            var settingsPanel = BuildSettingsPanel(() => settingsButton.Focus());
            Grid.SetColumn(settingsPanel,1); Panel.SetZIndex(settingsPanel,10); root.Children.Add(settingsPanel);
            root.SizeChanged += (_,__) => settingsPanel.MaxHeight = Math.Max(100,root.ActualHeight - 94);
            settingsButton.Click += (_,__) =>
            {
                settingsPanel.Visibility = settingsPanel.IsVisible ? Visibility.Collapsed : Visibility.Visible;
                if (settingsPanel.IsVisible) _compactWindow.Focus();
            };
            root.PreviewMouseDown += (_,__) =>
            {
                if (settingsPanel.IsVisible && !settingsPanel.IsMouseOver && !settingsButton.IsMouseOver && !_popupSize.IsDropDownOpen)
                    settingsPanel.Visibility = Visibility.Collapsed;
            };
            PreviewKeyDown += (_,e) =>
            {
                if (e.Key == Key.Escape && settingsPanel.IsVisible && !_popupSize.IsDropDownOpen)
                {
                    settingsPanel.Visibility = Visibility.Collapsed;
                    settingsButton.Focus(); e.Handled = true;
                }
            };
        }
        private Border BuildSettingsPanel(Action restoreFocus)
        {
            var content = new StackPanel();
            var title = Text("Messenger settings",17); title.FontWeight = FontWeights.SemiBold; content.Children.Add(title);
            var description = Text("Personalize Messenger on this desktop.",12,Muted);
            description.Margin = new Thickness(0,6,0,18); content.Children.Add(description);
            var windowLabel = Text("WINDOW SIZE",10,Muted); windowLabel.FontWeight = FontWeights.SemiBold; content.Children.Add(windowLabel);
            _compactWindow = new CheckBox { Content = "Use smaller Messenger window", FontSize = 12, Foreground = Ink,
                Margin = new Thickness(0,10,0,6), IsChecked = Properties.Settings.Default.DeviceMessenger_CompactWindow };
            _compactWindow.Checked += (_,__) => { ApplyWindowSize(); SavePopupPreferences(); };
            _compactWindow.Unchecked += (_,__) => { ApplyWindowSize(); SavePopupPreferences(); };
            content.Children.Add(_compactWindow);
            content.Children.Add(Text("Switch between a compact chat window and the full-size layout.",11,Muted));
            var popupLabel = Text("POPUP MESSAGES",10,Muted); popupLabel.FontWeight = FontWeights.SemiBold;
            popupLabel.Margin = new Thickness(0,20,0,10); content.Children.Add(popupLabel);
            var sizeLabel = Text("Popup size",12); sizeLabel.Margin = new Thickness(0,0,0,7); content.Children.Add(sizeLabel);
            _popupSize = new ComboBox { Height = 32, FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center,
                ItemsSource = new[] { "Normal size", "Small size" }, SelectedIndex = string.Equals(Properties.Settings.Default.DeviceMessenger_PopupSize,"Small",StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                ToolTip = "Choose the size of incoming message popups." };
            System.Windows.Automation.AutomationProperties.SetName(_popupSize,"Popup size");
            _popupSize.SelectionChanged += (_,__) => SavePopupPreferences(); content.Children.Add(_popupSize);
            _hidePopupPreview = new CheckBox { Content = "Hide message text", FontSize = 12, Foreground = Ink, Margin = new Thickness(0,18,0,6),
                IsChecked = Properties.Settings.Default.DeviceMessenger_HidePopupPreview,
                ToolTip = "Show the sender and a new-message notice without revealing the message contents." };
            _hidePopupPreview.Checked += (_,__) => SavePopupPreferences(); _hidePopupPreview.Unchecked += (_,__) => SavePopupPreferences();
            content.Children.Add(_hidePopupPreview);
            content.Children.Add(Text("Keep message contents private in popup notifications.",11,Muted));
            var note = Text("Changes save automatically.",11,Muted); note.Margin = new Thickness(0,18,0,12); content.Children.Add(note);
            var panel = new Border { Child = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, Width = 300, Padding = new Thickness(20), Margin = new Thickness(16,78,24,16),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Collapsed,
                Background = Brushes.White, BorderBrush = Brush("#DDE3EC"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = .15 } };
            var done = Button("Done"); done.HorizontalAlignment = HorizontalAlignment.Right;
            done.Click += (_,__) => { panel.Visibility = Visibility.Collapsed; restoreFocus(); };
            content.Children.Add(done);
            return panel;
        }
        private async Task SelectPeerAsync(MessengerDevice peer)
        {
            if (_selected != null) _drafts[_selected.Key] = _draft.Text;
            _selected = peer;
            _draft.Text = _selected != null && _drafts.TryGetValue(_selected.Key,out var draft) ? draft : "";
            _renderedMessages = ""; _messages.Children.Clear(); UpdateHeader();
            await RefreshAsync();
        }

        private void RenderDevices()
        {
            if (_deviceList == null) return;
            _renderingDevices = true;
            try
            {
                _deviceList.Items.Clear();
                var query = (_search.Text ?? "").Trim();
                foreach (var device in _devices.Where(x => (_activeOnly?.IsChecked != true || x.Online) &&
                    (query.Length == 0 || (x.MachineName + " " + x.UserName).IndexOf(query,StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    var content = new Grid();
                    content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
                    content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    content.Children.Add(Avatar(device.UserName,device.Online));
                    var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    var user = Text(device.UserName,13); user.FontWeight = FontWeights.SemiBold; user.TextWrapping = TextWrapping.NoWrap; user.TextTrimming = TextTrimming.CharacterEllipsis; labels.Children.Add(user);
                    var machine = Text(device.MachineName,11,Muted); machine.TextWrapping = TextWrapping.NoWrap; machine.TextTrimming = TextTrimming.CharacterEllipsis; machine.Margin = new Thickness(0,4,0,0); labels.Children.Add(machine);
                    Grid.SetColumn(labels,1); content.Children.Add(labels);
                    if (device.PendingCount > 0)
                    {
                        var badgeText = Text(device.PendingCount.ToString(),10,Brushes.White); badgeText.FontWeight = FontWeights.SemiBold;
                        var badge = new Border { Child = badgeText, Background = Accent, CornerRadius = new CornerRadius(10), Padding = new Thickness(6,3,6,3), Margin = new Thickness(7,0,0,0), VerticalAlignment = VerticalAlignment.Center };
                        Grid.SetColumn(badge,2); content.Children.Add(badge);
                    }
                    var item = new ListBoxItem { Content = content, Tag = device, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        ToolTip = device.UserName + " • " + device.MachineName + " • " + device.Presence + " • " + device.PendingCount + " pending", Margin = new Thickness(0,0,0,5) };
                    _deviceList.Items.Add(item);
                    if (_selected != null && string.Equals(device.Key,_selected.Key,StringComparison.OrdinalIgnoreCase)) _deviceList.SelectedItem = item;
                }
                _deviceCount.Text = _devices.Count(x => x.Online) + " active • " + _devices.Count + " known • " + _deviceList.Items.Count + " shown";
                _deviceList.ToolTip = "Active means the device reported presence within the last 15 minutes.";
                if (_deviceList.Items.Count == 0)
                    _deviceList.Items.Add(new ListBoxItem { IsEnabled = false, Content = Text("No matching devices. Try clearing search or turning off Active devices only.",12,Muted) });
            }
            finally { _renderingDevices = false; }
        }
        private void UpdateHeader()
        {
            _peerTitle.Text = _selected?.UserName ?? "Your conversations";
            _peerInitials.Text = _selected == null ? "…" : Initials(_selected.UserName);
            _presenceDot.Background = _selected?.Online == true ? Brush("#20A674") : Brush("#9CA8B8");
            _peerStatus.Text = _selected == null ? "Select a machine and user on the left." : _selected.MachineName + " • " + _selected.Presence + (_selected.Online ? " • Live chat" : "");
            _status.Text = _selected == null ? "Choose an active user to start a conversation." : _selected.Online
                ? "Messages stay here until you dismiss them."
                : "This device is offline. Your draft is kept while you wait.";
            UpdateComposer();
        }
        private void UpdateComposer()
        {
            if (_send == null) return;
            _send.IsEnabled = !_sending && _selected?.Online == true && !string.IsNullOrWhiteSpace(_draft.Text);
            _draft.IsEnabled = !_sending && _selected != null;
            _count.Text = _draft.Text.Length + " / 1,000  ·  Enter to send · Shift+Enter for a new line";
            _draftHint.Visibility = _draft.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            _draftHint.Text = _selected == null ? "Choose someone to write to…" : "Write a message…";
            _count.Foreground = _draft.Text.Length >= 900 ? Brush("#9A6700") : Muted;
        }
        private void SavePopupPreferences()
        {
            if (_popupSize == null || _hidePopupPreview == null) return;
            Properties.Settings.Default.DeviceMessenger_CompactWindow = _compactWindow.IsChecked == true;
            Properties.Settings.Default.DeviceMessenger_PopupSize = _popupSize.SelectedIndex == 1 ? "Small" : "Normal";
            Properties.Settings.Default.DeviceMessenger_HidePopupPreview = _hidePopupPreview.IsChecked == true;
            try { Properties.Settings.Default.Save(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DeviceMessenger] Could not save popup preferences: " + ex.Message); }
        }
        private void ApplyWindowSize()
        {
            var compact = _compactWindow.IsChecked == true;
            if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
            MinWidth = compact ? 680 : 760; MinHeight = compact ? 500 : 540;
            Width = compact ? 720 : 1020; Height = compact ? 560 : 720;
            _sidebarColumn.Width = new GridLength(compact ? 220 : 286);
        }
        private async Task RefreshAsync()
        {
            if (_closed) return;
            if (!AppSession.IsLoggedIn || AppSession.CurrentUserId != _sessionUserId) { Close(); return; }
            if (_refreshing) { _refreshAgain = true; return; }
            _refreshing = true;
            try
            {
                var devices = await _repository.GetDevicesAsync();
                if (_closed) return;
                _devices = devices;
                if (_selected != null)
                {
                    var updated = devices.FirstOrDefault(x => string.Equals(x.Key,_selected.Key,StringComparison.OrdinalIgnoreCase));
                    if (updated != null) _selected = updated;
                    else _selected.Online = false;
                }
                RenderDevices(); UpdateHeader();
                var peer = _selected;
                if (peer == null) { _status.Text = _devices.Count == 0 ? "No other devices are currently known. Open the updated app on another PC." : "Select a device to see pending messages."; return; }
                var messages = await _repository.GetConversationAsync(peer);
                if (_closed || _selected == null || !string.Equals(peer.Key,_selected.Key,StringComparison.OrdinalIgnoreCase)) return;
                RenderMessages(messages,peer);
                _status.Foreground = Muted;
                _status.Text = peer.Online ? "Connected • Double-click a bubble to dismiss it for you." : "This device is offline. You can keep writing; send when it reconnects.";
            }
            catch (Exception ex) { if (!_closed) { _status.Text = ex.Message; _status.Foreground = Brushes.Firebrick; } }
            finally { _refreshing = false; }
            if (_refreshAgain && !_closed) { _refreshAgain = false; await RefreshAsync(); }
        }
        private void RenderMessages(List<TemporaryDeviceMessage> rows, MessengerDevice peer)
        {
            var signature = peer.Key + ":" + string.Join(",",rows.Select(x => x.MessageId));
            if (signature == _renderedMessages) return;
            var toBottom = _renderedMessages.Length == 0 || _messageScroll.ScrollableHeight - _messageScroll.VerticalOffset < 32;
            _renderedMessages = signature; _messages.Children.Clear();
            if (rows.Count == 0) _messages.Children.Add(Text("No pending messages here. Say hello, or wait for a reply.",14,Muted));
            DateTime? lastDate = null;
            foreach (var row in rows)
            {
                var localTime = DateTime.SpecifyKind(row.CreatedAtUtc,DateTimeKind.Utc).ToLocalTime();
                if (lastDate != localTime.Date)
                {
                    var day = Text(localTime.Date == DateTime.Today ? "TODAY" : localTime.ToString("MMM d, yyyy").ToUpperInvariant(),10,Muted);
                    day.FontWeight = FontWeights.SemiBold; day.HorizontalAlignment = HorizontalAlignment.Center; day.Margin = new Thickness(0,0,0,20); _messages.Children.Add(day);
                    lastDate = localTime.Date;
                }
                var content = new StackPanel();
                var bubbleText = row.IsOutgoing ? Brushes.White : Ink;
                var bubbleMeta = row.IsOutgoing ? Brush("#F2CCD3") : Muted;
                var author = Text(row.IsOutgoing ? "You" : peer.UserName,10,bubbleMeta); author.FontWeight = FontWeights.SemiBold; content.Children.Add(author);
                var body = Text(row.Body,13,bubbleText); body.LineHeight = 21; body.Margin = new Thickness(0,6,0,8); content.Children.Add(body);
                var time = Text(localTime.ToString("h:mm tt"),10,bubbleMeta); time.HorizontalAlignment = HorizontalAlignment.Right; content.Children.Add(time);
                var bubble = new Border { Child = content, Background = row.IsOutgoing ? Accent : Brushes.White,
                    BorderBrush = row.IsOutgoing ? Accent : Brush("#E5EAF2"), BorderThickness = new Thickness(1), CornerRadius = row.IsOutgoing ? new CornerRadius(16,16,4,16) : new CornerRadius(4,16,16,16),
                    Padding = new Thickness(15), Margin = new Thickness(row.IsOutgoing ? 45 : 0,0,row.IsOutgoing ? 0 : 45,12),
                    HorizontalAlignment = row.IsOutgoing ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    MaxWidth = 430, Focusable = true, ToolTip = "Double-click to dismiss this message for you." };
                var busy = false;
                Func<Task> dismiss = async () =>
                {
                    if (busy) return; busy = true; bubble.Opacity = .5;
                    try { await _repository.DismissAsync(row.MessageId); await RefreshAsync(); await Services.DeviceMessengerNotifications.Instance.RefreshAsync(); }
                    catch (Exception ex) { _status.Text = ex.Message; _status.Foreground = Brushes.Firebrick; }
                    finally { busy = false; bubble.Opacity = 1; }
                };
                bubble.MouseLeftButtonDown += async (_,e) => { bubble.Focus(); if (e.ClickCount == 2) { e.Handled = true; await dismiss(); } };
                _messages.Children.Add(bubble);
            }
            if (toBottom) Dispatcher.BeginInvoke(new Action(() => _messageScroll.ScrollToEnd()),DispatcherPriority.Loaded);
        }
        private async Task SendAsync()
        {
            if (_sending || _selected?.Online != true || string.IsNullOrWhiteSpace(_draft.Text)) return;
            var peer = _selected; var body = _draft.Text;
            _sending = true; _deviceList.IsEnabled = false; _send.Content = "Sending…"; UpdateComposer();
            try
            {
                await _repository.SendAsync(peer,body);
                if (_closed) return;
                _drafts.Remove(peer.Key); _draft.Clear(); _renderedMessages = "";
                await RefreshAsync();
            }
            catch (Exception ex) { if (!_closed) { _status.Text = ex.Message; _status.Foreground = Brushes.Firebrick; } }
            finally { _sending = false; _deviceList.IsEnabled = true; _send.Content = "Send  ↗"; UpdateComposer(); }
        }
    }
}
