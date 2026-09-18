using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallClientPresenceItem
    {
        public string MachineName { get; set; }
        public string UserName { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public DateTime FirstSeenUtc { get; set; }
        public string ClientVersion { get; set; }
        public string Module { get; set; }
        public bool Online { get; set; }
    }
}
