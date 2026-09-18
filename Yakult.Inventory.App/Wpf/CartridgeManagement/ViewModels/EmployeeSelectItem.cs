namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class EmployeeSelectItem
    {
        public int    EmpId          { get; set; }
        public string Name           { get; set; }
        public string Position       { get; set; }
        public string DepartmentName { get; set; }
        public string BranchName     { get; set; }

        public override string ToString() => Name ?? "";
    }
}
