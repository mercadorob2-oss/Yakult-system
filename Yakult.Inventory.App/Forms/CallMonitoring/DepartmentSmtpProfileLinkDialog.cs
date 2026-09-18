using System.Collections.Generic;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class DepartmentSmtpProfileLinkDialog : SmtpProfileLinkDialog
    {
        public int SelectedDeptId => SelectedTargetId;

        public DepartmentSmtpProfileLinkDialog(
            List<LookupItem> departments,
            int? preselectedDeptId,
            string prefilledRecipientEmails,
            string prefilledEscalationEmails)
            : base(
                "Department",
                departments,
                preselectedDeptId,
                prefilledRecipientEmails,
                prefilledEscalationEmails)
        {
        }
    }
}
