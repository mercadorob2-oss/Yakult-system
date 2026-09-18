using System;
using System.ComponentModel;
using System.Linq;

namespace Yakult.Inventory.App.WPF.ItemMovementAudit.ViewModels
{
    /// <summary>UI-state persistence, ported verbatim from the deleted
    /// ViewItemMovementAuditPage.State.cs — same Settings.Default.ItemMovementAudit_* keys.
    /// WPF's DatePicker has no Min/MaxDate concept (unlike WinForms' DateTimePicker), so the
    /// original's ClampToPicker/IsValidUserDate logic simplifies to a default(DateTime) check.</summary>
    public sealed partial class ItemMovementAuditPageViewModel
    {
        private void RestoreUiState()
        {
            _restoringState = true;
            _pendingRestoreAfterLoad = true;
            try
            {
                var settings = Yakult.Inventory.App.Properties.Settings.Default;

                var savedFrom = settings.ItemMovementAudit_DateFrom;
                var savedTo = settings.ItemMovementAudit_DateTo;

                var restoredFrom = savedFrom != default ? savedFrom.Date : DateTime.Today.AddDays(-7);
                var restoredTo = savedTo != default ? savedTo.Date : DateTime.Today;
                if (restoredFrom > restoredTo) restoredFrom = restoredTo;

                if (restoredTo < DateTime.Today)
                {
                    var span = Math.Max(1, (restoredTo - restoredFrom).Days);
                    restoredTo = DateTime.Today;
                    restoredFrom = restoredTo.AddDays(-span);
                }

                _dateFrom = restoredFrom;
                _dateTo = restoredTo;
                OnPropertyChanged(nameof(DateFrom));
                OnPropertyChanged(nameof(DateTo));

                if (ViewModeOptions.Contains(settings.ItemMovementAudit_ViewMode)) _selectedViewMode = settings.ItemMovementAudit_ViewMode;
                if (DirectionOptions.Contains(settings.ItemMovementAudit_Direction)) _selectedDirection = settings.ItemMovementAudit_Direction;
                if (SourceOptions.Contains(settings.ItemMovementAudit_Source)) _selectedSource = settings.ItemMovementAudit_Source;

                _restoreMovementCategory = settings.ItemMovementAudit_Category ?? "All";
                _restoreMovementType = settings.ItemMovementAudit_Type ?? "All";

                _serialFilter = settings.ItemMovementAudit_Serial ?? string.Empty;
                _setCodeFilter = settings.ItemMovementAudit_SetCode ?? string.Empty;
                _userFilter = settings.ItemMovementAudit_User ?? string.Empty;
                _searchText = settings.ItemMovementAudit_Search ?? string.Empty;
                _showArchived = settings.ItemMovementAudit_ShowArchived;

                if (settings.ItemMovementAudit_PageSize > 0 && PageSizeOptions.Contains(settings.ItemMovementAudit_PageSize))
                    _pageSize = settings.ItemMovementAudit_PageSize;

                if (settings.ItemMovementAudit_TopRows > 0 && TopRowsOptions.Contains(settings.ItemMovementAudit_TopRows))
                    _topRows = settings.ItemMovementAudit_TopRows;

                if (!string.IsNullOrWhiteSpace(settings.ItemMovementAudit_SortKey))
                {
                    _restoreSortKey = settings.ItemMovementAudit_SortKey;
                    var order = settings.ItemMovementAudit_SortOrder;
                    _restoreSortDirection = order == (int)System.Windows.Forms.SortOrder.Ascending
                        ? ListSortDirection.Ascending
                        : order == (int)System.Windows.Forms.SortOrder.Descending
                            ? (ListSortDirection?)ListSortDirection.Descending
                            : null;
                }
                else
                {
                    // No persisted sort yet (fresh install, or after Reset Filters) — default to
                    // newest-first, matching an audit trail's expected "latest activity on top"
                    // behavior, and so the Sort By dropdown's initial selection actually matches
                    // what the grid shows instead of silently falling back to unsorted DB order.
                    _restoreSortKey = "EventTime";
                    _restoreSortDirection = ListSortDirection.Descending;
                }
            }
            catch
            {
                // Matches original's best-effort ListPageStateHelper.LogStateIssue logging.
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

                settings.ItemMovementAudit_DateFrom = DateFrom.Date;
                settings.ItemMovementAudit_DateTo = DateTo.Date;
                settings.ItemMovementAudit_ViewMode = SelectedViewMode ?? "Latest only";
                settings.ItemMovementAudit_Direction = SelectedDirection ?? "All";
                settings.ItemMovementAudit_Source = SelectedSource ?? "All";
                settings.ItemMovementAudit_Category = SelectedCategory ?? "All";
                settings.ItemMovementAudit_Type = SelectedType ?? "All";
                settings.ItemMovementAudit_Serial = SerialFilter ?? string.Empty;
                settings.ItemMovementAudit_SetCode = SetCodeFilter ?? string.Empty;
                settings.ItemMovementAudit_User = UserFilter ?? string.Empty;
                settings.ItemMovementAudit_Search = SearchText ?? string.Empty;
                settings.ItemMovementAudit_ShowArchived = ShowArchived;
                settings.ItemMovementAudit_PageSize = PageSize;
                settings.ItemMovementAudit_TopRows = TopRows;
                settings.ItemMovementAudit_SortKey = GetCurrentSortKey() ?? string.Empty;
                settings.ItemMovementAudit_SortOrder = (int)(_sortDirection == ListSortDirection.Ascending
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
