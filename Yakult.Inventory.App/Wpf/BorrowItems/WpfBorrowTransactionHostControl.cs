using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Yakult.Inventory.App.Models.BorrowItems;

namespace Yakult.Inventory.App.Wpf.BorrowItems
{
    public sealed class WpfBorrowTransactionHostControl : UserControl
    {
        private readonly ElementHost _host;
        private readonly WpfBorrowTransactionWorkspace _workspace;

        public WpfBorrowTransactionHostControl()
        {
            BackColor = Color.FromArgb(242, 245, 249);
            Dock = DockStyle.Fill;

            _workspace = new WpfBorrowTransactionWorkspace();

            // Wire WPF events back to WinForms Host
            _workspace.ScanSubmitted += (s, e) => ScanSubmitted?.Invoke(this, e);
            _workspace.ResolveClicked += (s, e) => ResolveClicked?.Invoke(this, e);
            _workspace.ModelSearchRequested += (s, e) => ModelSearchRequested?.Invoke(this, e);
            _workspace.AddNewItemClicked += (s, e) => AddNewItemClicked?.Invoke(this, e);
            _workspace.BrowseItemsClicked += (s, e) => BrowseItemsClicked?.Invoke(this, e);
            _workspace.BorrowClicked += (s, e) => BorrowClicked?.Invoke(this, e);
            _workspace.ReturnClicked += (s, e) => ReturnClicked?.Invoke(this, e);
            _workspace.AddEmployeeClicked += (s, e) => AddEmployeeClicked?.Invoke(this, e);
            _workspace.BorrowCompanyChanged += (s, e) => BorrowCompanyChanged?.Invoke(this, e);
            _workspace.BorrowDepartmentChanged += (s, e) => BorrowDepartmentChanged?.Invoke(this, e);
            _workspace.BorrowBranchChanged += (s, e) => BorrowBranchChanged?.Invoke(this, e);
            _workspace.ReturnCompanyChanged += (s, e) => ReturnCompanyChanged?.Invoke(this, e);
            _workspace.ReturnDepartmentChanged += (s, e) => ReturnDepartmentChanged?.Invoke(this, e);
            _workspace.ReturnBranchChanged += (s, e) => ReturnBranchChanged?.Invoke(this, e);
            _workspace.BackdateToggled += (s, e) => BackdateToggled?.Invoke(this, e);

            _host = new ElementHost
            {
                Dock = DockStyle.Fill,
                BackColorTransparent = true,
                Child = _workspace
            };

            Controls.Add(_host);
        }

        public event EventHandler<string> ScanSubmitted;
        public event EventHandler ResolveClicked;
        public event EventHandler<string> ModelSearchRequested;
        public event EventHandler AddNewItemClicked;
        public event EventHandler BrowseItemsClicked;
        public event EventHandler<BorrowFormEventArgs> BorrowClicked;
        public event EventHandler<BorrowFormEventArgs> ReturnClicked;
        public event EventHandler<bool> AddEmployeeClicked;
        public event EventHandler<int?> BorrowCompanyChanged;
        public event EventHandler<int?> BorrowDepartmentChanged;
        public event EventHandler<int?> BorrowBranchChanged;
        public event EventHandler<int?> ReturnCompanyChanged;
        public event EventHandler<int?> ReturnDepartmentChanged;
        public event EventHandler<int?> ReturnBranchChanged;
        public event EventHandler BackdateToggled;

        public string ScanText
        {
            get => _workspace.ScanText;
            set => _workspace.ScanText = value;
        }

        public void SetMode(TransactionMode mode) => _workspace.SetMode(mode);
        public void ShowModelSuggestions(List<BorrowItemLookup> matches) => _workspace.ShowModelSuggestions(matches);
        public void SetResolvedItem(BorrowItemLookup item) => _workspace.SetResolvedItem(item);
        public void SetReturnInfo(BorrowLogRow row) => _workspace.SetReturnInfo(row);
        public void SetCompanies(List<IdNamePair> companies) => _workspace.SetCompanies(companies);
        public void SetDepartments(List<IdNamePair> departments) => _workspace.SetDepartments(departments);
        public void SetEmployees(List<BorrowEmployeeLookup> employees) => _workspace.SetEmployees(employees);
        public void SelectCompany(int comId) => _workspace.SelectCompany(comId);
        public void SelectDepartment(int deptId) => _workspace.SelectDepartment(deptId);
        public void SelectBranch(int branchId) => _workspace.SelectBranch(branchId);
        public void SelectBorrowCompany(int comId) => _workspace.SelectBorrowCompany(comId);
        public void SelectBorrowDepartment(int deptId) => _workspace.SelectBorrowDepartment(deptId);
        public void SelectBorrowBranch(int branchId) => _workspace.SelectBorrowBranch(branchId);
        public void SelectEmployee(int empId) => _workspace.SelectEmployee(empId);
        public void Clear() => _workspace.Clear();
        public void SetBusy(bool busy) => _workspace.SetBusy(busy);
        public bool IsBackdateChecked => _workspace.IsBackdateChecked;
        public DateTime? GetBackdateLocal() => _workspace.GetBackdateLocal();
        public int? SelectedCompanyId => _workspace.SelectedCompanyId;
        public int? SelectedDepartmentId => _workspace.SelectedDepartmentId;
        public int? SelectedEmployeeId => _workspace.SelectedEmployeeId;
    }
}
