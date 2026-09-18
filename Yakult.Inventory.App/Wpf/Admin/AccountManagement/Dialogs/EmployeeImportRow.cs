using System.ComponentModel;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.Dialogs
{
    public enum EmployeeImportStatus { New, Update, Unchanged, Invalid }

    public class EmployeeImportRow : INotifyPropertyChanged
    {
        public int RowNumber { get; set; }

        public string EmployeeNumber { get; set; }
        public string TitleText      { get; set; }
        public string Name           { get; set; }
        public string Position       { get; set; }
        public string CompanyName    { get; set; }
        public string DepartmentName { get; set; }
        public string BranchName     { get; set; }
        public bool   Active         { get; set; }

        public EmployeeImportStatus Status { get; set; }
        public string StatusText => Status.ToString();

        public string ErrorMessage  { get; set; }
        public string ChangeSummary { get; set; }

        public int? MatchedEmpId { get; set; }
        public int? ComId        { get; set; }
        public int? BranchId     { get; set; }
        public int? DeptId       { get; set; }
        public int? TitleId      { get; set; }

        private bool _isIncluded = true;
        public bool IsIncluded
        {
            get => _isIncluded;
            set { if (_isIncluded != value) { _isIncluded = value; OnPropertyChanged(nameof(IsIncluded)); } }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
