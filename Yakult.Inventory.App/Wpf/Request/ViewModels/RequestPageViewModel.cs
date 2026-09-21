using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Request.ViewModels
{
    /// <summary>
    /// Business logic for the Requests page, ported from Pages\Request\ViewRequestsPage.cs.
    /// Preserves search/status filtering, Filter By date ordering, Sort By dropdown, manual
    /// pagination (fixed page size of 10, matching the original), and the Add/Edit/Mark
    /// Submitted/Archive/Delete/PDF-export workflows exactly as they existed in WinForms.
    ///
    /// Selection scope note: unlike the Invoice Reports page, the checkbox column here (and the
    /// header "select all") only ever operates on the CURRENT PAGE's rows — this matches the
    /// original, which looped `dgvRequests.Rows` (bound only to the current page) rather than the
    /// full filtered set. Do not "fix" this to span pages; it is the original behavior.
    /// </summary>
    public sealed partial class RequestPageViewModel : ViewModelBase
    {
        private readonly RequestRepository _repository = new RequestRepository();

        private List<RequestRow> _allRows = new List<RequestRow>();
        private List<RequestRow> _filteredRows = new List<RequestRow>();

        /// <summary>
        /// When set, LoadRequests() restricts the whole page (rows, stat tiles, category filter
        /// options) to only these categories — e.g. the Consumable Management Portal's Card 2
        /// restricts this to Cartridge/Ink/Printhead/Toner Cartridge. Null (default) means
        /// unrestricted, matching MainForm's normal "View Requests" usage.
        /// </summary>
        public HashSet<string> AllowedCategories { get; set; }

        /// <summary>
        /// When true, LoadRequests() additionally restricts to rows whose dbo.Request.WorkflowType
        /// is explicitly 'RequestSetManagement' — set once at submission time (see
        /// RequesterPortalService). This is what actually excludes pure-cartridge submissions
        /// (which own Card 1's queue) from Card 2; AllowedCategories alone is not enough, since a
        /// pure-cartridge request still has Item.Category == "Cartridge".
        /// </summary>
        public bool RestrictToRequestSetManagementWorkflow { get; set; }

        public RequestPageViewModel()
        {
            FilterByOptions = new ObservableCollection<string> { "Default", "Most Recently Added", "Oldest Added" };
            StatusOptions = new ObservableCollection<string> { "All", "Under Review", "On Hold", "Submitted", "Completed" };
            SortByOptions = new ObservableCollection<ListSortOption>();
            Rows = new ObservableCollection<RequestRow>();

            _selectedFilterBy = "Default";
            _selectedStatus = "All";
            PageSize = 10;

            RefreshCommand = new RelayCommand(LoadRequests);
            AddCommand = new RelayCommand(() => RequestAddNew?.Invoke());
            EditCommand = new RelayCommand(EditSelected, () => SelectedRow != null);
            MarkSubmittedCommand = new RelayCommand(MarkSelectedAsSubmitted, () => SelectedRow != null);
            // No CanExecute predicate here: WPF's CommandManager.RequerySuggested does not
            // reliably re-fire for a checkbox toggled inside a DataGrid cell in this
            // ElementHost-hosted WPF surface, which left these buttons stuck disabled even
            // after checking a row. Enablement is instead driven directly by the
            // HasAnySelectedOnPage binding on the Button (see RequestPageView.xaml); each
            // Execute method already guards against an empty selection with a warning.
            ArchiveCommand = new RelayCommand(ArchiveChecked);
            DeleteCommand = new RelayCommand(async () => await DeleteCheckedAsync());
            ExportSelectedPdfCommand = new RelayCommand(ExportSelectedPdf);
            BulkAddToSetCommand = new RelayCommand(BulkAddToSet);

            CategoryFilterOptions = new ObservableCollection<CategoryFilterOption>();
            ClearCategoryFilterCommand = new RelayCommand(ClearCategoryFilter);
            ResetFiltersCommand = new RelayCommand(ResetFilters);
            InitStatusCardCommands();

            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; BindPage(); });
            PrevPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, CurrentPage - 1); BindPage(); });
            NextPageCommand = new RelayCommand(() => { CurrentPage = CurrentPage + 1; BindPage(); });
            LastPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, GetTotalPages()); BindPage(); });

            _setFilterDebounceTimer.Tick += (s, e) => { _setFilterDebounceTimer.Stop(); LoadRequests(); };
            _searchDebounceTimer.Tick += (s, e) => { _searchDebounceTimer.Stop(); ApplyFilter(); };
        }

        // ── Events (WinForms interop seam — wired by the View's code-behind) ────
        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestWarning;
        public event Action<string, string> RequestError;
        public Func<string, string, bool> ConfirmYesNo { get; set; }

        /// <summary>
        /// Three-way confirmation used when deleting a Request that is still linked to a Set
        /// (e.g. the Set was created from this Request, or this Request was fulfilled into a
        /// Set). Returns "Yes" (delete both Request and Set, restore stock), "No" (cancel this
        /// item's deletion and leave both records untouched), or "Cancel" (abort the whole bulk
        /// delete operation). Wired by the View to a WinForms MessageBox.Show(..., YesNoCancel).
        /// </summary>
        public Func<string, string, string> ConfirmYesNoCancel { get; set; }
        public Func<string, string> RequestSaveFilePath { get; set; }

        /// <summary>View opens BatchAddRequestDialog (already a WPF Window) and calls LoadRequests() on success.</summary>
        public event Action RequestAddNew;

        /// <summary>View opens EditRequestDialog for this row; on OK (and not deleted), calls CommitEditedRequest.</summary>
        public event Action<RequestRow> RequestEditRow;

        /// <summary>View opens the extracted ArchiveRequestDialog for these checked rows.</summary>
        public event Action<List<RequestRow>> RequestArchiveRows;

        /// <summary>View opens AddRequestsToSetDialog for these checked ReqIds, then links them
        /// to the chosen/created Set via SetRepository.AddRequestToSetAsync — same pattern as
        /// ItemsPageView's "Bulk Add to Invoice".</summary>
        public event Action<List<int>> RequestBulkAddToSet;

        // ── Row selection (grid highlight, used by Edit/Mark Submitted/double-click) ──
        private RequestRow _selectedRow;
        public RequestRow SelectedRow
        {
            get => _selectedRow;
            set => SetField(ref _selectedRow, value);
        }

        // ── Commands ─────────────────────────────────────────────────────────
        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand MarkSubmittedCommand { get; }
        public RelayCommand ArchiveCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand ExportSelectedPdfCommand { get; }
        public RelayCommand BulkAddToSetCommand { get; }
        public RelayCommand ClearCategoryFilterCommand { get; }
        public RelayCommand ResetFiltersCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        // ── Grid data (current page only) ───────────────────────────────────
        public ObservableCollection<RequestRow> Rows { get; }

        private bool _hasAnySelectedOnPage;
        public bool HasAnySelectedOnPage
        {
            get => _hasAnySelectedOnPage;
            private set => SetField(ref _hasAnySelectedOnPage, value);
        }

        // ── "N Selected" badge — page-scoped (see class-level Selection scope note above) ──
        private int _selectedCount;
        public int SelectedCount { get => _selectedCount; private set => SetField(ref _selectedCount, value); }
        public bool HasSelection => SelectedCount > 0;

        public List<RequestRow> GetSelectedRows() => Rows.Where(r => r.Selected).ToList();
        public void ClearSelection() { foreach (var r in Rows) r.Selected = false; }

        /// <summary>Header "select all" checkbox — toggles Selected for the CURRENT PAGE's rows only
        /// (matches the original, which iterated `dgvRequests.Rows`, not the full filtered set).</summary>
        private bool? _selectAllState = false;
        public bool? SelectAllState
        {
            get => _selectAllState;
            set
            {
                _selectAllState = value;
                OnPropertyChanged();
                if (value != true && value != false) return;

                foreach (var row in Rows) row.Selected = value.Value;
            }
        }

        private void EditSelected()
        {
            var row = SelectedRow;
            if (row == null)
            {
                RequestWarning?.Invoke("No Selection", "Please select a request to edit.");
                return;
            }

            if (string.Equals(row.Category, "Cartridge", StringComparison.OrdinalIgnoreCase))
            {
                RequestWarning?.Invoke("Not Allowed",
                    "Cartridge requests cannot be edited here.\n\nPlease use the Cartridge Management module to manage cartridge requests.");
                return;
            }

            RequestEditRow?.Invoke(row);
        }

        private void ArchiveChecked()
        {
            var checkedRows = Rows.Where(r => r.Selected).ToList();
            if (checkedRows.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please select a request to archive.");
                return;
            }

            RequestArchiveRows?.Invoke(checkedRows);
        }
    }
}
