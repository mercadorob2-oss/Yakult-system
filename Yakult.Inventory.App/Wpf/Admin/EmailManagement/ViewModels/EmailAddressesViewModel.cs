using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.EmailManagement.ViewModels
{
    public class EmailAddressesViewModel : ViewModelBase
    {
        private const int PageSize = 20;
        private readonly EmailRepository _repo;

        private List<EmailAddressDto> _all      = new List<EmailAddressDto>();
        private List<EmailAddressDto> _filtered = new List<EmailAddressDto>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private readonly Dictionary<string, HashSet<string>> _columnFilters
            = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private string _sortColumn    = null;
        private bool   _sortAscending = true;

        private bool   _isLoading;
        private string _searchText = string.Empty;
        private string _pageInfo   = "Page 0 of 0 (0 records)";
        private bool   _canGoPrev;
        private bool   _canGoNext;

        public ObservableCollection<EmailAddressDto> PagedRows { get; } = new ObservableCollection<EmailAddressDto>();
        public List<EmailAddressDto> AllRows => _all;

        public bool   IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }
        public string PageInfo  { get => _pageInfo;  set => SetField(ref _pageInfo,  value); }
        public bool   CanGoPrev { get => _canGoPrev; set => SetField(ref _canGoPrev, value); }
        public bool   CanGoNext { get => _canGoNext; set => SetField(ref _canGoNext, value); }

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilter(); }
        }

        public EmailAddressesViewModel(EmailRepository repo)
        {
            _repo = repo;
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                _all = await _repo.GetEmailAddressesAsync(activeOnly: false);
                _currentPage = 1;
                ApplyFilter();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static string GetColumnValue(EmailAddressDto r, string column)
        {
            switch (column)
            {
                case "EmailAddress": return r.EmailAddress;
                case "DisplayName":  return r.DisplayName;
                case "Active":       return r.IsActive ? "Active" : "Inactive";
                default:             return null;
            }
        }

        public void ApplyFilter()
        {
            string q = (_searchText ?? "").Trim();

            IEnumerable<EmailAddressDto> filtered = _all;

            if (!string.IsNullOrWhiteSpace(q))
            {
                filtered = filtered.Where(a =>
                    (a.EmailAddress ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || (a.DisplayName ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            foreach (var kvp in _columnFilters)
            {
                var selected = kvp.Value;
                string col = kvp.Key;
                filtered = filtered.Where(r => selected.Contains(GetColumnValue(r, col) ?? "", StringComparer.OrdinalIgnoreCase));
            }

            _filtered = filtered.ToList();

            if (!string.IsNullOrEmpty(_sortColumn))
            {
                Func<EmailAddressDto, string> key = r => GetColumnValue(r, _sortColumn) ?? "";
                _filtered = _sortAscending
                    ? _filtered.OrderBy(key, StringComparer.OrdinalIgnoreCase).ToList()
                    : _filtered.OrderByDescending(key, StringComparer.OrdinalIgnoreCase).ToList();
            }

            _currentPage = 1;
            RebuildPagedRows();
        }

        public HashSet<string> GetColumnFilter(string column)
            => _columnFilters.TryGetValue(column, out var f) ? f : null;

        public void SetColumnFilter(string column, HashSet<string> values)
        {
            if (values == null || values.Count == 0)
                _columnFilters.Remove(column);
            else
                _columnFilters[column] = values;
            ApplyFilter();
        }

        public void ClearColumnFilter(string column)
        {
            _columnFilters.Remove(column);
            ApplyFilter();
        }

        public void SetSort(string column, bool ascending)
        {
            _sortColumn    = column;
            _sortAscending = ascending;
            ApplyFilter();
        }

        public List<string> GetUniqueValuesForColumn(string column)
        {
            return _all.Select(r => GetColumnValue(r, column) ?? "")
                       .Where(v => !string.IsNullOrEmpty(v))
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                       .ToList();
        }

        public void GoToNextPage()  { if (_currentPage < _totalPages) { _currentPage++; RebuildPagedRows(); } }
        public void GoToPrevPage()  { if (_currentPage > 1)           { _currentPage--; RebuildPagedRows(); } }
        public void GoToFirstPage() { if (_currentPage != 1)           { _currentPage = 1;           RebuildPagedRows(); } }
        public void GoToLastPage()  { if (_currentPage != _totalPages) { _currentPage = _totalPages; RebuildPagedRows(); } }

        private void RebuildPagedRows()
        {
            _totalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1)           _currentPage = 1;

            PagedRows.Clear();
            foreach (var r in _filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(r);

            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
            PageInfo  = _filtered.Count == 0
                ? "Page 0 of 0 (0 records)"
                : $"Page {_currentPage} of {_totalPages} ({_filtered.Count} records)";
        }

        // ── CRUD (identical backend calls to SystemEmailSettingsForm.cs) ──────

        public async Task<int> AddEmailsAsync(List<EmailAddressDto> newEmails)
        {
            int saved = 0;
            foreach (var item in newEmails)
            {
                item.CreatedByUserId = AppSession.CurrentUserId;
                await _repo.SaveEmailAddressAsync(item);
                saved++;
            }
            return saved;
        }

        // Validates + saves all rows currently loaded (mirrors "Save Changes" bulk-grid save).
        public async Task<(int Saved, List<string> Errors)> SaveAllAsync()
        {
            var errors = new List<string>();
            var toSave  = new List<EmailAddressDto>();

            for (int i = 0; i < _all.Count; i++)
            {
                var item = _all[i];
                var emailRaw = (item.EmailAddress ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(emailRaw))
                {
                    errors.Add($"Row {i + 1}: Email address is required.");
                    continue;
                }

                try
                {
                    var parsed = new MailAddress(emailRaw);
                    item.EmailAddress = parsed.Address;
                }
                catch
                {
                    errors.Add($"Row {i + 1}: \"{emailRaw}\" is not a valid email address.");
                    continue;
                }

                item.DisplayName = string.IsNullOrWhiteSpace(item.DisplayName) ? null : item.DisplayName.Trim();
                toSave.Add(item);
            }

            if (errors.Count > 0)
                return (0, errors);

            int saved = 0;
            foreach (var item in toSave)
            {
                if (item.EmailId == 0)
                    item.CreatedByUserId = AppSession.CurrentUserId;

                await _repo.SaveEmailAddressAsync(item);
                saved++;
            }

            return (saved, errors);
        }

        public async Task DeactivateAsync(int emailId)
        {
            var email = await _repo.GetEmailAddressByIdAsync(emailId);
            if (email == null) return;

            email.IsActive = false;
            await _repo.SaveEmailAddressAsync(email);
        }

        public async Task DeleteAsync(int emailId)
        {
            await _repo.DeleteEmailAddressAsync(emailId);
        }

        public Task<List<EmailAddressReferenceDto>> GetReferencesAsync(int emailId)
            => _repo.GetEmailAddressReferencesAsync(emailId);

        public Task UnlinkAsync(int emailId, bool includeSmtpProfiles)
            => _repo.UnlinkEmailAddressAsync(emailId, includeSmtpProfiles);

        public int NextEmailId() => _all.Count > 0 ? _all.Max(x => x.EmailId) + 1 : 1;
    }
}
