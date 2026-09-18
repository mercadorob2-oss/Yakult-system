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
    public partial class BranchEntry : UserControl
    {
        public string BranchName
        {
            get => txtName.Text.Trim();
            set => txtName.Text = value ?? "";
        }
        
        public string BranchDescription
        {
            get => txtDesc.Text.Trim();
            set => txtDesc.Text = value ?? "";
        }

        TextBox txtName;
        
        TextBox txtDesc;
        Button btnClose;   // ✕ button

        public BranchEntry()
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

            var lblName = new Label { AutoSize = true, Text = "Name", Left = 6, Top = 6 };
            var lblAddr = new Label { AutoSize = true, Text = "Address", Left = 192, Top = 6 };
            var lblDesc = new Label { AutoSize = true, Text = "Description", Left = 378, Top = 6 };

            txtName = new TextBox { Left = 6, Top = 24, Width = 180 };
            
            txtDesc = new TextBox { Left = 378, Top = 24, Width = 180 };

            btnClose = new Button
            {
                Text = "✕",
                FlatStyle = FlatStyle.System,
                Width = 26,
                Height = 22,
                Top = 4
            };
            this.Resize += (s, e) => btnClose.Left = this.ClientSize.Width - btnClose.Width - 6;
            btnClose.Click += (s, e) =>
            {
                var flp = Parent as FlowLayoutPanel;
                flp?.Controls.Remove(this);
                Dispose();
                flp?.PerformLayout();
            };

            Controls.Add(lblName);
            Controls.Add(lblAddr);
            Controls.Add(lblDesc);
            Controls.Add(txtName);
            
            Controls.Add(txtDesc);
            Controls.Add(btnClose);

            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            
            txtDesc.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            ResumeLayout(false);
            PerformLayout();
            btnClose.Left = this.ClientSize.Width - btnClose.Width - 6;
        }
    }
}