using System.Drawing;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Category
{
    /// <summary>
    /// Add-category dialog — name field only. Extracted from the inline Form built in
    /// ViewCategoryPage.BtnAdd_Click() so the WPF Categories page can reuse it unchanged.
    /// Name validation happens after the dialog closes (matches the original, which validated
    /// in BtnAdd_Click after ShowDialog() returned OK, not inside this Form).
    /// </summary>
    public class AddCategoryDialog : Form
    {
        private TextBox _txtName;

        public string CategoryName => _txtName.Text;

        public AddCategoryDialog()
        {
            Text = "Add New Category";
            Size = new Size(400, 200);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblName = new Label { Text = "Category Name:", Left = 20, Top = 20, Width = 120 };
            _txtName = new TextBox { Left = 150, Top = 20, Width = 200 };

            var btnOk = new Button { Text = "Save", Left = 150, Top = 80, Width = 80, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Cancel", Left = 240, Top = 80, Width = 80, DialogResult = DialogResult.Cancel };

            Controls.AddRange(new Control[] { lblName, _txtName, btnOk, btnCancel });
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
