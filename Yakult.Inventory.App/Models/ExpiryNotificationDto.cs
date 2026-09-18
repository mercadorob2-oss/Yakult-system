using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>A license/service or warranty row within 90 days of (or past) its end date.</summary>
    public class ExpiryNotificationItem
    {
        public string ItemName      { get; set; }
        public string ItemType      { get; set; }
        public int    DaysRemaining { get; set; }
        public bool   IsSet         { get; set; }
    }

    /// <summary>An unprocessed dbo.SetItemUpdate row (mobile status update awaiting review).</summary>
    public class MobileUpdateNotificationItem
    {
        public string   SetCode      { get; set; }
        public string   SerialNumber { get; set; }
        public string   NewStatus    { get; set; }
        public DateTime CreatedAt    { get; set; }
    }
}
