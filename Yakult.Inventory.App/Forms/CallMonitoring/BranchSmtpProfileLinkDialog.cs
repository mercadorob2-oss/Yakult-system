using System.Collections.Generic;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public sealed class BranchSmtpProfileLinkDialog : SmtpProfileLinkDialog
    {
        public int SelectedBranchId => SelectedTargetId;

        public BranchSmtpProfileLinkDialog(
            List<LookupItem> branches,
            int? preselectedBranchId,
            string prefilledRecipientEmails,
            string prefilledEscalationEmails)
            : base(
                "Branch",
                branches,
                preselectedBranchId,
                prefilledRecipientEmails,
                prefilledEscalationEmails)
        {
        }
    }
}
