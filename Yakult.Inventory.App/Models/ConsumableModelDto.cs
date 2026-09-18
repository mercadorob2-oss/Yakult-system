using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// DTO for dbo.ConsumableModel table - single source of truth for Ink/Toner/Print
    /// Head models, mirroring CartridgeModelDto without the vendor/condition split.
    /// </summary>
    public class ConsumableModelDto
    {
        public int ConsumableModelId { get; set; }
        public string ModelNumber { get; set; }
        public string Category { get; set; }
        public bool IsRequestable { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; }
        public int AvailableStock { get; set; }

        public string DisplayName => $"{ModelNumber} (Available: {AvailableStock})";
    }
}
