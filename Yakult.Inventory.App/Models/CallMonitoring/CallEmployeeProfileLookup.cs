namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallEmployeeProfileLookup
    {
        public int EmpId { get; set; }
        public string EmployeeName { get; set; }
        public string Position { get; set; }
        public int? UserId { get; set; }
        public string UserName { get; set; }

        public override string ToString()
        {
            var pos = string.IsNullOrWhiteSpace(Position) ? string.Empty : $" - {Position}";
            var acct = UserId.HasValue ? string.Empty : " (no account)";
            return $"{EmployeeName}{pos}{acct}";
        }
    }
}

