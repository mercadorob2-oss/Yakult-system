using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Services
{
    /// <summary>The original Yakumi floating widget, connected to the read-only assistant service.</summary>
    public sealed class YakumiChatView : UserControl,IDisposable
    {
        private static readonly Brush Ink=Brush("#202635");
        private static readonly Brush Muted=Brush("#677081");
        private static readonly Brush Accent=Brush("#B92335");
        private static readonly Brush Line=Brush("#E5E6E8");
        private static readonly Style ActionStyle=CreateActionStyle();
        private static readonly Style HeaderActionStyle=CreateActionStyle(true);
        private readonly YakumiAssistantService _service;
        private readonly Func<AssistantContext> _context;
        private Border _yakumiPanel;
        private Border _yakumiHeader;
        private StackPanel _yakumiMessages;
        private ScrollViewer _yakumiScroll;
        private TextBox _yakumiInput;
        private Grid _yakumiBody;
        private StackPanel _yakumiSettings;
        private TextBox _setUrlBox;
        private ComboBox _setModelBox;
        private TextBlock _setStatusText;
        private TextBlock _yakumiSubtitle;
        private Button _yakumiMinButton;
        private Button _sendButton;
        private Button _reloadModelsButton;
        private Button _testConnectionButton;
        private Button _saveSettingsButton;
        private Button _settingsStopButton;
        private CancellationTokenSource _settingsCts;
        private bool _updatingSettings;
        private string _settingsUrlValue;
        private string _settingsModelValue;
        private bool _yakumiMinimized;
        private bool _showingSettings;
        private bool _hasQuestion;
        private int _chatRevision;
        private int _scrollInteraction;
        private WrapPanel _starters;
        private Button _newReplyButton;
        private Border _latestReply;
        private bool _disposed;
        private string _stamp;
        private CancellationTokenSource _chatCts;
        public Action CloseRequested {get;set;}
        public Action<double> HeightChanged {get;set;}

        public YakumiChatView(Func<AssistantContext> context) : this(context,new YakumiAssistantService()) { }
        internal YakumiChatView(Func<AssistantContext> context,YakumiAssistantService service)
        {
            _context=context;_service=service;FontFamily=new FontFamily("Segoe UI");Width=350;Height=540;
            BuildYakumiPanel();Content=_yakumiPanel;NewConversation();
        }
        public void RefreshContext(AssistantContext context)
        {
            if(_disposed) return;
            bool changed=_stamp!=context.SessionStamp;
            if(_stamp!=null && _stamp!=context.SessionStamp) NewConversation("Your session, permissions or database changed. Previous chat was cleared.");
            _stamp=context.SessionStamp;
            if(!_hasQuestion && changed) UpdateStarters(context);
            ToolTip="Read-only assistant • "+context.Module+(context.SelectedRecordId.HasValue?" • selected record #"+context.SelectedRecordId:"");
        }
        private static Brush Brush(string hex)
        {
            var brush=(SolidColorBrush)new BrushConverter().ConvertFromString(hex);brush.Freeze();return brush;
        }
        private static TextBlock Label(string text,double size,Brush foreground,FontWeight? weight=null,Thickness? margin=null)
        {
            return new TextBlock {Text=text,FontSize=size,Foreground=foreground,FontWeight=weight ?? FontWeights.Normal,Margin=margin ?? new Thickness(0),TextWrapping=TextWrapping.Wrap};
        }

        private static Style CreateActionStyle(bool darkHeader = false)
        {
            var border = new FrameworkElementFactory(typeof(Border), "Surface");
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            border.AppendChild(presenter);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, darkHeader ? Brush("#FBD9DE") : Accent, "Surface"));
            hover.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1), "Surface"));
            if (darkHeader) hover.Setters.Add(new Setter(Border.BackgroundProperty, Brush("#8F1929"), "Surface"));
            template.Triggers.Add(hover);
            var press = new Trigger { Property = Button.IsPressedProperty, Value = true };
            press.Setters.Add(new Setter(UIElement.OpacityProperty, 0.72, "Surface"));
            template.Triggers.Add(press);
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focus.Setters.Add(new Setter(Border.BorderBrushProperty, darkHeader ? Brushes.White : Accent, "Surface"));
            focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "Surface"));
            template.Triggers.Add(focus);
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            return style;
        }


        private static BitmapImage _yakumiAvatarImage;

        internal static BitmapImage YakumiAvatarImage()
        {
            if (_yakumiAvatarImage != null) return _yakumiAvatarImage;
            try
            {
                var baseDir = Path.GetDirectoryName(typeof(YakumiChatView).Assembly.Location);
                string path = Path.Combine(baseDir, "AI_assets", "YakumiAvatar.png");
                if (!File.Exists(path)) return null;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 96;
                bmp.EndInit();
                bmp.Freeze();
                _yakumiAvatarImage = bmp;
                return bmp;
            }
            catch { return null; }
        }

        private static FrameworkElement YakumiAvatar(double size, double fontSize)
        {
            var bmp = YakumiAvatarImage();
            if (bmp != null)
            {
                return new Image
                {
                    Source = bmp,
                    Width = size,
                    Height = size,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };
            }
            return new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Background = Brushes.White,
                BorderBrush = Accent,
                BorderThickness = new Thickness(1.5),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = "Y",
                    FontSize = fontSize,
                    FontWeight = FontWeights.Bold,
                    Foreground = Accent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }


        private void BuildYakumiPanel()
        {
            _yakumiPanel = new Border
            {
                Width = 350,
                Height = 540,
                Background = Brushes.White,
                CornerRadius = new CornerRadius(14),
                BorderBrush = Line,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0),
                Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.25, Color = Colors.Black }
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _yakumiPanel.Child = root;

            var header = _yakumiHeader = new Border
            {
                Background = Accent,
                CornerRadius = new CornerRadius(14, 14, 0, 0),
                Padding = new Thickness(14, 10, 10, 10)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.Children.Add(YakumiAvatar(34, 16));
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(Label("Yakumi", 14, Brushes.White, FontWeights.Bold));
            _yakumiSubtitle = Label("Your Yakult assistant", 10, Brush("#FBD9DE"));
            _yakumiSubtitle.ToolTip=YakumiSubtitleText();
            titles.Children.Add(_yakumiSubtitle);
            Grid.SetColumn(titles, 1);
            headerGrid.Children.Add(titles);
            var newChat = new Button {Style=HeaderActionStyle,Content=Label("New chat",11,Brushes.White),Padding=new Thickness(5,4,5,4),ToolTip="Start a new chat"};
            newChat.Click+=(_,__)=> {NewConversation();_showingSettings=false;_yakumiMinimized=false;ApplyViewState();FocusInput();};
            Grid.SetColumn(newChat,2);headerGrid.Children.Add(newChat);
            _yakumiMinButton = new Button { Style = HeaderActionStyle, Content = Label("—", 13, Brushes.White, FontWeights.Bold), Foreground = Brushes.White, Padding = new Thickness(8, 2, 8, 2), Cursor = Cursors.Hand, ToolTip = "Minimize" };
            _yakumiMinButton.Click += (_, __) => ToggleYakumiMinimized();
            Grid.SetColumn(_yakumiMinButton, 2);
            headerGrid.Children.Add(_yakumiMinButton);
            var gear = new Button { Style = HeaderActionStyle, Content = Label("⚙", 13, Brushes.White, FontWeights.Bold), Foreground = Brushes.White, Padding = new Thickness(8, 2, 8, 2), Cursor = Cursors.Hand, ToolTip = "AI settings" };
            gear.Click += (_, __) => ToggleYakumiSettings();
            Grid.SetColumn(gear, 3);
            headerGrid.Children.Add(gear);
            Grid.SetColumn(_yakumiMinButton, 4);
            var close = new Button { Style = HeaderActionStyle, Content = Label("✕", 12, Brushes.White, FontWeights.Bold), Foreground = Brushes.White, Padding = new Thickness(8, 2, 8, 2), Cursor = Cursors.Hand, ToolTip = "Close" };
            close.Click += (_, __) => { Cancel(); CloseRequested?.Invoke(); };
            Grid.SetColumn(close, 5);
            headerGrid.Children.Add(close);
            header.Child = headerGrid;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            _yakumiBody = new Grid();
            _yakumiBody.RowDefinitions.Add(new RowDefinition {Height=new GridLength(1,GridUnitType.Star)});
            _yakumiBody.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            _yakumiBody.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            Grid.SetRow(_yakumiBody, 1);
            root.Children.Add(_yakumiBody);

            _yakumiScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled, Padding = new Thickness(10, 10, 10, 4),CanContentScroll=false };
            _yakumiMessages = new StackPanel();
            _yakumiScroll.Content = _yakumiMessages;
            _yakumiBody.Children.Add(_yakumiScroll);
            _yakumiScroll.PreviewMouseWheel+=(_,__)=>_scrollInteraction++;
            _yakumiScroll.PreviewMouseDown+=(_,__)=>_scrollInteraction++;
            _yakumiScroll.PreviewKeyDown+=(_,__)=>_scrollInteraction++;
            _yakumiScroll.ScrollChanged+=(_,e)=> {if(e.ExtentHeightChange==0 && IsNearBottom()) _newReplyButton.Visibility=Visibility.Collapsed;};
            _newReplyButton=new Button {Style=ActionStyle,Content="New reply ↓",FontSize=13,Background=Brushes.White,Foreground=Accent,BorderBrush=Accent,
                Padding=new Thickness(10,6,10,6),Margin=new Thickness(0,0,12,8),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Visibility=Visibility.Collapsed};
            _newReplyButton.Click+=(_,__)=> {ScrollToLatestReply();_newReplyButton.Visibility=Visibility.Collapsed;};
            _yakumiBody.Children.Add(_newReplyButton);
            _starters=new WrapPanel {Margin=new Thickness(12,4,12,0)};Grid.SetRow(_starters,1);_yakumiBody.Children.Add(_starters);

            var inputRow = new Grid { Margin = new Thickness(12, 8, 12, 12) };
            inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var inputBorder = new Border { Background = Brushes.White, BorderBrush = Brush("#D5D9E0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(12, 6, 12, 6), VerticalAlignment = VerticalAlignment.Center };
            _yakumiInput = new TextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Ink, FontSize = AssistantAnswerRenderer.MessageSize,
                AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=26,MaxHeight=80,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalContentAlignment = VerticalAlignment.Center };
            _yakumiInput.PreviewKeyDown += OnInputKeyDown;
            _yakumiInput.ToolTip="Enter sends · Shift+Enter adds a new line";
            System.Windows.Automation.AutomationProperties.SetName(_yakumiInput,"Ask Yakumi");
            var inputOverlay=new Grid();inputOverlay.Children.Add(_yakumiInput);
            var placeholder=Label("Ask Yakumi…",AssistantAnswerRenderer.MessageSize,Muted);placeholder.IsHitTestVisible=false;
            placeholder.VerticalAlignment=VerticalAlignment.Center;inputOverlay.Children.Add(placeholder);
            _yakumiInput.TextChanged+=(_,__)=>placeholder.Visibility=string.IsNullOrEmpty(_yakumiInput.Text)?Visibility.Visible:Visibility.Collapsed;
            inputBorder.Child = inputOverlay;
            inputRow.Children.Add(inputBorder);
            var send = new Button { Style = ActionStyle, Background = Accent, BorderBrush = Accent, Foreground = Brushes.White, Width = 36, Height = 36, Margin = new Thickness(8, 0, 0, 0), Cursor = Cursors.Hand, ToolTip = "Send" };
            send.Content = new TextBlock { Text = "➤", FontSize = 15, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _sendButton=send;
            send.Click += (_, __) => {if(_chatCts!=null) Cancel();else YakumiSendAsync(_yakumiInput.Text);};
            Grid.SetColumn(send, 1);
            inputRow.Children.Add(send);
            var composer=new StackPanel();composer.Children.Add(inputRow);
            composer.Children.Add(Label("Enter sends · Shift+Enter for a new line",11,Muted,margin:new Thickness(16,-7,12,8)));
            Grid.SetRow(composer,2);_yakumiBody.Children.Add(composer);

            // Settings view (hidden until the gear is clicked).
            _yakumiSettings = new StackPanel { Margin = new Thickness(14, 10, 14, 12), Visibility = Visibility.Collapsed };
            _yakumiSettings.Children.Add(Label("AI Server", 13, Ink, FontWeights.Bold));
            _yakumiSettings.Children.Add(Label("For shared AI, enter the AI computer's name or IP. Localhost means this desktop.", 10, Muted, margin:new Thickness(0,5,0,0)));
            _yakumiSettings.Children.Add(Label("Server URL", 10, Muted, FontWeights.Normal, new Thickness(0, 8, 0, 3)));
            _setUrlBox = new TextBox { Background = Brushes.White, BorderBrush = Brush("#D5D9E0"), BorderThickness = new Thickness(1), Foreground = Ink, FontSize = 12, Padding = new Thickness(8, 6, 8, 6) };
            _yakumiSettings.Children.Add(_setUrlBox);
            _yakumiSettings.Children.Add(Label("Model", 10, Muted, FontWeights.Normal, new Thickness(0, 8, 0, 3)));
            var modelRow = new Grid();
            modelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            modelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _setModelBox = new ComboBox { IsEditable = true, FontSize = 12, Padding = new Thickness(6, 4, 6, 4), VerticalContentAlignment = VerticalAlignment.Center };
            modelRow.Children.Add(_setModelBox);
            var reloadModels = _reloadModelsButton = new Button { Style = ActionStyle, Background = Brushes.White, BorderBrush = Brush("#D5D9E0"), BorderThickness = new Thickness(1), Foreground = Ink, Content = "⟳", FontSize = 13, Width = 34, Margin = new Thickness(6, 0, 0, 0), Cursor = Cursors.Hand, ToolTip = "Reload model list from server" };
            reloadModels.Click += async (_, __) => await LoadYakumiModelsAsync();
            Grid.SetColumn(reloadModels, 1);
            modelRow.Children.Add(reloadModels);
            _yakumiSettings.Children.Add(modelRow);
            var settingsButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            var testButton = _testConnectionButton = new Button { Style = ActionStyle, Content = "Test connection", FontSize = 11, Background = Brushes.White, BorderBrush = Accent, BorderThickness = new Thickness(1), Foreground = Accent, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand };
            testButton.Click += async (_, __) => await TestYakumiConnectionAsync();
            settingsButtons.Children.Add(testButton);
            var saveButton = _saveSettingsButton = new Button { Style = ActionStyle, Content = "Save & connect", FontSize = 11, FontWeight = FontWeights.Bold, Background = Accent, BorderBrush = Accent, Foreground = Brushes.White, Padding = new Thickness(16, 7, 16, 7), Cursor = Cursors.Hand };
            saveButton.Click += async (_, __) => await CheckYakumiConnectionAsync(true);
            settingsButtons.Children.Add(saveButton);
            _yakumiSettings.Children.Add(settingsButtons);
            _setStatusText = Label("", 10, Muted, FontWeights.Normal, new Thickness(0, 8, 0, 0));
            _setStatusText.TextWrapping = TextWrapping.Wrap;
            _yakumiSettings.Children.Add(_setStatusText);
            _settingsStopButton=new Button {Style=ActionStyle,Content="Stop reply",FontSize=13,Foreground=Accent,BorderBrush=Accent,Padding=new Thickness(10,6,10,6),Margin=new Thickness(0,8,0,0),HorizontalAlignment=HorizontalAlignment.Left,Visibility=Visibility.Collapsed};
            _settingsStopButton.Click+=(_,__)=> {if(_chatCts!=null) _chatCts.Cancel();};
            _yakumiSettings.Children.Add(_settingsStopButton);
            Grid.SetRow(_yakumiSettings, 1);
            root.Children.Add(_yakumiSettings);
            _setUrlBox.ToolTip="For example: http://AI-PC:11434";
            _setUrlBox.TextChanged+=(_,__)=>SettingsEdited();
            _setModelBox.SelectionChanged+=(_,__)=>SettingsEdited();
            _setModelBox.AddHandler(TextBox.TextChangedEvent,new TextChangedEventHandler((_,__)=>SettingsEdited()));
        }

        private static string YakumiSubtitleText()
        {
            try { return AppConfig.OllamaModel + " @ " + new Uri(AppConfig.OllamaBaseUrl).Host; }
            catch { return "Your friendly Yakult AI assistant"; }
        }

        private void ToggleYakumiSettings()
        {
            CancelSettingsCheck();
            bool showSettings = !_showingSettings;
            _showingSettings=showSettings;_yakumiMinimized=false;ApplyViewState();
            if (showSettings)
            {
                _updatingSettings=true;
                try {_setUrlBox.Text=AppConfig.OllamaBaseUrl;_setModelBox.SelectedItem=null;_setModelBox.Text=AppConfig.OllamaModel;}
                finally {RememberSettingsFields();_updatingSettings=false;}
                SetYakumiStatus("Saved connection. Test it or enter another server.", Muted);
                var _ = LoadYakumiModelsAsync();
            }
        }

        private void SetYakumiStatus(string text, Brush foreground)
        {
            _setStatusText.Text = text;
            _setStatusText.Foreground = foreground;
        }

        private async System.Threading.Tasks.Task LoadYakumiModelsAsync()
        {
            string url=_setUrlBox.Text,model=_setModelBox.Text;
            var cts=BeginSettingsCheck("Loading installed models…");
            try
            {
                var models=await OllamaChatService.GetInstalledModelsAsync(url,cts.Token);
                if(!IsCurrentSettingsCheck(cts)) return;
                UpdateConnectionFields(OllamaConnectionSettings.NormalizeBaseUrl(url),model,models);
                SetYakumiStatus(models.Count==0?"Server reachable, but no models are installed. Ask the AI server administrator to install a model.":models.Count+" installed model(s). Choose a model, then Save & connect.",models.Count==0?Accent:Muted);
            }
            catch(OperationCanceledException) {if(IsCurrentSettingsCheck(cts)) SetYakumiStatus("The server did not respond. Check its address, network and firewall.",Accent);}
            catch(Exception ex) {if(IsCurrentSettingsCheck(cts)) SetYakumiStatus(ex.Message,Accent);}
            finally {EndSettingsCheck(cts);}
        }

        private Task TestYakumiConnectionAsync() {return CheckYakumiConnectionAsync(false);}
        private async Task CheckYakumiConnectionAsync(bool save)
        {
            string url=_setUrlBox.Text,model=_setModelBox.Text;
            var cts=BeginSettingsCheck(save?"Validating server and model before connecting…":"Testing server and selected model…");
            try
            {
                var check=await OllamaConnectionSettings.ValidateAsync(url,model,cts.Token);
                if(!IsCurrentSettingsCheck(cts)) return;
                UpdateConnectionFields(check.BaseUrl,check.Model ?? model,check.Models);
                if(!check.ModelAvailable)
                {SetYakumiStatus(check.Models.Count==0?"Ollama is reachable, but no models are installed. Ask the AI server administrator to install one.":"Ollama is reachable, but the selected model is not installed. Choose a model from the list. Your saved connection was kept.",Accent);return;}
                if(save)
                {
                    AppConfig.SetOllamaConnection(check.BaseUrl,check.Model);
                    if(_chatCts!=null) _chatCts.Cancel();
                    NewConversation("Connection updated. Ask a new question.");
                    _yakumiSubtitle.ToolTip=YakumiSubtitleText();
                    SetYakumiStatus("Connected and saved for this Windows user. "+check.Model+" is available on "+new Uri(check.BaseUrl).Host+".",Brush("#1E8449"));
                }
                else SetYakumiStatus("Connected • Ollama "+check.Version+" • "+check.Model+" available. Click Save & connect to apply.",Brush("#1E8449"));
            }
            catch(OperationCanceledException) { }
            catch(Exception ex) {if(IsCurrentSettingsCheck(cts)) SetYakumiStatus(ex.Message+" Your saved connection was kept.",Accent);}
            finally {EndSettingsCheck(cts);}
        }

        private CancellationTokenSource BeginSettingsCheck(string message)
        {
            CancelSettingsCheck();RememberSettingsFields();_settingsCts=new CancellationTokenSource();SetSettingsBusy(true);SetYakumiStatus(message,Muted);return _settingsCts;
        }
        private bool IsCurrentSettingsCheck(CancellationTokenSource cts)
        {return !_disposed && ReferenceEquals(cts,_settingsCts) && !cts.IsCancellationRequested;}
        private void EndSettingsCheck(CancellationTokenSource cts)
        {if(ReferenceEquals(cts,_settingsCts)) {_settingsCts=null;if(!_disposed) SetSettingsBusy(false);}cts.Dispose();}
        private void CancelSettingsCheck()
        {var cts=_settingsCts;_settingsCts=null;if(cts!=null) cts.Cancel();if(!_disposed && _saveSettingsButton!=null) SetSettingsBusy(false);}
        private void SetSettingsBusy(bool busy)
        {_reloadModelsButton.IsEnabled=!busy;_testConnectionButton.IsEnabled=!busy;_saveSettingsButton.IsEnabled=!busy;}
        private void SettingsEdited()
        {
            if(_updatingSettings || _disposed || (_setUrlBox.Text==_settingsUrlValue && _setModelBox.Text==_settingsModelValue)) return;
            RememberSettingsFields();CancelSettingsCheck();SetYakumiStatus("Settings changed. Test and save to connect.",Muted);
        }
        private void RememberSettingsFields()
        {_settingsUrlValue=_setUrlBox.Text;_settingsModelValue=_setModelBox.Text;}
        private void UpdateConnectionFields(string url,string model,System.Collections.Generic.IReadOnlyList<string> models)
        {
            _updatingSettings=true;
            try {_setUrlBox.Text=url;_setModelBox.ItemsSource=models;_setModelBox.SelectedItem=null;_setModelBox.Text=model;}
            finally {RememberSettingsFields();_updatingSettings=false;}
        }

        private void ToggleYakumiMinimized()
        {
            _yakumiMinimized = !_yakumiMinimized;
            ApplyViewState();
        }
        private void ApplyViewState()
        {
            _yakumiBody.Visibility=_yakumiMinimized || _showingSettings?Visibility.Collapsed:Visibility.Visible;
            _yakumiSettings.Visibility=!_yakumiMinimized && _showingSettings?Visibility.Visible:Visibility.Collapsed;
            Height=_yakumiPanel.Height=_yakumiMinimized?56:540;
            _yakumiHeader.CornerRadius=_yakumiMinimized?new CornerRadius(14):new CornerRadius(14,14,0,0);
            _yakumiMinButton.Content = Label(_yakumiMinimized ? "+" : "—", 13, Brushes.White, FontWeights.Bold);
            _yakumiMinButton.ToolTip = _yakumiMinimized ? "Restore" : "Minimize";
            HeightChanged?.Invoke(Height);
        }

        private void AddYakumiChip(WrapPanel chips, string label, string question)
        {
            var chip = new Button
            {
                Style = ActionStyle,
                Content = label,
                FontSize = 11,
                Background = Brushes.White,
                BorderBrush = Accent,
                BorderThickness = new Thickness(1),
                Foreground = Accent,
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 6, 6),
                Cursor = Cursors.Hand
            };
            chip.Click += (_, __) =>
            {
                if (question == null) { _yakumiInput.Focus(); return; }
                YakumiSendAsync(question);
            };
            chips.Children.Add(chip);
        }

        private Border AddYakumiBubble(bool isUser, string text, bool live)
        {
            var bubble = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10,9,10,9),
                Margin = new Thickness(isUser?22:0,0,0,10),
                Background = isUser ? Accent : Brush("#F1F3F7"),
                Child=Label(text,AssistantAnswerRenderer.MessageSize,isUser?Brushes.White:Ink)
            };
            // Grid's star column constrains wrapping to the actual available viewport width.
            var row=new Grid();
            if(!isUser)
            {
                row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition());
                row.Children.Add(YakumiAvatar(24,11));Grid.SetColumn(bubble,1);
            }
            row.Children.Add(bubble);
            KeepScrollPosition(()=>_yakumiMessages.Children.Add(row),false);
            return bubble;
        }
        private bool IsNearBottom() {return _yakumiScroll.ScrollableHeight-_yakumiScroll.VerticalOffset<24;}
        private void ScrollToLatestReply()
        {
            if(_latestReply==null) {_yakumiScroll.ScrollToBottom();return;}
            _yakumiScroll.ScrollToVerticalOffset(_latestReply.TranslatePoint(new Point(0,0),_yakumiMessages).Y);
        }
        private void KeepScrollPosition(Action change,bool reply)
        {
            bool tail=IsNearBottom();double offset=_yakumiScroll.VerticalOffset;int interaction=_scrollInteraction,revision=_chatRevision;
            change();
            if(reply && (!tail || _yakumiMinimized || _showingSettings)) _newReplyButton.Visibility=Visibility.Visible;
            _yakumiScroll.Dispatcher.BeginInvoke(new Action(()=>
            {
                if(_disposed || revision!=_chatRevision || interaction!=_scrollInteraction) return;
                if(tail) {if(reply) ScrollToLatestReply();else _yakumiScroll.ScrollToBottom();}
                else _yakumiScroll.ScrollToVerticalOffset(offset);
            }),System.Windows.Threading.DispatcherPriority.Loaded);
        }
        private void RenderAnswer(Border bubble,AssistantResult result,string originStamp)
        {
            var answer=AssistantPresentationFormatter.Format(result);
            _latestReply=bubble;
            KeepScrollPosition(()=>
            {
                bubble.Background=Brush(result.Outcome==AssistantOutcome.Denied?"#FFF3DA":result.Outcome==AssistantOutcome.Unavailable?"#FCEAEC":
                    result.Outcome==AssistantOutcome.Cancelled?"#EDEFF2":result.Outcome==AssistantOutcome.NoMatches?"#EAF5F2":"#F1F3F7");
                bubble.Child=AssistantAnswerRenderer.Render(answer,async action=>await SubmitFollowUpAsync(action,originStamp),ActionStyle);
            },true);
        }
        private void UpdateStarters(AssistantContext context)
        {
            _starters.Children.Clear();_starters.Visibility=_hasQuestion?Visibility.Collapsed:Visibility.Visible;
            if(_hasQuestion) return;
            if(AssistantCatalog.Find("items").IsAllowed(context)) AddYakumiChip(_starters,"Stock","How many items are in stock?");
            if(AssistantCatalog.Find("requests").IsAllowed(context)) AddYakumiChip(_starters,"My requests","Show my requests");
            if(AssistantCatalog.Find("tickets").IsAllowed(context)) AddYakumiChip(_starters,"Tickets","Show my tickets");
            if(context.IsLoggedIn && AssistantKnowledge.Articles.Any(a=>a.IsAllowed(context))) AddYakumiChip(_starters,"Page help","How do I use this page?");
        }
        internal void OnInputKeyDown(object sender,KeyEventArgs e)
        {
            if(e.Key!=Key.Enter) return;
            e.Handled=true;
            HandleEnter((e.KeyboardDevice.Modifiers & ModifierKeys.Shift)!=0);
        }
        internal void HandleEnter(bool multiline)
        {
            if(multiline)
            {
                int caret=_yakumiInput.SelectionStart;_yakumiInput.SelectedText=Environment.NewLine;
                _yakumiInput.CaretIndex=caret+Environment.NewLine.Length;
            }
            else YakumiSendAsync(_yakumiInput.Text);
        }


        private async void YakumiSendAsync(string text) {await SubmitAsync(text);}
        internal Task SubmitAsync(string text)
        {
            text=(text ?? "").Trim();if(_disposed || _chatCts!=null || text.Length==0) return Task.CompletedTask;
            if(text.Length>AiIntentRouter.MaxQuestionLength) {AddYakumiBubble(false,"Please shorten your question to "+AiIntentRouter.MaxQuestionLength+" characters.",false);return Task.CompletedTask;}
            var context=_context();RefreshContext(context);
            return RunTurnAsync(text,context,(ct,progress,partial)=>_service.AskAsync(text,context,ct,progress,partial));
        }
        internal Task SubmitFollowUpAsync(AssistantFollowUp action,string originStamp)
        {
            if(_disposed || _chatCts!=null) return Task.CompletedTask;
            var context=_context();RefreshContext(context);
            if(context.SessionStamp!=originStamp) return Task.CompletedTask;
            if(action.Choice!=null)
            {
                if(!_service.IsClarificationCurrent(action.Choice,context)) return Task.CompletedTask;
                return RunTurnAsync(action.Label,context,(ct,progress,partial)=>_service.ResolveClarificationAsync(action.Choice,context,ct,progress));
            }
            var snapshot=action.Request;
            return RunTurnAsync(action.Label,context,(ct,progress,partial)=>_service.ExecuteRequestAsync(snapshot,context,ct,progress));
        }
        private async Task RunTurnAsync(string text,AssistantContext context,Func<CancellationToken,Action<string>,Action<string>,Task<AssistantResult>> execute)
        {
            _hasQuestion=true;_starters.Visibility=Visibility.Collapsed;AddYakumiBubble(true,text,false);_yakumiInput.Clear();
            var pending=AddYakumiBubble(false,"Working on your question…",false);
            var cts=new CancellationTokenSource();_chatCts=cts;
            int revision=_chatRevision;bool awaiting=true;SetChatBusy(true);FocusInput();
            Action<string> progress=description=>
            {
                Action update=()=>
                {
                    if(!awaiting || !IsCurrentTurn(cts,revision) || cts.IsCancellationRequested || _context().SessionStamp!=context.SessionStamp) return;
                    KeepScrollPosition(()=>pending.Child=Label(description,AssistantAnswerRenderer.MessageSize,Ink),false);
                };
                if(Dispatcher.CheckAccess()) update();else Dispatcher.BeginInvoke(update);
            };
            try
            {
                // Cumulative drafts are plain text until completion. Coalesce queued worker updates so a
                // fast model cannot flood the dispatcher or replace a completed/cancelled turn.
                string draft=null;int scheduled=0;
                Action<string> partial=content=>
                {
                    Interlocked.Exchange(ref draft,content);
                    if(Interlocked.Exchange(ref scheduled,1)!=0) return;
                    Dispatcher.BeginInvoke(new Action(()=>
                    {
                        Interlocked.Exchange(ref scheduled,0);
                        if(!awaiting || !IsCurrentTurn(cts,revision) || cts.IsCancellationRequested || _context().SessionStamp!=context.SessionStamp) return;
                        string latest=Interlocked.CompareExchange(ref draft,null,null);
                        KeepScrollPosition(()=>pending.Child=Label(latest,AssistantAnswerRenderer.MessageSize,Ink),false);
                    }));
                };
                var result=await execute(cts.Token,progress,partial);awaiting=false;
                if(!IsCurrentTurn(cts,revision)) return;
                cts.Token.ThrowIfCancellationRequested();
                var current=_context();if(current.SessionStamp!=context.SessionStamp) {RefreshContext(current);return;}
                RenderAnswer(pending,result,context.SessionStamp);
            }
            catch(OperationCanceledException) {if(IsCurrentTurn(cts,revision)) RenderAnswer(pending,AssistantResult.Message(AssistantOutcome.Cancelled,AssistantEnglish.Stopped),context.SessionStamp);}
            catch(Exception) {if(IsCurrentTurn(cts,revision)) RenderAnswer(pending,AssistantResult.Message(AssistantOutcome.Unavailable,"I couldn’t finish that just now. Please try again in a moment."),context.SessionStamp);}
            finally
            {
                awaiting=false;
                if(IsCurrentTurn(cts,revision)) {_chatCts=null;SetChatBusy(false);}
                cts.Dispose();
            }
        }
        private bool IsCurrentTurn(CancellationTokenSource cts,int revision) {return !_disposed && revision==_chatRevision && ReferenceEquals(_chatCts,cts);}
        private void SetChatBusy(bool busy)
        {_sendButton.Width=busy?48:36;_sendButton.Content=Label(busy?"Stop":"➤",busy?13:15,Brushes.White);_sendButton.ToolTip=busy?"Stop reply":"Send";_settingsStopButton.Visibility=busy?Visibility.Visible:Visibility.Collapsed;System.Windows.Automation.AutomationProperties.SetName(_sendButton,busy?"Stop reply":"Send");}
        public void FocusInput() {if(!_disposed && !_showingSettings && !_yakumiMinimized) _yakumiInput.Focus();}
        public void Cancel() {if(_chatCts!=null) _chatCts.Cancel();CancelSettingsCheck();}
        public void NewConversation(string message=null)
        {
            Cancel();_chatRevision++;_chatCts=null;SetChatBusy(false);_service.Reset();_hasQuestion=false;_yakumiMessages.Children.Clear();
            _yakumiInput.Clear();_latestReply=null;_newReplyButton.Visibility=Visibility.Collapsed;
            AddYakumiBubble(false,message ?? AssistantEnglish.Welcome,false);
            UpdateStarters(_context());
        }
        public void Dispose() {if(_disposed) return;_disposed=true;Cancel();_service.Reset();}
    }
}
