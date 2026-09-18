using System;
using System.Threading.Tasks;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Shell.ViewModels
{
    /// <summary>Portal-wide, dev-only maintenance actions — lives in the hamburger sidebar (not
    /// tucked inside a specific view like Reports) since these fix data shared across Gallery/
    /// Table/Kanban/Reports, not something scoped to whichever screen happens to be open.</summary>
    public sealed partial class RepairPortalShellViewModel
    {
        /// <summary>Gates the "Regenerate Thumbnails" sidebar item — fixes black backgrounds on
        /// dbo.RepairTicketAttachment rows whose ThumbnailBytes was precomputed before the
        /// white-flatten fix existed (see RepairTicketRepository.Tickets.cs's CreateThumbnailBytes).
        /// Not part of the normal technician workflow, so hidden entirely for non-developers.</summary>
        public bool IsDeveloperUser => AppSession.IsDeveloper;

        public RelayCommand RegenerateThumbnailsCommand { get; private set; }

        private void InitMaintenance()
        {
            RegenerateThumbnailsCommand = new RelayCommand(async () => await RegenerateThumbnailsAsync());
        }

        private async Task RegenerateThumbnailsAsync()
        {
            if (IsBusy) return;

            var confirmed = RequestConfirm?.Invoke(
                "Regenerate Thumbnails",
                "Re-resize every existing attachment's thumbnail from its original image? This can take a while depending on how many attachments exist, and affects Gallery/Table/Kanban and Report previews everywhere, not just this screen. Original images are untouched either way.") ?? false;
            if (!confirmed) return;

            SetBusy(true);
            try
            {
                var (updated, failed) = await _repository.RegenerateAttachmentThumbnailsAsync();
                RequestInfo?.Invoke("Regenerate Thumbnails",
                    failed == 0
                        ? $"Done — {updated} thumbnail(s) regenerated."
                        : $"Done — {updated} thumbnail(s) regenerated, {failed} failed (see Debug output for details).");
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Regenerate Thumbnails Failed", ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }
    }
}
