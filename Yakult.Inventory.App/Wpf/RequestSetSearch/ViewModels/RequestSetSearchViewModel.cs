using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RequestSetSearch.ViewModels
{
    /// <summary>
    /// A single Request or Set result card. Wraps RequestSetSearchResultDto for display.
    /// </summary>
    public sealed class RequestSetSearchCardVm
    {
        public RequestSetSearchResultDto Dto { get; }

        public RequestSetSearchCardVm(RequestSetSearchResultDto dto) => Dto = dto;

        public string DisplayTitle => string.IsNullOrWhiteSpace(Dto.Title) ? $"#{Dto.Id}" : Dto.Title;
        public string DisplaySubtitle => Dto.Subtitle;
        public string StatusText => Dto.Status;
        public string TypeLabel => Dto.IsRequest ? "REQUEST" : "SET";

        /// <summary>Drives card accent color — "Request" (blue) or "Set" (orange), same idea as
        /// SearchView's "Destination" but with its own local converter (RequestSetTypeToBrushConverter)
        /// instead of reusing SearchConverters' Item-destination switch, which has no Request/Set case.</summary>
        public string TypeKey => Dto.IsRequest ? "Request" : "Set";
    }

    /// <summary>
    /// ViewModel for the Consumable Management Portal Card 2 home search — mirrors
    /// Wpf\Search\Views\SearchView's state machine (Initial / Loading / NoResults / Results) and
    /// visual card design, but searches Requests + Sets (via SearchRequestsAndSetsAsync) instead
    /// of Items, and has no split detail panel / pagination / filter popup since the result set
    /// here is much smaller and each card navigates directly on click.
    /// </summary>
    public sealed class RequestSetSearchViewModel : ViewModelBase
    {
        public RequestSetSearchViewModel()
        {
            SelectCardCommand = new RelayCommand<RequestSetSearchCardVm>(SelectCard);
        }

        private enum State { Initial, Loading, NoResults, Results }

        private State _state = State.Initial;
        private void SetState(State s)
        {
            _state = s;
            OnPropertyChanged(nameof(IsInitial));
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(IsNoResults));
            OnPropertyChanged(nameof(IsResults));
        }

        public bool IsInitial => _state == State.Initial;
        public bool IsLoading => _state == State.Loading;
        public bool IsNoResults => _state == State.NoResults;
        public bool IsResults => _state == State.Results;

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set => SetField(ref _searchText, value);
        }

        private string _noResultsMessage = string.Empty;
        public string NoResultsMessage
        {
            get => _noResultsMessage;
            private set => SetField(ref _noResultsMessage, value);
        }

        public ObservableCollection<RequestSetSearchCardVm> Cards { get; } = new ObservableCollection<RequestSetSearchCardVm>();

        /// <summary>Raised when a card is clicked — the host Form decides how to navigate
        /// (ViewSetDetailPage for Sets, ViewRequestsPage for Requests).</summary>
        public event Action<RequestSetSearchResultDto> CardSelected;

        public System.Windows.Input.ICommand SelectCardCommand { get; }

        private void SelectCard(RequestSetSearchCardVm card) => CardSelected?.Invoke(card?.Dto);

        private CancellationTokenSource _searchCts;

        public async void ExecuteSearch()
        {
            var query = SearchText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(query))
            {
                SetState(State.Initial);
                return;
            }

            _searchCts?.Cancel();
            var cts = new CancellationTokenSource();
            _searchCts = cts;

            SetState(State.Loading);

            System.Collections.Generic.List<RequestSetSearchResultDto> results;
            try
            {
                var repo = new SearchRepository();
                results = await repo.SearchRequestsAndSetsAsync(query, 50);
            }
            catch
            {
                if (cts.Token.IsCancellationRequested) return;
                NoResultsMessage = "Search failed. Please try again.";
                SetState(State.NoResults);
                return;
            }

            if (cts.Token.IsCancellationRequested) return;

            if (results.Count == 0)
            {
                NoResultsMessage = $"No requests or sets found for \"{query}\".";
                SetState(State.NoResults);
                return;
            }

            Cards.Clear();
            foreach (var r in results) Cards.Add(new RequestSetSearchCardVm(r));
            SetState(State.Results);
        }

        public void Reset()
        {
            SearchText = string.Empty;
            Cards.Clear();
            SetState(State.Initial);
        }
    }
}
