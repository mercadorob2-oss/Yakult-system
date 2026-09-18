namespace Yakult.Inventory.App.Models.RepairPortal
{
    public sealed class RepairPortalSummaryCounts
    {
        public int Waiting { get; set; }
        public int Diagnosing { get; set; }
        public int Repairing { get; set; }
        public int AwaitingParts { get; set; }
        public int Completed { get; set; }
        public int Unrepairable { get; set; }

        /// <summary>Backs the "All Tickets" summary card — not a distinct status, just the sum of
        /// the others.</summary>
        public int Total => Waiting + Diagnosing + Repairing + AwaitingParts + Completed + Unrepairable;
    }
}
