using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.WPF.ItemMovementAudit.ViewModels
{
    public sealed partial class ItemMovementAuditPageViewModel
    {
        private static string NormalizeServerFilter(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var trimmed = value.Trim();
            return string.Equals(trimmed, "All", StringComparison.OrdinalIgnoreCase) ? null : trimmed;
        }

        private string GetTopRowsWarningSuffix()
        {
            if (TopRows <= 0 || _allMovements == null || _allMovements.Count < TopRows)
                return string.Empty;
            return $" Results may be capped by the Top {TopRows} load limit.";
        }

        private async Task<List<ItemMovementAuditDto>> LoadDataAsync(
            bool useLatestOnly,
            DateTime? fromDate,
            DateTime? toDate,
            string direction,
            string serial,
            string setCode,
            string source,
            string userName)
        {
            return useLatestOnly
                ? await _repo.GetLatestPerSerialAsync(
                    fromDate: fromDate, toDate: toDate, direction: direction, serial: serial,
                    setCode: setCode, source: source, userName: userName, top: TopRows)
                : await _repo.GetTimelineAsync(
                    fromDate: fromDate, toDate: toDate, direction: direction, serial: serial,
                    setCode: setCode, source: source, userName: userName, top: TopRows);
        }

        private void RestoreUsefulDefaultFilters()
        {
            _restoringState = true;
            try
            {
                _dateFrom = DateTime.Today.AddDays(-7);
                _dateTo = DateTime.Today;
                _selectedDirection = "All";
                _selectedSource = "All";
                _selectedCategory = "All";
                _selectedType = "All";
                _selectedFilterBy = "Default";
                _searchText = string.Empty;
                _serialFilter = string.Empty;
                _setCodeFilter = string.Empty;
                _userFilter = string.Empty;
                _showArchived = false;

                OnPropertyChanged(nameof(DateFrom));
                OnPropertyChanged(nameof(DateTo));
                OnPropertyChanged(nameof(DateRangeText));
                OnPropertyChanged(nameof(SelectedDirection));
                OnPropertyChanged(nameof(SelectedSource));
                OnPropertyChanged(nameof(SelectedCategory));
                OnPropertyChanged(nameof(SelectedType));
                OnPropertyChanged(nameof(SelectedFilterBy));
                OnPropertyChanged(nameof(SearchText));
                OnPropertyChanged(nameof(SerialFilter));
                OnPropertyChanged(nameof(SetCodeFilter));
                OnPropertyChanged(nameof(UserFilter));
                OnPropertyChanged(nameof(ShowArchived));
            }
            finally
            {
                _restoringState = false;
            }
        }

        public async Task LoadAsync()
        {
            if (_isLoading)
            {
                _reloadRequested = true;
                _loadVersion++;
                return;
            }

            var version = ++_loadVersion;
            _isLoading = true;
            try
            {
                IsBusy = true;
                await Task.Yield();

                var from = DateFrom.Date;
                var to = DateTo.Date;
                var directionFilter = NormalizeServerFilter(SelectedDirection);
                var serialFilter = NormalizeServerFilter(SerialFilter);
                var setCodeFilter = NormalizeServerFilter(SetCodeFilter);
                var sourceFilter = NormalizeServerFilter(SelectedSource);
                var userFilter = NormalizeServerFilter(UserFilter);

                var useLatestOnly = string.Equals(SelectedViewMode, "Latest only", StringComparison.OrdinalIgnoreCase);

                var data = await LoadDataAsync(
                    useLatestOnly, from, to, directionFilter, serialFilter, setCodeFilter, sourceFilter, userFilter);

                // Saved page state can outlive the data window it was saved against. If the
                // first load is empty, recover to the page's useful defaults instead of leaving
                // the audit screen at an apparently broken zero-record state.
                if (_pendingRestoreAfterLoad && data.Count == 0)
                {
                    RestoreUsefulDefaultFilters();
                    _pendingRestoreAfterLoad = false;
                    data = await LoadDataAsync(
                        useLatestOnly,
                        DateFrom.Date,
                        DateTo.Date,
                        NormalizeServerFilter(SelectedDirection),
                        NormalizeServerFilter(SerialFilter),
                        NormalizeServerFilter(SetCodeFilter),
                        NormalizeServerFilter(SelectedSource),
                        NormalizeServerFilter(UserFilter));
                }

                var enriched = EnrichMovements(data);
                PopulateMovementFilters(enriched);

                if (_pendingRestoreAfterLoad)
                {
                    _restoringState = true;
                    try
                    {
                        if (CategoryOptions.Contains(_restoreMovementCategory)) SelectedCategory = _restoreMovementCategory;
                        if (TypeOptions.Contains(_restoreMovementType)) SelectedType = _restoreMovementType;
                    }
                    finally
                    {
                        _restoringState = false;
                        _pendingRestoreAfterLoad = false;
                    }
                }

                if (version != _loadVersion || IsDisposed)
                    return;

                _allMovements = enriched;
                _isLoading = false;
                await ApplyFiltersAsync();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Load failed", ex.Message);
            }
            finally
            {
                _isLoading = false;
                IsBusy = false;
                if (_reloadRequested && !IsDisposed)
                {
                    _reloadRequested = false;
                    _ = LoadAsync();
                }
            }
        }

        private async Task ApplyFiltersAsync()
        {
            if (_isLoading || _allMovements == null)
                return;

            CancellationTokenSource cts = null;
            CancellationToken token = default;
            var version = 0;

            try
            {
                cts = new CancellationTokenSource();
                token = cts.Token;
                version = Interlocked.Increment(ref _applyFiltersVersion);

                var prev = Interlocked.Exchange(ref _applyFiltersCts, cts);
                if (prev != null) { try { prev.Cancel(); } catch { } try { prev.Dispose(); } catch { } }

                var all = _allMovements;
                var from = DateFrom.Date;
                var toExclusive = DateTo.Date.AddDays(1);

                var searchText = (SearchText ?? string.Empty).Trim();
                var serial = (SerialFilter ?? string.Empty).Trim();
                var setCode = (SetCodeFilter ?? string.Empty).Trim();
                var user = (UserFilter ?? string.Empty).Trim();

                var source = SelectedSource;
                var direction = SelectedDirection;
                var category = SelectedCategory;
                var type = SelectedType;
                var showArchived = ShowArchived;

                var result = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();

                    IEnumerable<ItemMovementAuditDto> filtered = all ?? new List<ItemMovementAuditDto>();

                    filtered = filtered.Where(m => m != null && m.EventTime >= from && m.EventTime < toExclusive);

                    if (!showArchived)
                        filtered = filtered.Where(m => !m.IsArchived);

                    if (!string.IsNullOrWhiteSpace(searchText))
                    {
                        filtered = filtered.Where(m =>
                            m != null &&
                            ((m.SerialNumber?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                             (m.SetCode?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                             (m.UserName?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                             (m.EmployeeName?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                             (m.BranchName?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                             (m.DepartmentName?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                             (m.Notes?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)));
                    }

                    if (!string.IsNullOrWhiteSpace(serial))
                        filtered = filtered.Where(m => m?.SerialNumber?.IndexOf(serial, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (!string.IsNullOrWhiteSpace(setCode))
                        filtered = filtered.Where(m => m?.SetCode?.IndexOf(setCode, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (!string.IsNullOrWhiteSpace(user))
                        filtered = filtered.Where(m => m?.UserName?.IndexOf(user, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (!string.IsNullOrWhiteSpace(source) && !string.Equals(source, "All", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.Equals(source, "ITCM", StringComparison.OrdinalIgnoreCase))
                        {
                            filtered = filtered.Where(m =>
                                string.Equals(m?.Source, "CallMonitoring", StringComparison.OrdinalIgnoreCase)
                                || (string.Equals(m?.ReferenceType, "Inventory", StringComparison.OrdinalIgnoreCase)
                                    && (m?.Notes?.IndexOf("Call ticket", StringComparison.OrdinalIgnoreCase) >= 0
                                        || m?.Notes?.IndexOf("Call Ticket", StringComparison.OrdinalIgnoreCase) >= 0))
                                || (string.Equals(m?.ReferenceType, "ItemAuditTrail", StringComparison.OrdinalIgnoreCase)
                                    && ((m?.AuditAction?.StartsWith("Call Ticket", StringComparison.OrdinalIgnoreCase) ?? false)
                                        || m?.Notes?.IndexOf("Call ticket", StringComparison.OrdinalIgnoreCase) >= 0
                                        || m?.Notes?.IndexOf("Call Ticket", StringComparison.OrdinalIgnoreCase) >= 0)));
                        }
                        else if (string.Equals(source, "RepairHistory", StringComparison.OrdinalIgnoreCase))
                        {
                            filtered = filtered.Where(m =>
                                string.Equals(m?.Source, "RepairHistory", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(m?.ReferenceType, "ItemRepairHistory", StringComparison.OrdinalIgnoreCase));
                        }
                        else
                        {
                            filtered = filtered.Where(m => string.Equals(m?.Source, source, StringComparison.OrdinalIgnoreCase));
                        }
                    }

                    var baseForSummary = filtered as IList<ItemMovementAuditDto> ?? filtered.ToList();
                    var total = baseForSummary.Count;
                    var inCount = 0;
                    var outCount = 0;
                    for (var i = 0; i < baseForSummary.Count; i++)
                    {
                        var d = baseForSummary[i]?.Direction;
                        if (string.Equals(d, "IN", StringComparison.OrdinalIgnoreCase)) inCount++;
                        else if (string.Equals(d, "OUT", StringComparison.OrdinalIgnoreCase)) outCount++;
                    }

                    IEnumerable<ItemMovementAuditDto> afterDirection;
                    if (!string.IsNullOrWhiteSpace(direction) && !string.Equals(direction, "All", StringComparison.OrdinalIgnoreCase))
                        afterDirection = baseForSummary.Where(m => string.Equals(m?.Direction, direction, StringComparison.OrdinalIgnoreCase));
                    else
                        afterDirection = baseForSummary;

                    if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
                        afterDirection = afterDirection.Where(m => string.Equals(m?.MovementCategory, category, StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "All", StringComparison.OrdinalIgnoreCase))
                        afterDirection = afterDirection.Where(m => string.Equals(m?.MovementType, type, StringComparison.OrdinalIgnoreCase));

                    var finalList = afterDirection.ToList();
                    return (Filtered: finalList, Total: total, InCount: inCount, OutCount: outCount);
                }, token);

                if (token.IsCancellationRequested || version != _applyFiltersVersion || IsDisposed)
                    return;

                TotalCount = result.Total;
                InCount = result.InCount;
                OutCount = result.OutCount;

                _filteredMovements = result.Filtered;
                CurrentPage = 1;

                ApplyCurrentSortIfAny();
                UpdatePagination();
                ApplySortGlyphAndDropdown();

                UpdateSummaryCardActiveStates();
                ScheduleSaveState();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Yakult.Inventory.App.Core.Logger.LogError("Item Movement Audit filter application failed.", ex);
                PageInfoText = "Failed to apply audit filters. Please refresh or adjust the current filters.";
            }
        }

        // ── Enrichment / classification (verbatim business logic) ───────────

        private static List<ItemMovementAuditDto> EnrichMovements(List<ItemMovementAuditDto> movements)
        {
            if (movements == null || movements.Count == 0)
                return movements ?? new List<ItemMovementAuditDto>();

            var enriched = movements.ToList();

            var groups = enriched
                .GroupBy(m => string.IsNullOrWhiteSpace(m.SerialNumber)
                    ? (m.ItemId.HasValue ? $"ItemId:{m.ItemId.Value}" : "(unknown)")
                    : $"Serial:{m.SerialNumber.Trim()}")
                .ToList();

            foreach (var g in groups)
            {
                var ordered = g
                    .OrderBy(x => x.EventTime)
                    .ThenBy(x => GetStableSortRank(x))
                    .ThenBy(x => x.ReferenceId ?? int.MaxValue)
                    .ToList();

                string prevStatus = null;
                string prevSetCode = null;
                string prevBranch = null;
                string prevDept = null;
                string prevEmp = null;

                foreach (var m in ordered)
                {
                    m.MovementCategory = ClassifyCategory(m);
                    m.MovementType = ClassifyType(m);
                    m.MovementPriority = ClassifyPriority(m);

                    m.PrevSetCode = prevSetCode;
                    m.PrevBranchName = prevBranch;
                    m.PrevDepartmentName = prevDept;
                    m.PrevEmployeeName = prevEmp;

                    m.NewSetCode = string.IsNullOrWhiteSpace(m.SetCode) ? prevSetCode : m.SetCode;
                    m.NewBranchName = string.IsNullOrWhiteSpace(m.BranchName) ? prevBranch : m.BranchName;
                    m.NewDepartmentName = string.IsNullOrWhiteSpace(m.DepartmentName) ? prevDept : m.DepartmentName;
                    m.NewEmployeeName = string.IsNullOrWhiteSpace(m.EmployeeName) ? prevEmp : m.EmployeeName;

                    m.StatusBefore = prevStatus;
                    m.StatusAfter = DetermineStatusAfter(m, prevStatus);

                    prevSetCode = m.NewSetCode;
                    prevBranch = m.NewBranchName;
                    prevDept = m.NewDepartmentName;
                    prevEmp = m.NewEmployeeName;
                    prevStatus = m.StatusAfter;
                }
            }

            return enriched;
        }

        private static int GetStableSortRank(ItemMovementAuditDto movement)
        {
            var referenceType = movement?.ReferenceType ?? string.Empty;

            if (referenceType.Equals("Inventory", StringComparison.OrdinalIgnoreCase)) return 1;
            if (referenceType.Equals("Request", StringComparison.OrdinalIgnoreCase)) return 2;
            if (referenceType.Equals("ItemAuditTrail", StringComparison.OrdinalIgnoreCase)) return 3;
            if (referenceType.Equals("SetItemUpdate", StringComparison.OrdinalIgnoreCase)) return 4;
            if (referenceType.Equals("BorrowLog", StringComparison.OrdinalIgnoreCase)) return 5;

            return 9;
        }

        private static string ClassifyCategory(ItemMovementAuditDto m)
        {
            if (m == null) return null;

            if (string.Equals(m.ReferenceType, "ItemAuditTrail", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Source, "AuditTrail", StringComparison.OrdinalIgnoreCase))
                return "Audit Trail";

            var notes = m.Notes ?? string.Empty;

            if (ContainsAny(notes, "repair", "repaired", "service", "serviced", "calibrat"))
                return "Maintenance";

            if (ContainsAny(notes, "item sold", "item disposed", " sold ", " disposed", "dispose"))
                return "Lifecycle";

            if (ContainsAny(notes, "archive", "archived", "inactive", "active", "lost", "missing", "damage", "damaged", "broken"))
                return "Status Changes";

            if (string.Equals(m.ReferenceType, "Request", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Source, "Request", StringComparison.OrdinalIgnoreCase))
                return "Request Lifecycle";

            if (string.Equals(m.ReferenceType, "Inventory", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Source, "Inventory", StringComparison.OrdinalIgnoreCase))
                return "Inventory Operations";

            if (string.Equals(m.ReferenceType, "BorrowLog", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Source, "Borrow", StringComparison.OrdinalIgnoreCase))
                return "Borrow Operations";

            if (ContainsAny(m.Source ?? string.Empty, "Mobile"))
                return "Inventory Operations";

            return "Other";
        }

        private static string ClassifyType(ItemMovementAuditDto m)
        {
            if (m == null) return null;

            if (string.Equals(m.ReferenceType, "ItemAuditTrail", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Source, "AuditTrail", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(m.AuditAction))
                    return m.AuditAction;

                return "Audit";
            }

            var notes = m.Notes ?? string.Empty;

            if (ContainsAny(notes, "item sold", " sold ")) return "Sold";
            if (ContainsAny(notes, "item disposed", "dispose", " disposed")) return "Disposed";

            if (ContainsAny(notes, "lost", "missing")) return "Lost";
            if (ContainsAny(notes, "broken")) return "Broken";
            if (ContainsAny(notes, "damage", "damaged")) return "Damaged";
            if (ContainsAny(notes, "archive", "archived")) return "Archived";
            if (ContainsAny(notes, "inactive")) return "Inactive";
            if (ContainsAny(notes, " active ", "activate", "activated")) return "Active";
            if (ContainsAny(notes, "repair", "repaired")) return "Repair";
            if (ContainsAny(notes, "service", "serviced")) return "Service";
            if (ContainsAny(notes, "calibrat")) return "Calibration";

            if (string.Equals(m.ReferenceType, "Request", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Source, "Request", StringComparison.OrdinalIgnoreCase))
            {
                var status = m.RequestStatus ?? string.Empty;
                if (ContainsAny(status, "approve", "approved")) return "Approved";
                if (ContainsAny(status, "return", "returned")) return "Returned";
                if (ContainsAny(status, "issue", "issued", "release", "released")) return "Issued";
                if (ContainsAny(status, "submit", "submitted")) return "Requested";
                return "Requested";
            }

            if (string.Equals(m.ReferenceType, "BorrowLog", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Source, "Borrow", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(m.Direction, "IN", StringComparison.OrdinalIgnoreCase))
                    return "Returned";
                return "Borrowed";
            }

            if (!string.IsNullOrWhiteSpace(m.InventoryEntryType))
            {
                if (string.Equals(m.InventoryEntryType, "Positive", StringComparison.OrdinalIgnoreCase))
                    return "Stock In";
                if (string.Equals(m.InventoryEntryType, "Negative", StringComparison.OrdinalIgnoreCase))
                    return "Stock Out";
            }

            if (string.Equals(m.Direction, "IN", StringComparison.OrdinalIgnoreCase)) return "Stock In";
            if (string.Equals(m.Direction, "OUT", StringComparison.OrdinalIgnoreCase)) return "Stock Out";

            return "Update";
        }

        private static string ClassifyPriority(ItemMovementAuditDto m)
        {
            if (m == null) return null;

            var notes = m.Notes ?? string.Empty;

            if (ContainsAny(notes, "lost", "missing", "broken", "damage", "damaged")) return "High";
            if (ContainsAny(notes, "unprocessed")) return "Medium";

            return "Low";
        }

        private static string DetermineStatusAfter(ItemMovementAuditDto m, string prevStatus)
        {
            if (m == null) return prevStatus;

            if (!string.IsNullOrWhiteSpace(m.AuditStatus) &&
                (string.Equals(m.ReferenceType, "ItemAuditTrail", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(m.Source, "AuditTrail", StringComparison.OrdinalIgnoreCase)))
                return m.AuditStatus;

            var type = m.MovementType ?? string.Empty;
            if (type == "Lost" || type == "Broken" || type == "Damaged" || type == "Archived" || type == "Inactive" || type == "Active"
                || type == "Sold" || type == "Disposed")
                return type;

            if (string.Equals(type, "Repair", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "Service", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "Calibration", StringComparison.OrdinalIgnoreCase))
                return "Maintenance";

            if (string.Equals(type, "Approved", StringComparison.OrdinalIgnoreCase)) return "Approved";
            if (string.Equals(type, "Requested", StringComparison.OrdinalIgnoreCase)) return "Requested";
            if (string.Equals(type, "Returned", StringComparison.OrdinalIgnoreCase)) return "Returned";

            var notes = m.Notes ?? string.Empty;
            if (ContainsAny(notes, "item sold", " sold ")) return "Sold";
            if (ContainsAny(notes, "item disposed", "dispose", " disposed")) return "Disposed";

            if (string.Equals(m.Direction, "OUT", StringComparison.OrdinalIgnoreCase)) return "Issued";
            if (string.Equals(m.Direction, "IN", StringComparison.OrdinalIgnoreCase)) return "In Stock";

            return prevStatus;
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            if (string.IsNullOrEmpty(value) || needles == null || needles.Length == 0)
                return false;

            foreach (var n in needles)
            {
                if (string.IsNullOrEmpty(n)) continue;
                if (value.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

        private void PopulateMovementFilters(List<ItemMovementAuditDto> movements)
        {
            var selectedCategory = SelectedCategory ?? "All";
            var selectedType = SelectedType ?? "All";

            var categories = (movements ?? new List<ItemMovementAuditDto>())
                .Select(m => m?.MovementCategory)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();

            var types = (movements ?? new List<ItemMovementAuditDto>())
                .Select(m => m?.MovementType)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();

            CategoryOptions.Clear();
            CategoryOptions.Add("All");
            foreach (var c in categories) CategoryOptions.Add(c);
            _selectedCategory = CategoryOptions.FirstOrDefault(s => string.Equals(s, selectedCategory, StringComparison.OrdinalIgnoreCase)) ?? "All";
            OnPropertyChanged(nameof(SelectedCategory));

            TypeOptions.Clear();
            TypeOptions.Add("All");
            foreach (var t in types) TypeOptions.Add(t);
            _selectedType = TypeOptions.FirstOrDefault(s => string.Equals(s, selectedType, StringComparison.OrdinalIgnoreCase)) ?? "All";
            OnPropertyChanged(nameof(SelectedType));
        }
    }
}
