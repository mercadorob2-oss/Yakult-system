using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Yakult.Inventory.App.WPF.Shared.Controls
{
    public class TokenSearchBox : UserControl
    {
        // ── UI fields (built in code — no XAML-named elements) ────────────────
        private Border    _outerBorder;
        private WrapPanel _tokensPanel;
        private TextBox   _inputBox;

        // ── Dependency Properties ─────────────────────────────────────────────

        public static readonly DependencyProperty TokensProperty =
            DependencyProperty.Register(
                nameof(Tokens),
                typeof(ObservableCollection<string>),
                typeof(TokenSearchBox),
                new PropertyMetadata(null, OnTokensPropertyChanged));

        public static readonly DependencyProperty PartialTextProperty =
            DependencyProperty.Register(
                nameof(PartialText),
                typeof(string),
                typeof(TokenSearchBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public ObservableCollection<string> Tokens
        {
            get => (ObservableCollection<string>)GetValue(TokensProperty);
            set => SetValue(TokensProperty, value);
        }

        public string PartialText
        {
            get => (string)GetValue(PartialTextProperty);
            set => SetValue(PartialTextProperty, value);
        }

        private static void OnTokensPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var box = (TokenSearchBox)d;

            if (e.OldValue is ObservableCollection<string> old)
                old.CollectionChanged -= box.OnTokensCollectionChanged;

            if (e.NewValue is ObservableCollection<string> @new)
                @new.CollectionChanged += box.OnTokensCollectionChanged;

            box.RebuildChips();
        }

        private void OnTokensCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            => RebuildChips();

        // ── Construction ──────────────────────────────────────────────────────

        public TokenSearchBox()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            _inputBox = new TextBox
            {
                MinWidth          = 80,
                BorderThickness   = new Thickness(0),
                Background        = Brushes.Transparent,
                FontFamily        = new FontFamily("Segoe UI"),
                FontSize          = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Padding           = new Thickness(3, 2, 3, 2),
                Margin            = new Thickness(1, 2, 1, 2)
            };
            _inputBox.KeyDown     += InputBox_KeyDown;
            _inputBox.TextChanged += InputBox_TextChanged;
            _inputBox.GotFocus    += InputBox_GotFocus;
            _inputBox.LostFocus   += InputBox_LostFocus;

            _tokensPanel = new WrapPanel
            {
                Orientation       = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            _tokensPanel.Children.Add(_inputBox);

            var scroller = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                MaxHeight = 80,
                Focusable = false,
                Content   = _tokensPanel
            };

            _outerBorder = new Border
            {
                BorderBrush     = new SolidColorBrush(Color.FromRgb(0xB0, 0xBA, 0xC8)),
                BorderThickness = new Thickness(1),
                Background      = Brushes.White,
                CornerRadius    = new CornerRadius(5),
                MinHeight       = 30,
                Padding         = new Thickness(3, 2, 3, 2),
                Child           = scroller
            };

            Content = _outerBorder;
            MouseLeftButtonDown += OnContainerClick;
        }

        // ── Chip building ─────────────────────────────────────────────────────

        private void RebuildChips()
        {
            if (_tokensPanel == null) return;

            _tokensPanel.Children.Clear();

            var tokens = Tokens;
            if (tokens != null)
            {
                foreach (var token in tokens)
                    _tokensPanel.Children.Add(CreateChip(token));
            }

            _tokensPanel.Children.Add(_inputBox);
        }

        private FrameworkElement CreateChip(string text)
        {
            var label = new TextBlock
            {
                Text              = text,
                FontFamily        = new FontFamily("Segoe UI"),
                FontSize          = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground        = new SolidColorBrush(Color.FromRgb(0x1A, 0x23, 0x33))
            };

            var removeBtn = new Button
            {
                Content           = "✕",
                FontSize          = 9,
                Background        = Brushes.Transparent,
                BorderThickness   = new Thickness(0),
                Padding           = new Thickness(3, 0, 1, 0),
                Cursor            = Cursors.Hand,
                Foreground        = new SolidColorBrush(Color.FromRgb(0x7A, 0x8A, 0x9A)),
                VerticalAlignment = VerticalAlignment.Center,
                Tag               = text,
                Focusable         = false
            };
            removeBtn.Click += (s, e) =>
            {
                Tokens?.Remove(text);
                e.Handled = true;
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(label);
            panel.Children.Add(removeBtn);

            return new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(0xEB, 0xF5, 0xFF)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(0x4E, 0x9A, 0xFC)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(10),
                Margin          = new Thickness(2, 2, 2, 2),
                Padding         = new Thickness(6, 2, 4, 2),
                Child           = panel
            };
        }

        // ── Input handling ────────────────────────────────────────────────────

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                CommitCurrentText();
                e.Handled = e.Key == Key.Enter;
            }
            else if (e.Key == Key.Back && string.IsNullOrEmpty(_inputBox.Text) && Tokens?.Count > 0)
            {
                Tokens.RemoveAt(Tokens.Count - 1);
                e.Handled = true;
            }
        }

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = _inputBox.Text;

            // Comma commits the current text as a token
            if (text.EndsWith(","))
            {
                _inputBox.Text = string.Empty;
                string trimmed = text.TrimEnd(',').Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    Tokens?.Add(trimmed);
                return;
            }

            // Live partial-text filter while typing
            SetCurrentValue(PartialTextProperty, text);
        }

        private void CommitCurrentText()
        {
            string text = _inputBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                _inputBox.Text = string.Empty;
                SetCurrentValue(PartialTextProperty, string.Empty);
                Tokens?.Add(text);
            }
        }

        // ── Focus visual feedback ─────────────────────────────────────────────

        private void InputBox_GotFocus(object sender, RoutedEventArgs e)
            => _outerBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4E, 0x9A, 0xFC));

        private void InputBox_LostFocus(object sender, RoutedEventArgs e)
            => _outerBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xB0, 0xBA, 0xC8));

        private void OnContainerClick(object sender, MouseButtonEventArgs e)
            => _inputBox?.Focus();
    }
}
