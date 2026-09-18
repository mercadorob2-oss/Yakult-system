using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Converters
{
    /// <summary>
    /// Lightweight integer spinner (NumericUpDown replacement).
    /// Exposes Value, MinValue, MaxValue dependency properties with coercion.
    /// Keyboard: Up/Down arrows and scroll wheel are supported.
    /// No third-party dependencies needed.
    /// </summary>
    public class IntegerSpinner : Control
    {
        static IntegerSpinner()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(IntegerSpinner),
                new FrameworkPropertyMetadata(typeof(IntegerSpinner)));
        }

        // ── Dependency properties ─────────────────────────────────────────────
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(int), typeof(IntegerSpinner),
                new FrameworkPropertyMetadata(0,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnValueChanged, CoerceValue));

        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register(nameof(MinValue), typeof(int), typeof(IntegerSpinner),
                new PropertyMetadata(0, OnRangeChanged));

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(nameof(MaxValue), typeof(int), typeof(IntegerSpinner),
                new PropertyMetadata(int.MaxValue, OnRangeChanged));

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public int MinValue
        {
            get => (int)GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }

        public int MaxValue
        {
            get => (int)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        // ── Template children ─────────────────────────────────────────────────
        private TextBox _textBox;
        private Button  _btnUp;
        private Button  _btnDown;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _textBox = GetTemplateChild("PART_TextBox") as TextBox;
            _btnUp   = GetTemplateChild("PART_Up")     as Button;
            _btnDown = GetTemplateChild("PART_Down")   as Button;

            if (_textBox != null)
            {
                _textBox.Text = Value.ToString();
                _textBox.PreviewKeyDown       += TextBox_PreviewKeyDown;
                _textBox.PreviewMouseWheel    += TextBox_MouseWheel;
                _textBox.LostFocus            += TextBox_LostFocus;
                _textBox.GotFocus             += (s, e) => _textBox.SelectAll();
            }

            if (_btnUp   != null) _btnUp.Click   += (s, e) => StepValue(+1);
            if (_btnDown != null) _btnDown.Click += (s, e) => StepValue(-1);
        }

        // ── Input handlers ────────────────────────────────────────────────────
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up)   { StepValue(+1); e.Handled = true; }
            if (e.Key == Key.Down) { StepValue(-1); e.Handled = true; }
        }

        private void TextBox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            StepValue(e.Delta > 0 ? +1 : -1);
            e.Handled = true;
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(_textBox.Text, out int v))
                Value = v;
            else
                _textBox.Text = Value.ToString();
        }

        private void StepValue(int delta)
        {
            Value = Math.Max(MinValue, Math.Min(Value + delta, MaxValue));
        }

        // ── Coerce & change callbacks ─────────────────────────────────────────
        private static object CoerceValue(DependencyObject d, object baseVal)
        {
            var s = (IntegerSpinner)d;
            int v = (int)baseVal;
            return Math.Max(s.MinValue, Math.Min(v, s.MaxValue));
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var s = (IntegerSpinner)d;
            if (s._textBox != null && s._textBox.Text != e.NewValue.ToString())
                s._textBox.Text = e.NewValue.ToString();
        }

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var s = (IntegerSpinner)d;
            s.CoerceValue(ValueProperty);
        }
    }
}
