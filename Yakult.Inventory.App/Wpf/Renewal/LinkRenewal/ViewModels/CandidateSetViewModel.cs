using System;
using System.Collections.Generic;
using System.Linq;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Renewal.LinkRenewal.ViewModels
{
    public class CandidateSetViewModel : ViewModelBase
    {
        // ── Identity ──────────────────────────────────────────────────────────

        public int      SetId           { get; }
        public string   SetCode         { get; }
        public string   SetType         { get; }
        public string   DocumentNumber  { get; }
        public string   ReferenceNumber { get; }
        public string   CompanyName     { get; }
        public DateTime? StartDate      { get; }
        public DateTime? EndDate        { get; }
        public decimal  TotalAmountDue  { get; }

        public string StartDateDisplay => StartDate?.ToString("yyyy-MM-dd") ?? "—";
        public string EndDateDisplay   => EndDate?.ToString("yyyy-MM-dd") ?? "—";
        public string AmountDisplay    => TotalAmountDue.ToString("N2");

        /// <summary>True when the candidate set's EndDate is in the past.</summary>
        public bool   IsExpired  => EndDate.HasValue && EndDate.Value.Date < DateTime.Today;
        public string SetStatus  => IsExpired ? "Expired" : "Active";

        // ── Items (loaded after initial candidate list is built) ──────────────

        public List<SetItemSummaryDto> Items { get; set; } = new List<SetItemSummaryDto>();

        // ── Match metrics (computed once items are assigned) ──────────────────

        private int _matchCount;
        public int MatchCount
        {
            get => _matchCount;
            private set { SetField(ref _matchCount, value); OnPropertyChanged(nameof(MatchCountDisplay)); }
        }

        private int _expiringItemCount;
        public int ExpiringItemCount
        {
            get => _expiringItemCount;
            private set => SetField(ref _expiringItemCount, value);
        }

        private double _matchPercent;
        public double MatchPercent
        {
            get => _matchPercent;
            private set { SetField(ref _matchPercent, value); OnPropertyChanged(nameof(MatchPercentDisplay)); }
        }

        private string _matchStatus = "—";
        public string MatchStatus
        {
            get => _matchStatus;
            private set => SetField(ref _matchStatus, value);
        }

        public string MatchPercentDisplay => $"{MatchPercent:F0}%";
        public string MatchCountDisplay   => $"{MatchCount} / {ExpiringItemCount}";

        // ── Flat search blob (SetCode + items text for ICollectionView filter) ─

        public string AllSearchableText { get; private set; } = "";

        // ────────────────────────────────────────────────────────────────────

        public CandidateSetViewModel(InvoiceSetPickerDto dto)
        {
            SetId           = dto.SetId;
            SetCode         = dto.SetCode         ?? "";
            SetType         = dto.SetType         ?? "";
            DocumentNumber  = dto.DocumentNumber  ?? "";
            ReferenceNumber = dto.ReferenceNumber ?? "";
            CompanyName     = dto.CompanyName     ?? "";
            StartDate       = dto.StartDate;
            EndDate         = dto.EndDate;
            TotalAmountDue  = dto.TotalAmountDue;

            RebuildSearchText();
        }

        /// <summary>
        /// Computes MatchCount / MatchPercent / MatchStatus against the expiring set's items.
        /// Call after <see cref="Items"/> has been populated.
        /// </summary>
        public void ComputeMatch(IList<SetItemSummaryDto> expiringItems)
        {
            if (expiringItems == null || expiringItems.Count == 0)
            {
                ExpiringItemCount = 0;
                MatchCount        = 0;
                MatchPercent      = 0;
                MatchStatus       = "No Items";
                RebuildSearchText();
                return;
            }

            var expiringCodes  = new HashSet<string>(
                expiringItems.Select(x => (x.ItemCode ?? "").Trim()),
                StringComparer.OrdinalIgnoreCase);

            var candidateCodes = new HashSet<string>(
                Items.Select(x => (x.ItemCode ?? "").Trim()),
                StringComparer.OrdinalIgnoreCase);

            int matched = expiringCodes.Count(c => !string.IsNullOrEmpty(c) && candidateCodes.Contains(c));

            ExpiringItemCount = expiringItems.Count;
            MatchCount        = matched;
            MatchPercent      = expiringItems.Count > 0 ? (double)matched / expiringItems.Count * 100.0 : 0;
            MatchStatus       = MatchPercent >= 100.0 ? "Perfect"
                              : MatchPercent >= 50.0  ? "Partial"
                              : "Low";

            RebuildSearchText();
        }

        private void RebuildSearchText()
        {
            var itemParts = Items.Select(i => $"{i.ItemCode} {i.Description}");
            AllSearchableText = string.Join(" ", new[]
            {
                SetCode, SetType, DocumentNumber, ReferenceNumber, CompanyName,
                string.Join(" ", itemParts)
            }).ToLowerInvariant();
        }
    }
}
