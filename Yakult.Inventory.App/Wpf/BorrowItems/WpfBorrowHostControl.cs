using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.Models.BorrowItems;

namespace Yakult.Inventory.App.Wpf.BorrowItems
{
    public sealed class WpfBorrowHostControl : UserControl
    {
        private readonly ElementHost _host;
        private readonly WpfBorrowWorkspace _workspace;

        public event EventHandler ReturnSelectedClicked;
        public event EventHandler DeleteBorrowClicked;
        public event EventHandler ExportCsvClicked;
        public event EventHandler PrevPageClicked;
        public event EventHandler NextPageClicked;
        public event EventHandler RefreshClicked;
        public event EventHandler BackToPortalClicked;
        public event EventHandler<BorrowLogRow> BorrowRowDoubleClicked;
        public event EventHandler<BorrowLogRow> BorrowRowSelected;

        public WpfBorrowHostControl()
        {
            BackColor = Color.FromArgb(241, 244, 247);
            Dock = DockStyle.Fill;

            _workspace = new WpfBorrowWorkspace();
            
            // Wire WPF events back to WinForms Host
            _workspace.ReturnSelectedClicked += (s, e) => ReturnSelectedClicked?.Invoke(this, e);
            _workspace.DeleteBorrowClicked += (s, e) => DeleteBorrowClicked?.Invoke(this, e);
            _workspace.ExportCsvClicked += (s, e) => ExportCsvClicked?.Invoke(this, e);
            _workspace.PrevPageClicked += (s, e) => PrevPageClicked?.Invoke(this, e);
            _workspace.NextPageClicked += (s, e) => NextPageClicked?.Invoke(this, e);
            _workspace.RefreshClicked += (s, e) => RefreshClicked?.Invoke(this, e);
            _workspace.BackToPortalClicked += (s, e) => BackToPortalClicked?.Invoke(this, e);
            _workspace.BorrowRowDoubleClicked += (s, e) => BorrowRowDoubleClicked?.Invoke(this, e);
            _workspace.BorrowRowSelected += (s, e) => BorrowRowSelected?.Invoke(this, e);

            _host = new ElementHost
            {
                Dock = DockStyle.Fill,
                BackColorTransparent = true,
                Child = _workspace
            };

            Controls.Add(_host);
        }

        public int SelectedTabIndex
        {
            get => _workspace.SelectedTabIndex;
            set => _workspace.SelectedTabIndex = value;
        }

        public BorrowLogRow SelectedOpenRow => _workspace.SelectedOpenRow;
        public BorrowLogRow SelectedHistoryRow => _workspace.SelectedHistoryRow;

        public void BindData(IEnumerable<BorrowLogRow> open, IEnumerable<BorrowLogRow> history)
        {
            _workspace.BindData(open, history);
        }

        public void SetKpis(int openCount, int todayBorrow, int overdue, int returnedToday, string oldestElapsed, string lastRefresh)
        {
            _workspace.SetKpis(openCount, todayBorrow, overdue, returnedToday, oldestElapsed, lastRefresh);
        }

        public void SetPaging(int openPageIndex, int openTotalCount, int historyPageIndex, int historyTotalCount, int pageSize)
        {
            _workspace.SetPaging(openPageIndex, openTotalCount, historyPageIndex, historyTotalCount, pageSize);
        }

        public void SelectOpenRowBySerial(string serial)
        {
            _workspace.SelectOpenRowBySerial(serial);
        }
    }
}
