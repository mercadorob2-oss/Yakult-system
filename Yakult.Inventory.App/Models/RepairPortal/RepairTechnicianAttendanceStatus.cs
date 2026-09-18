using System;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    public sealed class RepairTechnicianAttendanceStatus
    {
        public int? AttendanceId { get; set; }
        public int EmployeeId { get; set; }
        public DateTime WorkDate { get; set; }
        public DateTime? TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }

        public bool IsTimedIn => TimeIn.HasValue && !TimeOut.HasValue;

        public TimeSpan Elapsed
        {
            get
            {
                if (!TimeIn.HasValue) return TimeSpan.Zero;

                // TimeIn/TimeOut are already converted to Manila local time by the repository
                // (RepairTicketRepository.GetLocalDateTime/GetDateOrNull) before they ever reach
                // this model — comparing against DateTime.UtcNow here (as this used to) produced a
                // ~8 hour skew that always clamped to TimeSpan.Zero, making the elapsed timer look
                // permanently stuck at 00:00:00. Compare against local "now" instead.
                var end = TimeOut ?? DateTime.Now;
                var span = end - TimeIn.Value;
                return span < TimeSpan.Zero ? TimeSpan.Zero : span;
            }
        }

        public string ElapsedDisplay
        {
            get
            {
                var e = Elapsed;
                return $"{(int)e.TotalHours:00}:{e.Minutes:00}:{e.Seconds:00}";
            }
        }
    }
}
