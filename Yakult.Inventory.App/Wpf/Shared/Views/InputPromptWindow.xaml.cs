using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Shared.Views
{
    /// <summary>
    /// Generic single-line text prompt. WPF replacement for the old WinForms
    /// ShowInputDialog helper (e.g. Renewal Details' "Archive Reason" prompt).
    /// </summary>
    public partial class InputPromptWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string Caption { get; }
        public string PromptText { get; }

        private string _inputValue;
        public string InputValue
        {
            get => _inputValue;
            set { _inputValue = value; Raise(nameof(InputValue)); }
        }

        public string Result { get; private set; }

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        public InputPromptWindow(string promptText, string caption, string defaultValue = "")
        {
            InitializeComponent();
            Caption = caption;
            PromptText = promptText;
            InputValue = defaultValue;
            DataContext = this;

            OkCommand = new RelayCommand(() => { Result = InputValue; DialogResult = true; Close(); });
            CancelCommand = new RelayCommand(() => { Result = null; DialogResult = false; Close(); });

            Loaded += (s, e) => { InputTextBox.Focus(); InputTextBox.SelectAll(); };
        }

        /// <summary>
        /// Shows the prompt modally. Returns the entered text, or null if cancelled.
        /// </summary>
        public static string Show(Window owner, string promptText, string caption, string defaultValue = "")
        {
            var dlg = new InputPromptWindow(promptText, caption, defaultValue) { Owner = owner };
            return dlg.ShowDialog() == true ? dlg.Result : null;
        }
    }
}
