namespace Yakult.Inventory.App.Wpf.Set.BulkDeploy
{
    public sealed class BulkDeployRow
    {
        public string BundleKey { get; set; }
        public string Department { get; set; }
        public string ComputerName { get; set; }
        public string IPAddress { get; set; }
        public string ItemRole { get; set; }
        public string ItemName { get; set; }
        public string ModelNumber { get; set; }
        public string SerialNumber { get; set; }
        public string FixedAssetNumber { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; } = 1;
        public string DateDeployedText { get; set; }
        public string Condition { get; set; } = "Good";
        public string Vendor { get; set; }
        public string Remarks { get; set; }
        public string Employee { get; set; }
        public int? MatchedEmployeeId { get; set; }
        public string MatchedEmployeeName { get; set; }
        public string Company { get; set; }
        public string Branch { get; set; }
        public int? MatchedCompanyId { get; set; }
        public int? MatchedBranchId { get; set; }
        public string RowStatus { get; set; } = "Pending";
        public string RowMessage { get; set; } = "";
    }

    public sealed class BulkDeployEmployee
    {
        public int EmpId { get; set; }
        public string Name { get; set; }
        public string EmployeeNumber { get; set; }
    }
}
