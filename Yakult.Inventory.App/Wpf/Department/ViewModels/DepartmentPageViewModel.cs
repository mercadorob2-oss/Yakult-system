using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using Yakult.Inventory.App.Services;
using DepartmentDto = Yakult.Inventory.App.Pages.DepartmentDto;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Department.ViewModels
{
    /// <summary>Grid-display DTO, moved here verbatim from the bottom of the original
    /// Pages\Department\ViewDepartmentPage.cs. <c>Selected</c> is new (checkbox binding,
    /// mirrors Vendor/Category/Branch). <c>CompanyName</c> is kept even though the original
    /// query never populated it (dead field in the source) — left as-is, not removed.</summary>
    public class DepartmentViewDto
    {
        public bool Selected { get; set; }
        public int DeptId { get; set; }
        public string Name { get; set; }
        public string Section { get; set; }
        public string Description { get; set; }
        public string CompanyName { get; set; }
        public DateTime DateCreated { get; set; }
        public string CreatedByName { get; set; }
        public bool IsArchived { get; set; }
    }

    /// <summary>
    /// Business logic for the Departments page, ported verbatim from
    /// Pages\Department\ViewDepartmentPage.cs. Shares the same quirks as
    /// [[BranchPageViewModel]]: ApplySearchFilter() never reapplies an active column sort,
    /// Filter-By "Default" triggers a full synchronous DB reload, and picking the Sort-By
    /// dropdown resets Filter-By to Default first (possibly cascading into that reload) before
    /// applying the chosen sort. Archive uses an upsert (INSERT-or-UPDATE) into ArchiveStatus
    /// inside one transaction; Permanent Delete calls a private DeleteDepartment() per item.
    /// No permission gating exists on this page today (none added here).
    /// </summary>
    public sealed class DepartmentPageViewModel : ViewModelBase
    {
        private const int PageSize = 10;

        private readonly string _connectionString = Core.DatabaseConfig.ConnectionString;

        private List<DepartmentViewDto> _allDepartments = new List<DepartmentViewDto>();
        private List<DepartmentViewDto> _filteredDepartments = new List<DepartmentViewDto>();

        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;
        private bool _suppressSortByChange;

        public DepartmentPageViewModel()
        {
            FilterByOptions = new ObservableCollection<string> { "Default", "Most Recently Added", "Oldest Added" };
            SortByOptions = new ObservableCollection<ListSortOption>();
            PagedDepartments = new ObservableCollection<DepartmentViewDto>();

            _selectedFilterBy = "Default";

            AddCommand = new RelayCommand(Add);
            EditCommand = new RelayCommand(Edit);
            ArchiveCommand = new RelayCommand(Archive);
            DeleteCommand = new RelayCommand(PermanentDelete);
            RefreshCommand = new RelayCommand(LoadDepartments);
            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; UpdatePagination(); });
            PrevPageCommand = new RelayCommand(() => { if (CurrentPage > 1) { CurrentPage--; UpdatePagination(); } });
            NextPageCommand = new RelayCommand(() => { if (CurrentPage < TotalPages) { CurrentPage++; UpdatePagination(); } });
            LastPageCommand = new RelayCommand(() => { CurrentPage = TotalPages; UpdatePagination(); });

            TotalCardCommand = new RelayCommand(() => SetSummaryMode(DepartmentSummaryMode.All));
            ActiveCardCommand = new RelayCommand(() => SetSummaryMode(DepartmentSummaryMode.ActiveOnly));
            WithSectionCardCommand = new RelayCommand(() => SetSummaryMode(
                _summaryMode == DepartmentSummaryMode.WithSectionOnly ? DepartmentSummaryMode.All : DepartmentSummaryMode.WithSectionOnly));
            InactiveCardCommand = new RelayCommand(() => SetSummaryMode(DepartmentSummaryMode.InactiveOnly));
            ResetFiltersCommand = new RelayCommand(ResetFilters);

            _selection = new SelectionTracker<DepartmentViewDto>(d => d.Selected, (d, s) => d.Selected = s, d => d.DeptId);
            _selection.Changed += () =>
            {
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(HasSelection));
            };
        }

        public RelayCommand ResetFiltersCommand { get; private set; }

        /// <summary>Clears every filter control back to its default state.</summary>
        private void ResetFilters()
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));

            _selectedFilterBy = "Default";
            OnPropertyChanged(nameof(SelectedFilterBy));

            _showInactive = false;
            OnPropertyChanged(nameof(ShowInactive));
            _summaryMode = DepartmentSummaryMode.ActiveOnly;
            NotifySummaryCardsChanged();

            ApplySearchFilter();
        }

        // ── Filter/search state ──────────────────────────────────────────────

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplySearchFilter(); }
        }

        private bool _showInactive;
        public bool ShowInactive
        {
            get => _showInactive;
            set
            {
                if (!SetField(ref _showInactive, value)) return;
                _summaryMode = value ? DepartmentSummaryMode.All : DepartmentSummaryMode.ActiveOnly;
                NotifySummaryCardsChanged();
                ApplySearchFilter();
            }
        }

        // ── Clickable summary cards (Total/Active/With Section/Inactive) ─────
        private enum DepartmentSummaryMode { All, ActiveOnly, InactiveOnly, WithSectionOnly }
        private DepartmentSummaryMode _summaryMode = DepartmentSummaryMode.ActiveOnly;

        public bool IsTotalCardActive => _summaryMode == DepartmentSummaryMode.All;
        public bool IsActiveCardActive => _summaryMode == DepartmentSummaryMode.ActiveOnly;
        public bool IsWithSectionCardActive => _summaryMode == DepartmentSummaryMode.WithSectionOnly;
        public bool IsInactiveCardActive => _summaryMode == DepartmentSummaryMode.InactiveOnly;

        public RelayCommand TotalCardCommand { get; private set; }
        public RelayCommand ActiveCardCommand { get; private set; }
        public RelayCommand WithSectionCardCommand { get; private set; }
        public RelayCommand InactiveCardCommand { get; private set; }

        private void SetSummaryMode(DepartmentSummaryMode mode)
        {
            _summaryMode = mode;
            NotifySummaryCardsChanged();

            bool needsShowInactive = mode != DepartmentSummaryMode.ActiveOnly;
            if (_showInactive != needsShowInactive)
            {
                _showInactive = needsShowInactive;
                OnPropertyChanged(nameof(ShowInactive));
            }
            ApplySearchFilter();
        }

        private void NotifySummaryCardsChanged()
        {
            OnPropertyChanged(nameof(IsTotalCardActive));
            OnPropertyChanged(nameof(IsActiveCardActive));
            OnPropertyChanged(nameof(IsWithSectionCardActive));
            OnPropertyChanged(nameof(IsInactiveCardActive));
        }

        public ObservableCollection<string> FilterByOptions { get; }

        private string _selectedFilterBy;
        public string SelectedFilterBy
        {
            get => _selectedFilterBy;
            set
            {
                if (SetField(ref _selectedFilterBy, value))
                {
                    RequestClearSortGlyphs?.Invoke();

                    if (value == "Default")
                    {
                        LoadDepartments(); // full reload, matches CmbFilterBy_SelectedIndexChanged
                        return;
                    }

                    var dir = value == "Most Recently Added" ? ListSortDirection.Descending
                            : value == "Oldest Added" ? ListSortDirection.Ascending
                            : (ListSortDirection?)null;
                    if (dir == null) return;

                    SortDataSource("DateCreated", dir.Value);
                    UpdatePagination();
                }
            }
        }

        // ── Sort By dropdown ──────────────────────────────────────────────────

        public ObservableCollection<ListSortOption> SortByOptions { get; }

        private ListSortOption _selectedSortBy;
        public ListSortOption SelectedSortBy
        {
            get => _selectedSortBy;
            set
            {
                _selectedSortBy = value;
                OnPropertyChanged();

                if (_suppressSortByChange || value == null) return;

                SelectedFilterBy = "Default"; // may cascade into a full reload — see setter above

                SortDataSource(value.ColumnKey, value.Direction);
                UpdatePagination();
            }
        }

        public void SetSortByOptions(List<ListSortOption> options, string defaultColumnKey)
        {
            _suppressSortByChange = true;
            try
            {
                SortByOptions.Clear();
                foreach (var o in options) SortByOptions.Add(o);

                ListSortOption selected = null;
                if (_sortColumnKey != null)
                {
                    selected = options.FirstOrDefault(o =>
                        o.ColumnKey == _sortColumnKey && o.Direction == (_sortDirection ?? ListSortDirection.Ascending));
                }
                if (selected == null && !string.IsNullOrEmpty(defaultColumnKey))
                {
                    selected = options.FirstOrDefault(o => o.ColumnKey == defaultColumnKey && o.Direction == ListSortDirection.Ascending);
                }

                SelectedSortBy = selected ?? options.FirstOrDefault();
            }
            finally
            {
                _suppressSortByChange = false;
            }
        }

        /// <summary>Header-click sort — never touches Filter-By.</summary>
        public void SortByColumn(string columnKey, ListSortDirection direction)
        {
            if (string.IsNullOrEmpty(columnKey)) return;

            SortDataSource(columnKey, direction);
            UpdatePagination();

            _suppressSortByChange = true;
            try
            {
                var match = SortByOptions.FirstOrDefault(o => o.ColumnKey == _sortColumnKey && o.Direction == _sortDirection);
                if (match != null) SelectedSortBy = match;
            }
            finally
            {
                _suppressSortByChange = false;
            }
        }

        private void SortDataSource(string propertyName, ListSortDirection direction)
        {
            if (_filteredDepartments == null || string.IsNullOrWhiteSpace(propertyName))
                return;

            try
            {
                var sorted = direction == ListSortDirection.Ascending
                    ? _filteredDepartments.OrderBy(x => x.GetType().GetProperty(propertyName)?.GetValue(x, null))
                    : _filteredDepartments.OrderByDescending(x => x.GetType().GetProperty(propertyName)?.GetValue(x, null));

                _filteredDepartments = sorted.ToList();
                _sortColumnKey = propertyName;
                _sortDirection = direction;
            }
            catch
            {
                // Ignore sorting errors (matches original's silent catch).
            }
        }

        public event Action RequestClearSortGlyphs;

        // ── Paging ────────────────────────────────────────────────────────────

        public ObservableCollection<DepartmentViewDto> PagedDepartments { get; }

        // ── Selection (tracked against _allDepartments, not just the current page) ──────
        private readonly SelectionTracker<DepartmentViewDto> _selection;
        public int SelectedCount => _selection.SelectedCount;
        public bool HasSelection => _selection.HasSelection;

        public List<DepartmentViewDto> GetSelectedDepartments() => _selection.GetSelected(_allDepartments);
        public void SetDepartmentSelected(int deptId, bool selected) => _selection.SetSelected(_allDepartments, deptId, selected);
        public void SetPageSelected(bool selected) => _selection.SetManySelected(PagedDepartments, _allDepartments, selected);
        public void ClearSelection() => _selection.ClearSelection(_allDepartments);

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set => SetField(ref _currentPage, value);
        }

        private int TotalPages => (_filteredDepartments?.Count ?? 0) == 0 ? 0 : (int)Math.Ceiling((double)_filteredDepartments.Count / PageSize);

        private string _pageInfoText = "Page 0 of 0 (0 departments)";
        public string PageInfoText
        {
            get => _pageInfoText;
            private set => SetField(ref _pageInfoText, value);
        }

        private bool _canFirstPage, _canPrevPage, _canNextPage, _canLastPage;
        public bool CanFirstPage { get => _canFirstPage; private set => SetField(ref _canFirstPage, value); }
        public bool CanPrevPage { get => _canPrevPage; private set => SetField(ref _canPrevPage, value); }
        public bool CanNextPage { get => _canNextPage; private set => SetField(ref _canNextPage, value); }
        public bool CanLastPage { get => _canLastPage; private set => SetField(ref _canLastPage, value); }

        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand ArchiveCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        private void UpdatePagination()
        {
            var data = _filteredDepartments ?? new List<DepartmentViewDto>();
            if (data.Count == 0)
            {
                PagedDepartments.Clear();
                PageInfoText = "Page 0 of 0 (0 departments)";
                CanFirstPage = CanPrevPage = CanNextPage = CanLastPage = false;
                return;
            }

            int totalPages = TotalPages;
            if (CurrentPage > totalPages) CurrentPage = totalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var paged = data.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            PagedDepartments.Clear();
            foreach (var d in paged) PagedDepartments.Add(d);

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({data.Count} departments)";
            CanFirstPage = CurrentPage > 1;
            CanPrevPage = CurrentPage > 1;
            CanNextPage = CurrentPage < totalPages;
            CanLastPage = CurrentPage < totalPages;
        }

        // ── Summary cards (Total/Active/With Section/Inactive) ───────────────

        private int _totalCount, _activeCount, _withSectionCount, _inactiveCount;
        public int TotalCount { get => _totalCount; private set => SetField(ref _totalCount, value); }
        public int ActiveCount { get => _activeCount; private set => SetField(ref _activeCount, value); }
        public int WithSectionCount { get => _withSectionCount; private set => SetField(ref _withSectionCount, value); }
        public int InactiveCount { get => _inactiveCount; private set => SetField(ref _inactiveCount, value); }

        private void UpdateSummaryCards(IEnumerable<DepartmentViewDto> data)
        {
            var list = data?.ToList() ?? new List<DepartmentViewDto>();
            TotalCount = list.Count;
            ActiveCount = list.Count(d => !d.IsArchived);
            WithSectionCount = list.Count(d => !string.IsNullOrWhiteSpace(d.Section));
            InactiveCount = list.Count(d => d.IsArchived);
        }

        // ── Data load ─────────────────────────────────────────────────────────

        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestError;
        public event Action<string, string> RequestWarning;
        public event Action RequestAddDepartment;
        public event Action<DepartmentDto> RequestEditDepartment;

        public Func<string, string, bool> ConfirmYesNo { get; set; }

        /// <summary>Synchronous, matches the original LoadDepartments() (plain blocking ADO.NET).</summary>
        public void LoadDepartments()
        {
            try
            {
                var departments = new List<DepartmentViewDto>();

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT
                            d.DeptId,
                            d.Name,
                            d.Section,
                            d.Description,
                            d.DateCreated,
                            u.Name AS CreatedByName,
                            CASE WHEN arc.ArchiveId IS NULL THEN 0 ELSE 1 END AS IsArchived
                        FROM dbo.Department d
                        LEFT JOIN dbo.[User] u ON d.Createdby = u.UserId
                        LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Department' AND arc.EntityId = d.DeptId AND arc.IsArchived = 1
                        ORDER BY d.DateCreated DESC, d.DeptId DESC", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            departments.Add(new DepartmentViewDto
                            {
                                DeptId        = reader.GetInt32(0),
                                Name          = reader.GetString(1),
                                Section       = reader.IsDBNull(2) ? null : reader.GetString(2),
                                Description   = reader.IsDBNull(3) ? null : reader.GetString(3),
                                DateCreated   = reader.GetDateTime(4),
                                CreatedByName = reader.IsDBNull(5) ? "N/A" : reader.GetString(5),
                                IsArchived    = !reader.IsDBNull(6) && reader.GetInt32(6) == 1
                            });
                        }
                    }
                }

                _allDepartments = departments;
                ApplySearchFilter();
                _selection.Sync(_allDepartments);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load departments: {ex.Message}");
            }
        }

        /// <summary>No sort re-applied here — matches the original ApplySearchFilter().</summary>
        private void ApplySearchFilter()
        {
            if (_allDepartments == null)
            {
                _filteredDepartments = new List<DepartmentViewDto>();
                UpdateSummaryCards(Enumerable.Empty<DepartmentViewDto>());
                CurrentPage = 1;
                UpdatePagination();
                return;
            }

            var filtered = _allDepartments.AsEnumerable();

            string term = SearchText;
            if (!string.IsNullOrWhiteSpace(term))
            {
                var lower = term.Trim().ToLowerInvariant();
                filtered = filtered.Where(d =>
                    (!string.IsNullOrWhiteSpace(d.Name) && d.Name.ToLowerInvariant().Contains(lower)) ||
                    (!string.IsNullOrWhiteSpace(d.Section) && d.Section.ToLowerInvariant().Contains(lower)) ||
                    (!string.IsNullOrWhiteSpace(d.Description) && d.Description.ToLowerInvariant().Contains(lower)));
            }

            var forSummary = filtered.ToList();
            UpdateSummaryCards(forSummary);
            filtered = forSummary;

            switch (_summaryMode)
            {
                case DepartmentSummaryMode.ActiveOnly:
                    filtered = filtered.Where(d => !d.IsArchived);
                    break;
                case DepartmentSummaryMode.InactiveOnly:
                    filtered = filtered.Where(d => d.IsArchived);
                    break;
                case DepartmentSummaryMode.WithSectionOnly:
                    filtered = filtered.Where(d => !string.IsNullOrWhiteSpace(d.Section));
                    break;
                // All: no filter
            }

            _filteredDepartments = filtered.ToList();
            CurrentPage = 1;
            UpdatePagination();
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void Add() => RequestAddDepartment?.Invoke();

        private void Edit()
        {
            var checkedItems = _selection.GetSelected(_allDepartments);

            if (checkedItems.Count == 0)
            {
                RequestInfo?.Invoke("Information", "Please select at least one department to edit.");
                return;
            }
            if (checkedItems.Count > 1)
            {
                RequestInfo?.Invoke("Multiple Selection", "Please select only one department to edit.");
                return;
            }

            var deptDto = LoadDepartmentForEdit(checkedItems[0].DeptId);
            if (deptDto == null) return;

            RequestEditDepartment?.Invoke(deptDto);
        }

        private DepartmentDto LoadDepartmentForEdit(int deptId)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT d.DeptId, d.Name, d.Section, d.Description, d.DateCreated,
                               u.Name AS CreatedByName
                        FROM dbo.Department d
                        LEFT JOIN dbo.[User] u ON d.Createdby = u.UserId
                        WHERE d.DeptId = @DeptId", con))
                    {
                        cmd.Parameters.AddWithValue("@DeptId", deptId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new DepartmentDto
                                {
                                    DeptId        = reader.GetInt32(0),
                                    Name          = reader.GetString(1),
                                    Section       = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    Description   = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    DateCreated   = reader.GetDateTime(4),
                                    CreatedByName = reader.IsDBNull(5) ? "N/A" : reader.GetString(5)
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load department: {ex.Message}");
            }
            return null;
        }

        public bool UpdateDepartment(DepartmentDto dept)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        UPDATE dbo.Department
                        SET Name        = @Name,
                            Section     = @Section,
                            Description = @Description
                        WHERE DeptId = @DeptId", con))
                    {
                        cmd.Parameters.AddWithValue("@DeptId",      dept.DeptId);
                        cmd.Parameters.AddWithValue("@Name",        dept.Name);
                        cmd.Parameters.AddWithValue("@Section",     (object)dept.Section ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Description", (object)dept.Description ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
                ActivityLogger.Log(ActivityLogger.Actions.Update, "Department", dept.DeptId, $"Department '{dept.Name}' updated");
                return true;
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to update department: {ex.Message}");
                return false;
            }
        }

        private void Archive()
        {
            var checkedItems = _selection.GetSelected(_allDepartments);

            if (checkedItems.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please select at least one department to archive.");
                return;
            }

            string message = checkedItems.Count == 1
                ? $"Are you sure you want to archive this department?\n\nDepartment: {checkedItems[0].Name}\n\nThe department will be moved to the archive."
                : $"Are you sure you want to archive {checkedItems.Count} departments?\n\nAll selected departments will be moved to the archive.";

            bool confirm = ConfirmYesNo?.Invoke("Confirm Archive", message) ?? false;
            if (!confirm) return;

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var transaction = con.BeginTransaction())
                    {
                        try
                        {
                            string upsertArchiveSql = @"
                                IF NOT EXISTS (
                                    SELECT 1 FROM ArchiveStatus
                                    WHERE EntityType = 'Department' AND EntityId = @DeptId
                                )
                                    INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                    VALUES ('Department', @DeptId, 1, GETDATE(), @ArchivedBy, 'Archived from Department page')
                                ELSE
                                    UPDATE ArchiveStatus
                                    SET IsArchived = 1, ArchivedAt = GETDATE(), ArchivedBy = @ArchivedBy, ArchiveReason = 'Archived from Department page'
                                    WHERE EntityType = 'Department' AND EntityId = @DeptId";

                            foreach (var dept in checkedItems)
                            {
                                using (var cmd = new SqlCommand(upsertArchiveSql, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@DeptId", dept.DeptId);
                                    cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();

                            foreach (var dept in checkedItems)
                                ActivityLogger.Log(ActivityLogger.Actions.Delete, "Department", dept.DeptId, $"Department '{dept.Name}' archived");

                            string successMsg = checkedItems.Count == 1
                                ? "Department archived successfully!"
                                : $"{checkedItems.Count} departments archived successfully!";
                            RequestInfo?.Invoke("Success", successMsg);
                            LoadDepartments();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Error archiving department(s):\n\n{ex.Message}");
            }
        }

        private void PermanentDelete()
        {
            var checkedItems = _selection.GetSelected(_allDepartments);

            if (checkedItems.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please select at least one department to delete.");
                return;
            }

            string itemList = string.Join("\n", checkedItems.Select(d => $"• {d.Name}"));
            string countText = checkedItems.Count == 1 ? "this department" : $"these {checkedItems.Count} departments";

            bool confirm = ConfirmYesNo?.Invoke("Confirm Permanent Deletion",
                "⚠️ PERMANENT DELETE WARNING ⚠️\n\n" +
                $"This will PERMANENTLY delete {countText}:\n\n{itemList}\n\n" +
                "This action CANNOT be undone!\n\n" +
                "⚠️ Only proceed if this was a DATA ENTRY ERROR.\n" +
                "⚠️ Use 'Archive' button instead for normal records.\n\n" +
                "Are you absolutely sure you want to permanently delete?") ?? false;
            if (!confirm) return;

            bool doubleCheck = ConfirmYesNo?.Invoke("Final Confirmation",
                $"FINAL CONFIRMATION\n\n{checkedItems.Count} department(s) will be permanently deleted and cannot be recovered.\n\nAre you absolutely certain?") ?? false;
            if (!doubleCheck) return;

            int successCount = 0;
            int failureCount = 0;
            var failedItems = new List<string>();

            foreach (var dept in checkedItems)
            {
                try
                {
                    if (DeleteDepartment(dept.DeptId))
                    {
                        successCount++;
                    }
                    else
                    {
                        failureCount++;
                        failedItems.Add($"• {dept.Name} - Unknown error");
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    string reason = ex.Message.Contains("REFERENCE constraint") || ex.Message.Contains("conflicted with")
                        ? "Has related records (employees, etc.)"
                        : ex.Message;
                    failedItems.Add($"• {dept.Name} - {reason}");
                }
            }

            if (successCount > 0 && failureCount == 0)
            {
                RequestInfo?.Invoke("Deleted Successfully", $"✔ All {successCount} department(s) permanently deleted!\n\n✔ Removed from database");
            }
            else if (successCount > 0 && failureCount > 0)
            {
                RequestWarning?.Invoke("Partial Success",
                    $"⚠️ PARTIAL SUCCESS\n\n✔ Successfully deleted: {successCount} department(s)\n✗ Failed to delete: {failureCount} department(s)\n\n" +
                    $"Failed items:\n{string.Join("\n", failedItems)}\n\nTip: Use 'Archive' instead for items with related records.");
            }
            else if (failureCount > 0)
            {
                RequestError?.Invoke("Deletion Failed",
                    $"✗ Failed to delete all {failureCount} department(s)\n\nFailed items:\n{string.Join("\n", failedItems)}\n\nTip: Use 'Archive' instead for items with related records.");
            }

            if (successCount > 0)
                LoadDepartments();
        }

        /// <summary>Matches the original exactly: catches its own exceptions and shows an
        /// immediate error dialog, returning false rather than throwing. This means the
        /// try/catch around the call site in PermanentDelete() is effectively dead code (it
        /// never observes a thrown exception) — same as the original BtnPermanentDelete_Click,
        /// preserved rather than "fixed" since fixing it would change the failure UX (today a
        /// failed delete shows its own popup here PLUS a generic "Unknown error" line in the
        /// aggregate summary, instead of one popup with the specific reason).</summary>
        private bool DeleteDepartment(int deptId)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand("DELETE FROM dbo.Department WHERE DeptId = @DeptId", con))
                    {
                        cmd.Parameters.AddWithValue("@DeptId", deptId);
                        cmd.ExecuteNonQuery();
                    }
                }
                ActivityLogger.Log(ActivityLogger.Actions.Delete, "Department", deptId, $"Department ID {deptId} permanently deleted");
                return true;
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to delete department: {ex.Message}\n\nNote: Cannot delete if there are employees assigned to this department.");
                return false;
            }
        }
    }
}
