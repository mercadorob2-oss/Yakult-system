using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallTechOpenTicketRow
    {
        private string _department;
        private string _branch;
        private string _locationCache;

        public int TicketId { get; set; }
        public string TicketCode { get; set; }
        public int? DeptId { get; set; }
        public string Department
        {
            get => _department;
            set
            {
                _department = value;
                _locationCache = null;
            }
        }
        public int? BranchId { get; set; }
        public string Branch
        {
            get => _branch;
            set
            {
                _branch = value;
                _locationCache = null;
            }
        }

        public string Location
        {
            get
            {
                if (_locationCache != null)
                    return _locationCache;

                var dept = (_department ?? string.Empty).Trim();
                var branch = (_branch ?? string.Empty).Trim();

                if (!string.IsNullOrWhiteSpace(dept) && !string.IsNullOrWhiteSpace(branch))
                    _locationCache = $"{dept} ({branch})";
                else if (!string.IsNullOrWhiteSpace(dept))
                    _locationCache = dept;
                else if (!string.IsNullOrWhiteSpace(branch))
                    _locationCache = branch;
                else
                    _locationCache = string.Empty;

                return _locationCache;
            }
        }
        public string Issue { get; set; }
        public string Status { get; set; }
        public string Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
