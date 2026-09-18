using System;
using System.ComponentModel;
using System.Linq;

namespace Yakult.Inventory.App.WPF.RepairedItems.ViewModels
{
    /// <summary>UI-state persistence, ported verbatim from the deleted
    /// ViewRepairedItemsPage.State.cs — same Settings.Default.RepairedItems_* keys.</summary>
    public sealed partial class RepairedItemsPageViewModel
    {
        private void RestoreUiState()
        {
            _restoringState = true;
            _pendingSelectionRestore = true;
            try
            {
                var settings = Yakult.Inventory.App.Properties.Settings.Default;

                if (RepairStatusOptions.Contains(settings.RepairedItems_RepairStatus)) _selectedRepairStatus = settings.RepairedItems_RepairStatus;
                if (SpareOptions.Contains(settings.RepairedItems_Spare)) _selectedSpare = settings.RepairedItems_Spare;
                if (ActiveOptions.Contains(settings.RepairedItems_Active)) _selectedActive = settings.RepairedItems_Active;
                if (LocationOptions.Contains(settings.RepairedItems_Location)) _selectedLocation = settings.RepairedItems_Location;
                if (OriginOptions.Contains(settings.RepairedItems_Origin)) _selectedOrigin = settings.RepairedItems_Origin;

                _problemsOnly = settings.RepairedItems_ProblemsOnly;
                _searchText = string.Empty;

                _restoreConditionName = settings.RepairedItems_Condition ?? "All";
                _restoreCategory = settings.RepairedItems_Category ?? "All";
                _restoreSortKey = settings.RepairedItems_SortKey;

                var order = settings.RepairedItems_SortOrder;
                _restoreSortDirection = order == (int)System.Windows.Forms.SortOrder.Ascending
                    ? ListSortDirection.Ascending
                    : order == (int)System.Windows.Forms.SortOrder.Descending
                        ? (ListSortDirection?)ListSortDirection.Descending
                        : null;
            }
            catch
            {
                // Matches original's ListPageStateHelper.LogStateIssue best-effort logging.
            }
            finally
            {
                _restoringState = false;
            }
        }

        private void ApplyDeferredStateSelections()
        {
            if (!_pendingSelectionRestore) return;

            _restoringState = true;
            try
            {
                if (!string.IsNullOrWhiteSpace(_restoreConditionName) && !string.Equals(_restoreConditionName, "All", StringComparison.OrdinalIgnoreCase))
                    TrySelectConditionByName(_restoreConditionName);

                var catMatch = CategoryOptions.FirstOrDefault(c => string.Equals(c, _restoreCategory, StringComparison.OrdinalIgnoreCase));
                if (catMatch != null) SelectedCategory = catMatch;
            }
            finally
            {
                _restoringState = false;
                _pendingSelectionRestore = false;
            }
        }

        private void TryApplyRestoredSortState()
        {
            if (_sortColumnKey != null || string.IsNullOrWhiteSpace(_restoreSortKey) || _restoreSortDirection == null)
                return;

            _sortColumnKey = _restoreSortKey;
            _sortDirection = _restoreSortDirection;
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

                settings.RepairedItems_RepairStatus = SelectedRepairStatus ?? "All";
                settings.RepairedItems_Condition = SelectedCondition?.Name ?? "All";
                settings.RepairedItems_Category = SelectedCategory ?? "All";
                settings.RepairedItems_Spare = SelectedSpare ?? "All";
                settings.RepairedItems_Active = SelectedActive ?? "Active only";
                settings.RepairedItems_Location = SelectedLocation ?? "All";
                settings.RepairedItems_Origin = SelectedOrigin ?? "All";
                settings.RepairedItems_ProblemsOnly = ProblemsOnly;
                settings.RepairedItems_Search = SearchText ?? string.Empty;
                settings.RepairedItems_SortKey = _sortColumnKey ?? string.Empty;
                settings.RepairedItems_SortOrder = (int)(_sortDirection == ListSortDirection.Ascending
                    ? System.Windows.Forms.SortOrder.Ascending
                    : _sortDirection == ListSortDirection.Descending
                        ? System.Windows.Forms.SortOrder.Descending
                        : System.Windows.Forms.SortOrder.None);

                settings.Save();
            }
            catch
            {
                // Matches original's best-effort logging on save failure.
            }
        }
    }
}
