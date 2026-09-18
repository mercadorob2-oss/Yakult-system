using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yakult.Inventory.App.Session
{
    public static class SessionContext
    {
        public static int CurrentUserId { get; set; }
        public static string CurrentUserName { get; set; }
        public static string CurrentEmail { get; set; }
        public static bool IsDeveloper { get; set; }
    }
}
