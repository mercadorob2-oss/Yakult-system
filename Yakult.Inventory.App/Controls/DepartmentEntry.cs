using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Controls
{
    public partial class DepartmentEntry : UserControl
    {
        public string DeptName
        {
            get => txtName.Text.Trim();
            set => txtName.Text = value ?? "";
        }
        public string DeptDescription
        {
            get => txtDesc.Text.Trim();
            set => txtDesc.Text = value ?? "";
        }

        TextBox txtName;
        TextBox txtDesc;
        Button btnClose;    // ✕ button

        public DepartmentEntry()
        {
            AutoSize = false;
            MinimumSize = new Size(500, 84);
            Size = new Size(600, 84);
            Margin = new Padding(0, 4, 0, 4);
            BackColor = SystemColors.Window;
            BorderStyle = BorderStyle.FixedSingle;

            BuildUi();
        }

        private void BuildUi()
        {
            Padding = new Padding(6, 6, 6, 6);

            // Labels
            var lblName = new Label { AutoSize = true, Text = "Name", Left = 6, Top = 6 };
            var lblDesc = new Label { AutoSize = true, Text = "Description", Left = 274, Top = 6 };

            // Inputs
            txtName = new TextBox { Left = 6, Top = 24, Width = 260 };
            txtDesc = new TextBox { Left = 274, Top = 24, Width = 260 };

            // ✕ button (top-right)
            btnClose = new Button
            {
                Text = "✕",
                FlatStyle = FlatStyle.System,
                Width = 26,
                Height = 22,
                Top = 4
            };
            // position it on resize so it stays top-right
            this.Resize += (s, e) => btnClose.Left = this.ClientSize.Width - btnClose.Width - 6;
            btnClose.Click += (s, e) =>
            {
                var flp = Parent as FlowLayoutPanel;
                flp?.Controls.Remove(this);
                Dispose();
                flp?.PerformLayout(); // optional: immediate reflow
            };

            Controls.Add(lblName);
            Controls.Add(lblDesc);
            Controls.Add(txtName);
            Controls.Add(txtDesc);
            Controls.Add(btnClose);

            // Stretch second textbox when parent widens
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtDesc.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            ResumeLayout(false);
            PerformLayout();
            // ensure initial right alignment
            btnClose.Left = this.ClientSize.Width - btnClose.Width - 6;
        }
    }
}
