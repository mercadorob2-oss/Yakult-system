using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Detail.Views
{
    /// <summary>Conversation-thread notes (default-icon+name+timestamp+content) with an input
    /// box + Add Note button. Bound explicitly via dependency properties so it's reusable.</summary>
    public partial class NoteThreadControl : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource), typeof(IEnumerable), typeof(NoteThreadControl), new PropertyMetadata(null));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty NoteTextProperty = DependencyProperty.Register(
            nameof(NoteText), typeof(string), typeof(NoteThreadControl), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string NoteText
        {
            get => (string)GetValue(NoteTextProperty);
            set => SetValue(NoteTextProperty, value);
        }

        public static readonly DependencyProperty AddNoteCommandProperty = DependencyProperty.Register(
            nameof(AddNoteCommand), typeof(ICommand), typeof(NoteThreadControl), new PropertyMetadata(null));

        public ICommand AddNoteCommand
        {
            get => (ICommand)GetValue(AddNoteCommandProperty);
            set => SetValue(AddNoteCommandProperty, value);
        }

        public NoteThreadControl()
        {
            InitializeComponent();
        }
    }
}
