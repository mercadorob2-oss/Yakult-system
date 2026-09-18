using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.Models.BorrowItems;

namespace Yakult.Inventory.App.Wpf.BorrowItems
{
    public sealed class WpfBorrowBoardHostControl : UserControl
    {
        private readonly ElementHost _host;
        private readonly WpfBorrowBoardWorkspace _workspace;

        public event EventHandler<BorrowLogRow> ReturnBorrowClicked;
        public event EventHandler<BorrowLogRow> BorrowRowDoubleClicked;

        public WpfBorrowBoardHostControl()
        {
            BackColor = Color.FromArgb(241, 244, 247);
            Dock = DockStyle.Fill;

            _workspace = new WpfBorrowBoardWorkspace();

            _workspace.ReturnBorrowClicked += (s, row) => ReturnBorrowClicked?.Invoke(this, row);
            _workspace.BorrowRowDoubleClicked += (s, row) => BorrowRowDoubleClicked?.Invoke(this, row);

            _host = new ElementHost
            {
                Dock = DockStyle.Fill,
                BackColorTransparent = true,
                Child = _workspace
            };

            Controls.Add(_host);
        }

        public void BindData(List<BorrowLogRow> openItems)
        {
            _workspace.BindData(openItems);
        }

        public void RefreshElapsed()
        {
            _workspace.RefreshElapsed();
        }
    }
}
