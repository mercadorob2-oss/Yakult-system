// Helper classes shared by ViewSetDetailPage.xaml.cs

namespace Yakult.Inventory.App.Pages.Set
{
    public class EmployeeItem
    {
        public int    EmpId       { get; set; }
        public string DisplayText { get; set; }
    }

    internal sealed class ReceivedByItem
    {
        public int    EmpId  { get; }
        public string Label  { get; }

        public ReceivedByItem(int empId, string label)
        {
            EmpId = empId;
            Label = label;
        }

        public override string ToString() => Label;
    }

    internal sealed class OrgSetItem
    {
        public int    Id   { get; set; }
        public string Name { get; set; }
        public override string ToString() => Name;
    }
}
