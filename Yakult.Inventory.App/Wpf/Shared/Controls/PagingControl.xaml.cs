using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Yakult.Inventory.App.WPF.Shared.Controls
{
    /// <summary>
    /// Shared First/Prev/Next/Last pagination bar used by list pages converted from
    /// WinForms (Asset, Warranty, RepairedItems, ItemMovementAudit, Inventory).
    /// Bind the four commands and a page-info string from the page's ViewModel.
    /// </summary>
    public partial class PagingControl : UserControl
    {
        public static readonly DependencyProperty FirstPageCommandProperty =
            DependencyProperty.Register(nameof(FirstPageCommand), typeof(ICommand), typeof(PagingControl));

        public static readonly DependencyProperty PrevPageCommandProperty =
            DependencyProperty.Register(nameof(PrevPageCommand), typeof(ICommand), typeof(PagingControl));

        public static readonly DependencyProperty NextPageCommandProperty =
            DependencyProperty.Register(nameof(NextPageCommand), typeof(ICommand), typeof(PagingControl));

        public static readonly DependencyProperty LastPageCommandProperty =
            DependencyProperty.Register(nameof(LastPageCommand), typeof(ICommand), typeof(PagingControl));

        public static readonly DependencyProperty PageInfoTextProperty =
            DependencyProperty.Register(nameof(PageInfoText), typeof(string), typeof(PagingControl), new PropertyMetadata(string.Empty));

        public ICommand FirstPageCommand
        {
            get => (ICommand)GetValue(FirstPageCommandProperty);
            set => SetValue(FirstPageCommandProperty, value);
        }

        public ICommand PrevPageCommand
        {
            get => (ICommand)GetValue(PrevPageCommandProperty);
            set => SetValue(PrevPageCommandProperty, value);
        }

        public ICommand NextPageCommand
        {
            get => (ICommand)GetValue(NextPageCommandProperty);
            set => SetValue(NextPageCommandProperty, value);
        }

        public ICommand LastPageCommand
        {
            get => (ICommand)GetValue(LastPageCommandProperty);
            set => SetValue(LastPageCommandProperty, value);
        }

        public string PageInfoText
        {
            get => (string)GetValue(PageInfoTextProperty);
            set => SetValue(PageInfoTextProperty, value);
        }

        public PagingControl()
        {
            InitializeComponent();
        }
    }
}
