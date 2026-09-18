using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// DTO mapped from vw_UserActivityDetailed.
    /// Columns: ActivityId, UserId, Name, EmailAddress, ActionType, EntityType, EntityId, Description, CreatedDate.
    /// </summary>
    public class UserActivityLogDto
    {
        public int ActivityId { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; }
        public string EmailAddress { get; set; }
        public string ActionType { get; set; }
        public string EntityType { get; set; }
        public int? EntityId { get; set; }
        public string Description { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
