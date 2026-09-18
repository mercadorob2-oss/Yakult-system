namespace Yakult.Inventory.App.Models.RepairPortal
{
    public sealed class NewPartRequest
    {
        public int RepairTicketId { get; set; }
        public string CustomLabel { get; set; }
        public string ProblemDescription { get; set; }
        public string Severity { get; set; }
    }
}
