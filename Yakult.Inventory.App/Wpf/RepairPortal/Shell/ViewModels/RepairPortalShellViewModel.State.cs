using System;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Shell.ViewModels
{
    /// <summary>UI-state persistence — mirrors RepairedItemsPageViewModel.State.cs, using the
    /// RepairPortal_* keys added to Properties.Settings.settings.</summary>
    public sealed partial class RepairPortalShellViewModel
    {
        private void RestoreUiState()
        {
            _restoringState = true;
            try
            {
                var settings = Yakult.Inventory.App.Properties.Settings.Default;

                if (Enum.TryParse<RepairPortalViewMode>(settings.RepairPortal_LastViewMode, out var mode))
                    _selectedViewMode = mode;

                _quickFilterChip = settings.RepairPortal_LastQuickFilter ?? string.Empty;
                _searchText = settings.RepairPortal_LastSearch ?? string.Empty;
                _sortKey = string.IsNullOrWhiteSpace(settings.RepairPortal_LastSortKey) ? "DateReceivedDesc" : settings.RepairPortal_LastSortKey;
                _hideCompleted = settings.RepairPortal_HideCompleted;
            }
            catch
            {
                // Best-effort restore; defaults are already applied via field initializers.
            }
            finally
            {
                _restoringState = false;
            }
        }

        private void ScheduleSaveState()
        {
            if (_restoringState) return;
            _stateSaveTimer.Stop();
            _stateSaveTimer.Start();
        }

        private void SaveUiState()
        {
            if (_restoringState) return;

            try
            {
                var settings = Yakult.Inventory.App.Properties.Settings.Default;
                settings.RepairPortal_LastViewMode = SelectedViewMode.ToString();
                settings.RepairPortal_LastQuickFilter = QuickFilterChip ?? string.Empty;
                settings.RepairPortal_LastSearch = SearchText ?? string.Empty;
                settings.RepairPortal_LastSortKey = SortKey ?? "DateReceivedDesc";
                settings.RepairPortal_HideCompleted = HideCompleted;
                settings.Save();
            }
            catch
            {
                // Best-effort save.
            }
        }
    }
}
