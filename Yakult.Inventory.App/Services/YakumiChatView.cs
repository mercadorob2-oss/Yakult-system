using System;
using System.IO;
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
        private readonly YakumiAssistantService _service;
        private readonly Func<AssistantContext> _context;
        private Border _yakumiPanel;
        private StackPanel _yakumiMessages;
        private ScrollViewer _yakumiScroll;
        private TextBox _yakumiInput;
        private StackPanel _yakumiBody;
        private StackPanel _yakumiSettings;
        private TextBox _setUrlBox;
        private ComboBox _setModelBox;
        private TextBlock _setStatusText;
        private TextBlock _yakumiSubtitle;
        private Button _yakumiMinButton;
        private Button _sendButton;
        private bool _yakumiMinimized;
        private bool _disposed;
        private string _stamp;
        private CancellationTokenSource _chatCts;
        private readonly WrapPanel _pages=new WrapPanel {Margin=new Thickness(12,0,12,0)};
        public Action CloseRequested {get;set;}

        public YakumiChatView(Func<AssistantContext> context) : this(context,new YakumiAssistantService()) { }
        internal YakumiChatView(Func<AssistantContext> context,YakumiAssistantService service)
        {
            _context=context;_service=service;FontFamily=new FontFamily("Segoe UI");Width=350;Height=540;
            BuildYakumiPanel();Content=_yakumiPanel;NewConversation();
        }
        public void RefreshContext(AssistantContext context)
        {
            if(_disposed) return;
            if(_stamp!=null && _stamp!=context.SessionStamp) NewConversation("Your session, permissions or database changed. Previous chat was cleared.");
            _stamp=context.SessionStamp;
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
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, darkHeader ? Brush("#29364D") : Accent, "Surface"));
            hover.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1), "Surface"));
            if (darkHeader) hover.Setters.Add(new Setter(Border.BackgroundProperty, Brush("#29364D"), "Surface"));
            template.Triggers.Add(hover);
            var press = new Trigger { Property = Button.IsPressedProperty, Value = true };
            press.Setters.Add(new Setter(UIElement.OpacityProperty, 0.72, "Surface"));
            template.Triggers.Add(press);
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
            focus.Setters.Add(new Setter(Border.BorderBrushProperty, darkHeader ? Brush("#FF9AA8") : Accent, "Surface"));
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

        private static BitmapImage YakumiAvatarImage()
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

            var header = new Border
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
            headerGrid.Children.Add(YakumiAvatar(34, 16));
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(Label("Yakumi", 14, Brushes.White, FontWeights.Bold));
            _yakumiSubtitle = Label(YakumiSubtitleText(), 10, Brush("#FBD9DE"));
            titles.Children.Add(_yakumiSubtitle);
            Grid.SetColumn(titles, 1);
            headerGrid.Children.Add(titles);
            _yakumiMinButton = new Button { Style = ActionStyle, Content = Label("—", 13, Brushes.White, FontWeights.Bold), Foreground = Brushes.White, Padding = new Thickness(8, 2, 8, 2), Cursor = Cursors.Hand, ToolTip = "Minimize" };
            _yakumiMinButton.Click += (_, __) => ToggleYakumiMinimized();
            Grid.SetColumn(_yakumiMinButton, 2);
            headerGrid.Children.Add(_yakumiMinButton);
            var gear = new Button { Style = ActionStyle, Content = Label("⚙", 13, Brushes.White, FontWeights.Bold), Foreground = Brushes.White, Padding = new Thickness(8, 2, 8, 2), Cursor = Cursors.Hand, ToolTip = "AI settings" };
            gear.Click += (_, __) => ToggleYakumiSettings();
            Grid.SetColumn(gear, 2);
            headerGrid.Children.Add(gear);
            Grid.SetColumn(_yakumiMinButton, 3);
            var close = new Button { Style = ActionStyle, Content = Label("✕", 12, Brushes.White, FontWeights.Bold), Foreground = Brushes.White, Padding = new Thickness(8, 2, 8, 2), Cursor = Cursors.Hand, ToolTip = "Close" };
            close.Click += (_, __) => { Cancel(); CloseRequested?.Invoke(); };
            Grid.SetColumn(close, 4);
            headerGrid.Children.Add(close);
            header.Child = headerGrid;
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            _yakumiBody = new StackPanel();
            Grid.SetRow(_yakumiBody, 1);
            root.Children.Add(_yakumiBody);

            _yakumiScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 300, Padding = new Thickness(12, 10, 12, 4) };
            _yakumiMessages = new StackPanel();
            _yakumiScroll.Content = _yakumiMessages;
            _yakumiBody.Children.Add(_yakumiScroll);

            var chips = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 6, 12, 0) };
            AddYakumiChip(chips, "IT Support", "How do I contact IT support?");
            AddYakumiChip(chips, "System Access", "How do I request access to a system?");
            AddYakumiChip(chips, "Inventory", "How do I check item stock levels?");
            AddYakumiChip(chips, "Report Issue", "How do I report a system issue?");
            AddYakumiChip(chips, "Something else", null);
            _yakumiBody.Children.Add(_pages);
            _yakumiBody.Children.Add(chips);

            var inputRow = new Grid { Margin = new Thickness(12, 8, 12, 12) };
            inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var inputBorder = new Border { Background = Brushes.White, BorderBrush = Brush("#D5D9E0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(12, 6, 12, 6), VerticalAlignment = VerticalAlignment.Center };
            _yakumiInput = new TextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Ink, FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center };
            _yakumiInput.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    YakumiSendAsync(_yakumiInput.Text);
                    e.Handled = true;
                }
            };
            inputBorder.Child = _yakumiInput;
            inputRow.Children.Add(inputBorder);
            var send = new Button { Style = ActionStyle, Background = Accent, BorderBrush = Accent, Foreground = Brushes.White, Width = 36, Height = 36, Margin = new Thickness(8, 0, 0, 0), Cursor = Cursors.Hand, ToolTip = "Send" };
            send.Content = new TextBlock { Text = "➤", FontSize = 15, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _sendButton=send;
            send.Click += (_, __) => {if(_chatCts!=null) Cancel();else YakumiSendAsync(_yakumiInput.Text);};
            Grid.SetColumn(send, 1);
            inputRow.Children.Add(send);
            _yakumiBody.Children.Add(inputRow);

            // Settings view (hidden until the gear is clicked).
            _yakumiSettings = new StackPanel { Margin = new Thickness(14, 10, 14, 12), Visibility = Visibility.Collapsed };
            _yakumiSettings.Children.Add(Label("AI Server", 13, Ink, FontWeights.Bold));
            _yakumiSettings.Children.Add(Label("Server URL", 10, Muted, FontWeights.Normal, new Thickness(0, 8, 0, 3)));
            _setUrlBox = new TextBox { Background = Brushes.White, BorderBrush = Brush("#D5D9E0"), BorderThickness = new Thickness(1), Foreground = Ink, FontSize = 12, Padding = new Thickness(8, 6, 8, 6) };
            _yakumiSettings.Children.Add(_setUrlBox);
            _yakumiSettings.Children.Add(Label("Model", 10, Muted, FontWeights.Normal, new Thickness(0, 8, 0, 3)));
            var modelRow = new Grid();
            modelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            modelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _setModelBox = new ComboBox { IsEditable = true, FontSize = 12, Padding = new Thickness(6, 4, 6, 4), VerticalContentAlignment = VerticalAlignment.Center };
            modelRow.Children.Add(_setModelBox);
            var reloadModels = new Button { Style = ActionStyle, Background = Brushes.White, BorderBrush = Brush("#D5D9E0"), BorderThickness = new Thickness(1), Foreground = Ink, Content = "⟳", FontSize = 13, Width = 34, Margin = new Thickness(6, 0, 0, 0), Cursor = Cursors.Hand, ToolTip = "Reload model list from server" };
            reloadModels.Click += async (_, __) => await LoadYakumiModelsAsync();
            Grid.SetColumn(reloadModels, 1);
            modelRow.Children.Add(reloadModels);
            _yakumiSettings.Children.Add(modelRow);
            var settingsButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            var testButton = new Button { Style = ActionStyle, Content = "Test connection", FontSize = 11, Background = Brushes.White, BorderBrush = Accent, BorderThickness = new Thickness(1), Foreground = Accent, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand };
            testButton.Click += async (_, __) => await TestYakumiConnectionAsync();
            settingsButtons.Children.Add(testButton);
            var saveButton = new Button { Style = ActionStyle, Content = "Save", FontSize = 11, FontWeight = FontWeights.Bold, Background = Accent, BorderBrush = Accent, Foreground = Brushes.White, Padding = new Thickness(16, 7, 16, 7), Cursor = Cursors.Hand };
            saveButton.Click += (_, __) => SaveYakumiSettings();
            settingsButtons.Children.Add(saveButton);
            _yakumiSettings.Children.Add(settingsButtons);
            _setStatusText = Label("", 10, Muted, FontWeights.Normal, new Thickness(0, 8, 0, 0));
            _setStatusText.TextWrapping = TextWrapping.Wrap;
            _yakumiSettings.Children.Add(_setStatusText);
            Grid.SetRow(_yakumiSettings, 1);
            root.Children.Add(_yakumiSettings);
        }

        private static string YakumiSubtitleText()
        {
            try { return AppConfig.OllamaModel + " @ " + new Uri(AppConfig.OllamaBaseUrl).Host; }
            catch { return "Your friendly Yakult AI assistant"; }
        }

        private void ToggleYakumiSettings()
        {
            bool showSettings = _yakumiSettings.Visibility != Visibility.Visible;
            _yakumiSettings.Visibility = showSettings ? Visibility.Visible : Visibility.Collapsed;
            _yakumiBody.Visibility = showSettings ? Visibility.Collapsed : Visibility.Visible;
            if (showSettings)
            {
                _setUrlBox.Text = AppConfig.OllamaBaseUrl;
                SetYakumiStatus("", Muted);
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
            string url = (_setUrlBox.Text ?? "").Trim();
            if (url.Length == 0) url = AppConfig.OllamaBaseUrl;
            SetYakumiStatus("Loading models…", Muted);
            try
            {
                var models = await OllamaChatService.GetInstalledModelsAsync(url);
                _setModelBox.ItemsSource = models;
                string current = AppConfig.OllamaModel;
                _setModelBox.SelectedItem = null;
                foreach (var m in models)
                {
                    if (string.Equals(m, current, StringComparison.OrdinalIgnoreCase))
                    { _setModelBox.SelectedItem = m; break; }
                }
                if (_setModelBox.SelectedItem == null) _setModelBox.Text = current;
                SetYakumiStatus(models.Count == 0 ? "No models found on that server." : models.Count + " model(s) found.", Brush("#1E8449"));
            }
            catch (Exception ex)
            {
                SetYakumiStatus("Couldn't list models: " + ex.Message, Accent);
            }
        }

        private async System.Threading.Tasks.Task TestYakumiConnectionAsync()
        {
            string url = (_setUrlBox.Text ?? "").Trim();
            if (url.Length == 0) url = AppConfig.OllamaBaseUrl;
            SetYakumiStatus("Testing…", Muted);
            try
            {
                string version = await OllamaChatService.PingAsync(url);
                SetYakumiStatus("Connected • Ollama " + version, Brush("#1E8449"));
            }
            catch (Exception ex)
            {
                SetYakumiStatus("Not reachable: " + ex.Message, Accent);
            }
        }

        private void SaveYakumiSettings()
        {
            string url = (_setUrlBox.Text ?? "").Trim();
            string model = ((_setModelBox.SelectedItem as string) ?? _setModelBox.Text ?? "").Trim();
            Uri address;
            if(!Uri.TryCreate(url,UriKind.Absolute,out address) || (address.Scheme!="http" && address.Scheme!="https") || !string.IsNullOrEmpty(address.UserInfo))
            {SetYakumiStatus("Use an HTTP/HTTPS server URL without credentials.",Accent);return;}
            if (url.Length == 0) { SetYakumiStatus("Server URL is required.", Accent); return; }
            if (model.Length == 0) { SetYakumiStatus("Model is required.", Accent); return; }
            try
            {
                AppConfig.SetOllamaBaseUrl(url);
                AppConfig.SetOllamaModel(model);
                _yakumiSubtitle.Text = YakumiSubtitleText();
                _service.Reset();_pages.Children.Clear();
                SetYakumiStatus("Saved. New chats use " + model + ".", Brush("#1E8449"));
            }
            catch (Exception ex)
            {
                SetYakumiStatus("Couldn't save: " + ex.Message, Accent);
            }
        }

        private void ToggleYakumiMinimized()
        {
            _yakumiMinimized = !_yakumiMinimized;
            _yakumiBody.Visibility = _yakumiMinimized ? Visibility.Collapsed : Visibility.Visible;
            _yakumiMinButton.Content = Label(_yakumiMinimized ? "+" : "—", 13, Brushes.White, FontWeights.Bold);
            _yakumiMinButton.ToolTip = _yakumiMinimized ? "Restore" : "Minimize";
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
            FrameworkElement row;
            var bubble = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 0, 0, 8),
                MaxWidth = 250,
                Background = isUser ? Accent : Brush("#F0F1F5")
            };
            var stack = new StackPanel();
            var message = new TextBlock { Text = text, FontSize = 12, Foreground = isUser ? Brushes.White : Ink, TextWrapping = TextWrapping.Wrap };
            message.Tag = "msg";
            stack.Children.Add(message);
            var caption = new TextBlock { Text = "live data", FontSize = 9, Foreground = isUser ? Brush("#FBD9DE") : Muted, Visibility = live ? Visibility.Visible : Visibility.Collapsed };
            caption.Tag = "cap";
            stack.Children.Add(caption);
            bubble.Child = stack;
            bubble.Tag = stack;

            if (isUser)
            {
                bubble.HorizontalAlignment = HorizontalAlignment.Right;
                row = bubble;
            }
            else
            {
                var wrap = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 0) };
                wrap.Children.Add(YakumiAvatar(24, 11));
                wrap.Children.Add(bubble);
                row = wrap;
            }
            _yakumiMessages.Children.Add(row);
            while(_yakumiMessages.Children.Count>40) _yakumiMessages.Children.RemoveAt(0);
            _yakumiScroll.Dispatcher.BeginInvoke(new Action(() => _yakumiScroll.ScrollToBottom()), System.Windows.Threading.DispatcherPriority.Background);
            return bubble;
        }

        private static void ReplaceYakumiBubble(Border bubble, string text, bool live)
        {
            if (bubble == null) return;
            var stack = bubble.Tag as StackPanel;
            if (stack == null) return;
            foreach (var child in stack.Children)
            {
                var block = child as TextBlock;
                if (block == null) continue;
                if (Equals(block.Tag, "msg")) block.Text = text;
                if (Equals(block.Tag, "cap")) block.Visibility = live ? Visibility.Visible : Visibility.Collapsed;
            }
        }


        private async void YakumiSendAsync(string text) {await SubmitAsync(text);}
        internal async Task SubmitAsync(string text)
        {
            text=(text ?? "").Trim();if(_disposed || _chatCts!=null || text.Length==0) return;
            if(text.Length>AiIntentRouter.MaxQuestionLength) {AddYakumiBubble(false,"Please shorten your question to "+AiIntentRouter.MaxQuestionLength+" characters.",false);return;}
            var context=_context();RefreshContext(context);AddYakumiBubble(true,text,false);_yakumiInput.Clear();_pages.Children.Clear();
            var pending=AddYakumiBubble(false,"Yakumi is typing…",false);
            var cts=new CancellationTokenSource();_chatCts=cts;
            _sendButton.Content=Label("■",14,Brushes.White);_sendButton.ToolTip="Cancel reply";
            try
            {
                var result=await _service.AskAsync(text,context,cts.Token);
                if(_disposed || cts.IsCancellationRequested) return;
                var current=_context();if(current.SessionStamp!=context.SessionStamp) {RefreshContext(current);return;}
                ReplaceYakumiBubble(pending,result.Text,result.IsLive);
                if(result.Request!=null && result.IsLive)
                {
                    if(result.Request.Operation==AssistantOperation.Count && result.TotalRecords>0) AddYakumiChip(_pages,"Show records","show them");
                    if(result.Request.Page>0) AddYakumiChip(_pages,"Previous page","previous page");
                    if(result.HasNextPage) AddYakumiChip(_pages,"Next page","next page");
                }
                _yakumiScroll.ScrollToBottom();
            }
            catch(OperationCanceledException) {if(!_disposed) ReplaceYakumiBubble(pending,"(Cancelled.)",false);}
            catch(Exception) {if(!_disposed) ReplaceYakumiBubble(pending,"This question could not be completed. Please try a more specific lookup. No records were changed.",false);}
            finally
            {
                if(ReferenceEquals(_chatCts,cts)) _chatCts=null;cts.Dispose();
                if(!_disposed) {_sendButton.Content=Label("➤",15,Brushes.White);_sendButton.ToolTip="Send";_yakumiInput.Focus();}
            }
        }
        public void FocusInput() {if(!_disposed) _yakumiInput.Focus();}
        public void Cancel() {if(_chatCts!=null) _chatCts.Cancel();}
        public void NewConversation(string message=null)
        {
            Cancel();_service.Reset();_yakumiMessages.Children.Clear();_pages.Children.Clear();
            AddYakumiBubble(false,message ?? "Hi there! I'm Yakumi, your friendly Yakult AI assistant. How can I help you today?",false);
        }
        public void Dispose() {if(_disposed) return;_disposed=true;Cancel();_service.Reset();}
    }
}
