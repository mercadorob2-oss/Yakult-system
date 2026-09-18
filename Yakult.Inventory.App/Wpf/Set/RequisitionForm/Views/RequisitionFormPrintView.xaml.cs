using System.Windows;
using System.Windows.Controls;

namespace Yakult.Inventory.App.WPF.Set.RequisitionForm.Views
{
    public partial class RequisitionFormPrintView : UserControl
    {
        // Uniform shrink factor (1.0 = full size) applied to every font size and spacing
        // value in this form. RequisitionFormPrintService reduces it when a copy's line
        // items don't fit within its half of the page, so two copies always print on one
        // page instead of spilling the second copy onto a new page.
        public static readonly DependencyProperty FontScaleProperty =
            DependencyProperty.Register(nameof(FontScale), typeof(double), typeof(RequisitionFormPrintView),
                new PropertyMetadata(1.0));

        public double FontScale
        {
            get => (double)GetValue(FontScaleProperty); 
            set => SetValue(FontScaleProperty, value);
        }

        public RequisitionFormPrintView()
        {
            InitializeComponent();
        }
    }
}
